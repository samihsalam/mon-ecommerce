import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router, ActivatedRoute, convertToParamMap } from '@angular/router';

import { LoginComponent } from './login.component';
import { environment } from '../../../../../environments/environment';

describe('LoginComponent', () => {
  let httpMock: HttpTestingController;

  function configure(returnUrl?: string) {
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(returnUrl ? { returnUrl } : {}),
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
    const fixture = TestBed.createComponent(LoginComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should show inline errors after blur on invalid fields', () => {
    configure();
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component['form'].controls.email.markAsTouched();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('#email-error')).toBeTruthy();
  });

  it('should call the login endpoint and navigate to / by default on valid submit', async () => {
    configure();
    const fixture = TestBed.createComponent(LoginComponent);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    component['form'].setValue({ email: 'alice@example.com', password: 'password123' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush({ accessToken: 'a', refreshToken: 'b', expiresAt: '2026-01-01' });

    await submitPromise;

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('should navigate to the returnUrl query param when present', async () => {
    configure('/compte');
    const fixture = TestBed.createComponent(LoginComponent);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    component['form'].setValue({ email: 'alice@example.com', password: 'password123' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/login`);
    req.flush({ accessToken: 'a', refreshToken: 'b', expiresAt: '2026-01-01' });

    await submitPromise;

    expect(router.navigateByUrl).toHaveBeenCalledWith('/compte');
  });

  it('should show an invalid-credentials error on 401 response', async () => {
    configure();
    const fixture = TestBed.createComponent(LoginComponent);
    const component = fixture.componentInstance;

    component['form'].setValue({ email: 'alice@example.com', password: 'wrong-password' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/login`);
    req.flush({ errors: ['Email ou mot de passe incorrect.'] }, { status: 401, statusText: 'Unauthorized' });

    await submitPromise;

    expect(component['authStore'].error()).toBe('Email ou mot de passe incorrect.');
  });
});
