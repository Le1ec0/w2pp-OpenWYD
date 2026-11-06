# Mãos Sangrentas Book 5504 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Port TMProject 7.69's item 5504 acquisition into C# as an update to `MOBEXTRA.SecLearnedSkill` bit `0x10`, without deriving or writing `LearnedSkill[1]`.

**Architecture:** `WorldHub` validates the item and character, mutates the existing Extra field, and consumes one book. `WydCdk.Server` handles the recognized use-item result and sends the reference-compatible refresh; a separate disposable E2E fixture supplies an eligible character and the book. W2PP has no matching 5504 handler, so the behavior follows TMProject 7.69 and records that divergence.

**Tech Stack:** C# / .NET 10, existing `WorldHub`, legacy wire DTOs, and console-based `WydCdk.Engine.Tests`.

**Spec:** `docs/PORT_TRACEABILITY.md#skill-second-bar-acquisition-001`.

## Global Constraints

- Keep `LearnedSkill[1]` separate from `MOBEXTRA.SecLearnedSkill`; do not add a bridge, sidecar, or schema change.
- Keep the active account blob at 7945 bytes and preserve the W2PP field offsets.
- Follow TMProject 7.69 behavior for item 5504; document its absence in W2PP.
- Use only synthetic accounts and loopback fixtures; no commit, push, package, publish, or deploy.

## Review Focus

- Level 199 versus 200 must enforce the source's strict `>199` gate.
- `MORTAL` and `ARCH`, non-Trans classes, or missing base skill bit 15 must reject without changing item or Extra.
- Extra bits `0x1` or `0x100` conflict; existing `0x10` is already learned; unrelated Extra bits must survive.
- Success consumes one book and persists only Extra bit `0x10`; the client word remains unchanged.
- Carry slot/type validation and malformed MOB/Extra lengths must fail closed.

---

### Task 1: RED tests for book learning

**Files:**
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Test: `BloodHandsBookAcquisition`

**Interfaces:**
- Consumes: `WorldHub`, `UseItemRequest`, `LegacyUseItemResult`.
- Produces: expected public API `TryApplyBloodHandsBook(int, UseItemRequest, out LegacyBloodHandsBookOutcome?)`.

- [x] Add a behavior test for an eligible Celestial Trans character at level 200 with base learned bit 15 and item 5504 in carry slot 0. Assert acceptance, exactly Extra bit `0x10` added, unrelated Extra bits preserved, base learned word unchanged, and the book consumed.
- [x] Add rejection vectors for level 199, MORTAL, ARCH, wrong class, missing bit 15, existing Extra bit `0x1`, existing Extra bit `0x100`, already-set `0x10`, empty source, and invalid source type/slot. Assert no state mutation.
- [x] RED failed on the eligible 5504 case with the stub; GREEN passed `538/538` after implementation.

### Task 2: Implement exact WorldHub mutation

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `src/WydCdk.World/LegacyUseItemMath.cs`
- Modify: `src/WydCdk.World/LegacyAccountSnapshot.cs` only if a named Celestial constant is needed.

**Interfaces:**
- Consumes: the test fixture from Task 1.
- Produces: `TryApplyBloodHandsBook(int, UseItemRequest, out LegacyBloodHandsBookOutcome?)`; outcome carries source slot/item and the updated MOBEXTRA snapshot.

- [x] Implement the source gates in their observed order: level above 199, class master not MORTAL/ARCH, MOB class 0, no conflicting 9th-skill bits 0/8, learned base bit 15, and not already Extra bit 4.
- [x] On acceptance, set only Extra bit `0x10`, consume one item using the existing amount encoding, and return the updated state. Do not write the MOB learned word or invent a crosswalk.
- [x] Rerun the RED test and confirm GREEN, then run the full `WydCdk.Engine.Tests` project (`538/538`).

### Task 3: Wire dispatch and isolated manual fixture

**Files:**
- Modify: `src/WydCdk.Server/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/SkillSecondBarVisualFixture.cs`

**Interfaces:**
- Consumes: `TryApplyBloodHandsBook` and its outcome.
- Produces: a fixture preparation mode that creates a fresh character with level 200, Celestial class master, MOB class 0, base learned bit 15, Extra 0, and item 5504 in carry slot 0.

- [x] Dispatch item 5504 before item-data-dependent generic handlers; on success send message/UpdateEtc, and on rejection restore the source item and report the result.
- [x] Add a dedicated fixture mode under `Backup/Build`, refusing to overwrite a non-empty fixture path and leaving the current fixture untouched.
- [x] Build `WydCdk.Engine.slnx` with SDK 10.0.401; the fixture-preparation command created only the designated local synthetic account.

### Task 4: Record evidence and hand off the manual step

**Files:**
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `../BACKLOG.md`
- Modify: `../PROGRESSO.md`

- [x] Record the 7.69-only 5504 handler, W2PP divergence, exact Extra mutation, validation results, and the explicit non-bridge to `LearnedSkill[1]`.
- [ ] Provide the user one command to connect to the prepared fixture, use the book, and report whether it was consumed; the S-panel cell is not expected to light from Extra alone.

## Execution Ruling

Ruling: use the existing `Server` checkout — the target runtime, tests, and fixture files already have uncommitted task state that a fresh worktree would omit. Cost if wrong: this change shares a dirty checkout; scope edits to the listed files and do not commit.

## Execution log

- Implemented the 5504-only Extra mutation and the wire dispatcher. The actual 7.69 predicate checks conflict bits 0 and 8; combat's Divine Blessing consumer separately reads bit 1 (`0x2`). W2PP has no corresponding 5504 handler.
- The visual `SkillSecondBarVisual` fixture remains untouched. The new preparation mode targets the separate `Backup/Build/SkillSecondBarBloodHandsBook` directory.
