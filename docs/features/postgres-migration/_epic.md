---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-27"
feature_size: M
stage: "13"
ticket: "N/A — personal project"
---

# Epic — postgres-migration

Move WiseWizard off SQLite onto PostgreSQL for both domain data and Hangfire storage, and add a .NET Aspire local-dev orchestration (AppHost + ServiceDefaults) that provisions Postgres in a container and supplies secrets from `dotnet user-secrets`. The repositories already sit behind Core interfaces, so the driver swap is mechanical and preserves the existing tests and the >95% coverage gate.

## Upstream artefacts (tasks LINK, do not duplicate)

- Design: [2026-07-27-postgres-migration-and-aspire-design.md](../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — source of truth for every decision below.
- ADRs: [0007](../../00-overview/adr/0007-postgresql-datastore.md) (adopt PostgreSQL for domain + Hangfire, supersedes 0003), [0008](../../00-overview/adr/0008-aspire-local-dev.md) (.NET Aspire for local development).
- Superseded: [ADR-0003](../../00-overview/adr/0003-sqlite-persistence.md) (SQLite domain DB) is superseded by ADR-0007.

## Goals

- **Keep Dapper; swap the driver to Npgsql.** No EF Core rewrite. `ISqliteConnectionFactory` → `IDbConnectionFactory` returning `NpgsqlConnection`; repository SQL ported to the Postgres dialect.
- **Hangfire on the same Postgres**, isolated in a dedicated `hangfire` schema. Removes the second datastore, the `ConnectionStrings:Hangfire` key, and the `hangfireDb` wiring.
- **Testcontainers for integration tests.** Replace the shared-cache in-memory SQLite `TestDatabase` with a `Testcontainers.PostgreSql`-backed `IDbConnectionFactory` (one container per xUnit collection).
- **Aspire AppHost + ServiceDefaults for local dev.** One command provisions Postgres and injects `ConnectionStrings:WiseWizard`; Anthropic/Telegram/IBKR come from `dotnet user-secrets`.
- **Drop the PVC.** No WiseWizard-owned Postgres deployment on helios; the Owner provisions the database and scoped user, WiseWizard receives only a connection string via Infisical.

## Hard rules (must not be broken by any task)

- Domain models, Core abstractions' method signatures, and pipeline logic are unchanged — this is a datastore swap only.
- `Pipeline`/`Bot` depend only on `Core` abstractions, never on concrete `Infrastructure` types.
- Test coverage stays **> 95%** (line + branch); Aspire projects are excluded from CI coverage like `WiseWizard.Host`.
- Schema stays idempotent create-if-not-exists SQL — no EF Core, no ORM migration framework.

## Task list (7 atomic tasks, each ≤1 day → one PR)

| # | Task | Layer |
|---|---|---|
| T01 | `IDbConnectionFactory` + `NpgsqlConnectionFactory` (replace SQLite factory) | Persistence |
| T02 | `SchemaInitializer` Postgres dialect | Persistence |
| T03 | Repository dialect fixes (`RETURNING`, `ON CONFLICT`, numeric/decimal) | Persistence |
| T04 | Hangfire on Postgres (single DB, `hangfire` schema) | Host |
| T05 | Testcontainers Postgres harness; port integration tests | Tests |
| T06 | Host + deploy wiring (Program.cs, Dockerfile, k8s, packages, Infisical key) | Host + Deploy |
| T07 | Aspire `AppHost` + `ServiceDefaults` | Aspire |

## Dependency graph

```
T01 ──┬─► T02 ─► T03 ──┬─► T05 (uses T01,T02,T03)
      │         │      │
      │         └─► T04 ─► T06 (uses T03,T04)
      └─► T07
```

- Parallel branches: T04 (Hangfire) parallels T03; T07 (Aspire) can start once T01 lands.
- Critical path: T01 → T02 → T03 → T06.

## Estimate (S = ~2h, M = ~half-day, L = ~1 day)

T01 S · T02 S · T03 M · T04 S · T05 M · T06 M · T07 M — total ≈ 3-4 person-days.

## Owners

All tasks: `Owner` (single-developer project).
