import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router, ActivatedRoute, convertToParamMap } from '@angular/router';

import { ResetPasswordComponent } from './reset-password.component';
import { environment } from '../../../../../environments/environment';

describe('ResetPasswordComponent', () => {
  let httpMock: HttpTestingController;

  function configure(params: Record<string, string> = { email: 'alice@example.com', token: 'reset-token' }) {
    TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(params),
            },
          },
        },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    configure();
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should show a mismatch error when the two password fields differ', () => {
    configure();
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component['form'].setValue({ newPassword: 'newpassword1', confirmPassword: 'different1' });
    component['form'].controls.confirmPassword.markAsTouched();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('#confirmPassword-error')).toBeTruthy();
  });

  it('should call the reset-password endpoint and navigate to /connexion on valid submit', async () => {
    configure();
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    component['form'].setValue({ newPassword: 'newpassword1', confirmPassword: 'newpassword1' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/reset-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'alice@example.com', token: 'reset-token', newPassword: 'newpassword1' });
    req.flush(null);

    await submitPromise;

    expect(router.navigate).toHaveBeenCalledWith(['/connexion']);
  });

  it('should show an invalid-or-expired error on a 400 response', async () => {
    configure();
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    const component = fixture.componentInstance;

    component['form'].setValue({ newPassword: 'newpassword1', confirmPassword: 'newpassword1' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/reset-password`);
    req.flush({ errors: ['Ce lien de réinitialisation est invalide ou a expiré.'] }, { status: 400, statusText: 'Bad Request' });

    await submitPromise;

    expect(component['authStore'].error()).toBe('Ce lien de réinitialisation est invalide ou a expiré.');
  });

  it('should show an invalid-link message and no form when email/token are missing from the URL', () => {
    configure({});
    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Ce lien de réinitialisation est invalide ou incomplet.');
    expect(compiled.querySelector('form')).toBeFalsy();
  });
});
