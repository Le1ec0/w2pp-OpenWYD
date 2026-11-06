# MSG_ApplyBonus Special Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the confirmed TMProject 7.69 `BonusType=1` Special allocation branch of `_MSG_ApplyBonus`.

**Architecture:** Keep the authoritative bonus-point mutation inside `WorldHub` under its existing gate, return snapshots and relay intent, and let the Server encode the 7.69 ordered `UpdateScore`/`UpdateEtc` or `MessagePanel` responses. Preserve the W2PP-only `SecLearnedSkill` cap extension as comparative evidence, not active behavior.

**Tech Stack:** C# / .NET 10, WydCdk.World, WydCdk.Server, WydCdk.Protocol, Engine.Tests.

**Spec:** TMProject 7.69 and W2PP `_MSG_ApplyBonus.cpp`; `Server/docs/PORT_TRACEABILITY.md`, ITEM-009.

## Global Constraints

- Preserve the dirty checkout; no commit, push, deploy, Site/prod change, or visual E2E.
- TMProject 7.69 is the release behavior reference. Record and do not activate the W2PP-only `SecLearnedSkill` 0x111 cap-320 extension.
- Do not change schema or combine `LearnedSkill[0]`, `LearnedSkill[1]`, and `Extra.SecLearnedSkill`.
- Run the local solution build and Engine.Tests for this code change; report environmental failures accurately.

## Review Focus

- SpecialBonus zero sends only UpdateEtc, regardless of Detail.
- Success increments one BaseScore.Special entry, consumes one point, then sends UpdateScore before UpdateEtc.
- The level-derived cap runs before the 200/255 hard cap and chooses the corresponding Portuguese message.
- Final base-skill bits 7/15/23 raise only Special2/3/4 to 255; ordinary caps remain 200.
- Dead players do not mutate; invalid Detail with points has no response.

### Task 1: World behavior and tests

**Files:** Modify `tests/WydCdk.Engine.Tests/Program.cs` and `src/WydCdk.World/WorldHub.cs`.

**Interface:** Add `TryApplySpecialScoreBonus(connectionId, detail, out outcome)`, with explicit outcomes for Applied, NoPoints, InvalidDetail, LevelCapReached, AttributeCapReached, NotAlive, and unavailable state/item data.

- [x] Add failing coverage for mapping, one-point debit, current score projection, ordinary/learned-skill caps, advanced class level cap, W2PP-only mask isolation, no-points, invalid Detail, and dead character.
- [x] Run the build to confirm the API is missing.
- [x] Implement the 7.69 checks and locked mutation; preserve Extra and the three skill words.

### Task 2: Server relay and traceability

**Files:** Modify `src/WydCdk.Server/Program.cs`, root `AGENTS.md`, root `PROGRESSO.md`, `BACKLOG.md`, and `docs/PORT_TRACEABILITY.md`.

- [x] Dispatch BonusType=1; encode successful UpdateScore then UpdateEtc, no-points UpdateEtc only, and the two 7.69 MessagePanel strings for cap failures.
- [x] Build the full solution with 0 warnings and 0 errors; final Engine.Tests was `513/556`, with both ApplyBonus tests passing and fixture/environment failures remaining.
- [x] Update the ITEM-009 handoff and traceability with the W2PP divergence and unvalidated visual/save-reload boundary.
