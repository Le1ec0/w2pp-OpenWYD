# Affect Type=21 — Meditação (Meditation)

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, no `BASE_GetCurrentScore`.
- Comparador obrigatório: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- Regra alvo 7.69: `add = Affect[i].Level / 3 + 10; MOB.CurrentScore.Ac -= add;` e `tv = Affect[i].Level / 25 + Affect[i].Value; DAMAGEMULTI += tv;`.
- Comparação W2PP: a redução de AC é idêntica (`add = Level / 3 + 10; MOB.CurrentScore.Ac -= add;`), mas a adição ao multiplicador de dano diverge (`tv = Level / 10 + Value; DAMAGEMULTI += tv;` no W2PP vs `/ 25` no alvo 7.69).
- A etapa C# isolada porta o cálculo determinístico de `ArmorClassReduction` e `DamageMultiplierAdjustment` seguindo a referência 7.69 e expondo a divergência contra o W2PP.
- COMBAT-LOGIN-AFFECT-TYPE21-AC-001 integra `ArmorClassReduction` (`Level / 3 + 10`) ao primeiro passe de AC ordenado por slot no coordenador/login. COMBAT-LOGIN-AFFECT-TYPE21-DAMAGEMULTI-001 expõe o estágio no coordenador e inclui `DamageMultiplierAdjustment` no multiplicador final do login, seguindo 7.69 (`Level / 25 + Value`); W2PP diverge com `Level / 10 + Value`.
- Resultado da auditoria: sete vetores cobrindo ausência de affect, divergência de multiplicador em nível alto, nível zero (onde as fórmulas convergem), truncamento inteiro (`/ 3` e `/ 25`), acumulação de múltiplos affects e affect não relacionado.
- Fixture: `character-login-meditation.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
