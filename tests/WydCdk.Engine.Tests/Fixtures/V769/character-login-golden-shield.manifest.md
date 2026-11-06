# Affect Type=31 - Escudo Dourado (Golden Shield)

- Alvo 7.69: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, dentro de `BASE_GetCurrentScore`.
- Comparador: `Server/W2PP/Source/Code/Basedef.cpp`, tambem dentro de `BASE_GetCurrentScore`.
- Formula em ambas as fontes: `add = Affect[i].Level / 2 + Affect[i].Value; MOB.CurrentScore.Ac += add;`.
- A diferenca e a ordem geral: 7.69 aplica Type 24/31/25/16/11 numa primeira passagem por todos os slots; depois percorre todos os slots aplicando Type 12 e Type 8 bit 4. W2PP trata os branches no mesmo loop slot a slot. A formula isolada Type 31 coincide, mas o AC composto pode divergir quando outros modifiers intercalam.
- O C# inclui Type 31 na passagem AC ordenada do coordenador e da projecao de login; Type 8 bit 4 e aplicado nessa passagem e suprimido no composer geral para evitar dupla aplicacao.
- Sete vetores isolados cobrem ausencia, nivel/valor, divisao inteira, multipla acumulacao e tipo alheio. Os goldens W2PP agora usam a formula presente no source, em vez de esperar zero.
- O golden do coordenador e o golden de login tambem cobrem Type 31 com os outros modifiers AC e a ordem de slot.
- Fixture isolado: `character-login-golden-shield.golden.json`.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
