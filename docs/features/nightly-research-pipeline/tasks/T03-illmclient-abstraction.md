# T03 — `ILlmClient` abstraction + Batch DTOs (Core)

**Owner:** Owner · **Est:** S · **Deps:** none

## Scope
Define `ILlmClient` in `WiseWizard.Core/Abstractions` with Batch submit/poll/retrieve operations, and the tier-agnostic request/response DTOs. Provider-neutral: no Anthropic types in Core (ADR-0005 — provider abstracted). Model both tiers (cheap, synthesis) through the same interface; note the optional Sonnet middle tier as an extension point in a doc comment, not as MVP.

## Out of scope
The concrete Anthropic implementation (T05); synchronous calls (none — Batch only, PRD §6).

## DoD
- `ILlmClient` exposes submit-batch, poll-batch-status, retrieve-batch-results; no synchronous single-call method.
- DTOs carry per-tier token/cost fields for cost accounting (T12).
- Core builds with zero external dependencies.

## Links
[ADR-0005](../../../00-overview/adr/0005-model-cascade-batch-api.md) · sad.md §5
