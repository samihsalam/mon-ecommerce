import { Component, inject, input, signal } from '@angular/core';

import { CartStore } from '../../../cart/cart.store';
import { ToastService } from '../../../../core/services/toast.service';
import type { ProductDetail } from '../../product-detail.store';

@Component({
  selector: 'app-sticky-add-to-cart',
  standalone: true,
  templateUrl: './sticky-add-to-cart.component.html',
  styleUrl: './sticky-add-to-cart.component.scss',
})
export class StickyAddToCartComponent {
  readonly product = input.required<ProductDetail>();

  private readonly cartStore = inject(CartStore);
  private readonly toastService = inject(ToastService);

  protected readonly isAdding = signal(false);

  protected formatPrice(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }

  protected async onAddToCart(): Promise<void> {
    const product = this.product();
    if (!product.inStock || this.isAdding()) {
      return;
    }

    this.isAdding.set(true);
    try {
      const succeeded = await this.cartStore.addItem(product.id, 1);
      if (succeeded) {
        // 3s, not ToastService's 4s default — AC #2's explicit duration for this confirmation.
        this.toastService.show(`${product.name} ajoutée au panier`, 3000);
      } else {
        // CartStore.error is set on failure but nothing was reading it anywhere (review
        // finding) — a failed add would otherwise leave the user with zero feedback.
        this.toastService.show("Impossible d'ajouter cet article au panier.");
      }
    } finally {
      this.isAdding.set(false);
    }
  }
}
