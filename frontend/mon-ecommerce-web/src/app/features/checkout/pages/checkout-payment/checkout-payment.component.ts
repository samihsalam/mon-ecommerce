import { Component, computed, ElementRef, inject, OnDestroy, OnInit, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Stripe, StripeElements } from '@stripe/stripe-js';

import { environment } from '../../../../../environments/environment';
import { CartStore } from '../../../cart/cart.store';
import { StripeLoaderService } from '../../../../core/services/stripe-loader.service';
import { CheckoutStore } from '../../checkout.store';
import { OrderStepIndicatorComponent } from '../../components/order-step-indicator/order-step-indicator.component';

@Component({
  selector: 'app-checkout-payment',
  standalone: true,
  imports: [OrderStepIndicatorComponent],
  templateUrl: './checkout-payment.component.html',
  styleUrl: './checkout-payment.component.scss',
})
export class CheckoutPaymentComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly stripeLoader = inject(StripeLoaderService);
  protected readonly cartStore = inject(CartStore);
  protected readonly checkoutStore = inject(CheckoutStore);

  private readonly paymentElementContainer = viewChild<ElementRef<HTMLElement>>('paymentElementContainer');

  protected readonly initialized = signal(false);
  protected readonly submitting = signal(false);
  protected readonly stripeFailedToLoad = signal(false);
  // AC #3's exact copy, shown on a Stripe-side decline — deliberately NOT Stripe's own
  // error.message (raw, English, technical); the AC specifies this literal French text.
  protected readonly declineError = signal<string | null>(null);

  protected readonly totalInCents = computed(
    () => this.cartStore.totalInCents() + (this.checkoutStore.shippingOption()?.priceInCents ?? 0),
  );

  private stripe: Stripe | null = null;
  private elements: StripeElements | null = null;

  async ngOnInit(): Promise<void> {
    // Same step-order guard added to checkout-shipping in Story 4.4's review — payment can't
    // proceed without both a confirmed address and a chosen shipping option.
    if (!this.checkoutStore.address()) {
      void this.router.navigate(['/checkout/adresse']);
      return;
    }
    if (!this.checkoutStore.shippingOption()) {
      void this.router.navigate(['/checkout/livraison']);
      return;
    }

    await this.loadPaymentForm();
  }

  ngOnDestroy(): void {
    // Stripe Elements has no explicit unmount API on the Elements instance itself — dropping the
    // reference lets the mounted DOM node (already removed by Angular's own router navigation
    // away from this route) be garbage collected normally.
    this.elements = null;
    this.stripe = null;
  }

  protected async retryLoadPaymentForm(): Promise<void> {
    await this.loadPaymentForm();
  }

  private async loadPaymentForm(): Promise<void> {
    const shippingOption = this.checkoutStore.shippingOption();
    const address = this.checkoutStore.address();
    if (!shippingOption || !address) {
      return;
    }

    this.stripeFailedToLoad.set(false);
    this.initialized.set(false);

    const clientSecret = await this.checkoutStore.createPaymentIntent(shippingOption.id, address);
    if (!clientSecret) {
      this.initialized.set(true);
      return;
    }

    this.stripe = await this.stripeLoader.loadStripe(environment.stripePublishableKey);
    if (!this.stripe) {
      this.stripeFailedToLoad.set(true);
      this.initialized.set(true);
      return;
    }

    this.initialized.set(true);

    // No artificial delay needed here — #paymentElementContainer is unconditionally rendered in
    // the template (visibility controlled via [hidden], not @if), so viewChild() already
    // resolved by the time this async chain (an HTTP call, then loadStripe()) completes.
    this.mountPaymentElement(clientSecret);
  }

  private mountPaymentElement(clientSecret: string): void {
    if (!this.stripe) {
      return;
    }
    const container = this.paymentElementContainer();
    if (!container) {
      return;
    }

    this.elements = this.stripe.elements({ clientSecret });
    this.elements.create('payment').mount(container.nativeElement);
  }

  protected async onSubmit(): Promise<void> {
    if (!this.stripe || !this.elements || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.declineError.set(null);

    const { error, paymentIntent } = await this.stripe.confirmPayment({
      elements: this.elements,
      // Required even with redirect: 'if_required' — some payment methods still need an
      // off-site redirect (e.g. certain 3DS flows); Stripe appends ?payment_intent=pi_... to
      // this URL itself in that case, matching the query param used below for the common
      // (no-redirect) path, so both converge on the same confirmation-page contract.
      confirmParams: { return_url: `${window.location.origin}/checkout/confirmation` },
      redirect: 'if_required',
    });

    this.submitting.set(false);

    if (error) {
      // Do NOT clear CartStore/CheckoutStore here — AC #3's "without losing the cart".
      this.declineError.set('Paiement refusé. Vérifiez vos informations.');
      return;
    }

    // Payment succeeded client-side — order creation happens asynchronously via the Stripe
    // webhook (Story 4.6), which the confirmation page polls for using this payment intent id.
    void this.router.navigate(['/checkout/confirmation'], { queryParams: { payment_intent: paymentIntent?.id } });
  }

  protected formatPrice(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }
}
