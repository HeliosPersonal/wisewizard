# T01 — Migration: `runs`, `extracted_facts`, `verdicts` + indexes

*Superseded by ADR-0007: schema is now PostgreSQL (numeric money, BIGINT GENERATED ALWAYS AS IDENTITY). See docs/features/postgres-migration/.*

**Owner:** Owner · **Est:** S · **Deps:** none

## Scope
Create the domain-DB schema for the three tables this feature owns, plus the four indexes, exactly per [data-model.md](../data-model.md). SQLite (WAL) in the domain file per [ADR-0003](../../../00-overview/adr/0003-sqlite-persistence.md). Do NOT create `positions`/`watchlist`/`raw_documents` (owned elsewhere) — only reference their columns for the FKs.

## Out of scope
Repository code (T04), Hangfire storage schema (separate file, ADR-0004).

## DoD
- Migration creates `runs`, `extracted_facts`, `verdicts` with columns/constraints from data-model.md.
- Indexes `idx_runs_status_finished`, `idx_facts_run_ticker`, `idx_verdicts_run_ticker`, `idx_verdicts_ticker_created` present.
- Composite PK `(run_id, ticker)` on `verdicts`; FKs to `runs`/`raw_documents` declared.
- Migration applies cleanly on a fresh temp SQLite file in a test.

## Links
[data-model.md](../data-model.md) · [ADR-0003](../../../00-overview/adr/0003-sqlite-persistence.md)
