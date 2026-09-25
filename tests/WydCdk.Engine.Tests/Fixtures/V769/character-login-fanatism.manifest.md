# Affect Type=5 — Fanatismo / redução de DEX

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, no `BASE_GetCurrentScore`.
- Comparador obrigatório: `Server/W2PP/Source/Code/Basedef.cpp`, no `Buff Loop` de `BASE_GetCurrentScore`.
- Regra comum: para cada affect Type=5, `Dex = (short)(Dex * ((100 - Value) / 100.0f))`.
- A conversão usa `float` de precisão simples e narrowing para `short` em cada affect; a ordem dos slots é preservada e não existe clamp.
- Resultado da auditoria: alvo e W2PP são equivalentes nos sete vetores; o C# mantém a implementação como etapa pura e isolada.
- Fixture: `character-login-fanatism.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
