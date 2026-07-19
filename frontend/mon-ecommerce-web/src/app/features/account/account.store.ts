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
}

const initialState: AccountState = {
  profile: null,
  isLoading: false,
  error: null,
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
    };
  }),
);
