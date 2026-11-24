import { Routes } from '@angular/router';
import { entryRoute, guestsOnly, signedInOnly } from './services/auth-guards';

const authPage = () => import('./features/auth/auth-page').then((module) => module.AuthPage);

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: entryRoute },
  {
    path: 'rejestracja',
    title: 'Rejestracja | HeartRate',
    canActivate: [guestsOnly],
    data: { register: true },
    loadComponent: authPage,
  },
  {
    path: 'logowanie',
    title: 'Logowanie | HeartRate',
    canActivate: [guestsOnly],
    loadComponent: authPage,
  },
  {
    path: '',
    canActivate: [signedInOnly],
    loadComponent: () => import('./features/shell/app-shell').then((module) => module.AppShell),
    children: [
      {
        path: 'pulpit',
        title: 'Pulpit | HeartRate',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page').then((module) => module.DashboardPage),
      },
      {
        path: 'kalendarz',
        title: 'Kalendarz | HeartRate',
        loadComponent: () =>
          import('./features/calendar/calendar-page').then((module) => module.CalendarPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
