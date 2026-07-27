---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — Drill-down (details callback)

<!-- Stage 07. Covers happy path (full reasoning + cited Sources) + Ticker absent from latest Run + non-Owner callback. -->
<!-- Realizes sad.md §6 flow 2 (drill-down). PRD AC-02, AC-02b, AC-05, AC-06. -->

## Happy path — Owner taps "details" for a Ticker

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant TG as Telegram
    participant Bot as TelegramBotService
    participant Auth as ChatId allowlist
    participant CB as Callback handler
    participant RunRepo as Run repository
    participant VRepo as Verdict repository
    participant Fmt as Detail formatter

    Owner->>TG: tap "details" for Ticker
    TG->>Bot: callback (chat id + Ticker reference)
    Bot->>Auth: is this the Owner's chat?
    Auth-->>Bot: yes
    Bot->>CB: handle details for Ticker
    CB->>RunRepo: latest completed Run
    RunRepo-->>CB: run (completed)
    CB->>VRepo: Verdict for (run id, Ticker)
    VRepo-->>CB: full reasoning + cited Sources + "what changed"
    CB->>Fmt: render detail (escape reasoning + Source titles)
    Fmt-->>CB: detail message
    CB-->>Bot: message + acknowledge the tap
    Bot->>TG: send detail; acknowledge callback
    TG-->>Owner: full reasoning with cited Sources for that Ticker
```

## Cross-context — requested Ticker has no Verdict in the latest Run

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Bot as TelegramBotService
    participant CB as Callback handler
    participant RunRepo as Run repository
    participant VRepo as Verdict repository

    Owner->>Bot: tap "details" for Ticker
    Bot->>CB: handle details for Ticker
    CB->>RunRepo: latest completed Run
    RunRepo-->>CB: run (completed)
    CB->>VRepo: Verdict for (run id, Ticker)
    VRepo-->>CB: none for this Ticker in this Run
    CB-->>Owner: "no Verdict for this Ticker in the latest report" (no reasoning, no Sources)
```

## Authorization — a non-Owner chat sends a details callback

```mermaid
sequenceDiagram
    autonumber
    actor Stranger
    participant TG as Telegram
    participant Bot as TelegramBotService
    participant Auth as ChatId allowlist

    Stranger->>TG: tap "details" (forged/relayed callback)
    TG->>Bot: callback (non-allowlisted chat id)
    Bot->>Auth: is this the Owner's chat?
    Auth-->>Bot: no
    Note over Bot: drop before resolving Run or Verdict
    Bot--xTG: no data returned (existence of any Verdict neither confirmed nor denied)
```
