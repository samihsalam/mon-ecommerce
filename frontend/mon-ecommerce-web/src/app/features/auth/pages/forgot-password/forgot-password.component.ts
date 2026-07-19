import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { AuthStore } from '../../auth.store';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  protected readonly authStore = inject(AuthStore);

  // Shown after any submit attempt that reaches the backend, regardless of whether the
  // email is actually registered — the backend never reveals that, and neither should the UI.
  protected readonly submitted = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email } = this.form.getRawValue();
    const success = await this.authStore.forgotPassword(email);

    if (success) {
      this.submitted.set(true);
    }
  }
}
