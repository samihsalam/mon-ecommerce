import { Component, inject } from '@angular/core';

import { ToastService } from '../../services/toast.service';

// Story 8.4, AC #7: the announcement itself is made by ToastService.show() via CDK's
// LiveAnnouncer (see its comment for why that lives in the service, not here) — this component
// stays purely visual. No role="status" on the <div>: a second, independent live-region mechanism
// re-announcing the identical text would double-speak it to screen reader users.
@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    @if (toastService.message(); as message) {
      <div class="fixed bottom-6 left-1/2 -translate-x-1/2 rounded-card bg-success text-white px-6 py-3 shadow-lg">
        {{ message }}
      </div>
    }
  `,
})
export class ToastComponent {
  protected readonly toastService = inject(ToastService);
}
