import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';

import { CartStore } from '../../../cart/cart.store';
import { CheckoutStore, ShippingOption } from '../../checkout.store';
import { OrderStepIndicatorComponent } from '../../components/order-step-indicator/order-step-indicator.component';

@Component({
  selector: 'app-checkout-shipping',
  standalone: true,
  imports: [OrderStepIndicatorComponent],
  templateUrl: './checkout-shipping.component.html',
  styleUrl: './checkout-shipping.component.scss',
})
export class CheckoutShippingComponent implements OnInit {
  private readonly router = inject(Router);
  protected readonly cartStore = inject(CartStore);
  protected readonly checkoutStore = inject(CheckoutStore);

  protected readonly initialized = signal(false);

  // A local selection signal, not a direct template read of CheckoutStore.shippingOption() —
  // pre-seeded from it in ngOnInit so AC #5's "preserved on back navigation" shows the prior
  // choice already selected (not requiring a re-pick), while still giving the radio group a
  // single local source of truth to bind to as the customer changes their selection on this page.
  protected readonly selected = signal<ShippingOption | null>(null);

  protected readonly subtotalInCents = computed(
    () => this.cartStore.totalInCents() + (this.selected()?.priceInCents ?? 0),
  );

  async ngOnInit(): Promise<void> {
    // Checkout requires the address step to have been completed first — reachable by directly
    // navigating/bookmarking this URL, not just by skipping ahead in the UI, since nothing else
    // enforces step order (review finding).
    if (!this.checkoutStore.address()) {
      void this.router.navigate(['/checkout/adresse']);
      return;
    }

    await this.loadOptionsAndPreselect();
  }

  protected select(option: ShippingOption): void {
    this.selected.set(option);
  }

  protected async retryLoadShippingOptions(): Promise<void> {
    await this.loadOptionsAndPreselect();
  }

  private async loadOptionsAndPreselect(): Promise<void> {
    await this.checkoutStore.loadShippingOptions();

    const alreadyChosen = this.checkoutStore.shippingOption();
    if (alreadyChosen) {
      this.selected.set(alreadyChosen);
    }

    this.initialized.set(true);
  }

  protected formatPrice(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }

  protected onSubmit(): void {
    const option = this.selected();
    if (!option) {
      return;
    }

    this.checkoutStore.setShippingOption(option);
    void this.router.navigate(['/checkout/paiement']);
  }
}
