---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T10
deps: []
est: S
---

# T10 — `bot_delivery_log` table + idempotent delivery store

# Goal

Add the one small operational table this feature owns, `bot_delivery_log`, and a repository that makes alert delivery idempotent across process restarts: check-by-`event_key` before send, record on success. Holds no financial values — only event/message identifiers and timestamps.

## Scope

- Schema/migration for `bot_delivery_log` (id, `event_key` UNIQUE, `event_kind`, nullable `run_id` soft reference, `delivered_at`, `created_at`) with the `ux_bot_delivery_event` unique index.
- `IDeliveryLog` repository: `HasDelivered(eventKey)`, `RecordDelivered(eventKey, kind, runId?)`.
- WAL-mode connection consistent with ADR-0003; no cross-module FK to `runs`.

## Links

- Data model: [data-model.md](../data-model.md) — `bot_delivery_log` (the only table this feature owns) + `ux_bot_delivery_event`.
- ADR: [0003 SQLite persistence](../../../00-overview/adr/0003-sqlite-persistence.md).
- Consumed by: [T07 alert publisher](./T07-alert-publisher.md).

## Out of scope

- The alerting logic (T07); reading domain tables (T09).

## DoD

- Migration creates the table + unique index; idempotent to re-run.
- Unit/integration test: `RecordDelivered` then `HasDelivered` returns true; a duplicate `event_key` insert is a no-op / handled without error.
- Verify no financial columns exist on the table (privacy check from PRD §6.1).
