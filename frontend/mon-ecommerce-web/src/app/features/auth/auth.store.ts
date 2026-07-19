import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

interface AuthState {
  isLoading: boolean;
  error: string | null;
}

const initialState: AuthState = {
  isLoading: false,
  error: null,
};

const ACCESS_TOKEN_KEY = 'accessToken';
const REFRESH_TOKEN_KEY = 'refreshToken';

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);
    const platformId = inject(PLATFORM_ID);

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

          patchState(store, { isLoading: false });
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
    };
  }),
);
