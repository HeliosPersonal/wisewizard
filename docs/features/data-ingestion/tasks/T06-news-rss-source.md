---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T06
deps: [T01, T04]
estimate: M
branch: feat/ingest-news-rss-source
---

# T06 — News RSS Source (`INewsSource`)

## Goal

Implement `INewsSource` against a curated set of free news RSS feeds: fetch recent articles relevant to a Ticker and map them to `RawDocument` candidates.

## Scope

- `WiseWizard.Infrastructure/News/RssNewsSource.cs`: query the configured feed set per Ticker symbol, parse RSS/Atom, map to `RawDocument` candidates.
- Feed list is configuration-driven (`IOptions<T>`) so it can be tuned without code changes — feeds the PRD §8 open question on best feeds.
- Uses the T04 limiter at ≤ 1 req/s per host.

## Links

- PRD: [PRD.md §5 AC-01](../PRD.md), [§8 open question — RSS feeds](../PRD.md).
- SAD: [sad.md §5](../../../00-overview/sad.md) — `Infrastructure/News`.

## DoD

- Contract test against a recorded RSS fixture: articles parsed to `RawDocument` candidates with url/title/published_at.
- Feed list read from configuration; empty/malformed feed handled gracefully (logged, skipped).
- Opt-in real-feed integration test (excluded from CI default).
