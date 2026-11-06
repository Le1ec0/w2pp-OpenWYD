# MSG_ApplyBonus BonusType=2 Implementation Plan

> **For agentic workers:** execute this plan task by task and keep its checkboxes current.

**Goal:** Port the confirmed TMProject 7.69 `_MSG_ApplyBonus` skill-learning branch for base skill-tree positions 0..23.

**Architecture:** Parse the existing `0x0119` request in Protocol; route BonusType=2 from the current Server dispatcher; validate and mutate the signed 7.69 MOB fields under the WorldHub gate; return snapshots for ordered `UpdateScore` then `UpdateEtc` frames. Keep rules/messages testable and keep persistence adapters untouched.

**Sources:** TMProject 7.69 client `TMFieldScene.cpp:16588-16595`, TMSrv `Basedef.h` (`MAX_USER=1000`, `MAX_MOB=25000`, signed `STRUCT_MOB.SkillBonus`), `TMSrv/_MSG_ApplyBonus.cpp`, `TMSrv/CMob.cpp:3124-3129`, and `Server/TMSrv/run/Language.txt`; compare `Server/W2PP/Source/Code/TMSrv/_MSG_ApplyBonus.cpp` and `Basedef.h`.

## Scope and compatibility decisions

- `Detail=5000..5095` selects one of 4 class trees and 24 positions; skill cost comes from `g_pSpell[skillPosition + 24 * skillClass].SkillPoint`, and requirements from `g_pItemList[Detail]`.
- The `TargetID` is the skill-master MOB ID. Preserve the source range `1000 <= TargetID < 25000`; an out-of-range target or detail gets no response.
- Mutate the first 32-bit `MOB.LearnedSkill` word, signed 16-bit `SkillBonus`, and Coin for final skills; allow the explicit `CurrentScore` projection. Do not write `LearnedSkill[1]` or `MOBEXTRA.SecLearnedSkill`.
- Preserve the 7.69 final-skill fees: Mortal 10,000,000; Arch 50,000,000; other class masters 100,000,000. W2PP charges 50,000,000 for every class master; record but do not port that divergence.
- Preserve source check order and Portuguese `Language.txt` responses. Success projects current score while preserving current HP/MP and sends `UpdateScore` before `UpdateEtc`.
- No schema, persistence adapter, Site, visual E2E, commit, push, or deploy changes.

## Tasks

### 1. Contract tests first

- [x] Add Engine.Tests coverage for skill-cost lookup, class/target/detail gates, signed skill points, requirements, duplicate learning, final-skill prerequisite/one-only/cost rules, class-master fee/point overrides, state-word isolation, score projection, and message text.
- [x] Run the focused build/test to observe the missing World API before implementing it.

### 2. World behavior and Server wire response

- [x] Add a pure skill-learning rule/result unit and `WorldHub.TryApplySkillBonus` under the existing gate.
- [x] Route BonusType=2; encode success frames in 7.69 order and mapped failure messages; retain the existing dead-character HP-mode/crack response and silent invalid target/detail behavior.
- [x] Confirm no bytes outside the approved MOB fields changed: the test permits only the base mask, SkillBonus, final-skill Gold, and projected CurrentScore; MOBEXTRA is asserted unchanged. No second learned word exists in this blob adapter.

### 3. Local validation and handoff

- [x] Build the complete solution and run Engine.Tests; record exact results and SDK evidence. Build passed (0 warnings/errors); Engine.Tests `514/557`, with 43 failures including file/ACL, fixture, and loopback socket restrictions. SDK 10.0.401 was identified; `dotnet --info` exited 1 on Service Control Manager access denial.
- [x] Update root `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`, and `docs/PORT_TRACEABILITY.md` in place. Record the W2PP fee divergence and leave visual client E2E/save-reload explicitly pending.
- [x] No commit, push, deploy, or visual E2E was performed.
