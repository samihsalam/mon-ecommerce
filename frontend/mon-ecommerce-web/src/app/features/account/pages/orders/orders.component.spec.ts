import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { OrdersComponent } from './orders.component';
import { environment } from '../../../../../environments/environment';

describe('OrdersComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrdersComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should render the order list', async () => {
    const fixture = TestBed.createComponent(OrdersComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/api/v1/account/orders`);
    req.flush({
      items: [{ id: 'order-1', orderNumber: '#ABCD1234', date: '2026-01-01', totalInCents: 1000, status: 'Expédiée' }],
      totalCount: 1,
      page: 1,
      pageSize: 10,
    });

    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('#ABCD1234');
  });

  it('should show the empty state with a CTA when there are no orders', async () => {
    const fixture = TestBed.createComponent(OrdersComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/api/v1/account/orders`);
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 10 });

    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Aucune commande pour le moment');
    expect(compiled.textContent).toContain('Commencer à shopper');
  });
});
