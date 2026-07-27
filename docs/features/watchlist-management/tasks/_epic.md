---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "13"
ticket: "N/A — personal project"
---

# Epic — watchlist-management

The Owner curates a Watchlist of Tickers to research but not yet own, via the `/watch`, `/unwatch`, and `/watchlist` commands. The Watchlist plus Portfolio Tickers form the Universe each Run analyzes. This epic delivers the **Watchlist domain and its persistence** in `WiseWizard.Core` + `WiseWizard.Infrastructure`: the `WatchlistEntry` model, Ticker normalization/validation, the `IWatchlistRepository` (Dapper/SQLite), the size-cap/dedup/owned-exclusion invariants, and the add/remove/list command semantics.

## Scope boundary

- **In scope:** domain model, validation/normalization, repository abstraction + Dapper/SQLite implementation, `watchlist` table, domain service enforcing all invariants, and the domain-side authorization contract.
- **Out of scope (dependency):** the Telegram transport — how `/watch`, `/unwatch`, `/watchlist` are received, the chat-id allowlist wiring, and message rendering — is owned by the **telegram-bot-reporting** feature. This epic exposes the domain service and its semantics for that feature to call. See [PRD §1](../PRD.md).

## Upstream artefacts (LINK, do not duplicate)

- [PRD](../PRD.md) — §4 User stories, §5 Acceptance criteria, §6 NFR.
- [data-model](../data-model.md) — `watchlist` table, invariants, `IWatchlistRepository`.
- [seq-add-watch](../diagrams/seq-add-watch.md) · [seq-remove-watch](../diagrams/seq-remove-watch.md).
- [test-plan](../test-plan.md).
- [sad.md](../../00-overview/sad.md) §5 module boundaries, §6 runtime, §8 crosscutting.
- ADRs: [0001 single-process host](../../00-overview/adr/0001-single-process-generic-host.md) · [0003 SQLite persistence](../../00-overview/adr/0003-sqlite-persistence.md).

## Tasks

See [tracker.md](./tracker.md) for status and the dependency graph. Eight atomic tasks, each ≤ 1 day / ≤ 500 LOC:

1. `t01-watchlistentry-model` — domain model.
2. `t02-ticker-normalization-validation` — normalize + format-validate.
3. `t03-watchlist-schema` — `watchlist` table + WAL migration.
4. `t04-iwatchlistrepository-abstraction` — repository interface in Core.
5. `t05-dapper-repository-impl` — SQLite implementation.
6. `t06-watchlist-service` — domain service enforcing invariants.
7. `t07-owned-position-exclusion` — cross-context check against Positions (AC-08).
8. `t08-tests` — unit + integration suite per the test plan.

## Definition of Done (epic)

- All §5 AC (AC-01..AC-08) covered by passing tests ([test-plan](../test-plan.md)).
- `IWatchlistRepository` + `WatchlistEntry` live in `WiseWizard.Core`; Dapper impl in `WiseWizard.Infrastructure`; `Pipeline`/`Bot` depend only on the Core abstraction (sad.md §5 rule).
- All domain invariants (normalization, format, dedup, size cap, note length, owned-exclusion) enforced in Core and verified.
- Domain service ready for telegram-bot-reporting to wire in.
