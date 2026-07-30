import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';

import { ReturnRequestComponent } from './return-request.component';
import { environment } from '../../../../../environments/environment';

describe('ReturnRequestComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReturnRequestComponent],
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

  it('should not submit when the form is invalid', async () => {
    const fixture = TestBed.createComponent(ReturnRequestComponent);
    fixture.detectChanges();

    await fixture.componentInstance['onSubmit']();

    expect(fixture.componentInstance['form'].controls.reason.touched).toBe(true);
    httpMock.expectNone(`${environment.apiUrl}/api/v1/account/orders/order-1/returns`);
  });

  it('should submit and navigate back to the order on success', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const fixture = TestBed.createComponent(ReturnRequestComponent);
    fixture.detectChanges();

    fixture.componentInstance['form'].setValue({ reason: 'WrongSize', description: 'Trop petit.' });
    const submitPromise = fixture.componentInstance['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/orders/order-1/returns`);
    req.flush({ returnId: 'return-1', status: 'Pending' });
    await submitPromise;

    expect(navigateSpy).toHaveBeenCalledWith(['/compte/commandes', 'order-1']);
  });

  it('should show the backend error and not navigate on a 422', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const fixture = TestBed.createComponent(ReturnRequestComponent);
    fixture.detectChanges();

    fixture.componentInstance['form'].setValue({ reason: 'Other', description: 'desc' });
    const submitPromise = fixture.componentInstance['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/orders/order-1/returns`);
    req.flush({ detail: 'Fenêtre de retour expirée.' }, { status: 422, statusText: 'Unprocessable Entity' });
    await submitPromise;
    fixture.detectChanges();

    expect(navigateSpy).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Fenêtre de retour expirée.');
  });
});
