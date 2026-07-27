# SQLite → PostgreSQL migration + Aspire local dev — design

- **Date:** 2026-07-27
- **Status:** Approved (brainstorming complete)
- **Owner:** Owner

## Goal

Move WiseWizard off SQLite onto PostgreSQL, and add a .NET Aspire local-dev
orchestration (AppHost + ServiceDefaults) that provisions Postgres in a container and
supplies secrets from `dotnet user-secrets`. Drive the change through the existing SDLC
task/ADR structure. Match the conventions already established by the sibling Sentra app
(Postgres on helios, Hangfire on Postgres, Aspire AppHost as the one-command local entry).

## Decisions (locked with the Owner)

1. **Keep Dapper; swap the driver to Npgsql.** No EF Core rewrite. The repositories are
   already behind Core interfaces, so the change is mechanical and preserves the existing
   tests and the >95% coverage gate.
2. **Postgres hosting = a separate database inside the existing helios Postgres.** The Owner
   creates the database + scoped user on helios manually. WiseWizard only receives a
   connection string (via Infisical). No PVC, no WiseWizard-owned Postgres deployment.
3. **Aspire supplies local secrets via `dotnet user-secrets` + Aspire parameters.** The
   AppHost provisions a Postgres container and injects its connection string automatically;
   Anthropic/Telegram/IBKR values come from user-secrets (gitignored). Infisical remains the
   source for staging/prod only.
4. **Add both Aspire projects:** `WiseWizard.AppHost` and `WiseWizard.ServiceDefaults`
   (OTel/health/resilience), with `builder.AddServiceDefaults()` wired into the Host.

## Scope of change

### Data access (Dapper kept)

- `ISqliteConnectionFactory` → `IDbConnectionFactory` returning `NpgsqlConnection`.
- `SqliteConnectionFactory` → `NpgsqlConnectionFactory(connectionString)`. Drops the
  SQLite `PRAGMA journal_mode=WAL; foreign_keys=ON;` — Postgres enforces FKs natively.
- `SchemaInitializer` SQL → Postgres dialect:
  - `INTEGER PRIMARY KEY AUTOINCREMENT` → `BIGINT GENERATED ALWAYS AS IDENTITY`.
  - Money columns `REAL` → `numeric` (exact decimal; removes the `(double)` casts in repos).
  - Timestamps stay ISO-8601 round-trippable `text` — zero serialization-test churn.
  - `CREATE TABLE IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS` are valid in Postgres; kept.
  - `CHECK (id = 1)` singleton on `broker_session` is valid in Postgres; kept.
- Repository dialect fixes:
  - `INSERT ...; SELECT last_insert_rowid();` → `INSERT ... RETURNING <id>` (RunRepository,
    ExtractedFactRepository, BotDeliveryLogRepository — wherever an identity id is read back).
  - Money params: stop casting `decimal`→`double` on write and `double`→`decimal` on read;
    bind `decimal` directly against `numeric` (Dapper/Npgsql native). Net code removal.
  - Everything else (Dapper `@Param` placeholders, `Map`/`ToParams`, status token maps) is
    unchanged — `@name` is the same placeholder syntax in Npgsql.

### Hangfire

- `Hangfire.Storage.SQLite` → `Hangfire.PostgreSql`, pointed at the **same** database using a
  dedicated `hangfire` schema. Removes the second datastore, the `ConnectionStrings:Hangfire`
  key, the `hangfireDb` wiring in `Program.cs`, and the `/data` PVC.

### Tests

- `TestDatabase` (shared-cache in-memory SQLite) → a `Testcontainers.PostgreSql`-backed
  `IDbConnectionFactory`. One container per xUnit collection; schema initialized once; each
  `OpenAsync` returns a fresh `NpgsqlConnection`. All existing repository/integration tests run
  unchanged against real Postgres.
- Trade-off recorded: the hermetic repo suite now requires Docker locally. CI `ubuntu-latest`
  already has Docker. The CLAUDE.md testing-conventions line is updated accordingly.
- `SqliteConnectionFactoryTests` → `NpgsqlConnectionFactoryTests` (opens a connection, runs a
  round-trip). Coverage gate held at >95% line+branch.

### Aspire local dev (`src/Aspire/`)

- `WiseWizard.AppHost` — Aspire orchestrator. Provisions a Postgres container (with a named
  volume for persistence across runs), injects its connection string into the Host as
  `ConnectionStrings:WiseWizard`, and passes Anthropic/Telegram/IBKR settings from
  `dotnet user-secrets`. One command: `dotnet run --project src/Aspire/WiseWizard.AppHost`.
- `WiseWizard.ServiceDefaults` — shared OTel + health-check + HTTP resilience defaults;
  `builder.AddServiceDefaults()` called from `Program.cs`.
- Both projects are **local-dev only**: excluded from the Docker image (the runtime image
  still publishes just `WiseWizard.Host`) and excluded from CI coverage like `WiseWizard.Host`.

### Deploy wiring

- Domain connection string arrives from **Infisical** as `CONNECTIONSTRINGS__WISEWIZARD`
  (→ `ConnectionStrings:WiseWizard`). Documented in `k8s/README.md` alongside the existing secrets.
- `Dockerfile` — drop the `/data` `VOLUME`, the `mkdir/chown /data`, and the SQLite
  `ConnectionStrings__*` env defaults.
- `k8s/base/wisewizard` — remove `pvc.yaml` and the volume/volumeMount from `deployment.yaml`.
  Keep single-replica + `Recreate` (single Telegram long-poll + single nightly run).
- Packages (`Directory.Packages.props`): **add** `Npgsql`, `Hangfire.PostgreSql`,
  `Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`, the ServiceDefaults OTel/resilience
  packages, `Testcontainers.PostgreSql`. **Remove** `Microsoft.Data.Sqlite`,
  `Hangfire.Storage.SQLite`, and the two `SQLitePCLRaw.*` vuln pins (no longer reachable).

## SDLC artifacts produced

- **ADR-0007** — Adopt PostgreSQL for domain data + Hangfire (supersedes ADR-0003).
- **ADR-0008** — .NET Aspire for local development orchestration.
- **`docs/features/postgres-migration/`** — `_epic.md`, `tracker.md`, `data-model.md`
  (canonical Postgres schema), tasks **T01–T07**:
  - T01 — `IDbConnectionFactory` + `NpgsqlConnectionFactory` (replace SQLite factory).
  - T02 — `SchemaInitializer` Postgres dialect.
  - T03 — Repository dialect fixes (`RETURNING`, numeric/decimal).
  - T04 — Hangfire on Postgres (single DB, `hangfire` schema).
  - T05 — Testcontainers Postgres harness; port integration tests.
  - T06 — Host + deploy wiring (Program.cs, Dockerfile, k8s, packages, Infisical key).
  - T07 — Aspire `AppHost` + `ServiceDefaults`.
- **Doc updates:** ADR-0003 marked *Superseded by ADR-0007*; pointer notes added to the five
  feature `data-model.md` files and the two schema tasks (ibkr T02, nightly T01) that pin
  SQLite types; `sad.md` §7–§8 datastore text; `CLAUDE.md` stack + testing lines; the
  `wisewizard-*` memory files.

## Non-goals

- No EF Core, no ORM migration framework (schema stays idempotent create-if-not-exists SQL).
- No production overlay (staging only, as today).
- No change to domain models, Core abstractions' method signatures, or the pipeline logic.
- No WiseWizard-owned Postgres deployment on helios (Owner provisions the DB).

## Risks / mitigations

- **Docker required for repo tests** → documented; CI already provides it; Core/Bot suites
  remain network- and Docker-free.
- **Money precision change (`REAL`→`numeric`)** → strict improvement (exact decimal); existing
  value-equality tests confirm round-trips.
- **Hangfire schema on shared DB** → isolated via a dedicated `hangfire` schema, matching the
  Sentra pattern.
