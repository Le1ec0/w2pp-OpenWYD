# NPC Sell Price Math Implementation Plan

> **For agentic workers:** Execute this plan task-by-task. Do not commit or publish.

**Goal:** Port the shared 7.69/W2PP NPC resale-price arithmetic as a pure C# helper, while reporting source `int32` intermediate overflow as unverified.

**Architecture:** `WydCdk.World` receives the item base price, item ID, seller class/learned mask/special value, and NPC village/tax. It returns the calculated Gold amount or an explicit status when the legacy source rejects the price or its signed-overflow result is not established. No dispatcher, item mutation, or persistence is part of this slice.

**Tech Stack:** C# / .NET 10, `WydCdk.World`, `WydCdk.Engine.Tests`.

**Spec:** TMProject 7.69 and W2PP `TMSrv/_MSG_Sell.cpp` resale formula; local server-format `Common/ItemList.bin`; current SHOP-005 contract in `docs/PORT_TRACEABILITY.md`.

## Global Constraints

- Compare both local C++ sources; TMProject 7.69 remains the behavior authority.
- Keep price arithmetic pure in World; do not enable packet handling or mutate Gold/items.
- Preserve integer truncation and exact threshold/class/skill/item gates.
- Do not guess the result of signed 32-bit overflow; surface it as unverified.
- Target .NET 10; no schema, Site, deploy, commit, or push.

## Review Focus

- The quarter-price bands distinguish 5,000/5,001 and 10,000/10,001 after division by four.
- The class 3 + learned mask `0x200` seller bonus requires price greater than 1,000 and excludes item IDs 693-695.
- The bonus derives from `Special[2] / 10`, caps at 26, then halves with integer truncation.
- NPC village IDs outside 0..5 bypass city tax; valid villages require tax 0..30.
- Source multiplications that exceed signed `int32` return an unverified status; item 3443 demonstrates this input range in the local 7.69 ItemList.

---

### Task 1: Add pure legacy NPC sell price calculation

**Files:**
- Create: `src/WydCdk.World/LegacyNpcSellPriceMath.cs`
- Create: `tests/WydCdk.Engine.Tests/LegacyNpcSellPriceMathTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `../../BACKLOG.md`
- Modify: `../../PROGRESSO.md`

**Interfaces:**
- Consumes: item-list base Gold price and the already-authoritative seller/NPC scalar state.
- Produces: `LegacyNpcSellPriceMath.Calculate(...)` returning status plus sale amount; no world mutation or packet relay.

- [x] Write failing tests for quarter-price boundaries, seller bonus gates/cap/exclusions, city-tax boundaries, invalid inputs, and overflow status.
- [x] Run the test project build and observe failure because the calculation result type does not exist (`CS0246`).
- [x] Implement only the source-backed calculation and explicit overflow boundary.
- [x] Run the full Engine.Tests harness and full solution build; record all observed failures honestly (`517/560`, 43 failures; build 0 warnings/errors).
- [x] Update SHOP-005 traceability, backlog, and progress; leave the handler transaction pending.
