---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "03"
ticket: "N/A — personal project"
---

# PRD — ibkr-portfolio-read

> **Inputs (required):** [idea-brief](../../00-overview/idea-brief.md) · [CONTEXT](../../00-overview/CONTEXT.md)
> **Reference module:** N/A — green-field mode.
> **External context channels used:** None — only CONTEXT + idea-brief + sad.md + ADR-0001/0002/0006.

## 1. Context

The Owner actively manages a real Interactive Brokers account and wants a nightly research digest that reflects what is *actually* held right now, not stale or hand-typed holdings. Per [idea-brief](../../00-overview/idea-brief.md) §2, an individual investor with a "lazy" (days-to-weeks) style cannot continuously reconcile their holdings, so any research built on an out-of-date Portfolio silently misleads. This feature gives the System a trustworthy, read-only picture of the Owner's current Positions — the Portfolio half of the Universe that every Run analyzes. The consumer is the single Owner (idea-brief §3), who holds roughly 10-20 Positions and checks a morning digest.

Why now: the Owner is running a live IBKR account today (idea-brief §4) and the downstream nightly Run cannot start until the Portfolio can be read reliably. This is the broker-integration feature and a hard prerequisite for data-ingestion and the nightly-research-pipeline, which need the Portfolio Tickers to form the Universe.

Accepted vector (idea-brief §13): a single-process System reads the Portfolio strictly read-only and keeps the picture fresh. This feature realizes the "IBKR portfolio read (read-only)" slice of the locked-in five-feature MVP. Access is via the Broker's local gateway; the Brokerage session is held alive by periodic pings and re-authenticated by the Owner via a manual daily 2FA tap when the Broker forces a logout.

Traceability context: the Broker is accessed only through read-only capabilities (ADR-0002); no order-placement capability is wired into the System at all. The Brokerage session lifecycle — keep-alive ping plus manual 2FA re-auth with a Telegram alert — is fixed by ADR-0006, and the whole thing lives inside the single Generic Host process as a hosted service (ADR-0001, `IbkrSessionService`).

## 2. Goals

- The System always has the Owner's current Positions available for a Run, refreshed on its own without the Owner entering anything by hand (per idea-brief §13 "IBKR portfolio read (read-only)").
- The System holds the Brokerage session live on its own between Runs, so a fresh Portfolio is normally ready without the Owner acting (idea-brief §8 UX: daily re-auth reduced to a single tap).
- When the Portfolio cannot be refreshed, the System tells the Owner exactly why and keeps showing the last known-good Portfolio with its age, so the Owner is never silently misled (idea-brief §10 risk: session fragility).

## 3. Non-goals

- Placing, modifying, or cancelling any order — the Owner trades manually in the Broker's own app (idea-brief §5 out of scope; hard read-only invariant from CONTEXT).
- Real-time / intraday streaming of Positions — a snapshot per Run is sufficient for a nightly, days-to-weeks-horizon digest (idea-brief §3 frequency is daily).
- Automating the 2FA step or storing Broker credentials to bypass the daily logout — deliberately avoided as fragile and against the Broker's security model (ADR-0006).
- Building the Universe or the Watchlist — deduplicating Portfolio Tickers with Watchlist Tickers belongs to the watchlist-management and pipeline features (CONTEXT: Universe).

## 4. User stories

### US-01: Refresh the Portfolio automatically

**As an** Owner
**I want** the System to read my current Positions from the Broker on its own before each Run
**So that** the nightly research reflects what I actually hold today without me typing anything

### US-02: Keep the Brokerage session alive

**As an** Owner
**I want** the System to hold the Brokerage session live between Runs
**So that** a fresh Portfolio is normally ready without me having to act

### US-03: Be alerted when re-authentication is needed

**As an** Owner
**I want** to be told when the Broker has forced a logout and my Positions can no longer be refreshed
**So that** I can tap 2FA in the Broker's app and restore a fresh Portfolio

### US-04: See a trustworthy Portfolio with its age

**As an** Owner
**I want** the last known-good Positions kept and labelled with how old they are whenever a fresh read is not possible
**So that** I am never silently shown a stale Portfolio as if it were current

### US-05: Trust that the System never trades

**As an** Owner
**I want** the System to only ever read from the Broker
**So that** there is no possibility of the System placing or altering an order on my account

### US-06: Read Positions with their money figures

**As an** Owner
**I want** each Position to carry its quantity, average cost, market value and unrealized profit-or-loss
**So that** the research and digest can reason about the size and standing of each holding

## 5. Acceptance criteria

### AC-01 (US-01) — happy path

**Given** the Brokerage session is live and the Owner holds one or more Positions at the Broker
**When** the System refreshes the Portfolio ahead of a Run
**Then** the System records a fresh snapshot of the Owner's current Positions and marks the Portfolio as current as of that moment.

### AC-02 (US-06) — happy path, Position detail

**Given** the Brokerage session is live and the Owner holds a Position in a given Ticker
**When** the System refreshes the Portfolio
**Then** the recorded Position for that Ticker carries its quantity, average cost, market value and unrealized profit-or-loss.

### AC-03 (US-04) — error, refresh fails

**Given** the last refresh produced a known-good Portfolio and a later refresh cannot reach the Broker
**When** the System attempts to refresh the Portfolio
**Then** the System keeps the last known-good Positions, records that this refresh did not succeed, and leaves the Portfolio labelled with the time it was last current so the Owner can see its age.

### AC-04 (US-03) — error, session lapsed with alert

**Given** the Brokerage session has lapsed because the Broker forced a daily logout
**When** the System attempts to keep the session alive or to refresh the Portfolio
**Then** the System stops pinging, tells the Owner that a re-authentication tap is required, and resumes keeping the session alive once the Owner has re-authenticated.

### AC-05 (US-05) — authorization / capability boundary

**Given** the System is connected to the Broker
**When** any part of the System interacts with the Broker
**Then** the System is only ever able to read account and Position information and can never place, change, or cancel an order.

### AC-06 (US-04) — domain invariant, single current snapshot

**Given** a known-good Portfolio already exists from an earlier refresh
**When** the System records a newer successful refresh
**Then** the Portfolio reflects exactly one current set of Positions — the newest successful snapshot fully replaces the previous one, with no duplicate or leftover holdings from before.

### AC-07 (US-04) — domain invariant, empty Portfolio

**Given** the Brokerage session is live and the Owner holds no Positions at the Broker
**When** the System refreshes the Portfolio
**Then** the System records an empty but current Portfolio, distinct from a Portfolio that simply could not be refreshed.

### AC-08 (US-01) — cross-context, Portfolio feeds the Universe

**Given** the System has a current Portfolio
**When** a Run is about to begin its analysis
**Then** the Tickers of the current Positions are made available to form the Portfolio part of the Universe the Run will analyze.

### AC-09 (US-02) — happy path, session held between Runs

**Given** the Brokerage session is live and no Run is in progress
**When** the interval between keep-alive actions elapses
**Then** the System keeps the session live on its own so that the next refresh normally succeeds without the Owner acting.

## 6. Non-functional requirements

| Aspect | Target | Measurement |
|---|---|---|
| Keep-alive ping interval | every 60 s while the session is live | interval configured on `IbkrSessionService`; logged ping timestamps |
| Portfolio-read latency | ≤ 5 s from refresh start to snapshot persisted, p95 | timed span around a refresh, logged with `run_id` |
| Snapshot staleness threshold | Portfolio flagged stale to the Owner if older than 20 h at read time | age = read time − `as_of`; surfaced in digest/detail |
| Re-auth alert latency | Owner alerted within 2 min of the session being detected as lapsed | span from lapse-detection to Telegram send, logged |
| Session-recovery detection | live session re-detected within 60 s of the Owner completing 2FA | keep-alive resumes on next ping cycle after re-auth |
| Refresh reliability | ≥ 99% of refreshes attempted while the session is live persist a snapshot | successful-refresh count ÷ attempted-while-live, over a rolling 30-day window |

## 6.1 Security / privacy

- **Data classification:** confidential — the Portfolio is the Owner's personal financial holdings; it stays on the Owner's own server and is never shared with a third party.
- **Personal data touched:** the Owner's Positions (Ticker, quantity, avg_cost, market_value, unrealized_pnl, as_of) and Brokerage session state. No new PII fields beyond financial holdings; single Owner, no other person's data.
- **AuthZ/AuthN impact:** access to the Broker is via a live, Owner-authenticated Brokerage session held by the local gateway; the System uses only read-only Broker capabilities (ADR-0002). No order surface is bound. The gateway REST endpoint is bound to `localhost` only (sad.md §7).
- **Abuse cases:**
  - Accidental write to the Broker: prevented by design — only read capabilities are implemented; there is no code path that can place an order (AC-05).
  - Stale Portfolio silently used as current: prevented — refresh failure keeps the last known-good snapshot but preserves its `as_of` so the Owner sees its age (AC-03), and it is flagged stale past the staleness threshold (§6).
  - Broker credentials leaked from source/config: credentials never live in the System — the Owner authenticates 2FA in the Broker's own app; the System holds no password (ADR-0006).
  - Session-state or Portfolio DB exposed off-host: mitigated — single-process app and gateway on the same host; DB on local disk; dashboard/gateway bound to `localhost`.
- **Security review:** N/A — S-size, single Owner, internal-only host, no new PII beyond the Owner's own holdings, and read-only Broker access removes the trading-risk surface.

## 7. Metrics / KPIs

- **Portfolio freshness at Run start** — baseline: 0 (no automated read today), target: ≥ 95% of Runs begin with a Portfolio less than 24 h old.
- **Manual data entry eliminated** — baseline: manual (Owner would otherwise transcribe holdings), target: 0 manual Position entries per Run.
- **Re-auth turnaround** — baseline: TBD (measure over first 2 weeks how long a lapse persists), target: session restored within 4 h of the alert on ≥ 90% of forced logouts.
- **Refresh success rate** — baseline: 0, target: ≥ 99% of refreshes attempted while the session is live persist a snapshot (mirrors §6 reliability NFR for post-release tracking).

## 8. Open questions

- [ ] Is a 60 s keep-alive ping interval comfortably inside the Broker's idle-timeout, or should it be shorter? Default now: 60 s. — owner: Owner, due: before first production Run.
- [ ] What is the exact staleness threshold at which the digest should downgrade or suppress a Portfolio-dependent Verdict? Default now: flag-only at 20 h, never suppress. — owner: Owner, due: during nightly-research-pipeline PRD.
- [ ] Should the re-auth alert repeat / escalate if the Owner does not act within N hours? Default now: single alert per lapse. — owner: Owner, due: during telegram-bot-reporting PRD.
