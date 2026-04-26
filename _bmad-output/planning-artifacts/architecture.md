---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments: ['prd.md', 'ux-design-specification.md']
workflowType: 'architecture'
project_name: 'mon-ecommerce'
user_name: 'Bouchta'
date: '2026-04-12'
status: 'complete'
completedAt: '2026-04-12'
lastStep: 8
---

# Architecture Decision Document — mon-ecommerce

_Ce document se construit de manière collaborative, étape par étape. Les sections sont ajoutées au fur et à mesure des décisions architecturales._

---

## Analyse du Contexte Projet

### Aperçu des Exigences

**Exigences Fonctionnelles — 40 FRs en 8 domaines :**

| Domaine | FRs | Implication architecturale |
|---------|-----|---------------------------|
| Catalogue & Navigation | FR1–FR6 | API read-heavy, full-text search, URLs sémantiques SSR |
| Compte Client | FR7–FR12 | Auth JWT + refresh, gestion profil, historique commandes |
| Panier & Checkout | FR13–FR19 | Session panier anonyme → client, Stripe, anti-overselling |
| Commandes & Retours | FR20–FR23 | Workflow état commande, Stripe refund API, emails async |
| Admin Catalogue | FR24–FR29 | Import CSV bulk, gestion stocks, alertes seuil critique |
| Admin Commandes | FR30–FR33 | Filtrage multi-critères, mise à jour statuts, tracking |
| Dashboard Analytics | FR34–FR36 | Agrégations temps réel ou pré-calculées |
| Conformité & Emails | FR37–FR40 | Emails ≤ 30s, RGPD suppression données, bannière cookies |

**Exigences Non-Fonctionnelles déterminantes :**

| NFR | Cible | Impact architectural |
|-----|-------|---------------------|
| LCP catalogue | ≤ 2.5s mobile 4G | SSR obligatoire + CDN images WebP |
| API response | ≤ 500ms p95 | Indexes BDD, pagination, CQRS read models |
| Concurrent users | 200 sans dégradation | API stateless, connection pooling |
| Uptime | ≥ 99.5% | Health checks, graceful degradation Stripe |
| Anti-overselling | 0 tolérance | Vérification atomique à la confirmation commande |
| Emails transactionnels | ≤ 30s | Dispatch async via domain events |
| Marketplace-ready | Dès V1 | `vendor_id` sur Produit/Commande/Stock dès le schéma initial |

**Implications de la Spec UX :**
- Filtres temps réel sans rechargement → API catalogue optimisée ou filtrage client-side selon volume
- 6 composants custom (ProductCard, FilterChipBar, ProductGallery, StickyAddToCart, CartDrawer, OrderStepIndicator) → API doit exposer exactement les données dont chaque composant a besoin (BFF pattern ou query projections)
- Angular Universal SSR → state transfer hydration à concevoir
- Flutter + web → API REST versionnée unique consommée par les deux clients

### Échelle & Complexité

- **Niveau :** Medium-High — multiplateforme (web SSR + mobile natif) + marketplace-readiness + compliance RGPD + intégration Stripe
- **Domaine primaire :** Full-stack (backend REST API + frontend SPA SSR + mobile natif Flutter)
- **Composants architecturaux estimés :** ~12 (Auth, Catalog, Cart, Orders, Payments, Notifications, Admin, Analytics, FileStorage, Search, Identity, Gateway)

### Contraintes Techniques & Dépendances

- **Pas de WebSockets V1** — polling 30s pour mises à jour (décision PRD explicite)
- **Pas de mode offline mobile V1** — connexion requise pour toutes les opérations
- **PCI-DSS délégué Stripe** — zéro donnée carte côté backend, intégration via Stripe.js + Payment Intents
- **RGPD droit à l'oubli** — cascade delete ou anonymisation en base à implémenter (FR40)
- **Conformité stores mobiles** — guidelines Apple App Store (iOS 14+) et Google Play (Android 10+) dès le développement

### Préoccupations Transversales Identifiées

1. **Authentification & Autorisation** — JWT access (1h) + refresh token rotation, rôles Customer/Admin
2. **Gestion d'erreurs & Logging** — aucune erreur 5xx exposée aux clients, logging structuré centralisé
3. **Notifications Email** — pattern event-driven (domain events → handler email SendGrid/Mailgun)
4. **Stockage fichiers/images** — galeries multi-photos produits, WebP, CDN pour performance LCP
5. **Audit trail** — traçabilité complète paiements, changements stock, transitions statuts commandes
6. **RGPD** — suppression/anonymisation données client à la demande, consentement cookies
7. **Concurrence stock** — verrou optimiste ou pessimiste à la confirmation de commande
8. **Multi-vendor readiness** — `vendor_id` sur entités Produit, Commande, Stock dès le schéma V1

---

## Évaluation des Starters

### Domaine Technologique Primaire

Full-stack multiplateforme : Backend API REST (.NET) + Frontend SPA SSR (Angular) + Mobile natif (Flutter). Stack pré-défini dans le PRD — cette étape confirme les templates et commandes d'initialisation optimaux.

### Starters Sélectionnés

#### Backend — jasontaylordev/CleanArchitecture

Template de référence pour .NET Clean Architecture (18k+ stars, activement maintenu, .NET 9/10 supporté). Inclut nativement CQRS + MediatR + EF Core + FluentValidation — correspondance exacte avec le stack PRD.

**Commande d'initialisation :**

```bash
dotnet new install Clean.Architecture.Solution.Template
dotnet new ca-sln --client-framework none --database sqlserver --output MonEcommerce
```

**Décisions architecturales fournies :**
- Structure 4 couches : `Domain` / `Application` / `Infrastructure` / `WebAPI`
- CQRS via MediatR, pipeline behaviors (validation, logging, performance)
- EF Core + migrations, repository pattern abstrait
- FluentValidation + AutoMapper préconfigurés
- Scaffolding tests unitaires et d'intégration

**Version .NET :** .NET 9 (SDK 9.0.312, STS — stable production). Migration vers .NET 10 LTS post-lancement.

#### Frontend — Angular CLI 19 avec SSR natif

Angular 19 est la version courante. `@angular/ssr` intégré nativement dans le CLI depuis v17 — Angular Universal n'existe plus comme package séparé.

**Commande d'initialisation :**

```bash
npm install -g @angular/cli
ng new mon-ecommerce-web --ssr --style scss --routing true --standalone true
```

**Décisions architecturales fournies :**
- SSR + Hydration incrémentale (production-ready Angular 19)
- Standalone components — pattern 2024+ (pas de NgModules)
- SCSS, routing, lazy loading configurés
- Build Esbuild/Vite natif — performance build optimale

#### Mobile — Flutter 3.41.x

Flutter 3.41.x (Dart 3.9.0) — version stable actuelle.

**Commande d'initialisation :**

```bash
flutter create mon_ecommerce_mobile \
  --org com.monecommerce \
  --platforms ios,android \
  --project-name mon_ecommerce_mobile
```

**Décisions architecturales fournies :**
- Structure `lib/` standard Flutter
- Material 3 disponible nativement (base pour thème Élégance Naturelle)
- iOS 14+ / Android API 29+ configurables

### Structure du Dépôt

```
mon-ecommerce/
├── backend/          ← dotnet new ca-sln (Clean Architecture .NET 9)
│   ├── src/
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   └── WebAPI/
│   └── tests/
├── frontend/         ← ng new --ssr (Angular 19 standalone)
│   ├── src/app/
│   └── server.ts
└── mobile/           ← flutter create (Flutter 3.41.x)
    ├── lib/
    ├── ios/
    └── android/
```

**Note :** La première story d'implémentation sera l'initialisation de ces trois projets avec leurs commandes respectives.

---

## Décisions Architecturales Clés

### Analyse des Priorités

**Décisions critiques (bloquantes pour l'implémentation) :**
- Base de données : PostgreSQL
- Auth : ASP.NET Core Identity + JWT + refresh tokens en base
- Stockage images : Cloudinary
- State management Angular : NgRx Signal Store
- State management Flutter : Riverpod 3.0

**Décisions importantes (structurent l'architecture) :**
- Caching : Redis (StackExchange.Redis v2.12.14)
- Anti-overselling : concurrence optimiste EF Core (RowVersion)
- Format erreurs : ProblemDetails RFC 7807
- Logging : Serilog via ILogger<T>
- Hébergement V1 : Railway + Vercel

**Décisions différées (post-MVP) :**
- Full-text search avancé : Elasticsearch/Typesense (PostgreSQL FTS en V1)
- Pagination cursor-based (offset suffit en V1)
- Migration vers Azure/AWS (Railway suffit en V1)
- Monitoring avancé (Sentry Free + Railway Metrics en V1)

### Architecture de Données

| Décision | Choix | Version | Rationale |
|----------|-------|---------|-----------|
| Base de données | **PostgreSQL** | Latest stable | Gratuit, performance FTS, meilleur support cloud, marketplace-ready |
| ORM | **EF Core** | 9.x (inclus Jason Taylor) | Code-first migrations, LINQ, repository pattern |
| Caching distribué | **Redis** | StackExchange.Redis 2.12.14 + Microsoft.Extensions.Caching.StackExchangeRedis 10.0.5 | Catalogue read-heavy, sessions panier, rate limiting |
| Stockage images | **Cloudinary** | SDK .NET | WebP auto, CDN intégré, URL transforms — critique pour LCP ≤ 2.5s |
| Full-text search | **PostgreSQL FTS natif** | — | Suffisant V1 (≤ 10k produits). Elasticsearch post-MVP. |
| Anti-overselling | **Concurrence optimiste EF Core** | `ConcurrencyToken` + `xmin` PostgreSQL | Vérification atomique stock à la confirmation, sans lock global |
| Migrations | **EF Core Migrations** | — | Code-first, versionnées, rollback possible |

### Authentification & Sécurité

| Décision | Choix | Version | Rationale |
|----------|-------|---------|-----------|
| Identity | **ASP.NET Core Identity** | Inclus Jason Taylor | Users, password hashing bcrypt, rôles Customer/Admin |
| JWT | `Microsoft.AspNetCore.Authentication.JwtBearer` | .NET 9 natif | Access token 1h, standard .NET, intégré Identity |
| Refresh tokens | **Table PostgreSQL** `RefreshTokens` | — | Révocables, traçables, rotation à chaque usage |
| Paiement | **Stripe.net** + webhooks signés | Stripe.net latest | Payment Intents, zéro donnée carte stockée, PCI-DSS délégué |

### API & Communication

| Décision | Choix | Rationale |
|----------|-------|-----------|
| Documentation | **OpenAPI 3.0** via Swashbuckle (inclus Jason Taylor) | Auto-généré, Swagger UI en dev |
| Format erreurs | **ProblemDetails RFC 7807** | Standard HTTP natif .NET 9, cohérent Angular + Flutter |
| Pagination | **Page + PageSize** (offset) | Simple, compatible Angular CDK. Cursor-based post-MVP si > 50k produits |
| Versioning | **URL path** `/api/v1/` | Visible, simple, compatible SSR et mobile |
| Email | **SendGrid** (`SendGrid` NuGet) | Meilleur DX, templates HTML, deliverability éprouvée |
| Emails async | **Domain Events → MediatR Handler → SendGrid** | Pattern event-driven, découplé, ≤ 30s garanti |

### Frontend Angular

| Décision | Choix | Version | Rationale |
|----------|-------|---------|-----------|
| State management | **NgRx Signal Store** | v19+ | Signal-based, moins de boilerplate, typage fort, idéal catalogue/panier |
| HTTP | **Angular HttpClient** + intercepteurs | Angular 19 natif | Auth interceptor (JWT inject), error interceptor (401 redirect), retry |
| CSS | **Tailwind CSS v4** + Angular CDK | v4.x | Décidé UX spec, utilitaires whitespace, tokens CSS custom properties |
| Formulaires | **Angular Reactive Forms** | Angular 19 natif | Validation `onBlur`, TypeScript-typed, testables |

### Mobile Flutter

| Décision | Choix | Version | Rationale |
|----------|-------|---------|-----------|
| State management | **Riverpod 3.0** | 3.x | Recommandation 2026 e-commerce : compile-time safety, FutureProvider async, auto-disposal |
| HTTP | **Dio** + `dio_cache_interceptor` | Latest | Intercepteurs auth/retry/cache, plus puissant que `http` natif |
| Navigation | **go_router** | Latest | Standard Flutter officiel, deep linking, guards auth |
| JWT storage | **flutter_secure_storage** | Latest | Keychain iOS / Keystore Android |
| Images | **cached_network_image** | Latest | Cache local images Cloudinary, skeleton placeholder |

### Infrastructure & Déploiement

| Décision | Choix | Rationale |
|----------|-------|-----------|
| Logging | **Serilog** via `ILogger<T>` | Structured logging, 150+ sinks, enrichment correlation ID, interface MEL préservée |
| Hébergement V1 | **Railway** (backend + PostgreSQL) + **Vercel** (Angular SSR) | Simple, pas de Kubernetes V1, PostgreSQL managé Railway, SSR Vercel optimisé |
| CI/CD | **GitHub Actions** | Gratuit, workflows YAML, secrets management, intégration GitHub native |
| Monitoring | **Sentry Free** (errors) + Railway Metrics (perf) | Sentry error tracking .NET + Angular + Flutter, upgrade post-MVP |
| Conteneurisation | **Docker** (Dockerfile par service) | `docker-compose` dev local, image Docker prod Railway |

### Séquence d'Implémentation

1. Initialisation des 3 projets (backend/frontend/mobile)
2. PostgreSQL schema + EF Core migrations (Domain entities)
3. Auth (Identity + JWT + refresh tokens)
4. Catalogue API (CQRS — queries read-heavy)
5. Cloudinary integration (upload images)
6. Panier + Checkout + Stripe
7. Angular frontend (SSR + NgRx Signal Store)
8. Flutter mobile (Riverpod + go_router)
9. Back-office admin
10. CI/CD GitHub Actions + déploiement Railway/Vercel

### Dépendances Entre Décisions

- PostgreSQL → EF Core migrations → tout le reste
- ASP.NET Core Identity → JWT → Angular intercepteurs → Flutter secure storage
- Cloudinary → ProductCard images → LCP ≤ 2.5s
- Domain Events → SendGrid → emails transactionnels ≤ 30s
- NgRx Signal Store → CartDrawer + FilterChipBar state

---

## Patterns d'Implémentation & Règles de Cohérence

### Patterns de Nommage

**Base de données (PostgreSQL + EF Core) :**

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Tables | `snake_case` pluriel | `products`, `order_items`, `refresh_tokens` |
| Colonnes | `snake_case` | `created_at`, `vendor_id`, `is_active` |
| Clés primaires | `id` UUID | `id UUID PRIMARY KEY DEFAULT gen_random_uuid()` |
| Clés étrangères | `{table_singulier}_id` | `product_id`, `order_id`, `user_id` |
| Index | `ix_{table}_{colonne(s)}` | `ix_products_category_id` |
| Contraintes unique | `uq_{table}_{colonne}` | `uq_users_email` |

**Endpoints API REST :**

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Ressources | Pluriel `kebab-case` | `/api/v1/products`, `/api/v1/order-items` |
| Paramètres de route | `camelCase` | `/api/v1/products/{productId}` |
| Query params | `camelCase` | `?pageNumber=1&pageSize=20&categoryId=x` |
| Verbes HTTP | Standard REST | `GET` liste, `GET /{id}` détail, `POST` créer, `PUT` remplacer, `PATCH` modifier, `DELETE` supprimer |

**Code .NET (C#) :**

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Classes, interfaces | `PascalCase` | `ProductService`, `IProductRepository` |
| Méthodes | `PascalCase` async | `GetProductByIdAsync`, `CreateOrderAsync` |
| Variables locales | `camelCase` | `productId`, `orderTotal` |
| Constantes | `UPPER_SNAKE_CASE` | `MAX_PAGE_SIZE` |
| Fichiers | `PascalCase.cs` | `CreateProductCommand.cs`, `ProductDto.cs` |
| Namespaces | `MonEcommerce.{Layer}.{Feature}` | `MonEcommerce.Application.Products.Commands` |

**Code Angular (TypeScript) :**

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Composants | `PascalCase` classe, `kebab-case` sélecteur | `ProductCardComponent`, `app-product-card` |
| Fichiers | `kebab-case.component.ts` | `product-card.component.ts` |
| Services | `PascalCase` + `Service` | `ProductService`, `CartService` |
| Stores NgRx Signal | `PascalCase` + `Store` | `CatalogStore`, `CartStore` |
| Interfaces | `PascalCase` | `Product`, `OrderSummary`, `FilterParams` |

**Code Flutter (Dart) :**

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Classes, widgets | `PascalCase` | `ProductCard`, `CatalogScreen` |
| Fichiers | `snake_case.dart` | `product_card.dart`, `catalog_screen.dart` |
| Variables, fonctions | `camelCase` | `productId`, `addToCart()` |
| Providers Riverpod | `camelCase` + `Provider` | `catalogProvider`, `cartProvider` |
| Constantes | classe `AppConstants` camelCase | `AppConstants.apiBaseUrl` |

### Patterns de Structure

**Organisation par Feature (tous les layers) :**

```
# Backend — Application layer
Application/
  Products/
    Commands/CreateProduct/
      CreateProductCommand.cs
      CreateProductCommandHandler.cs
      CreateProductCommandValidator.cs
    Queries/GetProducts/
      GetProductsQuery.cs
      GetProductsQueryHandler.cs
      ProductDto.cs

# Frontend Angular
src/app/
  features/
    catalog/        (catalog.component.ts, catalog.store.ts, catalog.routes.ts)
    cart/
    checkout/
  shared/
    components/
    services/

# Mobile Flutter
lib/
  features/
    catalog/        (screens/, widgets/, providers/)
    cart/
    checkout/
  shared/
    widgets/
    services/
```

**Tests :**
- Backend : dossier `tests/` séparé (standard Jason Taylor)
- Angular : fichiers `.spec.ts` co-localisés
- Flutter : dossier `test/` miroir de `lib/`

### Patterns de Format

**Réponse API — Structure standard :**

```json
// Liste paginée
{ "items": [...], "totalCount": 47, "pageNumber": 1, "pageSize": 20, "totalPages": 3 }

// Objet unique — réponse directe (pas de wrapper)
{ "id": "uuid", "name": "Tote Parisienne", "priceInCents": 28500 }

// Erreur — ProblemDetails RFC 7807
{ "type": "...", "title": "Validation failed", "status": 422,
  "errors": { "name": ["Name is required"] } }
```

**Formats de données :**
- **JSON fields** : `camelCase` (Angular + Flutter JSON serialization)
- **Dates** : ISO 8601 UTC — `"2026-04-12T10:30:00Z"`
- **Prix** : `integer` en centimes côté API (`28500` = 285,00€), formaté côté client
- **IDs** : UUID v4 string
- **Booléens** : `true`/`false` natif, jamais `0`/`1`
- **Nulls** : champs absents préférés aux `null` explicites dans les réponses

### Patterns de Communication

**Domain Events MediatR :**
- Nommage : `{Entité}{ParticipéPassé}Event` → `OrderPlacedEvent`, `StockUpdatedEvent`
- Payload : `record` C# immuable → `record OrderPlacedEvent(Guid OrderId, Guid UserId, decimal Total)`
- Handlers : `{Event}{Action}Handler` → `OrderPlacedEmailHandler`

**NgRx Signal Store :**
- Méthodes store : verbe + nom camelCase → `loadProducts()`, `addToCart(product)`, `applyFilter(filter)`
- State slices : nom métier sans préfixe → `{ products, loading, error, filters, pagination }`

**Riverpod Flutter :**
- `AsyncNotifierProvider` pour state async → `catalogProvider`
- `StateProvider` pour UI state simple → `selectedCategoryProvider`
- `Provider` pour services stateless → `productRepositoryProvider`

### Patterns de Gestion d'Erreurs

**Backend .NET :**
- `ValidationBehavior` MediatR → `422` automatique si FluentValidation échoue
- `ExceptionHandler` middleware global → `500` ProblemDetails sans stack trace en production
- Interdit : `catch (Exception e) {}` sans log

**Angular :**
- `ErrorInterceptor` HTTP : `401` → redirect login, `500` → snackbar erreur
- Templates : `@if (error)` systématique, pas de `.subscribe()` sans error handler
- Sentry pour erreurs non gérées

**Flutter :**
- `AsyncValue.when(data:, loading:, error:)` systématique dans tous les widgets Riverpod
- Dio interceptor : `401` → refresh token → retry automatique
- `FlutterError.onError` + Sentry pour erreurs non gérées

### Règles Obligatoires — Tous les Agents IA

1. Respecter les conventions de nommage définies ci-dessus sans exception
2. Organiser le code par feature, pas par type technique
3. Retourner `ProblemDetails` RFC 7807 pour toutes les erreurs API
4. ISO 8601 UTC pour les dates, centimes (`integer`) pour les prix
5. Écrire des tests pour toute logique métier (handlers, validators)
6. Jamais exposer de stack trace en production
7. Préfixer namespaces .NET par `MonEcommerce.`
8. Toujours paginer les endpoints de liste (max `pageSize` = 100)
9. Versionner tous les endpoints sous `/api/v1/`
10. Logger avec `ILogger<T>` Serilog — jamais `Console.WriteLine`

---

## Structure du Projet & Frontières Architecturales

### Structure Backend (.NET 9 Clean Architecture)

```
backend/
├── .github/workflows/ci.yml · deploy.yml
├── docker-compose.yml        # PostgreSQL + Redis dev local
├── Dockerfile
├── MonEcommerce.sln
├── src/
│   ├── Domain/
│   │   ├── Common/BaseEntity.cs · BaseEvent.cs · ValueObject.cs
│   │   ├── Entities/
│   │   │   ├── Product.cs · Category.cs · ProductImage.cs
│   │   │   ├── Order.cs · OrderItem.cs
│   │   │   ├── Cart.cs · CartItem.cs
│   │   │   └── User.cs · Address.cs · RefreshToken.cs
│   │   ├── Enums/OrderStatus.cs · ReturnStatus.cs
│   │   ├── Events/
│   │   │   ├── OrderPlacedEvent.cs · OrderShippedEvent.cs
│   │   │   ├── ReturnRequestedEvent.cs · RefundIssuedEvent.cs
│   │   └── Exceptions/InsufficientStockException.cs
│   │
│   ├── Application/
│   │   ├── Common/
│   │   │   ├── Behaviours/ValidationBehaviour.cs · LoggingBehaviour.cs · PerformanceBehaviour.cs
│   │   │   ├── Interfaces/IApplicationDbContext.cs · IEmailService.cs · IFileStorageService.cs · IPaymentService.cs · ICacheService.cs
│   │   │   └── Models/PaginatedList.cs
│   │   ├── Products/Commands/ (CreateProduct · UpdateProduct · DeleteProduct · PublishProduct · ImportProducts)
│   │   ├── Products/Queries/ (GetProducts · GetProductById · GetProductsByCategory)
│   │   ├── Categories/Commands/ · Queries/
│   │   ├── Orders/Commands/ (PlaceOrder · UpdateOrderStatus · AddTrackingNumber · ProcessReturn)
│   │   ├── Orders/Queries/ (GetOrders · GetOrderById)
│   │   ├── Cart/Commands/ (AddToCart · UpdateCartItem · RemoveFromCart) · Queries/GetCart
│   │   ├── Payments/Commands/ (CreatePaymentIntent · IssueRefund) · Webhooks/HandleStripeWebhook
│   │   ├── Identity/Commands/ (Register · Login · RefreshToken · ResetPassword · DeleteAccount)
│   │   ├── Identity/Queries/GetProfile
│   │   ├── Admin/Stocks/ · Admin/Dashboard/Queries/GetDashboard
│   │   └── Notifications/EventHandlers/ (OrderPlacedEmailHandler · OrderShippedEmailHandler · RefundIssuedEmailHandler)
│   │
│   ├── Infrastructure/
│   │   ├── Persistence/ApplicationDbContext.cs · Configurations/ · Migrations/
│   │   ├── Identity/IdentityService.cs
│   │   ├── Services/EmailService.cs · FileStorageService.cs · PaymentService.cs · CacheService.cs
│   │   └── DependencyInjection.cs
│   │
│   └── WebAPI/
│       ├── Controllers/ProductsController · OrdersController · CartController · PaymentsController · AuthController · AdminController
│       ├── Middlewares/ExceptionHandlerMiddleware.cs · RateLimitingMiddleware.cs
│       └── Program.cs · appsettings.json
│
└── tests/
    ├── Domain.UnitTests/
    ├── Application.UnitTests/
    └── Application.IntegrationTests/  # PostgreSQL réel
```

### Structure Frontend (Angular 19 SSR)

```
frontend/
├── angular.json · package.json · tsconfig.json · tailwind.config.ts
├── src/
│   ├── main.ts · main.server.ts
│   ├── app/
│   │   ├── app.config.ts · app.config.server.ts · app.routes.ts
│   │   ├── core/
│   │   │   ├── interceptors/auth.interceptor.ts · error.interceptor.ts · loading.interceptor.ts
│   │   │   ├── guards/auth.guard.ts · admin.guard.ts
│   │   │   ├── services/seo.service.ts
│   │   │   └── models/product.model.ts · order.model.ts · cart.model.ts · user.model.ts
│   │   ├── features/
│   │   │   ├── catalog/      (catalog.store.ts · pages/ · components/product-grid · filter-chip-bar · product-card)
│   │   │   ├── cart/         (cart.store.ts · cart-drawer.component.ts)
│   │   │   ├── checkout/     (pages/address · shipping · payment · confirmation · order-step-indicator)
│   │   │   ├── account/      (pages/profile · orders · return-request)
│   │   │   ├── auth/         (pages/login · register · auth.store.ts)
│   │   │   └── admin/        (pages/dashboard · products · orders)
│   │   └── shared/
│   │       ├── components/product-gallery · sticky-add-to-cart · skeleton-loader
│   │       ├── services/product.service · cart.service · order.service · auth.service
│   │       └── pipes/currency-format.pipe · date-format.pipe
│   ├── environments/environment.ts · environment.production.ts
│   └── styles.scss
└── server.ts
```

### Structure Mobile (Flutter)

```
mobile/
├── pubspec.yaml · analysis_options.yaml
├── lib/
│   ├── main.dart
│   ├── app/
│   │   ├── app.dart · router.dart
│   │   └── theme/app_theme.dart · design_tokens.dart
│   ├── features/
│   │   ├── catalog/   (screens/ · widgets/product_card · filter_bottom_sheet · product_gallery · providers/catalog · filter)
│   │   ├── cart/      (screens/cart_screen · providers/cart_provider)
│   │   ├── checkout/  (screens/address · shipping · payment · confirmation · providers/checkout)
│   │   ├── account/   (screens/profile · orders · providers/account)
│   │   └── auth/      (screens/login · register · providers/auth)
│   └── shared/
│       ├── services/api_client.dart · secure_storage.dart · product_repository.dart
│       ├── widgets/sticky_add_to_cart · skeleton_loader · error_widget
│       └── models/product.dart · order.dart · user.dart
└── test/features/ · test/shared/
```

### Frontières Architecturales

**Flux de données principal :**
```
Angular/Flutter → REST /api/v1/ → WebAPI Controller
  → MediatR Send(Command/Query)
  → Application Handler (CQRS)
  → Infrastructure (EF Core PostgreSQL | Cloudinary | Stripe | Redis)
  → Domain Event → MediatR Publish()
  → EventHandler → SendGrid email
```

**Frontière Stripe :**
- `POST /api/v1/payments/create-intent` → PaymentIntent → retourne `clientSecret` au client
- Client appelle Stripe.js avec `clientSecret` — zéro donnée carte côté serveur
- `POST /api/v1/payments/webhook` → webhook signé → confirmation commande en base

**Frontière Auth JWT :**
- Access token (1h) injecté par `AuthInterceptor` Angular / `AuthInterceptor` Dio Flutter
- Refresh token (7j) en base table `refresh_tokens`, rotation à chaque usage
- `401` → tentative refresh automatique → si échec, redirect login

**Frontière Cache Redis :**
- Catalogue/filtres : TTL 5min (invalidé sur Create/Update/Delete produit)
- Compteurs stock : jamais mis en cache (lecture directe PostgreSQL)
- Panier anonyme : TTL 24h (clé `cart:{sessionId}`)

### Mapping FRs → Emplacement

| FRs | Emplacement backend | Emplacement frontend/mobile |
|-----|--------------------|-----------------------------|
| FR1–FR6 | `Application/Products/Queries/` | `features/catalog/` |
| FR7–FR12 | `Application/Identity/` | `features/auth/` + `features/account/` |
| FR13–FR14 | `Application/Cart/` | `features/cart/` |
| FR15–FR19 | `Application/Orders/Commands/PlaceOrder/` | `features/checkout/` |
| FR20–FR23 | `Application/Orders/Commands/ProcessReturn/` | `features/account/` |
| FR24–FR29 | `Application/Products/Commands/` | `features/admin/products/` |
| FR30–FR33 | `Application/Orders/` | `features/admin/orders/` |
| FR34–FR36 | `Application/Admin/Dashboard/` | `features/admin/dashboard/` |
| FR37–FR40 | `Application/Notifications/EventHandlers/` | Pages légales + bannière RGPD |
