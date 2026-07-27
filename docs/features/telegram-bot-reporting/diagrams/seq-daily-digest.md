---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: M
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — Daily digest (/report)

<!-- Stage 07. Covers happy path + no-completed-Run empty state + non-Owner authorization + multi-message chunking. -->
<!-- Realizes sad.md §6 flow 2 (report read). PRD AC-01, AC-04, AC-05, AC-06, AC-09. -->

## Happy path — Owner requests the report

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant TG as Telegram
    participant Bot as TelegramBotService
    participant Auth as ChatId allowlist
    participant Router as Command router
    participant RunRepo as Run repository
    participant VRepo as Verdict repository
    participant Fmt as Digest formatter

    Owner->>TG: /report
    TG->>Bot: update (chat id + command)
    Bot->>Auth: is this the Owner's chat?
    Auth-->>Bot: yes
    Bot->>Router: route "/report"
    Router->>RunRepo: latest completed Run
    RunRepo-->>Router: run (completed)
    Router->>VRepo: Verdicts for run id
    VRepo-->>Router: verdicts (signal + summary per Ticker)
    Router->>Fmt: render digest (escape, chunk ≤20 lines / ≤4000 chars)
    Fmt-->>Router: one or more messages + per-Ticker details buttons
    Router-->>Bot: messages
    Bot->>TG: send digest message(s) with inline keyboards
    TG-->>Owner: digest — one line per Ticker (Signal + reason) + details buttons
```

## Domain invariant — a Run is in progress, an earlier one completed

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Bot as TelegramBotService
    participant RunRepo as Run repository
    participant VRepo as Verdict repository

    Owner->>Bot: /report
    Note over RunRepo: a newer Run is in_progress; an older Run is completed
    Bot->>RunRepo: latest COMPLETED Run
    RunRepo-->>Bot: the older completed Run (never the in-progress one)
    Bot->>VRepo: Verdicts for that completed run id
    VRepo-->>Bot: verdicts from the latest completed Run only
    Bot-->>Owner: digest from the latest completed Run (no partial results)
```

## Multi-message chunking — digest larger than one message

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Bot as TelegramBotService
    participant Fmt as Digest formatter
    participant TG as Telegram

    Owner->>Bot: /report
    Bot->>Fmt: render digest (N Tickers)
    Note over Fmt: split at Ticker boundary when >20 lines or >4000 chars
    Fmt-->>Bot: ordered messages [1..k], every Ticker line + details button preserved
    loop for each chunk in order
        Bot->>TG: send chunk
    end
    TG-->>Owner: full digest across ordered messages, no Ticker dropped
```

## Empty state — no completed Run yet

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Bot as TelegramBotService
    participant RunRepo as Run repository

    Owner->>Bot: /report
    Bot->>RunRepo: latest completed Run
    RunRepo-->>Bot: none
    Bot-->>Owner: "no digest available yet" (nothing resembling Verdicts)
```

## Authorization — a non-Owner chat sends /report

```mermaid
sequenceDiagram
    autonumber
    actor Stranger
    participant TG as Telegram
    participant Bot as TelegramBotService
    participant Auth as ChatId allowlist

    Stranger->>TG: /report
    TG->>Bot: update (non-allowlisted chat id)
    Bot->>Auth: is this the Owner's chat?
    Auth-->>Bot: no
    Note over Bot: drop before any repository read
    Bot--xTG: no reply (existence of any data neither confirmed nor denied)
```
