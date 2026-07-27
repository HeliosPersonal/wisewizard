---
status: Accepted
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0004 — Use Hangfire for nightly job scheduling, persistence, and retries

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** Owner

## Context

The nightly Run is a multi-step chain (ingest → cheap-tier extract → synthesize → persist) that spans hours because Batch jobs are asynchronous. It must survive process restarts, retry flaky Source/LLM calls, and be observable. We must choose how to schedule and orchestrate it. See [sad.md](../sad.md) §4, §6.

## Decision drivers

- Recoverability — a Run must survive restarts and resume in-flight batches (sad.md §1 QG-2).
- Observability — the Owner wants to see job status/history (sad.md §7).
- Async, long-running steps with retries (sad.md §6).

## Considered options

1. **Hangfire** — persisted jobs, recurring scheduler, continuations, automatic retries, and a web dashboard.
2. **Hand-rolled `PeriodicTimer` + custom state table** — write scheduling, retry, and persistence ourselves.
3. **Quartz.NET** — mature scheduler, but no built-in persisted job queue with retries/dashboard of the same ergonomics.

## Decision outcome

**Chosen: Option 1.** Hangfire. It provides persistence, retries with backoff, continuations, and a dashboard out of the box — exactly the recoverability and observability the nightly Run needs — instead of reinventing them. The chain becomes a declarative sequence of continuation jobs whose state is durable.

## Consequences

**Positive**
- Restart-safe: job state and batch ids persist; polling resumes after a restart.
- Automatic retries with backoff for transient Source/LLM failures.
- Dashboard gives per-job visibility the Owner asked for.

**Negative**
- Adds a dependency and its storage schema.
- SQLite storage provider is community-maintained (accepted in [[0003-sqlite-persistence]]).

**Neutral**
- Continuations express the cascade cleanly; if steps grow, they remain individually retriable jobs.

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §4, §6
- Related ADR: [[0003-sqlite-persistence]], [[0005-model-cascade-batch-api]]
