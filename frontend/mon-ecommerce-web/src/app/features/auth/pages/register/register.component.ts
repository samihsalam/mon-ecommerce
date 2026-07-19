import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { AuthStore } from '../../auth.store';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly authStore = inject(AuthStore);

  // Not `updateOn: 'blur'`: that delays VALUE sync (not just validity), so pressing Enter to
  // submit right after typing — without a blur event firing first — would read a stale value.
  // The template already gates error display on `.touched`, which blur sets independently of
  // `updateOn`, so onBlur-only error display works correctly without this option.
  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, email, password } = this.form.getRawValue();
    const success = await this.authStore.register(name, email, password);

    if (success) {
      await this.router.navigate(['/']);
    }
  }
}
