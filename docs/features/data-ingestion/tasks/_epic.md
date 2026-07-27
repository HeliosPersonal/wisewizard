---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "13"
ticket: "N/A — personal project"
---

# Epic — data-ingestion

## Summary

Collect Raw documents for every Ticker in the current Universe from the three fixed free Sources (SEC EDGAR filings, news RSS, market/fundamental data), dedup by content hash within a Run, persist to `raw_documents`, and wire ingestion as the first step of the Hangfire nightly chain. Each Source sits behind a Core interface so adding a Source is a new implementation (Open/Closed).

This feature produces `raw_documents` keyed to `run_id` and hands them off to nightly-research-pipeline (which owns the `runs` table and the overall continuation chain).

## Upstream artefacts (LINK, do not duplicate)

- PRD: [PRD.md](../PRD.md) — user stories US-01..US-07, acceptance criteria AC-01..AC-08, NFR §6.
- Data model: [data-model.md](../data-model.md) — owns `raw_documents`, dedup index `ux_raw_documents_run_hash`, retention.
- Diagrams: [seq-ingest-ticker.md](../diagrams/seq-ingest-ticker.md), [seq-source-failure.md](../diagrams/seq-source-failure.md).
- Test plan: [test-plan.md](../test-plan.md).
- SAD: [sad.md](../../../00-overview/sad.md) §5 (module boundaries, Source interfaces), §6 (runtime flow 1), §8 (dedup by content hash).
- ADRs: [ADR-0003](../../../00-overview/adr/0003-sqlite-persistence.md) (SQLite + Dapper), [ADR-0004](../../../00-overview/adr/0004-hangfire-jobs.md) (Hangfire chain).
- Context: [CONTEXT.md](../../../00-overview/CONTEXT.md) §Glossary (Raw document, Source, Ticker, Universe, Run).

## Scope boundary

- IN: Source interfaces + implementations, dedup, `raw_documents` persistence, ingest step, retention cleanup, rate limiting.
- OUT (owned elsewhere): the `runs` table and overall Hangfire continuation chain (nightly-research-pipeline); the Universe read (ibkr-portfolio-read + watchlist-management); fact extraction / analysis (nightly-research-pipeline).

## Task list

| ID | Task | Layer | Est |
|---|---|---|---|
| T01 | Core models + Source abstractions | domain | S |
| T02 | `raw_documents` migration + Dapper repository | infra/db | M |
| T03 | Content-hash dedup logic | domain | S |
| T04 | Per-host polite rate limiter | infra | S |
| T05 | SEC EDGAR Source (`ISecFilingsSource`) | infra/source | M |
| T06 | News RSS Source (`INewsSource`) | infra/source | M |
| T07 | Market data Source (`IMarketDataSource`) | infra/source | M |
| T08 | Lookback + per-Source cap filtering | domain | S |
| T09 | Collection-gap recording | app | S |
| T10 | IngestStep orchestration | app | M |
| T11 | Hangfire wiring (ingest step + retention job) | app/host | S |
| T12 | Retention cleanup job | infra/app | S |
| T13 | Test suite + fixtures + load harness | tests | M |

## Dependency graph

```
T01 ──┬─> T02 ──┬────────────────────> T10 ─> T11
      ├─> T03 ──┤                        │
      ├─> T04 ──┼─> T05 ──┐              │
      │         ├─> T06 ──┼─> (Sources) ─┤
      │         └─> T07 ──┘              │
      ├─> T08 ─────────────────────────> T10
      └─> T09 ─────────────────────────> T10
                              T02 ─> T12 ─> T11
      (all) ─────────────────────────────────> T13
```

Parallel branches after T01: T02, T03, T04, T08, T09 can proceed independently. T05/T06/T07 each depend on T01 (+ T04 for rate limiting) and can be built in parallel — one branch per Source. T10 integrates everything; T11 wires it; T13 tests across all.

## Estimate

13 tasks, ~9-10 person-days total. Each task ≤ 1 day and a reviewable PR (≤ 500 LOC).

## Owner

Owner (single-developer project).
