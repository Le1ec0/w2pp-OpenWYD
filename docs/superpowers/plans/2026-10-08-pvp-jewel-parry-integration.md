# Type 8 bit 6 Parry runtime integration

## Goal and boundaries

Integrate the already-tested Type 8 bit 6 arithmetic into the current C# paths:

- accumulate `-30` per active attacker affect after a positive physical or skill hit against a player;
- apply the accumulated adjustment to equipment-derived Parry for automatic summon attacks;
- reset it when character state is replaced or equipped gear changes through modeled mutation paths.

Keep the state volatile and internal to `Participant`. Do not add login persistence, wire fields, a general `MobAttack` path, or a centralized `GetCurrentScore` implementation.

## Steps

1. [x] Add black-box `WorldHub` tests first for physical and skill hits, miss and Type 6 no-op behavior, multiple Type 8 affects, and reset on character/equipment replacement. Reuse the automatic summon attack as the observable Parry consumer. Existing pure-helper tests cover NPC/non-positive-damage filtering.
2. [x] Run Engine.Tests before implementation; RED recorded 401/402 with the expected missing-runtime assertion.
3. [x] Add a volatile Parry adjustment to `Participant`; update it only after miss and Type 6 gates leave positive damage against a player. Include it in summon Parry resolution. Reset it at character replacement, equipped-item writes, and successful mount catalyst mutation.
4. [x] Run Engine.Tests and the full solution build with the pinned SDK; record `dotnet --info` and outputs under `Backup/Build/PvpJewelParryIntegration-20261008`.
5. [x] Update `AGENTS.md`, `BACKLOG.md`, `PROGRESSO.md`, and `Server/docs/PORT_TRACEABILITY.md` with behavior, evidence, divergences, and remaining scope.
6. [x] Use the standing test-VPS authorization after local validation: publish only the self-contained runtime, preserve/verify a remote backup, and check UP/PVP listeners and hashes. Transfer no source, docs, secrets, or account fixtures.

## Validation

- Focused runner fails on the missing integration before code and passes after it: RED 401/402, GREEN 402/402.
- Full Engine.Tests passes: 402/402.
- `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` passes: 0 errors and one pre-existing CS8604 warning.
- No loopback TCP or visual E2E, persistence, wire, Git commit, or push is part of this slice. The existing sandbox authorization covers only runtime artifact publication and listener/hash verification on the test VPS.
- The runtime ZIP SHA-256 is `0D8D4013B6012F561C843CD4B27EBC8CFA557A86E175FB14F7CB140242B58FDD`; 197/197 runtime hashes matched after deployment. Both scheduled tasks were `Running`, with 8281/8282 listeners owned by the expected Core executable. The prior 197-file runtime and SHA manifest remain in `C:\WYDCDK\Backup\PvpJewelParryIntegration-20261008`.
