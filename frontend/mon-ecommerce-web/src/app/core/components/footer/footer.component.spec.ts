import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { FooterComponent } from './footer.component';

describe('FooterComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FooterComponent],
      providers: [provideRouter([])],
    });
  });

  it('should render links to all three legal pages', () => {
    const fixture = TestBed.createComponent(FooterComponent);
    fixture.detectChanges();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a')).map((a) =>
      a.getAttribute('href'),
    );

    expect(links).toContain('/cgv');
    expect(links).toContain('/confidentialite');
    expect(links).toContain('/retours');
  });
});
