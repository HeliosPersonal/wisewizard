---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T07
deps: [T09, T10]
est: M
---

# T07 — Alert publisher (Run failure, session re-auth)

## Goal

Deliver self-alerts to the Owner's chat when the nightly Run fails and when the Brokerage session lapses and needs re-authentication. Alerts are idempotent across a process restart via the delivery-log de-dup key (T10), and delivered within the §6 timeliness budget.

## Scope

- An alert port (`INotifyOwner` / `IAlertPublisher`) that pipeline and session services call on failure/lapse — this feature owns the publisher; the trigger sources are the nightly-research-pipeline and ibkr-portfolio-read features.
- Two alert kinds with fixed messages: Run-did-not-complete and re-authentication-needed.
- Idempotency: consult `bot_delivery_log` by `event_key` (T10) before send; record on success; suppress duplicates after a restart.
- Send via the bot client with retry-to-success; count delivered only on success.

## Links

- PRD: [PRD.md](../PRD.md) §5 AC-07 (Run failure), AC-08 (re-auth); §6 (alert latency ≤ 60 s, delivery reliability).
- Diagram: [seq-alert](../diagrams/seq-alert.md) (both alerts + idempotency).
- SAD: [sad.md](../../../00-overview/sad.md) §6 flow 3, §7 self-alerts.
- Data model: [data-model.md](../data-model.md) — `bot_delivery_log`.

## Out of scope

- Detecting the failure/lapse (owned by pipeline / ibkr-portfolio-read); the delivery-log table itself (T10).

## DoD

- Contract test: pipeline failure trigger and session-lapse trigger each cause exactly one alert to the Owner (AC-07, AC-08).
- Integration test: re-firing the same `event_key` after a restart sends nothing (idempotent).
- Test: a transient send failure is retried and only counted delivered on success.
- Latency from trigger to send within the §6 budget.
