# MSG_Sell Account Block Rule Plan

## Scope

Port the seller account block gate from the canonical TMProject 7.69 handler.
Record that W2PP omits this gate, and model the decision and notice intent
without connecting `_MSG_Sell` dispatch or account lookup.

## Tasks

- [x] Compare the account gate and its source order in 7.69 and W2PP.
- [x] Confirm the C# account snapshot already carries `IsBlocked`.
- [x] Add tests for the blocked and unblocked decisions.
- [x] Verify RED on the missing API, then implement the minimal pure rule.
- [x] Run Engine.Tests and the full solution build.
- [x] Compare the 43 existing failure names with the prior run.
- [x] Update `PROGRESSO.md`, `BACKLOG.md`, and `Server/docs/PORT_TRACEABILITY.md`.

## Validation

- Engine.Tests: 523/566; the same 43 test names fail as in the preceding run.
- Full `WydCdk.Engine.slnx` build: 0 warnings, 0 errors.
- No handler dispatch, Host smoke, visual E2E, schema, deploy, commit, or push.

## Follow-up

Integrate authoritative account state and the canonical 7.69 notice in the
handler. Next uncovered seller behavior is the volatile-potion restriction.
