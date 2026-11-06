# Non-Generated NPC Type 20 Timer Design

## Intent

Continue the ordinary non-generated NPC affect timer port with Poison
(Type 20), following Task 5CQ's Type 17 support.

## Source Contract

TMProject 7.69 `Servidor/Source/Code/TMSrv/Server.cpp:6210-6253` and W2PP
`Server/W2PP/Source/Code/TMSrv/Server.cpp:5145-5187` agree for mobs:

- Start `AffectValue` at 100, subtract signed `LearnedSkill / MAX_USER`, then
  divide by 10 using integer truncation.
- Compute integer `HpLose = (int)((100 - AffectValue) / 100.0)`.
- Use the mob cap `Perda = 10 * (100 - LearnedSkill / MAX_USER)` and floor
  `Hp * HpLose` at `Hp - Perda`, then clamp HP to `[1, MaxHp]`.
- Preserve the signed HP delta and request the HP relay on each Poison tick.
- Share the finite timer decrement and eight-byte expiry clear; refresh score
  when HP changes or the affect expires.
- The sources differ only in the fallback player cap (`500` in 7.69, `1000` in
  W2PP). The mob branch replaces it in both sources, so it does not apply here.

## Scope

- Process Type 20 on the existing non-generated NPC Peace visit and fourth
  Combat opportunity.
- Reuse the current snapshot lock, captured view, timer tail, and
  `SetHpDam`-then-`UpdateScore` relay.
- Keep generated NPC, summon, player, mount linkage, attack, and movement paths
  unchanged.

## Acceptance

- Unit tests reproduce missing Type 20 behavior before implementation, then
  cover the NPC damage cap, signed delta, timer decrement/expiry, and targetless
  Combat cadence without an attack.
- TCP loopback smoke verifies HP then score reaches only the captured view.
- Run Engine.Tests, full no-incremental solution build, `--mob-ai-smoke`, and
  `git diff --check`; update project Markdown in place.
- No package, publish, deploy, commit, or push.
