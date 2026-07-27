# T14 — Resume-after-restart recovery of in-flight batches

**Owner:** Owner · **Est:** L · **Deps:** T13

## Scope
Make a Run resumable after a process restart: on startup, Hangfire re-enters the persisted continuation; the pipeline loads the Run by `run_id`, reads the persisted Batch id from `runs.batch_ids_json`, and resumes polling the same Batch instead of re-submitting. Skip any already-completed step (e.g. cheap-tier extraction whose `extracted_facts` already exist). Rely on the `(run_id, ticker)` PK to make Verdict insert idempotent on replay.

## Out of scope
Failure/timeout handling (T15); initial submit logic (T08/T09).

## DoD
- Kill-and-restart mid-Run resumes the same pending Batch; 0 completed steps repeated and 0 duplicate Verdicts (AC-08, PRD §6) — integration test.
- Poll cadence = 5 min (configurable), driven by an injected clock in tests.
- Realizes [seq-batch-poll-resume](../diagrams/seq-batch-poll-resume.md) resume path.

## Links
[PRD.md §5 AC-08, §6 NFR](../PRD.md) · [ADR-0004](../../../00-overview/adr/0004-hangfire-jobs.md) · [data-model.md](../data-model.md) (`batch_ids_json`, `(run_id, ticker)` PK)
