import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';

import { CatalogueStore } from '../../catalogue.store';
import { FilterChipBarComponent } from '../../components/filter-chip-bar/filter-chip-bar.component';
import { ProductCardComponent } from '../../components/product-card/product-card.component';
import { ProductCardSkeletonComponent } from '../../components/product-card-skeleton/product-card-skeleton.component';

const SKELETON_COUNT = 8;

@Component({
  selector: 'app-catalogue',
  standalone: true,
  imports: [FilterChipBarComponent, ProductCardComponent, ProductCardSkeletonComponent],
  templateUrl: './catalogue.component.html',
  styleUrl: './catalogue.component.scss',
})
export class CatalogueComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly catalogueStore = inject(CatalogueStore);

  protected readonly skeletonCount = Array.from({ length: SKELETON_COUNT });

  ngOnInit(): void {
    void this.catalogueStore.loadCategories();

    // The category filter lives in the URL (not just component/store state) so it survives a
    // hard refresh or a shared link, and so browser back/forward restores it when returning from
    // a product detail page — satisfies AC #5 without any extra state-preservation plumbing.
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      void this.catalogueStore.browse(params.get('categoryId'));
    });
  }

  protected onCategorySelected(categoryId: string | null): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { categoryId: categoryId ?? null },
      queryParamsHandling: 'merge',
    });
  }

  protected onClearAll(): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  protected loadMore(): void {
    void this.catalogueStore.browse(
      this.catalogueStore.activeCategoryId(),
      this.catalogueStore.pageNumber() + 1,
    );
  }

  protected resultsLabel(): string {
    const category = this.catalogueStore
      .categories()
      .find((c) => c.id === this.catalogueStore.activeCategoryId());
    const noun = category ? category.name.toLowerCase() : 'produits';
    return `${this.catalogueStore.totalCount()} ${noun} trouvés`;
  }
}
