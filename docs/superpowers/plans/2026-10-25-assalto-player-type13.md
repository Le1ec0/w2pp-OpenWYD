# Assalto Type 13 Player Score Projection Implementation Plan

> **For agentic workers:** Execute this plan inline in the existing checkout; preserve the dirty worktree and do not commit, push, or deploy.

**Goal:** Make skill 11 `Assalto` reproject its Type 13 damage multiplier on player self targets and restore the baseline score when the affect expires.

**Architecture:** Reuse `LegacyCurrentScoreMath.CalculateAssaultAffectStage` and `LegacyCurrentScoreLoginProjector`; do not add a second damage formula. Add an explicit skill 11 producer predicate to the existing effect-only player score rebuild path, preserving live HP/MP.

**Tech Stack:** C# / .NET 10, `WydCdk.Engine.Tests`, synthetic TCP `--mob-ai-smoke`.

**Spec:** `AGENTS.md` compatibility rules and the compiled TMProject 7.69 SkillData/Basedef/_MSG_Attack evidence recorded in `docs/PORT_TRACEABILITY.md` for Task 5DH. The compiled project includes the parent `Basedef.cpp`; the separate `TMSrv/Basedef.cpp` copy containing `/23` is not compiled.

## Global Constraints

- TMProject 7.69 defines target behavior; compare W2PP and record differences.
- Preserve the existing dirty checkout. No commit, push, publish, deploy, Site, or production change.
- Synthetic loopback is not authenticated Host or visual-client E2E evidence.

## Review Focus

- SkillData row 11 must remain a non-aggressive effect-only Type 13 producer.
- Type 13 changes the 7.69 final damage multiplier by `Level / 10 + Value`; it does not change raw Damage or MaxHp in the target source.
- Current HP/MP must survive the self score rebuild and timer expiry.
- The player timer must restore the score after the source-derived duration expires.
- W2PP's extra raw Damage and MaxHp changes must not leak into the 7.69 port.

---

### Task 1: Prove the missing player producer

**Files:**
- Create: `tests/WydCdk.Engine.Tests/AttackPlayerAssaultAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Consumes: `WorldHub.TryApplySkillAttack`, `LegacyCurrentScoreLoginProjector.Project`, and SkillData row 11 values.
- Produces: A failing regression test for player score projection, recast, and timer expiry.

- [x] Write a self-target skill 11 test asserting Type 13, `Level=50`, `Value=7`, score multiplier 112%, unchanged HP/MP, and restoration after the 24-tick source duration.
- [x] Run `dotnet run --project tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore` and confirm RED is the unchanged player damage score (`508 passed / 1 failed`).

### Task 2: Enable the audited producer

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`

**Interfaces:**
- Consumes: Existing Type 13 score math and effect-only player target path.
- Produces: Explicit skill 11 Type 13 score rebuild with HP/MP preservation.

- [x] Add the explicit skill 11 / non-aggressive / AffectType 13 predicate to the effect-only player projection gate and audited score-affect type.
- [x] Run the Engine.Tests command and confirm the new Type 13 test passes (`509/509`).

### Task 3: Validate wire relay and record the slice

**Files:**
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`, `BACKLOG.md`, `PROGRESSO.md`, `AGENTS.md`, `docs/CLIENT_E2E_SMOKE.md`, `docs/PORT_TRACEABILITY.md`.

- [x] Add a three-peer loopback smoke asserting only the caster receives Type 13 UpdateScore/UpdateAffect with projected damage.
- [x] Run E2E build (0/0), complete `--mob-ai-smoke` (`ASSALTO_PLAYER_TYPE13_SMOKE_PASS` and `MOB_AI_SMOKE_PASS`), Engine.Tests (`509/509`), and full no-incremental solution build (0 errors; pre-existing CS8604).
- [x] Record source comparison, verification results, and remaining local-only validation limits.
