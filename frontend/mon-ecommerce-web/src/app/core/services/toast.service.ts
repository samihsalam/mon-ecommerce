import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _message = signal<string | null>(null);
  readonly message = this._message.asReadonly();

  private timeoutId: ReturnType<typeof setTimeout> | undefined;

  show(text: string): void {
    this._message.set(text);

    clearTimeout(this.timeoutId);
    this.timeoutId = setTimeout(() => this._message.set(null), 4000);
  }
}
