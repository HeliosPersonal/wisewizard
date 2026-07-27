# T09 — Synthesis step (per-Ticker Verdict contract + fixtures)

**Owner:** Owner · **Est:** L · **Deps:** T08

## Scope
Implement the synthesis tier: group `extracted_facts` per Ticker, submit a low-volume Batch that produces, per Ticker, a STRUCTURED Verdict (Signal, one-line summary, full reasoning, cited `document_id`s). Include the previous Run's Verdict for the Ticker in the prompt so the model can express a delta (delta computation itself is T10). Persist the synthesis Batch id into `runs.batch_ids_json` on submit. Define the structured contract and save fixtures.

## Out of scope
Delta rules (T10); evidence guard + insert (T11); cost totals (T12).

## DoD
- Structured synthesis contract defined; response maps to candidate Verdicts with Signal + summary + reasoning + cited sources (component tests on fixtures, zero network).
- Only distilled facts (not raw documents) reach this tier (QG-1).
- Batch id persisted on submit.
- Realizes [seq-nightly-run](../diagrams/seq-nightly-run.md) synthesis block.

## Links
[PRD.md §5 AC-01](../PRD.md) · [ADR-0005](../../../00-overview/adr/0005-model-cascade-batch-api.md) · [test-plan.md](../test-plan.md)
