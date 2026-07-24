import { TestBed } from '@angular/core/testing';

import { CheckoutStore } from './checkout.store';

describe('CheckoutStore', () => {
  it('should start with no address', () => {
    const store = TestBed.inject(CheckoutStore);

    expect(store.address()).toBeNull();
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
});
