import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { authInterceptor } from './auth.interceptor';
import { environment } from '../../../environments/environment';
import { ACCESS_TOKEN_KEY, REFRESH_TOKEN_KEY } from '../constants/storage-keys';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem(ACCESS_TOKEN_KEY, 'stale-access-token');
    localStorage.setItem(REFRESH_TOKEN_KEY, 'valid-refresh-token');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should refresh and retry the original request on a 401', fakeAsync(() => {
    let result: unknown;
    firstValueFrom(http.get(`${environment.apiUrl}/api/v1/orders`)).then((r) => (result = r));

    const original = httpMock.expectOne(`${environment.apiUrl}/api/v1/orders`);
    expect(original.request.headers.get('Authorization')).toBe('Bearer stale-access-token');
    original.flush(null, { status: 401, statusText: 'Unauthorized' });
    tick();

    const refreshReq = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/refresh`);
    refreshReq.flush({ accessToken: 'new-access-token', refreshToken: 'new-refresh-token', expiresAt: '2026-01-01' });
    tick();

    const retried = httpMock.expectOne(`${environment.apiUrl}/api/v1/orders`);
    expect(retried.request.headers.get('Authorization')).toBe('Bearer new-access-token');
    retried.flush({ ok: true });
    tick();

    expect(result).toEqual({ ok: true });
  }));

  it('should redirect to /connexion and propagate the error when refresh fails', fakeAsync(() => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    let rejected = false;
    firstValueFrom(http.get(`${environment.apiUrl}/api/v1/orders`)).catch(() => (rejected = true));

    const original = httpMock.expectOne(`${environment.apiUrl}/api/v1/orders`);
    original.flush(null, { status: 401, statusText: 'Unauthorized' });
    tick();

    const refreshReq = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/refresh`);
    refreshReq.flush({ errors: ['Refresh token invalide ou expiré.'] }, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(rejected).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(['/connexion'], jasmine.any(Object));
  }));

  it('should not attempt a refresh when the 401 comes from an auth endpoint itself', fakeAsync(() => {
    let rejected = false;
    firstValueFrom(http.post(`${environment.apiUrl}/api/v1/auth/login`, { email: 'a@b.com', password: 'x' })).catch(
      () => (rejected = true),
    );

    const loginReq = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/login`);
    loginReq.flush({ errors: ['Email ou mot de passe incorrect.'] }, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(rejected).toBe(true);
    httpMock.expectNone(`${environment.apiUrl}/api/v1/auth/refresh`);
  }));

  it('should not attempt a refresh when a 401 comes from the logout call itself', fakeAsync(() => {
    let rejected = false;
    firstValueFrom(http.post(`${environment.apiUrl}/api/v1/auth/logout`, { refreshToken: 'valid-refresh-token' })).catch(
      () => (rejected = true),
    );

    const logoutReq = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/logout`);
    logoutReq.flush(null, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(rejected).toBe(true);
    httpMock.expectNone(`${environment.apiUrl}/api/v1/auth/refresh`);
  }));
});
