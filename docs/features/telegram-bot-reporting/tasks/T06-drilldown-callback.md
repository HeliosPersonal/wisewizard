---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T06
deps: [T03, T08, T09]
est: M
---

# T06 — Drill-down callback handler (reasoning + cited Sources)

## Goal

Handle the "details" callback for a Ticker: resolve the latest completed Run, read that Ticker's full Verdict, and render its full reasoning together with the cited Sources the pipeline recorded, plus the "what changed" delta if present. If the requested Ticker has no Verdict in the latest Run, tell the Owner so and show no reasoning or Sources. Acknowledge the callback tap.

## Scope

- Callback handler consuming the parsed (Run reference + Ticker) from T03.
- Reads the single Verdict for (latest-completed Run, Ticker) via T09.
- Detail formatter (uses T08 escaping): reasoning text + a list of cited Sources (titles, escaped) + "what changed" line.
- Absent-Ticker path: "no Verdict for this Ticker in the latest report".
- Acknowledge the callback so the client stops its loading state.

## Links

- PRD: [PRD.md](../PRD.md) §5 AC-02 (reasoning + Sources), AC-02b (Ticker absent), AC-06 (latest completed Run only); §6 (drill-down latency ≤ 1.5 s).
- Diagram: [seq-drilldown](../diagrams/seq-drilldown.md).
- Data model: [data-model.md](../data-model.md) — reads one `verdicts` row by (`run_id`, `ticker`).

## Out of scope

- Producing reasoning or Sources (nightly-research-pipeline); the digest list (T05).

## DoD

- Integration test: Ticker with a Verdict → full reasoning + cited Sources shown, escaped (AC-02).
- Test: Ticker with no Verdict in the latest Run → "no Verdict" reply, no reasoning/Sources (AC-02b).
- Test: resolution always targets the latest completed Run even if a newer Run completed since the digest was sent (AC-06).
- Callback acknowledged in every path; latency within §6 budget.
