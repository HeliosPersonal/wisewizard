---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "15"
ticket: "N/A — personal project"
---

# Test plan — ibkr-portfolio-read

<!-- Stage 15. Upstream: PRD §5 (AC-01..AC-09), §6 (NFR) · data-model.md · diagrams/*.
sad.md §5 module boundaries: Core abstractions mocked; Ibkr adapter tested against a
stubbed gateway; SQLite persistence tested on a real in-memory/temp-file DB. -->

## Levels

| Level | Scope | Tooling |
|---|---|---|
| Unit | Position mapping (gateway shape → Core `Position`), age/staleness computation, single-alert-per-lapse guard, snapshot-replace transaction logic | xUnit + NSubstitute (mock `IBrokerReader`, repositories) |
| Integration | `PositionsRepository` / `SessionStateRepository` against a real SQLite DB (temp file); `IbkrSessionService` loop against a stubbed gateway HTTP endpoint | xUnit + `WebApplicationFactory`/`HttpMessageHandler` stub + throwaway SQLite file |
| Contract | `ClientPortalBrokerReader` maps recorded read-only gateway responses (Positions, session status) into Core models; read-only surface only | xUnit + saved gateway response fixtures |
| E2E (opt-in) | Full refresh + keep-alive against the real IBKR Client Portal gateway on the Owner's host | manual / opt-in integration run (`WiseWizard.Infrastructure.Tests`, network) |
| Load / timing | Keep-alive interval and read-latency NFRs | timing assertions in integration tests (no external load tool needed at this scale) |

## AC coverage

| AC | Test(s) | Level |
|---|---|---|
| AC-01 happy refresh | `Refresh_LiveSession_PersistsCurrentSnapshot_WithAsOf` | integration |
| AC-02 Position detail | `Map_GatewayPosition_CarriesQtyAvgCostMarketValuePnl` | unit + contract |
| AC-03 refresh fails, retain last-good | `Refresh_GatewayUnreachable_KeepsLastGood_RecordsFailedAttempt` | integration |
| AC-04 session lapse + alert + recover | `Session_Lapsed_StopsPing_AlertsOwner_ResumesOnRecovery` | integration |
| AC-05 read-only capability boundary | `Reader_ExposesNoOrderCapability` (type/API surface assertion) | unit + contract |
| AC-06 single current snapshot, no leftovers | `Refresh_ReplacesSnapshotWholesale_NoStaleRows` | integration |
| AC-07 empty-but-current Portfolio | `Refresh_ZeroPositions_RecordsEmptyCurrentSnapshot` | integration |
| AC-08 Portfolio feeds Universe | `CurrentPortfolio_ExposesTickersForUniverse` | unit |
| AC-09 keep-alive between Runs | `KeepAlive_LiveSession_PingsEachInterval_UpdatesState` | integration |

## Edge cases / error paths

- Gateway returns a Position with a Ticker already present (duplicate) → expected: PK on `positions.ticker` enforces one row; mapping folds/rejects duplicates (PRD §AC-06).
- Refresh fails on the very first ever attempt (no prior snapshot) → expected: no `positions` rows, `last_snapshot_at` stays NULL, failure recorded; digest shows "no Portfolio yet" not a stale one (data-model).
- Session lapses mid-refresh → expected: treated as a failed refresh (AC-03) *and* session marked lapsed with alert (AC-04); last-good retained.
- Owner does not re-auth for a long time → expected: single alert per lapse (no spam); Portfolio flagged stale past 20 h (PRD §6, open question §8).
- Gateway reports session live but returns malformed Positions → expected: refresh treated as failed (AC-03), last-good retained, error logged.
- Recovery after 2FA → expected: `status='live'`, `reauth_alerted_at` cleared, keep-alive resumes within 60 s (PRD §6).
- Money figures in a non-USD currency → expected: `currency` persisted alongside the figures (data-model), not coerced to USD.

## Test data

- Strategy: fixtures of recorded (sanitized) gateway responses — a multi-Position Portfolio, an empty Portfolio, a session-live status, a session-lapsed status — plus factory helpers building Core `Position` lists.
- No real Broker credentials or live account data in the repo; E2E fixtures are opt-in and run only on the Owner's host.
- Cleanup: per-test — each integration test uses a fresh temp SQLite file (or `:memory:` connection) created in setup and dropped in teardown.

## NFR validation

- Portfolio-read latency ≤ 5 s p95 → integration test asserts the refresh span against a fast local stub stays well under budget; real-gateway latency spot-checked in the opt-in E2E run.
- Keep-alive interval = 60 s → integration test drives a virtualized/accelerated clock and asserts one ping per interval and `last_keepalive_at` advancement (PRD §AC-09).
- Re-auth alert latency ≤ 2 min → integration test asserts the alert is requested on the first lapse-detection tick.
- Refresh reliability ≥ 99% → tracked post-release via logged success/attempt counts (KPI §7); not a pre-release gate.

## CI

- On PR: Unit + Integration + Contract suites (all hermetic — stubbed gateway, temp SQLite; no network).
- Nightly / release: same suites; the opt-in real-gateway E2E is run manually by the Owner on the host before a production cutover (needs a live Brokerage session and 2FA).
