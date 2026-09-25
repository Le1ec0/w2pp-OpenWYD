# UpdateScore 7.69 golden

- Source: `Backup/Tools/ReferenceSources/TMProject2GlobalClient`
- Source commit: `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`
- Header: `Projects/TMProject/Basedef.h`
- Oracle: MSVC Win32 probe `Server/tools/PortAudit/V769UpdateScoreGoldenProbe.cpp`
- Generator: `Server/tools/PortAudit/Generate-V769UpdateScoreGolden.ps1`
- Message: `MSG_UpdateScore`, opcode `0x0336`, payload 140 bytes, frame 152 bytes
- Target layout: 48-byte `STRUCT_SCORE`, Critical/SaveMana, 32 packed affect
  words, Guild/GuildLevel, Resist, aligned `ReqHp`/`ReqMp`, ushort Magic/Rsv,
  one-byte LearnedSkill and three trailing padding bytes
- W2PP comparison: frame/payload size is also 152/140, but W2PP has a packed
  score with int Level and Merchant/Direction/ChaosRate, then RegenHP/MP, CurrHp/
  CurrMp, int Magic and four Special bytes. The adapter maps CurrHp/MP to the
  target ReqHp/MP, checks Level/Magic narrowing and clears target-only fields.
- Additional source note: the community 7.69 DBSrv common header packs the same
  field list to 147 bytes, while the client header consumes the 152-byte layout;
  this ABI disagreement remains documented and the release target follows the
  client header.
- Scope: DTO + adapter + golden structural contract; emitter coverage, score
  recomputation, affect semantics and client E2E remain pending
