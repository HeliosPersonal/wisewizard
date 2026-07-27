# WiseWizard deployment (k8s + CI/CD)

Infrastructure for running **WiseWizard** on the **helios** home-lab k3s cluster as a tenant
on its shared infra, following the same conventions as Sentra (`docs/overflow/`).

> **Scope:** dev/staging. Production is a future overlay/branch.

## What's here

```
k8s/
├── base/
│   └── wisewizard/            # single deployable unit — deployment/service
│       ├── deployment.yaml    # app + infisical-creds + config + IBKR sidecar placeholder
│       ├── service.yaml       # ClusterIP (no Ingress — outbound-only app)
│       └── kustomization.yaml
├── overlays/
│   └── staging/               # apps-staging: image tag, env=Staging, config
│       └── kustomization.yaml
└── README.md

src/WiseWizard.Host/Dockerfile # multi-stage build (context = repo root)

.github/workflows/deploy.yml    # build+test (>95% coverage gate) → GHCR image → apply to apps-staging
```

## Single deployable unit

| Unit | Image | Inbound | Notes |
|---|---|---|---|
| `wisewizard` | `ghcr.io/heliospersonal/wisewizard` | none (ClusterIP) | Telegram long-poll + broker session + nightly Hangfire pipeline; stores domain data in an external PostgreSQL (no PVC) |

Listens on `:8080` for `/health` (k8s probes) and the `/hangfire` dashboard (reach via
`kubectl port-forward`, never Ingress). No public inbound traffic — Telegram is an outbound
long-poll; IBKR and Anthropic are outbound calls.

Domain data lives in an **external PostgreSQL** — a separate database inside the shared helios
Postgres, provisioned by the Owner with a scoped user (ADR-0007). Hangfire shares that same
database under a dedicated `hangfire` schema, so the pod is stateless: no PVC, no `/data` volume.

## IBKR Client Portal gateway (sidecar)

WiseWizard reads the portfolio **read-only** over the IBKR Client Portal gateway's local REST
API, so the gateway must run **in the same pod** (reachable at `https://localhost:5000/`). The
sidecar is a documented **placeholder** in `deployment.yaml` — IBKR does not publish a gateway
image, so supply your own and complete the **daily 2FA re-auth** (ADR-0006), e.g. via the IBKR
mobile app when prompted. Until the sidecar is enabled, the app runs and serves `/health`; broker
reads simply fail and are retried (graceful degradation).

## Secrets — Infisical

Nothing sensitive is baked into images or committed. The pod's Infisical SDK
(`AddEnvVariablesAndConfigureSecrets()` in `Program.cs`) pulls real secrets at runtime. The
**only** in-cluster secret is the bootstrap `infisical-creds` (client-id / client-secret /
project-id), referenced via `secretKeyRef`. Non-secret config (IBKR gateway URL, SEC User-Agent)
comes from the overlay-generated `wisewizard-config` ConfigMap.

Secret keys use `SCREAMING_SNAKE_CASE` with `__` as the section separator (maps to `:` in .NET
config). Expected secrets in the Infisical `staging` environment:

| Secret | .NET config key | Purpose |
|---|---|---|
| `CONNECTIONSTRINGS__WISEWIZARD` | `ConnectionStrings:WiseWizard` | Domain PostgreSQL (Hangfire shares it under a `hangfire` schema) |
| `ANTHROPIC__APIKEY` | `Anthropic:ApiKey` | Anthropic Batch API |
| `TELEGRAM__BOTTOKEN` | `Telegram:BotToken` | Telegram bot |
| `TELEGRAM__OWNERCHATID` | `Telegram:OwnerChatId` | Owner allowlist chat id |
| `IBKR__ACCOUNTID` | `Ibkr:AccountId` | IBKR account to read |

Create the bootstrap secret once per namespace:

```bash
kubectl -n apps-staging create secret generic infisical-creds \
  --from-literal=client-id=$INFISICAL_CLIENT_ID \
  --from-literal=client-secret=$INFISICAL_CLIENT_SECRET \
  --from-literal=project-id=$INFISICAL_PROJECT_ID
```

## Deploy

**CI (GitHub Actions).** Push to `main` touching `src/**`, `tests/**`, or `k8s/**` →
`deploy.yml`: build + tests with the **>95% coverage gate** → build/push GHCR image
(`sha-<12>` + `latest`) → pin the tag with `kustomize edit set image` →
`kubectl apply -k k8s/overlays/staging` → wait for rollout. The `deploy-staging` job runs in the
`staging` GitHub Environment — add a required reviewer there to gate apply.

**Local dev (one command).** For iterating on the app itself, .NET Aspire provisions a Postgres
container and injects `ConnectionStrings:WiseWizard` into the Host, wiring Anthropic/Telegram/IBKR
config from `dotnet user-secrets` (ADR-0008):
```bash
dotnet run --project src/Aspire/WiseWizard.AppHost
```
The integration test suite uses **Testcontainers** (a real throwaway Postgres), so **Docker must
be running** to run `WiseWizard.Infrastructure.Tests` locally.

**Local (cluster).**
```bash
# Render (no cluster needed)
kubectl kustomize k8s/overlays/staging

# Apply (needs a helios kubeconfig)
kubectl apply -k k8s/overlays/staging
kubectl -n apps-staging rollout status deploy/wisewizard

# Dashboard / health while running
kubectl -n apps-staging port-forward deploy/wisewizard 8080:8080
#   → http://localhost:8080/health , http://localhost:8080/hangfire
```

**Build + run the image locally:**
```bash
docker build -f src/WiseWizard.Host/Dockerfile -t wisewizard:local .
docker compose up   # starts a postgres:17-alpine + the app (serves /health + /hangfire on :8080)
```

## Required CI secrets

`GITHUB_TOKEN` (auto — GHCR push) · `KUBE_CONFIG_HELIOS` (base64 kubeconfig for helios).

```bash
base64 -w 0 ~/.kube/config   # value for KUBE_CONFIG_HELIOS
```

## TODOs

- **IBKR gateway sidecar image** — supply and enable in `base/wisewizard/deployment.yaml`.
- **Production overlay/branch** — future (only `staging` today).
- **Persistence** — delegated to the external PostgreSQL (separate DB inside the shared helios Postgres, provisioned by the Owner); the pod holds no state, so there is no PVC/StorageClass to manage.
