---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T01
deps: []
estimate: S
branch: feat/ingest-core-abstractions
---

# T01 — Core models + Source abstractions

## Goal

Add the zero-dependency Core models and Source interfaces this feature needs, so every Source sits behind a Core abstraction (Open/Closed).

## Scope

- `WiseWizard.Core/Models/RawDocument.cs` — matches [data-model.md](../data-model.md) `raw_documents` columns.
- `WiseWizard.Core/Models/SourceKind` — enum/const for `sec_edgar`, `news_rss`, `market_data`.
- `WiseWizard.Core/Abstractions/ISecFilingsSource`, `INewsSource`, `IMarketDataSource` — each returns candidate documents for a `(ticker, lookback)` request (sad.md §5).
- `WiseWizard.Core/Abstractions/IRawDocumentRepository` — signatures only (implemented in T02).

## Links

- Data model: [data-model.md](../data-model.md).
- SAD: [sad.md §5](../../../00-overview/sad.md) — Core abstractions, dependency direction.
- Context: [CONTEXT.md §Glossary](../../../00-overview/CONTEXT.md) — Raw document, Source.

## DoD

- Core builds with zero external dependencies (dependency-direction rule, sad.md §8).
- Interfaces compile; no implementations yet.
- Unit test asserts `RawDocument` carries all data-model fields.
