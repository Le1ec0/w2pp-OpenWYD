# Plan: MOB-AI-001 Lança de Gelo Type 1 through the damaging NPC skill path

**Spec:** `docs/superpowers/specs/2026-10-15-mob-ai-design.md`
**Execution:** inline in the current dirty checkout, preserving existing user changes.
**Scope:** skill 34 `Lança de Gelo` has `InstanceType=3` damage and a Type 1
affect. Port only this producer at NPC 1000, with source gates, score projection,
recast, existing NPC timer expiry, and captured-view `UpdateScore` smoke.

## Source contract

TMProject 7.69 SkillData row 34 (`Servidor/Server/Common/skilldata.csv:35`)
defines `InstanceType=3`, `AffectType=1`, `AffectValue=2`, `AffectTime=1`,
`Aggressive=1`, and `AffectResist=2`. TMProject `_MSG_Attack.cpp:1670-1727`
and W2PP `_MSG_Attack.cpp:1172-1228` calculate the element damage first, then
run the shared aggressive `Buffs Time` tail and call `SetAffect`. The C# damage
path uses `TryApplyNpcSkillAttackLocked`/`FinishNpcSkillAttackLocked` and did
not apply this ordinary skill affect; the effect-only skill 1 path is separate.

Both C++ gates require different leader and guild, a successful resistance
roll (`roll <= RegenMP + AffectResist + levelDifference`), no `RSV_BLOCK`, and
no Clan 6 target when the caster is a player. `SetAffect` rejects merchants
and `conn > MAX_USER`, admits NPC 1000, and resets an existing Type 1 record to
four ticks (TMProject `Server.cpp:10359-10390`, W2PP `Server.cpp:9209-9239`).
The Type 1 score projection already covered by Task 5CW subtracts 2 Run and 30
Attack, plus 40 Intelligence when the head item index is above 50. Skill 34's
initial lifetime is `(AffectTime + 1) * Delay / 100`; its fixture has
`Special2=0`, so Delay=100 and lifetime=2 ticks.

## Task 1

- [x] **RED/GREEN:** Engine.Tests failed before implementation at skill 34's
  missing NPC Type 1; now `499/499`. Covers damage path, packed score and
  head-gated Intelligence, two-tick initial lifetime, four-tick recast, source
  resistance gate, NPC 1001 exclusion, and generated/non-generated expiry with
  saturated score restoration.
- [x] Apply only skill 34's Type 1 affect in `FinishNpcSkillAttackLocked` at
  NPC 1000, reusing Task 5CW's baseline projection. Preserve source gate order,
  Type 1 recast, timer cadence, and captured-view `UpdateScore` outcome.
- [x] Add a three-peer loopback smoke for the skill 34 Type 1 `UpdateScore`.
- [x] Full no-incremental solution build: 0 errors; pre-existing CS8604 at
  `AttackDivineBlessingTests.cs:256`. Standalone E2E build: 0 warnings/errors.
  SDK 10.0.401; `dotnet --info` exits 1 because Service Control Manager access
  is denied.
- [x] Three-peer loopback `--mob-ai-smoke` passed
  `LANCA_DE_GELO_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true`.
- [x] Update `AGENTS.md`, `PROGRESSO.md`, and traceability with actual results.
- [x] Keep the work local: no package/publish/deploy, authenticated Host,
  visual E2E, Site/production change, commit, or push.
