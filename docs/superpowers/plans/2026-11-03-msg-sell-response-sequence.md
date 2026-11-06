# SHOP-005 MSG_Sell Response Sequence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a testable ordered response plan for accepted 7.69 NPC sales.

**Architecture:** A server-layer planner validates the decoded request against the committed sale outcome and returns ordered seller/view response steps. It reuses the existing WorldHub 7.69 frame builders and does not send frames or register dispatch.

**Tech Stack:** C# / .NET 10, WydCdk.Protocol, WydCdk.World, Engine.Tests.

**Spec:** `Server/docs/superpowers/specs/2026-11-03-msg-sell-response-sequence-design.md`

## Global Constraints

- Use `net10.0`, nullable reference types, implicit usings, async/cancellation patterns for any I/O.
- Preserve the 7.69 wire contract and compare against `Server/W2PP`; the reference defines source behavior, not architecture.
- Keep local-only; no schema, Site, deploy, commit, push, or E2E.
- Preserve unrelated dirty work in the shared checkout.

## Review Focus

- Bad checksum, wrong opcode, or truncated request: no response plan.
- Request target/source/slot mismatch with committed outcome: no response plan.
- Carry sale: echo then seller `0x0337`, with no `0x036B` step.
- Equipment slot 1 through 7: echo, `0x036B` to the view excluding seller, then seller `0x0337`.
- Equipment slot 8 or later: no `0x036B` step.

---

### Task 1: Ordered NPC sale response plan

**Files:**
- Create: `Server/src/WydCdk.Server/LegacyNpcSellResponsePlanner.cs`
- Modify: `Server/tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `Server/tests/WydCdk.Engine.Tests/LegacyNpcSellCompletionTests.cs`

**Interfaces:**
- Consumes: `WorldHub.TryBuildUpdateEquipFrame`, `WorldHub.TryBuildUpdateEtcFrame`, `WorldHub.GetParticipantIdsInView`, `LegacyNpcSellCompletionOutcome`, `ShopSellMessage.TryParse`.
- Produces: `LegacyNpcSellResponsePlanner.TryBuild(WorldHub, DecodedFrame, LegacyNpcSellCompletionOutcome, LegacyFrameCodec, out LegacyNpcSellResponsePlan?)`; ordered `LegacyNpcSellResponseStep` values identify seller or view delivery and carry a frame plus recipient IDs.

- [x] **Step 1: Add response-plan tests**

  Add coverage for malformed/mismatched requests, carry order, equipment slot 1 view recipients and frame order, and equipment slot 8 omission. Register a separate `NPC sell response sequence` Engine.Tests group.

- [x] **Step 2: Run Engine.Tests and confirm RED**

  Run: `dotnet run --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore`
  Expected: compilation fails because `LegacyNpcSellResponsePlanner` and its response-plan types do not exist yet.

- [x] **Step 3: Implement the planner**

  Create the response types and planner in `LegacyNpcSellResponsePlanner.cs`. Validate the decoded request and its correlation with the committed outcome before building frames. Preserve the request header values for the echo. Add an equipment view step only when `Source.RequiresEquipmentUpdate` is true, remove the seller from the captured 33x33 recipients, and append the seller's UpdateEtc step last.

- [x] **Step 4: Run Engine.Tests and confirm GREEN**

  Run: `dotnet run --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore`
  Expected: the response-sequence group passes. Any existing sandbox loopback failures are recorded by name.

- [x] **Step 5: Build and update project handoff**

  Run: `dotnet build Server/WydCdk.Engine.slnx --no-restore --verbosity:minimal`
  Expected: build succeeds with no warnings/errors. Update `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`, `Server/docs/PORT_ARCHITECTURE_BASELINE.md`, and `Server/docs/PORT_TRACEABILITY.md` in place with the response-plan boundary and validation result.

- [x] **Step 6: Review the scoped diff and record validation**

  Inspect the changed files, preserve unrelated changes, and record current build/test output. Do not commit or stage.
