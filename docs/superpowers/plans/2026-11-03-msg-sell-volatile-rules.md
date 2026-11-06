# MSG_Sell EF_VOLATILE Rule Plan

## Scope

Port the shared 7.69/W2PP sell restriction using the resolved
`EF_VOLATILE` value. Preserve the exact equality check and notice intent while
keeping item lookup and message dispatch outside this rule slice.

## Tasks

- [x] Compare the 7.69 and W2PP sell gates and response order.
- [x] Confirm both references use item effect index 38 for `EF_VOLATILE` and C# maps it likewise.
- [x] Add tests proving only value 1 blocks and requests the potion notice.
- [x] Verify RED on the missing API, then implement the pure rule.
- [x] Run Engine.Tests and the full solution build.
- [x] Compare the prior 43 failure names and update project handoff/status docs.

## Validation

- Engine.Tests: 524/567; the same 43 test names fail as in the preceding run.
- Full `WydCdk.Engine.slnx` build: 0 warnings, 0 errors.
- SDK 10.0.401; `dotnet --info` exits 1 because access to Service Control Manager is denied.
- No handler dispatch, Host smoke, visual E2E, schema, deploy, commit, or push.

## Follow-up

Resolve the authoritative item's volatile value at the handler boundary and
relay the localized notice. Next uncovered gate is `GetRecycleBin` after price
calculation.
