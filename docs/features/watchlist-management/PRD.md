---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "03"
ticket: "N/A — personal project"
---

# PRD — watchlist-management

> **Inputs (required):** [idea-brief](../../00-overview/idea-brief.md) · [CONTEXT](../../00-overview/CONTEXT.md)
> **Reference module:** N/A — green-field mode.
> **External context channels used:** None — only CONTEXT + idea-brief + sad.md.

## 1. Context

The Owner runs a lazy, days-to-weeks investing style and cannot continuously track securities that are not yet held. As the idea-brief §2 notes, "signals are missed and holding/trim decisions are made on stale information" — and that gap is widest for candidates the Owner is only considering, since nothing about them is captured anywhere the System can see. The single Owner (idea-brief §3) needs a way to name the Tickers worth researching so the nightly Run covers them alongside owned Positions.

Why now: the Owner is actively managing a real portfolio and building the WiseWizard MVP (idea-brief §4). The nightly research pipeline analyzes exactly the Universe — the deduplicated union of Portfolio Tickers and Watchlist Tickers (CONTEXT glossary). Without a curated Watchlist there is no way to feed research candidates into that Universe, so this feature is a prerequisite for candidate coverage.

Accepted vector: per idea-brief §13, the MVP is the lazy nightly-digest system whose candidate ideas "come only from the Owner's Watchlist" (idea-brief §5, §13 locked-in pointer). This feature delivers the Owner-curated Watchlist domain: adding, removing, and listing Tickers to research but not yet own.

This PRD owns the Watchlist domain and its persistence: the `WatchlistEntry` model, the repository abstraction, Ticker normalization and validation, and the semantics of the add/remove/list commands. The Telegram delivery surface — how the bot receives and renders the `/watch`, `/unwatch`, and `/watchlist` commands — belongs to the telegram-bot-reporting feature; this PRD only defines the command semantics that surface must honor and the single-Owner chat-id authorization rule the surface must enforce.

## 2. Goals

- The Owner adds a Ticker to the Watchlist with a single command, so the next Run researches it — realizing the idea-brief §13 "candidates come only from the Owner's Watchlist" vector.
- The Owner sees the current Watchlist on demand as a single list, so curation is a deliberate, reviewable act.
- Every entry in the Watchlist is a normalized, valid Ticker with no duplicates, so the Universe stays clean and the nightly Run wastes no effort on malformed or repeated candidates.

## 3. Non-goals

- Automatic market-wide idea discovery or screening — out of scope; candidates come only from the Owner's manual curation (idea-brief §5, CONTEXT out-of-scope).
- Managing Positions or the Portfolio — those are read-only from the Broker and owned by the ibkr-portfolio-read feature; the Watchlist never records owned holdings.
- Serving more than one user — single Owner only, so there is no sharing, tenancy, or per-user Watchlist (idea-brief §5, CONTEXT invariant "exactly one Owner").
- The Telegram transport and message rendering of the commands — owned by the telegram-bot-reporting feature; this feature defines only the domain semantics those commands invoke.

## 4. User stories

### US-01: Add a Ticker to watch

**As an** Owner
**I want** to add a Ticker to the Watchlist by naming its symbol
**So that** the next nightly Run researches it even though I do not own it yet.

### US-02: Remove a Ticker from watch

**As an** Owner
**I want** to remove a Ticker from the Watchlist by naming its symbol
**So that** the System stops spending research effort on a candidate I no longer care about.

### US-03: List the Watchlist

**As an** Owner
**I want** to see every Ticker currently on the Watchlist as one list
**So that** I can review what I am tracking and decide what to add or remove.

### US-04: Attach a short note to a watched Ticker

**As an** Owner
**I want** to record a short optional note when I add a Ticker
**So that** I remember why I started watching it when I review the Watchlist later.

### US-05: Keep the Watchlist clean of duplicates and malformed symbols

**As an** Owner
**I want** the System to reject a malformed symbol and to refuse a Ticker already on the Watchlist
**So that** the Watchlist stays a trustworthy, deduplicated set of research candidates.

### US-06: Keep the Watchlist separate from owned holdings

**As an** Owner
**I want** the System to recognize when a symbol I try to watch is already an owned Position
**So that** the Universe is not padded with a redundant Watchlist copy of something I already hold.

### US-07: Only I can manage my Watchlist

**As an** Owner
**I want** the System to accept Watchlist changes only from me
**So that** no other party can alter what the System researches on my behalf.

## 5. Acceptance criteria

### AC-01 (US-01) — happy path

**Given** the Owner is recognized and a valid symbol is not yet on the Watchlist and is not an owned Position
**When** the Owner adds that symbol to the Watchlist
**Then** the System records the Ticker on the Watchlist, notes the moment it was added, and confirms to the Owner that the Ticker is now watched.

### AC-02 (US-03) — happy path

**Given** the Owner is recognized and the Watchlist holds several Tickers
**When** the Owner asks to see the Watchlist
**Then** the System presents every watched Ticker as a single list, each shown with its note when one exists.

### AC-03 (US-02) — happy path

**Given** the Owner is recognized and a Ticker is currently on the Watchlist
**When** the Owner removes that Ticker
**Then** the System takes the Ticker off the Watchlist and confirms to the Owner that it is no longer watched.

### AC-04 (US-05) — error: malformed symbol

**Given** the Owner is recognized
**When** the Owner tries to add a symbol that is not a well-formed Ticker (for example it is empty, over-long, or contains characters that are not letters, digits, dots, or hyphens)
**Then** the System blocks the addition, adds nothing to the Watchlist, and explains to the Owner that the symbol must be a well-formed Ticker.

### AC-05 (US-02) — error: removing a Ticker that is not watched

**Given** the Owner is recognized and a given Ticker is not on the Watchlist
**When** the Owner tries to remove that Ticker
**Then** the System makes no change and tells the Owner that the Ticker was not on the Watchlist.

### AC-06 (US-07) — authorization

**Given** a party other than the Owner sends a request to add, remove, or list the Watchlist
**When** the System evaluates who is asking
**Then** the System declines to act on the request and makes no change to the Watchlist, because only the Owner may manage the Watchlist.

### AC-07 (US-05) — domain invariant: no duplicate Ticker

**Given** the Owner is recognized and a Ticker is already on the Watchlist
**When** the Owner tries to add the same Ticker again, in any letter casing or with surrounding spacing
**Then** the System keeps a single entry for that Ticker, adds no second copy, and tells the Owner that the Ticker is already watched, honoring the rule that a Ticker appears on the Watchlist at most once.

### AC-08 (US-06) — cross-context: symbol already owned

**Given** the Owner is recognized and a symbol names a Ticker the Owner currently holds as a Position in the Portfolio
**When** the Owner tries to add that symbol to the Watchlist
**Then** the System does not add it to the Watchlist and tells the Owner the Ticker is already an owned Position, so that the Universe is not padded with a redundant Watchlist copy of a held Ticker.

## 6. Non-functional requirements

| Aspect | Target | Measurement |
|---|---|---|
| Watchlist change acknowledgement latency | ≤ 500 ms from the moment the command reaches the domain to the persisted confirmation | domain-side stopwatch logged per add/remove operation |
| Watchlist read (list) latency | ≤ 200 ms for the full Watchlist | domain-side stopwatch logged per list operation |
| Maximum Watchlist size | ≤ 100 Tickers | count enforced by the domain before an add is persisted |
| Maximum note length | ≤ 280 characters | length checked by the domain before an add is persisted |
| Ticker symbol length | 1–10 characters after normalization | validated by the domain before an add is persisted |
| Durability | a recorded Watchlist change survives a process restart | change persisted to the domain database before confirmation |

## 6.1 Security / privacy

- **Data classification:** confidential — the Watchlist reveals the Owner's private research interests and, indirectly, likely future trades.
- **Personal data touched:** no new personal fields; entries hold a Ticker symbol, an added-at moment, and an optional free-text note authored by the Owner.
- **AuthZ/AuthN impact:** every Watchlist command is authorized against a single allowlisted Owner chat identity; any request from a non-allowlisted identity is declined and never reaches the domain mutation. The domain repository serves exactly one Owner's Watchlist — there is no per-user scoping because there is exactly one Owner (CONTEXT invariant).
- **Abuse cases:**
  - Non-Owner attempts a Watchlist command: the System declines and makes no change; the request never mutates domain state (AC-06).
  - Free-text note injection: the note is stored as opaque text and never interpreted; length is capped at 280 characters (NFR §6) so it cannot be used to bloat storage or the digest.
  - Spam adds: bounded by the maximum Watchlist size of 100 Tickers (NFR §6); duplicate adds are idempotent no-ops (AC-07) so repeated commands cannot inflate the list.
- **Security review:** N/A — S-size feature, single Owner, no new personal-data fields, authorization is a single chat-id allowlist already anticipated by sad.md §11 accepted debt.

## 7. Metrics / KPIs

- **Watchlist adoption** — baseline: 0 watched Tickers, target: ≥ 5 Tickers on the Watchlist within 14 days of first use (drives the idea-brief §11 Impact of covering research candidates).
- **Curation activity** — baseline: 0, target: at least one add or remove per week during active use, confirming the Watchlist is maintained rather than set-and-forgotten.
- **Watchlist hygiene** — baseline: TBD (measure over the first two weeks of use), target: 0 duplicate or malformed entries ever persisted, tracked via the count of rejected adds versus persisted entries. — baseline measurement plan: during the first two weeks, log every rejected add and confirm the persisted set stays deduplicated and well-formed.

## 8. Open questions

- [ ] Should removing a Ticker that also exists as an owned Position be allowed even though it was never on the Watchlist? Default now: treat it as "not on the Watchlist" and report so (AC-05). — owner: Owner, due: before break-tasks stage 13.
- [ ] Is a 100-Ticker maximum Watchlist size comfortable given the ~10–20 candidate scale in idea-brief §3? Default now: 100, well above expected use. — owner: Owner, due: before first production Run.
- [ ] Should the System validate that a symbol names a real, tradable security at add time, or accept any well-formed symbol and let the nightly Run surface "no data"? Default now: accept any well-formed symbol; the pipeline tolerates a Ticker with zero fresh documents (idea-brief §9 edge cases). — owner: Owner, due: during data-ingestion PRD.
