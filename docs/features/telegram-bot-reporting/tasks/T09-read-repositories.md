---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T09
deps: []
est: M
---

# T09 — Read repositories (Run / Verdict / Position) via Dapper

## Goal

Provide the read-only Dapper repositories the bot needs over the shared domain SQLite database: resolve the latest completed Run, read a Run's Verdicts, read one Ticker's Verdict, and read the current Positions. These read tables **owned by other features** — this task defines only the read queries and Core interfaces the bot depends on.

## Scope

- `IRunReadRepository`: latest completed Run (status = completed, newest `finished_at`).
- `IVerdictReadRepository`: all Verdicts for a Run id (ordered by Signal then Ticker); single Verdict by (`run_id`, `ticker`) with reasoning + cited Sources + "what changed".
- `IPositionReadRepository`: all current Positions with `as_of`.
- Dapper queries relying on upstream-owned indexes (see data-model read patterns); WAL-mode read connections (ADR-0003).

## Links

- Data model: [data-model.md](../data-model.md) — read access patterns + indexes owned upstream.
- ADR: [0003 SQLite persistence](../../../00-overview/adr/0003-sqlite-persistence.md).
- SAD: [sad.md](../../../00-overview/sad.md) §5 (Dapper repositories), §8 (persistence).

## Out of scope

- Defining/migrating `runs`, `verdicts`, `positions` (owned by nightly-research-pipeline / ibkr-portfolio-read); any write path.

## DoD

- Integration tests over a seeded SQLite DB: latest-completed-Run query ignores in-progress/failed Runs; Verdict-by-Run and Verdict-by-(Run,Ticker) return expected rows; missing Ticker returns none; Positions read returns rows with `as_of`.
- Read-only: no repository exposes a mutation.
- Queries hit the expected indexes (verified against the seeded schema).
