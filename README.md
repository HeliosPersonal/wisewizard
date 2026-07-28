# WiseWizard 🧙

> *A personal AI investment-research assistant that reads your Interactive Brokers portfolio and delivers a morning digest to Telegram — automatically, every night.*

[![CI/CD](https://github.com/HeliosPersonal/wisewizard/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/HeliosPersonal/wisewizard/actions/workflows/ci-cd.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## What it does

WiseWizard watches your portfolio and watchlist while you sleep. Each night it:

1. **Reads your live IBKR portfolio** — current positions, quantities, market values (read-only, zero trading)
2. **Ingests free public data** — SEC EDGAR filings, news RSS feeds, fundamentals for every ticker you hold or watch
3. **Runs a two-tier LLM cascade** — a cheap model filters noise and extracts facts at volume; a synthesis model produces a verdict per ticker
4. **Delivers a morning Telegram digest** — 🟢 hold / 🟡 attention / 🔴 review, one line per ticker, with drill-down on demand

Every verdict cites the sources that informed it. Nothing is advisory without evidence.

---

## Architecture at a glance

```
Telegram ←──→  Bot layer
                  │
           Pipeline layer  ←── Hangfire nightly jobs
                  │
        Infrastructure layer  ←── IBKR · Anthropic · SEC EDGAR · RSS
                  │
            Core (domain)  ←── zero external deps
                  │
            PostgreSQL  +  Hangfire schema
```

**Dependency direction:** `Host → Bot/Pipeline → Infrastructure → Core`

Core defines all abstractions. Infrastructure holds every external adapter. Adding a new data source = one new implementation, zero changes to the pipeline.

| Layer | Project |
|---|---|
| Composition root | `WiseWizard.Host` |
| Telegram bot | `WiseWizard.Bot` |
| Nightly pipeline | `WiseWizard.Pipeline` |
| External adapters | `WiseWizard.Infrastructure` |
| Domain + interfaces | `WiseWizard.Core` |
| Local orchestration | `Aspire/WiseWizard.AppHost` |

---

## Key design decisions

| # | Decision | Choice |
|---|---|---|
| ADR-001 | Process model | Single .NET Generic Host — simple ops, no microservice overhead |
| ADR-002 | Broker access | IBKR Client Portal (local REST) — read-only, daily 2FA |
| ADR-004 | Job scheduling | Hangfire — persistent, retryable, survives restarts |
| ADR-005 | LLM calls | Anthropic Message Batches API — async, cheap, bulk |
| ADR-007 | Storage | PostgreSQL via Dapper — domain data + Hangfire in same DB, separate schemas |
| ADR-008 | Local dev | .NET Aspire — provisions Postgres, injects config |

Full ADRs live in [`docs/00-overview/adr/`](docs/00-overview/adr/).

---

## Stack

- **.NET 10 / C#** — Generic Host, BackgroundService
- **PostgreSQL** — domain schema + `hangfire` schema (Npgsql + Dapper)
- **Hangfire** — nightly job at 23:00, 5-min batch poll, restart resume
- **Anthropic API** — cheap model (extraction) + synthesis model (verdicts)
- **IBKR Client Portal API** — local REST gateway, read-only
- **Telegram.Bot** — digest delivery + interactive drill-down
- **Infisical** — runtime secrets (EU cloud, machine identity)
- **Kubernetes / k3s** — production deployment
- **.NET Aspire** — local dev orchestration (not in prod image)

---

## Local development

### Prerequisites

- .NET 10 SDK
- Docker (for Aspire-provisioned Postgres + test containers)
- An Infisical account — or skip it for local dev (env vars only)

### Run with Aspire

```bash
dotnet run --project src/Aspire/WiseWizard.AppHost
```

Aspire provisions a local PostgreSQL container and injects configuration. No manual database setup needed.

For Infisical-backed secrets locally, set these user-secrets on `WiseWizard.AppHost`:

```bash
dotnet user-secrets set "Parameters:InfisicalClientId"     "<your-client-id>"     --project src/Aspire/WiseWizard.AppHost
dotnet user-secrets set "Parameters:InfisicalClientSecret" "<your-client-secret>" --project src/Aspire/WiseWizard.AppHost
dotnet user-secrets set "Parameters:InfisicalProjectId"    "<your-project-id>"    --project src/Aspire/WiseWizard.AppHost
```

Without those, the app falls back to `appsettings.json` + environment variables — fine for local development.

### Run tests

```bash
dotnet test
```

> **Note:** Infrastructure tests require Docker (Testcontainers spins up a throwaway Postgres). Core and Bot suites are Docker-free.

### Coverage

```bash
./coverage.sh
```

Coverage gate: **> 95%** line + branch. Excluded: `WiseWizard.Host` (composition root) and generated code.

---

## Project structure

```
src/
├── WiseWizard.Host/           # Entry point, DI wiring, Hangfire setup
├── WiseWizard.Bot/            # Telegram polling, command handlers
├── WiseWizard.Pipeline/       # Nightly research pipeline (model cascade)
├── WiseWizard.Infrastructure/ # IBKR, Anthropic, SEC EDGAR, RSS, Postgres repos
├── WiseWizard.Core/           # Domain models, interfaces, invariants
└── Aspire/
    ├── WiseWizard.AppHost/    # Local dev orchestration
    └── WiseWizard.ServiceDefaults/

tests/
├── WiseWizard.Core.Tests/
├── WiseWizard.Bot.Tests/
├── WiseWizard.Pipeline.Tests/
└── WiseWizard.Infrastructure.Tests/

docs/
├── 00-overview/               # CONTEXT, SAD, ADRs, idea-brief
└── features/                  # Per-feature PRDs and data models

k8s/                           # Kubernetes manifests (base + overlays)
terraform/                     # Infrastructure as code
```

---

## Production deployment

Secrets are managed by **Infisical** (EU cloud). Only three bootstrap credentials live in Kubernetes:

| K8s Secret key | Purpose |
|---|---|
| `INFISICAL_CLIENT_ID` | Machine identity client ID |
| `INFISICAL_CLIENT_SECRET` | Machine identity secret |
| `INFISICAL_PROJECT_ID` | WiseWizard Infisical project |

All application secrets (`CONNECTIONSTRINGS__WISEWIZARD`, `ANTHROPIC__APIKEY`, `TELEGRAM__BOTTOKEN`, etc.) are fetched from Infisical at pod startup under the `production` environment, `/app` path.

CI/CD (GitHub Actions) builds the Docker image, injects the bootstrap credentials, and applies the K8s manifests on every push to `main`.

---

## Domain invariants

- **Read-only broker access** — WiseWizard never places, modifies, or cancels an order. This is a hard architectural constraint, not a configuration option.
- **Evidence-backed verdicts** — every Verdict must cite ≥ 1 source. A verdict without evidence is invalid.
- **Single owner** — no multi-tenancy, no auth beyond a Telegram chat-id allowlist.
- **Resumable runs** — a Run persists all progress; a process restart picks up exactly where it left off.

---

## License

[MIT](LICENSE) — personal project, use at your own risk. This is not financial advice.
