import { Component, input } from '@angular/core';

export const CHECKOUT_STEP_LABELS = ['Adresse', 'Livraison', 'Paiement', 'Confirmée'] as const;

export type StepState = 'completed' | 'active' | 'upcoming';

@Component({
  selector: 'app-order-step-indicator',
  standalone: true,
  templateUrl: './order-step-indicator.component.html',
  styleUrl: './order-step-indicator.component.scss',
})
export class OrderStepIndicatorComponent {
  // 1-based — AC #4's "Étape 1/4".
  readonly currentStep = input.required<number>();

  protected readonly labels = CHECKOUT_STEP_LABELS;

  // Clamped to the valid [1, labels.length] range — an out-of-range currentStep (0, negative,
  // or > labels.length) would otherwise index labels[] out of bounds in the template's "Étape
  // X/4 — Label" caption and render the literal text "undefined" (a review finding). Not
  // reachable today (every call site hardcodes a valid step), but Stories 4.4-4.6 will pass
  // 2/3/4 here, so this guards against a future off-by-one rather than trusting every caller.
  protected get clampedStep(): number {
    return Math.min(Math.max(this.currentStep(), 1), this.labels.length);
  }

  protected stateFor(stepIndex: number): StepState {
    const step = stepIndex + 1;
    if (step < this.clampedStep) return 'completed';
    if (step === this.clampedStep) return 'active';
    return 'upcoming';
  }
}
