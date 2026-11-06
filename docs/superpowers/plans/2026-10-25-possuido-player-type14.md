# Possuido Type 14 Player Score Projection Implementation Plan

> **For agentic workers:** Execute inline in the existing checkout; preserve the dirty worktree and do not commit, push, publish, or deploy.

**Goal:** Project Type 14 from skill 3 `Possuido` onto a player self-target when the effect is applied, then restore MaxHp and Constitution when it expires.

**Architecture:** Reuse `LegacyCurrentScoreLoginProjector` and `CalculatePossessedAffectStage`. Add an explicit player producer predicate for the exact 7.69 skill row; preserve current HP/MP through the score rebuild.

**Spec:** TMProject 7.69 SkillData row 3 and compiled score/attack paths, compared with W2PP, recorded in `docs/PORT_TRACEABILITY.md` for Task 5DI.

## Global Constraints

- TMProject 7.69 is the target. Preserve the W2PP MaxHp and Constitution divergence as comparison only.
- Keep the existing dirty checkout intact. No commit, push, publish, deploy, Site, or production change.
- Synthetic TCP loopback is not authenticated Host or visual-client E2E evidence.

## Review Focus

- Skill 3 remains an effect-only, non-aggressive Type 14 self skill.
- Special2 supplies affect Level; Type 14 follows class-master-specific 7.69 MaxHp/Constitution math.
- Current HP/MP remain unchanged when the score is rebuilt.
- Recast refreshes the source-derived duration without stacking score deltas.
- Timer expiry restores baseline MaxHp and Constitution.
- W2PP's Type 14 rules do not leak into the 7.69 port.

---

### Task 1: Reproduce the missing player producer

**Files:**
- Create: `tests/WydCdk.Engine.Tests/AttackPlayerPossuidoAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

- [x] Test Mortal and non-Mortal class-master projection, Special2-derived Type 14, current HP/MP, recast, and timer expiry.
- [x] Run Engine.Tests and confirm RED is the unprojected player MaxHp/Constitution.

### Task 2: Enable the audited producer

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`

- [x] Add an explicit skill 3 / InstanceType 0 / Aggressive 0 / AffectType 14 predicate to the effect-only player score rebuild path and audited affect type.
- [x] Run Engine.Tests and confirm both class-master cases pass.

### Task 3: Validate frames and record the slice

**Files:**
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`, `BACKLOG.md`, `PROGRESSO.md`, `AGENTS.md`, `docs/CLIENT_E2E_SMOKE.md`, `docs/PORT_TRACEABILITY.md`.

- [x] Add a three-peer loopback smoke for the caster's Type 14 UpdateScore/UpdateAffect and resource preservation.
- [x] Run E2E build, complete `--mob-ai-smoke`, Engine.Tests, and full no-incremental solution build.
- [x] Record compiled-source evidence, W2PP divergence, validation, and local-only limits.
