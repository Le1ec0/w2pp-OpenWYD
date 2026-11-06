# Non-Generated NPC Type 22 Timer Implementation Plan

> **For agentic workers:** Implement inline in the current checkout and preserve unrelated dirty work.

**Goal:** Apply the existing 7.69 Type 22 countdown and expiry behavior to ordinary non-generated NPCs.

**Architecture:** Extend the WorldHub timer API and the existing sharded mob timer runtime. Select only persistent NPC snapshots with no generation index, owner, or autotrade state; do not schedule movement, combat, summons, or autotrade. Type 22 decrements on the Peace visit or the source-aligned fourth Combat shard visit and relays only the existing score refresh at expiry.

**Tech Stack:** C# / .NET 10, WorldHub, LegacyMobAiRuntime, Engine.Tests, TCP loopback smoke.

**Reference:** TMProject 7.69 `TMSrv/Server.cpp:6279-6895` and timer call sites in `ProcessSecMinTimer.cpp`; compare W2PP `Server.cpp:5213-5827` and matching timer calls. Both sources skip mob attack dispatch for Type 22 and use common finite affect countdown/score refresh.

## Steps

- [x] Add a failing Engine test for a non-generated Peace NPC retaining Type 22 instead of decrementing it.
- [x] Extend WorldHub's stable shard snapshot query and implement Type 22 countdown/expiry for eligible non-generated NPCs, preserving the generated/summon/autotrade paths.
- [x] Schedule the new timer on Peace and Combat shards without invoking non-generated NPC AI.
- [x] Add tests for Peace decrement/expiry and Combat fourth-visit cadence; expiry must clear all 8 bytes, leave HP unchanged, and request score refresh only.
- [x] Add synthetic TCP smoke asserting the two in-view peers receive UpdateScore at expiry and the outside peer receives no frame.
- [x] Run Engine.Tests, full solution build, smoke, and diff checks; update project Markdown in place. Do not package, publish, deploy, commit, or push.
