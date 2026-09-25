# Affect Type=14 — Possuido

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, no `BASE_GetCurrentScore`.
- Comparador obrigatório: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- SkillData 7.69: `Possuido` aponta para `AffectType=14`; o bloco de score usa `value = Level * 3 / 4 + Value`; Mortal/Arch multiplicam por 2 e as demais classes por 3; `MaxHp += value * 2` e `Con += value`.
- Divergência W2PP: Mortal/Arch não aplicam o multiplicador 2, as demais classes aplicam 3, `MaxHp += value * 22` e `Con += (Con + value) * 125 / 100`.
- A etapa C# porta somente a regra do alvo, preserva a ordem dos slots e faz narrowing para `short` na Constituição; a divergência não é ocultada. O nome do arquivo preserva o rastro anterior da auditoria.
- Resultado da auditoria: sete vetores, incluindo Mortal, Arch, Celestial, divisão inteira, repetição sequencial e affect não relacionado.
- Fixture: `character-login-samaritan.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
