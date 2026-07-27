---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "07"
ticket: "N/A — personal project"
---

# Sequence — read Positions (Portfolio refresh)

<!-- One file per flow. Covers happy path + empty Portfolio + refresh failure.
Upstream: PRD §AC-01, §AC-02, §AC-03, §AC-06, §AC-07, §AC-08 · sad.md §5, §6 · ADR-0002.
Modules: WiseWizard.Infrastructure/Ibkr (ClientPortalBrokerReader, IbkrSessionService),
WiseWizard.Core/Abstractions (IBrokerReader), WiseWizard.Infrastructure/Persistence. -->

## Happy path — refresh persists a current snapshot (PRD §AC-01, §AC-02, §AC-06)

```mermaid
sequenceDiagram
    autonumber
    participant SVC as IbkrSessionService
    participant RDR as ClientPortalBrokerReader<br/>(IBrokerReader)
    participant GW as IBKR Client Portal gateway<br/>(localhost, read-only)
    participant REPO as PositionsRepository<br/>(Dapper / SQLite)
    participant DB as Domain DB

    SVC->>RDR: refresh Portfolio (read-only)
    RDR->>GW: request current Positions
    GW-->>RDR: Positions (ticker, quantity, avg_cost,<br/>market_value, unrealized_pnl, currency)
    RDR-->>SVC: mapped Core Position list + read instant (as_of)
    SVC->>REPO: replace snapshot (Positions, as_of)
    REPO->>DB: BEGIN; DELETE FROM positions; INSERT new rows (shared as_of); COMMIT
    REPO->>DB: update broker_session: last_snapshot_at=as_of,<br/>last_refresh_ok='true', last_refresh_attempt_at=now
    DB-->>REPO: committed
    REPO-->>SVC: snapshot current as of as_of
    Note over SVC,DB: Portfolio Tickers now available to form the<br/>Portfolio part of the Universe (PRD §AC-08)
```

## Variant — empty Portfolio (PRD §AC-07)

```mermaid
sequenceDiagram
    autonumber
    participant SVC as IbkrSessionService
    participant RDR as ClientPortalBrokerReader
    participant GW as IBKR gateway (read-only)
    participant REPO as PositionsRepository
    participant DB as Domain DB

    SVC->>RDR: refresh Portfolio (read-only)
    RDR->>GW: request current Positions
    GW-->>RDR: zero Positions (Owner holds nothing)
    RDR-->>SVC: empty Position list + read instant
    SVC->>REPO: replace snapshot (empty, as_of)
    REPO->>DB: BEGIN; DELETE FROM positions; (no inserts); COMMIT
    REPO->>DB: update broker_session: last_snapshot_at=as_of, last_refresh_ok='true'
    DB-->>REPO: committed
    Note over SVC,DB: empty-but-current — distinct from a Portfolio that<br/>could not be refreshed (PRD §AC-07)
```

## Error path — refresh cannot reach the Broker (PRD §AC-03)

```mermaid
sequenceDiagram
    autonumber
    participant SVC as IbkrSessionService
    participant RDR as ClientPortalBrokerReader
    participant GW as IBKR gateway (read-only)
    participant REPO as PositionsRepository
    participant DB as Domain DB

    SVC->>RDR: refresh Portfolio (read-only)
    RDR->>GW: request current Positions
    GW-->>RDR: read fails (unreachable / error)
    RDR-->>SVC: refresh failed
    SVC->>REPO: record failed attempt (do NOT touch positions)
    REPO->>DB: update broker_session: last_refresh_attempt_at=now,<br/>last_refresh_ok='false' (last_snapshot_at unchanged)
    DB-->>REPO: committed
    Note over SVC,DB: last known-good positions retained;<br/>Portfolio still labelled with prior as_of → age visible (PRD §AC-03, §6)
```
