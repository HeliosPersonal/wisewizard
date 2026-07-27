---
status: Confirmed
owner: "Owner"
reviewers: []
updated_at: "2026-07-26"
feature_size: L
stage: "01"
ticket: "N/A — personal project"
value_score:
  rice: 18.0
  state: confirmed
  confirmed_at: "2026-07-26"
feasibility_state: confirmed
---

<!-- Stage 01 idea brief for the WiseWizard system as a whole. Product language only. -->

# Idea Brief — WiseWizard

## 1. Raw idea

The Owner wants to lazily buy stocks and maintain a portfolio (held at Interactive Brokers), managed through a Telegram bot. Idea: a platform where a nightly, cheap batch pipeline gathers data from free public sources, a cheap model filters relevance, and a top model summarizes — producing a morning digest that shows whether to keep current holdings, trim them, or consider new candidates. The long-term vision is an AI investment-research platform that behaves like a fund's research desk, spending most of its compute on research rather than on answering.

## 2. Problem

An individual investor with a "lazy" (days-to-weeks horizon) style cannot continuously track news, filings, and fundamentals across all holdings and candidates. Manually checking each Ticker daily is time-consuming and inconsistent, so signals (a filing, a downgrade, a material event) are missed and holding/trim decisions are made on stale information.

## 3. Users

A single Owner — a technically capable individual investor who holds a modest portfolio (~10-20 Positions) plus a watchlist of similar size, checks it in the morning, and prefers to read a 30-second digest and act manually. Frequency: daily.

## 4. Why now

The Owner is actively managing a real IBKR portfolio and wants to reduce daily research effort. Batch LLM APIs now make bulk overnight analysis cheap enough for a personal budget, which is the enabling trigger.

## 5. Out of scope

- Automated order execution — the Owner trades manually.
- Paid social-media feeds (X/Twitter).
- Market-wide automatic idea screening.
- Full multi-agent committee (Macro/Sector/Bull/Bear) — future phase.
- Multi-user / SaaS.

## 6. Competitive analysis

| # | Product · URL | Features | Value (1-5) | Gap |
|---|---|---|---|---|
| 1 | Seeking Alpha · seekingalpha.com | Crowd analysis, ratings, alerts | 3 | Generic, not tied to the Owner's actual holdings; no overnight per-position verdict |
| 2 | Bloomberg Terminal · bloomberg.com | Professional data + news | 5 | Cost prohibitive for an individual; overkill |
| 3 | Broker research (IBKR, etc.) · interactivebrokers.com | Analyst reports, screeners | 3 | Not synthesized into a lazy morning digest; requires active pulling |
| 4 | Generic LLM chat (ChatGPT/Claude) · — | Ask-anything analysis | 2 | Answers on demand, no continuous research, no portfolio state, no evidence discipline |

Footnote: comparison based on general product knowledge as of 2026-07; no live search performed for this personal project.

## 7. Strategic approaches

### Approach A — Lazy nightly digest MVP
- **Thesis**: A single-process app reads the portfolio read-only, runs an overnight cheap→smart model cascade over portfolio + watchlist, and delivers a morning traffic-light digest with drill-down.
- **For whom**: The Owner who wants 30-second mornings and manual trades.
- **Outcome metric**: Owner reviews a per-position verdict daily — baseline 0 → target ≥5 of 7 mornings/week.
- **Key trade-off**: Free sources are noisy; quality depends on model filtering.
- **Effort signal**: M
- **Recommended?** ●

### Approach B — Full research-desk platform now
- **Thesis**: Build the six-phase macro→sector→company→contradiction→thesis→portfolio pipeline with a multi-agent committee from day one.
- **For whom**: The Owner as future power user.
- **Outcome metric**: Quality of research vs a human desk — unmeasurable at MVP.
- **Key trade-off**: Months of work, high cost, open questions on LLM alpha.
- **Effort signal**: L
- **Recommended?** ◯

### Approach C — Trading automation first
- **Thesis**: Wire up order placement with confirmations before deep research.
- **For whom**: The Owner wanting hands-off execution.
- **Outcome metric**: Orders placed via bot.
- **Key trade-off**: Real-money risk, safety complexity, no research value yet.
- **Effort signal**: M
- **Recommended?** ◯

## 8. Multi-perspective feedback

### Engineer
- Free-source ingestion is achievable but noisy; needs dedup + relevance filtering.
- Broker integration requires a persistent local gateway session — the fragile part.
- Batch async pipeline maps cleanly onto a persistent job engine with retries.
- Single-process design keeps ops trivial on the Owner's own server.
- LLM output must be structured and evidence-linked to be trustworthy.

### Executive
- Delivers real value from day one (morning digest) at very low running cost.
- Read-only scope removes financial-loss and regulatory risk.
- Incremental path to the grander research-platform vision protects the investment.

### UX-researcher
- 30-second digest respects "lazy" intent; drill-down serves depth on demand.
- Telegram is a natural low-friction channel the Owner already uses.
- Daily 2FA re-auth is a friction point but acceptable if it is a single tap.

### Synthesis matrix
|         | Engineer | Executive | UX |
|---------|:--------:|:---------:|:--:|
| App. A  | +        | +         | +  |
| App. B  | -        | 0         | 0  |
| App. C  | 0        | -         | -  |

- A: buildable, cheap, respects lazy intent.
- B: valuable someday, too big now.
- C: risky, no research payoff yet.

## 9. Trade-offs and edge cases

### Trade-offs per approach
| Approach | Pros | Cons |
|---|---|---|
| A | Fast to value, cheap, low risk, incremental | Free-source noise, session upkeep |
| B | Maximal depth | Months, costly, unproven alpha |
| C | Hands-off trades | Real-money risk, no research |

### Edge cases
- Broker session expires mid-Run (daily forced logout).
- A Source is down or rate-limits during ingestion.
- A Batch job is still pending at wake-up time.
- A Ticker has zero fresh documents on a given night.
- Owner adds a Ticker to the watchlist mid-day.
- Process restarts while a Run is in flight.
- Two Tickers map to the same company / duplicate news.
- Portfolio is empty or watchlist is empty.

## 10. Risks

- **Devil's advocate**: LLMs may produce confident but wrong investment conclusions from noisy free sources, and the Owner may over-trust a 🟢/🔴. Mitigation: mandatory evidence citation, "what changed" deltas, and framing as advisory only.
- Broker session fragility could silently stale the portfolio snapshot.
- Free-source coverage gaps could miss material events (accepted MVP limitation).

## 11. RICE — proposed

- **Reach (R)**: 1 (single Owner, but daily use — reach scored as users) → normalized to 1 user, high frequency.
- **Impact (I)**: 3 (massive per-user impact — replaces daily manual research).
- **Confidence (C)**: 0.8 (clear MVP, some open questions on source quality — §15).
- **Effort (E)**: person-weeks for the MVP as scoped.
- **RICE = R × I × C / E ≈ 18** (personal-project heuristic, not a team backlog score).
- **State**: confirmed.

## 12. Feasibility — proposed

- [☑] **Tech**: .NET Generic Host + Hangfire + SQLite + HttpClient to Anthropic/EDGAR/RSS/IBKR Client Portal — all mature, no exotic infra.
- [☑] **Skills**: Owner is fluent in .NET/C#.
- [☑] **Time**: MVP scoped into five features, each a few days of work on an existing always-on server.
- **State**: confirmed.

## 13. Recommendation

**Selected: Approach A** — Build the lazy nightly-digest MVP first. It scores highest across all three perspectives (§8), delivers value from day one at negligible cost, and keeps the System read-only to eliminate financial risk. RICE ≈ 18 (§11) with confirmed feasibility (§12) on a familiar .NET stack. It fills the gap none of the competitors cover (§6): an overnight, evidence-based, per-position verdict tied to the Owner's actual holdings, delivered as a lazy morning digest. Approaches B and C are parked, not rejected — the architecture is deliberately incremental toward B.

**Locked-in pointer**: MVP = five features — IBKR portfolio read (read-only), watchlist management, data ingestion (free sources), nightly research pipeline (model cascade over Batch API), and Telegram bot reporting (digest + drill-down) — on a single-process .NET host with SQLite and Hangfire.

## 14. Parked & rejected approaches
| # | Approach | Status | Reason | Revisit trigger |
|---|---|:---:|---|---|
| B | Full research-desk platform | parked | Too large for MVP; unproven value | After MVP proves digest quality |
| C | Trading automation first | parked | Real-money risk, no research value | If Owner wants hands-off execution later |
| - | Paid X/Twitter feed | rejected | Cost + fragility vs free secondhand news | — |

## 15. Open questions
- [ ] Which specific free news RSS feeds give best signal/noise? — owner: Owner, due: during data-ingestion PRD
- [ ] Is Yahoo Finance unofficial data reliable enough, or is a free-tier API needed? — owner: Owner, due: during data-ingestion PRD
- [ ] Acceptable monthly Anthropic spend ceiling for the nightly Run? — owner: Owner, due: before first production Run

## Related
- [CONTEXT](./CONTEXT.md)
- Feature specs under `docs/features/`

## DoD self-check
- [x] 15 sections present
- [x] No implementation anti-pattern terms in problem/users sections
- [x] Length within bounds
- [x] Frontmatter status: Confirmed
- [x] RICE confirmed
- [x] Feasibility confirmed
- [x] Recommendation cites §6, §8, §11, §12
