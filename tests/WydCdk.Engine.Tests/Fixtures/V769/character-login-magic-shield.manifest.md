# `Affect.Type=11` — Escudo Mágico

- Fonte-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, `BASE_GetCurrentScore`.
- O alvo soma `Affect.Level / 5 + Affect.Value` no AC para cada registro ativo.
- O W2PP soma `Affect.Level / 3 + Affect.Value`; a divergência é intencional e aparece nos limites de divisão inteira.
- O estágio retorna apenas o delta de AC; não aplica clamp, não muta MOB e não entra no encoder/login.
- Seis vetores cobrem vazio, divisão inteira, repetição, máximo de nível/valor e affects de outros tipos.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
