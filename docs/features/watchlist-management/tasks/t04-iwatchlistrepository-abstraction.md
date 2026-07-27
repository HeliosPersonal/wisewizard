---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T04
deps: [T01]
estimate: S
---

# T04 — IWatchlistRepository abstraction

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Define `IWatchlistRepository` in `WiseWizard.Core/Abstractions` so the service and pipeline depend only on the abstraction (sad.md §5 dependency rule).

## Scope

- Methods per data-model: `GetAllAsync()`, `ExistsAsync(ticker)`, `AddAsync(entry)`, `RemoveAsync(ticker)` (returns whether a row was removed), `CountAsync()`.
- All async, all in terms of `WatchlistEntry` / normalized ticker strings.

## Upstream (link, do not duplicate)

- [data-model §Model & abstraction](../data-model.md)
- [sad.md §5 dependency direction](../../00-overview/sad.md)

## Definition of Done

- Interface compiles in `WiseWizard.Core` with no external deps.
- Signatures match the data-model.
- No implementation in this task (T05 supplies it).
