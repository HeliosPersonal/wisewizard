---
status: Draft
owner: "Owner"
updated_at: "2026-07-27"
stage: "13"
task_id: T05
estimate: M
deps: [T01, T02, T03]
---

# T05 — Testcontainers Postgres harness; port integration tests

## Scope

Replace the shared-cache in-memory-SQLite `TestDatabase` with a `Testcontainers.PostgreSql`-backed `IDbConnectionFactory` so the hermetic repository/integration suite runs against real Postgres.

- Start **one** Postgres container **per xUnit collection** (shared fixture); dispose it when the collection completes.
- Initialize the schema **once** against the container (via `SchemaInitializer`); each `OpenAsync` returns a **fresh** `NpgsqlConnection` to that database.
- Expose the container-backed factory as `IDbConnectionFactory` so all existing repository/integration tests run **unchanged**.
- Rename `SqliteConnectionFactoryTests` → `NpgsqlConnectionFactoryTests` (opens a connection, performs a round-trip).
- Note in the test project / CLAUDE.md testing-conventions line: the hermetic repo suite now **requires Docker locally**; CI `ubuntu-latest` already provides Docker. Core/Bot suites remain network- and Docker-free.

## Links

- Design: [design doc](../../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — "Tests".
- [ADR-0007](../../../00-overview/adr/0007-postgresql-datastore.md).
- Source: `tests/WiseWizard.Infrastructure.Tests/TestDatabase.cs`, `tests/WiseWizard.Infrastructure.Tests/SqliteConnectionFactoryTests.cs`.

## Definition of Done

- A `Testcontainers.PostgreSql`-backed `IDbConnectionFactory` starts one container per xUnit collection and initializes the schema once; each `OpenAsync` yields a fresh `NpgsqlConnection`.
- All existing repository/integration tests pass against real Postgres with no test-body changes beyond the factory swap.
- `NpgsqlConnectionFactoryTests` exists (round-trip) and the old `SqliteConnectionFactoryTests` is gone.
- The Docker-required note is recorded in the testing conventions.
- Overall line + branch coverage stays **> 95%**.
