# Character MOB 7.69 golden

- Wire oracle: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Projects/TMProject/Basedef.h::STRUCT_MOB`.
- Source checkout: `TMProject2GlobalClient`, commit `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`.
- Probe: `Server/tools/PortAudit/V769CharacterMobGoldenProbe.cpp`, compiled with MSVC Win32 against the client header.
- Regenerate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Server/tools/PortAudit/Generate-V769CharacterMobGolden.ps1`.
- Fixture: synthetic 1040-byte `STRUCT_MOB`; no account or live character data is used.
- ABI: `Equip=140`, `Carry=284`, `LearnedSkill[2]=796`, `CurrentKill=1036`, `TotalKill=1038`; the generator has compile-time assertions for these offsets and the complete struct size.
- W2PP mapping: copy 16 equipment entries and leave target entries 16/17 empty; copy 64 carry entries; map learned word 0 and zero target word 1. Client 7.69 uses word 1 for skills 200-246; W2PP `STRUCT_MOB` has only one word, and its `MOBEXTRA.SecLearnedSkill` is a separate field, not a bit-table mapping. The Win32 golden deliberately seeds word 1 with `0x12345678`; the adapter test clears only that target-only field when comparing the W2PP projection. Narrow `Level`, bonuses, `Magic`, and regen fields use checked conversions. The target dummy region and ABI padding are zeroed.
- Appearance: client field initialization consumes name bytes 12..15 as chaos/current kills/total kills. W2PP `GetCreateMob` derives those bytes from `Carry[KILL_MARK]`; the login adapter performs the same projection for the local character and also fills target `CurrentKill`/`TotalKill`.
- Adapter test: `W2ppCharacterMobV1Adapter` output is compared byte-for-byte with this compiled Win32 golden, and tests cover guilty chaos, equipment expansion, source size, coordinate/range rejection, and narrowing overflow.
- Scope: the DTO/adapter are isolated. This does not integrate `MSG_CNFCharacterLogin` into the listener or establish runtime/E2E compatibility; the private 7.69 TMSrv header still differs from the client/DBSrv common header.
