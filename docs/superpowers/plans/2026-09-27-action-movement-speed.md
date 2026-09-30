# Action Movement Speed Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the common 7.69/W2PP movement-speed validation into the C# Action handler and prove the authoritative clamp and shared crack-error increment over loopback.

**Architecture:** Keep the pure Action precondition and speed rules in `LegacyMovementRules`, using authoritative server-side HP and `AttackRun`. HP==0 has precedence; otherwise unavailable HP/AttackRun fails closed, and the allowed speed is `AttackRun & 0xF`. The host records 5 points/type 4 and replaces only the request's `Speed` before range checks and relay. Do not implement the 7.69-only account ban/detection side effect in this slice; record it as a separate unresolved divergence.

**Tech Stack:** C# / .NET 10, `WydCdk.Engine.Tests`, `WydCdk.Client.E2E`, PowerShell smoke tooling.

**Spec:** User-approved bounded scope in chat; project constraints in `AGENTS.md`; next-slice pointer in `BACKLOG.md`; legacy behavior in `W2PP/Source/Code/TMSrv/_MSG_Action.cpp` and `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Action.cpp`.

## Global Constraints

- Target the single 7.670 client contract; compare W2PP against community 7.69 and record divergences explicitly.
- Preserve the existing dirty checkout; do not stage, commit, push, modify Site/production, or overwrite unrelated user work.
- Do not implement the 7.69-only `SendBanAccount` / `SaveInfoDetect` branch in this recut.
- Run the verification battery at the end of the block, per the user's instruction; add the tests before the implementation, but defer interim test runs.
- Publish only a validated runtime-only build to the already-authorized test sandbox; no source, secrets, Site, or production.

## Review Focus

- Speed equal to the allowed low nibble must not add crack points or alter the packet; cover with a pure-rule boundary test.
- Speed above the allowed value must clamp to `AttackRun & 0xF`; test values at the upper nibble boundary and above it.
- A valid action with excessive speed must be relayed with only `Speed` normalized; cover via the packet loopback harness.
- The speed violation must share the same session counter as range/other Action errors and add exactly 5/type 4; cover in `MovementRangeTests` and verify host log increments in the guarded loopback smoke.
- Speed validation must precede the range rejection, matching both legacy sources; cover a packet that violates both gates and assert the combined counter with no movement relay.

---

### Task 1: Port and verify Action movement-speed normalization

**Files:**
- Modify: `src/WydCdk.World/LegacyMovementRules.cs`
- Modify: `src/WydCdk.World/LegacyCrackErrorRules.cs` for named type/points
- Modify: `src/WydCdk.Server/Program.cs` in the Action dispatch branch
- Test: `tests/WydCdk.Engine.Tests/MovementRangeTests.cs`
- Test: `tests/WydCdk.Client.E2E/ActionRangeNetwork.cs`
- Modify: `tools/PortAudit/Test-AutoTradeClose.ps1` (ActionRange log/count assertions)
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `docs/CLIENT_E2E_SMOKE.md`
- Modify: `../BACKLOG.md` and `../PROGRESSO.md`

**Interfaces:**
- Consumes: `LegacyMobCombatState.CurrentScore.AttackRun`, `ActionRequest.Speed`, and the existing `RecordCrackErrorAsync` / `LoginSessionRegistry` counter.
- Produces: `LegacyMovementRules.ResolveActionPreconditions(hp, attackRun, out speed)`; HP==0 precedes missing speed, live sessions fail closed on unavailable authoritative state, and the host continues movement using an `ActionRequest` copy with only `Speed` corrected.

- [x] Add unit cases for equal/under-limit speed, over-limit speed, `AttackRun` low-nibble values 0 and 15, and the exact 5/type 4 shared-counter outcome.
- [x] Add regression coverage for HP-zero precedence over unavailable AttackRun, unavailable HP/speed on live characters, and stopping after threshold logout. Missing WorldHub resources fail closed in the handler.
- [x] Add loopback cases to `ActionRangeNetwork` for each Action opcode: an over-limit speed on a valid one-cell move must relay with the authoritative speed; one packet violating both speed and range must be rejected without moving or relaying.
- [x] Extend `Test-AutoTradeClose.ps1 -ActionRange` assertions to verify type4/5 increment counts and totals from the owned host log; retain its loopback-only target and isolated-copy guards.
- [x] Implement the pure speed rule in `LegacyMovementRules` and wire it into `Program.cs` before the existing range check; log the original and clamped values. Keep the correction limited to `Speed`.
- [x] Run the complete engine test suite and solution build after the implementation, then run `Test-AutoTradeClose.ps1 -ActionRange` against its local loopback host and isolated account copies. Record exact totals and outcomes; no visual-client E2E claimed.
- [x] Update the traceability ledger, smoke notes, backlog, and progress in place. Record the deferred 7.69-only ban/detection divergence and keep threshold-TCP/visual E2E pending.
- [x] Publish the self-contained runtime-only build to the authorized test sandbox, preserve/validate the prior backup, and confirm UP/PVP listeners and hashes. No Site, production, or Git history touched.

**Results (28/09/2026):** 271 Engine.Tests passed; solution build completed with 0 warnings and 0 errors. Regression first failed for HP==0 plus missing AttackRun, then passed after the gate was centralized in legacy order. Debug loopback evidence: `Backup/Build/AutoTradeCloseSmoke/1fb224d3f291417ea0250c471076432b`; self-contained loopback evidence: `Backup/Build/AutoTradeCloseSmoke/3abde0cbd09e4d5fb13518268c604d94`; both exit 0 with assertions/cleanup true. The host recorded type4 totals 5/10/15/20, type5 totals 21..63, QuitTrade 73, and post-relogin reset 10. Runtime-only package `artifacts/WydCdk.Server-v7670-20260928-action-speed-reviewfix-r3.zip`: 197 files, SHA-256 `000F987FAF29C74F485D29F3F1B3CF3B2A97C1E2D960F7550918D5C1C7CCB5D9`; Core `A055A11A1DF0E9FAE60BB61CEBE252681EF37363A15B125A7DC2AD0328A1D483`, World `3B7CA3705756E861A5C656C33D699E99BDC3CDE6B6F5DBAB71F93ADC9048768E`, Protocol `D6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295`. Sandbox backup `backup-autotrade-close-0b04898eaf31414bae0f2005baa006ac`; UP/PVP Running, ports 8281/8282 verified, and all 197 hashes independently match. Five pre-existing logs/PDBs retained. The final static re-review found no further findings. The 7.69-only ban/detection side effect, threshold TCP and visual E2E remain deferred.

**Verification commands:**
- `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`
- `dotnet build .\WydCdk.Engine.slnx --no-restore --nologo -v:q`
- `.\tools\PortAudit\Test-AutoTradeClose.ps1 -ActionRange` (owns a server bound to `127.0.0.1:18283`, refuses an occupied port, uses isolated fixture copies, and checks original fixture hashes).

**Expected result:** Unit/build gates pass; packet loopback proves normalized relays and rejected dual-violation packets, while unit/log assertions prove the shared 5/type4 counter and state preservation. The 7.69-only ban/detection behavior remains explicitly deferred.
