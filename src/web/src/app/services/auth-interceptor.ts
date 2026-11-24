import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, of, switchMap, throwError } from 'rxjs';
import { Authorization, AuthSession } from './auth-session';

const publicPaths = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || publicPaths.includes(request.url)) return next(request);

  const auth = inject(AuthSession);
  const active = auth.authorization();
  if (!active) return next(request);

  const send = ({ tokens }: Authorization) =>
    next(request.clone({ setHeaders: { Authorization: `Bearer ${tokens.accessToken}` } }));
  const expired = Date.parse(active.tokens.expiresAtUtc) <= Date.now();

  return (expired ? auth.refresh(active) : of(active)).pipe(
    switchMap((authorization) =>
      send(authorization).pipe(
        catchError((error: HttpErrorResponse) => {
          if (error.status !== 401) return throwError(() => error);
          if (expired) {
            auth.expire(authorization);
            return throwError(() => error);
          }

          return auth.refresh(authorization).pipe(
            switchMap((refreshed) =>
              send(refreshed).pipe(
                catchError((retryError: HttpErrorResponse) => {
                  if (retryError.status === 401) auth.expire(refreshed);
                  return throwError(() => retryError);
                }),
              ),
            ),
          );
        }),
      ),
    ),
  );
};
