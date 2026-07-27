---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "03"
ticket: "N/A — personal project"
---

# PRD — data-ingestion

> **Inputs (required):** [idea-brief](../../00-overview/idea-brief.md) · [CONTEXT](../../00-overview/CONTEXT.md)
> **Reference module:** N/A — green-field mode.
> **External context channels used:** None — only CONTEXT + idea-brief + [sad.md](../../00-overview/sad.md) + [ADR-0003](../../00-overview/adr/0003-sqlite-persistence.md) + [ADR-0004](../../00-overview/adr/0004-hangfire-jobs.md).

## 1. Context

An individual investor with a "lazy" (days-to-weeks horizon) style cannot continuously track news, filings, and fundamentals across all holdings and candidates (idea-brief §2). Before any model can reason about a Ticker, the System needs fresh evidence about it. This feature is the evidence-collection stage: for every Ticker in the current Universe (Portfolio ∪ Watchlist), the System gathers Raw documents from free Sources — SEC EDGAR filings, news RSS feeds, and market/fundamental data — and persists them so the nightly research pipeline can extract facts and judge them. The consumer is the single Owner (idea-brief §3), who never triggers ingestion by hand; the System initiates it on schedule.

Why now: the Owner is actively managing a real IBKR portfolio and wants to reduce daily research effort (idea-brief §4). Signals (a filing, a downgrade, a material event) are missed today because manual checking is time-consuming and inconsistent. Reliable overnight collection of Raw documents is the prerequisite that makes the whole cheap→smart cascade possible; without it there is nothing for the models to analyze.

Accepted vector: Approach A — the lazy nightly-digest MVP over free Sources (idea-brief §13). This feature realizes the "free-sources ingestion" pillar of that MVP: cheap, best-effort collection that tolerates noisy and occasionally unavailable Sources rather than paying for premium feeds. Paid social-media feeds are explicitly out of scope (CONTEXT §Out of scope; idea-brief §5).

Traceability context: sad.md §5 places each Source behind a Core interface (`ISecFilingsSource`, `INewsSource`, `IMarketDataSource`) under `Infrastructure/Sec`, `/News`, `/Market`, so adding a Source is a new implementation (Open/Closed). sad.md §8 fixes the dedup rule: Raw documents are deduped by content hash within a Run. Ingestion is the first step of the Hangfire nightly chain (ADR-0004); this feature owns that step and the Source clients, and hands `raw_documents` keyed to a `run_id` to the nightly-research-pipeline feature.

## 2. Goals

- The System collects fresh Raw documents for every Ticker in the current Universe each night, without any manual action by the Owner (idea-brief §13 — "a nightly, cheap batch pipeline gathers data from free public sources").
- Collection is resilient: an unavailable or rate-limiting Source degrades gracefully, recording the gap and letting other Sources and other Tickers proceed (idea-brief §13; sad.md §1 QG-2).
- Collected evidence is clean and auditable: no duplicate Raw document within a Run, every document carries its origin Source and link, so downstream Verdicts can cite it (CONTEXT invariant — "every Verdict must cite the Raw documents that informed it").

## 3. Non-goals

- Analyzing, filtering, or extracting facts from Raw documents — that is the nightly-research-pipeline feature; ingestion only collects and persists.
- Paid social-media feeds (X/Twitter, etc.) — out of scope, expensive and fragile versus free secondhand news (idea-brief §5; CONTEXT §Out of scope).
- Automatic market-wide idea discovery — Tickers come only from the Owner's Universe, never from a market-wide scan (idea-brief §5).
- Choosing which Tickers form the Universe — that is owned by ibkr-portfolio-read (Portfolio) and watchlist-management (Watchlist); ingestion consumes the Universe as given.

## 4. User stories

### US-01: Collect documents for each Ticker

**As the** Owner
**I want** the System to collect fresh Raw documents from every Source for each Ticker in the Universe each night
**So that** the morning digest is based on the latest filings, news, and market data rather than stale information.

### US-02: Continue when a Source fails

**As the** Owner
**I want** the System to skip a Source that is unreachable or rate-limiting and record the gap, then continue with the remaining Sources and Tickers
**So that** one flaky free Source never blocks the whole night's collection.

### US-03: Respect Source access terms

**As the** Owner
**I want** the System to identify itself and stay within each Source's allowed access rate
**So that** WiseWizard keeps its free access and is never blocked for abusing a Source.

### US-04: No duplicate evidence within a Run

**As the** Owner
**I want** the System to store each distinct Raw document only once per Run
**So that** the same article or filing is not double-counted when the models weigh the evidence.

### US-05: Only ingest Tickers in the Universe

**As the** Owner
**I want** the System to collect documents only for Tickers I actually hold or watch
**So that** collection cost and noise stay bounded to what is relevant to me.

### US-06: Bound freshness and volume per Source

**As the** Owner
**I want** the System to collect only recent documents up to a capped count per Source per Ticker
**So that** a single night stays cheap and fast and does not drown the models in old or excessive material.

### US-07: Retain evidence for auditing, then clean up

**As the** Owner
**I want** old Raw documents kept long enough to audit recent Verdicts and then removed automatically
**So that** I can trace a conclusion to its evidence without the store growing without bound.

## 5. Acceptance criteria

### AC-01 (US-01) — happy path

**Given** the Owner's Universe contains a Ticker and the free Sources are reachable
**When** the System runs ingestion for that Ticker during a Run
**Then** the System records fresh Raw documents from each available Source for that Ticker, each tagged with its origin Source, its link, and the Run it belongs to.

### AC-02 (US-02) — error: a Source is unreachable or rate-limited

**Given** the System is ingesting a Ticker and one Source is unreachable or has signalled that its allowed access rate is exceeded
**When** the System attempts to collect from that Source
**Then** the System skips that Source for that Ticker, records the gap as a collection failure for the Run, and continues collecting from the remaining Sources and Tickers without aborting the Run.

### AC-03 (US-03) — authorization: respect Source access terms

**Given** the System is about to collect from a Source that grants free access only to callers who declare their identity and stay within an allowed request rate
**When** the System requests documents from that Source
**Then** the System presents its declared identity and paces its requests within the Source's allowed access rate, and if the Source signals that access is being exceeded the System backs off rather than continuing to request.

### AC-04 (US-04) — domain invariant: no duplicate within a Run

**Given** a Raw document with identical content has already been recorded for a Ticker earlier in the same Run
**When** the System collects the same document again from a Source during that Run
**Then** the System recognizes it as a duplicate by its content and does not record a second copy for that Run, honoring the invariant that no duplicate Raw document exists within a Run.

### AC-05 (US-05) — cross-context: only Universe Tickers

**Given** a Ticker that is neither in the Owner's Portfolio nor in the Owner's Watchlist
**When** the System assembles the set of Tickers to ingest for a Run
**Then** the System collects documents only for Tickers in the current Universe and never collects for a Ticker outside the Portfolio and Watchlist.

### AC-06 (US-06) — domain invariant: freshness and volume bounds

**Given** a Source returns many documents for a Ticker, some older than the collection lookback window and more than the allowed count
**When** the System collects from that Source for that Ticker
**Then** the System keeps only documents published within the lookback window, up to the allowed maximum count per Source per Ticker, and discards the rest.

### AC-07 (US-01) — happy path: zero fresh documents

**Given** a Ticker in the Universe for which no Source has any new document within the lookback window
**When** the System runs ingestion for that Ticker
**Then** the System records that the Ticker was ingested with no fresh documents and continues, without treating the empty result as a failure.

### AC-08 (US-07) — domain invariant: retention

**Given** Raw documents older than the configured retention window exist in the store
**When** the System performs its retention cleanup
**Then** the System removes Raw documents older than the retention window while keeping documents within it available for auditing.

## 6. Non-functional requirements

| Aspect | Target | Measurement |
|---|---|---|
| Per-Ticker ingest time budget | ≤ 30 s across all three Sources for one Ticker | wall-clock per-Ticker ingest duration logged with `run_id` + `ticker` |
| Whole-Universe ingest budget | ≤ 20 min for a ~40-Ticker Universe | wall-clock ingest-step duration logged with `run_id` |
| Max documents per Source per Ticker | ≤ 15 documents kept per Source per Ticker per Run | count of `raw_documents` rows grouped by `run_id`, `ticker`, `source` |
| Lookback window | last 14 days of filings/news (market/fundamental data = latest snapshot) | `published_at` of kept documents vs Run start; assertion in ingest step |
| Polite request rate — SEC EDGAR | ≤ 10 requests/second, declared User-Agent on every request | request-rate counter logged per Source; SEC's published 10 req/s ceiling |
| Polite request rate — news RSS / market data | ≤ 1 request/second per host, single concurrent request per host | request-rate counter logged per Source host |
| Source failure isolation | a single Source failure affects 0 other Sources and 0 other Tickers | fault-injection test asserts other Sources still persist documents |
| Retention window | Raw documents kept 90 days, then removed by cleanup | age of oldest `raw_documents` row after cleanup job |

## 6.1 Security / privacy

- **Data classification:** public — all collected Raw documents come from public free Sources (SEC EDGAR, public RSS, public market data); no Owner personal data is stored in `raw_documents`.
- **Personal data touched:** none new. The Universe (which Tickers the Owner holds/watches) is sensitive but is owned by ibkr-portfolio-read / watchlist-management; ingestion reads it but stores only the Ticker symbol alongside each document.
- **AuthZ/AuthN impact:** none inbound — ingestion is System-initiated with no Owner-facing surface. Outbound, the System authenticates itself to SEC EDGAR by declaring a contact User-Agent (fair-access requirement); no credentials are stored for RSS or market data.
- **Abuse cases:**
  - Source blocks WiseWizard for exceeding access terms: the System paces requests within each Source's allowed rate and backs off on rate-limit signals (AC-03) so it is not blocked.
  - SSRF / injection via a Source-supplied URL or document body: Source URLs are fixed/allowlisted per Source implementation and never taken from Owner input; document content is stored as opaque text and never executed or rendered as markup by ingestion.
  - Poisoned or oversized document floods the store: per-Source per-Ticker document cap and lookback window (AC-06) bound how much any one Source can inject per Run.
  - Duplicate flooding inflates evidence weight: content-hash dedup within a Run (AC-04) prevents the same content from being recorded twice.
- **Security review:** Required — M-size feature that makes outbound calls to external Sources and persists external content; review focuses on rate-limit compliance and content-handling safety.

## 7. Metrics / KPIs

- **Ticker coverage per Run** — baseline: 0, target: ≥ 95% of Universe Tickers have at least one Source successfully collected (not skipped) per Run within 30 days of go-live.
- **Fresh-document yield** — baseline: 0, target: ≥ 80% of Universe Tickers have ≥ 1 fresh Raw document per Run (measured over a rolling 7-Run window); a persistently lower yield flags a Source-quality open question.
- **Source failure rate** — baseline: TBD (measure over first 2 weeks of nightly Runs by logging skip events per Source), target: < 5% of (Source × Ticker) collection attempts skipped due to Source failure per Run.
- **Dedup effectiveness** — baseline: 0, target: 0 duplicate (same content hash) Raw documents persisted within any single Run.

## 8. Open questions

- [ ] Which specific free news RSS feeds give the best signal/noise for a Ticker's news? Default now: a small curated set of general financial-news RSS feeds queried per Ticker symbol, tuned after observing fresh-document yield. — owner: Owner, due: before first production Run
- [ ] Is an unofficial free market-data source (e.g. Yahoo Finance) reliable enough for the nightly snapshot, or is a free-tier API key needed? Default now: use one unofficial free source behind `IMarketDataSource`, swap to a free-tier API if the source-failure rate exceeds target. — owner: Owner, due: before first production Run
- [ ] Is a 14-day lookback and 15-doc-per-Source cap the right cost/quality balance? Default now: 14 days / 15 docs, revisit after measuring fresh-document yield. — owner: Owner, due: after first 2 weeks of nightly Runs
- [ ] Is 90-day retention sufficient for auditing "what changed since the previous Run" over longer gaps? Default now: 90 days. — owner: Owner, due: before first production Run
