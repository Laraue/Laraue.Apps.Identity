# AGENTS.md

Guidance for AI agents working in this repo. Human-readable conventions and gotchas that aren't
obvious from the code alone.

## What this project is

The global identity source of truth for the Laraue.* ecosystem. Every other Laraue app
(`Laraue Boards`, `Laraue Learn Language`, ...) has its own local notion of a user (today: a
Telegram id) - this service is what turns that local identity into one global Laraue user id
(`Guid`), shared across every service, and is where "which services does this user use" gets
recorded. It is intentionally **not** a billing/subscription service - that's
`Laraue.Apps.Billing`, a separate app; this service only answers "who is this user, globally" and
"what has this user touched".

**Stage 1 (this state)**: internal gRPC only, one login method (Telegram id). Callers
(`Laraue.Apps.Boards` first, `Laraue.Apps.LearnLanguage` next) call
`UserIdentityService.CreateUserIfNotExists` with their local Telegram id + which service they are,
and get back the global user id, creating the global user on first sight of that Telegram account.

**Not built yet - future stages, don't add speculatively**:
- Google Auth as a second login method (a `GoogleAccount` table alongside `TelegramAccount`,
  resolving to the same kind of global `User` - the schema already isolates "how a user
  authenticates" from the `User` row itself for exactly this reason, see "Domain model" below).
- Any public-facing API (a `WebApiHost`, e.g. for "see all your subscriptions/services/transactions
  in one place"). Stage 1 is internal-gRPC-only on purpose - see "Project layout".
- Aggregating actual subscriptions/transactions from `Laraue.Apps.Billing` here. This service only
  tracks *that* a user has touched a service (`UserService`), not what they've paid for - that
  stays Billing's job. A future "user dashboard" stage would likely have this service's public API
  call out to Billing's internal gRPC API to assemble that view, not duplicate Billing's data here.

## Domain model

- `User` - a global Laraue user identity (`Id` Guid, `CreatedAt`). Carries no credential itself -
  see `TelegramAccount` below for how a user actually authenticates. Keeping credentials in their
  own table(s) rather than on `User` is what lets a second login method (Google, stage 2) be added
  later without touching `User` or anything downstream of it.
- `TelegramAccount` - links a Telegram account to a `User`. `TelegramId` (the Telegram-assigned
  numeric id, **not** database-generated - see the `ValueGeneratedNever()` note in
  `DatabaseContext`) is the actual lookup key: it's globally unique per Telegram account, not per
  calling service, so the same Telegram account always resolves to the same global `User`
  regardless of which service asks (`Laraue Boards` and `Learn Language` calling with the same
  `TelegramId` get the same global user id back).
- `Service` - a consuming app (`LaraueBoards`, `LearnLanguage`), identified by its own `ServiceId`
  enum. This is a deliberately independent registry from `Laraue.Apps.Billing`'s own `ServiceId`
  enum - the two services aren't schema-coupled, even though the numeric values happen to start the
  same way today. Don't assume a given id means the same service in both systems.
- `UserService` - records that a `User` has used a `Service` (composite key
  `UserId`/`ServiceId`, `FirstSeenAt`). One row per user per service no matter how many times
  `CreateUserIfNotExists` is called for that pair - this is the basis for "which services does this
  user already use" from the top-level vision for this app.

## Project layout

Solution: `Laraue.Apps.Identity.sln`. Structured the same way as `Laraue.Apps.Billing` (see that
repo's `AGENTS.md` if you need the fuller rationale for this layering) - `Host -> Host{Services} ->
Services`, with a host never referencing `Services` (or `DataAccess`... though see the
InternalApiHost note below) directly, only through its own `Host{Services}` project.

- `src/Laraue.Apps.Identity.DataAccess` - EF Core `DatabaseContext`, entities, migrations, and the
  static seed data (`Data/ServicesData.cs`).
- `src/Laraue.Apps.Identity.Internal.Contracts` - the `.proto` service-to-service contract
  (`Protos/user_identity.proto`, `UserIdentityService.CreateUserIfNotExists`) plus its generated
  stubs, built with `GrpcServices="Both"` so it ships both the client stub (for callers like
  `Laraue.Apps.Boards`) and the server base class from one package. `IsPackable=true` - published to
  NuGet.org as `Laraue.Apps.Identity.Internal.Contracts` via `.github/workflows/nuget-publish.yml`
  (manual `workflow_dispatch` trigger; on `main` publishes the base `<Version>` from
  `Directory.Build.props` as-is, on any other branch appends `-alpha.<run number>` - same mechanism
  as Billing's, see that repo's `AGENTS.md` "NuGet publishing" section for the full explanation).
  No DB/ASP.NET dependencies - kept minimal so it can be shared as a package.
- `src/Laraue.Apps.Identity.Services` - core business logic (`UserIdentityService` -
  `CreateUserIfNotExistsAsync`), not tied to gRPC or any other host. Validates the `ServiceId` and
  handles the actual "look up by TelegramId, create the User + TelegramAccount if new, then ensure
  the UserService row exists" flow, including retrying past a lost race on either insert (see the
  `DbUpdateException` catches in `UserIdentityService.cs` - two concurrent `CreateUserIfNotExists`
  calls for the *same* Telegram account, or the same user+service pair, are expected and handled,
  not a bug).
- `src/Laraue.Apps.Identity.InternalApiServices` - the gRPC-facing implementation of
  `Internal.Contracts` (`UserIdentityGrpcService`, mapping wire types <-> `Services` types) plus its
  own DI composition (`ServiceCollectionExtensions.AddInternalApiServices()` registers
  `IUserIdentityService`/`UserIdentityService`). Kept out of `InternalApiHost` on purpose, same as
  Billing's `InternalApiServices`/`InternalApiHost` split.
- `src/Laraue.Apps.Identity.InternalApiHost` - the gRPC host: `Program.cs` only (Kestrel h2c/DI/
  OpenTelemetry wiring, migrations on startup via `db.Database.MigrateAsync()`). No gRPC service
  implementations of its own.
- `tests/Laraue.Apps.Identity.IntegrationTests` - `InternalApiTestHost` wraps
  `WebApplicationFactory<Program>` for `InternalApiHost` and hands back a real generated
  `UserIdentityService.UserIdentityServiceClient` wired to the in-memory `TestServer` via
  `Grpc.Net.Client` (no real socket) - tests call the gRPC contract the same way another Laraue app
  would, not the business-logic interface directly. `InternalApiTestHost.CleanDatabase()` /
  `DbExtensions.CleanDatabase` wipe `UserServices`/`TelegramAccounts`/`Users` (not the seeded
  `Services` table) - call it at the start of every test since, unlike Billing's tariffs, this
  service's tests write rows from the very first test.

## EF Core

- Snake_case naming convention (`.UseSnakeCaseNamingConvention()` in `InternalApiHost/Program.cs`),
  same as Billing.
- `TelegramAccount.TelegramId` is the entity's primary key **and** an externally-supplied value (the
  Telegram-assigned id), not a database identity/sequence - `DatabaseContext.OnModelCreating` calls
  `.Property(x => x.TelegramId).ValueGeneratedNever()` on it. Without this EF defaults a `long`
  primary key to an identity column, which would silently ignore whatever `TelegramId` the caller
  actually sent. If you add another external-id-as-primary-key table later (e.g. a Google account
  id), it needs the same treatment.
- Reference data (`Service`) is seeded via EF Core `HasData` in `DatabaseContext.OnModelCreating`,
  sourced from `DataAccess/Data/ServicesData.cs` - adding a new consuming service means editing that
  file and adding a migration, same convention as Billing's `*Data.cs` classes.

## Database safety

Same rules as `Laraue.Apps.Billing`: migrations run automatically on startup
(`InternalApiHost`/the integration test host both call `MigrateAsync()`/`Migrate()`), there's
normally no need for `dotnet ef database update` by hand, and **never run `dotnet ef database
drop`** - `--startup-project` determines which `appsettings.json` connection string a command
targets, and it doesn't necessarily point at the test database (dev `identity` vs. test
`identity_tests`). Confirm which database a destructive command targets before running it, and ask
first if there's any ambiguity.

## Testing

Naming convention: `{Handler}_Should{ExpectedBehavior}_When{Condition}`, matching Billing/Boards.
Prefer a separate `[Fact]`/`[Theory]` per case over one test covering several scenarios.

## Task flow

Create branch with pattern `feature/task-number-task-description`, matching Billing/Boards'
convention.
