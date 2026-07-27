---
status: Accepted
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-27"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0007 — Adopt PostgreSQL for domain data + Hangfire (supersedes 0003)

- **Status:** Accepted
- **Date:** 2026-07-27
- **Deciders:** Owner

## Context

WiseWizard now runs on the helios k3s cluster alongside the sibling Sentra app. Sentra already standardizes on PostgreSQL — a scoped database and a scoped user on a single shared Postgres instance — and runs Hangfire on that same Postgres. Aligning WiseWizard with this convention removes an entire datastore type from the cluster and matches how services on helios are provisioned and operated.

ADR-0003 chose SQLite (two files: one for domain data, one for Hangfire) when WiseWizard was a local-only, single-server app with zero external dependencies. That reasoning no longer holds now that the app is deployed next to Sentra on a cluster that already runs Postgres. This ADR supersedes ADR-0003. See [sad.md](../sad.md) §7-§8.

## Decision drivers

- Match the cluster and Sentra conventions (scoped DB + scoped user; Hangfire on Postgres).
- Zero new infrastructure — a single shared Postgres already runs on helios; the Owner provisions a scoped database + user, and WiseWizard receives only a connection string.
- Exact-decimal money: Postgres `numeric` versus SQLite `REAL`, removing the `decimal`↔`double` casts in the repositories.
- Drop the second datastore (`Hangfire.Storage.SQLite`) and the `SQLitePCLRaw.*` vulnerability pins that came with the SQLite stack.

## Considered options

1. **Stay on SQLite** — two files, as in ADR-0003.
2. **PostgreSQL for domain data, keep SQLite for Hangfire.**
3. **PostgreSQL for both domain data and Hangfire.**

## Decision outcome

**Chosen: Option 3.** PostgreSQL for domain data and Hangfire.

Dapper is kept — no EF Core — so the change is mechanical because the repositories already sit behind Core interfaces:

- Driver swap `Microsoft.Data.Sqlite` → `Npgsql`.
- `SchemaInitializer` ported to the Postgres dialect: `BIGINT GENERATED ALWAYS AS IDENTITY` for identity keys, `numeric` for money columns, timestamps stay ISO-8601 round-trippable text.
- Repositories ported: `INSERT OR IGNORE` → `ON CONFLICT DO NOTHING`, `INSERT OR REPLACE` → `ON CONFLICT DO UPDATE`, `last_insert_rowid()` → `RETURNING`, existence checks return `bool`, counts return `bigint`.
- Hangfire uses `Hangfire.PostgreSql` in the **same** database under a dedicated `hangfire` schema, matching the Sentra pattern and removing the `ConnectionStrings:Hangfire` key and the second file.

The domain connection string arrives via Infisical as `CONNECTIONSTRINGS__WISEWIZARD` (→ `ConnectionStrings:WiseWizard`).

## Consequences

**Positive**
- One datastore type on the cluster instead of two, matching Sentra and helios conventions.
- Exact-decimal money via `numeric`; the `double` casts in the money repositories are removed.
- Drops the `/data` PVC, the `Hangfire.Storage.SQLite` provider, and the `SQLitePCLRaw.*` vulnerability pins.

**Negative**
- Integration tests now require Docker via Testcontainers to spin up a real Postgres.
- Requires the shared helios Postgres to be reachable for the app and for integration tests.

**Neutral**
- Repositories were already behind Core interfaces, so the swap was mechanical and preserved the existing tests and the >95% coverage gate.

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §7-§8
- Superseded ADR: [[0003-sqlite-persistence]]
- Related ADR: [[0004-hangfire-jobs]], [[0008-aspire-local-dev]]
