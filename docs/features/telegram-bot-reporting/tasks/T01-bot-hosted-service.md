---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T01
deps: []
est: M
---

# T01 — Bot hosted-service skeleton (`TelegramBotService`)

## Goal

Stand up `WiseWizard.Bot`'s `TelegramBotService` as a `BackgroundService` inside the Generic Host that connects to Telegram via the `Telegram.Bot` client and receives updates (commands + callback queries), with each update dispatched to an (initially stub) handler entry point. No business logic yet — just a resilient receive loop.

## Scope

- `TelegramBotService : BackgroundService` with a start/stop lifecycle; receive loop wrapped in try/catch so a handler fault never kills the process (sad.md §8 error handling).
- An `IUpdateDispatcher` seam that the router (T03) will implement; T01 wires a no-op/echo stub behind it.
- Structured logging of received update kind + chat id (never message content beyond the command token).

## Links

- ADR: [0001 single-process Generic Host](../../../00-overview/adr/0001-single-process-generic-host.md) — hosted service, per-service try/catch.
- SAD: [sad.md](../../../00-overview/sad.md) §5 (WiseWizard.Bot module), §6 flow 2.

## Out of scope

- Authorization (T02), routing/handlers (T03–T06), formatting (T08), repositories (T09), DI registration into Host (T11).

## DoD

- Service starts and stops cleanly with the host; a handler exception is logged and the loop continues.
- Unit/integration test: a fake update source drives the loop and the dispatcher stub is invoked; a thrown handler exception does not stop the loop.
- No secrets in source (token read via config abstraction, provided by T11).
