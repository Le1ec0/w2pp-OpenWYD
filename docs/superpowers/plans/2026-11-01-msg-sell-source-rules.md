# MSG_Sell Source Slot Rules Implementation Plan

> **For agentic workers:** Execute this plan task-by-task. Do not commit or publish.

**Goal:** Port the shared 7.69/W2PP source-type and slot gates for NPC sale requests.

**Architecture:** A pure World rule resolves the packet's signed `MyType`/`MyPos` to equipment, carry, or cargo, preserving the legacy visual equipment relay flag and auto-trade cargo gate. It does not read or mutate inventory and is not connected to the Server dispatcher.

**Tech Stack:** C# / .NET 10, `WydCdk.World`, `WydCdk.Engine.Tests`.

**Spec:** TMProject 7.69 and W2PP `TMSrv/_MSG_Sell.cpp` source-position gates; `GetItemPointer` in each `Basedef.cpp`; `MAX_EQUIP=16`, `MAX_CARRY=64`, `MAX_CARGO=128`.

## Global Constraints

- Keep the local TMProject 7.69 source as behavior authority and compare W2PP.
- Equipment is server-side 16 slots; do not use the client visual 18-slot extension.
- Carry sale accepts slots 0..59; cargo accepts slots 0..127.
- Auto-trade cargo rejection precedes cargo position validation.
- No dispatcher, inventory/Gold mutation, persistence, or visual E2E.

## Review Focus

- Equipment position 0 is rejected; 1..15 are accepted; only 1..7 requests `SendEquip` refresh.
- Carry boundaries 0 and 59 are accepted; -1 and 60+ are rejected.
- Cargo boundaries 0 and 127 are accepted outside auto-trade; -1 and 128+ are rejected.
- `TradeMode` blocks cargo before position checking and leaves carry sales eligible.
- Unknown signed `MyType` values are rejected.

---

### Task 1: Add pure MSG_Sell source resolution

**Files:**
- Create: `src/WydCdk.World/LegacyNpcSellSourceRules.cs`
- Create: `tests/WydCdk.Engine.Tests/LegacyNpcSellSourceRulesTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `../../BACKLOG.md`
- Modify: `../../PROGRESSO.md`

**Interfaces:**
- Consumes: signed `MyType`, signed `MyPos`, and current `TradeMode`.
- Produces: source kind, validated position, status, and `RequiresEquipmentUpdate` flag.

- [x] Write failing boundary/order tests.
- [x] Run the test project build and observe the missing resolver result type (`CS0246`).
- [x] Implement only the source-backed position/trade-mode rule.
- [x] Run the full Engine.Tests harness and full solution build; record failures honestly (`518/561`, 43 failures; build 0 warnings/errors).
- [x] Update SHOP-005 traceability/progress and leave item validation/transaction pending.
