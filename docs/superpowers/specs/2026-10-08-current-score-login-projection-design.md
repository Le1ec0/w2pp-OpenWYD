# CurrentScore Login Projection Design

## Goal

Activate the audited current-score pipeline during character login without producing different score snapshots for the client and server. Derive one new character snapshot, project it through the existing 7.69 wire adapter, and install that same snapshot in the WorldHub.

This migration covers the score branches already audited and represented in `LegacyCurrentScoreMath`. It does not claim complete `BASE_GetCurrentScore` parity. Type 4 remains excluded because the selected common source and private TMSrv ABI are unresolved; other score effects not yet audited remain explicit gaps.

## Current Flow

- `CharacterLoginCoordinator.HandleAsync` loads `LegacyCharacterLoginData` and normalizes the KILL_MARK on a copy.
- The Host resolves login position, calls `W2ppCharacterLoginV1Adapter`, emits the login frame, registers the original W2PP MOB in `WorldHub`, and builds spawn and score frames from that original data.
- `CalculateCurrentScoreCoordinatorStage` is pure and returns intermediate snapshots. It does not yet produce a terminal MOB projection for login.
- The server currently permits authenticated startup without `--item-data`; the base/equipment score rebuild requires the item table.
- `WorldHub` state is the state later saved by the normal logout path. A derived MOB installed at login can therefore be persisted at logout without a write during login.

## Design

### Phase 1: Pure terminal projection for audited score branches

Add a pure World-layer projector that accepts the loaded character data and `LegacyItemDataTable`. It will:

- Work from copied MOB, Affect, MOBEXTRA, and equipment data; it will not mutate the loaded buffers or call a store.
- Resolve the 16-slot W2PP equipment plus any supplied versioned 18-slot equipment, then run the currently audited score stages in their 7.69 order.
- Close the terminal projection for supported outputs by composing the already audited final damage, AttackRun, scalar clamps, resistance clamps, Soul/Kibita, Haste, and transformation equipment results with the existing stage outputs.
- Apply only audited branches. Type 4 and other unaudited or unimplemented branches remain documented gaps; no formula will be inferred for them.
- Return a new `LegacyCharacterLoginData` with the derived MOB and equipment snapshot. Preserve unrelated fields and byte ranges.
- Reject invalid lengths or wire-narrowing failures explicitly. Do not silently fall back to the stored score after projection has begun.

The result represents the audited subset of 7.69 score behavior. Tests and documentation must not label it as full score parity.

Because this projection rebuilds CurrentScore from BaseScore, unsupported score-affect branches will not be re-applied when their fields are rebuilt. This is a known compatibility gap, not a pass-through guarantee. Do not preserve old derived fields selectively because that could apply supported affects twice. The project progress and traceability must name the omitted branches; a later slice can add them.

### Phase 2: One snapshot through active login

After a successful character read, the Host will create the derived snapshot once, before resolving the login position or sending frames. It will pass the same result to:

- `W2ppCharacterLoginV1Adapter` for the existing 7.69 login confirmation layout.
- `ResolveCharacterLoginPosition`, `BuildClientV769CreateMobFrame`, and `WorldHub.SetCharacterState`.
- The existing login `UpdateEtc`, `UpdateScore`, and `UpdateAffect` frame builders.

The 7.69 wire sizes and field layout remain unchanged. The account store is not written during login. The normal WorldHub logout save may persist the recalculated MOB, matching the runtime state used for combat after login.

### Configuration and failures

- In authenticated mode, require `--item-data` during startup validation before binding the listener. Diagnostic mode without an account store remains available without item data.
- If projection rejects malformed state or a value cannot fit the 7.69 output, do not add the character to the WorldHub or emit a partial login. Restore the session to character selection and log a structured reason without dumping MOB, Affect, or account payloads.
- There is no fallback to a different score calculation based on runtime configuration.

## Scope Boundaries

- Type 4 stays outside the projection until its source/ABI authority is resolved.
- Other unimplemented affects and score branches remain gaps; this work ports only audited behavior and records the boundary.
- No account schema, persistence timing, opcode, wire layout, or encoder change.
- No Site, production, deployment, commit, or push work.
- Visual client E2E remains a later validation layer; a local headless login smoke does not claim visual gameplay validation.

## Validation

- Pure projection tests verify ordered score stages, terminal output fields, equipment projection, narrowings, and input immutability.
- Login integration tests verify the same derived snapshot feeds the 7.69 login MOB, spawn/UpdateScore, and WorldHub state; logout persistence receives that runtime snapshot through the existing path.
- Startup tests verify authenticated mode rejects missing `--item-data` before the listener starts and diagnostic mode remains available.
- Failure tests verify malformed or unrepresentable output produces no partial world entry or login frames.
- Run Engine.Tests, a local headless login smoke, and a no-incremental solution build with the pinned SDK; record the final commands and outputs.
