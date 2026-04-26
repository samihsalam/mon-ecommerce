---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: ['prd.md', 'ux-design-specification.md', 'architecture.md']
workflowType: 'epics-and-stories'
project_name: 'mon-ecommerce'
user_name: 'Bouchta'
date: '2026-04-12'
status: 'complete'
completedAt: '2026-04-12'
lastStep: 4
---

# mon-ecommerce - Epic Breakdown

## Overview

Ce document fournit le découpage complet en epics et stories pour mon-ecommerce, décomposant les exigences du PRD, de la Spec UX et de l'Architecture en stories implémentables.

## Requirements Inventory

### Functional Requirements

FR1: Un visiteur peut parcourir le catalogue sans être connecté
FR2: Un visiteur peut filtrer les produits par catégorie, matière, couleur et prix
FR3: Un visiteur peut effectuer une recherche full-text dans le catalogue
FR4: Un visiteur peut consulter une fiche produit détaillée (galerie photos, description, dimensions, stock disponible)
FR5: Un visiteur peut naviguer vers des produits similaires d'une même catégorie
FR6: Un visiteur peut partager une fiche produit via un lien URL permanent et indexable
FR7: Un visiteur peut créer un compte client avec email et mot de passe
FR8: Un client peut se connecter et se déconnecter de son compte
FR9: Un client peut réinitialiser son mot de passe par email
FR10: Un client peut consulter et modifier ses informations personnelles (nom, adresse, email)
FR11: Un client peut consulter l'historique complet de ses commandes
FR12: Un client peut consulter le détail et le statut d'une commande spécifique
FR13: Un visiteur peut ajouter un produit au panier sans être connecté
FR14: Un visiteur peut modifier les quantités ou supprimer des articles du panier
FR15: Un client peut finaliser une commande en renseignant une adresse de livraison
FR16: Un client peut choisir un mode de livraison
FR17: Un client peut payer par carte bancaire via Stripe
FR18: Un client reçoit un email de confirmation immédiatement après paiement validé
FR19: Le système vérifie la disponibilité du stock au moment de la confirmation de commande (anti-overselling)
FR20: Un client peut initier une demande de retour pour une commande livrée
FR21: Un client reçoit un email à chaque changement de statut de sa commande
FR22: Un administrateur peut émettre un remboursement via le back-office (Stripe)
FR23: Un client reçoit une confirmation de remboursement par email
FR24: Un administrateur peut créer, modifier et supprimer des fiches produits
FR25: Un administrateur peut importer des produits en masse via CSV
FR26: Un administrateur peut gérer les stocks par produit (quantités et seuil d'alerte)
FR27: Un administrateur reçoit une alerte quand un produit atteint le seuil de stock critique
FR28: Un administrateur peut organiser les produits par catégories et sous-catégories
FR29: Un administrateur peut publier ou dépublier un produit
FR30: Un administrateur peut consulter toutes les commandes avec filtrage par statut, date et client
FR31: Un administrateur peut mettre à jour le statut d'une commande (en préparation / expédiée / livrée)
FR32: Un administrateur peut saisir un numéro de suivi de livraison
FR33: Un administrateur peut traiter les demandes de retour (validation et remboursement)
FR34: Un administrateur peut consulter les indicateurs clés du jour (CA, commandes, panier moyen)
FR35: Un administrateur peut visualiser les produits les plus consultés et les mieux vendus
FR36: Un administrateur peut identifier les produits en rupture de stock imminente
FR37: Le système envoie des emails transactionnels automatiques (confirmation, expédition, retour, remboursement) dans un délai ≤ 30s après l'événement
FR38: Un visiteur peut consulter les pages légales (CGV, confidentialité, retours)
FR39: Un visiteur peut accepter ou refuser les cookies non-essentiels via une bannière RGPD
FR40: Un client peut demander la suppression de ses données personnelles

### NonFunctional Requirements

NFR1: Performance — LCP catalogue ≤ 2.5s sur mobile 4G ; chargement fiche produit ≤ 2s ; réponse API ≤ 500ms p95 ; résultats recherche ≤ 1s ; confirmation paiement ≤ 3s
NFR2: Lighthouse — Score global ≥ 85/100 ; Score UX mobile ≥ 85/100
NFR3: Charge simultanée — 200 utilisateurs sans dégradation ; architecture supportant 10x la charge initiale sans refonte
NFR4: Scalabilité catalogue — ≥ 10 000 références sans dégradation ; toutes les listes paginées ; requêtes BDD indexées
NFR5: Disponibilité — ≥ 99.5% uptime hors maintenance planifiée ; backup quotidien automatique
NFR6: Sécurité — HTTPS TLS 1.2+ ; JWT expiration ≤ 1h + refresh token sécurisé ; bcrypt coût ≥ 12 ; zéro donnée carte côté serveur ; rate limiting 100 req/min/IP ; CORS, CSP, X-Frame-Options, HSTS
NFR7: Accessibilité — WCAG 2.1 niveau AA ; contraste ≥ 4.5:1 ; navigation clavier 100% ; alt sur toutes les images produits ; labels sur tous les formulaires
NFR8: Fiabilité paiement — 0 transaction échouée par bug plateforme ; audit trail complet ; dégradation gracieuse Stripe si indisponible
NFR9: Emails transactionnels — livraison ≤ 30s après événement déclencheur
NFR10: Conformité stores mobiles — App Store iOS 14+ et Google Play Android 10+ (API 29+)

### Additional Requirements

- AR1: Initialisation des 3 projets avec les starters définis en architecture (dotnet ca-sln + ng new --ssr + flutter create)
- AR2: docker-compose.yml dev local (PostgreSQL + Redis) à la racine du dossier backend
- AR3: EF Core migrations code-first pour toutes les entités Domain (Product, Order, Cart, User, RefreshToken, etc.)
- AR4: ASP.NET Core Identity + table RefreshTokens PostgreSQL + rotation à chaque usage
- AR5: Pipeline Behaviors MediatR : ValidationBehaviour + LoggingBehaviour + PerformanceBehaviour
- AR6: Domain Events MediatR Notifications : OrderPlacedEvent → OrderPlacedEmailHandler → SendGrid
- AR7: Cloudinary SDK .NET integration (FileStorageService) + upload images + URL transforms WebP
- AR8: Redis StackExchange.Redis : cache catalogue TTL 5min + panier anonyme TTL 24h + invalidation sur mutations
- AR9: Stripe.net : Payment Intents (clientSecret retourné client) + webhooks signés + IssueRefund
- AR10: Concurrence optimiste EF Core : ConcurrencyToken + xmin PostgreSQL sur entité Stock
- AR11: Serilog via ILogger<T> : structured logging + correlation ID enrichment
- AR12: GitHub Actions CI/CD : workflow build/test + workflow deploy Railway (backend) + Vercel (frontend)
- AR13: Dockerfile par service + docker-compose prod
- AR14: Sentry Free SDK : .NET (backend) + Angular (frontend) + Flutter (mobile)
- AR15: vendor_id sur entités Product, Order, Stock dès le schéma V1 (marketplace-readiness)
- AR16: OpenAPI 3.0 via Swashbuckle : Swagger UI activé en développement
- AR17: ProblemDetails RFC 7807 : ExceptionHandlerMiddleware global + ValidationBehaviour 422

### UX Design Requirements

UX-DR1: Implémenter les design tokens palette "Élégance Naturelle" via CSS custom properties Tailwind : #FFFFFF (fond), #111111 (texte), #C9A96E (accent), #FAF8F5 (fond secondaire), #555555 (texte secondaire), #E5E5E5 (bordures), #6B8F71 (succès), #C0564A (erreur)
UX-DR2: Implémenter le système typographique Cormorant Garamond (H1 32–48px Regular, H2 24–32px Medium) + DM Sans (H3 18–20px SemiBold, corps 14–16px Regular, labels 13–14px Medium, boutons 14px SemiBold +0.5px letterspacing, captions 12px) via @fontsource ou CDN
UX-DR3: Implémenter le design_tokens.dart Flutter avec les mêmes tokens couleur, typographie, espacement et rayons que la spec web (cohérence cross-canal)
UX-DR4: Implémenter le système d'espacement 8px (4/8/16/24/32/48/64px) + marges page (16px mobile, 24–40px desktop) + max-width 1280px + border-radius 4px cards/inputs / 2px boutons
UX-DR5: Implémenter les breakpoints Tailwind : sm:640px (grille 2 col) / md:768px (filtres sidebar) / lg:1024px (3 col) / xl:1280px (4 col max)
UX-DR6: Composant ProductCard (Angular standalone + Flutter widget) — 3 variants (Grid/List/Featured) · 4 états (Default/Hover scale 1.02/Favoris/Rupture overlay grisé) · ratio image 3:4 · role="article" aria-label="[Nom], [prix]" · focus ring beige doré 2px
UX-DR7: Composant FilterChipBar (Angular + Flutter) — chips scrollables horizontalement · badge compteur résultats · bouton reset · 4 états (Default/Actif fond #111111/Hover/Disabled) · déclenche bottom sheet filtres sur mobile · role="group" aria-pressed + aria-live compteur
UX-DR8: Composant ProductGallery (Angular + Flutter) — image principale + thumbnails cliquables · dots mobile · zoom hover desktop · skeleton loading · navigation clavier ←/→ · aria-roledescription="carousel" aria-label sur chaque image
UX-DR9: Composant StickyAddToCart (Angular + Flutter) — sticky bas pleine largeur mobile / inline colonne droite desktop · 4 états (Default/Loading spinner/Succès animation/Rupture disabled) · aria-live message succès · aria-disabled rupture
UX-DR10: Composant CartDrawer (Angular + Flutter) — drawer latéral 400px desktop / bottom sheet mobile · états Vide (illustration) / Rempli / Loading · role="dialog" aria-modal="true" focus trap · fermeture Echap
UX-DR11: Composant OrderStepIndicator (Angular + Flutter) — steps numérotés + ligne connexion + label · états Complété(or+check)/Actif(noir)/À venir(gris) · horizontal desktop / dots compact mobile · aria-current="step"
UX-DR12: Skip link "Aller au contenu principal" premier élément focusable sur chaque page (web Angular)
UX-DR13: Focus visible outline 2px #C9A96E offset 2px sur tous les éléments interactifs (web + mobile)
UX-DR14: alt descriptif systématique sur toutes les images produit ("Tote Parisienne en cuir cognac, vue de face")
UX-DR15: ARIA landmarks (main, nav, aside) sur toutes les pages Angular + Semantics widgets Flutter
UX-DR16: aria-live regions pour mises à jour compteur filtres ("X sacs trouvés") et badge panier
UX-DR17: Validation formulaires onBlur — erreurs inline sous champ en #C0564A avec icône ⚠ et aria-describedby — labels toujours visibles au-dessus du champ
UX-DR18: Focus trap dans toutes les modales/overlays · fermeture Echap · scroll body bloqué · z-index : nav 100 / modales 200 / toasts 300
UX-DR19: Pagination "infinite scroll" avec bouton "Charger plus" explicite (pas d'auto-scroll infini) + filtres persistants au retour depuis fiche produit
UX-DR20: États vides pour tous les contextes : panier ("Votre panier est vide" → catalogue) · favoris · 0 résultat recherche (suggestions catégories) · commandes vides

### FR Coverage Map

FR1: Epic 3 — Catalogue Produits & Découverte (parcourir sans compte)
FR2: Epic 3 — Catalogue Produits & Découverte (filtres catégorie, matière, couleur, prix)
FR3: Epic 3 — Catalogue Produits & Découverte (recherche full-text)
FR4: Epic 3 — Catalogue Produits & Découverte (fiche produit détaillée)
FR5: Epic 3 — Catalogue Produits & Découverte (produits similaires)
FR6: Epic 3 — Catalogue Produits & Découverte (URL permanente et indexable)
FR7: Epic 2 — Authentification & Compte Client (création compte)
FR8: Epic 2 — Authentification & Compte Client (connexion / déconnexion)
FR9: Epic 2 — Authentification & Compte Client (réinitialisation mot de passe)
FR10: Epic 2 — Authentification & Compte Client (modifier informations personnelles)
FR11: Epic 2 — Authentification & Compte Client (historique commandes)
FR12: Epic 2 — Authentification & Compte Client (détail et statut d'une commande)
FR13: Epic 4 — Panier & Checkout (ajouter au panier sans compte)
FR14: Epic 4 — Panier & Checkout (modifier quantités / supprimer articles)
FR15: Epic 4 — Panier & Checkout (adresse de livraison)
FR16: Epic 4 — Panier & Checkout (choix mode de livraison)
FR17: Epic 4 — Panier & Checkout (paiement Stripe)
FR18: Epic 4 — Panier & Checkout (email confirmation après paiement)
FR19: Epic 4 — Panier & Checkout (vérification stock anti-overselling)
FR20: Epic 5 — Commandes, Retours & Notifications (demande de retour)
FR21: Epic 5 — Commandes, Retours & Notifications (email changement statut commande)
FR22: Epic 5 — Commandes, Retours & Notifications (remboursement Stripe admin)
FR23: Epic 5 — Commandes, Retours & Notifications (email confirmation remboursement)
FR24: Epic 6 — Administration Catalogue (CRUD fiches produits)
FR25: Epic 6 — Administration Catalogue (import CSV en masse)
FR26: Epic 6 — Administration Catalogue (gestion stocks et seuils d'alerte)
FR27: Epic 6 — Administration Catalogue (alerte stock critique)
FR28: Epic 6 — Administration Catalogue (catégories et sous-catégories)
FR29: Epic 6 — Administration Catalogue (publier / dépublier produit)
FR30: Epic 7 — Administration Commandes & Dashboard (liste commandes avec filtres)
FR31: Epic 7 — Administration Commandes & Dashboard (mise à jour statut commande)
FR32: Epic 7 — Administration Commandes & Dashboard (saisie numéro suivi)
FR33: Epic 7 — Administration Commandes & Dashboard (traitement retours)
FR34: Epic 7 — Administration Commandes & Dashboard (KPIs du jour)
FR35: Epic 7 — Administration Commandes & Dashboard (produits les plus consultés / vendus)
FR36: Epic 7 — Administration Commandes & Dashboard (produits en rupture imminente)
FR37: Epic 5 — Commandes, Retours & Notifications (infrastructure emails transactionnels ≤30s)
FR38: Epic 8 — Conformité, Accessibilité & Qualité (pages légales CGV/confidentialité/retours)
FR39: Epic 8 — Conformité, Accessibilité & Qualité (bannière RGPD cookies)
FR40: Epic 8 — Conformité, Accessibilité & Qualité (suppression données personnelles)

## Epic List

### Epic 1: Foundation & Infrastructure
La plateforme est initialisée et fonctionnelle en local : les 3 projets (backend .NET, frontend Angular, mobile Flutter) tournent, la base de données PostgreSQL est migrée avec le schéma complet, Redis est configuré, le CI/CD est en place et les outils de monitoring sont branchés.
**ARs couverts :** AR1–AR17
**UX-DRs couverts :** UX-DR1–UX-DR5 (design tokens, typographie, espacement, breakpoints Tailwind, design_tokens.dart Flutter)

### Epic 2: Authentification & Compte Client
Les visiteurs peuvent créer un compte, se connecter/déconnecter, réinitialiser leur mot de passe, modifier leur profil et consulter leur historique de commandes.
**FRs couverts :** FR7, FR8, FR9, FR10, FR11, FR12

### Epic 3: Catalogue Produits & Découverte
Tout visiteur peut parcourir le catalogue sans compte, filtrer par catégorie/matière/couleur/prix, faire une recherche full-text, consulter une fiche produit complète avec galerie, naviguer vers des produits similaires et partager une fiche via URL indexable.
**FRs couverts :** FR1, FR2, FR3, FR4, FR5, FR6
**UX-DRs couverts :** UX-DR6 (ProductCard), UX-DR7 (FilterChipBar), UX-DR8 (ProductGallery), UX-DR12–UX-DR16

### Epic 4: Panier & Checkout
Les visiteurs peuvent ajouter des articles au panier sans compte, modifier leur panier, renseigner une adresse de livraison, choisir un mode de livraison, payer par carte via Stripe, recevoir un email de confirmation — avec vérification anti-overselling côté serveur.
**FRs couverts :** FR13, FR14, FR15, FR16, FR17, FR18, FR19
**UX-DRs couverts :** UX-DR9 (StickyAddToCart), UX-DR10 (CartDrawer), UX-DR11 (OrderStepIndicator)

### Epic 5: Commandes, Retours & Notifications
Les clients peuvent initier une demande de retour, recevoir des emails à chaque changement de statut de commande et à chaque remboursement. L'infrastructure d'emails transactionnels (Domain Events → SendGrid, ≤30s) est opérationnelle pour tous les événements de la plateforme.
**FRs couverts :** FR20, FR21, FR22, FR23, FR37

### Epic 6: Administration Catalogue
L'administrateur peut créer, modifier et supprimer des fiches produits, importer en masse via CSV, gérer les stocks avec seuils d'alerte, organiser par catégories/sous-catégories et publier ou dépublier des produits.
**FRs couverts :** FR24, FR25, FR26, FR27, FR28, FR29

### Epic 7: Administration Commandes & Dashboard
L'administrateur peut consulter et filtrer toutes les commandes, mettre à jour leurs statuts, saisir des numéros de suivi, traiter les retours avec remboursement Stripe, et visualiser les KPIs clés (CA, commandes, produits populaires, stocks critiques).
**FRs couverts :** FR30, FR31, FR32, FR33, FR34, FR35, FR36

### Epic 8: Conformité, Accessibilité & Qualité
La plateforme est légalement conforme (pages CGV/confidentialité/retours, bannière RGPD, droit à l'oubli) et accessible WCAG 2.1 AA (validation formulaires onBlur, focus trap modales, pagination explicite, états vides).
**FRs couverts :** FR38, FR39, FR40
**UX-DRs couverts :** UX-DR17–UX-DR20
**NFRs couverts :** NFR7 (WCAG 2.1 AA)

---

## Epic 1: Foundation & Infrastructure

La plateforme est initialisée et fonctionnelle en local : les 3 projets (backend .NET 9, frontend Angular 19 SSR, mobile Flutter 3.41) tournent, la base de données PostgreSQL est migrée avec le schéma complet, Redis est configuré, le CI/CD est en place et les outils de monitoring sont branchés.

### Story 1.1: Initialisation des 3 Projets

As a developer,
I want to initialize the three projects (backend .NET 9, frontend Angular 19 SSR, mobile Flutter 3.41) using the architecture-defined starters,
So that the team has a versioned, compilable codebase from day one.

**Acceptance Criteria:**

**Given** a fresh repository with the monorepo structure `backend/` `frontend/` `mobile/`
**When** the three init commands are executed (`dotnet new ca-sln`, `ng new --ssr`, `flutter create`)
**Then** each project compiles without errors (`dotnet build`, `ng build`, `flutter build apk`)
**And** the monorepo structure matches the architecture spec exactly
**And** `.gitignore` files are present and exclude build artifacts, secrets, and IDE files
**And** the solution can be opened and run locally without additional configuration

---

### Story 1.2: Environnement de Développement Local

As a developer,
I want a `docker-compose.yml` that starts PostgreSQL and Redis locally with a single command,
So that I can develop without cloud dependencies and onboard new team members quickly.

**Acceptance Criteria:**

**Given** Docker Desktop is installed
**When** `docker compose up` is run from the `backend/` directory
**Then** PostgreSQL is accessible on port 5432 with the correct database and credentials
**And** Redis is accessible on port 6379
**And** the backend connects successfully to both services on startup
**And** a `.env.example` file documents all required environment variables (DB connection, Redis, JWT secret, Stripe keys, Cloudinary, SendGrid)
**And** a `.env` file (gitignored) can be created from `.env.example` without modification for local dev

---

### Story 1.3: Schéma Domain & Migrations EF Core

As a developer,
I want all Domain entities modelled and migrated to PostgreSQL with the complete schema (including marketplace-ready `vendor_id`),
So that all future feature stories have a stable data foundation to build on.

**Acceptance Criteria:**

**Given** PostgreSQL is running via docker-compose
**When** `dotnet ef database update` is run
**Then** all tables are created: `products`, `categories`, `product_images`, `orders`, `order_items`, `carts`, `cart_items`, `users`, `addresses`, `refresh_tokens`
**And** `vendor_id UUID` column is present on `products`, `orders`, and a `stock` table
**And** all table and column names follow `snake_case` convention
**And** primary keys are UUID (`gen_random_uuid()`), foreign keys follow `{table_singulier}_id` convention
**And** required indexes are created (`ix_products_category_id`, `ix_orders_user_id`, etc.)
**And** the migration is idempotent (running it twice has no effect)

---

### Story 1.4: Infrastructure Authentification JWT

As a developer,
I want ASP.NET Core Identity configured with JWT access tokens (1h) and refresh token rotation stored in PostgreSQL,
So that all subsequent feature stories have a secure, ready-to-use authentication foundation.

**Acceptance Criteria:**

**Given** a valid email and password
**When** `POST /api/v1/auth/register` is called
**Then** the user is created with bcrypt hash (cost ≥ 12) and a `200` response with `accessToken` and `refreshToken` is returned

**Given** a valid `refreshToken`
**When** `POST /api/v1/auth/refresh` is called
**Then** a new `accessToken` and `refreshToken` are issued and the old refresh token is invalidated (rotation)

**Given** an IP making > 100 requests/minute
**When** the 101st request arrives on a public endpoint
**Then** a `429 Too Many Requests` ProblemDetails response is returned

**And** the `access_token` expires in 1 hour
**And** the `refresh_token` expires in 7 days and is stored in the `refresh_tokens` table
**And** passwords are never stored in plain text or logged

---

### Story 1.5: Couche Application — Pipeline MediatR & Error Handling

As a developer,
I want MediatR Pipeline Behaviors (ValidationBehaviour, LoggingBehaviour, PerformanceBehaviour), ProblemDetails RFC 7807 global error handling, OpenAPI/Swashbuckle, and Serilog configured,
So that all future feature handlers automatically benefit from validation, structured logging, and API documentation.

**Acceptance Criteria:**

**Given** a command with a missing required field is sent
**When** the MediatR pipeline processes it
**Then** a `422 Unprocessable Entity` ProblemDetails response is returned with field-level errors

**Given** a handler throws an unhandled exception
**When** the global ExceptionHandler middleware catches it
**Then** a `500` ProblemDetails response is returned with no stack trace exposed

**Given** the app is running in development mode
**When** `/swagger` is accessed
**Then** the Swagger UI is displayed with all endpoints documented

**And** structured JSON logs are emitted via `ILogger<T>` (Serilog sink)
**And** any handler taking > 500ms logs a performance warning
**And** `Console.WriteLine` is never used (lint rule or convention documented)

---

### Story 1.6: Intégration Services Externes

As a developer,
I want the interfaces and implementations for Cloudinary (`IFileStorageService`), Redis (`ICacheService`), SendGrid (`IEmailService`), and Stripe (`IPaymentService`) configured and testable,
So that feature stories can consume them without direct coupling to external SDKs.

**Acceptance Criteria:**

**Given** valid Cloudinary credentials
**When** `IFileStorageService.UploadAsync(file)` is called
**Then** a WebP-optimized URL is returned and the image is accessible via CDN

**Given** a cache key and value
**When** `ICacheService.SetAsync(key, value, ttl)` is called and then `GetAsync(key)` is called
**Then** the value is returned within the TTL and `null` is returned after expiry

**Given** valid SendGrid credentials
**When** `IEmailService.SendAsync(to, subject, body)` is called
**Then** the email is delivered (verified in SendGrid test mode)

**Given** valid Stripe test keys
**When** `IPaymentService.CreatePaymentIntentAsync(amountInCents)` is called
**Then** a `clientSecret` is returned and no card data is stored server-side

**And** all four services are registered in DI and injectable via their interfaces
**And** all credentials come from environment variables (never hardcoded)

---

### Story 1.7: Infrastructure Domain Events

As a developer,
I want MediatR Notifications infrastructure with `OrderPlacedEvent`, `OrderShippedEvent`, `ReturnRequestedEvent`, `RefundIssuedEvent` and their email handlers wired up,
So that all transactional emails fire automatically from domain events without coupling business logic to email sending.

**Acceptance Criteria:**

**Given** an `OrderPlacedEvent` is published via `IMediator.Publish()`
**When** the handler processes it
**Then** `IEmailService.SendAsync` is called with the correct order confirmation data

**Given** an `OrderShippedEvent` is published
**When** the handler processes it
**Then** the shipment notification email is dispatched within ≤ 30 seconds

**And** all events are immutable C# `record` types with required payload fields
**And** event handler naming follows `{Event}{Action}Handler` convention (`OrderPlacedEmailHandler`)
**And** handler failures are logged but do not throw to the caller (fire-and-forget pattern)
**And** unit tests cover each event handler in isolation

---

### Story 1.8: Design System Foundation

As a frontend/mobile developer,
I want the Tailwind CSS v4 design tokens (Élégance Naturelle palette, typography scale, spacing, breakpoints) and Flutter `design_tokens.dart` configured,
So that all UI components share consistent visual values across web and mobile.

**Acceptance Criteria:**

**Given** the Angular app is running
**When** any component uses Tailwind utility classes
**Then** CSS custom properties `--color-accent: #C9A96E`, `--color-text: #111111`, `--color-bg: #FFFFFF`, `--color-bg-secondary: #FAF8F5` are available globally

**Given** a heading element uses the configured typography
**When** rendered in the browser
**Then** Cormorant Garamond is applied to H1/H2 and DM Sans to body, labels, and buttons

**Given** the Flutter app is running
**When** any widget references `AppTokens.accentColor`
**Then** `#C9A96E` is applied consistently

**And** Tailwind breakpoints are configured: `sm:640px` `md:768px` `lg:1024px` `xl:1280px`
**And** the 8px grid spacing scale (4/8/16/24/32/48/64px) is documented in a shared config
**And** border-radius tokens are set: 4px for cards/inputs, 2px for buttons

---

### Story 1.9: CI/CD Pipeline & Déploiement

As a developer,
I want GitHub Actions pipelines (build + test) and Docker/Railway/Vercel deployment configurations in place,
So that every push is automatically validated and deployment is possible from the very first feature.

**Acceptance Criteria:**

**Given** a push to the `main` branch
**When** the GitHub Actions CI workflow runs
**Then** `dotnet build && dotnet test`, `ng build`, and `flutter build apk` all pass
**And** any failing test blocks the merge

**Given** the backend Dockerfile
**When** `docker build` is run
**Then** the image builds successfully and `docker run` starts the API on port 8080

**Given** Sentry DSNs are configured in environment variables
**When** the three apps start
**Then** Sentry is initialized in .NET (backend), Angular (frontend), and Flutter (mobile) and a test error is captured

**And** Railway configuration deploys the backend Docker image on push to `main`
**And** Vercel configuration deploys the Angular SSR app on push to `main`
**And** no secrets are committed to the repository (validated by CI secret-scan step)

---

## Epic 2: Authentification & Compte Client

Les visiteurs peuvent créer un compte, se connecter/déconnecter, réinitialiser leur mot de passe, modifier leur profil et consulter leur historique de commandes.

### Story 2.1: Inscription Client

As a visitor,
I want to create an account with my email and password,
So that I can access customer-only features (checkout, order history, returns).

**Acceptance Criteria:**

**Given** a visitor fills in name, email, and password (≥ 8 characters) on the registration form
**When** they submit
**Then** `POST /api/v1/auth/register` returns `200` with `accessToken` and `refreshToken`
**And** a welcome email is sent via SendGrid within 30 seconds
**And** the user is redirected to the catalogue

**Given** an email address already registered
**When** registration is attempted with that email
**Then** a `409 Conflict` ProblemDetails response is returned with a clear message

**And** password field validation runs onBlur (inline error under field in `#C0564A`)
**And** the password is never stored in plain text or returned in any API response
**And** the form works identically on Angular web and Flutter mobile

---

### Story 2.2: Connexion & Déconnexion

As a customer,
I want to log in with my email and password and log out,
So that I can access my account securely from web and mobile.

**Acceptance Criteria:**

**Given** valid credentials
**When** `POST /api/v1/auth/login` is called
**Then** `accessToken` (1h) and `refreshToken` (7d) are returned

**Given** the user is logged in on Angular web
**When** they click "Se déconnecter"
**Then** the `refreshToken` is revoked in the database and local tokens are cleared

**Given** an expired `accessToken`
**When** any authenticated API call is made
**Then** the Angular `AuthInterceptor` / Dio interceptor automatically calls `/auth/refresh` and retries the request

**And** tokens are stored in `localStorage` (Angular) and `flutter_secure_storage` (Flutter)
**And** after login the user is redirected to the catalogue (or the page they came from)
**And** a `401` on refresh failure redirects to the login page without data loss

---

### Story 2.3: Réinitialisation du Mot de Passe

As a customer,
I want to reset my password via email,
So that I can recover access to my account if I forget it.

**Acceptance Criteria:**

**Given** a registered email address
**When** `POST /api/v1/auth/forgot-password` is called
**Then** a reset email with a secure token link (valid 1 hour) is sent within 30 seconds

**Given** a valid reset token
**When** `POST /api/v1/auth/reset-password` is called with a new password
**Then** the password is updated and all existing refresh tokens for that user are revoked

**Given** an expired or already-used reset token
**When** the reset is attempted
**Then** a `400 Bad Request` ProblemDetails response is returned with a clear expiry message

**And** the reset link is single-use (invalidated after first successful use)
**And** the reset email template matches the platform's visual identity (DM Sans, Élégance Naturelle palette)

---

### Story 2.4: Profil Client — Consultation & Modification

As a customer,
I want to view and edit my personal information (name, address, email),
So that my details are always up to date for future orders.

**Acceptance Criteria:**

**Given** an authenticated customer
**When** `GET /api/v1/account/profile` is called
**Then** name, email, and saved addresses are returned

**Given** valid updated data
**When** `PATCH /api/v1/account/profile` is called
**Then** the changes are persisted and the updated profile is returned

**And** form validation runs onBlur with inline errors (aria-describedby linked to field)
**And** a success snackbar/toast confirms the save ("Profil mis à jour")
**And** email change requires current password confirmation
**And** the screen is accessible on Angular web and Flutter mobile

---

### Story 2.5: Historique des Commandes

As a customer,
I want to view the list of all my orders and the detail of each one,
So that I can track my past purchases and their current status.

**Acceptance Criteria:**

**Given** an authenticated customer with past orders
**When** `GET /api/v1/account/orders` is called
**Then** a paginated list is returned (date, total amount in cents, status, order number)

**Given** a specific order ID
**When** `GET /api/v1/account/orders/{orderId}` is called
**Then** full order details are returned (items, quantities, prices, delivery address, tracking number if available)

**Given** a customer with no orders
**When** the orders page is displayed
**Then** an empty state is shown: "Aucune commande pour le moment" with a CTA "Commencer à shopper"

**And** the order history is accessible in 2 taps from the profile screen (mobile)
**And** order status labels are human-readable in French (En préparation · Expédiée · Livrée · Annulée)
**And** the list is sorted by date descending (most recent first)

---

## Epic 3: Catalogue Produits & Découverte

Tout visiteur peut parcourir le catalogue sans compte, filtrer par catégorie/matière/couleur/prix, faire une recherche full-text, consulter une fiche produit complète avec galerie, naviguer vers des produits similaires et partager une fiche via URL indexable.

### Story 3.1: API Catalogue — Liste & Filtres

As a visitor,
I want to browse the product catalogue and filter by category, material, color, and price,
So that I can quickly narrow down to relevant products.

**Acceptance Criteria:**

**Given** a visitor requests the catalogue
**When** `GET /api/v1/products` is called (with optional query params `categoryId`, `material`, `color`, `priceMin`, `priceMax`, `pageNumber`, `pageSize`)
**Then** a paginated response is returned: `{ items, totalCount, pageNumber, pageSize, totalPages }`

**Given** the same filter combination is requested within 5 minutes
**When** the request hits the API
**Then** the response is served from Redis cache (TTL 5 min)

**Given** a product is created, updated, or deleted
**When** the mutation completes
**Then** the catalogue cache is invalidated immediately

**And** response time is ≤ 500ms at p95 under normal load
**And** `pageSize` is capped at 100
**And** prices are returned as integers in cents (`28500` = 285,00€)
**And** all list endpoints include pagination metadata

---

### Story 3.2: Recherche Full-Text

As a visitor,
I want to search the catalogue by keyword,
So that I can find a product quickly by name or description.

**Acceptance Criteria:**

**Given** a search term of 2+ characters
**When** `GET /api/v1/products?search=cuir` is called
**Then** results are returned sorted by relevance using PostgreSQL FTS (`to_tsvector` / `to_tsquery`)

**Given** a search with no results
**When** the results page is displayed
**Then** an empty state is shown: "Aucun résultat pour « [terme] »" with suggested category links

**Given** 2+ characters typed in the search bar
**When** the user is typing
**Then** a dropdown with up to 5 suggestions (categories + product names) appears within 300ms

**And** search response time is ≤ 1 second
**And** search works without being logged in
**And** the search input is accessible via keyboard (Tab focus, Enter to submit, Escape to close suggestions)

---

### Story 3.3: Composants ProductCard & FilterChipBar — Angular Web

As a web visitor,
I want to see products in a responsive grid with interactive filter chips,
So that I can discover and filter the catalogue without page reloads.

**Acceptance Criteria:**

**Given** the catalogue page loads
**When** products are displayed
**Then** each ProductCard shows: WebP image (ratio 3:4), brand, name, material, and price
**And** the grid is 4 columns on desktop (`xl:`), 3 on `lg:`, 2 on `md:`, 1 on mobile

**Given** a filter chip is tapped/clicked
**When** the filter is applied
**Then** the grid updates in real time without page reload
**And** the active chip has `background: #111111; color: white`
**And** the results counter ("12 sacs trouvés") updates via `aria-live="polite"`

**Given** one or more filters are active
**When** the "Tout effacer" button is clicked
**Then** all filters are reset and the full catalogue is shown

**And** filter state is preserved when navigating to a product detail and returning
**And** skeleton loader cards (same 3:4 ratio) are shown while fetching
**And** `role="article"` and `aria-label="[Nom], [prix]"` are set on each ProductCard
**And** focus ring is `2px solid #C9A96E` with `offset: 2px` on all interactive elements

---

### Story 3.4: Composants ProductCard & FilterChipBar — Flutter Mobile

As a mobile visitor,
I want to see products in a 2-column grid and access filters via a bottom sheet,
So that I have a fluid, native discovery experience.

**Acceptance Criteria:**

**Given** the catalogue screen loads on mobile
**When** products are displayed
**Then** a 2-column grid with ProductCard widgets shows image (ratio 3:4 via `cached_network_image`), name, and price

**Given** the filter icon is tapped
**When** the bottom sheet opens
**Then** filter chips (category, material, color, price range) are displayed with Material CDK bottom sheet animation
**And** a badge on the filter icon shows the count of active filters ("Filtres (2)")

**Given** a filter chip is tapped in the bottom sheet
**When** the selection is confirmed
**Then** the grid updates with filtered results and the bottom sheet closes

**And** skeleton loader placeholders are shown during data fetch (Riverpod `AsyncValue.when` loading state)
**And** `Semantics` widgets wrap all custom components with descriptive labels
**And** filter state persists when navigating back from product detail

---

### Story 3.5: Fiche Produit & ProductGallery

As a visitor,
I want to view a complete product page with a multi-photo gallery, description, dimensions, and stock availability,
So that I have all the information I need to make a purchase decision.

**Acceptance Criteria:**

**Given** a product detail page is requested
**When** `GET /api/v1/products/{productId}` is called
**Then** the response includes: name, description, price (cents), material, dimensions, stock quantity, category, and an array of image URLs (WebP, CDN)

**Given** the product gallery loads
**When** a visitor views the desktop layout
**Then** thumbnails are displayed in a left column and the main image fills the right (keyboard ←/→ navigation)

**Given** the product gallery loads on mobile
**When** a visitor swipes
**Then** images scroll horizontally with dot indicators below

**And** `aria-roledescription="carousel"` is set on the gallery container
**And** each image has a descriptive `alt` attribute (e.g., "Tote Parisienne en cuir cognac, vue de face")
**And** a "Retour facile 14j" badge is visible on every product page
**And** the URL follows the semantic pattern `/catalogue/{category-slug}/{product-slug}`
**And** skeleton placeholders display during image loading

---

### Story 3.6: Produits Similaires & SEO

As a visitor,
I want to see similar products on the detail page and share a permanent link,
So that I can discover alternatives and easily return to a product.

**Acceptance Criteria:**

**Given** a product detail page
**When** the page renders
**Then** a "Vous aimerez aussi" section shows up to 4 products from the same category

**Given** Angular SSR renders the product page
**When** a search engine crawls it
**Then** the page includes: dynamic `<title>` and `<meta description>`, Open Graph tags (`og:title`, `og:image`, `og:price`), and JSON-LD `Product` schema markup

**Given** the sitemap endpoint is called
**When** `GET /sitemap.xml` is requested
**Then** all published product URLs are listed with `lastmod` dates

**And** Lighthouse SEO score is ≥ 90/100 on the product detail page
**And** product URLs are permanent and human-readable (`/catalogue/sacs-cuir/tote-parisienne-marron`)
**And** the share button copies the canonical URL to clipboard with a toast confirmation

---

## Epic 4: Panier & Checkout

Les visiteurs peuvent ajouter des articles au panier sans compte, modifier leur panier, finaliser une commande avec paiement Stripe — avec vérification anti-overselling côté serveur.

### Story 4.1: Panier Anonyme & Gestion Articles

As a visitor,
I want to add products to my cart without being logged in and manage quantities,
So that I can prepare my order at my own pace without being forced to register.

**Acceptance Criteria:**

**Given** a visitor adds a product to the cart
**When** `POST /api/v1/cart/items` is called with `productId` and `quantity`
**Then** the item is added and the cart is returned with updated totals

**Given** an anonymous cart exists in Redis
**When** the visitor logs in
**Then** the anonymous cart is merged with their account cart (quantities summed for duplicate items)

**Given** `PATCH /api/v1/cart/items/{itemId}` is called with a new quantity of 0
**When** the request is processed
**Then** the item is removed from the cart

**And** anonymous carts are stored in Redis with TTL 24h (key: `cart:{sessionId}`)
**And** `DELETE /api/v1/cart/items/{itemId}` removes a specific item
**And** `GET /api/v1/cart` returns the current cart with item details, unit prices, and total in cents
**And** stock availability is NOT checked at this stage (only at order confirmation)

---

### Story 4.2: Composants StickyAddToCart & CartDrawer

As a visitor,
I want to add a product from the detail page and view my cart in a slide-in panel,
So that I can convert without losing my browsing context.

**Acceptance Criteria:**

**Given** a visitor is on a product detail page on mobile
**When** they scroll down
**Then** the StickyAddToCart bar remains fixed at the bottom showing price and "Ajouter au panier" button

**Given** the "Ajouter au panier" button is tapped
**When** the item is added successfully
**Then** the cart badge increments with a subtle animation and a snackbar confirms ("Tote Parisienne ajoutée au panier", 3s auto-dismiss)
**And** `aria-live="polite"` announces the confirmation to screen readers

**Given** the cart icon is tapped
**When** the CartDrawer opens
**Then** it slides in from the right (desktop) or bottom (mobile) with focus trapped inside
**And** pressing Escape closes it and returns focus to the trigger element

**Given** the cart is empty
**When** the CartDrawer opens
**Then** an empty state is shown with an illustration and "Découvrir notre catalogue" CTA

**And** `role="dialog"` and `aria-modal="true"` are set on the CartDrawer
**And** body scroll is locked while the drawer is open
**And** StickyAddToCart shows `aria-disabled` and "Rupture de stock" text when stock is 0

---

### Story 4.3: Checkout Étape 1 — Adresse de Livraison

As a customer,
I want to enter or select my delivery address as the first checkout step,
So that I can begin the order process with my shipping details confirmed.

**Acceptance Criteria:**

**Given** a logged-in customer with a saved address
**When** they reach checkout step 1
**Then** their saved address is pre-filled in the form

**Given** the address form is displayed
**When** a field loses focus with invalid data
**Then** an inline error appears below the field in `#C0564A` with `aria-describedby` linked

**Given** the form is valid and submitted
**When** the customer clicks "Continuer"
**Then** the address is saved to the session and the customer proceeds to step 2

**And** the OrderStepIndicator shows "Étape 1/4 — Adresse" as active
**And** required fields are: street, city, postal code, country
**And** form data is auto-saved between steps (no data loss on back navigation)
**And** the form is accessible on both Angular web and Flutter mobile

---

### Story 4.4: Checkout Étape 2 — Choix de Livraison

As a customer,
I want to choose a delivery method (Standard / Express) with visible pricing and estimated delay,
So that I can control the cost and timing of my order delivery.

**Acceptance Criteria:**

**Given** the customer reaches checkout step 2
**When** `GET /api/v1/shipping-options` is called
**Then** available options are returned with name, price (cents), and estimated delay (e.g., "3–5 jours ouvrés")

**Given** a shipping option is selected
**When** the selection changes
**Then** the order subtotal updates in real time to reflect the shipping cost

**Given** the customer clicks "Continuer"
**When** a shipping option is selected
**Then** the selection is saved and the customer proceeds to step 3

**And** the OrderStepIndicator shows "Étape 2/4 — Livraison" as active
**And** the previously selected option is preserved if the customer navigates back
**And** at least one shipping option is always available (Standard is always shown)

---

### Story 4.5: Checkout Étape 3 — Paiement Stripe

As a customer,
I want to pay by credit card via Stripe securely,
So that my order is paid without my card data ever reaching the server.

**Acceptance Criteria:**

**Given** the customer reaches checkout step 3
**When** `POST /api/v1/payments/create-intent` is called with the cart total
**Then** a `clientSecret` is returned and no card data is stored server-side

**Given** the customer enters card details in the Stripe.js / Flutter Stripe SDK form
**When** payment is submitted
**Then** Stripe processes the payment client-side using the `clientSecret`

**Given** the payment is declined
**When** Stripe returns an error
**Then** an inline error message is shown below the payment form ("Paiement refusé. Vérifiez vos informations.") without losing the cart

**And** the OrderStepIndicator shows "Étape 3/4 — Paiement" as active
**And** the Stripe payment form is embedded (Stripe Elements / Flutter), never a custom card input
**And** HTTPS is enforced on the payment page
**And** the order summary (items, shipping, total) is visible alongside the payment form

---

### Story 4.6: Confirmation Commande & Anti-Overselling

As a customer,
I want to receive immediate order confirmation after payment with stock verification,
So that I have certainty my order is registered and the stock is reserved for me.

**Acceptance Criteria:**

**Given** Stripe sends a signed `payment_intent.succeeded` webhook
**When** `POST /api/v1/payments/webhook` processes it
**Then** stock availability is checked atomically using EF Core optimistic concurrency (`xmin` PostgreSQL)

**Given** stock is insufficient at the time of webhook processing
**When** the overselling check fails
**Then** a Stripe refund is issued automatically and a notification email is sent to the customer

**Given** stock is sufficient
**When** the order is confirmed
**Then** the order is created in the database, stock is decremented, and `OrderPlacedEvent` is published

**Given** `OrderPlacedEvent` is published
**When** the `OrderPlacedEmailHandler` processes it
**Then** a confirmation email with order number and summary is delivered within ≤ 30 seconds

**And** the OrderStepIndicator shows "Étape 4/4 — Confirmée" on the confirmation page
**And** the confirmation page displays: order number, items summary, delivery address, estimated date
**And** the cart is cleared after successful order creation
**And** all payment transactions are logged in an audit trail (non-deletable)

---

## Epic 5: Commandes, Retours & Notifications

Les clients peuvent initier des retours, recevoir des emails à chaque changement de statut et de remboursement. L'infrastructure emails transactionnels (≤30s) couvre tous les événements de la plateforme.

### Story 5.1: Demande de Retour Client

As a customer,
I want to initiate a return request for a delivered order,
So that I can exercise my 14-day right of withdrawal without friction.

**Acceptance Criteria:**

**Given** a customer has an order with status "Livrée" within the last 14 days
**When** `POST /api/v1/account/orders/{orderId}/returns` is called with reason and description
**Then** a return request is created with status "En attente" and a unique return ID

**Given** the return request is created
**When** the `ReturnRequestedEvent` is published
**Then** an acknowledgement email is sent to the customer within ≤ 30 seconds

**Given** an order is older than 14 days or not in "Livrée" status
**When** a return is attempted
**Then** a `422 Unprocessable Entity` ProblemDetails response is returned with a clear message

**And** the return form includes: reason (dropdown), description (text), optional photos (Cloudinary upload)
**And** the return request is visible in the customer's order detail page
**And** the form is accessible on Angular web and Flutter mobile

---

### Story 5.2: Notifications Email — Changements de Statut

As a customer,
I want to receive an email at every order status change (En préparation → Expédiée → Livrée),
So that I am proactively informed without needing to check my account.

**Acceptance Criteria:**

**Given** an admin updates an order status to "Expédiée" with a tracking number
**When** the `OrderShippedEvent` is published
**Then** a shipment notification email is sent to the customer within ≤ 30 seconds
**And** the email includes the tracking number and a "Suivre ma commande" link

**Given** an order status is updated to "Livrée"
**When** the `OrderDeliveredEvent` is published
**Then** a delivery confirmation email is sent to the customer within ≤ 30 seconds

**And** all email templates use DM Sans typography and the Élégance Naturelle palette
**And** emails render correctly on Gmail, Outlook, and Apple Mail (tested via SendGrid preview)
**And** each email contains an unsubscribe link for transactional communication compliance
**And** email delivery is logged with timestamp and status for audit purposes

---

### Story 5.3: Remboursement Admin & Confirmation Client

As an administrator,
I want to validate a return request and issue a Stripe refund,
So that I can close the return cycle and maintain customer trust.

**Acceptance Criteria:**

**Given** a pending return request in the admin panel
**When** `PATCH /api/v1/admin/returns/{returnId}` is called with status "Validé"
**Then** the return status is updated and the customer is notified

**Given** the return is validated
**When** `POST /api/v1/admin/returns/{returnId}/refund` is called
**Then** the Stripe Refund API is called for the original payment amount
**And** the `RefundIssuedEvent` is published → customer receives a refund confirmation email within ≤ 30 seconds

**Given** a Stripe refund API failure
**When** the refund call fails
**Then** the error is logged, an alert is raised for the admin, and no partial state is persisted

**And** the refund email includes: amount refunded, original order number, and expected processing time (3–5 business days)
**And** a complete audit trail is stored: amount, date, admin user, Stripe refund ID
**And** only admins with the `Admin` role can issue refunds

---

### Story 5.4: Couverture Complète Emails Transactionnels

As the platform,
I want every business event to trigger its corresponding email within ≤ 30 seconds,
So that complete traceability and proactive customer communication are guaranteed across all scenarios.

**Acceptance Criteria:**

**Given** the full email matrix is implemented
**When** each triggering event occurs
**Then** the corresponding email is dispatched within ≤ 30 seconds:
- Inscription → email de bienvenue
- Commande confirmée → confirmation avec récapitulatif
- Commande expédiée → notification avec tracking
- Livraison confirmée → email de livraison
- Demande retour reçue → accusé de réception
- Remboursement émis → confirmation montant
- Réinitialisation mot de passe → lien sécurisé

**And** each email dispatch is logged with: event type, recipient, timestamp, SendGrid message ID
**And** integration tests assert the ≤ 30s delivery constraint for each email type
**And** failed email deliveries are retried (max 3 attempts) and logged as errors if all retries fail
**And** no email contains sensitive data (no card numbers, no passwords, no tokens in plain text)

---

## Epic 6: Administration Catalogue

L'administrateur peut créer, modifier, importer et gérer tout le catalogue produits et les stocks.

### Story 6.1: CRUD Fiches Produits

As an administrator,
I want to create, edit, and delete product listings with all their details,
So that the catalogue is always accurate and up to date.

**Acceptance Criteria:**

**Given** valid product data (name, description, price in cents, category, material, initial stock)
**When** `POST /api/v1/admin/products` is called
**Then** the product is created with status "Dépublié" (not visible publicly) and its ID is returned

**Given** an existing product
**When** `PUT /api/v1/admin/products/{id}` is called with updated fields
**Then** the product is updated, the Redis catalogue cache is invalidated, and the updated product is returned

**Given** an existing product
**When** `DELETE /api/v1/admin/products/{id}` is called
**Then** the product is soft-deleted (hidden from catalogue, not physically removed) and associated data is preserved

**And** only users with the `Admin` role can access these endpoints
**And** price is validated as a positive integer (cents), never negative or zero
**And** all mutations are logged with admin user ID and timestamp

---

### Story 6.2: Gestion des Images Produit

As an administrator,
I want to upload multiple photos for each product via Cloudinary,
So that product pages display high-quality WebP galleries optimized for fast loading.

**Acceptance Criteria:**

**Given** an image file is uploaded to `POST /api/v1/admin/products/{id}/images`
**When** the upload is processed
**Then** the image is stored in Cloudinary with automatic WebP conversion and resizing
**And** the CDN URL is saved to the product's image list and returned

**Given** multiple images exist for a product
**When** the admin reorders them via `PATCH /api/v1/admin/products/{id}/images/order`
**Then** the new display order is persisted and reflected on the public product page

**Given** a product has no images
**When** an admin tries to publish it
**Then** a `422` error is returned: "Au moins une image est requise pour publier un produit"

**And** individual images can be deleted via `DELETE /api/v1/admin/products/{id}/images/{imageId}`
**And** Cloudinary transformations enforce ratio 3:4 and max width 1200px
**And** upload progress is shown in the admin UI

---

### Story 6.3: Import CSV en Masse

As an administrator,
I want to import products in bulk via a CSV file,
So that I can quickly populate the catalogue without entering each product manually.

**Acceptance Criteria:**

**Given** a valid CSV file with columns: nom, description, prix, catégorie, matière, couleur, stock
**When** `POST /api/v1/admin/products/import` is called (multipart form)
**Then** all valid rows are imported and an import report is returned: `{ created: X, errors: [{ row, reason }] }`

**Given** a CSV with some invalid rows (missing required field, invalid price)
**When** the import is processed
**Then** valid rows are imported successfully and invalid rows are listed in the error report (import is not rolled back)

**Given** a CSV with 100 products
**When** the import is processed
**Then** it completes in under 30 seconds

**And** the CSV template is downloadable from the admin UI
**And** imported products default to "Dépublié" status
**And** duplicate detection: if a product with the same name exists, it is skipped and listed as a warning

---

### Story 6.4: Gestion des Stocks & Alertes

As an administrator,
I want to manage stock quantities per product and set alert thresholds,
So that I am never caught off-guard by unexpected stockouts.

**Acceptance Criteria:**

**Given** a valid stock quantity
**When** `PATCH /api/v1/admin/products/{id}/stock` is called with `{ quantity, alertThreshold }`
**Then** the stock is updated and a stock movement entry is logged (quantity, reason, admin, timestamp)

**Given** a stock update reduces quantity to at or below the alert threshold
**When** the `StockUpdatedEvent` is published
**Then** an alert notification is triggered (visible on the admin dashboard and optionally by email)

**Given** a stock adjustment that would result in negative stock
**When** the request is processed
**Then** a `422` error is returned: "Le stock ne peut pas être négatif"

**And** the stock movement history for each product is accessible via `GET /api/v1/admin/products/{id}/stock-history`
**And** alert thresholds default to 5 units if not explicitly set
**And** stock levels are never cached in Redis (always read directly from PostgreSQL)

---

### Story 6.5: Catégories & Publication

As an administrator,
I want to organize products into categories/subcategories and control their public visibility,
So that the catalogue is well-structured and product launches can be managed.

**Acceptance Criteria:**

**Given** a category name
**When** `POST /api/v1/admin/categories` is called
**Then** the category is created with an auto-generated URL slug from the name

**Given** a subcategory with a parent category ID
**When** `POST /api/v1/admin/categories` is called with `parentId`
**Then** the subcategory is created and appears nested under its parent in the catalogue filters

**Given** a product with at least one image
**When** `PATCH /api/v1/admin/products/{id}/publish` is called with `{ isPublished: true }`
**Then** the product becomes visible on the public catalogue immediately

**Given** a published product is unpublished
**When** `PATCH /api/v1/admin/products/{id}/publish` is called with `{ isPublished: false }`
**Then** the product is hidden from the public catalogue and the Redis cache is invalidated

**And** categories appear as filter options in the public catalogue
**And** slug generation follows kebab-case: "Sacs Mode" → `sacs-mode`
**And** categories cannot be deleted if they contain published products

---

## Epic 7: Administration Commandes & Dashboard

L'administrateur a une vue complète sur les commandes, peut les traiter et consulter les KPIs de la boutique.

### Story 7.1: Liste & Filtrage des Commandes

As an administrator,
I want to view all orders with filtering by status, date, and customer,
So that I have a complete operational overview of the shop.

**Acceptance Criteria:**

**Given** an admin accesses the orders list
**When** `GET /api/v1/admin/orders` is called
**Then** a paginated list is returned with columns: order number, customer name, date, total amount (cents), status

**Given** filter parameters are provided
**When** `GET /api/v1/admin/orders?status=Expédiée&dateFrom=2026-04-01&dateTo=2026-04-12&search=Salma` is called
**Then** only matching orders are returned

**And** results are sorted by date descending by default
**And** response time is ≤ 500ms at p95
**And** pagination metadata is included (`totalCount`, `pageNumber`, `pageSize`, `totalPages`)
**And** only users with the `Admin` role can access this endpoint

---

### Story 7.2: Mise à Jour Statut & Numéro de Suivi

As an administrator,
I want to update an order's status and enter a tracking number,
So that customers are informed and shipment traceability is maintained.

**Acceptance Criteria:**

**Given** an order in "En préparation" status
**When** `PATCH /api/v1/admin/orders/{id}/status` is called with `{ status: "Expédiée", trackingNumber: "..." }`
**Then** the status is updated and `OrderShippedEvent` is published → customer email sent within ≤ 30 seconds

**Given** a status transition to "Expédiée" without a tracking number
**When** the request is processed
**Then** a `422` error is returned: "Le numéro de suivi est requis pour le statut Expédiée"

**Given** an invalid status transition (e.g., "Livrée" → "En préparation")
**When** the request is processed
**Then** a `422` error is returned with the list of valid transitions

**And** every status change is logged: previous status, new status, admin user ID, timestamp
**And** valid transitions are: En attente → En préparation → Expédiée → Livrée
**And** cancellation is only possible from "En attente" or "En préparation"

---

### Story 7.3: Traitement des Demandes de Retour

As an administrator,
I want to view and process return requests (approve or reject) with Stripe refund,
So that the customer return cycle is closed efficiently.

**Acceptance Criteria:**

**Given** an admin accesses the returns list
**When** `GET /api/v1/admin/returns` is called
**Then** a list of return requests is returned with: order number, customer, reason, date, status, photos

**Given** a pending return request
**When** `PATCH /api/v1/admin/returns/{id}` is called with `{ status: "Validé" }`
**Then** the return is approved and the customer is notified by email

**Given** a validated return
**When** `POST /api/v1/admin/returns/{id}/refund` is called
**Then** the Stripe Refund API is called for the original payment amount
**And** `RefundIssuedEvent` is published → customer refund confirmation email within ≤ 30 seconds

**Given** a return is rejected
**When** `PATCH /api/v1/admin/returns/{id}` is called with `{ status: "Refusé", reason: "..." }`
**Then** the customer is notified by email with the rejection reason

**And** refund audit trail is stored: amount, Stripe refund ID, admin user, timestamp
**And** returns can be filtered by status (En attente / Validé / Refusé) and date

---

### Story 7.4: Dashboard KPIs Temps Réel

As an administrator,
I want to see today's key metrics (revenue, orders count, average order value) at a glance,
So that I can monitor the shop's daily health efficiently.

**Acceptance Criteria:**

**Given** an admin opens the dashboard
**When** `GET /api/v1/admin/dashboard` is called
**Then** the following metrics are returned: `revenueToday` (cents), `ordersToday`, `averageOrderValue` (cents), `revenueThisMonth`

**Given** the dashboard is open
**When** 30 seconds elapse (frontend polling)
**Then** the metrics are refreshed automatically without full page reload

**Given** today's revenue vs yesterday's revenue
**When** the dashboard renders
**Then** a trend indicator is shown (↑ green / ↓ red) with the percentage difference

**And** metrics are displayed as visual cards with clear labels in French
**And** amounts are formatted as currency (285,00 €) on the frontend, stored as cents in the API
**And** the dashboard loads in ≤ 1 second

---

### Story 7.5: Analytics Produits & Stocks Critiques

As an administrator,
I want to see the most viewed and best-selling products and those with critically low stock,
So that I can optimize the catalogue and anticipate restocking needs.

**Acceptance Criteria:**

**Given** an admin requests product analytics
**When** `GET /api/v1/admin/analytics/top-products` is called
**Then** top 10 most viewed and top 10 best-selling products for the last 7 days are returned

**Given** an admin requests low stock alerts
**When** `GET /api/v1/admin/analytics/low-stock` is called
**Then** all products with `stock ≤ alertThreshold` are returned with: name, current stock, threshold, and a direct link to the product edit page

**And** top-products data is cached in Redis with TTL 1 hour
**And** low-stock data is always read directly from PostgreSQL (never cached)
**And** the low-stock list is also surfaced as a widget on the main dashboard
**And** product view tracking increments a counter on each `GET /api/v1/products/{id}` call

---

## Epic 8: Conformité, Accessibilité & Qualité

La plateforme est légalement conforme (RGPD) et accessible WCAG 2.1 AA.

### Story 8.1: Pages Légales

As a visitor,
I want to access the CGV, privacy policy, and returns policy pages,
So that I can understand my rights and the platform's terms before purchasing.

**Acceptance Criteria:**

**Given** a visitor navigates to `/cgv`, `/confidentialite`, or `/retours`
**When** the page loads
**Then** the full legal content is displayed without requiring login

**Given** any page on the platform
**When** the footer is rendered
**Then** links to all three legal pages are visible and accessible

**And** all legal pages are server-side rendered (Angular SSR) and indexable by search engines
**And** pages use DM Sans typography and the Élégance Naturelle palette
**And** legal content is reviewed and approved before the platform launches publicly
**And** pages include `<title>` and `<meta description>` for SEO

---

### Story 8.2: Bannière RGPD & Gestion des Cookies

As a visitor,
I want to accept or refuse non-essential cookies via a clear banner,
So that I have control over my data in compliance with GDPR.

**Acceptance Criteria:**

**Given** a first-time visitor with no stored consent
**When** any page loads
**Then** the RGPD cookie banner is displayed with three options: "Accepter tout", "Refuser", "Personnaliser"

**Given** the visitor clicks "Refuser"
**When** consent is saved
**Then** only strictly necessary cookies are set and no analytics or marketing scripts are loaded

**Given** the visitor makes a consent choice
**When** the choice is stored
**Then** it is persisted for 12 months in `localStorage` and the banner does not reappear

**And** no tracking scripts load before consent is given
**And** the banner is fully keyboard accessible (Tab, Enter, Escape)
**And** `aria-label` is set on all banner buttons
**And** a "Modifier mes préférences" link in the footer allows consent to be changed at any time

---

### Story 8.3: Droit à l'Oubli & Suppression des Données

As a customer,
I want to request deletion of my personal data,
So that I can exercise my GDPR right to erasure.

**Acceptance Criteria:**

**Given** an authenticated customer
**When** they submit the "Supprimer mon compte" form in their account settings
**Then** `POST /api/v1/account/delete-request` creates a deletion request and a confirmation email is sent within 30 seconds

**Given** an admin processes the deletion request within 30 days
**When** the deletion is executed
**Then** personal data is anonymised in the database: name → "Utilisateur supprimé", email → irreversible hash, address → removed

**Given** the account is anonymised
**When** the order history is checked
**Then** order records are retained for accounting obligations but personal identifiers are removed

**And** the deletion request is logged with timestamp and processing admin
**And** after anonymisation the customer cannot log in with their old credentials
**And** Stripe customer data deletion is also requested via the Stripe API

---

### Story 8.4: Accessibilité WCAG 2.1 AA — Formulaires & Navigation

As a user with a disability,
I want all forms and navigation to be accessible via keyboard and screen readers,
So that I can use the platform without barriers.

**Acceptance Criteria:**

**Given** any form on the platform
**When** a field loses focus with invalid data
**Then** an inline error appears below the field with `aria-describedby` linking the error to the field and an ⚠ icon

**Given** a modal or overlay is open
**When** the user presses Tab
**Then** focus is trapped within the modal (cannot reach elements behind it)
**And** pressing Escape closes the modal and returns focus to the element that opened it

**Given** any page loads
**When** the first Tab press occurs
**Then** a skip link "Aller au contenu principal" is the first focused element

**And** all interactive elements have a visible focus ring: `2px solid #C9A96E` with `offset: 2px`
**And** Tab order is logical on all pages (follows visual reading order)
**And** navigation tested with: VoiceOver (iOS/macOS), TalkBack (Android), NVDA (Windows)
**And** Angular CDK `FocusTrap` and `LiveAnnouncer` are used for overlays and dynamic content

---

### Story 8.5: Accessibilité WCAG 2.1 AA — États Vides & Pagination

As a user,
I want informative empty states for every context and explicit pagination controls,
So that I never reach a dead end in the interface.

**Acceptance Criteria:**

**Given** the cart is empty
**When** the CartDrawer opens
**Then** the message "Votre panier est vide" is shown with a "Découvrir notre catalogue" CTA

**Given** a search or filter returns no results
**When** the empty state is rendered
**Then** "Aucun résultat pour « [terme] »" is shown with suggested category links and a "Réinitialiser les filtres" button

**Given** a customer with no orders views their order history
**When** the list renders
**Then** "Aucune commande pour le moment" is shown with a "Commencer à shopper" CTA

**Given** the catalogue has more products than the current page
**When** the bottom of the list is reached
**Then** a "Charger plus" button is displayed (no automatic infinite scroll)

**And** filter state is preserved when navigating from catalogue to product detail and back
**And** Lighthouse Accessibility score is ≥ 90/100 on: catalogue page, product detail page, and checkout flow
**And** axe-core is integrated in the Angular test suite and run on every CI build
**And** `flutter_test` accessibility tests cover all custom Flutter widgets
