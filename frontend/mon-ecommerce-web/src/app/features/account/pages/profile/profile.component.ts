import { Component, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AccountStore } from '../../account.store';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  protected readonly accountStore = inject(AccountStore);

  private loadedEmail = '';

  // The form is hidden until the profile has actually loaded — without this, a user could
  // start typing while loadProfile() is still in flight and have their input silently
  // clobbered by patchValue() once it resolves, and isEmailChanged (compared against a still-
  // empty loadedEmail) would spuriously show the current-password field. Also means a failed
  // load never exposes an unusable form.
  protected readonly initialized = signal(false);

  // Not `updateOn: 'blur'` — see Story 2.1's register.component.ts for why (Enter-key staleness).
  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    currentPassword: [''],
  });

  async ngOnInit(): Promise<void> {
    await this.accountStore.loadProfile();

    const profile = this.accountStore.profile();
    if (profile) {
      this.loadedEmail = profile.email;
      this.form.patchValue({ name: profile.name, email: profile.email });
      this.initialized.set(true);
    }
  }

  protected get isEmailChanged(): boolean {
    return this.form.controls.email.value !== this.loadedEmail;
  }

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, email, currentPassword } = this.form.getRawValue();
    const success = await this.accountStore.updateProfile(name, email, this.isEmailChanged ? currentPassword : null);

    if (success) {
      this.loadedEmail = email;
      this.form.patchValue({ currentPassword: '' });
    }
  }
}
