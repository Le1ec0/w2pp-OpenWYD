# Plan: MOB-AI-001 Type 1 Holy Touch on the first NPC index

**Spec:** `docs/superpowers/specs/2026-10-15-mob-ai-design.md`
**Execution:** inline in the current dirty checkout, preserving existing user changes.
**Scope:** skill 1 `Toque_Sagrado` against NPC connection 1000, its score projection, gates, recast, timer expiry, and view-scoped `UpdateScore` evidence.

## Source contract

TMProject 7.69 row 1 defines `AffectType=1`, `AffectValue=2`, `AffectTime=3`,
`Aggressive=1`, and `AffectResist=2`. The loader divides `AffectTime` by four,
so the normal first application has one tick when the caster's Special is zero.
TMProject and W2PP use the generic aggressive gates: different leader and guild,
not a merchant, target not `RSV_BLOCK`, player caster cannot affect Clan 6, and
resistance roll `<= RegenMP + AffectResist + levelDifference`. Both `SetAffect`
functions accept `conn == MAX_USER == 1000`, reject higher NPC indexes, and
reset an existing Type 1 duration to four ticks.

Both score loops subtract 2 from Run and 30 from Attack; Intelligence also loses
40 when the head item index is above 50. `AttackRun` packs Attack in the high
nibble in units of ten and Run in the low nibble. Final score composition clamps
Attack to 0..150 and Run to 0..6. The generic `LessAffect` path decrements the
timer, clears the full eight-byte affect record at expiry, and refreshes score.
The C# already has `LegacyCurrentScoreMath.CalculateHolyTouchAffectStage`; this
task connects that stage to the first-NPC attack and timer paths.

## Files

- Create `tests/WydCdk.Engine.Tests/AttackNpcHolyTouchAffectTests.cs`.
- Modify `tests/WydCdk.Engine.Tests/Program.cs` to register the behavior tests.
- Modify `src/WydCdk.World/WorldHub.cs` for the skill gate, packed score
  projection, saved pre-affect score, recast, and generated/non-generated expiry.
- Modify `tests/WydCdk.Client.E2E/MobAiNetwork.cs` for a three-peer loopback
  `UpdateScore` assertion.
- Update `docs/PORT_TRACEABILITY.md`, the root `AGENTS.md` handoff, and
  `PROGRESSO.md` after the required validation passes.

## Task 1

**Interfaces:** `WorldHub.TryApplySkillAttack` consumes a `LegacySkillDefinition`
and returns the existing `LegacySkillAttackOutcome`; the existing pure score
stage returns `LegacyHolyTouchAffectStage`. Keep changes inside `WydCdk.World`
and use the existing `LegacyNpcAffectScoreRelay`/`LegacyMobAiRelay` boundaries.

- [x] **RED:** Engine.Tests had 498 cases; the new Type 1 assertion failed
  (`497/498` passed) because the NPC producer/projection was missing. Coverage
  includes projection at NPC 1000,
  conditional Intelligence, the NPC 1001 boundary, all aggressive gates and
  resistance threshold, four-tick recast, and generated/non-generated expiry.
  The `AttackRun` saturation case verifies restoration of the exact original
  packed byte.
- [x] **GREEN:** apply skill 1 only at NPC 1000 through the source gates; save the
  original packed `AttackRun` and Intelligence on first application, derive the
  active projection from that baseline, preserve it on recast, and restore it
  when Type 1 expires. Add Type 1 to both existing NPC timer loops.
- [x] Add a three-peer `--mob-ai-smoke` case proving actor and observer receive
  the Type 1 `UpdateScore`, including packed score values, while the outside
  peer receives no frame. Marker:
  `TOQUE_SAGRADO_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true`.
- [x] Engine.Tests passed `498/498`; `dotnet build WydCdk.Engine.slnx
  --no-incremental` succeeded with 0 errors and the pre-existing CS8604 at
  `AttackDivineBlessingTests.cs:256`; E2E build succeeded with 0 warnings/errors.
  SDK 10.0.401; `dotnet --info` exited 1 because the runner cannot open Service
  Control Manager. Updated project handoff and traceability. Work remains local:
  no publish/deploy, authenticated Host or visual E2E, Site/production change,
  commit, or push.

## Review focus

- Type 1 must not decrement AttackRun repeatedly when recast or ticked.
- Expiry must restore the exact pre-affect packed score even when Attack or Run
  reached zero under the debuff.
- NPC 1001 and every source gate must leave both affect and score untouched.
- Timer score relay must remain limited to the NPC's captured view.
