import { inject } from '@angular/core';
import { CanActivateFn, RedirectFunction, Router } from '@angular/router';
import { AuthSession } from './auth-session';

export const entryRoute: RedirectFunction = () =>
  inject(AuthSession).signedIn() ? '/pulpit' : '/rejestracja';

export const guestsOnly: CanActivateFn = () =>
  inject(AuthSession).signedIn() ? inject(Router).createUrlTree(['/pulpit']) : true;

export const signedInOnly: CanActivateFn = () =>
  inject(AuthSession).signedIn() ? true : inject(Router).createUrlTree(['/logowanie']);
