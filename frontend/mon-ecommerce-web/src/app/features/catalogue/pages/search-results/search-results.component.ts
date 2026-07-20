import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { CatalogueStore } from '../../catalogue.store';
import { SearchBarComponent } from '../../components/search-bar/search-bar.component';

const MIN_TERM_LENGTH = 2;

@Component({
  selector: 'app-search-results',
  standalone: true,
  imports: [RouterLink, SearchBarComponent],
  templateUrl: './search-results.component.html',
  styleUrl: './search-results.component.scss',
})
export class SearchResultsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly catalogueStore = inject(CatalogueStore);

  protected readonly currentTerm = signal('');

  ngOnInit(): void {
    void this.catalogueStore.loadCategories();

    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const term = (params.get('q') ?? '').trim();
      this.currentTerm.set(term);

      if (term.length >= MIN_TERM_LENGTH) {
        void this.catalogueStore.search(term);
      }
    });
  }

  protected formatAmount(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }
}
