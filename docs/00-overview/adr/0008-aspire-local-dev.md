---
status: Accepted
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-27"
feature_size: L
stage: "04-05"
ticket: "N/A"
---

# 0008 — .NET Aspire for local development orchestration

- **Status:** Accepted
- **Date:** 2026-07-27
- **Deciders:** Owner

## Context

With ADR-0007, Postgres is now an external dependency rather than a local file. Local development therefore needs a one-command way to provision Postgres and inject the app's configuration, so a developer can run the whole system without hand-crafting a database or a settings file. The sibling Sentra app already solves this with a `Sentra.AppHost`; matching that pattern keeps the two apps consistent. See [sad.md](../sad.md) §7-§8.

## Decision drivers

- One-command local dev — provision Postgres and start the Host in a single step.
- No secrets committed — API keys and tokens must not land in the repo.
- Parity with the sibling Sentra app's AppHost pattern.
- Keep secrets out of the Docker runtime image.

## Considered options

1. **Manual `docker compose` + `appsettings`** — the developer runs a compose file and maintains a local settings file by hand.
2. **.NET Aspire AppHost + ServiceDefaults** — an Aspire orchestrator provisions Postgres and injects configuration; shared defaults for OTel/health/resilience.

## Decision outcome

**Chosen: Option 2.** Add two local-dev projects under `src/Aspire/`:

- `WiseWizard.AppHost` — the Aspire orchestrator. Provisions a Postgres container with a named volume (so data persists across runs), injects its connection string into the Host as `ConnectionStrings:WiseWizard`, and passes the Anthropic/Telegram/IBKR values from `dotnet user-secrets` (gitignored). Run with: `dotnet run --project src/Aspire/WiseWizard.AppHost`.
- `WiseWizard.ServiceDefaults` — shared OpenTelemetry, health-check, and HTTP resilience defaults, wired via `builder.AddServiceDefaults()` in `Program.cs`.

Both projects are **local-dev only**: excluded from the Docker runtime image (which still publishes only `WiseWizard.Host`) and excluded from CI coverage exactly like `WiseWizard.Host`. Infisical remains the source of configuration for staging and production.

## Consequences

**Positive**
- One command starts Postgres and the app locally.
- No secrets committed — local values come from `dotnet user-secrets`.
- Production parity: the same `ConnectionStrings:WiseWizard` shape as the deployed app.
- Aspire dashboard gives local traces and health at a glance.

**Negative**
- Docker is required for local development.
- Two extra projects to maintain.

**Neutral**
- Mirrors the Sentra `AppHost`/`ServiceDefaults` layout, so the pattern is already familiar.

## Links

- PRD: [[../idea-brief.md]]
- SAD: [[../sad.md]] §7-§8
- Related ADR: [[0007-postgresql-datastore]], [[0001-single-process-generic-host]]
