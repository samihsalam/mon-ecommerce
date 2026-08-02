import { Component } from '@angular/core';

import { SeoService } from '../../../../core/services/seo.service';

// Story 8.1: static content page — see cgv.component.ts's comment (same pattern, same Dev Notes
// caveat about placeholder legal copy).
@Component({
  selector: 'app-privacy-policy',
  standalone: true,
  templateUrl: './privacy-policy.component.html',
})
export class PrivacyPolicyComponent {
  constructor(seoService: SeoService) {
    seoService.setStaticPageSeo(
      'Politique de confidentialité',
      'Découvrez comment Mon Ecommerce collecte, utilise et protège vos données personnelles, et comment exercer vos droits RGPD.',
    );
  }
}
