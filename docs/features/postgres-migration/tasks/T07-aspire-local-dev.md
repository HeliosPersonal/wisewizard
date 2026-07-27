---
status: Draft
owner: "Owner"
updated_at: "2026-07-27"
stage: "13"
task_id: T07
estimate: M
deps: [T01]
---

# T07 — Aspire `AppHost` + `ServiceDefaults`

## Scope

Add a .NET Aspire local-dev orchestration so one command provisions Postgres and runs the Host with secrets from `dotnet user-secrets`.

- **`src/Aspire/WiseWizard.AppHost`** — Aspire orchestrator. Provisions a Postgres container **with a named volume** (persistence across runs), injects its connection string into the Host as `ConnectionStrings:WiseWizard`, and passes Anthropic/Telegram/IBKR settings from `dotnet user-secrets` (gitignored). One command: `dotnet run --project src/Aspire/WiseWizard.AppHost`.
- **`src/Aspire/WiseWizard.ServiceDefaults`** — shared OTel + health-check + HTTP resilience defaults; `builder.AddServiceDefaults()` is called from `Program.cs`.
- Both projects are **local-dev only**: excluded from the Docker image (runtime still publishes only `WiseWizard.Host`) **and** excluded from CI coverage like `WiseWizard.Host`.
- Add both projects to `WiseWizard.slnx`.
- **Packages:** `Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`, and the ServiceDefaults OTel/resilience packages.

## Links

- Design: [design doc](../../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — "Aspire local dev".
- [ADR-0008](../../../00-overview/adr/0008-aspire-local-dev.md), [ADR-0007](../../../00-overview/adr/0007-postgresql-datastore.md).
- Source: `WiseWizard.slnx`, `Directory.Packages.props`, `src/WiseWizard.Host/Program.cs`.

## Definition of Done

- `dotnet run --project src/Aspire/WiseWizard.AppHost` provisions a Postgres container (named volume) and starts the Host with `ConnectionStrings:WiseWizard` injected automatically; Anthropic/Telegram/IBKR values are supplied from `dotnet user-secrets`.
- `WiseWizard.ServiceDefaults` provides OTel/health/resilience defaults and `builder.AddServiceDefaults()` is wired into `Program.cs`.
- Both Aspire projects are in `WiseWizard.slnx`, excluded from the Docker image, and excluded from CI coverage (like `WiseWizard.Host`) — the **> 95%** gate is unaffected.
- Required packages are pinned in `Directory.Packages.props`.
