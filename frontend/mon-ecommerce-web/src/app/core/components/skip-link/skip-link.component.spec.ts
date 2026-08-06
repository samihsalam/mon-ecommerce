import { TestBed } from '@angular/core/testing';

import { SkipLinkComponent } from './skip-link.component';

describe('SkipLinkComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [SkipLinkComponent] });
  });

  it('should render a link to #main-content with the exact required text', () => {
    const fixture = TestBed.createComponent(SkipLinkComponent);
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a');
    expect(link).toBeTruthy();
    expect(link?.getAttribute('href')).toBe('#main-content');
    expect(link?.textContent?.trim()).toBe('Aller au contenu principal');
  });

  // AC #3: hidden until focused, so it never affects sighted layout, but is reachable by Tab.
  it('should be visually hidden by default and revealed on focus', () => {
    const fixture = TestBed.createComponent(SkipLinkComponent);
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a');
    expect(link?.classList).toContain('sr-only');
    expect(link?.classList).toContain('focus:not-sr-only');
  });
});
