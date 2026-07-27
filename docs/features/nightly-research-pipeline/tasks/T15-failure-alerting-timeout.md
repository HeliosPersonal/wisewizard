# T15 — Failure handling + Telegram alerting + max-wall-clock timeout

**Owner:** Owner · **Est:** M · **Deps:** T13

## Scope
Handle terminal failure cleanly across the chain: a Batch failure, a max-Run-wall-clock timeout (default 20h, configurable), or a cost-ceiling stop (from T12) must set `runs.status=failed` with a plain-language `failure_reason`, write NO partial/corrupted Verdicts, and self-alert the Owner via Telegram. The previous completed Run's Verdicts must remain the latest available for the digest.

## Out of scope
Cost projection logic (T12 signals the ceiling); happy-path chain (T13); the Telegram rendering internals (telegram-bot-reporting — this task only invokes the alert).

## DoD
- Batch fail / timeout / ceiling → Run marked `failed`, no partial Verdicts, Owner alerted, previous Run intact (AC-03, AC-07) — integration tests.
- Max wall-clock timeout enforced via injected clock; Run transitions to `failed` on expiry.
- Realizes [seq-run-failure](../diagrams/seq-run-failure.md) (both error paths).

## Links
[PRD.md §5 AC-03, AC-07, §6 NFR](../PRD.md) · sad.md §10 QG-2 · [seq-run-failure](../diagrams/seq-run-failure.md)
