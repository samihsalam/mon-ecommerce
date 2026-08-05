import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

import { CONSENT_KEY } from '../constants/storage-keys';

export type ConsentStatus = 'accepted-all' | 'rejected' | 'custom';

export interface ConsentRecord {
  status: ConsentStatus;
  nonEssential: boolean;
  timestamp: number;
}

// AC #3: consent is persisted for 12 months, not indefinitely — a record older than this is
// treated as absent so the banner re-shows.
const CONSENT_TTL_MS = 365 * 24 * 60 * 60 * 1000;

// Global consent state, mirroring ToastService/CartStore's shape: an injectable signal-backed
// service, read by CookieBannerComponent (mounted once in app.component.html) and by FooterComponent
// ("Modifier mes préférences"). hasNonEssentialConsent() is the gate any future analytics/marketing
// script loader must check before injecting anything — no such script exists in this codebase yet
// (AC #2, #4 are about this gate existing correctly, not about retrofitting a real integration).
@Injectable({ providedIn: 'root' })
export class ConsentService {
  private readonly platformId = inject(PLATFORM_ID);

  private readonly _consent = signal<ConsentRecord | null>(null);
  private readonly _bannerOpen = signal(false);

  readonly isBannerOpen = this._bannerOpen.asReadonly();
  readonly hasNonEssentialConsent = computed(() => this._consent()?.nonEssential ?? false);

  constructor() {
    // Runs during SSR too — localStorage doesn't exist there, same guard as cartSessionInterceptor.
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const stored = this.readStoredConsent();
    if (stored) {
      this._consent.set(stored);
    } else {
      this._bannerOpen.set(true);
    }
  }

  acceptAll(): void {
    this.saveConsent({ status: 'accepted-all', nonEssential: true, timestamp: Date.now() });
  }

  reject(): void {
    this.saveConsent({ status: 'rejected', nonEssential: false, timestamp: Date.now() });
  }

  acceptCustom(nonEssential: boolean): void {
    this.saveConsent({ status: 'custom', nonEssential, timestamp: Date.now() });
  }

  // "Modifier mes préférences" (AC #7) — re-shows the banner WITHOUT clearing the existing stored
  // consent; the visitor's next explicit choice (via the methods above) overwrites it.
  reopen(): void {
    this._bannerOpen.set(true);
  }

  private saveConsent(record: ConsentRecord): void {
    this._consent.set(record);
    this._bannerOpen.set(false);

    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    // Private browsing / disabled storage can throw (SecurityError, QuotaExceededError) — the
    // signal above already applies the choice for this session; only cross-reload persistence is
    // lost, so a write failure must not break the click handler that got here.
    try {
      localStorage.setItem(CONSENT_KEY, JSON.stringify(record));
    } catch {
      // Intentionally swallowed — see comment above.
    }
  }

  private readStoredConsent(): ConsentRecord | null {
    let raw: string | null;
    try {
      raw = localStorage.getItem(CONSENT_KEY);
    } catch {
      return null;
    }

    if (!raw) {
      return null;
    }

    try {
      const record: unknown = JSON.parse(raw);
      if (!this.isConsentRecord(record)) {
        return null;
      }
      const isExpired = Date.now() - record.timestamp > CONSENT_TTL_MS;
      return isExpired ? null : record;
    } catch {
      return null;
    }
  }

  // JSON.parse succeeding doesn't mean the shape is right — a wrong-shaped-but-parseable value
  // (e.g. `{}`) would otherwise flow into `Date.now() - undefined`, which is NaN, and
  // `NaN > CONSENT_TTL_MS` is false: malformed data would be silently treated as valid,
  // non-expired consent instead of absent.
  private isConsentRecord(value: unknown): value is ConsentRecord {
    if (typeof value !== 'object' || value === null) {
      return false;
    }
    const record = value as Partial<ConsentRecord>;
    return (
      (record.status === 'accepted-all' || record.status === 'rejected' || record.status === 'custom') &&
      typeof record.nonEssential === 'boolean' &&
      typeof record.timestamp === 'number'
    );
  }
}
