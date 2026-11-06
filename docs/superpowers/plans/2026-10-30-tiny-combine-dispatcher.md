# Tiny Combine Dispatcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the validated 7.69 Tiny combine request to locked WorldHub mutation and ordered client confirmations.

**Architecture:** Keep recipe and outcome behavior in WorldHub, expose a dedicated Protocol confirmation for `_MSG_CombineComplete`, and build the relay from outcome snapshots in the Server layer. The listener loads `Tiny ChanceBase` from an explicitly selected `CompRate.txt` or uses the 7.69 default 15; `--tiny-chance-base` is the explicit override. `LOCALSERVER` stays false because the legacy switch depends on an external server address that this port must not copy.

**Tech Stack:** C# / .NET 10, WydCdk.Protocol, WydCdk.World, WydCdk.Server, Engine.Tests.

**Spec:** `Server/docs/PORT_TRACEABILITY.md`, ITEM-008; `Server/src/WydCdk.World/LegacyTinyCombineRules.cs`; TMProject 7.69 `_MSG_CombineItemTiny.cpp` and `Language.txt`.

## Global Constraints

- Preserve local dirty work; do not commit, push, deploy, or run visual E2E.
- TMProject 7.69 defines behavior; W2PP is comparative only.
- Keep `LOCALSERVER` disabled at runtime; do not infer it from UP/PVP mode or copy its legacy address condition.
- Do not add immediate persistence; the accepted MOB follows the existing save-on-departure path.
- Port the authoritative `Merchant=6` treasury registration from NPC spawn and persist the raw MOB snapshot under the configured NPC root; do not infer the recipient from `Guild.txt` or CityTaxes.

## Review Focus

- Completion signal payload is a signed 32-bit result with scene id 30000.
- Success and failure send their item, message, completion, and Etc frames in different orders.
- Duplicate inventory positions must retain their intermediate item snapshots and duplicate updates.
- Invalid recipe and insufficient Gold send the source message plus result 0; stale item data sends only result 0.
- Concurrent state changes reject apply and must not overwrite a newer character state.

---

### Task 1: Completion signal protocol

**Files:** Create `Server/src/WydCdk.Protocol/TinyCombineCompleteConfirmation.cs`; modify `Server/tests/WydCdk.Engine.Tests/Program.cs`.

**Interfaces:** `TinyCombineCompleteConfirmation.ToFrame(codec, tick, keyword, result, sceneId=30000)` emits type `0xC0A7` with one little-endian `int32` payload.

- [x] Add a wire test for checksum, type, scene id, size, and result value 2; build to confirm it fails because the confirmation type is missing.
- [x] Implement the confirmation and build the Protocol and Engine.Tests projects.

### Task 2: Preserve intermediate item updates

**Files:** Modify `Server/src/WydCdk.World/LegacyTinyCombineOutcomeBuilder.cs` and existing Tiny outcome assertions in `Server/tests/WydCdk.Engine.Tests/Program.cs`.

**Interfaces:** Add `LegacyTinyCombineItemUpdate(Position, Item)` snapshots to the outcome while preserving `ItemUpdatePositions` compatibility.

- [x] Assert exact per-send item values for success and failure, including repeated slot 2; build to confirm the new property is missing.
- [x] Capture a snapshot after each legacy clear/send point and after final sanctuary adjustment.

### Task 3: Ordered relay

**Files:** Create `Server/src/WydCdk.Server/LegacyTinyCombineRelay.cs`; create `Server/tests/WydCdk.Engine.Tests/TinyCombineRelayTests.cs`; register the test in `Program.cs`.

**Interfaces:** `LegacyTinyCombineRelay.TryBuild(outcome, etcFrame, codec, tick, keyword, headerId, successMessage, failureMessage, out frames)` returns the exact ordered frame list for each outcome.

- [x] Add success and failure sequence tests, including repeated slot updates; build to confirm the relay is missing.
- [x] Emit success as initial item updates, result item updates, Etc, message, completion 1, final target item. Emit failure as initial item updates, repeated slot 2, message, completion 2, Etc.

### Task 4: Listener integration and configuration

**Files:** Modify `Server/src/WydCdk.Server/Program.cs`, add `Server/src/WydCdk.World/LegacyTinyChanceConfiguration.cs`, update `Server/tests/WydCdk.Engine.Tests/TinyCombineRelayTests.cs`, `BACKLOG.md`, and ITEM-008 in `Server/docs/PORT_TRACEABILITY.md`.

**Interfaces:** Add `--comp-rate <CompRate.txt>` and `--tiny-chance-base` (default 15, range 0..100, command-line override); parse `TinyCombineRequest` only for a Playing session; prepare, roll 0..114, atomically apply, build Etc and relay frames, and roll back if relay construction fails.

- [x] Add request-dispatch assertions if a listener-level test seam is available; otherwise build the full solution and run Engine.Tests.
- [x] Handle stale items, bad recipes, and insufficient Gold with the 7.69 response values; retain existing source message text.
- [x] Load the `Tiny ChanceBase` entry from CompRate using the distributed casing, validate 0..100, and let the explicit command-line value override it.
- [x] Map invalid-position signal 900 to `MSG_QuitTrade` `0x0384`; add a Tiny-specific close entry that preserves session/identity checks but bypasses the HP gate, matching 7.69/W2PP cleanup at HP 0/-1. Keep C# pair/listing closure bilateral and durable; test both HP values and record the remaining unilateral `Trade`/`AutoTrade`/`TradeMode` visual difference. Represent 7.69 `MyLog(LogType::Combines)` with categorized server events and do not import W2PP `ItemLog` item-code lines absent from the 7.69 log path.

### Task 5: Guild treasury tax

**Files:** Modify `Server/src/WydCdk.World/WorldHub.TinyCombine.cs`, `WorldHub.cs`, and `LegacyTinyCombineOutcomeBuilder.cs`; add `Server/src/WydCdk.World/LegacyGuildTreasuryFileStore.cs`; modify `Server/src/WydCdk.Server/Program.cs` and `Server/tests/WydCdk.Engine.Tests/Program.cs`.

- [x] Add failing coverage for Merchant=6 registration, charge-guild assignment, success-only 10M EXP credit, the 200B threshold/overshoot, joint rollback, and raw NPC MOB persistence.
- [x] Register the newest generated Merchant=6 NPC by `CurrentScore.Level`, set its MOB guild from `ChargeGuild`, and attach the treasury snapshot to successful Tiny outcomes.
- [x] Include the treasury snapshot in Tiny CAS rollback and persist the latest live MOB atomically to `npc/<MobName>` under the configured NPC root; disk failure leaves the runtime credit intact and is logged.
- [x] Skip inactive NPCs, clear treasury registration on removal, and reject persistence for an ID that is no longer the zone's registered treasury; cover connection-ID reuse, replacement, and trailing-space filename mapping.
- [x] Retry registered treasury saves once per minute after a transient disk failure.
- [x] Keep the remaining source-specific Trade/AutoTrade/TradeMode display difference and visual client E2E as pending gates.
