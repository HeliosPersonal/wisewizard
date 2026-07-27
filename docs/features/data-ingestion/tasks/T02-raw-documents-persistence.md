---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T02
deps: [T01]
estimate: M
branch: feat/ingest-raw-documents-persistence
---

# T02 — `raw_documents` migration + Dapper repository

## Goal

Create the `raw_documents` table (with dedup + read + retention indexes) and a Dapper repository implementing `IRawDocumentRepository`.

## Scope

- Migration creating `raw_documents` per [data-model.md](../data-model.md): all columns, FK `run_id → runs(id)`, indexes `ux_raw_documents_run_hash` (UNIQUE), `ix_raw_documents_run_ticker`, `ix_raw_documents_fetched_at`.
- `WiseWizard.Infrastructure/Persistence/RawDocumentRepository.cs`: insert (honoring the unique index), exists-by-`(run_id, content_hash)`, read-by-`run_id`/`ticker`, delete-older-than for retention.
- SQLite WAL per ADR-0003.

## Links

- Data model: [data-model.md](../data-model.md) — schema, indexes, constraints, retention.
- ADR: [ADR-0003](../../../00-overview/adr/0003-sqlite-persistence.md).

## DoD

- Migration applies on a fresh SQLite file; all three indexes present.
- Integration test: inserting a second row with the same `(run_id, content_hash)` is rejected by `ux_raw_documents_run_hash` (AC-04 backstop).
- Repository CRUD + range-delete covered by integration tests against a temp SQLite file.
