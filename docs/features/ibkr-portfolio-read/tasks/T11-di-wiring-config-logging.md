---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T11
estimate: M
deps: [T07, T08, T09]
---

# T11 — DI wiring, config/options, structured logging

## Scope

In `WiseWizard.Host` (composition root, ADR-0001): wire the feature end-to-end.

- Register `IBrokerReader` → `ClientPortalBrokerReader`, `IPortfolioReader`, both repositories, and `IbkrSessionService` as a hosted service.
- `IbkrOptions` from `appsettings` (keep-alive interval, gateway base address on `localhost`, staleness threshold); gateway `HttpClient` via `IHttpClientFactory`. No Broker credentials in config (ADR-0006).
- Structured logging with `run_id` scope on refresh spans (sad.md §8).
- Ensure schema-init (T02) runs at startup.

## Links

- ADR-0001 (Generic Host wiring), ADR-0002, ADR-0006.
- PRD §6 (configurable intervals/thresholds), §6.1 (localhost binding, no creds).
- sad.md §8 (config via `IOptions<T>`, logging).

## Definition of Done

- App starts with `IbkrSessionService` running; keep-alive and refresh operate against a stubbed gateway end-to-end (integration test via host factory).
- All intervals/addresses/thresholds come from `IbkrOptions`; no hard-coded gateway URL or secret in source (§6.1).
- Refresh logs carry `run_id` and timing (§6).
- `Pipeline`/`Bot` reference only Core abstractions from this feature (sad.md §5) — verified by project references.
