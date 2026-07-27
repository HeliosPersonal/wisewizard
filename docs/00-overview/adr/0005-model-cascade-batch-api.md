---
status: Accepted
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0005 — Use a two-tier model cascade over the Anthropic Batch API

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** Owner

## Context

The nightly Run must turn a few hundred Raw documents into per-Ticker Verdicts within a small personal budget. Running every document through a top-tier model synchronously would be slow and expensive. We must choose how to allocate model tiers and the request mode. See [sad.md](../sad.md) §4, §10 QG-1.

## Decision drivers

- Cost efficiency — bulk of volume must be cheap (sad.md §1 QG-1).
- Quality — final judgment needs a strong model.
- Nightly, non-interactive timing tolerates async latency (sad.md §6).

## Considered options

1. **Two-tier cascade over Batch API** — cheap tier does high-volume relevance+extraction; synthesis tier judges the distilled facts; both submitted as asynchronous batches (~50% cheaper).
2. **Single top-tier model, synchronous** — send everything to the strongest model in real time.
3. **Single cheap model for everything** — lowest cost, but weak judgment.

## Decision outcome

**Chosen: Option 1.** A two-tier cascade over the Batch API. The cheap tier absorbs the large document volume (relevance filtering + fact extraction), so only a small distilled input reaches the synthesis tier for the Verdict. Batch mode roughly halves cost and fits the overnight, non-interactive schedule. Sonnet may optionally sit between tiers for structured scoring if needed.

## Consequences

**Positive**
- Large cost reduction: volume on the cheap tier, batch discount on all calls.
- Strong final judgment where it matters, on a small distilled input.

**Negative**
- Batch latency (up to 24h SLA) means the Run is not instant — acceptable since it runs overnight.
- Two-stage prompt/response contracts to design and validate.

**Neutral**
- An intermediate Sonnet tier can be added later without changing the cascade shape (`ILlmClient` abstracts the provider).

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §4, §10
- Related ADR: [[0004-hangfire-jobs]]
