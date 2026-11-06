# Non-Generated NPC Type 17 Timer Design

## Intent

Continue the ordinary non-generated NPC affect timer port with Life Aura
(Type 17). TMProject 7.69 and `Server/W2PP` agree on its healing and common
timer behavior. The current C# timer already visits eligible NPCs but only
interprets Types 22 and 23.

## Scope

- Apply Type 17 to eligible persistent, non-generated NPCs in Idle/Peace and
  Combat on the timer opportunities already used by Types 22/23.
- Heal by `AffectLevel / 2 + AffectValue`, clamp HP to `[1, MaxHp]`, preserve
  the signed HP delta, decrement finite time below `32,400,000`, and clear all
  eight affect bytes at expiry.
- Relay the legacy HP update before score refresh, only to recipients captured
  at the NPC view.
- Do not add attacks, movement, or support for other affect types in this slice.

## Approaches

1. Extend the existing non-generated timer helper to process Type 17. This
   reuses the existing scheduler and relay contract, with Type 17-specific HP
   handling copied from the already tested generated-NPC path. **Selected**:
   it is the smallest source-backed change.
2. Extract a shared affect processor for generated and non-generated NPCs.
   This would touch established generated-NPC behavior and broaden review beyond
   the single requested branch.
3. Port Type 20 Poison first. It is another timer branch, but adds a separate
   damage formula and is less bounded than Type 17.

## Constraints and Acceptance

- Compare the 7.69 source and W2PP behavior before implementation.
- Preserve the dirty shared checkout; no commit, push, package, publish, or
  deploy.
- Tests must prove Peace healing/countdown/expiry and Combat cadence without a
  synthesized attack. Loopback smoke must prove frame order and view scoping.
- Run Engine.Tests, the full no-incremental solution build, `--mob-ai-smoke`,
  and `git diff --check`; record SDK/build evidence and update project Markdown
  in place.

