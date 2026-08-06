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

  // Story 8.3 AC #1: reveals the "Supprimer mon compte" confirmation panel — separate from
  // accountStore.deletionRequested (server-confirmed state) since this is purely local UI state
  // for the not-yet-submitted confirm/cancel step.
  protected readonly showDeleteConfirmation = signal(false);

  // Captured from accountStore.error() the instant requestAccountDeletion() resolves — NOT bound
  // directly to accountStore.error() in the template (review finding: that field is shared with
  // the profile-update form above; a stale profile-form error would otherwise bleed into this
  // panel the moment it opens, before the customer has even attempted a deletion request).
  protected readonly deletionError = signal<string | null>(null);

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

  protected openDeleteConfirmation(): void {
    this.deletionError.set(null);
    this.showDeleteConfirmation.set(true);
  }

  protected cancelDeleteConfirmation(): void {
    this.showDeleteConfirmation.set(false);
  }

  protected async confirmAccountDeletion(): Promise<void> {
    const success = await this.accountStore.requestAccountDeletion();

    if (success) {
      this.showDeleteConfirmation.set(false);
    } else {
      // Review finding: the panel must stay open on failure (e.g. a 409 "already pending") so the
      // customer sees why, instead of silently reappearing at the "Supprimer mon compte" button.
      this.deletionError.set(this.accountStore.error());
    }
  }
}
