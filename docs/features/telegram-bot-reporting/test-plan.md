---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "15"
ticket: "N/A — personal project"
---

# Test plan — telegram-bot-reporting

<!-- Stage 15 → see SDLC/plugin/skills/plan-tests/SKILL.md -->
<!-- Traces PRD.md §5 AC-01..AC-10 and §6 NFR. Bot boundary (Telegram.Bot client) and repositories are mocked; SQLite reads use a seeded in-memory/file DB fixture. -->

## Levels

| Level | Scope | Tooling |
|---|---|---|
| Unit | Pure formatting & auth logic: digest/detail/portfolio formatters, escaping, chunking (≤20 lines, ≤4000 chars), chat-id allowlist check, alert event_key de-dup, command/callback parsing | xUnit + FluentAssertions |
| Integration | Handlers ↔ Dapper repositories over a seeded SQLite DB (`runs`, `verdicts`, `positions`, `bot_delivery_log`); Telegram client mocked at the `ITelegramBotClient` boundary; verify sent messages/keyboards | xUnit + seeded SQLite + mocked bot client |
| Contract | Interaction contract between the alert publisher and pipeline/session services (the trigger interface `INotifyOwner`/alert port) | xUnit interface/consumer tests |
| E2E | Simulated update → `TelegramBotService` → captured outbound messages, exercising /report, /portfolio, drill-down callback, /watch·/unwatch·/watchlist, non-Owner drop | xUnit host-level test with fake update source + capturing bot client |
| Load | Digest/drill-down/portfolio latency (§6) and multi-message chunking under a large Universe | BenchmarkDotNet or a scripted timing harness (local, no network) |

## AC coverage

| AC | Test(s) | Level |
|---|---|---|
| AC-01 happy digest | `Report_WithCompletedRun_RendersOneLinePerTicker_WithSignalAndReason` | integration |
| AC-02 drill-down reasoning + Sources | `Details_ForTickerWithVerdict_ShowsReasoningAndCitedSources` | integration |
| AC-02b Ticker absent from Run | `Details_ForTickerWithoutVerdict_TellsOwnerNoVerdict_NoReasoning` | integration |
| AC-03 portfolio summary | `Portfolio_WithPositions_ShowsHoldingAndPnl_AndAsOfAge` | integration |
| AC-04 no completed Run | `Report_NoCompletedRun_TellsOwnerNoDigestYet` | integration |
| AC-05 non-Owner authorization | `AnyCommand_FromNonOwnerChat_Dropped_NoReply_NoRepoRead` | E2E |
| AC-06 latest completed Run only | `Report_WithInProgressRun_UsesLatestCompletedRunOnly` | integration |
| AC-07 Run-failure alert | `RunFailure_TriggersOwnerAlert_Once` | contract + integration |
| AC-08 re-auth alert | `SessionLapse_TriggersReauthAlert` | contract + integration |
| AC-09 chunking, digest > 1 message | `Report_LargeUniverse_ChunksOrderedMessages_NoTickerDropped` | unit + integration |
| AC-10 Watchlist command carried to domain | `WatchCommands_DelegatedToWatchlistDomain_ReplyReflectsOutcome` | integration |

## Edge cases / error paths

- Ticker symbol / note / reasoning / Source title containing Telegram markup characters → expected: escaped, message renders literally, no formatting break or active content (PRD §6.1 injection abuse case).
- Digest with exactly 20 Tickers vs 21 Tickers → expected: 1 message vs 2 ordered messages, boundary respected (AC-09, §6 max-per-message).
- A single Verdict whose reasoning + Sources exceed the message size ceiling → expected: split at Source boundary, all Sources preserved (§6 message size).
- Empty Portfolio on /portfolio → expected: "no current Positions" state, distinct from "could not refresh" (grounds on ibkr-portfolio-read AC-07 semantics).
- Stale Portfolio (old `as_of`) on /portfolio → expected: age surfaced with the summary.
- Details tap for a Ticker after a newer Run has completed since the digest was sent → expected: resolved against the current latest completed Run; if now absent, "no Verdict" reply (AC-02b, AC-06).
- Duplicate alert event after a process restart → expected: suppressed via `event_key` de-dup, no second message (data-model `bot_delivery_log`).
- Telegram send transiently fails → expected: retried; alert counted delivered only on success (§6 delivery reliability).
- Unknown / unsupported command from the Owner → expected: a brief "unknown command" reply, no data leak.

## Test data

- Strategy: SQL seed fixtures for `runs` (one completed, one in_progress, plus a "no runs" variant), `verdicts` (mixed Signals 🟢/🟡/🔴, one with many Sources, one long-reasoning, a large-Universe set of 25 Tickers), `positions` (populated, empty, stale `as_of`). Builder helpers construct Verdict/Position fixtures.
- Owner chat id and one non-Owner chat id from test config; the allowlist under test is set to the Owner id only.
- Telegram boundary: a capturing fake `ITelegramBotClient` records every send (text, chunk order, inline keyboard) for assertions; no network.
- Cleanup: per-test fresh SQLite (in-memory or temp file) created and dropped; fake client reset per test.

## NFR validation

- Digest response p95 ≤ 2 s, portfolio p95 ≤ 2 s, drill-down p95 ≤ 1.5 s → timing harness over the seeded DB with the mocked client (excludes real network, which is Telegram's own latency); assert formatter + repo read stay within budget.
- Max 20 Ticker lines / ≤ 4000 chars per message → unit assertions on the formatter over the 25-Ticker and long-reasoning fixtures (AC-09).
- Alert delivery within 60 s of detection → contract test asserts the publisher sends immediately on trigger; timing measured from trigger to fake-client send.
- Delivery reliability (retry to success) → integration test injects a transient send failure and asserts a retry then a recorded delivery.

## CI

- On PR: unit + integration + contract + E2E suites (all offline, no real Telegram/SQLite server needed — SQLite is file/in-memory, bot client mocked).
- Nightly: same suites plus the latency timing harness.
- Release: full suite green; manual smoke of /report, /portfolio, one drill-down, and one forced alert against the Owner's real chat before first production use.
