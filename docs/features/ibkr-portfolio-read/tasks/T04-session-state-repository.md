---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T04
estimate: S
deps: [T02]
---

# T04 — `SessionStateRepository` (singleton session state)

## Scope

In `WiseWizard.Infrastructure/Persistence`: Dapper repository over the singleton `broker_session` row.

- `Get()` — read the single row.
- `MarkLive(now)` — `status='live'`, `last_keepalive_at=now`, clear `reauth_alerted_at`.
- `MarkLapsed()` — `status='lapsed'`.
- `RecordAlertSent(now)` — set `reauth_alerted_at`.
- `RecordRefresh(attemptAt, ok, snapshotAt?)` — update `last_refresh_attempt_at`, `last_refresh_ok`, and `last_snapshot_at` only on success.

## Links

- data-model.md (`broker_session`).
- PRD [§AC-03](../PRD.md), [§AC-04](../PRD.md), [§AC-09](../PRD.md).
- [seq-session-reauth.md](../diagrams/seq-session-reauth.md).

## Definition of Done

- Each method updates only its intended columns (integration tests on a real SQLite row).
- `RecordRefresh(ok=false)` leaves `last_snapshot_at` unchanged ([§AC-03](../PRD.md)).
- `MarkLive` clears `reauth_alerted_at` (recovery path, [§AC-04](../PRD.md)).
- Operates strictly on the `id=1` singleton.
