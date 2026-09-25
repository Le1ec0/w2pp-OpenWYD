# `Affect.Type=15` - Toque de Athena

- Fonte-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, `BASE_GetCurrentScore`.
- Cada registro Type=15 calcula `Level / 10 + Value` e soma o resultado aos quatro Special, limitando cada um em 255.
- O W2PP usa a mesma formula para Special2/3/4, mas limita Special1 em 200. O alvo 7.69 limita Special1 tambem em 255.
- O estagio C# recebe os quatro Special iniciais, retorna o snapshot puro e nao muta MOB. O coordenador o projeta depois de Type=9 e antes de Soul.
- Sete vetores cobrem vazio, divisao inteira, cap divergente, repeticao, maximo e affects de outros tipos.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
