import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, map, Observable, of, shareReplay, tap, throwError } from 'rxjs';
import { windowsTimeZones } from '../shared/windows-time-zones';

export interface UserProfile {
  id: string;
  name: string;
  surname: string;
  email: string;
  dateOfBirth: string | null;
  timeZone: string;
  createdAtUtc: string;
}

interface Credentials {
  email: string;
  password: string;
}

interface Tokens {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken: string;
}

export interface Authorization {
  tokens: Tokens;
  generation: number;
}

const storageKey = 'heartrate.auth';

function restoreAuthorization(): Authorization | null {
  try {
    const tokens = JSON.parse(sessionStorage.getItem(storageKey) ?? 'null');
    if (
      tokens &&
      typeof tokens.accessToken === 'string' &&
      typeof tokens.expiresAtUtc === 'string' &&
      typeof tokens.refreshToken === 'string'
    ) {
      return { tokens, generation: 0 };
    }
  } catch {
    // Storage may be disabled or contain an invalid value.
  }
  return null;
}

@Injectable({ providedIn: 'root' })
export class AuthSession {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly current = signal(restoreAuthorization());
  private readonly user = signal<UserProfile | null>(null);
  private generation = 0;
  private refreshing: { authorization: Authorization; result: Observable<Authorization> } | null =
    null;

  readonly authorization = this.current.asReadonly();
  readonly signedIn = computed(() => this.current() !== null);
  readonly profile = this.user.asReadonly();
  readonly timeZone = computed(() => {
    const id = this.user()?.timeZone ?? 'Europe/Warsaw';
    const timeZone = windowsTimeZones.get(id.toLowerCase()) ?? id;
    try {
      return new Intl.DateTimeFormat('pl-PL', { timeZone }).resolvedOptions().timeZone;
    } catch {
      return 'Europe/Warsaw';
    }
  });

  register(request: Credentials & { name: string }): Observable<UserProfile> {
    return this.http.post<UserProfile>('/api/auth/register', request);
  }

  login(request: Credentials): Observable<Tokens> {
    return this.http.post<Tokens>('/api/auth/login', request).pipe(
      tap((tokens) => {
        this.user.set(null);
        this.save({ tokens, generation: ++this.generation });
      }),
    );
  }

  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>('/api/users/me').pipe(tap((user) => this.user.set(user)));
  }

  updateProfile(name: string, surname: string): Observable<UserProfile> {
    return this.http
      .patch<UserProfile>('/api/users/me', { name, surname })
      .pipe(tap((user) => this.user.set(user)));
  }

  logout(): Observable<void> {
    const active = this.current();
    return this.http.post<void>('/api/auth/logout', null, { timeout: 8000 }).pipe(
      catchError(() => of(undefined)),
      tap(() => {
        if (active && this.current()?.generation === active.generation) this.endSession();
      }),
    );
  }

  refresh(used: Authorization): Observable<Authorization> {
    const active = this.current();
    if (!active || active.generation !== used.generation) {
      return throwError(() => new HttpErrorResponse({ status: 401 }));
    }
    if (active !== used) return of(active);
    if (this.refreshing?.authorization === active) return this.refreshing.result;

    const result = this.http
      .post<Tokens>(
        '/api/auth/refresh',
        { refreshToken: active.tokens.refreshToken },
        { timeout: 8000 },
      )
      .pipe(
        map((tokens) => {
          if (this.current() !== active) throw new HttpErrorResponse({ status: 401 });
          const refreshed = { tokens, generation: active.generation };
          this.save(refreshed);
          return refreshed;
        }),
        catchError((error: HttpErrorResponse) => {
          if (error.status === 401) this.expire(active);
          return throwError(() => error);
        }),
        finalize(() => {
          if (this.refreshing?.authorization === active) this.refreshing = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    this.refreshing = { authorization: active, result };
    return result;
  }

  expire(used: Authorization): void {
    if (this.current() === used) this.endSession();
  }

  private endSession(): void {
    this.save(null);
    this.user.set(null);
    void this.router.navigate(['/logowanie'], { replaceUrl: true });
  }

  private save(authorization: Authorization | null): void {
    this.current.set(authorization);
    try {
      if (authorization) sessionStorage.setItem(storageKey, JSON.stringify(authorization.tokens));
      else sessionStorage.removeItem(storageKey);
    } catch {
      // In-memory authentication remains available when storage is disabled.
    }
  }
}
