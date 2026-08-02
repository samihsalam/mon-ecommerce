import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SeoService } from '../../../../core/services/seo.service';

// Story 8.1: static content page — see cgv.component.ts's comment. Content deliberately stays
// consistent with CreateReturnRequestCommandHandler's 14-day ReturnWindow constant (backend,
// Story 5.1) — see this story's Dev Notes.
@Component({
  selector: 'app-returns-policy',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './returns-policy.component.html',
})
export class ReturnsPolicyComponent {
  constructor(seoService: SeoService) {
    seoService.setStaticPageSeo(
      'Politique de retours',
      'Comment retourner un article commandé sur Mon Ecommerce : délai, conditions et remboursement.',
    );
  }
}
