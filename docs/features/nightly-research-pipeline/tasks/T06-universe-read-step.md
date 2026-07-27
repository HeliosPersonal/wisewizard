# T06 — Universe read step

**Owner:** Owner · **Est:** S · **Deps:** T04

## Scope
Add the first pipeline step: read the Universe = distinct Tickers of `positions` (ibkr-portfolio-read) ∪ `watchlist` (watchlist-management), read-only via their owning features' Core repository interfaces. Create the `runs` row (status `pending`) and record the Universe scope for the Run. Handle empty Universe gracefully.

## Out of scope
Ingestion (T07); redefining `positions`/`watchlist` (owned elsewhere — reference only).

## DoD
- Step returns the deduplicated Universe for the Run and persists a new `runs` row.
- Empty Universe → Run can finish with zero Verdicts, no LLM Batch submitted (test; PRD edge case).
- Realizes [seq-nightly-run](../diagrams/seq-nightly-run.md) steps 2-3.

## Links
[PRD.md §5 AC-01](../PRD.md) · [data-model.md](../data-model.md) · [CONTEXT.md](../../../00-overview/CONTEXT.md) (Universe)
