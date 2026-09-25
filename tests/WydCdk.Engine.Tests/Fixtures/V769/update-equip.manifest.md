# UpdateEquip 7.69 golden

- Source: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Projects/TMProject/Basedef.h`
- Source commit: `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`
- Oracle: `V769UpdateEquipGoldenProbe.cpp`, compiled with MSVC Win32 and the source header.
- Regenerate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Server/tools/PortAudit/Generate-V769UpdateEquipGolden.ps1`
- Payload: synthetic `MSG_UpdateEquip`, 56 bytes after the 12-byte `MSG_STANDARD` header.
- ABI checks: `MSG_STANDARD=12`, `sEquip` offset 12, `Equip2` offset 48, `sizeof(MSG_UpdateEquip)=68`.
- Sentinels cover all 18 visual equipment and secondary ancient-code entries; the two trailing ABI-padding bytes are zero.
- This fixture proves the client 7.69 wire layout only. The adapter leaves positions 16/17 empty because W2PP currently has 16 authoritative equipment slots.
