import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';

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
  {
    path: 'catalogue',
    loadComponent: () =>
      import('./features/catalogue/pages/catalogue/catalogue.component').then((m) => m.CatalogueComponent),
  },
  {
    path: 'recherche',
    loadComponent: () =>
      import('./features/catalogue/pages/search-results/search-results.component').then(
        (m) => m.SearchResultsComponent,
      ),
  },
  {
    path: 'compte',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/pages/profile/profile.component').then((m) => m.ProfileComponent),
  },
  {
    path: 'compte/commandes',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/pages/orders/orders.component').then((m) => m.OrdersComponent),
  },
  {
    path: 'compte/commandes/:orderId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/pages/order-detail/order-detail.component').then((m) => m.OrderDetailComponent),
  },
];
