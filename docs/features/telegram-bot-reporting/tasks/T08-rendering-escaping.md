---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T08
deps: []
est: S
---

# T08 — Message rendering & escaping utilities

## Goal

Shared rendering helpers used by every formatter: escape all dynamic values (Ticker symbols, notes, Verdict reasoning, Source titles) so no user- or Source-supplied text can break message formatting or embed active content, and a size-aware chunker that splits text at safe boundaries within the per-message character ceiling.

## Scope

- Escaper for the chosen Telegram message-formatting mode; unit-covered for the full set of special characters.
- Signal-glyph mapping (🟢 hold / 🟡 attention / 🔴 review) from the stored Signal value.
- Chunker: splits at Ticker boundaries (digest) or Source boundaries (detail) so no chunk exceeds the size ceiling, preserving order.

## Links

- PRD: [PRD.md](../PRD.md) §6 (max Tickers/message, message size ceiling), §6.1 (injection abuse case), §5 AC-09.
- CONTEXT: Signal glossary (🟢/🟡/🔴).

## Out of scope

- The handlers/formatters that call these helpers (T04–T06); network send.

## DoD

- Unit tests: markup characters in Ticker/note/reasoning/Source render literally (no formatting break, no active content).
- Unit tests: chunker respects the size ceiling and never splits mid-boundary; order preserved.
- Signal-glyph mapping covers all three Signal values and an unknown/defensive default.
