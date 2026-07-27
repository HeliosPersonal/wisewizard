# T08 — Cheap-tier extraction step (prompt/response contract + fixtures)

**Owner:** Owner · **Est:** L · **Deps:** T04, T05

## Scope
Implement the cheap-tier step: build a Batch over the Run's `raw_documents` that does relevance filtering + fact extraction, submit via `ILlmClient`, and (once complete) map the STRUCTURED response into `extracted_facts` rows (`ticker`, `fact`, `sentiment`, `materiality`, `document_id`). Define the structured request/response contract and save representative LLM fixtures. Persist the cheap-tier Batch id into `runs.batch_ids_json` immediately after submit (for resume, T14).

## Out of scope
Polling cadence/resume (T13/T14); synthesis (T09); cost totals (T12 consumes the tokens recorded here).

## DoD
- Structured cheap-tier contract defined; extraction maps deterministically from fixtures to `extracted_facts` (unit + component tests, zero network).
- Bulk of token volume routes here (feeds the ≥80% cheap-share NFR).
- Batch id persisted on submit.
- Realizes [seq-nightly-run](../diagrams/seq-nightly-run.md) cheap-tier block.

## Links
[PRD.md §6 NFR](../PRD.md) · [ADR-0005](../../../00-overview/adr/0005-model-cascade-batch-api.md) · [test-plan.md](../test-plan.md)
