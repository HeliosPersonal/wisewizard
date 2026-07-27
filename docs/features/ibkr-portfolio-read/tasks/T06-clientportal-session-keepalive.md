---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T06
estimate: M
deps: [T05]
---

# T06 — `ClientPortalBrokerReader` — session status + keep-alive ping

## Scope

In `WiseWizard.Infrastructure/Ibkr`: add session-status and keep-alive to `ClientPortalBrokerReader`.

- `CheckSession()` — query the gateway for authenticated/live vs lapsed.
- `Ping()` — keep-alive call that holds the session live (ADR-0006).
- Distinguish `live` / `lapsed` / `unknown` (unreachable gateway) from the gateway's response.

## Links

- PRD [§AC-04](../PRD.md), [§AC-09](../PRD.md).
- ADR-0006 (keep-alive + manual 2FA), ADR-0002.
- [seq-session-reauth.md](../diagrams/seq-session-reauth.md).

## Definition of Done

- Status fixtures map to `live` / `lapsed` / `unknown` correctly (contract tests).
- `Ping()` issues a read-only keep-alive call and reports success/failure.
- Unreachable gateway resolves to `unknown`, not a crash.
