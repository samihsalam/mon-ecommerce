import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';

import { PrivacyPolicyComponent } from './privacy-policy.component';

describe('PrivacyPolicyComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PrivacyPolicyComponent],
    });
  });

  it('should render the privacy policy content', () => {
    const fixture = TestBed.createComponent(PrivacyPolicyComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Politique de confidentialité');
    expect(text).toContain('RGPD');
  });

  it('should set the page title (AC #6)', () => {
    const fixture = TestBed.createComponent(PrivacyPolicyComponent);
    fixture.detectChanges();

    expect(TestBed.inject(Title).getTitle()).toBe('Politique de confidentialité | MonEcommerce');
  });
});
