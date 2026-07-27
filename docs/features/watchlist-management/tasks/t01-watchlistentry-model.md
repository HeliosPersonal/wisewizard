---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T01
deps: []
estimate: S
---

# T01 — WatchlistEntry domain model

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Add the `WatchlistEntry` domain model to `WiseWizard.Core/Models`, with zero external dependencies (sad.md §5).

## Scope

- Immutable `WatchlistEntry` with `Ticker` (normalized symbol), `AddedAt` (UTC instant), `Note` (optional).
- No validation logic here — that is T02; this is the data shape only.

## Upstream (link, do not duplicate)

- [data-model §Model & abstraction](../data-model.md)
- [PRD §4 US-01, US-04](../PRD.md)

## Definition of Done

- `WatchlistEntry` compiles in `WiseWizard.Core` with no external package references.
- Fields match the data-model exactly (Ticker, AddedAt, Note).
- A construction unit test asserts the fields round-trip.
