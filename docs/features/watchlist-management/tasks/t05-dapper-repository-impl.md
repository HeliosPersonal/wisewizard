---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T05
deps: [T03, T04]
estimate: M
---

# T05 — Dapper/SQLite repository implementation

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Implement `IWatchlistRepository` over SQLite using Dapper in `WiseWizard.Infrastructure/Persistence`.

## Scope

- Implement `GetAllAsync` (ordered by `added_at`), `ExistsAsync`, `AddAsync`, `RemoveAsync`, `CountAsync`.
- Map `WatchlistEntry` ↔ row; store `added_at` as ISO-8601 UTC text.
- Rely on the PK for the dedup backstop; `RemoveAsync` returns rows-affected > 0.

## Upstream (link, do not duplicate)

- [data-model §Entities, §Access patterns, §Model & abstraction](../data-model.md)
- [ADR-0003 SQLite persistence](../../00-overview/adr/0003-sqlite-persistence.md)
- [seq-add-watch](../diagrams/seq-add-watch.md) · [seq-remove-watch](../diagrams/seq-remove-watch.md)

## Definition of Done

- Implementation satisfies every `IWatchlistRepository` method.
- Integration tests (real SQLite temp file) cover add → exists → list → count → remove and PK-dedup backstop (test-plan integration level).
- `Infrastructure` depends on `Core` only; `Pipeline`/`Bot` never reference this concrete type.
