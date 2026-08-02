import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Title } from '@angular/platform-browser';

import { ReturnsPolicyComponent } from './returns-policy.component';

describe('ReturnsPolicyComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReturnsPolicyComponent],
      providers: [provideRouter([])],
    });
  });

  it('should render the returns policy content, consistent with the 14-day backend return window', () => {
    const fixture = TestBed.createComponent(ReturnsPolicyComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('14 jours');
  });

  it('should set the page title (AC #6)', () => {
    const fixture = TestBed.createComponent(ReturnsPolicyComponent);
    fixture.detectChanges();

    expect(TestBed.inject(Title).getTitle()).toBe('Politique de retours | MonEcommerce');
  });
});
