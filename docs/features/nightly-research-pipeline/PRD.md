---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "03"
ticket: "N/A — personal project"
---

# PRD — nightly-research-pipeline

> **Inputs (required):** [idea-brief](../../00-overview/idea-brief.md) · [CONTEXT](../../00-overview/CONTEXT.md)
> **Reference module:** N/A — green-field mode.
> **External context channels used:** None — only CONTEXT + idea-brief + [sad.md](../../00-overview/sad.md) (§4 seeds 2+3, §6 flow 1, §10 QG-1/QG-2/QG-3) + [ADR-0005](../../00-overview/adr/0005-model-cascade-batch-api.md), [ADR-0004](../../00-overview/adr/0004-hangfire-jobs.md), [ADR-0003](../../00-overview/adr/0003-sqlite-persistence.md).

## 1. Context

An individual investor with a "lazy" (days-to-weeks horizon) style cannot continuously track news, filings, and fundamentals across every holding and candidate (idea-brief §2). Manually checking each Ticker daily is time-consuming and inconsistent, so material signals — a filing, a downgrade, an event — are missed and holding/trim decisions are made on stale information. The single Owner is a technically capable investor with a modest Portfolio (~10-20 Positions) plus a Watchlist of similar size, who checks a digest each morning and trades manually (idea-brief §3). This feature is the heart of the System: it is the automated research engine that turns raw evidence into an evidence-backed, per-Ticker Verdict overnight.

The enabling trigger is that batch LLM APIs now make bulk overnight analysis cheap enough for a personal budget (idea-brief §4). Without an automated nightly pipeline the Owner has no continuous, evidence-disciplined research over the exact set of Tickers they hold or watch — the gap none of the competitors fill (idea-brief §6).

The accepted vector is Approach A — the lazy nightly-digest MVP (idea-brief §13): the System reads the Universe, runs an overnight cheap→smart Model cascade over the Batch API, and persists a Verdict per Ticker for the morning Daily digest. This PRD covers the orchestration and research core: reading the Universe, triggering ingestion, running the two-tier Model cascade over the Batch API, computing the delta versus the previous Run, and persisting Extracted facts and Verdicts — restart-safely.

Traceability context for the "how" (informs §1, not §5 AC): the nightly Run is orchestrated as a persisted Hangfire chain that survives restarts and resumes in-flight Batch jobs (sad.md §4 seed 2, ADR-0004); the two-tier cascade pushes bulk token volume to the cheap tier and reserves the synthesis tier for distilled judgment, all over the asynchronous Batch API (sad.md §4 seed 3, ADR-0005); Run state and Batch ids are persisted in SQLite for resumability (ADR-0003, ADR-0004). Pipeline logic is unit-tested against saved LLM fixtures with zero network (sad.md §5).

## 2. Goals

- The System produces a Verdict for every Ticker in the Universe each night, without the Owner initiating or supervising it (idea-brief §13 — "overnight cheap→smart model cascade over portfolio + watchlist").
- Every Verdict carries a Signal, a one-line summary, full reasoning, its cited Sources, and what changed since the previous Run — so the Owner can audit any conclusion (idea-brief §13; CONTEXT invariant on evidence).
- A nightly Run stays within a small personal budget by pushing the bulk of token volume to the cheap tier and using asynchronous Batch jobs (idea-brief §13; sad.md §10 QG-1).
- A Run survives a process restart and resumes in-flight Batch jobs without repeating completed work or corrupting the previous Run's Verdicts (idea-brief §13; sad.md §10 QG-2).

## 3. Non-goals

- Order execution — the System never converts a Verdict into a trade; the Owner acts manually in the Broker's app (idea-brief §5; CONTEXT invariant).
- Ad-hoc, on-demand analysis — the System initiates research itself on schedule and does not answer Owner questions between Runs (CONTEXT: System "initiates research itself"; idea-brief §5).
- Collecting Raw documents — ingestion from Sources is owned by the data-ingestion feature; this pipeline consumes the Raw documents it produced for the Run.
- Reading the Portfolio and managing the Watchlist — the Universe is supplied by the ibkr-portfolio-read and watchlist-management features; this pipeline only reads their combined output.
- A multi-agent committee (Macro/Sector/Bull/Bear) or a full thesis-history memory — the MVP is a two-tier cascade with a one-step delta only (idea-brief §5; sad.md §11 accepted debt).

## 4. User stories

### US-01: Run nightly research over the Universe

**As the** Owner
**I want** the System to run a complete research Run over the whole Universe every night on schedule
**So that** I wake up to fresh conclusions covering exactly the Tickers I hold or watch, without asking for anything.

### US-02: Get a per-Ticker Verdict with a Signal and summary

**As the** Owner
**I want** the System to produce, for each Ticker in the Universe, a Verdict with a Signal and a one-line summary
**So that** I can scan a traffic-light view of my whole Universe in seconds.

### US-03: See the evidence behind every Verdict

**As the** Owner
**I want** the System to attach to every Verdict the specific Sources it relied on
**So that** I can audit any conclusion rather than trusting an unexplained 🟢/🔴.

### US-04: See what changed since the previous Run

**As the** Owner
**I want** the System to state, per Ticker, what changed since the previous completed Run
**So that** I can focus my 30 seconds on Tickers where something actually moved.

### US-05: Keep spend within a personal budget

**As the** Owner
**I want** the System to keep each Run within a configured cost ceiling and record what each Run cost
**So that** running nightly research never surprises my personal budget.

### US-06: Never lose a night to a crash or a stuck batch

**As the** Owner
**I want** the System to survive a process restart mid-Run and resume any in-flight Batch job
**So that** an overnight restart or a slow provider does not silently skip a night's research or duplicate work.

### US-07: Be alerted and protected when a Run fails

**As the** Owner
**I want** the System to fail a broken Run cleanly, alert me, and keep the previous Run's Verdicts available
**So that** I still have yesterday's evidence-backed view and know a night was missed.

### US-08: Trust that the System only informs, never trades

**As the** Owner
**I want** the System to treat every Verdict as advisory only and never turn it into an order
**So that** the research engine can never touch my money.

## 5. Acceptance criteria

### AC-01 (US-01, US-02) — happy path

**Given** the Universe contains one or more Tickers and fresh Raw documents have been collected for the Run
**When** the scheduled nightly Run executes over the Universe
**Then** the System produces exactly one Verdict per Ticker in the Universe, each carrying a Signal and a one-line summary, marks the Run finished, and makes those Verdicts the latest available for the morning Daily digest.

### AC-02 (US-04) — happy path, delta present

**Given** a Ticker has a Verdict from the previous completed Run
**When** the current Run produces a new Verdict for that Ticker
**Then** the Verdict states what changed since the previous Run's Verdict for the same Ticker.

### AC-03 (US-07) — error path, batch failure

**Given** a Run is in progress and its previous completed Run's Verdicts are available to the Owner
**When** a Batch job for that Run fails or does not complete within the allowed Run time
**Then** the System ends the Run in a failed state without writing any partial or corrupted Verdicts, alerts the Owner that the night's Run failed, and leaves the previous Run's Verdicts intact and available.

### AC-04 (US-08) — authorization / advisory-only invariant

**Given** the System has produced a Verdict with a 🔴 review Signal for a Ticker the Owner holds
**When** the Run completes and persists that Verdict
**Then** the System records the Verdict as advisory information only and never places, modifies, or cancels any order at the Broker as a result of it.

### AC-04b (US-01) — authorization / only the scheduled System initiates a Run

**Given** the Owner is interacting with the System between scheduled Runs
**When** the Owner asks the System an ad-hoc research question
**Then** the System does not start a Run or answer the ad-hoc question, because only the scheduled System initiates a Run.

### AC-05 (US-03) — domain invariant, evidence required

**Given** the synthesis tier has proposed a conclusion for a Ticker but references no Raw document as evidence
**When** the System assembles that Ticker's Verdict
**Then** the System treats the evidence-less conclusion as invalid, blocks it from being persisted as a Verdict, and records that the Ticker had no citable evidence rather than publishing an unsupported Verdict.

### AC-06 (US-04) — cross-context, no previous Verdict

**Given** a Ticker is in the current Universe but has no Verdict from any previous completed Run (a first Run, or a newly added Ticker)
**When** the current Run produces a Verdict for that Ticker
**Then** the System marks the Verdict as new rather than fabricating a change since a previous Run that does not exist.

### AC-07 (US-05) — domain invariant, cost ceiling

**Given** a configured per-Run cost ceiling
**When** a Run's accumulated cost is projected to exceed that ceiling before all tiers complete
**Then** the System stops committing further work for that Run, ends the Run without publishing a partial set of Verdicts, alerts the Owner that the ceiling was reached, and leaves the previous Run's Verdicts available.

### AC-08 (US-06) — cross-context / recoverability, resume after restart

**Given** a Run has completed its cheap-tier extraction and submitted a synthesis-tier Batch job that is still pending
**When** the process restarts before that Batch job completes
**Then** the System resumes the same Run, recovers the pending Batch job and continues polling it, and produces the Run's Verdicts without repeating the already-completed extraction work or producing duplicate Verdicts.

### AC-09 (US-01) — happy path, empty evidence for a Ticker

**Given** the Universe contains a Ticker for which no fresh Raw documents were collected for the Run
**When** the Run executes
**Then** the System still completes the Run for the rest of the Universe and records for that Ticker that there was no fresh evidence this Run, without inventing a Verdict conclusion for it.

## 6. Non-functional requirements

| Aspect | Target | Measurement |
|---|---|---|
| Per-Run cost ceiling | ≤ configured limit (default 2.00 USD per Run); Run alerts and stops if exceeded | Per-Run cost summed from recorded tier costs; compared to configured ceiling |
| Cheap-tier token share | ≥ 80% of total Run token volume on the cheap tier | Recorded cheap-tier tokens ÷ total Run tokens |
| Batch poll interval | Every 5 minutes (configurable) while a Batch job is pending | Elapsed time between successive poll attempts, recorded per Run |
| Max Run wall-clock | ≤ 20 hours from Run start to finish, then the Run times out and fails cleanly | Run finished_at − started_at; timeout enforced by the Run |
| Resume-after-restart | 0 completed steps repeated and 0 duplicate Verdicts after a mid-Run restart | Kill-and-restart test: count of re-executed steps and duplicate Verdict rows = 0 |
| Batch discount usage | 100% of cheap-tier and synthesis-tier LLM work submitted as Batch jobs | Count of synchronous LLM calls during a Run = 0 |

## 6.1 Security / privacy

- **Data classification:** confidential — the Universe reveals the Owner's holdings and interests; Verdicts are private financial research.
- **Personal data touched:** no new personal-identity fields; the pipeline stores Tickers, Extracted facts, and Verdicts tied to `run_id`, all for the single Owner.
- **AuthZ/AuthN impact:** none new — the pipeline has no external interface; it is triggered only by the internal scheduler, never by an inbound request. The System is the only actor that starts a Run (AC-04b).
- **Abuse cases:**
  - Ad-hoc trigger by anything other than the scheduler → the System ignores it; only the scheduled recurring job starts a Run (AC-04b).
  - Verdict misused as a trading instruction → the System is strictly advisory and read-only toward the Broker; it never emits an order (AC-04; CONTEXT invariant).
  - Runaway spend from a retry storm or a stuck batch → per-Run cost ceiling stops the Run and alerts the Owner (AC-07); bounded retries with backoff.
- **Security review:** N/A — single-owner, no external interface, no new PII, read-only toward the Broker. Trust risk (over-reliance on a Signal) is mitigated by mandatory citations and deltas, not by an access-control boundary.

## 7. Metrics / KPIs

- **Nightly Run success rate** — baseline: 0 (no pipeline yet), target: ≥ 6 of 7 nights/week complete with a full set of Verdicts.
- **Per-Run cost** — baseline: 0, target: median ≤ 1.00 USD and 0 Runs exceeding the configured ceiling over a 30-day window.
- **Cheap-tier token share** — baseline: 0, target: ≥ 80% of token volume on the cheap tier, tracked per Run.
- **Verdict evidence compliance** — baseline: 0, target: 100% of persisted Verdicts cite ≥ 1 Source, verified per Run.
- **Resume correctness** — baseline: 0, target: 0 duplicated completed steps or duplicate Verdicts across all mid-Run restarts observed.

## 8. Open questions

- [ ] Acceptable monthly Anthropic spend ceiling that the per-Run ceiling must roll up to? Default now: per-Run ceiling 2.00 USD, no explicit monthly cap. — owner: Owner, due: before first production Run.
- [ ] Should the synthesis tier receive an optional Sonnet middle tier for structured scoring, or stay strictly two-tier for MVP? Default now: strictly two-tier; Sonnet middle tier is a documented extension point, not MVP (ADR-0005). — owner: Owner, due: after first month of Runs.
- [ ] Is 20 hours the right max Run wall-clock given the Batch API's up-to-24h SLA, or should a late Run be allowed to finish for the following morning? Default now: 20h timeout, then fail and show the previous Run. — owner: Owner, due: before first production Run.
