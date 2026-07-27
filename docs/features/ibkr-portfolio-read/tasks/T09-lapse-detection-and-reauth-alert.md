---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T09
estimate: M
deps: [T08]
---

# T09 — Session lapse detection + Telegram re-auth alert + recovery

## Scope

In `WiseWizard.Infrastructure/Ibkr` (+ a thin alert seam into `WiseWizard.Bot`): implement the lapse/re-auth flow of sad.md §6 flow 3.

- When `CheckSession()` reports lapsed: `MarkLapsed()`, stop pinging while lapsed (ADR-0006).
- Send a single Telegram alert per lapse (guard on `reauth_alerted_at`) telling the Owner to tap 2FA in the Broker's app.
- Keep polling session status; on recovery `MarkLive(now)` (which clears `reauth_alerted_at`) and resume keep-alive.
- Alert delivered via an abstraction so `Ibkr` does not depend on concrete `Bot` types (sad.md §5).

## Links

- PRD [§AC-04](../PRD.md), §6 (re-auth alert latency, recovery detection).
- sad.md §6 flow 3, §11; ADR-0006.
- [seq-session-reauth.md](../diagrams/seq-session-reauth.md) (failure + recovery).

## Definition of Done

- On lapse: pinging stops, one alert requested, `status='lapsed'`, `reauth_alerted_at` set ([§AC-04](../PRD.md)) — integration test.
- Repeated lapse ticks send no further alert until recovery (single-alert-per-lapse) — test.
- On recovery: `status='live'`, `reauth_alerted_at` cleared, keep-alive resumes ([§AC-04](../PRD.md)) — test.
- Last known-good Portfolio remains readable throughout the lapse (sad.md §11).
