import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';

import { CheckoutPaymentComponent } from './checkout-payment.component';
import { CheckoutStore } from '../../checkout.store';
import { CartStore } from '../../../cart/cart.store';
import { StripeLoaderService } from '../../../../core/services/stripe-loader.service';
import { environment } from '../../../../../environments/environment';

describe('CheckoutPaymentComponent', () => {
  let httpMock: HttpTestingController;
  let stripeLoaderSpy: jasmine.SpyObj<StripeLoaderService>;
  let confirmPaymentSpy: jasmine.Spy;
  let mountSpy: jasmine.Spy;

  const cannedAddress = { street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' };
  const cannedShippingOption = { id: 'standard', name: 'Livraison Standard', priceInCents: 490, estimatedDelay: '3–5 jours ouvrés' };

  beforeEach(async () => {
    mountSpy = jasmine.createSpy('mount');
    confirmPaymentSpy = jasmine.createSpy('confirmPayment').and.resolveTo({});
    const fakeElement = { mount: mountSpy };
    const fakeElements = { create: jasmine.createSpy('create').and.returnValue(fakeElement) };
    const fakeStripe = {
      elements: jasmine.createSpy('elements').and.returnValue(fakeElements),
      confirmPayment: confirmPaymentSpy,
    };
    stripeLoaderSpy = jasmine.createSpyObj('StripeLoaderService', ['loadStripe']);
    stripeLoaderSpy.loadStripe.and.resolveTo(fakeStripe as never);

    await TestBed.configureTestingModule({
      imports: [CheckoutPaymentComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: StripeLoaderService, useValue: stripeLoaderSpy },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function setupStores({ withAddress = true, withShipping = true } = {}) {
    if (withAddress) {
      TestBed.inject(CheckoutStore).setAddress(cannedAddress);
    }
    if (withShipping) {
      TestBed.inject(CheckoutStore).setShippingOption(cannedShippingOption);
    }
    TestBed.inject(CartStore);
    httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`).flush({
      items: [{ id: 'item-1', productId: 'p1', productName: 'Tote Parisienne', imageUrl: null, unitPriceInCents: 10000, quantity: 1, lineTotalInCents: 10000 }],
      totalInCents: 10000,
    });
  }

  it('should redirect to /checkout/adresse when no address is set', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    setupStores({ withAddress: false });
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith(['/checkout/adresse']);
    httpMock.expectNone(`${environment.apiUrl}/api/v1/payments/create-intent`);
  });

  it('should redirect to /checkout/livraison when no shipping option is set', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    setupStores({ withShipping: false });
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith(['/checkout/livraison']);
    httpMock.expectNone(`${environment.apiUrl}/api/v1/payments/create-intent`);
  });

  it('should create a payment intent with the chosen shipping option id, then load Stripe and mount the PaymentElement', async () => {
    setupStores();
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`);
    expect(req.request.body).toEqual({
      shippingOptionId: 'standard',
      street: '12 rue de la Paix',
      city: 'Paris',
      postalCode: '75002',
      country: 'France',
    });
    req.flush({ clientSecret: 'pi_abc_secret_xyz' });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(stripeLoaderSpy.loadStripe).toHaveBeenCalled();
    expect(mountSpy).toHaveBeenCalled();
  });

  it('should render the order summary with items, shipping, and total', async () => {
    setupStores();
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`).flush({ clientSecret: 'pi_abc_secret_xyz' });
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Tote Parisienne');
    expect(text).toContain('Livraison Standard');
    // 10000 (cart) + 490 (standard) = 10490 cents = 104.90 €
    expect(text).toContain('104.90');
  });

  it('should show the exact decline message, without clearing cart/checkout state, on a failed confirmPayment', async () => {
    confirmPaymentSpy.and.resolveTo({ error: { message: 'Your card was declined.' } });

    setupStores();
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`).flush({ clientSecret: 'pi_abc_secret_xyz' });
    await fixture.whenStable();
    fixture.detectChanges();

    await fixture.componentInstance['onSubmit']();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Paiement refusé. Vérifiez vos informations.');

    const checkoutStore = TestBed.inject(CheckoutStore);
    const cartStore = TestBed.inject(CartStore);
    expect(checkoutStore.address()).toEqual(cannedAddress);
    expect(checkoutStore.shippingOption()).toEqual(cannedShippingOption);
    expect(cartStore.items().length).toBe(1);
  });

  it('should navigate to /checkout/confirmation with the payment intent id on a successful payment', async () => {
    confirmPaymentSpy.and.resolveTo({ paymentIntent: { id: 'pi_abc' } });
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    setupStores();
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`).flush({ clientSecret: 'pi_abc_secret_xyz' });
    await fixture.whenStable();
    fixture.detectChanges();

    await fixture.componentInstance['onSubmit']();

    expect(navigateSpy).toHaveBeenCalledWith(['/checkout/confirmation'], { queryParams: { payment_intent: 'pi_abc' } });
  });

  it('should show a retry option when creating the payment intent fails', async () => {
    setupStores();
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`)
      .flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[role="alert"]')).toBeTruthy();

    const retryButton = Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Réessayer',
    );
    expect(retryButton).toBeTruthy();

    retryButton!.click();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`).flush({ clientSecret: 'pi_abc_secret_xyz' });
    await fixture.whenStable();
  });

  it('should show a retry option when Stripe.js itself fails to load', async () => {
    stripeLoaderSpy.loadStripe.and.resolveTo(null);

    setupStores();
    const fixture = TestBed.createComponent(CheckoutPaymentComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`).flush({ clientSecret: 'pi_abc_secret_xyz' });
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Impossible de charger le module de paiement.');
  });
});
