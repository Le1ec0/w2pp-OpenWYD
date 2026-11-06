# Smoke E2E wire do cliente 7.670

## SKILL-SECOND-BAR-ACQUISITION-001 - refresh inicial de LearnedSkill[1] (28/10/2026)

Captura em `Backup/Build/SkillSecondBarVisual/capture-2026-10-28.md`.
Com cliente isolado `WYD.exe` SHA-256
`51BE30D3E55231D300ABD7BD41E046690DD43EE6A4DAF5F9CD9AF4A92EFF03E2`, conta
sintetica `SKBAR01` e Host/proxy presos a `127.0.0.1`, a rodada observou:

```text
0x0114: word0=0x00000080, word1 recebido=0x00000000, enviado=0x00000000
0x0337: word0=0x00000080, word1 recebido=0x00000000, enviado=0x00000010
```

O cliente entrou no mapa e abriu a janela de tecnicas com `S` apos o refresh.
Isso valida o transporte/consumo do frame na sessao visual; nao identifica a
celula do bit 4 nem valida aquisicao, persistencia ou limites. E uma unica
variante; faltam os controles `0x1`, `0x2`, `0x100`, a variacao independente de
Extra e a grade das outras classes. Runtime C# e schema nao foram alterados.

## COMBAT-ATTACK-SKILL-96-TYPE4-001 - expiry de Poder Superior (26/10/2026)

O `--mob-ai-smoke` instala Type 4 finito em um jogador sintético e confirma que
o expiry limpa os oito bytes do registro e transmite UpdateScore somente aos
dois peers TCP na view capturada; o peer externo não recebe frame. Marcador:
`player-type4-expiry=score-only-relayed`, incluído em `MOB_AI_SMOKE_PASS`.
O Engine.Tests cobre separadamente a fórmula 7.69 de HP/MP/Damage/Magic,
cast/recast e restauração da origem de Magic. Smoke de WorldHub em loopback;
não autentica pelo Host nem inicia o cliente visual.

## COMBAT-ATTACK-SKILL-13-TYPE24-001 - expiração de Samaritano (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-build --no-restore -- --mob-ai-smoke
```

O marcador global `MOB_AI_SMOKE_PASS` inclui
`player-type24-expiry=score-only-relayed`. O caso instala Type 24 finito e
confirma a expiração com UpdateScore apenas para os dois peers na view; o peer
externo não recebe frame. O teste Engine cobre cast/recast e a fórmula de AC;
este smoke cobre o relay TCP sintético, sem autenticação pelo Host ou cliente
visual.

## COMBAT-ATTACK-SKILL-216-TYPE42-001 - expiry de Magia Misteriosa (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-build -- --mob-ai-smoke
```

O marcador global `MOB_AI_SMOKE_PASS` inclui
`player-type42-expiry=score-only-relayed`. O cenario instala um registro Type 42
e verifica que a expiry limpa o affect e transmite UpdateScore somente aos peers
na view capturada; o peer externo nao recebe frame. A formula HP/MP de Skill 216
e validada nos testes Engine; este smoke cobre o relay TCP sintetico, nao o Host
autenticado nem o cliente visual. A primeira tentativa de loopback foi bloqueada
pela sandbox (SocketException 10013); a execucao local autorizada depois passou,
exit 0.

## COMBAT-ATTACK-EQUIPMENT-GRADE-001 - bonus de equipamento no AttackOne (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-build -- --mob-ai-smoke
```

`EQUIPMENT_GRADE_COMBAT_SMOKE_PASS force-damage=200 reflect-damage=40
attack=view-scoped=true` confirma o ataque de jogador com grade 9, gema Force
Damage de REF_11 e grade 10 ReflectDamage; o AttackOne autoritativo chegou ao
atacante e ao alvo, enquanto o peer externo nao recebeu frame. O smoke completo
terminou com `MOB_AI_SMOKE_PASS`. A primeira tentativa de bind loopback foi negada
pela sandbox (SocketException 10013); a execucao local autorizada depois terminou
com exit 0. Cenario sintetico de WorldHub/peers TCP em loopback; nao autentica pelo
Host nem inicia o cliente visual.

## MOB-AI-001 Task 5DZ - RSV_FROST em skill contra NPC (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

O marcador `RSV_FROST_SKILL_NPC_TYPE1_SMOKE_PASS
attackrun-view-scoped=true` confirma hit de skill com dano positivo, Type 1 no
NPC e UpdateScore enviado aos dois peers dentro da view capturada; atacante e
peer externo nao recebem frame. O smoke completo terminou com
`MOB_AI_SMOKE_PASS`. Esse cenario usa WorldHub e peers TCP sinteticos em
loopback; nao autentica pelo Host nem inicia o cliente visual. Cobertura
deterministica adicional de RSV_DRAIN e gates esta em `AttackSkillSecondaryAffectTests`.

## MOB-AI-001 Task 5DY - passivas Hunter de score no login (26/10/2026)

As skills Hunter 2, 7, 18 e 22 sao projetadas como passivas de classe; o smoke
TCP atual nao tem marcador especifico para elas. `HunterClassPassiveSkillTests`
exercita o projetor de login com gates de classe e weapon unique para dano, AC e
Critical. O `--mob-ai-smoke` completo foi repetido depois da mudanca e terminou
com `MOB_AI_SMOKE_PASS`, que e regressao geral, nao cobertura de rede destas
passivas. Sem Host autenticado ou E2E visual.

## MOB-AI-001 Task 5DX - Nocao de Combate passiva de login (26/10/2026)

Skill 14 e passiva e nao tem cast ou marcador TCP dedicado no smoke atual.
`CombatAwarenessPassiveSkillTests` exercita o projetor de login para TK com o
bit aprendido, metade inteira de `CurrentScore.Special3` no dano e gates
negativos. O `--mob-ai-smoke` foi repetido depois da mudanca e terminou com
`MOB_AI_SMOKE_PASS`; esse marcador e regressao geral, nao cobertura TCP propria
da skill 14. Sem Host autenticado ou E2E visual desta passiva.

## MOB-AI-001 Task 5DW - Armadura Critica passiva de login (26/10/2026)

Skill 15 e passiva; nao existe cast nem marcador dedicado no smoke TCP atual.
`ArmorCriticalPassiveSkillTests` exercita o projetor de login para classe TK,
incluindo o bit aprendido, `AC * 1.1f`, +25 Critical e gates negativos. O
`--mob-ai-smoke` completo foi repetido depois da mudanca e terminou com
`MOB_AI_SMOKE_PASS`, mas esse resultado e regressao geral, nao cobertura TCP
especifica da skill 15. Sem Host autenticado ou E2E visual para esta passiva.

## MOB-AI-001 Task 5DV - skills 102 ativa e 82 passiva (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP local passou `PLAYER_SOUL_TYPE29_SKILL102_SMOKE_PASS` para o affect
Type 29 ativo da skill 102 e `HUNTER_PROFICIENCY_SKILL82_SMOKE_PASS` para o dano
de login projetado da passiva 82, seguido por ataque fisico e relay somente aos
peers na view. Marcador global `MOB_AI_SMOKE_PASS`. A selecao do learned bit 10
para skill 82 permanece inferencia do gate de classe `%24`, pois o shift nativo
`1 << 82` sobre o campo de 32 bits e indefinido. Validacao sintetica em loopback;
nao autentica pelo Host nem inicia WYD.exe.

## MOB-AI-001 Task 5DU - Ligacao Espectral Type 37 para jogador (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP cria tres peers locais, aplica skill 81 ao caster e valida
`UpdateScore`/`UpdateAffect` Type 37 somente nele. Com `Special3=20`, o affect
fica ativo por 19 ticks; um ataque fisico seguinte inclui `ForceDamage=20` no
`AttackOne`. O frame chega ao caster e ao observador dentro da view, sem chegar
ao peer externo. Marcador:

```text
SPECTRAL_LINK_PLAYER_TYPE37_SMOKE_PASS force-damage=20 caster-affect-only=true attack=view-scoped=true
```

Engine.Tests cobre a soma por registro ativo, cast/recast sem empilhar, duracao
e retirada do bonus no expiry. Validacao sintetica `WorldHub`/TCP em loopback:
nao autentica pelo Host nem inicia WYD.exe.

## MOB-AI-001 Task 5DT - Olhos de Aguia Type 36 para jogador (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP cria tres peers locais, equipa o caster com uma definicao
sintetica de arma `EF_WTYPE=41` e aplica a skill 92 nele proprio. Type 36
projeta `RSV_DRAIN=0x0002` no `UpdateScore`; `UpdateAffect` carrega Type 36,
Level 20, Value 1 e quatro ticks. Somente o caster recebe os frames; o peer na
view e o peer fora dela nao recebem mensagens. Marcador:

```text
EAGLE_EYES_PLAYER_TYPE36_SMOKE_PASS drain-affect-caster-only=true
```

Engine.Tests cobre arma tipo 40, ausencia de Type 36, HP/MP, recast e expiry
de quatro ticks com restauracao do RSV-base. O smoke integral tambem corrige a
fixture existente da Fada 1726 para caster DEX 11 contra alvo DEX 10; a taxa de
miss fica zero pela formula 7.69. Nenhuma regra de jogo mudou. Validacao
sintetica WorldHub/TCP em loopback: nao autentica pelo Host nem inicia WYD.exe.

## MOB-AI-001 Task 5DS - Evasao Aprimorada Type 26 para jogador (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP cria tres peers locais e aplica a skill 89 no caster classe 3.
`UpdateScore` carrega `RSV_PARRY=0x0008`; `UpdateAffect` carrega Type 26,
Level 20, Value 1 e nove ticks. Apenas o caster recebe os frames; os peers
observador e fora da view nao recebem mensagens. Marcador:

```text
EVASION_PLAYER_TYPE26_SMOKE_PASS parry-affect-caster-only=true
```

Engine.Tests cobre cast, recast sem duplicar o affect, HP/MP, expiry e
restauracao do RSV-base. Smoke sintetico de loopback: nao autentica pelo Host
nem inicia WYD.exe.

## MOB-AI-001 Task 5DR - Invisibilidade Type 28 para jogador (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP cria tres peers locais e aplica a skill 95 no caster classe 3.
`UpdateScore` carrega `RSV_HIDE=0x0010`; `UpdateAffect` carrega Type 28,
Level 20, Value 1 e um tick. Apenas o caster recebe os frames; o observador na
view e o peer fora da view nao recebem mensagens. Marcador:

```text
INVISIBILITY_PLAYER_TYPE28_SMOKE_PASS hide-affect-caster-only=true
```

Engine.Tests cobre cast, recast sem duplicar o affect, HP/MP, expiry de um tick
e restauracao do RSV-base. O loopback confirma os campos no wire 7.69, mas nao
autentica pelo Host nem inicia WYD.exe.

## MOB-AI-001 Task 5DQ - Encantar Gelo Type 27 para jogador (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP cria tres peers locais, equipa o caster com uma definicao sintetica
de arma `EF_WTYPE=101` e aplica a skill 75 nele proprio. Type 27 aciona
`RSV_FROST` no `UpdateScore`; o caster recebe `UpdateScore` seguido de
`UpdateAffect`, e os peers observador/fora da view nao recebem frames. Marcador:

```text
ICE_ENCHANT_PLAYER_TYPE27_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests cobre a arma incompatível, ausencia de Type 27, valor/level/duracao,
HP/MP, recast e expiry. A duracao de 19 ticks usa Special2=20 conforme o
contrato 7.69 da fixture. A verificacao inclui a preservacao de RSV no adapter
W2PP -> 7.69, porque o payload W2PP nao carrega o campo. Smoke sintetico de
WorldHub/TCP em loopback: nao autentica pelo Host nem inicia WYD.exe.

## MOB-AI-001 Task 5DP - Troca de Espiritos Type 38 para jogador (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP cria tres peers locais e aplica a skill 87 ao proprio caster.
Type 38 transfere metade de MaxMP para MaxHP (MaxMP=500 resulta em MaxHP=1250
e MaxMP=250), preservando HP/MP atuais. O caster recebe UpdateScore seguido de
UpdateAffect. O observador dentro da view e o peer fora da view nao recebem
frames. Marcador:

```text
SPIRIT_EXCHANGE_PLAYER_TYPE38_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests tambem cobre recast sem acumulo e restauracao do score-base na
expiry. Esta e uma validacao sintetica de WorldHub e peers TCP em loopback; nao
autentica pelo Host nem inicia o cliente visual.

## MOB-AI-001 Task 5DO - Explosao Eterea Type 31 para jogador (26/10/2026)

Execute a partir de `Server` depois do build:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

O smoke TCP cria tres peers locais e aplica a skill 85 ao proprio caster.
Special3=50 produz um affect Type 31 com Value 150, Level 50 e 12 ticks; o
score projetado adiciona 175 AC e preserva HP/MP. O caster recebe UpdateScore
seguido de UpdateAffect. O observador dentro da view e o peer fora da view nao
recebem frames. Marcador:

```text
GOLDEN_SHIELD_PLAYER_TYPE31_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests tambem cobre recast sem empilhamento e restauracao do AC-base na
expiry. Esta e uma validacao sintetica de WorldHub e peers TCP em loopback; nao
autentica pelo Host nem inicia o cliente visual. Evidencias em
`Backup/Build/GoldenShieldType31-20261026-01/`.

## MOB-AI-001 Task 5AY - drop temporizado DUNGEONEVENT (20/10/2026)

Execute a partir de `Server`:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --dungeon-event-smoke
```

O harness cria tres peers TCP em loopback, um evento sintetico com intervalo
3800 e um ItemList neutro. Confirma cinco itens 696, frames `CreateItem` com
checksum valido, entrega ao jogador e ao peer na borda inclusiva 33x33 e nenhum
frame no peer fora da view. O evento roda apos o reroll mesmo sem grupo NPC.

Marcador esperado:

```text
DUNGEON_EVENT_SMOKE_PASS loopback=verified items=5 item=696 create-item=relayed view-scoped=true
```

Execucao observada: passou em 20/10/2026. Este smoke valida o runtime, WorldHub,
wire e relay com dados sinteticos; nao inicia Host autenticado nem `WYD.exe` e
nao comprova E2E visual.

## MOB-AI-001 - runtime Peace/Combat e relays (Tasks 5AS-5BP, 21/10/2026)

O smoke TCP sintetico em loopback cobre os fluxos comuns do runtime: chase,
retorno de leash, rota Peace, aquisicao passiva, ataque, RouteType 5 e limpeza
de summon morto. O ataque usa uma unica barra mista sintetica nao listada em
template: `[17,255,40,255]`. Com SkillData row 17 (`InstanceType=2`) e
`Resist[0]=50`, o alvo recebe AttackOne de 68 bytes com SkillIndex 17,
SkillParm 1, Motion 4 e dano 1477, seguido por refresh HP/MP. O observer fora
da view nao recebe frames. Este percurso valida a integracao generica com
SkillData, WorldHub, Host runtime, wire e view relay.

Task 5AX tambem agenda a geracao de NPC no runtime e cobre a relocacao
`MinuteGenerate >= 500`: templates e limite vem do indice de origem; rota e
`GenerateIndex` vem do indice sorteado. O smoke TCP percorre essa relocacao antes
de serializar CreateMob e confirma a ordem leader/followers e view-scoping.

Task 5BC cobre o recuo com arma de alcance 4, AttackRun 4, BaseDex 10 e alvo
adjacente: o harness injeta roll 0 para selecionar o alvo e roll 11 para DEX.
O NPC sai de (10,10) para (9,10) com Action speed 4 / Effect 0. O viewer em
x=26 recebe Action e RemoveMob(type=0) ao sair da view; o peer em x=27 fica fora
e nao recebe frame.

Task 5BD cobre um NPC gerado RouteType 5 em Combat cujo `Leader=0`: o shard
executa o despawn type 3 antes de sorteios e o relay envia RemoveMob aos dois
peers na view 33x33; o peer em x=27 nao recebe frame. Testes de runtime cobrem
`Leader=-1/0/1000`, o no-op sem alvo e a preservacao do caminho para Leader=1.

Task 5BE cobre o descarte da Pista Lv6 dentro dos limites inclusivos
X=3423..3442/Y=1492..1511, exceto boss 4283. O smoke usa Clan 4, RouteType 5 e
Combat sem alvo para provar que a limpeza precede os filtros, envia
RemoveMob(type=3) aos dois peers na view e nao envia frame ao peer externo.
Testes Engine tambem cobrem Peace/Combat e summons Type 11 nos caminhos
dedicados. A contagem da area usa geradores 4284..4290; RouteType 3 mantém sua
faixa separada 4291..4301.

Task 5BF cobre a saida da Pista Lv2 durante o movimento Peace de NPCs gerados
4306..4364. O teste de rede verifica que a entrada no retangulo inclusivo
X=3373..3407/Y=1420..1453 envia Action antes de RemoveMob(type=3) ao viewer
existente e CreateMob, Action e RemoveMob(type=3) ao viewer que entra na view;
o peer externo nao recebe frame. O check de origem acontece apos
`GridMulticast`, cujo `TargetX/TargetY` representa o passo executado. W2PP usa
5790..5848; o port segue os IDs 7.69 e nao incrementa o contador da Lv6.

Task 5BG cobre a ativacao Merchant de NPC gerado RouteType 0 ao chegar ao fim
da rota. Essa transicao e interna ao snapshot do WorldHub e esta coberta por
Engine.Tests; o smoke TCP abaixo continua validando os relays comuns e nao
exercita nem autentica a loja Merchant.

Task 5BH cobre a propagacao interna do alvo passivo do leader aos followers
gerados vivos relacionados por `LeaderId`, ate 23 tiles por eixo. Como isso
altera apenas o estado interno usado pelo proximo ciclo Combat, nao ha frame
especifico para exercitar neste smoke TCP. O gate legado que propaga tambem
para a party do alvo (`Target < MAX_USER`) e testado separadamente: alvo jogador
mantem o loop; alvo summon nao o aciona.

Task 5BI cobre a limpeza de followers gerados ativos com HP zero no mesmo
aggro passivo. O harness usa um morto perto do alvo e outro fora do alcance do
alvo; o frame de cada `RemoveMob(type=1)` chega somente aos peers na view
capturada na posicao daquele NPC. O observer distante recebe somente a remocao
do segundo follower. O ramo legado `Mode=0` nao remove NPC nem envia frame:
WorldHub limpa `IsGeneratedGroupPartyMember`, mantendo `LeaderId`; Engine.Tests
reativa esse membro e confirma que ele nao volta ao proximo fan-out.

Task 5BJ cobre o timer para NPCs gerados mortos nos modos `Idle` e `Peace`.
Ambos recebem `RemoveMob(type=1)` antes dos demais ramos; o smoke alinha dois
IDs a mesma Peace shard, confere o frame no alvo e no observer dentro da view e
nenhum frame no peer externo. HP zero em Combat fica fora deste ramo. O marcador
e `dead-peace-idle-cleanup=relayed`.

Task 5BK cobre o efeito temporizado Type 17 em NPC gerado Idle. O harness usa
HP 900 e um efeito com valor 1 e tempo 1: o shard aplica `SetHpDam` com delta
1, expira/limpa o efeito e envia `UpdateScore` em seguida. Alvo e observer
dentro da view recebem os dois frames nessa ordem; o terceiro peer, fora da
view, nao recebe frame. O marcador e `life-aura=hp-score-relayed`.

Task 5BL cobre o Type 20 em um NPC gerado Idle com HP 10.000, `LearnedSkill=0`
e tempo 1. O shard aplica o limite NPC de dano 1.000, envia `SetHpDam` com HP
9.000 e delta -1.000, expira/limpa o efeito e envia `UpdateScore` em seguida.
Alvo e observer dentro da view recebem os frames nessa ordem; o terceiro peer
fora da view nao recebe frame. O marcador e `poison=hp-score-relayed`.

Task 5BM cobre o Type 24 criado por `GenerateSummon`: o harness usa um
`InstanceType=11` sintetico que cria summon RouteType 5 com tempo 20. A cada
oportunidade Peace reduz o tempo; na vigesima, remove o summon com
`RemoveMob(type=3)` sem executar movimento. O leader e o caster na view recebem
o frame; o terceiro peer fora da view nao recebe frame. Summons com alvo vivo
seguem fora desta fatia. O marcador e `summon-expiry=type3-relayed`.

Task 5BN cobre o Type 24 em Combat: cada summon com alvo vivo consome um tick
de affect a cada quatro oportunidades do loop de 800 ms. Na vigesima, a remocao
`RemoveMob(type=3)` substitui o ataque daquele ciclo. O harness valida dois
peers dentro da view e um fora dela; o marcador e
`summon-combat-expiry=type3-relayed`. A Task 5BQ fecha a extensao ao caso sem
alvo vivo.

Task 5BO cobre Type 17 (`Life Aura`) e Type 20 (`Poison`) em NPCs gerados
vivos no modo Combat, inclusive quando a lista de inimigos está vazia. Cada NPC
avança os efeitos a cada quatro visitas do shard de combate de 800 ms; os
frames `SetHpDam` e `UpdateScore` preservam essa ordem e ficam limitados à view
capturada. O marcador é `combat-affects-no-target=hp-score-relayed`.

Task 5BP cobre Type 22 (`Trovao`) em NPC gerado Combat sem alvo. O branch
legado de mob não cria ataque de raio; o timer finito decrementa, limpa o
affect ao expirar e relaya somente `UpdateScore` aos viewers capturados. O
smoke confirma ausência de `SetHpDam`, HP inalterado e view-scoping com o
marcador `combat-thunder=score-only-relayed`.

Task 5BR cobre Type 23 (`Aura Bestial`) nos timers de NPC gerado Idle/Peace e
Combat. 7.69/W2PP deixam o envio do Skill 52 limitado a indices de jogador;
em NPC, o timer expira sem ataque/HP e solicita apenas `UpdateScore`. Engine.Tests
cobre Peace e Combat; o smoke TCP em Combat confirma score-only para dois peers
na view e nenhum frame para o peer externo com
`combat-bestial-aura=score-only-relayed`.

Task 5BS cobre o mesmo Type 23 em summons RouteType 5: Peace consome o timer
na oportunidade existente; Combat consome a cada quarta oportunidade de
800 ms. Ao expirar, ambos relayam apenas `UpdateScore` aos viewers capturados,
sem dano, ataque ou despawn. Marcadores:
`summon-peace-bestial-aura=score-only-relayed` e
`summon-combat-bestial-aura=score-only-relayed`.

Task 5BT cobre Type 17 (`Life Aura`) em summons RouteType 5. Peace verifica
cura junto com o passo do follower e expiração sem movimento; Combat consome o
timer a cada quarta oportunidade de 800 ms. `SetHpDam` precede `UpdateScore`,
ambos limitados à view capturada; expiração com HP no cap envia somente score.
O teste de domínio também confirma que a cura/expiração não suprime um ataque
válido no Combat. Os marcadores são
`summon-peace-life-aura=hp-score-movement-expiry-relayed` e
`summon-combat-life-aura=hp-score-expiry-relayed`.

Task 5BU cobre Type 20 Poison para summons RouteType 5. O loopback verifica
que o dono de uma montaria Clan 4 correspondente recebe `SendItem` no slot de
equipamento 14 antes de `SetHpDam` e `UpdateScore`; os peers na view recebem
estes ultimos frames e o peer externo nao recebe nenhum. Marcador:
`summon-poison-mount=send-item-then-hp-score-relayed`.

Task 5BV cobre Type 22 (`Trovao`) nos summons RouteType 5 em Peace e Combat.
7.69/W2PP restringem o ataque de area aos jogadores; o mob apenas consome o
timer, sem HP nem ataque. A expiracao relaya apenas `UpdateScore` aos peers da
view capturada. Os markers `summon-peace-thunder=score-only-relayed` e
`summon-combat-thunder=score-only-relayed` verificam dois peers na view e
nenhum frame para o peer externo.

Task 5BQ cobre o Type 24 de um summon Combat sem alvo vivo selecionado. O
harness mantém o summon ocioso por 19 oportunidades de affect, preserva-o nas
três primeiras chamadas da oportunidade 20 e remove-o na quarta com
`RemoveMob(type=3)`. Os dois peers dentro da view recebem a remoção; o peer fora
da view não recebe frame. Marcador:
`summon-combat-no-target-expiry=type3-relayed`.

Task 5BW cobre a expiração Type 24 de um NPC gerado Idle. Com tempo 1, a
primeira visita Peace elegível remove o mob com `RemoveMob(type=3)`; os dois
peers dentro da view recebem a remoção e o peer externo não recebe frame.
Engine.Tests também cobre o countdown/expiry em Peace e o timer Combat sem
alvo. Marcador: `npc-type24-expiry=type3-relayed`.

Task 5BX cobre o expiry genérico Type 32 num NPC gerado Idle. Com tempo 1, a
primeira visita Peace elegivel limpa o registro e relaya somente `UpdateScore`;
dois peers dentro da view recebem o frame e o peer externo nao recebe nada.
Engine.Tests cobre tambem o decremento e expiry dos summons RouteType 5 e os
timers Combat sem alvo/sem ataque suprimido. Marcador:
`npc-type32-expiry=score-only-relayed`.

Task 5BZ cobre o expiry Type 16 e Type 33 de jogador: o harness deixa o slot 0
sem alteracao, avanca o slot 1, e verifica que o timer limpa os dois affects. O
ator e o peer dentro da view recebem `UpdateScore` seguido de `UpdateEquip`; o
peer externo nao recebe frame. Marcador:
`player-type16-type33-expiry=score-equip-relayed`.

Task 5CA cobre o expiry Type 32 de jogador no slot 1: o timer limpa o registro
de oito bytes e envia somente `UpdateScore`. Ator e observer dentro da view
recebem o frame valido; o peer externo nao recebe frame e nenhum peer recebe
`UpdateEquip`. Marcador: `player-type32-expiry=score-only-relayed`.

Task 5CB cobre Life Aura Type 17 no timer de jogadores, no slot 1. O timer soma
`AffectLevel / 2 + AffectValue`, decrementa a duracao finita e limpa o affect
ao expirar. Ator e observer dentro da view recebem `SetHpDam` seguido de
`UpdateScore`; o peer externo nao recebe frame. Mesmo com HP cheio, a visita
envia `SetHpDam` com delta zero; o score so atualiza quando HP muda ou expira.
Marcador: `player-type17-life-aura=hp-score-relayed`.

Execute a partir de `Server`:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-restore -- --mob-ai-smoke
```

Este e um smoke TCP sintetico em loopback, sem Host autenticado e sem iniciar
`WYD.exe`; valida o relay e o escopo de view, nao E2E visual.

O marcador esperado e:

```text
MOB_AI_SMOKE_PASS loopback=verified shards=explicit chase=relayed leash-return=relayed ranged-retreat=relayed peace-route=relayed route3-despawn=relayed route5-invalid-leader-despawn=relayed pista-lv2-exit=movement-then-remove pista-lv6-bounds-despawn=relayed passive-aggro=relayed passive-group-dead-cleanup=relayed dead-peace-idle-cleanup=relayed npc-type24-expiry=type3-relayed npc-type32-expiry=score-only-relayed player-type16-type33-expiry=score-equip-relayed player-type32-expiry=score-only-relayed player-type22-expiry=score-only-relayed player-generic-affect-expiry=score-only-relayed player-type17-life-aura=hp-score-relayed player-type20-poison=hp-score-relayed life-aura=hp-score-relayed poison=hp-score-relayed combat-affects-no-target=hp-score-relayed combat-thunder=score-only-relayed combat-bestial-aura=score-only-relayed summon-peace-bestial-aura=score-only-relayed summon-combat-bestial-aura=score-only-relayed summon-peace-thunder=score-only-relayed summon-combat-thunder=score-only-relayed summon-peace-life-aura=hp-score-movement-expiry-relayed summon-combat-life-aura=hp-score-expiry-relayed summon-poison-mount=send-item-then-hp-score-relayed summon-expiry=type3-relayed summon-combat-expiry=type3-relayed summon-combat-no-target-expiry=type3-relayed getattack-generic=relayed route5-combat=relayed summon-cleanup=relayed generated-group=relayed relocated-generated-group=relayed attack=positive death=relayed resurrection=refresh view-scoped=true
```

Validacao mais recente (Task 5CE): TDD RED reproduziu countdown generico Type 18
ausente; GREEN Engine.Tests `491/491`. Build sem incremental em pasta isolada:
0 erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`;
`--mob-ai-smoke` passou com `player-generic-affect-expiry=score-only-relayed`
e view-scoping. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso
negado ao Service Control Manager. Sem publish/deploy. Probe SSH `wyd-vps`
somente leitura foi negado no sandbox e expirou elevado antes do handshake em
TCP/22.

Evidencias antigas de layouts
especificos permanecem em `PORT_TRACEABILITY.md`; o smoke ativo e focado nos
metodos compartilhados.

## COMBAT-ATTACK-PEACEFUL-MAP-GATE-001 - dano no AttributeMap 0x40 (14/10/2026)

Smoke TCP/headless em `127.0.0.1:18283` com copias descartaveis de TESTE/TESTE2 em `../Backup/Build/AutoTradeCloseSmoke/peaceful-zone-20261014-01`. O Host recebeu `--heightmap` de 16.777.216 bytes zerados e `--attribute-map` de 1.048.576 bytes preenchidos com `0x40`; ItemList 7.670 e SkillData TMProject 7.69 foram carregados. Autotrade permaneceu desativado.

Com o diretorio atual em `Server`, Host local:

```powershell
dotnet run --no-build --project .\src\WydCdk.Server\WydCdk.Server.csproj -- --bind 127.0.0.1 --port 18283 --accounts-root ..\Backup\Build\AutoTradeCloseSmoke\peaceful-zone-20261014-01\account --account-review-root ..\Backup\Build\AutoTradeCloseSmoke\peaceful-zone-20261014-01\review --item-data ..\Client\7670\ItemList.bin --skill-data ..\Backup\Tools\ReferenceSources\TMProject2GlobalClient\Servidor\Server\Editores\SkillData\skilldata.csv --heightmap ..\Backup\Build\AutoTradeCloseSmoke\peaceful-zone-20261014-01\HeightMap.dat --attribute-map ..\Backup\Build\AutoTradeCloseSmoke\peaceful-zone-20261014-01\AttributeMap.dat
```

Cliente wire sintetico:

```powershell
dotnet run --no-build --no-restore --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj -- --attack-empty-target-smoke --host 127.0.0.1 --port 18283 --account-file ..\Backup\Build\AutoTradeCloseSmoke\peaceful-zone-20261014-01\account\T\TESTE --autotrade-close-peer-account-file ..\Backup\Build\AutoTradeCloseSmoke\peaceful-zone-20261014-01\account\T\TESTE2 --autotrade-state-file ..\Backup\Build\AutoTradeCloseSmoke\peaceful-zone-20261014-01\autotrade.json
```

Resultado: `CLIENT_E2E_PASS attack-empty-target-remove-and-index-gates`; o Host registrou slot fisico com dano 1 e HP do alvo 78->77 enquanto todo AttributeMap era `0x40`. O mesmo frame confirmou RemoveMob(type 1) para ID 24999 vazio e preservacao do slot, dano no alvo ativo seguinte e preservacao do ID 25000 sem RemoveMob. O harness multi-alvo foi descartado antes do ataque porque sua precondicao exige HP >=100000; essa fixture tinha HP 78. Host encerrado cooperativamente, porta livre, e apenas as copias descartaveis foram usadas. Saida detalhada em `Server/Backup/Build/AttackPeacefulMapGate-20261014/validation.txt`. O smoke nao executa WYD.exe nem comprova E2E visual.

## COMBAT-ATTACK-EMPTY-TARGET-SLOT-001 - slots vazios e limite MAX_MOB (14/10/2026)

Smoke TCP/headless em `127.0.0.1:18283`, com copias descartaveis das contas
TESTE/TESTE2 em `../Backup/Build/AutoTradeCloseSmoke/empty-target-20261014-03`.
O Host usa `ItemList.bin` 7.670 e `skilldata.csv` da referencia TMProject
7.69; o estado de autotrade permanece desativado. Foi verificado que 24999 e
indice valido vazio, recebe RemoveMob(type=1) antes do Attack e preserva
Damage=75; o slot ativo seguinte recebe dano. O primeiro indice invalido,
25000, preserva o slot e nao recebe RemoveMob. O harness encerra as duas
sessoes e valida os arquivos apos logout.

Com o diretorio atual em `Server`, Host local:

```powershell
dotnet run --no-build --project .\src\WydCdk.Server\WydCdk.Server.csproj -- --bind 127.0.0.1 --port 18283 --accounts-root ..\Backup\Build\AutoTradeCloseSmoke\empty-target-20261014-03\account --account-review-root ..\Backup\Build\AutoTradeCloseSmoke\empty-target-20261014-03\review --item-data ..\Client\7670\ItemList.bin --skill-data ..\Backup\Tools\ReferenceSources\TMProject2GlobalClient\Servidor\Server\TMSrv\run\skilldata.csv
```

Em outro terminal, cliente wire sintetico:

```powershell
dotnet run --no-build --no-restore --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj -- --attack-empty-target-smoke --host 127.0.0.1 --port 18283 --account-file ..\Backup\Build\AutoTradeCloseSmoke\empty-target-20261014-03\account\T\TESTE --autotrade-close-peer-account-file ..\Backup\Build\AutoTradeCloseSmoke\empty-target-20261014-03\account\T\TESTE2 --autotrade-state-file ..\Backup\Build\AutoTradeCloseSmoke\empty-target-20261014-03\autotrade.json
```

Resultado: `ATTACK_EMPTY_TARGET=private-remove-type1;slot-24999-damage75-preserved;next-slot-damaged`, `ATTACK_INVALID_TARGET_INDEX=25000;slot-preserved;no-remove` e `CLIENT_E2E_PASS attack-empty-target-remove-and-index-gates`. O processo Host foi encerrado depois do smoke. Essa validacao exercita frames wire sinteticos em loopback; nao executa WYD.exe e nao comprova E2E visual.

## COMBAT-ATTACK-VIEW-GATE-001 - alvo fora da janela de ataque (14/10/2026)

Smoke de protocolo por TCP em loopback com copias isoladas de `TESTE` e
`TESTE2` em `Backup/Build/AutoTradeCloseSmoke/view-gate-20261014-01`; o Host
nao recebe opcao de autotrade e usa a raiz descartavel de review. A porta
18283 foi conferida livre antes do Host. A fixture `Client/7670/ItemList.bin`
e a tabela `skilldata.csv` da referencia 7.69 alimentam o login/combat.

Com o diretorio atual em `Server`, Host local:

```powershell
dotnet run --no-build --project .\src\WydCdk.Server\WydCdk.Server.csproj -- --bind 127.0.0.1 --port 18283 --accounts-root ..\Backup\Build\AutoTradeCloseSmoke\view-gate-20261014-01\account --account-review-root ..\Backup\Build\AutoTradeCloseSmoke\view-gate-20261014-01\review --skill-data ..\Backup\Tools\ReferenceSources\TMProject2GlobalClient\Servidor\Server\Editores\SkillData\skilldata.csv --item-data ..\Client\7670\ItemList.bin
```

Em outro terminal, cliente wire sintetico:

```powershell
dotnet run --no-build --no-restore --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj -- --attack-view-gate-smoke --host 127.0.0.1 --port 18283 --account-file ..\Backup\Build\AutoTradeCloseSmoke\view-gate-20261014-01\account\T\TESTE --autotrade-close-peer-account-file ..\Backup\Build\AutoTradeCloseSmoke\view-gate-20261014-01\account\T\TESTE2 --autotrade-state-file ..\Backup\Build\AutoTradeCloseSmoke\view-gate-20261014-01\autotrade.json
```

`--autotrade-state-file` satisfaz o guard de fixtures compartilhado pelo
harness; o Host continua sem `--autotrade-state`, portanto o recurso fica
desativado. O cliente move TESTE2 para 34 tiles de TESTE e verifica, para
Damage -2 e Damage 75, um `RemoveMob(type=1)` privado antes da confirmacao
Attack, TargetID/marcador preservados e nenhum frame recebido pelo alvo.
Resultado repetido nesta validacao: `ATTACK_VIEW_GATE_POSITION=2101,2098->2135,2098;movementRemoveCount=0`,
`ATTACK_VIEW_GATE_VALID=private-remove-type1;damage-marker-minus2-preserved`,
`ATTACK_VIEW_GATE_INVALID=private-remove-type1;damage-marker-75-preserved-before-type77`
e `CLIENT_E2E_PASS attack-view-gate-private-remove-marker-preservation`.
Host encerrado cooperativamente. Smoke headless de wire em loopback; nao
executa WYD.exe e nao comprova E2E visual.

## COMBAT-ATTACK-TRANSFORMATION-SKILLS-001 - transformação e relogin (13/10/2026)

Smoke headless restrito à fixture descartável `Server/Backup/Build/AttackTransformationSkills-20261013/fixture`; o harness recusa outro caminho de conta. A fixture deriva dos snapshots sintéticos SCORETEST/SCOREOBS usados no smoke de projeção de login, com classe/skill do ator ajustadas para a skill 64 e Affect inicialmente limpo. Antes de cada execução, no diretório `Server`, recrie os arquivos descartáveis:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Backup\Build\AttackTransformationSkills-20261013\prepare-fixture.ps1
```

Host local iniciado em `Server`:

```powershell
dotnet run --no-build --project .\src\WydCdk.Server\WydCdk.Server.csproj -- --bind 127.0.0.1 --port 18283 --accounts-root .\Backup\Build\AttackTransformationSkills-20261013\fixture\account --account-review-root .\Backup\Build\AttackTransformationSkills-20261013\fixture\review --skill-data ..\Backup\Tools\ReferenceSources\TMProject2GlobalClient\Servidor\Server\Editores\SkillData\skilldata.csv --item-data ..\Client\7670\ItemList.bin
```

Cliente wire sintético:

```powershell
dotnet run --no-build --no-restore --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj -- --attack-transformation-smoke --host 127.0.0.1 --port 18283 --account-file .\Backup\Build\AttackTransformationSkills-20261013\fixture\account\S\SCORETEST
```

O smoke conecta SCORETEST e SCOREOBS, lança a skill 64 e verifica UpdateScore/UpdateEquip para ator e observador. Em seguida, confere Type 16 e o head transformado no arquivo após logout, reloga SCORETEST e valida a restauração do head e do score Type 16, e encerra a sessão. Resultado: `CLIENT_E2E_PASS attack-transformation-skill64-type16-score-equip-view-relay-logout-relogin`; saída em `Server/Backup/Build/AttackTransformationSkills-20261013/headless-smoke.txt`. Host encerrado cooperativamente. Isso valida protocolo e persistência headless em loopback; não executa WYD.exe nem comprova o visual in-game.

## COMBAT-ATTACK-BR-GRID-PARTY-CLEANUP-001 - refresh de grade e party (06/10/2026)

`GridRefreshPrecedesArenaPartyMemberRemoval` cria jogadores ativos, NPC, item
estático e drop de chão. Decodifica a visão 33x33 do receptor, verifica
CreateMob/PKInfo/CreateItem, exclui o próprio receptor e um jogador distante,
confirma PKInfo apenas para IDs `<MAX_USER=1000`, e observa os frames de party
depois do refresh. O teste também verifica que líder e membro fora do Coliseu
permanecem, e que o membro chamado de volta antes da grade não recebe remoção de
party. `ScheduleCarriesGridRefreshAndPartyCleanupScope` cobre os três blocos,
incluindo refresh no bloco sem ClearAreaLevel.

Engine.Tests 386/386 apos revisao do relay. Os casos cobrem summons do membro,
`RemoveMob` para todos os espectadores, supressao de `CreateMob` obsoleto em
snapshots posteriores e isolamento de falhas de envio por destinatario. O runner registra o CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build completo da solution com SDK 10.0.401
passou com 0 avisos/0 erros. Logs em
`Backup/Build/AttackBattleRoyaleGridRefresh-20261006/`.

Esta é uma validação headless do agendamento, snapshots de WorldHub e frames
decodificados por callbacks; não executou Host/TCP em horário real nem WYD.exe.
Portanto, não confirma entrega em rede ou E2E visual. Sem publicação sandbox,
Site/produção, commit ou push.

## COMBAT-ATTACK-BR-REWARD-001 - spawn e ordem dos frames do prêmio (06/10/2026)

`RewardSpawnCreatesAltarItemBeforeRewardNotice` executa a transição agendada
2->3, registra `BRItem` em (2621,1726), decodifica `MSG_CreateItem` e
`MessagePanel`, valida rotação 0..3 e confirma que o item existe no estado
autoritativo e pode ser recolhido. Os callbacks de envio de `WorldHub` capturam
`CreateItem` antes de `MessagePanel`; o item é entregue aos jogadores na visão
inclusiva 33x33 e o aviso aos jogadores no retângulo inclusivo maior. O teste
também rejeita índice fora de `MAX_ITEMLIST=6500` sem criar item/relay.

Engine.Tests 380/380. O runner registra o CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; o build completo da solution com SDK
10.0.401 passou com 0 avisos/0 erros. Logs e `dotnet --info` em
`Backup/Build/AttackBattleRoyaleReward-20261006/`.

Esta é uma validação headless do `WorldHub` e dos bytes/ordem de relay; não foi
executado um processo Host/TCP na fase de horário real, nem iniciado `WYD.exe`.
Portanto, não comprova entrega TCP ou E2E visual. A agenda 2->3 publica o estado
depois da tentativa de spawn e suprime o aviso quando não há item registrado.
Limpeza de área/portas, parties, dano ambiental, vencedor, placar e fluxo
completo de recompensa continuam pendentes; sem publicação sandbox,
Site/produção, commit ou push.

## COMBAT-ATTACK-BR-NOTICES-001 - primeira fatia de textos e destinatários (05/10/2026)

Na primeira etapa, `BattleRoyaleScheduleTests` verificou Ready/Start 0->1 e
1->2 nas três faixas de nível, as áreas inclusivas e o limite inferior Y distinto.
`_NN_BR_Rewarded` foi adiado até existir o item, como determinam TMProject 7.69
e W2PP; a dependência foi concluída pelo recorte acima. Engine.Tests naquela
etapa: 379/379; logs em `Backup/Build/AttackBattleRoyaleNotices-20261005/`.

## COMBAT-ATTACK-BR-SCHEDULE-001 - inicializacao local da agenda (05/10/2026)

Esta verificacao e de Host/headless, nao um E2E de gameplay. O servidor Debug e
o publish self-contained `win-x64` foram iniciados somente em
`127.0.0.1:18283`, depois de confirmar que a porta estava livre. Sem
`--battle-royale-config`, ambos anunciaram agenda desativada; com configuracao
valida, anunciaram `BRHour=23`, `BRItem=700`, canal `0/3`,
`TestServer=true`, `LocalServer=false` e estado volatil inicial 0. Uma conexao
TCP local foi aceita em cada modo e o shutdown cooperativo liberou a porta.

Nos dois runtimes, JSON malformado, arquivo ausente e `BRHour=24` encerraram o
processo antes do bind. O loopback nao inferiu flags de teste/local: o bypass
observado veio somente de `TestServer=true` explicito no arquivo temporario.
O teste deterministico de dominio valida 0->1->2->3->0 nas fases 0/5/13/14,
rotacao diaria, bypasses, inicio no meio do ciclo e fases perdidas. O smoke TCP
nao enviou frames de jogo, nao abriu `WYD.exe` e nao exercitou avisos,
recompensas, portas, areas ou placar.

`Engine.Tests`: 377/377; build da solution e Host: 0 avisos/0 erros; publish
self-contained concluido. Evidencias, configs temporarias sem segredos e logs
de startup ficam em `Backup/Build/AttackBattleRoyaleStateSchedule-20261005/`.
O log Debug compartilhado foi restaurado ao prefixo anterior ao smoke; a porta
18283 terminou livre. E2E visual e efeitos completos do evento continuam
pendentes. Sem publicacao sandbox, Site/producao, commit ou push.

## COMBAT-ATTACK-SKILL-TYPE6-001 - Type 6 contra dano de skill (05/10/2026)

Smoke headless TCP em `127.0.0.1:18283`, sem iniciar `WYD.exe`, com duas copias
isoladas de contas. O atacante classe 3 enviou os dois slots fisicos do teste
anterior e uma skill de dano 82; o alvo classe 0 com `Affect.Type==6` recebeu os
relays autoritativos com dano zero nos slots fisicos e no slot de skill. Ambos
os clientes viram o mesmo relay; nao houve `SetHpMp` e o HP do alvo permaneceu
100000. O harness verifica que o arquivo alvo tem Type 6 em um dos 32 affects.
As contas-fonte ficaram intactas; no logout normal, somente os campos de
posicao das copias isoladas do atacante e do alvo mudaram.

`CLIENT_E2E_PASS attack-type6-physical-and-skill-damage-zero-hp-unchanged`;
Engine.Tests 368/368, build da solution SDK 10.0.401 com 0 avisos/0 erros e
self-test do harness passaram. O host recebeu `shutdown` cooperativo e a porta
foi liberada. Logs RED/GREEN, build, SDK e smoke em
`Backup/Build/AttackSkillType6-20261005`. O wire foi testado sem cliente visual;
E2E 3D permanece pendente. Nao houve publicacao sandbox, Site/producao, commit
ou push.

## COMBAT-ATTACK-SKILL-TYPE6-NPC-001 - validacao de dominio (05/10/2026)

A extensao para NPC persistente classe 0/1 com Type 6 foi validada diretamente
por `WorldHub.TryApplySkillAttack`: dano positivo vira zero antes da atualizacao
de HP e da resolucao de morte. Casos de controle confirmam dano normal para
classe 2 com Type 6 e classe 1 com Type 5. Engine.Tests 368/368, solution build
SDK 10.0.401 0/0 e self-test do harness passaram. Nao foi executado smoke TCP
especifico de NPC nesta extensao, pois o recorte nao altera protocolo nem Host;
a evidencia de rede acima cobre os alvos jogadores da etapa anterior. Sem E2E
visual ou republicacao sandbox.

## COMBAT-ATTACK-DOUBLEMODE-MULTIPLIER-001 - estado escalar de XP (04/10/2026)

O estado interno de combate agora aceita um `DoubleModeMultiplier` inteiro,
default 1. O Host le opcionalmente `EVENT.DOUBLE` via `--auto-double-config` e
consulta o horario local a cada 2 segundos; no inicio aplica `MultEXP`, no fim
restaura 1. Engine.Tests 367/367 cobre FakeExp de cura em 0/1/2/3, teto 200,
XP generica de grupo em 0x/3x, parser/agenda semanal e propagacao pelo WorldHub.

O smoke TCP `AttackSkillHeal` passou em Debug no `127.0.0.1:18283` com
`--double-mode 3`. O teste move os personagens com Action para fora das cidades,
confirma a posicao do alvo `(2105,2170)`, Cura 27 Mortal com relay `-638`, HP
1000->1638 e FakeExp autoritativo 200 (79*3 limitado a 200), apesar do sentinel
77777 enviado pelo cliente. Os slots excedentes foram preservados; hashes das
fixtures originais intactos e porta liberada. Evidencia:
`Backup/Build/AutoTradeCloseSmoke/6fc7f69d01ee429c880932c84de7a4f3`.

O smoke Host em `127.0.0.1:18284` leu uma configuracao isolada com base 1 e
`MultEXP=3`, observou a ativacao e encerrou via entrada `shutdown`; readback
confirmou a porta liberada. Evidencia:
`Backup/Build/AutoDouble-20261004/host-schedule-smoke-result.txt`.
O smoke TCP Debug `AutoDoubleMagicTrumpet` manteve duas contas isoladas online
durante as transicoes de inicio e fim. Em cada socket chegaram, nessa ordem,
`MessagePanel` (`0x0101`, 140 bytes) e `MagicTrumpet` (`0x0D1D`, 108 bytes),
com os textos configurados, prefixo `[Event:]> `, cor Speak e checksums validos.
Resultado `ExitCode=0`, `AssertionsPassed=true`, `CleanupCompleted=true`, hashes
dos fixtures-fonte intactos e porta 18283 liberada. Evidencia:
`../Backup/Build/AutoTradeCloseSmoke/2db4e8c9c1cf48a7a0da930bfeda608a`.
Os fixtures de conta usados pelo teste sao copias isoladas; o logout persistiu
as posicoes resolvidas pelo WorldHub, sem alterar os fixtures-fonte. A agenda
usa `DateTime.Now` e le configuracao somente no startup. O smoke e headless TCP,
nao abre `WYD.exe`; nao houve republicacao sandbox nesta extensao nem E2E visual.

## COMBAT-ATTACK-DIVINE-BLESSING-001 - habilidade 201 (04/10/2026)

O estado secundario vem de `MOBEXTRA.SecLearnedSkill`, medido na ABI x86
7.69/W2PP como `MOBEXTRA=552`, `SecLearnedSkill` no offset 4 com largura 4.
Com o bit `0x2` presente, o batch de `MSG_Attack` skill 201 executa uma
subacao interna de skill 29 (`CurrentMp=-2`, `Special=0`) sob o mesmo lock.
O relay de cura usa o dano wire assinado `-RateRegen`; HP/MP sao autoritativos,
e o caminho nao altera `MOB.Exp` nem FakeExp persistido.

O smoke headless usa tres fixtures isoladas em `127.0.0.1:18283`. Com HP
900/1000 e MP 500, a Bencao Divina curou o autor em 100, consumiu 30 MP e
enviou `MSG_AttackOne` da subacao antes da confirmacao pai ao autor e ao
observador no alcance; o observador distante recebeu somente a acao pai. A
variante com MP insuficiente manteve HP 900/1000 e MP 10, enviou `SetHpMp`
antes da confirmacao pai e nao enviou a cura. As duas fixtures originais
mantiveram os hashes de origem, o harness limpou seus processos e liberou a
porta. Evidencias Debug e self-contained:
`Backup/Build/AutoTradeCloseSmoke/96c4f93a6a8540a08101215711948826` e
`Backup/Build/AutoTradeCloseSmoke/3e5f1367ec17424591080ec527121688`;
ambas `ExitCode=0`, `AssertionsPassed=true`, `CleanupCompleted=true`.

Engine.Tests 364/364; solution build SDK 10.0.401, 0 avisos/0 erros. O
primeiro smoke Debug parou antes de atacar porque as fixtures nasceram na mesma
regiao; ajustamos o observador distante para um spawn/selector distante e o
smoke passou sem mudar a logica do servidor. O smoke self-contained executou
`WydCdk.Server.Core.exe`. Estes testes nao abrem o `WYD.exe` e nao comprovam
gameplay visual; essa etapa segue pendente.

## COMBAT-ATTACK-HEAL-EXP-001 - bonus FakeExp de cura (04/10/2026)

O modo -AttackSkillHeal usa duas copias descartaveis de conta em
127.0.0.1:18283. TESTE2 entra Foema Mortal com Cura 27, Special[2]=300,
MP=5000; o peer inicia com HP 1000/5000. O pedido envia FakeExp=77777 como
sentinel e tres slots de skill: um alvo valido e dois slots excedentes. O
servidor cura o alvo ate o teto Mortal 638, envia o mesmo relay aos dois
clientes com slot (peer,-638), preserva os marcadores excedentes e envia
SetHpMp com HP 1638.

A fixture e sorteada no login; nesta execucao o peer nasceu numa das cinco
cidades. O FakeExp esperado no relay e zero, comprovando a exclusao urbana e
que o sentinel do cliente nao e aceito. A contribuicao positiva fora da cidade,
o maximo entre alvos, DoubleMode, o teto 200, auto-cura excluida, MOB.Exp
persistido intacto e abort sao cobertos pelos testes deterministas de
WorldHub/Protocol.

TDD RED antes do setter de FakeExp: o relay manteve 77777 e a assercao de
autoridade falhou em
Backup/Build/AutoTradeCloseSmoke/9728cdbbb0914320b0f7c03ec270ddc4.
GREEN Debug em
Backup/Build/AutoTradeCloseSmoke/7f4e4ef48d1a4c48b3f6f13f4feefd46 e
self-contained em Backup/Build/AutoTradeCloseSmoke/c1350a3ce85f4f1fb7401dbc37597d41.
Ambos tiveram ExitCode=0, AssertionsPassed=true, CleanupCompleted=true,
hashes originais intactos e porta 18283 liberada. Engine.Tests 349/349;
build completa SDK 10.0.401, 0 avisos/0 erros. Marker:
CLIENT_E2E_PASS attack-skill-heal type28-excess-slots-preserved-mortal-cap-638-relay-and-hp-sync.

Runtime-only self-contained (197 arquivos) publicado somente no sandbox
autorizado; ZIP SHA-256
E5AD645E63DC2823F94CF0B4FD91AF120E91FE28E8E2E6C7BFB92AC8A76B9A09. Readback
independente confirmou fingerprint identico dos 197 arquivos, hashes
Core/World/Protocol, backup anterior com 197 arquivos, UP/PVP Running,
listeners 8281/8282 no executavel aprovado e staging removido. E2E visual com
WYD.exe nao foi executado; continua pendente. Sem Site/producao, commit ou push.

## COMBAT-GUILD-DISABLE-001 - comandos e `bParty` (02/10/2026)

O smoke headless em `127.0.0.1:18283` usa copias descartaveis de duas contas
com Guild=77 e a skill 29. `guildoff` do autor envia um UpdateScore 7.69 com
Guild=0 aos dois clientes, UpdateAffect e `MSG_GuildDisable` Parm=1 privados ao
autor, sem UpdateAffect do autor no peer; o comando nao aparece como chat
publico. Repetir `guildoff` nao envia frames. O skill entre personagens
same-Guild e bloqueado com `-1` quando so um lado esta guildoff; `guildon`
restaura o score de Guild=77 e libera a cura.

Depois de ambos usarem `guildoff`, um alvo fora da party continua bloqueado por
10/type27: o C++ normaliza os dois Guilds efetivos zero para Guild atacante=-1.
Convite/aceite de party por TCP permite o mesmo skill; o peer subiu de 1000 para
1300 HP. Um segundo fluxo troca a ordem dos sockets no barrier para verificar
comando iniciado pelo peer. Marker: `CLIENT_E2E_PASS attack-party-skill
guilddisable-score-affect-signal-same-guild-party-eligibility`.

Debug e self-contained passaram em
`Backup/Build/AutoTradeCloseSmoke/5096c37d3a904e5082e5ba5e3d96703d` e
`Backup/Build/AutoTradeCloseSmoke/a844228f88714f9f9d9d86446773a128`;
`ATTACK_PARTY_SKILL_GATE_NETWORK_PASS`, `CRACK_NETWORK_PASS` e
`AUTOTRADE_NETWORK_PASS`. Engine.Tests 328/328, harness self-test passou e
solution build SDK 10.0.401 teve 0 avisos/0 erros. Os dois smokes confirmaram
hashes originais intactos, cleanup completo e porta 18283 liberada.

Runtime-only de 197 arquivos SHA-256
`F78CDCB4A9ED05FC49E41663AE421EB20ED82DD876987FAD81AF2215E5E55BD5` foi
publicado somente no sandbox. Readback independente confirmou Core
`7D662D334B4F6C99ADCD4CFCEAC49462BFA8A3CBEFF80DD24B4DB59A1BF85F6E`, World
`A8259F996BDD39672610D277912C77EE8747D812F50782B4B6022331A2A1F951`, Protocol
`1FCAA4AF4ABD7B2CE994DBE004382AFF5D2A2FC347D3DF391B2923F5957083D1`, UP/PVP
`Running` e listeners 8281/8282. Backup
`backup-autotrade-close-f2086228d5a94e488c4b9622c19799f1` preserva 197 arquivos;
staging removido. Build self-contained local preservada em
`Backup/Build/GuildDisable-20261002-runtime`. O helper remoto errou somente na
impressao do marcador final por quoting; o readback separado confirmou hashes e
servicos apos a copia e o reinicio. O smoke nao inicia `WYD.exe`; E2E visual
continua pendente.

## COMBAT-ATTACK-PARTY-001 - elegibilidade `bParty` em jogadores e NPCs registrados (02/10/2026)

O smoke headless em `127.0.0.1:18283` usa copias descartaveis das fixtures, a
skill 29 Recuperar aprendida pela Foema e um NPC sintetico isolado. O primeiro
ataque bloqueou o NPC estatico fora do Guild do autor e preservou seu slot no
relay como ID/`-1`, sem sincronizacao de HP/MP. O ataque seguinte bloqueou o
jogador outsider da mesma forma. O log registrou 10/type27 por cada alvo
bloqueado (totais 10 e 20). Depois o harness formou uma party por
`PartyInviteRequest` e `PartyAcceptRequest` TCP e repetiu a skill: o membro
recebeu cura autoritativa, HP 1000->1150, com relays iguais para ator e
observador. O passe de NPC no mesmo Guild esta coberto por teste de dominio,
nao por este smoke.

Resultado: `CLIENT_E2E_PASS attack-party-skill npc-outsider-type27-preserved-slot-party-wire-allows-heal` e
`ATTACK_PARTY_SKILL_GATE_NETWORK_PASS`. Evidencias Debug e self-contained em
`Backup/Build/AutoTradeCloseSmoke/3a2244dbc2354468839bbe0a30166266` e
`Backup/Build/AutoTradeCloseSmoke/168b00db40a1488bab0e877691be68ec`. Ambos
confirmaram hashes das contas originais intactos, cleanup completo e porta
18283 liberada. Engine.Tests 327/327; self-test do harness passou; solution
build SDK 10.0.401 com 0 avisos/0 erros.

O ZIP runtime-only local de 197 arquivos
(`WydCdk.Server-v7670-attack-party-npc-20261002.zip`) tem SHA-256
`41E281FCC93EC7CDD0E345BC60B4109C53EEE47066C4E6C301786490C1817F49`. Foi
publicado somente no sandbox; readback independente confirmou UP/PVP
`Running`, portas 8281/8282 `Listen` pelo `WydCdk.Server.Core.exe` e hashes
Core `428CD9E6EEE600860D62458FF3B35323FE6BE65D47CE50E78DA0DD4FB2D1BFEF`,
World `4A425700D01BF3103ABA84FDB073E3766F1A1DA949BD79471A8FE2B811CC0B9C` e
Protocol `B17A09C55AE7E8891269B615260E5C95665C8FFB1212F62379F45BBCC7DCAF81`.
Backup `backup-attack-party-npc-20261002-41e281fc` preserva os 197 arquivos
runtime; fingerprint SHA-256 do manifesto:
`EECC6EAF0FD479C1CEBB4AAE36F073E8E1C3845396CBBE6ED03C8A0C657B1EC9`. ZIP
remoto e staging temporario foram removidos. Lider dinamico de NPC,
O recorte de `GuildDisable` e registrado acima; lider dinamico de NPC e os
demais efeitos do Battle Royale continuam pendentes. A agenda/state foi
validada em COMBAT-ATTACK-BR-SCHEDULE-001 e os avisos de fase em
COMBAT-ATTACK-BR-NOTICES-001. O smoke nao inicia
`WYD.exe` e nao e E2E visual.

## COMBAT-ATTACK-MIXED-SLOTS-001 - slots fisicos e skill no mesmo `MSG_Attack` (03/10/2026)

O Host classifica cada slot pelo marcador `Damage` e chama um batch unico do
WorldHub, em ordem e sob um lock: `-2` fisico e `-1` skill. Uma acao concluida
emite uma confirmacao agregada antes dos relays secundarios; morte/XP/drop
diferidos sao ordenados pelo slot original. Um abort fisico por alcance mantem
efeitos imediatos ja aplicados, mas suprime confirmacao e finalizacao diferida.
`DoubleCritical` e `Progress` compartilham o estado da acao. Efeitos de skill
ainda nao portados continuam desligados.

Engine.Tests 345/345; self-test do harness aprovado; solution build SDK
10.0.401 com 0 avisos/0 erros. RED TCP confirmou que antes do Host misto um
slot `-2` se perdia em pacote com `SkillIndex` de skill. Smokes loopback Debug e
self-contained em `127.0.0.1:18283` passaram nos dois sentidos de 13 slots,
ordem dos relays, `Progress`, HP/MP finais e `ReqMp=4963`; tambem cobriram
marcador skill invalido com fisico processado e abort depois de cura, sem
confirmacao agregada. Evidencias:
`Backup/Build/AutoTradeCloseSmoke/d5c1724f8530453ab168163480dabbfa` e
`Backup/Build/AutoTradeCloseSmoke/1a10e562fde94fcf871b71dd07c2e997`. Ambas
registram exit 0, assertions/cleanup concluidos, hashes originais intactos e
porta liberada. Nenhum `WYD.exe` foi iniciado; E2E visual permanece pendente.

O runtime self-contained somente com arquivos de execucao tem 197 arquivos e
ZIP SHA-256 `D84ED926D252D4EFA74AD3B63341D9A89DE5E39D3B6DBE7C62DEE7F384ECCA5C`.
Assemblies: Core `3671B64CC31D06E11E81092F78D43F491A494402F19650A050B6336F55139C27`,
World `B4FC84DBE8EC8C0C85F7B18441364E8B63FA08A6AD27435DAEB18F21F31A318C`,
Protocol `1FCAA4AF4ABD7B2CE994DBE004382AFF5D2A2FC347D3DF391B2923F5957083D1`.
Publicado no sandbox autorizado em
`C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`;
readback dos 197 hashes confere com o manifesto local. Backup anterior de 197
arquivos preservado em
`C:\WYDCDK\Server\test-releases\backup-autotrade-close-0e962167791a43928556ac034682385b`,
fingerprint SHA-256 do manifesto
`A3FDFC4F0607A8979E500C29D4918CBF479D10469DEB6010BCA41F777297E26D`. UP/PVP
`Running`, listeners 8281 (PID 5180) e 8282 (PID 2624) no executavel aprovado;
staging temporario removido. A assercao local sobre o marcador de conclusao do
helper nao passou; nao repeti a troca e confirmei o estado por readback
independente dos hashes, backup, processos, tarefas e portas. Sem transferencia
de source/docs/segredos/fixtures; sem Site/producao, commit ou push.

## COMBAT-ATTACK-GOLDEN-SHIELD-001 - fee da skill 85 (02/10/2026)

## COMBAT-ATTACK-SUMMON-PARTY-001 - `bParty` para summons (03/10/2026)

Smoke headless local em `127.0.0.1:18283` com duas copias descartaveis das
fixtures e o `BaseSummon` 7.69. O peer classe 2, com skill 56 aprendida, criou
um Condor dinamico. Antes de formar party, o ator classe 1 tentou Recuperar
(skill 29) no summon: recebeu type27, e o relay manteve `summonId/-1`. O peer
convidou o ator por TCP; depois do aceite, o mesmo alvo passou pelo gate sem
10/type27. Como cura em summon nao faz parte deste recorte, o relay elegivel
ficou com slot vazio `(0,0)` e nao houve sincronizacao de HP/MP.

O mesmo smoke regrediu os bloqueios anteriores de NPC/player e GuildDisable:
quatro alvos outsiders somaram type27 `10/20/30/40`. O log do host confirmou a
skill 56 criou um summon, os dois clientes receberam o mesmo `MSG_CreateMob`
(`MobId=1000`, `CreateType=3`) e a skill 29 elegivel chegou ao ramo
`UnsupportedSkillType` sem efeito.

Resultado: `CLIENT_E2E_PASS attack-party-skill
guilddisable-player-npc-summon-party-eligibility-without-summon-effect` e
`ATTACK_PARTY_SKILL_GATE_NETWORK_PASS`. Evidencia Debug em
`Backup/Build/AutoTradeCloseSmoke/30458cc7c8ae4ae3bc5f2440d7fa9bbb`; a mesma
assertion passou contra runtime self-contained `win-x64` em
`Backup/Build/AutoTradeCloseSmoke/a60da7e60f044309b763a0d8b8ea22c1`. Build SDK
10.0.401 sem avisos/erros, contas originais intactas, cleanup completo e porta
18283 liberada. Naquela etapa o runtime aguardou a correcao do dispatcher misto,
que foi publicada depois em COMBAT-ATTACK-MIXED-SLOTS-001. E2E visual e efeitos
de skill em summons continuam pendentes.

## COMBAT-ATTACK-GOLDEN-SHIELD-001 - fee da skill 85 (02/10/2026)

Smoke headless local em `127.0.0.1:18283`, sem iniciar `WYD.exe`, usando duas
copias descartaveis das fixtures. O ator classe 3 aprendeu o bit 13 da skill 85,
com `Special[2]=3`, Gold 600 e MP 121. A primeira chamada debitou 300 Gold e
enviou `UpdateEtc` privado antes do relay de ataque; o MP autoritativo chegou a
zero. A segunda debitou os 300 restantes e enviou `UpdateEtc` Gold 0 antes do
`SetHpMp` por mana insuficiente, sem relay. A terceira retornou silenciosamente
com Gold zero: nenhum frame, consumo de mana ou relay. O observador recebeu os
relays normais, mas nenhum `UpdateEtc` privado. Apos logout, MOB.Coin e o Coin da
conta persistiram em zero; carry e cargo ficaram intactos.

Resultado: `ATTACK_GOLDEN_SHIELD_COIN_NETWORK_PASS`, manifesto em
`Backup/Build/AutoTradeCloseSmoke/057ac1b0c3e645dc850aa4f33a20aeed`.
Engine.Tests 326/326; self-test do harness passou; solution build SDK 10.0.401,
0 avisos/0 erros. Hashes das fixtures originais intactos e porta 18283 liberada.
O mesmo cenario passou contra o host Debug e o runtime `win-x64`
self-contained. Evidencias:
`Backup/Build/AutoTradeCloseSmoke/057ac1b0c3e645dc850aa4f33a20aeed` e
`Backup/Build/AutoTradeCloseSmoke/54b179bc3b68494f95d1490c034feab2`. O ZIP
runtime-only de 197 arquivos (SHA-256
`18EAEB6D90D1016CACEDF7B8932849E120F45FB987255FDCE3352642AA2F5069`) foi
publicado no sandbox. Backup de 199 arquivos verificado. Hashes: Core
`186ECB6636F1C69CC873BAA4A915CF46362649862D8569B8BD32B22B644A430D`, World
`0FCD1F7C1E28DC6C7EA7665F31766302C74B57928B869FC83204C0AE13F6349D`, Protocol
`B17A09C55AE7E8891269B615260E5C95665C8FFB1212F62379F45BBCC7DCAF81`. UP/PVP
`Running`, 8281/8282 `Listen`, temporarios remotos removidos. Nao e E2E visual;
essa etapa continua adiada. Sem Site, producao, commit ou push. A comparacao
TMProject 7.69/W2PP e os vetores de unidade estao em `PORT_TRACEABILITY.md`.

## COMBAT-ATTACK-TYPE6-001 - protecao fisica `Affect.Type==6` (02/10/2026)

Smoke headless local, sem iniciar `WYD.exe`: duas contas de fixture descartaveis
em `127.0.0.1:18283`, alvo classe 0 com Type 6 no ultimo dos 32 slots e atacante
com DEX que torna a chance de miss 0%. Um `MSG_Attack` area enviou dois slots
fisicos `-2` para o mesmo alvo. Ator e alvo receberam o mesmo relay autoritativo
com dano zero nos dois slots; nao houve `SetHpMp`. O harness verificou hashes
inalterados das duas contas originais, cleanup completo e porta liberada.

Resultado: `ATTACK_TYPE6_PROTECTION_NETWORK_PASS`, manifesto em
`Backup/Build/AutoTradeCloseSmoke/53a71bca3e5e4473b58e606f0cb09162`.
Engine.Tests 325/325; teste do parser do harness passou; build da solution SDK
10.0.401 com 0 avisos/0 erros. O ZIP runtime-only de 197 arquivos
(`0C970F828C0175EE73E3D91F946EDF25A2654D8E38F80F44B41C81346267F604`) foi
publicado no sandbox. Backup `backup-attack-type6-add1b6ed044c4c43a1e09e445a593710`
preserva 197 arquivos; readback independente confirmou hashes Core/World/Protocol,
UP/PVP `Running`, 8281/8282 `Listen` e staging removido. O smoke nao exercita o
cliente 3D; essa etapa continua adiada. Nao houve mudanca no Site/producao,
commit ou push. A matriz de fontes, divergencia W2PP e controles unitarios estao
em `PORT_TRACEABILITY.md`.

## COMBAT-ATTACK-REQMP-001 - `ReqMp` final de `MSG_Attack` (02/10/2026)

As confirmacoes `MSG_Attack` agora escrevem `ReqMp` pela conversao signed-short
do `CurrentMp` autoritativo final, como o handler TMProject 7.69 faz depois do
loop de alvos. W2PP escreve seu `pUser.ReqMp` no bloco inicial. O host nao passa
mais o sidecar `RequestedMana` para `AttackConfirmation`; sincronizacoes privadas
`SetHpMp` continuam fora desta fatia.

Engine.Tests GREEN 324/324; RED anterior a implementacao: 323/324, com falha
especifica `AttackResponseWire` (`ReqMp` diferia do MP autoritativo). Os vetores
cobrem os tres tamanhos de pacote e conversao de MP40000 para ReqMp -25536. Build
final da solucao, SDK 10.0.401, 0 avisos/0 erros; `dotnet --info` foi salvo em
`Backup/Build/AutoTradeCloseSmoke/combat-attack-reqmp-20261002/build/dotnet-info.log`.
Resumo do RED em
`Backup/Build/AutoTradeCloseSmoke/combat-attack-reqmp-20261002/tdd-red-summary.md`.

Smokes loopback Debug: one-target/68 bytes
`7ac84212595645139731ae5a22412e06`, area/168 bytes
`c5d7f1261b7245fe9eac03ad08c00e8c`, two-target/80 bytes
`38a5d4ce61ca429590f4e394d5c42eee`. Repetidos com o runtime self-contained local:
`8655d7f5fce14018aad830da8b0307af`, `a2634202d3104df1a24793ec06dc45c2` e
`97b2c76eaa684a10b9ec0a725dc0a96c`. Todos passaram a assercao
`ReqMp == (short)CurrentMp`, crack-counter e auto-trade; hashes originais
intactos e porta 18283 liberada. Nenhum executavel WYD.exe foi iniciado.

Runtime-only local self-contained em
`Server/artifacts/publish-attack-reqmp-20261002/runtime.zip`: 197 arquivos, sem
PDB/source/docs/segredos; ZIP SHA-256
`385EF07DA8DF67CA3443BDAB93A4527A21D38A9EB55EE06184BDD256BD17C710`; Core
`7C4B016E437C478AFB1873443EB020A8D44601C3F8C634773597B1A61C6ECFF1`, World
`7F99625904BA2FA1216E8E521B43DAF980BDFEA75B6D05D816ED5065FB431C53`, Protocol
`FC06269E22E7C971CC6385BC255D79F58A54AF2FA7D2680282B8DB0EA80D163F`.
Publicado no sandbox em `C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`
depois da autorizacao explicita para usar a identidade SSH existente. ZIP runtime-only
197 arquivos, SHA-256 `385EF07DA8DF67CA3443BDAB93A4527A21D38A9EB55EE06184BDD256BD17C710`;
Core `7C4B016E437C478AFB1873443EB020A8D44601C3F8C634773597B1A61C6ECFF1`, World
`7F99625904BA2FA1216E8E521B43DAF980BDFEA75B6D05D816ED5065FB431C53`, Protocol
`FC06269E22E7C971CC6385BC255D79F58A54AF2FA7D2680282B8DB0EA80D163F`. Backup
`C:\WYDCDK\Server\test-releases\backup-autotrade-close-c57914003fb74f489e9970c0c3285830`
verificado com 197 arquivos e hashes anteriores Core/World/Protocol. UP/PVP
Running, listeners 8281/8282 nos processos do executavel aprovado; ZIP de staging
removido. E2E visual com `WYD.exe` continua pendente. Sem Site/producao, commit ou
push.

## COMBAT-ATTACK-MISS-001 - miss em `MSG_Attack` (02/10/2026)

A taxa 7.69 compara DEX do alvo com DEX do atacante mais os bonus Rsv do
atacante, preservando a divisao inteira literal e os caps 75%/10%; o roll
uniforme 0..99 abaixo da taxa resulta em `dam=-3`. O caminho cobre jogador e
summon ativo de jogador, nao NPC comum. W2PP diverge no handler ativo com
GetParryRate, escala 1..1000 e miss defensivo `-4`; os caminhos automaticos
legados de parry permanecem separados.

`AttackMissTests` cobre a matematica, o miss fisico e de skill de jogador,
HP intacto e exclusao do NPC comum. Engine.Tests 324/324; build completa SDK
10.0.401, 0 avisos/0 erros. O smoke loopback de regressao multi-alvo passou em
`Backup/Build/AutoTradeCloseSmoke/c3aa25247dd349d8a97f95e04821eb3a`:
`ATTACK_MULTI_TARGET_NETWORK_PASS`, `CRACK_NETWORK_PASS`,
`AUTOTRADE_NETWORK_PASS`, hashes das fixtures originais intactos e porta 18283
liberada. O harness fixa DEX atacante 1001/alvo 1000 para manter taxa de miss
zero; nao valida `-3` por TCP, que fica coberto pelo WorldHub deterministico.
Smoke headless, sem iniciar WYD.exe; E2E visual permanece pendente.

Runtime-only self-contained aplicado ao sandbox aprovado: ZIP 197 arquivos,
SHA-256 `6069F1DC60BACDF54D9155E5E3A6A591C35E9A25A7DACAE730E41D40A2536E4B`;
Core `D0D908D79E7837FFFBA3030A965387893C804838B05CC0A17F0D97D21238BF1C`,
World `65D546A04089D397C58DD77D87C08E75AB8BF680949D81E3CBE1D02984453842`,
Protocol `E8DD33C2AA27D6DBC0FC89E59211D90CB94A295B7C48D1B9E34D41312B6C046B`.
Backup remoto `backup-autotrade-close-de8d802890b34dadad54bd12fb0bc76a`
preservado com 197 arquivos; hashes Core/World/Protocol anteriores conferidos.
UP/PVP `Running`, 8281/8282 `Listen` no executavel aprovado. Staging removido;
nao foram enviados source, documentacao ou segredos. Sem Site/producao,
commit/push.

## COMBAT-ATTACK-TOWER-001 - gate de torre (01/10/2026)

O comportamento `WarOfTower::AttackTower` foi portado com `GenerateIndex=1076`
da TMProject 7.69. Guilda zero ou guilda igual a da torre bloqueiam o slot; uma
guilda inimiga pode atacar. A leitura usa os campos MOB autoritativos. O gate
vale antes do dano fisico, efeitos de skill e type28; slot bloqueado fica zerado
no relay e nao soma crack-error.

`AttackTowerGateTests` cobre o comportamento no WorldHub para ataques fisicos,
skills, HP inalterado do NPC bloqueado e o ramo excedente do gate type28.
Engine.Tests 323/323; build completa SDK 10.0.401 com 0 avisos/0 erros. O smoke loopback existente de multi-alvo
passou em `Backup/Build/AutoTradeCloseSmoke/990b88eea0554e71ac3756c4b7e1151c`,
com fixtures originais intactas e porta 18283 liberada. Esse harness nao semeia
NPCs arbitrarios: nao foi feito smoke TCP especifico de ataque a torre, e o
E2E visual continua fora deste recorte.

Runtime self-contained publicado somente no sandbox: 197 arquivos, ZIP SHA-256
`CAB8B5E38B7F5DA9D633346D78C257B92CAD7C66D1962BFD0B8975A434B6518D`.
Backup remoto `backup-autotrade-close-41c805543dea44898d08a1160c1dbc4d`
preservado e conferido 197/197; UP/PVP Running, 8281/8282 Listen, hashes Core,
World e Protocol iguais aos locais. Staging removido. Nenhum deploy de Site ou
producao, commit ou push.

## COMBAT-ATTACK-MAXTARGET-001 (01/10/2026)

A regra das duas fontes soma 10/type28 por slot de skill acima do MaxTarget
inclusivo quando o tick e de cliente. O slot excedente nao recebe efeito, mas
segue no relay com o ID original e Damage=-1. O type28 vem antes da regra
skill-specific bParty/type27: a regressao same-clan inicialmente falhou no C#
e passou depois de reordenar o gate. A validacao de WarOfTower da source 7.69
ainda e uma pendencia de paridade.

TDD RED: Backup/Build/AutoTradeCloseSmoke/0e7ccbf52fb642eba0e97eed75695e71
mostrou slots 2/3 como (0,0) na confirmacao. GREEN final Debug:
Backup/Build/AutoTradeCloseSmoke/1885771e01934bd39cb85ee1fc781e85;
GREEN self-contained:
Backup/Build/AutoTradeCloseSmoke/64098822f50146608af0d46e0d273bce.
Nos dois, as faltas somaram type28 10/20; ator e observador receberam relay
igual, slots 2/3 (peerId,-1), Cura permitida resultou em HP 1638 (1000+638),
sem efeito nos slots excedentes. Evidence registra assertions, hashes dos
blobs originais intactos e cleanup/porta 18283 liberada. Engine.Tests 322/322;
build completa SDK 10.0.401, 0 avisos/0 erros. TCP/headless com fixtures
isoladas; nao iniciou WYD.exe e nao constitui E2E visual.

Runtime self-contained de 197 arquivos aplicado ao sandbox autorizado. ZIP
SHA-256 EFE9A40F3DD872F06FF1909E0F9BD2DC716603765CCA8F566A73F659CC66CC88;
Core 8132E9FB41EB046ADA3B711CFF14ACE9D22BF2F2513F4F223D8DD224D8834221;
World 26500E6F5226DF4560E1FD7BB5D67FBF53B5DD7885B82725E23DADA642A93727;
Protocol E8DD33C2AA27D6DBC0FC89E59211D90CB94A295B7C48D1B9E34D41312B6C046B.
Backup backup-autotrade-close-7358317a80fe4e3b89ce4faae2d5bdb3: 197/197
hashes comparados ao runtime anterior. UP/PVP Running, portas 8281/8282
Listen, staging remoto removido. Sem source/docs/segredos remotos,
Site/producao, commit ou push. E2E do cliente 3D permanece pendente.

## COMBAT-ATTACK-MULTITARGET-001 (01/10/2026)

O Host processa todos os slots fisicos `Damage==-2` presentes no frame em um
batch do WorldHub, na ordem e sob um lock, e envia uma unica
`AttackConfirmation` do snapshot final do atacante. Slots nao contiguos mantem
o indice original. `DoubleCritical` e `Progress` sao estado compartilhado da
acao; o byte do cliente e ignorado. O passivo 7.69 classe 3/bit `0x200000` pode
reservar o slot 1 como alvo zero/dano secundario. Mortes so sao finalizadas no
fim normal. O aborto por alcance mantem mutacoes anteriores sem rollback, para
nos slots seguintes e suprime relay e frames postergados de morte; os efeitos
imediatos capturados saem depois de liberar o lock. No caminho normal, o relay
agregado precede remocao/spawn e os demais efeitos postergados.

TMProject 7.69 e W2PP percorrem slots pela ordem, compartilham
`DoubleCritical/Progress`, resolvem esses valores apenas no slot 0 e finalizam
mortes depois do relay normal. Ambos ativam o passivo classe 3 no slot 0 e
esvaziam o alvo original do slot 1. Divergencia preservada: piso aleatorio de
dano secundario 7.69 (`rand()%60+10`) contra piso fixo 60 do W2PP; o port segue
7.69. O alcance continua sendo `>23` usando posicoes autoritativas C#, enquanto
as fontes C++ medem coordenadas do pacote; esta diferenca fica pendente.

RED antes da integracao do Host:
`Backup/Build/AutoTradeCloseSmoke/e3572fac1d82473189e079d7ba966df0`;
`evidence.json` registra `ExitCode=1`, `AssertionsPassed=false` e
`CleanupCompleted=true`, com falha especifica no slot 1 vazio. GREEN final,
depois de ajustar a ordem relay/efeitos:
`Backup/Build/AutoTradeCloseSmoke/93da451534ec4f3499b2bb028ff163c0`;
`evidence.json` registra `ExitCode=0`, `AssertionsPassed=true` e
`CleanupCompleted=true`. O smoke usa um frame de 13 slots, slot 0 vazio, slots
fisicos 1/2 contra o mesmo peer com HP alto e `DoubleCritical=0xFF` na entrada.
Ator e peer receberam exatamente um relay identico; slot 0 ficou intacto,
slots 1/2 continham alvo/dano autoritativos e `Progress=321` foi mantido. Os
hashes SHA-256 dos blobs originais foram
`72523FCE81161012904726D88D386B7BB0381EDB108011E6F8C5E5E3456BEAFD` e
`6D521B420AA6482A195BCA7E1B94CFE76B5F6847A094DF7EE67382E7E53726F1` e ficaram
intactos; a porta 18283 foi liberada. O harness reteve stdout, stderr, logs do
cliente e hashes dos assemblies/runtime e scripts.

Engine.Tests: 320/320; build completa `WydCdk.Engine.slnx` com SDK 10.0.401:
0 avisos/0 erros. Testes cobrem 1/2/13 slots, indices esparsos, ordem, estado
final do atacante, `DoubleCritical/Progress`, passivo, alvo repetido, morte e
aborto. O smoke final usou o publish self-contained em loopback:
`Backup/Build/AutoTradeCloseSmoke/2e6a52a615684949afb4f34f0403c9f4`,
`ExitCode=0`, assertions e cleanup verdadeiros, marcadores de multi-alvo,
crack-counter e autotrade aprovados; blobs originais intactos e porta 18283
liberada. O smoke e TCP/headless e nao inicia `WYD.exe`; E2E visual e validacao
no cliente 3D permanecem pendentes.

Publicacao runtime-only no sandbox em 01/10/2026, no alvo
`C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`.
O ZIP self-contained de 197 arquivos (sem PDB) tem SHA-256
`972C57AAF311D9B491EC3FABCB0D39190203DA44AAC69E3FE2AC476E25BDCBD5`; todos os
197 hashes remotos conferem com o manifesto local. UP/PVP voltaram a `Running`,
com listeners 8281/8282 nos processos do executavel aprovado. Backup verificado
de 199 arquivos, incluindo os dois logs operacionais, em
`C:\WYDCDK\Server\test-releases\backup-combat-attack-multitarget-20261001-93da4515`;
os hashes anteriores de Core/World/Protocol tambem conferem. Os dois logs ativos
foram preservados e nao entraram na troca runtime-only. Staging remoto removido;
sem transferencia de source, docs ou segredos, e sem Site/producao, commit ou
push.

### Revisao de codigo e runtime corrigido (01/10/2026)

Uma revisao estatica do primeiro runtime publicado encontrou tres defeitos
Important, todos corrigidos com regressao: o dano secundario de
`DoubleCritical` nao era carregado entre alvos posteriores e o override de
ManaControl podia deixar o slot reservado desatualizado; summons classe 4 nao
sincronizavam o HP da montaria em hits nao letais; e o Host coletava marcadores
`Damage==-2` mesmo quando `SkillIndex` selecionava uma skill valida. O Host agora
so forma batch fisico quando `SkillIndex==-1`, mantendo o fluxo da skill e
limpando um marcador fisico invalido posterior.

RED da rota skill/slot mistos:
`Backup/Build/AutoTradeCloseSmoke/9b0d6fcc14514dcc83d626c7393ca22a`;
GREEN debug `0442192caad249b39df8596d2555a180` e self-contained
`49d6e484af894cbc987dca346b9a5e74`. O GREEN confirmou Cura no slot 0 e
remocao do marcador inesperado no slot 2. Regressao multi-alvo debug:
`f4566aa86d92430287662ddfb45d09de`; self-contained final:
`72619534043c4fcb8277baf9c27ae400`. ManaControl debug:
`9979f7bba97046268c8a43304f35ef9b`; self-contained final:
`bc93db88392f4ddc8f86ed35d5aa5c74`. Um resultado de parry e dano zero e
legitimo nessa regra; o harness passou a aceitar esse resultado e a matematica
deterministica de conversao para ManaControl ganhou teste unitario.

Engine.Tests: 321/321; build completa da solution com SDK 10.0.401: 0 avisos e
0 erros. O publish self-contained local final esta em
`src/WydCdk.Server/bin/Release/net10.0/win-x64/attack-multitarget-reviewfix-20261001`.
O pacote runtime-only tem 197 arquivos e SHA-256
`5B3260C1321C1F4D185488273D3F5660E023FBA1FDA7F255ECDAC2A8A87CAFAD`. Hashes
locais: Core `710364C557AA420AC91C2120F9316144B5FBD3566B5C5C686B3FCAAA6B4D3DBB`,
World `6633926DB0402C6E7EB450943917571F9217A93BD567DA62B2C2C0B9EE421912`,
Protocol `E8DD33C2AA27D6DBC0FC89E59211D90CB94A295B7C48D1B9E34D41312B6C046B`.

Runtime-only republicado no sandbox autorizado. Backup anterior preservado em
`C:\WYDCDK\Server\test-releases\backup-combat-attack-multitarget-reviewfix-20261001-5b3260c1`
(197 arquivos de runtime e 2 logs); o backup da primeira publicacao tambem foi
mantido. A verificacao remota conferiu os 197 hashes instalados, os hashes do
backup e os assemblies acima. UP/PVP estao `Running`; 8281/8282 estao em
`Listen` nos processos aprovados. Staging, manifesto e scripts temporarios foram
removidos (`REMOTE_VERIFY_OK`, `TASK_TEMP_CLEANUP_OK`). Nenhuma source,
documentacao ou credencial foi transferida. Seguem pendentes E2E visual com
`WYD.exe`, outros gates e outros ramos de Attack; sem Site/producao, commit ou
push.

## COMBAT-ATTACK-MANACONTROL-001 (01/10/2026)

Modo `-AttackManaControl` / `--attack-mana-control-smoke`, loopback
`127.0.0.1:18283`, com blobs isolados. O ator classe 3 conhece os bits
`0x200000` (passivo) e `0x800000` (divisor 55); o alvo tem Affect.Type 18,
100000 MaxMp/Mp e 100000 HP. O teste envia 25 `MSG_AttackTwo` com segundo alvo
e dano adulterados. Para cada pacote, exige relay identico no atacante/observador,
MP e HP sincronizados no defensor e dano calculado pela drenagem observada com o
divisor 55. Quando `DoubleCritical & 4` ativa, segundo alvo fica zero e o segundo
dano iguala o dano reduzido do slot principal. Resultado: MP
100000->81795, HP 100000->94550 e 9/25 procs; os 25 frames de `SetHpMp` e de
ataque passaram. O engine cobre tambem limiar de 20%, ausencia do affect, divisor
60, deficit de MP, preservacao de `ReqMp` e comportamento sem segundo-slot.

Evidencia final:
`Backup/Build/AutoTradeCloseSmoke/54c502a7fffd4f79ad470686c20f9732`;
`evidence.json` registra `ExitCode=0`, `AssertionsPassed=true`,
`CleanupCompleted=true`, hashes originais intactos e porta 18283 liberada. A
primeira tentativa do runner deixou `Skill data: disabled` e os ataques foram
ignorados como `CombatNotReady`; o runner passou a iniciar o host com
`--skill-data`, e a execucao final acima passou. TCP/headless, sem iniciar
`WYD.exe`. O runtime self-contained foi publicado somente no sandbox em 01/10;
UP/PVP ficaram `Running`, portas 8281/8282 em `Listen` e hashes de
`WydCdk.Server.Core.dll`, `WydCdk.World.dll` e `WydCdk.Protocol.dll` conferidos
contra a build local. O backup remoto
`C:\WYDCDK\Server\test-releases\backup-autotrade-close-4d16d3cea8c14910b9ff9a7b96abb3cc`
foi preservado e a pasta temporaria de transferencia removida. Processamento
fisico multi-alvo foi concluido em COMBAT-ATTACK-MULTITARGET-001; outros gates e
E2E visual continuam pendentes. Sem Site/producao, commit ou push.

## COMBAT-ATTACK-DOUBLECRITICAL-002 (01/10/2026)

Modo `-AttackDoubleCritical` / `--attack-double-critical-smoke`, somente em
loopback e com copias isoladas. A smoke envia 25 `MSG_AttackTwo` fisicos: inicia
com `Progress=6`, avanca a sequencia e termina com `Progress=1025`. O servidor
retorna 7 no primeiro pacote, avanca os valores validos e normaliza o ultimo
para 1 antes de responder 2. Atacante e observador recebem os mesmos 25 frames
de 80 bytes; o runner tambem valida o slot secundario autoritativo e rejeita
respostas omitidas ou ataques recusados. A execucao observou 6/25 procs do
passivo bit 4. O teste deterministico da engine cobre os bits base 1/2, roll
signed, as duas faixas de multiplicador e a combinacao.

RED: antes do gate, `Progress=6` voltou sem avanco; evidencia
`Backup/Build/AutoTradeCloseSmoke/3efd3bbe63bf40a7a3605301dd7bc446`. GREEN:
`Backup/Build/AutoTradeCloseSmoke/9eb6ad1dd00b4cb8ba7dfe2ae1117214`;
`evidence.json` registra `ExitCode=0`, `AssertionsPassed=true`,
`CleanupCompleted=true`, hashes dos blobs originais e todos os assemblies, e a
porta 18283 liberada. A build final da solution teve 0 avisos/0 erros e
Engine.Tests 311/311; logs, SDK e RED/GREEN estao em
`Backup/Build/DoubleCriticalSmoke/COMBAT-ATTACK-DOUBLECRITICAL-002/final-verification`.
Smoke TCP/headless, nao inicia WYD.exe. E2E visual e o loop completo multi-alvo
continuam pendentes. Sem deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-DOUBLECRITICAL-001 (01/10/2026)

Modo `-AttackDoubleCritical` / `--attack-double-critical-smoke`, somente em
loopback com copias isoladas das fixtures. Atacante classe 3 com bit aprendido
`0x200000` envia 24 `MSG_AttackTwo` fisicos validos, com flag de entrada `0xFF`
e segundo alvo/dano adulterados. Os dois participantes recebem os mesmos 24
relays de 80 bytes; o servidor limpa a flag hostil, define somente `0` ou `4`,
mantem o segundo alvo em zero e escreve dano secundario autoritativo apenas no
proc. A execucao final observou 8/24 procs. Evidencia:
`Backup/Build/AutoTradeCloseSmoke/80be119128074c9e8e43230ceafc4903`.
`evidence.json` registra `ExitCode=0`, `AssertionsPassed=true`,
`CleanupCompleted=true` e hashes originais das duas fixtures; o runner confirmou
que permaneceram intactas e liberou a porta 18283. O teste exercita wire TCP
headless, nao inicia `WYD.exe`; E2E visual continua pendente. Sem deploy,
Site/producao, commit ou push.

## COMBAT-ATTACK-PENETRATION-CLAN4-001 (01/10/2026)

TMProject 7.69 e W2PP dividem o dano fisico por quatro para alvos jogadores ou
NPCs com `MOB.Clan == 4`. O C# agora verifica o clan autoritativo do snapshot do
alvo. O teste headless `WorldPhysicalAttack` cria um NPC clan 4 com 1000 HP e
rolagem fixa: antes da mudanca, o dano sem divisor era 94; depois, 23, deixando
977 HP. A suite Engine.Tests terminou em 310/310 PASS.

Build de `WydCdk.Engine.slnx`: SDK 10.0.401, 0 avisos/0 erros. O loopback
`-AttackSkillGate` passou como regressao do pacote fisico `SkillIndex=-1` e dos
relays, evidencias em
`Backup/Build/AutoTradeCloseSmoke/8a0dc7440e744e68924fb087251333ff`. Esse smoke
nao ataca o NPC clan 4; essa condicao tem cobertura headless de WorldHub, sem
smoke de wire especifico. RED/GREEN/build logs estao em
`Backup/Build/AutoTradeCloseSmoke/COMBAT-ATTACK-PENETRATION-CLAN4-001`. E2E
visual com WYD.exe continua pendente. Sem deploy, Site/producao, commit ou push.

## COMBAT-SKILL-DAMAGE-001 (30/09/2026)

Modo `-AttackSkillHeal` / `--attack-skill-heal-smoke`. Ator TESTE2 vira Foema
(classe 1), ClassMaster MORTAL, com Cura aprendida (bit 3), Special[0..1]=300,
SaveMana 0 e MP 5000. Observador TESTE com HP 1000/5000, Clan 0 e sem montaria
em Equip[13]. O ator envia `MSG_AttackOne` com skill 27 e `Dam[0]=(peer,-1)`. A
7.69 calcula 300*2+100=700 e limita a 638: os dois recebem o mesmo relay com slot
`(peer,-638)`, e o observador recebe `SetHpMp` com HP 1638; sem crack nem
rejeicao, carry/cargo/Gold intactos.
RED (runtime anterior ao bloco, compilado fora da arvore):
`Backup/Build/AutoTradeCloseSmoke/ae46f15c6f444894aaa9dda6486fb524`, relay `(2,-700)`.
GREEN: `10ba01d51da74b84a61e7faa586244ad`, relay `(2,-638)` e SetHpMp HP 1638.
Regressoes: skill gate `3b58366a58d24d36be05c743c494db61`, ganho de mana
`1b2bc48dbeb54f73b4c0e5a9197aa361`, timing `fc552441893d4c2e9e342c922eb4caf4`,
HP0 `fba6e9ea40f9441a993ed6bcf5d0e9b2`, RvR `60e8d2c555464ad8b9cd638d1cb20f7c`,
Action3 mana `a175f66beb74477a99663475c3f8f84e`.
Dano elemental por TCP fica sem smoke deterministico (rolagem e parry
aleatorios). Wire/headless; sem deploy, Site/producao, commit ou push.

## COMBAT-SKILL-SPECIAL-001 (30/09/2026)

O `-AttackSkillGate` grava tambem `Special[1]=200` e `Special[2]=40` (Base e
CurrentScore) na copia do ator. A skill 10 e estagio 2: com `Special[2]=40` e
SaveMana -128, custa 20*120/100=24 e depois 24*228/100=54; o MP fica
5000/4946/4810/4810 (a skill 96 usa Level1). O indice antigo lia `Special[1]=200`
e dava 4909/4773. O runner exige a sequencia exata.
RED (codigo atual com o indice antigo, compilado fora da arvore):
`Backup/Build/AutoTradeCloseSmoke/fde1b00d8f6b47d0acb88081e27c750c`, MP
5000/4909/4773/4773. GREEN: `bf32cba3c81e441fa5c37aac454180f9`, MP
5000/4946/4810/4810. Regressoes: ganho de mana `5dc53bdb2d554c39ac27cdae549167aa`,
timing `f63c31bdec2f435fa66079bccf733643`, HP0 `35807cb243ab41968f02a97b29b22502`,
RvR `107875d267a4471c8b822ffab618dacd`, Action3 mana `7ee4f60b0886448e810bafab4864f29e`.
Wire/headless; sem deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-MANA-001 (30/09/2026)

`-AttackSkillGate` agora grava SaveMana `0x80` (-128 na 7.69) na copia do ator;
o runner exigia `ATTACK_SKILL_MP=5000,4955,4819,4819` (228% de 20 e 60); desde
SKILL-SPECIAL-001 exige 5000/4946/4810/4810.
O novo modo `-AttackManaGain` / `--attack-mana-gain-smoke` usa ator classe0 com
skill 10 aprendida, SaveMana 120, MP 100 e MaxMp 107. Dois ataques da skill 10
sem alvo, espacados em 1 s, produzem custo -4: o MP relayado ao ator e ao
observador vai para 104 e depois para 107 (108 limitado por `SetReqMp`). Nenhum
crack error ou rejeicao; carry/cargo/Gold intactos.
RED (codigo do SKILLGATE sem a correcao de mana, compilado fora da arvore):
skill gate `Backup/Build/AutoTradeCloseSmoke/5df3afccb1814ff3a91afcf6c0705431` e
ganho `88d6ee7b880e44adbe0a50df6e7c7d5e`, ambos com `InsufficientMana`. GREEN:
skill gate `37a68ee78f814b65ba35c340377585fb`, ganho `a310436742db4f568c1e99c86e93ddca`.
Regressoes: timing `94474b7f39dc4aa994bf8a5a49443645`, HP0
`ae977f8258b64441a87ac9db7507512f`, RvR `3c9d3358093049ceb02c4ee8bb0dbfad`,
Action3 mana `3d2705d8babd4217ab757b0ebd1bb770`.
Wire/headless; nao executa `WYD.exe`. Sem deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-SKILLGATE-001 (30/09/2026)

Modo `-AttackSkillGate` / `--attack-skill-gate-smoke`: ator TESTE2 (classe 0,
LearnedSkill 0, MaxMp/Mp 5000, SaveMana 0 na copia; 0x80 desde MANA-001) e observador TESTE, loopback
`127.0.0.1:18283`, `--skill-data` 7.69. Ticks partem de agora-100 s e avancam 1 s
por pacote. Fase REJECTED: skills 30 (outra classe), 9 (passiva), 97 (morteiro
fail-closed) e -2 (invalida), sem relay de Attack nem SetHpMp/HpMode. Fase ACCEPTED:
-1 (fisico, alvo 0), 10 e 96 sem bit aprendido e 103 (sem checagem 7.69). Ator e
observador recebem os quatro relays na ordem e com payload identico; CurrentMp
5000, 4980, 4920 e 4920. Host: `Crack error ... points=8, type=10, total=8` e
`points=208, type=1, total=216`, sem outro incremento; uma linha de rejeicao por
motivo. Carry/cargo/Gold nos blobs intactos.
RED (runtime do checkpoint `d15acf39` compilado fora da arvore):
`Backup/Build/AutoTradeCloseSmoke/c1cb88947fb540918d0fbbddb6d48136`, com -1/10/96/103
rejeitados e o cliente falhando na assercao de relay. GREEN:
`Backup/Build/AutoTradeCloseSmoke/a67d5869cb67488cb0ff66271d949277`. Regressoes:
timing `fc62fc334a93478aa1f216f6b3b2a7d5`, HP0 `2d97d00fb6614a66ba13d7e41e024910`,
RvR `6986b6a2b7834d69981205945cf23554`.
O type30 (delay <700 ms) exige tick de host >= 2^31 e fica coberto so em unidade.
Wire/headless; nao executa `WYD.exe`. Sem deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-TIMING-001 (30/09/2026)

TMProject 7.69 e W2PP `_MSG_Attack.cpp` somam `AddCrackError(1,107)` quando o
`ClientTick` chega antes de `LastAttackTick+800` ou fica fora da janela do
servidor (120 s atras/15 s adiante), somando 1/type107. Depois do gate de 800 ms,
as duas sources tambem comparam o tick com `(uint)LastAttackTick-100`: mais de
100 ms de retrocesso soma 4/type7, sem retorno, antes de atualizar LastAttackTick
e prosseguir para a janela do servidor. O C# reporta esse side effect na mesma
ordem. Como ambas as expressoes sao uint32, o wrap de `LastAttackTick+800` pode
deixar um tick da era atual passar e marcar type7; se a janela do servidor tambem
rejeitar, type107 vem depois e pode ser registrado na mesma sessao. Tick reservado
e rejeitado pelo gate comum de frame; skill delay, mana e dano continuam fora.

A TMProject tambem chama `SendBanAccount(Analise)`/`SaveInfoDetect` no TooSoon e
ban de tres horas/`SaveInfoDetect` fora da janela; W2PP nao tem esses efeitos.
O C# nao importou essas acoes: a politica vigente restringe Analise ao opcode
Action, e os bans/sink do handler Attack exigem uma fatia e decisao proprias.
Isso nao altera o type107 comum nem o limite normal do contador de CrackError.

O runner `-AttackTimingGate` usa duas contas em copias locais e loopback
`127.0.0.1:18283`. O ator comeca com tick atual (chega a `InvalidSkillId`), depois
envia +799 ms (`TooSoon`), `uint.MaxValue-300` (fora da janela, mas atualiza o
LastAttackTick) e tick atual +1000 (wrap na soma +800, marca type7 e continua ate
`InvalidSkillId`). O peer, sem tick anterior, envia valor 120001 ms antigo e
recebe `OutsideServerWindow`. A barreira de chat drena os dois sockets e confirma
ausencia de relay `MSG_Attack` e de respostas HpMode/SetHpMp. O host registra
type107 total1 para cada rejeicao temporal, depois `4/type7 total6` no ator;
`InvalidSkillId` nao soma. O teste de dominio cobre os limites exatos de 100/101
ms e o wrap com retorno Accepted. Os blobs originais permanecem intactos.

TDD RED anterior de type107:
`Backup/Build/AutoTradeCloseSmoke/14de35c8ddcd4e56a04c992051ef6e72`. RED deste
type7 executou os pacotes e falhou somente na assercao do incremento ausente:
`Backup/Build/AutoTradeCloseSmoke/43a5a971c89b4a779ae06860223638b9` (exit do
cliente 0; `AssertionsPassed=false`, cleanup completo). GREEN:
`Backup/Build/AutoTradeCloseSmoke/2c94658ad5b643f59052b752171f9a83`
(`evidence.json`: exit0, assertions e cleanup true). Engine.Tests 309/309;
build solution com SDK .NET 10.0.401, 0 erros e 1 aviso CS8321 da funcao local
`ExperienceBonus` sem uso em Program.cs, fora deste recorte. Os processos de build,
suite e smoke ficaram ocultos com stdout/stderr redirecionados; nenhuma janela
nativa .NET apareceu. Hashes originais intactos e porta18283 liberada. Regressao
Attack HP0 em `Backup/Build/AutoTradeCloseSmoke/78af97932f8c4fcf86cd050b754189bb`;
RvR em `Backup/Build/AutoTradeCloseSmoke/02be512b091d4ab3aa4fed105df507b2`.
Validacao wire/headless; nao executa `WYD.exe`. Sem deploy, Site/producao,
commit ou push. Outros ramos de Attack e E2E visual continuam pendentes.

## COMBAT-ATTACK-RVR-001 (30/09/2026)

TMProject 7.69 `_MSG_Attack.cpp` barra o ataque sem Clan quando
`LastX/128==13 && LastY/128==15` (setor x1664..1791, y1920..2047). Esse ramo vem
depois do gate TradeMode e da exigencia de `USER_PLAY`, antes de HP zero, tick,
mana e dano. A resposta e `SendClientMsg` com
`Você não pode atacar nessa área.`. `Basedef.h` define o setor como `(13,15)`.
W2PP `_MSG_Attack.cpp` nao tem a restricao; o contrato de release escolhido
mantem o comportamento 7.69. No C#, `WorldHub.TryGetAttackState` captura combate,
posicao viva e byte `MOB.Clan` no mesmo lock; `LegacyAttackRvrGateRules` cobre a
regiao e Clan zero. O handler envia somente ao autor `MessagePanelConfirmation`
`0x0101`/140, cabecalho zerado e texto Latin-1. Nao soma CrackError, nao relaya
`MSG_Attack` e nao envia HpMode/SetHpMp.

O smoke `Test-AutoTradeClose.ps1 -AttackRvrGate` abre duas contas em fixtures
isoladas no loopback `127.0.0.1:18283`, move ambas a partir do spawn real e espera
o eco do servidor da ultima `Action` antes da barreira de visibilidade. Isso evita
uma corrida de ordenacao entre sockets. O atacante clanless envia `MSG_AttackOne`
`0x039D`/68 com tick atual e, depois, tick deliberadamente antigo 240; ambos
recebem exatamente um painel privado cada. O observador nao recebe painel,
ataque, HpMode ou SetHpMp. Os testes de dominio cobrem limites do setor, celulas
adjacentes, Clan diferente de zero e snapshot autoritativo. O controle negativo
anterior ao port nao recebia o painel. A regressao `-AttackDeadGate` tambem passou.

O RED contra o runtime anterior nao recebeu o painel RvR para o pacote de tick
240 e terminou no gate temporal antigo (`01fd55f5f37f483cbb224f498149efd2`).
O GREEN devolve o painel para tick atual e antigo, confirmando a prioridade RvR.
Engine.Tests: 306/306. SDK .NET 10.0.401 (`dotnet --info`). Build da solution:
0 avisos/0 erros; rebuild do projeto E2E: 0 avisos/0 erros. Build, testes e hosts
foram executados em processos ocultos com stdout/stderr redirecionados; nenhuma
janela nativa do .NET apareceu.
Smoke GREEN: `Backup/Build/AutoTradeCloseSmoke/dceda13b6b7d4d35b05de1b3f0d33725`
(`evidence.json`: exit 0, assertions true, cleanup concluido); regressao HP0:
`Backup/Build/AutoTradeCloseSmoke/349005fe16954ca4af140278992f9d24`. Hashes dos
blobs originais intactos e porta 18283 liberada. Validacao wire/headless; nao
executa `WYD.exe`, nao e E2E visual. Sem deploy, Site/producao, commit ou push.
Outros branches de Attack e E2E visual continuam pendentes.

## COMBAT-ATTACK-TRADEMODE-001 (30/09/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:20-24`) e W2PP
(`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:27-31`) testam `TradeMode`
antes de USER_PLAY, RvR, HP, tick, mana e dano. O atacante recebe apenas
`SendClientMsg`/`SendClientMessage` e o handler retorna. Em `SendFunc.cpp`, ambos
zeram `MSG_MessagePanel`, tamanho 140, tipo `0x0101`, ID zero, copiam 128 bytes e
zeram os dois bytes finais. O texto index 170 do `Language.txt` 7.69 e
`Não é possiível durante a auto venda.`; o `i` duplicado e mantido para
compatibilidade. C# consulta `LegacyAutoTradeBook.HasListing(connectionId)` antes
das demais validacoes e codifica o texto em Latin-1 em um unico painel privado
com ID/ClientTick/KeywordIndex zero. Este recorte nao cobra CrackError nem muta
combate, recursos ou a listing.

O smoke normal de `Test-AutoTradeClose.ps1` usa duas contas em copias das
fixtures, host em `127.0.0.1:18283`, e envia `MSG_AttackOne` `0x039D`/68 com
checksum valido durante a loja. O autor recebe exatamente o painel `0x0101`/140
com os bytes esperados; o observador nao recebe painel, `SetHpMp` ou relay de
ataque, e a listing continua presente. O marcador de barreira e enviado no mesmo
socket depois do ataque, provando a ordem no dispatcher. A barreira anterior,
que aguardava primeiro o chat em outro socket, nao ordenava as duas conexoes e
causou um falso negativo no harness; foi corrigida antes do GREEN. A regressao
completa de abrir/consultar/fechar autotrade, retry, compra rejeitada, carry,
cargo, Gold e relogin passou.

O runner converte excecoes de assercao em `CLIENT_E2E_FAIL` e exit 1; Engine.Tests
faz o mesmo por teste. A primeira RED tinha deixado uma `InvalidOperationException`
sem tratamento, que abriu a janela nativa `dotnet.exe - Erro de Aplicativo` no
Windows. Depois da captura das excecoes, RED e GREEN terminam sem esse dialogo.
Validacao final: Engine.Tests 305/305; solution build 0 avisos/0 erros; smoke
loopback exit 0; hashes das fixtures originais intactos e porta liberada.
Evidencia: `Backup/Build/AutoTradeCloseSmoke/207ca3d8fbac4c71bbc1260702562f10`
(`evidence.json`: `autotrade-close`, assertions true, cleanup concluido). Sem
deploy, Site/producao, commit ou push. RvR, outros gates/branches de Attack e
E2E visual continuam pendentes.

## COMBAT-ATTACK-DEAD-001 (30/09/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp`) e W2PP
(`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp`) rejeitam o atacante quando
`CurrentScore.Hp==0 && SkillIndex!=99`: enviam `SendHpMode`, somam
`AddCrackError(1,8)` e retornam antes de tick, mana e dano. `SendHpMode` nas
duas sources zera `MSG_SetHpMode` e preenche somente tipo/tamanho/ID/HP/Mode;
por isso o C# envia `0x0292`/20, HP0, Mode22, ID do autor e
ClientTick/KeywordIndex zero. Type8 nao tem log proprio, mas participa do
contador compartilhado e do limite de logout.

O smoke `-AttackDeadGate` usa copias distintas `TESTE2` (HP0) e `TESTE` (vivo),
host bindado em `127.0.0.1:18283` e a tabela `skilldata.csv` comunitaria 7.69.
Envia `MSG_AttackOne` `0x039D`/68 e exige um unico `SetHpMode` privado ao autor,
nenhum HpMode ao observador e nenhum relay do ataque. O `QuitTrade` seguinte
totaliza 11/type17, comprovando o ponto type8 suprimido; carry/cargo/Gold ficam
intactos. Engine.Tests passou com 305 casos, o build reportou 0 avisos/0 erros e
o self-test do harness passou. Evidencia GREEN:
`Backup/Build/AutoTradeCloseSmoke/8f691a31f62f408bbc9f3ed19388417c`;
`evidence.json` confirma `attack-dead-gate`, exit 0, assertions e cleanup; os
hashes dos blobs originais conferem e a porta foi liberada.

Este recorte de HP-zero deixa o gate RvR especifico de 7.69, outros branches de
ataque e E2E visual com `WYD.exe` pendentes. `TradeMode` foi tratado em
COMBAT-ATTACK-TRADEMODE-001 acima. Nao houve deploy, Site/producao, commit ou push.

## MOVE-ACTION-ILLUSION-COOLDOWN-AUDIT-001 (30/09/2026)

Auditoria das sources, sem executar runtime/testes: `LastActionTick` recebe
`SKIPCHECKTICK` no login e nao possui outra atribuicao no TMProject 7.69 nem no
W2PP. Por isso, o check de 900 ms (1/type105 em Action3; 1/type103 em Action)
nao fica ativo e nao foi habilitado no C#. A camada visual do cliente 7.670
continua pendente.

## BL-001 - runtime descartavel loopback (30/09/2026)

`Client/7670/WYD.exe` existente (1.667.584 bytes, SHA-256
`DA1BDF2322946C96E173AD08848E4C03234C348E31AFC8D5E6DE09BE21DAF6D6`) continua
sem proveniencia de build confirmada. A source de referencia esta no commit
`3ccc4d3`, com tres arquivos-fonte modificados localmente e preservados. A copia
sem `.git` em `Backup/Build/Client7670-Source-Isolated-20260930` teve os hashes
dos tres arquivos e do `.vcxproj` conferidos antes da compilacao.

O `.vcxproj` pede v142; o Visual Studio 18 instalado oferece MSVC 14.51/v145.
A compilacao base `Release|x86`, alvo `TMProject`, usou override
`PlatformToolset=v145` somente na chamada do MSBuild, com `OutDir`/`IntDir` no
snapshot. Build concluida sem erros e com um aviso C5298 (`U+001A` em
`Dependencies/Directx/Include/dxfile.h`). O EXE base em
`Backup/Build/Client7670-Source-Isolated-20260930/Artifacts/WYD.exe` tem
1.667.584 bytes, SHA-256
`A40D6FCFF5CC9ED3119DBA60982CD096923849FE02DCD13E89B8E85282F30109` e nao foi
iniciado.

Para preparar um runtime de teste, uma segunda compilacao isolada forca o include
local `ShellExecuteDisabled.h`, que anula `ShellExecute` para impedir abertura de
`Change.exe` e navegacao externa. Build `Release|x86` concluiu com exit 0 e 2044
funcoes compiladas; avisos C4005 (`STRICT` redefinido pelo include) e C5298
preexistente. O EXE de teste em
`Backup/Build/Client7670-Source-Isolated-20260930/Artifacts-Loopback/WYD.exe`
tem 1.667.584 bytes, SHA-256
`51BE30D3E55231D300ABD7BD41E046690DD43EE6A4DAF5F9CD9AF4A92EFF03E2`. Ambos os
EXEs nao possuem assinatura Authenticode; o Defender customizado reportou zero
deteccoes para o EXE de teste e sua copia runtime (protecao em tempo real ativa).

A copia descartavel `Backup/Build/Client7670-Loopback-Smoke-20260930` tem
42.628 arquivos e recebeu o EXE seguro e um `serverlist.bin` de 7040 bytes
(SHA-256 `A9CC8B0A548BA022AAD3010BC1ED8611288C83912FCB68FC0AF450892A6EDDB0`).
O round-trip pela rotina de referencia confirmou os 10 slots de status em
`http://127.0.0.1:8280/servtest.htm` e os 100 hosts como `127.0.0.1`. Robocopy
reportou 42.626 arquivos copiados (2,772 GiB) e zero falhas; foram excluidos o
EXE/serverlist anteriores e backups, logs, dumps e diretorios `ScreenShot`.

O `serverlist.bin` do perfil original `Client/7670` continua apontando para
`http://wydcdk.com.br/servtest.htm?v=20260924-1` e `124.198.128.225`; permaneceu
intocado. O CUA retornou `apps=[]` e `browsers=[]`, portanto o EXE seguro nao foi
iniciado e nao houve conexao. E2E grafico e fluxo de login continuam pendentes;
BL-001 permanece aberto. Nenhum arquivo do perfil ativo ou do checkout de
referencia foi substituido.

## MOVE-ACTION-ILLUSION-MANA-001 - custo fixo e sincronizacao de MP (29/09/2026)

`Test-AutoTradeClose.ps1 -ActionIllusionMana -SkipBuild` usou duas copias de
conta em loopback e o `skilldata.csv` primario 7.69. A linha73 tem
`SkillPoint=39` e `ManaSpent=45`; o fixture iniciou com MP90 e classe3/bit2.
`MSG_Action3` com tick antigo consumiu45 antes de type104, sem movimento, relay
ou `SetHpMp`; o Action3 valido seguinte consumiu os45 restantes, relayed ao autor
e peer, e enviou `SetHpMp` privado com MP0/ReqMp90. Nova tentativa sem MP nao
moveu/relayed e respondeu com sync privado MP0/ReqMp90. Logout/relogin preservou
MP0. A camada visual do WYD.exe nao foi exercitada.

Resultado: Engine.Tests 303 PASS/0 FAIL; solution build 0 erros/1 warning CS8321
(`ExperienceBonus` local nao usada, preexistente); self-test do harness PASS.
GREEN mana `Backup/Build/AutoTradeCloseSmoke/86016fe1f87c4d2f98100af962352841`;
`-ActionIllusionEligibility` GREEN `9d730d8ca5c94b58b9133d55a77be4b9` e
`-ActionRange` GREEN `fba2c79f765f412384cc489449bb801f` tambem passaram com a tabela
carregada. Todos registraram `ORIGINAL_HASHES_INTACT` e `PORT18283_RELEASED`.
Um primeiro fixture usou incorretamente39 (SkillPoint), detectado pela rejeicao
por MP insuficiente; depois da correcao do valor, o smoke do cliente passou e
uma assercao do wrapper foi ajustada para ignorar os dois type17 dos preflights.
Sem deploy, Site/producao, commit ou push. E2E visual pendente; o check de
cooldown type105 foi auditado como inativo em `MOVE-ACTION-ILLUSION-COOLDOWN-AUDIT-001`.

## MOVE-ACTION-ILLUSION-ELIGIBILITY-001 - classe/skill de MSG_Action3 (29/09/2026)

`Test-AutoTradeClose.ps1 -ActionIllusionEligibility -SkipBuild` usou copias
isoladas em loopback: ator classe2, sem bit aprendido2, nivel398 e HP-1; peer
vivo na janela de visao. O RED confirmou relay do Action3 ao ator e ao peer, mas
falhou na assercao type28 ausente. No GREEN, o mesmo movimento continuou e foi
relayed, sem HpMode; type28 somou exatamente10 e o QuitTrade seguinte chegou a
20 no contador compartilhado. A fronteira level399, classe valida, classe
invalida e bit ausente estao cobertos em Engine.Tests.

Resultado: Engine.Tests 303 PASS/0 FAIL; solution build 0 avisos/0 erros;
self-test do harness PASS. GREEN:
`Backup/Build/AutoTradeCloseSmoke/ada5e06696e14e578146dfabc1f5f11c`; RED:
`Backup/Build/AutoTradeCloseSmoke/5215e9f6612940a6bac4c7cb68ea367b` (relay
confirmado; falhou somente na validacao de type28). Marcadores GREEN:
`CLIENT_E2E_PASS illusion-eligibility type28 fallthrough-relay shared-counter
hp-carry-cargo-gold`, `ACTION_ILLUSION_ELIGIBILITY_NETWORK_PASS`,
`CRACK_NETWORK_PASS`, `AUTOTRADE_NETWORK_PASS`, `ORIGINAL_HASHES_INTACT` e
`PORT18283_RELEASED`. Fixtures originais nao foram alteradas; porta liberada.
O smoke anterior `-ActionRange` tambem passou apos fixar classe3/bit2 na sua
fixture, evidencia `Backup/Build/AutoTradeCloseSmoke/d9c3973434c64e9c9d78e4c69f47c984`.
Wire/headless, sem abrir WYD.exe. Cooldown e E2E visual pendentes; mana foi
concluida em MOVE-ACTION-ILLUSION-MANA-001. Sem deploy, Site/producao, commit ou push.

## MOVE-ACTION-ILLUSION-TICK-001 - janela ClientTick em MSG_Action3 (29/09/2026)

`Test-AutoTradeClose.ps1 -ActionRange` rodou em loopback com contas sinteticas
isoladas. O RED, antes do gate, reproduziu relay de `MSG_Action3` com tick mais
de 120 s antigo. No GREEN, ticks `-120001 ms` e `+30001 ms` nao foram relayed ao
autor nem ao peer e cada rejeicao somou 1/type104. Action normal continuou
rejeitando com 1/type102, e Action2 com `ClientTick=uint.MaxValue` permaneceu
relayed. A regressao tambem passou para Action valido, range/velocidade,
persistencia/relogin e carry/cargo/Gold.

Resultado: Engine.Tests 302 PASS/0 FAIL; solution build 0 avisos/0 erros;
self-test do harness PASS e smoke `-ActionRange` PASS. Marker:
`CLIENT_E2E_PASS action-range old-future-ticks-rejected
illusion-old-future-ticks-rejected action2-tick-bypass speed-equal-speed-clamped
three-opcodes dual-violation 33-34-66-67 private-correction
position-carry-cargo-gold-relogin`; tambem `ACTION_NETWORK_PASS`,
`CRACK_NETWORK_PASS`, `AUTOTRADE_NETWORK_PASS`, `ORIGINAL_HASHES_INTACT` e
`PORT18283_RELEASED`. Evidencia RED:
`Backup/Build/AutoTradeCloseSmoke/ce3d2ecd91c449c7a9905c41d443ff28`; GREEN:
`Backup/Build/AutoTradeCloseSmoke/ba0b956c68134fd8821fc8467e9d6cc7`. O cliente
visual nao foi aberto. A elegibilidade esta coberta em
`MOVE-ACTION-ILLUSION-ELIGIBILITY-001`; mana foi concluida em
`MOVE-ACTION-ILLUSION-MANA-001`, cooldown e E2E visual seguem pendentes. Sem
deploy, Site/producao, commit ou push.

## MOVE-ACTION-TICK-001 - janela ClientTick em MSG_Action (29/09/2026)

`Test-AutoTradeClose.ps1 -ActionRange` foi executado em loopback com contas
sinteticas isoladas. O RED, antes do gate no servidor, reproduziu o defeito:
`MSG_Action` com `ClientTick` mais de 120 s antigo chegava aos clientes. No
GREEN, ticks `-120001 ms` e `+30001 ms` nao foram relayed ao autor nem ao peer e
cada rejeicao somou 1/type102 na sessao. O controle `MSG_Action2` com
`ClientTick=uint.MaxValue` foi relayado, confirmando que o branch nao recebeu o
gate. A regressao tambem passou com Action valido e cobriu range, velocidade,
estado persistido/relogin e carry/cargo/Gold.

Resultado final: Engine.Tests 302 PASS/0 FAIL; solution build 0 avisos/0 erros;
self-test do harness PASS e smoke `-ActionRange` PASS. Evidencia retida:
`Backup/Build/AutoTradeCloseSmoke/b1d7e3f78bdb42d1a8bbbde9a4f5ff97`. O smoke
reportou `CLIENT_E2E_PASS action-range old-future-ticks-rejected
action2-tick-bypass speed-equal-speed-clamped three-opcodes dual-violation
33-34-66-67 private-correction position-carry-cargo-gold-relogin`,
`ACTION_NETWORK_PASS`, `CRACK_NETWORK_PASS`, `AUTOTRADE_NETWORK_PASS`,
`ORIGINAL_HASHES_INTACT` e `PORT18283_RELEASED`. A porta 18283 ficou livre.
Validacao wire/headless; nao abre WYD.exe. Sem deploy, Site/producao, commit ou
push. Mana/cooldown de Action3 e E2E visual continuam pendentes; elegibilidade
e tick estao cobertos em recortes proprios.

## MOVE-ACTION-RECALL-001 - recall pela origem (29/09/2026)

`Test-AutoTradeClose.ps1 -ActionRecall -SkipBuild` autenticou quatro contas
sinteticas isoladas: autor e observador na origem, observador no destino e
observador fora das duas janelas 33x33. O autor e o observador do destino
criaram drops via `MSG_DropItem`; apos o recall o autor recebeu
`MSG_DecayItem` para o item 10001 da origem e `MSG_CreateItem` para o item 10002
do destino. Os observadores nao receberam esses deltas. O recall tambem enviou
`MSG_Action` aos jogadores visiveis, `RemoveMob` ao observador deixado na origem
e `CreateMob`/`PKInfo` na entrada do destino; o distante ficou sem o Action.
As posicoes persistiram no logout. O teste de unidade adicional cobre itens
estaticos e drops juntos, IDs unicos no espaco compartilhado de `pItem`, e a
captura das duas janelas junto da mutacao no `WorldHub`. O relay nao usa
`BroadcastAsync`.

Resultado local: Engine.Tests 301 PASS/0 FAIL, solution build 0 avisos/0 erros,
self-test do harness PASS, smoke de recall e regression `-ActionRange` PASS. O
recall reportou `ACTION_ORIGIN_RECALL_NETWORK_PASS`, `CRACK_NETWORK_PASS`,
`AUTOTRADE_NETWORK_PASS`, hashes das fixtures originais intactos e
`PORT18283_RELEASED`. Evidencia do recall:
`Backup/Build/AutoTradeCloseSmoke/1bdcf451c5e94e5e814742d680829ea3`; regressao:
`Backup/Build/AutoTradeCloseSmoke/14f9767208e349e7b8d3bdc325cd9ac4`.
A execucao usou bypass de politica somente no processo PowerShell, sem alterar
a politica permanente. Validacao wire/headless; E2E visual com WYD.exe segue
pendente, assim como recalls por restricao de mapa e outros teleportes. Sem
deploy, Site/producao, commit ou push.

## MOVE-ACTION-STOP-001 - Action2 consecutivo (29/09/2026)

O smoke `Test-AutoTradeClose.ps1 -ActionNegativeHp -SkipBuild` autentica dois
clientes wire em loopback. No peer vivo, envia um `MSG_Action2` aceito e logo
depois outro: o primeiro chega aos dois clientes, o segundo nao e relayed e o
host registra exatamente uma linha `1/type101` para a sessao. Um `Action` normal
e aceito e libera novo `Action2`. A fase tambem verifica a sequencia dos tres
opcodes, trade/action gates existentes, persistencia/relogin e snapshots de
posicao, carry, cargo e Gold; somente copias isoladas das contas sao usadas.

Resultado local: Engine.Tests 298 PASS/0 FAIL, solution build 0 avisos/0 erros,
self-test do harness PASS. O smoke acima e o regression smoke
`Test-AutoTradeClose.ps1 -ActionRange -SkipBuild` passaram; ambos reportaram
`ACTION_NETWORK_PASS`, `CRACK_NETWORK_PASS`, `AUTOTRADE_NETWORK_PASS`, hashes
originais intactos e `PORT18283_RELEASED`. Evidencias:
`Backup/Build/AutoTradeCloseSmoke/d39cfcf26dc143fb9e305bb553f77801` e
`Backup/Build/AutoTradeCloseSmoke/463fd03be1864c37b574fbcaddf64063`.
Validacao wire/headless, sem abrir WYD.exe. O E2E visual continua pendente; sem
deploy, Site/producao, commit ou push.

## MOVE-ACTION-ANALYSIS-004 - limites severos por TCP (28/09/2026)

A bateria `Test-AutoTradeClose.ps1 -ActionAnalysis` agora exercita os limites
com contas sinteticas. O cliente confirma `AttackRun=255` no login: Speed15 e
permitido, relayado e nao cria Analise. Speed19 viola o nibble permitido 15,
mas e nao severo; o relay de ambos os clientes confirma Speed clampado para15,
sem sidecar/aviso/EOF. O caso Speed20 permanece positivo: a conta recebe o aviso
de Analise, a marca duravel precede o EOF e nenhum Action e relayed.

Resultado local: `Engine.Tests` 296 PASS/0 FAIL; build da solution 0/0; self-test
do harness e todas as fases TCP passaram. Os hashes das fixtures originais nao
mudaram e a porta 18283 foi liberada. Evidencia:
`Backup/Build/AutoTradeCloseSmoke/4b071419ef1c4c01a127f4d55d10f615`. O teste e
headless/loopback, sem WYD.exe, runtime deploy, Site ou producao. O detector de
porta do harness usa `netstat` e PID porque `Get-NetTCPConnection` nao expunha o
listener neste ambiente.

## Action severo marca conta em Analise e bloqueia relogin (28/09/2026)

O modo `Test-AutoTradeClose.ps1 -ActionAnalysis` usa somente copias isoladas de
`TESTE`, `TESTE2` e `TESTE3` em uma pasta GUID sob
`Backup/Build/AutoTradeCloseSmoke`. As credenciais sao substituidas nas copias
de teste e nunca sao impressas. Primeiro verifica que host com autenticacao
recusa iniciar sem `--account-review-root`; em seguida injeta uma falha de
escrita real no lado `TESTE3` (diretorio ocupando o nome do sidecar) e exige
aviso generico, EOF, ausencia de relay e posicao inalterada. O caso `TESTE2`
envia velocidade 20, exige quadro `0x0101` com checksum e aviso de Analise, EOF,
nenhum Action no peer, coordenadas persistidas intactas e sidecar duravel com a
evidencia autoritativa.

O harness encerra apenas o PID filho que iniciou, confirma a porta 18283 livre,
reinicia o host com a mesma raiz de contas/revisao e tenta senha incorreta para
`TESTE2`: a resposta de Analise antes da autenticacao prova a persistencia. A
conta-controle `TESTE` ainda deve receber `AccountLoginConfirmationV769`. Sem
parar o host, o harness move temporariamente a raiz duravel e autentica outra
vez; a consulta deve responder com aviso generico de indisponibilidade e EOF,
sem confirmar login, e a mesma pasta e restaurada antes do shutdown. A raiz de
evidencia retida inclui logs por fase e manifesto com hashes dos binarios/harness;
hashes das duas fixtures originais sao comparados no cleanup.
Esta bateria e wire/headless; nao abre WYD.exe nem altera Site, banco externo ou
producao.

**Resultado local (28/09/2026):** a suite `WydCdk.Engine.Tests` passou; a
solution build terminou com 0 avisos e 0 erros; o smoke TCP passou nas quatro
fases: falha real de escrita, marca duravel, restart/relogin bloqueado antes da
senha, conta-controle aceita e raiz indisponivel com negacao fail-closed. O
harness confirmou startup autenticado sem raiz recusado, ausencia de relay e
alteracao de coordenadas, hashes das fixtures originais intactos e porta 18283
liberada. Evidencia retida:
`Backup/Build/AutoTradeCloseSmoke/89f07045fd124e328a47c0b84fe6b08a`.

**Deploy operacional no sandbox (28/09/2026):** o pacote runtime-only de 197
arquivos foi publicado com SHA-256
`B70F7F6C5318EC54BBC879D8711B0624716768CE32A4D0C103422B71AD703E88`. As tarefas
UP/PVP estao Running em 8281/8282 no executavel atualizado; `servtest.htm` e os
dois heartbeats foram atualizados. Os 202 arquivos do runtime anterior estao
preservados no backup `C:\WYDCDK\Server\test-releases\backup-action-analysis-70c4d907`
e todos passaram verificacao SHA-256. Isto confirma start/listeners/status e
rollback, mas nao e E2E visual nem teste de login de conta real na VPS.

Na primeira tentativa o cliente E2E leu `CreateMob` (`0x0364`, 236 bytes) da
fila de entrada no mundo como se fosse a resposta do Action. O leitor passou a
consumir os frames anteriores, limitado a 32, e falhar se houver relay Action
antes do `MessagePanel`; a repeticao passou. Isto e smoke wire/headless, nao abre
o WYD.exe. A publicacao remota nao foi feita: as tarefas sandbox atuais escrevem
status em `C:\WYDCDK\Site\servtest.htm`, e reinicia-las alteraria esse arquivo
Site fora do escopo autorizado.

## MOVE-ACTION-ANALYSIS-003 — UP/PVP concorrentes no mesmo sidecar (28/09/2026)

O harness de `WydCdk.Client.E2E` aceita arquivos de estado dos personagens
separados dos arquivos usados somente para autenticar os clientes. Os caminhos
sao validados para permanecer em `UP|PVP/accounts/T/<mesma-conta>` dentro da
pasta GUID do smoke. A barreira opcional tambem e restrita a
`account-review/action-barrier`; os self-tests recusam estado/barreira fora
dessa arvore. Isso permite modelar a topologia real: UP e PVP mantem seus
proprios blobs, mas consultam a mesma raiz persistente de Analise.

O teste iniciou dois processos `WydCdk.Server.Core` reais na porta TCP 18283,
binds de loopback `127.0.0.1` e `127.0.0.2`, e dois processos independentes do
cliente E2E usando a mesma conta sintetica `TESTE2`. Depois de ambos autenticarem
e pararem antes do Action, o runner abriu o arquivo de lock estavel da faixa 22
com `FileShare.None` e liberou simultaneamente os clientes. Os logs de cada host
confirmaram o `5/type 4` antes da consulta de Analysis; enquanto o lock externo
estava aberto nao surgiu sidecar nem resultado de marcacao. Apos liberar o
handle, um host retornou `Marked` e o outro `AlreadyMarked`. Os dois clientes
terminaram com exit code 0 e verificaram aviso/EOF e ausencia de relay Action;
o autor manteve a posicao e ambos mantiveram carry/cargo/Gold persistidos. O
sidecar compartilhado terminou em `Analysis` com
`ActionSpeedViolationHighSeverity`. As contas de origem foram apenas
lidas/copias; seus hashes permaneceram iguais.

**Resultado local:** `Engine.Tests` 296 PASS/0 FAIL; solution build 0
avisos/0 erros; self-test do harness PASS; bateria completa
`Test-AutoTradeClose.ps1 -ActionAnalysis -SkipBuild` passou pelas fases startup
sem raiz, escrita indisponivel, marcacao, restart/login antes da senha e raiz
indisponivel. Evidencias do teste simultaneo (logs por host/cliente e log de
suite):
`Backup/Build/AutoTradeCloseSmoke/dual-world-analysis-64dd28c435ba434da3d724c8540e35d9`.
Bateria TCP: `Backup/Build/AutoTradeCloseSmoke/6a91756bb9b449de984a009b0728171e`.
Porta 18283 livre ao final. Validacao wire/headless, sem WYD.exe visual, deploy,
Site, producao, commit ou push; runtime sandbox permanece em ANALYSIS-001.

## Action cancela trade bilateral (28/09/2026)

TMProject 7.69 `_MSG_Action.cpp:24-28` e W2PP `:40-44` colocam o gate HP==0
antes de `Trade.OpponentID`; quando HP e diferente de zero, removem a troca dos
dois lados e retornam antes de speed, range ou movimento. O port chama uma
operacao atomica de dominio, emite `0x0384`/12 para cada destinatario (ID proprio,
tick e keyword zero) e nao relaya Action nem altera MOB/carry/cargo/Gold. HP==0
continua em HpMode +5/type3 sem cancelar; HP negativo cancela. O ramo autotrade
`TradeMode` e a deteccao/ban exclusiva 7.69 nao foram incluidos neste recorte
bilateral; o fechamento por Action/TradeMode esta documentado na secao abaixo.

Engine.Tests: 273 PASS; solution build: 0 avisos/0 erros. TCP loopback Debug
passou nos cenarios HP0 e HP-1, incluindo checksum/ID dos dois sinais, ausencia de movimento, par preservado no
HP0 ate Action do parceiro vivo e snapshots byte a byte/carry/Gold intactos.
Evidencias isoladas em `Backup/Build/AutoTradeCloseSmoke/9c938f323d104d59aed0b6ab9bbc3c0d`
(HP0) e `a1a4e40ebd1f4c7795dfbc833c48c22c` (HP-1); `Test-AutoTradeClose.ps1`
preparou as copias e confirmou hashes originais intactos, mas seu `Start-Process`
parou na colisao Path/PATH antes de iniciar o host. O listener e o harness foram
iniciados diretamente em loopback e encerrados; nao houve publicacao, deploy,
Site, producao, commit ou push. Sem smoke self-contained ou cliente visual.

Este registro cobre somente o trade bilateral sem loja. A extensao de Action
com `TradeMode` e a combinacao loja+trade estao no proximo recorte.

## Action fecha TradeMode com trade bilateral (28/09/2026)

TMProject 7.69 e W2PP confirmam o gate exato `HP==0` no `Action`; depois,
`RemoveTrade` fecha primeiro um par bilateral, ou a loja `TradeMode` do autor.
No C# o fechamento foi ligado ao coordenador duravel compartilhado, com lease,
expected-delete e recovery. Se a loja estiver no autor ou no parceiro, fecha o
conjunto aplicavel e retorna antes do movimento. O gate de Action nao e o de
QuitTrade: HP negativo segue para este fechamento, enquanto HP==0 preserva
HpMode sem cancelar.

No fechamento aceito, cada participante recebe seu `0x0384`/12, com ID proprio
e tick/keyword zero; cada loja fechada gera `CreateMob` normal/236 para os
destinatarios na janela de visao. Nao ha PK, relay de Action,
`CreateMobTrade`/260 ou `RemoveMob`; carry/cargo/Gold/MOB permanecem intactos.
Unidade cobre duas lojas junto de trade bilateral, HP negativo, shop-only,
no-op, HP==0 e recovery ambigua.

Engine.Tests: 276 PASS; solution build: 0 avisos/0 erros. Smoke E2E wire em
`127.0.0.1:18283` passou combinando trade bilateral e duas lojas: conferiu sinais
privados e CreateMob normal para os dois streams de visao, ausencia dos frames
proibidos, fechamento persistido e limpeza apos retries/relogin. Resultado:
`CLIENT_E2E_PASS autotrade-open-query-close-normal236-retry-buy-rejected-carry-cargo-gold-relogin`,
`CRACK_NETWORK_PASS` e `AUTOTRADE_NETWORK_PASS`. Evidencia:
`Backup/Build/AutoTradeCloseSmoke/39e5171332814620b95ebc0a671b0650`.
Hashes das fixtures originais intactos e porta liberada. O harness simula o wire;
nao abre o WYD.exe, portanto o teste visual real permanece pendente. Sem deploy,
Site, producao, commit ou push.

Na primeira execucao, uma assercao do harness esperava o ID do mob no header do
CreateMob; o contrato coloca `SceneId=30000` no header e o mob ID no payload.
A expectativa foi corrigida e o smoke completo passou; isso nao era falha do
servidor.

## Action range / correcao privada (27/09/2026)

`_MSG_Action.cpp` W2PP e TMProject 7.69 validam Action/Action2/Action3 contra
VIEWGRID 33 por eixo antes do mapa; >33 soma1/type5, e somente >66 corrige com
Action3 privado/Speed6 de `GetAction`. C# usa posicao autoritativa, descarta
origem/rota stale para o gate e nao muda posicao nem faz relay em rejeicao. O
movimento aceito e reenviado ao ator e somente aos personagens na janela de
multicast 33x33. Route zero/target origem/server tick sao conferidos na correcao.

Suite267 PASS e build da solution0 warnings/0 errors. TCP loopback cobriu os tres
opcodes, efeitos0/1/2, deltas positivos/negativos nos dois eixos e diagonais;
34/66 rejeitam sem correcao, 67 corrige privadamente, limite33 aceita; regressao
de relay dentro/fora de visao, stale origin/route e ausencia de HP-mode. 42 erros
type5 acumulados no mesmo ator; QuitTrade observa52 e relogin zera para10. Save
antes do relogin confere destino do servidor, HP/carry64/cargo128/Gold intactos.
Evidencia loopback: `Backup/Build/AutoTradeCloseSmoke/40af9d46592a40659ea7a45765a3eb96`;
ExitCode0, assertions e cleanup true, listener18283 encerrado e hashes originais
das fixtures preservados. Threshold2G foi exercitado somente na suite de unidade;
cliente grafico/E2E visual nao foi executado.

Self-contained repete o mesmo E2E: `d8b09ae5b15d47d791cad5c85a232f5c` (passou).
Controle da runtime anterior `d022056beff04868a28a88ad814c04eb` falhou na
correcao Action3 privada, com cleanup e fixtures intactas. Regressao Motion
`e50cb81f4c5d4215a46b6673800f9428`, ReqTradeList
`acc671ee731c44b6b92e7eedba2be226` e autotrade evidence guard
`d2880c6190514d66b4da312449e7b0d7` passaram.

Runtime self-contained publicado como pacote somente DLL/EXE/deps/runtimeconfig
(197 arquivos; ZIP SHA-256 `04CF787E7CE983A269626F500AA378A6FB44508A4B255FF5ABAFFD239E5C47DF`)
na sandbox `C:/WYDCDK/Server/test-releases/20260924-v7670/WydCdk.Server-v7670-20260924`.
Backup do runtime anterior: `backup-autotrade-close-293dd25bdc89446fb71c86beb3d0ba54`.
Helper confirmou tasks UP/PVP Running, portas8281/8282 Listen, caminho do EXE e
hashes Core `AB7B1B147E5DE498A63762F3E79EC1AEF181D689B18D0C98D81D55E3CF7504EC`,
World `0A716BBFAABE0BB2B4579B351AA47A402BA7B9698F4C1550171253AFF2462B0B` e
Protocol `D6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295`.
Staging temporario removido apos verificacao. Sem mudanca do Site/producao,
source/segredos no VPS, commit ou push.

## Action movement speed (28/09/2026)

W2PP e TMProject 7.69 limitam `MSG_Action.Speed` a
`CurrentScore.AttackRun & 0xF`; excesso soma 5/type 4 e clampa somente Speed
antes do gate de range. C# aplica a regra server-side nos tres opcodes
Action/Stop/Illusion. O smoke de pacote cobre velocidade no limite e acima do
limite em movimento valido, relay privado/ao peer com o clamp e violacao dupla
speed+range rejeitada sem movimento/relay. O contador e compartilhado: quatro
eventos type 4 chegam a 5/10/15/20; 43 eventos range type 5 seguem em 21..63;
QuitTrade observa 73 e relogin volta a 10.

Engine.Tests: 271 PASS; solution build: 0 warnings/0 errors. TCP loopback Debug
passou em `Backup/Build/AutoTradeCloseSmoke/1fb224d3f291417ea0250c471076432b`;
o mesmo teste contra o executavel self-contained passou em
`Backup/Build/AutoTradeCloseSmoke/3abde0cbd09e4d5fb13518268c604d94`.
Ambos registram ExitCode0, assertions/cleanup true, hashes das contas originais
intactos e porta 18283 liberada. A regressao unit test-first cobre HP==0 com
AttackRun ausente, HP ausente, personagem vivo sem AttackRun e HP negativo. A
revisao estatica final nao encontrou outros achados. Nao e smoke visual do cliente.

O port omite deliberadamente o side effect exclusivo do TMProject 7.69:
`Speed>=20` ou `AttackRun>=300` tambem aciona `SendBanAccount`/
`SaveInfoDetect`. Registrar e decidir essa divergencia em recorte proprio.
Runtime self-contained com 197 arquivos publicado na sandbox
`C:/WYDCDK/Server/test-releases/20260924-v7670/WydCdk.Server-v7670-20260924`;
backup `backup-autotrade-close-0b04898eaf31414bae0f2005baa006ac` preservado.
Pacote local `artifacts/WydCdk.Server-v7670-20260928-action-speed-reviewfix-r3.zip`;
ZIP SHA-256 `000F987FAF29C74F485D29F3F1B3CF3B2A97C1E2D960F7550918D5C1C7CCB5D9`;
Core `A055A11A1DF0E9FAE60BB61CEBE252681EF37363A15B125A7DC2AD0328A1D483`,
World `3B7CA3705756E861A5C656C33D699E99BDC3CDE6B6F5DBAB71F93ADC9048768E`,
Protocol `D6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295`.
UP/PVP Running, 8281/8282 Listen e os hashes das 197 entradas conferidos
independentemente; o destino tem 202 arquivos, incluindo cinco extras anteriores
(dois logs e tres PDBs) preservados e nao incluidos no pacote. Staging remoto
removido apos a verificacao. Sem Site/producao, source/segredos, commit ou push.

## Action/AddCrackError (27/09/2026)

TMProject/W2PP `_MSG_Action.cpp` e dispatchers confirmam Action036C,
Action2/Stop0366 e Action3/Illusion0368 no mesmo gate. Frame52/payload40;
0366 tambem identifica PK16, portanto assercoes usam ActionRequest.TryParse,
nao somente opcode. Fora USER_PLAY: nenhuma resposta HpMode/Action nem pontos.
Em jogo HP==0: privado HpMode20/HP0/Mode22 e5/type3; HP-1 move sem pontos.
Type3 nao loga: QuitTrade observa10/20 antes do jogo,25 (morto)/10 (negativo)
depois dos tres pacotes e10 apos relogin. Logs ligados ao ID da sessao.

Suite265 PASS; build completa0 avisos/0 erros e build do harness repetida0/0.
Self-tests recusam port8281/modos conflitantes e relay com opcode/ID/tick/route
alterados. Reviewer independente nao encontrou problemas no gate.
Falha inicial a9d26d9bbf834b29a1aa11c4bc18b600 revelou expectativa fixa de spawn
incorreta: ResolveCharacterLoginPosition sorteia novato em torno2100/2100,
independente de SPX/SPY. Teste corrigido, servidor de spawn nao alterado.
Captura spawn wire offsets0/2; verifica destino salvo MOB+40/+42 antes do novo
login. Depois captura novo spawn e verifica seu salvamento; carry64/cargo128 e
Gold preservados. Actor morto nao move; negativo e vizinho vivo movem tres vezes.

Runs em Backup/Build/AutoTradeCloseSmoke (ExitCode0/AssertionsPassed/Cleanup):
- Debug HP0:45ce811e413f4439939f608c1dc24465; HP-1:acbdaa3ac964474297648c26d0a06e13.
- Self-contained HP0:94ed32ec862444a9959a24004df919cb; HP-1:fd4d4438abd840449957e060c3dc7cfc.
- Regressao ReqTradeList-1:19f13a74a0374cc6b81438c0554bf613;
  Motion-1:8bf934a35e6b4209a85a2c2be98fa953;
  autotrade/evidence validator:17a696478db94545bba8203bf869ed67.

Runtime self-contained em Server/artifacts/publish-20260927-action-crack.
Empacotamento inicial concorreu com smoke, encontrou DLL ocupada e nao produziu
ZIP; repeticao depois do encerramento dos hosts passou.
Controle runtime anterior publish-20260927-tradelist-crack:
860d483158d345feb86918a087747ea4 falhou HP/mode response mismatch;
ExitCode=-532462766/AssertionsPassed=false/CleanupCompleted=true.
Pacote action-crack-20260927-runtime.zip:197 arquivos DLL/EXE e deps/runtimeconfig,
sem source/docs/segredos/configs de ambiente. ZIP SHA256:
46D2A950374306D170534EBF9B3BA0749D2ECF610C9640617418617B2F46E96A.
Core SHA256:EBBCE04D51BAA225D5FB8870202DC74F2A161D25029E372D0F5AA5D5DCC643BA.
World SHA256:D7963218230A9EF3DAC04AB67169735F231834F81F00A01523919504E68878B9.
Protocol SHA256:D6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295.
Apply-AutoTradeCloseSandbox.ps1 confirmou197 arquivos no destino aprovado
C:/WYDCDK/Server/test-releases/20260924-v7670/WydCdk.Server-v7670-20260924,
backup-autotrade-close-b0654dc714b64a258cbba446bf103ef9 preservado; UP/PVP Running,
8281/PID6092 e8282/PID1420 Listen e executable path/hashes iguais.
Sem Site/Git/producao; contas e wrappers/configs fora do pacote.
Demais branches Action, thresholdTCP e E2E3D permanecem fora deste recorte.

## ReqTradeList/AddCrackError (27/09/2026)

As duas `_MSG_ReqTradeList.cpp` e seus SendAutoTrade foram comparados:
HP==0 ou Mode!=USER_PLAY: SendHpMode, AddCrackError10/type87 e return antes do
alvo; HpMode somente playing. HP negativo nao e bloqueado. C# portado no helper
compartilhado, regra pura ajustada de<=0 para==0. Nao redesenha compra/loja.
Guard IDs<=0/>ushortMax evita excecao da lease para inteiros wire invalidos,
preservando IDs C#/NPC offline em vez de importar MAX_USER sem adapter. HP
relido apos aguardar lease; se zerou, libera lease antes de HpMode/crack/logout.
Race deterministica durante essa espera foi fechada no recorte P3 de 28/09, abaixo.

Validacao somente no fim do bloco conforme usuario: suite264 PASS/build0/0;
erro inicial CS1061 no teste nullable DecodedFrame corrigido com Value; build
limpa repetida. Guardas parser rejeitam port8281/modos conflitantes, aceitam
somente127.0.0.1:18283/AutoTradeCloseSmoke/<copy>/account/T/TESTE* e autotrade.json.
Self-tests visuais recusam dono/title divergentes; HpMode no peer recusado.
Accepted/Selection usam ReqTradeList alvo0 + logout como barreira causal,
somente ack0116 e logs10/20/type87. CharacterLogin zera contador. Owner vivo
abre item900/cargo4/preco100/taxa5; consulta HP0 recebe HpMode20/0/22 privado,
sem lista/visual; HP-1 recebe lista196+visual260 privados iguais a abertura.
Consulta viva e alvos0/-1/65536 nao pontuam. QuitTrade morto depois soma30/10
no mesmo ID do ator; todos os logs vinculados ao marcador LIST_ACTOR_ID.
Loja JSON intacta nas consultas, carry64/cargo128/Gold completos dos dois
intactos no disco antes da saida e apos relogin; HP do ator preservado.

Runs abaixo de Backup/Build/AutoTradeCloseSmoke:
- debug inicial HP0:6c7b3ab5b443486e81410cb52ca513b4; HP-1:2f9b3abc3ae649f4915360284282795d.
- debug final HP0:10c0abed14f6483d99eb38ceb7a72268; HP-1:ce97844625a4477abd157632e6193756.
- self-contained final HP0:53492079df4b40fc8d109ef185a35ee2; HP-1:594362f64b3845a3aca7080a25a71e81.
- regressao autotrade:b07d4270d5884fbbbad86e062df8ddb3; MotionHP-1:29b8f144d125439695f9ede82940d0e4.
- validador hashes/result/output/cleanup:0b2ec2667b384f72a479b55dcbd87430.
- controle runtime Motion anterior HP0:3d7c805c241f4cbc96bce3f338178e99, HP/mode mismatch.
- controle anterior HP-1:427f4d1a4de44cd7867fa425973ba730, lista ausente.
Ambos negativos nativeExit=-532462766/AssertionsPassed=false/CleanupCompleted=true.
Runs aprovados exit0/assertionstrue/cleanuptrue. Originais SHA-256 preservados,
18283 liberada. Revisao read-only final conferiu correcoes de snapshot/IDs e
os dois minors de visual/logconnection; sem bloqueadores. A cobertura P3 foi
adicionada no recorte seguinte, sem reabrir a publicacao anterior.

Runtime-only197 publicado target sandbox existente20260924-v7670;
backup-autotrade-close-41791a9601ee4a25b57e60a14189173e preservado.
UP/PVP Running,8281 PID6604/8282 PID5704, paths/hashes conferidos pelo helper.
Core FD51AB31EC7D96715BE9E3214A0BA4A2ADF6881C4B278753FC85329978DBCA1E;
World84AD8E5DA326BA8FAA19284EA9330C10706D52A6E0E7EAFF34A37D38CE37B482;
ProtocolD6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295;
ZIP402207F4ED57F8A2A61429ECE9602A1740E81FF3F0AF173E641F7C4ADDC8C015.
SCP -O, helper -NonInteractive/Force restrito por PID/path e backup runtime.
Contas/configs/Site/source/segredos fora do pacote; sem commit/push/producao.
Threshold coberto em unidade, nao TCP; E2E visual e demais callers/reset pendentes.

### ReqTradeList: morte durante a espera de lease (28/09/2026)

O comportamento pos-lease ja existia; o bloco fecha a lacuna de teste, sem
alterar HP==0/type87 nem o wire. `TradeListRequestHandler` foi extraido do
dispatcher do `Program.cs` para exercitar o mesmo fluxo host com o `WorldHub`,
`LoginSessionRegistry` e `LegacyAutoTradeBook` reais. O teste retém a lease
ordenada de requester/loja, invoca o handler ate ficar bloqueado, altera HP
para zero e libera a lease. Exige uma unica resposta `0x0292`/20 checksum-
valida, ID do requester, tick77, HP0/Mode22, uma chamada10/type87 e nenhuma
resposta `0x0397`/196 ou `0x0363`/260. O callback de envio tenta readquirir a
mesma lease com timeout, provando que ela foi liberada antes do I/O; o controle
vivo ainda recebe list196+visual260 e nao pontua.

Controle negativo: a comparacao pos-lease foi temporariamente mutada para
`HP==int.MinValue`; o teste falhou com `ReqTradeList did not reject the requester
after the lease wait`. Condicao original restaurada; suite completa 272 PASS,
build da solucao 0 avisos/0 erros e self-tests do harness PASS.

Integracao do dispatcher validada tambem por smoke TCP com host compilado e
harness em `127.0.0.1:18283`, usando apenas copias de fixtures em
`Backup/Build/AutoTradeCloseSmoke/44363fdb4cf64e80b878d25df373ea82`: exit0,
`CLIENT_E2E_PASS trade-list-gates hp=0`, HpMode privado, consulta viva
list196/visual260, counters10/20/type87, QuitTrade30/type17, relogin e blobs
intactos. O runner PowerShell padrao falhou antes de iniciar o host porque
`Start-Process` encontrou `Path` e `PATH` duplicados; o host foi iniciado
diretamente no loopback e encerrado por `stop`, sem alterar a policy permanente.
Esse TCP smoke cobre o dispatcher normal, nao simula a corrida concorrente;
esta fica provada pelo teste deterministico in-process. Porta liberada, originais
preservados; sem VPS, Site, producao, commit ou push.

## Motion/AddCrackError (27/09/2026)

Comparacao `_MSG_Emotion.cpp` TMProject 7.69 / `_MSG_Motion.cpp` W2PP:
HP==0 ou modo fora USER_PLAY chama AddCrackError4/type6; SendHpMode somente
playing. HP negativo relaya, diferentemente de QuitTrade<=0. Contador/helper
e logout compartilhados, sem redesenhar relay ou implementar todos os callers.
263 PASS/build0/0. Rede debug HP0:24916b0f6b834507be65c70c40e70bdc;
HP-1:30e780192e23455aa1a58bd98160c624. Self-contained inicial HP0:
a93bf638146a48648b9b925ea0ce0851; HP-1:822cb14f250a4a3a99e2d5e83e1e1a46.
Teste cobre Accepted/Selection sem HpMode/Motion, logs4/8, reset no login,
HP0 log4/type6 sem relay, HP-1 sem incremento; QuitTrade depois total14/10;
Motion vivo preserva campos/ID/tick em ambos streams; carry/cargo/Gold completos
e relogin intactos. Originais preservados e porta18283 liberada em todos.
Regressoes autotrade2467de644b88401d8b3f7b52a14510c3 e DeadQuit
2fc639b3f53843008f09be9905edbd53; validador aa6e263959454a5c9f6ca9c11366d3cd.
Falha inicial f13f4051080c484ca6f98ca464d75b93 era offset de Gold no harness:
wire24 versus blobW2PP28; corrigido pela constante CoinOffset. Build inicial
concorrente com suite gerou warning de DLL bloqueada e suite antiga262, evidencias
descartadas; rerun sequencial limpo263. Controle antigo8318a1b74a1f4912a4db40388bd44619
nao vale como negativo funcional: conflito de porta, repetir isoladamente.
Revisao read-only sem defeito de runtime; lacuna P3 em ausencia de HpMode no peer
corrigida com assercao compartilhada e vetor negativo; self-test e build0/0 passaram.
Smokes finais contra self-contained: HP0 291e1d50f131477980937058be6bb86f;
HP-1 6664e21fa4c54e30bcc2d96333496d79. Controle antigo isolado
b2a08387f2e54a149ff390535dd4b7be: nativeExit0, AssertionsPassed=false,
CleanupCompleted=true; falhou especificamente Motion/shared QuitTrade counters.
Runtime-only197 publicado no mesmo target sandbox20260924-v7670; backup
backup-autotrade-close-03b8a821c8214ba79fffea21f0adfdaa preservado. UP/PVP
Running verificados pelo helper;8281 PID504/8282 PID3232, paths e hashes iguais.
Core19F1A4D9155B8A70086C53EBF5F46B3841A36F161C61A7F8FEED3BE0B2F138E0;
World26FFCDF82792568574737D4CA7932CF7A16EC650C2D435523861AB6F5BE59ADB;
ProtocolD6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295;
ZIP6F76BCA7E6DC43743DD7D11F840080C7B2AA5DF61588391B3E4F131432413C82.
SFTP/primeira invocacao SSH encerraram conexao; SCP -O e repeticao SSH passaram.
Aplicacao nao interativa com Force restrito por PID/path; contas/configs/segredos/
source/Site nao incluidos. Site intacto; sem commit/push/E2E visual.

## QuitTrade/AddCrackError (27/09/2026)

As duas sources usam AddCrackError(conn,10,17) quando HP<=0 ou Mode!=USER_PLAY.
SendHpMode retorna sem frame fora USER_PLAY. Server.cpp soma NumError, suprime
log somente para tipos3/8/15 e chama CharLogOut apos aviso Language23 quando
NumError>=2000000000. DataServerPackageHandler/ProcessDBMessage zeram no login
do personagem. C# reproduz este recorte por sessao; satura no limite em vez de
herdar overflow de int C++. Demais callers/reset por timer/Restart/Attack nao
estao integrados; nao afirmar paridade de anti-cheat completo.

Validacao no fim do bloco, conforme pedido: 262 PASS e solution build0/0.
Revisao independente sem achados confirmados. Testes unitarios cobrem limite
exato, abaixo/acima, overflow, tipos suprimidos, isolamento/lifecycle e incrementos
concorrentes. Nao foi exercitado o caminho de limite via TCP (200M pedidos);
o host reutiliza o mesmo logout validado nos smokes normais.

Rede debug: Test-AutoTradeClose.ps1 -SkipBuild, evidencia
Backup/Build/AutoTradeCloseSmoke/4b33df4c88594217a6c5860ed5d2e293.
QuitTrade antes do login e na selecao, seguido de logout como barreira causal:
somente ack0116, sem HpMode/cancelamento/PK; logs10/20. QuitTrade vivo/retries
nao pontuam. Autotrade/normal236/relogin/carry/cargo/Gold regredidos.

Rede HP0: mesmo runner com -DeadQuit usa copias novas abaixo E2E7670, sem
afrouxar a guarda existente; evidencia f8dbe12cfdc64dc88dd21a663b970b2b.
HpMode20/HP0/22 e log10/type17; par mantido ate o vivo fechar, carry/Gold completos
nos blobs/relogin intactos. Tentativa inicial fora desta familia foi rejeitada
pela guarda, sem mudar originais. Ambos runners liberaram18283 e preservaram hashes.
Self-contained passou os dois modos: autotrade-close evidencia
d083c99739e3457abc25a3b808fc91e2; HP0 evidencia e6117fe30aaf4b44a581b9a8e50a38ab.
Restore RID win-x64 local foi necessario apos NETSDK1047; --ignore-failed-sources
e NuGetAudit=false usados somente nesta chamada, sem trocar pacotes/config.
Auditoria online de vulnerabilidades nao foi feita neste bloco.

Reforco do manifesto: AssertionsPassed separado de ExitCode nativo evita que
uma falha nas contagens dos logs pareca sucesso. Runtime anterior a este recorte
termina E2E nativo0, mas falha corretamente no runner (AssertionsPassed=false,
CleanupCompleted=true): evidencia dbbd8bf318f143139a325ad75a9750f7.
Atual passou validador hashes/output/resultado e cleanup negativo, evidencia
1cf57b2e61e3494ab12342313684706d. Este complemento do harness foi verificado
localmente depois da revisao do codigo produtivo.
Publicacao runtime-only sandbox concluida: 197 arquivos, target ativo
C:/WYDCDK/Server/test-releases/20260924-v7670/WydCdk.Server-v7670-20260924.
Backup final backup-autotrade-close-09d8701091f3459582f735e176921426 preservado;
UP/PVP Running, 0.0.0.0:8281 PID6984/8282 PID4420, paths conferidos pelo helper.
Hashes locais/remotos: Core C78741516D3130393C60B2AC0ADA6817386D2F25D753C1484447ACD7EC282F00;
World 80B67FA904ED50108812B2C72793BDAA5515C80978447018C118F823A3271234;
Protocol D6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295.
Zip 372DB837B8A17E85E96BECB61F3A4D8A58E8A43CD04D231BCDC941AC02D7F646.

Incidente operacional: helper inicial PID7132 estagnou apos parada; tarefas Ready,
Core ausente e hashes ainda antigos. Backup e9e148a1fa4c4bc69ecab37a8c150af7
conferido por hashes antigos/whitelist; Restore-QuitTradeSandbox.ps1 encerrou
somente aquele helper identificado e restaurou197 runtime files/religou tarefas.
Reaplicacao com -NonInteractive e Stop-Process -Force (somente PIDs do target
ja verificados) passou com progress markers. Confirmacao interativa e hipotese
do travamento, nao stack trace comprovado. Ambos backups preservados; configs,
contas, Site, source e segredos fora do pacote. Nenhum commit/push.

## T6: autotrade real no listener (26/09/2026)

`Test-AutoTradeClose.ps1` cria somente copias em Backup/Build/AutoTradeCloseSmoke,
usa loopback18283/accounts-root/autotrade-state/ItemList real e encerra apenas
seu PID. Invocacao validada: ExecutionPolicy somente Process e `& ./Server/tools/
PortAudit/Test-AutoTradeClose.ps1` na shell existente; filho powershell -File
apresenta Path/PATH duplicados neste ambiente. Nao alterar politica da maquina.

RED self-contained anterior publish-20260925-quittrade-hpgate abriu/consultou
loja e falhou por normal236 ausente ao fechar. Evidencia955ac834ae5641f09fe6b0039bd5bfc4.
GREEN debug91a3adfd7502470f9144762981a2bf3a e release046aef37a0324dae9e9600e4b085a49e:
lista196/shop260 para dono/consulta e visual260 para vizinho; QuitTrade emite
sinal12/PK16/normal236, sem RemoveMob/shop260. Compra antiga rejeitada; dois
reenvios nao duplicam visual. Carry64/cargo128/Gold completos conferidos no disco
antes do logout e apos relogin dos dois; hashes originais intactos/18283 liberada.
O spawn e validado pelos limites de Armia fora da area protegida, nao por ponto
salvo fixo; nao exercita outras cidades, mapas ou colisao.

Review independente: porta padrao8281 indevida e barreira de compra na conexao
errada corrigidas. `dotnet .../WydCdk.Client.E2E.dll --self-test-autotrade-harness`
teve dois RED->GREEN: rejeita porta habitual e captura resposta buyer-only via
TCP real. Testes negativos adicionais rejeitam externo/originais/modos mistos/
estado ausente antes de autenticar. Suite259/build0avisos0erros/MariaDB isolado
passaram. Concorrencia/recovery continuam T3/T4, nao inferir dessa rede sequencial.
Minors anteriores resolvidos: Id do sinal/PK state0 agora exigidos na fixture
neutra, negativos com checksum valido tiveram RED->GREEN. Runner guarda
client.log completo e evidence.json (version1, exit nativo, hashes de runtime,
harness/Protocol/script e fixtures originais). Test-AutoTradeCloseEvidence
passou em cf2b5eebee4d4ee791c8d9d76a1e3055; falha real contra runtime antigo em
10cf2ed09cce434f890cbd1d5681e5d9 conserva stderr/nonzero, nao vira PASS por Tee.
Sem dados de autenticacao no contrato de output. Junctions deliberadas nao
testadas; runner usa GUID/copias ordinarias. Isto nao e E2E visual no cliente3D.
Review posterior corrigiu janela de cleanup: manifesto agora e gravado em
finally independente mesmo se o callback falhar. ExitCode preservado e
CleanupCompleted=false impedem sucesso indevido. Teste -CleanupFailureOnly
teve RED->GREEN; smoke final8bd21c4cfd1d4db5a47f28f673839e9b e suite259/build
limpa passaram. Validador ainda nao exige papeis de todos os artefatos, e sua
regex de formatos sensiveis nao certifica ausencia geral de segredos; ver ledger.
Publicacao runtime-only sandbox concluida: 197 arquivos conferidos, UP/PVP
Running e 8281/8282 Listen, hashes Core/World/Protocol iguais aos locais. Alvo
ativo test-releases/20260924-v7670/WydCdk.Server-v7670-20260924, configs/contas
intactas e backup backup-autotrade-close-7c34c243ab694647b2dd8e168bc864d7.
Hashes completos/staging no ledger. Sem smoke autenticado remoto, cliente3D,
commit/push ou Site/producao. Isto valida somente o recorte wire/rede do bloco.

## Logout/disconnect e cleanup residual (26/09/2026)

Suite completa 259 PASS e solution build sem avisos/erros. Testes de aplicacao
com world/book/arquivo reais cobrem save apos compra/rollback, captura RemoveMob16
antes de Leave, view33x33, envio falho sem restauracao, pendencia preservada,
reconnect esperando compra, expected delete stale apos AllUnchanged e conflito/
resposta incerta. Barreira por owner registrada atomicamente rejeita geracao
fora dos IDs salvos; recovery detached nunca toca jogador substituto ou envia
relays antigos. Fixture de substituicao corrigida para MOB com Coin999; nenhuma
mudanca de regra de saldo. Contas originais TESTE/TESTE2 com hashes intactos.
Politica ordinaria de remocao/logout/NPC offline permanece anterior ao recorte.
Nao houve novo smoke TCP, MariaDB vivo, E2E visual ou deploy. T6 precisa abrir/
consultar loja real, fechar e validar visual236, retry/relogin pela rede; estes
testes de aplicacao nao substituem esse gate.

## Abertura/consulta: captura antes do envio (26/09/2026)

Host captura lista196/visual260/destinatarios sob startLease/listLease, depois
libera a lease antes dos sockets. Save de abertura continua protegido e antecede
a captura. Suite 251 PASS; dois testes novos usam book/world reais e cobrem
online/offline, vizinho16/17, consulta privada, reabertura e lease invalida.
Somente captura de aplicacao foi exercitada; dispatcher TCP real e falha de
socket de abertura/consulta ainda requerem T6. Sem novo smoke/release/VPS.

## Recovery por owner no login (26/09/2026)

CloseAndRecoverAsync agora antecede a entrada no mundo. Descobre o fechamento
pelos AffectedOwners, adquire os IDs salvos e revalida token/request antes do
readback. Os planos sao capturados sob leases e enviados depois do retorno.
Suite 249 PASS: desconectado, owner sem loja, Unknown/Conflict/AllUnchanged,
autor original e retry sem delete cego. Handle online/stale nao-NPC bloqueia
reconnect sem apagar loja nesta evidencia historica; cleanup detached posterior
esta registrado na secao vigente acima.
Esta fatia tem testes de aplicacao/arquivo e build, nao novo smoke TCP/login,
MariaDB vivo, E2E visual ou deploy. As evidencias de rede abaixo sao anteriores.

## Regressao do dispatch de fechamento (26/09/2026)

QuitTrade do host local agora chama CloseAndCaptureAsync: outcome/frames
capturados sob leases e enviados apos liberacao. LoginSessionRegistry passou
a ser compartilhado/concurrente, removendo sessoes em finally. Suite 245 PASS.

Repetidos em loopback127.0.0.1:18283 com contas copiadas para
`E2E7670/t5shared-9f5cc99e6ec746379ff486a8b844e466/account/T`:
`--trade-quit-peer-account-file <copia TESTE> --delete-slot 4 --trade-quit-idle`
e `--trade-quit-negative-hp --trade-quit-pk`. Ator copia TESTE2, slot4 item413/10,
store JSON vazio; sem ItemList/mapas/skills/MariaDB/status. Ambos exit0/PASS,
carry/Gold completos duraveis/relogin preservados. Listener encerrado via stdin.

Isto e regressao bilateral/idle/HP, NAO smoke de loja autotrade: abertura real,
CreateMob normal apos fechar loja e lifecycle pela rede continuam T6 pendentes.
Nenhum deploy, alteracao de originais, commit/push ou E2E visual.

## QuitTrade sem troca ativa (26/09/2026)

`--trade-quit-idle` implica PK e exige loopback/copias isoladas, sem combinar
com modos mortos. Fixture `E2E7670/idlequit-20260926/account/T/{TESTE,TESTE2}`,
ator TESTE2 com item 413/10 slot4; sem tabelas/mapas/MariaDB. Depois do fechamento
bilateral normal, envia dois QuitTrade adicionais. Cada um exige sinal proprio
`0x0384`/12, PK `0x0366`/16 com subject do autor nos dois streams e checksum;
peer nao pode receber cancelamento. Barreiras nos dois lados recusam sinais/PK
extras. Passou PK 0 e PK 1 (`--trade-quit-pk-active`), carry/Gold completos dos
dois conferidos nos blobs antes do logout e no relogin.

Dominio: plano NoActiveTrade tem OpponentId0, PK calculado e recipients 1/2/3,
sem jogador fora de alcance; MOB intacto e HP0/-1 sem plano mesmo sem par.
Controle negativo dominio falhou "omitted the legacy refresh plan"; runtime
`WydCdk.Server-v7670-20260926-trade-pk` passou fechamento inicial, mas expirou
aguardando sinal proprio no primeiro reenvio. Novo runtime passou, assim como
regressoes HP0/-1. Suite 220 PASS/0 FAIL e build sem erros/avisos.

7.69 e W2PP: RemoveTrade envia 900 mesmo sem par em USER_PLAY; QuitTrade faz
GridMulticast PK depois. C# mantem NoActiveTrade como resultado diagnostico,
com plano nao-nulo; host nunca envia a conexao 0. Reset/visual de autotrade e
ISTradTimer nao foram portados neste bloco. Mencoes historicas de "sem troca
ativa sem refresh" abaixo ficam substituidas para o caminho bilateral/idle.

Deploy sandbox concluido: pacote `WydCdk.Server-v7670-20260926-idle-quit.zip`,
SHA-256 `9C0E54C9D9C36C35785E689E7278D1225366CD8F334FCC043D5A6136DC3FCD8B`.
Backup `C:\WYDCDK\Server\test-releases\20260926-before-idle-quit`.
World DLL `0F3F7716A272EFC3E41EF608C7EE4D0F9F7BEAEC9273D3125FFB22ACCFA6025A`;
Core DLL `9F7786CF08C052407986A74236F19A9DED13EA3EE0ED68FE888AFD9537D29300`.
Hashes remotos iguais aos locais; UP/PVP Running, portas8281/8282 Listen no
executavel esperado. Originais intactos; listener local encerrado.
Sem commit/push, source remoto, Site ou producao.

## QuitTrade com HP negativo (26/09/2026)

`--trade-quit-negative-hp` implica modo morto e semeia HP -1 somente na copia
isolada do ator. Requer loopback, peer distinto e `--delete-slot` como verificacao
de carry; nao pode ser combinado com `--trade-quit-killed`. Fixture usada:
`E2E7670/negativehp-20260926/account/T/{TESTE,TESTE2}`, ator TESTE2 com item
413/10 no slot 4. MOB W2PP: +216, stride 816, CurrentScore +92, HP +24.

Com `--trade-quit-pk`, aceite executado em 127.0.0.1:18283: login preservou HP -1;
oferta inbound aceita; QuitTrade retornou `0x0292`/20, HP -1/modo22/ID do ator e
checksum valido. Barreiras pos-QuitTrade dos dois streams nao receberam PK ou
cancelamento. O vivo fechou o mesmo par, ambos receberam cancelamento e PK 0
com subject ID do vivo. Carry/Gold completos dos dois iguais nos blobs antes
do logout e no relogin. Sem combate, ItemList, mapas ou MariaDB nesse host.

Controle negativo: `artifacts/publish-20260925-quittrade` (antes do gate) recebeu
os mesmos frames e cancelou indevidamente: harness exit1, "Dead QuitTrade emitted
a bilateral cancellation signal". Atual `WydCdk.Server-v7670-20260926-trade-pk`
passou exit0. Regressao `--trade-quit-dead --trade-quit-pk` HP0 passou.
Suite 220 PASS/0 FAIL, build sem erros/avisos. Guardas de host externo, originais
e modos negativos/killed incompatíveis verificadas antes da escrita.
Hashes originais intactos, listener encerrado cooperativamente. Somente
simulador/documentacao alterados; sem novo runtime, deploy, commit ou push.

7.69 e W2PP usam `CurrentScore.Hp <= 0` antes de RemoveTrade; nao ha divergencia
nesse gate. As mencoes historicas de HP negativo pendente abaixo sao substituidas
por este smoke; AddCrackError, ISTradTimer e E2E visual continuam pendentes.

## Refresh PK no fechamento aceito (26/09/2026)

`--trade-quit-pk` exige loopback e copias isoladas, junto do modo de fechamento
bilateral. Recebe cancelamento e depois `0x0366`/16, checksum valido, subject ID
do autor e estado 0 nos dois streams. Passou no fechamento vivo e combinado com
`--trade-quit-killed`; o ramo morto preserva o par sem cancelamento/PK.
Controle negativo antes da implementacao recebeu os cancelamentos, mas expirou
aguardando PK; o runtime novo passou. Suite completa: 220 PASS, build limpa.

`LegacyTradePkRules` cobre 20 vetores de guilty/PKMode/Newbie/Castle/Tower e
limites inclusivos. WorldHub testa recipients autor/peer/borda +16, exclusao +17,
guilty e rejeicao morto/sem troca. O retangulo RvR adicional W2PP nao pertence
ao alvo 7.69. Sem troca ativa permanece sem refresh, divergencia conhecida.
Estado 1 por guilty agora foi exercitado; exclusao do distante continua pendente
na rede, assim como estado 1 por PKMode/Newbie/Castle/Tower.

`--trade-quit-pk-active` implica `--trade-quit-pk` e grava guilty=8 somente na
copia isolada do autor do fechamento (peer no modo morto), antes do login.
W2PP: primeiro MOB +216, stride 816, carry +268, KILL_MARK slot 63, byte +4.
Referencias `_MSG_QuitTrade.cpp` 7.69/W2PP concordam nesse gate; RvR segue excluido.
Fixtures: `E2E7670/pkactive-20260926/account/T/{TESTE,TESTE2}`; item 413/10 slot 4
semeado somente em TESTE2. Host publicado anteriormente rodou localmente em
127.0.0.1:18283, sem mapas/tabelas de combate/itens ou MariaDB.

Aceite: fechamento vivo e `--trade-quit-dead` receberam PK estado 1/subject do
autor/checksum valido nos dois streams. Morto recebe SetHpMode HP0/modo22,
sem cancelamento/PK depois da barreira pre-QuitTrade, e o vivo fecha o mesmo par.
Carry/Gold completos dos dois conferidos nos blobs antes do logout e no relogin.
Regressao estado 0 passou em `E2E7670/pkzero-20260926`. Controle negativo do
assert antigo de estado 0 recusou o estado ativo; nao foi defeito do servidor.
O cenario morto reproduziu falso positivo no harness: PK inicial do peer ainda
enfileirado. Barreiras de chat nos dois streams antes do QuitTrade drenam esses
relays; o gate posterior continua proibindo PK/cancelamento. Reteste passou.
Host externo e fixtures originais recusados antes de semeadura; SHA-256 dos
originais TESTE/TESTE2 iguais aos anteriores. Somente testes/MDs, sem novo deploy.

Publicacao sandbox: pacote `WydCdk.Server-v7670-20260926-trade-pk.zip`, SHA-256
`19CB4078DA63CD918FFFC231AFACDDE4D0F3CA037CBB36E59F65663A057AAC7F`.
Backup remoto `C:\WYDCDK\Server\test-releases\20260926-before-trade-pk`.
World DLL: `FA1364EBD751CBA61AD29FA1CBFEDE5DDD517463D1A5F2C25F9E2C3035351F5E`;
Core DLL: `0EAC958A30E278DC437F544DC434D0FB54C328BE34DCE3B7DAEBF4D81533E1C1`.
Hashes remotos conferidos; tarefas UP/PVP Running e portas 8281/8282 Listen no
executavel esperado. Sem Site, producao, commit ou push.

As secoes anteriores abaixo registram o escopo historico antes deste refresh;
suas mencoes de PK pendente ficam substituidas somente para fechamento aceito.

## Morte durante trade bilateral (26/09/2026)

`--trade-quit-killed --trade-quit-peer-account-file <copia/TESTE> --delete-slot 4`
com ator `<copia/TESTE2>` usa as mesmas guardas de loopback/copias do modo morto.
Fixtures em `Backup/Build/E2E7670/killedtrade-20260926`. Antes do login, o ator
recebe HP 1 e o par Damage 10000/bit LearnedSkill 31, somente nos MOBs copiados.
Os dois entram vivos; o ator oferta item 413/10 do slot 4 e o par oferta 1 Gold,
ambos sem check. Nenhuma compra ou transferencia e concluida.

O host carrega o CSV real de
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv`.
O harness envia `MSG_AttackOne` `0x039D`/68, skill 103 e damage slot `-2`:
e intencao de ataque fisico, nao dano informado pelo cliente. 7.69 e W2PP
possuem o branch `dam == -2` em `_MSG_Attack.cpp`. O C# valida metadata,
skill aprendida e timing, calcula dano a partir do estado autoritativo e
envia `SetHpMp` `0x0181`/28 com HP 0 ao alvo. Logs confirmaram resolucao fisica.
Nao ha ItemList/mapas/NPCs/MariaDB/status externo nesse smoke; nao representa
um teste completo de PvP ou equivalencia integral do combate C++.

Depois da morte, QuitTrade retorna `SetHpMode` HP 0/modo 22; barreiras nos dois
streams confirmam ausencia de cancelamento. O par vivo fecha a mesma troca e
recebe sinal bilateral, provando o estado staged preservado. Carry completo e
Gold dos dois conferidos nos blobs antes do logout e no relogin.
`CLIENT_E2E_PASS killed-during-trade hp-mode pair-preserved carry-and-gold-unchanged`.
O mesmo teste no pacote anterior ao gate HP falhou exit 1 ao receber `0x0384`
indevido, depois de confirmar a morte pelo ataque; runtime atual passou exit 0.
Guardas de host externo e arquivos originais passaram, com SHA-256 dos dois
originais inalterados. Build zero avisos/erros, suite 218 PASS. Listeners
encerrados; somente harness/MDs alterados, sem novo deploy/commit/push/Site.
Nao cobre HP negativo, AddCrackError, refresh PK, concorrencia de checks/commit,
ressurreicao automatica, MariaDB ou E2E visual.

## QuitTrade com HP zero (26/09/2026)

Modo `--trade-quit-dead --trade-quit-peer-account-file <copia/TESTE> --delete-slot 4`,
com ator `<copia/TESTE2>`, listener `127.0.0.1:18283` e file store isolado.
Parse rejeita host nao-loopback, contas iguais e arquivos originais em
`E2E7670/account/T`; permite apenas `E2E7670/<copia>/account/T/TESTE[2]`.
O harness semeia HP zero no MOB/CurrentScore do slot escolhido, offset
`216 + slot*816 + 92 + 24`, antes do login. O item 413/10 fica no carry;
`--delete-slot` e somente seletor de retencao, nao envia DeleteItem.

O jogador vivo abre uma oferta sem check para o morto. O login confirma HP
zero; QuitTrade do morto retorna `0x0292`/20, HP 0, USER_PLAY 22, ID proprio e
checksum valido. Barreiras de chat nos dois streams rejeitam qualquer `0x0384`
antes do marcador. O QuitTrade posterior do vivo recebe cancelamento bilateral
`0x0384`/12, provando que o par anterior continuava ativo. Carry completo/Gold
dos dois blobs foram conferidos antes do logout e no relogin dos dois.
`CLIENT_E2E_PASS dead-quit-trade hp-mode pair-preserved carry-and-gold-unchanged`.

Fixtures: `Backup/Build/E2E7670/deadquit-20260926`. Controle negativo com
`Server/artifacts/WydCdk.Server-v7670-20260925-quittrade.zip` (SHA-256
`2FB939412AAAEF8FBA7FA317BD1BA836664E2794CE0A6EAE2DB6FAEA9A72355D`),
extraido fora da arvore ativa, falhou exit 1 ao receber `0x0384` em vez de
SetHpMode: `Dead QuitTrade emitted a bilateral cancellation signal`.
Runtime atual passou novamente exit 0; caminho vivo tambem passou.
Build SDK 10.0.401, zero avisos/erros, suite 218 PASS. Listeners locais
encerrados; originais intactos. Somente harness/MDs, sem novo deploy/commit/push.

7.69/W2PP coincidem no gate HP <= 0; o alvo tambem reseta ISTradTimer no caminho
vivo. Este modo nao cobre morte causada durante troca ja bilateral (coberta
pelo modo killed acima), HP negativo,
AddCrackError, refresh de PK, MariaDB ou E2E visual.

## Replay de troca somente Gold: reproduzido e corrigido (26/09/2026)

O modo `--trade-replay-peer-account-file ... --trade-gold-only` nao usa
`--delete-slot`, nao oferta itens e mantem as guardas de loopback/fixtures.
Copias usadas: `Backup\Build\E2E7670\gold-replay-f41e8dadc48f43e5886781082607d8bd`.
Listener `127.0.0.1:18283`, backend de arquivos, sem tabelas de combate/itens,
MariaDB ou status externo. Contas originais nao foram alteradas.

Primeiro commit: carry intacto e saldos TESTE2/TESTE de 5000001/4999999,
confirmados em UpdateCarry e nos blobs antes do logout. Reenvios dos checks
originais em ordem controlada produziram um segundo commit duravel:
5000002/4999998. Relogin confirmou esse desvio e o teste terminou exit 1,
`Completed-trade replay changed actor carry or Gold beyond one transfer`.
Isso e repeticao de transferencia, nao criacao de Gold: a soma foi conservada.

Causa no C#: TryStageTradeOffer faz SetOffer para MyCheck=1 sem exigir uma
oferta previa do proprio lado; a comparacao SameTradeOffer so roda se o lado
ja tinha MyCheck=1. Checks antigos conseguem reconstruir uma troca apos
ClearTradePairLocked. Nas referencias 7.69/W2PP, o branch MyCheck=1 compara
MSG_Trade contra a oferta guardada (memcmp) antes de concluir; nao houve
execucao dos servidores C++ e nao se afirma equivalencia do bug neles.

Correcao implementada: check exige oferta propria e par
previamente staged; dados de item/posicao/Gold devem coincidir antes de mutar
o estado. Check nao abre nova troca. SameTradeOffer permite a transicao 0->1
do check, mas nao mudanca de itens/posicoes/Gold/alvo. Wire inalterado.
Teste de dominio observado vermelho antes do fix; suite atual 218 PASS.
E2E em `gold-fix-260318ae24284774ac313c17891ba7c4` recusou os tres checks
antigos (`CheckWithoutOffer`), preservou o primeiro debito/credito e aceitou
segunda troca intencional na mesma sessao. Barreiras por chat no mesmo TCP
confirmam processamento ordenado; nao reutilizam PIN nem alteram autenticacao.
UpdateCarry completo, blobs antes do logout e relogin final conferidos:
`CLIENT_E2E_PASS gold-only-replay blocked new-trade-accepted persisted-both`.
Regressao item/Gold passou no mesmo runtime: ItemChanged no ator e
CheckWithoutOffer no par, sem reabrir troca. Listener encerrado, originais
intactos. Fix publicado somente na VPS de teste; sem commit/push/Site.
Build self-contained win-x64, SDK 10.0.401; pacote
`Server/artifacts/WydCdk.Server-v7670-20260926-trade-check.zip`, SHA-256
`F8398B58CC3304C53012BD79C9734EE53C3E3764655952DC863AFFC4FBE232D1`.
Runtime `C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`;
backup preservado em `test-releases/20260926-before-trade-check`.
UP/PVP Running, 8281/8282 Listen, processos identificados pelo caminho do runtime.
Hashes locais/remotos: World
`93BFAD8DCD1644C48B2177D575F4300142406E0741897E70FB739664C9671B56`, Core
`4E3B49FB521932C4AD195EB7A88F08D102775E005102683AE124F88E49163568`.
Isso verifica publicacao/operacao, nao executa o E2E de trade na VPS.
Nao cobre replay de toda a sequencia de ofertas sem check, concorrencia entre
commits, crash/recovery, MariaDB ou E2E visual; nao e anti-replay global.

## Troca concluida e replay (26/09/2026)

Modo `--trade-replay-peer-account-file`, restrito a loopback e fixtures distintas
`E2E7670/.../TESTE` e `TESTE2`. `--delete-slot` seleciona o item ofertado; nao
envia delete neste modo. Preparar copias isoladas das contas, com item 413/10
no slot 4 do TESTE2, espaco no TESTE e pelo menos 1 Gold no TESTE.

```powershell
dotnet run --project .\Server\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-build --no-restore -- `
  --port 18283 --account-file <E2E7670\copia\account\T\TESTE2> `
  --trade-replay-peer-account-file <E2E7670\copia\account\T\TESTE> --delete-slot 4
```

Execucao contra `127.0.0.1:18283`, file store isolado: duas ofertas sem check,
check do ator (`0x0186`/12), check do par e `UpdateCarry` (`0x0185`/528) para
ambos, com checksum e ID corretos. Carry completo e Gold conferidos contra
uma transferencia: item 413/10 do TESTE2 para TESTE, +1/-1 Gold.
Os dois blobs tambem foram lidos antes do logout: `TRADE_DURABLE_PASS` em
ambos, impedindo que o salvamento no logout esconda commit ausente.
Cada frame de check original foi reenviado duas vezes, preservando bytes,
tick, keyword e checksum. Logs confirmaram `ItemChanged` para o ator que ja
nao possuia o item; o check monetario do par reabriu oferta sem confirmacao,
mas nao concluiu nova troca. Logout/relogin confirmou os dois carries e saldos
exatos: `CLIENT_E2E_PASS completed-trade-replay single-item-and-gold-transfer
persisted-both`. Guarda de host externo rejeitada antes da conexao.

Build SDK 10.0.401: zero avisos/erros; suite deterministica 217 PASS.
As fixtures originais nao foram usadas para gravacao; copias isoladas ficam
em `Backup\Build\E2E7670\replay-b72971e3d5fc4561a3e5d4dbea048d4c`.
Listener encerrado. Nenhuma mudanca no runtime/deploy/cliente/Site.
Nao cobre troca apenas de Gold, concorrencia entre commits, crash/recovery,
MariaDB ou E2E visual, e nao demonstra anti-replay global.

## Pacotes alterados e repetidos (26/09/2026)

O modo `--adversarial-peer-account-file` exige loopback e duas fixtures distintas
`E2E7670/.../TESTE` ou `TESTE2`. Host externo e rejeitado antes de conectar.
Com o listener local e item 413 semeado no slot 4 do TESTE2:

```powershell
dotnet run --project .\Server\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-build --no-restore -- `
  --account-file .\Backup\Build\E2E7670\account\T\TESTE2 `
  --adversarial-peer-account-file .\Backup\Build\E2E7670\account\T\TESTE `
  --delete-slot 4
```

Foram enviados 22 pacotes com checksum valido: cinco variantes de DeleteItem
(slots -1/60, indices -1/6500 e indice divergente) e seis de TradeOffer
(alvo proprio/ausente, Gold negativo/excessivo, item alterado e slot duplicado),
cada uma duas vezes. Os logs confirmaram rejeicao de todas as variantes.
O harness compara carry completo e Gold apos relogin; os estados das duas
contas foram preservados. Duas ofertas validas sem check foram retransmitidas,
sem concluir troca. O primeiro QuitTrade fechou bilateralmente; o replay foi
ignorado como `NoActiveTrade`. Resultado: `CLIENT_E2E_PASS
adversarial-delete-trade-replay carry-and-gold-unchanged`.
O item semeado foi removido pelo delete normal e o relogin confirmou slot vazio.
A suite deterministica manteve 217 PASS.

Este recorte nao prova protecao global contra replay/cheats nem exercita
repeticao de troca concluida. Somente harness/documentacao mudaram;
nenhum runtime foi republicado neste bloco.

## Smoke do delete de item (25/09/2026)

O caso de trade usa duas conexoes locais simultaneas e duas contas descartaveis:
`--account-file ...\E2E7670\account\T\TESTE2 --trade-delete-peer-account-file
...\E2E7670\account\T\TESTE --delete-slot 4`. Antes, semear o slot 4 do
`TESTE2`; depois, limpar pelo delete normal. O harness confirma a oferta
`0x0383` recebida pelo par, o sinal `0x0384`/12 com checksum e ID proprio em
ambas as conexoes, e o item preservado apos relogin. Na execucao de
25/09/2026, esse fluxo passou; o slot de teste foi limpo em seguida.

Para o fechamento voluntario, usar as mesmas duas contas e o item semeado,
trocando o argumento do par por `--trade-quit-peer-account-file ...\TESTE`;
`--delete-slot 4` nessa modalidade apenas indica qual item deve continuar
presente apos relogin, nao envia delete. O smoke de 25/09/2026 confirmou
`MSG_QuitTrade` e `0x0384`/12 com checksum/ID proprio nos dois clientes,
sem mudar o item. A fixture foi limpa com o delete normal depois.

O gate de HP <= 0 no `MSG_QuitTrade` foi testado no dominio: preserva a troca
bilateral e o carry; o host foi ligado para responder `MSG_SetHpMode`. O smoke
de rede historico acima valida somente o caminho vivo. Em 26/09 os modos
dead/killed acima validaram HP zero e morte durante troca por combate real,
sem atalho de debug no host; AddCrackError e refresh PK continuam pendentes.

Em conta descartavel `TESTE2` na pasta `E2E7670`, preparar o slot 4 com
`--seed-stack-slot 4`. Com o listener UP local ativo, `--delete-slot 4`
envia `0x02E4`/20 e confirma o carry vazio apos logout/relogin. Para o caminho
bloqueado, executar `--set-account-blocked true` separadamente, depois
`--delete-slot 4 --expect-delete-blocked`; o harness exige o aviso
`0x0101`/140 com checksum valido e o item preservado apos relogin. Restaurar
com `--set-account-blocked false` e remover o item de teste com o delete normal.
O comando de bloqueio possui guarda para a fixture `E2E7670/.../TESTE2`.

Aceite observado: o caso bloqueado recebeu `0x0101`/140 e reteve o item 413
no slot 4 apos relogin; o caso desbloqueado removeu o item e o relogin
confirmou o slot vazio. A fixture voltou ao estado desbloqueado.

`WydCdk.Client.E2E` simula somente o transporte do cliente 7.69/7.670. Ele
reutiliza `LegacyFrameCodec` do projeto, envia o handshake, autentica uma conta
de sandbox, verifica o PIN persistido, solicita login do personagem e faz logout. O harness não abre o
`WYD.exe` e não substitui o E2E visual; seu objetivo é separar falhas do server,
do wire e da composição visual.

O `--account-file` deve apontar para uma conta local de teste já criada. O
programa lê os 28 bytes de cabeçalho dessa conta somente em memória para evitar
credencial na linha de comando; não use uma conta real nem versiona o arquivo.

Exemplo local:

```powershell
dotnet run --project .\Server\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-restore -- `
  --account-file .\Backup\Build\E2E7670\account\T\TESTE `
  --host 127.0.0.1 --port 8281 --slot 0
```

## Projeção auditada de CurrentScore no login (08/10/2026)

Execução headless local em `127.0.0.1:18283`, usando contas sintéticas
descartáveis, cópias de trabalho sob `Backup/Build/CurrentScoreLoginProjection-20261008`
e `Client/7670/ItemList.bin` somente leitura. O cenário normal valida o MOB na
confirmação 7.69, o `CreateMob` recebido por outro cliente, `UpdateEtc`,
`UpdateScore`, logout e MOB salvo, então reloga. O affect Type 35 do fixture
resultou em MaxHP 1150 e MaxMP 535 em ambos os logins; não aplicou o bônus duas
vezes. O hash do fixture fonte foi
`EE7BB8F3AC51E40D69E988348F49BDB412D5171FB9FBD1EF2C3C5CB7333EADF8`; o hash da
ItemList original foi
`8B2407F9055FE4FD92F29BDBD4A943559CC0C8711753B9FE67BE09C080ABB625`.
Resultado: `CLIENT_E2E_PASS current-score-login-projection`.

O cenário de rejeição usa uma cópia sintética da ItemList com valor de Critical
que não cabe no campo 7.69 e equipamento preparado para acioná-lo. O login não
recebe confirmação, o observer não recebe `CreateMob`, e o hash do arquivo da
conta rejeitada permanece
`43FFB353D077C22639C10110537A6C0094A0961A6B5C58C629CC23C16D01A0C3`. O hash da
ItemList alterada foi
`7C7BE16A7386EE15D09900EBE7B86CB8166E344426B2E7AA26EE89210752859B`.
Resultado: `CLIENT_E2E_PASS current-score-login-projection-reject no-login-or-spawn`.

Os comandos, saída integral, exit codes, hashes e SDK estão nos logs
`Backup/Build/CurrentScoreLoginProjection-20261008`. Este harness verifica
wire/login em loopback; não abre `WYD.exe` e não comprova E2E visual. A
idempotência observada no relogin vale para o Type 35 do cenário. O loop 7.69
persiste Magic de Type 34 e Resist em personagens de nível >= 2000 de modo que
podem servir de nova entrada no relogin; essa limitação de source está detalhada
em `PORT_TRACEABILITY.md` e permanece sem solução de persistência neste recorte.

Execução final, a partir de `Server` (cada comando de Host fica ativo em um
terminal até o smoke terminar; os processos foram interrompidos depois das
assertions):

```powershell
dotnet run --no-build --project .\src\WydCdk.Server\WydCdk.Server.csproj -- `
  --bind 127.0.0.1 --port 18283 `
  --accounts-root .\Backup\Build\CurrentScoreLoginProjection-20261008\final-normal-01\account `
  --account-review-root .\Backup\Build\CurrentScoreLoginProjection-20261008\final-normal-01\review `
  --item-data ..\Client\7670\ItemList.bin

dotnet run --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj `
  --no-build --no-restore -- `
  --host 127.0.0.1 --port 18283 `
  --account-file .\Backup\Build\CurrentScoreLoginProjection-20261008\final-normal-01\account\S\SCORETEST `
  --current-score-login-projection-smoke
```

Para rejeição, usar `final-reject-01\account`, `final-reject-01\review` e
`final-reject-01\ItemList.bin` no comando do Host, e executar:

```powershell
dotnet run --project .\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj `
  --no-build --no-restore -- `
  --host 127.0.0.1 --port 18283 `
  --account-file .\Backup\Build\CurrentScoreLoginProjection-20261008\final-reject-01\account\S\SCORETEST `
  --current-score-login-projection-reject-smoke
```

Resultado final: Engine.Tests 405/405; `WydCdk.Engine.slnx --no-incremental`
com 0 erros e 1 CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build do harness E2E com 0 avisos/erros;
ambos os smokes com exit code 0. Os logs completos e `dotnet --info` estão em
`Backup/Build/CurrentScoreLoginProjection-20261008`. Os logs de Host terminam
com exit code 1 porque as sessões foram interrompidas após os smokes; os dois
harnesses clientes terminaram com exit code 0 e as portas foram liberadas.

Reexecucao em 26/10/2026 apos MOB-AI-001 Task 5EA: Engine.Tests `531/531`, build
solution sem incremental com 0 avisos/erros e `CLIENT_E2E_PASS
current-score-login-projection` em `127.0.0.1:18283`. O smoke usou uma copia
descartavel `final-normal-02` dos fixtures; o Host foi interrompido apos o
harness cliente concluir com exit code 0 e a porta foi liberada. Esta execucao
mantem o cenario geral Type 35; a nova passiva Escudo do Tormento (bit 19) e
verificada pelo teste direto `CurrentScoreLoginProjectionTests`, nao por este
fixture de rede.

# GuildWar control loopback smoke

Run from `Server` after building the solution, with the local 7.69 `ItemList.bin`:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-restore -- `
  --guild-war-smoke --item-data ..\Client\7670\ItemList.bin
```

The harness creates two synthetic accounts/characters and generated guild data
under a unique `%TEMP%` root, starts the C# Host on `127.0.0.1` at a free port,
and cleans only its verified temporary directory after stopping the child
process. It does not read `TESTE`, `TESTE2`, production accounts, or connect to
an external database/endpoint. The scenario checks a spoofed guild request
without state mutation, a one-sided declaration, reciprocal acceptance, the
global start notice, and `SendWarInfo` including `GuildZone[4].Clan`.

Observed on 14/10/2026: two loopback runs passed with
`GUILD_WAR_ONE_SIDED_DECLARATION_PASS`,
`GUILD_WAR_RECIPROCAL_ACCEPTANCE_PASS`, and
`GUILD_WAR_LOOPBACK_SMOKE_PASS`. This is headless TCP evidence; it does not
prove that the stock/compiled client emits `MSG_War` or displays the notices.
GuildWar state is volatile and local to one Host process.

The Host relay-order regression can be run separately:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-restore -- --self-test-guild-war-relay
```

Observed after the concurrency fix: `GUILD_WAR_RELAY_ORDER_PASS`. The test
holds the first asynchronous relay open and verifies the next transition waits
until that batch finishes.

## Smoke do split de stack

O harness aceita `--split-slot` e `--split-quantity`. Para preparar uma conta
de sandbox descartavel com o item stackavel 413/quantidade 10, use o modo
explicito `--seed-stack-slot`; ele so escreve se o slot de carry estiver vazio:

```powershell
dotnet run --project .\Server\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-restore -- `
  --account-file .\Backup\Build\E2E7670\account\T\TESTE2 `
  --seed-stack-slot 4
```

Com o listener UP local ativo, o cenario de aceitacao e:

```powershell
dotnet run --project .\Server\tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj --no-restore -- `
  --account-file .\Backup\Build\E2E7670\account\T\TESTE2 `
  --split-slot 4 --split-quantity 3
```

Aceite observado em 25/09/2026: `0x02E5`/24 foi aceito; o servidor enviou dois
`0x0182`/24 com checksum valido, destino no slot 5 antes da origem no slot 4;
depois do logout e novo login, o carry voltou com quantidade 7 na origem e 3 no
destino. O modo seed altera somente a conta local de sandbox; nunca use uma
conta real ou publique esse arquivo.

Aceite: `LOGIN_RX` 0x010A/1928, `SECURE_RX` 0x0FDE/12, `CHAR_RX` 0x0114/1728 e `LOGOUT_RX`
0x0116/12, todos com checksum válido, seguidos de `CLIENT_E2E_PASS`.

## MOB-AI-001 Task 5CD - player Type 22 countdown smoke (23/10/2026)

Execute from `Server`:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-restore -- --mob-ai-smoke
```

The three-peer loopback scenario initializes a player Type 22 record with a
two-second timer. Its first matching shard decrements silently; the second
clears all eight bytes and sends only `UpdateScore` to the actor and in-view
peer. The outside peer receives no frame. Marker:
`player-type22-expiry=score-only-relayed`.

This smoke verifies only the common timer countdown/expiry tail. It does not
exercise the Type 22 Skill 33 area attack; that dispatch is covered separately
by Task 5CF below.

## MOB-AI-001 Task 5CE - generic player affect timer smoke (23/10/2026)

The same three-peer loopback scenario applies Type 18, which has no specialized
`ProcessAffect` branch in either reference. The first timer visit decrements
silently; the second clears all eight bytes and sends only `UpdateScore` to the
actor and in-view peer. The outside peer receives no frame. Marker:
`player-generic-affect-expiry=score-only-relayed`.

The WorldHub unit test also verifies the `unsigned int` behavior shared by
TMProject 7.69 and W2PP: a zero starting `Time` wraps to `UINT_MAX` rather than
expiring. This Type 18 smoke does not exercise area attacks; the separate
Type 22/23 dispatch scenario is documented below.

## MOB-AI-001 Task 5CF - player Type 22/23 area attack relay (23/10/2026)

Run from `Server`:

```powershell
dotnet run --no-restore --project tests\WydCdk.Client.E2E\WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

The loopback case runs once with player affect Type 22 / Skill 33 and once with
Type 23 / Skill 52. It checks a checksum-valid 0x0367 Attack with the expected
skill, target IDs, and positive server-calculated damage on the actor and one
in-view peer. One NPC target starts at 1 HP; its `RemoveMob(type=3)` follows the
Attack for both in-view peers. The outside peer receives no frame. Marker:
`player-type22-23-area-attack=view-scoped`.

The Engine.Tests case checks the 2x2 scan before the 5x5 scan and the six-target
limit. These cases use synthetic SkillData, NPC state, and TCP loopback; they do
not authenticate through the Host or launch the TMProject client. Allied guild
protection and Pista/Zard-specific death relays remain open until their C#
state/relay paths have been modeled.

## MOB-AI-001 Task 5CG - player timer Pista/Zard outcome relay (23/10/2026)

Run from `Server`:

```powershell
dotnet run --project tests\WydCdk.Client.E2E -- --mob-ai-smoke
```

The Type 22/23 cases now hit a Zard with the counter initialized to 79. The
attacker receives `_MSG_MobLeft=80` and rune 475; the in-view peer receives
neither player-specific frame. The same batch kills the configured final Pista
Lv6 mob, then sends `RemoveMob(type=3)` and the generated next-wave NPC to the
actor and in-view peer. The outside peer receives no frame. Marker:
`player-area-quest-effects=mobleft-reward-pista-spawn-relayed`.

The Pista smoke uses C#'s current W2PP-style IDs `5775..5785 -> 5767`; TMProject
7.69 defines `4291..4301` and boss `4283`. This proves the relay of the current
transition only; it does not establish Pista 7.69 behavioral parity. Pista
teleport and reset/removal variants are implemented by the same relay but are
not separately exercised here. The smoke is synthetic loopback: no Host login
or visual TMProject client E2E.

## MOB-AI-001 Task 5CQ - non-generated NPC Life Aura smoke (23/10/2026)

Run from `Server`:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

The three-peer loopback case gives an ordinary persistent NPC a Type 17 Life
Aura affect with a one-tick duration. Its Peace shard applies the source heal,
clamps HP, and expires the record in the same visit. Both in-view peers receive
the 7.69 `SetHpDam` frame followed by `UpdateScore`; the outside peer receives
no frame. Marker:
`NON_GENERATED_NPC_TYPE17_SMOKE_PASS hp-score=true view-scoped=true`.

Engine.Tests separately covers Peace max-HP clamping and expiry plus the
four-opportunity Combat cadence without a target. This smoke uses synthetic
WorldHub state and TCP peers; it does not authenticate through the Host or
launch the TMProject client.

## MOB-AI-001 Task 5CR - non-generated NPC Poison smoke (23/10/2026)

Run from `Server`:

```powershell
dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke
```

The three-peer loopback case gives an ordinary persistent NPC a Type 20 Poison
affect with a one-tick duration. Its Peace shard applies the 7.69/W2PP mob
damage cap, decrements the timer, and expires the record in the same visit. Both
in-view peers receive `SetHpDam` with the signed HP delta followed by
`UpdateScore`; the outside peer receives no frame. Marker:
`NON_GENERATED_NPC_TYPE20_SMOKE_PASS hp-score=true view-scoped=true`.

Engine.Tests separately covers the LearnedSkill-derived damage cap and the
four-opportunity Combat cadence without a target. This smoke uses synthetic
WorldHub state and TCP peers; it does not authenticate through the Host or
launch the TMProject client.

## MOB-AI-001 Task 5CS - non-generated NPC Type 32 expiry (23/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case gives an ordinary persistent NPC a Type 32 affect
with a two-tick duration. Its Peace shard decrements the timer; the next visit
expires it, clears the affect, and relays UpdateScore only to the two peers in
the captured view. The outside peer receives no frame. Marker:
`NON_GENERATED_NPC_TYPE32_SMOKE_PASS score-only=true view-scoped=true`.

The same smoke also attacks an ordinary NPC at the first legacy NPC index
1000 with skill 47 and no Type 19 to remove. Its Type 32 affect appears in a
valid 7.69 UpdateScore relayed to the actor and a peer on the inclusive NPC
view boundary; a peer outside the view receives no frame. Marker:
`CANCELAMENTO_NPC_TYPE32_SMOKE_PASS score-view-scoped=true`. The harness uses
synthetic WorldHub state and local TCP peers, without authenticated Host login
or a visual client.

Engine.Tests separately covers Peace and targetless Combat's fourth-opportunity
cadence, unchanged HP, no attack, and score-only expiry. This smoke uses
synthetic WorldHub state and local TCP peers; it does not authenticate through
the Host or launch the TMProject client.

## MOB-AI-001 Task 5CU - Enfraquecer Type 10 NPC score relay (23/10/2026)

Run from `Server` after building the solution with the existing `--mob-ai-smoke`
command above. The three-peer loopback case uses skill 51 against an NPC at the
first legacy NPC index 1000. It checks that Type 10 reduces the NPC's transmitted
CurrentScore damage by `Level / 5 + Value`, both in-view peers receive a valid
UpdateScore, and the outside peer receives no frame. Marker:
`ENFRAQUECER_NPC_TYPE10_SMOKE_PASS damage-view-scoped=true`.

Engine.Tests covers finite expiry and damage restoration for both generated and
non-generated NPCs. The smoke uses synthetic WorldHub state and local TCP peers;
it does not authenticate through the Host or launch the TMProject client.

## MOB-AI-001 Task 5CV - Perseguicao Type 3 NPC score relay (23/10/2026)

Run from `Server` after building the solution with `--mob-ai-smoke`. The
three-peer loopback case uses skill 16 against an NPC at the first legacy NPC
index 1000. It checks the Type 3 resistance projection from head item index 50,
valid UpdateScore frames to both peers in the NPC view, and no frame to the
outside peer. Marker: `PERSEGUICAO_NPC_TYPE3_SMOKE_PASS
resist-view-scoped=true`.

Engine.Tests covers the four resistance fields, head item boundary, target
gates, source resistance roll, recast duration, and timer expiry/restoration in
generated and non-generated NPCs. The smoke uses synthetic WorldHub state and
local TCP peers; it does not authenticate through the Host or launch the
TMProject client.

## MOB-AI-001 Task 5CY - Lamina Congelante Type 7 score relay (25/10/2026)

Run from `Server` after a solution build with `--mob-ai-smoke`. The three-peer
loopback case applies skill 19 to NPC index 1000. It verifies that both peers in
the NPC view receive a valid Type 7 UpdateScore with AttackRun `0x96` and
Intelligence 75, while the outside peer receives no frame. Marker:
`LAMINA_CONGELANTE_NPC_TYPE7_SMOKE_PASS attackrun-view-scoped=true`.

Engine.Tests separately covers the source target gates, the resistance roll,
NPC 1001 exclusion, the head item threshold, and Type 7 expiry on generated and
non-generated NPCs. The harness uses synthetic WorldHub state and local TCP
peers; it does not authenticate through the Host or launch the TMProject client.

## MOB-AI-001 Task 5CZ - Type 20 tick from damage skills (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case applies skill 21 (`Punhalada_Venenosa`) to NPC
index 1001. It verifies the internal Type 20 affect has value 10, level 50,
and one tick remaining, then relays a valid `UpdateScore` to the actor and
observer in the NPC view. The wire affect word is `0x1401` (Type 20, one tick).
The peer outside the view receives no frame. Marker:
`POISON_TICK_NPC_TYPE20_SMOKE_PASS view-scoped=true`.

Engine.Tests covers all three active damage skills with Type 20 (`20`, `21`,
and `40`), their tick fields, the source target gates, and the SetTick Merchant
boundary. This smoke uses synthetic WorldHub state and TCP loopback peers; it
does not authenticate through the Host or launch the TMProject client.

## MOB-AI-001 Task 5DD - Type 1 score for offensive player skills (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case applies Lanca de Gelo (skill 34) to a player
target. It verifies that the target receives valid Type 1 `UpdateScore` and
`UpdateAffect` frames with AttackRun `0x64` and Intelligence 60, while the
attacker and outside peer receive no frame. Marker:
`LANCA_DE_GELO_PLAYER_TYPE1_SMOKE_PASS score-affect-target-only=true`.

Engine.Tests covers the player Type 1 score transition for skills 1, 23, 34,
and 36, including recast and timer expiry. This smoke uses synthetic WorldHub
state and TCP loopback peers; it does not authenticate through the Host or
launch the TMProject client.

## MOB-AI-001 Task 5DE - Perseguicao Type 3 score for player targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case applies Perseguicao (skill 16) to a player target.
It verifies the Type 3 resistance penalty in the target's UpdateScore and the
matching UpdateAffect frame, while preserving the zero-damage effect-only
behavior. The attacker and peer outside the target view receive no frame.
Marker: `PERSEGUICAO_PLAYER_TYPE3_SMOKE_PASS score-affect-target-only=true`.

Engine.Tests separately covers score projection, four-resistance saturation,
recast, timer expiry, Detox, and baseline resistance persistence across logout
and login. The harness uses synthetic WorldHub state and local TCP peers; it
does not authenticate through the Host or launch the TMProject client.

## MOB-AI-001 Task 5DM - Protecao Elemental Type 25 score for player self-targets (26/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer TCP loopback case applies Protecao Elemental (skill 53) to its
caster. It checks the 7.69 Type 25 AC and four-resistance projection, preserved
current HP/MP, and the Type 25 `UpdateScore` followed by `UpdateAffect`. Only the
caster stream receives those frames; the in-view observer and outside peer
receive no frame. Marker:

```text
PROTECAO_ELEMENTAL_PLAYER_TYPE25_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests separately covers the source-derived 24-tick duration, recast
without score stacking, and expiry restoration. This smoke uses synthetic
WorldHub state and local TCP peers; it does not authenticate through the Host
or launch the TMProject client.

### Authenticated Host and visual-client follow-up (26/10/2026)

The real Host loopback runner passed with a generated legacy-account fixture:

```text
HOST_AUTHENTICATED_TYPE25_PASS auth=account-file host=127.0.0.1 skill=53 score-before-affect=true generated-fixture=true external-database=none
```

It authenticated the account, secure code, and character through the Host, then
verified the skill 53 Type 25 `UpdateScore` before `UpdateAffect`. The run used
the generated fixture and local account-file mode, with no external database.
The full build after the `CS8604` test-only null-flow fix completed with 0
warnings and 0 errors; Engine.Tests passed `515/515`.

The visual fixture sets `Equip[0].sIndex=32`, matching class 2 as derived by
the client. The first run exposed two fixture/data issues. A saved start at
(2000,2000) selected Field 15,15, whose `env` files are absent; the disposable
account's normal disconnect save moved it to Field 16,16, which exists in the
client copy. Separately, the supplied `SkillData.bin` was 25,796 bytes
(248x104 plus a four-byte trailer), while the client source reads 248
`STRUCT_SPELL` records of 96 bytes (23,808 bytes). That stride mismatch made
the parsed `MaxTarget` for skill 53 zero and the first attack packet had no
target.

The test repacked the first 96 bytes of each original 104-byte row, retaining
the source's XOR-encoded bytes. The corrected file is 23,808 bytes with SHA-256
`5239C6C702B76E85BEEB3F71374D0D75D9852E408AE70729D05800D94E32EFB1`; it was
copied to `Client/7670/SkillData.bin` and the four `Runtime/Interface*` copies.
The original 104-byte file, SHA-256
`9A294F0C1194BB3D0342F7F6146ACBAAFE50C07F6C7D5AB053F095936FF74D5C`, remains
preserved under `Backup/Build/Type25AuthE2E-20261026-05/`.

The isolated client
`Backup/Build/Client7670-Loopback-Smoke-20260930/WYD.exe` (SHA-256
`51BE30D3E55231D300ABD7BD41E046690DD43EE6A4DAF5F9CD9AF4A92EFF03E2`)
authenticated to the local account-file Host and entered the world as
`PROTECT25` (`BeastMaster`) without the critical-data message. Selecting
hotbar slot 1 and right-clicking the world sent `0x039D` (72 bytes), skill 53,
with self-target ID 1. The Host resolved the attack, consumed MP to 869, and
sent `0x0336` with AC 135, HP 1000, MP 869, resistances 2/2/2/2, and Type 25;
`0x03B9` carried Type 25, Level 50, Value 10, and 24 ticks. The client status
panel showed the same score. The E2E used the isolated binary above; the active
`Client/7670/WYD.exe` has a different, unverified hash and was not exercised.
Host and account fixture were local/loopback; Host stopped and port 8281 was
released. No package/publish/deploy, Site/production change, commit, or push.

Build, test, and authenticated-Host outputs are under
`Backup/Build/Type25AuthE2E-20261026-01/`; visual Host, fixture, and asset
evidence are under `Backup/Build/Type25AuthE2E-20261026-05/`.

## MOB-AI-001 Task 5DL - Meditacao Type 21 score for player self-targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer TCP loopback case applies Meditacao (skill 77) to its caster.
It verifies the Type 21 AC decrease and damage multiplier increase, preserved
current HP/MP, and the 7.69 `UpdateScore` followed by `UpdateAffect`. Only the
caster stream receives those frames; the in-view observer and outside peer
receive no frame. Marker:

```text
MEDITACAO_PLAYER_TYPE21_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests separately covers the source-derived 24-tick duration, recast
without score stacking, and expiry restoration. The `--mob-ai-smoke` entrypoint
catches exceptions and reports them to stderr. This smoke uses synthetic
WorldHub state and local TCP peers; it does not authenticate through the Host
or launch the TMProject client.

## MOB-AI-001 Task 5DK - Toque de Athena Type 15 score for player self-targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer TCP loopback case applies Toque de Athena (skill 45) to its
caster. It verifies the four Type 15 Specials, preserved current HP/MP, and the
7.69 `UpdateScore` followed by `UpdateAffect`. Only the caster stream receives
those frames; observer and outside peer receive no frame. Marker:

```text
TOQUE_DE_ATHENA_PLAYER_TYPE15_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests separately covers first-cast duration 24, recast duration 25 and
level 62 after Special4 rises to 62, no score accumulation, and expiry
restoration. The `--mob-ai-smoke` entrypoint catches exceptions and reports
them to stderr, avoiding an unhandled CLR exception dialog. The smoke uses
synthetic WorldHub state and local TCP peers; it does not authenticate through
the Host or launch the TMProject client.

## MOB-AI-001 Task 5DJ - Escudo Magico Type 11 score for player targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer TCP loopback case applies Escudo Magico (skill 43) to its caster.
It checks Type 11's `Level / 5 + Value` AC increase, preserved current HP/MP,
and the 7.69 `UpdateScore` followed by `UpdateAffect`. Only the caster stream
receives those frames; observer and outside peer receive no frame. Marker:

```text
ESCUDO_MAGICO_PLAYER_TYPE11_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests separately covers source-derived 24-tick duration, recast without
AC stacking, and expiry restoration. The smoke uses synthetic WorldHub state
and local TCP peers; it does not authenticate through the Host or launch the
TMProject client.

## MOB-AI-001 Task 5DI - Possuido Type 14 score for player self-targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case applies Possuido (skill 3) to its caster. It checks
the Type 14 MaxHp/Constitution formula for a Mortal caster, preserved live
HP/MP, and the 7.69 `UpdateScore` followed by `UpdateAffect`. Only the caster
stream receives those frames; the observer and outside peer receive no frame.
Marker:

```text
POSSUIDO_PLAYER_TYPE14_SMOKE_PASS score-affect-caster-only=true
```

Engine.Tests separately covers Mortal and non-Mortal class-master formulas,
source-derived 24-tick duration, recast without score stacking, and expiry
restoration. The harness uses synthetic WorldHub state and TCP loopback peers;
it does not authenticate through the Host or launch the TMProject client.

## MOB-AI-001 Task 5DH - Assalto Type 13 score for player self-targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case applies Assalto (skill 11) to its caster. It checks
that the caster receives Type 13 UpdateScore with the 7.69 projected damage and
the matching UpdateAffect. The caster's current HP/MP are preserved; an in-view
observer and a peer outside the view receive no frame. Marker:
`ASSALTO_PLAYER_TYPE13_SMOKE_PASS score-affect-caster-only=true`.

Engine.Tests covers the source-derived Special3 level and 24-tick duration,
multiplier-only projection, recast, and expiry restoration. This smoke uses
synthetic WorldHub state and local TCP peers; it does not authenticate through
the Host or launch the TMProject client.

## MOB-AI-001 Task 5DG - Enfraquecer Type 10 score for player targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case applies Enfraquecer (skill 51) to a player target.
It verifies that Type 10 reduces transmitted CurrentScore damage by
`Level / 5 + Value`, then checks the matching UpdateAffect. The skill reports
zero direct damage; the target's current HP/MP remain intact. Only the target
receives the score and affect frames. Marker:
`ENFRAQUECER_PLAYER_TYPE10_SMOKE_PASS score-affect-target-only=true`.

Engine.Tests separately covers the source duration, four-tick recast, and
damage restoration on player timer expiry. The harness uses synthetic
WorldHub state and local TCP peers; it does not authenticate through the Host
or launch the TMProject client.

## MOB-AI-001 Task 5DF - Lamina Congelante Type 7 score for player targets (25/10/2026)

Run from `Server` after building the solution:

```powershell
dotnet tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll --mob-ai-smoke
```

The three-peer loopback case applies Lamina Congelante (skill 19) to a player
target. It verifies Type 7 `UpdateScore` with AttackRun `0x86` and Intelligence
75, followed by the matching `UpdateAffect`; the target retains current HP/MP.
The attacker and peer outside the target view receive no frame. Marker:
`LAMINA_CONGELANTE_PLAYER_TYPE7_SMOKE_PASS score-affect-target-only=true`.

Engine.Tests separately covers initial score projection, Type 7's source recast
time/level behavior, and timer expiry/restoration. This smoke uses synthetic
WorldHub state and TCP loopback peers; it does not authenticate through the
Host or launch the TMProject client.

## SHOP-005 - authenticated NPC sell Host smoke (04/11/2026)

Run from `Server` with a build of the E2E runner and the local 7.69-derived
item table:

```powershell
dotnet run --no-build --no-restore --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --npc-sell-smoke --item-data ../Client/7670/ItemList.bin
```

The runner starts a disposable authenticated file-store Host on loopback, with
temporary account/review/NPC roots. It filters a single TMProject 7.69
`Perzen_Normal` generator row, adds `StartWait=600` to hold that synthetic NPC
on its source tile while the Host's 200 ms AI runtime runs, and creates a
synthetic class-3 seller with learned bit `0x1000`, so this focused sale test
does not depend on a nearby merchant view. The wait override applies only to
the disposable fixture. It sells a carry item and checks checksummed yellow panel
`0x5000`, exact `0x037A` echo, and final `0x0337` UpdateEtc with the new Coin.
The runner then stops the Host while the seller is still connected, waits for a
clean exit, confirms the final treasury checkpoint log marker, and reads back
the saved Coin and cleared carry after process exit. Success marker:
`NPC_SELL_LOOPBACK_SMOKE_PASS ... response=5000-037A-0337 departure-checkpoint-confirmed`.

The final fixture passed twice consecutively. This is an authenticated Host/TCP
loopback smoke, not visual client E2E. It
does not validate equipment view relay, MariaDB, transaction recovery across
player and treasury checkpoints, or all rejection messages.

The focused target-out-of-view rejection uses the same runner with a dedicated
fixture:

```powershell
dotnet run --no-build --no-restore --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --npc-sell-out-of-view-smoke --item-data ../Client/7670/ItemList.bin
```

This fixture sets `Merchant=1` on the temporary `Perzen_Normal`, places a
class-0 seller outside the target's view, and verifies the source-backed close
signal (`0x0196`, ID `30000`, empty payload) while confirming the carry item
remains. It passed with marker
`NPC_SELL_OUT_OF_VIEW_LOOPBACK_SMOKE_PASS`; the log is
`Backup/Build/npc-sell-close-shop-20261104/out-of-view-smoke.log`. The fixture
change is temporary and does not alter production data.

The empty carry-slot resync uses its own authenticated fixture:

```powershell
dotnet run --no-build --no-restore --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --npc-sell-empty-carry-resync-smoke --item-data ../Client/7670/ItemList.bin
```

It checks the seller receives `MSG_SendItem` (`0x0182`) for inventory type 1,
slot 0, item index 0, then shuts down the Host and confirms the slot and Coin
were not mutated. Success marker:
`NPC_SELL_EMPTY_CARRY_RESYNC_LOOPBACK_SMOKE_PASS`.
