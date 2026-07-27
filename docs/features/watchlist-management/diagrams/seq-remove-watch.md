---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — Remove a Ticker from the Watchlist

<!-- Stage 07. Covers happy path + not-watched error + authz path.
     The Bot participant (transport) is owned by telegram-bot-reporting. -->

> **Upstream:** [PRD](../PRD.md) §5 AC-03, AC-05, AC-06 · [data-model](../data-model.md) · [sad.md §5, §6](../../00-overview/sad.md)

## Happy path (AC-03)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Bot as WiseWizard.Bot handler
    participant Svc as Watchlist domain service
    participant Repo as IWatchlistRepository
    participant DB as Domain DB (SQLite)

    Owner->>Bot: /unwatch <symbol>
    Note over Bot: authorize sender chat-id (telegram-bot-reporting)
    Bot->>Svc: remove(symbol)
    Svc->>Svc: normalize (trim + uppercase)
    Svc->>Repo: remove(ticker)
    Repo->>DB: delete row by primary key
    DB-->>Repo: 1 row removed
    Repo-->>Svc: true
    Svc-->>Bot: removed(ticker)
    Bot-->>Owner: confirms "<ticker> is no longer watched"
```

## Error path: Ticker not on the Watchlist (AC-05)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Bot as WiseWizard.Bot handler
    participant Svc as Watchlist domain service
    participant Repo as IWatchlistRepository
    participant DB as Domain DB (SQLite)

    Owner->>Bot: /unwatch <symbol not watched>
    Bot->>Svc: remove(symbol)
    Svc->>Svc: normalize
    Svc->>Repo: remove(ticker)
    Repo->>DB: delete row by primary key
    DB-->>Repo: 0 rows removed
    Repo-->>Svc: false
    Svc-->>Bot: not watched (no change)
    Bot-->>Owner: tells the Owner the Ticker was not on the Watchlist
```

## Authorization path: sender is not the Owner (AC-06)

```mermaid
sequenceDiagram
    autonumber
    actor Other as Non-Owner
    participant Bot as WiseWizard.Bot handler
    participant Svc as Watchlist domain service

    Other->>Bot: /unwatch <symbol>
    Note over Bot: sender chat-id not on Owner allowlist
    Bot-->>Other: declines; request never reaches Svc
    Note over Svc: no domain call, no change to the Watchlist
```
