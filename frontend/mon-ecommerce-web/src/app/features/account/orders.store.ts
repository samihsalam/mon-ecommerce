import { inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
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

interface OrderDetail extends OrderSummary {
  trackingNumber: string | null;
  shippingAddress: Address;
  items: OrderItem[];
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
}

const initialState: OrdersState = {
  orders: [],
  totalCount: 0,
  page: 1,
  pageSize: 10,
  selectedOrder: null,
  isLoading: false,
  error: null,
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
    };
  }),
);
