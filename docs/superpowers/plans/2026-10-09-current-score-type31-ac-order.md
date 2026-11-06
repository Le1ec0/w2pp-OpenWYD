# CurrentScore Type 31 ArmorClass Ordering Implementation Plan

> **For agentic workers:** Execute inline with `superpowers:executing-plans`; follow RED-GREEN for every behavior change. Do not commit: the project instructions prohibit automatic commits.

**Goal:** Project the approved CurrentScore ArmorClass affect subset through login in the two pass order used by TMProject 7.69.

**Architecture:** Add a pure AC composition stage. Its first slot pass applies Type 24, 31, 16, and 11; its second slot pass applies Type 12 and Type 8 bit 4. The coordinator uses this final AC while its existing score composer continues to project the other selected fields.

**Tech Stack:** C# `net10.0`, custom `WydCdk.Engine.Tests` runner, JSON golden fixtures, Markdown traceability.

**Spec:** Approved scope in the user message on 2026-10-09; login projection boundary in `docs/superpowers/specs/2026-10-08-current-score-login-projection-design.md`.

## Global Constraints

- Follow TMProject 7.69 `Servidor/Source/Code/Basedef.cpp` as target and compare behavior with `Server/W2PP/Source/Code/Basedef.cpp`.
- Preserve existing dirty work. Do not stage, commit, push, deploy, change persistence, or change wire.
- Type 21 and unreviewed CurrentScore branches remain outside this slice.
- Run Engine.Tests and the complete solution build; record `dotnet --info`.

## Review Focus

- Type 24/31 additions must precede Type 16 scaling and Type 11 addition in first-pass slot order; the golden AC test must distinguish rearrangement.
- Type 8 bit 4 and Type 12 must stay interleaved by slot in the second pass; a reverse slot order must produce a different integer result.
- Type 16 must preserve the existing BeastMaster gate, transformation selector, integer truncation, and learned-skill condition.
- Type 31 uses integer `Level / 2 + Value` for both sources; W2PP has the branch but at a different point in its loop.
- Coordinator AC must reach the login projection once; Type 8 bit 4 must not be applied twice.

### Task 1: Red tests for ordered AC projection and W2PP Type 31

**Files:**
- Modify: `tests/WydCdk.Engine.Tests/CurrentScoreAffectCompositionTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-golden-shield.golden.json`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.golden.json`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/current-score-login-projection.golden.json`

**Interfaces:**
- Consumes: `LegacyCurrentScoreMath` public stage methods and the current golden fixture loader.
- Produces: failing coordinator/login goldens for the new two-pass AC result and an independent W2PP Type 31 oracle that matches its source branch.

- [x] Add a coordinator behavior test with starting AC 100 and first-pass sequence Type 24(value 4), Type 31(level 5/value 2), Type 16(value 5/level 200), Type 11(level 5/value 2), followed by Type 8(bit 4) then Type 12(value 20). Expected AC is 153.
- [x] Add Type 31 to the existing login projection golden with starting AC 101 and two Type 8 records; only slot 0 has bit 4 set, so expected final AC is 127.
- [x] Update the Type 31 golden W2PP expectations to the same literal additions as 7.69; keep unrelated-affect vectors at zero.
- [x] Run Engine.Tests and confirm RED is caused by the old coordinator result and stale W2PP fixture expectation, not compilation errors.

### Task 2: Implement the pure AC pass and coordinator integration

**Files:**
- Modify: `src/WydCdk.World/LegacyCurrentScoreMath.cs`
- Modify: `tests/WydCdk.Engine.Tests/CurrentScoreAffectCompositionTests.cs`

**Interfaces:**
- Consumes: the new failing test from Task 1.
- Produces: `CalculateCurrentScoreArmorClassAffectCompositionStage(ReadOnlySpan<byte>, int startingArmorClass, int characterClass, int learnedSkills)` returning `LegacyCurrentScoreArmorClassAffectCompositionStage`.

- [x] Add the return record and implement first-pass slot order Type 24/31/16/11, then second-pass slot order Type 12/Type 8 bit 4. Add direct tests for Type 8-before-Type 12 (AC 11 → 9), the reverse order (AC 11 → 8), the Type 16 class gate, and truncated input.
- [x] Reuse a per-affect Type 16 AC kernel so the standalone transformation stage keeps its existing behavior.
- [x] Add a coordinator-only path for Type 8 score composition that skips its AC mutation; use the ordered AC stage as `ScoreAffects.ArmorClass` and `AfterScoreAffects.CurrentScore.Ac`.
- [x] Verify the new focused tests and existing composition tests pass.

### Task 3: Login/coordinator goldens and audit records

**Files:**
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.golden.json`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/current-score-login-projection.golden.json`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.manifest.md`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-golden-shield.manifest.md`
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `../PROGRESSO.md`
- Modify: `../BACKLOG.md`

**Interfaces:**
- Consumes: the AC stage and coordinator integration from Task 2.
- Produces: target/W2PP arithmetic evidence, order coverage in the coordinator golden, and current project status.

- [x] Add Type 31 to the existing interleaved coordinator golden and assert final AC 127 after Type 31 +15 and the slot-0 Type 8 bit 4 +11.
- [x] Correct the W2PP audit: same Type 31 formula, later loop order than 7.69; retain Type 21 and remaining branches as pending.
- [x] Record that this is a selected AC projection, not full `BASE_GetCurrentScore` parity.

### Task 4: Full local verification

**Files:**
- Modify: `tests/WydCdk.Engine.Tests/CurrentScoreLoginStartupTests.cs` to drain redirected child output while waiting.
- Evidence: `Backup/Build/CurrentScoreType31-20261009/`

**Interfaces:**
- Consumes: all changes from Tasks 1–3.
- Produces: Engine.Tests result, solution build result, SDK information, and build logs.

- [x] Run `dotnet --info` and save it with the build evidence.
- [x] Run Engine.Tests and record the final count.
- [x] Run `dotnet build WydCdk.Engine.slnx --no-incremental --nologo -v:q` and record warnings/errors.
- [x] Review the targeted diff and `git diff --check`; leave all unrelated changes untouched.
