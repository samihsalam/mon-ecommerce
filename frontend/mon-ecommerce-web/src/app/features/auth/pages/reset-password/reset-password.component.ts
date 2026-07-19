import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AbstractControl, ReactiveFormsModule, FormBuilder, ValidationErrors, Validators } from '@angular/forms';

import { AuthStore } from '../../auth.store';

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password && confirmPassword && password !== confirmPassword ? { passwordsMismatch: true } : null;
}

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss',
})
export class ResetPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly authStore = inject(AuthStore);

  private readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  // A direct/stale/stripped visit to this route without email+token would otherwise submit
  // empty strings, fail backend validation with a 422 (not the 400 the "invalid link" message
  // is keyed on), and confusingly show "an error occurred, try again" instead of a clear signal.
  protected readonly linkIsMissingParams = !this.email || !this.token;

  protected readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatchValidator },
  );

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { newPassword } = this.form.getRawValue();
    const success = await this.authStore.resetPassword(this.email, this.token, newPassword);

    if (success) {
      await this.router.navigate(['/connexion']);
    }
  }
}
