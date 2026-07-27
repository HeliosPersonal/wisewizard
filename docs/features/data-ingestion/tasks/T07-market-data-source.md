---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T07
deps: [T01, T04]
estimate: M
branch: feat/ingest-market-data-source
---

# T07 — Market data Source (`IMarketDataSource`)

## Goal

Implement `IMarketDataSource` against a free-tier / unofficial market-and-fundamentals provider: fetch a latest metrics snapshot for a Ticker as one `RawDocument`.

## Scope

- `WiseWizard.Infrastructure/Market/MarketDataSource.cs`: fetch latest price + basic fundamentals, serialize into a snapshot `RawDocument` (title NULL, content = serialized metrics, published_at = snapshot time).
- Behind `IMarketDataSource` so the provider can be swapped if the unofficial source breaks (PRD §8 open question; sad.md §11 risk).
- Uses the T04 limiter at ≤ 1 req/s per host.

## Links

- PRD: [PRD.md §5 AC-01](../PRD.md), [§8 open question — market-data reliability](../PRD.md).
- SAD: [sad.md §5](../../../00-overview/sad.md) — `Infrastructure/Market`; [§11](../../../00-overview/sad.md) risk (unofficial source).

## DoD

- Contract test against a recorded snapshot fixture: metrics parsed to one `RawDocument`.
- Provider failure surfaces as a recoverable error (handled by T09/T10), not a crash.
- Opt-in real-provider integration test (excluded from CI default).
