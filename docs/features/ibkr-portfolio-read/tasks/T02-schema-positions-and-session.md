---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T02
estimate: S
deps: []
---

# T02 — `positions` + `broker_session` schema init

*Superseded by ADR-0007: schema is now PostgreSQL (numeric money, BIGINT GENERATED ALWAYS AS IDENTITY). See docs/features/postgres-migration/.*

## Scope

In `WiseWizard.Infrastructure/Persistence`: add the SQLite schema-init for the two tables this feature owns, exactly as specified in [data-model.md](../data-model.md).

- `positions` — PK `ticker`, columns per data-model; `REAL` money, `TEXT` ISO-8601 `as_of`.
- `broker_session` — singleton (`CHECK (id = 1)`), status + freshness columns.
- Idempotent `CREATE TABLE IF NOT EXISTS`; seed the single `broker_session` row (`id=1`, `status='unknown'`).

## Links

- data-model.md (both tables + constraints).
- PRD [§AC-06](../PRD.md), [§AC-07](../PRD.md) (snapshot / empty semantics rely on schema).
- ADR-0003 (SQLite domain DB, Dapper).

## Definition of Done

- Schema-init runs against a fresh SQLite file and creates both tables with the exact columns/constraints from data-model.md.
- `broker_session` seeded with exactly one row (`id=1`); `CHECK (id=1)` rejects a second row (integration test).
- Re-running init on an existing DB is a no-op (idempotent).
