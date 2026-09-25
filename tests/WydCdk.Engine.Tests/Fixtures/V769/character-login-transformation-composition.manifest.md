# Composition of audited Type=16 score stages

- Source-alvo: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp`, `BASE_GetCurrentScore`, affect loop `Type=16`.
- Comparador: `Server/W2PP/Source/Code/Basedef.cpp`, reproduzido somente no fixture/teste para separar coincidencias e divergencias antes de qualquer integracao.
- O estagio compoe os resultados ja auditados de Attack/Run, dano, AC, MaxHp, Critical, `Equip[0]`/`EF_SANC` e resistencia para o mesmo snapshot imutavel.
- A resistencia e o resultado do estagio geral de affects (`Type=3/8/16/25`), nao uma segunda soma de `Type=16`; isso evita duplicar `RegAdd`/resistencia elemental.
- A ordem dos affects permanece observavel: AC, MaxHp e resistencia sao sequenciais/ordenados; velocidade e equipamento preservam a ultima transformacao valida; dano/Critical acumulam.
- Os oito vetores incluem o caso sem transformacao, cada forma com divergencias relevantes, repeticao Wolf->Eden, gates de classe e affects de resistencia fora de `Type=16`.
- O metodo C# continua puro: nao muta MOB, nao troca o encoder ativo, nao entra no listener e nao e E2E de cliente.
- SHA-256 do `Basedef.cpp` alvo no momento da auditoria: `D84B2E358825CC0AF1FF8D0CFD316E09304892C15422A48C3D94A3D047927610`.
- SHA-256 do `Server/W2PP/Source/Code/Basedef.cpp` comparado: `C9E04E7F0AAC87DFDAB367B77F3DC8384355FE1166898FFEB62E095C551B99F7`.
