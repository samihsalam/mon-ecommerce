import { TestBed } from '@angular/core/testing';

import { StickyAddToCartComponent } from './sticky-add-to-cart.component';
import { CartStore } from '../../../cart/cart.store';
import { ToastService } from '../../../../core/services/toast.service';
import type { ProductDetail } from '../../product-detail.store';

describe('StickyAddToCartComponent', () => {
  const cannedProduct: ProductDetail = {
    id: 'product-1',
    name: 'Tote Parisienne',
    description: 'Un sac en cuir.',
    priceInCents: 28500,
    material: 'Cuir',
    color: 'Cognac',
    dimensions: '30x20x10cm',
    stockQuantity: 3,
    inStock: true,
    categoryId: 'cat-1',
    categoryName: 'Sacs',
    categorySlug: 'sacs',
    imageUrls: [],
  };

  let addItemSpy: jasmine.Spy;
  let toastShowSpy: jasmine.Spy;

  beforeEach(() => {
    addItemSpy = jasmine.createSpy('addItem').and.resolveTo(true);
    toastShowSpy = jasmine.createSpy('show');

    TestBed.configureTestingModule({
      imports: [StickyAddToCartComponent],
      providers: [
        { provide: CartStore, useValue: { addItem: addItemSpy } },
        { provide: ToastService, useValue: { show: toastShowSpy } },
      ],
    });
  });

  function createWithProduct(product: ProductDetail) {
    const fixture = TestBed.createComponent(StickyAddToCartComponent);
    fixture.componentRef.setInput('product', product);
    fixture.detectChanges();
    return fixture;
  }

  it('should show "Rupture de stock" and aria-disabled when the product is out of stock', () => {
    const fixture = createWithProduct({ ...cannedProduct, inStock: false, stockQuantity: 0 });
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.textContent?.trim()).toBe('Rupture de stock');
    expect(button.getAttribute('aria-disabled')).toBe('true');
  });

  it('should show "Ajouter au panier" and no aria-disabled when in stock', () => {
    const fixture = createWithProduct(cannedProduct);
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.textContent?.trim()).toBe('Ajouter au panier');
    expect(button.getAttribute('aria-disabled')).toBeNull();
  });

  it('should call CartStore.addItem and show a 3s toast on tap when in stock', async () => {
    const fixture = createWithProduct(cannedProduct);
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    button.click();
    await fixture.whenStable();

    expect(addItemSpy).toHaveBeenCalledWith('product-1', 1);
    expect(toastShowSpy).toHaveBeenCalledWith('Tote Parisienne ajoutée au panier', 3000);
  });

  it('should not call CartStore.addItem when out of stock', async () => {
    const fixture = createWithProduct({ ...cannedProduct, inStock: false, stockQuantity: 0 });
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    button.click();
    await fixture.whenStable();

    expect(addItemSpy).not.toHaveBeenCalled();
    expect(toastShowSpy).not.toHaveBeenCalled();
  });

  it('should apply visual disabled styling (opacity + no pointer events) when out of stock', () => {
    const fixture = createWithProduct({ ...cannedProduct, inStock: false, stockQuantity: 0 });
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.classList.contains('opacity-50')).toBe(true);
    expect(button.classList.contains('pointer-events-none')).toBe(true);
  });

  it('should show a failure toast and not a success toast when addItem fails', async () => {
    addItemSpy.and.resolveTo(false);
    const fixture = createWithProduct(cannedProduct);
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    button.click();
    await fixture.whenStable();

    expect(toastShowSpy).toHaveBeenCalledWith("Impossible d'ajouter cet article au panier.");
    expect(toastShowSpy).not.toHaveBeenCalledWith('Tote Parisienne ajoutée au panier', 3000);
  });
});
