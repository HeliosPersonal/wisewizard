---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "13"
ticket: "N/A — personal project"
---

# Epic — telegram-bot-reporting

The Owner-facing Telegram bot: the presentation/interaction layer of WiseWizard. It READS what other features produced (Verdicts from nightly-research-pipeline, Positions from ibkr-portfolio-read) and delivers them to the single Owner, plus carries Watchlist commands to the watchlist-management domain and pushes self-alerts on Run failure and Broker-session re-auth.

## Upstream artefacts (LINK, do not duplicate)

- PRD: [PRD.md](../PRD.md) — §5 AC-01..AC-10, §6 NFR, §6.1 security.
- Data model: [data-model.md](../data-model.md) — owns only `bot_delivery_log`; reads `runs`/`verdicts`/`positions`/`watchlist` owned elsewhere.
- Sequences: [seq-daily-digest](../diagrams/seq-daily-digest.md), [seq-drilldown](../diagrams/seq-drilldown.md), [seq-alert](../diagrams/seq-alert.md).
- Test plan: [test-plan.md](../test-plan.md).
- SAD: [sad.md](../../../00-overview/sad.md) §5 (WiseWizard.Bot module), §6 flow 2 (digest + drill-down) & flow 3 (session alert).
- ADRs: [0001 single-process Generic Host](../../../00-overview/adr/0001-single-process-generic-host.md), [0003 SQLite persistence](../../../00-overview/adr/0003-sqlite-persistence.md).

## Tech constraints (FIXED)

- `Telegram.Bot` library; `TelegramBotService` hosted service (ADR-0001).
- Reads domain SQLite via Dapper repositories (Verdicts, Positions, Run); inline keyboards for the "details" buttons.
- Chat-id allowlist for single-Owner authorization (sad.md §11 accepted debt).
- Wires `/watch`, `/unwatch`, `/watchlist` to the watchlist-management domain — this feature owns the bot handler; watchlist-management owns the domain/persistence.
- Does NOT define or migrate `verdicts`/`positions`/`watchlist`; does NOT read the Broker or compute Verdicts.

## Dependency graph

```
T01 hosted-service host ──┬─> T02 allowlist auth ──> T03 command router ──┬─> T04 /portfolio
                          │                                              ├─> T05 /report digest
T09 read repositories ────┴──────────────────────────────────────────────┤   └─> T06 drill-down callback
T08 rendering/escaping ───────────────────────────────────────────────────┤
T10 delivery-log (owned table) ──> T07 alert publisher                     │
T11 wiring/config ────────────────────────────────────────────────────────┘  (integrates T01–T10)
```

- T01 → T02 → T03 gate all command/callback handlers.
- T08 (rendering/escaping) and T09 (repositories) are prerequisites for T04/T05/T06.
- T07 (alerts) needs T10 (delivery-log) and T09.
- T11 (wiring) integrates everything and lands last.
- Watchlist command handler (`/watch`, `/unwatch`, `/watchlist`) is folded into T03's router + a thin delegate; it depends on the watchlist-management domain being available (cross-feature dependency, not re-implemented here).

## Task list

| # | Task | Est |
|---|---|---|
| T01 | Bot hosted-service skeleton (`TelegramBotService`) | M |
| T02 | Chat-id allowlist authorization filter | S |
| T03 | Command/callback router (incl. Watchlist command delegation) | M |
| T04 | `/portfolio` handler + Position formatter | M |
| T05 | `/report` digest formatter (Signal + reason, chunking) | M |
| T06 | Drill-down callback handler (reasoning + cited Sources) | M |
| T07 | Alert publisher (Run failure, session re-auth) | M |
| T08 | Message rendering & escaping utilities | S |
| T09 | Read repositories (Run, Verdict, Position) via Dapper | M |
| T10 | `bot_delivery_log` table + idempotent delivery store | S |
| T11 | DI wiring, config, secrets, host registration | M |

Total ≈ 8–9 person-days. Each task ≤ 1 working day, each a reviewable PR (≤ 500 LOC preferred).
