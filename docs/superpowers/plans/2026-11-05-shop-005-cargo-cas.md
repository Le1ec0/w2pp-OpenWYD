# SHOP-005 Cargo CAS completion plan

## Goal and scope

Finish the NPC sale from Cargo only. Cargo remains account/world scoped and is
shared by active character sessions of one account. Do not add Cargo to the
character checkpoint, enable generic Cargo movement, add buyback/`MSG_Buy`,
change schema, or reject duplicate-account logins. A committed sale changes
only the complete Cargo array and the selling character's carried `MOB.Coin`;
bank Coin and every other account byte remain unchanged.

## Execution sequence

1. [x] Complete the focused BL-000 live-writer/gate audit for active C# paths
   that change Cargo or carried Gold, plus checkpoint and departure writes.
   The repository-wide BL-000 class inventory remains open and is not claimed
   complete. Record lock order and unsupported cross-process cases in
   `docs/PORT_TRACEABILITY.md`.
2. [x] Add the cancellable multi-account operation lease with canonical account
   keys, stable ordinal acquisition, de-duplication, reverse release, and
   explicit ownership/disposal checks. AutoTrade's sorted connection-ID lease
   remains outermost; checkpoint order is account reservation -> slot gate.
3. [x] Replace guild invite's sequential single-character writes with one
   expected-value pair transaction that supports two slots of one account and
   two accounts. Publish in-memory state only after durable success and do not
   report success for conflict, unavailable, or uncertain outcomes.
4. [x] Implement file and MariaDB adapter NPC Cargo CAS: compare full expected
   Cargo and carried Gold, write only Cargo and the target character's carried
   Gold, return `Conflict` without mutation, and preserve bank Coin/unrelated
   bytes. File recovery uses its account journal; MariaDB uses a conditional
   byte-range update inside an existing row transaction and no DDL. The
   conditional update avoids MariaDB 1020 from concurrent locking reads while
   preserving unrelated blob bytes.
5. [x] Add one account/world Cargo owner with revision and shared active-session
   projection. Cargo sale dispatch is prepare -> account lease -> revalidation
   -> durable CAS -> runtime publication -> response/resync. Conflict and
   uncertain outcomes reconcile from authoritative storage or block the
   account for recovery; no success frame precedes commit/publication.
6. [x] Add regression coverage for lease ordering/cancellation, pair atomicity,
   CAS conflict/no-write, bank/other-byte preservation, journal recovery,
   duplicate active sessions, and post-commit publication. Build and
   authenticated headless Cargo smoke passed on 05/11. On 06/11, the compiled
   integration harness passed five consecutive runs against a disposable
   MariaDB 11.8.5 instance on VPS loopback, covering lifecycle, checkpoint,
   Cargo CAS success/stale/concurrent/rollback, and same-account/two-account
   guild invite pairs. Cargo sale and character checkpoint/relogin smokes also
   passed on the VPS. Full Engine.Tests there reported 580/602; the 22 failures
   require reference source fixtures kept off the VPS. No visual client E2E ran.

## Gate order and failure rules

- Checkpoint/departure: account lease -> connection slot lease -> capture and
  persist. On departure, an existing AutoTrade book lease remains outermost:
  sorted book connection-ID lease -> sorted account leases -> connection slot
  lease. AutoTrade purchase: sorted book connection-ID lease -> sorted account leases
  -> runtime/store commit or rollback. Bilateral trade and guild invite: sorted
  account leases -> snapshot/revalidation -> pair-atomic store -> runtime
  publication. Cargo sale: seller account lease -> expected-state capture and
  durable CAS -> non-cancelable publication; release before socket sends.
- Never hold the `WorldHub` monitor across awaited I/O. Adapters do not call
  back into runtime state or acquire application leases.
- Cancellation before store commit leaves runtime unchanged. Once a store
  operation may have committed, cancellation is suppressed until its outcome
  is resolved; unknown outcomes require authoritative reload/reconciliation.
  `Conflict` causes no runtime mutation and requires resync/reload before a new
  user request. Success is sent only after durable commit and publication.
- The file gate coordinates one process per shared account root. Do not claim
  cross-process safety unless a separate mechanism is implemented and tested.

## Verification gates

Use test-first changes: add and run a failing focused test before each
implementation slice, then run focused tests and the full required validation.
The user explicitly authorized a Server commit and push after validation. This
block does not authorize a Site/prod deployment or schema change.
