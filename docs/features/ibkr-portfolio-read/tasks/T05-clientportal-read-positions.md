---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task_id: T05
estimate: M
deps: [T01]
---

# T05 — `ClientPortalBrokerReader` — read Positions from the gateway

## Scope

In `WiseWizard.Infrastructure/Ibkr`: begin `ClientPortalBrokerReader : IBrokerReader`. Implement reading the Owner's current Positions from the local Client Portal gateway via `HttpClient` (localhost) and map the gateway shape into Core `Position` objects, stamping a single read instant as `as_of`.

- Read-only calls only (ADR-0002). No order endpoints referenced.
- Map quantity, avg_cost, market_value, unrealized_pnl, currency; uppercase Ticker.
- Handle multi-Position, empty, and malformed responses (malformed → surfaced as a failed read).

## Links

- PRD [§AC-01](../PRD.md), [§AC-02](../PRD.md), [§AC-05](../PRD.md), [§AC-07](../PRD.md).
- ADR-0002 (Client Portal API, read-only), sad.md §5 (`Infrastructure/Ibkr`).
- [seq-read-positions.md](../diagrams/seq-read-positions.md), data-model.md.

## Definition of Done

- Recorded gateway response fixtures map to correct `Position` fields (contract test, [§AC-02](../PRD.md)).
- Empty response maps to an empty Position list, distinct from a failed read ([§AC-07](../PRD.md)).
- Malformed response is surfaced as a failed read, not a partial snapshot.
- No order-placing HTTP call exists anywhere in the adapter ([§AC-05](../PRD.md)).
