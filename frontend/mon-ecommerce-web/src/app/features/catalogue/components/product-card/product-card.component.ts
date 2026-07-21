import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';

import type { ProductSummary } from '../../catalogue.store';
import { buildProductUrl } from '../../product-url.util';

@Component({
  selector: 'app-product-card',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './product-card.component.html',
  styleUrl: './product-card.component.scss',
})
export class ProductCardComponent {
  // Plain @Input, not a computed()/signal — this codebase's other catalogue components
  // (SearchBarComponent etc.) are all plain-Input-based, and a computed() here would be a real
  // bug: it wouldn't track @Input reassignment (a plain property write, not a signal), so it
  // would freeze at the FIRST product it ever rendered inside a reused/recycled component
  // instance (e.g. an @for-tracked list where Angular reuses the component for a different item).
  @Input({ required: true }) product!: ProductSummary;

  protected formattedPrice(): string {
    return (this.product.priceInCents / 100).toFixed(2) + ' €';
  }

  protected ariaLabel(): string {
    return `${this.product.name}, ${this.formattedPrice()}`;
  }

  protected productUrl(): string[] {
    return buildProductUrl(this.product.categorySlug, this.product.id, this.product.name);
  }
}
