---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T07
estimate: M
deps: [T06, T04]
---

# T07 — `IbkrSessionService` hosted service — keep-alive loop

## Scope

In `WiseWizard.Infrastructure/Ibkr`: add `IbkrSessionService : BackgroundService` (registered in `Host`, ADR-0001). Implement the keep-alive loop only (refresh comes in T08, lapse handling in T09).

- On each interval (configurable, default 60 s — PRD §6): `Ping()` + `CheckSession()`, then `SessionStateRepository.MarkLive(now)` when live.
- Loop wrapped in try/catch so a transient failure never kills the process (sad.md §8).
- Interval from `IOptions<IbkrOptions>`.

## Links

- PRD [§AC-09](../PRD.md), §6 (keep-alive interval).
- ADR-0001 (hosted service in Generic Host), ADR-0006.
- [seq-session-reauth.md](../diagrams/seq-session-reauth.md) (happy keep-alive).

## Definition of Done

- Service pings once per configured interval and advances `last_keepalive_at` while live (integration test on accelerated clock, [§AC-09](../PRD.md)).
- A thrown error in one tick is caught; the loop continues (test).
- Interval is configurable via options; default 60 s.
