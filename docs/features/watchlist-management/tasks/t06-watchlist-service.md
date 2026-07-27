---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T06
deps: [T02, T04]
estimate: M
---

# T06 — Watchlist domain service

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Implement the Watchlist domain service that orchestrates add / remove / list and enforces every domain invariant, returning outcomes the transport layer (telegram-bot-reporting) turns into Owner-facing messages.

## Scope

- `Add(symbol, note)`: normalize + validate (T02) → check duplicate via `ExistsAsync` → check size cap via `CountAsync` (≤ 100) → check note length (≤ 280) → `AddAsync`. Returns a discriminated outcome: added / already-watched / malformed / size-cap-exceeded / note-too-long. (Owned-Position check added in T07.)
- `Remove(symbol)`: normalize → `RemoveAsync` → returns removed / not-watched.
- `List()`: returns all entries with notes (ordered).
- No Telegram/transport code and no message-string formatting here — outcomes only.

## Upstream (link, do not duplicate)

- [PRD §5 AC-01, AC-02, AC-03, AC-04, AC-05, AC-07; §6 NFR size/note caps](../PRD.md)
- [data-model §Domain invariants](../data-model.md)
- [seq-add-watch](../diagrams/seq-add-watch.md) · [seq-remove-watch](../diagrams/seq-remove-watch.md)

## Definition of Done

- Service enforces normalization, format, dedup (idempotent no-op), size cap, and note-length invariants.
- Unit tests against a mocked `IWatchlistRepository` cover AC-01/02/03/04/05/07 outcomes and the size-cap and note-length edges (test-plan).
- Depends only on Core abstractions.
