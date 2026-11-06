# Non-Generated NPC Type 20 Timer Implementation Plan

> Implement inline in the current shared checkout and preserve unrelated dirty work.

**Goal:** Apply the source-backed Type 20 Poison HP tick and timer behavior to eligible non-generated NPCs.

**Architecture:** Extend the existing WorldHub timer helper. Reuse its Peace/Combat cadence, locked mob/affect snapshots, captured recipients, and relay outcome. Keep all other NPC and player paths unchanged.

**Reference:** TMProject 7.69 `Server.cpp:6210-6253`; W2PP `Server.cpp:5145-5187`; Peace and Combat timer callsites as recorded in Task 5CO/5CQ.

## Steps

- [x] Add failing Peace and targetless Combat tests for the Poison cap, signed HP delta, countdown/expiry, and four-opportunity cadence.
- [x] Extend the existing non-generated NPC timer branch with the mob Type 20 formula and HP/score outcome flags.
- [x] Add three-peer TCP loopback smoke for `SetHpDam` then `UpdateScore` and captured-view scoping.
- [x] Run Engine.Tests `495/495`, full no-incremental solution build, `--mob-ai-smoke`, and `git diff --check`.
- [x] Record source trace, evidence, limitations, and next handoff in project Markdown.

## Limits

Synthetic WorldHub/TCP evidence does not validate an authenticated Host or the visual client. Do not package, publish, deploy, commit, or push.

## Review

- Independent read-only review found no critical or important issues. Its minor suggestion to cover HP floor 1 and a zero-delta tick before expiry was addressed by the added regression case and confirmed in follow-up review; Engine.Tests pass `495/495`.
