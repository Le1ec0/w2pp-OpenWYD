# MSG_Sell Active Trade Rule Plan

## Scope

Capture the active-trade branch at the start of `_MSG_Sell` using TMProject
7.69 and W2PP evidence. Keep this slice as a pure decision until all required
trade-close and auto-trade side effects can be integrated safely.

## Tasks

- [x] Compare the `OpponentID` predicate and `RemoveTrade` call order in both sources.
- [x] Add a pure resolver for zero versus any nonzero opponent ID.
- [x] Test continuation, bilateral removal order, and truthy negative IDs.
- [x] Register the focused test group and run Engine.Tests.
- [x] Build the full solution and compare the existing failure-name set.
- [x] Update `PROGRESSO.md`, `BACKLOG.md`, and `Server/docs/PORT_TRACEABILITY.md`.

## Validation

- Engine.Tests: 522/565; the same 43 test names fail as in the preceding run.
- Full `WydCdk.Engine.slnx` build: 0 warnings, 0 errors.
- No handler dispatch, Host smoke, visual E2E, schema, deploy, commit, or push.

## Follow-up

Integrate the bilateral runtime close and durable auto-trade listing cleanup
without adding PK side effects, then continue with the `TradeMode` gate.
