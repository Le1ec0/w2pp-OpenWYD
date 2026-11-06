# MOB-AI-001 - IA generica de monstros

**Status:** em andamento. Tasks 5AS-5CE cobrem aquisicao passiva, chase/leash Combat, caminhada Peace e segmentos, summons Type 11 RouteType 5, `GetAttack` generico por SkillBar/SkillData 7.69, grupos/followers, propagacao de aggro passivo do leader aos followers, capacidade/agendamento/relocacao, `DUNGEONEVENT`, recuo ranged, saidas das Pistas Lv2/Lv6, ativacao Merchant no fim de RouteType 0, efeitos temporizados Type 17 Life Aura e Type 20 Poison em NPCs gerados vivos Idle/Peace/Combat (incluindo Combat sem inimigos), e Type 24 de summon no ciclo Peace e Combat com alvo selecionado, mais Types 17 Life Aura e 20 Poison e o tail generico `LessAffect` no timer de jogadores. Engine.Tests esta em `491/491`; smoke sintetico loopback cobre relays view-scoped, nao as transicoes internas de Merchant ou follower aggro. A leitura de `Route[len]` foi auditada como sem efeito no buffer normal produzido por `BASE_GetRoute`. Os 23 registros ativos Formation 4 leem cinco offsets dentro da tabela e coincidem com o catalogo C#; o fallback fora de 120 bytes permanece apenas para entradas hipoteticas sem equivalencia afirmada a C++ UB. Ataques area Type 22/23, outras rotinas de `CMob`/timer, timer Combat de summons sem alvo, comportamentos de quest e bosses nomeados, Host autenticado e E2E visual seguem pendentes.

## Objetivo

Portar para C# a fatia comum de IA de monstros da referencia comunitaria TMProject 7.69: adquirir e selecionar alvos, perseguir, executar o ataque fisico normal, abandonar um alvo fora do limite e retornar pelas rotas do gerador. O estado de batalha acionado por ataques deve ser consumido por esse ciclo. O wire continua sendo o contrato do cliente 7.69.

## Direcao de cobertura aprovada (19/10/2026)

Priorizar a cobertura das classes, metodos e fluxos ativos da IA de mobs antes
do refinamento por template. Mapear cada responsabilidade de `CMob`, do ciclo
`ProcessSecMinTimer`, de `GetFunc` e do caminho `CNPCGenerator` para os
componentes C# responsaveis; implementar o comportamento compartilhado no
desenho modular do projeto, sem transliteracao classe a classe.

`SkillBar` e `Special` sao dados lidos do template. O algoritmo de `GetAttack`
deve operar sobre esses valores e sobre `SkillData`, com testes por ramo do
metodo (seletor, distancia, `special2`, efeitos e retornos antecipados). Um
template pode permanecer como fixture de integracao, mas nao exige uma nova
implementacao do metodo nem uma bateria exclusiva por mob. Depois da cobertura
funcional ampla, a etapa de refinamento completa divergencias restantes,
casos de borda e validacao autenticada/visual.

## Evidencia auditada em 15-20/10/2026

- TMProject 7.69: `CMob.cpp::StandingByProcessor` implementa aquisicao por visao, movimento ocioso, seguimento de summons e progresso de segmentos; `CMob.cpp::BattleProcessor` seleciona alvos e decide entre perseguir, atacar e retornar. `ProcessSecMinTimer.cpp` executa primeiro Peace em seis grupos (`SecCounter % 6`) e depois Combat em quatro (`SecCounter % 4`) por callback, traduzindo as decisoes em movimento, ataque, teleporte ou remocao. `Server.cpp::SetBattle` ativa MOB_COMBAT, adiciona o alvo e tenta a fala de combate. `_MSG_Attack.cpp` propaga o combate aos NPCs das parties reais do atacante e do alvo.
- W2PP tem os mesmos corpos de `SetBattle` e `BattleProcessor` na comparacao feita. A divergencia confirmada esta em `CMob.cpp::SelectTargetFromEnemyList`: W2PP descarta jogador com `BaseScore.Level > MAX_LEVEL` que nao seja merchant; a verificacao equivalente da 7.69 esta comentada. O port segue 7.69 e nao aplica esse filtro. Em `StandingByProcessor`, a 7.69 faz RouteType 5 seguir o summoner mesmo sem Face especial; W2PP exige Affect24 ou Face315..345. O fluxo de repouso move direto a distancia >=13, usa caminho entre 5..12 e fica parado ate 4. Em `BattleProcessor`, ambos primeiro selecionam EnemyList; RouteType 5 valida Leader/Summoner/vinculo e, com alvo, aproxima do summoner quando distancia >=20, senao continua o processamento de ataque.
- Em 16/10, Task 5D integrou o movimento Peace a partir do planner existente. TMProject 7.69 e W2PP chamam `GetNextPos(0)` quando nao ha aquisicao passiva e o mob nao esta no segmento. 7.69 inicia em `SegmentX/Y + rand()%5 - 2` e limita a distancia por `BaseScore.Strength`; W2PP inicia no segmento exato. O C# segue 7.69, calcula/revalida um passo pelo WorldHub e relaya o Action. Engine cobre o limite de Strength; smoke loopback cobre RouteType 0 e uma view local.
- Em 16/10, Task 5E integrou o ramo Peace para summons Type 11 `RouteType=5`: a 7.69 segue o summoner diretamente a distancia >=13 com `DoTeleport`/Action `Effect=1, Speed=2` e `GetEmptyMobGrid` ate raio 9 (`MAX_SERVER=10`), usa `GetTargetPos` entre 5 e 12 com Action `Effect=0` e fica parada ate 4. W2PP so entra no sub-ramo seguidor quando `Affect[0].Type==24` ou Face esta entre 315 e 345; `GenerateSummon` atribui Affect 24 aos summons Type 11 deste port. C# valida party e estado vivo do summoner sob o WorldHub e impede o passo Peace quando `summonTargets` contem alvo atual valido, mesmo com `EnemyList` vazia; alvo desconectado, morto, oculto ou fora da janela e limpo como stale. O teste verifica os limiares, alvo/stale cleanup, busca bloqueada ate raio 4 com destino livre no raio 5, efeitos de wire e relay para viewers exclusivos de origem/destino, alem do viewer externo. O smoke de rede permanece Type 0. O ramo Combat RouteType 5 >=20 continua pendente.
- Em 16/10, Task 5F integrou o ramo Combat RouteType 5 >=20 ao loop de summon de 800 ms. TMProject 7.69 e W2PP concordam em `BattleProcessor`: selecionar alvo primeiro, validar Leader/Summoner/vinculo de party, definir destino no summoner e retornar o bit 2 quando `BASE_GetDistance >=20`; o timer chama `DoTeleport`, que usa `GetEmptyMobGrid` ate raio 9 e `GetAction` `Effect=1, Speed=2`. C# combina alvo, teleport e ataque em oportunidade ordenada, enumera a lista de summons do evocador membro da party, captura views sob o gate e omite o ataque mesmo se nenhum tile estiver livre. Abaixo de 20, permanece o ataque fisico. TDD Engine cobre 19/20, ataque seguinte, destino bloqueado e relays view-scoped; o smoke TCP loopback inclui `route5-combat=relayed`. Nao valida Host autenticado ou jogo visual.
- Em 16/10, Task 5K auditou 665 templates 7.69 de 816 bytes: cinco templates Clan 1 usam `[52,52,52,255]`; `NPCGener.txt` referencia quatro deles, exceto `Lich_Infernal`. SkillData row 52 e `Furia_de_Gaia`, InstanceType 4. Ambos os `GetAttack` escolhem SkillIndex 52, SkillParm 1, Motion 4 e Resist index 2 para o bar fixo; o threshold ranged 7.69 >=2/W2PP >=3 nao altera a selecao. O timer nao aplica SetAffect/SetTick com parm 1. W2PP nao contem runtime `SkillData.csv`, portanto a linha 52 foi confirmada apenas na tabela primaria 7.69. C# e o smoke loopback validam Resist[2], AttackOne 68 bytes, dano, HP/MP e view-scoping; nao e Host autenticado nem cliente visual.
- Em 16/10, Task 5L auditou o bar `[88,88,88,255]` em `HorizonCropper` e `Horizon_Cropper`, ambos templates gerados Clan 1. SkillData row 88 e `Lamina_das_Sombras`, InstanceType 1 / Resist -1. Motion e 4 em todas as distancias: `special2=255` e nao-zero no par ranged e aciona `if (special2)` tambem no W2PP. SkillParm 1 nao ativa SetAffect/SetTick e slot 3 e 255; W2PP nao contem `SkillData.csv`. RED/GREEN Engine.Tests e smoke TCP cobrem AttackOne, HP/MP e view-scoping; evidencia sintetica, sem Host autenticado ou cliente visual.
- Em 17/10, Task 5O auditou `[38,38,38,38]` em seis templates 7.69 de 816 bytes, todos Clan 1 e referenciados em `NPCGener.txt`. SkillData row 38 identifica `Fenix_de_Fogo`, `InstanceType=2`. TMProject 7.69 (`GetFunc.cpp::GetAttack`) usa slots 2/3 a distancia >=2; W2PP usa o mesmo par a >=3, mas os quatro slots identicos mantem `SkillIndex=38`, `SkillParm=1`, `Motion=4` em ambos. O sorteio entre slots 0..2 tambem resulta sempre em 38; Resist e `InstanceType-2=0`. O quarto-slot branch so cura quando `InstanceType=6`, portanto nao altera este bar mesmo com HP baixo. O timer so executa SetAffect/SetTick com `SkillParm=0`; este ataque permanece com parm 1. A linha 38 e confirmada pela tabela primaria 7.69; W2PP nao contem `SkillData.csv`.
- Em 17/10, Task 5P auditou `[40,40,40,40]` nos templates `Arvak` e `Treant_Amald` (816 bytes, Clan 1, NPCGener ativo). SkillData row 40 e `Névoa_Venenosa`, InstanceType 4. O GetAttack literal usa SkillIndex 40 / SkillParm 1 pelo default; o especial 125..127, que tambem mapeia para a skill 40, nao se aplica ao valor 40 do bar. Os limiares ranged diferem (7.69 >=2, W2PP >=3), mas as quatro entradas repetidas mantem os campos e Resist[2] invariaveis. Slot 3 nao cura com InstanceType 4 e o timer nao aplica SetAffect/SetTick com SkillParm 1. Smoke loopback sintetico cobre dano/resistencia, AttackOne, HP/MP e view-scoping; nao e Host autenticado nem WYD.exe visual.
- TMProject 7.69 guarda cinco pontos de segmento, ranges, waits e textos de acao por segmento em `NPCGENLIST`. A Task 1 adicionou esses campos ao `LegacyNpcGenerationCatalog`; os textos leem apenas o primeiro token e aceitam no maximo 78 caracteres, como `CNPCGene.cpp::ParseString/SetAct`. O spawn gerado copia `definition.Route` e `definition.RouteType` a `LegacyWorldNpc`; o runtime aplica essas rotas Peace aos NPCs suportados fora do seguidor RouteType 5.
- `FightAction` esta declarado no `NPCGENLIST` e e consultado por `SetBattle`. A busca no codigo fonte 7.69 nao localizou uma atribuicao/carregamento dessa matriz, portanto a origem dos textos deve ser rastreada nos dados efetivamente usados antes de inventar nomes de chave no parser.
- No C# existem `LegacyWorldNpc.Mode`, `CurrentTarget`, `EnemyList`, `GenerateIndex` e `RouteType`, alem de `SetNpcCombatState`/`TryGetNpcCombatState`. O runtime periodico processa Peace em seis shards e aplica `Aggro`, espera/segmento, passos de rota dos NPCs suportados e seguimento Peace de summons Type 11 RouteType 5; o estagio Combat de quatro shards roda logo depois, consumindo alvos adquiridos e aplicando chase/leash/ataque suportados.
- `NpcAttackConfirmation` codifica `MSG_AttackOne` e hoje e usado por `RunSummonBattleLoopAsync`, a cada 800 ms, junto com `TryApplyAllSummonAttacks`/`TrySelectSummonTarget`. Esse e o caminho de summons; sua existencia nao demonstra IA de monstros comuns. A rotina atual escolhe alvo por regras proprias e deve ser comparada com a prioridade do `EnemyList` antes de declarar equivalencia.
- `SetBattle` da 7.69 rejeita entidade vazia, id invalido e autoalvo; exige `USER_PLAY` para alvo jogador, mas nao aplica esse gate a alvo NPC; usa caixa retangular inclusiva de 23 tiles por eixo. `MAX_USER=1000`, `MAX_MOB=25000`. A ordem e `Mode=MOB_COMBAT` (valor 5), `AddEnemyList`, sorteio de chat e validacao de gerador. `AddEnemyList` ignora ids <=0, deduplica, usa ate 13 vagas e nao substitui entradas quando cheia; para ids <=MAX_USER tambem exclui ocultos e merchant. `SetBattle` nao define `CurrentTarget`; isso ocorre no proximo processamento. O tail generico de `_MSG_Attack` propaga entre as parties reais do atacante/alvo apenas NPCs vivos na lista, apos o alvo vivo ser persistido; nao adiciona NPCs proximos indiscriminadamente. A varredura especial KEFRA tem um defeito e nao deve ser descrita como insercao de todos os jogadores da area.
- `LegacyNpcMode.Combat` no C# vale 1 e a constante 7.69 `MOB_COMBAT` vale 5; sao estados internos, mapeados pelo planner. O spawn gerado copia `definition.RouteType` e a rota carregada para `LegacyWorldNpc`.
- A aquisicao passiva chama `GetEnemyFromView` somente quando `Leader==0`. A busca percorre a grade em ordem y/x: janela 9x9 normalmente; Clan 7/8 usa 16x16 com inicio -6. Requer HP>0 e Mode ativo, filtra RSV_HIDE apenas para jogadores e aceita candidatos (inclusive NPCs) apenas quando `ClanTable[mobClan][targetClan]==0`. Nao filtra Merchant. Depois verifica se a posicao do proprio mob esta dentro de `Segment±16`; nao exige que o candidato esteja dentro desse segmento. Logo a ordem e hostilidade devem vir explicitas ao planejador.
- `WorldHub.Enter` cria participante antes de carregar o personagem; somente um snapshot MOB completo representa o estado que pode corresponder a USER_PLAY. Parties, NPCs gerados e summons ficam em colecoes separadas. O port consegue enumerar summons vinculados a uma party de jogadores, inclusive de outro caster, mas nao possui PartyList de NPCs gerados; nao inferir grupos a partir de GenerateIndex.
- Os handlers fisico e de skill persistem snapshots locais depois do calculo. O tail SetBattle deve ocorrer por slot depois da persistencia MOB/Affect e apenas nos ramos que alcancam o tail da source, inclusive alguns com dano zero; nao aplicar em lote por todo resultado `Accepted`. A selecao atual de summon recalcula e sobrescreve seu alvo a cada oportunidade, por isso o alvo explicito precisa preceder esse fallback.
- `Server.cpp` inicia `TIMER_SEC` em 500 ms; `ProcessSecMinTimer.cpp` muda para 200 ms quando termina a contagem `ServerDown == 120`. Em cada callback, `SecCounter` seleciona Peace com `% 6`, iniciado em `MAX_USER + Sec6` e avancado por seis; depois seleciona Combat com `% 4`, iniciado em `MAX_USER + Sec4` e avancado por quatro. O C# usa o mesmo callback para os dois grupos, com offset 1000, nominal de 1,2 s entre oportunidades Peace e 800 ms entre oportunidades Combat. Se o mesmo NPC cair nos dois buckets, a aplicacao de Aggro ocorre antes do processamento Combat. `PeriodicTimer` espera o tick seguinte depois do trabalho anterior; sobrecarga pode alongar os intervalos e nao sera compensada com catch-up artificial. A fase inicial 500 ms/`ServerDown` nao existe no Host C# e permanece registrada como diferenca.
- `GetAttack` tambem seleciona habilidades automatizadas pelas `SkillBar`/`Special` do mob, ajusta dano por resistencia e pode escolher alvos/efeitos especiais. Um fixture concreto TM 7.69 `run/npc/Lobo` (Clan 2, `SkillBar[0..3]=255`) resulta em `SkillIndex=-1`, `SkillParm=1`, `Motion=4` e `Resist=-1`; nao aplica resistencia elemental. O ramo inicia com Motion aleatorio 4..6, mas o ramo NPC o substitui. Habilidade selecionada e configuracao Clan 4 ficam fora do primeiro contrato fisico; sem slots 255, a decisao deve ser Unsupported, nunca fallback fisico.
- A evidencia de runtime continua parcial: o passo Peace de RouteType 5 so cobre summons Type 11; seu ramo Combat >=20, Clan 4 e heads especiais continuam fora desta fatia. A autenticacao do Host e gameplay visual ainda nao foram validados. Engine.Tests 486/486 apos a revisao; o smoke loopback nao exercita o seguidor Type 11.

- Em 17/10, Task 5Q auditou `[32,32,32,32]` nos templates `Golem_Inferno` e `Lagarto_Inferno` (816 bytes, Clan 1, NPCGener ativo). SkillData row 32 e `Ataque_de_Fogo`, InstanceType 2. Os slots repetidos mantem SkillIndex 32 / SkillParm 1 / Motion 4 em ambos os limiares ranged; Resist[0] e aplicado, o slot 3 nao cura e o timer nao aplica SetAffect/SetTick com SkillParm 1. Smoke loopback sintetico cobre dano/resistencia, AttackOne, HP/MP e view-scoping; nao e Host autenticado nem WYD.exe visual.
- Em 17/10, Task 5R auditou `[39,39,39,39]` no template ativo `Imp_Inferno` (816 bytes, Clan 1, NPCGener). SkillData row 39 e `Inferno`, InstanceType 2. TMProject 7.69 e W2PP usam limiares ranged distintos, mas os quatro slots repetidos mantem SkillIndex 39 / SkillParm 1 / Motion 4 em ambos; Resist[0] e aplicado. O slot 3 nao cura, e o timer nao aplica SetAffect/SetTick com SkillParm 1. Smoke loopback sintetico cobre dano/resistencia, AttackOne, HP/MP e view-scoping; nao e Host autenticado nem WYD.exe visual.
- Em 17/10, Task 5S auditou `[48,48,48,51]` nos cinco templates Orc ativos `Orc_Mago`, `Orc_Sha_Trooper`, `Orc_Shama@@`, `Orc_Shaman` e `Orc_Shaman_` (816 bytes, NPCGener). SkillData row 48 e Fera Flamejante / InstanceType 2; row 51 e Enfraquecer / InstanceType 0. 7.69 e W2PP divergem nos limiares ranged, mas ambos os pares selecionam skill 48, SkillParm 1, Motion 4 e Resist[0]. Slot 3 nao cura (exigiria InstanceType 6), e o timer nao aplica efeitos. Engine.Tests RED 488/486/2, GREEN 488/488; smoke TCP loopback valida dano, AttackOne, HP/MP e view-scoping, sem Host autenticado ou WYD.exe visual.
- Em 18/10, Task 5X auditou `[255,255,51,255]` no template ativo `Carbuncle_Ouro` (816 bytes, Clan 1, NPCGener). SkillData row 51 e Enfraquecer / InstanceType 0. TMProject 7.69 seleciona slots 0/1 abaixo da distancia 2 e 2/3 a partir de 2; W2PP usa limiar 3. Rolls 85..99 selecionam SkillIndex 51 / Resist=-2; todos os caminhos usam SkillParm 1 / Motion 4, Resist -2 fica fora da faixa elemental 0..3, o slot 3 vazio nao cura e o timer nao aplica affects/ticks. Engine.Tests RED 488/486/2, GREEN 488/488; smoke TCP loopback valida dano 985 apesar de Resist[0]=50, AttackOne, HP/MP e view-scoping, sem Host autenticado ou WYD.exe visual.
- Em 18/10, Task 5Y auditou `[255,255,1,255]` nos templates ativos `Simio_Anciao`, `Guerreiro_Elfo` e `Batorero_Inf` (816 bytes, Clan 1, NPCGener). O loader 7.69 mapeia a coluna 7 para InstanceType; row 1 e Toque Sagrado / InstanceType 0 (2 e InstanceAttribute). TMProject 7.69 seleciona slots 0/1 abaixo da distancia 2 e 2/3 a partir de 2; W2PP usa limiar 3. Rolls 85..99 selecionam SkillIndex 1 / Resist=-2; rolls 0..84 preservam o ramo por distancia. SkillParm 1 / Motion 4; -1 e -2 ficam fora da faixa elemental, slot 3 nao cura e timer nao aplica affects/ticks. A primeira expectativa Resist=0/dano 1477 foi corrigida; revalidacao smoke confirma dano 985 com Resist[0]=50. Engine.Tests 488/488; sem Host autenticado ou WYD.exe visual.
- Em 18/10, Task 5Z auditou `[255,255,2,255]` nos templates ativos `Templario_Amald` e `Templario_Inf` (816 bytes, Clan 1, NPCGener). SkillData row 2 e Golpe Duplo / InstanceType 1; os slots de distancia seguem 7.69 >=2 e W2PP >=3, enquanto rolls 85..99 selecionam SkillIndex 2 / Resist=-1. SkillParm 1 / Motion 4, dano fisico sem modificador elemental e sem timer effect. Engine.Tests 488/488; smoke TCP loopback valida dano 985 com Resist[0]=50, AttackOne, HP/MP e view-scoping, sem Host autenticado ou WYD.exe visual.
- Em 18/10, Task 5AA auditou `[255,255,12,255]` nos templates ativos `Talos` e `Talos_Clone` (816 bytes, Clan 1, NPCGener). SkillData row 12 e Espada da Fenix / InstanceType 1. TMProject 7.69 seleciona o par ranged em distancia >=2; W2PP em >=3. Rolls 85..99 selecionam SkillIndex 12 / Resist=-1; como `special2=255` e nao-zero, os caminhos usam SkillParm 1 / Motion 4 e mantem o dano fisico sem modificador elemental. Slot 3 vazio e timer nao aplica efeito. Engine.Tests 488/488; smoke TCP loopback valida dano 985 com Resist[0]=50, AttackOne, HP/MP e view-scoping, sem Host autenticado ou WYD.exe visual.
- Em 18/10, Task 5AB auditou `[32,35,32,35]` nos templates ativos `Troll_Caido@@`, `Troll_Mago_` e `Troll_Mago` (816 bytes, Clan 1/1/2, NPCGener). SkillData rows 32/35 sao Ataque de Fogo e Tempestade de Meteoros, ambos InstanceType 2. TMProject 7.69 troca slots ranged em distancia >=2 e W2PP em >=3, mas os pares repetidos `[32,35]` preservam a mesma selecao; rolls 0..49/50..84/85..99 escolhem skill 32/35/32. SkillParm 1 / Motion 4 / Resist[0]; slot 3 nao cura e timer nao aplica affects/ticks. TDD GREEN Engine.Tests 488/488 e smoke TCP loopback valida dano 1477 com Resist[0]=50, AttackOne, HP/MP e view-scoping. Duas revalidacoes posteriores da suite terminaram 487/488 por timeout em `CurrentScoreLoginStartupTests`; invocacoes diretas do DLL atual confirmaram que os dois casos invalidos saem com erro antes do bind, mas a causa do timeout do harness nao foi determinada. Sem Host autenticado ou WYD.exe visual.
- Em 18/10, Task 5AC auditou `[17,17,17,17]` no template ativo `Rei_Glantuar` (816 bytes, Clan 8, NPCGener). SkillData row 17 e Espada Flamejante / InstanceType 2. TMProject 7.69 troca slots ranged em distancia >=2 e W2PP em >=3, mas os quatro slots iguais mantem SkillIndex 17 / Resist[0] para todos os rolls e distancias. `special=17/special2=17` resulta em SkillParm 1 / Motion 4; o slot 3 nao cura (InstanceType 2), e o timer exige SkillParm 0 para affects/ticks. TDD RED Engine.Tests 488/486/2; GREEN 488/488. Planner e WorldHub cobrem distancias 1/2/3/4, rolls 0/49/50/84/85/99, layout exato, HP baixo e Clan 4. Smoke TCP loopback valida dano 1477 com Resist[0]=50, AttackOne, HP/MP e view-scoping, sem Host autenticado ou WYD.exe visual.
- Em 18/10, Task 5AD auditou `[255,255,79,255]` no template ativo `Arqueiro_Elfo` (816 bytes, Clan 1; 26 referencias Leader e 26 Follower em NPCGener; SHA-256 em PORT_TRACEABILITY). SkillData row 79 e Tempestade_de_Raios / InstanceType 1 / Resist -1. TMProject 7.69 muda para o par ranged em distancia >=2 e W2PP em >=3; em d=2 e rolls 0..84, o SkillIndex primario diverge (79 vs -1), mas ambos usam Motion 4 porque `special2=255` e nao-zero. Rolls 85..99 selecionam Skill 79 em ambas; abaixo de d=2 rolls 0..84 retornam -1. Slot 3 e vazio e nao ha efeito de timer. Planner/WorldHub e smoke validam os campos e dano fisico; loopback sem Host autenticado ou WYD.exe visual.

- Em 18/10, Task 5AE portou `[33,33,37,255]` dos templates ativos Clan 1 `Kalitz_Mago` e `Kalitz_Mago_` (816 bytes; 28 referencias NPCGener; hashes em `PORT_TRACEABILITY.md`). SkillData row 33 e Relampago / InstanceType 5 / Resist[3]; row 37 e Trovao / InstanceType 0 / Resist -2. Rolls 0..49/50..84 escolhem Skill 33 e 85..99 escolhem Skill 37. TMProject 7.69 muda para slots ranged em distancia >=2; W2PP em >=3, mas Motion e 4 nos dois pares: `special2=33` consome `rand()%4` e `special2=255` aciona o mesmo default. O seletor bruto define SkillIndex e Resist. SkillParm 1, slot 4 vazio, sem cura ou timer effect. RED Engine.Tests 488/486/2; GREEN 488/488; WorldHub valida dano de dominio 1492 para Skill 37 / Resist -2, smoke TCP valida o mesmo sem modificador elemental. Sem Host autenticado ou WYD.exe visual.

## Comportamento proposto

1. **Entrada em combate.** No mesmo ponto em que o handler 7.69 aceita o ataque contra um mob vivo, atualizar o modo do alvo e registrar o atacante na `EnemyList`, sem escrever `CurrentTarget`. Respeitar a janela retangular inclusiva de 23 tiles por eixo; rejeitar estado vazio, ids invalidos e autoalvo; exigir `USER_PLAY` apenas para alvo jogador. `AddEnemyList` ignora ids <=0, deduplica, limita a 13 vagas e deixa a lista intacta quando cheia; para ids <=MAX_USER tambem ignora ocultos ou merchant. O modo passa a Combat antes da tentativa de insercao.
2. **Propagacao da party.** Replicar somente as duas varreduras genericas do tail `_MSG_Attack`: NPCs vivos listados na party real do atacante recebem o alvo; NPCs vivos listados na party do alvo recebem o atacante. Em parties de jogadores, incluir todos os summons vinculados ao leader, mesmo que outro membro os tenha invocado. Remover summons vazios/mortos pelo caminho de remocao; nao adicionar NPCs proximos nem inventar parties de NPC gerado. Preservar os `continue` dos caminhos sem tail. Este estado passa a ter consumidor no combate, em vez de permanecer como metadado sem efeito.
3. **Aquisição passiva.** Monstro comum em repouso, sem Leader, percorre candidatos hostis da visao na ordem y/x da grade; remove candidatos mortos/inativos e jogador oculto, mas nao aplica filtro Merchant. So aceita a oportunidade quando a posicao do mob esta dentro do segmento atual ±16. Candidato pode ser NPC. O resultado solicita `SetBattle` para o alvo e propaga party; nao grava `CurrentTarget`, que sera escolhido no proximo tick. RouteType 5 usa no repouso os limites de follow 13/5/4; em Combat, primeiro seleciona EnemyList e aplica a regra separada de distancia 20 antes de atacar.
4. **Lista e escolha do alvo.** Limpar entradas vazias, mortas, ocultas ou fora da janela; ignorar IDs invalidos sem remover a vaga. Escolher o menor escore de distancia, somando 2 a alvos NPC; em empate, a ultima vaga vence. Usar os ranges por clan, setor especial e Kefra definidos em 7.69. Manter o comportamento 7.69 para jogadores acima do nivel maximo.
5. **Perseguicao, ataque e retorno.** Aplicar range de ataque, chance baseada em INT/DEX e raio especial de Kefra. Nesta fatia, resolver a oportunidade normal de ataque fisico contra um jogador, com dano/HP autoritativos e relay 7.69. Ao ultrapassar a area de leash definida pelo segmento, limpar o combate e retornar ao percurso. Integrar os RouteType e segmentos que o ciclo generico realmente consome; nao transformar isto em reescrita do sistema completo de spawn.
6. **Rotas e acoes.** Ler os cinco pontos, ranges e waits necessarios a `SetSegment`/`GetNextPos`, incluindo chegada, espera, retorno e mudanca de segmento. Emitir `SegmentAction`, `FleeAction` e `FightAction` somente quando a fonte de dados provar que estao preenchidos; sem texto carregado, o efeito e no-op como na source.
7. **Execucao e relay.** Criar um loop async cancelavel para monstros gerados que nao pertencam a `Participant.SummonIds`; summons ficam sob o consumidor existente para evitar dois ataques por oportunidade. O loop usa quatro grupos equivalentes a `SecCounter % 4`; o host inicia esse agendamento em 200 ms depois de estar pronto para aceitar conexoes, de modo que cada mob seja reavaliado em cerca de 800 ms. A 7.69 usa 500 ms durante a inicializacao e troca para 200 ms quando `ServerDown == 120`; essa fase de inicializacao nao existe no Host C# e a diferenca deve permanecer registrada. Resolver mutacao e dano sob o lock autoritativo do `WorldHub`; relays usam os frames e as visoes ja existentes (`MSG_Action`, `MSG_AttackOne`, remocao e chat), sem criar opcode.

### Integracao com summons existente

O ciclo comum nao deve duplicar o ataque de summons. A proposta e manter um unico ataque por oportunidade no caminho atual, passar a priorizar um alvo valido recebido por `SetBattle`/`EnemyList` e manter a selecao atual apenas quando nao houver alvo explicito. Isso conserva a funcionalidade C# ja existente enquanto a prioridade explicita aproxima a semantica 7.69. A diferenca de selecao automatica restante fica registrada como divergencia ate haver comparacao e decisao comportamental; nao usar esse caminho para afirmar equivalencia total. O loop generico identifica summons pela associacao a `Participant.SummonIds`, nao apenas por `RouteType`.

## Arquitetura

- Adicionar um planejador deterministico e sem I/O no modulo existente `WydCdk.World` (por exemplo, `LegacyMobAiPlanner`). Ele recebe snapshot do mob, gerador, candidatos, terreno/visao e valores aleatorios explicitos; retorna uma decisao tipada de estado, movimento, ataque, retorno, fala ou remocao.
- `WorldHub` continua autoridade de posicao, HP, party, alvo e lista de inimigos. Coleta a entrada sob o lock, aplica a decisao sob o mesmo lock e devolve resultados/relays ao Host; o planejador nao depende de `WorldHub`, TCP ou banco.
- `Program.cs` conduz o loop com `PeriodicTimer`, `CancellationToken` e shutdown cooperativo, iniciando-o depois de `listener.Start()` sem aguardar antes do accept. O runtime nao chama `Task.Run` para I/O e nao envia payload de autenticacao em logs. Falhas do loop devem pedir cancelamento do Host e ser observadas no shutdown.
- `WorldHub` captura destinatarios de visao junto com a mutacao sob o gate; o Host codifica e envia fora dele. `BroadcastAsync` e global e nao atende a esse contrato. O teste de rede usa apenas `IPAddress.Loopback` e verifica os dois endpoints.
- Estender o catalogo de NPC somente com os campos de geracao comprovadamente consumidos pelo ciclo. Nao criar projeto/servico novo e nao alterar o wire versionado.
- `GetAttack` usa os quatro slots/`SkillData.InstanceType` para nao-Clan-4 e `BaseScore.Special` para Clan 4; os mapas comuns 121..128 seguem o par selecionado. Linha SkillData ausente falha fechado. Quatro slots 255 produzem `SkillIndex=-1`, `SkillParm=1`, `Motion=4`, sem resistencia elemental. O dano usa `BASE_GetDamage` com roll 99..110; no timer, o miss usa roll 1..100 e marcador -3. Os efeitos Type 17 Life Aura e Type 20 Poison em NPCs gerados Idle/Peace foram adicionados nas Tasks 5BK/5BL; os demais efeitos automatizados do timer e o tail normal contra NPC continuam fora do port. `NpcAttackConfirmation` segue isolado para summons; manter `CurrentHp`/`TargetID` com largura wire de 32 bits no encoder de mob normal.

## Fora deste bloco

- Scripts de quests, arenas, Pistas, bosses nomeados, drops e efeitos especiais que cercam o timer generico; cada um continua em sua propria fatia com evidencia da 7.69.
- O escopo inicial desta especificacao nao incluia grupos e calendario; a decisao posterior de cobertura por metodo agora inclui esses fluxos como continuacao do port de `CNPCGenerator`/`GenerateMob`.
- Troca de `ClientVersion`, pacote ou opcode.
- O ciclo Peace do summon RouteType 5 agora processa expiracao Type 24 (Task 5BM), mas nao o faz no timer Combat; exceto pelos Types 17 e 20 das Tasks 5BK/5BL, o caminho generico ainda nao executa os demais efeitos automatizados do timer (`SetAffect`/`SetTick`) nem o tail normal de ataque contra NPC. Esses ramos permanecem no backlog.

## Validacao e aceite

- Testes deterministas do planejador para entrada/saida de combate, ids/estados invalidos e jogador fora de `USER_PLAY`, alcance 23 inclusivo, filtros de insercao oculto/merchant, deduplicacao/capacidade 13, party real dos dois lados, limpeza de alvos, prioridade por distancia/penalidade NPC e empate pela ultima vaga, filtros 7.69, limites de leash, ataque/perseguicao, retorno e avancos/esperas de segmento.
- Testar `GetAttack` por ramo: faixas do seletor, troca de par em distancia 2, mapeamentos `special2`, resistência por SkillData, Clan 4 via `BaseScore.Special`, specials 121..128, slot 3 e entradas sem metadata. Manter somente a cauda normal contra NPC como limite explicito.
- Testes de catalogo para campos de rota, ranges/StartWait resolvidos no spawn e acao vazia; nao inventar chaves `FightAction` enquanto a origem delas nao estiver identificada.
- Testes do `WorldHub` para garantir que uma decisao aplicada atualize posicao/HP/alvo sob autoridade do mundo, que summons consumam o alvo explicito recebido antes do fallback atual, que o ciclo nao ataque duas vezes por tick e que alvos invalidos nao recebam dano.
- Build da solution com SDK fixado 10.0.401, Engine.Tests e smoke TCP/headless isolado em que um NPC gerado adquire, persegue e ataca um jogador; verificar o frame recebido e que participantes fora da visao nao recebam o relay. Confirmar bind/connect loopback, usar timeouts, registrar `dotnet --info` e limites ambientais conforme AGENTS.md.
- Somente apos esses gates atualizar in-place `BACKLOG.md`, `PROGRESSO.md` e a secao MOB-AI de `PORT_TRACEABILITY.md`. E2E visual continua pendente ate executar com a release cliente 7670 compilada da TMProject.

## Fontes principais

- TMProject: `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/CMob.cpp` (`StandingByProcessor`, `BattleProcessor`, `AddEnemyList`, `SelectTargetFromEnemyList`, `SetSegment`).
- TMProject: `.../TMSrv/ProcessSecMinTimer.cpp` (consumidores de repouso e combate; grupos `% 4`).
- TMProject: `.../TMSrv/Server.cpp::SetBattle` e `.../TMSrv/_MSG_Attack.cpp` (entrada e propagacao de party).
- W2PP: `W2PP/Source/Code/TMSrv/CMob.cpp`, `Server.cpp`, `_MSG_Attack.cpp` e `ProcessSecMinTimer.cpp`.
- C#: `src/WydCdk.World/WorldHub.cs`, `LegacyNpcGenerationCatalog.cs`; `src/WydCdk.Server/Program.cs`; `src/WydCdk.Protocol/NpcAttackConfirmation.cs`.

## Evidencia Task 5BK - Type 17 Life Aura (21/10/2026)

TMProject 7.69 and W2PP agree that the Idle/Peace timer applies Type 17 after
dead-NPC cleanup: HP gains `AffectLevel / 2 + AffectValue`, clamps to
`[1, MaxHp]`, and finite time below `32400000` decrements once. At expiry the
affect fields clear; the timer requests score refresh if HP changed or the
effect expired. C# ports this only for generated, alive Idle/Peace NPCs,
atomically in WorldHub, and relays `SetHpDam` then view-scoped `UpdateScore`.
Other affect types and Combat/non-generated call sites remain open. Local
evidence: Engine.Tests `490/490`, full solution build with 0 errors and the
pre-existing CS8604, and loopback `--mob-ai-smoke` marker
`life-aura=hp-score-relayed`; no authenticated or visual E2E.

## Evidencia Task 5BL - Type 20 poison (21/10/2026)

TMProject 7.69 and W2PP agree on Type 20 NPC damage: derive `AffectValue`
from signed `LearnedSkill / MAX_USER`, truncate `HpLose`, cap the loss with
NPC `Perda = 10 * (100 - LearnedSkill / MAX_USER)`, then clamp HP to
`[1, MaxHp]`. Active SkillData rows 20, 21 and 40 select this timer type. The
player-only default `Perda` differs between sources, but their generated-NPC
branches match. C# processes only generated, alive
Idle/Peace NPCs; finite duration decrements below `32400000` and clears at
expiry. It relays signed `SetHpDam` followed by view-scoped 7.69 `UpdateScore`
when HP changed or the affect expired. Other effect types and Combat/non-generated
call sites remain open. Local evidence: Engine.Tests `490/490`, full solution
build with 0 errors and the pre-existing CS8604, and loopback `--mob-ai-smoke`
marker `poison=hp-score-relayed`; no authenticated or visual E2E.

## Evidencia Task 5BM - Type 24 lifetime de summon em Peace (21/10/2026)

TMProject 7.69 and W2PP create standard skill summons with affect Type 24
time 20; summon IDs 28..37 use persistent time `2000000000`. Their timer
decrements finite durations below `32400000` and calls `DeleteMob(idx, 3)` at
zero for mobs. C# now applies that countdown on the RouteType 5 Peace shard
when the summon has no enemy or live selected target. Expiry removes the
summon and owner relationship under the WorldHub gate and relays captured-view
`RemoveMob(type=3)`; the combat timer is still pending. Local evidence:
Engine.Tests `490/490`, full solution build with 0 errors and the pre-existing
CS8604, and loopback `--mob-ai-smoke` marker
`summon-expiry=type3-relayed`; no authenticated or visual E2E.

## Evidencia Task 5BO - Types 17/20 em Combat para NPCs gerados (21/10/2026)

TMProject 7.69 e W2PP chamam `ProcessAffect` para mobs vivos em Combat antes do
restante do ciclo, inclusive quando ainda nao ha alvo ativo; Type 17 aplica o
heal `AffectLevel / 2 + AffectValue` e Type 20 aplica o dano NPC com `Perda`
limitada por `LearnedSkill`. O C# reutiliza essas formulas e a expiracao ja
portadas para Idle/Peace. O runtime agora consome o efeito na quarta visita
Combat de cada NPC gerado (quatro oportunidades de 800 ms, cerca de 3.2 s),
relaya `SetHpDam` antes de `UpdateScore` e captura a view. A fase inicia por
NPC na primeira visita Combat, sem preservar o offset global por index da
source. Tests Engine `490/490`; build completa sem incremental, 0 erros e o
CS8604 preexistente; smoke TCP confirma Life Aura e Poison sem alvo, frames
ordenados e view-scoped (`combat-affects-no-target=hp-score-relayed`). Sem
Host autenticado ou E2E visual.

## Evidencia Task 5BN - Type 24 lifetime de summon em Combat (21/10/2026)

TMProject 7.69 and W2PP call `ProcessAffect` for Combat mobs when
`index % 16 == SecCounter % 16`; their steady timer interval is 200 ms. C#
processes a Type 24 timer once every four 800 ms summon battle opportunities
when a live target is selected, matching the 3.2 s interval. Expiry removes
the summon before its attack and relays captured-view `RemoveMob(type=3)`.
Eligible summons share one C# phase rather than the source's per-mob offset;
summons without a live selected target remain pending. Local evidence:
Engine.Tests `490/490`, full solution build with 0 errors and the pre-existing
CS8604, and loopback `--mob-ai-smoke` marker
`summon-combat-expiry=type3-relayed`; no authenticated or visual E2E.

## Evidencia Task 5BP - Type 22 timer de NPC gerado (21/10/2026)

TMProject 7.69 and W2PP only build the Thunder attack relay for player
indices; their mob path still consumes finite Type 22 time in `LessAffect`,
clears the affect at zero and requests a score refresh. C# now persists that
countdown for generated living NPCs in Idle/Peace/Combat. A non-expiring tick
emits no client frame; expiry relays only `UpdateScore` to the captured view
and does not change HP or synthesize lightning damage. RED/GREEN Engine.Tests
`490/490`, full solution build with 0 errors and the pre-existing CS8604, and
loopback smoke marker `combat-thunder=score-only-relayed`; no authenticated or
visual E2E.

## Evidencia Task 5AF (19/10/2026)

O layout ativo `[38,34,38,51]` mapeia rolls 0..49/85..99 para Skill 38 / Resist[0]
e rolls 50..84 para Skill 34 / Resist[1], mantendo Motion 4. `special2=34/51`
consome `rand()%4`, mas o planner C# recebe o seletor explicitamente e nao
reproduz a sequencia global de `rand()`. O slot 3, row 51 InstanceType 0, nao
cura; teste com HP baixo e roll 50 confirma que ele nao substitui o ataque.
Engine.Tests passou `488/488` com loopback permitido; build completa teve 0 erros,
2 NU1900 pelo feed NuGet indisponivel e o CS8604 preexistente. O smoke de tres
peers validou AttackOne, dano 1477, HP/MP e view-scoping. O ZIP self-contained
local tem SHA-256 `221B9C8097B978BDFBBFB1786D2B8292F5CC4A8AD4558ED3C8FE57DC1B3DF972`;
SSH `wyd-vps` nao transferiu o pacote. Host autenticado e E2E visual continuam
pendentes.

## Evidencia Task 5AG (19/10/2026)

O template Clan 1 `Lich_Demo` usa `[52,52,51,255]`. Seletor 0..84 escolhe
Skill 52 / Resist[2] e 85..99 escolhe Skill 51 / Resist -2; perto, `special2=52`
consome `rand()%4` e mantem Motion 4 pelo default; no par ranged, `special2=255`
tambem seleciona Motion 4. Os limiares para trocar de par sao >=2 na TMProject
7.69 e >=3 no W2PP. Row 51 nao cura e `SkillParm=1` pula os
efeitos do timer. Engine.Tests passou `488/488`, incluindo fronteiras do seletor,
distancias, Clan 4 e HP baixo. WorldHub valida ambos os resultados com Motion 4.
Smoke de tres peers validou Skill 52, dano 1477, HP/MP e relay exclusivo
(`skillbar-lich-demo-mixed=relayed`). Build completa: zero erros, NU1900 por
feed de auditoria indisponivel e CS8604 preexistente. O pacote self-contained
local tem SHA-256 `AE1578961A459203EE2F9EFADD206FF721F7233DAF5F6C4F8277896059843B6C`;
SSH nao transferiu o runtime. Host autenticado e E2E visual continuam pendentes.

## Evidencia Task 5AH (19/10/2026)

`Mago_Inf` usa `[36,34,39,255]`: SkillData rows 36/34 sao InstanceType 3 /
Resist[1], row 39 e InstanceType 2 / Resist[0]. O seletor bruto escolhe Skill
36 em 0..49, Skill 34 em 50..84 e Skill 39 em 85..99. TMProject 7.69 muda para
o par ranged em distancia >=2 e W2PP em >=3; o ramo perto consome `rand()%4`
em `special2=34` e mantem Motion 4, enquanto o ranged `special2=255` tambem
seleciona Motion 4. O slot
3 vazio nao cura e SkillParm 1 pula efeitos do timer. Planner e WorldHub cobrem
distancias, limites do seletor, resistencias e Clan 4. Engine.Tests `488/488`;
build completa: 0 erros e CS8604 preexistente; build E2E 0/0. Smoke TCP de tres
peers valida Skill 34, Resist[1]=50, dano 1477, AttackOne, HP/MP e relay
exclusivo (`skillbar-mago-inf-mixed=relayed`). Corrigido o fixture de aquisicao
passiva para alvo adjacente, como exige a busca 7.69. `dotnet --info` identificou
SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager. Sem Host
autenticado nem E2E visual WYD.exe.

## Evidencia Task 5AI (19/10/2026)

`Shama_Amald` usa `[19,33,48,255]`: SkillData rows 19/33/48 tem
InstanceType 3/5/2 e Resist[1]/Resist[3]/Resist[0]. O seletor bruto escolhe
19 em 0..49, 33 em 50..84 e 48 em 85..99 independentemente de distancia.
TMProject 7.69 troca para o par ranged em distancia >=2 e W2PP em >=3;
`special2=33` consome `rand()%4` e mantem Motion 4 no ramo perto, e o ranged
`special2=255` tambem seleciona Motion 4. Slot 3 vazio nao cura; SkillParm 1 pula efeitos
do timer. Planner/WorldHub cobrem distancias 1/2/3, resistencias e Clan 4.
Engine.Tests `488/488`; build completa 0 erros e CS8604 preexistente; build
E2E 0/0. Smoke TCP de tres peers valida Skill 33, Resist[3]=50, dano 1477,
AttackOne, HP/MP e relay exclusivo (`skillbar-shama-amald-mixed=relayed`).
O NPC do smoke tem alcance zero sem ItemList/arma, por isso o alvo compartilha
tile; cobertura de tile adjacente segue no WorldHub. `dotnet --info` identificou
SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager. Sem Host
autenticado ou E2E visual WYD.exe.

## Evidencia Task 5AJ (19/10/2026)

`Mago_Amald` usa `[36,34,26,255]`: rows 36/34/26 sao InstanceType 3/3/7 e
Resist[1]/[1]/[5]. O seletor bruto escolhe 36 em 0..49, 34 em 50..84 e 26
em 85..99. TMProject 7.69 muda para o par ranged em distancia >=2 e W2PP em
>=3; `special2=34` perto e `special2=255` ranged deixam Motion 4 / SkillParm 1.
Resist 5 fica fora da faixa elemental 0..3 e o slot 3 esta vazio. Planner e
WorldHub cobrem distancias 1/2/3, fronteiras do seletor, resistencias e Clan 4.
Engine.Tests `488/488`; build completa 0 erros e CS8604 preexistente; E2E
build 0/0. Smoke TCP de tres peers valida Skill 26, Resist[5] sem modificador,
dano 985, AttackOne de 68 bytes, HP/MP e relay exclusivo
(`skillbar-mago-amald-mixed=relayed`). `dotnet --info` identificou SDK 10.0.401
e saiu 1 por acesso negado ao Service Control Manager. Loopback sintetico, sem
Host autenticado nem E2E visual WYD.exe.

## Evidencia Task 5AK (19/10/2026)

`Dark_Shadow_` e um template ativo de 816 bytes, Clan 1, com
`SkillBar=[26,255,255,255]` no offset 796 e duas referencias Leader/Follower
em `NPCGener.txt`. SHA-256 `8FE2AD15130ED655E70FC47856E8279ED2EB6260EEBF2164BCBC28E089F7F9F8`.
SkillData row 26 e Flash / InstanceType 7 / Resist 5. Em distancia <2, o
seletor bruto retorna Skill 26 para rolls 0..99, com Resist 5 em 0..49 e
Resist -1 em 50..99. A partir de distancia 2, TMProject 7.69 usa o par ranged
([255,255]); W2PP muda para esse par a partir de distancia 3. Como a barra
ranged nao tem skill, o seletor ainda escolhe Skill 26 / Resist 5 em 0..49;
em 50..99 o SkillIndex fica -1 / Resist -1. `special2=255` deixa Motion 4 /
SkillParm 1 em todos os casos; Resist 5 fica fora da faixa elemental 0..3,
slot 3 vazio nao cura, e Clan 4 segue Unsupported.

Planner/WorldHub cobrem distancias 1/2/3, fronteiras do seletor, barra exata,
variante e Clan 4. TDD RED teve dois fails esperados da Task 5AK; a primeira
execucao sandbox tambem teve 36 falhas ambientais (filesystem/socket/listener).
Na execucao local elevada, Engine.Tests passou `488/488`. O smoke TCP sintetico
de tres peers valida Skill 26 / Resist 5, dano 985 sem modificador, AttackOne
de 68 bytes / Motion 4, refresh HP/MP e relay somente ao alvo
(`skillbar-dark-shadow-primary=relayed`). Build completa sem incremental:
0 erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; build E2E
0 avisos/erros; `--mob-ai-smoke` exit 0. `dotnet --info` identificou SDK
10.0.401 e saiu 1 por acesso negado ao Service Control Manager. Evidencia
sintetica/loopback, sem Host autenticado nem E2E visual WYD.exe.

## Evidencia Task 5AL (19/10/2026)

`Ranger_Inf` e um template ativo de 816 bytes, Clan 1, com
`SkillBar=[255,79,73,255]` no offset 796 e 24 referencias Leader/Follower em
`NPCGener.txt`. SHA-256 `6A19B0C7A29DC23C159C34A53C056F8CE6BCFD05491627520DF4C0CE1351F22E`.
SkillData row 79 (`Tempestade_de_Raios`) tem InstanceType 1 / Resist -1; row 73
(`Ilusão`) tem InstanceType 0 / Resist -2. O seletor bruto escolhe row 79 em
50..84 e row 73 em 85..99; rolls 0..49 preservam a skill primaria. TMProject
7.69 troca para slots ranged em distancia >=2; W2PP em >=3. Com o layout, os
campos finais mantem Motion 4 / SkillParm 1. Resist -1/-2 nao aplica modificador
elemental; slot 3 vazio nao cura e o timer ignora SkillParm 1. Clan 4 segue
Unsupported.

Planner e WorldHub cobrem distancias 1/2/3, fronteiras do seletor, layout
alternativo e Clan 4. TDD RED teve duas falhas esperadas da Task 5AL, mais 36
falhas de permissao do sandbox em filesystem/loopback/listener; a execucao local
elevada passou Engine.Tests `488/488`. WorldHub manteve dano 995 com Resist[1]=50.
O smoke TCP sintetico de tres peers seleciona Skill 73 / Resist -2, confirma
dano 985, AttackOne de 68 bytes, refresh HP/MP e relay somente ao alvo
(`skillbar-ranger-inf-mixed=relayed`). Build completa sem incremental: 0 erros
e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke`
exit 0. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Evidencia sintetica em loopback, sem Host autenticado
nem E2E visual WYD.exe. Publish self-contained local: 200 arquivos / 83.979.739
bytes; ZIP `Server/Backup/Build/MOB-AI-001-Task5AM-20261019.zip`, SHA-256
`BFBFC64E1591A54D176670A919A64E8FDA937CDE605FDAE2344FD03D88527E6`. SSH
`wyd-vps` foi negado no sandbox e expirou na tentativa elevada read-only, sem
transferencia/restart/check UP-PVP.

## Evidencia Task 5AM (19/10/2026)

`Shaman_Inf` e um template ativo de 816 bytes, Clan 1, com
`SkillBar=[52,55,63,255]` no offset 796 e 24 referencias Leader/Follower em
`NPCGener.txt`. SHA-256 `D1A9DD297529AF7888E1DDF66F1297B75C6386018432874E35F5701FF181D427`.
SkillData rows 52/55/63 tem InstanceType 4/4/11 e Resist[2]/[2]/9. O seletor
bruto escolhe 52 em 0..49, 55 em 50..84 e 63 em 85..99, sobrepondo os slots
base em todas as distancias. TMProject 7.69 usa o par ranged a partir de d=2;
W2PP a partir de d=3. `special2=55` perto e `special2=255` ranged mantem
Motion 4 / SkillParm 1. Resist 9 nao aplica modificador elemental; Resist[2]
aplica. Slot 3 vazio nao cura, e SkillParm 1 pula os efeitos do timer. Clan 4
segue Unsupported.

Planner e WorldHub cobrem distancias 1/2/3, limites 0/49/50/84/85/99, as tres
resistencias, layout alterado e Clan 4. TDD RED teve duas falhas esperadas na
Engine.Tests e uma falha esperada no smoke (`attacks=[]`); GREEN Engine.Tests
`488/488`. WorldHub confirma 1492 com Resist[2]=50 e 995 para Resist 9. O smoke
TCP sintetico de tres peers seleciona Skill 55 / Resist[2]=50, confirma dano
1477, AttackOne de 68 bytes, refresh HP/MP e relay somente ao alvo
(`skillbar-shaman-inf-mixed=relayed`). Build completa sem incremental: 0 erros
e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke`
exit 0. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Evidencia sintetica em loopback, sem Host autenticado
nem E2E visual WYD.exe.

## Evidencia Task 5AP - Bruxa (19/10/2026)

`Bruxa`/`Bruxa_` usam `[24,27,28,27]` (Clan 7/8, 50 referencias em
`NPCGener.txt`). A 7.69 troca o par primario em distancia >=2 e W2PP em >=3;
ambos os pares consomem `rand()%4` por `special2=27` e geram Motion 4/5/7/8.
O seletor escolhe Skill 24/27/28 em 0..49/50..84/85..99. O slot 3 row 27 / Cura
(`InstanceType=6`) pode substituir o ataque em rolls 25..64 e HP inteiro
`CurrentHP*10/(MaxHP+1) <=8`, causando dano `MaxHP/10` ao mob ou leader valido
com menor percentual. O C# segue o contrato 7.69 e falha fechado para leader
positivo invalido; Clan 4 permanece fora desse recorte.

TDD RED teve duas falhas esperadas; Engine.Tests passou `488/488`. WorldHub
validou dano self e leader. O smoke loopback confirma Motion 7, SkillIndex 6,
dano self 100, AttackOne, UpdateScore e relay view-scoped
(`skillbar-bruxa-row27=relayed`). A evidencia nao cobre Host autenticado nem
E2E visual com `WYD.exe`.

## Evidencia Task 5AQ - Torre_Runica (19/10/2026)

`Torre_Runica` e um template ativo Clan 1 com `[255,255,255,27]`, duas
referencias em `NPCGener.txt` e `RouteType=1`. Slots 0..2 preservam o ataque
fisico base. O slot 3 row 27 (`InstanceType=6`) substitui-o em rolls 25..64
quando HP inteiro chega a <=8/10, enviando dano `MaxHP/10` a self ou leader com
menor fracao. A 7.69 troca para o par ranged em distancia >=2; W2PP em >=3.
Como o `special2` ranged e 27, o Motion usa o draw 0..3 e mapeia 4/5/7/8; C#
segue 7.69. O C# falha fechado para leader positivo nao resolvido.

Planner/WorldHub cobrem distancias 1/2/3, todos os motions, limites do roll,
HP, alvo self, leader invalido, Clan e layout. Smoke sintetico confirma Motion
7 a distancia 2, dano 100, AttackOne, UpdateScore e view-scoping. Engine.Tests
`488/488`; build completa 0 erros + CS8604 preexistente; smoke passou; SDK
10.0.401. Pacote local final: 200 arquivos / 83.990.847 bytes, ZIP SHA-256
`3EECFAA04C5FBDD0C2B28FA34AFE1FDB4AE3B319D206B76411F10AD6D8C3288A`. Host
autenticado, E2E visual e deploy remoto nao foram validados.

## Evidencia Task 5AN - Orc_Medico (19/10/2026)

`Orc_Medico`/`Orc_Medico_` usam `[40,40,51,29]`; comparar separadamente o
comportamento do slot 3 `InstanceType=6`: rolls 25..64 e HP inteiro
`CurrentHp*10/(MaxHp+1) <= 8` ativam ataque de dano positivo `MaxHp/10` ao mob
ou leader valido com menor percentual (empate permanece no mob). A source usa o
proprio mob quando `Leader<=0`, mas dereferencia IDs positivos sem validar
existencia/HP; o C# falha fechado para leader positivo ausente, morto, nao-gerado,
persistente ou summon. `MaxHP/10 == 0` tambem retorna `Unsupported` sem mutacao.
O nome de SkillData e Recuperar, mas o cliente reduz HP. Implementacao e testes
cobrem escolha do alvo NPC, update/morte, esses limites e relay view-scoped. O
smoke permanece sintetico; Host autenticado e E2E visual seguem pendentes.

## Evidencia Task 5AO - Feiticeira (19/10/2026)

`Feiticeira` (Clan 7) e `Feiticeira_` (Clan 8) usam `[33,36,38,27]`; as 55
referencias ativas estao no `NPCGener.txt`. SkillData row 27 tem
`InstanceType=6`. O ramo ranged de 7.69 comeca em distancia 2 e o do W2PP em
distancia 3. O C# reproduz o Motion ranged por `rand()%4` (4/5/7/8), o seletor
0..49/50..84/85..99 (rows 33/36/38) e a substituicao do slot 3 com dano
`MaxHP/10` em HP <=8/10 e roll 25..64. O harness de tres peers confirma Motion 7,
dano proprio 100 e relay view-scoped em TCP loopback. Continua sem prova de Host
autenticado ou E2E visual com `WYD.exe`.

## Evidencia Task 5AS - GetAttack generico (19/10/2026)

O planner agora deriva os campos do ataque dos valores da SkillBar e de
`SkillData.InstanceType`, sem branches por layout de template. Casos de teste
representam o seletor, distancia, sete valores de `special2`, resistencia,
slot 3, metadata ausente e limites ainda nao portados. O C# segue a fronteira
ranged 7.69 d>=2, embora W2PP use d>=3.

Engine.Tests `488/488`; rebuild da solution sem incremental: zero erros e o
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. O smoke loopback usa
`[17,255,40,255]` sintetica, valida Skill 17, dano 1477, AttackOne, HP/MP e
relay view-scoped (`getattack-generic=relayed`). `dotnet --info` identificou
SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager. Sem Host
autenticado ou E2E visual. Tasks 5AT/5AU depois concluiram Clan 4/specials e a
inicializacao da rota em `GenerateMob`.

## Evidencia Task 5AT - Clan 4 e specials 121..128 (19/10/2026)

TMProject 7.69 seleciona o par do Clan 4 por `BaseScore.Special`; W2PP mantem
SkillBar, divergencia que o planner trata seguindo 7.69. Os mapas comuns
121..128 alimentam o mesmo seletor/SkillData dos demais mobs. WorldHub propaga
Special1..4 a partir do snapshot e entrega copia defensiva. Engine.Tests
`488/488`; sem mudanca wire.

## Evidencia Task 5AU - geracao dos pontos de rota (19/10/2026)

`GenerateMob` de TMProject 7.69 e W2PP resolve os cinco segmentos no spawn:
para X nao-zero, cada eixo sorteia de `base-range` ate `base`; X zero zera X/Y;
`WaitSec` comeca em `SegmentWait[0]`; `PosX/PosY` nao-zero reancora todos os
pontos. C# agora armazena a rota resolvida, iguala a posicao inicial ao Start e
ancora os pontos na entrada da Pista quando ha override. Engine.Tests `488/488`;
build completa: 0 erros e CS8604 preexistente; smoke TCP loopback passou com
`getattack-generic=relayed`, rotas e view-scoping. `dotnet --info` identifica SDK
10.0.401 e sai 1 por acesso negado ao Service Control Manager. Task 5AV depois
acrescentou codigo de follower/group spawn e relay, ainda sem validacao final;
contagem/capacidade e agenda seguem pendentes. Sem Host autenticado ou E2E visual.

## Handoff 20/10/2026 - Tasks 5AV-5BH concluidas localmente

Task 5AV foi validada: spawn de followers/grupos e relay view-scoped, com a
capacidade por origem, agenda e relocacao refinadas nas Tasks 5AW-5AX. Task 5AY
porta `DUNGEONEVENT`; 5AZ/5BA ajustam HP inicial e variacao de Clan; 5BB porta
o fim de RouteType 3; 5BC o recuo ranged; 5BD o descarte RouteType 5 com Leader
invalido; 5BE a limpeza da Pista Lv6; e 5BF a saida da Pista Lv2 durante o
movimento Peace; 5BG ativa Merchant em NPCs gerados RouteType 0 ao fim da rota.
Task 5BH propaga o alvo da aquisicao passiva do leader gerado para followers
vivos dentro de +/-23 por eixo, conforme TMProject 7.69 e W2PP. O loop da
party do alvo continua condicionado a `Target < MAX_USER`: alvo jogador
propaga uma vez; alvo summon nao. Limpeza de follower morto/`PartyList` nao
faz parte da fatia.
Detalhes fonte/teste por fatia estao em
`Server/docs/PORT_TRACEABILITY.md`; o smoke TCP atual esta em
`Server/docs/CLIENT_E2E_SMOKE.md`.

A verificacao Task 5BH terminou com Engine.Tests `490/490`, build completa sem
incremental com 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`, build E2E 0 avisos/erros e smoke loopback
geral aprovado; esse smoke nao exercita Merchant nem follower aggro interno.
SDK 10.0.401 (`dotnet --info` exit 0). Sem pacote/publicacao,
transferencia/restart na VPS, Host autenticado, E2E visual, Site/producao,
commit ou push.

Auditoria adjacente do timer: `GetAttack` em TMProject 7.69 e W2PP inicializa
`SkillIndex=-1`/`SkillParm=0`; todos os ramos que selecionam skill valida
definem SkillParm diferente de zero. Portanto o guard de `SetAffect`/`SetTick`
(`skill >= 0 && skill < MAX_SKILLINDEX && SkillParm == 0`) nao e alcancavel
para o resultado atual de `GetAttack`; nao adicionar efeito C# para esse
caminho. Os indices 109..111 desviam por `goto KefraAttackLabel` para ataque em
area, comportamento nomeado de boss fora deste recorte generico.

Auditorias adjacentes sem mudanca de codigo: `BASE_GetRoute` limpa os 24 bytes
antes de escrever direcoes, tornando `Route[strlen(Route)]` NUL; `SetSegment` e
`_MSG_Action` rejeitam os valores derivados. Os registros ativos Formation 4
geram no maximo um follower e leem cinco offsets dentro da tabela. Proxima
auditoria: selecionar outra lacuna ativa de `CMob`/timer.
## Evidencia Task 5CC - Poison Type 20 no timer de jogadores (23/10/2026)

TMProject 7.69 calls `ProcessAffect` for live players every matching 16-slot
second. Its Type 20 player branch (`Server.cpp:6210-6253`) truncates `HpLose`
0.9 to integer zero and limits loss to 500 HP per tick. W2PP uses a 1000 HP
cap (`Server.cpp:5145-5187`); C# follows the selected 7.69 contract. Both
versions decrement finite time below 32400000, clear the full record on expiry,
send SetHpDam every tick, and refresh score only when HP changes or the affect
expires.

TDD GREEN Engine.Tests `491/491` covers the 500 HP cap, integer-truncation HP
floor, countdown/expiry and requested-HP cache. Loopback smoke marker is
`player-type20-poison=hp-score-relayed`; no authenticated or visual client E2E.
Full no-incremental solution build: 0 errors plus the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. Local self-contained publish is recorded in
`PORT_TRACEABILITY.md`.

## Evidencia Task 5CD - countdown Type 22 no timer de jogadores (23/10/2026)

O ramo Type 21 esta comentado em TMProject 7.69 e W2PP. Type 22 ativo percorre
duas areas centradas em `TargetX/TargetY`, preenche ate seis slots e envia
`MSG_Attack` Skill 33 ao handler normal de combate. O countdown compartilhado
decrementa tempo finito abaixo de 32,400,000 e limpa o registro de oito bytes na
expiracao, com refresh de score. A mudanca C# deste recorte porta somente esse
tail: nao executa o ataque periodico.

Engine.Tests `491/491`; smoke TCP
`player-type22-expiry=score-only-relayed` confere countdown silencioso,
UpdateScore na view capturada e ausencia de frame fora dela. Build completa sem
incremental: 0 erros mais CS8604 preexistente. Publish self-contained local
Task5CD: 200 arquivos/84.062.551 bytes; ZIP SHA-256
`6B9102C32EEA44BD03EF600AD46BDD5B3BF8963C1FEA7FAD90D168835073A7E0`. SSH
`wyd-vps` expirou em TCP/22; sem deploy, autenticacao de Host, E2E visual,
Site/producao, commit ou push.

## Evidencia Task 5CE - tail generico de affect no timer de jogadores (23/10/2026)

O timer de jogador agora percorre o `LessAffect` comum para todo affect ativo
nao-zero sem ramo especializado. TMProject 7.69 e W2PP declaram
`STRUCT_AFFECT.Time` unsigned: valor finito abaixo de 32.400.000 decrementa,
expiry limpa os oito bytes e atualiza score; tempo inicial zero faz wrap para
`UINT_MAX`. Os ramos Type 17/20 de HP e Type 16/33 de equipamento permanecem
especiais. O dispatcher das areas Type 22/23 segue pendente.

Engine.Tests `491/491`; smoke loopback passou com
`player-generic-affect-expiry=score-only-relayed`. Build da solucao em output
isolado: 0 erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`.
SDK 10.0.401; `dotnet --info` exit 1 por acesso negado ao Service Control
Manager. Sem publish/deploy. Probe SSH `wyd-vps` somente leitura foi negado no
sandbox e expirou elevado antes do handshake em TCP/22; sem sessao RDP ou
transferencia remota.
