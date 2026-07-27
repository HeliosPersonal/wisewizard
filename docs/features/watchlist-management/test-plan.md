---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "15"
ticket: "N/A — personal project"
---

# Test plan — watchlist-management

<!-- Stage 15. Levels adapted to the .NET domain-feature reality (xUnit + Dapper/SQLite).
     No HTTP/E2E surface here — the Telegram transport is owned and tested by
     telegram-bot-reporting; this plan tests the Watchlist domain + persistence. -->

> **Upstream:** [PRD](../PRD.md) §5 AC · [data-model](../data-model.md) · [seq-add-watch](./diagrams/seq-add-watch.md) · [seq-remove-watch](./diagrams/seq-remove-watch.md)

## Levels

| Level | Scope | Tooling |
|---|---|---|
| Unit | Ticker normalization + format validation, dedup decision, size-cap, note-length, owned-Position exclusion — all against a mocked `IWatchlistRepository` and mocked Positions read | xUnit + Moq |
| Integration | `IWatchlistRepository` Dapper implementation against a real SQLite database (add / exists / count / list / remove, PK dedup backstop) | xUnit + SQLite (temp file, WAL) |
| Contract | N/A — no external API contract; the command semantics are the contract consumed by telegram-bot-reporting and are asserted at the unit level | — |
| E2E | N/A here — end-to-end command flow through the Telegram bot is owned by telegram-bot-reporting | — |
| Load | Micro-benchmark of list + add latency against the NFR targets | xUnit timing assertion / BenchmarkDotNet (optional) |

## AC coverage

| AC | Test(s) | Level |
|---|---|---|
| AC-01 happy add | `Add_ValidUnownedNewTicker_PersistsAndConfirms` | integration |
| AC-02 list | `GetAll_ReturnsAllEntriesOrderedByAddedAt_WithNotes` | integration |
| AC-03 happy remove | `Remove_WatchedTicker_DeletesAndReportsRemoved` | integration |
| AC-04 malformed symbol | `Add_MalformedSymbol_RejectedNothingPersisted` (empty, over-long, illegal chars) | unit |
| AC-05 remove not-watched | `Remove_TickerNotWatched_NoChangeReportsNotWatched` | integration |
| AC-06 authorization | asserted in telegram-bot-reporting; domain-side `Add_WhenCallerUnauthorized_DomainNeverInvoked` guard | unit |
| AC-07 duplicate | `Add_DuplicateTicker_AnyCasingOrSpacing_KeepsSingleEntry` (unit for decision, integration for PK backstop) | unit + integration |
| AC-08 already owned | `Add_SymbolIsOwnedPosition_RefusedNotPersisted` | unit |

## Edge cases / error paths

- Lowercase / mixed-case / space-padded symbol (`aapl`, ` AaPl `) → expected: normalized to `AAPL`, deduped against existing `AAPL` (AC-07).
- Symbol with a dot or hyphen (`BRK.B`, `RDS-A`) → expected: accepted as well-formed.
- Symbol exactly 10 chars → accepted; 11 chars → rejected as malformed (NFR §6, AC-04).
- Note exactly 280 chars → accepted; 281 chars → rejected before persistence (NFR §6).
- Add when Watchlist already holds 100 Tickers → expected: refused with size-cap explanation; nothing persisted (NFR §6).
- Remove a Ticker that exists only as an owned Position, never watched → expected: reported as not on the Watchlist (AC-05; see open question in PRD §8).
- Add a Ticker, restart the process, list → expected: the Ticker is still present (durability NFR §6).
- Empty Watchlist listed → expected: an empty list, no error (AC-02 with zero entries).

## Test data

- Strategy: fixtures build `WatchlistEntry` instances; a small in-memory Positions stub supplies owned Tickers for AC-08. Integration tests seed rows directly via the repository.
- Cleanup: per-test — each integration test uses a fresh temporary SQLite file (or a transaction rolled back per test) so tests do not share state.

## NFR validation

- Change acknowledgement ≤ 500 ms → integration timing assertion around a single add against a warm SQLite file.
- List latency ≤ 200 ms for a full (100-Ticker) Watchlist → integration timing assertion over a seeded 100-row table.
- Durability → kill-and-reopen the SQLite file between write and read in one integration test.

## CI

- Unit + integration suites run on every push (fast; SQLite temp files need no external service).
- Load/timing assertions run in the same integration suite; treated as soft signals, not hard gates, given single-developer scale.
