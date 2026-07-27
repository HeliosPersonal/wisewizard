---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — keep-alive and manual 2FA re-auth

<!-- One file per flow. Covers happy keep-alive + session lapse/re-auth (sad.md §6 flow 3).
Upstream: PRD §AC-04, §AC-09 · sad.md §6 (flow 3 broker-session-expiry), §11 · ADR-0006.
Modules: WiseWizard.Infrastructure/Ibkr (IbkrSessionService), WiseWizard.Bot (Telegram alert). -->

## Happy path — keep the session alive between Runs (PRD §AC-09)

```mermaid
sequenceDiagram
    autonumber
    participant SVC as IbkrSessionService
    participant GW as IBKR Client Portal gateway<br/>(localhost, read-only)
    participant REPO as SessionStateRepository<br/>(Dapper / SQLite)
    participant DB as Domain DB

    loop every keep-alive interval (60 s, PRD §6)
        SVC->>GW: keep-alive ping (session status)
        GW-->>SVC: session live / authenticated
        SVC->>REPO: record keep-alive
        REPO->>DB: update broker_session: status='live', last_keepalive_at=now
    end
    Note over SVC,DB: session held live on its own;<br/>next refresh normally succeeds unattended (PRD §AC-09)
```

## Failure + recovery — Broker forces logout, Owner re-authenticates (PRD §AC-04, sad.md §6 flow 3)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant SVC as IbkrSessionService
    participant GW as IBKR gateway (read-only)
    participant REPO as SessionStateRepository
    participant DB as Domain DB
    participant BOT as TelegramBotService

    SVC->>GW: keep-alive ping (or refresh attempt)
    GW-->>SVC: session lapsed / not authenticated
    SVC->>REPO: mark session lapsed
    REPO->>DB: update broker_session: status='lapsed'
    Note over SVC: stop pinging while lapsed (ADR-0006)

    alt not yet alerted this lapse
        SVC->>BOT: request re-auth alert to Owner
        BOT-->>Owner: "Brokerage session expired — tap 2FA in the Broker app"
        SVC->>REPO: record alert sent
        REPO->>DB: update broker_session: reauth_alerted_at=now
    end

    Owner->>GW: complete 2FA in the Broker's own app (manual)

    loop resume keep-alive polling
        SVC->>GW: keep-alive ping (session status)
        GW-->>SVC: session live again
    end
    SVC->>REPO: mark session recovered
    REPO->>DB: update broker_session: status='live',<br/>last_keepalive_at=now, reauth_alerted_at=NULL
    Note over SVC,DB: keep-alive resumes; last known-good Portfolio<br/>stayed available throughout the lapse (sad.md §6, §11)
```
