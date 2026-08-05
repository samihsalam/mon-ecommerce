import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { FooterComponent } from './footer.component';
import { ConsentService } from '../../services/consent.service';

describe('FooterComponent', () => {
  let reopenSpy: jasmine.Spy;

  beforeEach(() => {
    reopenSpy = jasmine.createSpy('reopen');

    TestBed.configureTestingModule({
      imports: [FooterComponent],
      providers: [provideRouter([]), { provide: ConsentService, useValue: { reopen: reopenSpy } }],
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

  it('should render a "Modifier mes préférences" control that reopens the cookie banner', () => {
    const fixture = TestBed.createComponent(FooterComponent);
    fixture.detectChanges();

    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Modifier mes préférences'),
    );
    expect(button).toBeTruthy();

    button?.click();

    expect(reopenSpy).toHaveBeenCalled();
  });
});
