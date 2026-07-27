# T04 — Repositories for runs / facts / verdicts (Dapper)

**Owner:** Owner · **Est:** M · **Deps:** T01, T02

## Scope
Implement Dapper repositories in `WiseWizard.Infrastructure/Persistence` behind Core repository interfaces for: create/update `runs` (status, batch_ids_json, cost/token fields), bulk-insert `extracted_facts`, insert `verdicts` (idempotent on `(run_id, ticker)`), read "latest completed Run", read "previous Verdict per Ticker".

## Out of scope
Business rules (evidence guard = T11); Universe/raw_documents reads (T06/T07 use their owning features' repos, referenced read-only).

## DoD
- Repos implement Core interfaces; `Pipeline` depends only on the interfaces.
- Verdict insert is idempotent under replay (relies on `(run_id, ticker)` PK) — covered by a test.
- "Latest completed Run" and "previous Verdict per Ticker" queries use the indexes from [data-model.md](../data-model.md).
- Repo tests pass against a temp SQLite file.

## Links
[data-model.md](../data-model.md) · [ADR-0003](../../../00-overview/adr/0003-sqlite-persistence.md)
