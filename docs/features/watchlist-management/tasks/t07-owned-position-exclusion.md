---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T07
deps: [T06]
estimate: S
---

# T07 — Owned-Position exclusion (cross-context, AC-08)

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Enforce that a symbol naming an owned Position cannot be added to the Watchlist, so the Universe is not padded with a redundant Watchlist copy of a held Ticker.

## Scope

- In `Add`, before persistence, read the current owned Tickers via the Positions abstraction owned by ibkr-portfolio-read (Core abstraction, read-only cross-context).
- If the normalized symbol is an owned Position, refuse the add with an `already-owned` outcome; persist nothing.
- No FK — this is a domain rule (data-model), not a DB constraint.

## Upstream (link, do not duplicate)

- [PRD §5 AC-08](../PRD.md)
- [data-model §ER diagram note, §Domain invariants "Not-owned"](../data-model.md)
- [seq-add-watch cross-context path](../diagrams/seq-add-watch.md)

## Dependency note

Reads the Positions abstraction defined by the **ibkr-portfolio-read** feature. Coordinate the interface name/shape with that feature; if unavailable at build time, gate behind a thin Core port and stub in tests.

## Definition of Done

- `Add` refuses an owned symbol with the `already-owned` outcome; nothing persisted.
- Unit test with a stub Positions read covers AC-08.
