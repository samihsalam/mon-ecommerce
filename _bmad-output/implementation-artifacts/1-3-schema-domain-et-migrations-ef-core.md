# Story 1.3: Schéma Domain & Migrations EF Core

Status: done

## Story

As a developer,
I want all Domain entities modelled and migrated to PostgreSQL with the complete schema (including marketplace-ready `vendor_id`),
So that all future feature stories have a stable data foundation to build on.

## Acceptance Criteria

1. `dotnet ef database update` creates all expected tables
2. `vendor_id UUID` present on products, orders, stock
3. snake_case naming on all tables and columns (via EFCore.NamingConventions)
4. UUID primary keys with `gen_random_uuid()`
5. Indexes created: ix_products_category_id, ix_orders_user_id, ix_refresh_tokens_token, ix_carts_user_id, ix_order_items_order_id, ix_cart_items_cart_id
6. Migration idempotent
7. `dotnet build` — 0 errors, 0 warnings

## Tasks / Subtasks

- [x] Task 1: Mise à jour BaseEntity Guid (AC: #4)
  - [x] `int Id` → `Guid Id`

- [x] Task 2: Suppression entités template Todo (AC: build propre)
  - [x] Domain: TodoList, TodoItem, PriorityLevel, TodoItemCompletedEvent, UnsupportedColourException, Colour
  - [x] Application: TodoItems/, TodoLists/, WeatherForecasts/, LookupDto
  - [x] Infrastructure: TodoListConfiguration, TodoItemConfiguration
  - [x] Web: Endpoints/TodoItems.cs, TodoLists.cs, WeatherForecasts.cs

- [x] Task 3: Création des 10 entités e-commerce (AC: #1)
  - [x] Category, Product, ProductImage, Stock, Address, Order, OrderItem, Cart, CartItem, RefreshToken
  - [x] OrderStatus enum (Pending/Processing/Shipped/Delivered/Cancelled)

- [x] Task 4: Configurations EF Core snake_case (AC: #3, #4, #5)
  - [x] EFCore.NamingConventions 9.0.0 ajouté
  - [x] `UseSnakeCaseNamingConvention()` dans DependencyInjection.cs
  - [x] 10 configurations IEntityTypeConfiguration<T>
  - [x] `gen_random_uuid()` sur tous les PKs
  - [x] vendor_id sur products, orders, stocks
  - [x] xmin (xid) rowVersion sur Stock pour concurrence optimiste

- [x] Task 5: Mise à jour ApplicationDbContext + IApplicationDbContext (AC: #1)
  - [x] 10 DbSet<T> e-commerce
  - [x] Template Todo supprimé

- [x] Task 6: Migration EF Core InitialCreate (AC: #1, #6)
  - [x] `dotnet ef migrations add InitialCreate` exécuté
  - [x] Fichier: `src/Infrastructure/Data/Migrations/20260426123107_InitialCreate.cs`
  - [x] `dotnet build` — 0 erreur, 0 warning

## Dev Notes

### Appliquer la migration

```bash
cd backend/MonEcommerce
docker compose up -d          # PostgreSQL doit tourner
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

### Tables créées

| Table | Notes |
|---|---|
| categories | self-referencing parentId |
| products | vendor_id, ix_products_category_id |
| product_images | cascade delete |
| stocks | vendor_id, xmin rowVersion, 1:1 product |
| orders | vendor_id, ix_orders_user_id |
| order_items | snapshot ProductName + UnitPriceInCents |
| carts | nullable UserId (anonymous) |
| cart_items | |
| addresses | ix_addresses_user_id |
| refresh_tokens | ix_refresh_tokens_token (unique) |
| ASP.NET Identity tables | AspNetUsers, Roles, Claims, etc. |

### Deferred vers Story 1.4+

- `EnsureDeletedAsync` + `EnsureCreatedAsync` encore présents dans ApplicationDbContextInitialiser — Story 1.4 remplacera par migration auto au démarrage
- Hardcoded admin credentials (`administrator@localhost` / `Administrator1!`) — Story 1.4

### References

- [Source: epics.md#Story 1.3] — Acceptance criteria
- [Source: architecture.md#AR3, AR10, AR15] — EF Core migrations, xmin, vendor_id

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6 (bagstore-fullstack-dev)

### Completion Notes List

- ✅ BaseEntity.Id: int → Guid
- ✅ Template Todo supprimé intégralement (Domain, Application, Infrastructure, Web)
- ✅ 10 entités e-commerce créées
- ✅ OrderStatus enum
- ✅ EFCore.NamingConventions 9.0.0
- ✅ 10 configurations EF Core
- ✅ vendor_id sur products, orders, stocks
- ✅ xmin concurrency token sur Stock
- ✅ Migration InitialCreate générée
- ✅ `dotnet build` — 0 erreur, 0 warning
- ⚠️ `dotnet ef database update` à exécuter manuellement (Docker requis)
