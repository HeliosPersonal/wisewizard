---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T05
deps: [T01, T04]
estimate: M
branch: feat/ingest-sec-edgar-source
---

# T05 — SEC EDGAR Source (`ISecFilingsSource`)

## Goal

Implement `ISecFilingsSource` against the official free SEC EDGAR API: fetch recent filings for a Ticker, declaring the System's identity and staying within EDGAR's allowed rate.

## Scope

- `WiseWizard.Infrastructure/Sec/EdgarFilingsSource.cs`: resolve Ticker → CIK, fetch recent filings, map to `RawDocument` candidates (url, title, content, published_at).
- Every request carries the declared contact User-Agent (EDGAR fair-access); uses the T04 limiter at ≤ 10 req/s.
- Surfaces transport errors and rate-limit signals to the caller (handled by T09/T10) rather than throwing past the step.

## Links

- PRD: [PRD.md §5 AC-01, AC-03](../PRD.md).
- SAD: [sad.md §5](../../../00-overview/sad.md) — `Infrastructure/Sec`.
- Diagram: [seq-source-failure.md](../diagrams/seq-source-failure.md) (declared identity + backoff).

## DoD

- Contract test against a recorded EDGAR fixture: filings parsed to `RawDocument` candidates; declared User-Agent present on every request.
- Rate-limit signal from fixture triggers backoff (via T04), not a hard failure.
- Opt-in real-EDGAR integration test (excluded from CI default).
