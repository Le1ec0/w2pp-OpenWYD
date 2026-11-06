# SHOP-005 MSG_Sell Host Dispatch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire authenticated `0x037A MSG_Sell` requests through the existing 7.69 preparation, commit, and response stages.

**Architecture:** Keep protocol encoding in `WydCdk.Protocol`, the accepted-sale plan in the existing server-layer planner, and socket delivery in `Program.cs`. Use existing WorldHub rollback and checkpoint services; do not add a durable sale format.

**Tech Stack:** C# / .NET 10, WydCdk.Protocol, WydCdk.World, WydCdk.Server, Engine.Tests.

**Spec:** `Server/docs/superpowers/specs/2026-11-04-msg-sell-dispatch-design.md`

## Constraints

- Compare the behavior with TMProject 7.69 and W2PP before changing it.
- Keep `net10.0`, nullable reference types, async I/O, and cancellation tokens.
- Preserve unrelated work in the shared checkout. No schema, Site, deployment, commit, or push.
- Do not run a visual client session; use local tests and an isolated loopback Host smoke if feasible.

## Review focus

- Success panel bytes: `0x5000`, 96-byte Latin-1 text field, yellow `0xFFFFFF00`, seller ID.
- Successful response order: panel, exact request echo, optional equipment view update, UpdateEtc.
- Carry and equipment slots 8+ have no equipment-view step.
- Stale commit/planner results never send success frames; planner failure attempts the existing conditional rollback.
- Checkpoint cadence remains periodic; no immediate database transaction is introduced.
- HP/mode rejection and active trade cancellation produce their native effects through existing services.

### Task 1: Add the legacy experience-panel protocol frame

**Files:** create `Server/src/WydCdk.Protocol/ExperienceMessagePanel.cs`; create/register focused Engine.Tests coverage.

- [x] Test the fixed payload, zero-filled 96-byte text area, yellow color at offset 96, type, total size, ID, and checksum.
- [x] Implement the minimal fixed-layout frame encoder.
- [x] Run focused Engine.Tests and record output.

### Task 2: Add the sale notice to the accepted response plan

**Files:** modify `LegacyNpcSellResponsePlanner.cs` and `LegacyNpcSellCompletionTests.cs`.

- [x] Assert panel, echo, optional view update, and UpdateEtc ordering for carry and equipment.
- [x] Implement `Adquiriu {net price} de gold` in yellow before the echo.
- [x] Run the response-plan group.

### Task 3: Register authenticated Host dispatch

**Files:** modify `Server/src/WydCdk.Server/Program.cs`.

- [x] Prepare valid requests using the session, account snapshot store, item table, WorldHub, and auto-trade state.
- [x] Preserve the HP/mode response and crack-error action; close active trades through `LegacyAutoTradeCloseCoordinator`, deliver its captured frames, then stop this request.
- [x] Build the completion plan from the current recycle-bin snapshot, commit through `WorldHub.TryCommitNpcSell`, and build the response plan.
- [x] Conditionally roll back if a response plan cannot be built; otherwise send every response step in order to seller/view recipients.
- [x] Record non-ready preparation/commit outcomes without emitting success frames.

### Task 4: Verify the integrated slice

- [x] Run Engine.Tests and record the result. The restricted run denied temp-file and loopback access; the local rerun finished `588/588`, including the separate seller/treasury checkpoint regression.
- [x] Build `WydCdk.Engine.slnx` with pinned .NET SDK 10.0.401: 0 warnings/errors.
- [x] Run the isolated authenticated file-store loopback sale smoke twice; a test-only `StartWait=600` keeps the generated merchant route stable while AI runs.
- [x] Update `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`, `Server/docs/PORT_ARCHITECTURE_BASELINE.md`, `Server/docs/PORT_TRACEABILITY.md`, and the E2E smoke guide in place.
- [x] Review the scoped handoff, evidence paths, and shared checkout status. Preserve the unrelated dirty files; do not stage or commit.
