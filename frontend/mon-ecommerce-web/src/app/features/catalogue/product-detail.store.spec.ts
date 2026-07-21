import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ProductDetailStore } from './product-detail.store';
import { environment } from '../../../environments/environment';

describe('ProductDetailStore', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  const cannedProduct = {
    id: 'product-1',
    name: 'Tote Parisienne',
    description: 'Un sac en cuir.',
    priceInCents: 28500,
    material: 'Cuir',
    color: 'Cognac',
    dimensions: '30x20x10cm',
    stockQuantity: 3,
    inStock: true,
    categoryId: 'cat-1',
    categoryName: 'Sacs',
    categorySlug: 'sacs',
    imageUrls: ['a.webp', 'b.webp'],
  };

  it('should load the product detail', async () => {
    const store = TestBed.inject(ProductDetailStore);

    const loadPromise = store.loadProduct('product-1');

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/products/product-1`);
    expect(req.request.method).toBe('GET');
    req.flush(cannedProduct);

    await loadPromise;

    expect(store.product()?.name).toBe('Tote Parisienne');
    expect(store.isLoading()).toBe(false);
    expect(store.error()).toBeNull();
  });

  it('should show an error when the product fails to load', async () => {
    const store = TestBed.inject(ProductDetailStore);

    const loadPromise = store.loadProduct('missing-id');

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/products/missing-id`);
    req.flush(null, { status: 404, statusText: 'Not Found' });

    await loadPromise;

    expect(store.error()).toBe("Ce produit est introuvable ou n'est plus disponible.");
    expect(store.product()).toBeNull();
  });

  it('should clear the previous product and similar-products list when loading a new one', async () => {
    const store = TestBed.inject(ProductDetailStore);

    const firstLoad = store.loadProduct('product-1');
    httpMock.expectOne(`${environment.apiUrl}/api/v1/products/product-1`).flush(cannedProduct);
    await firstLoad;

    const secondLoad = store.loadProduct('product-2');
    // Before the second request resolves, the store should already have cleared the first
    // product's data (loadProduct() clears synchronously before its own await) — regression
    // coverage for the stale-data-flash bug class fixed in Story 3.5's review.
    expect(store.product()).toBeNull();

    httpMock.expectOne(`${environment.apiUrl}/api/v1/products/product-2`).flush({ ...cannedProduct, id: 'product-2' });
    await secondLoad;

    expect(store.product()?.id).toBe('product-2');
  });

  it('should load similar products', async () => {
    const store = TestBed.inject(ProductDetailStore);

    const loadPromise = store.loadSimilarProducts('product-1');

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/products/product-1/similar`);
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        id: 'product-2',
        name: 'Sac Similaire',
        priceInCents: 19900,
        material: null,
        color: null,
        imageUrl: null,
        categoryId: 'cat-1',
        categoryName: 'Sacs',
        categorySlug: 'sacs',
        inStock: true,
      },
    ]);

    await loadPromise;

    expect(store.similarProducts().length).toBe(1);
    expect(store.similarProducts()[0].name).toBe('Sac Similaire');
  });

  it('should fail silently (empty list) when similar products fail to load', async () => {
    const store = TestBed.inject(ProductDetailStore);

    const loadPromise = store.loadSimilarProducts('product-1');

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/products/product-1/similar`);
    req.flush(null, { status: 500, statusText: 'Server Error' });

    await loadPromise;

    expect(store.similarProducts()).toEqual([]);
    // A failed "similar products" fetch must not surface as the page's main error state.
    expect(store.error()).toBeNull();
  });
});
