---
status: Draft
owner: "Owner"
updated_at: "2026-07-27"
stage: "13"
task_id: T03
estimate: M
deps: [T02]
---

# T03 — Repository dialect fixes (`RETURNING`, `ON CONFLICT`, numeric/decimal)

## Scope

Port the repository SQL from the SQLite dialect to the Postgres dialect. Dapper `@param` placeholders, `Map`/`ToParams`, and status-token maps are unchanged (`@name` is identical under Npgsql).

- **Identity read-back:** `INSERT ...; SELECT last_insert_rowid();` → `INSERT ... RETURNING <id>`.
  - `RunRepository` — `RETURNING run_id`.
  - `ExtractedFactRepository`, `BotDeliveryLogRepository` — `RETURNING <id>` wherever the identity id is read back.
- **Upsert / ignore:** `INSERT OR IGNORE` → `INSERT ... ON CONFLICT DO NOTHING`.
  - `BotDeliveryLogRepository` — conflict target `event_key`.
  - `RawDocumentRepository` — conflict target the `(run_id, content_hash)` unique index.
  - `WatchlistRepository` — conflict target `ticker` (PK).
- **Replace:** `VerdictRepository` `INSERT OR REPLACE` → `INSERT ... ON CONFLICT (run_id, ticker) DO UPDATE SET ...`.
- **Boolean/count return types:**
  - `WatchlistRepository.ContainsAsync` — `EXISTS` returns `boolean` in Postgres, so read `<bool>` (not `<long>`).
  - `WatchlistRepository.CountAsync` — `COUNT(*)` returns `bigint`, so read `<long>` then cast (or use `COUNT(*)::int`).
- **Money:** remove the `decimal`→`double` write casts and `double`→`decimal` read casts; bind `decimal` directly against the `numeric` columns (Dapper/Npgsql native).
- Remove any stray `using Microsoft.Data.Sqlite;` imports.

## Links

- Design: [design doc](../../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — "Repository dialect fixes".
- [ADR-0007](../../../00-overview/adr/0007-postgresql-datastore.md).
- Source: `src/WiseWizard.Infrastructure/Persistence/RunRepository.cs`, `ExtractedFactRepository.cs`, `BotDeliveryLogRepository.cs`, `RawDocumentRepository.cs`, `VerdictRepository.cs`, `WatchlistRepository.cs`, `PositionsRepository.cs`, `BrokerSessionRepository.cs`.

## Definition of Done

- `RunRepository` (and any other identity repo) reads the new id via `RETURNING`, not `last_insert_rowid()`.
- `BotDeliveryLog`, `RawDocument`, and `Watchlist` inserts use `ON CONFLICT ... DO NOTHING` on the correct conflict target; `VerdictRepository` uses `ON CONFLICT (run_id, ticker) DO UPDATE SET ...`.
- `WatchlistRepository.ContainsAsync` reads `<bool>`; `CountAsync` reads a `bigint`/`long` correctly.
- No `decimal`↔`double` money casts remain; `numeric` columns round-trip as exact `decimal`.
- No `using Microsoft.Data.Sqlite;` remains in the Infrastructure project.
- **All existing repository tests still pass**, and repository line + branch coverage stays **> 95%**.
