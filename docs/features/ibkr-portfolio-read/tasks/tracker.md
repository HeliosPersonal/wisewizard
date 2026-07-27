---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: S
stage: "13"
ticket: "N/A — personal project"
---

# Task tracker — ibkr-portfolio-read

Epic: [_epic.md](./_epic.md). Status values: `todo` · `in-progress` · `in-review` · `done`.

| # | Task | Est | Deps | Owner | Status |
|---|---|---|---|---|---|
| [T01](./T01-core-position-and-ibrokerreader.md) | `Position` model + `IBrokerReader` abstraction | S | — | Owner | todo |
| [T02](./T02-schema-positions-and-session.md) | `positions` + `broker_session` schema init | S | — | Owner | todo |
| [T03](./T03-positions-repository.md) | `PositionsRepository` (snapshot replace) | M | T02 | Owner | todo |
| [T04](./T04-session-state-repository.md) | `SessionStateRepository` (singleton) | S | T02 | Owner | todo |
| [T05](./T05-clientportal-read-positions.md) | `ClientPortalBrokerReader` read Positions | M | T01 | Owner | todo |
| [T06](./T06-clientportal-session-keepalive.md) | Session status + keep-alive ping | M | T05 | Owner | todo |
| [T07](./T07-ibkr-session-service-keepalive-loop.md) | `IbkrSessionService` keep-alive loop | M | T06, T04 | Owner | todo |
| [T08](./T08-refresh-orchestration-and-persist.md) | Refresh orchestration + persist snapshot | L | T07, T03 | Owner | todo |
| [T09](./T09-lapse-detection-and-reauth-alert.md) | Lapse detection + re-auth alert + recovery | M | T08 | Owner | todo |
| [T10](./T10-expose-portfolio-tickers-to-universe.md) | Expose current Portfolio Tickers to Universe | S | T01, T03 | Owner | todo |
| [T11](./T11-di-wiring-config-logging.md) | DI wiring, config/options, logging | M | T07, T08, T09 | Owner | todo |
