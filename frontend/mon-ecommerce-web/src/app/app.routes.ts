import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
  },
  {
    path: 'inscription',
    loadComponent: () =>
      import('./features/auth/pages/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'connexion',
    loadComponent: () => import('./features/auth/pages/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'mot-de-passe-oublie',
    loadComponent: () =>
      import('./features/auth/pages/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent,
      ),
  },
  {
    path: 'reinitialiser-mot-de-passe',
    loadComponent: () =>
      import('./features/auth/pages/reset-password/reset-password.component').then((m) => m.ResetPasswordComponent),
  },
];
