# MSG_Attack Mixed Physical and Skill Slots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Process physical (`Damage == -2`) and skill (`Damage == -1`) targets from one `MSG_Attack` in original slot order under one `WorldHub` lock and emit one aggregate attack response.

**Architecture:** Introduce a typed action batch in `WydCdk.World` and use lock-held physical and skill helpers so the batch shares action state without nesting locks. Preserve the existing single-branch APIs as one-slot wrappers; have the Host classify markers by slot, collect results under the world operation, then build/send the aggregate and secondary frames after the lock is released.

**Tech Stack:** C# / .NET 10, existing console-based `WydCdk.Engine.Tests`, `WydCdk.Client.E2E` TCP harness, PowerShell smoke runner.

**Spec:** `docs/superpowers/specs/2026-10-03-msg-attack-mixed-slot-design.md`

## Global Constraints

- Compare behavior against TMProject 7.69 and `Server/W2PP`; the approved spec is the implementation boundary.
- Preserve 1/2/13-slot wire layouts and original indexes; never use client damage as authoritative damage.
- Keep one `WorldHub` lock for ordered slot mutation and death finalization; perform all socket I/O after releasing it.
- Preserve partial mutations on a physical out-of-range abort; skip later slots, suppress final attack confirmation, and do not finalize deferred deaths.
- Add no new skill effects, formulas, opcodes, or client changes; preserve current skill and physical gates and the type28-before-type27 order.
- Preserve the dirty checkout and all unrelated work. No reset, staging, commit, push, Site or production change. Sandbox runtime publication is the final step and uses only the already-authorized test destination after local gates pass.
- Run builds through CLI with `UseSharedCompilation=false`; record `dotnet --info` and SDK 10.0.401 before final verification.

## Review Focus

- A skill marker without a valid skill definition and a physical marker at any slot in a skill packet must not be misclassified: test the no-mutation domain contract in Task 1 and marker classification over TCP in Task 5.
- A slot-0 skill followed by physical damage must not resolve `DoubleCritical/Progress`; a slot-0 physical attack must share that state with later physical slots: test both orderings in Task 4.
- A class-3 passive reservation of wire slot 1 must replace the original slot-1 marker and prevent its target mutation: test in Task 4.
- A later physical out-of-range result must retain earlier mutations while suppressing confirmation and deferred death work: test state behavior in Task 4 and aggregate-response behavior in Task 5 where the guarded harness can stage the abort case.
- Skill and physical deaths can touch NPC rewards, summons, player resurrection, and captured relays; test normal completion and abort behavior in Task 4, with relay order covered in Task 5.

---

### Task 1: Add the typed batch contract and request validation

**Files:**
- Create: `src/WydCdk.World/LegacyAttackActionBatch.cs`
- Create: `tests/WydCdk.Engine.Tests/AttackMixedSlotTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `src/WydCdk.World/WorldHub.cs` (batch API surface only)

**Interfaces:**
- Produces `LegacyAttackActionSlotKind { Physical, Skill }`.
- Produces `LegacySkillAttackBatchSlotRolls` with deterministic overrides corresponding to the existing `TryApplySkillAttack` roll parameters.
- Produces `LegacyAttackActionBatchSlotRequest(int SlotIndex, LegacyAttackActionSlotKind Kind, int TargetConnectionId, LegacyPhysicalAttackBatchSlotRolls? PhysicalRolls = null, LegacySkillAttackBatchSlotRolls? SkillRolls = null, int? SkillTargetIndex = null)`; null means use the original wire `SlotIndex`, while the optional override preserves legacy single-skill callers that pass `targetIndex=-1` or another non-wire value.
- Produces `LegacyAttackActionBatchRequest(int AttackerConnectionId, IReadOnlyList<LegacyAttackActionBatchSlotRequest> Slots, LegacySkillDefinition? Skill = null, LegacyItemDataTable? ItemData = null, int Weather = 0, ushort ClientProgress = 0, bool SupportsSecondaryDamageSlot = false, LegacyPhysicalAttackBatchActionRolls? PhysicalActionRolls = null, uint ClientTick = ClientTickPolicy.SkipCheckTick)`.
- Produces an indexed `LegacyAttackActionBatchSlotOutcome` with kind, physical result/outcome or skill result/outcome; aggregate `LegacyAttackActionBatchOutcome` with slot outcomes, abort index, DoubleCritical, NextProgress, attacker snapshot, completion flag, and wire-slot overrides.
- Produces `WorldHub.TryApplyAttackActionBatch(LegacyAttackActionBatchRequest request, out LegacyAttackActionBatchOutcome? outcome)`; initially retain only argument validation and an unavailable result so behavioral assertions compile and fail.

- [x] Add `WorldHubExposesTypedAttackActionBatch`, using reflection so the test compiles before the new API exists; assert that the generic batch method and typed request/result contract are present.
- [x] Register the test in Engine.Tests and run the suite; expected RED is the assertion that `WorldHub` lacks the requested batch contract.
- [x] Add the request/result types and a minimal batch surface that validates a non-null request with 1–13 unique, ascending slot indexes in 0–12, requires a skill definition for skill slots, and returns unavailable for a valid request.
- [x] Add `AttackActionBatchRejectsInvalidShapeWithoutMutation` for empty, over-13, duplicate, descending, and out-of-range indexes plus a skill slot without a definition; run Engine.Tests and expect all contract/validation tests to pass.

Run from `Server`:

```powershell
dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

### Task 2: Route existing physical batches through the unified operation

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `src/WydCdk.World/LegacyPhysicalAttackBatch.cs`
- Modify: `tests/WydCdk.Engine.Tests/AttackMixedSlotTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Consumes the Task 1 batch contract.
- Produces a physical-only implementation of `TryApplyAttackActionBatch` using the existing `TryApplyPhysicalAttackLocked` helper and `LegacyPhysicalAttackBatchActionState`.
- Keeps `TryApplyPhysicalAttackBatch(LegacyPhysicalAttackBatchRequest, out LegacyPhysicalAttackBatchOutcome?)` as an adapter that maps generic results to the existing physical result types.
- Keeps `TryApplyPhysicalAttack(...)` as the existing one-slot compatibility wrapper and preserves its roll arguments and return behavior.

- [x] Write `UnifiedPhysicalBatchMatchesLegacyBatchAndSingleSlotWrapper` to compare indexed outcomes, action state, abort index, wire overrides, and target snapshots through both APIs, plus `PhysicalBatchPreservesOriginalIndexesAndCardinality` for 1, 2, and 13 physical slots and repeated-target mutation order.
- [x] Run Engine.Tests and confirm the new generic-path assertion fails before adapting the implementation.
- [x] Implement the physical branch in the generic operation under one `lock (gate)`; keep range-abort, passive slot reservation, shared DoubleCritical/Progress, and normal-only physical death finalization unchanged.
- [x] Adapt the physical public batch and single-slot wrappers without changing their signatures.
- [x] Run Engine.Tests; expect the unified physical parity case and all existing physical batch regressions to pass.

### Task 3: Extract the skill lock-held path and preserve single-slot skill behavior

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `tests/WydCdk.Engine.Tests/AttackMixedSlotTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/AttackPartySkillGateTests.cs`

**Interfaces:**
- Consumes the Task 1/2 batch request and result types.
- Produces a private `TryApplySkillAttackLocked(...)` helper that receives already validated skill inputs and never opens a `lock` or performs network I/O.
- Adapts public `TryApplySkillAttack(...)` to a one-slot skill batch while preserving its signature, return enum, nullable outcome, random overrides, target index, and client tick.
- Generic skill slot results retain the current `LegacySkillAttackResult` and `LegacySkillAttackOutcome` values for Host consumption.

- [x] Add `GenericSingleSlotSkillBatchMatchesPublicWrapper` covering accepted heal, unsupported effect, MaxTarget/type28, party/type27, and target rejection outcomes; compare generic batch values with the existing public wrapper.
- [x] Run Engine.Tests and confirm the new generic skill batch assertion fails while the public wrapper still establishes its compatibility baseline.
- [x] Extract the existing skill body into the lock-held helper; preserve validation, gates, calculations, and immediate non-death effects without broadening skill support.
- [x] Make `TryApplySkillAttack` call the one-slot generic operation and map its single outcome back to the existing API.
- [x] Run Engine.Tests; expect all skill wrapper and existing skill gate tests to pass with unchanged outcomes.

### Task 4: Execute mixed slots in order and defer skill death finalization

**Files:**
- Modify: `src/WydCdk.World/LegacyAttackActionBatch.cs`
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `tests/WydCdk.Engine.Tests/AttackMixedSlotTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Consumes lock-held physical/skill helpers from Tasks 2–3.
- Produces the full `TryApplyAttackActionBatch` behavior for either slot kind in a single ordered loop and lock; one action state carries DoubleCritical, Progress, and passive slot 1 reservation while preserving existing per-target ManaControl semantics.
- Result preserves each original slot index and kind and contains all per-slot outcome/effect data required by the Host; normal completion finalizes queued skill and physical deaths exactly once.

- [x] Write failing tests `SkillThenPhysicalDoesNotResolveSlotZeroCritical`, `PhysicalThenSkillSharesActionState`, `PassiveSlotReservationSkipsOriginalSkillSlotOne`, `MixedType28PrecedesPartyType27`, `UnsupportedSkillMarkerDoesNotMutate`, and `CrackErrorRemainsPerSlot`.
- [x] Write failing tests `MixedSkillNpcDeathFinalizesOnceAfterNormalCompletion`, `MixedPlayerResurrectionFinalizesOnlyAfterNormalCompletion`, and `PhysicalOutOfRangeAbortDefersEarlierSkillAndPhysicalDeaths`; assert one-time NPC removal/rewards, normal-only resurrection-scroll consumption, retained partial mutations, skipped later slots, and `CompletedNormally == false` after abort.
- [x] Run Engine.Tests and confirm each new assertion fails against the incomplete generic loop/death handling.
- [x] Implement ordered per-slot dispatch; only resolve physical DoubleCritical/Progress when original index 0 enters the physical helper. If that helper reserves wire index 1, emit its override and skip the original request at index 1.
- [x] Queue skill death consequences while processing slots and finalize them once only after a normal batch; on physical `OutOfRange`, retain immediate state mutations but skip later slots and all queued death work.
- [x] Run Engine.Tests; expect mixed ordering, gates, unsupported-skill no-op, per-slot CrackError, normal death, abort, resurrection item, and existing standalone physical/skill regressions to pass.

### Task 5: Classify Host slots and relay one aggregate confirmation

**Files:**
- Modify: `src/WydCdk.Server/Program.cs`
- Create: `tests/WydCdk.Client.E2E/AttackMixedSlotsNetwork.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/AutoTradeCloseHarnessTests.cs`
- Modify: `tools/PortAudit/Test-AutoTradeClose.ps1`

**Interfaces:**
- Consumes `WorldHub.TryApplyAttackActionBatch(...)` and indexed outcomes from Task 4.
- Adds E2E option `--attack-mixed-slots-smoke`, registered as `AttackMixedSlotsSmoke`, and a runner mode `AttackMixedSlots`.
- Produces one `AttackConfirmation` from the original request frame and final attacker snapshot, writes each processed result at its original index, applies passive wire overrides, and emits captured secondary/death frames only after sending the aggregate response and after `WorldHub` has released its lock.

- [x] Add guarded loopback coverage using isolated actor/peer account copies: build 13-slot packets with physical `-2`/skill `-1` in both orders, assert one identical attack confirmation per participant, unchanged slot alignment, authoritative final HP/MP/ReqMp and Progress, and effect frames after the attack relay. Include an invalid `SkillIndex` packet and a deterministic physical out-of-range abort after an earlier skill mutation, using a third isolated character moved by an accepted loopback `MSG_Action`.
- [x] Add harness self-tests for requiring distinct isolated fixtures, `127.0.0.1`, port `18283`, and rejection when combined with another attack mode.
- [x] Run the new smoke on the pre-change local runtime and capture RED: the mixed action failed because the Host dropped physical slot `-2` in a skill-indexed packet. Evidence is retained under `Backup/Build/AutoTradeCloseSmoke/3f215dcc4e6247f09a6b6aa52cd1c4f6`.
- [x] Replace global `SkillIndex` branch selection with per-slot `-2` physical / valid `-1` skill classification; consume action-level mana/gates once, call the WorldHub batch once, and remove the separate per-branch world calls.
- [x] Build one response for normal completion; on physical out-of-range abort suppress it while relaying only already-captured immediate effects. Send the aggregate attack frame before other captured frames, keep network I/O outside the world lock, and order deferred death/XP/drop relays by original slot.
- [x] Run Engine.Tests (345/345), harness self-tests, and the guarded loopback smoke; assert both slot orders, one identical response per participant, one response suppression on abort, and exact Host outcome order. Final Debug evidence: `Backup/Build/AutoTradeCloseSmoke/d5c1724f8530453ab168163480dabbfa` (exit 0, original hashes intact, cleanup complete, port 18283 released).

### Task 6: Complete local verification, document evidence, and publish sandbox runtime

**Files:**
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `docs/CLIENT_E2E_SMOKE.md`
- Modify: `../AGENTS.md`
- Modify: `../PROGRESSO.md`
- Modify: `../BACKLOG.md`

**Interfaces:**
- Consumes the final implementation and test evidence from Tasks 1–5.
- Produces an in-place record of 7.69/W2PP/client-producer comparison, the mixed markers and dormant `Range == -1` base-data caveat, the one-lock/one-confirmation contract, test outcomes, and remaining non-goals.

- [x] Record `dotnet --info`; run full Engine.Tests and full solution build from `Server` (SDK 10.0.401; 345/345 tests; build 0 warnings/0 errors; logs in `.superpowers/sdd/2026-10-03-msg-attack-mixed-slot/task-6-*`).

```powershell
dotnet --info
dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore -p:UseSharedCompilation=false
dotnet build .\WydCdk.Engine.slnx --no-restore --nologo -v:minimal -m:1 -p:UseSharedCompilation=false
```

- [x] Run the guarded TCP smoke on `127.0.0.1:18283` in Debug and self-contained modes using fresh fixture copies; both manifests passed with exit 0, assertions/cleanup true, original fixture hashes intact, and the port freed.
- [x] Update module traceability and smoke evidence, then update the root checklist/backlog/context in place; visual client E2E, other unported skills, Site, and production remain outside this slice.
- [x] Re-review the final diff and current checkout status: `git diff --check` passed, the index is clean, and no Git mutation was made.
- [x] After every local gate passed, publish only the self-contained runtime artifacts to the previously authorized test sandbox. Readback confirmed 197 runtime hashes and the preserved previous-runtime backup; UP/PVP are `Running`, ports 8281/8282 listen in the approved executable, and temporary staging was removed. No source, docs, secrets, or account fixtures were transferred.

## Plan Self-Review

- **Spec coverage:** slot kind/index/cardinality and one/two/thirteen-slot order are covered in Tasks 1–2; physical and skill compatibility wrappers in Tasks 2–3; marker classification, shared action state, passive slot override, gates, unsupported effects, and per-slot errors in Task 4; one aggregate Host relay and post-lock I/O in Task 5; source/test evidence, local verification, docs, and sandbox boundary in Task 6.
- **TDD order:** each behavior is first pinned by a failing domain or TCP assertion before its corresponding implementation; Task 1 adds only API scaffolding so tests can compile and fail on behavior.
- **Type consistency:** generic request uses the existing physical roll/action-roll records plus a skill-specific roll record; outcomes preserve typed existing physical and skill result enums; both legacy public APIs map exactly one slot to their existing return contracts.
- **Review focus:** all five failure/input groups map to the explicitly named domain or TCP tests in the owning tasks.
- **Proportion:** six tasks cover the single WorldHub/Host behavior and its required headless validation without extending to other opcodes or visual client E2E.
