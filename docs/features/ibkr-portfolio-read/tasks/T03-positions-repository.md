---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T03
estimate: M
deps: [T02]
---

# T03 — `PositionsRepository` (wholesale snapshot replace)

## Scope

In `WiseWizard.Infrastructure/Persistence`: Dapper repository for the `positions` snapshot.

- `ReplaceSnapshot(positions, asOf)` — one transaction: `DELETE FROM positions;` then insert new rows with a single shared `as_of`. Empty list → delete only (empty-but-current).
- `GetCurrentPositions()` — read all rows.
- `GetByTicker(ticker)` — PK lookup for drill-down.

## Links

- data-model.md (`positions`, write pattern).
- PRD [§AC-01](../PRD.md), [§AC-06](../PRD.md), [§AC-07](../PRD.md).
- [seq-read-positions.md](../diagrams/seq-read-positions.md) (transactional replace).

## Definition of Done

- `ReplaceSnapshot` fully replaces prior rows in one transaction — no leftover/duplicate rows after replace (integration test, [§AC-06](../PRD.md)).
- Empty list yields zero rows but is a successful replace (integration test, [§AC-07](../PRD.md)).
- All rows of one snapshot share the same `as_of` ([§AC-01](../PRD.md)).
- Repository depends only on the DB connection abstraction, not on `Ibkr` types.
