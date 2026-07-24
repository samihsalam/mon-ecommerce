import { signal } from '@angular/core';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { HeaderComponent } from './header.component';
import { CartStore } from '../../../features/cart/cart.store';

describe('HeaderComponent', () => {
  let itemCount: ReturnType<typeof signal<number>>;
  let openSpy: jasmine.Spy;

  function configure(initialItemCount: number): void {
    itemCount = signal(initialItemCount);
    openSpy = jasmine.createSpy('open');

    TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [provideRouter([]), { provide: CartStore, useValue: { itemCount, open: openSpy } }],
    });
  }

  it('should not render a badge when the cart is empty', () => {
    configure(0);
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('span')).toBeNull();
  });

  it('should render the item count as a badge when the cart has items', () => {
    configure(3);
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('3');
  });

  it('should NOT pulse on the initial observation, even if the cart already has items (hydration)', fakeAsync(() => {
    // Simulates CartStore already having resolved a non-empty cart by the time HeaderComponent
    // first reads itemCount() — e.g. a returning visitor. This must not be mistaken for a
    // real, user-triggered add (review finding).
    configure(3);
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    tick(500);
    fixture.detectChanges();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('span');
    expect(badge?.classList.contains('cart-badge-pulse')).toBe(false);
  }));

  it('should pulse when itemCount increases after the initial observation', fakeAsync(() => {
    configure(1);
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    tick(500);
    fixture.detectChanges();

    itemCount.set(2);
    fixture.detectChanges();
    tick(0);
    fixture.detectChanges();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('span');
    expect(badge?.classList.contains('cart-badge-pulse')).toBe(true);

    tick(400);
    fixture.detectChanges();
    expect(badge?.classList.contains('cart-badge-pulse')).toBe(false);
  }));

  it('should not pulse when itemCount decreases', fakeAsync(() => {
    configure(3);
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    tick(500);
    fixture.detectChanges();

    itemCount.set(2);
    fixture.detectChanges();
    tick(0);
    fixture.detectChanges();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('span');
    expect(badge?.classList.contains('cart-badge-pulse')).toBe(false);
  }));

  it('should call CartStore.open() when the cart icon is clicked', () => {
    configure(0);
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('button')?.dispatchEvent(new MouseEvent('click'));

    expect(openSpy).toHaveBeenCalled();
  });
});
