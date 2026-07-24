import { computed, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { signalStore, withState, withMethods, withComputed, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

export interface CartItem {
  id: string;
  productId: string;
  productName: string;
  imageUrl: string | null;
  unitPriceInCents: number;
  quantity: number;
  lineTotalInCents: number;
}

export interface CartDto {
  items: CartItem[];
  totalInCents: number;
}

interface CartState {
  items: CartItem[];
  totalInCents: number;
  isOpen: boolean;
  isLoading: boolean;
  error: string | null;
}

const initialState: CartState = {
  items: [],
  totalInCents: 0,
  isOpen: false,
  isLoading: false,
  error: null,
};

export const CartStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ items }) => ({
    itemCount: computed(() => items().reduce((sum, item) => sum + item.quantity, 0)),
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    const platformId = inject(PLATFORM_ID);

    // Shared across loadCart/addItem/updateItemQuantity/removeItem — all four mutate the SAME
    // items/totalInCents state, so a single counter correctly discards ANY stale, out-of-order
    // response regardless of which method issued it (review finding: two independent reviewers
    // confirmed this was reachable — e.g. a slow initial loadCart() resolving AFTER a user's
    // addItem() would silently revert the cart to its pre-add state). Same pattern as
    // ProductDetailStore's requestId guard.
    let requestId = 0;

    async function loadCart(): Promise<void> {
      const currentRequestId = ++requestId;
      patchState(store, { isLoading: true, error: null });
      try {
        const cart = await firstValueFrom(http.get<CartDto>(`${environment.apiUrl}/api/v1/cart`));
        if (currentRequestId !== requestId) return;
        patchState(store, { isLoading: false, items: cart.items, totalInCents: cart.totalInCents });
      } catch {
        if (currentRequestId !== requestId) return;
        patchState(store, { isLoading: false, error: 'Impossible de charger le panier.' });
      }
    }

    // Hydrate once at construction — no meaningful anonymous cart to render during SSR, so this
    // only actually fires in the browser (same reasoning as AuthStore's localStorage read).
    if (isPlatformBrowser(platformId)) {
      void loadCart();
    }

    return {
      loadCart,

      async addItem(productId: string, quantity: number): Promise<boolean> {
        const currentRequestId = ++requestId;
        patchState(store, { isLoading: true, error: null });
        try {
          const cart = await firstValueFrom(
            http.post<CartDto>(`${environment.apiUrl}/api/v1/cart/items`, { productId, quantity }),
          );
          if (currentRequestId !== requestId) return true;
          patchState(store, { isLoading: false, items: cart.items, totalInCents: cart.totalInCents });
          return true;
        } catch {
          if (currentRequestId !== requestId) return false;
          patchState(store, { isLoading: false, error: "Impossible d'ajouter cet article au panier." });
          return false;
        }
      },

      async updateItemQuantity(itemId: string, quantity: number): Promise<boolean> {
        const currentRequestId = ++requestId;
        patchState(store, { isLoading: true, error: null });
        try {
          const cart = await firstValueFrom(
            http.patch<CartDto>(`${environment.apiUrl}/api/v1/cart/items/${itemId}`, { quantity }),
          );
          if (currentRequestId !== requestId) return true;
          patchState(store, { isLoading: false, items: cart.items, totalInCents: cart.totalInCents });
          return true;
        } catch {
          if (currentRequestId !== requestId) return false;
          patchState(store, { isLoading: false, error: 'Impossible de mettre à jour cet article.' });
          return false;
        }
      },

      async removeItem(itemId: string): Promise<boolean> {
        const currentRequestId = ++requestId;
        patchState(store, { isLoading: true, error: null });
        try {
          const cart = await firstValueFrom(
            http.delete<CartDto>(`${environment.apiUrl}/api/v1/cart/items/${itemId}`),
          );
          if (currentRequestId !== requestId) return true;
          patchState(store, { isLoading: false, items: cart.items, totalInCents: cart.totalInCents });
          return true;
        } catch {
          if (currentRequestId !== requestId) return false;
          patchState(store, { isLoading: false, error: 'Impossible de supprimer cet article.' });
          return false;
        }
      },

      open(): void {
        patchState(store, { isOpen: true });
      },

      close(): void {
        patchState(store, { isOpen: false });
      },
    };
  }),
);
