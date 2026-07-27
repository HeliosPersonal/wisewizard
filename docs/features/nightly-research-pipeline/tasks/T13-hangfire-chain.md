# T13 — Hangfire recurring job at 23:00 + continuation chain

**Owner:** Owner · **Est:** M · **Deps:** T10, T11, T12

## Scope
Wire the steps into a Hangfire recurring job at 23:00 local, expressed as a chain of continuations: Universe read (T06) → ingestion handoff (T07) → cheap-tier submit+poll (T08) → extraction persist → synthesis submit+poll (T09) → delta (T10) + evidence guard/persist (T11) with cost accounting (T12) → mark Run finished. Each step is an individually retriable, persisted job (ADR-0004). Register only the scheduler as the Run initiator — no ad-hoc trigger path (AC-04b).

## Out of scope
Restart recovery of pending batches (T14); failure alerting (T15) — this task wires the happy chain.

## DoD
- Recurring job registered at 23:00; only the scheduler starts a Run (AC-04b) — test asserts no ad-hoc trigger.
- Full chain over a fixture Universe produces one Verdict per Ticker and marks the Run finished (AC-01) — integration test.
- Steps are persisted continuations (visible in the Hangfire dashboard).
- Realizes [seq-nightly-run](../diagrams/seq-nightly-run.md) end-to-end.

## Links
[PRD.md §5 AC-01, AC-04b](../PRD.md) · [ADR-0004](../../../00-overview/adr/0004-hangfire-jobs.md) · sad.md §6 flow 1
