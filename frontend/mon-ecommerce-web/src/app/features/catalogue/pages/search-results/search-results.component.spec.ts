import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { SearchResultsComponent } from './search-results.component';
import { environment } from '../../../../../environments/environment';
import { expectNoAccessibilityViolations } from '../../../../core/testing/axe-helper';

describe('SearchResultsComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SearchResultsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap({ q: 'sac introuvable' })) } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function createAndLoad() {
    const fixture = TestBed.createComponent(SearchResultsComponent);
    fixture.detectChanges();

    httpMock.expectOne(`${environment.apiUrl}/api/v1/products/categories`).flush([]);
    httpMock
      .expectOne((req) => req.url === `${environment.apiUrl}/api/v1/products`)
      .flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20, totalPages: 0 });
    await fixture.whenStable();
    fixture.detectChanges();

    return fixture;
  }

  // Story 8.5, AC #2: "Réinitialiser les filtres" alongside the existing no-results message and
  // category suggestions.
  it('should show a "Réinitialiser les filtres" button that navigates back to /recherche with no query params', async () => {
    const fixture = await createAndLoad();
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Aucun résultat pour « sac introuvable »');
    const resetButton = Array.from(compiled.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Réinitialiser les filtres'),
    );
    expect(resetButton).toBeTruthy();

    resetButton?.click();

    expect(navigateSpy).toHaveBeenCalledWith(['/recherche']);
  });

  // Story 8.5, AC #7.
  it('should have no axe-core accessibility violations on the no-results state', async () => {
    const fixture = await createAndLoad();

    await expectNoAccessibilityViolations(fixture.nativeElement as HTMLElement);
  });
});
