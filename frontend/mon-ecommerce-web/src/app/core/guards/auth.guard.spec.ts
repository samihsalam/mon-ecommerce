import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { RouterStateSnapshot, ActivatedRouteSnapshot } from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthStore } from '../../features/auth/auth.store';
import { ACCESS_TOKEN_KEY } from '../constants/storage-keys';

describe('authGuard', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  afterEach(() => localStorage.clear());

  function runGuard(url: string) {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );
  }

  it('should allow access when authenticated', () => {
    localStorage.setItem(ACCESS_TOKEN_KEY, 'a-token');
    TestBed.inject(AuthStore);

    const result = runGuard('/compte');

    expect(result).toBe(true);
  });

  it('should redirect to /connexion with a returnUrl when not authenticated', () => {
    TestBed.inject(AuthStore);
    const router = TestBed.inject(Router);

    const result = runGuard('/compte') as UrlTree;

    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result)).toBe('/connexion?returnUrl=%2Fcompte');
  });
});
