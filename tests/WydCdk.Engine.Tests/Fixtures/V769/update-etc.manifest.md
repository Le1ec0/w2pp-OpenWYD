# UpdateEtc 7.69 golden

- Source: `Backup/Tools/ReferenceSources/TMProject2GlobalClient`
- Source commit: `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`
- Header: `Projects/TMProject/Basedef.h`
- Oracle: MSVC Win32 probe `Server/tools/PortAudit/V769UpdateEtcGoldenProbe.cpp`
- Generator: `Server/tools/PortAudit/Generate-V769UpdateEtcGolden.ps1`
- Message: `MSG_UpdateEtc`, opcode `0x0337`, payload 36 bytes, frame 48 bytes
- Target layout: FakeExp, Exp, two LearnedSkill words, three signed bonus
  shorts, Coin, with two alignment bytes before Coin and four trailing padding
  bytes in the native struct
- W2PP comparison: frame/payload size is also 48/36; W2PP uses unsigned Hold,
  `long long Learn`, three unsigned bonus shorts, a ushort Magic and Coin. The
  adapter checks Hold narrowing, splits Learn into the two target words,
  preserves bonus bit patterns and discards Magic because target UpdateEtc has
  no Magic field.
- Scope: DTO + adapter + golden structural contract; emitter coverage, client
  E2E and semantic integration with UpdateScore remain pending
