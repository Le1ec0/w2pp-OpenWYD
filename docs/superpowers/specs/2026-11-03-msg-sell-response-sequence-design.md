# SHOP-005 NPC sell response sequence

## Intent

Represent the accepted 7.69 NPC sale response as an ordered, testable plan so the later Host dispatch can reproduce the native recipients and frames.

## Goals

- Echo the accepted `0x037A MSG_Sell` request to the seller, preserving its decoded payload and request header values.
- For equipment slots 1 through 7, add `0x036B MSG_UpdateEquip` for the seller's inclusive 33x33 view, excluding the seller.
- Send `0x0337 MSG_UpdateEtc` to the seller after the optional equipment multicast.
- Reject malformed or sale-mismatched requests and produce no partial response plan.

## Non-goals

- Do not register `0x037A` in the Host dispatcher or write to sockets in this slice.
- Do not add a synchronous sale transaction or persistence format. The 7.69 handler mutates runtime state; player account and treasury checkpoints are performed elsewhere, while recycle-bin state is session-owned.
- Do not change the NPC sale gates, prices, mutations, schema, or E2E fixture.

## Approaches

- Dispatch the complete sale from `Program.cs` now. Deferred because dispatch combines persistence/recovery and failure behavior that this response-only slice does not establish.
- Add a separate durable sale journal. Deferred because the reference does not synchronously persist `MSG_Sell`, and a new transaction format would change the observed boundary.
- Build an ordered response plan from the committed outcome and the existing WorldHub frame builders. Chosen for this slice because it captures the native wire and recipient order without introducing network side effects.

## Constraints

- Keep the canonical 7.69 frame layouts and compare W2PP where it differs.
- Keep `Domain` independent of transport; this planner remains in `WydCdk.Server` and consumes existing `WydCdk.Protocol`/`WydCdk.World` contracts.
- Keep .NET 10, local-only validation, no schema/deploy/Site/commit/push changes.

## Builder choices

- The caller supplies a decoded request frame and a committed sale outcome. The planner verifies the request's target, source type, and slot against that outcome before building any response.
- The planner returns ordered steps with explicit seller or view recipients. It uses `WorldHub.TryBuildUpdateEquipFrame`, `TryBuildUpdateEtcFrame`, and `GetParticipantIdsInView`.
- Slot 8 and later do not request `SendEquip`, matching the source's `isEquip = (sMyPos < 8)` rule.
