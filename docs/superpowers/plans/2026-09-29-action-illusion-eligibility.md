# MOVE-ACTION-ILLUSION-ELIGIBILITY-001 Implementation Plan

> **For agentic workers:** Implement inline in the existing Server checkout. This slice uses test-first steps and does not include Git history changes.

**Goal:** Match the TMProject 7.69 `MSG_Action3` class/learned-skill eligibility penalty while preserving its below-threshold movement continuation.

**Architecture:** Read class, learned-skill bits, and level from the authoritative `LegacyMobCombatState` already fetched by the Action handler. Add the shared-counter constants and record 10/type28 only when class is not 3 or bit 2 is missing and level is below 399. Continue normal Action3 processing unless the shared crack-error threshold logs the character out.

**Tech Stack:** C#/.NET 10, Engine.Tests, loopback Client.E2E smoke, PowerShell harness.

**Spec:** User-approved Action3 eligibility slice; primary contract `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Action.cpp`, compared with `Server/W2PP/Source/Code/TMSrv/_MSG_Action.cpp`.

## Global Constraints

- Preserve the Server checkout's existing dirty work; do not stage, commit, push, deploy, or change Site/production.
- Keep TMProject 7.69 as the behavioral authority and record any W2PP divergence.
- Do not implement mana, the 900 ms cooldown, or Illusion effect behavior in this slice.
- Keep loopback tests on port 18283 with isolated account copies; preserve and verify original fixture hashes.
- Update `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`, `Server/docs/PORT_TRACEABILITY.md`, and `Server/docs/CLIENT_E2E_SMOKE.md` in place.

## Review Focus

- Wrong class and missing learned bit below level 399 add exactly 10/type28: unit and network cases.
- Class 3 with bit 2 set adds no eligibility error: unit boundary case.
- Invalid eligibility below threshold still relays an accepted Action3 movement: loopback case.
- Level 399 suppresses type28: unit boundary; the accepted Action3 relay is covered by the level-398 loopback case.
- The type28 addition shares the counter and logout threshold: unit outcome plus loopback QuitTrade counter probe.

---

### Task 1: Add failing Action3 eligibility coverage

**Files:**
- Modify: `tests/WydCdk.Client.E2E/Program.cs` and `AutoTradeCloseHarnessTests.cs` for a guarded, isolated eligibility mode.
- Create: `tests/WydCdk.Client.E2E/ActionIllusionEligibilityNetwork.cs`.
- Modify: `tools/PortAudit/Test-AutoTradeClose.ps1` to seed only copied fixtures and assert exact type28 and shared-counter totals.

**Interfaces:**
- Consume existing `ActionRequest`, `E2eSession`, Action relay assertions, and `--autotrade-close-peer-account-file` fixture guard.
- Produce a loopback scenario that relays Action3 for invalid class/skill at level 398 and probes the shared counter with QuitTrade.

- [x] Write the loopback assertions and fixture setup before changing production behavior.
- [x] Run the isolated smoke against the current runtime and confirm it fails because type28 is absent while movement continuation still behaves as expected.

### Task 2: Implement the minimum server rule and unit boundaries

**Files:**
- Create: `src/WydCdk.World/LegacyIllusionRules.cs` for the pure class/skill/level predicate.
- Modify: `src/WydCdk.World/LegacyCrackErrorRules.cs` for named 10/type28 constants.
- Modify: `src/WydCdk.Server/Program.cs` in the shared Action dispatch before the existing Action3 tick gate.
- Modify: `tests/WydCdk.Engine.Tests/MovementRangeTests.cs` and `Program.cs` to cover valid/invalid eligibility and level 398/399.

**Interfaces:**
- Consume `actionResources.CharacterClass`, `LearnedSkill`, and `CurrentScore.Level`.
- Produce `LegacyCrackErrorOutcome` via existing `RecordCrackErrorAsync`; continue unless `RequiresCharacterLogout` is true.

- [x] Add unit boundary tests and verify their expected failure before implementation.
- [x] Add constants and the Action3-only predicate/recording branch; keep mana and cooldown untouched.
- [x] Run full Engine.Tests, solution build, E2E harness self-test, and the isolated loopback eligibility smoke.

### Task 3: Record evidence in project docs

**Files:**
- Modify: root `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`.
- Modify: `docs/PORT_TRACEABILITY.md`, `docs/CLIENT_E2E_SMOKE.md`.

- [x] Record source comparison, exact penalty, fall-through, test results, fixture integrity, and deferred mana/cooldown scope.
- [x] Run `git diff --check` and inspect the targeted diff without changing unrelated dirty files.
