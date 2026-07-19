import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthStore } from '../../features/auth/auth.store';

// AuthStore.isAuthenticated is set synchronously at store construction (reads localStorage
// directly, before first render — see auth.store.ts), so there's no async race to guard
// against here, unlike Flutter's cold-start check.
export const authGuard: CanActivateFn = (route, state) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/connexion'], { queryParams: { returnUrl: state.url } });
};
