import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'inscription',
    loadComponent: () =>
      import('./features/auth/pages/register/register.component').then((m) => m.RegisterComponent),
  },
];
