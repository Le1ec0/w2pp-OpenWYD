# Affect Type=7 — Lâmina Congelada (Frozen Blade)

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, no `BASE_GetCurrentScore`.
- Comparador obrigatório: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- Regra alvo 7.69: `tmp = Affect[i].Level / 10 + 10; Att -= tmp;` e, caso `MOB.Equip[0].sIndex > 50`, `tempint -= (tmp + 10); MOB.CurrentScore.Int = tempint;`.
- Comparação W2PP: regra idêntica (`Att -= tmp;` e se `sIndex > 50`, `Int -= (tmp + 10)`).
- A etapa C# porta o cálculo isolado retornando os deltas acumulados de velocidade de ataque (`AttackSpeedDelta`) e inteligência (`IntelligenceDelta`), sem mutar o MOB nem truncar a aritmética inteira.
- Resultado da auditoria: sete vetores cobrindo ausência de affect, personagem jogador (`headItemIndex <= 50`), monstro/face acima de 50 (`headItemIndex > 50`), divisão inteira, acumulação múltipla e afeto não relacionado.
- Fixture: `character-login-frozen-blade.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
