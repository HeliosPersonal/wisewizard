---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — ingest one Ticker (happy path + dedup)

<!-- Stage 07. Covers the happy collection path for a single Ticker plus the
dedup and Universe-scoping invariants. Realizes PRD §5 AC-01, AC-04, AC-05,
AC-06, AC-07. See sad.md §5 (Source interfaces) and §8 (dedup by content hash). -->

## Happy path

```mermaid
sequenceDiagram
    autonumber
    participant HF as Hangfire (nightly chain)
    participant IS as IngestStep
    participant DB as Domain DB
    participant SEC as ISecFilingsSource
    participant NEWS as INewsSource
    participant MKT as IMarketDataSource

    HF->>IS: run ingest step (run_id)
    IS->>DB: read Universe (Portfolio ∪ Watchlist)
    DB-->>IS: Tickers in Universe
    Note over IS: for each Ticker in Universe only (AC-05)

    IS->>SEC: fetch filings (ticker, lookback 14d)
    SEC-->>IS: filings (declared User-Agent, paced)
    IS->>NEWS: fetch articles (ticker, lookback 14d)
    NEWS-->>IS: articles
    IS->>MKT: fetch latest metrics snapshot (ticker)
    MKT-->>IS: metrics snapshot

    Note over IS: keep only within lookback window,\ncap ≤15 per Source (AC-06)
    loop each candidate document
        IS->>IS: compute content_hash
        IS->>DB: exists (run_id, content_hash)?
        alt new content
            DB-->>IS: not found
            IS->>DB: insert raw_document (run_id, ticker, source, url,\ntitle, content, published_at, content_hash, fetched_at)
        else duplicate within Run (AC-04)
            DB-->>IS: found
            Note over IS: skip — no second copy this Run
        end
    end
    Note over IS: Ticker with zero fresh docs is recorded\nas ingested, not a failure (AC-07)
    IS-->>HF: ingest step done for run_id
```

## Invariant note

- **Universe scoping (AC-05):** `IngestStep` iterates only the Tickers returned by the Universe read; a Ticker outside Portfolio ∪ Watchlist is never fetched.
- **Dedup (AC-04):** the `(run_id, content_hash)` uniqueness check happens before insert; the DB unique index `ux_raw_documents_run_hash` is the backstop if two candidates race.
- **Bounds (AC-06):** lookback filter and per-Source cap are applied in code before the dedup loop.
