---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
ticket: "N/A — personal project"
task_id: T13
deps: [T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11, T12]
estimate: M
branch: feat/ingest-test-suite
---

# T13 — Test suite + fixtures + load harness

## Goal

Complete the AC-to-test matrix from the test plan: shared fixtures, the opt-in real-Source suite, and the NFR load harness.

## Scope

- Source fixtures (canned EDGAR list, RSS XML, market snapshot) and the Universe builder with an out-of-Universe negative control.
- Ensure every PRD §5 AC (AC-01..AC-08) has a passing test per the [test-plan.md](../test-plan.md) coverage table.
- Opt-in real-Source integration suite tagged so CI excludes it by default (feeds RSS/market reliability open questions).
- Load harness: whole-Universe ≤ 20 min and per-Ticker ≤ 30 s over a synthetic ~40-Ticker Universe with mocked-latency Sources.

## Links

- Test plan: [test-plan.md](../test-plan.md).
- PRD: [PRD.md §5 (all AC), §6 NFR, §7 KPIs](../PRD.md).

## DoD

- All AC-mapped tests green; coverage table in test-plan fully satisfied.
- CI runs Unit + Integration + Contract offline; real-Source suite excluded by default.
- Load harness reports per-Ticker and whole-Universe durations against the NFR budgets.
