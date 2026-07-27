---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "03"
ticket: "N/A — personal project"
---

# PRD — telegram-bot-reporting

> **Inputs (required):** [idea-brief](../../00-overview/idea-brief.md) · [CONTEXT](../../00-overview/CONTEXT.md)
> **Reference module:** N/A — green-field mode.
> **External context channels used:** None — only CONTEXT + idea-brief + sad.md (§5 WiseWizard.Bot, §6 flows 2 and 3) + ADR-0001/0003.

## 1. Context

The Owner runs a lazy, days-to-weeks investing style and wants a 30-second morning read that says, per Ticker, whether to keep holding, pay attention, or review — then trades manually. Per [idea-brief](../../00-overview/idea-brief.md) §2, "manually checking each Ticker daily is time-consuming and inconsistent, so signals are missed and holding/trim decisions are made on stale information." All the research that answers this is produced overnight by the nightly Run; without a delivery-and-interaction surface it stays locked in the database and delivers no value. This feature is that surface: the Owner-facing Telegram bot the single Owner (idea-brief §3) already uses every morning.

Why now: the Owner is running a live IBKR account and building the WiseWizard MVP (idea-brief §4). The nightly-research-pipeline produces Verdicts and the ibkr-portfolio-read feature keeps a current Portfolio, but neither is reachable by the Owner until this bot exists. Telegram is "a natural low-friction channel the Owner already uses" (idea-brief §8 UX), which is why the digest and drill-down are delivered there rather than in a bespoke app.

Accepted vector (idea-brief §13): the MVP is the lazy nightly-digest system whose morning value is "a lazy morning digest with drill-down." This feature realizes the "Telegram bot reporting (digest + drill-down)" slice of the locked-in five-feature MVP (idea-brief §13 locked-in pointer). It is the presentation and interaction layer: it READS what other features produced and never computes a Verdict or reads the Broker itself.

Traceability context: the bot lives inside the single Generic Host process as a hosted service (ADR-0001, `TelegramBotService`) and reads the domain data through repositories over the shared SQLite database (ADR-0003). It presents the Daily digest and drill-down (sad.md §6 flow 2) and delivers self-alerts on Run failure and Broker-session loss (sad.md §6 flow 3). It also hosts the Owner's Watchlist commands (`/watch`, `/unwatch`, `/watchlist`) as the transport surface, invoking the watchlist-management domain, which owns the Watchlist semantics and persistence. Single-Owner authorization is a chat-id allowlist, the accepted debt named in sad.md §11 ("no auth on the bot beyond a chat-id allowlist — fine for single-user").

## 2. Goals

- The Owner reads a Daily digest on demand — one line per Ticker with a Signal and a one-phrase reason — in a single Telegram message, so a morning review takes about 30 seconds (per idea-brief §13 "a lazy morning digest with drill-down").
- The Owner drills into any Ticker to see the full reasoning and the cited Sources behind its Verdict, so a 🟢/🔴 can always be audited rather than blindly trusted (idea-brief §10 risk: over-trust; §13 evidence discipline).
- The Owner sees the current Portfolio and its profit-or-loss on demand, so the morning read is grounded in what is actually held (idea-brief §13 portfolio-tied research).
- The Owner is alerted on the same channel when a Run fails or the Brokerage session needs re-authentication, so a broken night never passes silently (idea-brief §10 risk: session fragility; sad.md §6 flow 3).

## 3. Non-goals

- Computing, scoring, or altering a Verdict, a Signal, or a Position — those are produced by the nightly-research-pipeline and ibkr-portfolio-read features; this feature only reads and renders them (sad.md §5 dependency direction).
- Owning the Watchlist domain — the add/remove/list semantics, validation, and persistence belong to watchlist-management; this feature only carries the `/watch`, `/unwatch`, and `/watchlist` commands to that domain and renders its replies.
- Placing, modifying, or cancelling any order, or any Broker interaction — the System is strictly read-only and the bot never touches the Broker (CONTEXT invariant; idea-brief §5).
- Serving more than one user — a single allowlisted Owner chat; no sharing, tenancy, or multi-user sessions (idea-brief §5, CONTEXT invariant "exactly one Owner").
- On-demand ad-hoc research questions — the System initiates research itself on schedule; the bot never triggers a Run or answers free-form analysis prompts (CONTEXT: System is not an agent that answers ad-hoc questions).

## 4. User stories

### US-01: Read the Daily digest

**As an** Owner
**I want** to request the latest Daily digest and see one line per Ticker with a Signal and a one-phrase reason
**So that** I can review every holding and candidate in about 30 seconds and decide where to look closer.

### US-02: Drill into a Ticker's reasoning

**As an** Owner
**I want** to tap a Ticker in the digest and see its full reasoning with the cited Sources
**So that** I can audit why the System gave that Signal before I act.

### US-03: See the current Portfolio

**As an** Owner
**I want** to request a summary of my current Positions with their profit-or-loss
**So that** my morning read is grounded in what I actually hold right now.

### US-04: Be alerted when a Run fails

**As an** Owner
**I want** the System to message me when the nightly Run does not complete
**So that** I know the morning digest may be missing or stale rather than assuming all is well.

### US-05: Be alerted when re-authentication is needed

**As an** Owner
**I want** the System to message me when the Brokerage session has lapsed and needs a re-authentication tap
**So that** I can restore a fresh Portfolio for the next Run.

### US-06: Be the only one the bot answers

**As an** Owner
**I want** the bot to respond only to me and to reveal nothing to anyone else
**So that** my holdings and research stay private even if a stranger messages the bot.

### US-07: See a clear empty state before the first digest

**As an** Owner
**I want** the bot to tell me plainly when no completed Run exists yet
**So that** I am not shown a blank or misleading digest before the first night has finished.

### US-08: Manage the Watchlist from the same bot

**As an** Owner
**I want** to add, remove, and list Watchlist Tickers through the same bot
**So that** I curate research candidates without leaving the channel I already use.

## 5. Acceptance criteria

### AC-01 (US-01) — happy path

**Given** the Owner is recognized and at least one completed Run exists with Verdicts
**When** the Owner requests the report
**Then** the System presents the Daily digest as one line per Ticker, each line showing the Ticker's Signal and a one-phrase reason, drawn from the latest completed Run.

### AC-02 (US-02) — cross-context, drill-down into full reasoning

**Given** the Owner is recognized and is viewing the Daily digest for the latest completed Run
**When** the Owner opens the details for a Ticker that has a Verdict in that Run
**Then** the System shows that Ticker's full reasoning together with the Sources the pipeline recorded for the Verdict, so the Signal can be audited against its evidence.

### AC-02b (US-02) — cross-context, requested Ticker absent from the Run

**Given** the Owner is recognized and the latest completed Run has no Verdict for a particular Ticker
**When** the Owner opens the details for that Ticker
**Then** the System tells the Owner that this Ticker has no Verdict in the latest report, and shows no reasoning or Sources for it.

### AC-03 (US-03) — happy path, Portfolio summary

**Given** the Owner is recognized and a current Portfolio of one or more Positions exists
**When** the Owner requests the portfolio summary
**Then** the System presents each Position with its holding and its profit-or-loss and notes how current the Portfolio is.

### AC-04 (US-07) — error, no completed Run yet

**Given** the Owner is recognized and no Run has completed yet
**When** the Owner requests the report
**Then** the System tells the Owner plainly that no digest is available yet and presents nothing that resembles Verdicts.

### AC-05 (US-06) — authorization, non-Owner chat

**Given** a chat that is not the Owner's allowlisted chat sends any command to the bot
**When** the System evaluates who is asking
**Then** the System does not act on the command and reveals no Portfolio, Verdict, or Watchlist information, neither confirming nor denying that any such data exists, because only the Owner may interact with the bot.

### AC-06 (US-01) — domain invariant, only the latest completed Run

**Given** the Owner is recognized and a Run is currently in progress while an earlier Run has already completed
**When** the Owner requests the report
**Then** the System shows only the Verdicts from the latest completed Run and never any partial or in-progress Run's results.

### AC-07 (US-04) — happy path, Run-failure alert

**Given** the nightly Run has failed to complete
**When** the System detects the failure
**Then** the System sends the Owner a message on the bot that the Run did not complete, so the Owner knows the latest digest may be missing or stale.

### AC-08 (US-05) — happy path, re-auth alert

**Given** the Brokerage session has lapsed and needs the Owner to re-authenticate
**When** the System detects the lapse
**Then** the System sends the Owner a message on the bot that a re-authentication tap is required to restore a fresh Portfolio.

### AC-09 (US-01) — domain invariant, digest larger than one message

**Given** the Owner is recognized and the latest completed Run holds more Tickers than fit in a single Telegram message
**When** the Owner requests the report
**Then** the System presents the whole digest across as many ordered messages as needed, with every Ticker's line and its details control preserved, and no Ticker silently dropped.

### AC-10 (US-08) — cross-context, Watchlist command carried to its domain

**Given** the Owner is recognized
**When** the Owner sends a command to add, remove, or list Watchlist Tickers
**Then** the System hands the command to the Watchlist domain and presents that domain's outcome back to the Owner, without itself deciding whether the change is valid.

## 6. Non-functional requirements

| Aspect | Target | Measurement |
|---|---|---|
| Digest response latency | ≤ 2 s from the Owner's request to the first digest message sent, p95 | timed span from update received to first send, logged per command |
| Portfolio summary latency | ≤ 2 s from request to summary sent, p95 | timed span from update received to send, logged per command |
| Drill-down response latency | ≤ 1.5 s from the details tap to the detail message sent, p95 | timed span from callback received to send, logged per drill-down |
| Maximum Tickers per digest message | ≤ 20 Ticker lines per message; overflow chunked into ordered messages | line count enforced by the formatter before send |
| Message size ceiling | each sent message ≤ 4000 characters (inside Telegram's per-message limit); longer content split at Ticker or Source boundaries | rendered length checked by the formatter before send |
| Alert delivery latency | Owner alerted within 60 s of the System detecting a Run failure or a session lapse | span from detection to send, logged per alert |
| Delivery reliability | ≥ 99% of triggered alerts and requested digests are delivered or retried to success | delivered ÷ triggered over a rolling 30-day window |

## 6.1 Security / privacy

- **Data classification:** confidential — the digest, drill-down, and Portfolio summary expose the Owner's personal holdings, research conclusions, and interests. All content stays between the Owner's server and the Owner's own Telegram chat.
- **Personal data touched:** no new stored personal fields. The bot reads Positions (Ticker, quantity, market value, unrealized P&L) and Verdicts (Signal, summary, reasoning, cited Sources) produced by other features, and renders them into messages. It may keep a small delivery-log / bot-state record (see data-model) holding only message and Run identifiers and timestamps — no financial values.
- **AuthZ/AuthN impact:** every incoming update — command or details tap — is authorized against a single allowlisted Owner chat identity before any data is read or any reply is composed. A non-allowlisted chat is dropped with no data access. There is no per-user scoping because there is exactly one Owner (CONTEXT invariant). The bot uses only read access to the domain data plus the Watchlist domain's own commands; it never reads the Broker or writes Verdicts or Positions.
- **Abuse cases:**
  - Non-Owner chat sends any command or tap: the System does not act and reveals nothing, neither confirming nor denying that any Portfolio or Verdict exists — existence is hidden to avoid leaking that the Owner uses the bot at all (AC-05).
  - Forged or replayed details tap referencing a Ticker or Run the Owner should not see: the tap is authorized by chat identity first and then resolved only against the latest completed Run; a Ticker with no Verdict in that Run yields a plain "no Verdict" reply, never another Run's data (AC-02b, AC-06).
  - Free-text or crafted symbol in a Watchlist command used to inject markup into a rendered reply: all dynamic values (Ticker symbols, notes, reasoning, Source titles) are escaped before rendering so no user- or Source-supplied text can alter message formatting or embed active content.
  - Message flooding from the Owner's own chat: bounded by the single-Owner design and per-command handling; repeated requests re-read the same latest Run and mutate nothing.
- **Security review:** N/A — M-size feature but read-only over data owned elsewhere, single Owner, internal-only host, no new PII fields, and authorization is the single chat-id allowlist already anticipated by sad.md §11 accepted debt. The one new authorization boundary (the allowlist) is trivial and explicitly designed to hide existence from non-Owners.

## 7. Metrics / KPIs

- **Morning digest engagement** — baseline: 0 (no digest today), target: the Owner reads a per-position Verdict on ≥ 5 of 7 mornings per week within 30 days (directly the idea-brief §7 Approach A outcome metric).
- **Drill-down usage** — baseline: 0, target: the Owner opens details on at least one Ticker on ≥ 3 mornings per week, confirming the evidence discipline is actually used, not just delivered.
- **Alert timeliness** — baseline: TBD (measure over the first two weeks how long a failure or lapse currently goes unnoticed), target: ≥ 95% of Run-failure and session-lapse alerts delivered within 60 s of detection. — baseline measurement plan: during the first two weeks, log detection-to-delivery spans for every triggered alert.
- **Digest response latency (post-release)** — baseline: 0, target: p95 ≤ 2 s from request to first digest message (mirrors §6 for ongoing tracking).

## 8. Open questions

- [ ] Should the re-auth alert repeat or escalate if the Owner does not act within N hours, or fire once per lapse? Default now: a single alert per lapse (carried over from ibkr-portfolio-read §8). — owner: Owner, due: before break-tasks stage 13.
- [ ] When the digest spans multiple messages, should the details controls sit inline per message or in one combined index message? Default now: inline controls per chunk so each Ticker's line carries its own details control. — owner: Owner, due: before break-tasks stage 13.
- [ ] Should the digest surface a stale-Portfolio warning when the latest Run ran on an out-of-date Portfolio snapshot? Default now: show the Portfolio age on the portfolio summary only; leave the digest itself unqualified. — owner: Owner, due: during first production Run review.
- [ ] Should drill-down details for a Ticker be reachable directly by command as well as by tap? Default now: tap only, to keep the interaction model simple. — owner: Owner, due: before first production Run.
