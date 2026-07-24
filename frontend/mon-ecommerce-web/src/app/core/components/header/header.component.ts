import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CartStore } from '../../../features/cart/cart.store';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent {
  protected readonly cartStore = inject(CartStore);

  // Toggled true then back to false shortly after every itemCount INCREASE — the badge's
  // "subtle animation" (AC #2) plays on this class, not on element creation, since the badge
  // element already exists once the cart has at least one item (an @if-based re-render alone
  // wouldn't replay a CSS animation for 1 -> 2, only for 0 -> 1).
  protected readonly pulsing = signal(false);
  // null (not 0) until the effect below has run once — distinguishes "we haven't observed a
  // count yet" from "we observed 0". Without this, a returning visitor whose CartStore hydrates
  // via its automatic GET /api/v1/cart with an already non-empty cart would see the badge pulse
  // on load (0 -> N looks identical to a real add otherwise) even though nothing was just added —
  // a real review finding, since CartStore's initial load is async and its result is
  // indistinguishable from a genuine increase without tracking whether this is the first
  // observation.
  private previousItemCount: number | null = null;
  private pulseTimeoutId: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    effect(() => {
      const count = this.cartStore.itemCount();
      if (this.previousItemCount !== null && count > this.previousItemCount) {
        this.pulsing.set(false);
        clearTimeout(this.pulseTimeoutId);
        // Re-set on the next tick so a class removed-then-immediately-readded still
        // restarts the CSS animation (browsers don't replay an animation if the class
        // never actually left the render between two synchronous signal updates), then
        // cleared again after the animation's duration so the NEXT increase can replay it.
        this.pulseTimeoutId = setTimeout(() => {
          this.pulsing.set(true);
          this.pulseTimeoutId = setTimeout(() => this.pulsing.set(false), 400);
        }, 0);
      }
      this.previousItemCount = count;
    });
  }

  protected onCartClick(): void {
    this.cartStore.open();
  }
}
