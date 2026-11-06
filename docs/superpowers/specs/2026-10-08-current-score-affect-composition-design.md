# CurrentScore Type 34/35/8 Composition Design

## Goal

Compose the already-isolated 7.69 Type 34, Type 35, and Type 8 score effects in the pure current-score coordinator while preserving affect-slot order among those branches.

## Behavior

- Add a pure composition stage that scans the 32 eight-byte affect records in slot order and carries MaxHP, MaxMP, AC, Damage, the local Magic accumulator, LearnedSkill, and additive RSV flags.
- Reuse the existing Type 34, 35, and 8 arithmetic through per-affect helpers so their standalone stages retain the same behavior, after correcting the Type 35 helper to the selected common 7.69 source branch: compute MaxHP +15% and MaxMP +7% with integer truncation; compare each increment with its 100,000 threshold and set the score field to 100,000 only when that increment reaches the threshold. Otherwise add the increment, even if the result exceeds 100,000.
- Source selection: `TMSrv.vcxproj` compiles `Servidor/Source/Code/../Basedef.cpp`; its Type 35 branch uses 15%/7%. The duplicate `TMSrv/Basedef.cpp` branch uses 10%/10% but is not included by that project. W2PP uses 10%/10%, so this is a recorded source divergence.
- This is source-level port evidence. The existing TMSrv common/private MOB layout mismatch remains unresolved, so the implementation does not claim that a rebuilt legacy TMSrv binary has been validated at runtime.
- Feed the composition after the existing Type 12 AC reduction projection and before Soul in `CalculateCurrentScoreCoordinatorStage`. Return both the affect-stage result and an immutable post-stage MOB snapshot; Soul must see the projected score and LearnedSkill.
- Use `mob.Magic` as the unchanged source-Magic input and `abilities.Magic` as the starting local Magic accumulator.
- Type 8 bit 1 resistance remains in `ApplyResistanceAffectStage`; bits 0 and 3 remain attack effects. Type 4 is outside this composition slice. The common source included by `TMSrv.vcxproj` has a Type 4 branch, while the uncompiled TMSrv copy differs; the existing Type 4 authority decision is not resolved here.
- Keep the coordinator pure. Do not connect it to login, persistence, wire, or runtime state.

## Validation

- Add an interleaved Type 8/35/34 golden case whose result distinguishes slot order from grouping effects by type.
- Cover both Type 35 cap branches: increments below 100,000 are added to the current score; increments at or above 100,000 set the score field to 100,000.
- Assert the stage output, post-stage state observed by Soul, LearnedSkill/RSV projections, and that the input MOB remains unchanged.
- Run `Engine.Tests` and build `WydCdk.Engine.slnx` with the pinned SDK; record evidence in `Server/Backup/Build/CurrentScoreAffectComposition-20261008`.
- Update `AGENTS.md`, `BACKLOG.md`, `PROGRESSO.md`, and `Server/docs/PORT_TRACEABILITY.md`.
