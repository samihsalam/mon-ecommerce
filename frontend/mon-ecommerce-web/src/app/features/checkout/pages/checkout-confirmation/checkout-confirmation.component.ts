import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { CheckoutStore, OrderDetail } from '../../checkout.store';
import { OrderStepIndicatorComponent } from '../../components/order-step-indicator/order-step-indicator.component';

const POLL_INTERVAL_MS = 2000;
// ~30s total — matches AC #4's own confirmation-email SLA (≤30s) as the natural upper bound for
// "the order should exist by now" (Story 4.6's Dev Notes).
const MAX_POLL_ATTEMPTS = 15;

@Component({
  selector: 'app-checkout-confirmation',
  standalone: true,
  imports: [OrderStepIndicatorComponent, RouterLink],
  templateUrl: './checkout-confirmation.component.html',
  styleUrl: './checkout-confirmation.component.scss',
})
export class CheckoutConfirmationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly checkoutStore = inject(CheckoutStore);

  protected readonly order = signal<OrderDetail | null>(null);
  protected readonly refunded = signal(false);
  protected readonly timedOut = signal(false);
  protected readonly missingPaymentIntent = signal(false);

  // Guards the poll loop against continuing to fire HTTP requests (and writing to signals on a
  // detached component) after the customer navigates away mid-poll — there's no Observable/
  // subscription here for takeUntilDestroyed to hook into, just a plain async loop.
  private destroyed = false;

  async ngOnInit(): Promise<void> {
    const paymentIntentId = this.route.snapshot.queryParamMap.get('payment_intent');
    if (!paymentIntentId) {
      // No natural "previous step" to send the customer back to here — payment may already have
      // succeeded server-side even though this page can't identify which one to show.
      this.missingPaymentIntent.set(true);
      return;
    }

    this.destroyRef.onDestroy(() => (this.destroyed = true));

    await this.pollForOrder(paymentIntentId);
  }

  private async pollForOrder(paymentIntentId: string): Promise<void> {
    for (let attempt = 1; attempt <= MAX_POLL_ATTEMPTS; attempt++) {
      const result = await this.checkoutStore.getOrderByPaymentIntent(paymentIntentId);
      if (this.destroyed) return;

      if (result.status === 'found') {
        this.order.set(result.order);
        return;
      }
      if (result.status === 'refunded') {
        this.refunded.set(true);
        return;
      }

      if (attempt < MAX_POLL_ATTEMPTS) {
        await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
        if (this.destroyed) return;
      }
    }

    if (!this.destroyed) {
      this.timedOut.set(true);
    }
  }

  protected formatPrice(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }
}
