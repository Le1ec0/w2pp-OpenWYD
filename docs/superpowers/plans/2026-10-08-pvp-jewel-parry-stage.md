# Type 8 PvP Parry Stage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task by task. Steps use checkbox syntax for tracking.

**Goal:** Add an isolated pure stage that applies the 7.69 Type 8 bit 6 Parry penalty for a positive hit against a player target.

**Architecture:** A stateless helper scans the attacker's 32-record affect snapshot and returns raw target Parry after subtracting 30 for each Type 8 affect with Level bit 6. It does not clamp or mutate WorldHub state, and remains outside login, persistence, and wire paths.

**Tech Stack:** C# / .NET 10, existing Engine.Tests executable harness.

**Spec:** `docs/PORT_TRACEABILITY.md#combat-attack-pvp-jewel-parry-audit-001` and the approved isolated-stage scope in this session.

## Global Constraints

- Follow TMProject 7.69 behavior: process each of 32 eight-byte affect records; require Type 8, Level bit 6, positive damage, and a player target; subtract 30 per matching affect.
- Return the raw value without a floor; the later legacy Parry-rate calculation owns its clamp.
- Do not integrate into WorldHub, login, persistence, or wire in this slice.
- Preserve existing user changes and do not stage, commit, push, or publish.

## Review Focus

- Two active Type 8 bit 6 affects reduce Parry by 60, including when the result is below zero.
- A different affect type or a Type 8 affect without bit 6 does not reduce Parry.
- Zero or negative damage and an NPC target leave Parry unchanged.
- A snapshot shorter than 32 eight-byte records is rejected.

---

### Task 1: Pure Parry penalty stage

**Files:**
- Create: `src/WydCdk.World/LegacyPvpJewelParryMath.cs`
- Create: `tests/WydCdk.Engine.Tests/AttackPvpJewelParryTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interface:**
- Produce `LegacyPvpJewelParryMath.ApplyParryPenalty(ReadOnlySpan<byte> attackerAffectSnapshot, int targetParry, int damage, bool isPlayerTarget) -> int`.

- [x] Write tests for per-affect subtraction, non-matching Type/bit, damage/target gates, no floor, and truncated snapshot rejection.
- [x] Run Engine.Tests and confirm the new reference fails with `CS0103` because the helper is absent.
- [x] Implement the smallest helper that satisfies the tests.
- [x] Run the full Engine.Tests executable and solution build; inspect the diff and confirm no stateful or wire integration was added.
