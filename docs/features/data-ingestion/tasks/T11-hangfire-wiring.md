---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T11
deps: [T10, T12]
estimate: S
branch: feat/ingest-hangfire-wiring
---

# T11 — Hangfire wiring (ingest step + retention job)

## Goal

Register `IngestStep` as the first step of the nightly Hangfire chain and register the retention cleanup as a recurring job, via DI in the Host.

## Scope

- Register Sources, repository, rate limiter, `IngestStep` in the Host DI container.
- Expose `IngestStep` as a Hangfire job that the nightly chain enqueues first; it produces `raw_documents` keyed to `run_id` and hands off to the next step (extraction) owned by nightly-research-pipeline.
- Register the T12 retention job as a recurring Hangfire job.
- The overall continuation chain (ingest → extract → synthesize → persist) is owned by nightly-research-pipeline; this task only contributes the ingest job + its registration (interface handoff).

## Links

- ADR: [ADR-0004](../../../00-overview/adr/0004-hangfire-jobs.md).
- SAD: [sad.md §5 (Host wiring), §6 flow 1](../../../00-overview/sad.md).
- Data model: [data-model.md — Handoff](../data-model.md).

## DoD

- Ingest job registered and runnable via Hangfire; on completion, `raw_documents` exist for the `run_id`.
- Retention job registered as recurring.
- Integration test enqueues the ingest job against mocked Sources and asserts persisted rows for the `run_id`.
