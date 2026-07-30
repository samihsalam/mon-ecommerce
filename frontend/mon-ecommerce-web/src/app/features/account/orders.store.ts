import { inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

interface OrderSummary {
  id: string;
  orderNumber: string;
  date: string;
  totalInCents: number;
  status: string;
}

interface OrderItem {
  productName: string;
  unitPriceInCents: number;
  quantity: number;
}

interface Address {
  id: string;
  street: string;
  city: string;
  postalCode: string;
  country: string;
}

// Story 5.1 — dropdown values must match the backend's ReturnReason enum exactly (by string
// name, sent as a form field the [FromForm] ReturnReason binder parses by enum member name).
export type ReturnReason = 'WrongSize' | 'DefectiveProduct' | 'NotAsDescribed' | 'ChangedMind' | 'Other';

export const RETURN_REASON_LABELS: Record<ReturnReason, string> = {
  WrongSize: 'Mauvaise taille',
  DefectiveProduct: 'Produit défectueux',
  NotAsDescribed: 'Non conforme à la description',
  ChangedMind: "Changement d'avis",
  Other: 'Autre',
};

export interface ReturnSummary {
  id: string;
  status: string;
  reason: string;
  created: string;
}

interface OrderDetail extends OrderSummary {
  trackingNumber: string | null;
  shippingAddress: Address;
  items: OrderItem[];
  return: ReturnSummary | null;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface OrdersState {
  orders: OrderSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  selectedOrder: OrderDetail | null;
  isLoading: boolean;
  error: string | null;
  // Distinct from `error` above: set only on a failed requestReturn() call, so the return-request
  // form can show it inline without clobbering (or being clobbered by) the order-detail page's
  // own loading error state.
  returnError: string | null;
}

const initialState: OrdersState = {
  orders: [],
  totalCount: 0,
  page: 1,
  pageSize: 10,
  selectedOrder: null,
  isLoading: false,
  error: null,
  returnError: null,
};

export const OrdersStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);

    return {
      async loadOrders(page = 1): Promise<void> {
        patchState(store, { isLoading: true, error: null });

        try {
          const result = await firstValueFrom(
            http.get<PagedResult<OrderSummary>>(`${environment.apiUrl}/api/v1/account/orders`, {
              params: { page, pageSize: store.pageSize() },
            }),
          );
          patchState(store, {
            isLoading: false,
            orders: result.items,
            totalCount: result.totalCount,
            page: result.page,
          });
        } catch {
          patchState(store, {
            isLoading: false,
            error: 'Impossible de charger vos commandes. Veuillez réessayer.',
          });
        }
      },

      async loadOrderDetail(orderId: string): Promise<void> {
        patchState(store, { isLoading: true, error: null, selectedOrder: null });

        try {
          const order = await firstValueFrom(
            http.get<OrderDetail>(`${environment.apiUrl}/api/v1/account/orders/${orderId}`),
          );
          patchState(store, { isLoading: false, selectedOrder: order });
        } catch {
          patchState(store, {
            isLoading: false,
            error: 'Impossible de charger cette commande. Veuillez réessayer.',
          });
        }
      },

      // multipart/form-data — the backend's [FromForm] binding expects reason/description as
      // form fields and photos as files, not a JSON body (Story 5.1, AC #4's photo upload).
      // Returns true/false rather than throwing, so the calling component can decide what to do
      // next (navigate away on success, stay and show returnError() on failure) without a
      // try/catch of its own — same convention as CheckoutStore.createPaymentIntent.
      async requestReturn(
        orderId: string,
        reason: ReturnReason,
        description: string,
        photos: File[],
      ): Promise<boolean> {
        patchState(store, { returnError: null });

        const formData = new FormData();
        formData.append('reason', reason);
        formData.append('description', description);
        photos.forEach((photo) => formData.append('photos', photo, photo.name));

        try {
          await firstValueFrom(
            http.post(`${environment.apiUrl}/api/v1/account/orders/${orderId}/returns`, formData),
          );
          return true;
        } catch (err) {
          const message =
            err instanceof HttpErrorResponse && err.status === 422 && err.error?.detail
              ? (err.error.detail as string)
              : 'Impossible de créer la demande de retour. Veuillez réessayer.';
          patchState(store, { returnError: message });
          return false;
        }
      },
    };
  }),
);
