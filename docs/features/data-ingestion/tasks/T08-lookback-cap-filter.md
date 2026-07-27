---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T08
deps: [T01]
estimate: S
branch: feat/ingest-lookback-cap-filter
---

# T08 — Lookback + per-Source cap filtering

## Goal

Filter candidate documents to those within the lookback window and cap the count per Source per Ticker before persistence.

## Scope

- `WiseWizard.Core` filter: keep documents with `published_at` within the last 14 days; drop older, future-dated, or unparseable-date documents.
- Cap kept documents to ≤ 15 per Source per Ticker per Run, preferring the newest.
- Bounds are configuration-driven (`IOptions<T>`) — feeds PRD §8 open question on lookback/cap tuning.

## Links

- PRD: [PRD.md §5 AC-06](../PRD.md), [§6 NFR](../PRD.md) (lookback, max docs).
- Diagram: [seq-ingest-ticker.md](../diagrams/seq-ingest-ticker.md) (bounds note).

## DoD

- Unit test: documents older than lookback dropped; boundary (exactly at window edge) has defined behavior.
- Unit test: more than the cap → only the newest cap kept.
- Unit test: future-dated / unparseable-date document excluded and logged.
