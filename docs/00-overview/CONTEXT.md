---
status: Living
updated_at: "2026-07-26"
---

# Domain Context — WiseWizard

<!--
CONTEXT.md = domain glossary, not SPEC. NO implementation details here — only
domain words and boundaries. This is the canonical vocabulary that every feature
PRD, ADR, and data model must use verbatim. If a feature doc contradicts a term
here, this file wins.
-->

## Glossary

### Actors / roles

- **Owner** — the single human user of the system; owns the brokerage account, the watchlist, and receives all reports. NOT a multi-tenant "user" — WiseWizard is a single-owner system with exactly one Owner.
- **System** — the automated WiseWizard application acting on schedule without human prompting. NOT an agent that answers ad-hoc questions; it initiates research itself.

### Portfolio domain

- **Position** — a holding the Owner currently owns in the brokerage account: a Ticker with a quantity, average cost, market value, and unrealized P&L. NOT a trade or an order — WiseWizard never places orders.
- **Portfolio** — the full set of the Owner's current Positions at a point in time. NOT a target/model portfolio — it is the actual live state read from the broker.
- **Ticker** — the stock/ETF symbol identifying a security (e.g. `AAPL`, `VOO`). NOT a company name; the canonical key for grouping data.
- **Broker** — the external brokerage (Interactive Brokers) holding the Owner's account. Accessed read-only. NOT a trading venue WiseWizard sends orders to.
- **Brokerage session** — an authenticated, live connection to the Broker's local gateway, kept alive by periodic pings and re-authenticated manually by the Owner when the Broker forces daily logout. NOT a stateless API key.

### Watchlist domain

- **Watchlist** — the Owner-curated list of Tickers to research but not (yet) owned. NOT an automatic market scanner — the Owner adds/removes entries manually.
- **Universe** — the deduplicated union of Portfolio Tickers + Watchlist Tickers that a single Run analyzes. NOT the whole market; bounded by what the Owner holds or watches.

### Research domain

- **Run** — one complete nightly execution of the research pipeline over the Universe, identified by a run id, with a start/finish time and status. NOT a single API call; a Run orchestrates many steps.
- **Raw document** — a single unprocessed item collected from a Source during ingestion (a news article, a filing, a metrics snapshot), keyed to a Ticker. NOT an analyzed conclusion.
- **Source** — an external origin of Raw documents: SEC EDGAR filings, news RSS feeds, or market/fundamental data. NOT a paid social-media feed (explicitly out of scope for MVP).
- **Extracted fact** — a structured statement distilled by the cheap-tier model from one Raw document: what was said about a Ticker, its sentiment, and how material it is. NOT the Owner-facing conclusion.
- **Verdict** — the per-Ticker conclusion produced by the synthesis-tier model for a Run: a Signal, a one-line summary, full reasoning, cited sources, and what changed since the previous Run. NOT a buy/sell order — it is advisory only.
- **Signal** — the traffic-light classification of a Verdict: 🟢 hold, 🟡 attention, 🔴 review. NOT a numeric price target.
- **Daily digest** — the short Owner-facing message summarizing all Verdicts for the latest Run, one line per Ticker, delivered to Telegram. NOT the full reasoning — that is revealed on drill-down.
- **Model cascade** — the tiered LLM pipeline: cheap-tier model filters/extracts at high volume, synthesis-tier model judges at low volume. NOT a single-model call.
- **Batch job** — an asynchronous bulk LLM request submitted to the model provider and polled for completion, used to cut cost on the nightly Run. NOT a synchronous real-time request.

## Invariants

- The System can never place, modify, or cancel an order at the Broker — all Broker access is strictly read-only.
- Every Verdict must cite at least the Raw documents (Sources) that informed it; a Verdict with no evidence is invalid.
- Exactly one Owner exists; the System never serves an anonymous or second user.
- A Verdict always belongs to exactly one Run and one Ticker; the "previous" Verdict for a Ticker is the one from the prior completed Run.
- Only Tickers in the current Universe (Portfolio ∪ Watchlist) are analyzed in a Run; nothing outside it.
- A Run must survive a process restart — its progress and any in-flight Batch jobs are persisted and resumable.
- The Daily digest reports only Verdicts from the latest completed Run.

## Out of scope

- **Order execution / trading** — the Owner acts manually in the Broker's own app; WiseWizard only informs. Reason: keeps the System read-only and removes the entire class of financial-loss risk.
- **Paid social-media feeds (X/Twitter, etc.)** — expensive and fragile; important opinions are captured secondhand through news Sources. Reason: preserves the "cheap, free-sources" philosophy.
- **Automatic market-wide idea discovery / screening** — candidates come only from the Owner's Watchlist. Reason: bounds cost and scope; belongs to a later research-platform phase.
- **Multi-agent committee architecture (Macro/Sector/Bull/Bear/etc.)** — the MVP uses a two-tier model cascade, not specialized debating agents. Reason: the full fund-desk vision is a future phase, not the MVP.
- **Multi-user / SaaS** — single Owner only. Reason: no auth, tenancy, or billing complexity in MVP.
