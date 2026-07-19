import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';

import { RegisterComponent } from './register.component';
import { environment } from '../../../../../environments/environment';

describe('RegisterComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should show inline errors after blur on invalid fields', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component['form'].controls.email.markAsTouched();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('#email-error')).toBeTruthy();
  });

  it('should call the register endpoint and navigate on valid submit', async () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    component['form'].setValue({ name: 'Alice', email: 'alice@example.com', password: 'password123' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/register`);
    expect(req.request.method).toBe('POST');
    req.flush({ accessToken: 'a', refreshToken: 'b', expiresAt: '2026-01-01' });

    await submitPromise;

    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should show duplicate-email error on 409 response', async () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    const component = fixture.componentInstance;

    component['form'].setValue({ name: 'Alice', email: 'alice@example.com', password: 'password123' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/register`);
    req.flush({ detail: 'Conflict' }, { status: 409, statusText: 'Conflict' });

    await submitPromise;

    expect(component['authStore'].error()).toBe('Un compte existe déjà avec cet email.');
  });
});
