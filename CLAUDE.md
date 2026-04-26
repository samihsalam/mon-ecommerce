# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Monorepo e-commerce full-stack — 3 projets indépendants, langue française, méthode bmad.

```
mon-ecommerce/
├── backend/MonEcommerce/        ← .NET 9 Clean Architecture
├── frontend/mon-ecommerce-web/  ← Angular 19 SSR (standalone components)
└── mobile/mon_ecommerce_mobile/ ← Flutter 3.41
```

Artefacts bmad (PRD, architecture, epics, sprint) : `_bmad-output/`

---

## Commandes

### Backend (.NET 9)
```bash
# Depuis backend/MonEcommerce/
dotnet build                              # Build la solution complète
dotnet test                               # Tous les tests
dotnet test --filter "ClassName=MappingTests"  # Un seul test
dotnet run --project src/Web             # Démarrer l'API (http://localhost:5000)
dotnet watch --project src/Web           # Hot reload

# Base de données (SQL Server — DESKTOP-M36577B, pas de Docker requis)
dotnet ef migrations add <NomMigration> --project src/Infrastructure --startup-project src/Web
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

### Frontend Angular
```bash
# Depuis frontend/mon-ecommerce-web/
npm start          # Dev server http://localhost:4200
npm run build      # Build production SSR
npm test           # Tests Karma/Jasmine
```

### Mobile Flutter
```bash
# Depuis mobile/mon_ecommerce_mobile/
flutter analyze    # Lint
flutter test       # Tests unitaires
flutter run        # Lancer sur émulateur/device
```

---

## Architecture Backend — Clean Architecture

Couches (de bas en haut, dépendances vers l'intérieur) :

| Couche | Projet | Rôle |
|---|---|---|
| **Domain** | `MonEcommerce.Domain` | Entités, Value Objects, Domain Events, interfaces pures |
| **Application** | `MonEcommerce.Application` | Commands/Queries MediatR, Validators FluentValidation, DTOs AutoMapper |
| **Infrastructure** | `MonEcommerce.Infrastructure` | EF Core + SQL Server, ASP.NET Identity + JWT, implémentations interfaces |
| **Web** | `MonEcommerce.Web` | Minimal API endpoints, DI registration, middleware |

### Pipeline MediatR (ordre d'exécution)
`LoggingBehaviour → UnhandledExceptionBehaviour → AuthorizationBehaviour → ValidationBehaviour → PerformanceBehaviour → Handler`

- `ValidationBehaviour` lance `ValidationException` → intercepté par `ProblemDetailsExceptionHandler` → HTTP 422
- `PerformanceBehaviour` log un warning si le handler dépasse 500ms
- `AuthorizationBehaviour` vérifie l'attribut `[Authorize]` sur les commands/queries

### Enregistrement des endpoints
Chaque endpoint implémente `IEndpointGroup` avec une méthode `static void Map(RouteGroupBuilder)`.  
`WebApplicationExtensions.MapEndpoints()` les découvre par réflexion et les monte sur `/api/{ClassName}`.

Pour ajouter un endpoint :
1. Créer une classe dans `src/Web/Endpoints/` qui implémente `IEndpointGroup`
2. C'est tout — elle est automatiquement découverte et enregistrée

### Ajouter une fonctionnalité (pattern standard)

```
Domain/Entities/          ← Entité + Domain Event si nécessaire
Application/{Feature}/
  Commands/               ← Command + Handler + Validator
  Queries/                ← Query + Handler + DTO
Infrastructure/Data/
  Configurations/         ← EF Core IEntityTypeConfiguration<T>
Web/Endpoints/            ← IEndpointGroup
```

### Base de données
- **ORM** : EF Core 9 + **Microsoft.EntityFrameworkCore.SqlServer**
- **Connexion locale** : `Server=DESKTOP-M36577B;Database=MonEcommerce;Trusted_Connection=True;TrustServerCertificate=True`
- `ApplicationDbContext` hérite de `IdentityDbContext<ApplicationUser>`
- Les configurations EF sont dans `Infrastructure/Data/Configurations/` via `IEntityTypeConfiguration<T>`
- Les interceptors `AuditableEntityInterceptor` et `DispatchDomainEventsInterceptor` s'appliquent à tous les `SaveChanges`

### Entités Domain actuelles
> **Note** : `TodoList` et `TodoItem` sont des entités template à remplacer par les entités e-commerce (Epic 1.3).  
> Les vraies entités cibles sont dans `_bmad-output/planning-artifacts/epics.md` (Story 1.3).

---

## Architecture Frontend Angular 19

- **Standalone components** uniquement — pas de `NgModule`
- Point d'entrée config : `src/app/app.config.ts`
- Routing : `src/app/app.routes.ts`
- SSR activé via `@angular/ssr` — `server.ts` est le point d'entrée Node.js

---

## Architecture Mobile Flutter 3.41

- Material 3 activé par défaut
- `lib/main.dart` est le seul fichier pour l'instant (à structurer en Epic 1.8+)
- State management : Riverpod (à installer, prévu dans les stories)

---

## Authentification JWT

- Tokens signés HS256, clé configurée dans `appsettings.json > Jwt:Secret`
- Refresh token rotation : l'ancien token est révoqué à chaque refresh
- Endpoints publics protégés par rate limiter `"auth"` (100 req/min)
- Routes : `POST /api/v1/auth/register|login|refresh|logout`

## Variables d'environnement

Clé JWT en production : stocker dans Azure Key Vault ou variable d'environnement, jamais en clair.  
Ne jamais commiter des secrets.

---

## Suivi de l'avancement

Sprint plan : `_bmad-output/implementation-artifacts/sprint-status.yaml`  
Stories implémentées : `_bmad-output/implementation-artifacts/*.md`  
Prochaine story à implémenter : vérifier le premier statut `backlog` dans le sprint-status.
