import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';

import { CheckoutShippingComponent } from './checkout-shipping.component';
import { CheckoutStore } from '../../checkout.store';
import { CartStore } from '../../../cart/cart.store';
import { environment } from '../../../../../environments/environment';

describe('CheckoutShippingComponent', () => {
  let httpMock: HttpTestingController;

  const cannedOptions = [
    { id: 'standard', name: 'Livraison Standard', priceInCents: 490, estimatedDelay: '3–5 jours ouvrés' },
    { id: 'express', name: 'Livraison Express', priceInCents: 990, estimatedDelay: '1–2 jours ouvrés' },
  ];

  const cannedAddress = { street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CheckoutShippingComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function continueButton(fixture: { nativeElement: unknown }): HTMLButtonElement {
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    return buttons.find((b) => b.textContent?.trim() === 'Continuer') as HTMLButtonElement;
  }

  // Sets an address on CheckoutStore by default — the step-order guard (AC-adjacent review fix)
  // redirects away without one, and most of these tests are about the shipping step itself.
  function createAndLoad(cartTotalInCents = 10000, { withAddress = true } = {}) {
    if (withAddress) {
      TestBed.inject(CheckoutStore).setAddress(cannedAddress);
    }

    const cartStore = TestBed.inject(CartStore);
    // CartStore auto-fetches on construction — flush that before anything else.
    httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`).flush({ items: [], totalInCents: cartTotalInCents });

    const fixture = TestBed.createComponent(CheckoutShippingComponent);
    fixture.detectChanges();

    if (withAddress) {
      httpMock.expectOne(`${environment.apiUrl}/api/v1/shipping-options`).flush(cannedOptions);
    }

    return { fixture, cartStore };
  }

  it('should fetch and render the available shipping options', async () => {
    const { fixture } = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Livraison Standard');
    expect(compiled.textContent).toContain('Livraison Express');
  });

  it('should redirect to /checkout/adresse and never fetch shipping options when no address is set', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const { fixture } = createAndLoad(10000, { withAddress: false });
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith(['/checkout/adresse']);
    httpMock.expectNone(`${environment.apiUrl}/api/v1/shipping-options`);
  });

  it('should pre-select an already-chosen CheckoutStore shipping option', async () => {
    const checkoutStore = TestBed.inject(CheckoutStore);
    checkoutStore.setAddress(cannedAddress);
    checkoutStore.setShippingOption(cannedOptions[1]);

    const { fixture } = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component['selected']()?.id).toBe('express');
  });

  it('should update the displayed subtotal (cart total + shipping cost) when the selection changes', async () => {
    const { fixture } = createAndLoad(10000);
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component['subtotalInCents']()).toBe(10000);

    component['select'](cannedOptions[1]);
    fixture.detectChanges();

    expect(component['subtotalInCents']()).toBe(10990);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('109.90');
  });

  it('should disable "Continuer" with no selection and enable it once one is picked', async () => {
    const { fixture } = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = continueButton(fixture);
    expect(button.disabled).toBe(true);

    fixture.componentInstance['select'](cannedOptions[0]);
    fixture.detectChanges();

    expect(button.disabled).toBe(false);
  });

  it('should call setShippingOption and navigate to /checkout/paiement when the button is actually clicked', async () => {
    // Deliberately a real DOM click, not calling onSubmit() directly — a prior version of this
    // test called onSubmit() directly and passed even though the template's (ngSubmit) binding
    // was dead code with no FormsModule imported (clicking would have triggered a native page
    // reload instead). This exercises the real click path so that class of bug can't hide again.
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);
    const checkoutStore = TestBed.inject(CheckoutStore);

    const { fixture } = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    fixture.componentInstance['select'](cannedOptions[0]);
    fixture.detectChanges();

    continueButton(fixture).click();

    expect(checkoutStore.shippingOption()).toEqual(cannedOptions[0]);
    expect(navigateSpy).toHaveBeenCalledWith(['/checkout/paiement']);
  });

  it('should not navigate on submit when no option is selected', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const { fixture } = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    fixture.componentInstance['onSubmit']();

    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('should show an error with a retry option, and retry actually re-fetches, when the initial load fails', async () => {
    TestBed.inject(CheckoutStore).setAddress(cannedAddress);
    TestBed.inject(CartStore);
    httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`).flush({ items: [], totalInCents: 10000 });

    const fixture = TestBed.createComponent(CheckoutShippingComponent);
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/api/v1/shipping-options`)
      .flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[role="alert"]')).toBeTruthy();

    const retryButton = Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Réessayer',
    ) as HTMLButtonElement;
    expect(retryButton).toBeTruthy();

    retryButton.click();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/shipping-options`).flush(cannedOptions);
    await fixture.whenStable();
    fixture.detectChanges();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Livraison Standard');
    expect(compiled.querySelector('[role="alert"]')).toBeFalsy();
  });
});
