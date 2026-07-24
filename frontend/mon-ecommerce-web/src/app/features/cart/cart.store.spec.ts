import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { CartStore, CartDto } from './cart.store';
import { environment } from '../../../environments/environment';

describe('CartStore', () => {
  let httpMock: HttpTestingController;

  const emptyCart: CartDto = { items: [], totalInCents: 0 };

  const cannedCart: CartDto = {
    items: [
      {
        id: 'item-1',
        productId: 'product-1',
        productName: 'Tote Parisienne',
        imageUrl: 'a.webp',
        unitPriceInCents: 5000,
        quantity: 2,
        lineTotalInCents: 10000,
      },
    ],
    totalInCents: 10000,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // CartStore fires an automatic GET /api/v1/cart the moment it's constructed (hydrates on
  // injection, same "load on construction" pattern as AuthStore reading localStorage) — every
  // test must flush that initial request before asserting on any subsequent action.
  function injectAndFlushInitialLoad(initial: CartDto = emptyCart): InstanceType<typeof CartStore> {
    const store = TestBed.inject(CartStore);
    httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`).flush(initial);
    return store;
  }

  it('should hydrate from GET /api/v1/cart on construction', async () => {
    const store = injectAndFlushInitialLoad(cannedCart);
    await Promise.resolve();

    expect(store.items().length).toBe(1);
    expect(store.totalInCents()).toBe(10000);
    expect(store.itemCount()).toBe(2);
  });

  it('should compute itemCount as zero for an empty cart', async () => {
    const store = injectAndFlushInitialLoad();
    await Promise.resolve();

    expect(store.itemCount()).toBe(0);
  });

  it('should sum quantities across multiple line items for itemCount', async () => {
    const store = injectAndFlushInitialLoad({
      items: [
        { ...cannedCart.items[0], quantity: 2 },
        { ...cannedCart.items[0], id: 'item-2', productId: 'product-2', quantity: 3 },
      ],
      totalInCents: 25000,
    });
    await Promise.resolve();

    expect(store.itemCount()).toBe(5);
  });

  it('should replace state from the response when adding an item', async () => {
    const store = injectAndFlushInitialLoad();

    const addPromise = store.addItem('product-1', 2);
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart/items`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ productId: 'product-1', quantity: 2 });
    req.flush(cannedCart);

    const succeeded = await addPromise;

    expect(succeeded).toBe(true);
    expect(store.items().length).toBe(1);
    expect(store.totalInCents()).toBe(10000);
  });

  it('should return false and set an error when adding an item fails', async () => {
    const store = injectAndFlushInitialLoad();

    const addPromise = store.addItem('product-1', 2);
    httpMock.expectOne(`${environment.apiUrl}/api/v1/cart/items`).flush(null, { status: 404, statusText: 'Not Found' });

    const succeeded = await addPromise;

    expect(succeeded).toBe(false);
    expect(store.error()).toBe("Impossible d'ajouter cet article au panier.");
  });

  it('should replace state from the response when updating item quantity', async () => {
    const store = injectAndFlushInitialLoad(cannedCart);
    await Promise.resolve();

    const updatePromise = store.updateItemQuantity('item-1', 5);
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart/items/item-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ quantity: 5 });
    req.flush({ items: [{ ...cannedCart.items[0], quantity: 5, lineTotalInCents: 25000 }], totalInCents: 25000 });

    await updatePromise;

    expect(store.items()[0].quantity).toBe(5);
    expect(store.totalInCents()).toBe(25000);
  });

  it('should replace state from the response when removing an item', async () => {
    const store = injectAndFlushInitialLoad(cannedCart);
    await Promise.resolve();

    const removePromise = store.removeItem('item-1');
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart/items/item-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(emptyCart);

    await removePromise;

    expect(store.items()).toEqual([]);
    expect(store.itemCount()).toBe(0);
  });

  it('should toggle isOpen via open()/close()', () => {
    const store = injectAndFlushInitialLoad();

    expect(store.isOpen()).toBe(false);
    store.open();
    expect(store.isOpen()).toBe(true);
    store.close();
    expect(store.isOpen()).toBe(false);
  });
});
