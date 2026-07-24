import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpInterceptorFn } from '@angular/common/http';
import { tap } from 'rxjs';

import { CART_SESSION_ID_KEY } from '../constants/storage-keys';

const SESSION_HEADER = 'X-Cart-Session-Id';

// Attaches the client's anonymous cart session id to every outgoing request — required on
// POST /auth/login specifically, since Auth.Login's merge-on-login (Story 4.1) reads it from
// THAT request.
//
// The id is generated CLIENT-SIDE, eagerly, the first time this interceptor runs with none yet
// in localStorage — not left to the server to mint one (review finding: relying on the server's
// generated id, read back off the response, meant two cart-touching requests fired before either
// resolved — e.g. CartStore's auto GET /cart at app construction racing a user's addItem() POST —
// could each arrive header-less and get handed TWO DIFFERENT server-generated sessions, silently
// orphaning whichever cart's response lost the race to update localStorage last). Generating and
// persisting the id synchronously here, before the request is even dispatched, closes that race
// entirely: JS has no true concurrency, so the first of any two synchronously-triggered requests
// to reach this interceptor generates and writes the id, and the second reads back what the first
// just wrote — every request from then on carries the SAME id, regardless of response ordering.
//
// The backend (Carts.ResolveOwner) still mints its own id as a fallback when a request arrives
// with no header at all — kept as defense in depth, not the primary mechanism anymore.
//
// Runs during SSR too (server.ts renders on the server), where localStorage doesn't exist —
// guard with isPlatformBrowser, same pattern as authInterceptor.
export const cartSessionInterceptor: HttpInterceptorFn = (req, next) => {
  const platformId = inject(PLATFORM_ID);

  if (!isPlatformBrowser(platformId)) {
    return next(req);
  }

  let sessionId = localStorage.getItem(CART_SESSION_ID_KEY);
  if (!sessionId) {
    sessionId = crypto.randomUUID();
    localStorage.setItem(CART_SESSION_ID_KEY, sessionId);
  }

  const outgoingReq = req.clone({ setHeaders: { [SESSION_HEADER]: sessionId } });

  return next(outgoingReq).pipe(
    tap((event) => {
      if (!('headers' in event)) {
        return;
      }
      const generatedSessionId = event.headers.get(SESSION_HEADER);
      if (generatedSessionId && generatedSessionId !== sessionId) {
        localStorage.setItem(CART_SESSION_ID_KEY, generatedSessionId);
      }
    }),
  );
};
