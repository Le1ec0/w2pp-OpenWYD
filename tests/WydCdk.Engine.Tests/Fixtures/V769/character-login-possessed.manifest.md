# `Affect.Type=24` - Samaritano

- Fonte-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, em `BASE_GetCurrentScore`.
- Comparador obrigatorio: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- SkillData 7.69: `Samaritano` aponta para `AffectType=24`; o comentario do bloco no alvo usa `Possuido` e o W2PP usa `Samaritano`, mas a formula e a mesma.
- Alvo e W2PP: `add = CurrentScore.Ac / 4 + Value`; depois `CurrentScore.Ac += add`, com divisao inteira e aplicacao sequencial por slot.
- A etapa C# porta a formula como um valor puro de ArmorClass, sem integrar o coordenador.
- Resultado da auditoria: sete vetores, incluindo divisao inteira, ordem sequencial, valores pequenos/grandes e affects nao relacionados.
- Fixture: `character-login-possessed.golden.json` (nome historico do arquivo da primeira auditoria).
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
