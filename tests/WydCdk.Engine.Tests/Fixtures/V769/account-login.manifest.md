# Account login 7.69 golden

- Wire oracle: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Projects/TMProject/Basedef.h::MSG_CNFAccountLogin`.
- Source checkout: `TMProject2GlobalClient`, commit `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`.
- Client `Basedef.h` SHA-256: `13F4B5116316F7E88A8B009F4D3359281971B41C9964021E849F748EC5006D19`.
- Probe: `Server/tools/PortAudit/V769AccountLoginGoldenProbe.cpp`, compiled with MSVC Win32 against `Projects/TMProject`.
- Regenerate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Server/tools/PortAudit/Generate-V769AccountLoginGolden.ps1`.
- Payload fixture: 1916 bytes as hex; full client frame is 1928 bytes. All bytes are synthetic; no live account/player data is used.
- ABI checks: `MAX_CARGO=120`, `MSG_STANDARD=12`, `STRUCT_SELCHAR=904`, `MSG_CNFAccountLogin=1928`; offsets are SecretCode 12, SelChar 32, Cargo 936, Coin 1896, AccountName 1900, SSN1 1916, SSN2 1920. The payload contains 4 alignment bytes before SelChar and 4 trailing bytes.
- Sentinels cover SecretCode, all 120 cargo entries, Coin, AccountName and SSNs. Selection is zeroed here because its 904-byte contents are independently covered by `character-selection.golden.hex`.
- This validates the client header wire only. The 7.69 common DBSrv and private TMSrv headers disagree with each other and with the client; no listener integration or client E2E is implied.
