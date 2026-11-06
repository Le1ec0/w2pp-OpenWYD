# MSG_Sell NPC Target Rules Implementation Plan

> Local-only port slice. Do not commit or publish.

**Goal:** Model the TMProject 7.69/W2PP NPC-target gates for `_MSG_Sell`.

**Scope:** Preserve the class-3 `LearnedSkill & 0x1000` gate bypass, ordinary ID range, exact `Merchant==1` check, and inclusive `GetInView` coordinate rectangle. Keep the helper pure and outside dispatch.

**Safety boundary:** Reject `TargetID >= MAX_MOB` before the legacy bypass because the source jumps around that check and later indexes `pMob[TargetID]`.

- [x] Compare 7.69 and W2PP handlers, `GetInView`, and shared constants.
- [x] Write target-ID, merchant, view, bypass, and safe-index tests first; observe the missing API failure.
- [x] Implement the pure resolver and reuse the existing 33-cell movement constants.
- [x] Run Engine.Tests and the full solution build; record the unchanged 43 failing test names.
- [x] Update traceability, backlog, and progress in place.
