---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T04
deps: [T03, T08, T09]
est: M
---

# T04 — `/portfolio` handler + Position formatter

## Goal

Handle `/portfolio`: read the current Positions and render a summary — one line per Position with its holding and profit-or-loss — plus how current the Portfolio is (its `as_of` age). Handle the empty-Portfolio state distinctly from "no data".

## Scope

- Handler that reads all current Positions via the Position repository (T09).
- Position formatter (uses T08 escaping): per-line Ticker, quantity/market value, unrealized P&L; header noting the Portfolio's age.
- Empty state: "no current Positions" message, distinct from an unavailable/stale read.
- Stale surfacing: show the age; do not suppress (per PRD §8 open question default).

## Links

- PRD: [PRD.md](../PRD.md) §5 AC-03 (Portfolio summary); §6 (portfolio latency ≤ 2 s).
- Data model: [data-model.md](../data-model.md) — reads `positions` (owned by ibkr-portfolio-read).
- Diagram: none dedicated — mirrors the read pattern in [seq-daily-digest](../diagrams/seq-daily-digest.md).

## Out of scope

- Reading the Broker or refreshing Positions (ibkr-portfolio-read owns that); computing P&L (comes from the Position row).

## DoD

- Integration test over seeded `positions`: summary shows holding + P&L per Position and the `as_of` age (AC-03).
- Test: empty Portfolio renders the "no current Positions" state.
- Test: stale `as_of` is surfaced with the summary.
- Latency check: formatter + repo read within the §6 budget on the seeded DB.
