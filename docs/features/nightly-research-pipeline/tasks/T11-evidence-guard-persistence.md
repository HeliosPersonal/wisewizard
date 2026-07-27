# T11 — Evidence-invariant guard + Verdict persistence

**Owner:** Owner · **Est:** M · **Deps:** T09

## Scope
Enforce the domain invariant before persisting: a Verdict must cite ≥1 Raw document (`sources_json` non-empty). A candidate conclusion with no citable Extracted fact is invalid — block it, and instead record for that Ticker that there was no citable evidence this Run (also covers a Ticker with no fresh documents). Persist valid Verdicts via T04's idempotent insert.

## Out of scope
Delta (T10 — its output is written here); repository internals (T04).

## DoD
- Evidence-less candidate blocked and recorded as no-evidence, never persisted as a Verdict (AC-05) — test.
- Ticker with no fresh documents → recorded as no-evidence, Run still completes for the rest (AC-09) — test.
- Every persisted Verdict has ≥1 source (100% compliance KPI) — asserted.
- Realizes [seq-nightly-run](../diagrams/seq-nightly-run.md) persist/invariant block.

## Links
[PRD.md §5 AC-05, AC-09](../PRD.md) · [CONTEXT.md](../../../00-overview/CONTEXT.md) invariant (every Verdict cites ≥1 Source)
