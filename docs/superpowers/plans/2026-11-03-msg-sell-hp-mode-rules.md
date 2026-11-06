# MSG_Sell HP and USER_PLAY Rules Implementation Plan

> Local-only port slice. Do not commit or publish.

**Goal:** Port the opening HP/mode gate and its response intent from TMProject 7.69 and W2PP `_MSG_Sell`.

**Scope:** Reject exactly `HP == 0` or a connection state other than `LoginSessionState.Playing`; preserve `SendHpMode` followed by `AddCrackError(10, 23)` as an explicit caller obligation. Keep the helper pure and outside dispatch.

- [x] Compare the first gate and response order in 7.69/W2PP.
- [x] Write boundary tests first and observe the missing result API.
- [x] Implement the pure resolver and response metadata.
- [x] Run Engine.Tests and the full solution build; compare the 43 failing test names.
- [x] Update traceability, backlog, and progress in place.
