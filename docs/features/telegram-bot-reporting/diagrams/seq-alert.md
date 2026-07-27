---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — Self-alerts (Run failure & session re-auth)

<!-- Stage 07. Covers Run-failure alert, session-lapse alert, and idempotent no-duplicate-alert on restart. -->
<!-- Realizes sad.md §6 flow 3 (session alert) + monitoring self-alerts (sad.md §7). PRD AC-07, AC-08. -->

## Run-failure alert (happy path)

```mermaid
sequenceDiagram
    autonumber
    participant Pipeline as NightlyPipeline
    participant Pub as Alert publisher
    participant Log as bot_delivery_log
    participant Bot as TelegramBotService
    participant TG as Telegram
    actor Owner

    Pipeline->>Pub: Run failed (run id, reason)
    Pub->>Log: seen event_key run_failed:<run id>?
    Log-->>Pub: not yet
    Pub->>Bot: compose alert "Run did not complete"
    Bot->>TG: send alert to Owner's chat
    TG-->>Owner: "the nightly Run did not complete — the latest digest may be stale"
    Bot-->>Pub: delivered
    Pub->>Log: record event_key run_failed:<run id>
```

## Session re-auth alert (happy path)

```mermaid
sequenceDiagram
    autonumber
    participant Session as IbkrSessionService
    participant Pub as Alert publisher
    participant Log as bot_delivery_log
    participant Bot as TelegramBotService
    participant TG as Telegram
    actor Owner

    Session->>Pub: Brokerage session lapsed (lapse started at T)
    Pub->>Log: seen event_key session_lapse:<T>?
    Log-->>Pub: not yet
    Pub->>Bot: compose alert "re-authentication needed"
    Bot->>TG: send alert to Owner's chat
    TG-->>Owner: "tap 2FA to re-authenticate and restore a fresh Portfolio"
    Bot-->>Pub: delivered
    Pub->>Log: record event_key session_lapse:<T>
```

## Idempotency — process restarts after an alert already fired

```mermaid
sequenceDiagram
    autonumber
    participant Src as Pipeline / Session
    participant Pub as Alert publisher
    participant Log as bot_delivery_log
    participant Bot as TelegramBotService

    Note over Src: same failure/lapse re-observed after a restart
    Src->>Pub: event (same event_key)
    Pub->>Log: seen event_key?
    Log-->>Pub: already delivered
    Note over Pub: suppress — no duplicate alert
    Pub--xBot: nothing sent
```
