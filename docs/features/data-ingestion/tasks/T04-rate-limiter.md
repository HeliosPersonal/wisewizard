---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T04
deps: [T01]
estimate: S
branch: feat/ingest-rate-limiter
---

# T04 — Per-host polite rate limiter

## Goal

Provide a reusable per-host request pacer so each Source stays within its allowed access rate and backs off on rate-limit signals (polite access).

## Scope

- `WiseWizard.Infrastructure` per-host limiter: SEC EDGAR ≤ 10 req/s; RSS / market-data ≤ 1 req/s per host, single concurrent request per host (PRD §6).
- Backoff hook: on a rate-limit signal, widen the interval / wait before the next request.
- Injected into every Source client (T05–T07).

## Links

- PRD: [PRD.md §5 AC-03](../PRD.md), [§6 NFR](../PRD.md) (polite rate rows).
- Diagram: [seq-source-failure.md](../diagrams/seq-source-failure.md) (rate-limit path).

## DoD

- Unit test measures inter-request spacing meets each configured rate.
- Unit test: a rate-limit signal triggers a wider interval before the next request.
