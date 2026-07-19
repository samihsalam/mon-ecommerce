import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute, convertToParamMap } from '@angular/router';

import { OrderDetailComponent } from './order-detail.component';
import { environment } from '../../../../../environments/environment';

describe('OrderDetailComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ orderId: 'order-1' }) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should render the full order detail', async () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/orders/order-1`);
    req.flush({
      id: 'order-1',
      orderNumber: '#ABCD1234',
      date: '2026-01-01',
      totalInCents: 1000,
      status: 'Expédiée',
      trackingNumber: 'TRACK123',
      shippingAddress: { id: 'addr-1', street: '1 Rue de Paris', city: 'Paris', postalCode: '75001', country: 'France' },
      items: [{ productName: 'T-shirt', unitPriceInCents: 1000, quantity: 1 }],
    });

    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('#ABCD1234');
    expect(compiled.textContent).toContain('T-shirt');
    expect(compiled.textContent).toContain('TRACK123');
    expect(compiled.textContent).toContain('Paris');
  });
});
