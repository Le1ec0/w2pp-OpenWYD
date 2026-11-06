# Skill attack RSV proc design

## Intent

Port the `RSV_FROST` and `RSV_DRAIN` secondary effects from the common 7.69
`Exec_MSG_Attack` tail to C# skill attacks. The physical attack route already
implements these effects; the skill route currently omits them. This closes a
behavioral gap for positive-damage skill hits against players, NPCs and summons.

## Goals and boundaries

- Run each proc only after the attack has positive final damage, with its own
  50% roll.
- Frost uses SkillData 36 through `SetAffect`; Drain uses SkillData 40 through
  `SetTick`. Preserve their different NPC-index and Merchant gates.
- Project Type 1 score changes on player and NPC targets and return the changed
  affect snapshots for the existing relay path.
- Keep physical attacks, skill damage formulas, and all unrelated skill effects
  unchanged.
- Compare TMProject 7.69 and W2PP at the producer and helper boundaries before
  changing behavior.

## Source evidence

TMProject 7.69 `_MSG_Attack.cpp:1943-1968` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1466-1489` run the two procs in
the common target loop after `dam <= 0` has continued. Frost calls
`SetAffect(idx, 36, Special2 + 150, Special2)` and `SetTick` with skill 36;
Drain calls the same pair with skill 40. SkillData row 36 provides Type 1 and
row 40 provides Tick Type 20. Both sources agree on the producer.

`SetAffect` rejects a Merchant and target indices above 1000, and checks
`RSV_BLOCK` for aggressive skills. `SetTick` permits indices above 1000 but
rejects a Merchant there, and also checks `RSV_BLOCK`. The existing C# helper
`TryApplyLegacySecondaryAttackEffect` already captures these separate rules.

## Approaches

1. Duplicate the frost and drain calls in each skill-target finalizer. This
   keeps each edit local but repeats the roll and gating logic.
2. Add one shared C# proc helper and invoke it from player, NPC and summon skill
   finalizers after final damage resolution. This keeps the common source tail
   together while leaving the existing `SetAffect` and `SetTick` gates separate.

Implementation decision: use approach 2. Add optional per-slot roll inputs so
Engine tests and the loopback smoke can exercise both proc outcomes without
depending on randomness. Normal Host requests keep the existing random behavior.

## Constraints and acceptance

- Use the pinned .NET 10 SDK and preserve the existing dirty checkout.
- Add behavior tests before implementation; validate the Engine suite, solution
  build, and a dedicated loopback smoke marker.
- Update existing project handoff, progress, backlog, and traceability files in
  place. No commit, push, Site/prod change, or deployment is part of this slice.
