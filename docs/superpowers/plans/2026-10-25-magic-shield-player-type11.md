# Escudo Magico Type 11 Player Score Projection Implementation Plan

> Execute inline in the existing dirty checkout; preserve unrelated work and do not commit, push, publish, or deploy.

**Goal:** Apply the audited Type 11 armor-class stage when skill 43 Escudo Magico changes a player affect, then restore baseline AC when it expires.

**Architecture:** Reuse `LegacyCurrentScoreLoginProjector` and `CalculateMagicShieldAffectStage`. Add one explicit predicate for skill 43 / InstanceType 0 / Aggressive 0 / AffectType 11; preserve live HP/MP through the existing score rebuild.

**Spec:** Compiled TMProject 7.69 skill row 43 and generic `SetAffect` score tail, compared against W2PP, recorded in `docs/PORT_TRACEABILITY.md` as Task 5DJ.

## Global Constraints

- TMProject 7.69 is the target. Type 11 adds `Level / 5 + Value` to AC; W2PP's `/3` remains comparison only.
- Keep the existing dirty checkout intact. No commit, push, publish, deploy, Site, or production change.
- Synthetic TCP loopback is not authenticated Host or visual-client E2E.

## Review Focus

- Skill 43 remains a non-aggressive effect-only Type 11 producer.
- Special4 supplies the affect level for skill 43.
- HP/MP remain unchanged while AC is rebuilt.
- Recast refreshes source-derived duration without stacking AC.
- Timer expiry clears Type 11 and restores baseline AC.

---

### Task 1: Reproduce the missing player score rebuild

**Files:**
- Create: `tests/WydCdk.Engine.Tests/AttackPlayerMagicShieldAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

- [x] Cover Type 11 value/level/time, AC projection, resources, recast, and timer expiry.
- [x] Run Engine.Tests and confirm RED is the unprojected player AC.

### Task 2: Enable the audited producer

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`

- [x] Add an exact skill 43 / InstanceType 0 / Aggressive 0 / AffectType 11 predicate to player score rebuild and audited affect mapping.
- [x] Run Engine.Tests and confirm AC applies and restores.

### Task 3: Validate wire smoke and record evidence

**Files:**
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`, `BACKLOG.md`, `PROGRESSO.md`, `AGENTS.md`, `docs/CLIENT_E2E_SMOKE.md`, `docs/PORT_TRACEABILITY.md`.

- [x] Add a three-peer loopback smoke for Type 11 UpdateScore/UpdateAffect and resource preservation.
- [x] Run Engine.Tests, E2E build, complete `--mob-ai-smoke`, and full no-incremental solution build.
- [x] Record compiled-source evidence, W2PP divergence, validation, and local-only limits.
