# Dados de skills

## Confirmado no legado

- O servidor inicializa `g_pSpell[MAX_SKILLINDEX]` lendo `../../Common/SkillData.csv` em `BASE_InitializeSkill()` (`Server/W2PP/Source/Code/Basedef.cpp`). O fallback é `../../TMSRV/Run/SkillData.csv`.
- `MAX_SKILLINDEX` é 248. Cada linha válida é indexada pelo primeiro campo (`Id`); IDs fora desse intervalo são ignorados.
- `AffectTime` é dividido por 4 durante a carga, antes de ser usado pelo TMSrv.
- O CSV tem duas colunas `Act`. O código legado passa o mesmo buffer para os dois `%s` do `sscanf`; portanto, a segunda coluna é a ação efetiva que fica no `STRUCT_SPELL.Act`. A skill 9 demonstra isso: a primeira ação contém `...24...` e a segunda `...8...`.

## Skills 9 e 10

Na tabela de referência do servidor (`Tools/Reference759/ServidorSource/Common/skilldata.csv`, extraída em `Server/artifacts/skilldata-reference/skilldata.csv`):

- ID 9: `Mestre_das_Armas`, passiva.
- ID 10: `Golpe_Mortal`, agressiva, `ManaSpent=20`, `Delay=15`, `MaxTarget=2`.

Há uma diferença de fonte que não pode ser ignorada: o `Release/Common/SkillData.csv` que existia no `HEAD` do projeto tem 151 linhas (IDs 0–150), e o ID 10 usa `InstanceValue=40`; a referência 7.559 extraída do `Source.rar` tem 248 linhas e `InstanceValue=100`. O loader aceita ambos os formatos, mas a tabela 7.559 não substitui a base do projeto.

O comentário `DESCOMPILADO POR SWEDKA` em `Tools/Reference759/ServidorSource/Code/Basedef.cpp` identifica uma versão descompilada de `BASE_GetSkillDamage`; ele não é uma tabela alternativa de skills. A presença e os parâmetros das skills 9 e 10 vêm do CSV e de referências a esses bits em `LearnedSkill` (`1 << 9` para Mestre das Armas e `1 << 10` para a perícia do caçador).

## Cliente versus servidor

- `Client/7600/SkillData.bin`, `Client/7662/SkillData.bin` e `Client/7559/SkillData.bin` têm o mesmo SHA-256 (`49126B17...`) e são o formato do cliente, com layout maior e transformação XOR `0x5A`.
- O C# não usa o binário do cliente como autoridade de combate. `LegacySkillDataTable` porta a leitura do CSV do servidor e conserva as duas colunas `Act`, expondo a segunda como ação efetiva.
- O CSV extraído é uma referência 7.59/7.559. Antes de tratá-lo como dado definitivo de produção para o 7.600, comparar uma fonte 7.600 correspondente. A implementação atual carrega a tabela por `TextReader`, mas ainda não a conecta ao cálculo de dano nem ao listener de ataque.

## Estado do port

- A sonda x86 compilada contra o `Basedef.h` real confirmou os campos usados no combate: `CurrentScore` em `STRUCT_MOB+92`, `CurrentScore.Mp` em `+120`, `SaveMana` em `+795`, `SkillBar` em `+796`, `GuildLevel` em `+800` e `Resist` em `+806`. O valor `+803` que existia no port era incorreto e foi corrigido.
- **Portado e testado:** modelo/loader de `SkillData.csv` nos formatos de 23 e 24 campos; `AffectTime /= 4`; teste dos IDs 9 e 10 e do comportamento das colunas `Act`; gate de metadados conectado ao listener quando `--skill-data` é informado; leitura tipada de `Class`, `LearnedSkill`, `CurrentScore` e `SaveMana` no `STRUCT_MOB`; fórmula autoritativa de custo de mana equivalente a `BASE_GetManaSpent`.
- **Portado e testado:** o listener aplica o custo autoritativo sob o lock do `WorldHub`; o relay de ataque preenche `CurrentMp`/`ReqMp` a partir do estado do servidor e a rejeicao por MP insuficiente envia `MSG_SetHpMp`. O marcador físico `-2` resolve alvo entre jogadores, aplica `BASE_GetDamage` e reduz o dano PvP para um quarto antes de mutar o HP do alvo; `Dam[]` continua sem autoridade do cliente.
- **Portado e testado:** `BASE_GetSkillDamage(dam, ac, combat)` e o cálculo-base `BASE_GetSkillDamage(skillnum, mob, weather, weapondamage)`, incluindo divisão inteira, curva, instâncias, classe, especiais, arma/clima, `Magic` e a exceção da skill 79. A composição de `WeaponDamage` também reproduz domínio, meia-contribuição da arma secundária e +40 de arma ancestral. O leitor de `ItemList.bin` reproduz o corpo XOR `0x5A`, o layout de 140 bytes e `BASE_GetItemAbility` para `EF_DAMAGE`, `EF_MAGIC`, `EF_PARRY`, `EF_DAMAGE2`, posição e sanctuary. `GetParryRate` reproduz os clamps, bônus de `Rsv` e contribuições de Dex/skill do atacante; o resolver agrega parry dos 16 slots do alvo e relaya `-3`/`-4` sem mutar HP. Com `--item-data`, o listener deriva `Magic` do `STRUCT_MOB`, `WeaponDamage` dos slots 6/7 e resolve os slots de alvo permitidos após o desconto de mana; o fator aleatório permanece injetável nos testes.
- **Pendente:** validar a fonte de dados exata do 7.600 contra `Tools/OriginalSources/WYDBR760.exe` — o teste operacional atual usa um perfil derivado em `Client\7600` — e ampliar o resolver autoritativo para regras completas de elegibilidade por mapa/PK/war, `Delay`/`Aggressive` fora do gate atual, gestão/persistência de guerra das zonas, `pHeightGrid`/carga de mapa e a sincronização legada completa do `DoRecall`, NPCs e morte/XP, além das demais regras de resistência. A ressurreição (`skill 99`) com spawn base de cidade/iniciante, `ChargeGuild` de `Guild.txt`, posição da sessão, `MSG_Action` e busca de célula livre por ocupação, cura (`InstanceType == 6`), desintoxicação (`InstanceType == 8`), aplicação genérica de `AffectType`/`TickType`, gate de mapa seguro/estado de guerra, bloqueios agressivos por `Rsv`/`AffectResist` e parry já cobrem as rotas confirmadas e testadas até aqui.
