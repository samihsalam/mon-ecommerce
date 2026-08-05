import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { CookieBannerComponent } from './cookie-banner.component';
import { ConsentService } from '../../services/consent.service';

@Component({
  standalone: true,
  imports: [CookieBannerComponent],
  template: `<button id="trigger">Modifier mes préférences</button>
    <app-cookie-banner />`,
})
class HostComponent {}

describe('CookieBannerComponent', () => {
  let isBannerOpen: ReturnType<typeof signal<boolean>>;
  let hasNonEssentialConsent: ReturnType<typeof signal<boolean>>;
  let acceptAllSpy: jasmine.Spy;
  let rejectSpy: jasmine.Spy;
  let acceptCustomSpy: jasmine.Spy;

  function configure(): void {
    isBannerOpen = signal(true);
    hasNonEssentialConsent = signal(false);
    acceptAllSpy = jasmine.createSpy('acceptAll');
    rejectSpy = jasmine.createSpy('reject');
    acceptCustomSpy = jasmine.createSpy('acceptCustom');

    TestBed.configureTestingModule({
      imports: [CookieBannerComponent, HostComponent],
      providers: [
        {
          provide: ConsentService,
          useValue: {
            isBannerOpen,
            hasNonEssentialConsent,
            acceptAll: acceptAllSpy,
            reject: rejectSpy,
            acceptCustom: acceptCustomSpy,
            reopen: jasmine.createSpy('reopen'),
          },
        },
      ],
    });
  }

  beforeEach(() => configure());

  it('should render the three buttons with aria-labels when the banner is open', () => {
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[aria-label="Accepter tous les cookies"]')).toBeTruthy();
    expect(el.querySelector('[aria-label="Refuser les cookies non-essentiels"]')).toBeTruthy();
    expect(el.querySelector('[aria-label="Personnaliser les cookies"]')).toBeTruthy();
  });

  it('should render nothing when the banner is closed', () => {
    isBannerOpen.set(false);
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="dialog"]')).toBeNull();
  });

  it('should call ConsentService.acceptAll() when "Accepter tout" is clicked', () => {
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[aria-label="Accepter tous les cookies"]') as HTMLButtonElement).click();

    expect(acceptAllSpy).toHaveBeenCalled();
  });

  it('should call ConsentService.reject() when "Refuser" is clicked', () => {
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[aria-label="Refuser les cookies non-essentiels"]') as HTMLButtonElement).click();

    expect(rejectSpy).toHaveBeenCalled();
  });

  it('should reveal the customize panel when "Personnaliser" is clicked', () => {
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[aria-label="Personnaliser les cookies"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[aria-label="Enregistrer mes préférences de cookies"]')).toBeTruthy();
    expect(el.querySelector('[aria-label="Accepter tous les cookies"]')).toBeNull();
  });

  it('should call acceptCustom() with the toggle state when saving custom preferences', () => {
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[aria-label="Personnaliser les cookies"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const checkbox = fixture.nativeElement.querySelector('input[type="checkbox"]') as HTMLInputElement;
    checkbox.click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[aria-label="Enregistrer mes préférences de cookies"]') as HTMLButtonElement).click();

    expect(acceptCustomSpy).toHaveBeenCalledWith(true);
  });

  it('should close the customize panel on Escape without recording any consent', () => {
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[aria-label="Personnaliser les cookies"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[aria-label="Accepter tous les cookies"]')).toBeTruthy();
    expect(acceptAllSpy).not.toHaveBeenCalled();
    expect(rejectSpy).not.toHaveBeenCalled();
    expect(acceptCustomSpy).not.toHaveBeenCalled();
  });

  it('should set role="dialog" and aria-modal="true" on the banner', () => {
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();

    const dialog = (fixture.nativeElement as HTMLElement).querySelector('[role="dialog"]');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
  });

  it('should seed the customize toggle from the visitor\'s existing consent, not always false', () => {
    hasNonEssentialConsent.set(true);
    const fixture = TestBed.createComponent(CookieBannerComponent);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[aria-label="Personnaliser les cookies"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const checkbox = fixture.nativeElement.querySelector('input[type="checkbox"]') as HTMLInputElement;
    expect(checkbox.checked).toBe(true);

    // Saving without touching the checkbox must NOT silently downgrade the prior consent to false.
    (fixture.nativeElement.querySelector('[aria-label="Enregistrer mes préférences de cookies"]') as HTMLButtonElement).click();
    expect(acceptCustomSpy).toHaveBeenCalledWith(true);
  });

  it('should restore focus to the triggering element when the banner closes', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('#trigger') as HTMLButtonElement;

    trigger.focus();
    expect(document.activeElement).toBe(trigger);

    fixture.detectChanges();
    await fixture.whenStable();

    isBannerOpen.set(false);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(document.activeElement).toBe(trigger);
  });
});
