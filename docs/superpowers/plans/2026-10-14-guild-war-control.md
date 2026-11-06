# GuildWar Control Implementation Plan

**Status:** completed locally on 2026-10-14; validation evidence is recorded below and in the SDD progress ledger.

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task by task. Keep the current dirty checkout intact; do not commit, push, deploy, or publish.

**Goal:** Feed the 7.69 directed and reciprocal GuildWar state through the C# host so existing PK gates and client notifications follow the reference.

**Architecture:** Parse the server-declared 7.69 `MSG_War` frame in Protocol, resolve transitions as pure World logic, apply state under the `WorldHub` lock, and relay resulting notices from the Host. The state remains volatile and local to one Host process; the client-source gap and cross-process replication remain explicit limits.

**Tech Stack:** C#/.NET 10, `WydCdk.Protocol`, `WydCdk.World`, `WydCdk.Server`, the existing `WydCdk.Engine.Tests` executable harness, and `WydCdk.Client.E2E` headless harness.

**Spec:** [2026-10-14-guild-war-control-design.md](../specs/2026-10-14-guild-war-control-design.md)

## Global Constraints

- Follow TMProject 7.69 where it differs from W2PP; record any newly found divergence.
- Full `_MSG_War` type is `0x0E0E`; the 20-byte frame carries two signed 32-bit guild IDs after the 12-byte header.
- The authenticated MOB supplies Guild and GuildLevel; client `Parm1` must match it, and GuildLevel must be 9.
- Client `Enemy=0` is invalid. Cancellation remains an internal trusted control operation.
- `GuildWarPartners` is directed state; only reciprocal entries exempt PK.
- GuildWar state is in-memory, local to one Host process, and is not added to persistence or cross-process transport.
- `MSG_SendWarInfo` uses type `0x03A8`, 24 bytes, `ID=30000`, parameters `(opponent guild, GuildZone[4].Clan, 0)`.
- Guild notices resolve names through optional `Guilds.txt` and the exact 7.69 index/mark fallback; `ServerGroup` is typed configuration, default 0 for this sandbox and valid only in `0..9`.
- Preserve unrelated modifications in the dirty `Server` checkout. Do not stage or commit.
- Keep the existing wire formats and opcodes. Do not modify Site, production, or release artifacts.
- Build/test evidence records `dotnet --info`; use the pinned SDK 10.0.401.

## Review Focus

- Direction flags produce the complete message type `0x0E0E`; `0x000E` is only the base opcode.
- A leader cannot spoof `Parm1` to mutate a different guild's state.
- A one-sided declaration never grants the reciprocal PK exception.
- Mutual acceptance and cancellation refresh only eligible active guild members and preserve notice scope.
- Guild notice text resolves names from the configured group and uses the source fallback when `Guilds.txt` lacks an entry.
- A headless request passing does not establish that the stock client UI sends or displays these messages.
- Multiple Host processes do not share GuildWar state in this scope.

## Files and Responsibilities

- `src/WydCdk.Protocol/GuildWarRequest.cs`: fixed-layout inbound DTO and parser/encoder.
- `src/WydCdk.Protocol/SendWarInfoConfirmation.cs`: fixed-layout `_MSG_SendWarInfo` encoder.
- `src/WydCdk.World/LegacyGuildWarControl.cs`: pure transition decisions and typed effects.
- `src/WydCdk.World/LegacyGuildNameCatalog.cs`: `Guilds.txt` lookup and 7.69 guild-index/mark fallback.
- `src/WydCdk.World/WorldHub.cs`: active-player authorization, locked state mutation, snapshots, notification recipient selection, and the existing `GuildZone[4].Clan` value.
- `src/WydCdk.Server/Program.cs`: typed `GuildNamesPath`/`ServerGroup` options, authenticated packet route, and frame relays. Reuse existing `--guild-data` for `GuildZone[4].Clan`; do not add a second Guild.txt option.
- `tests/WydCdk.Engine.Tests/GuildWarProtocolTests.cs`, `GuildWarControlTests.cs`, `GuildNameTests.cs`, and `Program.cs`: parser, catalog, and domain/WorldHub regressions.
- `tests/WydCdk.Client.E2E/GuildWarFixtureFactory.cs`, `GuildWarNetwork.cs`, `Program.cs`, and the E2E project file if needed: isolated TCP smoke with generated synthetic account/character files in a temporary root; no real account files.
- `docs/CLIENT_E2E_SMOKE.md`, `docs/PORT_TRACEABILITY.md`, root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md`: record the command, source comparison, evidence, and remaining client/cluster limits after implementation validates.

## Tasks

### 1. Add the 7.69 request and SendWarInfo wire adapters

- [x] Write `GuildWarProtocolTests.ServerWarRequestRoundTripsExactV769Frame` asserting `Type=0x0E0E`, `Size=20`, payload offsets 0/4 for signed `Guild`/`Enemy`, and checksum-valid parse.
- [x] Write `GuildWarProtocolTests.RejectsWrongTypeSizeChecksumAndGuildBounds` covering wrong type, truncated/oversized frame, invalid checksum, and IDs outside `1..65535`.
- [x] Write `GuildWarProtocolTests.SendWarInfoUsesExactV769Signal` asserting `Type=0x03A8`, `Size=24`, `ID=30000`, and three int32 parameters.
- [x] Register the new tests in `tests/WydCdk.Engine.Tests/Program.cs` and run the full Engine.Tests harness to confirm the new tests fail for missing adapters.
- [x] Implement `GuildWarRequest(int GuildId, int EnemyGuildId)` with `MessageType=0x0E0E`, `PacketSize=20`, `TryParse(DecodedFrame, out GuildWarRequest?)`, and `ToFrame(...)` following `InviteGuildRequest`.
- [x] Implement `SendWarInfoConfirmation(int OpponentGuildId, int Clan, int AllyGuildId)` with the exact constants and payload above.
- [x] Run `dotnet run --project tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore`; require all tests to pass.

### 2. Implement the pure GuildWar transition

- [x] Write `GuildWarControlTests.DeclarationCreatesOnlyTheRequestingGuildPointer` and `ReciprocalRequestAcceptsAndEmitsStartedEffects` against the intended `LegacyGuildWarControl` API; run Engine.Tests and confirm the expected missing behavior.
- [x] Write `GuildWarControlTests.RequesterAlreadyMappedIsRejected`, `TargetMappedElsewhereStillReceivesOneSidedDeclaration`, and cancellation tests for pending and reciprocal wars.
- [x] Implement pure request/cancel transition functions over an `IReadOnlyDictionary<int,int>`. Positive requests reject an already-mapped requester, accept when target points back, and otherwise create a directed declaration. Cancellation clears both directions only when reciprocal; otherwise it clears only the requester pointer.
- [x] Return typed effects for localized guild notices, the global started notice, and the guild IDs whose active members need `_MSG_SendWarInfo` refresh. Do not perform I/O.
- [x] Run the full Engine.Tests harness; require all transition and existing tests to pass.

### 3. Port GuildWar name lookup and notice formatting

- [x] Write `GuildNameTests.GuildsFileOverridesTheSourceFallback`, `MissingNamesUseV769GuildIndexAndMarks`, `UnderBarsBecomeSpaces`, and `RejectsInvalidServerGroup`; run Engine.Tests and confirm missing catalog behavior.
- [x] Port `BASE_GetGuildName` behavior into `LegacyGuildNameCatalog`: parse optional `Guilds.txt` entries `(group, server, guild, name)`, normalize underscores, and fall back to the exact byte tables `g_pGuildIndex`/`g_pGuildMark` from TMProject 7.69 when a name is absent. Assert at least one exact fallback name as a golden value.
- [x] Add typed `GuildNamesPath` and `ServerGroup` settings to `ServerOptions` (CLI: `--guild-names` and `--server-group`); default group to 0 for this local sandbox and reject values outside 0..9.
- [x] Add `LegacyGuildWarMessages` formatting with the exact 7.69 Language.txt entries 210, 211, 212, and 221; ensure Latin1 output is preserved by `MessagePanelConfirmation`.
- [x] Run Engine.Tests and require catalog and formatting tests to pass.

### 4. Connect WorldHub state and authorization

- [x] Write WorldHub tests for valid leader request, non-leader, mismatched `Parm1`, invalid IDs, and inactive participant; run Engine.Tests and confirm missing state changes fail.
- [x] Write snapshot tests: declaration is one-sided and does not exempt PK; reciprocal acceptance exempts it; cancellation removes the exemption immediately for player and summon skill gates.
- [x] Implement `WorldHub.TryApplyGuildWarRequest(int connectionId, int requestedGuildId, int enemyGuildId, out LegacyGuildWarUpdate? update)` deriving Guild/GuildLevel from the active participant and applying the transition under `gate`; keep Protocol types out of World.
- [x] Implement a trusted WorldHub cancellation method for internal Host control calls; do not route `Enemy=0` from the client.
- [x] Publish copied read-only `GuildWarPartners`; select active recipients from their current GuildId and carry `GuildZone[4].Clan` from the already loaded `LegacyGuildZoneState.Clans[4]` (zero if `--guild-data` is omitted). Set ally parameter to zero.
- [x] Run the full Engine.Tests harness; require all tests to pass.

### 5. Route requests and relay outcomes in the Host

- [x] Write a self-contained headless TCP smoke that creates two synthetic account/character fixtures under a unique temp root, starts the Server child process bound only to `127.0.0.1` on a free loopback port, logs both guild members in, submits a valid leader request frame, and checks directed declaration, reciprocal acceptance, notices, and `_MSG_SendWarInfo`; also submit a spoofed-guild request and confirm no state mutation. Include the temp `--account-review-root`, required `--item-data`, and generated `--guild-data`/guild-name inputs; always stop the child and remove only this smoke's verified temp root.
- [x] Run the new smoke before adding the handler and confirm it fails because the packet is not handled.
- [x] Add the authenticated `GuildWarRequest.TryParse` branch near the guild request handlers in `Program.cs`. Require `LoginSessionState.Playing`, then call the WorldHub request API.
- [x] Resolve guild names with the configured catalog; relay localized guild/global notices through `MessagePanelConfirmation` and per-member state through `SendWarInfoConfirmation`, preserving source recipients and IDs.
- [x] Add the `--guild-war-smoke` entry to the isolated E2E harness and fixture factory. It must not read or copy `TESTE`/`TESTE2`, any production account, or any real character file.
- [x] Run `dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-restore -- --guild-war-smoke --item-data ..\Client\7670\ItemList.bin` and require loopback completion with no external database or endpoint.

### 6. Validate the slice and update project context

- [x] Run `dotnet --info`; record SDK 10.0.401 and report any Service Control Manager permission failure without masking it.
- [x] Run `dotnet run --project tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore` and the GuildWar loopback smoke.
- [x] Run `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental --nologo -v:minimal -m:1 -p:UseSharedCompilation=false`; record warnings and errors.
- [x] Update `Server/docs/PORT_TRACEABILITY.md`, root `AGENTS.md`, `PROGRESSO.md`, and `BACKLOG.md` with the source decision, passing evidence, and client-source/cross-process limits.
- [x] Document the smoke invocation and its generated-only fixture boundary in `docs/CLIENT_E2E_SMOKE.md`.
- [x] After review identified possible relay reordering across concurrent handlers, serialize each transition with its full relay batch and pass the deterministic relay-order regression.
- [x] Confirm `git diff --check` and inspect `git status`; leave all changes unstaged and uncommitted.

## Interfaces

- `GuildWarRequest.TryParse` owns frame integrity, field decoding, and the wire guild-ID bounds; `WorldHub` and the pure transition repeat the semantic bounds before mutation.
- `LegacyGuildWarControl` owns only deterministic directed-map transitions and typed effects.
- `WorldHub` is the authority for active character identity and `GuildWarPartners`; Host code must not mutate the map directly.
- Host relay consumes effects returned by WorldHub and uses the existing Protocol frame encoders.


