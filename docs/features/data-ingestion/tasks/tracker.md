---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "13"
ticket: "N/A — personal project"
---

# Tracker — data-ingestion

Status legend: `todo` · `in-progress` · `in-review` · `done`.

| ID | Task | Branch | Deps | Owner | Est | Status |
|---|---|---|---|---|---|---|
| T01 | [Core models + Source abstractions](./T01-core-models-abstractions.md) | `feat/ingest-core-abstractions` | — | Owner | S | todo |
| T02 | [`raw_documents` migration + repository](./T02-raw-documents-persistence.md) | `feat/ingest-raw-documents-persistence` | T01 | Owner | M | todo |
| T03 | [Content-hash dedup logic](./T03-content-hash-dedup.md) | `feat/ingest-content-hash-dedup` | T01 | Owner | S | todo |
| T04 | [Per-host polite rate limiter](./T04-rate-limiter.md) | `feat/ingest-rate-limiter` | T01 | Owner | S | todo |
| T05 | [SEC EDGAR Source](./T05-sec-edgar-source.md) | `feat/ingest-sec-edgar-source` | T01, T04 | Owner | M | todo |
| T06 | [News RSS Source](./T06-news-rss-source.md) | `feat/ingest-news-rss-source` | T01, T04 | Owner | M | todo |
| T07 | [Market data Source](./T07-market-data-source.md) | `feat/ingest-market-data-source` | T01, T04 | Owner | M | todo |
| T08 | [Lookback + per-Source cap filtering](./T08-lookback-cap-filter.md) | `feat/ingest-lookback-cap-filter` | T01 | Owner | S | todo |
| T09 | [Collection-gap recording](./T09-collection-gap-recording.md) | `feat/ingest-collection-gap` | T01 | Owner | S | todo |
| T10 | [IngestStep orchestration](./T10-ingest-step-orchestration.md) | `feat/ingest-step-orchestration` | T02,T03,T05,T06,T07,T08,T09 | Owner | M | todo |
| T11 | [Hangfire wiring](./T11-hangfire-wiring.md) | `feat/ingest-hangfire-wiring` | T10, T12 | Owner | S | todo |
| T12 | [Retention cleanup job](./T12-retention-cleanup.md) | `feat/ingest-retention-cleanup` | T02 | Owner | S | todo |
| T13 | [Test suite + fixtures + load harness](./T13-test-suite.md) | `feat/ingest-test-suite` | T01–T12 | Owner | M | todo |
