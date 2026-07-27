# T05 — `AnthropicLlmClient`: Batch submit/poll/retrieve (Infra)

**Owner:** Owner · **Est:** L · **Deps:** T03

## Scope
Implement `AnthropicLlmClient` in `WiseWizard.Infrastructure/Llm` against `ILlmClient` (T03), using the Anthropic Message Batches API for both tiers: submit a batch, poll its status, retrieve results. Parse per-request token/cost usage into the DTO fields. Bounded retries with backoff for transient transport failures. All work is Batch-mode — no synchronous calls (PRD §6).

## Out of scope
Prompt/response contract content (T08/T09 own the tier prompts); polling schedule/orchestration (T13/T14 own the Hangfire polling cadence).

## DoD
- `submit` returns a Batch id; `poll` maps provider status to pending/complete/failed; `retrieve` returns per-request results.
- Token/cost usage populated on results.
- Component tests run against saved LLM fixtures with zero network; an opt-in live test exercises the real API (excluded from CI, per [test-plan.md](../test-plan.md)).

## Links
[ADR-0005](../../../00-overview/adr/0005-model-cascade-batch-api.md) · sad.md §5
