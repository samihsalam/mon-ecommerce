# Deferred Work

## Deferred from: code review de 1-1-initialisation-des-3-projets (2026-04-14)

- Bearer transformer n'attache pas les security requirements aux opérations OpenAPI — à corriger dans Story 1.4 (JWT Auth infrastructure)
- Bearer scheme name `"Bearer"` vs `"BearerScheme"` (IdentityConstants) — configuration auth correcte dans Story 1.4
- `InitialiseDatabaseAsync` ignorée en production — remplacer par startup migration dans Story 1.3
- `EnsureDeletedAsync` + `EnsureCreatedAsync` en dev — remplacer par migrations EF Core dans Story 1.3
- Credentials admin hardcodés dans SeedAsync (`administrator@localhost` / `Administrator1!`) — à sécuriser dans Story 1.3
- ServiceDefaults (Aspire) orphelin dans le dépôt — nettoyage lors d'une story de maintenance
- `flutter build apk` non exécuté (Android SDK absent) — à valider manuellement quand Android SDK disponible
- SSR `maxAge: '1y'` pour tous les assets y compris index.html — configuration Cache-Control dans Story 3.x
- MappingTests `RuntimeHelpers.GetUninitializedObject` — à remplacer par tests domain-spécifiques dans Story 1.3+
