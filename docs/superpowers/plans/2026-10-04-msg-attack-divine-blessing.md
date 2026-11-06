# COMBAT-ATTACK-DIVINE-BLESSING-001 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port TMProject 7.69 skill 201 Bênção Divina as an authoritative self-heal sub-action inside the ordered `MSG_Attack` WorldHub batch.

**Architecture:** Read `MOBEXTRA.SecLearnedSkill` from the preserved raw account slot through a measured x86 adapter. Represent missing table row 201 with a code-only zero descriptor that mirrors the 7.69 zero-initialized entry, then process its server-created skill 29 action, mana debit, HP mutation, and relay capture inside the existing WorldHub batch lock. The Host builds and sends captured child frames before the parent confirmation, with all socket I/O outside the lock.

**Tech Stack:** C# / .NET 10, `WydCdk.Engine.Tests` console suite, `WydCdk.Client.E2E` TCP harness, PowerShell loopback runner, existing Win32 ABI probe.

**Spec:** `docs/superpowers/specs/2026-10-04-msg-attack-divine-blessing-design.md`

## Global Constraints

- TMProject 7.69 is the behavior authority; compare the touched branch with `Server/W2PP` and record that W2PP has no active Bênção Divina branch.
- Do not add skill 201 to `SkillData.csv`; its code-only descriptor has `Id=201` and otherwise zero fields, matching `g_pSpell[201]` after `memset`.
- The 7945-byte account and 552-byte `MOBEXTRA` slot remain raw; change only the measured 4-byte `SecLearnedSkill` read path and never trust a packet field for the bit or MP.
- For the effective TMProject 7.69 TMSrv ABI, probe `Servidor\Source\Code\Basedef.h`: the TMSrv project resolves `ProcessClientMessage.h` to this parent header. Do not probe the adjacent `TMSrv\Basedef.h` as authoritative; it omits `SecLearnedSkill` and is not the header used by that include chain.
- The parent 201 action has zero mana cost. Its synthetic skill 29 child uses `Special=0`, the existing `SaveMana` formula, and authoritative `CurrentMp`.
- Keep DivineBlessing/RateRegen ephemeral and local to the child action; never store them on the participant or carry them between slots. All child return paths, including insufficient mana, Clan 4, dead target, and map predicate, end that local context before the next slot.
- Preserve 1/2/13-slot wire layouts, original indexes, type28 excess-slot gate, partial mutations and child relays on later abort; suppress the parent confirmation and deferred finalization after abort.
- Match the 7.69 DivineBlessing mount divisors: item 786 uses `max(2,sanc)`, 1936 uses `max(2,sanc)*10`, and 1937 uses `max(2,sanc)*1000`. Do not reuse the current generic heal's divergent mount factors.
- Capture child state and visible-view recipients under the WorldHub lock. Preserve secondary outcomes in parent-slot order; serialize/send each `MSG_SetHpMp` correction or child `MSG_AttackOne` in that order, then send the parent frame, all after releasing the lock.
- Tests use copied loopback fixtures and restore/verify originals in `finally`; no WYD.exe visual E2E, Site, production, commit, or push.
- Run the server through CLI/headless with `UseSharedCompilation=false`; record `dotnet --info` and the SDK used. The smoke runner must remain restricted to `127.0.0.1:18283` and distinct isolated account copies.

## Review Focus

- A wrong `MOBEXTRA` offset, width, or adjacent-bit interpretation could enable the heal for another secondary skill; pin the measured ABI and exact `0x2` mask in Task 1.
- Skill 201 has no CSV row but remains an accepted server action with no class/primary-skill requirement and zero parent cost; pin its table absence, gate behavior, and MP snapshot in Task 2.
- The synthetic `CurrentMp=-2` marker must not bypass skill 29's `Special=0`/`SaveMana` cost; pin accepted and insufficient-mana paths, including correction-before-parent order, in Tasks 3–4.
- HP 0 and negative HP follow different source paths; mount integer division, Clan 4, self-map attribute equality, and stale transient state can all produce plausible but wrong results; pin them in Task 3.
- A child may heal and relay before a later physical slot aborts the parent batch; pin child persistence/relay and parent suppression in Tasks 3–5.

---

### Task 1: Measure and expose the secondary-skill ABI field

**Files:**
- Modify: `tools/PortAudit/StructLayoutProbe.cpp`
- Modify: `tools/PortAudit/Measure-StructLayouts.ps1`
- Modify: `src/WydCdk.World/LegacyAccountSnapshot.cs`
- Create: `src/WydCdk.World/LegacyMobExtra.cs`
- Modify: `src/WydCdk.World/LegacyMobCombatState.cs`
- Modify: `src/WydCdk.World/WorldHub.cs:3368-3398`
- Create: `tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Produces `LegacyMobExtra.TryReadSecondaryLearnedSkill(ReadOnlySpan<byte> mobExtra, out uint mask)` and `TrySetDivineBlessingLearnedBit(Span<byte> mobExtra, bool enabled)`, returning `false` for a truncated slot. The write helper changes only bit `0x2` and preserves adjacent bits; production uses the read path only, while isolated smoke fixtures use the write path.
- Produces `LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset`, set to the measured field offset only after the Win32 probe confirms it; field width is four bytes.
- Adds `uint SecondaryLearnedSkill { get; init; }` to `LegacyMobCombatState`; `WorldHub.TryGetCombatState` populates it from the participant's raw `MobExtra` under `gate`.

- [x] **Step 1: Write `SecondarySkillAdapterUsesMeasuredOffsetAndReadsOnlyBit2`, `SecondarySkillAdapterWritesOnlyBit2`, and `CombatStateReadsSecondarySkillFromAccountExtra`.** Assert 552-byte fixture size, `0x2` detection, adjacent bits `0x1`/`0x4` do not satisfy the bit check, enabling/disabling preserves adjacent bits and all other bytes, and short input fails closed. Register the tests in `Program.cs`.
- [x] **Step 2: Run Engine.Tests and confirm RED.** Run from `Server`:

```powershell
dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Expected: compile/test failure because `LegacyMobExtra` and `SecondaryLearnedSkill` do not exist.

- [x] **Step 3: Extend the Win32 probe.** Print `sizeof(MOBEXTRA)`, `offsetof(MOBEXTRA, SecLearnedSkill)`, and field width for the effective 7.69 server common header (used by TMSrv and DBSrv) and W2PP; use the appropriate type spelling per profile. Point the existing TMSrv profile at the parent `Servidor\Source\Code` include directory, not the stale adjacent `TMSrv\Basedef.h`. Make `Measure-StructLayouts.ps1` fail if the two effective server ABIs disagree or if the field width is not four bytes.
- [x] **Step 4: Run `pwsh -NoProfile -File .\tools\PortAudit\Measure-StructLayouts.ps1`.** Expected: effective 7.69 and W2PP server headers both compile as Win32 and report the same `SecLearnedSkill` offset/width; `sizeof(MOBEXTRA)` is 552. If they disagree, stop before adding the C# offset and record the ABI divergence for review.
- [x] **Step 5: Implement the adapter and state projection.** Set `MobExtraSecondaryLearnedSkillOffset` to the measured value; read/write the field as unsigned 32-bit little-endian only in the adapter; leave raw bytes and all unrelated `MOBEXTRA` fields untouched.
- [x] **Step 6: Run the targeted tests and then Engine.Tests.** The console runner executes the new tests as part of the suite. Expected: all three new tests PASS and the full suite has no regressions.

### Task 2: Admit skill 201 without inventing a CSV row or charging parent mana

**Files:**
- Create: `src/WydCdk.World/LegacyDivineBlessing.cs`
- Modify: `src/WydCdk.World/LegacySkillAttackGate.cs`
- Modify: `src/WydCdk.Server/Program.cs:2482-2624`
- Modify: `src/WydCdk.World/LegacyAttackActionBatch.cs`
- Modify: `tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Produces `LegacyDivineBlessing.SkillId = 201`, `HealSkillId = 29`, and a code-only `ParentActionDefinition` whose other spell fields are zero. It is not inserted into `LegacySkillDataTable` or CSV.
- `LegacySkillAttackGate.Evaluate(...)` accepts 201 even when `table[201]` is null, while retaining the common 700 ms timing gate and ordinary preceding `MSG_Attack` gates.
- Extends `LegacyAttackActionBatchRequest` with optional `DivineBlessingHealSkill`; for parent 201, Host supplies the code-only zero descriptor as `Skill` and the real table definition `skills[29]` as the child definition.

- [x] **Step 1: Add `Skill201MissingCsvDefinitionPassesTheSkillGate` and `Skill201UsesZeroCostParentDescriptor`.** Assert `skills[201]` remains null, the gate accepts 201 without a primary learned bit/class-book check, normal timing still rejects a too-short tick, parent mana remains unchanged, and the zero descriptor is not registered in the table.
- [x] **Step 2: Run Engine.Tests and confirm RED.** Use the Engine.Tests command from Task 1. Expected: the skill gate returns `MissingSkillDefinition` and the code-only descriptor type is absent.
- [x] **Step 3: Implement the code-only action descriptor and gate exception.** Keep index bounds, Passive/timing ordering, and the regular gates for all other skills unchanged; do not change `MAX_SKILLINDEX` or populate CSV row 201.
- [x] **Step 4: Route Host skill 201 as a zero-cost parent.** Resolve `skillDefinition` to the code-only descriptor only for ID 201, pass the real skill 29 definition separately in the batch request, and run the ordinary authoritative mana path against the zero-cost parent so `SetReqMp` semantics remain intact. Never read request `CurrentMp` as state.
- [x] **Step 5: Run the two new tests and Engine.Tests.** Expected: 201 passes the gate with zero parent debit, missing/other skill definitions keep their existing behavior, and regressions pass.

### Task 3: Process the skill 29 child inside the WorldHub batch lock

**Files:**
- Modify: `src/WydCdk.World/LegacySkillCombatMath.cs`
- Modify: `src/WydCdk.World/LegacyAttackActionBatch.cs`
- Modify: `src/WydCdk.World/WorldHub.cs:3888-4053, 4786-5161`
- Modify: `tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Produces `LegacyAttackActionSecondaryOutcome` with parent slot index, target/position, authoritative mob snapshot, signed heal slot damage, mana correction fields when needed, and recipient connection IDs captured in the actor's 33x33 view under `gate`. Append each outcome as it occurs while processing slots so Host preserves child/correction order.
- Defines `LegacyAttackActionSecondaryOutcome` as `(ParentSlotIndex, Kind, MobSnapshot, RequestedHp, RequestedMp, PositionX, PositionY, WireTargetId, WireDamage, RecipientConnectionIds)`, where `Kind` is `SetHpMp` or `AttackOne`; `AttackOne` carries the exact source slot values, including zeroed slots for invalid/dead targets.
- Adds `SecondaryActions` to `LegacyAttackActionBatchOutcome`; outcomes are retained even when a later physical slot aborts the parent batch.
- Produces `WorldHub.TryConsumeSkillManaLocked(Participant participant, LegacySkillDefinition skill, int? specialOverride, out LegacyMobCombatState? state, out byte[]? mobSnapshot, out int requestedMana)`; the public skill-mana API delegates to it while the batch calls it with `specialOverride: 0`.
- Produces `LegacySkillCombatMath.GetManaSpent(LegacySkillDefinition skill, LegacyMobCombatState state, int special)` and `LegacyDivineBlessing.CalculateHeal(int currentHp, int maxHp, int mountIndex, int mountSanction) -> LegacyDivineBlessingHealResult(CurrentHp, WireDamage)`.
- `WireDamage` is `-RateRegen` before sanction division, because 7.69 mutates HP using divided damage but writes the original signed `dam` to the slot at `_MSG_Attack.cpp:1761`.

- [x] **Step 1: Add RED tests for authoritative bit, exact mana, HP formula, and batch behavior:** `DivineBlessingRequiresMobExtraBit2`, `DivineBlessingUsesSkill29SpecialZeroAndSaveMana`, `DivineBlessingInsufficientManaCapturesSetHpMpWithoutHealing`, `DivineBlessingUses769MountDivisorsAndHpCap`, `DivineBlessingClanAndSelfMapPredicatesMatch769`, `DivineBlessingZeroAndNegativeHpFollowDifferentGates`, `DivineBlessingSelfHealDoesNotAddFakeOrPersistedExp`, and `DivineBlessingChildSurvivesLaterBatchAbort`. Registered all tests.
- [x] **Step 2: Run the new tests and confirm RED.** Engine.Tests → `RESULT tests=362 passed=354 failed=8`; eight Divine Blessing assertions failed while the existing 354 cases remained green.
- [x] **Step 3: Add the explicit Special override and lock-held debit.** Kept public `TryConsumeSkillMana` semantics unchanged for client skills; shared its MP/ReqMp mutation logic with the nested call under `gate`.
- [x] **Step 4: Add the skill-201 parent branch after existing slot/target validation.** Read bit `0x2` from authoritative raw `participant.MobExtra` while locked. If unset, no child is produced. If set, process the server-created skill-29 self-target with `Special=0`, skip-tick semantics, and the server-owned `CurrentMp=-2` origin; no packet field can supply that marker.
- [x] **Step 5: Apply the special Type 6 path.** Applied `RateRegen=MaxHp-Hp` and the 7.69 mount divisors directly, with Clan 4, the self-map predicate, HP cap, dead-target-after-mana behavior, no self FakeExp/MOB.Exp mutation, local-only transient state, and signed `-RateRegen` wire damage.
- [x] **Step 6: Capture secondary outcomes before leaving the lock.** Insufficient mana captures SetHpMp state; other child outcomes capture AttackOne slots and view recipients. Outcomes preserve parent-slot order and survive a later abort; Clan 4 preserves `(actorId,-1)`, while negative HP yields `(0,0)` after mana debit. No socket I/O occurs under the lock.
- [x] **Step 7: Run all Task 3 tests and Engine.Tests.** The full suite passed: `RESULT tests=362 passed=362 failed=0`.

### Task 4: Serialize and send child responses before the parent

**Files:**
- Modify: `src/WydCdk.Protocol/AttackConfirmation.cs`
- Modify: `src/WydCdk.Server/Program.cs:2560-2705`
- Modify: `src/WydCdk.World/WorldHub.cs` (captured-recipient broadcast API)
- Modify: `tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Produces `AttackConfirmation.TryCreateSyntheticOneTarget(ReadOnlySpan<byte> mob, ushort actorId, short targetX, short targetY, ushort wireTargetId, int wireDamage, out AttackConfirmation? confirmation)` for a 68-byte `MSG_AttackOne`; its wire skill is 29, motion is 255, FakeExp is zero, and `CurrentMp`/narrowed `ReqMp` come from WorldHub.
- Produces `WorldHub.BroadcastToParticipantsAsync(IReadOnlyList<int> recipientIds, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)`; it resolves callbacks for the captured IDs under a short lock and sends after releasing it. The captured ID list includes the actor once, matching `GridMulticast(..., skip=0)`.

- [x] **Step 1: Add protocol tests `SyntheticDivineBlessingRelayUsesAttackOneLayout` and `SyntheticDivineBlessingRelayUsesAuthoritativeResources`.** Tests assert type `0x039D`, size 68, target ID and signed wire damage, skill 29, motion 255, zero FakeExp, authoritative HP/MP/EXP, and signed-short ReqMp narrowing.
- [x] **Step 2: Run the protocol tests and confirm RED.** The missing factory produced the two intended failures; the other 362 tests passed.
- [x] **Step 3: Implement the synthetic confirmation factory.** Builds a fresh one-target payload and does not copy parent slots, packet MP/damage, DoubleCritical, or FakeExp.
- [x] **Step 4: Relay captured child outcomes in Host.** Host walks ordered `SecondaryActions` after the batch lock returns, sends SetHpMp directly to the actor or AttackOne to captured recipients, then sends the parent only after normal completion. No socket I/O occurs under the WorldHub action lock.
- [x] **Step 5: Keep abort semantics explicit.** Child frames are relayed before the normal/abort branch; abort suppresses aggregate parent/deferred work. Invalid child serialization fails closed before parent confirmation.
- [x] **Step 6: Run protocol tests, Task 3 tests, and Engine.Tests.** The full suite passed: `RESULT tests=364 passed=364 failed=0`.

### Task 5: Add guarded TCP loopback coverage, self-contained validation, and handoff evidence

**Files:**
- Create: `tests/WydCdk.Client.E2E/AttackDivineBlessingNetwork.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/AutoTradeCloseHarnessTests.cs`
- Modify: `tools/PortAudit/Test-AutoTradeClose.ps1`
- Modify: `docs/CLIENT_E2E_SMOKE.md`
- Modify: `../PROGRESSO.md`
- Modify: `BACKLOG.md` only if this slice changes an existing acceptance status

**Interfaces:**
- Adds `--attack-divine-blessing-smoke`, routed to `AttackDivineBlessingNetwork.RunAsync`.
- Adds the runner switch `-AttackDivineBlessing`; it seeds `SecLearnedSkill & 0x2` only in isolated copies, requires `127.0.0.1:18283`, validates distinct actor/observer copies, stores logs/evidence/hashes, and restores originals in `finally`.

- [ ] **Step 1: Add E2E option/harness self-tests.** Assert DivineBlessing mode rejects non-loopback host/port, identical actor and observer paths, and original fixture paths; assert its finally cleanup restores the exact source fixture hashes.
- [ ] **Step 2: Add RED network assertions.** The loopback smoke logs in an actor and visible observer, sends skill 201 with a legal tick and target marker, and asserts both receive child AttackOne before the parent, the observer only receives it inside the captured view, resources/slot match the authoritative heal, and persisted MOB.Exp is unchanged. Add an insufficient-mana fixture mode that expects SetHpMp before the parent and no child AttackOne/heal.
- [ ] **Step 3: Run the harness self-tests and confirm RED.** Expected: missing CLI mode/network runner assertions fail; originals and the loopback listener remain untouched.
- [ ] **Step 4: Implement the isolated TCP smoke and runner mode.** Seed only the test actor copy's measured MOBEXTRA field, leave originals and secrets untouched, add complete cleanup/evidence assertions, and keep this headless (no `WYD.exe`).
- [ ] **Step 5: Run Debug gates.** From `Server`, run Engine.Tests, then:

```powershell
dotnet build .\WydCdk.Engine.slnx --no-restore -v quiet -m:1 -p:UseSharedCompilation=false
pwsh -NoProfile -File .\tools\PortAudit\Test-AutoTradeClose.ps1 -AttackDivineBlessing
```

Expected: zero build warnings/errors, all Engine.Tests pass, smoke `ExitCode=0`, assertions/cleanup true, original fixture hashes unchanged, and child/correction/parent ordering verified.

- [ ] **Step 6: Run self-contained gates.** Publish only the server runtime to `artifacts\publish-divine-blessing-20261004` using `dotnet publish .\src\WydCdk.Server\WydCdk.Server.csproj -c Release -r win-x64 --self-contained true -p:UseSharedCompilation=false -o .\artifacts\publish-divine-blessing-20261004`; rerun the smoke with `-SkipBuild -Runtime .\artifacts\publish-divine-blessing-20261004\WydCdk.Server.Core.exe`. Expected: the same wire/state assertions pass against the self-contained executable.
- [ ] **Step 7: Complete validation and docs.** Record `dotnet --info`, probe output, test counts, build warnings/errors, debug/self-contained evidence IDs, smoke limits, and whether visual E2E remains pending in `docs/CLIENT_E2E_SMOKE.md`; update the high-level `PROGRESSO.md` checklist and existing `BACKLOG.md` acceptance entry in place. Recheck `git status` and fixture hashes; do not stage or commit.
- [ ] **Step 8: Publish only the runtime-only build to the already-authorized test sandbox after local gates pass.** Preserve a remote backup, transfer no source/docs/secrets, verify package and Core/World/Protocol SHA-256 values, and confirm UP/PVP plus listeners 8281/8282. Do not touch Site or production. If the approved route is unavailable, retain the local evidence and report the exact blocker.
