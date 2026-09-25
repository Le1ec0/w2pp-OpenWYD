# Coordenador dos blocos auditados de `BASE_GetCurrentScore`

- Fonte-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`.
- A ordem coordenada segue os blocos já portados/testados: rebuild base/equipamento, `Cur Special`, transformação `Type=16`, `Type=11` Escudo Magico, `Type=9` Arma Magica, `Type=15` Toque de Athena, `Type=5` Fanatismo, `Type=6` DEX, `Type=12` redução de AC, `Type=2` Haste, Soul e override Kibita.
- O estado `AfterTransformation` projeta somente Damage aditivo, AC e MaxHp. Attack/Run, multiplicador, Critical e equipamento permanecem em snapshots explícitos porque seus blocos de commit ainda não foram portados.
- A Soul observa o MaxHp produzido pela transformação; Kibita observa o resultado da Soul. Isso é validado por vetores Mortal Wolf, Celestial Eden e gate de classe.
- `RsvWithAffects` é somente a união do Rsv resetado pelo rebuild com o mask produzido pelo Haste; não substitui o MOB persistido.
- A resistência clampada vem do estagio geral de affects, após a transformação, e não soma `RegAdd` novamente.
- Este coordenador é uma fronteira de composição local: não integra login/listener, não troca encoder e não declara paridade dos blocos de buffs ainda pendentes.
- O Type=11 Escudo Magico projeta seu delta de AC entre Type=16 e Soul: o alvo usa `Level / 5 + Value` e o W2PP `Level / 3 + Value`.
- O Type=9 Arma Magica projeta Damage depois do Type=11 e antes de Soul; o alvo nao acrescenta Magic, enquanto o W2PP acrescenta `+5` por affect.
- O Type=15 Toque de Athena projeta os quatro Special depois de Type=9 e antes de Soul; o alvo usa cap 255 em todos, enquanto o W2PP usa cap 200 apenas em Special1.
- O Type=5 Fanatismo projeta a redução sequencial de `Dex` antes de Type=6; alvo e W2PP usam o mesmo fator `float` e narrowing para `short`.
- O Type=6 DEX projeta a mutacao sequencial de `Dex` depois de Type=15; alvo e W2PP usam o mesmo fator `float` e narrowing para `short`.
- O Type=12 reducao de AC projeta a mutacao sequencial de `Ac` depois de Type=6; alvo e W2PP usam o mesmo fator `float` e truncamento para `int`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
