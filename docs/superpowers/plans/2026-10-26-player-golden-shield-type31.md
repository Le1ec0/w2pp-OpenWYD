# Player Golden Shield Type 31 Implementation Plan

> **For agentic workers:** Execute inline in the existing authorized checkout. Use test-first steps and preserve unrelated local changes. Do not commit, push, package, publish, or deploy.

**Goal:** Rebuild a player's CurrentScore after self-cast skill 85 applies its audited Type 31 AC affect.

**Architecture:** Reuse the existing Type 31 Golden Shield score stage and player login projector. Add an exact skill 85 matcher to the online player skill path; keep affect math, wire, persistence, and generic skill behavior unchanged.

**Tech Stack:** C# / .NET 10.0, `WydCdk.World`, Engine.Tests, three-peer TCP smoke.

**Spec:** `docs/PORT_TRACEABILITY.md`, Type 31 Golden Shield stage and skill 85 source audit; TMProject 7.69 is the behavior target, with `Server/W2PP` compared explicitly.

## Global Constraints

- Preserve the existing dirty checkout and work on the authorized current workspace.
- Skill 85 must match only `InstanceType=0`, `Aggressive=0`, and `AffectType=31`.
- Type 31 adds `Level / 2 + Value` to ArmorClass in both TMProject 7.69 and W2PP; do not change the shared score formula.
- Preserve current HP/MP when rebuilding the live player score; no schema, wire, persistence, or generic effect changes.
- Use the pinned .NET SDK 10.0.401 and validate the solution, Engine.Tests, and the matching network smoke.
- No commit, push, package, publish, deploy, Site, or production change.

## Review Focus

- Skill metadata mismatch: nonmatching skill IDs or flags must not gain the Type 31 projection.
- Recast: repeated casts must not stack AC.
- Expiry: the existing player affect timer must restore the baseline score.
- Resource preservation: rebuilding score must not overwrite live HP/MP.
- Relay scope: only the caster receives the self-target score and affect updates.

---

### Task 1: Add the audited skill 85 Type 31 runtime projection

**Files:**
- Create: `tests/WydCdk.Engine.Tests/AttackPlayerGoldenShieldAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`
- Modify: root `AGENTS.md`, `BACKLOG.md`, `PROGRESSO.md`, `docs/PORT_TRACEABILITY.md`, and `docs/CLIENT_E2E_SMOKE.md`

- [x] Add and register a regression for skill 85 self-cast: Type 31 value 150, Special3-derived level 50, AC +175, unchanged HP/MP, no stacking on recast, and baseline restoration on expiry.
- [x] Run Engine.Tests and confirm the new case fails because Type 31 is applied without a live score rebuild (`515/516`, AC stayed at 100).
- [x] Add the exact Type 31 skill matcher to player score rebuilding and the audited affect selector; leave the Type 31 score stage unchanged.
- [x] Add a three-peer TCP smoke that verifies UpdateScore and Type 31 UpdateAffect reach only the caster.
- [x] Run Engine.Tests (`516/516`), the TCP smoke, the no-incremental solution build (0 warnings/errors), and `dotnet --info`; SDK 10.0.401 identified, exit 1 due Service Control Manager access denial.
- [x] Update the project handoff, progress/backlog, source audit, and smoke traceability in place.
