# CreateMob 7.69 golden

- Source: `Backup/Tools/ReferenceSources/TMProject2GlobalClient`
- Source commit: `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`
- Header: `Projects/TMProject/Basedef.h`
- Oracle: MSVC Win32 probe `Server/tools/PortAudit/V769CreateMobGoldenProbe.cpp`
- Generator: `Server/tools/PortAudit/Generate-V769CreateMobGolden.ps1`
- Fixture bytes: 224-byte payload after the 12-byte `MSG_STANDARD` header
- Message: `MSG_CreateMob`, opcode `0x0364`, total size 236 bytes
- Client layout: 18 visual equipment values at payload offset 22, 32 affects at 58,
  score at 128, `Equip2[18]` at 178, `Nick[26]` at 196, and `Server` at 222
- W2PP projection: source payload is 220 bytes with 16 equipment and 16
  `AnctCode` values; target-only indices 16/17 and `Server` are zeroed
- Scope: structural DTO/adapter only; visual-code calculation, NPC policy,
  relay/listener integration, and gameplay authority for slots 16/17 remain pending
