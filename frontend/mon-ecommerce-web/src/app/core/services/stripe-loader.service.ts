import { Injectable } from '@angular/core';
import { loadStripe, Stripe } from '@stripe/stripe-js';

// Thin injectable wrapper around @stripe/stripe-js's loadStripe() — a plain ES module function
// can't be mocked via Angular DI (or reliably via spyOn under esbuild/Karma), so components that
// need Stripe.js inject this instead, and tests substitute a fake implementation. No other logic
// belongs here; the actual Elements/PaymentElement/confirmPayment usage stays in the component
// that owns the payment form's lifecycle (checkout-payment.component.ts).
@Injectable({ providedIn: 'root' })
export class StripeLoaderService {
  loadStripe(publishableKey: string): Promise<Stripe | null> {
    return loadStripe(publishableKey);
  }
}
