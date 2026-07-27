---
status: Draft
owner: "Owner"
updated_at: "2026-07-27"
stage: "13"
task_id: T06
estimate: M
deps: [T03, T04]
---

# T06 — Host + deploy wiring (Program.cs, Dockerfile, k8s, packages)

## Scope

Wire the Host and deployment to a single Postgres connection string and strip all SQLite / PVC remnants.

- **Program.cs:** read `ConnectionStrings:WiseWizard` only (arrives from Infisical as `CONNECTIONSTRINGS__WISEWIZARD`); no separate Hangfire connection string.
- **Packages (`Directory.Packages.props`):** **add** `Npgsql`, `Hangfire.PostgreSql`; **remove** `Microsoft.Data.Sqlite`, `Hangfire.Storage.SQLite`, and the two `SQLitePCLRaw.*` vuln pins (no longer reachable).
- **Dockerfile:** drop the `/data` `VOLUME`, the `mkdir`/`chown /data`, and the SQLite `ConnectionStrings__*` env defaults. Runtime image still publishes only `WiseWizard.Host`.
- **k8s (`k8s/base/wisewizard`):** remove `pvc.yaml` and the volume/`volumeMount` from `deployment.yaml` (and drop it from the folder `kustomization.yaml`). **Keep** single-replica + `Recreate` strategy (single Telegram long-poll + single nightly run).
- **`k8s/README.md`:** add `CONNECTIONSTRINGS__WISEWIZARD` to the secrets table.
- **`compose.yaml`:** update to supply the Postgres connection string (no SQLite volume).

## Links

- Design: [design doc](../../../superpowers/specs/2026-07-27-postgres-migration-and-aspire-design.md) — "Deploy wiring".
- [ADR-0007](../../../00-overview/adr/0007-postgresql-datastore.md), [ADR-0008](../../../00-overview/adr/0008-aspire-local-dev.md).
- Source: `src/WiseWizard.Host/Program.cs`, `Directory.Packages.props`, `Dockerfile`, `k8s/base/wisewizard/deployment.yaml`, `k8s/base/wisewizard/pvc.yaml`, `k8s/base/wisewizard/kustomization.yaml`, `k8s/README.md`, `compose.yaml`.

## Definition of Done

- `Program.cs` binds only `ConnectionStrings:WiseWizard`; the app starts against a Postgres connection string supplied via `CONNECTIONSTRINGS__WISEWIZARD`.
- `Directory.Packages.props` lists `Npgsql` + `Hangfire.PostgreSql` and no longer references `Microsoft.Data.Sqlite`, `Hangfire.Storage.SQLite`, or the `SQLitePCLRaw.*` pins.
- `Dockerfile` has no `/data` VOLUME, no `mkdir`/`chown /data`, and no SQLite env defaults; the image builds and publishes only `WiseWizard.Host`.
- `pvc.yaml` is deleted and no volume/`volumeMount` remains in `deployment.yaml`; the deployment stays single-replica with `Recreate`.
- `k8s/README.md` secrets table includes `CONNECTIONSTRINGS__WISEWIZARD`; `compose.yaml` supplies the Postgres connection string.
- Solution builds; the Host is excluded from the coverage gate as today.
