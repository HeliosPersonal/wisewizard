---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "13"
ticket: "N/A — personal project"
---

# Tracker — telegram-bot-reporting

| # | Task | Deps | Est | Owner | Status | Primary AC / links |
|---|---|---|---|---|---|---|
| T01 | [Bot hosted-service skeleton](./T01-bot-hosted-service.md) | — | M | Owner | Todo | ADR-0001; sad.md §5 |
| T02 | [Chat-id allowlist auth filter](./T02-chatid-allowlist-auth.md) | T01 | S | Owner | Todo | AC-05; §6.1 |
| T03 | [Command/callback router](./T03-command-router.md) | T02 | M | Owner | Todo | AC-10; sad.md §5 |
| T04 | [`/portfolio` handler + formatter](./T04-portfolio-handler.md) | T03, T08, T09 | M | Owner | Todo | AC-03 |
| T05 | [`/report` digest formatter](./T05-report-digest-formatter.md) | T03, T08, T09 | M | Owner | Todo | AC-01, AC-04, AC-06, AC-09 |
| T06 | [Drill-down callback handler](./T06-drilldown-callback.md) | T03, T08, T09 | M | Owner | Todo | AC-02, AC-02b, AC-06 |
| T07 | [Alert publisher](./T07-alert-publisher.md) | T09, T10 | M | Owner | Todo | AC-07, AC-08; sad.md §6 flow 3 |
| T08 | [Rendering & escaping utilities](./T08-rendering-escaping.md) | — | S | Owner | Todo | §6, §6.1; AC-09 |
| T09 | [Read repositories (Run/Verdict/Position)](./T09-read-repositories.md) | — | M | Owner | Todo | data-model.md read patterns |
| T10 | [`bot_delivery_log` idempotent store](./T10-delivery-log-store.md) | — | S | Owner | Todo | data-model.md; ADR-0003 |
| T11 | [DI wiring, config, secrets](./T11-wiring-config.md) | T01–T10 | M | Owner | Todo | ADR-0001; sad.md §8 |

Status legend: Todo / In progress / In review / Done.
