---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — Source failure and rate-limit handling

<!-- Stage 07. Covers the error and authorization paths: a Source is unreachable
or signals its allowed access rate is exceeded. Realizes PRD §5 AC-02 (error)
and AC-03 (authorization / polite access). See sad.md §1 QG-2, §8. -->

## Error path: a Source is unreachable

```mermaid
sequenceDiagram
    autonumber
    participant IS as IngestStep
    participant DB as Domain DB
    participant SEC as ISecFilingsSource
    participant NEWS as INewsSource
    participant MKT as IMarketDataSource

    Note over IS: ingesting one Ticker

    IS->>SEC: fetch filings (ticker)
    SEC-->>IS: transport error / timeout
    Note over IS: retry with backoff (bounded)
    IS->>SEC: fetch filings (retry)
    SEC-->>IS: still failing
    IS->>DB: record collection gap\n(run_id, ticker, source=sec_edgar, reason)
    Note over IS: skip this Source, do NOT abort (AC-02)

    IS->>NEWS: fetch articles (ticker)
    NEWS-->>IS: articles OK
    IS->>DB: persist deduped news documents

    IS->>MKT: fetch metrics (ticker)
    MKT-->>IS: metrics OK
    IS->>DB: persist metrics snapshot

    Note over IS: other Sources and other Tickers unaffected
    IS-->>IS: continue to next Ticker
```

## Authorization / rate-limit path: Source signals allowed access exceeded

```mermaid
sequenceDiagram
    autonumber
    participant IS as IngestStep
    participant RL as Rate limiter (per Source host)
    participant DB as Domain DB
    participant SEC as ISecFilingsSource

    Note over IS,SEC: SEC EDGAR grants free access only with a\ndeclared identity + within an allowed request rate (AC-03)

    IS->>RL: acquire slot (≤10 req/s, SEC host)
    RL-->>IS: slot granted
    IS->>SEC: fetch filings\n(declared User-Agent = contact identity)
    SEC-->>IS: rate-limit signal (access exceeded)

    Note over IS: back off — do NOT keep requesting (AC-03)
    IS->>RL: widen interval / wait
    IS->>SEC: fetch filings (after backoff, still identified)
    alt access restored
        SEC-->>IS: filings OK
        IS->>DB: persist deduped filings
    else still limited after bounded backoff
        SEC-->>IS: rate-limit signal
        IS->>DB: record collection gap\n(run_id, ticker, source=sec_edgar, reason=rate_limited)
        Note over IS: skip Source for this Ticker (AC-02), continue
    end
```

## Notes

- **Isolation (AC-02):** a failed or rate-limited Source produces a recorded gap for that (Run, Ticker, Source) and never fails the Run or blocks other Sources/Tickers; the Hangfire ingest step still completes so the pipeline chain continues (ADR-0004, sad.md §1 QG-2).
- **Polite access (AC-03):** every request to a Source that requires it carries the System's declared identity (User-Agent for SEC EDGAR), and the per-host rate limiter keeps requests within the Source's allowed access rate, backing off on rate-limit signals rather than hammering.
