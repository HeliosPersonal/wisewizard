---
status: Draft
owner: "Owner"
reviewers: ["Owner"]
updated_at: "2026-07-26"
feature_size: L
stage: "15"
ticket: "N/A — personal project"
---

# Test plan — nightly-research-pipeline

<!-- Realizes PRD.md §5 (AC) + §6 (NFR). Pipeline logic is unit-tested against saved LLM fixtures with zero network (sad.md §5). -->

## Levels

| Level | Scope | Tooling |
|---|---|---|
| Unit | Pure pipeline logic: relevance/extraction mapping, delta computation, evidence-invariant guard, cost accounting, status transitions | xUnit + fixtures |
| Component | Full cascade over mocked `ILlmClient` returning **saved LLM fixtures** (cheap + synthesis) and an in-memory/temp SQLite domain DB; **zero network** | xUnit + Moq + saved fixtures + temp SQLite |
| Integration | Hangfire continuation chain + persistence + resume-after-restart against a real temp SQLite domain DB and Hangfire SQLite file | xUnit + Hangfire + temp SQLite |
| Contract | Structured cheap-tier and synthesis-tier request/response contracts validated against fixture schemas | schema assertions over saved fixtures |
| Live (opt-in) | Real `AnthropicLlmClient` Batch submit/poll/retrieve, gated, off by default | opt-in integration, real API |

## AC coverage

| AC | Test(s) | Level |
|---|---|---|
| AC-01 (happy: Verdict per Ticker w/ Signal + summary) | `Run_over_universe_produces_one_verdict_per_ticker_with_signal_and_summary` | Component |
| AC-02 (delta present) | `Delta_states_change_vs_previous_run_verdict` | Unit |
| AC-03 (batch fail/timeout → clean fail + alert + prior preserved) | `Batch_failure_fails_run_cleanly_alerts_and_keeps_previous_verdicts` | Integration |
| AC-04 (advisory-only, never an order) | `Verdict_persistence_never_triggers_broker_order` (asserts no Broker write path invoked) | Component |
| AC-04b (only scheduled System initiates a Run) | `Ad_hoc_request_does_not_start_a_run` | Unit |
| AC-05 (evidence invariant: block evidence-less Verdict) | `Conclusion_without_citable_fact_is_blocked_and_recorded_as_no_evidence` | Component |
| AC-06 (cross-context: no prior Verdict → mark new) | `First_run_or_new_ticker_marks_verdict_as_new_not_fabricated_change` | Unit |
| AC-07 (cost ceiling → stop + alert + prior preserved) | `Projected_cost_over_ceiling_stops_run_and_alerts` | Integration |
| AC-08 (resume after restart, 0 repeated steps / 0 duplicates) | `Restart_mid_run_resumes_batch_without_repeating_extraction_or_duplicating_verdicts` | Integration |
| AC-09 (empty evidence for a Ticker) | `Ticker_with_no_fresh_documents_records_no_evidence_and_run_still_completes` | Component |

## Edge cases / error paths
- Empty Universe (no Positions, no Watchlist) → expected: Run finishes with zero Verdicts, no error, no LLM Batch submitted.
- Ticker with zero fresh Raw documents → expected: recorded as "no fresh evidence this Run"; no fabricated Verdict (AC-09).
- Two Tickers citing the same Raw document → expected: each Verdict independently cites the shared document; no dedup collision.
- Synthesis proposes a conclusion but cites no fact → expected: blocked, recorded as no evidence (AC-05).
- Batch pending past max wall-clock → expected: Run fails cleanly with `failure_reason` timeout, Owner alerted (AC-03).
- Restart after cheap tier persisted, synthesis batch pending → expected: cheap tier not re-run; same batch id resumed (AC-08).
- Restart after Verdicts partially inserted → expected: `(run_id, ticker)` PK makes re-insert idempotent; no duplicates (AC-08).
- Cost ceiling reached between tiers → expected: no partial Verdict set published; prior Run intact (AC-07).

## Test data
- Strategy: saved LLM fixtures (canned cheap-tier extraction responses and synthesis-tier Verdict responses) checked into the test project; seeded prior-Run Verdicts for delta tests; factory builders for `runs`, `raw_documents`, `extracted_facts`.
- Cleanup: per-test temp SQLite files (domain + Hangfire) created and deleted per test; no shared network state.

## NFR validation
- Cheap-tier token share ≥ 80% → assert recorded `tokens_cheap / tokens_total ≥ 0.80` on a representative fixture Run.
- Per-Run cost ≤ ceiling → seed tier costs; assert Run stops and alerts when projected total exceeds the configured ceiling (AC-07).
- Poll interval = 5 min (configurable) → assert the poll job's configured interval; use a virtual clock in tests, not real waits.
- Max Run wall-clock ≤ 20h → assert timeout enforced via injected clock; Run transitions to `failed` on expiry.
- Resume-after-restart: 0 repeated steps / 0 duplicate Verdicts → assert re-executed step count = 0 and duplicate Verdict rows = 0 after a simulated restart.
- Batch discount usage: 0 synchronous LLM calls → assert the mock `ILlmClient` records only Batch submit/poll/retrieve, never a synchronous call.

## CI
- On PR: Unit + Component + Integration suites (all offline, using saved fixtures and temp SQLite) must pass.
- Nightly: same suites; Live opt-in suite excluded from CI, run manually before a production cutover.
