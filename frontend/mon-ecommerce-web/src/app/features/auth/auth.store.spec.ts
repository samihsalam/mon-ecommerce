import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AuthStore } from './auth.store';
import { environment } from '../../../environments/environment';
import { ACCESS_TOKEN_KEY, REFRESH_TOKEN_KEY } from '../../core/constants/storage-keys';

describe('AuthStore', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should start authenticated if an access token is already in localStorage', () => {
    localStorage.setItem(ACCESS_TOKEN_KEY, 'existing-token');
    const store = TestBed.inject(AuthStore);

    expect(store.isAuthenticated()).toBe(true);
  });

  it('should start unauthenticated if no access token is in localStorage', () => {
    const store = TestBed.inject(AuthStore);

    expect(store.isAuthenticated()).toBe(false);
  });

  it('should clear tokens and mark unauthenticated on logout, even if the API call fails', async () => {
    localStorage.setItem(ACCESS_TOKEN_KEY, 'a');
    localStorage.setItem(REFRESH_TOKEN_KEY, 'b');
    const store = TestBed.inject(AuthStore);

    const logoutPromise = store.logout();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/logout`);
    req.flush(null, { status: 500, statusText: 'Server Error' });

    await logoutPromise;

    expect(localStorage.getItem(ACCESS_TOKEN_KEY)).toBeNull();
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('should dedupe concurrent refresh() calls into a single HTTP request', async () => {
    localStorage.setItem(REFRESH_TOKEN_KEY, 'old-refresh-token');
    const store = TestBed.inject(AuthStore);

    const [first, second] = [store.refresh(), store.refresh()];

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/refresh`);
    req.flush({ accessToken: 'new-access', refreshToken: 'new-refresh', expiresAt: '2026-01-01' });

    const [firstResult, secondResult] = await Promise.all([first, second]);

    expect(firstResult).toBe(true);
    expect(secondResult).toBe(true);
    expect(localStorage.getItem(ACCESS_TOKEN_KEY)).toBe('new-access');
  });

  it('should clear tokens when refresh() fails', async () => {
    localStorage.setItem(ACCESS_TOKEN_KEY, 'stale-access');
    localStorage.setItem(REFRESH_TOKEN_KEY, 'expired-refresh-token');
    const store = TestBed.inject(AuthStore);

    const refreshPromise = store.refresh();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/refresh`);
    req.flush({ errors: ['Refresh token invalide ou expiré.'] }, { status: 401, statusText: 'Unauthorized' });

    const result = await refreshPromise;

    expect(result).toBe(false);
    expect(localStorage.getItem(ACCESS_TOKEN_KEY)).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('should sync isAuthenticated when another tab clears the access token', () => {
    localStorage.setItem(ACCESS_TOKEN_KEY, 'existing-token');
    const store = TestBed.inject(AuthStore);
    expect(store.isAuthenticated()).toBe(true);

    window.dispatchEvent(new StorageEvent('storage', { key: ACCESS_TOKEN_KEY, newValue: null }));

    expect(store.isAuthenticated()).toBe(false);
  });

  it('should ignore storage events for unrelated keys', () => {
    localStorage.setItem(ACCESS_TOKEN_KEY, 'existing-token');
    const store = TestBed.inject(AuthStore);

    window.dispatchEvent(new StorageEvent('storage', { key: 'some-other-key', newValue: 'x' }));

    expect(store.isAuthenticated()).toBe(true);
  });
});
