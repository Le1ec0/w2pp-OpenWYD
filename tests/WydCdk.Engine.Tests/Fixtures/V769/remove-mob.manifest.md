# RemoveMob 7.69 golden

- Source: `Backup/Tools/ReferenceSources/TMProject2GlobalClient`
- Source commit: `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`
- Header: `Projects/TMProject/Basedef.h`
- Oracle: MSVC Win32 probe `Server/tools/PortAudit/V769RemoveMobGoldenProbe.cpp`
- Generator: `Server/tools/PortAudit/Generate-V769RemoveMobGolden.ps1`
- Fixture bytes: 4-byte payload after the 12-byte `MSG_STANDARD` header
- Message: `MSG_RemoveMob`, total size 16 bytes, `RemoveType` at payload offset 0
- W2PP comparison: `Server/W2PP/Source/Code/Basedef.h` also measures 16 bytes
  with the same `int RemoveType` payload; no adapter is required
- Scope: wire/layout equivalence only; removal policy and relay behavior remain
  owned by the world/application flow
