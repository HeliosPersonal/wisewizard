---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "13"
ticket: "N/A — personal project"
---

# Epic — ibkr-portfolio-read

Read the Owner's current Positions from the Broker read-only, keep the Brokerage session alive with periodic pings and a manual daily 2FA re-auth, and persist a single current Portfolio snapshot for the nightly Run.

## Upstream artefacts (tasks LINK, do not duplicate)

- PRD: [PRD.md](../PRD.md) — §4 User stories US-01..US-06, §5 AC-01..AC-09, §6 NFR.
- Architecture: [sad.md](../../../00-overview/sad.md) — §5 module boundaries (`WiseWizard.Core/Abstractions`, `WiseWizard.Infrastructure/Ibkr`, `.../Persistence`), §6 flow 3 (broker-session-expiry), §8 crosscutting.
- ADRs: [0001](../../../00-overview/adr/0001-single-process-generic-host.md) (Generic Host + hosted service), [0002](../../../00-overview/adr/0002-ibkr-client-portal-api.md) (Client Portal API, read-only), [0006](../../../00-overview/adr/0006-manual-2fa-keepalive.md) (keep-alive + manual 2FA).
- Data model: [data-model.md](../data-model.md) — `positions` snapshot + `broker_session` singleton.
- Sequences: [seq-read-positions.md](../diagrams/seq-read-positions.md), [seq-session-reauth.md](../diagrams/seq-session-reauth.md).
- Test plan: [test-plan.md](../test-plan.md).

## Hard rules (from PRD / sad.md / ADRs — must not be broken by any task)

- Broker access is strictly **read-only**. No task may wire an order-placement / modify / cancel capability (PRD §AC-05, CONTEXT invariant, ADR-0002).
- The System never stores Broker credentials; 2FA is a manual Owner tap (ADR-0006).
- `Pipeline`/`Bot` depend only on `Core` abstractions, never on concrete `Infrastructure` types (sad.md §5).
- The Portfolio is a snapshot, overwritten wholesale per successful refresh; a failed refresh retains the last known-good (PRD §AC-03, §AC-06; data-model).

## Task list (11 atomic tasks, each ≤1 day / ≤500 LOC → one PR)

| # | Task | Layer |
|---|---|---|
| T01 | `Position` domain model + `IBrokerReader` abstraction (read-only) | Core |
| T02 | `positions` + `broker_session` schema init | Persistence |
| T03 | `PositionsRepository` (wholesale snapshot replace) | Persistence |
| T04 | `SessionStateRepository` (singleton session state) | Persistence |
| T05 | `ClientPortalBrokerReader` — read Positions from gateway | Infrastructure/Ibkr |
| T06 | `ClientPortalBrokerReader` — session status + keep-alive ping | Infrastructure/Ibkr |
| T07 | `IbkrSessionService` hosted service — keep-alive loop | Infrastructure/Ibkr + Host |
| T08 | Refresh orchestration + snapshot persistence (happy + fail retain) | Infrastructure/Ibkr |
| T09 | Session lapse detection + Telegram re-auth alert + recovery | Infrastructure/Ibkr + Bot |
| T10 | Expose current Portfolio Tickers to the Universe | Core + Persistence |
| T11 | DI wiring, config/options, structured logging | Host |

## Dependency graph

```
T01 ──┬─► T05 ─► T06 ─► T07 ─► T08 ─► T09
      │                          │
      └─► T10                    └─► (uses T03,T04)
T02 ──┬─► T03 ─► T08
      └─► T04 ─► T07
T11 depends on T07,T08,T09 (final wiring)
```

- Parallel branches: T01 (Core) and T02 (schema) can start together. T03/T04 (repos) parallel once T02 lands. T05/T06 (adapter) parallel with the repo branch once T01 lands.
- Critical path: T01 → T05 → T06 → T07 → T08 → T09 → T11.

## Estimate (S = ~2h, M = ~half-day, L = ~1 day)

T01 S · T02 S · T03 M · T04 S · T05 M · T06 M · T07 M · T08 L · T09 M · T10 S · T11 M — total ≈ 4-5 person-days.

## Owners

All tasks: `Owner` (single-developer project).
