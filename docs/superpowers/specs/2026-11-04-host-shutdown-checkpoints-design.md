# Host Shutdown Checkpoints Design

## Goal

On cooperative Host shutdown, finish accepted connection handlers and their existing departure saves, stop the periodic treasury loop, then make one final registered-treasury file checkpoint before reporting the Host offline.

## Current State

The listener accepts clients and starts `RunAllocatedConnectionAsync` without retaining its task. Shutdown stops the listener and cancels shared runtime loops, then awaits those loops; connection `finally` blocks perform departure cleanup and save with `CancellationToken.None`, but the Host can exit before those tasks finish. The 12-second treasury loop is awaited, but cancellation currently ends it without a final `SaveRegisteredAsync` pass.

## Approaches

1. **Drain accepted handlers, then independently sweep treasury files (selected).** `LegacyConnectionTaskDrain` stops accepting tracked tasks and awaits outstanding handlers; after the accept loop stops, await the handlers, await the periodic treasury loop, then save registered treasury MOBs once. This uses existing writers and keeps account and NPC persistence independent.
2. **Wait for the next 12-second cycle.** This adds shutdown delay and still leaves a window after the last periodic pass; it does not provide a clear shutdown checkpoint.
3. **Create a sale transaction across account and NPC stores.** This would require a new journal/commit protocol and changes the durability model beyond shutdown; it is out of scope.

## Behavior

- No accepted connection task is detached from cooperative shutdown. Completed successful tasks are pruned as new connections arrive so the drain stays bounded by recent concurrent work; faulted tasks remain observable at drain.
- The caller stops producing connections before it drains. The drain waits for all tracked handlers, including their existing logout/disconnect persistence in `finally`.
- With `--npc-root`, the Host waits for the periodic treasury loop to stop, then calls the existing `SaveRegisteredAsync` once with a non-cancelled token. This order captures final in-memory treasury state after connection handlers have stopped.
- The final treasury sweep uses the existing per-MOB temp-file/replace writer. It is not atomic with account saves or across treasury MOBs. The recycle bin remains transient; no schema, journal, outbox, retry protocol, or immediate sale write is added.
- A final treasury I/O/format failure is logged and collected with other shutdown failures; the offline status is still published before the Host returns a nonzero failure.
- Without `--npc-root`, the final treasury sweep is skipped.

## Review Focus

- A pending handler keeps shutdown alive until its task completes.
- Completed successful handlers do not accumulate without bound; faulted handlers remain visible to drain.
- Shutdown waits for the periodic treasury loop before the last treasury save.
- The final treasury save runs after connection departure saves and still runs when the periodic loop reports a failure.
- Account and treasury writes remain independent; no E2E visual/client or MariaDB behavior is claimed by this slice.

## Verification

- Focused Engine.Tests cover connection-task drain, successful-task pruning, and fault visibility.
- The authenticated NPC sell loopback smoke stops the Host while the seller remains connected, then verifies the departure checkpoint after process exit.
- The existing `NPC sell and treasury independent checkpoints` test continues to prove that treasury files change only when the registered treasury checkpoint runs.
- Build the full `WydCdk.Engine.slnx`, run the full Engine.Tests suite, and run the NPC sell loopback smoke twice with the local ItemList fixture.
