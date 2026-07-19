import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AccountStore } from './account.store';
import { ToastService } from '../../core/services/toast.service';
import { environment } from '../../../environments/environment';

describe('AccountStore', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should load the profile', async () => {
    const store = TestBed.inject(AccountStore);

    const loadPromise = store.loadProfile();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`);
    expect(req.request.method).toBe('GET');
    req.flush({ name: 'Alice', email: 'alice@example.com', addresses: [] });

    await loadPromise;

    expect(store.profile()).toEqual({ name: 'Alice', email: 'alice@example.com', addresses: [] });
  });

  it('should update the profile and show a toast on success', async () => {
    const store = TestBed.inject(AccountStore);
    const toastService = TestBed.inject(ToastService);
    spyOn(toastService, 'show');

    const updatePromise = store.updateProfile('Alice Updated', 'alice@example.com', null);

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ name: 'Alice Updated', email: 'alice@example.com', currentPassword: null });
    req.flush({ name: 'Alice Updated', email: 'alice@example.com', addresses: [] });

    const result = await updatePromise;

    expect(result).toBe(true);
    expect(toastService.show).toHaveBeenCalledWith('Profil mis à jour');
  });

  it('should surface the backend error message on a failed update', async () => {
    const store = TestBed.inject(AccountStore);

    const updatePromise = store.updateProfile('Alice', 'alice-new@example.com', 'wrong-password');

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`);
    req.flush({ errors: ['Mot de passe actuel incorrect.'] }, { status: 400, statusText: 'Bad Request' });

    const result = await updatePromise;

    expect(result).toBe(false);
    expect(store.error()).toBe('Mot de passe actuel incorrect.');
  });
});
