import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpInterceptorFn } from '@angular/common/http';

import { ACCESS_TOKEN_KEY } from '../constants/storage-keys';

// Attaches the stored access token to outgoing requests. 401-refresh-and-retry
// is explicitly Story 2.2's scope, not this one's.
//
// This interceptor also runs during SSR (server.ts renders on the server),
// where `localStorage` doesn't exist — guard with isPlatformBrowser.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const platformId = inject(PLATFORM_ID);

  if (!isPlatformBrowser(platformId)) {
    return next(req);
  }

  const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);

  if (!accessToken) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${accessToken}` },
    }),
  );
};
