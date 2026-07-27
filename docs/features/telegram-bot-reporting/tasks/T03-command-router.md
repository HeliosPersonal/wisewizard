---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T03
deps: [T02]
est: M
---

# T03 — Command/callback router (incl. Watchlist delegation)

## Goal

Parse an authorized update into a command or a callback and route it to the correct handler: `/portfolio` → T04, `/report` → T05, details callback → T06, and `/watch` · `/unwatch` · `/watchlist` → a thin delegate that invokes the **watchlist-management** domain and renders its outcome. Unknown commands get a brief "unknown command" reply.

## Scope

- Command token parser (leading `/word`, argument tail) and callback-data parser (Ticker + Run reference the details button carries).
- Dispatch table mapping tokens/callbacks to handler interfaces (handlers land in their own tasks).
- Watchlist delegate: calls the watchlist-management domain's add/remove/list operation and passes the reply text back to the send path. This feature does not validate symbols or dedupe — that is the domain's job (watchlist-management PRD).
- Unknown-command fallback reply.

## Links

- PRD: [PRD.md](../PRD.md) §5 AC-10 (Watchlist command carried to its domain).
- SAD: [sad.md](../../../00-overview/sad.md) §5 (Bot handlers).
- Cross-feature: watchlist-management PRD (owns Watchlist semantics/persistence; AC-01..AC-08 there).

## Out of scope

- Handler bodies for /portfolio, /report, drill-down (T04–T06); the Watchlist domain implementation (watchlist-management feature).

## DoD

- Unit test: each token/callback routes to the right handler stub; unknown command yields the fallback reply.
- Unit test: a Watchlist command is delegated to a mocked watchlist domain and the domain's reply is what gets sent (AC-10); the router itself makes no add/remove decision.
- Callback-data parse round-trips the Ticker + Run reference the details button encodes.
