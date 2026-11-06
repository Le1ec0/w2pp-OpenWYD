# SHOP-005 NPC sell Host dispatch

## Intent

Connect the already-tested 7.69 NPC sale preparation, revalidated WorldHub commit,
and ordered success-response plan to the authenticated Host's `0x037A` frame loop.

## Goals

- Dispatch accepted carry and equipment sales through the existing read-only
  preparation and atomic in-memory commit stages.
- Match the 7.69 success sequence: `0x5000` yellow sale notice, original
  `0x037A` echo, optional `0x036B` view update for equipment positions below 8,
  then seller `0x0337` UpdateEtc.
- On a missing/stale response plan, conditionally roll back the committed sale
  before sending any success frame.
- Preserve the native early HP/mode rejection and active-trade cancellation
  effects where existing Host services support them.
- Let the existing periodic player and treasury checkpoints persist runtime
  state at their established cadence; do not add a synchronous sale write.

## Non-goals

- Cargo sales, buyback, `MSG_Buy`, NPC stock, merchant catalogs, and a durable
  transaction spanning player and treasury stores.
- Recreating every localized rejection message, client visual E2E, schema
  changes, Site, deploy, commit, or push.
- Changing the already-decided `GetRecycleBin` behavioral divergence: TMProject
  7.69 inserts the sale, W2PP does not, and the selected 7.69 behavior is already
  implemented by the completion plan.

## Source contract

- TMProject 7.69 `_MSG_Sell.cpp` performs HP/mode rejection, closes an active
  trade and returns, blocks auto-trade cargo and blocked accounts, evaluates the
  item/merchant/price, applies the sale, sends the yellow `0x5000` panel notice,
  echoes `0x037A`, optionally sends equipment appearance, and ends with
  UpdateEtc.
- `SendMsgExp` uses `MSG_Exp_Msg_Panel_` (`0x5000`, 96-byte text plus 32-bit
  color) with the seller's connection ID. `TNColor::Yellow` is
  `0xFFFFFF00`.
- W2PP has the same success notice and response ordering, but its sell handler
  lacks the 7.69 account-block gate and recycle-bin insertion. TMProject 7.69
  remains the chosen contract.

## Failure and persistence boundary

- Failed preparation and commit do not emit success frames.
- A planner failure after commit invokes the existing compare-and-restore
  rollback; a failed rollback is logged for operator review.
- Socket failures after a committed sale do not roll back a state transition
  already accepted by the server. Normal departure and periodic checkpoint
  paths remain the persistence boundary, matching the reference runtime model.
- Account/Cargo mutation and atomic player-plus-treasury durable recovery remain
  open work and are recorded in the backlog.
