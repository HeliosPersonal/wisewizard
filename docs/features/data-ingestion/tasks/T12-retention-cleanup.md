---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T12
deps: [T02]
estimate: S
branch: feat/ingest-retention-cleanup
---

# T12 — Retention cleanup job

## Goal

Remove Raw documents older than the retention window so the store does not grow without bound, while keeping recent evidence auditable.

## Scope

- Cleanup routine deleting `raw_documents` where `fetched_at` is older than 90 days (PRD §6), using the T02 repository range-delete over `ix_raw_documents_fetched_at`.
- Retention by `fetched_at` (when collected), not `published_at`, per [data-model.md](../data-model.md).
- Exposed as a callable job; scheduled by T11.

## Links

- PRD: [PRD.md §5 AC-08](../PRD.md), [§6 NFR — retention](../PRD.md).
- Data model: [data-model.md — Retention / cleanup](../data-model.md).

## DoD

- Integration test: documents older than the window removed; documents within it kept.
- Boundary test at exactly the retention age has defined behavior.
