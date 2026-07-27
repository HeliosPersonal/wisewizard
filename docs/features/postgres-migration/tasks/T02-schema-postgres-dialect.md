---
status: Draft
owner: "Owner"
updated_at: "2026-07-27"
stage: "13"
task_id: T02
estimate: S
deps: [T01]
---

# T02 — `SchemaInitializer` Postgres dialect

## Scope

Port `SchemaInitializer` DDL from the SQLite dialect to the Postgres dialect. Table/column/index names and the overall shape stay identical — only the type keywords and identity syntax change.

- Identity columns: `INTEGER PRIMARY KEY AUTOINCREMENT` → `BIGINT GENERATED ALWAYS AS IDENTITY` (`runs.run_id`, `extracted_facts` id, `bot_delivery_log` id, and any other auto-id PK).
- Money columns: `REAL` → `numeric` (exact decimal).
- Timestamps: **keep** as ISO-8601 round-trippable `text` — no serialization-test churn.
- `CREATE TABLE IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS` are valid in Postgres — kept.
- The `broker_session` singleton `CHECK (id = 1)` is valid in Postgres — kept, along with the seeded `id=1` row.
- The whole init must remain **idempotent** (re-running against an existing DB is a no-op).

## Links

- Design: [design doc](../../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — "SchemaInitializer SQL → Postgres dialect".
- [ADR-0007](../../../00-overview/adr/0007-postgresql-datastore.md).
- Source: `src/WiseWizard.Infrastructure/Persistence/SchemaInitializer.cs`.
- Pinned-type notes: ibkr `T02-schema-positions-and-session.md`, nightly `T01-migration-runs-facts-verdicts.md`.

## Definition of Done

- Schema-init runs against a fresh Postgres database and creates every table with `BIGINT GENERATED ALWAYS AS IDENTITY` ids, `numeric` money columns, and `text` ISO-8601 timestamps.
- Indexes are created with `CREATE INDEX IF NOT EXISTS`; all existing indexes are present.
- `broker_session` is seeded with exactly one `id=1` row; `CHECK (id = 1)` rejects a second row (integration test).
- Re-running init on an existing DB is a no-op (idempotent; integration test asserts no error and no duplicate rows).
- Coverage for `SchemaInitializer` stays **> 95%** (line + branch).
