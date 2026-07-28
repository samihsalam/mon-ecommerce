import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { CheckoutConfirmationComponent } from './checkout-confirmation.component';
import { CheckoutStore, OrderConfirmationResult, OrderDetail } from '../../checkout.store';

describe('CheckoutConfirmationComponent', () => {
  let getOrderSpy: jasmine.Spy<(paymentIntentId: string) => Promise<OrderConfirmationResult>>;

  const cannedOrder: OrderDetail = {
    id: 'order-1',
    orderNumber: '#ABC12345',
    date: '2026-07-28T00:00:00Z',
    totalInCents: 5490,
    status: 'En préparation',
    trackingNumber: null,
    shippingAddress: { id: 'addr-1', street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' },
    items: [{ productName: 'Chaise Scandinave', unitPriceInCents: 5000, quantity: 1 }],
  };

  function createWithQueryParam(paymentIntent: string | null): ComponentFixture<CheckoutConfirmationComponent> {
    TestBed.configureTestingModule({
      imports: [CheckoutConfirmationComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap(paymentIntent ? { payment_intent: paymentIntent } : {}) },
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(CheckoutConfirmationComponent);
    // Spy before detectChanges() — ngOnInit (which calls this) only runs on the first
    // detectChanges(), so the store instance must already be wired to a spy beforehand.
    getOrderSpy = spyOn(TestBed.inject(CheckoutStore), 'getOrderByPaymentIntent');
    return fixture;
  }

  it('should show a clear message and never poll when the payment_intent query param is missing', async () => {
    const fixture = createWithQueryParam(null);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(getOrderSpy).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain("Impossible d'identifier votre paiement");
  });

  it('should render the order summary as soon as the first poll finds it', async () => {
    const fixture = createWithQueryParam('pi_abc');
    getOrderSpy.and.resolveTo({ status: 'found', order: cannedOrder });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getOrderSpy).toHaveBeenCalledWith('pi_abc');
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('#ABC12345');
    expect(text).toContain('Chaise Scandinave');
    expect(text).toContain('Paris');
  });

  it('should show the stock-unavailable message and stop polling when the payment was refunded', async () => {
    const fixture = createWithQueryParam('pi_refunded');
    getOrderSpy.and.resolveTo({ status: 'refunded' });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getOrderSpy).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Le stock était insuffisant');
  });

  it('should poll again after a "pending" result and render the order once it is found', fakeAsync(() => {
    const fixture = createWithQueryParam('pi_pending_then_found');
    getOrderSpy.and.returnValues(
      Promise.resolve({ status: 'pending' }),
      Promise.resolve({ status: 'found', order: cannedOrder }),
    );

    fixture.detectChanges(); // triggers ngOnInit -> first poll attempt (pending)
    tick(); // flush the first promise
    tick(2000); // advance past the poll interval, triggering the second attempt
    tick(); // flush the second promise
    fixture.detectChanges();

    expect(getOrderSpy).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('#ABC12345');
  }));
});
