# `Affect.Type=13` - Assalto

- Fonte-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, em `BASE_GetCurrentScore`.
- Comparador obrigatorio: `Server/W2PP/Source/Code/Basedef.cpp`, no bloco `Buff Loop` de `BASE_GetCurrentScore`.
- Alvo 7.69: para cada registro Type=13, calcula `Level / 10 + Value` com divisao inteira e soma apenas esse valor a `DAMAGEMULTI`.
- Divergencia W2PP: aplica antes um aumento de 15% sobre o `Damage` atual, limita o resultado a `MAX_DAMAGE=1000000000`, soma o mesmo valor a `DAMAGEMULTI` e reduz `MaxHp` para `MaxHp * 9 / 10` com truncamento inteiro.
- A etapa C# porta somente o alvo; `Damage` e `MaxHp` retornam inalterados para deixar explicita a ausencia dessas mutacoes na release-alvo.
- Resultado da auditoria: sete vetores, incluindo divisao inteira, repeticao sequencial, cap de dano, truncamento de MaxHp, valor zero e affect nao relacionado.
- Fixture: `character-login-assault.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
