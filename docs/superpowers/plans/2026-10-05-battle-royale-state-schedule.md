# COMBAT-ATTACK-BR-SCHEDULE-001 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide the Host with an optional, locally configured Battle Royale schedule that publishes legacy states 0–3 to the `bParty` gate.

**Architecture:** Parse a typed configuration and keep the state transitions in a deterministic World-domain schedule. A small controller applies each transition atomically to `WorldHub`; the Host owns only the optional two-second polling loop and cooperative shutdown.

**Tech Stack:** C# / .NET 10, `WydCdk.Engine.Tests` console suite, `WydCdk.Server` headless host, PowerShell loopback smoke.

**Spec:** `docs/superpowers/specs/2026-10-05-battle-royale-state-schedule-design.md`

## Global Constraints

- TMProject 7.69 is the behavior authority; compare the touched branch with `Server/W2PP` and preserve the matching 0/5/13/14-minute transitions.
- The optional configuration is `--battle-royale-config <arquivo.json>` and requires `BRItem`, `BRHour`, `ServerIndex`, `NumServerInGroup`, `TestServer`, and `LocalServer`.
- Use local time, `fase = minuto % 20`, and `(diaDoMes - 1) % NumServerInGroup`; do not infer test/local flags from bind address, environment, or server mode.
- State is volatile and starts at 0. Do not catch up a process that starts mid-cycle or persist the scheduler state.
- Schedule transitions update only `BattleRoyaleState` and `BattleRoyaleItem` atomically under the WorldHub lock; preserve every other `LegacyCombatWorldState` field.
- This slice emits no event notices/frames and does not modify characters, parties, maps, doors, rewards, or score.
- Preserve the dirty checkout and unrelated user changes. Do not reset, stage, commit, push, publish to Site/production, or move/overwrite existing artifacts.
- Record `dotnet --info`; keep any smoke on isolated local loopback, verify its port is free first, and use only temporary non-secret configuration.

## Review Focus

- A missing property, wrong JSON type, invalid path, or out-of-range value must fail before the listener opens; test parser rejection and the headless startup gate in Tasks 1 and 3.
- A server selected by a one-based or weekday rotation would start on the wrong day; test zero-based `ServerIndex` against day-of-month rotation in Task 1.
- Loopback must not silently activate `TestServer`/`LocalServer`; test explicit flags and non-selected channels in Task 1 and valid Host configuration in Task 3.
- A process starting after minute 0 must remain in state 0 until a later eligible phase 0; test startup mid-cycle and skipped phases in Task 1.
- Publishing a transition must not overwrite a concurrent `DoubleModeMultiplier` or other world state; test atomic updates in Task 2.

---

### Task 1: Add typed Battle Royale configuration and deterministic schedule

**Files:**
- Create: `src/WydCdk.World/LegacyBattleRoyaleSchedule.cs`
- Create: `tests/WydCdk.Engine.Tests/BattleRoyaleScheduleTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Produces `LegacyBattleRoyaleConfiguration(int BRItem, int BRHour, int ServerIndex, int NumServerInGroup, bool TestServer, bool LocalServer)` with `static Parse(string json)`, `Validate()`, and `IsEligible(DateTime localNow)`.
- Produces `LegacyBattleRoyaleSchedule(LegacyBattleRoyaleConfiguration configuration)`, initialized with state 0, with `int CurrentState` and `LegacyBattleRoyaleTransition? Tick(DateTime localNow)`.
- Produces `LegacyBattleRoyaleTransition(int PreviousState, int State, int BRItem)` only when an eligible state transition occurs.
- `Tick` changes 0→1 only at configured hour/phase 0, 1→2 at phase 5, 2→3 at phase 13, and 3→0 at phase >=14; it does not derive or catch up state from a later phase.

- [x] **Step 1: Write failing configuration tests.** Added `ConfigParsesAllRequiredFields`, `ConfigRejectsMissingOrWrongFields`, and `ConfigRejectsInvalidRanges`. Assertions cover the six required values, disabled/boundary values, missing/wrong JSON properties, malformed JSON, and invalid ranges.
- [x] **Step 2: Run the targeted Engine.Tests suite and confirm RED.** The runner failed to build because `LegacyBattleRoyaleConfiguration` is not defined yet (CS0103 at the new tests); the pre-existing CS8604 warning in `AttackDivineBlessingTests.cs:256` was also reported. Evidence: `.superpowers/sdd/2026-10-05-battle-royale-state-schedule/task-1-config-red.log`.
- [x] **Step 3: Implement the required-field parser and validation.** Read the six PascalCase properties explicitly so missing values cannot fall back to CLR defaults; wrap JSON syntax/type errors as `InvalidDataException` and preserve the validation ranges from the spec.
- [x] **Step 4: Write failing deterministic schedule tests.** Added `ScheduleTransitionsAtPhasesZeroFiveThirteenAndFourteen`, `ScheduleRepeatsDoNotRepeatTransitions`, `ScheduleStartsMidCycleAtZero`, `ScheduleDoesNotCatchUpSkippedFiveOrThirteen`, and `ScheduleUsesDailyServerRotationAndExplicitBypasses`. Cover day 1 and day 3 with a three-server group, selected/non-selected `ServerIndex`, both flags independently, `BRItem=0`, wrong hour, and repeated ticks within a phase.
- [x] **Step 5: Run the targeted tests and confirm RED.** Confirmed compile failure because `LegacyBattleRoyaleSchedule` and `LegacyBattleRoyaleTransition` were absent. Evidence: `.superpowers/sdd/2026-10-05-battle-royale-state-schedule/task-1-schedule-red.log`.
- [x] **Step 6: Implement `Tick(DateTime localNow)`.** Keep the instance state at 0 initially; use `BRItem > 0`, local day-of-month rotation, explicit bypass booleans, configured hour, and the exact state-dependent phases from the spec. Return no transition when the state and observed phase do not match.
- [x] **Step 7: Run the targeted tests, then the full Engine.Tests suite.** Expected: all new parser/schedule tests and existing tests pass.

### Task 2: Publish transitions atomically to WorldHub

**Files:**
- Modify: `src/WydCdk.World/WorldHub.cs`
- Create: `src/WydCdk.World/LegacyBattleRoyaleScheduleController.cs`
- Modify: `tests/WydCdk.Engine.Tests/BattleRoyaleScheduleTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Produces `WorldHub.SetBattleRoyaleState(int state, int item)`, which copies the current combat state and changes only `BattleRoyaleState`/`BattleRoyaleItem` inside one `lock (gate)`.
- Produces `LegacyBattleRoyaleScheduleController(LegacyBattleRoyaleConfiguration configuration, WorldHub world)` with `LegacyBattleRoyaleTransition? Tick(DateTime localNow)`. It consumes the Task 1 schedule and publishes each returned transition through `SetBattleRoyaleState`.

- [x] **Step 1: Added `ControllerPublishesStateAndItemWithoutOverwritingWorldFields`.** Seed a non-default `LegacyCombatWorldState`, tick phases 0/5/13/14, and assert the world receives each matching state plus configured item while `DoubleModeMultiplier`, `RvrState`, `CastleState`, `GuildTowerState`, and all remaining state fields remain unchanged.
- [x] **Step 2: Run the test and confirm RED.** Confirmed compile failure CS0246 for the absent controller; the existing CS8604 warning remained. Evidence: `.superpowers/sdd/2026-10-05-battle-royale-state-schedule/task-2-controller-red.log`.
- [x] **Step 3: Implement `SetBattleRoyaleState` as one locked record-copy update.** Do not implement this as a separate `GetCombatWorldState` followed by `SetCombatWorldState`; that sequence can overwrite a concurrent update.
- [x] **Step 4: Implement the controller to publish only actual transitions.** Repeated/no-op ticks must not call the WorldHub setter or create transition logs.
- [x] **Step 5: Run controller test and full Engine.Tests suite.** Controller/world assertions passed, including preservation of every other combat-state field; full suite 377/377. Evidence: `.superpowers/sdd/2026-10-05-battle-royale-state-schedule/task-2-controller-green.log`.

### Task 3: Wire optional configuration and cooperative Host loop

**Files:**
- Modify: `src/WydCdk.Server/Program.cs`

**Interfaces:**
- Adds optional `BattleRoyaleConfigPath` to `ServerOptions` and recognizes `--battle-royale-config <path>`; adding it at the end of the record must preserve existing positional/default constructors.
- Loads and validates `LegacyBattleRoyaleConfiguration` before `TcpListener.Start()`.
- Adds `RunBattleRoyaleScheduleLoopAsync(WorldHub world, LegacyBattleRoyaleConfiguration configuration, CancellationToken cancellationToken)` using a two-second `PeriodicTimer`, `LegacyBattleRoyaleScheduleController`, and `DateTime.Now`.
- Starts no loop without the config option; with config, the Host logs startup/transition state but sends no event frames and performs no Battle Royale side effects.

- [x] **Step 1: Add the CLI option and startup logging.** The optional path is appended to `ServerOptions`; config is parsed before listener construction/start. No config logs disabled; valid config logs hour/item/rotation/explicit bypass values.
- [x] **Step 2: Add the optional schedule loop and shutdown await.** The Host uses a two-second `PeriodicTimer`, `DateTime.Now`, cancellation from the shutdown token, transition-only logging, and awaits the task in cleanup.
- [x] **Step 3: Build the Host project.** Build succeeded with 0 warnings and 0 errors. Output: `Backup/Build/AttackBattleRoyaleStateSchedule-20261005/host-project-build.log`.
- [x] **Step 4: Run headless loopback smoke with temporary config.** Debug and self-contained runs on port 18283 accepted TCP connects with config absent/valid; malformed JSON, missing file, and BRHour=24 exited before binding. The valid processes stopped cooperatively; Ctrl+C closed the Debug listener/log, and stdin `stop` exited 0. Trace and per-case outputs: `Backup/Build/AttackBattleRoyaleStateSchedule-20261005/`.
- [x] **Step 5: Review smoke output and WorldHub assertions.** Smoke trace contains no Battle Royale frames/notices or full-event side effects; only the already-tested state/item pair is published by controller transitions.

### Task 4: Complete verification and update project records

**Files:**
- Modify: `docs/PORT_TRACEABILITY.md`
- Modify: `docs/CLIENT_E2E_SMOKE.md`
- Modify: `../AGENTS.md`
- Modify: `../PROGRESSO.md`
- Modify: `../BACKLOG.md`
- Write evidence under: `Backup/Build/AttackBattleRoyaleStateSchedule-20261005/`

**Interfaces:**
- Records the matching 7.69/W2PP schedule, configuration contract, volatile startup behavior, state-only boundary, test/build/Host smoke evidence, and remaining full-event effects without claiming visual E2E.
- Updates the existing root context/checklist/backlog sections in place; do not create a `CONTEXTO_*.md` file.

- [x] **Step 1: Capture SDK and run required local gates.** From `Server`, ran `dotnet --info`, full `Engine.Tests`, and `dotnet build .\WydCdk.Engine.slnx --no-restore --nologo -v:minimal -m:1 -p:UseSharedCompilation=false`. SDK 10.0.401; final suite run after review passed 377/377 and solution build had 0 warnings/errors. One preceding run hit an unrelated randomized physical-attack fixture and is documented in traceability. Outputs are retained in the evidence directory.
- [x] **Step 2: Repeat Host smoke on Debug and self-contained runtime if the Host CLI smoke succeeds in Debug.** Published locally with `dotnet publish .\src\WydCdk.Server\WydCdk.Server.csproj -c Release -r win-x64 --self-contained true -p:UseSharedCompilation=false -o .\artifacts\publish-battle-royale-schedule-20261005`; Debug/self-contained runs covered disabled and valid config, TCP connect, cooperative shutdown, malformed/missing/out-of-range config before bind, and port release. No source, docs, secrets, or account fixtures were transferred.
- [x] **Step 3: Update traceability, smoke record, root checklist, backlog, and current context in place.** Recorded local validation of schedule/state production, the explicit active state 1/2/3 `bParty` gate regression, and the boundary that full Battle Royale effects and visual client E2E remain pending.
- [x] **Step 4: Review evidence and worktree.** Rechecked `git status`, `git diff --check`, smoke cleanup and port release; preserved all unrelated dirty files without staging or committing.

## Plan Self-Review

- **Spec coverage:** required configuration fields/ranges and fail-before-listener behavior are in Tasks 1 and 3; time/day eligibility and state-dependent 0/5/13/14 transitions are in Task 1; atomic state/item publication and preservation of other world fields are in Task 2; optional Host polling, local clock, no-config behavior, and cooperative shutdown are in Task 3; local tests, smoke evidence, project docs, and the full-event boundary are in Task 4.
- **Step scan:** each task separates RED assertions from the minimal implementation and its verification. The plan does not authorize broad refactors or unrelated combat behavior.
- **Type consistency:** Task 1 defines configuration/schedule/transition APIs; Task 2 consumes them through the controller and `WorldHub.SetBattleRoyaleState`; Task 3 consumes the controller from the Host loop.
- **Review focus:** malformed configuration and early failure map to Tasks 1/3; rotation and explicit flags map to Task 1; startup mid-cycle and skipped phases map to Task 1; atomic preservation maps to Task 2; cancellation and listener cleanup map to Task 3.
- **Proportion:** four tasks cover the typed schedule, the WorldHub bridge, Host wiring, and evidence/docs without implementing full event effects.

## Approved follow-up: Battle Royale grid refresh and party cleanup (06/10/2026)

This follow-up was approved after the schedule-only plan completed. It implements
the existing TMProject 7.69/W2PP sequence for 0->1 and 1->2: optional level
recall first, then `SendGridMob` for players still in the inclusive arena, then
party removal only for non-leader members still present. It covers the third
minute bucket, which refreshes/cleans party without `ClearAreaLevel`. Door
cleanup, environmental damage, winner/score, and complete reward remain outside.

- [x] Add failing schedule, spatial snapshot, order, party membership, and
  post-recall tests; observe missing transition/relay/WorldHub APIs as RED.
- [x] Capture player/NPC/summon and map/ground-item views; encode CreateMob,
  eligible PKInfo (`MAX_USER=1000`) and CreateItem; remove only current members.
- [x] Match `RemoveParty` summon cleanup: despawn the removed member's summons,
  send RemoveMob to current viewers, and suppress stale CreateMob frames in
  recipient snapshots that were captured earlier.
- [x] Isolate IO/disposed send failures per recipient; preserve later party
  cleanup and report failed endpoint IDs to the Host.
- [x] Wire Host execution after notice and optional area-clear relay.
- [x] Validate Engine.Tests 386/386 and full solution build with SDK 10.0.401;
  update project records and retain logs under
  `Backup/Build/AttackBattleRoyaleGridRefresh-20261006/`.

One initial full-suite rerun failed the existing randomized `world skill attack`
resurrection-scroll assertion (384/385); the following complete run passed
385/385. No Host/TCP smoke at the real transition time or visual E2E was run.
