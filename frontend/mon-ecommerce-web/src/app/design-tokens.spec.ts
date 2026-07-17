import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

@Component({
  selector: 'app-design-tokens-test-host',
  standalone: true,
  template: `
    <h1 id="heading">Titre</h1>
    <div
      id="accent-box"
      style="background-color: var(--color-accent); border-radius: var(--radius-card);"
    ></div>
  `,
})
class DesignTokensTestHostComponent {}

describe('Design tokens (Story 1.8)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DesignTokensTestHostComponent],
    }).compileComponents();
  });

  it('resolves --color-accent to #C9A96E when referenced via var()', () => {
    const fixture = TestBed.createComponent(DesignTokensTestHostComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('#accent-box') as HTMLElement;
    const backgroundColor = getComputedStyle(el).backgroundColor;
    expect(backgroundColor).toBe('rgb(201, 169, 110)');
  });

  it('resolves --radius-card to 4px when referenced via var()', () => {
    const fixture = TestBed.createComponent(DesignTokensTestHostComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('#accent-box') as HTMLElement;
    const borderRadius = getComputedStyle(el).borderRadius;
    expect(borderRadius).toBe('4px');
  });

  it('applies Cormorant Garamond to h1 via the base layer rule', () => {
    const fixture = TestBed.createComponent(DesignTokensTestHostComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('#heading') as HTMLElement;
    const fontFamily = getComputedStyle(el).fontFamily;
    expect(fontFamily).toContain('Cormorant Garamond');
  });

  it('exposes the full Élégance Naturelle palette as global CSS custom properties', () => {
    const fixture = TestBed.createComponent(DesignTokensTestHostComponent);
    fixture.detectChanges();
    const styles = getComputedStyle(fixture.nativeElement);
    const expected: Record<string, string> = {
      '--color-bg': '#FFFFFF',
      '--color-bg-secondary': '#FAF8F5',
      '--color-text': '#111111',
      '--color-text-secondary': '#555555',
      '--color-accent': '#C9A96E',
      '--color-accent-hover': '#A8864A',
      '--color-border': '#E5E5E5',
      '--color-success': '#6B8F71',
      '--color-error': '#C0564A',
    };
    for (const [token, value] of Object.entries(expected)) {
      expect(styles.getPropertyValue(token).trim()).toBe(value);
    }
  });
});
