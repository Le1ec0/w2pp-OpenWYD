# Action Account Analysis Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the 7.69 high-severity Action-speed detection as durable account Analysis, disconnect the triggering session, and reject later account logins.

**Architecture:** Add an account-review persistence port and a versioned atomic file implementation, separate from password/account storage. Check the review state before authentication, and route qualifying Action violations through the store before movement can mutate or relay. Keep the current solution layout and use the existing loopback harness patterns.

**Tech Stack:** C#/.NET 10, `WydCdk.Engine.Tests` custom runner, `WydCdk.Client.E2E`, PowerShell loopback harness.

**Spec:** `docs/superpowers/specs/2026-09-28-action-account-analysis.md`

## Global Constraints

- Only the 7.670 client is accepted; 7.69 is the wire/behavior reference and W2PP is a comparator.
- Analysis triggers only when `requestedSpeed > (AttackRun & 0xF)` and (`requestedSpeed >= 20` or full authoritative `AttackRun >= 300`).
- Use a versioned account-review sidecar under required `--account-review-root`; do not reuse raw `Ban/*.bin` or modify the Site account schema.
- Missing, invalid, or unavailable review state must never become a permissive login or movement path.
- Write tests with each task, but execute the test/build/network battery together only at the end of the block, as the user requested.
- Preserve the dirty `Server` checkout and existing user changes. No reset, staging, commit, push, Site/production operation, or secret output.
- After all local gates pass, publish only the runtime artifact to the already-authorized test sandbox, with backup/hash/listener checks; no source or secrets.

## Review Focus

- Invalid account names and path traversal must never create or escape a review path — pin in `AccountReviewStoreTests.InvalidNamesNeverResolveOutsideRoot`.
- A truncated/unknown-version record must deny login rather than look unmarked — pin in `AccountReviewStoreTests.InvalidRecordsFailClosed` and `AccountReviewLoginTests.ReviewReadFailureClosesLogin`.
- High score alone must not mark an account when the packet speed is allowed — pin in `AccountReviewActionTests.HighScoreWithoutSpeedViolationIsNotFlagged`.
- The inclusive thresholds must use full authoritative `AttackRun`, and an Action that also crosses the shared crack-error logout threshold must still take the Analysis path — pin in `AccountReviewActionTests.ThresholdsAndCrackErrorBoundary`.
- The 7.69/W2PP server headers currently declare `STRUCT_SCORE.AttackRun` as an 8-bit `BYTE`; do not widen the wire or host score to make the `>=300` branch reachable. Preserve the pure predicate boundary, test the runtime maximum, and document that `requestedSpeed>=20` is the reachable severe gate under this ABI.
- Concurrent marks must preserve the first evidence, and a write failure must stop movement — pin in `AccountReviewStoreTests.ConcurrentMarkIsIdempotent` and the Action failure case in `AccountReviewActionTests`.

---

### Task 1: Account-review contract and durable file store

**Files:**
- Create: `src/WydCdk.World/AccountReviewStore.cs`
- Create: `src/WydCdk.World/LegacyAccountNameRules.cs`
- Create: `src/WydCdk.World/FileAccountReviewStore.cs`
- Modify: `src/WydCdk.World/LegacyFileAccountStore.cs`
- Modify: `src/WydCdk.World/MariaDbAccountAuthenticator.cs`
- Test: `tests/WydCdk.Engine.Tests/AccountReviewStoreTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Produce `IAccountReviewStore.ReadStatusAsync(string accountName, CancellationToken) -> ValueTask<AccountReviewStatus>` and `MarkForAnalysisAsync(AccountReviewEvidence evidence, CancellationToken) -> ValueTask<AccountReviewMarkResult>`.
- `AccountReviewStatus` is `NotMarked` or `Analysis`; `AccountReviewMarkResult` is `Marked` or `AlreadyMarked`.
- `AccountReviewEvidence` contains normalized account, stable reason code, optional character/IP, requested and allowed speeds, full `AttackRun`, and UTC detection time.
- `FileAccountReviewStore(string root)` creates/validates the configured root. Missing sidecar returns `NotMarked`; I/O errors and malformed/unsupported records throw for callers to fail closed. Invalid names are rejected before path construction.
- Persist `<NORMALIZED_ACCOUNT>.review.json` with `Version=1`, account, `Status=Analysis`, UTC timestamp and the evidence object. Reject reparse/symlink traversal where the runtime supports detecting it; do not follow an account-controlled link outside the configured root.

- [x] Add `AccountReviewStoreTests` for missing status, mark/read round-trip using a new store instance, first-evidence preservation on repeated mark, same-account concurrency, invalid name/path traversal, and truncated/unknown-version or identity-mismatched documents. Add a symlink/reparse-point escape case where the test environment supports creating one; otherwise record the unsupported capability and keep the production rejection check.
- [x] Add `LegacyAccountNameRules.TryNormalize(string, out string)` for the existing 1–15 ASCII `[A-Z0-9_-]` account contract; replace the duplicate private normalizers in both account-auth backends without changing their outcomes.
- [x] Implement `FileAccountReviewStore` with one version-1 JSON document per normalized account, containing status plus evidence. Serialize same-account operations in-process; write a same-root temporary file, flush, then atomically replace. Never overwrite a malformed existing document or a prior Analysis record.
- [x] Register the new store tests in the existing custom runner. Do not execute tests yet; all execution is deferred to Task 5.

### Task 2: Login denial and runtime configuration

**Files:**
- Modify: `src/WydCdk.World/AccountLoginCoordinator.cs`
- Modify: `src/WydCdk.World/AccountLoginFailureNotice.cs`
- Modify: `src/WydCdk.Server/Program.cs` (`ServerOptions`, startup and login dispatch)
- Modify: `src/WydCdk.Server.ControlPanel/MainForm.cs`
- Modify: `tools/Install-StatusPublishing.ps1` (edit only; do not run)
- Modify: `tools/PortAudit/Test-AutoTradeClose.ps1` (runtime argument)
- Test: `tests/WydCdk.Engine.Tests/AccountReviewLoginTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`

**Interfaces:**
- Add `AccountLoginRejection { None, AccountUnderAnalysis, ReviewStoreUnavailable }` and append it to `AccountLoginOutcome`; `IsSuccess` is false for either rejection.
- Change the coordinator constructor to `AccountLoginCoordinator(LoginSessionRegistry sessions, IAccountStore accounts, IAccountReviewStore accountReviews)`.
- Add `AccountLoginFailureNotice.ForReview(AccountLoginRejection)` for the neutral Analysis text and generic store-unavailable text; retain `For(AccountAuthenticationStatus)` for existing authentication failures.

- [x] Add coordinator tests with a counting fake authenticator: an Analysis result closes the session before `AuthenticateAsync`; an unmarked account follows the existing auth path; store failure closes login without invoking authentication; invalid account names continue to receive the existing auth rejection without any review-file lookup.
- [x] Implement review lookup after `BeginAccountLogin` and before password authentication. Normalize valid identities first; map Analysis and store errors to explicit rejection outcomes and close the pending session. Do not include exception paths or account-review file contents in the client notice.
- [x] Add `--account-review-root <directory>` to `ServerOptions` and its usage text. Require it whenever legacy-file or MariaDB authentication is enabled; construct one `FileAccountReviewStore` for both auth modes and pass it to `AccountLoginCoordinator` and connection handlers. Leave the no-auth diagnostic listener valid, including its legacy single-port invocation.
- [x] Wire the new option into Control Panel startup: add an editable review-root field defaulting to `%LOCALAPPDATA%\WydCdk\account-review`, add a fifth configuration-grid row, and pass it in `StartServer`. Wire the option into the local smoke runner and the static scheduled-task argument builder. The installer script is not executed or applied to any host in this task.
- [x] Register login tests; defer execution to Task 5.

### Task 3: Action trigger, durable mark, and safe termination

**Files:**
- Modify: `src/WydCdk.World/LegacyMovementRules.cs`
- Create: `src/WydCdk.World/LegacyActionAccountReviewCoordinator.cs`
- Test: `tests/WydCdk.Engine.Tests/AccountReviewActionTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `src/WydCdk.Server/Program.cs` (Action dispatch and crack-error helper)

**Interfaces:**
- Add `LegacyMovementRules.RequiresAccountAnalysis(int requestedSpeed, int attackRun) -> bool`, true only for an actual low-nibble speed violation and inclusive high-severity thresholds.
- Add `LegacyActionAccountReviewCoordinator(IAccountReviewStore store).ReviewSpeedViolationAsync(string accountName, string? characterName, string? remoteAddress, int requestedSpeed, int allowedSpeed, int attackRun, CancellationToken) -> ValueTask<ActionAccountReviewResult>` with `NotRequired`, `Marked`, `AlreadyMarked`, and `PersistenceUnavailable` results.
- Change the local `RecordCrackErrorAsync` helper to return `Task<LegacyCrackErrorOutcome?>` and accept `bool deferCharacterLogout = false`; only the high-severity Analysis path passes `true`, preserving counter/logging but deferring its generic logout notice until the Analysis result is known.

- [x] Add tests for `(speed=20, AttackRun=5)` qualifying, `(speed=13, AttackRun=300)` qualifying, `speed=19/AttackRun=299` below threshold, `speed == (AttackRun & 0xF)` never qualifying even for score 300+, and a persistence failure returning `PersistenceUnavailable`.
- [x] Test the shared crack-error arithmetic at the exact logout boundary and verify the qualifying review operation still runs when the same `5/type 4` increment reaches it.
- [x] Implement the pure predicate and coordinator; capture the authenticated account, character name and remote endpoint before any logout helper can clear session state. Keep persistence failure distinct from a confirmed Analysis mark.
- [x] In `Program.cs`, retain the existing `5/type 4` increment and clamp-only behavior for non-severe violations. For a severe violation, call `RecordCrackErrorAsync(..., deferCharacterLogout: true)`, persist the mark, send the `MSG_MessagePanel` Analysis notice on success (or a generic failure notice on store failure), then return/close before position mutation or any Action relay. Preserve first evidence and log operational failures without logging passwords, reconnect tokens, adapter/MAC values, or raw records.
- [x] Register action tests; defer execution to Task 5.

### Task 4: Headless end-to-end persistence and relogin

**Files:**
- Create: `tests/WydCdk.Client.E2E/ActionAccountAnalysisNetwork.cs`
- Modify: `tests/WydCdk.Client.E2E/Program.cs`
- Modify: `tools/PortAudit/Test-AutoTradeClose.ps1` (new isolated `-ActionAnalysis` mode)

**Interfaces:**
- Add `--action-account-analysis-smoke` for the mark/disconnect phase and `--account-analysis-login-smoke` for the post-restart login phase; both are loopback-only and require distinct isolated actor/control fixture files.
- Add `--account-review-peer-account-file <path>` for the isolated control account used in those two phases.
- The PowerShell mode owns only its child server PID, loopback port 18283, and a unique evidence directory under `Backup/Build/AutoTradeCloseSmoke`; it retains the same review root across the controlled restart.

- [x] Add network assertions: an authenticated actor with allowed speed 1 sends speed 20; actor receives checksum-valid `0x0101` Analysis text and EOF; peer receives no movement relay; persisted character coordinates remain unchanged.
- [x] Add restart-phase assertions: stop only the owned server PID, confirm port 18283 is free, restart with the same account/review roots, then prove a wrong-password login for the marked identity receives the Analysis notice before authentication while the control account still gets a valid account-login confirmation.
- [x] Add a loopback read-failure case after startup by temporarily moving the isolated review root while the host remains running; a valid control login must receive the generic unavailable notice and EOF, with no account-login confirmation. Restore the exact root before stopping the owned host.
- [x] Extend the guarded PowerShell mode to make isolated copies of test account blobs, rewrite only the copies to unique synthetic credentials, confirm auth-mode startup is refused when `--account-review-root` is omitted, pass the configured root for the positive run, restart the server for the second phase, retain logs/evidence on failure, and confirm original fixture hashes and port cleanup. Never print fixture passwords.
- [x] Run the headless loopback phases after implementation; initial smoke exposed queued `CreateMob` frames and the bounded ordered reader was corrected before the final battery.

### Task 5: Final verification, documentation, and sandbox artifact

**Files:**
- Modify in place: `AGENTS.md`, `PROGRESSO.md`, `BACKLOG.md`
- Modify: `docs/PORT_TRACEABILITY.md`, `docs/CLIENT_E2E_SMOKE.md`, `docs/README.md`
- Verify: `WydCdk.Engine.slnx`, `tests/WydCdk.Engine.Tests`, `tools/PortAudit/Test-AutoTradeClose.ps1`

- [x] Update documentation only after the implementation is complete; record the 7.69/W2PP divergence, file schema/lifecycle, config requirement, tested paths and remaining limits. Keep `PROGRESSO.md` concise and move executable follow-up items to `BACKLOG.md`.
- [x] Run, together at the end: `dotnet run --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj --no-restore`; `dotnet build .\WydCdk.Engine.slnx --no-restore --nologo -v:q`; and `tools\PortAudit\Test-AutoTradeClose.ps1 -ActionAnalysis -SkipBuild` against its owned loopback listener and isolated fixtures. Fix regressions, then rerun the complete final battery.
- [x] After local checks, restore the `win-x64` target, publish Release self-contained, and package/hash 197 runtime files with no PDB/source/docs/secrets. Package SHA-256: `B70F7F6C5318EC54BBC879D8711B0624716768CE32A4D0C103422B71AD703E88`.
- [x] Sandbox deployment completed after explicit authorization: runtime-only package SHA-256 `B70F7F6C5318EC54BBC879D8711B0624716768CE32A4D0C103422B71AD703E88`; UP/PVP Running on 8281/8282 from the new executable; `servtest.htm` and both `.wyd-status` heartbeats refreshed. Rollback backup `C:\WYDCDK\Server\test-releases\backup-action-analysis-70c4d907` preserves all 202 prior runtime files with matching hashes, original wrappers, task XML, and package. `account-review` ACL is protected for SYSTEM/Administrators only.
- [x] Final local static review: smoke manifest records original fixture hashes intact and cleanup complete; package excludes source/docs/secrets. Listener port 18283 is free. No stage/commit/push.

## Handoff

Implementation, local validation, and sandbox runtime deployment are complete. Remote verification confirms current listeners/status, package hashes, protected review-root ACL, and all backup hashes. No Site source/schema, production, Git, or credential changes were made. Login with a real account and visual client E2E remain untested.
