import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

// AC #2 (Story 8.1): links to the three legal pages, visible and accessible on every route —
// rendered once in app.component.html alongside the header, not per-page.
@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './footer.component.html',
  styleUrl: './footer.component.scss',
})
export class FooterComponent {
  protected readonly year = new Date().getFullYear();
}
