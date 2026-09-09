# Laraue.Apps.Identity

Global user identity source of truth for the Laraue.* apps. Every consuming app (`Laraue Boards`,
`Laraue Learn Language`, ...) has its own local user id (today: a Telegram id) - this service turns
that into one global Laraue user id (`Guid`) shared across every service, and records which
services a user has touched. See [AGENTS.md](AGENTS.md) for the fuller design rationale, including
what's deliberately *not* built yet (Google Auth, a public API, subscription/transaction
aggregation).

**Stage 1 (this state)**: internal gRPC only. `UserIdentityService.CreateUserIfNotExists` takes a
Telegram id + a `ServiceId`, creates a global user on first sight of that Telegram account (or
returns the existing one), and records that the calling service is used by that user.

## App structure

### Laraue.Apps.Identity.DataAccess
EF Core `DatabaseContext`, entity models, migrations, and the static reference data (`Service`)
seeded via `HasData`.

### Laraue.Apps.Identity.Internal.Contracts
The gRPC contract other apps call directly: `user_identity.proto`
(`UserIdentityService.CreateUserIfNotExists`) plus its generated client/server stubs, served by
`InternalApiHost`/`InternalApiServices`.

Published to NuGet.org as `Laraue.Apps.Identity.Internal.Contracts` via a manually-triggered
`.github/workflows/nuget-publish.yml` run - `main` publishes the real version, any other branch
publishes a `-alpha.<run number>` prerelease so another service can integrate against an
in-progress contract change before it merges.

### Laraue.Apps.Identity.Services
Business logic shared across hosts (not tied to any one of them): `UserIdentityService` - the
lookup/create-if-missing flow for a global user, keyed by Telegram id.

### Laraue.Apps.Identity.InternalApiServices
The gRPC-facing implementation of `Internal.Contracts` (`UserIdentityGrpcService`), mapping between
the wire contract and `Laraue.Apps.Identity.Services`. Kept separate from `InternalApiHost` so the
host project stays bootstrap-only.

### Laraue.Apps.Identity.InternalApiHost
The internal gRPC host: `Program.cs` only (Kestrel, DI, OpenTelemetry wiring, migrations on
startup) - no service implementations of its own. Listens on plain HTTP/2 (h2c, no TLS), since this
is trusted service-to-service traffic, not public-facing.

## Local run

1. Have Postgres running locally and reachable with the credentials in
   `src/Laraue.Apps.Identity.InternalApiHost/appsettings.json`'s `ConnectionStrings:Postgre`
   (defaults to `User ID=postgres;Password=postgres;Host=localhost;Port=5432`, database
   `identity`).
2. Run the host:
   ```
   dotnet run --project src/Laraue.Apps.Identity.InternalApiHost
   ```
   Migrations apply automatically on startup - no separate `dotnet ef database update` step.
3. The host listens on two ports by default (see `Kestrel:GrpcPort`/`Kestrel:HealthPort` in
   `appsettings.json`): `http://localhost:5363` for gRPC (HTTP/2-only, cleartext) and
   `http://localhost:5364` for `/_health`/`/_metrics` (HTTP/1.1-only). They're split because Kestrel
   can't multiplex HTTP/1.1 and h2c on the same endpoint without TLS - see the comment in
   `Program.cs` if you're wondering why.

## Running tests

`tests/Laraue.Apps.Identity.IntegrationTests` needs its own Postgres database, separate from the
dev one - see that project's `appsettings.json` (`identity_tests`). Migrations run automatically
when the test host starts, same as the real service.
```
dotnet test tests/Laraue.Apps.Identity.IntegrationTests
```
