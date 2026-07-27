---
status: Done
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-27"
feature_size: M
stage: "13"
ticket: "N/A — personal project"
---

# Task tracker — postgres-migration

Epic: [_epic.md](../_epic.md). Status values: `todo` · `in-progress` · `in-review` · `done`.

All tasks implemented and verified: solution builds with 0 warnings; 426 tests pass
(Core 145, Bot 97, Infrastructure 184) with the >95% line+branch coverage gate holding;
the Docker image builds and serves `/health` against a real PostgreSQL container (domain
tables in `public`, Hangfire under the `hangfire` schema); the staging kustomize overlay
renders clean with no PVC.

| # | Task | Est | Deps | Owner | Status |
|---|---|---|---|---|---|
| [T01](./T01-db-connection-factory.md) | `IDbConnectionFactory` + `NpgsqlConnectionFactory` | S | — | Owner | done |
| [T02](./T02-schema-postgres-dialect.md) | `SchemaInitializer` Postgres dialect | S | T01 | Owner | done |
| [T03](./T03-repository-dialect.md) | Repository dialect fixes (`RETURNING`, `ON CONFLICT`, numeric) | M | T02 | Owner | done |
| [T04](./T04-hangfire-postgres.md) | Hangfire on Postgres (`hangfire` schema) | S | T02 | Owner | done |
| [T05](./T05-testcontainers-harness.md) | Testcontainers Postgres harness; port integration tests | M | T01, T02, T03 | Owner | done |
| [T06](./T06-host-and-deploy-wiring.md) | Host + deploy wiring (Program.cs, Dockerfile, k8s, packages) | M | T03, T04 | Owner | done |
| [T07](./T07-aspire-local-dev.md) | Aspire `AppHost` + `ServiceDefaults` | M | T01 | Owner | done |
