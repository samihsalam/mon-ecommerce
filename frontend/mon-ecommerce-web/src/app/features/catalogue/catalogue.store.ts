import { inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

export interface ProductSummary {
  id: string;
  name: string;
  priceInCents: number;
  material: string | null;
  color: string | null;
  imageUrl: string | null;
  categoryId: string;
  categoryName: string;
  inStock: boolean;
}

export interface CategorySummary {
  id: string;
  name: string;
  slug: string;
}

interface SearchSuggestions {
  categories: string[];
  products: string[];
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

interface CatalogueState {
  results: ProductSummary[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  isSearching: boolean;
  searchError: string | null;
  suggestions: SearchSuggestions;
  isLoadingSuggestions: boolean;
  categories: CategorySummary[];
}

const initialState: CatalogueState = {
  results: [],
  totalCount: 0,
  pageNumber: 1,
  pageSize: 20,
  totalPages: 0,
  isSearching: false,
  searchError: null,
  suggestions: { categories: [], products: [] },
  isLoadingSuggestions: false,
  categories: [],
};

export const CatalogueStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const http = inject(HttpClient);

    // Monotonic request counters — not request cancellation, just staleness detection. Without
    // this, an earlier search/suggestions call that resolves AFTER a later one (out-of-order
    // network response) would unconditionally overwrite the store with stale data, since neither
    // HttpClient call is aborted when superseded. Each call captures its own id before awaiting;
    // if the counter has moved on by the time the response arrives, the result is discarded.
    let searchRequestId = 0;
    let suggestionsRequestId = 0;

    return {
      async search(term: string, pageNumber = 1): Promise<void> {
        const requestId = ++searchRequestId;
        patchState(store, { isSearching: true, searchError: null });

        try {
          const result = await firstValueFrom(
            http.get<PagedResult<ProductSummary>>(`${environment.apiUrl}/api/v1/products`, {
              params: { search: term, pageNumber, pageSize: store.pageSize() },
            }),
          );
          if (requestId !== searchRequestId) {
            return;
          }
          patchState(store, {
            isSearching: false,
            results: result.items,
            totalCount: result.totalCount,
            pageNumber: result.pageNumber,
            totalPages: result.totalPages,
          });
        } catch {
          if (requestId !== searchRequestId) {
            return;
          }
          patchState(store, {
            isSearching: false,
            results: [],
            searchError: 'Impossible de charger les résultats. Veuillez réessayer.',
          });
        }
      },

      async loadSuggestions(term: string): Promise<void> {
        const requestId = ++suggestionsRequestId;
        patchState(store, { isLoadingSuggestions: true });

        try {
          const result = await firstValueFrom(
            http.get<SearchSuggestions>(`${environment.apiUrl}/api/v1/products/suggestions`, {
              params: { search: term },
            }),
          );
          if (requestId !== suggestionsRequestId) {
            return;
          }
          patchState(store, { isLoadingSuggestions: false, suggestions: result });
        } catch {
          if (requestId !== suggestionsRequestId) {
            return;
          }
          patchState(store, { isLoadingSuggestions: false, suggestions: { categories: [], products: [] } });
        }
      },

      clearSuggestions(): void {
        // Bumping the counter also discards any suggestions request already in flight — otherwise
        // it could still resolve after the box was cleared and repopulate the dropdown.
        suggestionsRequestId++;
        patchState(store, { suggestions: { categories: [], products: [] }, isLoadingSuggestions: false });
      },

      async loadCategories(): Promise<void> {
        if (store.categories().length > 0) {
          return;
        }

        try {
          const categories = await firstValueFrom(
            http.get<CategorySummary[]>(`${environment.apiUrl}/api/v1/products/categories`),
          );
          patchState(store, { categories });
        } catch {
          // Empty-state category links are a nice-to-have, not critical — fail silently so a
          // categories-endpoint outage never blocks the search-results page itself from rendering.
        }
      },
    };
  }),
);
