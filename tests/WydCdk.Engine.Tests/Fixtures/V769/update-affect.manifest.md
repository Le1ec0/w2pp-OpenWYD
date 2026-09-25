# UpdateAffect 7.69 golden

- Source: `Backup/Tools/ReferenceSources/TMProject2GlobalClient`
- Source commit: `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`
- Header: `Projects/TMProject/Basedef.h`
- Oracle: MSVC Win32 probe `Server/tools/PortAudit/V769UpdateAffectGoldenProbe.cpp`
- Generator: `Server/tools/PortAudit/Generate-V769UpdateAffectGolden.ps1`
- Message: `MSG_UpdateAffect`, opcode `0x03B9`, payload 256 bytes, frame 268 bytes
- Target entry layout: `Type` byte, signed `Level` byte, signed `Value` short,
  signed `Time` int; 32 entries of 8 bytes
- W2PP comparison: `MSG_SendAffect` uses the same 8-byte stride and frame size,
  but orders fields as `Type`, byte `Value`, ushort `Level`, uint `Time`;
  the adapter swaps fields and rejects narrowing overflow
- Scope: structural DTO/adapter only; affect application, mutation and relay remain pending
