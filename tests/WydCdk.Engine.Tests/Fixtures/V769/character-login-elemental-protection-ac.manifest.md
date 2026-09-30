# Affect Type=25 — Proteção Elemental (Parcela de ArmorClass)

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, no `BASE_GetCurrentScore`.
- Comparador obrigatório: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- Regra alvo 7.69: `def = Affect[i].Level / 2 + Affect[i].Value; if (Affect[i].Level >= 255) def += 30; MOB.CurrentScore.Ac += def;`.
- Comparação W2PP: o W2PP modifica apenas as resistências elementais (Fogo, Trovão, Gelo) e não adiciona ArmorClass para `Type=25`.
- A parcela de resistências do Type=25 já foi auditada separadamente em `character-login-resistance-affects.manifest.md`. Esta etapa isola a mutação de defesa física.
- Resultado da auditoria: sete vetores cobrindo ausência de affect, nível padrão (< 255), bônus limiar de nível 255 (`+30`), limite inferior 254 (sem bônus), divisão inteira truncada (`level / 2`), acumulação múltipla e afeto não relacionado.
- Fixture: `character-login-elemental-protection-ac.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
