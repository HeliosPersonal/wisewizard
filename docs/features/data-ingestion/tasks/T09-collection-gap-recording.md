---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T09
deps: [T01]
estimate: S
branch: feat/ingest-collection-gap
---

# T09 — Collection-gap recording

## Goal

Record when a Source is skipped for a Ticker (unreachable or rate-limited) so the gap is auditable, without aborting the Run.

## Scope

- Represent a gap as `(run_id, ticker, source, reason)` and persist/log it (structured log with `run_id`; a lightweight `ingest_gaps` record or log-only, decided in PR).
- Reasons: `unreachable`, `rate_limited`, `parse_error`.
- Distinct from AC-07 "zero fresh docs" — an empty-but-successful collection is NOT a gap.

## Links

- PRD: [PRD.md §5 AC-02, AC-07](../PRD.md), [§7 KPI — Source failure rate](../PRD.md).
- Diagram: [seq-source-failure.md](../diagrams/seq-source-failure.md).

## DoD

- Unit test: a failed Source produces exactly one gap record for `(run_id, ticker, source)` with the correct reason.
- Unit test: a successful-but-empty collection produces no gap.
