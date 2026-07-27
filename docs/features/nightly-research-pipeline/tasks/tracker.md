---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "13"
ticket: "N/A — personal project"
---

# Tracker — nightly-research-pipeline

Epic: [_epic.md](./_epic.md). All tasks ≤ 1 working day, each a reviewable PR (≤500 LOC preferred). Owner = `Owner`.

| # | Task | Status | Est | Deps | Owner |
|---|---|---|---|---|---|
| T01 | [Migration: runs/facts/verdicts + indexes](./T01-migration-runs-facts-verdicts.md) | To do | S | — | Owner |
| T02 | [Domain models: Run/Fact/Verdict/Signal](./T02-domain-models.md) | To do | S | — | Owner |
| T03 | [ILlmClient abstraction + Batch DTOs](./T03-illmclient-abstraction.md) | To do | S | — | Owner |
| T04 | [Repositories (Dapper)](./T04-repositories.md) | To do | M | T01, T02 | Owner |
| T05 | [AnthropicLlmClient Batch submit/poll/retrieve](./T05-anthropic-llm-client.md) | To do | L | T03 | Owner |
| T06 | [Universe read step](./T06-universe-read-step.md) | To do | S | T04 | Owner |
| T07 | [Ingestion-step handoff](./T07-ingestion-step-handoff.md) | To do | S | T04 | Owner |
| T08 | [Cheap-tier extraction step](./T08-cheap-tier-extraction-step.md) | To do | L | T04, T05 | Owner |
| T09 | [Synthesis step](./T09-synthesis-step.md) | To do | L | T08 | Owner |
| T10 | [Delta computation](./T10-delta-computation.md) | To do | M | T09 | Owner |
| T11 | [Evidence guard + Verdict persistence](./T11-evidence-guard-persistence.md) | To do | M | T09 | Owner |
| T12 | [Cost + token logging + ceiling](./T12-cost-logging-ceiling.md) | To do | M | T09 | Owner |
| T13 | [Hangfire recurring job + continuation chain](./T13-hangfire-chain.md) | To do | M | T10, T11, T12 | Owner |
| T14 | [Resume-after-restart](./T14-resume-after-restart.md) | To do | L | T13 | Owner |
| T15 | [Failure handling + alerting + timeout](./T15-failure-alerting-timeout.md) | To do | M | T13 | Owner |

Statuses: To do / In progress / In review / Done.
