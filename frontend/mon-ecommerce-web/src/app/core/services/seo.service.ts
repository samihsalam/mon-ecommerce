import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

import type { ProductDetail } from '../../features/catalogue/product-detail.store';

const MAX_DESCRIPTION_LENGTH = 160;

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly document = inject(DOCUMENT);
  private readonly titleService = inject(Title);
  private readonly meta = inject(Meta);

  // Held across calls (not re-queried by attribute each time) so repeated client-side navigation
  // between product pages updates the SAME <script>/<link> elements instead of appending a new
  // one per visit. On SSR this starts fresh every request (a new service instance per request),
  // which is exactly what's wanted — no cross-request state leakage.
  private jsonLdScript: HTMLScriptElement | null = null;
  private canonicalLink: HTMLLinkElement | null = null;

  setProductSeo(product: ProductDetail, canonicalUrl: string): void {
    const title = `${product.name} | MonEcommerce`;
    const description = this.truncateDescription(product.description);
    const image = product.imageUrls[0];
    const price = (product.priceInCents / 100).toFixed(2);

    this.titleService.setTitle(title);
    this.meta.updateTag({ name: 'description', content: description });

    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    // An empty og:image content is worse than no tag at all — per the OG spec og:image must be a
    // valid absolute URL, and link-preview generators render a broken-image icon for an empty
    // string rather than falling back gracefully the way they do for a missing tag. Also removes
    // any stale og:image left over from a PREVIOUS product (this service's <meta> tags persist
    // across client-side navigations) if the new product has no image while the last one did.
    if (image) {
      this.meta.updateTag({ property: 'og:image', content: image });
    } else {
      this.meta.removeTag('property="og:image"');
    }
    this.meta.updateTag({ property: 'og:type', content: 'product' });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    // og:price isn't a real Open Graph property — Facebook's own e-commerce OG extension uses
    // product:price:amount/product:price:currency, which is what any real consumer (link preview
    // generators, crawlers) actually reads. See Story 3.6's Dev Notes.
    this.meta.updateTag({ property: 'product:price:amount', content: price });
    this.meta.updateTag({ property: 'product:price:currency', content: 'EUR' });

    this.setCanonicalLink(canonicalUrl);
    this.setJsonLd(product, canonicalUrl, price);
  }

  private truncateDescription(description: string): string {
    if (description.length <= MAX_DESCRIPTION_LENGTH) {
      return description;
    }
    return description.slice(0, MAX_DESCRIPTION_LENGTH - 3).trimEnd() + '...';
  }

  private setCanonicalLink(url: string): void {
    if (!this.canonicalLink) {
      this.canonicalLink = this.document.createElement('link');
      this.canonicalLink.setAttribute('rel', 'canonical');
      this.document.head.appendChild(this.canonicalLink);
    }
    this.canonicalLink.setAttribute('href', url);
  }

  private setJsonLd(product: ProductDetail, canonicalUrl: string, price: string): void {
    const schema = {
      '@context': 'https://schema.org',
      '@type': 'Product',
      name: product.name,
      description: product.description,
      image: product.imageUrls,
      url: canonicalUrl,
      offers: {
        '@type': 'Offer',
        priceCurrency: 'EUR',
        price,
        availability: product.inStock ? 'https://schema.org/InStock' : 'https://schema.org/OutOfStock',
      },
    };

    if (!this.jsonLdScript) {
      this.jsonLdScript = this.document.createElement('script');
      this.jsonLdScript.type = 'application/ld+json';
      this.document.head.appendChild(this.jsonLdScript);
    }
    this.jsonLdScript.textContent = JSON.stringify(schema);
  }
}
