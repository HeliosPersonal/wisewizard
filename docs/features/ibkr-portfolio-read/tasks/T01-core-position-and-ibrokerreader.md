---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T01
estimate: S
deps: []
---

# T01 — `Position` domain model + `IBrokerReader` abstraction (read-only)

## Scope

In `WiseWizard.Core`: add the `Position` model (`Models/`) and the `IBrokerReader` abstraction (`Abstractions/`). Zero external dependencies (sad.md §5).

- `Position`: Ticker, quantity, avg_cost, market_value, unrealized_pnl, currency, as_of — mirrors [data-model.md](../data-model.md) `positions`.
- `IBrokerReader`: read-only capability only — a method to read the current Positions and one to check session status. **No** order/write members.

## Links

- PRD [§AC-02](../PRD.md), [§AC-05](../PRD.md) (read-only surface), US-06.
- sad.md §5 (`WiseWizard.Core/Abstractions IBrokerReader`).
- ADR-0002 (read-only), data-model.md.

## Definition of Done

- `Position` + `IBrokerReader` compile in `WiseWizard.Core` with no external package refs.
- `IBrokerReader` exposes read + session-status members only; a unit/surface test asserts there is no order-placing member (supports [§AC-05](../PRD.md)).
- Types documented with XML summaries referencing the glossary terms verbatim.
