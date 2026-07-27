---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
stage: "13"
task_id: T02
deps: [T01]
estimate: S
---

# T02 — Ticker normalization + format validation

→ Epic: [_epic.md](./_epic.md) · Tracker: [tracker.md](./tracker.md)

## Goal

Implement Ticker normalization and format validation in `WiseWizard.Core` as a pure, network-free function used by the domain service (T06).

## Scope

- Normalize: trim surrounding whitespace, uppercase.
- Validate: after normalization, 1–10 characters, each a letter, digit, dot (`.`), or hyphen (`-`); reject everything else.
- Return a clear valid/invalid result the service can turn into an Owner-facing message; do not format Owner text here.

## Upstream (link, do not duplicate)

- [data-model §Domain invariants](../data-model.md) — normalization + format rules.
- [PRD §5 AC-04, AC-07; §6 NFR symbol length](../PRD.md)
- [seq-add-watch error path](../diagrams/seq-add-watch.md)

## Definition of Done

- Pure function, no I/O, no external deps.
- Unit tests cover: lowercase/mixed/space-padded normalize to canonical; `BRK.B` and `RDS-A` accepted; empty, 11-char, and illegal-char symbols rejected (test-plan edge cases).
- Tests pass.
