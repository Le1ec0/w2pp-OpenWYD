# Player skill 13 Type 24 Samaritan

## Goal

Complete the online player-score projection for skill 13 (`Samaritano`) using
the already-portable Type 24 AC stage. Keep the matcher tied to the audited
7.69/W2PP skill row and preserve HP/MP while applying, refreshing, and expiring
the affect.

## Evidence

- TMProject 7.69 `Basedef.cpp:3588-3592` and W2PP `Basedef.cpp:4225-4229`
  apply each Type 24 as `AC += AC / 4 + Value` in affect-slot order.
- The skill 13 fixture row is non-passive, self-targeted, non-aggressive,
  non-ticking, and produces Type 24.
- `LegacyCurrentScoreMath` already composes Type 24. `WorldHub` does not yet
  classify the online skill 13 row for score rebuild.

## Steps

- [x] Add player-cast, recast, and expiry regression coverage; RED reproduced
  the missing projection (AC 100 vs 125 expected).
- [x] Add the exact skill 13 matcher to player score rebuild and affect mapping.
- [x] Run Engine.Tests (`534/534`), the full no-incremental solution build
  (exit 0), and the loopback smoke (`player-type24-expiry=score-only-relayed`).
- [x] Update `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`, `PORT_TRACEABILITY.md`,
  and the smoke guide in place with results and limits.
