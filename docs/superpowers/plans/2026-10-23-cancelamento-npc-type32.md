# Cancelamento Type 32 em NPC Implementation Plan

> **For agentic workers:** Use the existing inline execution method. Keep the dirty checkout intact; do not stage, commit, push, publish, or deploy.

**Goal:** Port the skill 47 fallback Type 32 to the first NPC index accepted by the legacy `SetAffect` guard.

**Architecture:** Extend only the existing NPC effect-skill path in `WorldHub`. Reuse `TryApplyLegacyAffects` and preserve the source guard boundary (`conn > MAX_USER`), so the fallback applies at NPC connection 1000 and not above it. Relay the resulting 7.69 UpdateScore to that NPC's 33x33 view through a focused server helper.

**Tech Stack:** C# / .NET 10, existing Engine.Tests harness.

**Spec:** `docs/superpowers/specs/2026-10-23-cancelamento-npc-type32-design.md`

## Global Constraints

- TMProject 7.69 is the primary behavior reference; compare W2PP and record the shared strict `conn > MAX_USER` guard.
- Preserve the intentionally dirty checkout and all unrelated changes.
- No commit, push, publish, deploy, Site, or production changes.

## Review Focus

- NPC connection 1000 receives Type 32 only after the Type 19 scan finds none and aggressive affect gates allow it.
- NPC connections above 1000 remain unchanged by the generic fallback.
- Existing Type 19 removal and player Type 32 fallback remain unchanged.
- Same-leader/guild, merchant, RSV_BLOCK, and Clan 6 gates do not gain a bypass.

---

### Task 1: Port the first-NPC Cancelamento fallback

**Files:**

- Modify `tests/WydCdk.Engine.Tests/AttackCancelAffectTests.cs` and `tests/WydCdk.Client.E2E/MobAiNetwork.cs`.
- Modify `src/WydCdk.World/WorldHub.cs`, `src/WydCdk.Server/Program.cs`, and `src/WydCdk.Server/WydCdk.Server.csproj`; add `src/WydCdk.Server/LegacyNpcAffectScoreRelay.cs`.
- Update `docs/PORT_TRACEABILITY.md`, `docs/CLIENT_E2E_SMOKE.md`, root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md` in place.

**Interfaces:**

- Consume `WorldHub.TryApplyNpcEffectSkillAttackLocked` and `TryApplyLegacyAffects` without changing public API.
- Preserve `LegacySkillAttackOutcome.TargetAffectSnapshot` as the observable state result.

**Steps:**

- [x] Add regressions that use Cancelamento against NPC indices 1000 and 1001 with no Type 19; assert Type 32 appears only at 1000, and same-leader/guild, merchant, Clan 6, and `RSV_BLOCK` targets stay unchanged.
- [x] Run Engine.Tests and confirm the 1000 assertion fails because the NPC handler had no generic AffectType fallback (`494/495`).
- [x] Add the fallback after the existing Type 19 and RSV_BLOCK gates, restricted to `targetConnectionId == LegacyMobAiPlanner.MaximumPlayerId` and non-merchants.
- [x] Run Engine.Tests (`495/495`), the no-incremental solution build (0 errors, one pre-existing CS8604), and `dotnet --info` (SDK 10.0.401, exit 1 on Service Control Manager access).
- [x] Relay the NPC UpdateScore to its 33x33 view and extend `--mob-ai-smoke`; the actor and inclusive-boundary peer receive Type 32, the outside peer receives no frame.
- [x] Run `git diff --check` (exit 0; only existing LF/CRLF conversion warnings) and record source boundaries/final local evidence in project docs.
