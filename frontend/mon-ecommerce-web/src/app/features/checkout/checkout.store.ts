import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';

export interface CheckoutAddress {
  street: string;
  city: string;
  postalCode: string;
  country: string;
}

interface CheckoutState {
  address: CheckoutAddress | null;
}

const initialState: CheckoutState = {
  address: null,
};

// Root-provided, plain client-side state — no HTTP calls. Holds the checkout wizard's
// in-progress data across steps (address now; shipping/payment fields join this store in
// Stories 4.4/4.5). Being root-provided means it naturally survives Angular Router navigation
// between checkout steps, satisfying "no data loss on back navigation" (Story 4.3 AC #6) without
// any extra persistence — deliberately not backed by sessionStorage/localStorage, since surviving
// a hard page refresh was never part of the AC.
export const CheckoutStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => ({
    setAddress(address: CheckoutAddress): void {
      patchState(store, { address });
    },
  })),
);
