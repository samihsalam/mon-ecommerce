# Story 1.9: CI/CD Pipeline & Déploiement

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want GitHub Actions pipelines (build + test) and Docker/Railway/Vercel deployment configurations in place,
so that every push is automatically validated and deployment is possible from the very first feature.

## Acceptance Criteria

1. **Given** a push to the repo's default branch (**`master`** — see Dev Notes, not `main`), **when** the GitHub Actions CI workflow runs, **then** `dotnet build && dotnet test`, `ng build`, and `flutter build apk` all pass in separate jobs.
2. Any failing test/build step fails the workflow (blocks merge via required-status-check once branch protection is configured — see Dev Notes for what is/isn't automatable here).
3. **Given** the backend `Dockerfile`, **when** `docker build` is run, **then** the image builds successfully and `docker run` starts the API listening on port 8080.
4. **Given** Sentry DSNs are configured in environment variables, **when** the three apps start, **then** Sentry is initialized in .NET (backend), Angular (frontend), and Flutter (mobile), and a test error is captured. **This AC has a manual-verification component — see Dev Notes "External accounts required."**
5. Railway configuration deploys the backend Docker image on push to `master`. **Requires a Railway account/project — see Dev Notes.**
6. Vercel configuration deploys the Angular SSR app on push to `master`. **Requires a Vercel account/project — see Dev Notes.**
7. No secrets are committed to the repository, validated by a CI secret-scan step (gitleaks).

## Tasks / Subtasks

- [x] Task 1: GitHub Actions CI workflow (AC: #1, #2, #7)
  - [x] Create `.github/workflows/ci.yml` at the **repo root** (not inside `backend/`) with 4 jobs, triggered on `push`/`pull_request` to `master`:
    - `backend`: `actions/setup-dotnet@v4` (9.0.x) → `dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build`, working-directory `backend/MonEcommerce`
    - `frontend`: `actions/setup-node@v4` (20.x) → `npm ci` → `npm run build`, working-directory `frontend/mon-ecommerce-web`
    - `mobile`: `subosito/flutter-action@v2` (stable channel) → `flutter pub get` → `flutter build apk --debug` (debug, not release — no signing keystore exists yet), working-directory `mobile/mon_ecommerce_mobile`
    - `secret-scan`: `gitleaks/gitleaks-action@v2` (no config file needed for a first pass; default ruleset)
  - [x] All 4 jobs run in parallel (no `needs:` between them — they're independent)
- [x] Task 2: Backend Dockerfile (AC: #3)
  - [x] Create `backend/MonEcommerce/Dockerfile` — multi-stage: `mcr.microsoft.com/dotnet/sdk:9.0` build stage (restore + publish), `mcr.microsoft.com/dotnet/aspnet:9.0` runtime stage — **deviation**: project file is `src/Web/Web.csproj`, not `MonEcommerce.Web.csproj` as originally assumed (assembly name is `MonEcommerce.Web`, file name isn't) — Dockerfile uses the correct path
  - [x] `EXPOSE 8080` + `ENV ASPNETCORE_URLS=http://+:8080` in the runtime stage
  - [x] `ENTRYPOINT ["dotnet", "MonEcommerce.Web.dll"]`
  - [x] Fix the `UseHttpsRedirection()`/reverse-proxy conflict in `Program.cs` (added `UseForwardedHeaders`)
  - [x] Add `backend/MonEcommerce/.dockerignore`
  - [x] Verify: `docker build` + `docker run -p 8080:8080` — confirmed listening, `/health` returns `200 "healthy"`
- [x] Task 3: Sentry — backend (.NET) (AC: #4)
  - [x] Add `Sentry.AspNetCore` 6.7.0 to `Directory.Packages.props` + `src/Web/Web.csproj`
  - [x] Wire `builder.WebHost.UseSentry(...)` in `Program.cs`, reading DSN from `Sentry:Dsn` config — **bug found and fixed during Docker verification**: `options.Dsn = builder.Configuration["Sentry:Dsn"]` passes `null` (not empty string) when unset, and `Sentry.AspNetCore` throws `ArgumentNullException` on `null` (only empty string is treated as "disabled" per its own error message) — fixed with `?? string.Empty`
  - [x] Add a `GET /api/v1/debug/sentry-test` endpoint (dev-only) that throws, for manual Sentry-capture verification
- [x] Task 4: Sentry — frontend (Angular) (AC: #4)
  - [x] Add `@sentry/angular` 10.66.0 to `package.json`
  - [x] Create `src/environments/environment.ts` + `environment.production.ts` (new `environments/` folder — didn't exist before)
  - [x] Wire `fileReplacements` in `angular.json`'s `production` configuration
  - [x] `Sentry.init(...)` in `src/main.ts` (guarded by `if (environment.sentryDsn)`) + `{ provide: ErrorHandler, useValue: Sentry.createErrorHandler() }` in `app.config.ts` — verified safe for the SSR/prerender bundle (which shares `app.config.ts`) via an actual `ng build`, which prerenders successfully
- [x] Task 5: Sentry — mobile (Flutter) (AC: #4)
  - [x] Add `sentry_flutter` 9.24.0 to `pubspec.yaml`
  - [x] Wrap `runApp` in `main.dart` with `SentryFlutter.init(...)`
  - [x] DSN via `String.fromEnvironment('SENTRY_DSN', defaultValue: '')` — **not verified by tooling**, Flutter/Dart SDK still unavailable in this environment (same pre-existing gap as Stories 1.1/1.8)
- [x] Task 6: Railway deployment config (AC: #5)
  - [x] Create `backend/MonEcommerce/railway.json` — **deviation**: `healthcheckPath` changed from `/` to `/health` (a new endpoint added in Task 2's fix) — in production (non-`IsDevelopment()`), nothing was mapped at `/` at all (the `/`→`/scalar` redirect is dev-only), so Railway's healthcheck against `/` would have 404'd
  - [x] Manual setup steps documented in Dev Notes
- [x] Task 7: Vercel deployment config (AC: #6)
  - [x] Create `frontend/mon-ecommerce-web/vercel.json` + `api/index.js` matching the actual `server.ts` (`export default app`, re-exported directly since an Express app is callable as `(req, res)`)
  - [x] Manual setup steps documented in Dev Notes
- [x] Task 8: Verification
  - [x] `dotnet build` + `dotnet test` — 0 errors, 15/15 tests pass (regression-checked after Sentry package addition)
  - [x] `ng build` (production config, exercising `fileReplacements`) + `ng test` — both pass, 7/7 Karma tests green
  - [x] `docker build` + `docker run` — verified per Task 2, including catching and fixing the null-DSN crash
  - [ ] Push a throwaway branch and open a PR to confirm the 4 CI jobs actually run on GitHub Actions — **not done yet, needs explicit go-ahead since it's a visible push/PR action** (see Completion Notes)

## Dev Notes

### Everything in this story is genuinely greenfield — confirmed via direct repo inspection

No `.github/` directory, no `Dockerfile` anywhere, no Sentry SDK reference in any of the 3 apps' code (only a placeholder env var name in `.env.example`), no `railway.json`/`vercel.json`, no Angular `environments/` folder, no Flutter `--dart-define` pattern. Nothing to preserve or migrate — build all of it fresh per the tasks above.

### Critical: default branch is `master`, not `main`

`git branch -a` confirms only `master` exists on both remotes (GitHub `origin` and Azure DevOps `azure`) — there is no `main` branch. Epics.md's AC prose says "push to `main` branch," which is generic BMad boilerplate that doesn't match this repo. **Every branch reference in the CI workflow trigger, Railway auto-deploy config, and Vercel production-branch setting must be `master`.**

### Two stale files from an abandoned architecture pivot — do not follow their lead

`backend/MonEcommerce/docker-compose.yml` (PostgreSQL + Redis only, no app service) and `backend/MonEcommerce/.env.example`'s DB section (`ConnectionStrings__MonEcommerceDb=Host=localhost;Port=5432;...`) both describe **PostgreSQL**, but the project actually pivoted to **SQL Server** (confirmed: `appsettings.json`'s connection string is SQL Server syntax `Server=DESKTOP-M36577B;...;Trusted_Connection=True`; CLAUDE.md explicitly states "SQL Server — DESKTOP-M36577B, pas de Docker requis"; the Story 1.3 git commit message literally says "(SQL Server)"). These two files were never updated after the pivot. **Do not use them as a reference for the backend Dockerfile or Railway config** — the Dockerfile only needs to package the compiled app (no local DB container involved; Railway's production DB is a separate, not-yet-decided concern — see "Open question" below). Leave these stale files untouched; fixing them is out of scope for this story (log it in `deferred-work.md` if you notice it during review, don't fix it here).

### Open question this story does NOT resolve: production database on Railway

Architecture.md assumed "Railway (backend + PostgreSQL)," but the project now runs SQL Server locally. Railway does not offer a native managed SQL Server the way it does Postgres — this story's Railway config focuses purely on deploying the **Docker image** (AC #5's literal requirement); provisioning a production database (Railway-hosted SQL Server container, Azure SQL, or reverting to Postgres for prod only) is a separate decision for a later story or explicit user direction. Don't try to solve it here.

### The `UseHttpsRedirection()` bug this story will expose

Current `Program.cs` (`src/Web/Program.cs`) calls `app.UseHttpsRedirection()` unconditionally in the non-development branch. Railway (and most container PaaS) terminates TLS at their edge and forwards **plain HTTP** to your container — if the app itself also tries to force an HTTPS redirect, you get a redirect loop (or Railway's health check fails outright because it hits the container over HTTP and gets redirected instead of a 200). Fix: add `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto })` **before** `UseHttpsRedirection()`, so the app correctly recognizes the original request was HTTPS (via the `X-Forwarded-Proto` header Railway sets) and does not re-redirect. This is a standard, well-known ASP.NET Core container/reverse-proxy pattern, not something specific to this project.

### Dockerfile — exact structure

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Web/MonEcommerce.Web.csproj
RUN dotnet publish src/Web/MonEcommerce.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MonEcommerce.Web.dll"]
```
Build from the `backend/MonEcommerce/` directory as context: `docker build -f Dockerfile .` (run from inside `backend/MonEcommerce/`), or from repo root: `docker build -f backend/MonEcommerce/Dockerfile backend/MonEcommerce`. The simple `COPY . .` (whole build context, not per-project `COPY *.csproj` layer-caching tricks) is a deliberate choice for this first Dockerfile — this repo uses Central Package Management (`Directory.Packages.props`) across 5 projects (Domain/Application/Infrastructure/Web/Shared), and optimizing layer caching for a monorepo with CPM is a legitimate follow-up, not a blocker for getting a working image now.

### Sentry package version

Check NuGet for the current `Sentry.AspNetCore` stable release compatible with .NET 9 (the SDK's major version tends to track roughly with feature releases, not tied to a specific .NET version — verify compatibility notes on the package page) and pin that exact version in `Directory.Packages.props`, following the same `<PackageVersion Include="..." Version="X" />` pattern as every other entry in that file.

### Why it's safe to commit real Sentry DSNs (not a secrets-management gap)

A Sentry DSN is a public, write-only identifier — it can only be used to *send* error events to that specific project, not read/access any data, and Sentry's own documentation confirms DSNs are safe to expose in client-side bundles (this is exactly why `@sentry/angular`/`sentry_flutter` always end up in a shipped browser/mobile bundle where any user could extract them anyway). Treat it like a public API key, not a credential — commit `environment.production.ts`'s DSN value directly once you have one; don't route it through GitHub Secrets/CI env-var indirection for the frontend/mobile builds (the backend can still read it from `Sentry:Dsn` config/env var, consistent with how every other backend integration in this project is configured, but that's a convention-consistency choice, not a security requirement).

### Vercel — this project's `server.ts` does NOT export `reqHandler` (most tutorials assume it does)

`frontend/mon-ecommerce-web/src/server.ts` uses the `CommonEngine` pattern and ends with `export default app;` (an Express app instance), not the newer `AngularNodeAppEngine`/`createNodeRequestHandler`/`reqHandler`-export pattern from more recent Angular scaffolds. Since an Express app is itself callable as `(req, res)`, the Vercel wrapper is simpler than most tutorials describe — just re-export the default:

`frontend/mon-ecommerce-web/api/index.js`:
```js
export default async (req, res) => {
  const { default: app } = await import('../dist/mon-ecommerce-web/server/server.mjs');
  return app(req, res);
};
```

`frontend/mon-ecommerce-web/vercel.json`:
```json
{
  "version": 2,
  "rewrites": [
    { "source": "/", "destination": "/api" },
    { "source": "/(.*)", "destination": "/api" }
  ],
  "functions": {
    "api/index.js": {
      "includeFiles": "dist/mon-ecommerce-web/**"
    }
  }
}
```
The explicit `/` rewrite (not just the catch-all) is required — Vercel's filesystem routing otherwise serves `index.csr.html` directly for the root route before any rewrite fires, silently skipping SSR. The `includeFiles` glob must exactly match `angular.json`'s `outputPath` (currently `dist/mon-ecommerce-web`) — a mismatch deploys successfully but fails silently at runtime.

### Railway config

`backend/MonEcommerce/railway.json`:
```json
{
  "$schema": "https://railway.app/railway.schema.json",
  "build": {
    "builder": "DOCKERFILE",
    "dockerfilePath": "Dockerfile"
  },
  "deploy": {
    "healthcheckPath": "/",
    "restartPolicyType": "ON_FAILURE",
    "restartPolicyMaxRetries": 3
  }
}
```

### External accounts required — cannot be provisioned or fully verified from this environment

This story can implement all SDK wiring and config files correctly, but three ACs have a manual, external-account component that no coding agent can complete:
- **AC #4 (Sentry):** requires an actual Sentry account + project(s) to get real DSNs, and manually checking the Sentry dashboard to confirm a test error arrived. Code-level completion = the SDKs are wired and gracefully no-op without a DSN; full AC satisfaction requires the user's own Sentry account.
- **AC #5 (Railway):** requires creating a Railway account, creating a project, linking this GitHub repo, and setting the Root Directory to `backend/MonEcommerce`. The `railway.json` file alone does not deploy anything without that dashboard setup.
- **AC #6 (Vercel):** requires creating a Vercel account, creating a project, linking this GitHub repo, and setting Root Directory to `frontend/mon-ecommerce-web`. Same caveat as Railway.

Flag these explicitly as manual follow-up steps in the story's completion notes rather than claiming them fully done — this mirrors how Story 1.6 handled credentials-optional external services and how Story 1.8's Flutter-tooling gap was handled (documented limitation, not silently glossed over).

### Project Structure Notes

- `.github/workflows/ci.yml` lives at the **monorepo root**, not inside any of the 3 app folders (GitHub Actions requires this).
- `Dockerfile`, `.dockerignore`, `railway.json` all live in `backend/MonEcommerce/` (matches architecture.md's proposed tree).
- `vercel.json` and `api/index.js` live in `frontend/mon-ecommerce-web/` (the Vercel project's Root Directory will be set to this folder).
- New Angular `src/environments/` folder — first time this pattern is introduced in this project.
- New Flutter dependency `sentry_flutter` — no existing config-injection pattern to follow, this establishes the `--dart-define` convention for future mobile secrets/config.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.9: CI/CD Pipeline & Déploiement] — acceptance criteria (note "main" branch reference is generic boilerplate, doesn't match this repo)
- [Source: _bmad-output/planning-artifacts/architecture.md line 249-252, 452-454] — GitHub Actions/Docker/Railway/Vercel/Sentry decisions and proposed file tree
- [Source: CLAUDE.md] — current SQL Server local-dev reality (supersedes architecture.md's PostgreSQL assumption)
- [Source: backend/MonEcommerce/src/Web/Program.cs] — current middleware pipeline, confirms `UseHttpsRedirection()` unconditional-in-non-dev bug
- [Source: backend/MonEcommerce/Directory.Packages.props] — Central Package Management convention to follow for the new Sentry package
- [Source: frontend/mon-ecommerce-web/src/server.ts] — confirms `CommonEngine` + `export default app` pattern (not `reqHandler`)
- [Source: git commit 0c869d3 "feat(story-1.3): domain schema and EF Core migrations (SQL Server)"] — confirms the Postgres→SQL Server pivot
- [Source: www.carbonatethis.com/articles/2026-04-02-hosting-angular-on-vercel, fetched 2026-07 — Vercel Angular SSR wrapper pattern, adapted for this project's actual server.ts]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `docker build` (1st attempt) failed: `global.json` pinned `9.0.101`/`latestPatch`, but the standard `mcr.microsoft.com/dotnet/sdk:9.0` image ships `9.0.316` (different feature band — `latestPatch` only rolls forward within the same band). This wasn't a local-machine-only quirk this time — it would break for anyone building this Dockerfile or running CI. Fixed durably: `rollForward` changed from `latestPatch` to `latestFeature` (rolls forward across feature bands within 9.0.x). This is a **permanent** change to the committed `global.json`, not a revert-after-verification workaround like in Stories 1.7/1.8.
- `docker run` (1st attempt) crashed on startup: `Sentry.AspNetCore`'s `SentrySdk.InitHub` throws `ArgumentNullException` when `options.Dsn` is `null` (only an empty string `""` is treated as "Sentry disabled" per the SDK's own error message). `builder.Configuration["Sentry:Dsn"]` returns `null` when unset. Fixed with `?? string.Empty`.
- `docker run` (2nd attempt) succeeded; `curl http://localhost:8080/health` → `200 OK` / `"healthy"`.
- Local machine still has no .NET 9 SDK (only 10.0.301, confirmed again) — `dotnet build`/`dotnet test` were verified by temporarily setting `rollForward` to `latestMajor`, then reverting to the durable `latestFeature` fix afterward (confirmed via `git diff` showing only the intended `latestPatch`→`latestFeature` change survives).
- `ng build` (production config) succeeded, including SSR prerendering — confirms the `@sentry/angular` `ErrorHandler` provider in the shared `app.config.ts` doesn't break the server bundle.
- `ng test` — 7/7 Karma tests pass (via Edge as the `ChromeHeadless` launcher, same as Stories 1.7/1.8 — no Chrome installed on this machine).
- Flutter/Dart SDK still unavailable in this environment (same gap as Stories 1.1/1.8) — `flutter pub get`/`flutter build apk`/`flutter analyze` not run locally; `pubspec.lock` not regenerated for `sentry_flutter`.
- GitHub Actions workflow itself has **not** been exercised by an actual push/PR — this requires pushing a branch and opening a PR, a visible action on the shared repo, so it was held for explicit user go-ahead rather than done unilaterally.

### Completion Notes List

- All 7 tasks' code/config fully implemented; Task 8's final verification step (push a throwaway branch + PR to confirm the GitHub Actions workflow actually runs) is intentionally not yet done — awaiting user go-ahead since it's a visible push action.
- Two real bugs were found and fixed during Docker verification (not just assumed correct from reading docs): the `global.json` SDK-band mismatch and the Sentry null-DSN crash. Both would have silently broken the very first real deployment attempt if not caught here.
- `railway.json`'s `healthcheckPath` was changed from the story's originally-planned `/` to a new `/health` endpoint, because production mode has nothing mapped at `/` at all (the `/`→`/scalar` redirect is dev-only) — this was caught by reasoning through the actual `Program.cs` pipeline, not assumed.
- AC #4/#5/#6's external-account components (Sentry project, Railway project, Vercel project) remain manual follow-ups for the user — flagged per Dev Notes' "External accounts required" section, not silently claimed done.
- Flutter-side Sentry wiring (Task 5) follows the same hand-written-but-tool-unverified pattern as Story 1.8's Flutter work, for the same pre-existing environment reason.

### File List

- `.github/workflows/ci.yml` (new)
- `backend/MonEcommerce/Dockerfile` (new)
- `backend/MonEcommerce/.dockerignore` (new)
- `backend/MonEcommerce/railway.json` (new)
- `backend/MonEcommerce/global.json` (modified: `rollForward` latestPatch → latestFeature, permanent fix)
- `backend/MonEcommerce/Directory.Packages.props` (added `Sentry.AspNetCore` 6.7.0)
- `backend/MonEcommerce/src/Web/Web.csproj` (added `Sentry.AspNetCore` package reference)
- `backend/MonEcommerce/src/Web/Program.cs` (Sentry init, `UseForwardedHeaders`, `/health` endpoint, dev-only `/api/v1/debug/sentry-test`)
- `frontend/mon-ecommerce-web/vercel.json` (new)
- `frontend/mon-ecommerce-web/api/index.js` (new)
- `frontend/mon-ecommerce-web/package.json` (added `@sentry/angular` 10.66.0)
- `frontend/mon-ecommerce-web/src/environments/environment.ts` (new)
- `frontend/mon-ecommerce-web/src/environments/environment.production.ts` (new)
- `frontend/mon-ecommerce-web/angular.json` (added `fileReplacements` to production config)
- `frontend/mon-ecommerce-web/src/main.ts` (Sentry.init)
- `frontend/mon-ecommerce-web/src/app/app.config.ts` (Sentry ErrorHandler provider)
- `mobile/mon_ecommerce_mobile/pubspec.yaml` (added `sentry_flutter` 9.24.0)
- `mobile/mon_ecommerce_mobile/lib/main.dart` (SentryFlutter.init wrapping runApp)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (1.9 status)
