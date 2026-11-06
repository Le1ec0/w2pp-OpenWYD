# SHOP-005 MOB-Resident NPC Sale Commit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Atomically apply a prepared NPC sale to WorldHub-owned equipment/carry, the 7.69 equipment mirror, Gold, and transient recycle-bin state, with compare-and-swap validation and rollback support.

**Architecture:** A pure completion builder turns the successful live preparation into copied before/after snapshots. A WorldHub partial validates seller MOB, the independent equipment mirror when applicable, target/location tax, Gold, and recycle-bin snapshots under its lock before replacing WorldHub-owned state together. Equipment slots 1..15 are also mirrored by the versioned 18-slot sidecar; WorldHub keeps both runtime representations aligned, and the existing departure snapshot copies MOB before persisting the sidecar. Cargo remains unsupported until its account-store owner has a versioned mutation contract; the sale packet remains undispatched.

**Tech Stack:** C# / .NET 10, existing `WydCdk.World` APIs, Engine.Tests harness.

**Spec:** `docs/PORT_TRACEABILITY.md`, sections `SHOP-005 - ordered _MSG_Sell gate composition` and `SHOP-005 - live _MSG_Sell preparation coordinator`; canonical reference `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Sell.cpp`, compared with `W2PP/Source/Code/TMSrv/_MSG_Sell.cpp`.

## Global Constraints

- TMProject 7.69 is the canonical behavior reference; compare W2PP and record differences.
- Do not dispatch `0x037A`, change account schema, persist new fields, deploy, commit, or push.
- Only apply state owned by WorldHub in this slice; cargo and external account-store mutation stay unsupported.
- Preserve `VillageFiveCityTaxUnverified`; do not infer behavior from the legacy `g_pGuildZone[5]` out-of-bounds read.
- Use .NET 10, `async`/`CancellationToken` for I/O, and validate solution build plus Engine.Tests.

## Review Focus

- Stale seller MOB or recycle-bin snapshots must reject the commit without changing any state.
- A target kind/position/merchant or city-tax change after preparation must reject it.
- Coin must clamp at 2,000,000,000 only while the source `int32` sum is representable; larger sums return unverified overflow.
- Equipment/carry clear exactly the selected 8-byte slot; equipment also clears the 7.69 mirror and resets the equipment-derived parry adjustment. Cargo and village 5 yield no plan.
- Successful insertion and rollback must move/restore item, Coin, and all ten recycle-bin entries together.

---

### Task 1: Revalidated WorldHub NPC sale for MOB-resident sources

**Files:**
- Create: `src/WydCdk.World/LegacyNpcSellCompletionRules.cs`
- Create: `src/WydCdk.World/WorldHub.NpcSellCommit.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Create: `tests/WydCdk.Engine.Tests/LegacyNpcSellCompletionTests.cs`
- Update: `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`, `docs/PORT_TRACEABILITY.md`, `docs/PORT_ARCHITECTURE_BASELINE.md`

**Interfaces:**
- Consumes: `LegacyNpcSellPreparationOutcome`, `LegacyNpcSellFlowResult`, `LegacyRecycleBinEntry`, live `WorldHub` participant state.
- Produces: `LegacyNpcSellCompletionRules.TryBuild(preparation, connectionId, recycleBinSnapshot, randomValue, out plan)`, `WorldHub.TryCommitNpcSell(plan, out outcome)`, and `WorldHub.TryRollbackNpcSell(outcome)`.

- [x] **Step 1: Write failing tests** for carry/equipment clearing and mirror synchronization, capped Coin/buyback, stale seller/equipment/target/bin rejection, conditional rollback, cargo no-plan, and unverified `int32` overflow.
- [x] **Step 2: Run Engine.Tests to verify RED** because the completion plan and WorldHub commit APIs did not exist; the later mirror-snapshot assertion also failed compilation until its safe WorldHub snapshot API was added.
- [x] **Step 3: Implement the pure copied-snapshot planner** for equipment/carry only; preserve the 7.69 strict `< 2,000,000,000` coin rule, reject unrepresentable signed `int32` sums, and refuse village 5.
- [x] **Step 4: Implement WorldHub commit and rollback under its lock**, revalidating seller MOB/equipment mirror, target/location tax, Coin, and recycle-bin state; equipment commits update both runtime copies and rollback restores the parry adjustment.
- [x] **Step 5: Run the full Engine.Tests and `dotnet build WydCdk.Engine.slnx --no-restore --verbosity:minimal`**; record SDK info and the two remaining loopback limits.
- [x] **Step 6: Update the existing port handoff and traceability in place.** Leave packet dispatch, durable account commit, external tax/experience, cargo, notices and relays for later slices.
