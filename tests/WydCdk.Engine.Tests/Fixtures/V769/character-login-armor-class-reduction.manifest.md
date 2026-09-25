# `Affect.Type=12` - Reducao de AC

- Fonte-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, `BASE_GetCurrentScore`.
- O alvo calcula `fvalue = (100 - Value) / 100.0f` e grava `CurrentScore.Ac = (int)(Ac * fvalue)` para cada Type=12, em ordem de slot.
- O W2PP usa a mesma formula, o mesmo `float` e o mesmo truncamento para `int`; nao foi encontrada divergencia comportamental neste bloco.
- O estagio C# e puro, preserva a mutacao sequencial, nao aplica clamp adicional e o coordenador o projeta depois de Type=6.
- Sete vetores cobrem vazio, reducao positiva, truncamento float, repeticao sequencial, reducao total, fator negativo e affects de outros tipos.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
