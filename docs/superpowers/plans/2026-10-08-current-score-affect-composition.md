# CurrentScore Type 34/35/8 Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline implementation. Each task uses test-first steps.

**Goal:** Compose existing Type 34, Type 35, and Type 8 score math in the pure 7.69 coordinator, preserving slot order among those affects.

**Architecture:** Extract per-affect arithmetic helpers from the three existing pure stage methods and reuse them in a new slot-ordered composition stage. Project that result into the existing immutable coordinator between Type 12 AC reduction and Soul; keep login, persistence, and wire untouched.

**Tech Stack:** C# / .NET 10.0, `WydCdk.World`, `WydCdk.Engine.Tests`, golden JSON fixtures.

**Spec:** `docs/superpowers/specs/2026-10-08-current-score-affect-composition-design.md`

## Global Constraints

- Follow the common `Servidor/Source/Code/Basedef.cpp` included by `TMSrv.vcxproj`; its Type 35 is 15% MaxHP and 7% MaxMP. W2PP and the uncompiled `TMSrv/Basedef.cpp` copy use 10%/10%.
- Keep the existing TMSrv common/private MOB ABI mismatch as an explicit runtime limitation; this task validates source-level C# behavior only.
- Compare W2PP divergences; Type 4 is outside this composition slice. Do not claim that the common source lacks a Type 4 branch; its authority decision remains separate because the common/private source and ABI differ.
- Preserve existing dirty work; do not reset, stage, commit, or push.
- Keep Domain/score arithmetic independent of wire, persistence, and login orchestration.
- Use SDK 10.0.401 and record `dotnet --info` with validation evidence.

## Review Focus

- Interleaved Type 8/35/34 affects: per-slot results must differ correctly from a grouped-by-type evaluation.
- Magic origin: each relevant affect reads unchanged `mob.Magic` while accumulating into the local Magic value.
- Bit 6: LearnedSkill bit 28 must survive the projection passed to later score stages.
- Bit 2: `RSV_VISION` must be additive and preserve existing RSV/Haste flags.
- Boundary: Type 8 bit 1 is handled by the separate resistance stage, bits 0/3 are attack effects, and Type 4 is outside this stage; do not infer Type 4 is absent from the common source.

---

### Task 1: Compose selected score affects in the existing pure coordinator

**Files:**
- Modify: `src/WydCdk.World/LegacyCurrentScoreMath.cs`
- Test: `tests/WydCdk.Engine.Tests/CurrentScoreVigorTests.cs`
- Test: `tests/WydCdk.Engine.Tests/CurrentScoreAffectCompositionTests.cs`
- Test: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.golden.json`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.manifest.md`
- Modify: root `AGENTS.md`, root `BACKLOG.md`, root `PROGRESSO.md`, and `docs/PORT_TRACEABILITY.md`

**Interfaces:**
- Consumes: existing `CalculateDivinaAffectStage`, `CalculateVigorAffectStage`, `CalculatePvpJewelAffectStage`, and `CalculateCurrentScoreCoordinatorStage` behavior.
- Produces: `LegacyCurrentScoreAffectCompositionStage` with MaxHP, MaxMP, AC, Damage, local Magic, LearnedSkill, and `RsvFlagsToAdd`; coordinator exposes this stage and an immutable `AfterScoreAffects` snapshot before Soul.

- [x] **Step 1: Update the Type 35 tests to the selected common 7.69 source formula.** Assert 999 HP / 101 MP becomes 1148 / 108, two affects from 50,000 HP / 50,001 MP become 66,125 / 57,246, a below-threshold increment can raise the result above 100,000, and an increment at/above 100,000 assigns 100,000.
- [x] **Step 2: Run Engine.Tests and confirm the updated Type 35 assertion fails against the current 10%/10% helper.** RED: 401/402, with the expected actual 1089 HP / 111 MP.

  Run: `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`

  Expected: Type 35 fails with the old 10%/10% output (1089 HP / 111 MP for the first vector).
- [x] **Step 3: Correct `CalculateVigorAffectStage` to the common 7.69 source branch.** Per affect, calculate `MaxHp * 15 / 100` and `MaxMp * 7 / 100`, truncate, and compare each increment with the 100,000 threshold. Set the score field to 100,000 when its increment reaches the threshold; otherwise add the increment, even if the result exceeds 100,000. Retain the immutable snapshot and ordered behavior.
- [x] **Step 4: Run Engine.Tests and confirm all Type 35 tests pass.** GREEN: 402/402.
- [x] **Step 5: Add a pure-composer test using Type 8 bit 4 (slot 0), Type 35 (slot 1), Type 34 (slot 2), and Type 8 bits 0/1/2/3/5/6/7 (slot 3), starting at MaxHP 102.** Expected slot-order result: MaxHP 232; grouping by type would produce 233. Assert MaxMP 64, AC 111, Damage 133, local Magic 157 from source MOB.Magic 1001 plus starting local Magic 7, LearnedSkill bit 28, and only RSV_VISION in this stage. Assert Type 8 bit 1 is handled by the separate resistance stage; Type 4 is ignored by this scoped composer without asserting it is absent from the target source; bits 0/3 and the input snapshot remain untouched. Also test a truncated snapshot.
- [x] **Step 6: Run Engine.Tests to verify the new composer API test fails because `CalculateCurrentScoreAffectCompositionStage` does not exist yet.** RED produced the expected CS0117 missing-member diagnostic at both new API call sites.
- [x] **Step 7: Extract private per-affect arithmetic helpers and add `CalculateCurrentScoreAffectCompositionStage`.** Scan all 32 affects once in slot order; dispatch only active Type 34, 35, and 8 score branches; leave Type 8 resistance bit 1 to the existing resistance stage and ignore attack bits 0/3.
- [x] **Step 8: Run Engine.Tests and verify the new pure-composer test passes while the existing standalone Type 34/35/8 vectors remain unchanged.** GREEN: 403/403.
- [x] **Step 9: Add a golden coordinator case with the same interleaved affects, a Type 2 Haste record, and Type 4.** Assert the result reaches the post-12 snapshot and Soul/Kibita, bit 28 is preserved, RSV_VISION combines with Haste, Type 8 bit 1 affects only resistance, Type 4 is not handled by this scoped composer, and the input MOB remains unchanged.
- [x] **Step 10: Run Engine.Tests and confirm the coordinator case fails because the current coordinator does not yet project the selected affects before Soul.** RED: temporarily withholding the MOB projection yielded 402/403; only the expected post-affect projection assertion failed. The projection was restored.
- [x] **Step 11: Expose the stage and `AfterScoreAffects` snapshot from `LegacyCurrentScoreCoordinatorStage`; apply MaxHP/MaxMP/AC/Damage/Magic/LearnedSkill/RSV before Soul, pass `mob.Magic` and `abilities.Magic` as the source/local Magic values, and union the stage RSV flags with Haste.**
- [x] **Step 12: Run focused and full Engine.Tests, then build `WydCdk.Engine.slnx` without incremental compilation.** Final Engine.Tests: 403/403. `dotnet build .\WydCdk.Engine.slnx --no-incremental`: 0 errors, 1 pre-existing CS8604 warning at `AttackDivineBlessingTests.cs:256`. SDK info and command logs are in `Backup/Build/CurrentScoreAffectComposition-20261008`.
- [x] **Step 13: Update the four approved project documents and the golden fixture manifest with the 7.69/W2PP comparison, source-selection correction, scope, results, and limits.** Updated root `AGENTS.md`, `BACKLOG.md`, `PROGRESSO.md`, `docs/PORT_TRACEABILITY.md`, and the coordinator fixture manifest.

### Review correction (08/10/2026)

Independent review found that the common Type 35 source compares the percentage increment itself to `MAX_HP/MAX_MP`, not the resulting score. For example, from 120,000 HP the 15% increment is 18,000 and the source adds it, producing 138,000; only an increment >=100,000 assigns the field to 100,000. The previous helper/test/document phrasing clamped the result and was inaccurate. The increment-threshold case was added test-first: RED 402/403, followed by final Engine.Tests 403/403 and solution build exit 0, 0 errors, 1 pre-existing CS8604 warning. Evidence is in `Backup/Build/CurrentScoreAffectComposition-20261008`.

**Completion checks:** full test count and build exit code are recorded; no login, persistence, or wire path uses this pure coordinator; no unrelated files are staged or committed.
