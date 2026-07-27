---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T03
deps: []
estimate: S
---

# T03 — watchlist table + WAL migration

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Create the `watchlist` table in the domain SQLite database and ensure WAL journal mode, per ADR-0003.

## Scope

- `watchlist(ticker TEXT PRIMARY KEY NOT NULL, added_at TEXT NOT NULL, note TEXT NULL)`.
- Idempotent creation (create-if-not-exists) as part of the domain DB schema init.
- Enable WAL mode on the domain database.

## Upstream (link, do not duplicate)

- [data-model §Entities, §Constraints](../data-model.md)
- [ADR-0003 SQLite persistence](../../00-overview/adr/0003-sqlite-persistence.md)

## Definition of Done

- Schema init creates `watchlist` with the exact columns/constraints from the data-model.
- Running init twice is a no-op (idempotent).
- WAL mode verified on the domain DB file.
- A smoke test opens the DB and confirms the table + PK exist.
