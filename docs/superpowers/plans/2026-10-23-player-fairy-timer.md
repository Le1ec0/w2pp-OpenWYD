# Player Fairy Timer Attack Implementation Plan

> **For agentic workers:** Implement inline in the current checkout. Follow the project's TDD cycle and preserve unrelated local changes.

**Goal:** Port the 7.69 player timer's automatic attacks for equipped Fairy items 753, 769, and 1726.

**Architecture:** Preserve the last living target of an accepted attack in player runtime state. On the player affect timer, apply the exact source gates, charge the configured skill mana, and resolve one skill target through `WorldHub`'s existing combat batch. Relay the resulting server-authored `MSG_AttackOne` and combat side effects through the existing player affect relay.

**Tech Stack:** C# / .NET 10, `WorldHub`, `LegacyPlayerAffectRuntime`, protocol confirmations, Engine.Tests, loopback Client.E2E.

**Spec:** `AGENTS.md` current 7.69 / W2PP comparison and local-only constraints; TMProject `Servidor/Source/Code/TMSrv/Server.cpp:6033-6163` plus `_MSG_Attack.cpp:2283` and W2PP counterparts `Server.cpp:4968-5098`, `_MSG_Attack.cpp:1678`.

## Global Constraints

- Compare the behavior with both TMProject 7.69 and `Server/W2PP` before changing code.
- Preserve the 7.69 item-to-skill mapping: 753 -> 32, 769 -> 34, 1726 -> 36; generated request uses motion 254 and `ClientTickPolicy.SkipCheckTick`.
- Keep attack target, coordinates, combat state, and relays server-authoritative; do not trust packet damage.
- Keep the scope local to the player timer/combat relay. No Site, production, commit, or push changes.

## Task 1: Capture current-target and Fairy behavior in tests

**Files:**
- Modify: `tests/WydCdk.Engine.Tests/PlayerTransformationAffectTimerTests.cs`
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs` only if the smoke entry point needs a new method call.

- [x] Add Engine.Tests coverage that an accepted attack leaves its living target selected and the timer maps each Fairy item to its source skill ID.
- [x] Add gate coverage for a dead/missing target, forbidden player target while PK mode is off, Clan 4 target, and insufficient mana.
- [x] Add a loopback case that the synthesized 68-byte `MSG_AttackOne` reaches only the captured view and target HP/resource updates reach the target.
- [x] Confirm RED before runtime changes and GREEN in the final Engine.Tests run (`495/495`).

## Task 2: Port target tracking and the timer branch

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `src/WydCdk.World/LegacyAttackActionBatch.cs` only if the timer outcome needs a dedicated single-target field.
- Modify: `src/WydCdk.Protocol/AttackConfirmation.cs`
- Modify: `src/WydCdk.Server/LegacyPlayerAffectRelay.cs`

- [x] Store the last living target selected by each accepted player attack batch, in slot order, matching the 7.69/W2PP `_MSG_Attack` tail.
- [x] Implement the timer gates from `ProcessAffect`: active Fairy index, target presence/mode, map attribute or non-mount NPC, PK mode for player targets, distinct leader/guild, player RSV/merchant, same-clan 7/8, and legacy target-coordinate gate.
- [x] Consume mana using the configured skill and existing Magic Mirror rule; then call the common attack batch with one server-selected target and the skip-check tick.
- [x] Emit a server-authored one-target attack frame with source skill and motion, plus existing target HP/score, death, reward, and view-removal side effects.
- [x] Run Engine.Tests `495/495`, full build with 0 errors (one pre-existing CS8604), and loopback smoke marker `player-fairy-753-769-1726=attackone-view-scoped`.

## Task 3: Verify and record the bounded port

**Files:**
- Modify in place: `docs/PORT_TRACEABILITY.md`, root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md`.

- [x] Run Engine.Tests through its console harness (`dotnet run --project tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj`), build `WydCdk.Engine.slnx --no-incremental`, and the `--mob-ai-smoke` loopback harness; record SDK/build warnings accurately.
- [x] Record the Task 5CN scope, tests, source/W2PP comparison, and remaining differences in existing project Markdown.
- [x] Keep this turn local: no package/publish/deploy, authenticated Host, visual `WYD.exe` E2E, Site/production, commit, or push.
