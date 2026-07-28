import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { CheckoutStore, ShippingOption } from './checkout.store';
import { environment } from '../../../environments/environment';

describe('CheckoutStore', () => {
  let httpMock: HttpTestingController;

  const cannedOptions: ShippingOption[] = [
    { id: 'standard', name: 'Livraison Standard', priceInCents: 490, estimatedDelay: '3–5 jours ouvrés' },
    { id: 'express', name: 'Livraison Express', priceInCents: 990, estimatedDelay: '1–2 jours ouvrés' },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should start with no address and no shipping option', () => {
    const store = TestBed.inject(CheckoutStore);

    expect(store.address()).toBeNull();
    expect(store.shippingOption()).toBeNull();
    expect(store.shippingOptions()).toEqual([]);
  });

  it('should patch state when setAddress is called', () => {
    const store = TestBed.inject(CheckoutStore);

    store.setAddress({ street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' });

    expect(store.address()).toEqual({
      street: '12 rue de la Paix',
      city: 'Paris',
      postalCode: '75002',
      country: 'France',
    });
  });

  it('should overwrite a previously set address', () => {
    const store = TestBed.inject(CheckoutStore);

    store.setAddress({ street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' });
    store.setAddress({ street: '5 avenue Foch', city: 'Lyon', postalCode: '69001', country: 'France' });

    expect(store.address()?.city).toBe('Lyon');
  });

  it('should populate shippingOptions when loadShippingOptions succeeds', async () => {
    const store = TestBed.inject(CheckoutStore);

    const loadPromise = store.loadShippingOptions();
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/shipping-options`);
    expect(req.request.method).toBe('GET');
    req.flush(cannedOptions);
    await loadPromise;

    expect(store.shippingOptions()).toEqual(cannedOptions);
    expect(store.shippingOptionsError()).toBeNull();
  });

  it('should set an error and leave shippingOptions untouched when loadShippingOptions fails', async () => {
    const store = TestBed.inject(CheckoutStore);

    const loadPromise = store.loadShippingOptions();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/shipping-options`).flush(null, {
      status: 500,
      statusText: 'Server Error',
    });
    await loadPromise;

    expect(store.shippingOptions()).toEqual([]);
    expect(store.shippingOptionsError()).toBe('Impossible de charger les options de livraison. Veuillez réessayer.');
  });

  it('should patch state when setShippingOption is called', () => {
    const store = TestBed.inject(CheckoutStore);

    store.setShippingOption(cannedOptions[1]);

    expect(store.shippingOption()).toEqual(cannedOptions[1]);
  });

  const cannedAddress = { street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' };

  it('should return the clientSecret when createPaymentIntent succeeds', async () => {
    const store = TestBed.inject(CheckoutStore);

    const promise = store.createPaymentIntent('standard', cannedAddress);
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      shippingOptionId: 'standard',
      street: '12 rue de la Paix',
      city: 'Paris',
      postalCode: '75002',
      country: 'France',
    });
    req.flush({ clientSecret: 'pi_abc_secret_xyz' });

    expect(await promise).toBe('pi_abc_secret_xyz');
    expect(store.paymentError()).toBeNull();
  });

  it('should set paymentError and return null when createPaymentIntent fails', async () => {
    const store = TestBed.inject(CheckoutStore);

    const promise = store.createPaymentIntent('standard', cannedAddress);
    httpMock
      .expectOne(`${environment.apiUrl}/api/v1/payments/create-intent`)
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(await promise).toBeNull();
    expect(store.paymentError()).toBe('Impossible de préparer le paiement. Veuillez réessayer.');
  });

  const cannedOrder = {
    id: 'order-1',
    orderNumber: '#ABC12345',
    date: '2026-07-28T00:00:00Z',
    totalInCents: 5490,
    status: 'En préparation',
    trackingNumber: null,
    shippingAddress: { id: 'addr-1', ...cannedAddress },
    items: [{ productName: 'Chaise Scandinave', unitPriceInCents: 5000, quantity: 1 }],
  };

  it('should return a "found" result with the order when getOrderByPaymentIntent succeeds', async () => {
    const store = TestBed.inject(CheckoutStore);

    const promise = store.getOrderByPaymentIntent('pi_abc');
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/orders/by-payment-intent/pi_abc`);
    expect(req.request.method).toBe('GET');
    req.flush(cannedOrder);

    expect(await promise).toEqual({ status: 'found', order: cannedOrder });
  });

  it('should return a "pending" result when the order is not yet confirmed (404)', async () => {
    const store = TestBed.inject(CheckoutStore);

    const promise = store.getOrderByPaymentIntent('pi_pending');
    httpMock
      .expectOne(`${environment.apiUrl}/api/v1/account/orders/by-payment-intent/pi_pending`)
      .flush(null, { status: 404, statusText: 'Not Found' });

    expect(await promise).toEqual({ status: 'pending' });
  });

  it('should return a "refunded" result when stock was insufficient (409)', async () => {
    const store = TestBed.inject(CheckoutStore);

    const promise = store.getOrderByPaymentIntent('pi_refunded');
    httpMock
      .expectOne(`${environment.apiUrl}/api/v1/account/orders/by-payment-intent/pi_refunded`)
      .flush(null, { status: 409, statusText: 'Conflict' });

    expect(await promise).toEqual({ status: 'refunded' });
  });
});
