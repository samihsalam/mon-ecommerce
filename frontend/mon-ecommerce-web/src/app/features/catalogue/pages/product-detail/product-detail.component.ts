import { Component, DestroyRef, effect, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { environment } from '../../../../../environments/environment';
import { SeoService } from '../../../../core/services/seo.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ProductCardComponent } from '../../components/product-card/product-card.component';
import { ProductGalleryComponent } from '../../components/product-gallery/product-gallery.component';
import type { ProductDetail } from '../../product-detail.store';
import { ProductDetailStore } from '../../product-detail.store';
import { buildProductUrl, extractProductIdFromSlug } from '../../product-url.util';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [RouterLink, ProductGalleryComponent, ProductCardComponent],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss',
})
export class ProductDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly seoService = inject(SeoService);
  private readonly toastService = inject(ToastService);
  protected readonly productDetailStore = inject(ProductDetailStore);

  // Not GUID-shaped at all (malformed/hand-typed URL) is distinguished from "well-formed id, but
  // the API 404s" — both end up showing the same "not found" message, but this one is known up
  // front without waiting on a network round trip.
  protected readonly invalidSlug = signal(false);

  constructor() {
    // Reacts to the product signal rather than being chained inline after loadProduct()'s await —
    // this is the one place SEO tags need to be (re-)applied for every successful load, regardless
    // of which call path populated the signal, so it doesn't belong duplicated into ngOnInit's
    // paramMap subscription callback. Title/Meta are SSR-safe by design, which is what actually
    // gets AC #2's tags into the crawled HTML.
    effect(() => {
      const product = this.productDetailStore.product();
      if (product) {
        this.seoService.setProductSeo(product, this.canonicalUrl(product));
      }
    });
  }

  ngOnInit(): void {
    // Subscribes to paramMap rather than reading route.snapshot once — Angular's default
    // RouteReuseStrategy reuses this component instance (no ngOnInit re-run) when navigating
    // between two URLs matching the same route config with only the params differing (e.g. a
    // "produits similaires" link from one product-detail page to another — exactly what this
    // story adds). A one-shot snapshot read would keep showing the FIRST product's data in that
    // case; this re-runs on every param change, same pattern already established by
    // SearchResultsComponent/CatalogueComponent.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const productId = extractProductIdFromSlug(params.get('productSlug'));
      if (productId) {
        this.invalidSlug.set(false);
        void this.productDetailStore.loadProduct(productId);
        void this.productDetailStore.loadSimilarProducts(productId);
      } else {
        this.invalidSlug.set(true);
      }
    });
  }

  protected formatPrice(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }

  protected canonicalUrl(product: ProductDetail): string {
    const path = buildProductUrl(product.categorySlug, product.id, product.name).join('/');
    return `${environment.siteUrl}${path}`;
  }

  protected async onShare(product: ProductDetail): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.canonicalUrl(product));
      this.toastService.show('Lien copié !');
    } catch {
      // Clipboard API can be unavailable (insecure context, permission denied, older browser) —
      // a distinct failure toast rather than silently doing nothing.
      this.toastService.show('Impossible de copier le lien.');
    }
  }
}
