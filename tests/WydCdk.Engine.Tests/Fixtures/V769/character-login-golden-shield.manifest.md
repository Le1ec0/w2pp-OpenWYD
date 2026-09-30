# Affect Type=31 — Escudo Dourado (Golden Shield)

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, no `BASE_GetCurrentScore`.
- Comparador obrigatório: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- Regra alvo 7.69: `add = Affect[i].Level / 2 + Affect[i].Value; MOB.CurrentScore.Ac += add;`.
- Comparação W2PP: o tipo `Type=31` não existe no loop de buffs do W2PP; o acréscimo de defesa correspondente é nulo (0).
- A etapa C# porta o cálculo isolado de `ArmorClassAddition` para a release 7.670 mantendo a divergência explícita contra o legado.
- Resultado da auditoria: sete vetores cobrindo ausência de affect, nível padrão, nível zero com valor, divisão inteira truncada (`level / 2`), acumulação múltipla e afeto não relacionado.
- Fixture: `character-login-golden-shield.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
