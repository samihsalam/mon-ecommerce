import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { ProductDetailComponent } from './product-detail.component';
import { ProductDetail } from '../../product-detail.store';
import { environment } from '../../../../../environments/environment';
import { expectNoAccessibilityViolations } from '../../../../core/testing/axe-helper';

describe('ProductDetailComponent', () => {
  let httpMock: HttpTestingController;

  const productId = '11111111-1111-1111-1111-111111111111';

  const cannedProduct: ProductDetail = {
    id: productId,
    name: 'Tote Parisienne',
    description: 'Un sac élégant et intemporel.',
    priceInCents: 15000,
    material: 'Cuir',
    color: 'Camel',
    dimensions: '30x20x10cm',
    stockQuantity: 5,
    inStock: true,
    categoryId: 'cat-1',
    categoryName: 'Sacs',
    categorySlug: 'sacs',
    imageUrls: ['https://cdn.example.com/tote.webp'],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ productSlug: `tote-parisienne-${productId}` })) },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function createAndLoad() {
    const fixture = TestBed.createComponent(ProductDetailComponent);
    fixture.detectChanges();

    httpMock.expectOne(`${environment.apiUrl}/api/v1/products/${productId}`).flush(cannedProduct);
    httpMock.expectOne(`${environment.apiUrl}/api/v1/products/${productId}/similar`).flush([]);
    // loadProduct()/loadSimilarProducts() are fire-and-forget from ngOnInit (no promise the test
    // can await directly) — whenStable() lets their pending `await firstValueFrom(...)` continue
    // past the flush() before the next detectChanges() reads the now-updated signals.
    await fixture.whenStable();

    // StickyAddToCartComponent (behind an @if on the now-loaded product) only gets instantiated
    // after this next change-detection pass — which is also when its injected CartStore fires its
    // own auto GET /cart on construction, same as every other spec rendering it.
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/api/v1/cart`).flush({ items: [], totalInCents: 0 });
    await fixture.whenStable();
    fixture.detectChanges();

    return fixture;
  }

  it('should render the product name and price once loaded', async () => {
    const fixture = await createAndLoad();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Tote Parisienne');
    expect(text).toContain('150.00 €');
  });

  // Story 8.5, AC #7.
  it('should have no axe-core accessibility violations', async () => {
    const fixture = await createAndLoad();

    await expectNoAccessibilityViolations(fixture.nativeElement as HTMLElement);
  });
});
