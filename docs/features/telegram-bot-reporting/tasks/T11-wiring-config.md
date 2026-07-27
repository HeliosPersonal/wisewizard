---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T11
deps: [T01, T02, T03, T04, T05, T06, T07, T08, T09, T10]
est: M
---

# T11 — DI wiring, config, secrets, host registration

## Goal

Wire the whole `WiseWizard.Bot` feature into the Generic Host: register `TelegramBotService`, the authorizer, router, handlers, formatters, repositories, delivery-log, and alert publisher in DI; bind configuration (Telegram token, Owner chat id, DB path) via `IOptions<T>` from appsettings + user-secrets; expose the alert port for the pipeline and session services to call.

## Scope

- DI registration of all bot components behind their Core interfaces (dependency direction Host → Bot → Infrastructure → Core, sad.md §5).
- `BotOptions` bound from config; Telegram token and Owner chat id from user-secrets/env — never committed (sad.md §8 secrets).
- Register `TelegramBotService` as a hosted service alongside the existing services (ADR-0001).
- Publish the `IAlertPublisher` port so nightly-research-pipeline and ibkr-portfolio-read can trigger alerts.

## Links

- ADR: [0001 single-process Generic Host](../../../00-overview/adr/0001-single-process-generic-host.md).
- SAD: [sad.md](../../../00-overview/sad.md) §5 (composition root), §8 (config, secrets).
- Integrates T01–T10.

## Out of scope

- Any handler/formatter/repository logic (their own tasks); the pipeline/session detection code that calls the alert port.

## DoD

- Host starts with the bot service registered; DI resolves the full graph with no missing dependency.
- E2E test at host level: a simulated Owner update flows through auth → router → handler → captured send; a non-Owner update is dropped (AC-05).
- Secrets loaded from user-secrets/env; none present in committed config.
- Manual smoke: /report, /portfolio, one drill-down, one forced alert against the Owner's real chat (release checklist).
