# Affect Type=10 — Enfraquecer (Weaken)

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, no `BASE_GetCurrentScore`.
- Comparador obrigatório: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- Regra alvo 7.69: `sub = Affect[i].Level / 5 + Affect[i].Value; MOB.CurrentScore.Damage -= sub;`.
- Comparação W2PP: regra idêntica (`sub = Level / 5 + Value; MOB.CurrentScore.Damage -= sub;`).
- A etapa C# porta o cálculo determinístico da redução de dano (`DamageReduction`), sem mutar o MOB nem truncar valores intermediários.
- Resultado da auditoria: sete vetores cobrindo ausência de affect, nível padrão, nível zero com valor, divisão inteira truncada (`level / 5`), acumulação múltipla e afeto não relacionado.
- Fixture: `character-login-weaken.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
