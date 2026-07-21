import { inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import type { ProductSummary } from './catalogue.store';

export interface ProductDetail {
  id: string;
  name: string;
  description: string;
  priceInCents: number;
  material: string | null;
  color: string | null;
  dimensions: string | null;
  stockQuantity: number;
  inStock: boolean;
  categoryId: string;
  categoryName: string;
  categorySlug: string;
  imageUrls: string[];
}

interface ProductDetailState {
  product: ProductDetail | null;
  isLoading: boolean;
  error: string | null;
  similarProducts: ProductSummary[];
}

const initialState: ProductDetailState = {
  product: null,
  isLoading: false,
  error: null,
  similarProducts: [],
};

export const ProductDetailStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);

    // Two independent counters, one per HTTP call this store makes — loadProduct and
    // loadSimilarProducts are always triggered together on navigation but resolve independently,
    // so each needs its own staleness guard (see similarRequestId below; it was originally
    // reasoned to be unnecessary, but two independent reviews confirmed a real, reachable race:
    // navigating A -> B before A's /similar response lands can overwrite B's similarProducts with
    // A's stale list, and nothing about that self-corrects afterward).
    let requestId = 0;
    let similarRequestId = 0;

    return {
      async loadProduct(id: string): Promise<void> {
        const currentRequestId = ++requestId;
        patchState(store, { isLoading: true, error: null, product: null, similarProducts: [] });

        try {
          const product = await firstValueFrom(
            http.get<ProductDetail>(`${environment.apiUrl}/api/v1/products/${id}`),
          );
          if (currentRequestId !== requestId) {
            return;
          }
          patchState(store, { isLoading: false, product });
        } catch {
          if (currentRequestId !== requestId) {
            return;
          }
          patchState(store, {
            isLoading: false,
            error: "Ce produit est introuvable ou n'est plus disponible.",
          });
        }
      },

      async loadSimilarProducts(id: string): Promise<void> {
        const currentSimilarRequestId = ++similarRequestId;

        try {
          const similarProducts = await firstValueFrom(
            http.get<ProductSummary[]>(`${environment.apiUrl}/api/v1/products/${id}/similar`),
          );
          if (currentSimilarRequestId !== similarRequestId) {
            return;
          }
          patchState(store, { similarProducts });
        } catch {
          if (currentSimilarRequestId !== similarRequestId) {
            return;
          }
          // A failed "similar products" fetch shouldn't block or error out the rest of an
          // otherwise-successful product page — same reasoning as CatalogueStore.loadCategories().
          patchState(store, { similarProducts: [] });
        }
      },
    };
  }),
);
