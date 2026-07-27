---
status: Accepted
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0006 — Keep the broker session alive with pings and manual daily 2FA re-auth

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** Owner

## Context

The IBKR Client Portal session ([[0002-ibkr-client-portal-api]]) must stay authenticated for the app to read Positions, but IBKR forces a daily logout and uses 2FA. We must decide how the session is maintained and re-authenticated. See [sad.md](../sad.md) §6 (flow 3).

## Decision drivers

- Reliability over cleverness — a stale session must never silently corrupt the portfolio view (sad.md §11).
- Read-only, single-user context — occasional manual action is acceptable (CONTEXT).
- Avoid fragile credential automation.

## Considered options

1. **Keep-alive ping + manual daily 2FA tap** — app pings to hold the session; when IBKR forces re-auth, it alerts the Owner to tap 2FA in the IBKR mobile app.
2. **Fully automated login** — script credentials + defeat/bypass 2FA to avoid any manual step.
3. **Re-login on demand only** — no keep-alive; authenticate right before each read.

## Decision outcome

**Chosen: Option 1.** Keep-alive ping plus a manual daily 2FA tap. Automating 2FA is fragile and works against the broker's security model; a single daily tap is a small, honest cost for a single-user system. The app pings on an interval to hold the session and alerts the Owner via Telegram when a re-auth is required. The last good Positions snapshot is retained so the digest still renders during a lapse.

## Consequences

**Positive**
- Robust and aligned with IBKR's security model; no brittle 2FA automation.
- Owner is explicitly notified when action is needed.

**Negative**
- One manual tap per day (or whenever the broker forces re-auth).
- If the Owner does not re-auth, the portfolio snapshot goes stale (surfaced with a timestamp + alert).

**Neutral**
- Keep-alive interval is configurable; behavior is encapsulated in `IbkrSessionService`.

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §6, §11
- Related ADR: [[0002-ibkr-client-portal-api]]
