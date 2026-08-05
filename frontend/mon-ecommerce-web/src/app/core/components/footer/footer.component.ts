import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ConsentService } from '../../services/consent.service';

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
  protected readonly consentService = inject(ConsentService);
  protected readonly year = new Date().getFullYear();

  // Story 8.2 AC #7: reopens the cookie banner without clearing the existing stored consent —
  // not a routerLink, there's no dedicated preferences route, it reopens the same global banner
  // component already mounted in app.component.html.
  protected reopenCookiePreferences(): void {
    this.consentService.reopen();
  }
}
