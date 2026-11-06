# Non-Generated NPC Type 17 Timer Implementation Plan

> **For agentic workers:** Implement inline in the current shared checkout and preserve unrelated dirty work.

**Goal:** Apply the source-backed Type 17 heal and timer expiry to eligible non-generated NPCs.

**Architecture:** Extend the existing non-generated NPC timer helper and reuse its Peace/Combat cadence, captured view, and affect relay outcome. Keep generated NPC, summon, player, movement, and attack paths unchanged.

**Tech Stack:** C# / .NET 10, WorldHub, LegacyMobAiRuntime, Engine.Tests, TCP loopback smoke.

**Spec:** `docs/superpowers/specs/2026-10-23-nongenerated-npc-type17-design.md`

## Global Constraints

- Compare TMProject 7.69 and `Server/W2PP` before porting behavior.
- Preserve the shared dirty checkout and unrelated local changes.
- Do not package, publish, deploy, commit, or push.
- Keep generated NPCs, summons, players, attacks, and movement outside this slice.

## Review Focus

- A Peace NPC heals by the source formula, clamps to MaxHp, and decrements once on its shard visit.
- A Combat NPC waits for the existing fourth-opportunity cadence and does not synthesize an attack without a target.
- A full-HP Life Aura still follows source timer semantics; expiry clears all affect bytes and refreshes score.
- SetHpDam precedes UpdateScore and both are restricted to the captured view.

## Files

- Modify `tests/WydCdk.Engine.Tests/LegacyMobAiRuntimeTests.cs` for Peace and Combat behavior.
- Modify `tests/WydCdk.Client.E2E/MobAiNetwork.cs` for three-peer frame order and view-scope smoke.
- Modify `src/WydCdk.World/WorldHub.cs` to consume Type 17 and return the HP/score outcome.
- Update `docs/PORT_TRACEABILITY.md`, `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md` in place.

## Steps

- [x] Add failing Peace and Combat tests; confirm the unprocessed Type 17 behavior with RED.
- [x] Add Type 17 to the existing helper, implementing the source heal formula and reusing the existing outcome/relay ordering.
- [x] Add a loopback smoke that verifies SetHpDam then UpdateScore for in-view peers and no frames outside the view.
- [x] Run Engine.Tests `495/495`, full no-incremental solution build, `--mob-ai-smoke`, and `git diff --check`.
- [x] Record the 7.69/W2PP trace, update project handoff/backlog, and document the final evidence and remaining boundaries.

## Review

- Independent review approved the implementation with no critical or important findings.
- Minor deferred: add a test for a full-HP Type 17 tick whose timer remains active; it should relay zero-delta `SetHpDam` without `UpdateScore`.
- Minor deferred: add a multi-slot regression for multiple Type 17 records and mixed supported affects. The current loop matches the source walk, but this combination is not characterized by tests.

