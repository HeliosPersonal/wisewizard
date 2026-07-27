# T02 — Domain models: Run, ExtractedFact, Verdict, Signal

**Owner:** Owner · **Est:** S · **Deps:** none

## Scope
Add `Run`, `ExtractedFact`, `Verdict` models and the `Signal` type (🟢 hold / 🟡 attention / 🔴 review) to `WiseWizard.Core/Models`. Use glossary terms verbatim ([CONTEXT.md](../../../00-overview/CONTEXT.md)). Core has zero external dependencies (sad.md §5).

## Out of scope
Persistence mapping (T04), LLM DTOs (T03).

## DoD
- Models exist in Core with fields matching [data-model.md](../data-model.md) (Run status set, Verdict with `sources`, `change_from_yesterday`, etc.).
- `Signal` enumerates hold/attention/review only.
- Core project still references nothing external; unit test constructs each model.

## Links
[data-model.md](../data-model.md) · [CONTEXT.md](../../../00-overview/CONTEXT.md) glossary
