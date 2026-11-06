# Non-Generated NPC Type 23 Timer Design

## Intent

Port the existing Type 23 Bestial Aura timer behavior for ordinary persistent
NPCs in the C# world. In both C++ references, the area attack is dispatched only
for player indexes; an NPC reaches the shared affect countdown and score refresh
without damaging targets.

## Scope

- Include non-generated NPCs with no owner and no AutoTrade state, using the
  current Peace and Combat timer shards.
- Decrement finite time, clear all affect bytes at expiry, preserve HP, and relay
  only the score refresh captured for the NPC's view.
- Keep generated NPC, summon, AutoTrade, player, movement, and combat behavior
  outside this slice.

## Source Evidence

- TMProject 7.69 `Servidor/Source/Code/TMSrv/Server.cpp:6589-6895` checks Type
  23, stops NPC target scanning at `idx >= MAX_USER`, and guards
  `ProcessClientMessage` with `idx < MAX_USER`; the shared timer tail follows.
- W2PP `Server/W2PP/Source/Code/TMSrv/Server.cpp:5522-5827` has the same
  player-only attack dispatch and common countdown behavior.
- Peace and Combat timer call sites invoke `ProcessAffect` for NPCs in both
  sources, including the existing fourth-visit Combat phase.

## Approaches

1. Duplicate the Type 22 timer method for Type 23. Rejected because source
   countdown and expiry are identical and separate implementations could drift.
2. Extend the existing non-generated NPC timer helper to accept the audited
   score-only area affects Type 22 and 23. Recommended because it preserves the
   established shard cadence and changes only the allowed effect types.
3. Run the player area attack or generated-NPC attack path for Type 23. Rejected
   because both references guard attack dispatch to player indexes.

## Acceptance

Peace and Combat tests prove countdown, full affect clear at expiry, unchanged
HP, and no synthesized attack. TCP loopback proves score-only UpdateScore goes
to the two in-view peers and not to the outside peer. Validation is local and
synthetic; it does not establish authenticated Host or visual client behavior.
