import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { OrdersStore } from './orders.store';
import { environment } from '../../../environments/environment';

describe('OrdersStore', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should load the paginated order list', async () => {
    const store = TestBed.inject(OrdersStore);

    const loadPromise = store.loadOrders();

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/api/v1/account/orders` && r.params.get('page') === '1',
    );
    expect(req.request.method).toBe('GET');
    req.flush({
      items: [{ id: 'order-1', orderNumber: '#ABCD1234', date: '2026-01-01', totalInCents: 1000, status: 'Expédiée' }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    });

    await loadPromise;

    expect(store.orders().length).toBe(1);
    expect(store.totalCount()).toBe(1);
  });

  it('should show an error when loading orders fails', async () => {
    const store = TestBed.inject(OrdersStore);

    const loadPromise = store.loadOrders();

    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/api/v1/account/orders`);
    req.flush(null, { status: 500, statusText: 'Server Error' });

    await loadPromise;

    expect(store.error()).toBe('Impossible de charger vos commandes. Veuillez réessayer.');
  });

  it('should load a single order detail', async () => {
    const store = TestBed.inject(OrdersStore);

    const loadPromise = store.loadOrderDetail('order-1');

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/orders/order-1`);
    expect(req.request.method).toBe('GET');
    req.flush({
      id: 'order-1',
      orderNumber: '#ABCD1234',
      date: '2026-01-01',
      totalInCents: 1000,
      status: 'Expédiée',
      trackingNumber: null,
      shippingAddress: { id: 'addr-1', street: '1 Rue de Paris', city: 'Paris', postalCode: '75001', country: 'France' },
      items: [{ productName: 'T-shirt', unitPriceInCents: 1000, quantity: 1 }],
    });

    await loadPromise;

    expect(store.selectedOrder()?.orderNumber).toBe('#ABCD1234');
  });
});
