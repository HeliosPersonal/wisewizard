---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — nightly Run (happy path)

<!-- Realizes PRD §5 AC-01, AC-02, AC-05, AC-06, AC-09 and sad.md §6 critical flow 1. -->
<!-- Covers: happy path + evidence-required invariant + delta computation. -->

## Happy path

```mermaid
sequenceDiagram
    autonumber
    participant Cron as Hangfire scheduler (23:00)
    participant P as NightlyPipeline
    participant DB as Domain DB (runs/facts/verdicts)
    participant ING as data-ingestion
    participant LLM as ILlmClient → AnthropicLlmClient (Batch)

    Cron->>P: trigger recurring Run
    P->>DB: create run (status=pending)
    P->>DB: read Universe (positions ∪ watchlist)
    P->>ING: request ingestion for run_id
    ING-->>DB: write raw_documents keyed by run_id
    ING-->>P: ingestion complete
    P->>DB: read raw_documents for run_id
    P->>DB: status=extracting

    Note over P,LLM: Cheap tier — high volume: relevance filter + fact extraction
    P->>LLM: submit cheap-tier Batch (documents)
    LLM-->>P: batch id
    P->>DB: persist batch id (batch_ids_json)
    loop poll every 5 min until complete
        P->>LLM: poll cheap-tier batch
        LLM-->>P: pending
    end
    LLM-->>P: cheap-tier results ready
    P->>LLM: retrieve cheap-tier results
    P->>DB: persist extracted_facts (+ cheap tokens/cost)
    P->>DB: status=synthesizing

    Note over P,LLM: Synthesis tier — low volume: per-Ticker Verdict + delta
    P->>DB: read previous completed Run's Verdicts (delta baseline)
    P->>LLM: submit synthesis Batch (distilled facts per Ticker + prior Verdict)
    LLM-->>P: batch id
    P->>DB: persist batch id (batch_ids_json)
    loop poll every 5 min until complete
        P->>LLM: poll synthesis batch
        LLM-->>P: pending
    end
    LLM-->>P: synthesis results ready
    P->>LLM: retrieve synthesis results
    P->>DB: status=persisting

    Note over P,DB: Evidence invariant + delta rules per Ticker
    alt Ticker has ≥1 citable Extracted fact
        P->>DB: read prior Verdict for Ticker
        alt prior Verdict exists
            P->>P: compute change_from_yesterday vs prior
        else no prior Verdict (first Run / new Ticker)
            P->>P: mark change_from_yesterday = new
        end
        P->>DB: insert Verdict (signal, summary, reasoning, sources_json≥1, delta)
    else no citable evidence (empty/unsupported)
        P->>DB: record "no fresh evidence this Run" (no Verdict conclusion)
    end

    P->>DB: sum cost_total_usd / tokens; status=finished, finished_at
    Note over P,DB: latest completed Run now serves the morning Daily digest
```
