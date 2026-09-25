# Character selection 7.69 golden

- Source: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Projects/TMProject/Basedef.h`
- Source commit: `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`
- Header SHA-256: `13F4B5116316F7E88A8B009F4D3359281971B41C9964021E849F748EC5006D19`
- Oracle: `V769CharacterSelectionGoldenProbe.cpp`, compiled with MSVC Win32 and the source header.
- Regenerate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Server/tools/PortAudit/Generate-V769CharacterSelectionGolden.ps1`
- Payload: synthetic `STRUCT_SELCHAR`, 904 bytes; no account or player data.
- ABI checks: `STRUCT_SCORE=48`, `STRUCT_ITEM=8`, `STRUCT_SELCHAR=904`, `MSG_CNFNewCharacter=920`, `SelChar` offset 16; selection offsets Score 80, Equip 272, Guild 848, Coin 856, Exp 872.
- Sentinels cover all four characters and all 18 equipment entries; padding starts zeroed.
- This fixture proves the client header's wire layout only. It does not establish server-side gameplay support for equipment positions 15–17.

# Character-login calculation fixtures

- `character-login-hpmp.golden.json` covers the 7.69 `BASE_GetHpMp` stage; details and source provenance are in `character-login-hpmp.manifest.md`.
- `character-login-soul.golden.json` covers the isolated 7.69 Soul contribution to MaxHP/MaxMP; details and the W2PP divergence are in `character-login-soul.manifest.md`.
- `character-login-kibita-soul.golden.json` covers the late 7.69 Kibita override; its Mortal/level/affect gate and MaxMP divergence from W2PP are documented in `character-login-kibita-soul.manifest.md`.
- `character-login-ability-stage.golden.json` covers the equipment-derived seed in the 7.69 `BASE_GetCurrentScore` `Cur HP/MP` block; source comparison and the 16/18 equipment caveat are in `character-login-ability-stage.manifest.md`.
- `character-login-final-clamps.golden.json` covers the final scalar and resistance clamps; source comparison and the Magic-cap divergence are in `character-login-final-clamps.manifest.md`.
- `character-login-resistance-affects.golden.json` covers active resistance modifiers from `Affect.Type` 3, 8, 16, and 25; its W2PP comparison and transform-order evidence are in `character-login-resistance-affects.manifest.md`.
- `character-login-final-damage.golden.json` covers the trailing face-gated damage addition and accumulated multiplier; source comparison and the W2PP earlier-class-block caveat are in `character-login-final-damage.manifest.md`.
- `character-login-final-speed.golden.json` covers the final AttackRun composition; its separate mount table/gate stage is covered in `character-login-mount-speed.manifest.md`.
- `character-login-mount-speed.golden.json` covers the standard/temporary mount run-floor tables and gates; the 7.69/W2PP data delta and source out-of-bounds edge at item 2390 are documented in `character-login-mount-speed.manifest.md`.
- `character-login-haste-affect.golden.json` covers Type=2 Run additions and the `RSV_HASTE` bit; source equivalence with W2PP and scope boundaries are documented in `character-login-haste-affect.manifest.md`.
- `character-login-holy-touch.golden.json` covers the target/W2PP Type=1 Holy Touch deltas; details are in `character-login-holy-touch.manifest.md`.
- `character-login-samaritan.golden.json` covers target/W2PP Type=14 Possuido MaxHp/Constitution behavior and divergence; details are in `character-login-samaritan.manifest.md`.
- `character-login-assault.golden.json` covers target/W2PP Type=13 damage-multiplier, Damage and MaxHp behavior and divergence; details are in `character-login-assault.manifest.md`.
- `character-login-possessed.golden.json` covers target/W2PP Type=24 Samaritano sequential ArmorClass behavior; details are in `character-login-possessed.manifest.md`.
- `character-login-magic-shield.golden.json` covers the target/W2PP Type=11 AC delta; details are in `character-login-magic-shield.manifest.md`.
- `character-login-magic-weapon.golden.json` covers the target/W2PP Type=9 damage/multiplier stage and the W2PP-only Magic +5; details are in `character-login-magic-weapon.manifest.md`.
- `character-login-athena-touch.golden.json` covers the target/W2PP Type=15 Special stage and the Special1 cap divergence; details are in `character-login-athena-touch.manifest.md`.
- `character-login-dexterity.golden.json` covers the target/W2PP Type=6 sequential DEX mutation; details are in `character-login-dexterity.manifest.md`.
- `character-login-fanaticism.golden.json` covers the target/W2PP Type=5 sequential DEX reduction; details are in `character-login-fanatism.manifest.md`.
- `character-login-armor-class-reduction.golden.json` covers the target/W2PP Type=12 sequential AC reduction; details are in `character-login-armor-class-reduction.manifest.md`.
- `character-login-transformation-speed.golden.json` covers Type=16 Attack/Run bonuses, class/value/skill gates, overwrite order, and 7.69/W2PP differences; details are in `character-login-transformation-speed.manifest.md`.
- `character-login-transformation-damage.golden.json` covers Type=16 damage addition and additive multiplier contribution; details and the Wolf/Eden differences from W2PP are in `character-login-transformation-damage.manifest.md`.
- `character-login-transformation-ac.golden.json` covers sequential Type=16 ArmorClass mutation and the Wolf/Titan/Eden differences from W2PP; details are in `character-login-transformation-ac.manifest.md`.
- `character-login-transformation-maxhp.golden.json` covers sequential Type=16 MaxHp mutation and the skilled Wolf/Bear differences from W2PP; details are in `character-login-transformation-maxhp.manifest.md`.
- `character-login-transformation-critical-equipment.golden.json` covers Type=16 Critical additions and head-item/sanctuary mutation; details are in `character-login-transformation-critical-equipment.manifest.md`.
- `character-login-transformation-composition.golden.json` covers the pure composition of the audited Type=16 stages over one affect snapshot; details are in `character-login-transformation-composition.manifest.md`.
- `character-login-score-coordinator.golden.json` covers the ordered composition of the audited score blocks before login integration; details are in `character-login-score-coordinator.manifest.md`.
- These calculation fixtures are synthetic and source-derived, not player snapshots or compiled C++ runtime captures.
