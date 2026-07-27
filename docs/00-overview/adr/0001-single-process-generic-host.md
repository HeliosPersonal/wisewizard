---
status: Accepted
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0001 — Host the bot, broker session, and jobs in one .NET Generic Host process

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** Owner

## Context

WiseWizard has three long-lived responsibilities: a Telegram bot that must respond instantly, a broker session that must stay alive and read positions, and a nightly research pipeline. They share the same SQLite domain data. We must decide whether these run as one process or several. See [sad.md](../sad.md) §4-§5.

## Decision drivers

- KISS — single developer, single always-on server (sad.md §2 Constraints).
- Resilience through operational simplicity (sad.md §1 QG-2).
- Shared domain DB accessed by all three responsibilities (sad.md §5).
- Low load — no need for independent scaling of parts.

## Considered options

1. **Single Generic Host process with three `BackgroundService` hosted services** — bot, broker session, Hangfire server co-hosted, one DI container, one config.
2. **Separate processes per responsibility** — independently deployable bot / pipeline / broker apps communicating through the shared DB.
3. **Serverless / scheduled functions** — cloud functions on a timer for the pipeline, hosted bot elsewhere.

## Decision outcome

**Chosen: Option 1.** One Generic Host process with hosted services. It gives a single deploy, one log stream, one config, and shared DI over the SQLite domain DB, matching the single-developer/single-server reality. A failure in one hosted service is contained with try/catch so the others keep running.

## Consequences

**Positive**
- One deploy, one restart, one log stream — trivial ops.
- Shared DI and configuration; no inter-process contract to maintain.
- Broker gateway and REST consumer live on the same host (`localhost`).

**Negative**
- Cannot scale or restart one responsibility independently.
- A process-fatal bug takes down all three at once (mitigated by per-service try/catch and Hangfire persistence).

**Neutral**
- Splitting into separate processes later is possible; the clean module boundaries (§5) make extraction feasible if ever needed.

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §4, §5
- Related ADR: [[0003-sqlite-persistence]], [[0004-hangfire-jobs]]
