import { Component, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, ValidationErrors, ValidatorFn } from '@angular/forms';
import { Router } from '@angular/router';

import { AccountStore } from '../../../account/account.store';
import { CheckoutStore } from '../../checkout.store';
import { OrderStepIndicatorComponent } from '../../components/order-step-indicator/order-step-indicator.component';

// Angular's built-in Validators.required only checks value == null || length === 0 — a
// whitespace-only string ("   ") has length > 0 and passes it (review finding). This rejects
// that case too, without needing a third-party library for one rule.
function requiredNotBlank(): ValidatorFn {
  return (control): ValidationErrors | null => {
    const value = typeof control.value === 'string' ? control.value.trim() : control.value;
    return value ? null : { required: true };
  };
}

@Component({
  selector: 'app-checkout-address',
  standalone: true,
  imports: [ReactiveFormsModule, OrderStepIndicatorComponent],
  templateUrl: './checkout-address.component.html',
  styleUrl: './checkout-address.component.scss',
})
export class CheckoutAddressComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly accountStore = inject(AccountStore);
  protected readonly checkoutStore = inject(CheckoutStore);

  protected readonly initialized = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    street: ['', requiredNotBlank()],
    city: ['', requiredNotBlank()],
    postalCode: ['', requiredNotBlank()],
    country: ['', requiredNotBlank()],
  });

  async ngOnInit(): Promise<void> {
    // AC #6: an address already confirmed for THIS checkout (e.g. the customer navigated back
    // from step 2) takes priority over the account's generically-saved address, since it
    // reflects what they already chose for this specific order.
    const checkoutAddress = this.checkoutStore.address();
    if (checkoutAddress) {
      this.form.patchValue(checkoutAddress);
      this.initialized.set(true);
      return;
    }

    await this.accountStore.loadProfile();
    const savedAddress = this.accountStore.profile()?.addresses[0];
    if (savedAddress) {
      this.form.patchValue(savedAddress);
    }
    // Initialized regardless of whether a saved address was found — pre-fill (AC #1) is a
    // convenience, not a precondition for entering a fresh address.
    this.initialized.set(true);
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.checkoutStore.setAddress(this.form.getRawValue());
    void this.router.navigate(['/checkout/livraison']);
  }
}
