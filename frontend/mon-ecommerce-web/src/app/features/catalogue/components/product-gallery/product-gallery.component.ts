import { Component, ElementRef, Input, OnChanges, signal, ViewChild } from '@angular/core';

type ImageStatus = 'loading' | 'loaded' | 'error';

@Component({
  selector: 'app-product-gallery',
  standalone: true,
  templateUrl: './product-gallery.component.html',
  styleUrl: './product-gallery.component.scss',
})
export class ProductGalleryComponent implements OnChanges {
  @Input({ required: true }) images!: string[];
  @Input({ required: true }) productName!: string;

  @ViewChild('scrollContainer') private readonly scrollContainer?: ElementRef<HTMLDivElement>;

  protected readonly activeIndex = signal(0);
  // Keyed by index rather than a single gallery-wide flag — each image can finish loading (or
  // fail) at a different time, so a single flag would hide the skeleton for images still loading
  // as soon as the FIRST one resolved. Backs the thumbnail column and the mobile strip, which
  // each render exactly one real <img> per index.
  protected readonly imageStatus = signal<ImageStatus[]>([]);
  // The desktop MAIN image is a SEPARATE <img> element from its same-index thumbnail — two
  // independent network requests for the same URL. It needs its own status, not a read of
  // imageStatus()[activeIndex()]: that value is only ever written by the thumbnail's own (load)/
  // (error), so the main image's skeleton/error state was previously driven by an element it
  // isn't even rendering (a lazy-loaded thumbnail can resolve well after or well before the main
  // image genuinely finishes).
  protected readonly mainImageStatus = signal<ImageStatus>('loading');

  ngOnChanges(): void {
    if (this.imageStatus().length !== this.images.length) {
      this.imageStatus.set(this.images.map(() => 'loading'));
    }
    // Not reachable via any path in this story today (nothing yet reuses this component instance
    // with a shrinking images array), but latent: without this clamp, a future caller that DOES
    // reuse the instance (e.g. a "related products" carousel) could leave activeIndex pointing
    // past the end of a shorter array, silently rendering images[activeIndex()] as undefined.
    if (this.activeIndex() >= this.images.length) {
      this.activeIndex.set(Math.max(this.images.length - 1, 0));
    }
  }

  protected altFor(index: number): string {
    // No per-image caption/angle field exists on ProductImage (Story 1.3's schema) — a numbered
    // fallback still identifies which product and which image in sequence, satisfying the AC's
    // underlying "descriptive alt text" intent without data this story doesn't have.
    return `${this.productName}, vue ${index + 1}`;
  }

  protected onImageLoad(index: number): void {
    this.setStatus(index, 'loaded');
  }

  protected onImageError(index: number): void {
    this.setStatus(index, 'error');
  }

  protected onMainImageLoad(): void {
    this.mainImageStatus.set('loaded');
  }

  protected onMainImageError(): void {
    this.mainImageStatus.set('error');
  }

  protected setActive(index: number): void {
    this.setActiveIndex(index);
    this.scrollMobileTo(index);
  }

  protected previous(): void {
    if (this.images.length === 0) {
      return;
    }
    this.setActive((this.activeIndex() - 1 + this.images.length) % this.images.length);
  }

  protected next(): void {
    if (this.images.length === 0) {
      return;
    }
    this.setActive((this.activeIndex() + 1) % this.images.length);
  }

  protected onMobileScroll(): void {
    const container = this.scrollContainer?.nativeElement;
    if (!container || container.clientWidth === 0) {
      return;
    }
    const index = Math.round(container.scrollLeft / container.clientWidth);
    // Not setActive(): that also calls scrollMobileTo(), which would fight the scroll gesture
    // that's already in progress and driving this handler in the first place.
    this.setActiveIndex(Math.min(Math.max(index, 0), this.images.length - 1));
  }

  private setActiveIndex(index: number): void {
    if (index === this.activeIndex()) {
      return;
    }
    this.activeIndex.set(index);
    this.mainImageStatus.set('loading');
  }

  private setStatus(index: number, status: ImageStatus): void {
    const next = [...this.imageStatus()];
    next[index] = status;
    this.imageStatus.set(next);
  }

  private scrollMobileTo(index: number): void {
    const container = this.scrollContainer?.nativeElement;
    if (!container) {
      return;
    }
    container.scrollTo({ left: container.clientWidth * index, behavior: 'smooth' });
  }
}
