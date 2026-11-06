# Skill attack RSV procs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the source-backed `RSV_FROST` Type 1 and `RSV_DRAIN` Type 20 procs to positive-damage skill attacks.

**Architecture:** Extend the existing skill attack roll contract, then call one shared proc helper from the player, NPC and summon skill-target paths after their final damage gates. Reuse `TryApplyLegacySecondaryAttackEffect` so `SetAffect` and `SetTick` retain their distinct bounds and Merchant behavior.

**Tech Stack:** C#, .NET 10, Engine.Tests, Client.E2E loopback smoke.

**Spec:** `docs/superpowers/specs/2026-10-26-skill-attack-rsv-procs-design.md`

## Global Constraints

- Compare behavior against TMProject 7.69 and W2PP before porting.
- Keep `SetAffect` and `SetTick` gates separate; Type 1 is bounded at NPC 1000, Type 20 may reach NPC 1001 unless Merchant.
- Preserve all existing working-tree changes; no commit, push, Site/prod, or deployment.
- A loopback smoke is not authenticated Host or visual-client E2E.

## Review Focus

- Frost succeeds only on a positive hit and a roll of 0; verify Type 1 score projection and affect snapshot.
- Frost must not cross target index 1000 or bypass `RSV_BLOCK`.
- Drain uses its separate roll and `SetTick` boundary: NPC 1001 accepts, Merchant at 1001 rejects.
- Roll 1 and zero damage leave both proc effects unapplied.
- Skill targets include player, NPC and summon state; returned snapshots must reach the existing view-scoped relay.

---

### Task 1: Port RSV procs to skill attacks

**Files:**
- Modify: `src/WydCdk.World/LegacyAttackActionBatch.cs`
- Modify: `src/WydCdk.World/WorldHub.cs`
- Create: `tests/WydCdk.Engine.Tests/AttackSkillSecondaryAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/` smoke entry and focused network scenario
- Modify: `docs/PORT_TRACEABILITY.md`, root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md`

**Interfaces:**
- Add nullable `FrostRoll` and `DrainRoll` to `LegacySkillAttackBatchSlotRolls`.
- Add trailing optional `frostRoll = -1` and `drainRoll = -1` inputs to
  `TryApplySkillAttack`; accept only -1 or 0..1.
- The shared helper consumes SkillData rows 36/40, the attacker's `Special2`,
  the target affect/MOB/state, and both optional rolls; it returns whether
  either affect record changed.
- Target-specific code persists score and affect snapshots using existing
  player/NPC/summon outcome paths.

- [x] **Step 1: Write focused behavior tests**
  - `SkillFrostProcProjectsType1OnNpc1000`
  - `SkillFrostProcHonorsNpc1000AndRsvBlockGates`
  - `SkillDrainProcUsesSetTickOnNpc1001AndRejectsMerchant`
  - `SkillProcsRequirePositiveDamageAndSuccessfulRolls`
  - `SkillFrostProcProjectsPlayerType1AndDrainPersistsOnSummon`
- [x] **Step 2: Run the focused tests and observe the missing behavior**
- [x] **Step 3: Add deterministic per-slot frost/drain roll inputs and propagate them through the skill attack batch**
- [x] **Step 4: Run the focused tests again; confirm they fail on missing skill-path procs**
- [x] **Step 5: Implement the shared proc helper and invoke it after final positive-damage resolution for player, NPC and summon targets**
- [x] **Step 6: Run focused and full Engine.Tests; both must pass** — Engine.Tests `531/531`.
- [x] **Step 7: Add and run a dedicated `--mob-ai-smoke` loopback marker for a skill-hit proc and its view-scoped relay** — `RSV_FROST_SKILL_NPC_TYPE1_SMOKE_PASS`; global `MOB_AI_SMOKE_PASS`.
- [x] **Step 8: Run `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental`; record SDK/output and update project Markdown in place** — full solution and E2E builds `0 warnings/0 errors`; SDK `10.0.401`.

