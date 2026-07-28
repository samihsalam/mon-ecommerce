import { inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

export interface OrderItem {
  productName: string;
  unitPriceInCents: number;
  quantity: number;
}

export interface OrderDetail {
  id: string;
  orderNumber: string;
  date: string;
  totalInCents: number;
  status: string;
  trackingNumber: string | null;
  shippingAddress: { id: string; street: string; city: string; postalCode: string; country: string };
  items: OrderItem[];
}

// Discriminated result for GetOrderByPaymentIntent polling (Story 4.6) — "pending" (still
// processing, the webhook hasn't landed yet) and "refunded" (stock ran out) are both expected,
// valid outcomes for the confirmation page's poll loop, not error states to throw on.
export type OrderConfirmationResult =
  | { status: 'found'; order: OrderDetail }
  | { status: 'pending' }
  | { status: 'refunded' };

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

      // address (Story 4.6): the backend now persists it and carries it through Stripe metadata
      // so the asynchronous webhook — which has no access to this client-side store — can
      // reconstruct the order later. See CreatePaymentIntentCommandHandler's Dev Notes.
      async createPaymentIntent(shippingOptionId: string, address: CheckoutAddress): Promise<string | null> {
        try {
          const response = await firstValueFrom(
            http.post<{ clientSecret: string }>(`${environment.apiUrl}/api/v1/payments/create-intent`, {
              shippingOptionId,
              street: address.street,
              city: address.city,
              postalCode: address.postalCode,
              country: address.country,
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

      // 404 ("pending") and 409 ("refunded") are both expected outcomes the confirmation page's
      // poll loop needs to distinguish (Story 4.6) — neither is treated as a failed HTTP call.
      async getOrderByPaymentIntent(paymentIntentId: string): Promise<OrderConfirmationResult> {
        try {
          const order = await firstValueFrom(
            http.get<OrderDetail>(`${environment.apiUrl}/api/v1/account/orders/by-payment-intent/${paymentIntentId}`),
          );
          return { status: 'found', order };
        } catch (err) {
          if (err instanceof HttpErrorResponse && err.status === 409) {
            return { status: 'refunded' };
          }
          return { status: 'pending' };
        }
      },
    };
  }),
);
