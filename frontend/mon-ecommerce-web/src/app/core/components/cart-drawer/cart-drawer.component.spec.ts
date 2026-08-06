import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { CartDrawerComponent } from './cart-drawer.component';
import { CartStore, CartItem } from '../../../features/cart/cart.store';
import { expectNoAccessibilityViolations } from '../../testing/axe-helper';

@Component({
  standalone: true,
  imports: [CartDrawerComponent],
  template: `<button id="trigger">Ouvrir le panier</button>
    <app-cart-drawer />`,
})
class HostComponent {}

describe('CartDrawerComponent', () => {
  const cannedItem: CartItem = {
    id: 'item-1',
    productId: 'product-1',
    productName: 'Tote Parisienne',
    imageUrl: null,
    unitPriceInCents: 5000,
    quantity: 2,
    lineTotalInCents: 10000,
  };

  let isOpen: ReturnType<typeof signal<boolean>>;
  let items: ReturnType<typeof signal<CartItem[]>>;
  let isLoading: ReturnType<typeof signal<boolean>>;
  let closeSpy: jasmine.Spy;
  let updateItemQuantitySpy: jasmine.Spy;
  let removeItemSpy: jasmine.Spy;

  function configure(): void {
    isOpen = signal(false);
    items = signal<CartItem[]>([]);
    isLoading = signal(false);
    closeSpy = jasmine.createSpy('close').and.callFake(() => isOpen.set(false));
    updateItemQuantitySpy = jasmine.createSpy('updateItemQuantity').and.resolveTo(true);
    removeItemSpy = jasmine.createSpy('removeItem').and.resolveTo(true);

    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        provideRouter([]),
        {
          provide: CartStore,
          useValue: {
            isOpen,
            items,
            isLoading,
            itemCount: () => items().reduce((sum, i) => sum + i.quantity, 0),
            totalInCents: () => items().reduce((sum, i) => sum + i.lineTotalInCents, 0),
            close: closeSpy,
            updateItemQuantity: updateItemQuantitySpy,
            removeItem: removeItemSpy,
          },
        },
      ],
    });
  }

  beforeEach(() => configure());

  it('should show the empty state when there are no items', () => {
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Votre panier est vide');
    expect(text).toContain('Découvrir notre catalogue');
  });

  // Story 8.5, AC #7.
  it('should have no axe-core accessibility violations when open with items', async () => {
    items.set([cannedItem]);
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    await expectNoAccessibilityViolations(fixture.nativeElement as HTMLElement);
  });

  // Story 8.5, AC #7 — the empty-cart state has structurally different DOM (a CTA link instead
  // of an item list) from the "with items" state above, so it's checked separately (review finding).
  it('should have no axe-core accessibility violations when open and empty', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    await expectNoAccessibilityViolations(fixture.nativeElement as HTMLElement);
  });

  it('should set role="dialog" and aria-modal="true" on the panel', () => {
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    const dialog = (fixture.nativeElement as HTMLElement).querySelector('[role="dialog"]');
    expect(dialog).toBeTruthy();
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
  });

  it('should render cart items when present, not the empty state', () => {
    items.set([cannedItem]);
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Tote Parisienne');
    expect(text).not.toContain('Votre panier est vide');
  });

  it('should close and restore focus to the trigger element when Escape is pressed', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('#trigger') as HTMLButtonElement;

    trigger.focus();
    expect(document.activeElement).toBe(trigger);

    isOpen.set(true);
    fixture.detectChanges();
    await fixture.whenStable();

    const dialog = (fixture.nativeElement as HTMLElement).querySelector('[role="dialog"]') as HTMLElement;
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(closeSpy).toHaveBeenCalled();
    expect(document.activeElement).toBe(trigger);
  });

  it('should disable the quantity stepper and remove buttons while a mutation is in flight', () => {
    items.set([cannedItem]);
    isLoading.set(true);
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button[aria-label*="Réduire"], button[aria-label*="Augmenter"], button[aria-label*="Retirer"]');
    expect(buttons.length).toBe(3);
    buttons.forEach((button) => expect((button as HTMLButtonElement).disabled).toBe(true));
  });

  it('should not disable the stepper/remove buttons when no mutation is in flight', () => {
    items.set([cannedItem]);
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button[aria-label*="Réduire"], button[aria-label*="Augmenter"], button[aria-label*="Retirer"]');
    buttons.forEach((button) => expect((button as HTMLButtonElement).disabled).toBe(false));
  });

  it('should call updateItemQuantity with quantity+1 when the increment button is clicked', async () => {
    items.set([cannedItem]);
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    const incrementButton = (fixture.nativeElement as HTMLElement).querySelector(
      'button[aria-label*="Augmenter"]',
    ) as HTMLButtonElement;
    incrementButton.click();
    await fixture.whenStable();

    expect(updateItemQuantitySpy).toHaveBeenCalledWith('item-1', 3);
  });

  it('should call removeItem when the remove button is clicked', async () => {
    items.set([cannedItem]);
    const fixture = TestBed.createComponent(HostComponent);
    isOpen.set(true);
    fixture.detectChanges();

    const removeButton = (fixture.nativeElement as HTMLElement).querySelector(
      'button[aria-label*="Retirer"]',
    ) as HTMLButtonElement;
    removeButton.click();
    await fixture.whenStable();

    expect(removeItemSpy).toHaveBeenCalledWith('item-1');
  });
});
