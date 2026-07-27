---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — Run failure (clean fail + alert, previous Run preserved)

<!-- Realizes PRD §5 AC-03 (batch failure/timeout) and AC-07 (cost ceiling). sad.md §10 QG-2. -->
<!-- Covers: two failure modes; both fail cleanly, alert the Owner, and keep the previous Run's Verdicts intact. -->

## Error path — Batch job fails or Run exceeds max wall-clock

```mermaid
sequenceDiagram
    autonumber
    participant P as NightlyPipeline
    participant DB as Domain DB (runs/verdicts)
    participant LLM as ILlmClient (Batch poll)
    participant TG as Telegram alert (self-alert)

    Note over P,DB: previous completed Run's Verdicts already persisted and available
    P->>LLM: poll pending batch
    alt batch reports failure
        LLM-->>P: failed
    else max Run wall-clock (default 20h) exceeded
        P->>P: Run timeout reached
    end
    P->>DB: do NOT write any partial/corrupted Verdicts for this Run
    P->>DB: status=failed, finished_at, failure_reason
    P->>TG: alert Owner "tonight's Run failed"
    Note over DB,TG: previous Run's Verdicts remain the latest available for the digest
```

## Error path — per-Run cost ceiling reached

```mermaid
sequenceDiagram
    autonumber
    participant P as NightlyPipeline
    participant DB as Domain DB (runs)
    participant TG as Telegram alert

    P->>DB: accumulate cost_total_usd after a tier completes
    P->>P: project remaining cost for next tier
    alt projected cost > configured ceiling
        P->>P: stop committing further work for this Run
        P->>DB: status=failed, failure_reason="cost ceiling reached"
        P->>DB: leave no partial set of Verdicts published
        P->>TG: alert Owner "Run stopped: cost ceiling reached"
        Note over DB,TG: previous Run's Verdicts remain available
    else within ceiling
        P->>P: continue to next tier
    end
```
