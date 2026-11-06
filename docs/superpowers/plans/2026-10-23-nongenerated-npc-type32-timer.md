# Non-Generated NPC Type 32 Timer Implementation Plan

> **For agentic workers:** Implement inline in the current checkout and preserve unrelated dirty work.

**Goal:** Apply source-backed Type 32 countdown and score-only expiry to eligible ordinary NPCs.

**Architecture:** Extend `WorldHub.TryProcessNonGeneratedNpcTimerAffects` to accept Type 32 as a timer-only affect. Keep its existing eligibility checks, Peace/Combat cadence, view capture, and relay.

**Tech Stack:** C# / .NET 10, WorldHub, Engine.Tests, TCP loopback smoke.

**Spec:** `docs/superpowers/specs/2026-10-23-nongenerated-npc-type32-design.md`

## Global Constraints

- Compare TMProject 7.69 with `Server/W2PP` before porting behavior.
- Preserve the shared dirty checkout and unrelated local changes.
- Do not package, publish, deploy, commit, or push.
- Do not change the skill 47 producer, target eligibility, or other timer paths.

## Review Focus

- Peace Type 32 timer decrements on the NPC's assigned shard visit and stays silent before expiry.
- Targetless Combat Type 32 decrements on the fourth opportunity and does not synthesize an attack.
- Expiry clears all eight bytes, preserves HP, and relays only UpdateScore to the captured view.

### Task 1: Extend the ordinary NPC timer for Type 32

**Files:**

- Modify `tests/WydCdk.Engine.Tests/LegacyMobAiRuntimeTests.cs` for Peace and Combat regression cases.
- Modify `tests/WydCdk.Client.E2E/MobAiNetwork.cs` to include Type 32 in the score-only view-scoped smoke.
- Modify `src/WydCdk.World/WorldHub.cs` to accept Type 32 in the existing ordinary NPC timer helper.
- Update `docs/PORT_TRACEABILITY.md`, `docs/CLIENT_E2E_SMOKE.md`, root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md` in place.

**Interfaces:**

- Consume the existing `WorldHub.TryProcessNonGeneratedNpcTimerAffects` Peace and Combat entry points.
- Add no public API; preserve existing affect outcome and captured-view relay contracts.

**Steps:**

- [x] Add failing Peace and targetless Combat tests; confirm both fail because Type 32 is ignored by the ordinary NPC timer.
- [x] Add the Type 32 TCP loopback assertion; confirm the smoke fails before production change.
- [x] Add Type 32 to the existing timer-only affect filter and update the XML summaries.
- [x] Run Engine.Tests, full no-incremental solution build, `--mob-ai-smoke`, `dotnet --info`, and `git diff --check`.
- [x] Record source comparison and final local evidence in the traceability and handoff documents.

## Review

- Keep the Type 32 producer and the legacy `conn > MAX_USER` guard outside this timer slice.
- Treat TCP loopback as synthetic WorldHub evidence; authenticated Host and visual client remain unverified.
- Final review: independent read-only reviewer found no Critical or Important issues; approved for local close. Deferred minor: the Combat test checks the timer after groups of four opportunities rather than asserting the unchanged state after each of opportunities 1-3; the implementation phase logic is correct by inspection.
