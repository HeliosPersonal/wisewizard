---
status: Accepted
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0002 — Read portfolio via the IBKR Client Portal API (read-only)

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** Owner

## Context

WiseWizard needs the Owner's current Positions from Interactive Brokers. IBKR offers no stateless cloud REST key; every API requires a live authenticated session held by a local gateway process. We must pick which IBKR API surface to use, and confirm scope. See [sad.md](../sad.md) §3.

## Decision drivers

- HTTP/JSON simplicity and debuggability (sad.md §2).
- Read-only invariant — the System must never place orders (CONTEXT invariant).
- Runs on the Owner's own server next to the gateway (sad.md §7).

## Considered options

1. **Client Portal API** — a local Java gateway (`clientportal.gw`) exposing REST on `localhost`; consumed with `HttpClient`.
2. **TWS API / IB Gateway** — socket protocol via the desktop gateway; in .NET via the official C# TWS client library.
3. **Third-party aggregator (e.g. Plaid-style)** — outsource brokerage read access.

## Decision outcome

**Chosen: Option 1.** Client Portal API. Reading positions is a couple of GET calls over plain HTTP/JSON, which is far simpler to build and debug than the socket-based TWS protocol, and needs no third-party data sharing. Access is strictly read-only.

## Consequences

**Positive**
- Plain HTTP/JSON — easy to consume and test with `HttpClient`.
- No extra third party; data stays on the Owner's server.
- Read-only endpoints only; no order surface wired up.

**Negative**
- Requires the local gateway process running alongside the app.
- Session must be kept alive and periodically re-authenticated (see [[0006-manual-2fa-keepalive]]).

**Neutral**
- If richer/real-time data is ever needed, the official .NET TWS API remains available behind the same `IBrokerReader` interface.

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §3, §4
- Related ADR: [[0006-manual-2fa-keepalive]]
