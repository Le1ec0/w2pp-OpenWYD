# Non-Generated NPC Type 32 Timer Design

## Intent

Process an already-present Type 32 affect on an eligible ordinary persistent
NPC using the timer behavior shared by TMProject 7.69 and W2PP. Type 32 uses
the same countdown and expiry tail as other generic affects: finite time
decrements, expiry clears the eight-byte record, and score refresh is requested.

## Scope

- Include non-generated live NPCs with no leader and no AutoTrade state.
- Preserve the current Peace shard visit and fourth Combat opportunity cadence.
- Keep HP unchanged; request no HP update, attack, movement, or despawn.
- Relay UpdateScore only at expiry and only to the view captured by WorldHub.
- Do not change skill 47 production, SetAffect target eligibility, player,
  generated-NPC, summon, or visual-client behavior.

## Source Evidence

- TMProject 7.69 `_MSG_Attack.cpp:1591-1617` and W2PP
  `Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1126-1152` special-case skill
  47 to remove Type 19. When Type 19 is absent, the common skill path calls
  `SetAffect` (`TMProject:1727`, `W2PP:1228`). TMProject SkillData row 47
  (`Servidor/Server/TMSrv/run/skilldata.csv:48`) defines `Cancelamento` with
  AffectType 32.
- Both `SetAffect` implementations reject `conn > MAX_USER`; this slice does
  not widen that producer rule or claim skill 47 can create Type 32 for every
  NPC slot.
- In both `ProcessAffect` implementations Type 32 reaches the shared
  `LessAffect` tail. Finite time below 32,400,000 decrements; expiry clears
  Type/Value/Level/Time and requests score refresh
  (`TMProject Server.cpp:6899-6920`; `W2PP Server.cpp:5831-5851`).
- The existing source timer loops call `ProcessAffect` on Peace and Combat
  NPC visits; C# already models those schedules for ordinary NPC timers.

## Acceptance

Engine tests cover Peace countdown and targetless Combat fourth-opportunity
countdown, full affect clearing at expiry, unchanged HP, no attack, and a
score-only outcome. TCP loopback confirms UpdateScore reaches both in-view peers
and no frame reaches the outside peer. Evidence remains synthetic and does not
establish authenticated Host or visual-client behavior.
