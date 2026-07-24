import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';

import { cartSessionInterceptor } from './cart-session.interceptor';
import { environment } from '../../../environments/environment';
import { CART_SESSION_ID_KEY } from '../constants/storage-keys';

describe('cartSessionInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([cartSessionInterceptor])), provideHttpClientTesting()],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should attach the stored session id header when present', async () => {
    localStorage.setItem(CART_SESSION_ID_KEY, 'session-abc');

    const promise = firstValueFrom(http.get(`${environment.apiUrl}/api/v1/cart`));
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`);
    expect(req.request.headers.get('X-Cart-Session-Id')).toBe('session-abc');
    req.flush({ items: [], totalInCents: 0 });

    await promise;
  });

  it('should generate and persist a new session id eagerly when none is stored yet', async () => {
    const promise = firstValueFrom(http.get(`${environment.apiUrl}/api/v1/cart`));
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`);
    const attachedId = req.request.headers.get('X-Cart-Session-Id');

    expect(attachedId).toBeTruthy();
    expect(localStorage.getItem(CART_SESSION_ID_KEY)).toBe(attachedId);

    req.flush({ items: [], totalInCents: 0 });
    await promise;
  });

  it('should reuse the same generated id for a second request instead of generating another one', async () => {
    const firstPromise = firstValueFrom(http.get(`${environment.apiUrl}/api/v1/cart`));
    const firstReq = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`);
    const firstId = firstReq.request.headers.get('X-Cart-Session-Id');
    firstReq.flush({ items: [], totalInCents: 0 });
    await firstPromise;

    const secondPromise = firstValueFrom(http.get(`${environment.apiUrl}/api/v1/cart`));
    const secondReq = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`);
    expect(secondReq.request.headers.get('X-Cart-Session-Id')).toBe(firstId);
    secondReq.flush({ items: [], totalInCents: 0 });
    await secondPromise;
  });

  it('should persist a session id the server generates and returns via the response header', async () => {
    const promise = firstValueFrom(http.get(`${environment.apiUrl}/api/v1/cart`));
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`);
    req.flush(
      { items: [], totalInCents: 0 },
      { headers: new HttpHeaders({ 'X-Cart-Session-Id': 'generated-session-xyz' }) },
    );

    await promise;

    expect(localStorage.getItem(CART_SESSION_ID_KEY)).toBe('generated-session-xyz');
  });

  it('should not rewrite storage when the response echoes the same session id already stored', async () => {
    localStorage.setItem(CART_SESSION_ID_KEY, 'session-abc');
    const setItemSpy = spyOn(Storage.prototype, 'setItem').and.callThrough();

    const promise = firstValueFrom(http.get(`${environment.apiUrl}/api/v1/cart`));
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`);
    req.flush({ items: [], totalInCents: 0 }, { headers: new HttpHeaders({ 'X-Cart-Session-Id': 'session-abc' }) });

    await promise;

    expect(setItemSpy).not.toHaveBeenCalledWith(CART_SESSION_ID_KEY, jasmine.anything());
  });
});
