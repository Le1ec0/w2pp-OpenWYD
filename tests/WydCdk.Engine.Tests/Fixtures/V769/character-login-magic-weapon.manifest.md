# `Affect.Type=9` - Arma Magica

- Fonte-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, `BASE_GetCurrentScore`.
- Para cada registro Type=9, o alvo calcula `Level * 5 / 20 + Value`, aplica `* 3 / 2` com divisao inteira e soma o resultado ao Damage.
- O alvo acrescenta `5` ao multiplicador de dano por registro. Quando `Class == 1` e o bit `0x80000` esta aprendido, triplica o dano calculado e acrescenta mais `10` ao multiplicador.
- O alvo nao altera Magic nesse bloco.
- O W2PP preserva a mesma formula de Damage/multiplicador, mas acrescenta `5` a Magic por registro Type=9. Essa diferenca nao foi escondida no port.
- O estagio C# e puro, nao aplica clamp, nao muta MOB e o coordenador projeta apenas o Damage auditado antes de Soul.
- Sete vetores cobrem vazio, divisao inteira, gate de classe, bit de mastery, repeticao e affects de outros tipos.
- SHA-256 do `Basedef.cpp` alvo: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp`: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
