# CurrentScore Login Projection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline execution or `superpowers:subagent-driven-development` for delegated execution. Implement each task test-first.

**Goal:** Rebuild the audited subset of 7.69 CurrentScore from BaseScore and equipped items at character login, then use one projected character snapshot for the login confirmation, world state, spawn, and login score frames.

**Architecture:** Add a pure World-layer projector over `LegacyCharacterLoginData` and `LegacyItemDataTable`. It will compose the existing ordered score stages, close their audited terminal outputs, patch a copied 816-byte W2PP MOB, and return a new login-data record. The Host will calculate this result once before login side effects and route the same record through the existing 7.69 adapters and `WorldHub`. Authenticated startup will require `--item-data`; diagnostic mode without account authentication remains available.

**Tech Stack:** C# / .NET 10.0, `WydCdk.World`, `WydCdk.Server`, `WydCdk.Engine.Tests`, `WydCdk.Client.E2E`, source-derived JSON fixtures.

**Spec:** `docs/superpowers/specs/2026-10-08-current-score-login-projection-design.md`

## Global Constraints

- Treat the compiled-in 7.69 common `Basedef.cpp` path as the behavior target and compare every covered branch with `Server/W2PP`; record each divergence and the selected behavior in `docs/PORT_TRACEABILITY.md`.
- Keep Type 4 and all other unaudited score branches outside this projection. Rebuilding CurrentScore from BaseScore does not preserve unsupported derived effects; record that compatibility gap and do not claim full `BASE_GetCurrentScore` parity.
- Preserve affect-slot order and the existing stage math in `LegacyCurrentScoreMath`. Measure any newly needed `MOBEXTRA` field offsets against the account-file ABI before using them; do not infer offsets from field declarations.
- Copy input MOB/Affect/MOBEXTRA/equipment before projection. Do not write account storage during login. Existing logout persistence may save the installed runtime state.
- Require a complete valid ItemList before binding in authenticated mode (`--accounts-root` or `--mariadb-account-config`). Keep unauthenticated diagnostic mode usable without ItemList.
- Preflight projection and 7.69 narrowing before emitting login frames, entering the WorldHub, or sending reconnect side effects. Projection failure must restore character selection and leave no partial participant.
- Keep 7.69 wire sizes and layouts unchanged. No schema, opcode, Site, production, deployment, commit, or push changes.
- Preserve all existing dirty work. Do not reset, stage, or overwrite unrelated files.
- Use SDK 10.0.401 and record `dotnet --info` with validation evidence. Run the full Engine.Tests suite and a no-incremental solution build; do not treat headless smoke as visual `WYD.exe` E2E.

## Review Focus

- Missing or invalid `--item-data`: authenticated startup exits before a listener binds; diagnostic startup without account authentication still starts.
- Invalid/truncated MOB, Affect, MOBEXTRA, or versioned equipment: projection rejects before mutating any source buffer or installing a world participant.
- Out-of-range values at signed/unsigned 7.69 fields: checked conversion fails before any login confirmation or spawn frame is written.
- Stale CurrentScore input: repeating projection from the same persisted BaseScore and affects is deterministic and does not add supported bonuses twice.
- Snapshot fan-out: the MOB in login confirmation, CreateMob, UpdateScore/UpdateEtc, WorldHub, and logout save all derive from the same projected `LegacyCharacterLoginData`.
- Unsupported Type 4 and other unimplemented branches: their old derived values are not silently passed through or replaced with guessed formulas.

---

### Task 1: Add source-backed terminal projection vectors

**Files:**
- Create: `tests/WydCdk.Engine.Tests/CurrentScoreLoginProjectionTests.cs`
- Create: `tests/WydCdk.Engine.Tests/Fixtures/V769/current-score-login-projection.golden.json`
- Create: `tests/WydCdk.Engine.Tests/Fixtures/V769/current-score-login-projection.manifest.md`
- Modify: `tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `tools/PortAudit/StructLayoutProbe.cpp`
- Modify: `tools/PortAudit/Measure-StructLayouts.ps1`
- Read/compare: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, `TMSrv/CMob.cpp`, and `Server/W2PP/Source/Code/Basedef.cpp` / `TMSrv/CMob.cpp`

**Interfaces:**
- Test target: `LegacyCurrentScoreLoginProjector.Project(LegacyCharacterLoginData data, LegacyItemDataTable itemData)` returning a new `LegacyCharacterLoginData` with the derived MOB and equipment snapshot.
- Inputs: 816-byte W2PP MOB, 256-byte Affect, 552-byte MOBEXTRA, the 16 equipment records in MOB or the versioned 18-slot `ClientEquipment`, and an ItemList table.
- Expected outputs: a source-derived vector for each committed terminal MOB field, plus explicit preservation assertions for unrelated byte ranges.

- [x] **Step 1: Trace the login call order and terminal score writes in the selected 7.69 source and W2PP.** Record which existing C# stages supply each input and which final fields still need composition. Measure `Soul` and any other missing MOBEXTRA offsets with the compiled layout probe; compare the result to the W2PP account ABI before adding constants.
- [x] **Step 2: Add synthetic golden vectors for baseline equipment, ordered mixed affects, Soul/Kibita, resistance, transformation, a boundary conversion, and Type 4 alongside audited affects.** Seed distinct stale CurrentScore values so the test proves supported fields rebuild from BaseScore and unsupported Type 4 is not passed through. Include input bytes/state and expected final supported MOB fields. State in the manifest that vectors are source-derived, not live-player captures or full parity evidence.
- [x] **Step 3: Register the fixture and a test that calls the proposed projector.** Assert the returned data differs only in supported score/equipment fields, the original arrays remain byte-identical, and two projections from the same input produce identical output.
- [x] **Step 4: Run Engine.Tests and confirm the new test fails because the terminal projector is not implemented yet.**

  Run: `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`

  Expected: the new CurrentScore login projection assertion fails; existing tests remain unchanged.

---

### Task 2: Implement the pure terminal projector

**Files:**
- Create: `src/WydCdk.World/LegacyCurrentScoreLoginProjector.cs`
- Modify: `src/WydCdk.World/LegacyCurrentScoreMath.cs` only if a missing terminal composition result is needed
- Modify: `src/WydCdk.World/LegacyAccountSnapshot.cs` only for offsets confirmed by the compiled ABI probe
- Modify: `tests/WydCdk.Engine.Tests/CurrentScoreLoginProjectionTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/current-score-login-projection.golden.json`

**Interfaces:**
- `public static LegacyCharacterLoginData Project(LegacyCharacterLoginData data, LegacyItemDataTable itemData)` validates input sizes and equipment shape, invokes `CalculateCurrentScoreCoordinatorStage` plus the existing final damage, AttackRun, resistance, and scalar-clamp helpers, and returns copied/projected login data.
- The projector writes only measured 7.69/W2PP MOB fields: CurrentScore, the already audited derived ability fields (Magic, AttackRun, Critical, SaveMana, Regen, Resistance), learned-skill bits, RSV, and transformation equipment changes.
- Preserve current HP/MP according to the existing base/equipment rebuild contract, then apply the final HP/MP bounds after the derived maxima are known.

- [x] **Step 1: Extend the golden test with malformed input cases.** Cover short MOB, Affect, or MOBEXTRA; invalid 18-slot equipment length; and a terminal value that cannot fit its target field. Assert each returns a specific argument/projection failure and leaves every input byte unchanged.
- [x] **Step 2: Run Engine.Tests and verify the added tests fail against the absent projector or the current intermediate-only stages.**
- [x] **Step 3: Implement `LegacyCurrentScoreLoginProjector.Project` as a pure copy-transform-return function.** Use BaseScore as the rebuild seed; preserve the 7.69 affect order; compose final damage, attack/run, scalar, resistance, Soul/Kibita, Haste/RSV, and transformation equipment outputs only where source-backed stages exist. Use checked narrowing for serialized fields and reject unsupported equipment shapes rather than truncating them.
- [x] **Step 4: Run Engine.Tests and verify all new projection vectors, immutability checks, deterministic-repeat checks, and malformed-input cases pass.**
- [x] **Step 5: Run the existing isolated score tests.** Confirm Type 34/35/8, transformation, resistance, Soul/Kibita, and ability-stage results have not regressed.

  Run: `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`

  Expected: all score projection and existing Engine.Tests cases pass.

---

### Task 3: Route one projected snapshot through character login

**Files:**
- Modify: `src/WydCdk.Server/Program.cs`
- Modify: `tests/WydCdk.Engine.Tests/CurrentScoreLoginProjectionTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Create: `tests/WydCdk.Client.E2E/CurrentScoreLoginProjectionNetwork.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs` (the `E2eOptions` record and parser are declared inline there)

**Interfaces:**
- After `CharacterLoginCoordinator.HandleAsync` succeeds, calculate `projectedData` exactly once using the loaded `data` and the startup-loaded ItemList.
- Pass only `projectedData` into `W2ppCharacterLoginV1Adapter.Adapt`, `ResolveCharacterLoginPosition`, `BuildClientV769CreateMobFrame`, `WorldHub.SetCharacterState`, and the login UpdateEtc/UpdateScore/UpdateAffect builders.
- Build and validate the login confirmation and projection-dependent frames before socket writes, `WorldHub.EnterAsync`, or autotrade reconnect side effects. On a projection/narrowing failure, call `CompleteCharacterRefresh`, log only account/slot/reason type, and continue without adding a participant.

- [x] **Step 1: Add a headless smoke option that parses login confirmation and subsequent CreateMob/UpdateScore/UpdateEtc frames against the golden projection values.** Use distinctive BaseScore, stale CurrentScore, Affect, and equipment values; run against the current loopback Host to establish that the score projection assertions fail because it still returns the stored score.

  Run: `dotnet run --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-restore -- --host 127.0.0.1 --port 18283 --account-file <temporary synthetic account file> --current-score-login-projection-smoke`

  Expected: RED reports the stored-vs-projected score mismatch; no source account fixture is used or modified.
- [x] **Step 2: Update the Host to calculate one `projectedData` value and use it at every login fan-out site.** Move projection and adapter narrowing ahead of any login/network/world side effect. Keep the store read-only during login.
- [x] **Step 3: Run the same smoke and confirm all decoded login outputs reflect the golden projected values.** Confirm the Host path no longer references the original `data` after projection except for diagnostics that do not expose payloads.
- [x] **Step 4: Run Engine.Tests and build the E2E harness.**

  Run: `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`

  Run: `dotnet build .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-restore`

  Expected: engine tests pass and the new smoke option compiles.

---

### Task 4: Require ItemList before authenticated startup

**Files:**
- Modify: `src/WydCdk.Server/Program.cs` (`ServerOptions.Parse` and startup validation)
- Create: `tests/WydCdk.Engine.Tests/CurrentScoreLoginStartupTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Authentication is enabled when `--accounts-root` or `--mariadb-account-config` is supplied.
- Reject authenticated configuration without `--item-data` during option validation, before the listener is created. Continue to load and validate the specified ItemList before binding.

- [x] **Step 1: Add process-level tests for authenticated startup without `--item-data`, authenticated startup with a missing ItemList path, and diagnostic startup without account authentication or ItemList.** Assert the first two exit before binding the requested loopback port and the diagnostic process can bind it.
- [x] **Step 2: Run Engine.Tests and verify the missing-ItemList authenticated-startup case fails against current option parsing.**
- [x] **Step 3: Add the startup requirement with a clear `--item-data` error.** Keep the check before listener construction and do not make ItemList a requirement for the unauthenticated diagnostic mode.
- [x] **Step 4: Run Engine.Tests and confirm all startup cases pass, including process cleanup on assertion failure.**

  Run: `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`

  Expected: authenticated mode fails before bind when data is absent or unreadable; diagnostic mode without ItemList still starts.

---

### Task 5: Validate network fan-out and logout persistence

**Files:**
- Modify: `tests/WydCdk.Client.E2E/CurrentScoreLoginProjectionNetwork.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs` (the `E2eOptions` record and parser are declared inline there)
- Modify: `docs/CLIENT_E2E_SMOKE.md`

- [x] **Step 1: Run the new loopback smoke against a Host process configured with a temporary copy of a synthetic account, a temporary account-review root, and the local ItemList.** Verify the login MOB, CreateMob, UpdateScore, and UpdateEtc carry the expected projected values and valid 7.69 frame checksums.
- [x] **Step 2: Exercise projection rejection with a synthetic score that cannot fit a 7.69 output field.** Confirm the client receives no login confirmation/spawn and a second loopback observer receives no CreateMob for the rejected character. Truncated-array rejection remains covered by the pure projector tests because fixed-layout account files cannot encode short per-slot buffers.
- [x] **Step 3: Complete normal logout, reread the disposable account copy, and verify the existing logout persistence path saved the same derived MOB used at runtime.** Verify the original fixture and ItemList hashes remain unchanged.
- [x] **Step 4: Rerun the smoke from fresh temporary copies to verify that relogin does not apply supported score bonuses twice.**
- [x] **Step 5: Record exact commands, temporary-fixture identity, hashes, and results in `docs/CLIENT_E2E_SMOKE.md`.** Explicitly mark this as headless wire/login evidence, not visual gameplay validation.

  Run: `dotnet run --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-build --no-restore -- --host 127.0.0.1 --port 18283 --account-file <temporary synthetic account file> --current-score-login-projection-smoke`

  Expected: the disposable-account smoke prints `CLIENT_E2E_PASS current-score-login-projection` and all assertions/cleanup succeed.

---

### Task 6: Update project traceability and validate the full slice

**Files:**
- Modify: root `AGENTS.md`
- Modify: root `BACKLOG.md`
- Modify: root `PROGRESSO.md`
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `tests/WydCdk.Engine.Tests/Fixtures/V769/current-score-login-projection.manifest.md`
- Create: `Backup/Build/CurrentScoreLoginProjection-20261008/` validation logs

- [x] **Step 1: Update `PORT_TRACEABILITY.md` with the selected-source/W2PP comparison, covered score blocks, Type 4 and unaudited gaps, ABI/narrowing limits, and evidence locations.**
- [x] **Step 2: Update root AGENTS, BACKLOG, and PROGRESSO in place.** Mark only the audited login projection as integrated; keep full score parity and visual E2E pending.
- [x] **Step 3: Record the final `dotnet --info` output.**

  Run: `dotnet --info`

- [x] **Step 4: Run the full Engine.Tests suite, the local headless login smoke, and a no-incremental solution build.** Save complete stdout/stderr and exit codes in `Backup/Build/CurrentScoreLoginProjection-20261008/`.

  Run: `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`

  Run: `dotnet build .\WydCdk.Engine.slnx --no-incremental`

  Expected: Engine.Tests pass, the loopback smoke reports `CLIENT_E2E_PASS`, and the solution build exits 0. Record any pre-existing warning separately; do not claim visual E2E.

- [x] **Step 5: Review `git status --short` and the final diff.** Confirm only task files changed, unrelated dirty work is preserved, no secrets or account payloads entered logs, and nothing was staged or committed.

**Completion checks:** one projected record feeds every login output and the WorldHub; authenticated startup cannot bind without valid ItemList; all tests/build/smoke evidence is recorded; documented gaps remain explicit; no deploy, commit, or push occurred.
