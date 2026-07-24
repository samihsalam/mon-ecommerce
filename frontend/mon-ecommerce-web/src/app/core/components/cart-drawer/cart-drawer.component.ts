import { Component, effect, ElementRef, inject, PLATFORM_ID, viewChild } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';
import { A11yModule } from '@angular/cdk/a11y';

import { CartStore } from '../../../features/cart/cart.store';
import { ToastService } from '../../services/toast.service';

const BODY_SCROLL_LOCK_CLASS = 'overflow-hidden';

@Component({
  selector: 'app-cart-drawer',
  standalone: true,
  imports: [RouterLink, A11yModule],
  templateUrl: './cart-drawer.component.html',
  styleUrl: './cart-drawer.component.scss',
})
export class CartDrawerComponent {
  protected readonly cartStore = inject(CartStore);

  private readonly toastService = inject(ToastService);
  private readonly dialogEl = viewChild<ElementRef<HTMLElement>>('dialog');
  private readonly platformId = inject(PLATFORM_ID);
  private previouslyFocusedElement: HTMLElement | null = null;

  constructor() {
    // Drives the body-scroll-lock (AC #5) and manual focus management (AC #3): capturing
    // "what was focused before" and moving focus INTO the dialog once it renders — deliberately
    // NOT using cdkTrapFocusAutoCapture for the latter, since that runs its own capture
    // asynchronously and could race with capturing previouslyFocusedElement below (risk of
    // grabbing an element already inside the dialog instead of the actual external trigger).
    // Doing both steps here, in one place, removes that race: capture is synchronous, and moving
    // focus in is deferred with setTimeout so it runs after Angular has created the dialog's view.
    effect(() => {
      if (!isPlatformBrowser(this.platformId)) {
        return;
      }

      if (this.cartStore.isOpen()) {
        this.previouslyFocusedElement = document.activeElement as HTMLElement | null;
        document.body.classList.add(BODY_SCROLL_LOCK_CLASS);
        setTimeout(() => this.dialogEl()?.nativeElement.focus());
      } else {
        document.body.classList.remove(BODY_SCROLL_LOCK_CLASS);
        this.previouslyFocusedElement?.focus();
        this.previouslyFocusedElement = null;
      }
    });
  }

  protected close(): void {
    this.cartStore.close();
  }

  protected formatPrice(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }

  protected async decrement(itemId: string, quantity: number): Promise<void> {
    const succeeded = await this.cartStore.updateItemQuantity(itemId, quantity - 1);
    if (!succeeded) {
      this.toastService.show('Impossible de mettre à jour cet article.');
    }
  }

  protected async increment(itemId: string, quantity: number): Promise<void> {
    const succeeded = await this.cartStore.updateItemQuantity(itemId, quantity + 1);
    if (!succeeded) {
      this.toastService.show('Impossible de mettre à jour cet article.');
    }
  }

  protected async remove(itemId: string): Promise<void> {
    const succeeded = await this.cartStore.removeItem(itemId);
    if (!succeeded) {
      this.toastService.show('Impossible de supprimer cet article.');
    }
  }
}
