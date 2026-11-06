# Skill 216 Type 42 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port Skill 216 `Magia Misteriosa` Type 42 so player MaxHP/MaxMP match TMProject 7.69 on login, cast, recast, and expiry.

**Architecture:** Add Type 42 to the existing slot-ordered current-score affect composition, passing the already-composed level and MOBEXTRA class-master value into the stage. Recognize only the audited self-target skill definition in WorldHub so cast and expiry use the existing player score projector.

**Tech Stack:** C# / .NET 10, WydCdk.World, WydCdk.Engine.Tests, local headless/TCP smoke.

**Spec:** `AGENTS.md` current project direction and handoff; `docs/PORT_TRACEABILITY.md`; TMProject 7.69 `Basedef.cpp:3591-3598`, W2PP `Basedef.cpp:4454-4460`, and both SkillData row 216.

## Global Constraints

- Keep Protocol, Application, Domain, Infrastructure, and Host dependencies pointing inward; this score rule belongs in World.
- Use TMProject 7.69 as the selected wire/data behavior and record the W2PP comparison.
- Keep SDK/TFM at the pinned .NET 10 line; do not add dependencies.
- Preserve the dirty checkout; do not commit, push, publish, deploy, or touch Site/production.
- Validate locally and describe headless/TCP evidence at its actual scope.

## Review Focus

- Arch/Mortal use the character level directly; other class-master values add the 7.69 maximum level (399).
- Type 42 uses integer division and the current MaxMP at its affect slot; order with Type 38 must remain observable.
- Recast refreshes the same Type 42 record without stacking MaxHP/MaxMP.
- Cast preserves current HP/MP while updating maximums; expiry restores the baseline maxima.
- Skill 216 must match the non-passive, non-aggressive, self-target Type 42 row before triggering a score rebuild.

---

### Task 1: Port Type 42 into player score projection

**Files:**
- Modify: `src/WydCdk.World/LegacyCurrentScoreMath.cs`
- Modify: `src/WydCdk.World/WorldHub.cs`
- Create: `tests/WydCdk.Engine.Tests/AttackPlayerSoulSwapAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/CurrentScoreAffectCompositionTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify in place after validation: `../AGENTS.md`, `../PROGRESSO.md`, `../BACKLOG.md`, `docs/PORT_TRACEABILITY.md`, `docs/CLIENT_E2E_SMOKE.md`

**Interfaces:**
- `CalculateCurrentScoreAffectCompositionStage` consumes the 32-record affect snapshot, starting maxima, the composed character level, and class-master value.
- `CalculateCurrentScoreCoordinatorStage` supplies its current score level and class-master to the affect composer.
- WorldHub recognizes skill 216 only when its audited SkillData fields identify passive 0, target type 0, instance type 0, aggressive 0, tick type 0, affect type 42.

- [x] Write direct composition and WorldHub cast/recast/expiry tests with literal expected values.
- [x] Run Engine.Tests and confirm the new assertions fail because Type 42 is not projected (`532/533`, class-master/Affect.Level calculation absent).
- [x] Implement the minimal ordered Type 42 branch and exact skill matcher; reuse the generic expiry rebuild.
- [x] Run Engine.Tests (`533/533`), full solution no-incremental build (exit 0; includes E2E project), and `--mob-ai-smoke` (exit 0 with `player-type42-expiry=score-only-relayed` and `MOB_AI_SMOKE_PASS`).
- [x] Update existing project handoff/checklist/traceability in place with source comparison, test/build/smoke evidence, and remaining Host-authenticated/visual E2E limits.
