# MSG_Sell 7.69 Wire Contract Implementation Plan

> **For agentic workers:** Execute this plan task-by-task. Do not commit or publish.

**Goal:** Add only the 7.69 `MSG_Sell` frame contract to the C# protocol layer.

**Architecture:** A protocol DTO owns the shared request/echo wire shape. Engine.Tests pins its literal payload, frame size, signed fields, ABI tail padding, and parser rejection cases. The Server dispatcher and sell behavior stay unchanged.

**Tech Stack:** C# / .NET 10, `WydCdk.Protocol`, console-based `WydCdk.Engine.Tests`.

**Spec:** TMProject `Projects/TMProject/Basedef.h:1168-1175`, `SGrid.cpp:1817-1827`; 7.69 TMSrv `Basedef.h:1957-1964`; W2PP `Basedef.h:2135-2142`; project constraints in `AGENTS.md`.

## Global Constraints

- Use the local TMProject 7.69 source as the wire authority and compare W2PP before porting.
- Keep `Protocol` independent of transport, world, and persistence.
- Target `net10.0`; do not change schema, handler dispatch, or business behavior.
- Do not commit, push, deploy, or run the visual client E2E.

## Review Focus

- Wrong opcode must not parse as `MSG_Sell`.
- Invalid checksum must be rejected.
- Truncated or wrong-sized payloads must be rejected.
- Signed `MyType`/`MyPos` values must round-trip unchanged.
- The two ABI tail-padding bytes must be preserved on echo.

---

### Task 1: Add the MSG_Sell wire DTO and regression coverage

**Files:**
- Create: `src/WydCdk.Protocol/ShopSellMessage.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `../../BACKLOG.md`
- Modify: `../../PROGRESSO.md`

**Interfaces:**
- Consumes: `LegacyFrameCodec`, `DecodedFrame`, and `PacketHeader`.
- Produces: `ShopSellMessage` for type `0x037A`, with `TargetId`, signed `MyType`, signed `MyPos`, and two preserved padding bytes; 20-byte frame and 8-byte payload.

- [x] Write a failing `MSG_Sell 7.69 wire` test for payload bytes, 20-byte frame, signed fields, padding, round-trip, invalid checksum, wrong opcode, and truncation.
- [x] Run the test harness build and observe the new test fail because `ShopSellMessage` does not exist (`CS0246`).
- [x] Implement the minimal DTO/parser/serializer in `ShopSellMessage.cs`.
- [x] Run the new test and full validation; record pre-existing/environment failures without treating them as passes.
- [x] Update the SHOP-005 traceability and progress records, explicitly leaving the handler and sell transaction pending.
