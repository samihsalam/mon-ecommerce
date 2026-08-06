import { Component } from '@angular/core';

// AC #3 (Story 8.4): first focusable element on every page — mounted first in app.component.html,
// before <app-header />. Visually hidden until focused (sr-only / focus:not-sr-only) so it never
// affects sighted layout, but jumps straight to the top of the Tab order for keyboard users.
@Component({
  selector: 'app-skip-link',
  standalone: true,
  templateUrl: './skip-link.component.html',
  styleUrl: './skip-link.component.scss',
})
export class SkipLinkComponent {}
