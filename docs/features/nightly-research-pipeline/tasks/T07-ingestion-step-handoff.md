# T07 — Ingestion-step handoff

**Owner:** Owner · **Est:** S · **Deps:** T04

## Scope
Add the pipeline step that triggers the data-ingestion feature for the current `run_id`, waits for it to report completion, then reads the `raw_documents` collected for that Run (read-only; `raw_documents` owned by data-ingestion). This is the input pool for the cheap tier and the citation pool for Verdicts.

## Out of scope
Fetching from Sources / dedup (owned by data-ingestion); extraction (T08).

## DoD
- Step invokes ingestion keyed by `run_id`, then loads `raw_documents` for the Run.
- A Ticker with zero fresh documents is handled downstream as "no fresh evidence" (AC-09) — this step just surfaces the empty set.
- Realizes [seq-nightly-run](../diagrams/seq-nightly-run.md) steps 4-7.

## Links
[PRD.md §3](../PRD.md) (handoff) · data-ingestion feature (`raw_documents`)
