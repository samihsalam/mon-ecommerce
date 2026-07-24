import { TestBed } from '@angular/core/testing';

import { OrderStepIndicatorComponent } from './order-step-indicator.component';

describe('OrderStepIndicatorComponent', () => {
  function create(currentStep: number) {
    const fixture = TestBed.createComponent(OrderStepIndicatorComponent);
    fixture.componentRef.setInput('currentStep', currentStep);
    fixture.detectChanges();
    return fixture;
  }

  it('should set aria-current="step" only on the active step', () => {
    const fixture = create(2);
    const compiled = fixture.nativeElement as HTMLElement;

    const current = compiled.querySelectorAll('[aria-current="step"]');
    expect(current.length).toBe(1);
  });

  it('should mark steps before the current one as completed', () => {
    const fixture = create(3);
    const component = fixture.componentInstance;

    expect(component['stateFor'](0)).toBe('completed');
    expect(component['stateFor'](1)).toBe('completed');
    expect(component['stateFor'](2)).toBe('active');
    expect(component['stateFor'](3)).toBe('upcoming');
  });

  it('should render the literal "Étape X/4 — Label" caption for the active step', () => {
    const fixture = create(1);
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Étape 1/4 — Adresse');
  });

  it('should render a checkmark icon for completed steps', () => {
    const fixture = create(2);
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('svg')).toBeTruthy();
  });

  it('should clamp an out-of-range currentStep (0) instead of rendering "undefined"', () => {
    const fixture = create(0);
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Étape 1/4 — Adresse');
    expect(compiled.textContent).not.toContain('undefined');
  });

  it('should clamp an out-of-range currentStep (5) instead of rendering "undefined"', () => {
    const fixture = create(5);
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Étape 4/4 — Confirmée');
    expect(compiled.textContent).not.toContain('undefined');
  });
});
