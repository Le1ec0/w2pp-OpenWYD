# MOB-AI-001 Type 1 damage skills 23 and 36 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the already-ported Type 1 NPC affect for damaging skills 23 and 36 at the legacy first-NPC boundary.

**Architecture:** Reuse the existing aggressive Type 1 gates, score baseline, affect timer, and NPC relay path. Keep the admitted skill IDs explicit so unrelated Type 1 definitions do not gain behavior without source review.

**Tech Stack:** C#/.NET 10, `WydCdk.World`, Engine.Tests, loopback Client E2E harness.

**Spec:** `docs/superpowers/specs/2026-10-15-mob-ai-design.md`, TMProject 7.69 and W2PP `_MSG_Attack.cpp` / `SetAffect` behavior.

## Global Constraints

- Preserve the dirty checkout and update project Markdown in place.
- Use TMProject 7.69 as the wire/data/behavior target and compare the matching W2PP path before changing C#.
- Apply Type 1 only to audited damaging skills 23, 34, and 36, and only at NPC connection 1000, matching `SetAffect`'s `MAX_USER` bound.
- Keep score updates view-scoped; do not package, publish, deploy, commit, or push.

## Review Focus

- SkillData row IDs and Type 1 fields match the active 7.69 CSV rows: pin rows 23 and 36 in the producer tests.
- Recasting one Type 1 skill with another resets the source timer without stacking the score penalty: test cross-skill recast.
- NPC 1001 and source aggressive/resistance gates remain unchanged: test the bound and shared rejection gate.
- Score projection expires through existing generated and non-generated NPC timers: retain the existing Type 1 expiry coverage and run it with the full suite.
- Only viewers in the captured NPC view receive the changed score: extend the Type 1 loopback smoke.

---

### Task 1: Port the two audited Type 1 producers

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`
- Create: `tests/WydCdk.Engine.Tests/AttackNpcType1DamageSkillAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`
- Modify: `docs/PORT_TRACEABILITY.md`, root `AGENTS.md`, root `PROGRESSO.md`, and root `BACKLOG.md`

**Interfaces:**
- Consume `LegacySkillDefinition` from the existing damaging NPC target path.
- Reuse the current NPC Type 1 helper and its `LegacyNpcHolyTouchScoreBaseline` state.
- Reuse `LegacyNpcAffectScoreRelay` for the loopback score update.

- [x] Add RED tests for 7.69 rows 23 and 36, projected score, NPC 1001 bound, and cross-skill recast timer.
- [x] Run Engine.Tests and confirm the new producer assertions fail (`501/502`).
- [x] Expand the explicit damaging Type 1 skill allowlist to 23, 34, and 36; keep existing gate and score logic.
- [x] Run Engine.Tests and confirm the new assertions pass (`502/502`).
- [x] Add a three-peer Type 1 damage-skill smoke; actor and in-view observer receive `UpdateScore`, outside peer receives no frame.
- [x] Run full Engine.Tests, no-incremental solution build, and `--mob-ai-smoke`; final `git diff --check` review recorded after documentation updates.
- [x] Update traceability, progress, backlog, and the root handoff with actual results and remaining validation limits.
