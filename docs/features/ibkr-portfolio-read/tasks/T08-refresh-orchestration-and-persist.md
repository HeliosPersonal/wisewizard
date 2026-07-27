---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T08
estimate: L
deps: [T07, T03]
---

# T08 — Refresh orchestration + snapshot persistence (happy + fail-retain)

## Scope

In `WiseWizard.Infrastructure/Ibkr`: add the Portfolio-refresh path to `IbkrSessionService` (invokable ahead of a Run and on a schedule).

- On refresh: read Positions via `IBrokerReader`, then `PositionsRepository.ReplaceSnapshot(positions, asOf)` and `SessionStateRepository.RecordRefresh(ok=true, snapshotAt=asOf)`.
- On failed read (unreachable/malformed): do **not** touch `positions`; `RecordRefresh(ok=false)` — last known-good retained (PRD §AC-03).
- Empty read → empty-but-current snapshot (PRD §AC-07).
- Time the refresh span and log with `run_id` for the latency NFR (PRD §6).

## Links

- PRD [§AC-01](../PRD.md), [§AC-03](../PRD.md), [§AC-06](../PRD.md), [§AC-07](../PRD.md), §6 (read latency).
- [seq-read-positions.md](../diagrams/seq-read-positions.md) (all three variants).
- data-model.md (snapshot write pattern).

## Definition of Done

- Successful refresh persists a wholesale-replaced snapshot and updates `last_snapshot_at` ([§AC-01](../PRD.md), [§AC-06](../PRD.md)) — integration test.
- Failed refresh retains last known-good and records the failed attempt, `last_snapshot_at` unchanged ([§AC-03](../PRD.md)) — integration test.
- Empty refresh yields an empty current snapshot distinct from a failure ([§AC-07](../PRD.md)) — integration test.
- Refresh span logged; local-stub latency well under the 5 s p95 budget (§6).
