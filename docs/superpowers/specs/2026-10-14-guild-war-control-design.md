# GuildWar control port — design

**Status:** approved and implemented locally on 2026-10-14; see the implementation plan and `Server/docs/PORT_TRACEABILITY.md` for validation and limits.

## Goal

Port the TMProject 7.69 GuildWar request and state transitions into the C# host
so the authoritative reciprocal guild-war map used by player and summon PK gates
is populated through gameplay. Keep the behavior in the existing modular
monolith; do not introduce a service or a database schema for this volatile
state.

## Source behavior

- `TMSrv/_MSG_War.cpp::Exec_MSG_War` accepts a request only when both guild IDs
  are in `1..65535`, the active character belongs to `Parm1`, and its
  `GuildLevel` is 9. The client request requires `Parm2 > 0`; zero is rejected.
- The source constant is `_MSG_War = 14 | FLAG_CLIENT2GAME | FLAG_DB2GAME |
  FLAG_GAME2DB`, which evaluates to full `Type=0x0E0E`. Its
  `MSG_STANDARDPARM2` is 20 bytes: the 12-byte message header followed by two
  signed 32-bit parameters (`Guild`, `Enemy`). The earlier shorthand `0x000E`
  names only the base opcode, not the full wire type.
- The selected client source under `Projects/TMProject` has no `_MSG_War` send
  site and no `_MSG_SendWarInfo` receive site. The server source declares these
  directions, but that alone does not prove the stock client UI exposes them.
  Headless tests can prove the server contract; visual client support remains
  unverified until an E2E call path is found or added to the client.
- `DBSrv/CFileDB.cpp` keeps one directed `Guild -> Enemy` entry and distributes
  updates to game servers. `TMSrv/Server.cpp::DoWar` treats a one-sided entry as
  a declaration. A request from the opposing guild makes the relationship
  reciprocal, broadcasts the started notice, and refreshes war information for
  active members of both guilds. The PK exception applies only to reciprocal
  entries.
- For a positive target, `DoWar` rejects a requester that already has a
  nonzero war pointer. Otherwise it accepts if the target points back to the
  requester; if not, it creates the requester's directed declaration, even when
  the target points to a different guild. Preserve this source behavior and
  cover the crossed-pointer case explicitly.
- `DoWar(..., 0)` supports cancellation internally: it clears both entries and
  refreshes active members if the relationship is reciprocal; otherwise it
  clears only the requesting guild's pending declaration. The player ingress
  handler rejects zero, so this is not a client cancellation packet. Cancellation
  must remain behind a trusted server-control entry point.
- Declarations/cancellations send the localized guild notices from the 7.69
  message table. Mutual acceptance also sends the global started notice.
  `SendWarInfo` uses type `168 | GAME2CLIENT | CLIENT2GAME` (`0x03A8`),
  `MSG_STANDARDPARM3` (24 bytes), `ID=30000`, and carries opponent guild, the
  configured Clan from `GuildZone[4]`, and ally guild.
- Notice text resolves guild names with `BASE_GetGuildName`: the server loads
  `Guilds.txt` when present and otherwise formats the source's guild-index/mark
  fallback using `ServerGroup`. The C# host currently has neither this resolver
  nor a `ServerGroup` option, so name resolution is part of this slice.
- W2PP has the corresponding handlers and directed map. For the PK exemption,
  its differences are already recorded in `PORT_TRACEABILITY.md`; the C# port
  follows TMProject 7.69.

## C# design

1. Add an explicit 7.69 `MSG_War` request DTO/parser in `WydCdk.Protocol`, with
   exact full type `0x0E0E` and 20-byte frame checks. Keep the wire fields
   unchanged. Treat successful headless injection as server support, not proof
   that the stock client sends this request.
2. Add a pure `LegacyGuildWarControl` transition in `WydCdk.World` for
   declare/accept/cancel. It returns the changed directed map and typed effects
   (localized guild/global notices and per-member `SendWarInfo` updates); it does
   not access sockets or persistence.
3. Let `WorldHub` apply each accepted transition under its existing lock and
   publish a copied, read-only `GuildWarPartners` snapshot. The transition must
   not change the map for invalid IDs, a non-leader, or a character whose current
   guild differs from the request.
4. Route the client request only for an authenticated active participant. Derive
   guild and guild level from the participant's MOB snapshot; never trust a
   client-supplied guild as authority. `Enemy=0` is rejected on this route.
5. Relay source-compatible notices and `_MSG_SendWarInfo` using existing 7.69
   frame conventions. For `_MSG_SendWarInfo`, encode type `0x03A8`, total size
   24, `ID=30000`, `Parm1=opponent guild`, `Parm2=GuildZone[4].Clan`, and
   `Parm3=0` because the C# host has no authoritative GuildAlly map. Keep
   alliance behavior outside this slice. Do not invent opcodes or mutate
   Guild/Ally state. The stock client's receive/display behavior must be verified
   separately with the compiled `WYD.exe`.
6. Add `LegacyGuildNameCatalog` to resolve `(ServerGroup, GuildId)` from an
   optional `Guilds.txt` in the source's `group server guild name` format, with
   an exact fallback port of the 7.69 guild-index/mark byte tables. Add typed
   `GuildNamesPath` and `ServerGroup` options; default `ServerGroup` to 0 for the
   current local sandbox and reject values outside `0..9`. Preserve the source
   message templates and byte encoding.
7. Provide cancellation only through an internal trusted World/Host control
   method, not by weakening the client request validation. Do not add an
   operator command or web endpoint in this slice.

The directed state remains in memory and resets with the host, matching the
reference's in-memory `g_pGuildWar` array. This slice covers one C# Host process;
it does not add the reference DBSrv's cross-process fan-out. If UP/PVP are run as
separate worlds that must share GuildWar state, that propagation needs a separate
design and validation before claiming cluster-wide equivalence.

## Acceptance and tests

- Golden/parser tests prove the server-declared `MSG_War` full type `0x0E0E`,
  total size 20, and
  `Guild`/`Enemy` parameter offsets. Reject wrong type, size, checksum, zero or
  out-of-range IDs.
- Domain tests cover declaration as one-sided state, reciprocal acceptance,
  cancellation of pending and reciprocal state, requester already mapped,
  target mapped to a third guild, and no mutation for invalid requests. Assert
  notices and war-info recipients match the 7.69 transition.
- Name-catalog tests cover a `Guilds.txt` hit, source fallback for a missing
  name, group bounds, and exact encoded text for the localized GuildWar notices.
- World tests prove the combat snapshot changes atomically: one-sided state does
  not exempt PK, reciprocal state does, and cancellation removes the exemption
  immediately for player and summon skill gates.
- Host/headless tests prove unauthenticated users, non-leaders, and spoofed
  `Parm1` cannot change state; valid leader requests produce the 7.69 relays.
- Run Engine.Tests and the solution build with the pinned .NET 10 SDK. Record
  `dotnet --info`; run the authorized local headless network smoke for the
  request/relay path. No visual E2E claim without the compiled `WYD.exe`.

## Out of scope

Guild alliance management, territory-war scheduling/results, persistent or
cross-process GuildWar replication, Site controls, and changes to the 7.69 wire
layout. No commit, push, deployment, or production changes.
