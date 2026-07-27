---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T10
estimate: S
deps: [T01, T03]
---

# T10 — Expose current Portfolio Tickers to the Universe

## Scope

Provide a read path so the Portfolio part of the Universe can be assembled by the pipeline: expose the current Positions' Tickers from the persisted snapshot through a Core abstraction (e.g. `IPortfolioReader.GetCurrentPortfolio()` / `GetPortfolioTickers()`), backed by `PositionsRepository`.

- Cross-context boundary: this feature only *exposes* the Portfolio Tickers; deduping with the Watchlist into the Universe belongs to watchlist-management / pipeline (PRD §3 non-goal).
- Consumers depend on the Core abstraction, not on `Infrastructure` (sad.md §5).

## Links

- PRD [§AC-08](../PRD.md) (Portfolio feeds Universe), US-01.
- CONTEXT (Universe = Portfolio ∪ Watchlist).
- data-model.md.

## Definition of Done

- A current-Portfolio read returns exactly the Tickers of the persisted snapshot (unit test with a seeded snapshot, [§AC-08](../PRD.md)).
- An empty current Portfolio returns zero Tickers (not an error).
- No Watchlist/Universe dedup logic added here (stays a non-goal).
