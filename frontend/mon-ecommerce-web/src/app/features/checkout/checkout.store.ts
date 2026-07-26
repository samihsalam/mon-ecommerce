import { inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

export interface CheckoutAddress {
  street: string;
  city: string;
  postalCode: string;
  country: string;
}

export interface ShippingOption {
  id: string;
  name: string;
  priceInCents: number;
  estimatedDelay: string;
}

interface CheckoutState {
  address: CheckoutAddress | null;
  shippingOptions: ShippingOption[];
  shippingOption: ShippingOption | null;
  shippingOptionsError: string | null;
  paymentError: string | null;
}

const initialState: CheckoutState = {
  address: null,
  shippingOptions: [],
  shippingOption: null,
  shippingOptionsError: null,
  paymentError: null,
};

// Root-provided, holds the checkout wizard's in-progress data across steps (address, shipping,
// payment). Being root-provided means it naturally survives Angular Router navigation between
// checkout steps, satisfying "no data loss on back navigation" (Story 4.3 AC #6, Story 4.4 AC #5)
// without any extra persistence — deliberately not backed by sessionStorage/localStorage, since
// surviving a hard page refresh was never part of either AC.
export const CheckoutStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);

    return {
      setAddress(address: CheckoutAddress): void {
        patchState(store, { address });
      },

      async loadShippingOptions(): Promise<void> {
        try {
          const shippingOptions = await firstValueFrom(
            http.get<ShippingOption[]>(`${environment.apiUrl}/api/v1/shipping-options`),
          );
          patchState(store, { shippingOptions, shippingOptionsError: null });
        } catch {
          patchState(store, {
            shippingOptionsError: 'Impossible de charger les options de livraison. Veuillez réessayer.',
          });
        }
      },

      setShippingOption(shippingOption: ShippingOption): void {
        patchState(store, { shippingOption });
      },

      async createPaymentIntent(shippingOptionId: string): Promise<string | null> {
        try {
          const response = await firstValueFrom(
            http.post<{ clientSecret: string }>(`${environment.apiUrl}/api/v1/payments/create-intent`, {
              shippingOptionId,
            }),
          );
          patchState(store, { paymentError: null });
          return response.clientSecret;
        } catch {
          patchState(store, {
            paymentError: 'Impossible de préparer le paiement. Veuillez réessayer.',
          });
          return null;
        }
      },
    };
  }),
);
