---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T05
deps: [T03, T08, T09]
est: M
---

# T05 — `/report` digest formatter (Signal + reason, chunking, buttons)

## Goal

Handle `/report`: resolve the latest **completed** Run, read its Verdicts, and render the Daily digest — one line per Ticker with its Signal (🟢/🟡/🔴) and a one-phrase reason — with a per-Ticker "details" inline button. Chunk into ordered messages when the digest exceeds the size limits. Show a plain empty state when no completed Run exists.

## Scope

- Handler reads latest-completed Run (T09 Run repo) then its Verdicts (T09 Verdict repo), ordered by Signal then Ticker.
- Digest formatter (uses T08 escaping): one line per Ticker = Signal glyph + Ticker + one-phrase summary; attaches an inline "details" button per Ticker encoding (Run reference + Ticker).
- Chunking: ≤ 20 Ticker lines and ≤ 4000 chars per message; split at Ticker boundaries into ordered messages, every Ticker line + its button preserved, none dropped.
- Empty state: no completed Run → "no digest available yet", nothing resembling Verdicts.
- Domain invariant: never read an in-progress Run — the Run repo query selects only completed.

## Links

- PRD: [PRD.md](../PRD.md) §5 AC-01 (happy), AC-04 (empty state), AC-06 (latest completed Run only), AC-09 (chunking); §6 (digest latency, max Tickers/message, size ceiling).
- Diagram: [seq-daily-digest](../diagrams/seq-daily-digest.md) (happy, invariant, chunking, empty).
- Data model: [data-model.md](../data-model.md) — reads `runs` + `verdicts` (owned by nightly-research-pipeline).

## Out of scope

- Computing Verdicts/Signals (nightly-research-pipeline); the drill-down body (T06).

## DoD

- Integration test: completed Run with mixed Signals → one line per Ticker with Signal + reason + a details button each (AC-01).
- Test: an in-progress Run present alongside an older completed Run → digest uses only the completed Run (AC-06).
- Test: no completed Run → "no digest available yet" (AC-04).
- Unit test: 21+ Tickers or > 4000 chars → multiple ordered messages, no Ticker dropped, buttons preserved (AC-09).
- Latency check within §6 budget on the seeded DB.
