import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ProductGalleryComponent } from '../../components/product-gallery/product-gallery.component';
import { extractProductIdFromSlug } from '../../product-url.util';
import { ProductDetailStore } from '../../product-detail.store';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [RouterLink, ProductGalleryComponent],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss',
})
export class ProductDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly productDetailStore = inject(ProductDetailStore);

  // Not GUID-shaped at all (malformed/hand-typed URL) is distinguished from "well-formed id, but
  // the API 404s" — both end up showing the same "not found" message, but this one is known up
  // front without waiting on a network round trip.
  protected readonly invalidSlug = signal(false);

  ngOnInit(): void {
    // Subscribes to paramMap rather than reading route.snapshot once — Angular's default
    // RouteReuseStrategy reuses this component instance (no ngOnInit re-run) when navigating
    // between two URLs matching the same route config with only the params differing (e.g. a
    // future "produits similaires" link from one product-detail page to another). A one-shot
    // snapshot read would keep showing the FIRST product's data in that case; this re-runs on
    // every param change, same pattern already established by SearchResultsComponent/
    // CatalogueComponent.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const productId = extractProductIdFromSlug(params.get('productSlug'));
      if (productId) {
        this.invalidSlug.set(false);
        void this.productDetailStore.loadProduct(productId);
      } else {
        this.invalidSlug.set(true);
      }
    });
  }

  protected formatPrice(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }
}
