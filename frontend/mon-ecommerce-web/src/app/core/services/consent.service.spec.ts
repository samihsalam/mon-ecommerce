import { TestBed } from '@angular/core/testing';

import { ConsentService } from './consent.service';
import { CONSENT_KEY } from '../constants/storage-keys';

describe('ConsentService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  afterEach(() => localStorage.clear());

  it('should open the banner when no consent is stored', () => {
    const service = TestBed.inject(ConsentService);

    expect(service.isBannerOpen()).toBe(true);
    expect(service.hasNonEssentialConsent()).toBe(false);
  });

  it('should close the banner and persist acceptance on acceptAll()', () => {
    const service = TestBed.inject(ConsentService);

    service.acceptAll();

    expect(service.isBannerOpen()).toBe(false);
    expect(service.hasNonEssentialConsent()).toBe(true);

    const stored = JSON.parse(localStorage.getItem(CONSENT_KEY)!);
    expect(stored.status).toBe('accepted-all');
    expect(stored.nonEssential).toBe(true);
  });

  it('should close the banner and persist refusal on reject(), with non-essential consent false', () => {
    const service = TestBed.inject(ConsentService);

    service.reject();

    expect(service.isBannerOpen()).toBe(false);
    expect(service.hasNonEssentialConsent()).toBe(false);

    const stored = JSON.parse(localStorage.getItem(CONSENT_KEY)!);
    expect(stored.status).toBe('rejected');
    expect(stored.nonEssential).toBe(false);
  });

  it('should persist a custom choice via acceptCustom()', () => {
    const service = TestBed.inject(ConsentService);

    service.acceptCustom(true);

    expect(service.isBannerOpen()).toBe(false);
    expect(service.hasNonEssentialConsent()).toBe(true);

    const stored = JSON.parse(localStorage.getItem(CONSENT_KEY)!);
    expect(stored.status).toBe('custom');
    expect(stored.nonEssential).toBe(true);
  });

  it('should not show the banner on a fresh instance when valid consent is already stored', () => {
    localStorage.setItem(
      CONSENT_KEY,
      JSON.stringify({ status: 'accepted-all', nonEssential: true, timestamp: Date.now() }),
    );

    const service = TestBed.inject(ConsentService);

    expect(service.isBannerOpen()).toBe(false);
    expect(service.hasNonEssentialConsent()).toBe(true);
  });

  it('should treat consent older than 12 months as absent and re-show the banner', () => {
    const thirteenMonthsAgo = Date.now() - 13 * 30 * 24 * 60 * 60 * 1000;
    localStorage.setItem(
      CONSENT_KEY,
      JSON.stringify({ status: 'accepted-all', nonEssential: true, timestamp: thirteenMonthsAgo }),
    );

    const service = TestBed.inject(ConsentService);

    expect(service.isBannerOpen()).toBe(true);
    expect(service.hasNonEssentialConsent()).toBe(false);
  });

  it('should treat malformed stored consent as absent', () => {
    localStorage.setItem(CONSENT_KEY, 'not-json');

    const service = TestBed.inject(ConsentService);

    expect(service.isBannerOpen()).toBe(true);
  });

  it('should treat valid-JSON-but-wrong-shape stored consent as absent (not NaN-comparison-passes-through)', () => {
    localStorage.setItem(CONSENT_KEY, JSON.stringify({}));

    const service = TestBed.inject(ConsentService);

    expect(service.isBannerOpen()).toBe(true);
    expect(service.hasNonEssentialConsent()).toBe(false);
  });

  it('should treat a stored record with an invalid status value as absent', () => {
    localStorage.setItem(
      CONSENT_KEY,
      JSON.stringify({ status: 'not-a-real-status', nonEssential: true, timestamp: Date.now() }),
    );

    const service = TestBed.inject(ConsentService);

    expect(service.isBannerOpen()).toBe(true);
  });

  it('should not throw and should treat consent as absent if localStorage.getItem throws', () => {
    spyOn(localStorage, 'getItem').and.throwError('SecurityError');

    expect(() => TestBed.inject(ConsentService)).not.toThrow();
    const service = TestBed.inject(ConsentService);
    expect(service.isBannerOpen()).toBe(true);
  });

  it('should not throw if localStorage.setItem throws when saving a choice', () => {
    const service = TestBed.inject(ConsentService);
    spyOn(localStorage, 'setItem').and.throwError('QuotaExceededError');

    expect(() => service.acceptAll()).not.toThrow();
    expect(service.isBannerOpen()).toBe(false);
    expect(service.hasNonEssentialConsent()).toBe(true);
  });

  it('should reopen the banner without clearing the previously stored consent', () => {
    const service = TestBed.inject(ConsentService);
    service.acceptAll();

    service.reopen();

    expect(service.isBannerOpen()).toBe(true);
    expect(service.hasNonEssentialConsent()).toBe(true);
    const stored = JSON.parse(localStorage.getItem(CONSENT_KEY)!);
    expect(stored.status).toBe('accepted-all');
  });
});
