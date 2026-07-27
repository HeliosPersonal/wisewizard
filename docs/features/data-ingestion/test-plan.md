---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "15"
ticket: "N/A — personal project"
---

# Test plan — data-ingestion

<!-- Stage 15. Traces every PRD §5 AC to a test. Sources are mocked at the Core
interface boundary (ISecFilingsSource / INewsSource / IMarketDataSource) so the
suite runs offline; real-Source checks are opt-in integration tests. -->

## Levels

| Level | Scope | Tooling |
|---|---|---|
| Unit | Content-hash dedup, lookback/cap filtering, Universe scoping, gap recording — pure logic on mocked Sources | xUnit + Moq |
| Integration | IngestStep ↔ SQLite (Dapper repository), unique-index enforcement, retention cleanup | xUnit + real SQLite temp file |
| Contract | Each Source client conforms to its Core interface (declared User-Agent, paced requests) against recorded fixtures | xUnit + recorded HTTP fixtures |
| Integration (opt-in) | Real SEC EDGAR / RSS / market-data reachability + parsing | xUnit `[Trait("integration","real")]`, excluded from CI default |
| Load | Whole-Universe ingest time budget on synthetic ~40-Ticker Universe | custom timed harness in test project |

## AC coverage

| AC | Test(s) | Level |
|---|---|---|
| AC-01 happy path | `Ingest_Ticker_persists_documents_from_each_source` | integration |
| AC-02 Source unreachable/rate-limited → skip + record gap, continue | `Ingest_when_one_source_fails_records_gap_and_continues_others` | unit + integration |
| AC-03 respect access terms (declared identity + polite rate + backoff) | `SecSource_sends_declared_user_agent`; `SecSource_backs_off_on_rate_limit_signal`; `RateLimiter_holds_requests_within_allowed_rate` | contract + unit |
| AC-04 no duplicate within a Run (content hash) | `Dedup_skips_second_document_with_same_content_hash`; `Unique_index_blocks_duplicate_run_hash` | unit + integration |
| AC-05 only Universe Tickers ingested | `Ingest_iterates_only_universe_tickers`; `Ingest_never_fetches_ticker_outside_portfolio_or_watchlist` | unit |
| AC-06 lookback window + per-Source cap | `Filter_drops_documents_older_than_lookback`; `Filter_caps_documents_per_source_per_ticker` | unit |
| AC-07 zero fresh documents = success, not failure | `Ingest_ticker_with_no_fresh_docs_completes_without_failure` | integration |
| AC-08 retention cleanup removes old docs | `Cleanup_removes_documents_older_than_retention_window`; `Cleanup_keeps_documents_within_window` | integration |

## Edge cases / error paths
- Two candidate documents in the same Run hash-collide on identical content → only one row persisted (dedup + unique index backstop).
- A Source returns a document dated in the future / with an unparseable date → excluded from the lookback window, logged, not persisted.
- All three Sources fail for a Ticker → Ticker recorded with only gaps, Run still completes (AC-02 × 3).
- Source returns exactly the cap count and one older doc → cap kept, older dropped (boundary of AC-06).
- Retention boundary: a document exactly at 90 days → defined behavior asserted (kept until strictly older than window).
- Same article appears in Run N and Run N+1 → persisted in both Runs (dedup is Run-scoped, not global).

## Test data
- Strategy: fixtures per Source — canned EDGAR filing list, canned RSS feed XML, canned market-data snapshot; factory builds `raw_documents` with controllable `published_at`/`content`/`fetched_at`.
- Universe fixture: builder producing a Ticker set with known Portfolio and Watchlist membership, plus an out-of-Universe Ticker used as a negative control for AC-05.
- Cleanup: integration tests use a per-test temporary SQLite file, deleted after each test.

## NFR validation
- Per-Ticker ingest ≤ 30 s and whole-Universe ≤ 20 min → timed load harness over a synthetic ~40-Ticker Universe with mocked Sources returning fixed latency.
- Max ≤ 15 docs per Source per Ticker → assertion on persisted row counts grouped by (`run_id`,`ticker`,`source`).
- Polite rate (SEC ≤ 10 req/s; RSS/market ≤ 1 req/s per host) → rate-limiter unit test measures inter-request spacing.
- Source failure isolation → fault-injection integration test asserts other Sources still persist while one fails.

## CI
- On PR: Unit + Integration + Contract suites (all offline, deterministic).
- Nightly / on-demand: opt-in real-Source integration suite (network) to catch Source drift (feeds RSS/market-data reliability open questions in PRD §8).
- Load harness: run manually before a release change to ingest concurrency or Source count.
