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
    path: 'catalogue/:categorySlug/:productSlug',
    loadComponent: () =>
      import('./features/catalogue/pages/product-detail/product-detail.component').then(
        (m) => m.ProductDetailComponent,
      ),
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
  {
    path: 'compte/commandes/:orderId/retour',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/pages/return-request/return-request.component').then(
        (m) => m.ReturnRequestComponent,
      ),
  },
  {
    path: 'checkout/adresse',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/checkout/pages/checkout-address/checkout-address.component').then(
        (m) => m.CheckoutAddressComponent,
      ),
  },
  {
    path: 'checkout/livraison',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/checkout/pages/checkout-shipping/checkout-shipping.component').then(
        (m) => m.CheckoutShippingComponent,
      ),
  },
  {
    path: 'checkout/paiement',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/checkout/pages/checkout-payment/checkout-payment.component').then(
        (m) => m.CheckoutPaymentComponent,
      ),
  },
  {
    path: 'checkout/confirmation',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/checkout/pages/checkout-confirmation/checkout-confirmation.component').then(
        (m) => m.CheckoutConfirmationComponent,
      ),
  },
  // Story 8.1: public legal pages, no authGuard (AC #1 — accessible without login).
  {
    path: 'cgv',
    loadComponent: () => import('./features/legal/pages/cgv/cgv.component').then((m) => m.CgvComponent),
  },
  {
    path: 'confidentialite',
    loadComponent: () =>
      import('./features/legal/pages/privacy-policy/privacy-policy.component').then((m) => m.PrivacyPolicyComponent),
  },
  {
    path: 'retours',
    loadComponent: () =>
      import('./features/legal/pages/returns-policy/returns-policy.component').then((m) => m.ReturnsPolicyComponent),
  },
];
