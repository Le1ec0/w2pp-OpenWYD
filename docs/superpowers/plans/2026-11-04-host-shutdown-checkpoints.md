# Host Shutdown Checkpoints Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Drain accepted Host connection handlers and run one final registered-treasury checkpoint during cooperative shutdown.

**Architecture:** Keep the existing shared cancellation token for stopping the accept loop and periodic services. Track accepted connection tasks until the accept loop stops, await those handlers and the periodic treasury loop, then call the existing `LegacyGuildTreasuryFileStore.SaveRegisteredAsync` once. The account and treasury stores remain independent.

**Tech Stack:** C# / .NET 10, `WydCdk.Server`, `WydCdk.World`, Engine.Tests, authenticated loopback E2E harness.

**Spec:** `Server/docs/superpowers/specs/2026-11-04-host-shutdown-checkpoints-design.md`

## Global Constraints

- Preserve `net10.0`, nullable reference types, and async I/O with `CancellationToken`.
- Use the existing 4-second player checkpoint and 12-second treasury period; do not change either cadence.
- Do not introduce a joint sale transaction, new journal/schema, immediate sale write, Site change, deploy, commit, or push.
- Preserve unrelated changes in the dirty shared checkout.

## Review Focus

- A pending connection handler must keep shutdown alive through its departure save.
- Successful completed tasks must be pruned; a faulted handler must still be observable when draining.
- Final treasury save must happen after active handlers and the periodic treasury loop finish.
- Final treasury I/O/format failure must not prevent publishing offline status, but must surface as a shutdown failure.
- Without `--npc-root`, shutdown must not attempt a treasury file write.

---

### Task 1: Implement the existing connection-drain contract

**Files:**
- Create: `Server/src/WydCdk.Server/LegacyConnectionTaskDrain.cs`
- Existing test: `Server/tests/WydCdk.Engine.Tests/LegacyConnectionTaskDrainTests.cs`
- Existing registration: `Server/tests/WydCdk.Engine.Tests/Program.cs`

**Interface:** The shared checkout already has `LegacyConnectionTaskDrainTests` registered. Implement `LegacyConnectionTaskDrain.Track(Task)` and `StopAcceptingAndDrainAsync()`. `Track` rejects new handlers once stopping begins; successful completions are pruned on later registration, and faulted/canceled tasks remain visible to drain.

- [x] Run `dotnet run --project tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore`; expected initial red is the missing `LegacyConnectionTaskDrain` type.
- [x] Implement the smallest thread-safe drain with a stop gate, completed-success pruning, idempotent stop, and fault visibility.
- [x] Run Engine.Tests and confirm the new cases pass.

### Task 2: Drain sessions and checkpoint treasury on Host shutdown

**Files:**
- Modify: `Server/src/WydCdk.Server/Program.cs`
- Modify: `Server/tests/WydCdk.Client.E2E/NpcSellNetwork.cs`

- [x] Update the authenticated NPC sell smoke to stop the Host while the seller remains connected, then verify MOB Coin and cleared carry persisted after process exit.
- [x] Track every accepted `RunAllocatedConnectionAsync` task and drain the registry after the accept loop stops.
- [x] After handlers drain and `guildTreasurySaveLoop` completes, call `SaveRegisteredAsync(world, CancellationToken.None)` once when `guildTreasuryFileStore` is configured.
- [x] Log final saved/skipped treasury counts; collect known I/O/format failures while still publishing offline status.
- [x] Run the authenticated loopback smoke twice and confirm the Host exits cleanly with the seller checkpoint persisted.

### Task 3: Record shutdown semantics and validation

**Files:**
- Modify: `AGENTS.md`
- Modify: `PROGRESSO.md`
- Modify: `BACKLOG.md`
- Modify: `Server/docs/PORT_ARCHITECTURE_BASELINE.md`
- Modify: `Server/docs/PORT_TRACEABILITY.md`
- Modify: this plan

- [x] Document the ordering, independent-store boundary, configured-root behavior, and failure reporting.
- [x] Run `dotnet build WydCdk.Engine.slnx --no-restore --verbosity:minimal -m:1` and record the result.
- [x] Run Engine.Tests and record the full result.
- [x] Review scoped files and checkout status. Do not stage or commit.
