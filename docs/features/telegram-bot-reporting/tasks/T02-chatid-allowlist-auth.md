---
status: Draft
owner: "Owner"
updated_at: "2026-07-26"
stage: "13"
task: T02
deps: [T01]
est: S
---

# T02 — Chat-id allowlist authorization filter

## Goal

Authorize every incoming update against a single allowlisted Owner chat id before any repository read or reply. A non-allowlisted chat is dropped silently — no reply, no data access — so existence of any data is neither confirmed nor denied.

## Scope

- An `IOwnerAuthorizer` / filter invoked at the top of the dispatch path (before T03 routing).
- Owner chat id sourced from config (provided by T11); comparison for both commands and callback queries.
- Drop path: log the rejected chat id at debug, send nothing.

## Links

- PRD: [PRD.md](../PRD.md) §5 AC-05 (authorization), §6.1 (hide existence from non-Owner).
- SAD: [sad.md](../../../00-overview/sad.md) §11 accepted debt (chat-id allowlist).

## Out of scope

- The routing/handlers themselves (T03+); config plumbing (T11).

## DoD

- Unit test: allowlisted chat passes to the dispatcher; non-allowlisted chat is dropped with no dispatcher call and no outbound send (AC-05).
- Test covers both a command update and a callback-query update from a non-Owner.
- No branch reveals whether data exists for the dropped chat.
