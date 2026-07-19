import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { ForgotPasswordComponent } from './forgot-password.component';
import { environment } from '../../../../../environments/environment';

describe('ForgotPasswordComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(ForgotPasswordComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should show inline error after blur on an invalid email', () => {
    const fixture = TestBed.createComponent(ForgotPasswordComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component['form'].controls.email.markAsTouched();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('#email-error')).toBeTruthy();
  });

  it('should show the neutral confirmation message after a successful submit', async () => {
    const fixture = TestBed.createComponent(ForgotPasswordComponent);
    const component = fixture.componentInstance;

    component['form'].setValue({ email: 'alice@example.com' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/forgot-password`);
    expect(req.request.method).toBe('POST');
    req.flush(null);

    await submitPromise;
    fixture.detectChanges();

    expect(component['submitted']()).toBe(true);
  });

  it('should show the same neutral confirmation message even for an email the backend has never heard of', async () => {
    const fixture = TestBed.createComponent(ForgotPasswordComponent);
    const component = fixture.componentInstance;

    component['form'].setValue({ email: 'unknown@example.com' });

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/forgot-password`);
    req.flush(null);

    await submitPromise;

    expect(component['submitted']()).toBe(true);
  });
});
