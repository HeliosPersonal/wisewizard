---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T10
deps: [T02, T03, T05, T06, T07, T08, T09]
estimate: M
branch: feat/ingest-step-orchestration
---

# T10 — IngestStep orchestration

## Goal

Compose the pieces into `IngestStep`: for each Universe Ticker, collect from all Sources, filter, dedup, persist, and record gaps — isolating Source failures.

## Scope

- `WiseWizard.Pipeline/Steps/IngestStep.cs`: read the Universe (Portfolio ∪ Watchlist) and iterate only those Tickers (AC-05); for each, call the three Sources; apply T08 filter, T03 dedup, T02 persist; on Source failure record a T09 gap and continue.
- Empty result for a Ticker is a success, not a gap (AC-07).
- Step-level try/catch per Source keeps one Source/Ticker failure from failing the step (sad.md §8 error handling).

## Links

- PRD: [PRD.md §5 AC-01, AC-02, AC-05, AC-07](../PRD.md).
- Diagrams: [seq-ingest-ticker.md](../diagrams/seq-ingest-ticker.md), [seq-source-failure.md](../diagrams/seq-source-failure.md).
- SAD: [sad.md §6 flow 1](../../../00-overview/sad.md), §8 error handling.

## DoD

- Integration test: happy path persists deduped documents from each Source for each Universe Ticker.
- Integration test: one failing Source records a gap; other Sources/Tickers still persist (isolation).
- Unit test: out-of-Universe Ticker never fetched (AC-05).
