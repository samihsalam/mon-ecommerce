import { Component, effect, ElementRef, inject, PLATFORM_ID, signal, viewChild } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';

import { ConsentService } from '../../services/consent.service';

// AC #1, #5, #6: first-load RGPD banner. Mounted once in app.component.html (same pattern as
// ToastComponent/CartDrawerComponent) and driven entirely by ConsentService's isBannerOpen signal.
@Component({
  selector: 'app-cookie-banner',
  standalone: true,
  imports: [A11yModule],
  templateUrl: './cookie-banner.component.html',
  styleUrl: './cookie-banner.component.scss',
})
export class CookieBannerComponent {
  protected readonly consentService = inject(ConsentService);

  protected readonly showCustomize = signal(false);
  protected readonly nonEssentialToggle = signal(false);

  private readonly platformId = inject(PLATFORM_ID);
  private readonly bannerEl = viewChild<ElementRef<HTMLElement>>('banner');
  private previouslyFocusedElement: HTMLElement | null = null;

  constructor() {
    // Moves focus into the trapped region as soon as the banner appears, and restores it to
    // whatever was focused before (e.g. the footer's "Modifier mes préférences" button) once the
    // banner closes — same capture/restore pattern as CartDrawerComponent's open effect
    // (setTimeout defers until Angular has rendered the @if).
    effect(() => {
      if (!isPlatformBrowser(this.platformId)) {
        return;
      }

      if (this.consentService.isBannerOpen()) {
        this.previouslyFocusedElement = document.activeElement as HTMLElement | null;
        setTimeout(() => this.bannerEl()?.nativeElement.focus());
      } else {
        this.previouslyFocusedElement?.focus();
        this.previouslyFocusedElement = null;
      }
    });
  }

  protected acceptAll(): void {
    this.consentService.acceptAll();
    this.resetCustomizePanel();
  }

  protected reject(): void {
    this.consentService.reject();
    this.resetCustomizePanel();
  }

  // Seeds the toggle from the visitor's CURRENT consent (not always `false`) — a returning
  // visitor reopening "Personnaliser" via "Modifier mes préférences" must see their real
  // non-essential-cookies state, otherwise clicking "Enregistrer mes choix" without touching the
  // checkbox would silently downgrade a prior "Accepter tout"/custom-true consent to false.
  protected openCustomize(): void {
    this.nonEssentialToggle.set(this.consentService.hasNonEssentialConsent());
    this.showCustomize.set(true);
  }

  protected saveCustom(): void {
    this.consentService.acceptCustom(this.nonEssentialToggle());
    this.resetCustomizePanel();
  }

  protected toggleNonEssential(): void {
    this.nonEssentialToggle.update((value) => !value);
  }

  // AC #5: Escape closes the "Personnaliser" panel back to the 3-button view. It deliberately
  // never records a consent choice — doing so on Escape would set consent without an explicit
  // button click, defeating the point of RGPD's explicit-consent requirement (see story Dev Notes).
  protected closeCustomizePanel(): void {
    this.showCustomize.set(false);
  }

  private resetCustomizePanel(): void {
    this.showCustomize.set(false);
    this.nonEssentialToggle.set(false);
  }
}
