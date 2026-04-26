# Story 1.4 — Infrastructure Authentification JWT

**Status**: done  
**Completed**: 2026-04-26

## Ce qui a été implémenté

### Packages ajoutés
- `Microsoft.AspNetCore.Authentication.JwtBearer 9.0.5` (Infrastructure.csproj)
- `System.IdentityModel.Tokens.Jwt 8.11.0` (Infrastructure.csproj)

### Application layer (interfaces pures)

**`src/Application/Common/Interfaces/IJwtService.cs`**
- `GenerateAccessToken(userId, email, roles)` → JWT signé HS256
- `GenerateRefreshToken()` → token aléatoire 64 bytes en Base64Url

**`src/Application/Common/Interfaces/IAuthService.cs`**
- `RegisterAsync`, `LoginAsync`, `RefreshTokenAsync`, `LogoutAsync`
- Retourne `Result<AuthResponse>` — aucune dépendance vers Infrastructure

**`src/Application/Auth/Models/AuthResponse.cs`**
- Record : `(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)`

**`src/Application/Common/Models/Result.cs`** (extension)
- Ajout du type générique `Result<T>` avec `Success(T)` et `Failure(errors)`

### Auth Commands (Application layer)

| Fichier | Type |
|---|---|
| `RegisterCommand.cs` + Handler + Validator | email + password, FluentValidation |
| `LoginCommand.cs` + Handler | email + password |
| `RefreshTokenCommand.cs` + Handler | refreshToken |
| `LogoutCommand.cs` + Handler | refreshToken |

Tous les handlers délèguent à `IAuthService` (pattern Clean Architecture).

### Infrastructure layer

**`src/Infrastructure/Identity/JwtService.cs`**  
Génère access tokens (HS256, expiry configurable) et refresh tokens (CSPRNG).

**`src/Infrastructure/Identity/AuthService.cs`**  
Implémente `IAuthService` — rotation du refresh token (révocation de l'ancien à chaque refresh).

**`src/Infrastructure/DependencyInjection.cs`** (mise à jour)  
- Remplacé `AddBearerToken(IdentityConstants.BearerScheme)` par `AddJwtBearer`  
- Enregistré `IJwtService`, `IAuthService` en Transient

### Web layer

**`src/Web/Endpoints/Auth.cs`** (remplace Users.cs)  
Routes : `POST /api/v1/auth/register`, `/login`, `/refresh`, `/logout`  
- register/login/refresh : `AllowAnonymous` + `RequireRateLimiting("auth")`  
- logout : `RequireAuthorization`

**`src/Web/Program.cs`** (mise à jour)  
- `AddFixedWindowLimiter("auth")` : 100 req/min, queue=0
- Pipeline : `UseRateLimiter → UseAuthentication → UseAuthorization`

### Configuration (`appsettings.json`)
```json
"Jwt": {
  "Secret": "dev-secret-key-32-chars-minimum!!",
  "Issuer": "MonEcommerce",
  "Audience": "MonEcommerceClient",
  "AccessTokenExpirationMinutes": 60,
  "RefreshTokenExpirationDays": 7
}
```

## Décisions techniques

- **IAuthService abstraction** : évite une violation Clean Architecture (Application → Infrastructure)
- **Refresh token rotation** : l'ancien token est révoqué à chaque refresh (sécurité)
- **ClockSkew = Zero** : pas de tolérance de délai sur l'expiry JWT
- **Rate limiter "auth"** : protection brute-force sur les endpoints publics d'auth
