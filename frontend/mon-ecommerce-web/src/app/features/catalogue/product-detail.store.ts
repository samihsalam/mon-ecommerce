import { inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

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
}

const initialState: ProductDetailState = {
  product: null,
  isLoading: false,
  error: null,
};

export const ProductDetailStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);

    // A single monotonic counter is enough here (unlike CatalogueStore's shared search/browse
    // counter) — this store only ever has one fetch method, so "later request supersedes earlier
    // one" reduces to a plain sequence check.
    let requestId = 0;

    return {
      async loadProduct(id: string): Promise<void> {
        const currentRequestId = ++requestId;
        patchState(store, { isLoading: true, error: null, product: null });

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
    };
  }),
);
