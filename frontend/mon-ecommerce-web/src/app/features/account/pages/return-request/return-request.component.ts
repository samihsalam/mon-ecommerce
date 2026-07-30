import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { OrdersStore, ReturnReason, RETURN_REASON_LABELS } from '../../orders.store';

@Component({
  selector: 'app-return-request',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './return-request.component.html',
  styleUrl: './return-request.component.scss',
})
export class ReturnRequestComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  protected readonly ordersStore = inject(OrdersStore);

  protected readonly orderId = this.route.snapshot.paramMap.get('orderId')!;
  protected readonly reasons: { value: ReturnReason; label: string }[] = (
    Object.keys(RETURN_REASON_LABELS) as ReturnReason[]
  ).map((value) => ({ value, label: RETURN_REASON_LABELS[value] }));

  protected readonly submitting = signal(false);
  protected readonly selectedPhotos = signal<File[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    reason: ['' as ReturnReason | '', Validators.required],
    description: ['', Validators.required],
  });

  protected onPhotosSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedPhotos.set(input.files ? Array.from(input.files) : []);
  }

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { reason, description } = this.form.getRawValue();
    const success = await this.ordersStore.requestReturn(
      this.orderId,
      reason as ReturnReason,
      description,
      this.selectedPhotos(),
    );
    this.submitting.set(false);

    if (success) {
      void this.router.navigate(['/compte/commandes', this.orderId]);
    }
  }
}
