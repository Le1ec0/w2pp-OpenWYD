# GetRecycleBin State Transition Plan

## Scope

Port the canonical 7.69 `GetRecycleBin` insertion transition over transient
world-session state and serve the client `0x03E8` recycle-bin display request.
Do not add account-file persistence or connect the sale handler in this slice.

## Tasks

- [x] Compare 7.69 and W2PP insertion algorithms and `_MSG_Sell` call sites.
- [x] Confirm the 10-entry `STRUCT_RECYCLE` shape and login-time clearing.
- [x] Add RED tests for vacancy selection, full-bin shift, guards and tick modulo.
- [x] Implement the immutable C# state transition.
- [x] Store the fixed ten slots on `WorldHub.Participant`, expose defensive snapshots, and discard them on `Leave`.
- [x] Dispatch `0x03E8` only for matching header ID and `USER_PLAY`; ignore client row data and project the session state.
- [x] Add isolation, leave/reset, authoritative-response, header-ID, and session-state tests.
- [x] Run Engine.Tests and the full solution build.
- [x] Record the two sandbox loopback failures and update project handoff docs.

## Validation

- The insertion transition, client projection, and session/request tests pass.
- Full `WydCdk.Engine.slnx` build: 0 warnings, 0 errors; log:
  `Server/Backup/Build/shop-recycle-session-build.log`.
- Engine.Tests with process-local `TEMP`/`TMP` under the writable checkout:
  569/571. The two remaining loopback tests are blocked by sandbox socket
  permissions; the default temp path also caused atomic `File.Move` failures.
  Log: `Server/Backup/Build/shop-recycle-session-tests.log`.
- SDK 10.0.401; `dotnet --info` exits 1 because access to Service Control
  Manager is denied.
- No schema, `_MSG_Sell` integration, authenticated Host smoke, visual E2E,
  deploy, commit, or push.

## Follow-up

Connect the session-owned `GetRecycleBin` transition to `_MSG_Sell`, then
complete buyback and its purchase gates/mutation/relay. Keep the recycle array
out of account-file persistence; both references clear it at login.
