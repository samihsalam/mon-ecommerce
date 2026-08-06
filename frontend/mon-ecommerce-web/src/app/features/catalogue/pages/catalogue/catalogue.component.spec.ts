import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { CatalogueComponent } from './catalogue.component';
import { ProductSummary } from '../../catalogue.store';
import { environment } from '../../../../../environments/environment';
import { expectNoAccessibilityViolations } from '../../../../core/testing/axe-helper';

describe('CatalogueComponent', () => {
  let httpMock: HttpTestingController;

  const cannedProduct: ProductSummary = {
    id: 'product-1',
    name: 'Tote Parisienne',
    priceInCents: 15000,
    material: 'Cuir',
    color: 'Camel',
    imageUrl: 'https://cdn.example.com/tote.webp',
    categoryId: 'cat-1',
    categoryName: 'Sacs',
    categorySlug: 'sacs',
    inStock: true,
  };

  function configure(queryParams: Record<string, string>): void {
    TestBed.configureTestingModule({
      imports: [CatalogueComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap(queryParams)) } },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  async function createAndLoad(items: ProductSummary[] = []) {
    const fixture = TestBed.createComponent(CatalogueComponent);
    fixture.detectChanges();

    httpMock.expectOne(`${environment.apiUrl}/api/v1/products/categories`).flush([
      { id: 'cat-1', name: 'Sacs', slug: 'sacs' },
      { id: 'cat-2', name: 'Ceintures', slug: 'ceintures' },
    ]);
    httpMock
      .expectOne((req) => req.url === `${environment.apiUrl}/api/v1/products`)
      .flush({ items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 });
    await fixture.whenStable();
    fixture.detectChanges();

    return fixture;
  }

  describe('with no category filter active', () => {
    beforeEach(() => configure({}));

    // Review finding: with no filter active there is nothing for the button to reset, so it
    // must not be shown — only the category suggestions.
    it('should show the empty state WITHOUT a reset button, but with category suggestions', async () => {
      const fixture = await createAndLoad();

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Aucun produit dans cette catégorie.');
      expect(compiled.textContent).toContain('Sacs');
      const resetButton = Array.from(compiled.querySelectorAll('button')).find((b) =>
        b.textContent?.includes('Réinitialiser les filtres'),
      );
      expect(resetButton).toBeFalsy();
    });

    it('should have no axe-core accessibility violations on the empty state', async () => {
      const fixture = await createAndLoad();

      await expectNoAccessibilityViolations(fixture.nativeElement as HTMLElement);
    });

    it('should have no axe-core accessibility violations on the populated product grid', async () => {
      const fixture = await createAndLoad([cannedProduct]);

      await expectNoAccessibilityViolations(fixture.nativeElement as HTMLElement);
    });
  });

  describe('with a category filter active', () => {
    beforeEach(() => configure({ categoryId: 'cat-1' }));

    it('should show the reset button, and exclude the active category from suggestions', async () => {
      const fixture = await createAndLoad();

      const compiled = fixture.nativeElement as HTMLElement;
      const resetButton = Array.from(compiled.querySelectorAll('button')).find((b) =>
        b.textContent?.includes('Réinitialiser les filtres'),
      );
      expect(resetButton).toBeTruthy();

      // Scoped to the suggestion links specifically — FilterChipBarComponent separately renders
      // every category (including the active "Sacs" one) as a filter chip elsewhere on the page,
      // so asserting against the whole page's textContent would be a false negative for "Sacs".
      const suggestionNames = Array.from(compiled.querySelectorAll('a[href*="categoryId"]')).map((a) =>
        a.textContent?.trim(),
      );
      expect(suggestionNames).toContain('Ceintures');
      expect(suggestionNames).not.toContain('Sacs');
    });
  });
});
