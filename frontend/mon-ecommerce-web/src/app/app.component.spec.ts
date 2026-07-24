import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AppComponent } from './app.component';
import { environment } from '../environments/environment';

describe('AppComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  // CartStore (injected transitively via HeaderComponent/CartDrawerComponent) fires an automatic
  // GET /api/v1/cart on construction — flush it so it doesn't linger as an unresolved request.
  function flushInitialCartLoad(): void {
    httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`).flush({ items: [], totalInCents: 0 });
  }

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
    flushInitialCartLoad();
  });

  it('should render the router outlet, header, toast, and cart-drawer mount points', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    flushInitialCartLoad();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
    expect(compiled.querySelector('app-toast')).toBeTruthy();
    expect(compiled.querySelector('app-header')).toBeTruthy();
    expect(compiled.querySelector('app-cart-drawer')).toBeTruthy();
  });
});
