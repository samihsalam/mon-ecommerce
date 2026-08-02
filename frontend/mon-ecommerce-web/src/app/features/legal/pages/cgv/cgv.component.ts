import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SeoService } from '../../../../core/services/seo.service';

// Story 8.1: static content page, no data fetching — SSR "just works" the same way every other
// route component already does. Placeholder legal copy, NOT reviewed/approved legal text — see
// this story's Dev Notes (AC #5 cannot be satisfied by any engineering story).
@Component({
  selector: 'app-cgv',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './cgv.component.html',
})
export class CgvComponent {
  constructor(seoService: SeoService) {
    seoService.setStaticPageSeo(
      'Conditions générales de vente',
      "Consultez les conditions générales de vente de Mon Ecommerce : commande, paiement, livraison, garanties et litiges.",
    );
  }
}
