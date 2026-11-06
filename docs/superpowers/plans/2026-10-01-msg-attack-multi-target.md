# MSG_Attack Multi-Target Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task.

**Goal:** Process all physical `MSG_Attack` slots in wire order as one authoritative WorldHub action and return one aggregate confirmation.

**Architecture:** `WorldHub` receives indexed physical slots and resolves them under one lock with one shared action state. The host builds one `AttackConfirmation` after the batch and sends frames outside the lock; the existing one-target WorldHub API wraps a one-slot batch.

**Tech Stack:** C#/.NET 10, `WydCdk.Engine.Tests` console suite, `WydCdk.Client.E2E` TCP loopback harness.

**Spec:** [MSG_ATTACK_MULTI_TARGET_DESIGN.md](../../MSG_ATTACK_MULTI_TARGET_DESIGN.md)

## Global Constraints

- TMProject2GlobalClient 7.69 defines the behavior target; compare `Server/W2PP` and record any divergence.
- Wire attack types carry 1, 2, or 13 slots; only physical marker `Damage == -2` enters this slice.
- `DoubleCritical` and `Progress` are action-wide and are resolved only when original slot 0 reaches the physical branch; the client `DoubleCritical` byte is not authoritative.
- Range remains `> 23` using authoritative C# positions in this slice; packet-coordinate parity is separate.
- Use TDD and run the custom Engine.Tests suite, full solution build, and TCP smoke on `127.0.0.1:18283`; do not open `WYD.exe`.
- Before build, record `dotnet --info` because the shared root currently has no `global.json`. Use CLI commands with `UseSharedCompilation=false`; do not launch Visual Studio or other GUI build tools.
- Preserve existing dirty changes. No reset, staging, commit, push, Site/prod change, or secret output. After local gates pass, the existing authorization permits runtime-only publication to the test sandbox with backup and hash/listener verification.
- For test changes, follow `superpowers:test-driven-development` and read its `writing-good-tests.md` guidance before authoring cases.

## Review Focus

- A non-physical, absent, or rejected slot 0 must not cause a later physical slot to calculate `DoubleCritical` or advance `Progress`; cover in Tasks 1 and 3.
- Counts 1/2/13 and gaps between physical slots must preserve original wire indexes; cover in Task 2.
- A class-3 passive proc replaces slot 1 and must leave that slot's original target untouched; cover in Task 3.
- A repeated target observes earlier HP changes and its death is finalized once; cover in Tasks 2 and 4.
- An out-of-range later slot preserves earlier mutations and immediate effects, skips later slots and suppresses the final relay/death effects; cover in Task 4 and the host path in Task 5.

## Files and Interfaces

- Create `Server/tests/WydCdk.Client.E2E/AttackMultiTargetNetwork.cs` for the loopback smoke.
- Modify `Server/tests/WydCdk.Client.E2E/Program.cs` to register `--attack-multi-target-smoke` with the existing isolated fixture guard.
- Create `Server/src/WydCdk.World/LegacyPhysicalAttackBatch.cs` for the batch request/result records and per-slot roll overrides.
- Modify `Server/src/WydCdk.World/WorldHub.cs` to expose
  `TryApplyPhysicalAttackBatch(LegacyPhysicalAttackBatchRequest request, out LegacyPhysicalAttackBatchOutcome? outcome)`.
- Preserve the public signature of `TryApplyPhysicalAttack(...)`; implement it as a one-slot wrapper and map the first slot result back to `LegacyPhysicalAttackResult` and `LegacyPhysicalAttackOutcome`.
- `LegacyPhysicalAttackBatchRequest` carries attacker ID, ordered
  `LegacyPhysicalAttackBatchSlotRequest` entries (`SlotIndex`, target ID, and
  target-specific roll overrides), `ClientProgress`, whether the wire packet
  supports slot 1, item data, and action-level critical/passive roll overrides.
- `LegacyPhysicalAttackBatchSlotRolls` groups the existing per-target injection
  arguments (`RandomFactor`, parry, absorption, frost/drain, and drop/Pista
  rolls); action-level rolls group the existing DoubleCritical/passive overrides
  so the action values are consumed at most once.
- `LegacyPhysicalAttackBatchOutcome` carries per-slot results with original
  indexes, `AbortSlotIndex`, action `DoubleCritical`, `NextProgress`, final
  attacker snapshot, and whether processing completed normally.
- Modify `Server/src/WydCdk.Server/Program.cs` to collect every physical slot,
  call the batch once, build one confirmation, and flush captured effects after
  the WorldHub lock. `AttackRequest` and `AttackConfirmation` already expose the
  required 1/2/13-slot parsing and indexed output; change them only if a new test
  demonstrates a missing contract.
- Modify `Server/tests/WydCdk.Engine.Tests/Program.cs`; add named cases to its
  existing custom test registry.
- Update `Server/docs/PORT_TRACEABILITY.md`, `Server/docs/CLIENT_E2E_SMOKE.md`,
  root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md` after verification.

---

### Task 1: Capture the missing wire behavior with a failing loopback test

**Files:** create `AttackMultiTargetNetwork.cs`; modify E2E `Program.cs`.

- [x] Write a smoke that connects two isolated account copies on loopback, sends one 13-slot `MSG_Attack` with slot 0 empty and physical slots 1 and 2 targeting the high-HP peer, and asserts one attack response on each stream, matching payloads, and positive server-calculated damage at indexes 1 and 2. Set input `DoubleCritical=0xFF`; the reply must not echo that client byte and must preserve `Progress` because slot 0 is skipped.
- [x] Register `--attack-multi-target-smoke`; require distinct isolated fixtures, loopback host, port `18283`, and the existing isolated autotrade state path. Do not use original account blobs.
- [x] Run the smoke against the current local runtime and capture the expected RED: the response has empty slot 1. Evidence: `Backup/Build/AutoTradeCloseSmoke/75913d403f554dad8968040d98ad715c`.

Run from repository root:

```powershell
dotnet run --project .\Server\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-restore -p:UseSharedCompilation=false -- --host 127.0.0.1 --port 18283 --account-file <isolated-actor-copy> --autotrade-close-peer-account-file <isolated-peer-copy> --autotrade-state-file <isolated-copy-root\autotrade.json> --attack-multi-target-smoke
```

Expected before implementation: `CLIENT_E2E_FAIL` because the current host selects the physical branch only when slot 0 is `-2`, so no physical target resolves and slots 1 and 2 remain empty. Run only against the disposable local runtime and fixture copies.

### Task 2: Add the batch contract and ordered WorldHub processing

**Files:** create `LegacyPhysicalAttackBatch.cs`; modify `WorldHub.cs` and Engine.Tests `Program.cs`.

**Interfaces:**

- `LegacyPhysicalAttackBatchSlotRequest(int SlotIndex, int TargetConnectionId, LegacyPhysicalAttackBatchSlotRolls? Rolls = null)`.
- `LegacyPhysicalAttackBatchRequest` contains `AttackerConnectionId`, ordered `Slots`, `ClientProgress`, `SupportsSecondaryDamageSlot`, optional `ItemData`, and action roll overrides.
- `LegacyPhysicalAttackBatchOutcome` contains indexed `SlotOutcomes`, `AbortSlotIndex`, action `DoubleCritical`, `NextProgress`, `AttackerMobSnapshot`, and `CompletedNormally`.
- Each slot outcome contains the original index, the existing `LegacyPhysicalAttackResult`, and its optional `LegacyPhysicalAttackOutcome`.

- [x] Add the result/request type declarations and a compiling batch method surface after the Task 1 RED is recorded.
- [x] Write `WorldPhysicalAttackBatchCardinalityAndOrder` for 1, 2, and 13 targets; assert each output index and target match input order, and assert a repeated live target loses HP twice in that order.
- [x] Run Engine.Tests and confirm the new assertions fail against the unfinished batch implementation.
- [x] Implement one `lock (gate)` over the full ordered loop. Extract the current per-target body into a lock-held helper that receives the batch action context; collect per-index outcomes without network I/O.
- [x] Make `TryApplyPhysicalAttack` construct a one-slot request at index 0, call the batch, and preserve its current return/out parameters and deterministic roll arguments.
- [x] Run Engine.Tests; expect all 1/2/13, order, repeated-target, and existing single-target tests to pass.

Run from `Server`:

```powershell
dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

### Task 3: Share action critical/progress and reserve the passive slot

**Files:** modify `LegacyPhysicalAttackBatch.cs`, `WorldHub.cs`, Engine.Tests `Program.cs`.

- [x] Write `WorldPhysicalAttackBatchSharedCriticalProgress` before changing the action calculation. For two and 13 physical slots, compare the result to one action-level `LegacyDoubleCriticalMath.Resolve`: flags apply consistently to every later physical target and progress advances once, not once per target. Confirm a client-supplied `0xFF` flag is not used.
- [x] Write `WorldPhysicalAttackBatchPassiveReservesSlotOne`: force the class-3/`0x200000` 1-in-4 proc, assert slot 0 includes the secondary damage, output slot 1 has target ID zero and secondary damage, and the original slot-1 target HP is unchanged.
- [x] Run Engine.Tests and confirm these behavioral assertions fail before implementation.
- [x] Implement one `LegacyPhysicalAttackBatchActionState` for the entire batch. Calculate base critical/progress only when original slot 0 reaches the physical branch; reuse its flags afterward. Apply the 7.69 passive only at slot 0 and reserve original slot 1 when it procs.
- [x] Run Engine.Tests; expect shared-state and passive-slot cases plus prior physical combat regressions to pass.

### Task 4: Defer death effects until normal completion and preserve abort semantics

**Files:** modify `WorldHub.cs`, batch result types, Engine.Tests `Program.cs`.

- [x] Write `WorldPhysicalAttackBatchDeathFinalizesAfterNormalCompletion`: a lethal first target followed by another valid slot is finalized once at normal completion, with removal and existing NPC XP/drop/Pista effects attached to that target's result.
- [x] Write `WorldPhysicalAttackBatchOutOfRangeKeepsPartialMutation`: slot 0 mutates/kills an NPC and slot 1 is out of range. Assert no rollback, slot 1 is the abort index, later slots are skipped, the dead NPC is still registered at HP zero, and no deferred removal/reward occurs. Include a ManaControl target case to confirm its immediate resource mutation remains in the partial outcome.
- [x] Write `WorldPhysicalAttackBatchAbortDoesNotConsumeResurrection`: an earlier lethal player hit with a resurrection item followed by an out-of-range slot leaves the item unconsumed and the player death unfinalized.
- [x] Run Engine.Tests and confirm abort/resurrection assertions fail before changing death handling. The normal-path death case is a compatibility regression because its output is intentionally observationally unchanged.
- [x] Queue NPC/summon/player death work during slot processing. Only after the last slot completes normally, finalize each queued death once, including the existing NPC rewards and resurrection-scroll behavior. On `OutOfRange`, return partial outcomes without rollback or death finalization.
- [x] Run Engine.Tests; expect normal death, partial abort, immediate ManaControl, resurrection, and legacy death/reward tests to pass.

### Task 5: Route the host through one aggregate confirmation

**Files:** modify `Server/src/WydCdk.Server/Program.cs`, Engine.Tests `Program.cs`, and `AttackMultiTargetNetwork.cs`.

- [x] Extend the attack pipeline test so a two-slot physical packet produces one confirmation populated at original indexes, with aggregate final attacker resources, action `DoubleCritical`, and `Progress`.
- [x] Run the loopback smoke from Task 1 again and confirm it still fails until the host routes the collected physical slots into `TryApplyPhysicalAttackBatch`; RED evidence: `Backup/Build/AutoTradeCloseSmoke/e3572fac1d82473189e079d7ba966df0`.
- [x] Change host routing to collect all `Damage == -2` slots in ascending wire index only when `SkillIndex == -1`, call WorldHub once, and preserve the frame's original slot count for the class-3 passive decision. A review regression confirms that a valid skill remains on the skill path when a later slot carries an invalid physical marker.
- [x] Fill one `AttackConfirmation` from the aggregate state. On normal completion, write/broadcast that frame once, then send deferred death frames. On abort, send captured immediate per-slot updates after releasing the lock, suppress the attack relay and deferred death frames, and do not process later slots.
- [x] Run Engine.Tests and the loopback smoke. GREEN debug evidence `93da451534ec4f3499b2bb028ff163c0`; final self-contained evidence `2e6a52a615684949afb4f34f0403c9f4`.

### Task 6: Verify the slice, document evidence, and publish the sandbox runtime

**Files:** update `Server/docs/PORT_TRACEABILITY.md`, `Server/docs/CLIENT_E2E_SMOKE.md`, root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md`.

- [x] Record `dotnet --info`; run the full Engine.Tests suite and the full solution build using CLI only. Final after review fixes: SDK 10.0.401; 321/321; 0 warnings/0 errors. Logs: `.superpowers/sdd/2026-10-01-msg-attack-multi-target/final-verification`.
- [x] Run the guarded TCP smoke on `127.0.0.1:18283` with new fixture copies. RED/GREEN evidence, fixture hashes, exit codes, and port-release result are recorded in `CLIENT_E2E_SMOKE.md`; client visual E2E remains pending.
- [x] Update traceability with 7.69/W2PP comparison, 1/2/13 coverage, shared action state, passive slot reservation, death/abort behavior, retained authoritative-position range difference, and the review regressions for ManaControl, summon mount HP, and mixed skill routing.
- [x] Update project progress/backlog and the existing AGENTS context in place; unrelated Site, client, and legacy port gaps remain outside this slice.
- [x] Publish only the corrected runtime artifacts to the already-authorized test sandbox. Final package SHA-256 `5B3260C1321C1F4D185488273D3F5660E023FBA1FDA7F255ECDAC2A8A87CAFAD`; backup `backup-combat-attack-multitarget-reviewfix-20261001-5b3260c1` and all 197 runtime hashes verified; UP/PVP Running and listeners 8281/8282 confirmed; temporary staging removed; no source/docs/secrets transferred and no Site/production touched. The earlier backup remains preserved.
- [x] Leave all changes unstaged and uncommitted; no push.

Commands from `Server`:

```powershell
dotnet --info
dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore -p:UseSharedCompilation=false
dotnet build .\WydCdk.Engine.slnx --no-restore --nologo -v:minimal -m:1 -p:UseSharedCompilation=false
```

## Plan Self-Review

- **Spec coverage:** indexed 1/2/13 slots and wire order: Task 2; shared `DoubleCritical`/`Progress` and passive reservation: Task 3; no-rollback range abort, deferred NPC/summon/player death, and immediate effects: Task 4; one host confirmation and no network I/O under the WorldHub lock: Task 5; TCP loopback, documentation, full build/tests, and sandbox publication: Tasks 1, 5, and 6.
- **TDD order:** Task 1 records a runtime RED before production changes. Each later behavior test is added and run before the corresponding WorldHub/host behavior is implemented.
- **Type consistency:** one request has indexed slots and action-level inputs; each slot outcome carries `LegacyPhysicalAttackResult` plus optional `LegacyPhysicalAttackOutcome`; the aggregate owns abort index and action state. The single-target wrapper maps the first slot back to its existing API.
- **Review focus:** all five input/failure classes above map to explicit Engine.Tests or TCP smoke cases; no checklist item is left without an owner.
- **Proportion:** six tasks cover one behavior across WorldHub, host, and its required verification/documentation without expanding to other attack gates or client E2E.
