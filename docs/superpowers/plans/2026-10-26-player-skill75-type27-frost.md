# MOB-AI-001 Task 5DQ - Skill 75 Type 27 Ice Enchant player score plan

> **For agentic workers:** implement this bounded task inline. Preserve the shared dirty checkout and leave all changes unstaged; no commit, push, publish, or deploy without completing the local gates and the existing sandbox authorization.

**Goal:** Port the 7.69 player skill 75 `Encantar Gelo` Type 27 score projection and its active-weapon gate through cast, 7.69 score wire, recast, and expiry.

**Architecture:** Keep the existing modular C# server and reuse `LegacyCurrentScoreLoginProjector` for online score rebuilds. Add a small source-backed Type 27 RSV stage in `LegacyCurrentScoreMath`, then opt in only skill 75 through the audited player-skill matcher in `WorldHub`.

**Tech Stack:** C# / .NET 10, Engine.Tests console runner, three-peer TCP `--mob-ai-smoke`.

**Spec:** TMProject 7.69 `skilldata.csv` row 75 and `Basedef.cpp:3658-3659`; compare W2PP `Basedef.cpp:4421-4422` and `_MSG_Attack.cpp:1206-1234`. Record the source divergence and validation in `docs/PORT_TRACEABILITY.md`.

## Global Constraints

- TMProject 7.69 defines the target behavior; compare W2PP before porting and document divergence explicitly.
- Type 27 sets `RSV_FROST` only when Type 27 is active and equipped slot 6 has `EF_WTYPE=101`.
- Restrict the online producer matcher to skill 75 with `InstanceType=0`, `Aggressive=0`, and `AffectType=27`.
- Preserve current HP/MP during the score rebuild; use the existing affect timer for expiry.
- Preserve unrelated local changes; test locally and update project Markdown in place.

## Review Focus

- Type 27 with weapon type 101 sets RSV_FROST; without Type 27 it does not.
- Type 27 with another weapon type stays inactive, following the 7.69 gate rather than W2PP's commented-out gate.
- Skill cast and recast preserve HP/MP, and expiry clears Type 27 and restores the prior RSV state.
- The 7.69 `UpdateScore` frame preserves authoritative RSV even though the W2PP source payload omits it.
- UpdateScore and UpdateAffect reach only the caster in the synthetic three-peer loopback.

---

### Task 1: Type 27 player score projection

**Files:**
- Create: `tests/WydCdk.Engine.Tests/AttackPlayerIceEnchantAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `src/WydCdk.World/LegacyCurrentScoreMath.cs`
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `src/WydCdk.Protocol/W2ppUpdateScoreV1Adapter.cs`
- Modify: `src/WydCdk.Server/Program.cs`, `LegacyMobAiRelay.cs`, `LegacyNpcAffectScoreRelay.cs`, and `LegacyPlayerAffectRelay.cs`
- Modify: `src/WydCdk.World/LegacyBattleRoyaleAreaClearRelay.cs`
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`
- Modify: `docs/PORT_TRACEABILITY.md`, `docs/CLIENT_E2E_SMOKE.md`, root `BACKLOG.md`, `PROGRESSO.md`, and `AGENTS.md`

**Interfaces:**
- Consumes: `LegacyCurrentScoreLoginProjector.Project`, `LegacyCurrentScoreMath.CalculateCurrentScoreCoordinatorStage`, the existing effect-only `WorldHub.TryApplySkillAttack` path, and `TryProcessPlayerTimerAffects`.
- Produces: `LegacyCurrentScoreMath.FrostRsvFlag` and `CalculateIceEnchantAffectStage(ReadOnlySpan<byte> affectSnapshot, ReadOnlySpan<LegacyItem> equipment, LegacyItemDataTable itemData)`; `WorldHub.IsAuditedType27PlayerSkill` for skill 75 only; adapter overload that carries authoritative MOB.Rsv into `UpdateScoreConfirmationV769`.

- [x] Write a regression test that projects Type 27 with a synthetic item definition whose weapon type is 101; verify RSV_FROST, the wrong-weapon guard, and no flag without the affect.
- [x] Add the test to the Engine.Tests runner and observe the expected missing-projection RED.
- [x] Add the Type 27 score stage and call it from the existing current-score coordinator using equipment slot 6 and the supplied ItemList table.
- [x] Extend the player cast test to apply skill 75, verify Type 27 and the RSV flag, preserve current HP/MP on recast, then expire and verify both clear and baseline restoration; assert value 1, level 20, and 19 ticks for the fixture.
- [x] Preserve MOB.Rsv when adapting W2PP score payloads to the 7.69 `UpdateScore`, and update production relay call sites that have the full MOB snapshot.
- [x] Run Engine.Tests: `518/518`.
- [x] Extend the three-peer TCP smoke; `ICE_ENCHANT_PLAYER_TYPE27_SMOKE_PASS` and the global `MOB_AI_SMOKE_PASS` passed. Only the caster received `UpdateScore` and `UpdateAffect`.
- [x] Build E2E and the full solution without incremental outputs: 0 warnings/errors; run `--mob-ai-smoke`.
- [x] Update traceability, smoke guide, backlog, progress, and the AGENTS handoff with source comparison, results, and limits.

**Acceptance:** Engine.Tests passes; E2E and full solution builds have no warnings/errors; the synthetic smoke passes; 7.69/W2PP divergence and the lack of authenticated/visual validation are explicit. No code is staged or committed.
