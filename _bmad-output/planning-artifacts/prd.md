---
stepsCompleted: ['step-01-init', 'step-02-discovery', 'step-02b-vision', 'step-02c-executive-summary', 'step-03-success', 'step-04-journeys', 'step-05-domain', 'step-06-innovation', 'step-07-project-type', 'step-08-scoping', 'step-09-functional', 'step-10-nonfunctional', 'step-11-polish', 'step-12-complete']
inputDocuments: []
briefCount: 0
researchCount: 0
brainstormingCount: 0
projectDocsCount: 0
workflowType: 'prd'
classification:
  projectType: web_app_mobile
  domain: ecommerce_retail
  complexity: medium
  projectContext: greenfield
  stack:
    backend: .NET Core (Clean Architecture, CQRS, MediatR, EF Core)
    frontend: Angular 17+
    mobile: Flutter (iOS & Android)
    payment: Stripe
  businessModel: single_vendor_to_marketplace
---

# Product Requirements Document — mon-ecommerce

**Auteur :** Bouchta
**Date :** 2026-04-11

---

## Résumé Exécutif

**mon-ecommerce** est une plateforme e-commerce spécialisée dans la vente de sacs (mode, cuir, voyage, sport) et accessoires, conçue pour la nouvelle société de Bouchta. Elle répond à l'absence de solution verticale dédiée aux sacs offrant une expérience d'achat moderne et multicanale. Cible principale : acheteurs B2C. Évolution planifiée vers un modèle marketplace multi-vendeurs.

**Proposition de valeur :** permettre à tout client de trouver exactement le sac qu'il cherche en moins de 2 minutes — navigation guidée, filtres avancés, checkout optimisé sur web et mobile.

**Différenciateur :** là où les généralistes (Amazon, Zalando) noient l'acheteur dans des milliers de catégories, mon-ecommerce se concentre exclusivement sur les sacs. Chaque étape du parcours — découverte, filtrage, sélection, achat — réduit la friction et conduit naturellement vers le bon produit.

| Dimension | Valeur |
|-----------|--------|
| **Type de projet** | Web SPA Angular + Applications mobiles Flutter (iOS/Android) |
| **Domaine** | E-commerce / Mode & Accessoires |
| **Complexité** | Moyenne |
| **Contexte** | Greenfield |
| **Modèle business** | Single vendor (V1) → Marketplace multi-vendeurs (V2+) |
| **Stack** | .NET Core · Angular 17+ · Flutter · Stripe · SQL Server/PostgreSQL |

---

## Critères de Succès

### Succès Utilisateur

| Critère | Cible |
|---------|-------|
| Temps de découverte produit | ≤ 3 clics / ≤ 2 minutes |
| Fluidité du checkout | Commande complétée en ≤ 4 étapes |
| Score UX mobile | ≥ 85/100 (Lighthouse) |
| Satisfaction client | Note moyenne ≥ 4/5 |
| Taux de retour produits | < 15% |

### Succès Business

| Horizon | Cible |
|---------|-------|
| 1 mois | 1ère commande payée, ≥ 30 produits en ligne |
| 3 mois | ≥ 50 commandes, taux de conversion ≥ 2% |
| 6 mois | Panier moyen ≥ 60€, ≥ 20% de clients récurrents |
| 12 mois | Architecture prête pour ≥ 3 vendeurs marketplace |

### Succès Technique

| Critère | Cible |
|---------|-------|
| Performance | LCP < 2s, Lighthouse ≥ 85/100 |
| Disponibilité | ≥ 99.5% uptime |
| Paiement | 0 transaction échouée par bug plateforme |
| Mobile | Fonctionnel sur iOS 14+ et Android 10+ |

---

## Parcours Utilisateurs

### Parcours 1 — Salma, l'acheteuse pressée (chemin idéal)

**Persona :** Salma, 32 ans. Cherche un sac en cuir pour offrir à sa mère. 15 minutes disponibles.

**Déroulement :** Elle ouvre l'app Flutter sur iPhone. La page d'accueil présente les catégories visuelles (Cuir · Voyage · Sport · Mode). Elle sélectionne "Cuir" + filtres "Marron" + "< 80€". Trois sacs apparaissent. Elle consulte la galerie (6 photos), la description matière et les dimensions, puis ajoute au panier. Checkout en 4 étapes : adresse → livraison → Stripe → confirmation.

**Résolution :** Commande passée en 7 minutes. Email de confirmation reçu immédiatement.

**Capacités requises :** filtres avancés, galerie multi-photos, checkout rapide, paiement Stripe, email transactionnel.

---

### Parcours 2 — Karim, l'administrateur (gestion quotidienne)

**Persona :** Karim, gérant de la boutique. Reçoit une livraison de 20 nouveaux sacs.

**Déroulement :** Il ouvre le back-office depuis son PC. Il importe 20 fiches produits via CSV, ajoute les photos, définit les stocks et prix. Il traite 3 commandes en attente (statut → "Expédié", numéros de tracking saisis). Il consulte le tableau de bord : CA du jour, produits les plus vus, 2 références en stock critique.

**Résolution :** En 30 minutes, stock à jour, commandes traitées, vue claire sur la santé de la boutique.

**Capacités requises :** import CSV, gestion commandes et statuts, alertes stock, dashboard analytics.

---

### Parcours 3 — Ahmed, le client déçu (cas limite / retour)

**Persona :** Ahmed a reçu un sac dont la couleur ne correspond pas aux photos.

**Déroulement :** Il ouvre son espace client, retrouve la commande, clique "Signaler un problème". Formulaire de retour rempli en 2 minutes. Étiquette de retour prépayée reçue par email. L'admin valide le remboursement via le back-office (Stripe).

**Résolution :** Remboursement reçu sous 48h. L'expérience de retour fluide préserve la confiance malgré l'incident.

**Capacités requises :** espace client, formulaire de retour, workflow retour admin, remboursement Stripe, emails automatisés.

---

### Parcours 4 — Visiteur anonyme (découverte sans achat immédiat)

**Persona :** Découvre le site via Instagram, curieuse mais pas prête à acheter.

**Déroulement :** Parcourt le catalogue sans compte, utilise les filtres, consulte 5 fiches produits. Quitte le site. Revient deux jours plus tard suite à un email de rappel (post-MVP) et commande.

**Résolution :** La conversion se fait en deux temps. Le produit capte l'intention même différée.

**Capacités requises :** catalogue public sans compte (SEO), wishlist (post-MVP), email de relance (post-MVP).

---

### Tableau de Traçabilité Parcours → Capacités

| Capacité | Parcours | Phase |
|----------|----------|-------|
| Catalogue + filtres (catégorie, matière, couleur, prix) | 1, 4 | MVP |
| Galerie produit multi-photos | 1 | MVP |
| Checkout ≤ 4 étapes | 1 | MVP |
| Paiement Stripe + anti-overselling | 1, 3 | MVP |
| Emails transactionnels | 1, 3 | MVP |
| Back-office administrateur | 2 | MVP |
| Gestion commandes, statuts, tracking | 2, 3 | MVP |
| Alertes stock critique | 2 | MVP |
| Dashboard analytics | 2 | MVP |
| Espace client + historique commandes | 3 | MVP |
| Gestion retours et remboursements | 3 | MVP |
| Catalogue public SEO-indexable | 4 | MVP |
| Wishlist | 4 | Post-MVP |
| Email de relance panier abandonné | 4 | Post-MVP |
| Codes promotionnels | 2 | Post-MVP |

---

## Exigences Domaine

### Conformité & Réglementation

- **RGPD :** Consentement cookies explicite, politique de confidentialité, droit à l'oubli et portabilité des données. Pages dédiées obligatoires avant ouverture.
- **CGV & Mentions légales :** Conditions Générales de Vente, politique de retour, mentions légales éditeur — pages obligatoires.
- **Droit de rétractation :** 14 jours légaux pour retourner tout produit sans justification. Workflow retour intégré au MVP.
- **PCI-DSS :** Délégué à Stripe — aucune donnée de carte ne transite ou n'est stockée côté serveur.

### Intégrations Requises

| Service | Usage | Phase |
|---------|-------|-------|
| Stripe | Paiements carte + remboursements | MVP |
| SendGrid / Mailgun | Emails transactionnels | MVP |
| Transporteur partenaire | Tracking livraison automatique | Post-MVP |

### Risques et Mitigations

| Risque | Probabilité | Mitigation |
|--------|-------------|------------|
| Panne Stripe pendant checkout | Très faible | Message UX explicite + retry + logs |
| Données clients exposées | Faible | HTTPS, JWT, bcrypt, pas de logs sensibles |
| Non-conformité RGPD au lancement | Moyenne | Bannière cookies + CGV rédigées avant ouverture |
| Overselling (stock désynchronisé) | Moyenne | Vérification stock côté serveur à la confirmation, pas au panier |
| Rejet app mobile par les stores | Faible | Suivi guidelines Apple/Google dès le développement |
| Périmètre MVP trop large | Moyenne | Web en priorité, mobile en parallel track si nécessaire |

---

## Exigences Techniques

### Architecture

- **Backend :** .NET Core — Clean Architecture (Domain / Application / Infrastructure / API), CQRS + MediatR, Entity Framework Core, SQL Server ou PostgreSQL
- **Frontend Web :** Angular 17+ SPA avec SSR via Angular Universal (obligatoire pour le SEO)
- **Mobile :** Flutter (iOS 14+ · Android 10+), gestion d'état Riverpod ou BLoC, tokens JWT via Flutter Secure Storage
- **API :** REST versionnée `/api/v1/`, JWT avec refresh token, rate limiting 100 req/min/IP

### Matrice de Support Plateformes

| Plateforme | Support |
|------------|---------|
| Chrome, Firefox, Safari, Edge | 2 dernières versions majeures |
| iOS Safari | 14+ |
| Chrome Android | Dernière version |
| App iOS | iOS 14+ |
| App Android | Android 10+ (API 29+) |

### Stratégie SEO

- SSR Angular Universal — indexation complète du catalogue
- URLs sémantiques (ex: `/catalogue/sacs-cuir/sac-cabas-marron`)
- Méta-tags dynamiques par page produit (Open Graph inclus)
- Sitemap XML automatique
- Données structurées JSON-LD (rich snippets Google)

### Décisions Techniques Clés

| Sujet | Décision |
|-------|----------|
| Temps réel | Polling 30s en V1 (pas de WebSockets) |
| Mode hors-ligne mobile | Non requis en V1 |
| Fonctionnalités device | Appareil photo uniquement (V1) |
| Notifications push | Infrastructure prévue en V1, activation post-MVP |
| Responsive web | Mobile-first, breakpoints 768px / 1024px |

---

## Scoping & Roadmap

**Philosophie MVP :** prouver que le parcours d'achat guidé convertit avant d'ajouter des fonctionnalités secondaires. Équipe : 1 développeur senior full-stack ou 2-3 personnes.

### Phase 1 — MVP (Lancement)

*Parcours couverts : Salma (achat) · Karim (back-office) · Ahmed (retour)*

- Catalogue avec filtres avancés (catégorie, matière, couleur, prix) et recherche full-text
- Fiche produit complète (galerie, description, dimensions, stock)
- Panier + checkout ≤ 4 étapes + paiement Stripe
- Vérification stock anti-overselling côté serveur
- Compte client (inscription, connexion, historique, retours)
- Back-office admin (catalogue, stocks, commandes, alertes, dashboard)
- Emails transactionnels (confirmation, expédition, retour, remboursement)
- Application web Angular 17+ avec SSR
- Applications mobiles Flutter (iOS + Android)
- Authentification JWT sécurisée
- Pages légales (CGV, confidentialité, retours) + bannière RGPD

### Phase 2 — Croissance (Post-MVP)

*Déclenché à ≥ 50 commandes/mois*

- Wishlist et favoris
- Avis et notes produits
- Codes promotionnels
- Notifications push mobile
- Analytics avancé (conversion par catégorie, entonnoir)
- Email de relance panier abandonné
- Recommandations produits basiques

### Phase 3 — Marketplace (V2+)

*Déclenché après validation du modèle single-vendor*

- Onboarding marchands et vendeurs tiers
- Gestion commissions et reversements
- Espace vendeur avec analytics
- Tracking livraison multi-transporteurs automatisé
- Recommandations personnalisées avancées

---

## Exigences Fonctionnelles

### Découverte & Navigation Catalogue

- **FR1 :** Un visiteur peut parcourir le catalogue sans être connecté
- **FR2 :** Un visiteur peut filtrer les produits par catégorie, matière, couleur et prix
- **FR3 :** Un visiteur peut effectuer une recherche full-text dans le catalogue
- **FR4 :** Un visiteur peut consulter une fiche produit détaillée (galerie photos, description, dimensions, stock disponible)
- **FR5 :** Un visiteur peut naviguer vers des produits similaires d'une même catégorie
- **FR6 :** Un visiteur peut partager une fiche produit via un lien URL permanent et indexable

### Gestion du Compte Client

- **FR7 :** Un visiteur peut créer un compte client avec email et mot de passe
- **FR8 :** Un client peut se connecter et se déconnecter de son compte
- **FR9 :** Un client peut réinitialiser son mot de passe par email
- **FR10 :** Un client peut consulter et modifier ses informations personnelles (nom, adresse, email)
- **FR11 :** Un client peut consulter l'historique complet de ses commandes
- **FR12 :** Un client peut consulter le détail et le statut d'une commande spécifique

### Panier & Processus d'Achat

- **FR13 :** Un visiteur peut ajouter un produit au panier sans être connecté
- **FR14 :** Un visiteur peut modifier les quantités ou supprimer des articles du panier
- **FR15 :** Un client peut finaliser une commande en renseignant une adresse de livraison
- **FR16 :** Un client peut choisir un mode de livraison
- **FR17 :** Un client peut payer par carte bancaire via Stripe
- **FR18 :** Un client reçoit un email de confirmation immédiatement après paiement validé
- **FR19 :** Le système vérifie la disponibilité du stock au moment de la confirmation de commande

### Gestion des Commandes & Retours

- **FR20 :** Un client peut initier une demande de retour pour une commande livrée
- **FR21 :** Un client reçoit un email à chaque changement de statut de sa commande
- **FR22 :** Un administrateur peut émettre un remboursement via le back-office (Stripe)
- **FR23 :** Un client reçoit une confirmation de remboursement par email

### Administration du Catalogue

- **FR24 :** Un administrateur peut créer, modifier et supprimer des fiches produits
- **FR25 :** Un administrateur peut importer des produits en masse via CSV
- **FR26 :** Un administrateur peut gérer les stocks par produit (quantités et seuil d'alerte)
- **FR27 :** Un administrateur reçoit une alerte quand un produit atteint le seuil de stock critique
- **FR28 :** Un administrateur peut organiser les produits par catégories et sous-catégories
- **FR29 :** Un administrateur peut publier ou dépublier un produit

### Administration des Commandes

- **FR30 :** Un administrateur peut consulter toutes les commandes avec filtrage par statut, date et client
- **FR31 :** Un administrateur peut mettre à jour le statut d'une commande (en préparation / expédiée / livrée)
- **FR32 :** Un administrateur peut saisir un numéro de suivi de livraison
- **FR33 :** Un administrateur peut traiter les demandes de retour (validation et remboursement)

### Tableau de Bord & Analytics

- **FR34 :** Un administrateur peut consulter les indicateurs clés du jour (CA, commandes, panier moyen)
- **FR35 :** Un administrateur peut visualiser les produits les plus consultés et les mieux vendus
- **FR36 :** Un administrateur peut identifier les produits en rupture de stock imminente

### Conformité & Communication

- **FR37 :** Le système envoie des emails transactionnels automatiques (confirmation, expédition, retour, remboursement) dans un délai ≤ 30s après l'événement
- **FR38 :** Un visiteur peut consulter les pages légales (CGV, confidentialité, retours)
- **FR39 :** Un visiteur peut accepter ou refuser les cookies non-essentiels via une bannière RGPD
- **FR40 :** Un client peut demander la suppression de ses données personnelles

---

## Exigences Non-Fonctionnelles

### Performance

| Critère | Cible |
|---------|-------|
| LCP page catalogue | ≤ 2.5s sur mobile 4G |
| Chargement fiche produit | ≤ 2s |
| Réponse API | ≤ 500ms (95e percentile) |
| Résultats de recherche | ≤ 1s |
| Confirmation paiement Stripe | ≤ 3s |
| Score Lighthouse global | ≥ 85/100 |
| Charge simultanée V1 | 200 utilisateurs sans dégradation |

### Sécurité

| Critère | Cible |
|---------|-------|
| Transport | HTTPS sur tous les endpoints (TLS 1.2+) |
| Authentification | JWT expiration ≤ 1h + refresh token sécurisé |
| Mots de passe | bcrypt coût ≥ 12, jamais stockés en clair |
| Données paiement | Zéro donnée carte côté serveur (Stripe) |
| Données personnelles | Chiffrement en base de données |
| Rate limiting | Max 100 req/min/IP sur endpoints publics |
| Headers HTTP | CORS, CSP, X-Frame-Options, HSTS configurés |

### Scalabilité

| Critère | Cible |
|---------|-------|
| Catalogue | ≥ 10 000 références sans dégradation |
| Utilisateurs | Architecture supportant 10x la charge initiale sans refonte |
| Requêtes BDD | Indexées, toutes les listes paginées |
| Évolution marketplace | Modèle de données et API compatibles multi-vendeur dès V1 |

### Accessibilité

| Critère | Cible |
|---------|-------|
| Standard | WCAG 2.1 niveau AA |
| Contraste | Ratio ≥ 4.5:1 pour le texte normal |
| Navigation clavier | 100% des fonctionnalités accessibles |
| Images | Attributs `alt` sur toutes les images produits |
| Formulaires | Labels associés à tous les champs |

### Fiabilité & Intégrations

| Critère | Cible |
|---------|-------|
| Disponibilité | ≥ 99.5% uptime (hors maintenance planifiée) |
| Sauvegarde | Backup quotidien automatique de la base |
| Audit paiements | Traçabilité complète de toutes les transactions |
| Gestion erreurs | Aucune erreur 5xx exposée à l'utilisateur final |
| Stripe | Dégradation gracieuse si indisponible (message UX + retry) |
| Email transactionnel | Livraison ≤ 30s après événement déclencheur |
| Stores mobiles | Conformité App Store (iOS 14+) et Google Play (Android 10+) |
