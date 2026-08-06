import axe from 'axe-core';

// Story 8.5, AC #7: axe-core (the framework-agnostic engine — not a Cypress/Playwright wrapper,
// this app's test runner is Karma/Jasmine) run directly against a component fixture's rendered
// DOM. Throws with a readable per-violation breakdown on failure, instead of Jasmine's default
// "expected [] to equal [...]" (which gives no indication of WHICH rule failed or where).
export async function expectNoAccessibilityViolations(element: HTMLElement): Promise<void> {
  const results = await axe.run(element);

  // Always exercises expect() on the success path too — a helper that only calls fail()
  // conditionally would leave a passing spec with zero registered expectations, which Jasmine
  // reports as a "has no expectations" warning (and fails outright under a strict CI config).
  const summary = results.violations
    .map((v) => `- [${v.id}] ${v.description} (${v.nodes.length} node(s))`)
    .join('\n');
  expect(results.violations.length)
    .withContext(summary ? `axe-core violations:\n${summary}` : '')
    .toBe(0);
}
