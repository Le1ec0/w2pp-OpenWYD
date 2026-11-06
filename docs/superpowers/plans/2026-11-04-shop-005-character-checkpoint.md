# SHOP-005 Character Checkpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist authoritative in-game character state through a TMProject-compatible bounded round-robin checkpoint.

**Architecture:** Reserve/reuse Host connection IDs `1..999`; a serial timer scans from its cursor to the next `Playing` player, skipping empty IDs and saving at most one player per nominal four-second tick. A composed storage operation saves the live MOB, position, Extra, affects, and 18-slot equipment while preserving fields owned by independent writers; file storage uses its journal and shared gate, and MariaDB commits blob plus sidecar in one transaction.

**Tech Stack:** C# / .NET 10, `WydCdk.World`, `WydCdk.Server`, console-based `WydCdk.Engine.Tests`, local MariaDB integration harness.

**Spec:** `Server/docs/superpowers/specs/2026-11-04-shop-005-character-checkpoint-design.md`

## Global Constraints

- Keep `ClientVersion=7670`, wire formats, the 7,945-byte W2PP account blob, and database schema unchanged.
- Use TMProject 7.69 as the behavior authority; do not reproduce the `SaveUser` daily-counter assignment defect.
- Persist only live Host state: MOB, position, `MOBEXTRA`, affects, and 18-slot equipment; keep `ShortSkill`, Donate, and SkillBar on their existing ownership paths.
- Never hold the `WorldHub` lock during I/O; acquire the per-slot checkpoint lock before revalidating the session and capturing its snapshot.
- Keep file `--accounts-root` single-process; production UP/PVP stores use MariaDB.
- Use async file/database APIs with `CancellationToken`; no automatic commit, push, deploy, Site change, or client change.
- Record `dotnet --info`; SDK is pinned at .NET 10.0.401 with `latestPatch` and `allowPrerelease=false`.

## Review Focus

- Exhausted/reused connection slots never duplicate IDs or enter the NPC range — `LegacyConnectionSlotAllocatorTests` (Task 1).
- Empty, non-playing, inconsistent, and active slots produce the source order and one save at most per tick — `LegacyCharacterCheckpointRuntimeTests` (Task 3).
- Logout/reconnect while a periodic save is in flight never writes the old occupant after the slot is reused — `LegacyCharacterCheckpointRuntimeTests` and Host smoke (Task 3).
- SkillBar, ShortSkill, Donate, Cargo, daily quest, and unrelated account bytes survive a checkpoint; carried Gold is written only inside MOB and account-bank Coin is preserved — `LegacyCharacterCheckpointFileStoreTests` (Task 2) and MariaDB integration (Task 2).
- A crash/fault between blob and equipment writes restores an all-before or all-after state, and slow/failing saves do not overlap or stop the cursor — file journal tests (Task 2) and runtime tests (Task 3).

---

### Task 1: Bounded connection slot allocator

**Files:**
- Create: `Server/src/WydCdk.World/LegacyConnectionSlotAllocator.cs`
- Create: `Server/tests/WydCdk.Engine.Tests/LegacyConnectionSlotAllocatorTests.cs`
- Modify: `Server/tests/WydCdk.Engine.Tests/Program.cs` (register the suite)

**Interfaces:**
- Produces `LegacyConnectionSlotAllocator.TryAllocate(out int connectionId) -> bool` and `Release(int connectionId) -> bool`.
- `TryAllocate` returns unique IDs in circular order `1..999`; ID `0` and IDs `>=1000` are never issued. Exhaustion returns `false`; releasing an active ID makes it reusable.

- [x] **Step 1: Write allocator tests** for initial ID 1, unique allocation through 999, exhaustion, wrap/reuse, invalid release, and duplicate release.
- [x] **Step 2: Run the focused test** with `dotnet run --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj`; confirm the new allocator contract fails before implementation.
- [x] **Step 3: Implement `LegacyConnectionSlotAllocator`** as a thread-safe circular pool with a fixed range `1..999` and no per-connection allocations outside that range.
- [x] **Step 4: Rerun the focused test** and confirm all allocator cases pass.

### Task 2: Composite checkpoint API and store transactions

**Files:**
- Create: `Server/src/WydCdk.World/LegacyCharacterCheckpoint.cs`
- Modify: `Server/src/WydCdk.World/LegacyFileAccountStore.cs`
- Modify: `Server/src/WydCdk.World/MariaDbWorldCharacterStore.cs`
- Create: `Server/tests/WydCdk.Engine.Tests/LegacyCharacterCheckpointFileStoreTests.cs`
- Modify: `Server/tests/WydCdk.Character.Integration/Program.cs`
- Modify: `Server/tests/WydCdk.Engine.Tests/Program.cs` (register the suite)

**Interfaces:**
- Create `LegacyCharacterCheckpointSaveRequest(string AccountName, int CharacterSlot, ReadOnlyMemory<byte> Mob, short PositionX, short PositionY, ReadOnlyMemory<byte> MobExtra, ReadOnlyMemory<byte> Affect, IReadOnlyList<LegacyItem> ClientEquipment)`.
- Create `LegacyCharacterCheckpointSaveResult` with `Success`, `AccountNotFound`, `SlotOutOfRange`, `InvalidLength`, `CharacterNotAvailable`, and `NotAvailable`.
- Create `ICharacterCheckpointStore.TrySaveCharacterCheckpointAsync(LegacyCharacterCheckpointSaveRequest request, CancellationToken cancellationToken = default)` and make `ICharacterStore` inherit it; both `LegacyFileAccountStore` and `MariaDbWorldCharacterStore` implement it.

- [x] **Step 1: Write file-store tests** for field updates, SkillBar/ShortSkill/Donate/Cargo/daily-quest/unowned-byte preservation, invalid inputs, sidecar synchronization, and journal recovery from both `Prepared` and `Committed` phases.
- [x] **Step 2: Write isolated MariaDB integration assertions** for blob-plus-sidecar commit, field preservation, and rollback after an injected sidecar-write failure.
- [x] **Step 3: Run both test sets** before implementation: `dotnet run --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj` and `dotnet run --project Server/tests/WydCdk.Character.Integration/WydCdk.Character.Integration.csproj -- --config <protected-file> --host 127.0.0.1 --database wyd_cdk_autotrade_close_test`; confirm the new checkpoint assertions fail because the public operation is not implemented.
- [x] **Step 4: Add the request/result/interface** and implement `TrySaveCharacterCheckpointAsync` in both stores. File storage constructs the updated 7,945-byte account image from the current stored image, preserves SkillBar and the account-bank Coin at `AccountCoinOffset`, and includes the live MOB (with carried Gold), Extra, affects, and encoded 18-slot sidecar in one journaled operation. MariaDB performs equivalent blob and sidecar changes in one `Serializable` transaction.
- [x] **Step 5: Generalize file journal validation/recovery** to read the current two-account v1 journal and write/read a v2 account-plus-sidecar form; restrict sidecar entries to paths and lengths produced by `TryResolveClientEquipmentSidecarPath` and recover before combined login reads.
- [x] **Step 6: Replace separate file/equipment writer gates with one async state gate** shared by account-file mutations, checkpoint journal operations, sidecar mutations, and reads that combine account and sidecar state. Cover numeric token, Donate, position/state/guild/ShortSkill, new/delete character, client equipment, atomic two-account save, and checkpoint methods.
- [x] Extend that gate to `LegacyAutoTradeFileStore` when it targets the same account root; cover that the account checkpoint reader and AutoTrade reader/writer path await the same process-local gate. It does not coordinate separate processes.
- [x] **Step 7: Run `Engine.Tests` focused cases** and confirm normal writes, concurrent independent writers, invalid paths, and interrupted journal recovery preserve the expected before/after state.
- [ ] **Step 8: Run the MariaDB integration harness** against loopback database `wyd_cdk_autotrade_close_test`; confirm success and rollback cases pass without changing schema.

Task 2 validation note: earlier integration attempts against `127.0.0.1` and
`::1` returned provider code 1045 while opening the protected local config. The
fresh 04/11 attempt returned provider code 1042; the loopback TCP probe to
`127.0.0.1:3306` was false. Every attempt failed before creating tables or
changing data; rerun when the local database service is available.

### Task 3: Round-robin runtime, logout sharing, and Host wiring

**Files:**
- Create: `Server/src/WydCdk.Server/LegacyCharacterCheckpointRuntime.cs`
- Modify: `Server/src/WydCdk.Server/Program.cs`
- Create: `Server/tests/WydCdk.Engine.Tests/LegacyCharacterCheckpointRuntimeTests.cs`
- Modify: `Server/tests/WydCdk.Engine.Tests/Program.cs` (register the suite)
- Create: `Server/tests/WydCdk.Client.E2E/LegacyCharacterCheckpointNetwork.cs`
- Modify: `Server/tests/WydCdk.Client.E2E/Program.cs` (register a loopback checkpoint smoke)

**Interfaces:**
- `LegacyCharacterCheckpointRuntime(LoginSessionRegistry sessions, WorldHub world, ICharacterCheckpointStore store, Func<string, int, short, short, CancellationToken, ValueTask<CharacterPositionSaveResult>> savePositionFallback, TimeProvider? timeProvider = null)` exposes `RunAsync(CancellationToken)`, `RunSinglePassAsync(CancellationToken) -> ValueTask<LegacyCharacterCheckpointPassResult>`, and `SaveOnDepartureAsync(LoginSession session, bool disconnect, CancellationToken) -> ValueTask`.
- `LegacyCharacterCheckpointPassResult` is `record LegacyCharacterCheckpointPassResult(int NextSlot, int? SavedSlot, LegacyCharacterCheckpointSaveResult? SaveResult)`; `LegacyCharacterCheckpointPolicy.Interval` is four seconds.
- A pass starts at slot 1, checks at most all 999 IDs, skips empty/non-Playing/stale participants in the same tick, saves at most one eligible character, and resumes at the slot after the saved ID. Use a four-second `PeriodicTimer`; serialize passes and do not create catch-up queues or overlapping writes.
- The same per-slot semaphore protects periodic and departure capture/write. Recheck `LoginSession.State`, account, and character slot after acquiring it; capture `WorldHub` snapshot only then. If no full snapshot exists on departure, preserve the current position-only fallback.

- [x] **Step 1: Write runtime tests** for initial cursor, skipping empty IDs, one active save per pass, wrap `999 -> 1`, stale/closed session revalidation, departure waiting behind an in-flight checkpoint, continued operation after store failure, and no overlapping writes.
- [x] **Step 2: Run the focused test** with `dotnet run --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj`; confirm the scheduler contract fails before implementation.
- [x] **Step 3: Implement `LegacyCharacterCheckpointRuntime`** with the four-second interval, fixed scan/cursor semantics, per-slot async gates, the composite store request, safe result logging, and cooperative cancellation.
- [x] **Step 4: Wire `Program.cs`** to allocate IDs from `LegacyConnectionSlotAllocator`, close accepted clients when the pool is exhausted, release each slot in a wrapper `finally` after `InspectConnectionAsync`, run/await the checkpoint loop, and route logout/disconnect persistence through `SaveOnDepartureAsync`.
- [x] **Step 5: Rerun runtime and allocator tests**; confirm departure save failure still closes the session/releases the slot and a later occupant is never written using the prior occupant's snapshot.
- [x] **Step 6: Implement `--character-checkpoint-smoke`** in the E2E harness. Launch an isolated Host with a disposable accounts root, log in and enter a character, make a persisted character-state change, wait for one checkpoint, reconnect, and assert the change plus equipment sidecar survived. Keep credentials and account data in the disposable fixture.
- [x] **Step 7: Run the checkpoint network smoke** with `dotnet run --project Server/tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --character-checkpoint-smoke --item-data Client/7670/ItemList.bin`; require an explicit pass marker and confirm the child Host shut down cleanly.

### Task 4: Documentation and final validation

**Files:**
- Modify: `AGENTS.md` handoff wording in-place
- Modify: `BACKLOG.md` SHOP-005 section in-place
- Modify: `PROGRESSO.md` SHOP-005 checklist in-place
- Modify: `Server/docs/PORT_TRACEABILITY.md` SHOP-005 section in-place
- Modify: `Server/docs/PORT_ARCHITECTURE_BASELINE.md` checkpoint notes in-place

- [x] **Step 1: Correct the timer description** to state that TMProject scans past empty IDs within a tick, saves at most one player every nominal four seconds, and therefore revisits a player after roughly `active-player-count × 4 s`; remove the earlier full `999 × 4 s` claim.
- [x] **Step 2: Record the implemented field contract, file journal, MariaDB transaction, ID capacity, test results, and remaining limitations** in the existing SHOP-005 entries. The original pending note for `0x037A` is superseded: authenticated sale dispatch is now active and its loopback evidence is recorded in `PORT_TRACEABILITY.md`; buyback, NPC buy/stock, visual E2E, and deployment remain pending.
- [x] **Step 3: Capture `dotnet --info`** to the existing build-evidence area and record SDK 10.0.401 / any Service Control Manager access warning.
- [x] **Step 4: Build the solution** with `dotnet build Server/WydCdk.Engine.slnx --no-restore --verbosity:minimal -m:1`; expected: 0 compiler warnings and errors, with any `NU1900` recorded as inconclusive feed audit.
- [x] **Step 5: Run `Engine.Tests`** with `dotnet run --no-build --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj`; confirm all new tests pass and report any pre-existing loopback failures by name.
- [ ] **Step 6: Run the MariaDB integration harness** with the protected local config, literal loopback host, and exact isolated database; confirm checkpoint and rollback behavior.
- [x] **Step 7: Run the authenticated loopback smoke** with `dotnet run --project Server/tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --character-checkpoint-smoke --item-data Client/7670/ItemList.bin`; the harness creates a disposable `--accounts-root`, logs in and enters a character, triggers a state change, waits for the checkpoint, reconnects, and verifies persistence. Do not access production or deploy.
- [x] **Step 8: Review the final diff** to confirm no schema/client/Site changes and no commit, push, deploy, or unrelated module rewrite.

Task 4 latest validation note (05/11): renamed the account projection to
`LegacyAccountSnapshot.BankCoin`, named the MOB field
`MobCarriedGoldOffset`, and removed the old MOB-to-bank copy from checkpoint,
character-state, guild, and atomic-state writers in both backends. The complete
solution build passed with 0 warnings/errors. `Engine.Tests` completed
`590/592`; `legacy file character checkpoint composite` passed, and the two
loopback failures were `authenticated startup requires ItemList before bind`
(`CurrentScoreLoginStartupTests`) and `serialized network stream concurrent
writes` (`SocketException 10013`). `dotnet --info` identified SDK 10.0.401 and
exited 1 because the Service Control Manager denied access. MariaDB integration
remains pending; this run used no isolated database fixture. No visual E2E was
run.

Continuation probe (05/11): `Test-NetConnection` reported no listener at either
`127.0.0.1:13306` or `127.0.0.1:3306`, so the MariaDB harness was not run. No
DDL or test data was changed. Task 2 Step 8 and Task 4 Step 6 remain unchecked;
retry the exact isolated `wyd_cdk_autotrade_close_test` when its local listener
is available.
