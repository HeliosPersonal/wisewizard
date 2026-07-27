---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "13"
ticket: "N/A — personal project"
---

# Epic — nightly-research-pipeline

The orchestration and research core of WiseWizard: a nightly, restart-safe Hangfire Run that reads the Universe, triggers ingestion, runs the two-tier Model cascade over the Anthropic Batch API (cheap-tier relevance + fact extraction; synthesis-tier per-Ticker Verdict with Signal, summary, reasoning, cited Sources, and delta vs the previous Run), and persists Extracted facts + Verdicts. Resumes in-flight Batch jobs after a restart.

## Upstream artefacts (LINK, do not duplicate)

- PRD: [PRD.md](../PRD.md) — US-01..US-08, AC-01..AC-09, NFR §6.
- Data model: [data-model.md](../data-model.md) — owns `runs`, `extracted_facts`, `verdicts`.
- Diagrams: [seq-nightly-run](../diagrams/seq-nightly-run.md), [seq-batch-poll-resume](../diagrams/seq-batch-poll-resume.md), [seq-run-failure](../diagrams/seq-run-failure.md).
- Test plan: [test-plan.md](../test-plan.md).
- SAD: [sad.md](../../../00-overview/sad.md) §4 seeds 2+3, §5 module map, §6 flow 1, §10 QG-1/2/3.
- ADRs: [ADR-0005](../../../00-overview/adr/0005-model-cascade-batch-api.md), [ADR-0004](../../../00-overview/adr/0004-hangfire-jobs.md), [ADR-0003](../../../00-overview/adr/0003-sqlite-persistence.md).
- Context: [CONTEXT.md](../../../00-overview/CONTEXT.md).

## Handoff interfaces

- Consumes `raw_documents` (data-ingestion), the Universe = `positions` (ibkr-portfolio-read) ∪ `watchlist` (watchlist-management).
- Produces `verdicts` + `runs` consumed by telegram-bot-reporting.

## Module scope (sad.md §5)

- `WiseWizard.Core/Abstractions` — `ILlmClient` and repository interfaces.
- `WiseWizard.Infrastructure/Llm` — `AnthropicLlmClient` (Batch submit/poll/retrieve).
- `WiseWizard.Infrastructure/Persistence` — Dapper repositories for `runs`/`extracted_facts`/`verdicts`.
- `WiseWizard.Pipeline` — `NightlyPipeline` + `Steps/` continuation chain.
- `WiseWizard.Host` — recurring-job registration + config.

Rule (sad.md §5): `Pipeline` depends only on `Core` abstractions, never on concrete `Infrastructure`.

## Task list

| # | Task | Est |
|---|---|---|
| T01 | Migration: `runs`, `extracted_facts`, `verdicts` + indexes | S |
| T02 | Domain models: Run, ExtractedFact, Verdict, Signal | S |
| T03 | `ILlmClient` abstraction + Batch DTOs (Core) | S |
| T04 | Repositories for runs / facts / verdicts (Dapper) | M |
| T05 | `AnthropicLlmClient`: Batch submit/poll/retrieve (Infra) | L |
| T06 | Universe read step | S |
| T07 | Ingestion-step handoff | S |
| T08 | Cheap-tier extraction step (prompt/response contract + fixtures) | L |
| T09 | Synthesis step (per-Ticker Verdict contract + fixtures) | L |
| T10 | Delta computation (vs previous Run) | M |
| T11 | Evidence-invariant guard + Verdict persistence | M |
| T12 | Cost + token logging and per-Run ceiling enforcement | M |
| T13 | Hangfire recurring job at 23:00 + continuation chain | M |
| T14 | Resume-after-restart recovery of in-flight batches | L |
| T15 | Failure handling + Telegram alerting + max-wall-clock timeout | M |

## Dependency graph

```
T01 ─┬─ T04 ─┬─ T06 ─┐
T02 ─┘        ├─ T07 ─┤
T03 ── T05 ───┴─ T08 ─┴─ T09 ─┬─ T10 ─┐
                               ├─ T11 ─┼─ T13 ─┬─ T14
                               └─ T12 ─┘        └─ T15
```

Parallel branches: T01+T02+T03 at the start; T06/T07/T08 once repos + client exist; T10/T11/T12 once synthesis exists.

## Definition of Done (epic)

- All AC-01..AC-09 covered by passing tests ([test-plan.md](../test-plan.md)), offline via saved LLM fixtures.
- A full Run over a fixture Universe produces one Verdict per Ticker, each citing ≥1 Source.
- Kill-and-restart mid-Run repeats 0 completed steps and produces 0 duplicate Verdicts.
- Per-Run cost + cheap-tier token share recorded; ceiling stops a Run and alerts the Owner.
