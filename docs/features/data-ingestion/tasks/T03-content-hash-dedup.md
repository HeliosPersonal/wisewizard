---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T03
deps: [T01]
estimate: S
branch: feat/ingest-content-hash-dedup
---

# T03 — Content-hash dedup logic

## Goal

Compute a stable content hash for a candidate document and decide whether it already exists within the current Run — realizing the "no duplicate within a Run" invariant.

## Scope

- `WiseWizard.Core` hashing helper: normalize content (trim/collapse whitespace, drop volatile fields) then hash (e.g. SHA-256 hex) into `content_hash`.
- Dedup decision using `IRawDocumentRepository.ExistsInRun(run_id, content_hash)`.
- Dedup is **Run-scoped**: the same content in a later Run is not a duplicate.

## Links

- PRD: [PRD.md §5 AC-04](../PRD.md).
- SAD: [sad.md §8](../../../00-overview/sad.md) — dedup by content hash.
- Diagram: [seq-ingest-ticker.md](../diagrams/seq-ingest-ticker.md) (dedup loop).

## DoD

- Unit test: identical content → identical hash; trivial whitespace differences → same hash.
- Unit test: second document with same hash in same Run is flagged duplicate; same hash in a different Run is not.
