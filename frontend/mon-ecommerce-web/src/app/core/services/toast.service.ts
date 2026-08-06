import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { LiveAnnouncer } from '@angular/cdk/a11y';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _message = signal<string | null>(null);
  readonly message = this._message.asReadonly();

  private readonly liveAnnouncer = inject(LiveAnnouncer);
  private readonly platformId = inject(PLATFORM_ID);
  private timeoutId: ReturnType<typeof setTimeout> | undefined;

  // Story 8.4, AC #7: announced imperatively here, once per show() call — NOT derived reactively
  // from the `message` signal in ToastComponent. A signal-driven effect() would silently skip
  // re-announcing two consecutive identical messages (Angular signals use Object.is equality, so
  // setting the same string twice never re-triggers the effect), which is exactly the case where
  // an announcement matters most (e.g. two failed submits in a row with the same error). Calling
  // announce() here also means the visible <div> no longer needs role="status" — LiveAnnouncer
  // owns the announcement responsibility, so double-announcing the same text through two separate
  // live-region mechanisms is avoided.
  show(text: string, durationMs = 4000): void {
    this._message.set(text);

    if (isPlatformBrowser(this.platformId)) {
      this.liveAnnouncer.announce(text);
    }

    clearTimeout(this.timeoutId);
    this.timeoutId = setTimeout(() => this._message.set(null), durationMs);
  }
}
