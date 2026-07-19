import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { AuthStore } from '../../auth.store';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly authStore = inject(AuthStore);

  // Not `updateOn: 'blur'` — see Story 2.1's register.component.ts for why (Enter-key staleness).
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    const success = await this.authStore.login(email, password);

    if (success) {
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
      // navigateByUrl resolves to `false` (not a rejection) for a malformed/unroutable
      // returnUrl — fall back to `/` rather than silently stranding the user on the login page.
      const navigated = await this.router.navigateByUrl(returnUrl);
      if (!navigated) {
        await this.router.navigateByUrl('/');
      }
    }
  }
}
