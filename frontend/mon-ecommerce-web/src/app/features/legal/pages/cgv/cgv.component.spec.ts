import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Title } from '@angular/platform-browser';

import { CgvComponent } from './cgv.component';

describe('CgvComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CgvComponent],
      providers: [provideRouter([])],
    });
  });

  // AC #1: full content displayed without requiring login — no authGuard dependency, no
  // authentication service injected at all.
  it('should render the CGV content', () => {
    const fixture = TestBed.createComponent(CgvComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Conditions générales de vente');
    expect(text).toContain('droit de rétractation');
  });

  it('should set the page title (AC #6)', () => {
    const fixture = TestBed.createComponent(CgvComponent);
    fixture.detectChanges();

    expect(TestBed.inject(Title).getTitle()).toBe('Conditions générales de vente | MonEcommerce');
  });
});
