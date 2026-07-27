# T12 — Cost + token logging and per-Run ceiling enforcement

**Owner:** Owner · **Est:** M · **Deps:** T09

## Scope
Accumulate per-tier token/cost from `ILlmClient` results into `runs` (`cost_cheap_usd`, `cost_synthesis_usd`, `cost_total_usd`, `tokens_cheap`, `tokens_total`). After each tier, project the remaining cost; if the projected total would exceed the configured per-Run ceiling, stop committing further work and hand off to the failure path (T15) with reason "cost ceiling reached". Expose cheap-tier token share for the ≥80% NFR.

## Out of scope
The alert mechanism itself (T15); tier prompts (T08/T09).

## DoD
- Per-Run cost/tokens recorded and summed; cheap-tier share computed.
- Projected-over-ceiling stops the Run before publishing a partial Verdict set (AC-07) — integration test.
- Config-driven ceiling (default 2.00 USD, PRD §6); ceiling value not hardcoded.

## Links
[PRD.md §6 NFR, §5 AC-07](../PRD.md) · sad.md §10 QG-1 · [seq-run-failure](../diagrams/seq-run-failure.md) (ceiling path)
