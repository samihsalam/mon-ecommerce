import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';

import { CheckoutAddressComponent } from './checkout-address.component';
import { CheckoutStore } from '../../checkout.store';
import { environment } from '../../../../../environments/environment';

describe('CheckoutAddressComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CheckoutAddressComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should pre-fill from the account profile when no checkout address exists yet', async () => {
    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`);
    req.flush({
      name: 'Alice',
      email: 'alice@example.com',
      addresses: [{ id: 'addr-1', street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' }],
    });
    await fixture.whenStable();

    const component = fixture.componentInstance;
    expect(component['form'].controls.street.value).toBe('12 rue de la Paix');
    expect(component['form'].controls.city.value).toBe('Paris');
  });

  it('should leave the form empty when the account has no saved address', async () => {
    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`);
    req.flush({ name: 'Alice', email: 'alice@example.com', addresses: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component['form'].controls.street.value).toBe('');
    expect(component['initialized']()).toBe(true);
  });

  it('should prefer an existing CheckoutStore address over the account profile, and not fetch the profile at all', async () => {
    const checkoutStore = TestBed.inject(CheckoutStore);
    checkoutStore.setAddress({ street: '5 avenue Foch', city: 'Lyon', postalCode: '69001', country: 'France' });

    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    expect(component['form'].controls.city.value).toBe('Lyon');
    httpMock.expectNone(`${environment.apiUrl}/api/v1/account/profile`);
  });

  it('should show an inline error with aria-describedby when a required field is touched and empty', async () => {
    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`).flush({
      name: 'Alice',
      email: 'alice@example.com',
      addresses: [],
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component['form'].controls.street.markAsTouched();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('#street') as HTMLInputElement;
    const error = compiled.querySelector('#street-error');

    expect(input.getAttribute('aria-describedby')).toBe('street-error');
    expect(error?.textContent).toContain('requise');
  });

  it('should call CheckoutStore.setAddress and navigate to /checkout/livraison on valid submit', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);
    const checkoutStore = TestBed.inject(CheckoutStore);

    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`).flush({
      name: 'Alice',
      email: 'alice@example.com',
      addresses: [],
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component['form'].setValue({ street: '12 rue de la Paix', city: 'Paris', postalCode: '75002', country: 'France' });
    component['onSubmit']();

    expect(checkoutStore.address()).toEqual({
      street: '12 rue de la Paix',
      city: 'Paris',
      postalCode: '75002',
      country: 'France',
    });
    expect(navigateSpy).toHaveBeenCalledWith(['/checkout/livraison']);
  });

  it('should not navigate when the form is invalid on submit', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`).flush({
      name: 'Alice',
      email: 'alice@example.com',
      addresses: [],
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component['onSubmit']();

    expect(navigateSpy).not.toHaveBeenCalled();
    expect(component['form'].controls.street.touched).toBe(true);
  });

  it('should treat a whitespace-only field as invalid, not a valid empty-looking value', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`).flush({
      name: 'Alice',
      email: 'alice@example.com',
      addresses: [],
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component['form'].setValue({ street: '   ', city: 'Paris', postalCode: '75002', country: 'France' });
    component['onSubmit']();

    expect(component['form'].controls.street.invalid).toBe(true);
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('should show an error notice, and still leave the form usable, when the profile fetch fails', async () => {
    const fixture = TestBed.createComponent(CheckoutAddressComponent);
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/api/v1/account/profile`)
      .flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component['initialized']()).toBe(true);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[role="alert"]')).toBeTruthy();
    expect(compiled.querySelector('form')).toBeTruthy();
  });
});
