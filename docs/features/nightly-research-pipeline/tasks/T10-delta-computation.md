# T10 — Delta computation (vs previous Run)

**Owner:** Owner · **Est:** M · **Deps:** T09

## Scope
Compute `change_from_yesterday` per Ticker by comparing the current candidate Verdict against the previous completed Run's Verdict for the same Ticker (read via the `(ticker, created_at DESC)` index). If no previous Verdict exists (first Run or newly added Ticker), mark the Verdict as `new` rather than fabricating a change.

## Out of scope
Persisting Verdicts (T11); prior-Verdict read query lives in T04's repo.

## DoD
- With a seeded prior Verdict, delta states what changed (AC-02) — snapshot test.
- With no prior Verdict, delta is marked `new`, never a fabricated change (AC-06) — test.
- Pure function unit-tested with zero network.

## Links
[PRD.md §5 AC-02, AC-06](../PRD.md) · [data-model.md](../data-model.md) (`idx_verdicts_ticker_created`) · [CONTEXT.md](../../../00-overview/CONTEXT.md) (previous Verdict = prior completed Run)
