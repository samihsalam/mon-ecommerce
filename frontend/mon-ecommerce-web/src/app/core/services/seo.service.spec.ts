import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { Meta, Title } from '@angular/platform-browser';

import { SeoService } from './seo.service';
import type { ProductDetail } from '../../features/catalogue/product-detail.store';

describe('SeoService', () => {
  const product: ProductDetail = {
    id: 'product-1',
    name: 'Tote Parisienne',
    description: 'Un sac en cuir cognac fabriqué à la main, parfait pour un usage quotidien.',
    priceInCents: 28500,
    material: 'Cuir',
    color: 'Cognac',
    dimensions: '30x20x10cm',
    stockQuantity: 3,
    inStock: true,
    categoryId: 'cat-1',
    categoryName: 'Sacs',
    categorySlug: 'sacs',
    imageUrls: ['https://cdn.example.com/a.webp', 'https://cdn.example.com/b.webp'],
  };

  const canonicalUrl = 'https://monecommerce.fr/catalogue/sacs/tote-parisienne-product-1';

  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  afterEach(() => {
    // document.head is the REAL browser DOM, not reset between tests by TestBed the way the
    // component tree is — each test's SeoService starts with a fresh in-memory canonicalLink/
    // jsonLdScript reference (null), so without this cleanup, every test's setProductSeo() call
    // appends ANOTHER tag to the same shared head, and querySelector (which returns the FIRST
    // match in document order) would pick up a stale tag from an earlier test.
    document.querySelectorAll('link[rel="canonical"], script[type="application/ld+json"]').forEach((el) => el.remove());
  });

  it('should set the page title and meta description', () => {
    const service = TestBed.inject(SeoService);
    const titleService = TestBed.inject(Title);

    service.setProductSeo(product, canonicalUrl);

    expect(titleService.getTitle()).toBe('Tote Parisienne | MonEcommerce');
    const meta = TestBed.inject(Meta);
    expect(meta.getTag('name="description"')?.content).toContain('Un sac en cuir cognac');
  });

  it('should set Open Graph and product price meta tags', () => {
    const service = TestBed.inject(SeoService);
    const meta = TestBed.inject(Meta);

    service.setProductSeo(product, canonicalUrl);

    expect(meta.getTag('property="og:title"')?.content).toBe('Tote Parisienne | MonEcommerce');
    expect(meta.getTag('property="og:image"')?.content).toBe('https://cdn.example.com/a.webp');
    expect(meta.getTag('property="og:type"')?.content).toBe('product');
    expect(meta.getTag('property="og:url"')?.content).toBe(canonicalUrl);
    expect(meta.getTag('property="product:price:amount"')?.content).toBe('285.00');
    expect(meta.getTag('property="product:price:currency"')?.content).toBe('EUR');
  });

  it('should set a canonical link tag', () => {
    const service = TestBed.inject(SeoService);
    const document = TestBed.inject(DOCUMENT);

    service.setProductSeo(product, canonicalUrl);

    const link = document.querySelector('link[rel="canonical"]');
    expect(link?.getAttribute('href')).toBe(canonicalUrl);
  });

  it('should inject a JSON-LD Product schema script tag', () => {
    const service = TestBed.inject(SeoService);
    const document = TestBed.inject(DOCUMENT);

    service.setProductSeo(product, canonicalUrl);

    const script = document.querySelector('script[type="application/ld+json"]');
    expect(script).toBeTruthy();
    const schema = JSON.parse(script!.textContent!);
    expect(schema['@type']).toBe('Product');
    expect(schema.name).toBe('Tote Parisienne');
    expect(schema.offers.price).toBe('285.00');
    expect(schema.offers.availability).toBe('https://schema.org/InStock');
  });

  it('should reuse (not duplicate) the canonical link and JSON-LD script on a second call', () => {
    const service = TestBed.inject(SeoService);
    const document = TestBed.inject(DOCUMENT);

    service.setProductSeo(product, canonicalUrl);
    service.setProductSeo({ ...product, name: 'Autre Produit' }, canonicalUrl);

    expect(document.querySelectorAll('link[rel="canonical"]').length).toBe(1);
    expect(document.querySelectorAll('script[type="application/ld+json"]').length).toBe(1);
  });

  it('should mark an out-of-stock product as OutOfStock in the JSON-LD schema', () => {
    const service = TestBed.inject(SeoService);
    const document = TestBed.inject(DOCUMENT);

    service.setProductSeo({ ...product, inStock: false }, canonicalUrl);

    const script = document.querySelector('script[type="application/ld+json"]');
    const schema = JSON.parse(script!.textContent!);
    expect(schema.offers.availability).toBe('https://schema.org/OutOfStock');
  });

  it('should truncate a long description for the meta tag but not for the JSON-LD schema', () => {
    const service = TestBed.inject(SeoService);
    const meta = TestBed.inject(Meta);
    const document = TestBed.inject(DOCUMENT);
    const longDescription = 'A'.repeat(200);

    service.setProductSeo({ ...product, description: longDescription }, canonicalUrl);

    expect(meta.getTag('name="description"')!.content.length).toBeLessThanOrEqual(160);
    expect(meta.getTag('name="description"')!.content.endsWith('...')).toBe(true);

    const script = document.querySelector('script[type="application/ld+json"]');
    const schema = JSON.parse(script!.textContent!);
    expect(schema.description).toBe(longDescription);
  });
});
