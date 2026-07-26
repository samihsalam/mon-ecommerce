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
});
