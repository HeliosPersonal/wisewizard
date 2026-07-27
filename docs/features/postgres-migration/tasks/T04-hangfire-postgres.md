---
status: Draft
owner: "Owner"
updated_at: "2026-07-27"
stage: "13"
task_id: T04
estimate: S
deps: [T02]
---

# T04 — Hangfire on Postgres (`hangfire` schema)

## Scope

Move Hangfire storage from a separate SQLite file onto the **same** Postgres database that holds domain data, isolated in a dedicated `hangfire` schema.

- Replace `Hangfire.Storage.SQLite` with `Hangfire.PostgreSql` in the Hangfire configuration.
- Point Hangfire at the same connection string as the domain DB (`ConnectionStrings:WiseWizard`), configured to use the dedicated `hangfire` schema (matching the Sentra pattern).
- Remove the `ConnectionStrings:Hangfire` key and the `hangfireDb` wiring from `Program.cs` — there is no longer a second datastore.

## Links

- Design: [design doc](../../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — "Hangfire".
- [ADR-0007](../../../00-overview/adr/0007-postgresql-datastore.md), [ADR-0004](../../../00-overview/adr/0004-hangfire-jobs.md) (Hangfire scheduling).
- Source: `src/WiseWizard.Host/Program.cs`.

## Definition of Done

- Hangfire uses `Hangfire.PostgreSql` against the same Postgres DB with the `hangfire` schema; its tables are created under that schema, not in the domain schema.
- `ConnectionStrings:Hangfire` and the `hangfireDb` wiring are gone from `Program.cs`; no `Hangfire.Storage.SQLite` reference remains.
- The nightly job still schedules and runs (verified via the existing Host smoke path / job-registration test).
- No SQLite Hangfire file is created at runtime.
