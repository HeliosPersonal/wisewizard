---
status: Draft
owner: "Owner"
updated_at: "2026-07-27"
stage: "13"
task_id: T01
estimate: S
deps: []
---

# T01 — `IDbConnectionFactory` + `NpgsqlConnectionFactory`

## Scope

In `WiseWizard.Infrastructure/Persistence`: replace the SQLite connection factory with a Postgres one that returns an `NpgsqlConnection`.

- Rename `ISqliteConnectionFactory` → `IDbConnectionFactory` (same single `OpenAsync` shape; returns an open `NpgsqlConnection` / `DbConnection`).
- Rename `SqliteConnectionFactory` → `NpgsqlConnectionFactory(string connectionString)`; open an `NpgsqlConnection` from the supplied connection string.
- **Drop** the SQLite `PRAGMA journal_mode=WAL;` and `PRAGMA foreign_keys=ON;` statements — Postgres enforces foreign keys natively and needs no journal PRAGMA.
- Update every usage across all repositories (`RunRepository`, `ExtractedFactRepository`, `BotDeliveryLogRepository`, `RawDocumentRepository`, `VerdictRepository`, `WatchlistRepository`, `PositionsRepository`, `BrokerSessionRepository`) and `StartupInitializer` to depend on `IDbConnectionFactory`.

## Links

- Design: [design doc](../../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — "Data access (Dapper kept)".
- [ADR-0007](../../../00-overview/adr/0007-postgresql-datastore.md), [ADR-0008](../../../00-overview/adr/0008-aspire-local-dev.md).
- Source: `src/WiseWizard.Infrastructure/Persistence/ISqliteConnectionFactory.cs`, `SqliteConnectionFactory.cs`; `src/WiseWizard.Host/HostedServices/StartupInitializer.cs`.

## Definition of Done

- `IDbConnectionFactory.OpenAsync` returns an open `NpgsqlConnection`; no SQLite PRAGMA statements remain in the factory.
- `NpgsqlConnectionFactory` is constructed from a connection string; a unit/integration test opens a connection and performs a round-trip (`SELECT 1`).
- All repositories and `StartupInitializer` reference `IDbConnectionFactory`; the solution compiles with no reference to the old SQLite factory types.
- Line + branch coverage for the new factory stays **> 95%**.
