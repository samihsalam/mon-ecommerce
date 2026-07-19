import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { ACCESS_TOKEN_KEY } from '../constants/storage-keys';
import { AuthStore } from '../../features/auth/auth.store';

// Requests to these endpoints must never trigger the refresh-and-retry logic below — a 401
// from /auth/refresh itself means the session truly can't be recovered, and retrying it
// would recurse. /auth/logout is excluded too: AuthStore.logout() already treats any
// failure there as best-effort and clears local state regardless, so there's no need to
// refresh-and-retry a logout call — doing so would just flip isAuthenticated true again
// right before logout flips it back false, and could even redirect to /connexion mid-logout
// if the refresh itself then failed.
const AUTH_ENDPOINTS = ['/api/v1/auth/login', '/api/v1/auth/register', '/api/v1/auth/refresh', '/api/v1/auth/logout'];

// Attaches the stored access token to outgoing requests, and transparently refreshes
// and retries on a 401. 401-refresh-and-retry is Story 2.2's scope; token-attach alone
// was Story 2.1's.
//
// This interceptor also runs during SSR (server.ts renders on the server),
// where `localStorage` doesn't exist — guard with isPlatformBrowser.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const platformId = inject(PLATFORM_ID);

  if (!isPlatformBrowser(platformId)) {
    return next(req);
  }

  const authStore = inject(AuthStore);
  const router = inject(Router);

  const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
  const authedReq = accessToken ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } }) : req;

  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => req.url.includes(path));

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      return from(authStore.refresh()).pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            router.navigate(['/connexion'], { queryParams: { returnUrl: router.url } });
            return throwError(() => error);
          }

          const newAccessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
          const retriedReq = newAccessToken
            ? req.clone({ setHeaders: { Authorization: `Bearer ${newAccessToken}` } })
            : req;
          return next(retriedReq);
        }),
      );
    }),
  );
};
