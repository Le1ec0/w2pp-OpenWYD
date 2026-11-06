# Non-Generated NPC Type 23 Timer Implementation Plan

> **For agentic workers:** Implement inline in the current checkout and preserve unrelated dirty work.

**Goal:** Apply the source-backed Type 23 countdown and score-only expiry to eligible non-generated NPCs.

**Architecture:** Extend the existing non-generated NPC Type 22 timer to process Type 23 as the same score-only timer effect. Keep the current Peace and Combat shard scheduling, eligibility, view capture, and relay paths.

**Tech Stack:** C# / .NET 10, WorldHub, LegacyMobAiRuntime, Engine.Tests, TCP loopback smoke.

**Spec:** `docs/superpowers/specs/2026-10-23-nongenerated-npc-type23-design.md`

## Global Constraints

- Compare TMProject 7.69 with `Server/W2PP` before porting behavior.
- Preserve the shared dirty checkout and unrelated local changes.
- Do not package, publish, deploy, commit, or push.
- Keep generated NPC, summon, AutoTrade, player, and visual-client behavior outside this slice.

## Review Focus

- Type 23 on a Peace NPC decrements at its assigned Peace shard visit and remains silent before expiry.
- Type 23 on a Combat NPC uses the existing fourth-visit affect cadence and does not synthesize an attack without a target.
- Expiry clears all 8 bytes, leaves HP unchanged, and relays UpdateScore only to the captured view.

## Files

- Modify `tests/WydCdk.Engine.Tests/LegacyMobAiRuntimeTests.cs` for Peace and Combat regression coverage.
- Modify `tests/WydCdk.Client.E2E/MobAiNetwork.cs` to check view-scoped Type 23 expiry in loopback.
- Modify `src/WydCdk.World/WorldHub.cs` to allow the already scheduled timer helper to process Type 23.
- Update `docs/PORT_TRACEABILITY.md`, `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md` in place.

## Steps

- [x] Add failing Peace and Combat tests and confirm they fail because Type 23 is not consumed.
- [x] Add the Type 23 loopback expiry assertion and confirm the smoke fails before the implementation.
- [x] Add Type 23 to the existing score-only countdown helper; do not add attack or movement behavior.
- [x] Run Engine.Tests, full no-incremental solution build, `--mob-ai-smoke`, and `git diff --check`.
- [x] Update the project handoff, backlog, and source traceability in place.

## Review

- Independent review approved the slice with no critical or important findings.
- Minor deferred: the `thunderType` and `bestialAuraType` constants in `WorldHub.cs` have excess indentation. This is formatting only and does not affect behavior.
- Optional follow-up: add a regression proving this helper does not process Type 23 for generated NPCs, leaders, or AutoTrade NPCs. The current eligibility predicate excludes them; that exclusion is not covered by the new tests.
