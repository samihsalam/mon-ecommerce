import { inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ToastService } from '../../core/services/toast.service';

interface Address {
  id: string;
  street: string;
  city: string;
  postalCode: string;
  country: string;
}

interface Profile {
  name: string;
  email: string;
  addresses: Address[];
}

interface AccountState {
  profile: Profile | null;
  isLoading: boolean;
  error: string | null;
  deletionRequested: boolean;
}

const initialState: AccountState = {
  profile: null,
  isLoading: false,
  error: null,
  deletionRequested: false,
};

export const AccountStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);
    const toastService = inject(ToastService);

    return {
      async loadProfile(): Promise<void> {
        patchState(store, { isLoading: true, error: null });

        try {
          const profile = await firstValueFrom(http.get<Profile>(`${environment.apiUrl}/api/v1/account/profile`));
          patchState(store, { isLoading: false, profile });
        } catch {
          patchState(store, {
            isLoading: false,
            error: 'Impossible de charger votre profil. Veuillez réessayer.',
          });
        }
      },

      async updateProfile(name: string, email: string, currentPassword: string | null): Promise<boolean> {
        patchState(store, { isLoading: true, error: null });

        try {
          const profile = await firstValueFrom(
            http.patch<Profile>(`${environment.apiUrl}/api/v1/account/profile`, { name, email, currentPassword }),
          );
          patchState(store, { isLoading: false, profile });
          toastService.show('Profil mis à jour');
          return true;
        } catch (err) {
          const message =
            err instanceof HttpErrorResponse && err.status === 400 && err.error?.errors?.length
              ? err.error.errors[0]
              : 'Une erreur est survenue. Veuillez réessayer.';

          patchState(store, { isLoading: false, error: message });
          return false;
        }
      },

      // Story 8.3, AC #1: no confirmation is destructive here — the account stays fully usable
      // until an admin processes the request (up to 30 days later), so there's nothing to roll
      // back locally on success beyond flipping deletionRequested for the UI.
      async requestAccountDeletion(): Promise<boolean> {
        patchState(store, { isLoading: true, error: null });

        try {
          await firstValueFrom(http.post(`${environment.apiUrl}/api/v1/account/delete-request`, {}));
          patchState(store, { isLoading: false, deletionRequested: true });
          return true;
        } catch (err) {
          const message =
            err instanceof HttpErrorResponse && err.status === 409
              ? 'Une demande de suppression est déjà en cours pour ce compte.'
              : 'Une erreur est survenue. Veuillez réessayer.';

          patchState(store, { isLoading: false, error: message });
          return false;
        }
      },
    };
  }),
);
