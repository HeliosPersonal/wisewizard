---
status: Superseded
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0003 — Use SQLite for domain data and a separate SQLite file for Hangfire

- **Status:** Superseded by ADR-0007 (2026-07-27)
- **Date:** 2026-07-26
- **Deciders:** Owner

## Context

*This decision held while WiseWizard was local-only. Once it was deployed on helios alongside Sentra — which standardizes on PostgreSQL — this ADR was superseded by ADR-0007 (2026-07-27).*

WiseWizard persists Positions, Watchlist, Raw documents, Extracted facts, Verdicts, and Run metadata, plus Hangfire's own job state. We must choose a datastore that fits a single-user, low-volume, single-server system. See [sad.md](../sad.md) §7-§8.

## Decision drivers

- KISS / zero-admin — single developer, single server (sad.md §2).
- Tiny data volume — a few hundred document rows per night (sad.md §7).
- One-file backup convenience.

## Considered options

1. **SQLite for domain data + a second SQLite file for Hangfire storage.**
2. **PostgreSQL for Hangfire, SQLite for domain data.**
3. **PostgreSQL for everything.**

## Decision outcome

**Chosen: Option 1.** SQLite for both, in two separate files. It has zero external dependencies and one-file backups, and the workload is far below any SQLite limit. Hangfire gets its own file because it prefers exclusive access to its storage; keeping it separate avoids write contention with domain queries.

## Consequences

**Positive**
- No external database to install, secure, or back up beyond copying files.
- Dapper over SQLite is simple and fast at this scale.

**Negative**
- `Hangfire.Storage.SQLite` is community-maintained and weaker under heavy concurrency (irrelevant at our load).
- Concurrent writers must be handled with WAL mode + care (single-process design keeps this simple).

**Neutral**
- Migration to PostgreSQL later is straightforward if volume ever grows; repositories are behind Core interfaces.

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §8
- Related ADR: [[0001-single-process-generic-host]], [[0004-hangfire-jobs]]
- Superseded by: [[0007-postgresql-datastore]]
