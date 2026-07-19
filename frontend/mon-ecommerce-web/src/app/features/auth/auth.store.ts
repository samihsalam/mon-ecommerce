import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ACCESS_TOKEN_KEY, REFRESH_TOKEN_KEY } from '../../core/constants/storage-keys';

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

interface AuthState {
  isLoading: boolean;
  error: string | null;
  isAuthenticated: boolean;
}

const initialState: AuthState = {
  isLoading: false,
  error: null,
  isAuthenticated: false,
};

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);
    const platformId = inject(PLATFORM_ID);

    // A page refresh while logged in shouldn't flash "logged out" before the store settles.
    if (isPlatformBrowser(platformId) && localStorage.getItem(ACCESS_TOKEN_KEY)) {
      patchState(store, { isAuthenticated: true });
    }

    // Dedupe concurrent refresh attempts: if several requests 401 at once, only the first
    // should call /auth/refresh — the backend rotates (revokes the old token), so a second
    // concurrent call using the now-revoked token would fail even though the session is fine.
    let refreshInFlight: Promise<boolean> | null = null;

    return {
      async register(name: string, email: string, password: string): Promise<boolean> {
        patchState(store, { isLoading: true, error: null });

        try {
          const response = await firstValueFrom(
            http.post<AuthResponse>(`${environment.apiUrl}/api/v1/auth/register`, { name, email, password }),
          );

          if (isPlatformBrowser(platformId)) {
            localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
            localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
          }

          patchState(store, { isLoading: false, isAuthenticated: true });
          return true;
        } catch (err) {
          const message =
            err instanceof HttpErrorResponse && err.status === 409
              ? 'Un compte existe déjà avec cet email.'
              : "Une erreur est survenue lors de l'inscription. Veuillez réessayer.";

          patchState(store, { isLoading: false, error: message });
          return false;
        }
      },

      async login(email: string, password: string): Promise<boolean> {
        patchState(store, { isLoading: true, error: null });

        try {
          const response = await firstValueFrom(
            http.post<AuthResponse>(`${environment.apiUrl}/api/v1/auth/login`, { email, password }),
          );

          if (isPlatformBrowser(platformId)) {
            localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
            localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
          }

          patchState(store, { isLoading: false, isAuthenticated: true });
          return true;
        } catch (err) {
          const message =
            err instanceof HttpErrorResponse && err.status === 401
              ? 'Email ou mot de passe incorrect.'
              : 'Une erreur est survenue lors de la connexion. Veuillez réessayer.';

          patchState(store, { isLoading: false, error: message });
          return false;
        }
      },

      async logout(): Promise<void> {
        if (isPlatformBrowser(platformId)) {
          const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
          if (refreshToken) {
            try {
              await firstValueFrom(http.post(`${environment.apiUrl}/api/v1/auth/logout`, { refreshToken }));
            } catch {
              // Best-effort: logging out locally must always succeed even if revoking
              // the token server-side fails (e.g. the API is temporarily unreachable).
            }
          }
          localStorage.removeItem(ACCESS_TOKEN_KEY);
          localStorage.removeItem(REFRESH_TOKEN_KEY);
        }

        patchState(store, { isAuthenticated: false });
      },

      async refresh(): Promise<boolean> {
        if (refreshInFlight) {
          return refreshInFlight;
        }

        refreshInFlight = (async () => {
          try {
            if (!isPlatformBrowser(platformId)) return false;

            const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
            if (!refreshToken) return false;

            const response = await firstValueFrom(
              http.post<AuthResponse>(`${environment.apiUrl}/api/v1/auth/refresh`, { refreshToken }),
            );

            localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
            localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
            patchState(store, { isAuthenticated: true });
            return true;
          } catch {
            localStorage.removeItem(ACCESS_TOKEN_KEY);
            localStorage.removeItem(REFRESH_TOKEN_KEY);
            patchState(store, { isAuthenticated: false });
            return false;
          } finally {
            refreshInFlight = null;
          }
        })();

        return refreshInFlight;
      },
    };
  }),
);
