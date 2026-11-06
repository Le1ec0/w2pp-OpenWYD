# SHOP-005 Sell Tax Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the two ordered NPC guild-tax EXP credits from TMProject 7.69 into the same in-memory WorldHub sale mutation.

**Architecture:** Preserve gross and net sell prices so the source's two integer divisions produce `tcoin`. Under the existing WorldHub lock, apply the city recipient (cap 200,000,000,000) and then global zone 4 recipient (cap 20,000,000,000,000), and return one before/after snapshot per changed NPC for conditional rollback. Do not add persistence, handler dispatch, or relay.

**Tech Stack:** C# / .NET 10; `WydCdk.World`; `WydCdk.Engine.Tests`.

**Spec:** `AGENTS.md` current architecture and port rules; `Server/docs/PORT_TRACEABILITY.md`, `SHOP-005` source comparison.

## Global Constraints

- Compare TMProject 7.69 and W2PP; follow the canonical 7.69 behavior and record differences.
- Keep all treasury changes in memory and within the current sale transaction lock.
- Do not change persistence schema, dispatch, relay, commit, push, deploy, or run visual E2E.
- Preserve unrelated dirty working-tree changes.

## Review Focus

- Two truncating divisions in `tcoin=(gross-net)/2/2`; reject impossible negative/gross-less-than-net inputs.
- Strict EXP thresholds and full-credit overshoot.
- Inclusive `MAX_USER` and exclusive `MAX_MOB` treasury ID bounds.
- Both city and global checks run in source order, including a shared zone-4 recipient.
- Missing, inactive, or out-of-range recipient does not reject an otherwise valid sale; `WorldHub.EnterNpc` requires full `CharacterStride`, while the credit keeps a defensive EXP-length check.
- Rollback checks every changed treasury before restoring any snapshot.

---

### Task 1: Preserve sell gross price and port pure tax arithmetic

**Files:**
- Modify: `Server/src/WydCdk.World/LegacyNpcSellPriceMath.cs`
- Modify: `Server/src/WydCdk.World/LegacyNpcSellFlowRules.cs`
- Test: `Server/tests/WydCdk.Engine.Tests/LegacyNpcSellPriceMathTests.cs`
- Test: `Server/tests/WydCdk.Engine.Tests/LegacyNpcSellTaxExperienceTests.cs`

- [x] Add tests for gross price retention and the two truncations, then run to confirm failure.
- [x] Implement `GrossAmount`/`GrossPrice` and `LegacyNpcSellTaxExperienceMath.TryCalculate(int grossPrice, int netPrice, out int experience)`.
- [x] Run Engine.Tests and confirm the arithmetic cases pass.

### Task 2: Apply ordered treasury credits and rollback with the sale

**Files:**
- Modify: `Server/src/WydCdk.World/WorldHub.NpcSellCommit.cs`
- Modify: `Server/tests/WydCdk.Engine.Tests/LegacyNpcSellCompletionTests.cs`

- [x] Add sale commit tests for ordered recipients, cap/overshoot, duplicate zone 4, unavailable recipients, zero `tcoin`, ID `MAX_USER`, and rollback before implementation; extend the existing Tiny guild-tax test for the shared registration boundary.
- [x] Apply city then global EXP updates under the WorldHub lock, aggregating duplicate recipient snapshots.
- [x] Make rollback compare all final treasury snapshots first, then restore them with the seller state.
- [x] Run `Engine.Tests` and verify the complete solution build.

### Task 3: Update handoff and traceability

**Files:**
- Modify: `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`
- Modify: `Server/docs/PORT_TRACEABILITY.md`, `Server/docs/PORT_ARCHITECTURE_BASELINE.md`

- [x] Record exact source conditions, validation results, remaining dispatch/persistence/relay work, and known baseline test failures in place.
