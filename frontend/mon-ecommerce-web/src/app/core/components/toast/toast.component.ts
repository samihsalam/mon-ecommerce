import { Component, inject } from '@angular/core';

import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    @if (toastService.message(); as message) {
      <div
        role="status"
        class="fixed bottom-6 left-1/2 -translate-x-1/2 rounded-card bg-success text-white px-6 py-3 shadow-lg"
      >
        {{ message }}
      </div>
    }
  `,
})
export class ToastComponent {
  protected readonly toastService = inject(ToastService);
}
