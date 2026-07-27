---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T08
deps: [T05, T06, T07]
estimate: M
---

# T08 — Unit + integration test suite

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Complete the test suite so every §5 AC and every NFR target is verified, per the test plan.

## Scope

- Unit tests (xUnit + Moq): normalization/validation, dedup decision, size cap, note length, owned-Position exclusion, authorization guard contract.
- Integration tests (xUnit + real SQLite temp file): add/exists/list/count/remove, PK-dedup backstop, durability across reopen.
- Timing assertions for the change-ack (≤ 500 ms) and list (≤ 200 ms) NFRs as soft signals.
- Fill the [test-plan AC coverage table](../test-plan.md) with the concrete test names.

## Upstream (link, do not duplicate)

- [test-plan](../test-plan.md) — levels, AC coverage, edge cases, NFR validation.
- [PRD §5 AC-01..AC-08; §6 NFR](../PRD.md)

## Definition of Done

- Every AC-01..AC-08 has ≥ 1 passing test mapped in the test-plan table.
- Edge cases from the test plan (casing, dot/hyphen symbols, length bounds, size cap, empty list, durability) covered.
- Suite green in CI on push.
