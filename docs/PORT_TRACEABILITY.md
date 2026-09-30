# Matriz de rastreabilidade do port

## COMBAT-ATTACK-TIMING-001 - erros temporais de MSG_Attack (30/09/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/_MSG_Attack.cpp` e W2PP
`Source/Code/TMSrv/_MSG_Attack.cpp` somam `AddCrackError(1,107)` quando
`ClientTick < (uint)LastAttackTick+800` e quando o tick fica fora de
`[max(0, CurrentTime-120000), CurrentTime+15000]`. Entre esses gates, se
`ClientTick < (uint)LastAttackTick-100`, ambas somam `AddCrackError(4,7)` sem
retornar; copiam o tick anterior, atualizam LastAttackTick/LastAttack e seguem
para a janela do servidor. `LoginSessionRegistry.TryAcceptAttackTiming` agora
reporta o efeito type7 depois do gate TooSoon e antes dessa atualizacao. O host
registra type7 antes de type107; se type7 sozinho cruza o threshold, encerra
antes de skill/mana/dano, e se acompanha OutsideServerWindow adia o logout ate
adicionar type107. O contador e limite sao compartilhados. Tick reservado e
rejeitado antes do handler; InvalidSkillId, atraso especifico de skill, mana e
dano continuam fora deste recorte.

A TMProject adiciona `SendBanAccount(Analise)` e `SaveInfoDetect` no TooSoon e
ban `Tempo3horas`/`SaveInfoDetect` fora da janela. W2PP nao tem esses efeitos. O
port nao os integrou aqui: Analise e restrita a Action pela regra vigente, e
ban/sink para Attack ficam como divergencia pendente de decisao especifica.

Engine.Tests verifica 4/type7 logado, acumulacao no mesmo contador type107,
threshold, gate TooSoon, limites de 100/101 ms e wrap unsigned; 309/309 PASS.
RED type107: `Backup/Build/AutoTradeCloseSmoke/14de35c8ddcd4e56a04c992051ef6e72`;
RED type7: `Backup/Build/AutoTradeCloseSmoke/43a5a971c89b4a779ae06860223638b9`;
GREEN: `Backup/Build/AutoTradeCloseSmoke/2c94658ad5b643f59052b752171f9a83`.
Build solution SDK 10.0.401, 0 erros e 1 aviso CS8321 da funcao local
`ExperienceBonus` sem uso em Program.cs, fora deste recorte. O smoke loopback confirmou um
TooSoon e dois OutsideServerWindow com type107, duas InvalidSkillId e type7
total6 apos wrap; sem relay/HP/MP, fixtures intactas e cleanup completo. Regressao
HP0 passou em `Backup/Build/AutoTradeCloseSmoke/78af97932f8c4fcf86cd050b754189bb`,
e RvR em `Backup/Build/AutoTradeCloseSmoke/02be512b091d4ab3aa4fed105df507b2`.
E2E visual e outros ramos de Attack continuam pendentes.

## COMBAT-ATTACK-RVR-001 - restricao RvR em MSG_Attack (30/09/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/_MSG_Attack.cpp` verifica, depois de
TradeMode e USER_PLAY, `LastX / 128 == RvrMapX`, `LastY / 128 == RvrMapY` e
`!MOB.Clan`; `Basedef.h` define `RvrMapX=13`, `RvrMapY=15`. O setor fechado
corresponde a x1664..1791 e y1920..2047. A source envia a mensagem privada
`Você não pode atacar nessa área.` e retorna antes do gate HP zero, tick, mana e
combate, sem `AddCrackError`. W2PP `_MSG_Attack.cpp` nao contem esse ramo; para a
release 7670 foi escolhida a regra da referencia 7.69 e a divergencia foi mantida
explicita.

`LegacyAttackRvrGateRules` implementa a regra. `WorldHub.TryGetAttackState` copia
combat state, posicao viva e byte Clan do MOB sob um unico lock para evitar
misturar snapshots. O listener, depois dos gates de TradeMode e USER_PLAY,
responde ao autor com `MessagePanelConfirmation` `0x0101`/140 (ID/tick/keyword
zero, texto Latin-1), sem relay do ataque, `SetHpMode`, `SetHpMp` ou CrackError.
Unitarios cobrem os limites, os quatro setores adjacentes, Clan nao-zero e captura
autoritaria; o smoke loopback testa tick atual e tick antigo 240 antes do gate de
tick e confirma resposta privada e ausencia de relay ao observador.

Engine.Tests 306 PASS; SDK .NET 10.0.401; build da solution e rebuild E2E
0 avisos/0 erros.
Evidencia de rede
`Backup/Build/AutoTradeCloseSmoke/dceda13b6b7d4d35b05de1b3f0d33725`; regressao
HP0 `349005fe16954ca4af140278992f9d24`. Fixtures originais intactas, cleanup
concluido e porta18283 liberada. E2E visual, demais branches de Attack e deploy
permanecem fora deste recorte.

## MOVE-ACTION-ILLUSION-COOLDOWN-AUDIT-001 (30/09/2026)

Auditoria somente de source em TMProject 7.69 e W2PP. O login atribui
`LastActionTick=SKIPCHECKTICK` (`DataServerPackageHandler.cpp:820` na 7.69;
`ProcessDBMessage.cpp:862` no W2PP). A busca por todas as referencias do campo
nas duas arvores encontrou apenas essas inicializacoes. Os ramos normais de
`MSG_Action` e `MSG_Action3` testam `LastActionTick != 235543242` antes do limite
de 900 ms, com `1/type103` e `1/type105`, respectivamente; assim, ambos os
checks ficam inativos no estado atual das fontes. `LastIllusionTick` e atualizado
depois do tick valido, mas nao altera o sentinel. Nao adicionar gate type105 ao
C# sem uma futura atribuicao comprovada e a semantica correspondente.

Sem alteracao de runtime ou execucao de testes nesta auditoria. A camada visual
do cliente 7.670 continua pendente.

## MOVE-ACTION-ILLUSION-MANA-001 - custo e sincronizacao de MP em MSG_Action3 (29/09/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/_MSG_Action.cpp` e W2PP
`Source/Code/TMSrv/_MSG_Action.cpp` leem o custo fixo de `g_pSpell[73].ManaSpent`
no ramo Illusion. Na linha73 de `Servidor/Server/Common/skilldata.csv`, o valor39
e `SkillPoint`; `ManaSpent` e45. Se `CurrentScore.Mp < 45`, ambas chamam
`SendSetHpMp` para o autor e retornam. Caso contrario subtraem o custo antes dos
gates type105 e type104. A referencia 7.69 deixa `pUser[conn].ReqMp -= mana`
comentado; W2PP executa essa subtracao. O port escolhe o contrato 7.69: altera
somente `CurrentScore.Mp`, preserva `ReqMp` e nao reutiliza `BASE_GetManaSpent`
nem o desconto por SaveMana das skills de ataque.

O listener requer a definicao carregada do skill73 para processar Action3; dado
ausente/negativo falha fechado. MP insuficiente produz `SetHpMp` privado e nao
relaya movimento. Movimento aceito relaya Action3 e depois envia `SetHpMp` ao
autor. A despesa ocorre antes dos gates type105/type104; portanto tick invalido
consome MP e soma type104, mas o retorno de tick nao envia sincronizacao. Cooldown
permanece fora deste recorte.

Unitario: custo fixo45 reduz MP100->55 mesmo com SaveMana100, preservando ReqMp100;
MP44 nao e aceito nem mutado. E2E em loopback com a tabela primaria real semeou
MP90: tick antigo consumiu45 e somou1/type104 sem relay/sync; Action3 valido
consumiu os45 restantes, relayed a ator/peer e sincronizou MP0/ReqMp90 apenas ao
autor; Action3 seguinte foi recusado por MP insuficiente com sync privado. Logout
salvou MP0 e relogin confirmou persistencia. O RED inicial confundiu
`SkillPoint=39` com `ManaSpent`; o GREEN corrigiu a fixture para45. O primeiro
smoke client GREEN passou, mas o wrapper contou tambem os dois type17 de seus
preflights; a assercao foi ajustada e o run final passou.

Engine.Tests 303 PASS/0 FAIL; build da solution 0 erros e 1 warning CS8321
(`ExperienceBonus` local sem uso, preexistente neste checkout); self-test do
harness PASS. Evidencias finais: mana
`Backup/Build/AutoTradeCloseSmoke/86016fe1f87c4d2f98100af962352841`, elegibilidade
`9d730d8ca5c94b58b9133d55a77be4b9`, alcance
`fba2c79f765f412384cc489449bb801f`. Hashes das fixtures originais intactos e
porta18283 liberada. Wire/headless loopback, sem WYD.exe; sem deploy, Site,
producao, commit ou push. E2E visual segue pendente; cooldown type105 foi
auditado como inativo em `MOVE-ACTION-ILLUSION-COOLDOWN-AUDIT-001`.

## MOVE-ACTION-ILLUSION-ELIGIBILITY-001 - classe e skill da MSG_Action3 (29/09/2026)

Depois dos gates comuns de USER_PLAY, HP==0 e cancelamento de trade, TMProject
7.69 `_MSG_Action.cpp` verifica no ramo `MSG_Action3` se `Class==3` e se
`(LearnedSkill & 2)!=0`. Se qualquer requisito falhar e `Level<MAX_LEVEL`
(`MAX_LEVEL=399`), chama `AddCrackError(10,28)`; somente o retorno de threshold
encerra o handler. Abaixo do threshold, o pacote segue para os gates posteriores.
W2PP `_MSG_Action.cpp` tem a mesma regra e a mesma ordem.

O C# consulta `LegacyMobCombatState` autoritativo e aplica a regra antes do
type104 de tick. O resultado `RequiresCharacterLogout` e a unica condicao nova
de parada; type28 continua no contador compartilhado da sessao. Unitarios cobrem
classe3 + bit2 valido, classe incorreta, bit ausente e niveis 398/399.
`-ActionIllusionEligibility` usou copia isolada com classe2, bit ausente, level398
e HP-1; Action3 foi relayed ao ator e ao observador, sem HpMode, e o `QuitTrade`
subsequente observou type28=10 + seu proprio incremento type17=10 (total20).

O RED `5215e9f6612940a6bac4c7cb68ea367b` confirmou a retransmissao, mas falhou
na assercao de type28 ausente. O GREEN
`ada5e06696e14e578146dfabc1f5f11c` passou com `ACTION_ILLUSION_ELIGIBILITY_NETWORK_PASS`,
`CRACK_NETWORK_PASS`, `AUTOTRADE_NETWORK_PASS`, hashes originais intactos e
porta18283 liberada. A regressao `-ActionRange` tambem passou em
`d9c3973434c64e9c9d78e4c69f47c984`. Engine.Tests 303 PASS/0 FAIL; self-test do
harness PASS. O custo e o sync de mana estao em
`MOVE-ACTION-ILLUSION-MANA-001`; o check type105 de 900 ms foi auditado como
inativo. A validacao visual do efeito de Illusion e E2E visual seguem pendentes.

## MOVE-ACTION-ILLUSION-TICK-001 — janela ClientTick de MSG_Action3 (29/09/2026)

TMProject 7.69 `_MSG_Action.cpp` e W2PP `_MSG_Action.cpp` validam o tick de
`MSG_Action3` com os mesmos limites do `MSG_Action`: nao anterior a
`max(0, CurrentTime-120000)` nem posterior a `CurrentTime+15000`. A rejeicao de
Illusion soma `AddCrackError(1,104)` nas duas referencias e retorna antes dos
gates comuns de velocidade/alcance/mapa e do relay. O C# aplica esses limites
inclusivos ao `ActionRequest.IllusionMessageType`; Action normal conserva
1/type102 e Action2 continua sem gate temporal.

O intervalo de 900 ms (`type105`) nao foi incluido: a auditoria
`MOVE-ACTION-ILLUSION-COOLDOWN-AUDIT-001` confirmou que a condicao legada depende
de `LastActionTick != SKIPCHECKTICK`, mas as duas sources so inicializam o campo
no login. A elegibilidade de classe/skill, executada antes do tick, foi concluida
em `MOVE-ACTION-ILLUSION-ELIGIBILITY-001`; custo e sync de MP foram concluidos em
`MOVE-ACTION-ILLUSION-MANA-001`.

O RED reproduziu `MSG_Action3` com tick mais de 120 s antigo sendo relayed. No
GREEN, `-120001 ms` e `+30001 ms` nao chegaram ao autor/peer e cada um somou
1/type104. Action valido, Action2 com tick futuro, velocidade/range, estado
persistido/relogin e carry/cargo/Gold tambem passaram. Engine.Tests 302 PASS/0
FAIL; solution build 0 avisos/0 erros; self-test E2E PASS. RED:
`Backup/Build/AutoTradeCloseSmoke/ce3d2ecd91c449c7a9905c41d443ff28`; GREEN:
`Backup/Build/AutoTradeCloseSmoke/ba0b956c68134fd8821fc8467e9d6cc7`. Hashes das
fixtures originais intactos e porta 18283 livre. Wire/headless em loopback, sem
WYD.exe ou deploy; custo de mana esta em `MOVE-ACTION-ILLUSION-MANA-001`; o
check type105 foi auditado como inativo e E2E visual segue pendente.
Elegibilidade esta coberta em
`MOVE-ACTION-ILLUSION-ELIGIBILITY-001`.
Sem Site/producao, commit ou push.

## MOVE-ACTION-TICK-001 — janela ClientTick do MSG_Action (29/09/2026)

TMProject 7.69 `_MSG_Action.cpp` e W2PP `_MSG_Action.cpp` aplicam o gate no
ramo normal de `MSG_Action`: `ClientTick` anterior a
`max(0, CurrentTime-120000)` ou posterior a `CurrentTime+15000` gera
`AddCrackError(1,102)` e retorna antes dos gates de velocidade/alcance, mapa,
mutacao e relay. Os limites sao inclusivos: igualdade nos extremos e aceita.
O C# aplica a janela usando ticks monotonicamente derivados de
`Environment.TickCount64` e aritmetica `uint` unchecked compativel com o wire.
`MSG_Action2` e um branch independente sem este gate; `MSG_Action3` tem gates de
elegibilidade, mana e janela de tick em recortes proprios; o check type105 de
900 ms e inativo conforme `MOVE-ACTION-ILLUSION-COOLDOWN-AUDIT-001`.

A auditoria das duas sources encontrou `LastActionTick=SKIPCHECKTICK` no login
e nenhuma atribuicao posterior no codigo pesquisado. Por isso, o check legado
de intervalo minimo de 900 ms esta inativo e nao foi habilitado junto com a
janela. O comportamento segue limitado ao tick velho/futuro descrito acima.

O RED anterior a mudanca reproduziu relay de `MSG_Action` com tick mais de 120 s
antigo. No GREEN, os casos `-120001 ms` e `+30001 ms` foram rejeitados, cada um
somou 1/type102 e nenhum Action chegou ao autor/peer; o controle `Action2` com
`ClientTick=uint.MaxValue` continuou relayado. A regressao tambem cobriu Action
valido, limites de range/velocidade, persistencia/relogin e integridade das
fixtures. Engine.Tests 302 PASS/0 FAIL; solution build 0 avisos/0 erros; self-test
E2E PASS. Evidencia: `Backup/Build/AutoTradeCloseSmoke/b1d7e3f78bdb42d1a8bbbde9a4f5ff97`;
hashes originais intactos e porta 18283 liberada. Validacao wire/headless em
loopback, sem WYD.exe ou deploy; custo de mana de Action3 esta em
`MOVE-ACTION-ILLUSION-MANA-001`; E2E visual permanece pendente e o cooldown
type105 foi auditado como inativo.
Elegibilidade e tick de Action3 estao em recortes proprios. Sem
Site/producao, commit ou push.

## MOVE-ACTION-RECALL-001 - recall pela origem (29/09/2026)

TMProject 7.69 `_MSG_Action.cpp` chama `DoRecall` e retorna antes dos gates
normais quando a posicao autoritativa cai nos setores Celestiais elegiveis ou
(13,15) sem clan. `DoRecall` envia o `MSG_Action` com Effect=1 ao autor e chama
`GridMulticast` para relay e transicao de visibilidade espacial. W2PP diverge:
comenta o gate Celestial e nao tem o gate sem clan; a regra 7.69 permanece como
contrato.

`WorldHub.TryRecallForActionOriginWithViews` muda a posicao e captura sob o
mesmo lock as duas janelas 33x33, os itens estaticos de mapa (`mapItems`) e os
drops dinamicos (`groundItems`). TMProject 7.69 e W2PP usam a mesma tabela
`pItem`: `InitItem` ocupa os primeiros slots e `CreateItem` usa `GetEmptyItem`
para alocar no mesmo pool. O C# agora coordena os IDs das duas colecoes, limita
o pool aos slots 1..4999 e rejeita IDs solicitados que colidam com item estatico.
No `GridMulticast(conn, tx, ty, msg)`, as duas referencias enviam
`MSG_DecayItem` de item que saiu e `MSG_CreateItem` de item que entrou somente
ao personagem em transicao; o branch nao envia esses deltas aos observadores.
`LegacyActionOriginRecallRelay` agora preserva isso: alem do Action e dos
RemoveMob/CreateMob/PKInfo por visao, calcula os deltas pelos IDs dos snapshots
atomicos e envia-os apenas ao autor. O relay nao chama `BroadcastAsync`. PKInfo
usa o estado 7.69 comum de culpa/PK/castelo/torre/NewbieEvent; RvR continua fora
do contrato.

Testes de unidade verificam os IDs compartilhados, item estatico + drop nas
visoes de origem/destino, opcodes/IDs/coordenadas dos frames e ausencia de relay
dos itens aos outros participantes. O smoke TCP com quatro contas isoladas
dropou itens 10001/10002 por `MSG_DropItem`, confirmou `MSG_DecayItem`/
`MSG_CreateItem` no autor e nenhum desses deltas nos observadores; Action e
transicoes de jogadores tambem passaram. Os blobs persistiram nas posicoes
verificadas no logout. Engine.Tests 301 PASS/0 FAIL, solution build 0 avisos/0
erros, self-test E2E PASS e regression `-ActionRange` PASS; hashes originais
intactos e porta 18283 liberada. Evidencias: recall
`Backup/Build/AutoTradeCloseSmoke/1bdcf451c5e94e5e814742d680829ea3`; regressao
`Backup/Build/AutoTradeCloseSmoke/14f9767208e349e7b8d3bdc325cd9ac4`.
Wire/headless validado; E2E visual segue pendente. Recall por restricao de mapa
e outros teleportes usam caminhos separados; sem deploy, Site/producao, commit
ou push.

## MOVE-ACTION-STOP-001 — bloqueio de Action2 consecutivo (29/09/2026)

TMProject 7.69 `_MSG_Action.cpp` e W2PP `_MSG_Action.cpp` conferem
`LastMove == _MSG_Action2` no ramo de Stop, registram exatamente 1/type101 e
retornam antes de velocidade/relay. O C# guarda o ultimo opcode de movimento na
`LoginSession`; `CompleteCharacterLogin` o zera e o registry o troca somente
quando um movimento aceito altera a posicao. O host pontua o stop repetido e
interrompe o handler; movimento normal aceito libera o proximo Action2. O teste
de dominio cobre rejeicao sem mutacao de coordenadas, liberacao por Action e
reset por relogin.

Engine.Tests: 298 PASS/0 FAIL; solution build: 0 avisos/0 erros; self-test do
harness passou. TCP loopback `-ActionNegativeHp` confirmou primeiro Stop relayado,
segundo Stop sem relay, restauracao por Action, Stop seguinte aceito, contador
compartilhado type101, logout/relogin e integridade de posicao/carry/cargo/Gold.
`-ActionRange` passou em regressao, incluindo os tres opcodes e os limites de
range/velocidade. Evidencias:
`Backup/Build/AutoTradeCloseSmoke/d39cfcf26dc143fb9e305bb553f77801` e
`Backup/Build/AutoTradeCloseSmoke/463fd03be1864c37b574fbcaddf64063`; hashes das
fixtures originais intactos e porta 18283 liberada. O smoke TCP de recall passou
(ver MOVE-ACTION-RECALL-001); Illusion/ticks e E2E visual permanecem pendentes. Sem deploy,
Site/producao, commit ou push.

Recorte MOVE-ACTION-TRADE-001 (28/09/2026; escopo bilateral): TMProject 7.69
`_MSG_Action.cpp:24-28` e W2PP `:40-44` concordam: HP==0 retorna antes da
limpeza; caso contrario, `Trade.OpponentID` chama RemoveTrade para os dois
participantes e interrompe Action antes de movimento. `WorldHub.TryCancelTradeForAction`
faz a limpeza bilateral sob lock, sem passar pelo gate diferente de QuitTrade e
sem mutar character state. O host envia `0x0384`/12 para cada jogador, com ID do
destinatario e tick/keyword zero, e nao relaya Action. HP=-1 segue a limpeza;
HP==0 preserva o par. Unidade verifica ambos os HP, close persistente pendente,
os dois lados do trade e MOB/carry/Gold byte a byte; TCP loopback Debug HP0/-1
passou. Suite273 PASS; solution build 0 avisos/0 erros; rastreio E2E detalhado
em `CLIENT_E2E_SMOKE.md`. Esse recorte nao cobria `TradeMode`; seu fechamento
acionado por Action esta rastreado em MOVE-ACTION-TRADEMODE-001 abaixo. O
ban/deteccao exclusiva 7.69 continua fora do port.

Recorte MOVE-ACTION-TRADEMODE-001 (28/09/2026): TMProject 7.69 e W2PP fazem
`Action` retornar primeiro em `HP==0`, depois removem o par bilateral (se
existir) ou `TradeMode` do autor. C# chama `CloseForActionAndCaptureAsync`, que
usa as leases/expected-delete/recovery ja compartilhadas com compra, fechamento
e saida; o hint `HasListing` so direciona e a operacao revalida o estado sob lease.
Shop no autor ou parceiro fecha a transacao combinada e Action retorna antes de
speed/range/movimento. HP negativo e aceito por este caminho, diferente do gate
`HP<=0` de QuitTrade. Emite `0x0384`/12 por participante online e CreateMob
normal/236 por shop aos destinatarios de visao; sem PK, relay Action,
CreateMobTrade/260 ou RemoveMob. Nenhuma alteracao em carry/cargo/Gold/MOB.
Engine.Tests 276 PASS; solution build 0 avisos/0 erros; E2E loopback combinado
passou em `Backup/Build/AutoTradeCloseSmoke/39e5171332814620b95ebc0a671b0650`;
fixtures originais intactas, porta liberada. E2E visual, evento AFK/ISTradTimer e
ban/deteccao 7.69 continuam pendentes; sem deploy/site/commit/push.

Recorte MOVE-ACTION-RANGE-001 (27/09/2026): `_MSG_Action.cpp` 7.69/W2PP compara
destino com a posicao server-side antes do mapa; `VIEWGRID=33`, correcao privada
Action3 apenas quando algum eixo e `>66`, AddCrackError `1/type5` em todo alvo
`>33`. C# sincroniza snapshot WorldHub -> LoginSessionRegistry antes do gate,
ignora origem/rota cliente na decisao, rejeita sem movimento/relay e filtra o
relay aceito pela janela 33x33. `LegacyMovementRules` cobre limites/sinais e
overflow long; unidade verifica o contador compartilhado. Suite267 PASS/build
limpo; TCP loopback testa os tres opcodes, 42 rejeicoes, correcao/ausencia de
correcao, relay dentro/fora de visao e destino/saldo/inventario apos relogin.
Evidencia: `Backup/Build/AutoTradeCloseSmoke/40af9d46592a40659ea7a45765a3eb96`.
Build self-contained smoke: `Backup/Build/AutoTradeCloseSmoke/d8b09ae5b15d47d791cad5c85a232f5c`;
controle runtime anterior: `d022056beff04868a28a88ad814c04eb` falha na correcao
privada, cleanup concluido. Motion regressao `e50cb81f4c5d4215a46b6673800f9428`,
ReqTradeList `acc671ee731c44b6b92e7eedba2be226` e validador autotrade
`d2880c6190514d66b4da312449e7b0d7` passaram. Package runtime-only197:
`action-range-crack-20260927-runtime.zip`, SHA-256
`04CF787E7CE983A269626F500AA378A6FB44508A4B255FF5ABAFFD239E5C47DF`;
Core `AB7B1B147E5DE498A63762F3E79EC1AEF181D689B18D0C98D81D55E3CF7504EC`,
World `0A716BBFAABE0BB2B4579B351AA47A402BA7B9698F4C1550171253AFF2462B0B`,
Protocol `D6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295`.
Sandbox target `C:/WYDCDK/Server/test-releases/20260924-v7670/WydCdk.Server-v7670-20260924`;
backup `backup-autotrade-close-293dd25bdc89446fb71c86beb3d0ba54`, UP/PVP Running,
8281/8282 Listen, hashes e path do executable verificados. Threshold 2G apenas
unitario; sem cliente visual, Site/producao, commit ou push.

Recorte MOVE-ACTION-SPEED-001 (28/09/2026): `_MSG_Action.cpp` W2PP e TMProject
7.69 limitam Speed a `CurrentScore.AttackRun & 0xF`, somam 5/type 4 no excesso e
normalizam somente Speed antes do teste de range. C# usa o score autoritativo e
o contador compartilhado para Action/Action2/Action3. Recursos WorldHub ausentes
rejeitam a acao; com recursos presentes, HP==0 tem prioridade sobre score de
velocidade ausente e envia HpMode20/Mode22 +5/type3. HP ausente ou AttackRun
ausente em personagem vivo falha fechado. Depois de registrar erro de velocidade,
a sessao e revalidada e o handler para se o threshold encerrou USER_PLAY. A source
7.69 tambem aciona `SendBanAccount`/`SaveInfoDetect` em `Speed>=20` ou
`AttackRun>=300`; esse efeito e deliberadamente pendente, ausente no W2PP.
Suite 271 PASS/build 0/0; smokes TCP Debug+self-contained provam relay corrigido
e violacao dupla sem relay; teste unitario de regressao cobre HP-zero/score
ausente. Type4 5/10/15/20;
type5 21..63; QuitTrade 73/relogin 10. Evidencias em `CLIENT_E2E_SMOKE.md`.
Runtime-only197 sandbox: ZIP SHA-256
`000F987FAF29C74F485D29F3F1B3CF3B2A97C1E2D960F7550918D5C1C7CCB5D9`, backup
`backup-autotrade-close-0b04898eaf31414bae0f2005baa006ac`, UP/PVP Running e
8281/8282 Listen; os 197 hashes do pacote comparados independentemente. Cinco
extras preexistentes foram preservados. Sem threshold TCP, visual E2E,
Site/producao, commit/push.

Recorte MOVE-ACTION-CRACK-001 (27/09/2026): `_MSG_Action.cpp` 7.69/W2PP,
ProcessClientMessage.cpp W2PP e PacketHandler.cpp alvo mapeiam os tres opcodes
ao mesmo handler. Fora USER_PLAY nao pontua/nao envia HpMode; HP==0 envia
HpMode20/Mode22 privado e soma5/type3 sem log; negativo segue caminho existente.
Gate C# antes de sincronizacao/mapa/recall. Contador compartilhado e threshold
2G cobertos em unidade; rede prova via QuitTrade totais10/20/25-ou10/reset10.
265 PASS/build0/0; debug/self-contained HP0/-1, destino persistido e inventarios
intactos; guardas relay opcode/ID/tick/payload e parser isolado executadas.
Spawn real capturado: entrada de novato sorteia posicao, nao restaura SPX/SPY.
Review gate sem achados; demais branches/threshold TCP/E2E3D nao cobertos.
Controle runtime anterior falha no HpMode; runtime-only197 sandbox publicado
com backup/hashes/UP-PVP Running/listeners conferidos. Runs em CLIENT_E2E_SMOKE.

Recorte TRADE-LIST-CRACK-001 (27/09/2026): `_MSG_ReqTradeList.cpp`/SendAutoTrade
7.69 e W2PP: HP==0 ou Mode!=USER_PLAY registra10/type87 antes do alvo;
SendHpMode privado/silencioso fora playing. LegacyAutoTradeListRules corrigido
<=0 -> ==0; negativo permitido. Host preserva captura/lista sob lease e relays
existentes; rele combatstate apos await, release antes do ramo HP0/contador.
Guard IDs adaptado ao contrato da lease C#1..ushortMax e NPC offline, nao
transplanta MAX_USER da source nem altera alocacao. Wire039A/16 nao mudou.
264 PASS/build0/0; debug/self-contained HP0/-1 e controles antigos falham;
lista196/visual260 privados, payload/owner, logs ligados ao ator, estado duravel
e relogin conferidos. Sandbox publicado runtime-only197 com backup/hashes.
Review final sem bloqueadores; P3 morte durante espera da lease foi fechado no
recorte TRADE-LIST-LEASE-RACE-001 abaixo. Threshold TCP e E2E3D pendentes.
Ver CLIENT_E2E_SMOKE para os runs/hashes.

Recorte TRADE-LIST-LEASE-RACE-001 (28/09/2026): as duas sources C++ fazem o
gate HP==0/USER_PLAY antes do lookup e nao possuem a espera de lease do adapter
C#. `TradeListRequestHandler` isola o fluxo host sem mudar wire/regra: rele HP
depois do AcquireAsync, solta a lease antes de HpMode/resposta e soma 10/type87
uma vez. Teste deterministico segura a lease do requester/loja, inicia o mesmo
handler, muda HP para zero e libera; valida HpMode privado (checksum, ID, tick,
HP e Mode), ausencia de list196/visual260, contador unico e reacquire da lease
durante o callback de envio. Controle vivo recebe list196+visual260 sem novo
erro. Controle negativo com a condicao pos-lease mutada para HP==int.MinValue
falhou no assert da corrida; regra restaurada e suite272 PASS. Build da solucao
0 avisos/0 erros. Smoke TCP loopback do host/listener/harness com fixture HP0
passou, inclusive consultas vivas, logs10/20/type87, list196/visual260,
QuitTrade30/type17, logout/relogin e estado persistido intacto. Essa rede
valida a integracao normal do dispatcher, nao a corrida concorrente. O helper
PowerShell nao iniciou o processo devido a colisao Path/PATH do Start-Process;
o mesmo runtime foi iniciado diretamente no loopback e encerrado cooperativamente.
Sem alterar policy permanente, VPS, Site, producao, commit ou push.

Recorte MOVE-CRACK-001 (27/09/2026): `_MSG_Emotion.cpp` TMProject 7.69 e
`_MSG_Motion.cpp` W2PP comparados: HP==0 ou Mode!=USER_PLAY soma4/type6;
SendHpMode privado somente playing; HP negativo relaya. Host usa contador e
logout comuns a QuitTrade, sem alterar wire/relay. Suite263/build0/0 e rede
debug/self-contained HP0/-1 passaram; HpMode privado/vetor negativo e controle
runtime antigo validados. Runtime sandbox publicado com backup/hashes/listeners.

Recorte TRADE-CRACK-001 (27/09/2026): LegacyCrackErrorRules e
LoginSessionRegistry/host QuitTrade comparados com Server.cpp::AddCrackError,
_MSG_QuitTrade.cpp, SendFunc.cpp::SendHpMode e reset no character login das
duas sources. Mesma regra10/type17/limite2G; C# satura contra overflow e reutiliza
logout normal, sem ban novo. 262 PASS/build0/0; rede Accepted/Selection/HP0 e
relogin/carry/Gold passaram. Limite unitario, nao E2E TCP; demais callers e resets
periodicos/Restart/Attack pendentes. Ver CLIENT_E2E_SMOKE para evidencias/deploy.

> Snapshot historico de 18/09/2026. A politica de referencias 7.59/7559 e
> 7.60/7600 nao e mais vigente. Durante o reinicio, revalidar cada ficha contra
> a referencia comunitaria 7.69 e registrar os hashes/releases efetivamente
> examinados; nao renomear uma evidencia antiga como 7.69 sem nova verificacao.

**Baseline historico:** 18/09/2026; linhas novas 7.69 verificadas em 19/09/2026  
**Fonte de verdade:** esta matriz registra o estado atual das rotas; o detalhe de cada metodo deve ser mantido no formato da ficha do `PORT_ARCHITECTURE_BASELINE.md`.  
**Regra:** `implementado` nao significa automaticamente `validado pelo cliente`.

## Smoke de trade concluido e replay (26/09/2026)

Replay somente Gold reproduziu segundo commit duravel no C#; corrigido apos
teste de dominio vermelho. TryStageTradeOffer agora exige HasOffer nos dois
lados antes de check e compara dados antes de SetOffer, inclusive no primeiro
check. MyCheck nao entra na igualdade da oferta (transicao legitima 0->1).
7.69/W2PP possuem memcmp contra oferta guardada; exigir staged bilateral em vez
de aceitar fallback de check inicial e adaptacao explicita, sem mudar wire.
Suite 218 PASS; E2E Gold recusa tres checks antigos, permite segunda troca
legitima na mesma sessao e verifica blobs/relogin. Regressao item/Gold passou.
Comportamento C++ nao executado. Fix publicado somente na VPS de teste:
UP/PVP Running, 8281/8282 Listen e hashes World/Core iguais aos artefatos locais.
Reproducao e resultados em `CLIENT_E2E_SMOKE.md`; sem anti-replay global.

Evidencia adicional de trade em 26/09/2026: simulador headless em loopback com
duas contas copiadas concluiu item por 1 Gold, recebeu `0x0185`/528 nos dois
lados e conferiu os blobs duraveis antes do logout. Reenvio exato dos dois
checks, duas vezes cada, preservou uma unica transferencia apos relogin.
O ator foi rejeitado por ItemChanged; o par reabriu oferta sem novo commit.
Ver `CLIENT_E2E_SMOKE.md`. Nao promove trade a C7: MariaDB, concorrencia,
crash/recovery, troca somente Gold e E2E visual permanecem fora do recorte.

## Recalculo de personagem no login — recortes isolados (21/09/2026)

`LegacyCurrentScoreMath` agora cobre, em metodos separados e ainda fora do
adapter/listener, o reset `BaseScore -> CurrentScore` com abilities de equipamento,
o estagio `Cur Special`, Soul normal e o override tardio Kibita, o seed de
abilities da secao `Cur HP/MP`, os clamps escalares finais de HP/MP, regen, Magic
e Critical, os modificadores de resistencia por affects, o fechamento de dano e
os clamps finais das quatro resistencias, a composicao final de AttackRun e o
affect `Type=2` Haste (soma de Run e flag `RSV_HASTE=0x20`). A comparacao do fechamento de dano esta na ficha
[`character-login-final-damage.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-damage.manifest.md);
o bloco final coincide com W2PP para as mesmas entradas, mas o W2PP possui adicoes
anteriores por classe ausentes do alvo 7.69. As comparacoes de Soul estao nas fichas
[`character-login-soul.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-soul.manifest.md)
e [`character-login-kibita-soul.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-kibita-soul.manifest.md);
o override Kibita coincide na condicao, mas diverge de W2PP no delta de MaxMP.
A comparacao do seed (incluindo regras faltantes de `BASE_GetItemAbility`) esta na ficha
[`character-login-ability-stage.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-ability-stage.manifest.md).
O alvo 7.69 limita Special1 a 255 (W2PP usa 200); para Soul, 7.69 aplica Int/Con
escalados por `ClassMaster`/Soul a cada `Affect.Type=29`, enquanto W2PP calcula
temporarios mas aplica os deltas dos atributos de equipamento. Os 31 casos golden
testam os multiplicadores-alvo, repeticao de affect, truncamento float, summon e
preservacao de HP/MP atuais.
O seed de abilities calcula SaveMana/Magic/Run/AttackSpeed/RegenHP/RegenMP/Critical
antes dos bonus e caps posteriores; os vetores sao source-derived, nao runtime. As
formulas do bloco coincidem com W2PP. Os clamps finais comuns tambem coincidem,
exceto `MAX_DAMAGE_MG` (7.69: 190.000.000; W2PP: 1.000.000.000); os casos estao na
ficha [`character-login-final-clamps.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-clamps.manifest.md).
Quatorze vetores cobrem as mutacoes de resistencia para `Affect.Type` 3/8/16/25;
7.69 inclui Holy em `Type=25` e usa adicoes de transformacao que divergem do W2PP.
Detalhes e comparacao executavel estao na ficha
[`character-login-resistance-affects.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-resistance-affects.manifest.md).
O fechamento final AttackRun coincide com W2PP para os mesmos valores
acumulados; oito vetores cobrem bonus, divisao por Dexterity, caps, floor de
montaria condicionado por `face` em cache e empacotamento. `LegacyMountRunRules`
resolve separadamente o gate por item/efeito e os pisos de 30 mounts comuns e 15
temporarios. Os seis primeiros mounts comuns usam valores 4/5 no alvo 7.69, mas
6 no W2PP. O indice 2390 e rejeitado: o limite inclusivo nas duas sources tenta
ler uma linha alem das 30 disponiveis. A ficha e
[`character-login-mount-speed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-mount-speed.manifest.md).
O recorte `Affect.Type=16` tambem retorna os bonus finais de Attack/Run na ordem
da tabela: a ultima transformacao valida substitui as anteriores. A tabela-base
coincide com W2PP, mas `AttAdd` diverge para Bear, Titan e Eden. A fonte-alvo e
`Servidor/Source/Code/Basedef.cpp`, incluida pelo projeto TMSrv; a copia local
TMSrv nao compilada diverge no Titan. A ficha
[`character-login-transformation-speed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-speed.manifest.md)
registra os vetores e limites. O recorte nao aplica outros efeitos/mutacoes da
transformacao nem esta integrado ao fluxo. Demais bonus de buffs e efeitos de
montaria seguem fora desses recortes, detalhados em
[`character-login-final-speed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-speed.manifest.md).
O recorte de dano `Affect.Type=16` retorna em separado o Damage aditivo (+10 por
Wolf valido) e a soma de `multi - 100` que alimenta `DAMAGEMULTI`; o multiplicador
final nao e aplicado por esse metodo. A aritmetica usa `Affect.Level` ushort sem
clamp. W2PP diverge no `DamAdd` de Wolf com skill e Eden (10 contra 25/20 no
alvo). Ver
[`character-login-transformation-damage.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-damage.manifest.md).
O recorte `Affect.Type=16` de AC escala o valor ja acumulado em cada affect,
trunca na divisao por 100 e adiciona +5 por Wolf valido; portanto, trocar a ordem
das formas muda o resultado. A base Min/Max coincide com W2PP, mas o `AcAdd`
diverge para Wolf com skill, Titan e Eden. Dezenove vetores cobrem as duas fontes;
a ficha [`character-login-transformation-ac.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-ac.manifest.md)
detalha o estagio, que nao esta integrado ao login.
O recorte MaxHp `Affect.Type=16` tambem aplica cada faixa sobre o MaxHp ja
atualizado, sem clamp local. Base HP e aritmetica coincidem com W2PP; `HpAdd`
diverge para Wolf e Bear com skill. O `RegAdd` soma nas quatro resistencias e ja
e coberto pelo recorte de resistencia Type=16, nao sendo duplicado aqui. Vinte
vetores e a ficha
[`character-login-transformation-maxhp.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-maxhp.manifest.md)
registram as duas fontes.
O recorte de Critical/equipamento `Affect.Type=16` acumula o bonus de Critical
por affect valido e retorna uma copia do item de cabeca com indice 22/23/24/25/32
e efeito `EF_SANC`. A mutacao do item e a formula de sancao coincidem com W2PP,
mas o Critical diverge para Astaroth com skill (target 0, W2PP +5) e Eden (target
+10, W2PP +6). Vinte vetores, incluindo repeticao e gates, estao na ficha
[`character-login-transformation-critical-equipment.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-critical-equipment.manifest.md).
O estagio de composicao [`character-login-transformation-composition.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-composition.manifest.md)
reexecuta os resultados ja auditados para o mesmo snapshot de affects: Attack/Run,
dano, AC, MaxHp, Critical, equipamento e resistencia. Sao oito vetores com
comparacao W2PP; a resistencia continua vindo do estagio geral Type=3/8/16/25,
sem somar `RegAdd` duas vezes. O resultado e um snapshot puro, fora do listener,
do encoder ativo e do E2E do cliente.
O recorte [`character-login-magic-shield.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-magic-shield.manifest.md)
fecha o `Affect.Type=11` em seis vetores: o alvo soma `Level / 5 + Value` ao
AC e o W2PP soma `Level / 3 + Value`. O metodo retorna somente o delta, sem
clamp ou mutacao do MOB, e permanece fora do login/listener.
O recorte [`character-login-magic-weapon.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-magic-weapon.manifest.md)
fecha o `Affect.Type=9` em sete vetores: Damage e o multiplicador usam a mesma
formula no alvo/W2PP, mas somente o W2PP soma `Magic +5` por affect. O C# projeta
o Damage antes de Soul e mantem o delta de Magic como divergencia explicita.
O recorte [`character-login-athena-touch.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-athena-touch.manifest.md)
fecha o `Affect.Type=15` em sete vetores: todos os Special usam `Level / 10 + Value`,
mas o alvo limita Special1/2/3/4 em 255 e o W2PP limita Special1 em 200. O C#
projeta os quatro Special depois de Type=9 e antes de Soul.
O recorte [`character-login-dexterity.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-dexterity.manifest.md)
fecha o `Affect.Type=6` em sete vetores: alvo e W2PP usam o mesmo fator `float`,
narrowing para `short` e mutacao sequencial. O C# projeta DEX depois de Type=15.
O recorte [`character-login-fanatism.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-fanatism.manifest.md)
fecha o `Affect.Type=5` em sete vetores: alvo e W2PP usam o mesmo fator `float`,
narrowing para `short` e mutacao sequencial. O C# projeta Fanatismo antes de Type=6.
O recorte [`character-login-holy-touch.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-holy-touch.manifest.md)
fecha o `Affect.Type=1` em sete vetores: alvo e W2PP subtraem `Value` do Run,
30 do Att e, quando `head index > 50`, 40 da Int por affect. O C# retorna deltas
puros; o empacotamento final de `AttackRun` continua fora deste recorte.
O recorte [`character-login-samaritan.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-samaritan.manifest.md)
fecha o `Affect.Type=14` (Possuido) em sete vetores. O alvo usa `MaxHp += value * 2` e
`Con += value`; W2PP usa `MaxHp += value * 22` e `Con += (Con + value) * 125 / 100`.
O C# porta apenas o alvo e mantém a divergencia comparativa explicita, sem integrar
o estagio ao coordenador.
O recorte [`character-login-assault.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-assault.manifest.md)
fecha o `Affect.Type=13` em sete vetores. O alvo soma somente `Level / 10 + Value`
ao multiplicador de dano; W2PP tambem altera Damage em 15%, aplica
`MAX_DAMAGE=1000000000` e reduz MaxHp para 90%. O C# porta apenas a contribuicao
do alvo e mantem Damage/MaxHp inalterados, sem integrar o estagio ao coordenador.
O recorte [`character-login-possessed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-possessed.manifest.md)
fecha o `Affect.Type=24` (Samaritano) em sete vetores. Alvo e W2PP usam `add = Ac / 4 + Value`
e `Ac += add` sequencialmente, com truncamento inteiro; a diferenca de comentario
(`Possuido` no alvo e `Samaritano` no W2PP) nao representa divergencia de comportamento.
O `SkillData.csv` 7.69 confirma `Possuido -> Type=14` e `Samaritano -> Type=24`;
RegenHP e dano fisico nao sao alterados nesses branches. O C# porta a formula
comum como etapa pura, sem integrar o coordenador.
O recorte [`character-login-frozen-blade.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-frozen-blade.manifest.md)
fecha o `Affect.Type=7` (Lamina Congelada) em sete vetores: alvo e W2PP coincidem em subtrair
`Level / 10 + 10` de AttackSpeed e, quando `headItemIndex > 50`, `Level / 10 + 20` de Int.
O recorte [`character-login-weaken.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-weaken.manifest.md)
fecha o `Affect.Type=10` (Enfraquecer) em sete vetores: alvo e W2PP coincidem na subtracao
`Level / 5 + Value` de Damage.
O recorte [`character-login-meditation.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-meditation.manifest.md)
fecha o `Affect.Type=21` (Meditacao) em sete vetores: a reducao sequencial de AC `Level / 3 + 10`
coincide entre as sources, mas o incremento no multiplicador de dano diverge (`Level / 25 + Value`
no alvo contra `Level / 10 + Value` no W2PP).
O recorte [`character-login-golden-shield.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-golden-shield.manifest.md)
fecha o `Affect.Type=31` (Escudo Dourado) em sete vetores: o alvo soma `Level / 2 + Value` ao AC,
enquanto o W2PP nao possui o affect em seu loop de score (adicao nula).
O recorte [`character-login-elemental-protection-ac.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-elemental-protection-ac.manifest.md)
fecha a parcela de ArmorClass do `Affect.Type=25` em sete vetores: o alvo soma `Level / 2 + Value`
(+30 se `Level >= 255`) ao AC, enquanto o W2PP altera apenas as resistencias elementais sem tocar na defesa.
O recorte [`character-login-armor-class-reduction.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-armor-class-reduction.manifest.md)
fecha o `Affect.Type=12` em sete vetores: alvo e W2PP usam o mesmo fator `float`,
truncamento para `int` e mutacao sequencial. O C# projeta AC depois de Type=6.
O coordenador [`character-login-score-coordinator.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.manifest.md)
ordena os blocos auditados em snapshots: base/equipamento, Special, Type=16,
Type=11, Type=9, Type=15, Type=5, Type=6, Type=12, Haste, Soul e Kibita. Ele projeta AC/MaxHp/Damage entre etapas para preservar a
ordem observada na fonte; Critical, Attack/Run, equipamento, multiplier e buffs
nao portados continuam fora do commit. Quatro vetores cobrem Soul normal,
Kibita, resistencia clampada e gate de classe, sem integracao no login.
O bloco Haste coincide com W2PP e esta coberto por sete vetores, mas retorna
somente o delta/mask: nao muta MOB nem foi integrado ao fluxo. Ver
[`character-login-haste-affect.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-haste-affect.manifest.md).
Outros derivados/mutacoes de buffs e montarias continuam pendentes.
O helper comum 7.69 declara 18 slots, mas
TMSrv/W2PP/C# trabalham com 16; os indices 16/17 ficam fora deste recorte.

**Backlog:** prioridades, critérios de aceite e fontes externas candidatas ficam em
[`BACKLOG.md`](../../BACKLOG.md); esta matriz registra somente a rastreabilidade
por símbolo, wire, estado, teste e nível.

## Niveis

|  Nivel |               Significado                 |
| ------ | ----------------------------------------- |
|   C0   | nao inventariado                          |
|   C1   | referencia/simbolo legado localizado      |
|   C2   | parser ou wire parcial                    |
|   C3   | regra implementada e teste deterministico |
|   C4   | fluxo local/rede executado                |
|   C5   | cliente real validado                     |
|   C6   | build publicada e servico remoto validado |
|   C7   | aceite funcional com evidencia completa   |

## Auditoria comparativa classe a classe

O estagio vigente percorre cada classe ativa do C# contra as duas fontes locais:
7.69 como contrato-alvo e `Server/W2PP` como comparador obrigatorio. Cada lote
registra somente classes cujo codigo foi lido nas tres arvores; ausencia de
equivalente e uma conclusao valida, mas nunca implicita.

Para a auditoria, classificar cada tipo C# como contrato wire, modelo/regra de
dominio, caso de uso/estado de sessao, adapter de storage/dados, host/operacao ou
ferramenta. Golden byte-a-byte se aplica a contratos wire e adapters binarios;
regras de dominio recebem fixtures comportamentais, casos de uso recebem cenarios
de transicao/falha, e infraestrutura recebe testes de contrato/integracao isolada.
Um nome agrupado na matriz so conta como coberto depois de cada tipo do grupo ter
responsabilidade, decisao e evidencia identificadas.

### Ficha de auditoria: `MSG_CNFCharacterLogin` 7.69

O wire do cliente/header comum DBSrv e um DTO proprio: 1728 bytes no total,
`STRUCT_MOB` 1040, `ShortSkill` no offset absoluto 1062, `EXT1` 1080 e `EXT2`
1368. `CharacterLoginConfirmationV769` e o golden Win32 cobrem esse envelope.
`W2ppCharacterLoginV1Adapter` agora compoe os campos W2PP ja mapeados no payload
e um teste valida o frame inteiro; ambos continuam fora do listener. Esse e o contrato selecionado para o cliente 7.69:
o handler recebe `MSG_CNFCharacterLogin`, copia os 1040 bytes do MOB e usa os
offsets seguintes conforme esse header. O DBSrv comum tambem declara o mesmo layout.

A source do TMSrv privado 7.69 contradiz esse contrato: seu header local mede MOB
816/16 equipamentos e `MSG_CNFCharacterLogin` 2640 bytes; `DataServerPackageHandler`
envia `sizeof(MSG_CNFCharacterLogin)`. Apesar de declarar uma forma
`MSG_CNFClientCharacterLogin` de 1832 bytes, esse handler nao envia essa forma.
W2PP, por sua vez, realmente relaya `MSG_CNFClientCharacterLogin` de 1832 bytes a
partir do pacote interno de 2648. Nenhum desses tamanhos substitui o contrato 1728
que o cliente 7.69 declara e consome. Nao copiar o wire privado inconsistente do
TMSrv; a implementacao C# alvo usara o DTO cliente 1728 quando a integracao do
fluxo estiver completa.

| Bloco/campos | Comparacao W2PP -> cliente 7.69 | Decisao atual |
|---|---|---|
| `PosX/PosY`, `Slot`, `ClientID`, `Weather` | W2PP inicia a posicao a partir de SPX/SPY, mas escolhe depois um spawn vivo (cidade/guilda/treinamento e grid livre) e envia esse resultado. O client 7.69 copia PosX/PosY para `MOB.HomeTownX/Y`; portanto nao sao simplesmente o SPX/SPY persistido. C# ja tem `ResolveCharacterLoginPosition`; o mapper devera receber essa posicao separada. Slot e ID da sessao sao escalares; W2PP envia `CurrentWeather`, enquanto o host C# ainda nao tem clima. | Manter spawn como dado do caso de uso, slot/ID explicitos e Weather=0 ate portar clima; nao derivar PosX/Y apenas do blob. |
| Prefixo de `MOB` | `MobName`, Clan, Merchant, Guild, Class e Coin mapeiam por semantica. O client tem `Rsv` byte +21; W2PP tem ushort +22, portanto o adapter preserva o byte baixo e rejeita flags no byte alto. W2PP Quest byte +24 alarga para ushort +22. Exp permanece +32; SPX/SPY +40/+42 sao as coordenadas salvas, mas a resposta recebe o spawn vivo final separado. | Implementado em `W2ppCharacterMobV1Adapter`; spawn e parametro obrigatorio e deve ser resolvido pelo caso de uso. |
| `BaseScore` / `CurrentScore` | Ambos comecam em +44/+92 e medem 48 bytes. W2PP usa Level int e Merchant/AttackRun/Direction/ChaosRate; client usa Level short, Reserved/AttackRun e padding. Level e convertido checked, Reserved/padding sao zero; specials preservam os 16 bits. | Implementado no adapter; overflow de Level rejeitado e bytes comparados ao golden 7.69 compilado. |
| `Equip` / `Carry` | Equip comeca em +140 nos dois layouts, mas W2PP tem 16 itens e o client 18; copiar 0..15 e manter 16/17 em extensao versionada. Carry e 64 itens em ambos, deslocado de +268 para +284. | `CharacterMobV769`/`ClientEquipmentStateV769` cobrem os 18 itens; o `WorldHub` mantem a extensao separada, projeta os indices novos em CreateMob/UpdateEquip e muta slots 16/17 sem aumentar o blob W2PP. MariaDB usa `wyd_client_equipment_769` (migration 004); backend legado sem essa tabela continua com 16 + dois vazios. |
| skills e campos finais do MOB | W2PP `STRUCT_MOB` tem um `LearnedSkill` de 32 bits em +780; o client 7.69 tem dois uints em +796. O word 0 e copiado bit a bit. O client `IsValidSkill` consulta word 1 para skills 200..246; o TMSrv 7.69 tambem serializa `MOB.Learned[1]`. Tanto W2PP quanto 7.69 mantem `MOBEXTRA.SecLearnedSkill` separado de `MOB.Learned[1]`, com usos distintos no TMSrv; nao ha conversao entre eles. Como W2PP nao persiste a extensao de skills 7.69, o adapter inicializa word 1 em zero. `SkillBar[4]` mapeia a `MOB.ShortSkill[4]`. Magic uint32->byte, RegenHP/MP ushort->byte e bonus ushort->short sao checked; Critical, SaveMana, GuildLevel e Resist[4] mantem a representacao. Dummy e padding target sao zerados. | Mapeamento W2PP executado e testado: word 0 preservado, word 1 zerado como extensao indisponivel na origem. Nao copiar `SecLearnedSkill`; se as skills 200..246 forem implementadas, exigir estado canonico explicito. |
| nome/contadores/appearance | O client 7.69, ao entrar no Field, le chaos de `MobName[12]`, kills de `[13..15]` e termina o nome em `[12]`. W2PP `GetCreateMob` monta esse sufixo via `GetPKPoint`, `GetGuilty`, `GetCurKill` e `GetTotKill` a partir de `KILL_MARK` em Carry[63]. `CurrentKill/TotalKill` target tambem foram medidos em +1036/+1038. | Implementado para self-login: os 12 bytes do nome sao seguidos do sufixo e os mesmos valores preenchem os contadores explicitos. Chaos zera quando guilty valido, como no sender W2PP. |
| `EXT1.Data[0]` | Client le este int como FakeExp. W2PP `SendEtc` envia `extra.Hold`; TMSrv 7.69 envia o mesmo valor como `FakeExp`. | Mapeamento aprovado: `W2PP STRUCT_MOBEXTRA.Hold` (uint, +476) -> `EXT1.Data[0]` (int), com overflow rejeitado. `CharacterLoginExtensionsV769` e `W2ppCharacterLoginExtensionsV1Adapter` implementam somente esta parte, isolados. |
| `EXT1.Data[1..7]` e `EXT1.Affect[32]` | O handler de login do client so le Data[0], nao consome Affect nesse pacote. O `STRUCT_AFFECT` cliente e Type/Level/Value/Time (char/char/short/int); W2PP e Type/Value/Level/Time (byte/byte/ushort/uint). W2PP grava levels 500 e 2000, que nao cabem no Level char, logo copia crua ou truncamento corromperia semantica. O client declara `MSG_UpdateAffect` (`0x03B9`) como rota separada. | Data[1..7] e Affect ficam zero neste confirmation baseline; efeitos ativos usam adapter tipado e wire separado. |
| `UpdateAffectConfirmationV769` / `W2ppUpdateAffectV1Adapter` | cliente `Basedef.h::MSG_UpdateAffect` (`0x03B9`) | W2PP `MSG_SendAffect` (`0x03B9` com flags), `STRUCT_AFFECT` | Ambos medem frame 268/payload 256 com 32 entradas de 8 bytes, mas o cliente ordena `Type, Level, Value, Time` e W2PP `Type, Value, Level, Time`. O adapter troca os campos e rejeita `Level > 127` ou `Time > int.MaxValue`, em vez de truncar efeitos como 500/2000. | DTO + adapter + golden Win32 + teste byte a byte executados; relay integrado no login e em mutacoes com snapshot autoritativo; efeitos incompatíveis sao omitidos sem truncamento; E2E continua pendente |
| `EXT2` | 360 bytes contem Quest[12], LastConnectTime, SubClass[2], ItemPassWord, ItemPos, SendLevItem, AdminGuildItem e Dummy. O handler client de CharacterLogin nao le Ext2; `STRUCT_MOBEXTRA` W2PP nao e layout equivalente e inclui dados que nao devem ser vazados por copia bruta. | Ext2 zerado ate existir consumidor e mapeamento por campo; sem copiar MOBEXTRA para ele. |

O caminho 7.69 nao fecha um oracle runtime: o handler DBSrv deixa Ext1/Ext2 sem
copia e sem inicializar o struct local; os headers privados do TMSrv ainda usam
MOB816/16 equipamentos e tamanhos diferentes do cliente/DBSrv comum. Essa
divergencia e evidência de defeito/incompletude da source, nao uma regra a
reproduzir. W2PP tambem distingue seu pacote interno de 2648 bytes do relay real
de 1832 bytes; o encoder C# legado envia o interno. O DTO/adapter do MOB e seu
golden Win32 de 1040 bytes agora existem, mas continuam isolados. A extensao
7.69 `LearnedSkill[1]` e explicitamente zerada na projecao W2PP, pois a origem nao
tem esse word e `MOBEXTRA.SecLearnedSkill` e distinto. `W2ppCharacterLoginV1Adapter`
compoe MOB, short skills, spawn, slot/ID/clima e FakeExp no confirmation 7.69; um
teste cobre os blocos, alinhamento e frame codificado. `LegacyHpMpMath` agora
porta isoladamente `BASE_GetHpMp`, com vetores por classe. Para integrar, ainda e
preciso portar/validar os demais recalculos e mutacoes que TMSrv aplica ao MOB,
migrar coordenadamente os outros wires de login/selecao e validar captura/runtime
e E2E. Affect requer wire separado; EXT2 permanece zerado.

| Classe C# | Referencia 7.69 | Comparador W2PP | Decisao | Estado |
|---|---|---|---|---|
| `PacketHeader` | `Servidor/Source/Code/Basedef.h::_MSG` | `Source/Code/Basedef.h::_MSG` | manter o header de 12 bytes (`Size`, `KeyWord`, `CheckSum`, `Type`, `ID`, `ClientTick`). | auditado; teste de header executado |
| `LegacyFrameCodec` | `Servidor/Source/Code/CPSock.cpp` | `Source/Code/CPSock.cpp` | manter `INITCODE 0x1F11F311`, cifra, checksum e limite de 8192 bytes comuns. | auditado; testes de cifra/checksum executados |
| `LegacyFrameStream` / `DecodedFrame` | `Servidor/Source/Code/CPSock.cpp` | `Source/Code/CPSock.h/.cpp` | manter o acumulador de stream; `DecodedFrame` e wrapper .NET do header, payload e checksum, sem objeto C++ equivalente. | auditado; teste de fragmentacao executado |
| `SerializedNetworkStream` | `CPSock::AddMessage/SendMessageA` em `Servidor/Source/Code/CPSock.cpp` | mesmos metodos em `Source/Code/CPSock.cpp` | **Adaptador arquitetural, nao port 1:1.** Nas duas sources, `AddMessage` grava frames cifrados num buffer por socket e atualiza `Size`/`KeyWord`/`CheckSum`/`ClientTick`; `SendMessageA` conserva `nSentPosition` e compacta o restante com `RefreshSendBuffer` apos envio parcial. O C# envolve cada `NetworkStream` uma vez e serializa `Write`/`WriteAsync` via `SemaphoreSlim`; o `Program` usa esse wrapper nas respostas e broadcasts. Nao replica batching, buffer fixo nem `nSentPosition`; os metodos C++ observados tambem nao mostram lock proprio. Manter a adaptacao para o modelo concorrente .NET, sem copiar a fila legada; a integridade de dois writes concorrentes foi provada em loopback: os blocos chegam completos em uma ordem ou na outra, sem intercalacao. | comparacao estatica concluida nas duas sources; chamadas TX encaminhadas pelo wrapper; teste concorrente de frames/ordem executado |
| `LegacyKeywordTable` | `Servidor/Source/Code/CPSock.cpp::pKeyWord[512]` | `Source/Code/CPSock.cpp::pKeyWord[512]` | manter tabela de 512 bytes e transformacao CPSock. | auditado; teste de tabela/cifra executado |
| `ClientTickPolicy` | `Servidor/Source/Code/Basedef.cpp` | `Source/Code/Basedef.cpp`, `TMSrv/ProcessClientMessage.cpp` | manter: `SKIPCHECKTICK` recebido do cliente e invalido. | auditado; teste de tick executado |
| `AccountLoginRequest` | `Basedef.h::MSG_AccountLogin`, `TMSrv/_MSG_AccountLogin.cpp` | `Basedef.h::MSG_AccountLogin`, `TMSrv/_MSG_AccountLogin.cpp` | manter wire de 116 bytes e campos. A release propria exige `ClientVersion == 7670`: a 7.69 deixa a comparacao comentada e W2PP a exige para seu valor legado; a constante 7.670 e politica explicita desta migracao, nao valor copiado de nenhuma das duas. O parser permanece neutro e `ClientReleasePolicy` aplica a admissao antes da autenticacao. | auditado; parser e politica de aceitar 7670/rejeitar demais testados |
| `ClientReleasePolicy` | `APP_VERSION=1059`; comparacao de `ClientVersion` comentada em `TMSrv/_MSG_AccountLogin.cpp` | `APP_VERSION=7640`; comparacao de versao ativa no mesmo handler, exceto no ramo `_PACKET_DEBUG` | classe de politica sem equivalente 1:1; manter o requisito do produto `7670` no host, antes de autenticar, sem copiar os valores das fontes. `AccountLoginRequest` continua aceitando sintaticamente outras versoes para separar parse de admissao. | diferenca comparada nas duas fontes; `Program` aplica antes da autenticacao; teste de aceitar 7670/rejeitar demais existente |
| `AccountLoginConfirmation` | cliente `Projects/TMProject/Basedef.h::MSG_CNFAccountLogin`; servidor comum/DBSrv `Servidor/Source/Code/Basedef.h::MSG_DBCNFAccountLogin`; encaminhamento em `TMSrv/DataServerPackageHandler.cpp` | `Basedef.h::MSG_DBCNFAccountLogin`, `TMSrv/ProcessDBMessage.cpp` | **Divergencia bloqueadora medida:** cliente 7.69 = 1928 bytes (`SecretCode` +12, `SelChar` +32, `Cargo` +936, `Coin` +1896, `AccountName` +1900, `SSN1` +1916). Header comum/DBSrv = 1992; TMSrv local = 1928, mas com selecao antiga de 840 e Cargo +872 (128 itens), logo mesmo tamanho nao significa mesmo contrato. W2PP = 2008; C# legado emitia 2000 e serializava campos W2PP extras. | encoder 7.69 integrado ao listener; autenticacao, sessao e wire de falha preservados; E2E cliente ainda pendente |
| `AccountLoginConfirmationV769` / `W2ppAccountLoginV1Adapter` | `Projects/TMProject/Basedef.h::MSG_CNFAccountLogin`; leitura em `TMSelectServerScene.cpp`; resposta DB em `DBSrv/CFileDB.cpp` | `Basedef.h::MSG_DBCNFAccountLogin`, `TMSrv/ProcessDBMessage.cpp` | DTO cliente separado: payload 1916/frame 1928; offsets relativos ao payload SecretCode +0, padding +16..19, selecao +20, cargo +924, Coin +1884, AccountName +1888, SSN1 +1904, SSN2 +1908, padding final +1912..15. Golden MSVC Win32 usa sentinelas sinteticas. Adapter copia selecao/cargo 0..119/Coin/nome; rejeita carga nao vazia 120..127; nao equipara `HashKeyTable` a `SecretCode` e projeta SecretCode/SSNs como zero, conforme o pacote alvo zero-inicializado e os campos nao atribuidos pelo handler DBSrv. `QuestDiaria`, `Unknown28`, Keys e bloqueio ficam fora do wire cliente. A medicao e do header cliente; DBSrv comum 7.69 e TMSrv privado ainda divergem entre si. | DTO + adapter + golden byte a byte + teste de frame 1928 integrado ao listener; E2E cliente pendente |
| `LegacyDailyQuest` | `STRUCT_QUEST` existe no servidor 7.69, mas nao integra `MSG_CNFAccountLogin` do cliente/servidor raiz | mesmos campos em `Basedef.h::STRUCT_QUEST`, incluidos em `MSG_DBCNFAccountLogin` | manter o DTO de 52 bytes somente para persistencia/fluxos que comprovem esse formato; nao serializar os 52 bytes no login 7.69 sem um wire separado. No header raiz 7.69, a mensagem de login termina em SSN1/SSN2; a copia local de `TMSrv` nao e o contrato compilado pelo projeto. | diferenca estatica confirmada; uso de quest fora do login ainda precisa de rastreabilidade |
| `AccountSecureRequest` | `Basedef.h::MSG_AccountSecure`, `TMSrv/_MSG_AccountSecure.cpp`, `TMSrv/CFileDB.cpp` | mesmos simbolos | manter wire de 32 bytes e PIN de 6 bytes; `Unknown[10]` nao participa da regra nas fontes. | auditado; parser/coordinator testados |
| `AccountSecureSignal` | `TMSrv/DataServerPackageHandler.cpp::_MSG_AccountSecure/_MSG_AccountSecureFail` | `TMSrv/ProcessDBMessage.cpp::_MSG_AccountSecure/_MSG_AccountSecureFail` | manter os sinais sem payload, tipos `0x0FDE`/`0x0FDF` e `ESCENE_FIELD`; C# elimina o hop DB. | auditado; encoder/coordinator testados |
| `AccountLoginCoordinator` / `AccountLoginOutcome` / `AccountLoginFailureNotice` | `TMSrv/_MSG_AccountLogin.cpp` mais resposta DB em `CFileDB.cpp`/`DataServerPackageHandler.cpp` | `TMSrv/_MSG_AccountLogin.cpp` mais `DBSrv/CFileDB.cpp`/`ProcessDBMessage.cpp` | sao coordenacao e resultados C# sem classe legada unica: manter autenticacao antes de `USER_SELCHAR` e fechar em falha; a versao obrigatoria 7670 e aplicada pelo host via `ClientReleasePolicy`, antes da autenticacao. | auditoria estatica concluida; parser/politica testados; E2E 7.670 pendente |
| `AccountSecureCoordinator` / `AccountSecureStatus` / `AccountSecureOutcome` | `TMSrv/_MSG_AccountSecure.cpp`, `TMSrv/CFileDB.cpp::_MSG_AccountSecure` e sinais DB | mesmos handlers em TMSrv, `DBSrv/CFileDB.cpp` e `ProcessDBMessage.cpp` | preservar a ordem: troca sem PIN validado fica silenciosa; PIN ainda nao definido aceita configuracao; verificacao compara 6 bytes; troca exige sessao validada. `SecureVerified` e estado de sessao C#, sem objeto legado equivalente. | auditoria estatica concluida; fluxos e sinais testados |
| `LegacyItem` | `Basedef.h::STRUCT_ITEM` | `Basedef.h::STRUCT_ITEM` | manter os 8 bytes (indice + tres pares efeito/valor). E tipo compartilhado de wire, nao uma regra de inventario. | auditado na estrutura; testes de serializacao existentes |
| `LegacyScore` | cliente `Projects/TMProject/Basedef.h::STRUCT_SCORE`; servidor comum/DBSrv `Servidor/Source/Code/Basedef.h::STRUCT_SCORE` | `Basedef.h::STRUCT_SCORE` | **Mesmos 48 bytes, sem contrato semantico comum.** Cliente: `Level` short em +0, `Ac` +4, `Damage` +8, `Reserved` +12, `AttackRun` +13, alinhamento +14..15, demais valores +16..47. Header comum 7.69 usa unions/bitfields nos bytes +12/+13; TMSrv 7.69/W2PP/C# usam `Level` int e `Merchant/AttackRun/Direction/ChaosRate`. O C# ativo permanece DTO W2PP; `ClientScoreV769` e separado. | offsets medidos nos quatro headers; score cliente coberto no golden `STRUCT_SELCHAR`; golden W2PP standalone pendente |
| `LegacyCharacterSelection` / `LegacyCharacterSlot` | cliente `Projects/TMProject/Basedef.h::STRUCT_SELCHAR`, `TMHuman.cpp` e `TMFieldScene.cpp` (`NewSlot1/2`); servidor comum/DBSrv `Servidor/Source/Code/Basedef.h::STRUCT_SELCHAR` e `DBGetSelChar` | `Basedef.h::STRUCT_SELCHAR` e DB equivalente | **Divergencia bloqueadora medida:** cliente e DBSrv comum 7.69 usam coordenadas unsigned, Score[4], Equip[4][18], tamanho 904 e Equip no +272; TMSrv 7.69/W2PP/C# usam 16 slots e tamanho 840. W2PP `DBGetSelChar` contem typo que grava SPY em SPX; 7.69 projeta SPX/SPY corretamente, como o reader C# atual. O golden 7.69 valida os 904 bytes; regras de gameplay/persistencia/relay de 16/17 continuam abertas. | reader e projecao antigos permanecem W2PP; serializer versionado e golden 7.69 testados em classes separadas |
| `CharacterSelectionV769` / `ClientScoreV769` / confirmations New/Delete / `W2ppCharacterSelectionV1Adapter` | `Projects/TMProject/Basedef.h::STRUCT_SELCHAR`, `STRUCT_SCORE`, `MSG_CNFNewCharacter`, `MSG_CNFDeleteCharacter`; `DBSrv/CFileDB.cpp::DBGetSelChar` | `LegacyCharacterSelection`/`LegacyScore` C# que representam W2PP | DTO wire 7.69: selection 904 bytes com offsets Score +80, Equip +272, Guild +848, Coin +856 e Exp +872; golden Win32 sintetico cobre todos os campos e 18 equipamentos. New/Delete acrescentam 4 bytes x86 apos MSG_STANDARD: SelChar +16, frames 920. Adapter preserva bits das coordenadas, valida Level int->short, mantem AttackRun/atributos comuns, zera Reserved, copia 16 itens e deixa 16/17 vazios; Merchant/Direction/ChaosRate sao descartados por nao terem equivalente 7.69. Nomes so sao projetados se ASCII, sem NUL e <=16 bytes. A confirmacao de AccountLogin e os sucessos de New/Delete estao ligados ao listener; E2E cliente permanece pendente. | MSVC Win32 golden byte a byte; testes de frames New/Delete 920 e projecao passaram |
| `CreateCharacterRequest` | `Basedef.h::MSG_CreateCharacter`, `TMSrv/_MSG_CreateCharacter.cpp` | mesmos simbolos | manter wire de 36 bytes e validacao de nome/estado; C# substitui o hop `USER_WAITDB` por `USER_CHARWAIT` enquanto persiste no proprio coordinator. | auditado; parser/coordinator e fixture executados |
| `NewCharacterConfirmation` | `Projects/TMProject/Basedef.h::MSG_CNFNewCharacter` / `STRUCT_SELCHAR` 7.69; sender DB em `DBSrv/CFileDB.cpp` e relay em `TMSrv/DataServerPackageHandler.cpp` | `Basedef.h::MSG_CNFNewCharacter` / `STRUCT_SELCHAR` W2PP | cliente/DBSrv comum 7.69 medem 920 bytes, `SelChar` no offset 16. O padding x86 torna incorreta a soma simples 12+904=916. TMSrv 7.69/W2PP medem 856 (`SelChar` +16, 16 slots); o encoder C# legado ativo emite 852 com `SelChar` +12. `NewCharacterConfirmationV769` serializa a estrutura medida e o listener usa o frame alvo no sucesso. | probe Win32/golden selection e frame 920 testados; falha/E2E pendentes |
| `NewCharacterFailSignal` | `TMSrv/_MSG_CreateCharacter.cpp`, `TMSrv/DataServerPackageHandler.cpp` | `TMSrv/_MSG_CreateCharacter.cpp`, `TMSrv/ProcessDBMessage.cpp` | manter sinal vazio `0x011A`; C# responde diretamente sem hop DB. | auditado; encoder/coordinator testados |
| `DeleteCharacterRequest` | `Basedef.h::MSG_DeleteCharacter`, `TMSrv/_MSG_DeleteCharacter.cpp` | mesmos simbolos | manter wire de 44 bytes, nome e senha; C# preserva a identidade da sessao e nao confia no nome para selecionar o slot. | auditado; parser/coordinator e fixture executados |
| `DeleteCharacterConfirmation` | `Projects/TMProject/Basedef.h::MSG_CNFDeleteCharacter` / `STRUCT_SELCHAR` 7.69; sender DB e relay TMSrv | `Basedef.h::MSG_CNFDeleteCharacter` / `STRUCT_SELCHAR` W2PP | Mesmo layout medido de NewCharacter: cliente/DBSrv comum 920 bytes com `SelChar` +16; TMSrv/W2PP 856 com 16 slots; C# ativo 852 com selecao +12. `DeleteCharacterConfirmationV769` usa o envelope compartilhado de 4 bytes e o listener usa o frame alvo no sucesso. | probe Win32 static_assert e fixture do corpo; teste de frame passou; falha/E2E pendentes |
| `DeleteCharacterFailSignal` | `Basedef.h::_MSG_DeleteCharacterFail`, handler DB de delete | mesmos simbolos | manter sinal vazio `0x011B`; C# responde diretamente sem hop DB. | auditado; encoder/coordinator testados |
| `CharacterLoginRequest` | cliente `Projects/TMProject/Basedef.h::MSG_CharacterLogin` e `TMSelectCharScene.cpp`; servidor `Basedef.h`/`TMSrv/_MSG_CharacterLogin.cpp` | `Basedef.h::MSG_CharacterLogin`, `TMSrv/_MSG_CharacterLogin.cpp` | o cliente 7.69 declara 36 bytes (`Header + Slot + Force + SecretCode[16]`) e envia uma instancia zerada; os dois servidores declaram 20 bytes e o handler consome `Slot`/`Force`, encaminhando apenas `sizeof(MSG_CharacterLogin)` ao DB, sem usar `Force` nem o tail. C# aceita 20 ou 36 e ignora os 16 bytes finais; logo 36 nao e exclusivo do adaptador 7.60, mas o nome/semantica `SecretCode` do contrato 7.69 ainda nao esta modelado. Nao rejeitar a forma 7.69; se a semantica mudar, tratar o campo explicitamente. | comparacao estatica cliente 7.69/servidor 7.69/W2PP concluida; parser testa formas 20 e 36 com tail zero; captura runtime 7.670 pendente |
| `LegacyHpMpMath` / `BASE_GetHpMp` | `Servidor/Source/Code/Basedef.cpp` e `TMSrv/DataServerPackageHandler.cpp`; `TMSrv.vcxproj` compila `..\\Basedef.cpp`, nao a copia local `TMSrv/Basedef.cpp` | `W2PP/Source/Code/Basedef.cpp::BASE_GetHpMp` | Corpo da rotina e incrementos por classe coincidem; `BaseSIDCHM` diverge para FM e HT: o helper 7.69 selecionado usa FM `{5,10,5,5,60,65}` e HT `{8,9,13,6,70,55}`, enquanto W2PP usa FM `{5,8,5,5,60,65}` e HT `{8,9,13,6,75,60}`. O helper selecionado pelo projeto 7.69 usa os limites do header comum (`MAX_HP=400000`, `MAX_MP=100000`); W2PP usa `1000000000` para ambos. A copia TMSrv local nao compilada coincide com W2PP e diz `100000` para HP e MP. O calculo so atualiza BaseScore/CurrentScore.MaxHp/MaxMp e preserva HP/MP atuais. A incompatibilidade geral de ABI TMSrv/common impede chamar isto de oracle runtime. | `LegacyHpMpMath` isolado; 12 vetores fonte-derivados cobrem 4 classes x Mortal/Arch/Celestial, alem de caps/entrada invalida. Usa explicitamente as tabelas/limites do helper comum selecionado, sem adotar valores W2PP onde divergem. Nao ligado ao adapter/listener; demais mutacoes de login pendentes |
| `CharacterLogoutRequest` | `Basedef.h::_MSG_CharacterLogout`, `TMSrv/_MSG_CharacterLogout.cpp` | mesmos simbolos | manter `MSG_STANDARD` vazio; persistencia e transicao de sessao ficam no coordinator C#. | auditado; wire/transicoes testados |
| `CharacterLogoutConfirmation` | `Basedef.h::_MSG_CNFCharacterLogout`, `TMSrv/_MSG_CharacterLogout.cpp` | mesmos simbolos | manter sinal vazio `0x0116`, com ID da conexao como no legado. | auditado; wire/transicoes testados |
| `CharacterLoginCoordinator` / `CharacterLoginStatus` / `CharacterLoginOutcome` / `LegacyCharacterLoginData` | `TMSrv/_MSG_CharacterLogin.cpp`, `CFileDB.cpp::_MSG_DBCharacterLogin`, `DataServerPackageHandler.cpp` | `TMSrv/_MSG_CharacterLogin.cpp`, `DBSrv/CFileDB.cpp::_MSG_DBCharacterLogin`, `ProcessDBMessage.cpp` | mescla os dois processos legados; manter a validacao do slot antes da transicao para `USER_CHARWAIT` e a leitura do personagem. Em ambas as sources, slot fora da faixa envia a mensagem `_NN_SelectCharacter` antes de retornar; C# restaura `CharacterSelection`, mas nao envia resposta. Separar essa diferenca de mensagem das falhas posteriores do DB, cuja resposta deve ser rastreada por ramo. O C# aplica o marcador inicial de Kill Mark ao MOB, comportamento de inicializacao que o handler legado executa apos carregar o personagem. | comparacao estatica concluida; parser/coordinator testados; diferenca de mensagem para slot invalido registrada; cliente real pendente |
| `CreateCharacterCoordinator` / `CreateCharacterStatus` / `CreateCharacterOutcome` | `TMSrv/_MSG_CreateCharacter.cpp`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter` | mesmos simbolos/ordem em TMSrv e DBSrv | as duas sources validam slot/classe/PIN/nome/ocupacao, reservam o nome global, inicializam MOB/extra/affect/ShortSkill e gravam a conta; reserva anterior a falha de escrita tambem pode deixar artefato no legado. A diferenca e concorrencia: DBSrv despacha os handlers sincronamente no loop central; C# dispara `InspectConnectionAsync` por conexao. A reserva MariaDB e autocommit separado, enquanto o blob e slot sao revalidados em transacao serializavel; uma criacao concorrente que perde o mesmo slot deixa sua reserva sem personagem. O template C# tambem e lido depois da reserva; `TemplateUnavailable` deixa reserva orfa. | sequencia comparada nas duas sources; fixtures sequenciais existentes; corrida de mesmo slot/diferentes nomes e falha de template/escrita precisam de testes deterministas; nao equivaler a atomicidade do legado |
| `DeleteCharacterCoordinator` / `DeleteCharacterStatus` / `DeleteCharacterOutcome` | `TMSrv/_MSG_DeleteCharacter.cpp`, `DBSrv/CFileDB.cpp::_MSG_DBDeleteCharacter` e resposta DB | mesmos simbolos/ordem em TMSrv e DBSrv | ambas as sources comparam a senha, removem a reserva global do nome, limpam ShortSkill, chamam `BASE_ClearMob/BASE_ClearMobExtra`, gravam a conta e retornam selecao; os bloqueios por classe/level estao comentados. A remocao do nome precede `DBWriteAccount` no legado e o retorno da escrita e ignorado. C# File grava/limpa o blob antes de remover o arquivo do nome (ordem oposta); MariaDB atualiza blob e remove registro na mesma transacao, mas tambem limpa Affect, que os handlers C++ nao limpam neste caminho. | wire/refresh e defaults cobertos; divergencias estaticas de ordem/affect registradas; teste de falha entre etapas e politica de estado persistido pendentes |
| `LegacyCharacterName` | `Basedef.cpp::BASE_CheckValidString`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter`, `ConfigIni.cpp::ReadFilterName` | `Basedef.cpp::BASE_CheckValidString`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter` | lista reservada comum, faixa 4..15 e comandos/COMn/LPTn coincidem. A validacao C++ permite pares de bytes altos ao avancar dois bytes; o parser C# decodifica ASCII, converte esses bytes em `?` e os rejeita, portanto nao preserva nomes multibyte. 7.69 acrescenta filtro configuravel `FilterName/block.json` ausente no W2PP; o JSON gerado por default vem com `ATIVO=0`, logo o efeito padrao e inativo, mas C# nao tem o adapter de configuracao. | casos ASCII testados; diferenca multibyte e capacidade de filtro configuravel registradas; politica 7.670/config ativa e testes de byte pendentes |
| `LegacyCharacterTemplateStore` | `DBSrv/Server.cpp::ReadBaseMob`, `ConfigIni.cpp::ReadbaseMob`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter`; cliente `TMHuman.cpp` | `DBSrv/Server.cpp` le `BaseMob/TK|FM|BM|HT`; `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter` | 7.69 carrega `Config/baseMob.json`, mas a configuracao e o TMSrv local ainda declaram 16 equipamentos; o cliente, por outro lado, desenha os grids 16/17 como `NewSlot1/2`. Cliente/DBSrv `STRUCT_MOB` = 1040, Equip +140, Carry +284; TMSrv/W2PP = 816, Carry +268. C# le templates W2PP com stride 816; nao assumir zeros/defaults para as posicoes novas como regra final ate rastrear create, login, save e visualizacao. | divergencia de capacidade confirmada; fixture atual sintetica; mapa de inicializacao e elegibilidade dos slots 16/17 pendentes |
| `LegacyCharacterStorageDefaults` | `Basedef.cpp::BASE_ClearMob/BASE_ClearMobExtra`, `TMSrv/ProcessDBMessage.cpp` inicializacao de Kill Mark | mesmos metodos/etapa de login | manter defaults persistidos do slot vazio em 2112,2112 e `ClassMaster=MORTAL`, alem de Kill Mark inicial ausente; a rotina agrega trechos de pontos diferentes do legado. | auditoria estatica concluida; fixtures de selecao/delete testadas |
| `LegacyAccountSnapshot` | `DBSrv/CFileDB.cpp::DBReadAccount/DBGetSelChar`, `Basedef.h::STRUCT_ACCOUNTFILE` | mesmos simbolos em `DBSrv/CFileDB.cpp` e `Basedef.h` | **Sao formatos de storage diferentes, nao uma extensao simples de slots.** W2PP: `sizeof=7952`, Char +216, Cargo +3480, Coin +4504, `IsBlocked` +7944; o parser C# aceita minimo de 7945 bytes e le MOB816/16 equips/Cargo128. Header comum DBSrv 7.69: `sizeof=8792`, Char +216, Cargo +4376, Coin +5336, Donate +8640, extensao opaca de 8 bytes antes de TempKey +8652, `IsBlocked` +8784; MOB1040/18 equips/Cargo120. Nao adotar o blob cru 7.69 como modelo canonico nem deslocar campos por aritmetica; manter `W2ppAccountFileV1` isolado para compatibilidade/importacao e preferir storage C# versionado. | ABI `sizeof/offsetof` medida; offsets W2PP/7.69 diferem; adapter/importacao e golden sintetico de storage pendentes |
| `IAccountStore` / `ICharacterStore` / `IAccountSnapshotStore` / `LegacyFileAccountStore` / `MariaDbWorldCharacterStore` | `STRUCT_ACCOUNTFILE`, `DBSrv/CFileDB.cpp` e `DBGetSelChar` | `STRUCT_ACCOUNTFILE`, `DBSrv/CFileDB.cpp` e `DBGetSelChar` | contratos/adapters nao correspondem a classes C++ individuais. A persistencia C# atual usa layout W2PP versionado implicitamente (blob MariaDB de 7945 bytes; parser minimo 7945, embora `sizeof` nativo seja 7952), nao o arquivo bruto 7.69 de 8792 bytes. Isolar o formato atual como `W2ppAccountFileV1Adapter`; um importador 7.69 so sera criado se houver requisito de migracao e teste. Persistencia nova deve mapear entidade canonica via adapter, sem expor offsets no dominio. | adapters atuais auditados; versao do contrato precisa ficar explicita; golden de storage e concorrencia deterministica pendentes |
| `AccountAuthenticationStatus` / `AccountAuthenticationResult` / `DeleteCharacterStoreResult` / `CharacterPositionSaveResult` / `CharacterStateSaveResult` / `CharacterGuildSaveResult` / `CharacterShortSkillSaveResult` / `DonateBalanceSaveResult` | retornos booleanos/estados de `DBSrv/CFileDB.cpp` e handlers | mesmos retornos/handlers | tipos de resultado C# para coordenacao e adapters, sem DTO C++ homonimo; preservar a traducao para as confirmacoes/falhas wire, nao os numeros internos do adapter. | contratos locais mapeados; matriz de falhas por operacao pendente |
| `MariaDbConnectionOptions` / `MariaDbConnectionFactory` | sem equivalente: conexao MariaDB configurada pelo novo host | sem equivalente: conexao MariaDB configurada pelo novo host | manter fora do dominio legado; configura provider a partir de JSON, cria conexoes parametrizadas e nao deve expor credenciais em log/docs. | fluxo estatico auditado; provider/config de cada ambiente nao validado nesta auditoria |
| `IDonateBalanceStore` / `IDonatePixStore` / `IDonateCatalogStore` / `MariaDbDonateShopStore` | saldo/cadastro nas rotinas DB 7.69, import de catalogo comentado | saldo/import DB ativo, sem adapter C# equivalente | persistencia de saldo so continua se integrar a conta Cash do Site; catalogo/painel de compra in-game fica estacionado. Definir fonte de verdade, transacao e entrega no bau antes de reativar qualquer adapter comercial. | adapter analisado; schema ativo/atomicidade e contrato Site pendentes |
| `ServerWireLog` / `TeeTextWriter` | sem equivalente: logging de host novo | sem equivalente: logging de host novo | manter como observabilidade, nunca como parte do protocolo. Divergencia de seguranca: o redator atual nao inclui `DeleteCharacterRequest` (`0x0211`, contem `AccountPassword`) nem `AccountLoginConfirmation` (`0x010A`, inclui chaves e `BlockPassword`); ambos podem ter payload gravado em claro. Corrigir a classificacao antes de manter logs em ambiente com contas reais. | auditoria estatica; redacao incompleta confirmada; sem teste de segredo nos logs |
| `ServerStatusFilePublisher` | sem equivalente: health/status do projeto, fora do protocolo do servidor 7.69 | sem equivalente: health/status do projeto, fora do protocolo W2PP | manter o adapter separado: atualiza `servtest.htm` na inicializacao/cadencia de 60 s, preserva `0` jogadores e marca `-1` no shutdown cooperativo; timestamp fica em `.wyd-status`. A expiracao apos 120 s pertence ao watchdog PowerShell, nao ao processo C#. O watchdog agora consulta e grava `values[slot]`, mantendo slots independentes. | implementacao e teste local executados; script/live site ainda nao sao E2E |
| `ServerOptions` | sem equivalente: argumentos/configuracao do host C# | sem equivalente: argumentos/configuracao do host C# | manter como configuracao operacional, sem misturar com politica de wire/dominio. Validacao de paridade dos argumentos com deployment continua em checklist proprio. | parser auditado; cobertura integral de combinacoes pendente |
| `Program` / `MainForm` (`WydCdk.Server.ControlPanel`) | sem equivalente: UI WinForms nova | sem equivalente: UI WinForms nova | manter fora do core e tratar como host local. O core aceita `stop`/`shutdown` pela entrada padrao e executa o `finally`/status offline; o painel solicita essa parada e usa `Process.Kill` somente apos timeout de cinco segundos. | encerramento cooperativo implementado; build/UI e fluxo real do painel pendentes |
| `ClientPatcher` (`PatchDefinition`, `NativeHookPayload`, `X86Emitter`, `RelFixup`, `AbsoluteFixup`, `PeSection`, `PeLayout`) | sem equivalente no servidor 7.69; utilitario nativo historico do checkout C# | sem equivalente no W2PP servidor | estacionar como ferramenta de laboratorio/historico. O projeto esta listado na solution, mas nao e referencia do core; nao distribuir nem executar contra o cliente-alvo 7.670, cuja estrategia aprovada e source compilada sem DLL/hooks. | escopo auditado; exclusao do pacote de release precisa de gate automatizado/documentado |
| `SetShortSkillRequest` | `Basedef.h::MSG_SetShortSkill`, `TMSrv/_MSG_SetShortSkill.cpp` | mesmos simbolos/handler | manter os 20 bytes de payload: `SkillBar[4]`, `ShortSkills[16]`; ambos os legados apenas copiam os vetores ao estado do personagem. O C# persiste de imediato depois de validar `USER_PLAY`, decisao de durabilidade que nao altera o wire. | auditado; parser e persistencia testados |
| `PkInfoConfirmation` | `Basedef.h::_MSG_PKInfo` como `MSG_STANDARDPARM`, `TMSrv/_MSG_PKMode.cpp` | mesmos simbolos/handler | manter frame de 16 bytes com `state` int32 e ID do alvo, multicast na grade. O calculo comum cobre culpa/PK/castelo/torre/NewbieEvent; a extensao RvR existe no W2PP, nao na 7.69. Action-origin usa esse estado para sincronizar um jogador que entra na visao dos dois lados; os demais fluxos PK seguem suas fichas proprias. | wire e relay Action-origin testados por TCP; RvR permanece fora do alvo |
| `ActionRequest` | `Basedef.h::MSG_Action`, `TMSrv/_MSG_Action.cpp` | mesmos simbolos | manter layout de 52 bytes e opcodes `Action`/`Action2`/`Action3`. O DTO preserva `WireType`; HP==0 tem precedencia sobre score de velocidade ausente e envia HpMode20/Mode22 +5/type3; estado WorldHub ausente ou HP/AttackRun ausente para personagem vivo rejeita. Speed permitido e `CurrentScore.AttackRun & 0xF`; excesso soma `5/type4` no contador da sessao e corrige somente Speed antes do gate de range; depois de somar, revalida USER_PLAY para nao continuar apos logout por threshold. O side effect 7.69 de `Speed>=20` ou `AttackRun>=300` marca Analysis e fecha antes do relay; auditoria do log `SaveInfoDetect` esta em MOVE-ACTION-ANALYSIS-005. O gate de origem MOVE-ACTION-RECALL-001 precede velocidade/alcance: setores (9,1)/(8,2)/(10,2) para classMaster 3/4/5 com level>=180, ou setor (13,15) quando `MOB.Clan==0`. W2PP comenta o gate Celestial e nao tem o gate sem clan; 7.69 e a autoridade. `TryRecallForActionOriginWithViews` captura a visao de origem/destino na mesma mutacao protegida por lock; `LegacyActionOriginRecallRelay` envia Action apenas a visiveis, remove jogadores que deixam a janela e envia CreateMob/PKInfo a ambos os lados na entrada. O fluxo nao usa `BroadcastAsync`. Destino fora da janela inclusiva `VIEWGRID=33` e rejeitado pela posicao autoritativa; somente >66 em qualquer eixo envia Action3/Speed6 privado. Movimento aceito relaya ao autor e a jogadores na janela 33x33. `LegacyMovementMapRules`/`WorldHub` aplicam `0x80`/`0x20`, `BASE_GetGuild` para cinco cidades e `DoRecall`; indices46/47 de `Language.txt` precedem o recall por mapa. | Engine.Tests 299 PASS/0 FAIL, incluindo 11 vetores de origem e teste da captura espacial; build 0/0. TCP quatro contas confirmou Action/RemoveMob/CreateMob/PKInfo, negou relay distante e verificou posicoes no logout; `-ActionRange` regressou. Outros recalls/teleportes e E2E visual permanecem pendentes |
| `MotionRequest` / `SetHpModeConfirmation` | `Basedef.h::MSG_Motion`, `Basedef.h::MSG_SetHpMode`, `TMSrv/_MSG_Emotion.cpp`, `SendFunc.cpp::SendHpMode` | `TMSrv/_MSG_Motion.cpp`, demais simbolos iguais | manter Motion20/relay; HP==0 ou fora USER_PLAY soma4/type6 por sessao; HpMode20/Mode22 privado somente playing; HP negativo relaya | MOVE-CRACK-001:263 PASS/build0/0; rede HP0/-1, Accepted/Selection, shared-counter e carry/cargo/Gold/relogin; sandbox runtime-only publicado; visual/threshold TCP pendentes |
| `LoginSessionRegistry` | `pUser[conn].Mode`, `pMob[conn].TargetX/TargetY`, `LastActionTick` nos handlers TMSrv | mesmos campos/handlers | adaptador arquitetural C#, sem classe C++ equivalente. Manter transicoes de login; separar futuramente politica de movimento e timing de combate, hoje acopladas indevidamente ao registro de sessao. | auditado; transicoes/movimento/timing testados; extracao bloqueada ate caracterizacao |
| `AttackRequest` / `AttackConfirmation` | `Basedef.h::MSG_Attack/MSG_AttackOne/MSG_AttackTwo`, `TMSrv/_MSG_Attack.cpp` | mesmos structs e handler | manter os tres wires: 168/68/80 bytes, offsets de posicao/alvo/atacante/skill e slots `STRUCT_DAM`. O cliente so declara marcador de dano (`-2` fisico, `-1` skill); HP, EXP, MP, ReqMp, cena e dano sao reescritos pelo servidor. C# consulta a listing ativa (equivalente a `TradeMode`) antes de USER_PLAY e envia MessagePanel 170 privado `0x0101`/140 com cabecalho zero e texto Latin-1 de `Language.txt`; fora desse gate, exige USER_PLAY, aplica o RvR 7.69 a Clan zero no setor (13,15), depois rejeita HP exatamente0 salvo skill99 antes do timing/mana/dano, enviando SetHpMode privado 20/22 com cabecalho zero e AddCrackError(1,8). HP negativo nao aciona o gate de morte. TooSoon (< LastAttackTick+800) e tick fora da janela 120s/15s somam 1/type107 em 7.69 e W2PP; depois do gate de 800 ms, `ClientTick < (uint)LastAttackTick-100` soma 4/type7 sem retorno, antes da atualizacao do tick e da janela. C# reporta o type7, preserva a aritmetica unsigned e compartilha contador/limite; wrap pode produzir type7 seguido de type107. A 7.69 tambem bane e grava SaveInfoDetect nos ramos type107; W2PP nao, diferenca pendente de decisao, sem aplicar Analise fora de Action. RvR nao existe em W2PP e foi mantido pela referencia 7.69. | COMBAT-ATTACK-DEAD-001 + COMBAT-ATTACK-TRADEMODE-001 + COMBAT-ATTACK-RVR-001 + COMBAT-ATTACK-TIMING-001: Engine.Tests 309 PASS, solution build SDK 10.0.401 com 0 erros/1 aviso CS8321 em `ExperienceBonus` local sem uso, smokes wire/headless; timing confirmou TooSoon, duas rejeicoes OutsideServerWindow/type107 e 4/type7 total6 apos wrap, sem relay/HP/MP. Evidencias RED `Backup/Build/AutoTradeCloseSmoke/43a5a971c89b4a779ae06860223638b9`, GREEN `Backup/Build/AutoTradeCloseSmoke/2c94658ad5b643f59052b752171f9a83`; regressao Attack HP0/RvR aprovada. Outros gates de mapa/PvP/efeitos/anti-cheat pendentes; E2E visual pendente |
| `NpcAttackConfirmation` | `Basedef.h::MSG_AttackOne`, `TMSrv/GetFunc.cpp::GetAttack` | mesmos simbolos/funcao | reutilizar o `MSG_AttackOne` de 68 bytes, `ID=ESCENE_FIELD`, atacante/posicoes/alvo autoritativos e dano no primeiro `STRUCT_DAM`. `GetAttack` define `SkillParm=0`, `SkillIndex=-1` e motion aleatorio 4..6; o C# ainda usa valores simplificados para summon, que precisam ser cotejados antes de afirmar equivalencia visual. | wire auditado; ataque autonomo C# testado; sem equivalencia integral de semantica/cliente |
| `SetHpMpConfirmation` | `Basedef.h::MSG_SetHpMp`, `SendFunc.cpp::SendSetHpMp` | mesmos simbolos | manter frame de 28 bytes com HP/MP e ReqHp/ReqMp autoritativos; nao e resposta exclusiva de mana, tambem sincroniza dano, cura e ressurreicao. | auditado; wire e recursos testados |
| `UpdateScoreConfirmationV769` / `W2ppUpdateScoreV1Adapter` | cliente `Projects/TMProject/Basedef.h::MSG_UpdateScore` (`0x0336`) | W2PP `MSG_UpdateScore` (`0x0336` com flags), `TMSrv/SendFunc.cpp::SendScore` | O cliente 7.69 mede frame/payload 152/140: `STRUCT_SCORE` 48, Critical/SaveMana, 32 affects, Guild/GuildLevel, Resist, padding de alinhamento, ReqHp/ReqMp, ushort Magic/Rsv e LearnedSkill. W2PP tambem mede 152/140, mas usa score com Level int e Merchant/Direction/ChaosRate, RegenHP/MP, CurrHp/CurrMp, Magic int e quatro Special bytes; o adapter mapeia CurrHp/MP para ReqHp/MP, verifica narrowing de Level/Magic e zera campos target-only. O header DBSrv comum 7.69 empacota a mesma lista para 147 bytes, divergencia que impede copiar o sender sem confirmar a unidade ABI efetiva. | DTO + adapter + golden Win32 + teste byte a byte executados; relay target integrado no login e nos caminhos de consumiveis, combate, guild refresh e ressurreicao; o encoder legado de 160 bytes permanece fora do caminho alvo; recomputacao completa e E2E pendentes |
| `UpdateEtcConfirmationV769` / `W2ppUpdateEtcV1Adapter` | cliente `Projects/TMProject/Basedef.h::MSG_UpdateEtc` (`0x0337`) | W2PP/TMSrv `MSG_UpdateEtc` (`0x0337` com flags), `TMSrv/SendFunc.cpp::SendEtc` | Cliente 7.69 mede frame/payload 48/36: FakeExp, Exp, dois words de LearnedSkill, tres bonus signed short e Coin, com padding nativo. W2PP/TMSrv tambem medem 48/36, mas usam Hold unsigned, Exp, Learn long long, tres bonus unsigned short, Magic ushort e Coin. O adapter converte Hold com rejeicao de overflow, divide Learn nos dois words do alvo e preserva os bonus por bits; Magic e descartado porque pertence ao UpdateScore no cliente 7.69. | DTO + adapter + golden Win32 + teste byte a byte executados; relay target integrado com UpdateScore nos caminhos de login, consumiveis, combate e ressurreicao; E2E permanece pendente |
| `MessageChatRequest` / `MessageChatConfirmation` | `Basedef.h::MSG_MessageChat`, `TMSrv/_MSG_MessageChat.cpp` | mesmos simbolos/handler | manter frame fixo de 140 bytes, ID do remetente e multicast local sem eco. C# deliberadamente cobre somente chat local; comandos, mute, canais de guilda/party/reino/cidadao e filtros sao modulos separados, nao efeitos colaterais do parser. | auditado; wire e relay local testados; politicas de chat pendentes |
| `MessageWhisperRequest` / `MessageWhisperConfirmation` | `Basedef.h::MSG_MessageWhisper`, `TMSrv/_MSG_MessageWhisper.cpp` | mesmos simbolos/handler | manter 128 bytes (nome 16 + texto 100), remetente reescrito pelo servidor e entrega apenas a alvo online. `/cp` e o unico comando caracterizado. Reply, disponibilidade de whisper, mute e comandos/eventos da referencia ficam estacionados. | auditado; wire, alvo e `/cp` testados; politicas/comandos pendentes |
| `PartyInviteRequest` / `PartyAcceptRequest` / `PartyRemoveRequest` / `PartyAddConfirmation` / `PartyRemoveConfirmation` | `Basedef.h::MSG_SendReqParty/MSG_CNFAddParty/MSG_AcceptParty/MSG_RemoveParty`, `TMSrv/SendFunc.cpp::SendAddParty/SendRemoveParty` e handlers | mesmos simbolos/handlers | manter convite lider->alvo, aceite vinculado ao convite pendente, formacao/maximo e remocao autoritativos; add confirmation e frame 40 bytes com `Target=52428`, remove confirmation e frame 16 bytes de dois shorts. O C# usa plano de formacao para evitar mutacao parcial. Diferencas de eventos e regras locais (Battle Royale/OnlyTrade) nao migram sem decisao. | auditado; wire/formacao/remocao testados; regras especiais e E2E pendentes |
| `InviteGuildRequest` | `Basedef.h::_MSG_InviteGuild` como `MSG_STANDARDPARM2`, `TMSrv/_MSG_InviteGuild.cpp` | mesmos simbolos/handler | manter 20 bytes, alvo/tipo validados, custo e persistencia antes da atualizacao visual. A 7.69 adiciona penalidade e capacidade de GuildHall ausentes no W2PP; permanecem requisitos distintos ate caracterizacao de dados/persistencia. | auditado; wire e persistencia/visual basicos testados; GuildHall/penalidade e transacao compensatoria pendentes |
| `QuestRequest` | `Basedef.h::_MSG_Quest` como `MSG_STANDARDPARM2`, `TMSrv/_MSG_SingleControl.cpp` | `Basedef.h::_MSG_Quest`, `TMSrv/_MSG_Quest.cpp` | manter 20 bytes, com NPC e confirmacao como int32. O C# cobre somente Perzen e entrada Pista por regras explicitamente configuradas; nao representa o dispatcher integral de quest de nenhuma das fontes. | auditado; wire e rotas Perzen/Pista testados; catalogo/dispatcher de quests pendentes |
| `CreateMobConfirmation` / `CreateMobTradeConfirmationV769` | cliente `Basedef.h::MSG_CreateMob`/`MSG_CreateMobTrade`, `GetFunc.cpp::GetCreateMob`/`GetCreateMobTrade` e `TMFieldScene::OnPacketCreateMob` | mesmos simbolos/funcoes | Probe Win32: cliente/DBSrv `MSG_CreateMob` 236 bytes, `Equip` +34, `Affect` +70, `Score` +140, `Equip2` +190; `MSG_CreateMobTrade` 260 bytes, `Equip2` +190, `Nick` +208, `Desc` +234, `Server` +258. TMSrv/W2PP `MSG_CreateMobTrade` mede 252 bytes totais, com 16 equipamentos/aparencias, `Tab` em +190 e `Desc` em +216. C# mantém contratos client 18-slot separados do sender W2PP 16-slot; sem a extensao os indices 16/17 continuam zero, e com ela a projecao visual usa os 18 itens. | `CreateMobConfirmationV769`/`W2ppCreateMobV1Adapter` possuem fixture Win32 de payload 224/frame 236; `CreateMobTradeConfirmationV769` possui fixture estrutural byte a byte de payload 248/frame 260; `W2ppCreateMobTradeV1Adapter` possui fixture de projeção do payload W2PP 240 para o contrato cliente; os relays de CreateMob normal, UpdateEquip e autotrade aceitam o estado 18-slot quando disponivel; política NPC, calculo de visual por semantica dos slots novos e E2E continuam pendentes |
| `RemoveMobConfirmation` | cliente `Basedef.h::MSG_RemoveMob`, W2PP `Basedef.h::MSG_RemoveMob`, `SendFunc.cpp::SendRemoveMob` | mesmos simbolos/funcoes | Cliente 7.69 e W2PP medem 16 bytes: `MSG_STANDARD` de 12 bytes seguido de `int RemoveType` no payload offset 0; o ID do mob fica no header. Nao ha delta de layout nem campo target-only. | golden Win32 de payload 4/frame 16 e teste byte a byte executados; nenhuma adapter estrutural e necessaria; politica de remocao e relay continuam no fluxo de mundo |
| `NpcChatConfirmation` / `MessagePanelConfirmation` | `Basedef.h::MSG_MessageChat/MSG_MessagePanel`, `SendFunc.cpp` | mesmos simbolos | manter ambos os textos fixos de 140 bytes, mas com remetente/ID distintos: NPC usa o ID do NPC, painel e notificacao privada do servidor. | auditado; wire e emissores principais testados |
| `StartTimeConfirmation` / `MobLeftConfirmation` | `_MSG_StartTime` / `_MSG_MobLeft` como `MSG_STANDARDPARM`, timers e `CCastleZakum.cpp` | mesmos sinais/timers | manter 16 bytes e `ESCENE_FIELD`, distribuindo somente a jogadores na area/party adequada. C# cobre Pista/Castelo configurados; a matriz completa de salas, eventos e timers fica pendente. | auditado; wire e rotas Pista/Castelo testados; catalogo completo de emissores pendente |
| `DonateShopOpenRequest` / `RetailNpcShopRequest` | `Basedef.h::MSG_REQShopList`, `TMSrv/_MSG_REQShopList.cpp` | mesmos simbolos/handler | `0x027B` e request retail de NPC; nao o confundir com compra web usando Cash. Manter o fluxo retail por Gold sob auditoria. O bridge Donate de 20 bytes e custom e fica estacionado. | parsers/gates testados; sem aceite para loja Cash in-game |
| `DonateBalanceConfirmation`, `DonateShopOpenConfirmation`, `DonateStoreCatalogConfirmation`, `DonateStoreEntry`, `DonatePurchaseRequest`, `DonateShopCatalogRequest`, `DonateShopClientState` | 7.69 e W2PP mantem saldo `Donate` e incluem uma variante de fork para `MSG_BUY` com `EF_DONATE`; 7.69 possui ainda opcodes/protocolos auxiliares, enquanto W2PP nao contem os opcodes do painel custom | mesmo saldo/variantes de fork; sem equivalentes para os opcodes C# `0x0406/0411/0418/041C` | regra de produto: Cash e saldo de conta/site; Premium Neil abre a loja web; checkout debita Cash e entrega item no bau. Compra por Cash/Donate via NPC/painel nao sera implementada. O C# atual `TryPurchaseDonateItem` debita e insere no MOB, portanto e extensao fora de escopo para estacionar/remover em limpeza autorizada. Preservar somente o dado/bridge de conta que a integracao Site realmente exigir. | wire custom e compra C# existem, mas estao fora do escopo; integracao web->debito->bau nao localizada/implementada |
| `LegacySkillAttackGate` / `LegacySkillCombatMath` / `LegacyMobCombatState` | gates e formulas de `TMSrv/_MSG_Attack.cpp`, `CMob.cpp`, `GetFunc.cpp` | mesmos fluxos-base, com variacoes de regras locais | preservar calculo inteiro, custo autoritativo de mana, limite de master/parry e estado lido do `STRUCT_MOB`; cada formula so avanca depois de cotejada nas duas referencias. | auditado; mana/dano fisico/skill e varios ramos de NPC testados; score integral, delays, regras de area/war e efeitos restantes pendentes |
| `LegacyMobAbilityMath` / `LegacyCurrentScoreMath` / `LegacyEquipmentRules` / `LegacyExperienceMath` | cliente `Projects/TMProject/Basedef.cpp::BASE_GetMobAbility/BASE_CanEquip`; servidor `Servidor/Source/Code/Basedef.cpp`; TMSrv `CMob.cpp::GetCurrentScore` e `SendFunc.cpp::SendEquip` | `Basedef.cpp` ability/clear loops, `TMSrv/CMob.cpp::GetCurrentScore` | A regra de `BASE_CanEquip` coincide com W2PP nos guards relevantes (mascara `EF_POS`, conflito 6/7 e rejeicao explicita da posicao 15); isso nao prova estado de 18 slots. O handler TMSrv `_MSG_Move_Item.cpp` valida destino de equipamento em 0..14 e origem em 1..14 usando seu `MAX_EQUIP=16`, igual ao W2PP. A release C# 7670 mantém `LegacyEquipmentRules` como gate de semantica e o `WorldHub` usa a lista versionada de 18 para trade, appearance, parry, force, PVP e reflect; os slots 16/17 so passam se o item-data atribuir posicao valida. `LegacyMobAbilityMath` ja percorre 16 ou 18 conforme a entrada; `LegacyCurrentScoreMath`/Experience e outros loops que recebem apenas MOB continuam 16 ate o recalculo autoritativo aceitar a extensao. A incoerencia de build permanece: `TMSrv.vcxproj` compila `..\Basedef.cpp` com header comum 18 enquanto translation units TMSrv usam `TMSrv/Basedef.h` 16; nao tratar esse source como oracle runtime coerente. | comparacao C#↔W2PP concluida; serializer/migration/runtime 18-slot e teste de mutacao/relay executados; score integral, semantica final dos slots novos, ABI TMSrv e E2E cliente continuam pendentes |
| `GetItemRequest` / `GetItemConfirmation` / `DecayItemConfirmation` | `Basedef.h::MSG_GetItem/MSG_CNFGetItem/MSG_DecayItem`, `TMSrv/_MSG_GetItem.cpp` | mesmos simbolos/handler | manter wire e pickup autoritativo; o destino e somente carry e a validade/alcance vem do item de chao, nao do cliente. | auditado; wire/pickup/decay testados |
| `DropItemRequest` / `DropItemConfirmation` / `CreateItemConfirmation` | `Basedef.h::MSG_DropItem/MSG_CNFDropItem/MSG_CreateItem`, `TMSrv/_MSG_DropItem.cpp` | mesmos simbolos/handler | manter wire e escolha autoritativa da celula; a origem e validada no servidor. Cargo permanece fora do recorte atual. | auditado; wire/drop/chao testados |
| `DeleteItemRequest` | `Basedef.h::MSG_DeleteItem`, `TMSrv/_MSG_Clear_Item.cpp` | `Basedef.h::MSG_DeleteItem`, `TMSrv/_MSG_DeleteItem.cpp` | manter wire de 20 bytes e limpeza autoritativa do carry; a confirmacao visual ocorre por `SendItem`, nao por resposta dedicada. | auditado; wire/delete carry testados |
| `SplitItemRequest` | `Basedef.h::MSG_SplitItem`, `TMSrv/_MSG_SplitItem.cpp` | mesmos simbolos/handler | manter wire de 24 bytes, faixa 1..119 e insercao no primeiro carry livre. A elegibilidade vem de catalogo configuravel: 7.69 usa `groupItens`, W2PP traz lista fixa; nao transplantar lista sem evidencia da release 7.670. | auditado; wire/split testados |
| `UseItemRequest` | `Basedef.h::MSG_UseItem`, `TMSrv/_MSG_Use_Item.cpp` | `Basedef.h::MSG_UseItem`, handler equivalente | manter wire de 36 bytes e autoridade de origem/destino/grade no servidor. O voucher Vol 184 que soma `EF_DONATE` e top-up por item nas duas fontes e comportamento distinto de comprar em loja; confirmar se entra no modelo Cash do Site antes de manter. Compra de Cash por NPC/painel esta excluida. | auditado; wire/familias portadas testados; decisao do voucher pendente |
| `SendItemConfirmation` | `Basedef.h::MSG_SendItem`, `TMSrv/SendFunc.cpp::SendItem` | mesmos simbolos | manter 24 bytes e atualizacao de slot por `invType`, `Slot`, `STRUCT_ITEM`; e a resposta comum a mutacoes de inventario, nao confirmacao exclusiva de uma rota. | auditado; wire testado |
| `UpdateCarryConfirmation` | cliente 7.69 `Projects/TMProject/Basedef.h::MSG_Carry`/`TMHuman::OnPacketCarry`, servidor `Servidor/Source/Code/TMSrv/SendFunc.cpp::SendCarry` | `MSG_UpdateCarry` / `MSG_Carry` (`0x0185`) | manter frame de 528 bytes: 64 `STRUCT_ITEM` de 8 bytes no payload e `Coin` no offset 512; e o refresh completo usado pela conclusao de trade, nao uma sequencia de `MSG_SendItem`. | DTO/encoder, fixture de contrato byte a byte e relay para os dois participantes testados; E2E pendente |
| `EquipmentAppearanceV769` / `W2ppEquipmentAppearanceV1Adapter` | cliente `Projects/TMProject/Basedef.h::MSG_UpdateEquip`, opcode `0x036B`, `TMHuman`; `TMSrv/SendFunc.cpp::SendEquip` | W2PP `MSG_UpdateEquip` (`Equip[MAX_EQUIP]`, `AnctCode[MAX_EQUIP]`) e `SendEquip` | Cliente/DBSrv comum 7.69 mede 68 bytes: `sEquip` +12 e `Equip2` +48 (18 slots); o TMSrv privado/W2PP mede 60 bytes: vetores de 16 e `AnctCode` +44. O sender TMSrv percorre os 16 do header local, logo nao emite appearance dos indices 16/17; o C# possui DTO isolado de 18 slots e aceita tanto a projecao legada 16->18 quanto a lista versionada completa. | probe Win32 executado; golden Win32, teste do DTO/adapter, relay visual com slots 16/17 e mutacao de runtime executados; E2E cliente e semantica completa dos slots novos pendentes |
| `UpdateItemRequest` / `UpdateItemConfirmation` | `Basedef.h::MSG_UpdateItem`, `TMSrv/_MSG_UpdateItem.cpp`, timers/portoes | mesmos simbolos, `CCastleZakum` e timers | manter wire de 20 bytes e mutacao autoritativa do estado de item de mapa; mascaras, chave, altura, gate/timer e multicast pertencem ao dominio de mundo. | auditado; wire, mascara, timer e portoes testados |
| `LegacyItemDataTable` / `LegacyServerItemDataTable` / `LegacyItemDefinition` / `LegacyServerItemDefinition` / `LegacyItemStaticEffect` / `LegacyItemEffect` | loader server 7.69 em `Servidor/Source/Code/Basedef.cpp`, tipos/constantes em `Basedef.h`; layout client em `Projects/TMProject` | loaders/tipos equivalentes em `Source/Code/Basedef.cpp/.h` | os leitores C# mantem separados o layout client/converter de 164 bytes e o runtime server de 140 bytes. A habilidade C# continua subconjunto no leitor cliente, nao `BASE_GetItemAbility` completo; o leitor server-side ainda e somente formato. | parser client e server 7.69/W2PP testados; fonte autoritativa do arquivo, consumidores server-side e ramos restantes de habilidade pendentes |
| `LegacySkillDataTable` / `LegacySkillDefinition` | `Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill`, `Basedef.h::STRUCT_SPELL` | mesmos simbolos; `MAX_SKILLINDEX=248` | manter `/4` em `AffectTime` e a semantica da segunda coluna de `Act`; C# rejeita linha malformada em faixa ao inves de aceitar parcial via `sscanf`. | comparacao estatica concluida; fixture/parser testados |
| `LegacySummonCatalog` / `LegacySummonTemplate` / `LegacySummonBonus` | `TMSrv/CNPCGene.cpp::CNPCSummon::Initialize`, `Server.cpp::GenerateSummon`, `Basedef.cpp::pSummonBonus` | mesmos simbolos/funcoes | C# carrega 43/50 slots por padrao, incluindo `Cav._Arcano`, `Arq._Arcano` e `Mag._Arcano` nos slots 40..42. A leitura historica de 7.59 continua disponivel somente com limite explicito de 40 slots; slots 43..49 permanecem indisponiveis. | catalogo 7.69/W2PP carregado e nomes/slots testados; E2E de combate/relay especifico dos slots Arcano pendente |
| `LegacyNpcGenerationCatalog` / `LegacyNpcGenerationDefinition` / `LegacyGeneratedNpc` / `LegacyNpcGenerationBuilder` | `TMSrv/CNPCGene.cpp::CNPCGenerator::ParseString`, `Server.cpp::GenerateMob` | mesmos parser e geracao; W2PP tem apenas variacao de diagnostico | manter como adapter parcial para spawn configurado de lider; segmentos, followers, grupos, contadores, rotas, waits/actions e agenda do legado nao estao cobertos. | subset implementado/testado; geracao integral pendente |

**Proximo lote:** continuar a auditoria dos leitores e consumidores de dados
estaticos, resolvendo primeiro o contrato ItemList server/client e o catalogo
de summons. Depois retomar combate (gates, delay, mapa/PvP e emissores de score)
e os recortes restantes de `WorldHub`. A validacao visual do cliente 7.670 segue
pendente.

### Politica de versao da release

`7.670` e o identificador da release-alvo e corresponde ao inteiro obrigatorio
`7670` em `MSG_AccountLogin.ClientVersion`. A constante deve ser emitida pela
build do cliente; o servidor apenas a valida. As fontes registram valores antigos
distintos (`APP_VERSION` 1001/1059 na arvore 7.69 e 7640 no W2PP), que devem ser
substituidos na migracao do cliente e rejeitados pelo listener 7.670. Adaptadores
de opcode/layout continuam possiveis, mas nao dispensam a versao exigida.

## Dados estaticos e formatos de referencia

| ID | Referencia 7.69 | C# | Contrato e diferenca W2PP | Teste/evidencia | Nivel |
|---|---|---|---|---|---|
| ITEMDATA-001 | `Servidor/Source/Code/Basedef.h::STRUCT_ITEMLIST/BASE_ReadItemList`; `Projects/TMProject/Basedef.h` e `Tools/ItemListConverter/WYDConverter.h` | `LegacyItemDataTable` + `LegacyServerItemDataTable` | o contrato cliente/converter le 6500 registros de 164 bytes (XOR `0x5A`, trailer opcional), com `nPos` int no offset 136. A source do servidor 7.69 e o W2PP definem 6500 registros de 140 bytes, com `nPos` short em 134, `Extra` em 136 e `Grade` em 138; ambos leem trailer de checksum e decodificam o corpo com o mesmo XOR. | os dois leitores C# possuem fixtures e rejeicao de trailer parcial; o probe Win32 confirmou 164 no cliente e 140 no DBSrv/TMSrv 7.69 e W2PP; arquivo autoritativo e consumidores server-side ainda precisam de decisao | C2 |
| ITEMDATA-002 | `STRUCT_ITEMLIST` e efeitos `EF_*` em `Servidor/Source/Code/Basedef.h`; habilidade `BASE_GetItemAbility/Sanc/Gem` em `Basedef.cpp` | `LegacyItemDefinition` / `LegacyServerItemDefinition` / `LegacyItemStaticEffect` / `LegacyItemEffect` / metodos de `LegacyItemDataTable` | os DTOs C# mantem os formatos separados, sem converter 32-bit `nPos` do cliente em `short` por presuncao. `GetItemAbility` continua um subconjunto no leitor cliente: faltam ramos presentes nas duas sources como restricoes por `nUnique`, substituicao `EF_CRITICAL2`/`EF_ACADD2` e bonuses de montaria; o leitor server-side ainda nao foi ligado a habilidade autoritativa. | parser/offsets dos dois contratos possuem fixtures; comparacao do runtime server-file, escolha da fonte e ramos restantes pendentes | C2 |
| SKILLDATA-001 | `Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill`, `Basedef.h::STRUCT_SPELL` | `LegacySkillDataTable` / `LegacySkillDefinition` | ambas as sources usam `MAX_SKILLINDEX=248`, convertem `AffectTime / 4` e passam o mesmo buffer `skilldata` para as duas colunas de acao; a segunda coluna prevalece no `Act[0..5]`, e `Act[6..7]` fica zero. O C# preserva as duas colunas para auditoria e expoe `Action2` como efetiva; parsing de linha valida e deliberadamente mais estrito que `sscanf`. | parser/tempo/acoes cotejados; teste de fixture existente; dados reais completos nao estao no checkout de referencia |
| SUMMONDATA-001 | `TMSrv/CNPCGene.cpp::CNPCSummon::Initialize`, `Server.cpp::GenerateSummon`, `Basedef.cpp::pSummonBonus` | `LegacySummonCatalog` / `LegacySummonTemplate` / `LegacySummonBonus` | C# carrega 43 templates em slots 0..42 por padrao; 7.69 e W2PP inicializam os mesmos 43, incluindo os tres templates Arcano. Os 50 slots continuam sendo o limite do wire/tabela; bonuses nao zero estao somente nos primeiros oito em ambas as fontes. | discrepancia de tres templates resolvida no leitor/catalogo; E2E de summon e cobertura de bonus especifica ainda pendentes |
| NPCDATA-001 | `TMSrv/CNPCGene.cpp::CNPCGenerator::ParseString/ReadNPCGenerator`, `Server.cpp::GenerateMob` | `LegacyNpcGenerationCatalog` / `LegacyNpcGenerationDefinition` / `LegacyGeneratedNpc` | C# e parser subset, intencionalmente usado para spawn do lider. O legado tambem carrega segmentos, waits/actions, destino, followers, grupos, contador/maximo e agenda; nao tratar o leitor/`TryCreateLeader` como port completo de `GenerateMob`. | subset auditado; parser e spawn configurado testados; followers/rotas/grupos/timers pendentes | C2 |

## Rotas de entrada atualmente ligadas

| ID | Referencia legada | Entrada C# / opcode | Handler ou metodo de dominio | Estado/persistencia | Saida principal | Teste/evidencia | Nivel |
|---|---|---|---|---|---|---|---|
| AUTH-001 | `_MSG_AccountLogin` / `CFileDB` | `AccountLoginRequest` `0x020D` | `AccountLoginCoordinator.HandleAsync` | sessao; arquivo/MariaDB | `AccountLoginConfirmationV769` `0x010A` ou `MessagePanel` | parser, coordinator, adapter/golden 1928, frame projetado no listener, failure notice | C4 |
| AUTH-002 | `_MSG_AccountSecure` | `AccountSecureRequest` `0x0FDE` | `AccountSecureCoordinator.HandleAsync` | PIN/security store | `AccountSecureSignal` | parser, signal e coordinator | C3 |
| CHAR-001 | `_MSG_CreateCharacter` | `CreateCharacterRequest` `0x020F` | `CreateCharacterCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_SELCHAR`; snapshot, nome, template | `NewCharacterConfirmationV769` `0x0110`, layout alvo Win32 920 bytes, `SelChar` +16 | adapter de seleção, contrato/golden 920, template, create fixture e sucesso integrado ao listener; falha/E2E pendentes | C4 |
| CHAR-002 | `_MSG_DeleteCharacter` | `DeleteCharacterRequest` `0x0211` | `DeleteCharacterCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_SELCHAR`; slot limpo/defaults | `DeleteCharacterConfirmationV769` `0x0112`, layout alvo Win32 920 bytes, `SelChar` +16 | adapter de seleção, contrato/golden 920, fixture de refresh e sucesso integrado ao listener; falha/E2E pendentes | C4 |
| CHAR-003 | `_MSG_CharacterLogin` | `CharacterLoginRequest` `0x0213` | `CharacterLoginCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_PLAY`; rejeicao retorna a `USER_SELCHAR`, MOB/MOBEXTRA | `CharacterLoginConfirmationV769` `0x0114`, `CreateMobV769`, `UpdateEtcV769`, `UpdateScoreV769` | parser, transicao e composer W2PP -> 7.69 testados; cadeia integrada no listener com frames 1728/236/48/152; relays de CreateMob/UpdateEtc/UpdateScore cobrem consumiveis, combate, summons/NPCs, guild refresh e ressurreicao; UpdateAffect cobre login e mutacoes com snapshot autoritativo quando os campos cabem no wire; UpdateEquip cobre troca de equipamento, catalisador e durabilidade de montaria; `BASE_GetHpMp`, quinze recortes e uma composicao de `LegacyCurrentScoreMath`, mais `LegacyMountRunRules`, portados/testados isoladamente; demais recalculos TMSrv, narrowing incompativel e E2E pendentes | C4 |
| CHAR-004 | `_MSG_CharacterLogout` | `CharacterLogoutRequest` `0x0215` | `LoginSessionRegistry` + save | SPX/SPY e estado | `CharacterLogoutConfirmation` `0x0116` | logout wire e transicoes | C4 |
| MOVE-001 | `_MSG_Action` | `ActionRequest` `0x036C` e variantes | `TryApplyMovement`, summons | sessao/posicao | relay de action/create mob | parser, movement, world broadcast | C3 |
| MOVE-002 | `_MSG_Motion` | `MotionRequest` `0x036A` | relay de movimento; `MSG_SetHpMode` quando HP=0 | sessao/HP | frame de motion ou SetHpMode | motion wire e gate de vida | C3 |
| SKILL-001 | `_MSG_SetShortSkill` | `SetShortSkillRequest` `0x0378` | save de short skills | estado do personagem | sem confirmacao dedicada | wire 32 bytes e persistencia | C3 |
| WORLD-001 | `_MSG_PKInfo` | `PkInfoConfirmation` `0x0366` | politica PK/multicast de visao | flag visual de PK/culpa | `MSG_STANDARDPARM` | wire 16 bytes; politica/multicast pendentes | C2 |
| COMBAT-001 | `_MSG_Attack` | `AttackRequest` `0x0367/039D/039E` | `LegacyAutoTradeBook.HasListing`, `LegacyAttackRvrGateRules`, `WorldHub.TryGetAttackState`, `TryConsumeSkillMana`, `TryApplyPhysicalAttack` | listing, posicao/Clan, HP/MP, alvo, drops, XP | `MessagePanelConfirmation`, `AttackConfirmation`, `SetHpMpConfirmation`, `UpdateEtc`, `SetHpModeConfirmation` | wire 168/68/80; TradeMode responde MessagePanel 170 privado antes de USER_PLAY; gate 7.69 RvR bloqueia Clan zero no setor `(13,15)` com MessagePanel privado antes de HP0/tick/mana; W2PP nao tem esse gate. HP0/skill99 e type8 compartilhado, mana e dano fisico testados; outros gates/branches pendentes. Ver `CLIENT_E2E_SMOKE.md` | C3 |
| COMBAT-002 | ataque de skill | `AttackRequest` + skill index | `TryApplySkillAttack` | HP/MP, efeitos, summon | ataque, recursos, morte | gate/formulas e ramos iniciais de NPC testados; delays, todos os efeitos e equivalencia de area pendentes | C3 |
| ITEM-001 | `_MSG_UseItem` | `UseItemRequest` `0x0373` | `TryApplyPotion` | MOB/carry/HP/MP | `SetHpMpConfirmation`, `SendItem` | potion, reset, item families | C3 |
| ITEM-002 | `_MSG_DropItem` | `DropItemRequest` `0x0272` | `TryDropItem` | carry -> ground item | drop/create/broadcast | wire e ground drop | C3 |
| ITEM-003 | `_MSG_GetItem` | `GetItemRequest` `0x0270` | `TryGetGroundItem` | ground -> carry | get/decay/send item | wire e pickup | C3 |
| ITEM-004 | 7.69 `Basedef.h::MSG_SwapItem`, `SGrid.cpp`, cliente `TMHuman.cpp` e `TMSrv/_MSG_Move_Item.cpp` (`0x0376`) | `TradingItemRequest` / `TradingItemConfirmation`, payload 8 bytes / frame 20 bytes | `WorldHub.TryTradeItems` | carry 0..59 e carry↔equipamento; C# permite destino Equip 0..14 e origem Equip 1..14; cargo estacionado | echo + dois `SendItem` | C# valida a mesma faixa de slot que o handler TMSrv/W2PP e preserva a ordem de campos 7.69 no request; wire de mesmo tamanho ainda nao distingue layouts, pois W2PP usa ordem inversa e `WarpID` int. UI 7.69 expoe indices 16/17, mas a rota TMSrv os rejeita pelo header privado de 16 e pelo limite `MAX_EQUIP-1`; a regra client-side de `EF_POS` nao os habilita no servidor. A compilacao TMSrv do `..\Basedef.cpp` com header comum 18 cria conflito de layout com os translation units 16-slot, portanto a equivalencia observada da rota nao resolve os efeitos das helpers ligadas. | C3; comparacao de faixa concluida; indices 15..17 sem suporte nessa rota; fechamento de ABI TMSrv e golden 7.69 pendentes |
| ITEM-005 | `_MSG_SplitItem` | `SplitItemRequest` `0x02E5` | `TrySplitCarryItem` | stack origem -> primeiro carry livre | dois `SendItem`, destino depois origem | wire, quantidade, item permitido, sem espaco; smoke E2E confirmou checksum, ordem e persistencia 7/3 apos relogin | C4 |
| ITEM-007 | `_MSG_DeleteItem` | `DeleteItemRequest` `0x02E4`/20 | `TryDeleteCarryItem` | carry 0..59 -> vazio | aviso `MessagePanel` se conta bloqueada; `0x0384`/12 aos dois lados se cancelar trade; sem resposta dedicada no aceite | 7.69/W2PP enviam sinal 900 via `RemoveTrade` aos dois participantes; 7.69 barra conta bloqueada e W2PP nao. C# preserva adicionalmente a checagem do indice atual contra pacote obsoleto. Smoke E2E confirmou bloqueio, remocao persistida, cancelamento bilateral com item retido apos relogin | C4 |
| SOCIAL-001 | `_MSG_MessageChat` | `MessageChatRequest` `0x0333` | view relay | nenhum | chat para visao | wire e player view | C3 |
| SOCIAL-002 | `_MSG_MessageWhisper` | `MessageWhisperRequest` `0x0334` | busca por nome + `SendAsync` | nenhum | whisper privado | wire e target | C3 |
| SOCIAL-003 | `_MSG_SendReqParty` | `PartyInviteRequest` `0x037F` | `TryPreparePartyInvite` | pending party | confirmation/panel | party wire/lifecycle | C3 |
| SOCIAL-004 | `_MSG_AcceptParty` | `PartyAcceptRequest` `0x03AB` | `TryAcceptParty` | party/leader | add/remove mob | party lifecycle | C3 |
| SOCIAL-005 | `_MSG_RemoveParty` | `PartyRemoveRequest` `0x037E` | `TryRemovePartyMember` | party | removal/disband | party lifecycle | C3 |
| SOCIAL-006 | `_MSG_InviteGuild` | `InviteGuildRequest` `0x03D5` | `TryPrepareGuildInvite` | guild/coin + store | UpdateEtc/CreateMob | guild invite/update | C3 |
| EVENT-001 | `_MSG_Quest` / Perzen | `QuestRequest` `0x028B` | `TryExchangePerzenItem` | carry/date | chat + replacement item | Perzen exchange | C3 |
| EVENT-002 | `_MSG_Quest` / Pista | `QuestRequest` `0x028B` | `TryRegisterPistaParty` | registration/timers | entry plan | Pista registration/schedule | C3 |
| SHOP-001 | custom Donate panel open | `DonateShopOpenRequest` | old balance/catalog policy | C# session | custom opcodes | none in target client | parked: no in-game Cash store |
| SHOP-002 | retail NPC shop list | `RetailNpcShopRequest` | `0x027B` / `IsDonateShopNpc` legacy bridge | NPC/Gold shop | retail catalog | distinguish Gold retail from Cash | audit normal Gold flow; Donate bridge parked |
| SHOP-003 | custom Donate catalog | `DonateShopCatalogRequest` | custom catalog provider | rate limiter | `MSG_UpdateDonateStore` | no target UI contract | parked/out of product scope |
| SHOP-004 | custom Donate purchase | `DonatePurchaseRequest` | `TryPurchaseDonateItem` | atomic debit + MOB | balance/item/panel | target is web debit + chest delivery | existing C# behavior conflicts; park/remove |
| WEBSTORE-001 | Premium Neil / account Cash web shop | no C# route identified | Site checkout + account balance + chest delivery | account/Cash source of truth | web checkout and warehouse grant | site route, idempotency, transaction/outbox | not implemented/verified |
| OPS-001 | wire diagnostics | all decoded frames | `ServerWireLog.WriteFrame` | append-only log | RX/TX evidence | masking/flush behavior | C4 |

## Rotas localizadas, mas ainda nao fechadas

Estas entradas aparecem na referencia `ProcessClientMessage.cpp` ou nos objetos legados, mas nao possuem ainda uma linha C3/C4 equivalente no dispatcher atual.

| ID | Simbolo legado | Situacao atual | Dependencia para implementar |
|---|---|---|---|
| ITEM-006 | `_MSG_UpdateItem` | wire, agregado de `InitItem`, mascara oficial, mutacao de altura, chave, dispatcher, multicast, inicio/portoes Zakum, `_MSG_StartTime`, timer de expiracao, fechamento automatico estatico por minuto e decay dinamico C3 | validar E2E; `pItem[]` permanece volátil |
| ITEM-008 | `_MSG_CombineItem*` | varias variantes legadas | servico de inventario e tabela de regras |
| ITEM-009 | `_MSG_ApplyBonus` | referencia localizada | efeitos, limites e persistencia |
| TRADE-001 | `_MSG_ReqTradeList` / `_MSG_SendAutoTrade` | `TradeListRequest` isolado; parser/encoder de `MSG_STANDARDPARM` (`0x039A`, 16 bytes) testado; `LegacyAutoTradeListRules` cobre solicitante vivo/em `USER_PLAY`, alvo existente/ativo/em `USER_PLAY` e janela inclusiva `VIEWGRIDX/Y=33`; `LegacyAutoTradeLocationRules` reproduz os cinco limites de vila, a área protegida de Armia e a taxa por `CityTax`; `AutoTradeListConfirmation` e `AutoTradeStartRequest` compartilham e testam o layout client/server de `MSG_SendAutoTrade`/`MSG_AutoTrade` (`0x0397`, 196 bytes) com 12 slots; `LegacyAutoTradeBook` valida uma oferta autoritativa em memoria, incluindo cargo 7.69 de 120 slots, preço, taxa, título, conta bloqueada e `EF_NOTRADE`; o dispatcher abre a loja, consulta o book, reaplica os gates e `LegacyAutoTradeListRelay` entrega a resposta ao solicitante; `LegacyAutoTradeVisualRelay` projeta o MOB/affects autoritatorios e o titulo para `MSG_CreateMobTrade` 7.69 de 260 bytes, enviando ao dono, à vizinhanca e ao solicitante de lista; `LegacyAutoTradeRemovalRelay` monta `MSG_RemoveMob` de 16 bytes no logout/desconexao, enviado somente à vizinhanca 33x33 antes de `WorldHub.Leave`; `LegacyAutoTradeFileStore` persiste owner/listing, localizacao e taxa em documento versionado quando `--autotrade-state` esta ativo, e o host salva na abertura e remove na saida; `MariaDbAutoTradeStateStore` usa `wyd_autotrade_state` no modo MariaDB; `LegacyAutoTradeRehydrator` restaura o MOB offline no boot e projeta `MSG_CreateMobTrade` para novos logins | migração e round-trip local no MariaDB aprovados; banco externo, política de reconexão do dono e E2E pendentes |
| TRADE-005 | `_MSG_ReqBuy` / `MSG_ReqBuy` / `_MSG_ItemSold` | `AutoTradePurchaseRequest` preserva `0x0398`, payload 24/frame 36, `Pos`, `TargetID`, padding ABI, `Price`, `Tax` e `STRUCT_ITEM`; `LegacyAutoTradePurchaseRules` valida alvo/alcance, loja ativa, posição, preço/taxa/item do request contra o listing, cargo do vendedor contra o item, Gold do comprador, espaço 0..59, vila 0..4 e limite de 2G do vendedor, inclusive alvo offline explicitamente reidratado; `ItemSoldConfirmation` preserva `0x039B`, standard-parm2 de 20 bytes e `ESCENE_FIELD`; `LegacyAutoTradeSettlementMath` reproduz o limiar de 100 mil e o truncamento inteiro do imposto; `LegacyAutoTradePurchasePlanBuilder` produz cópias do carry/cargo, saldos e listing pós-venda sem mutar as fontes; `LegacyAutoTradePurchaseCommitRequest` separa o MOB runtime do comprador do MOB persistido e fecha os dois blobs com o listing na mesma transação serializável no MariaDB ou via journal/recovery no file store; `LegacyAutoTradeBook` protege o slot, `WorldHub` aplica/rollbacka o comprador, `LegacyAutoTradePurchaseCoordinator` prepara o request do host e `LegacyAutoTradePurchaseExecutor` coordena book/NPC offline/comprador/commit com rollback reverso; o dispatcher emite `MSG_UpdateCarry`, `MSG_ItemSold` e o visual offline somente depois do commit | 211 testes deterministas, incluindo coordinator, relay pós-commit, CAS distintos, commit/recovery do file store e a fatia 18-slot; smoke local do MariaDB passou com compra, verificação pós-commit e replay CAS; banco remoto, interrupção e E2E continuam pendentes |
| TRADE-002 | `_MSG_Trade` / `_MSG_CNFCheck` | `TradeOfferRequest` e `TradeCheckConfirmation` testados; `WorldHub.TryStageTradeOffer` cobre estado bilateral em memoria, carry autoritativo, saldo, duplicidade, `EF_NOTRADE`, guilda, PK e checks; `LegacyTradeOfferRelay` encaminha a oferta e o dispatcher conclui os dois checks com `TryCompleteTrade` + `LegacyTradeCompletionRelay`/`UpdateCarryConfirmation` para os dois participantes; `LegacyTradePersistence` monta os dois estados e o host chama `IAtomicCharacterStateStore` quando disponivel; `MariaDbWorldCharacterStore` e `LegacyFileAccountStore` fazem o commit dos dois blobs em transacao serializavel ou journal/recovery, respectivamente | transacao MariaDB ainda sem teste contra banco vivo; mesma conta, ciclo completo e E2E |
| TRADE-003 | `_MSG_QuitTrade` / `RemoveTrade` 7.69 e W2PP; `GetGuilty` / `GridMulticast` | `TradeCloseRequest` `0x0384`/12; HP <=0 retorna SetHpMode sem limpar par; aceite envia cancelamento a ambos e PK `0x0366`/16 ao autor/vizinhos 33x33; sem par, NoActiveTrade carrega plano com OpponentId0: sinal proprio e PK sem cancelar ex-parceiro. `LegacyTradePkRules` usa guilty/PKMode/Newbie/Castle/Tower sob lock, sem RvR extra W2PP | 220 PASS/build limpa; rede HP0/-1 preserva par, sem PK/cancelamento; redes PK0/1 por guilty e dois reenvios idle passam com carry/Gold completos intactos nos blobs/relogin. Controles negativos dominio/rede antigos falham. Deploy sandbox com backup/hashes UP/PVP conferidos; ver CLIENT_E2E_SMOKE. Pendentes: PK1 por PKMode/Newbie/Castle/Tower e exclusao do distante na rede, AddCrackError, ISTradTimer, concorrencia fora do fechamento coordenado e E2E visual. O reset/visual normal para loja online no Action esta no recorte MOVE-ACTION-TRADEMODE-001. |
| TRADE-AFK-001 | 7.69 `CUser.h`, `_MSG_SendAutoTrade.cpp:112`, `_MSG_QuitTrade.cpp:16`, `ProcessSecMinTimer.cpp:928-988`; W2PP `CUser.h`, `_MSG_SendAutoTrade`, `ProcessSecMinTimer.cpp:912-972` | auditoria sem implementacao: ISTradTimer e timestamp de abertura de autotrade, nao contador/cooldown bilateral; 7.69 grava timeGetTime e zera no QuitTrade; consumidor usa LojinhaTimer, MAC e escolha por timestamp no evento AFK. W2PP tem consumidor, mas nenhuma atribuicao no handler de abertura auditado | C# sem equivalente; nao adicionar estado sem consumidor. Premio 7.69 premioLojaAfk/OnlyTrade difere do W2PP titulo #VemProCdK/item475. Evento, recompensa, cadencia real, selecao por MAC e reset ficam pendentes em trilha propria, sem port automatico ou mudanca de runtime |
| TRADE-CLOSE-001 | `_MSG_QuitTrade` / `RemoveTrade` 7.69 e W2PP; extensao C# de loja persistida | T1..T6 no recorte: reserva/HP/recovery, captura sob leases e host QuitTrade; registry compartilhado/concurrente; planos sinais/PK/CreateMob236/view33x33; recovery por AffectedOwners; abertura/consulta196+260; logout/disconnect save/Leave protegidos contra compra/rollback, sockets depois da lease; cleanup detached expected-delete/barreira atomica owner sem tocar ID reutilizado; ver ledger | 259 PASS/build limpa/MariaDB isolado; smoke TCP real debug e self-contained abriu/consultou loja, fechou normal236 sem RemoveMob, rejeitou compra posterior e dois retries/relogin preservaram carry/cargo/Gold. Controle negativo anterior falhou. Dois Important do review do harness corrigidos RED->GREEN (porta18283/barreira comprador), dois minors deferidos no ledger. Runtime-only publicado no sandbox com backup/UP-PVP Running/8281-8282 Listen/hashes iguais. Politica ordinaria de remocao preservada, E2E visual cliente3D e port inteiro nao concluidos |
| TRADE-004 | `_MSG_TradingItem` completo (cliente 7.69 `MSG_SwapItem`) | apenas carry 0..59 | cargo, equipamento, merge, validade e regras especiais |
| SHOP-005 | `_MSG_Buy` / `_MSG_Sell` | nao portado neste fluxo | catalogo de NPC, Gold, estoque e transacao |
| SHOP-006 | `_MSG_Deposit` / `_MSG_Withdraw` | nao portado | banco/cargo e lock por conta |
| SOCIAL-007 | autotrade | referencia localizada; `LegacyAutoTradeRehydrator` e `LegacyAutoTradeReconnectCoordinator` ligados | estado persistente, rehidratacao e fechamento no login do owner; vendas/rollback e E2E pendentes |
| WORLD-001 | `_MSG_ReqTeleport` | referencia localizada | mapa, zona e custo |
| WORLD-002 | `_MSG_ChangeCity` | referencia localizada | zonas, spawn e persistencia |
| WORLD-003 | `_MSG_PKMode` | referencia localizada | relacoes PK/war e broadcast |
| EVENT-003 | `_MSG_War` | referencia localizada | estado global e timers |
| EVENT-004 | ranking/challenge | referencia localizada | fonte de dados e wire de retorno |

## Regras para atualizar a matriz

Atualizacao de 23/09/2026: `LegacyAutoTradeReconnectCoordinator` fecha o NPC e
o estado do mesmo account/slot antes da confirmacao de login, captura a
vizinhanca para o `MSG_RemoveMob` e remove o registro persistido. A referencia
W2PP comprova o reset de `TradeMode`/`AutoTrade` no carregamento do personagem,
mas nao comprova uma politica propria de venda offline persistente. Migracao,
vendas/rollback e E2E continuam pendentes.

Atualizacao posterior de 23/09/2026: `LegacyAutoTradePurchaseExecutor` agora
coordena o CAS do book, NPC offline e comprador, chama o commit duravel e faz
rollback em ordem reversa quando o commit e rejeitado. O dispatcher de
`MSG_ReqBuy` foi ligado somente para backends com
`ILegacyAutoTradePurchaseCommitStore`; depois do commit ele envia
`MSG_UpdateCarry`, multicasta `MSG_ItemSold` na vizinhanca do vendedor e
reprojeta o visual offline. O file store tambem possui journal/recovery e teste
de rollback de transacao preparada; o smoke local do MariaDB tambem passou com
compra, verificacao pos-commit e replay CAS. Banco remoto, E2E e interrupcao
durante uma venda real ainda nao foram exercitados.

1. Uma entrada nova começa em C0/C1, nunca em “done”.
2. O simbolo legado e a referencia comportamental devem ser registrados antes do codigo.
3. O wire deve ter teste de tamanho, opcode, checksum, offsets e rejeicoes.
4. O handler deve registrar estado lido, estado mutado e destino de cada resposta.
5. Persistencia precisa indicar adapter, transacao, concorrencia e rollback.
6. A coluna de cliente somente avanca com evidencia do cliente real; build do servidor nao basta.
7. A coluna remota somente avanca com build/publicacao e logs/portas verificados.
8. Qualquer divergencia deve ficar em “known deviations”, nunca escondida em comentario solto.

## Inventario mecanico

Para localizar tipos e metodos no checkout atual, executar na raiz de `Server`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\PortAudit\Generate-PortInventory.ps1
```

O gerador e uma leitura mecanica auxiliar, nao substitui a analise semantica da referencia C++ nem a ficha de equivalencia. Ele deve ser executado antes de uma revisao de arquitetura e depois de grandes extracoes para detectar metodos que ficaram sem classificacao.

## Proximo bloco controlado

O wire e o caminho normal de dominio de `_MSG_UpdateItem` foram fechados em C3. O bloco ITEM-005 tambem passou pelo smoke de cliente/rede sintetico em loopback: os dois `MSG_SendItem` foram conferidos na ordem destino/origem e o estado foi relido depois de logout/login.

## Ficha ITEM-005 — `_MSG_SplitItem`

### Referencia analisada

- `W2PP/Source/Code/Basedef.h:1206`: `_MSG` tem 12 bytes (`Size`, `KeyWord`, `CheckSum`, `Type`, `ID`, `ClientTick`).
- `W2PP/Source/Code/Basedef.h:2381-2390`: `_MSG_SplitItem = 229 | FLAG_CLIENT2GAME`; `Slot`, `sIndex`, `Num` sao tres `int`.
- `W2PP/Source/Code/TMSrv/ProcessClientMessage.cpp:279`: roteamento para `Exec_MSG_SplitItem`.
- `W2PP/Source/Code/TMSrv/_MSG_SplitItem.cpp:21-78`: regra completa da operacao.
- `W2PP/Source/Code/TMSrv/Server.cpp:1059-1081`: `PutItem` escolhe o primeiro carry livre e envia o item inserido.

### Contrato de wire

| Campo | Valor |
|---|---|
| opcode | `229 + 0x0200 = 0x02E5` |
| direcao | cliente -> servidor |
| header | 12 bytes |
| payload | 12 bytes |
| pacote total | 24 bytes |
| payload offset 0 | `int Slot` |
| payload offset 4 | `int sIndex` — presente no wire, mas ignorado pela funcao legada |
| payload offset 8 | `int Num` |
| resposta | nao existe confirmation/echo de split |
| respostas indiretas | `MSG_SendItem` do novo slot, depois `MSG_SendItem` do slot original |

### Ordem comportamental legada

1. Rejeita `Slot < 0` ou `Slot >= MAX_CARRY - 4`; com `MAX_CARRY=64`, o intervalo aceito e 0–59.
2. Rejeita `Num <= 0` ou `Num >= 120`; o intervalo aceito e 1–119.
3. Se o modo nao e `USER_PLAY`, envia `SendHpMode` e encerra.
4. Se existe trade ativo, chama `RemoveTrade` e encerra sem dividir.
5. Aceita somente os indices `413, 412, 419, 420, 416, 414` ou `2390..2419`.
6. Conta slots livres no carry configurado.
7. Rejeita sem espaco, quantidade zero/um ou `amount <= Num`.
8. Atualiza o item original para `amount - Num` usando o efeito de quantidade legado.
9. Cria o novo item com o mesmo `sIndex` e `Num`; os demais efeitos sao zerados pela construcao legada de `STRUCT_ITEM`.
10. `PutItem` grava no primeiro slot livre e envia o novo item.
11. `SendItem` envia novamente o item original atualizado.

### Decisoes de equivalencia para o C#

- Nao validar `request.sIndex` contra o item persistido: a referencia nao o usa.
- Nao acrescentar uma guarda de HP apenas por consistencia com outras operacoes; `_MSG_SplitItem` verifica modo, nao vida do personagem.
- Nao copiar efeitos adicionais do item para o novo slot sem prova da referencia; o comportamento observado reconstrói somente `sIndex` e quantidade.
- O estado bilateral de trade ja existe no `WorldHub` como fatia de dominio em memoria: `TryStageTradeOffer` valida o carry vivo, saldo, slots, itens nao negociaveis, itens de guilda, PK e checks; `TryCompleteTrade` monta os carries candidatos, valida espaco/Gold e aplica a conclusao atomicamente, devolvendo snapshots com metadados de conta, slot, posicao e `MOBEXTRA`; `TryRollbackTradeCompletion` restaura os dois snapshots somente se o estado pos-completion ainda coincide, e `TryCloseTrade` limpa ambos os lados enquanto `Leave` reporta o par cancelado no disconnect. Como a oferta staged ainda nao mutou carry/Gold, o disconnect nao gera rollback persistente. O host ja despacha `MSG_Trade`/`MSG_QuitTrade`, envia `MSG_CNFCheck` e usa `UpdateCarryConfirmation` para o refresh completo dos dois participantes. `LegacyTradePersistence` monta a solicitacao dupla; `MariaDbWorldCharacterStore` faz o commit conjunto com locks ordenados e `LegacyFileAccountStore` faz o commit dos dois blobs por journal/recovery. Quando mesma conta ou falha do backend impedem o commit, o host restaura o runtime por CAS e nao emite relay. A transacao MariaDB ainda nao foi exercitada contra banco vivo. Para autotrade, `LegacyAutoTradeLocationRules` resolve vila/taxa, `LegacyAutoTradeBook` valida a abertura contra conta, cargo e item data, `LegacyAutoTradeListRelay` devolve a listagem, `LegacyAutoTradeVisualRelay` monta o visual 7.69 a partir do MOB/affects autoritatorios e `LegacyAutoTradeRemovalRelay` monta a remocao visual de 16 bytes no logout/desconexao. `LegacyAutoTradeFileStore` cobre a persistencia local opt-in de owner/listing, localizacao e taxa, `MariaDbAutoTradeStateStore` usa a tabela `wyd_autotrade_state` quando o modo MariaDB esta ativo e `LegacyAutoTradeRehydrator` reanexa o MOB offline ao `WorldHub` no boot; migracao/banco ao vivo, politica de reconexao do dono e E2E continuam fora. Essa fatia ainda nao deve ser tratada como equivalencia C7 completa.
- O MOB alterado fica autoritativo no `WorldHub`; o host tenta persistencia imediata somente quando o store oferece `IAtomicCharacterStateStore`. Sem essa capacidade, o save completo ocorre no logout/desconexao. O salvamento completo tambem espelha o Coin do MOB no campo account-wide usado pela selecao.

### Testes obrigatorios antes de ligar o dispatcher

- wire valido: opcode `0x02E5`, tamanho 24, offsets e checksum;
- split de quantidade 10 em 3: origem 7, destino 3, mesmo indice;
- primeiro slot livre escolhido;
- slots `-1`, `60`, `63` e `64` rejeitados;
- `Num` 0, 120 e negativo rejeitados;
- item nao agrupavel rejeitado;
- origem vazia rejeitada;
- `Num >= amount` rejeitado sem mutacao;
- carry cheio rejeitado sem mutacao;
- `sIndex` informado diferente do item real nao altera o resultado;
- dois frames `MSG_SendItem` emitidos na ordem destino, origem;
- estado salvo e restaurado no logout/desconexao;
- smoke E2E wire com `WydCdk.Client.E2E`, incluindo checksum, relogin e leitura do carry.

**Status atual:** C3 — wire, dominio, dispatcher e testes deterministas implementados.  
**Proxima acao:** selecionar o proximo bloco controlado apos a suite completa; a validacao com o `WYD.exe` visual continua pendente.
**Evidencia C4 (25/09/2026):** `WydCdk.Client.E2E` confirmou `0x02E5`/24,
dois `0x0182`/24 com checksum valido na ordem destino/origem e persistencia
7/3 apos logout e novo login. O status C5 permanece reservado ao cliente visual real.

## Ficha ITEM-006 — `_MSG_UpdateItem`

### Referencia analisada

- `W2PP/Source/Code/Basedef.h:2209-2222`: estados `NOTHING=0`, `OPEN=1`, `CLOSED=2`, `LOCKED=3` e estrutura `MSG_UpdateItem`.
- `W2PP/Source/Code/TMSrv/ProcessClientMessage.cpp:180-182`: roteamento para `Exec_MSG_UpdateItem`.
- `W2PP/Source/Code/TMSrv/_MSG_UpdateItem.cpp:21-105`: validacoes, consumo de chave e abertura normal do portao.
- `W2PP/Source/Code/TMSrv/CCastleZakum.cpp:72-208`: fluxo especial de portoes do Castelo Zakum.
- `W2PP/Source/Code/TMSrv/CCastleZakum.cpp:314-343`: `ProcessSecTimer`, decremento a cada dois segundos e limpeza da arena ao chegar a zero.
- `W2PP/Source/Code/Basedef.h:2527`: `_MSG_StartTime = 161 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME`.
- `W2PP/Source/Code/TMSrv/SendFunc.cpp:300-309`: `_MSG_StartTime` usa `MSG_STANDARDPARM` com um `int Parm`.
- `Backup/Tools/Reference759/ClienteSource/Projects/TMProject/Basedef.h:2721-3010`: tabela estatica `g_pGroundMask[10][4][6][6]`; o servidor usa os indices 0..5 por `MAX_GROUNDMASK=6`.
- `W2PP/Source/Code/Basedef.cpp:5948-6002`: `BASE_UpdateItem`, que altera a altura/malha do terreno pela mascara do item.

### Contrato de wire

| Campo | Valor |
|---|---|
| opcode | `116 + 0x0200 + 0x0100 = 0x0374` |
| direcao | cliente -> servidor e servidor -> clientes da cena |
| header | 12 bytes |
| payload | 8 bytes |
| pacote total | 20 bytes |
| payload offset 0 | `int ItemID` — no servidor legado e `gateid + 10000` |
| payload offset 4 | `int State` |
| resposta | o proprio `MSG_UpdateItem`, multicast na posicao do item |

### Comportamento legado identificado

1. Rejeita personagem morto ou fora de `USER_PLAY`, envia `SendHpMode` e registra `AddCrackError`.
2. Rejeita `State` fora de 0..5 e `ItemID` fora de `10000..10000+MAX_ITEM-1`.
3. Converte `ItemID` para o indice do agregado global `pItem[]`.
4. Delega primeiro ao `CCastleZakum::OpenCastleGate`; chaves 10..14 exigem nivel/quest e podem iniciar a quest, remover mobs antigos, gerar mobs, enviar `_MSG_StartTime` ao lider/party e iniciar timer.
5. No caminho normal, quando o item esta `LOCKED` ou o cliente pede estado 3, procura no carry um item com `EF_KEYID` igual ao portao, consome a chave e envia `MSG_SendItem` vazio.
6. Chaves especiais podem ser suprimidas para o item de indice 773 (`pItem[gateid].ITEM.sIndex != 773`).
7. `UpdateItem` altera o estado/malha de terreno; somente se houver alteracao, o servidor envia o frame para a visao `GridMulticast`.
8. A operacao registra coordenadas do portao e nao envia confirmacao privada adicional no caminho normal.

### Dependencias ainda ausentes no C#

- o fluxo Castelo Zakum agora inicia por chave, valida `EF_QUEST`, remove mobs da arena, gera os NPCs catalogados, envia `_MSG_StartTime`, abre os portoes internos e expira a quest pelo timer de dois segundos;
- o `SendHpMode`/`AddCrackError` ainda e apenas resultado/log no caminho C#, sem o mesmo aviso legado;
- a tabela oficial de `g_pGroundMask` foi portada para `LegacyGroundMaskTable.CreateOfficial`; a injecao continua disponivel somente para fixtures de teste;
- nao existe persistencia de estado de portao nem timer de `Delay`/`Decay` no storage do mundo.

### Decisao de port

O contrato wire foi implementado em `WydCdk.Protocol.UpdateItemMessages` e `WydCdk.Protocol.StartTimeSignal`. O caminho normal e o ramo Zakum foram ligados ao `WorldHub` e ao dispatcher com carregamento opt-in por `--init-item`; o estado inicial e o update usam a mesma chave `ItemID = pItem slot + 10000`, o multicast usa a janela 33x33 do item, o timer autoritativo reproduz o `SecCounter % 2` do legado e o timer de minuto reproduz o fechamento estatico `Delay 0 -> 1 -> LOCKED`.

### Proxima acao

`LegacyMapItemCatalog`/`LegacyMapItemState`/`LegacyGroundMaskTable` estao integrados ao `WorldHub`; `--init-item` carrega o catalogo, o login envia `MSG_CreateItem` dos itens estaticos e o caminho Zakum valida `EF_QUEST`, gera a quest, envia `_MSG_StartTime`, abre os portoes internos, fecha portoes estaticos no timer de minuto, decai drops dinamicos com `MSG_DecayItem` e limpa a arena ao expirar. A leitura do legado confirmou a volatilidade de `pItem[]`; o proximo passo e validar o fluxo no cliente 7559/7600/7662.

## MOVE-ACTION-ANALYSIS-001 — conta em Analise por velocidade severa (28/09/2026)

### Referencia e decisao

TMProject 7.69 `Servidor/Source/Code/TMSrv/_MSG_Action.cpp` aplica o efeito
somente dentro de uma violacao real de velocidade: `requestedSpeed >
(AttackRun & 0xF)`, seguida pelo limite inclusivo `requestedSpeed >= 20 ||
AttackRun >= 300`. A comparacao usa o `AttackRun` completo no segundo limite.
Depois dos cancelamentos de trade/loja, o handler marca a conta, persiste a
informacao de deteccao e impede que o Action continue. O `_MSG_AccountLogin.cpp`
consulta o estado de Analysis antes de autenticar a senha. O W2PP local confirma
a mascara de velocidade e o clamp, mas nao possui esse side effect severo de
conta; a regra nova e, portanto, especifica da referencia 7.69.

Ressalva de ABI: `STRUCT_SCORE.AttackRun` e `BYTE` no header servidor 7.69 e
`unsigned char` no header W2PP; o modelo ativo C# tambem e `byte`. Logo, embora
o predicado puro preserve o comparador `AttackRun >= 300`, essa metade do gate
nao e alcancavel com o campo autoritativo atual. `requestedSpeed >= 20` e o
gatilho severo efetivamente alcançavel. Nao alargar o campo nem o wire para
fabricar o score 300; o teste separa o limiar abstrato do maximo runtime 255.

O port nao copia `Ban/<conta>.bin` nem integra a variante binaria do W2PP. Usa
`IAccountReviewStore` e `FileAccountReviewStore`, independentes do backend de
senha/personagem e do Site. Cada conta normalizada tem `<CONTA>.review.json`,
schema `version=1`, `status=Analysis`, timestamp UTC e evidencia minima: motivo
estavel, personagem opcional, IP remoto opcional, velocidade pedida/permitida e
`AttackRun`. A primeira evidencia e mantida; arquivo temporario no mesmo volume,
flush e rename sem overwrite tornam visiveis somente documentos completos.
Locks locais e entre processos usam as mesmas 128 faixas. A faixa e calculada
com FNV-1a sobre o nome ASCII normalizado, de forma deterministica entre UP/PVP;
cada faixa tem um arquivo persistente `.account-review-lock-NNN.lck` aberto com
`FileShare.None`. `ReadStatus` e `MarkForAnalysis` adquirem o lock antes de ler
ou alterar o sidecar; a espera e cancelavel. Os arquivos de lock nao devem ser
apagados enquanto houver servidores ativos, e seus handles sao liberados pelo
sistema operacional no encerramento do processo. A quantidade fica limitada a
128, evitando criar um arquivo por tentativa de login. Isto e coordenacao entre
processos que compartilham o mesmo host e volume local, nao um lock distribuido
entre maquinas ou uma garantia para qualquer filesystem de rede.

`--account-review-root` e obrigatorio ao habilitar autenticacao por arquivo ou
MariaDB. Login de conta valida/normaliza a identidade, consulta o sidecar antes
da senha e fecha a sessao ao encontrar Analysis ou erro/corrupcao/raiz ausente.
O Action severo soma o `5/type 4` comum, adia o logout generico de crack-error,
grava a marca antes do relay, envia aviso neutro somente quando a persistencia
confirma e fecha a conexao. Falha de escrita envia aviso generico, fecha e nao
move. Violacao nao severa mantem o clamp anterior; score alto sem violacao nao
marca. Nenhum detalhe de senha, PIN, token ou MAC entra na evidencia.

O painel WinForms expoe a raiz em `%LOCALAPPDATA%\WydCdk\account-review`; o
construtor estatico de argumentos do instalador aponta para uma raiz persistente
fora da release. O arquivo nao expira e nao ha painel/operacao de liberacao neste
bloco. Host deve ser configurado com a mesma raiz persistente e acessivel em todos
os processos autenticados no host.

Validacao local em 28/09: suite `WydCdk.Engine.Tests` passou, solution build
terminou com 0 avisos/erros, e o harness loopback passou startup sem raiz,
falha de escrita, marca duravel, restart/login marcado com senha incorreta,
conta-controle e falha de leitura da raiz. Evidencia:
`Backup/Build/AutoTradeCloseSmoke/89f07045fd124e328a47c0b84fe6b08a`; hashes das
fixtures originais intactos e porta 18283 liberada. O leitor E2E consome frames
anteriores de inicializacao ate o `MessagePanel` (limite 32) e exige ausencia de
relay Action antes do aviso.

Validacao adicional local em 28/09 (MOVE-ACTION-ANALYSIS-002): harness abriu dois
processos reais do runner sob uma barreira comum. Com o lock mantido, ambos
aguardaram; apos a liberacao, uma marcacao retornou `Marked`, a outra
`AlreadyMarked`, e a evidencia da vencedora permaneceu no JSON. Um processo
leitor tambem aguardou o lock e leu `Analysis`. 160 contas de teste produziram
no maximo 128 arquivos de lock. Suite completa passou e `WydCdk.Engine.slnx`
buildou com 0 avisos/erros. O smoke TCP `ActionAnalysis` tambem passou contra o
listener local, cobrindo escrita indisponivel, marca duravel antes do EOF sem
relay/movimento, relogin bloqueado antes da senha, conta-controle aceita e raiz
indisponivel; fixtures originais intactas e porta 18283 liberada. Evidencia:
`Backup/Build/AutoTradeCloseSmoke/eda6e8c5b8084aa4a3b080a6c7f42498`. Esta
validacao e local; o runtime sandbox nao inclui este subbloco.

Deploy sandbox em 28/09: pacote runtime self-contained com 197 arquivos, sem
PDB/source/docs/segredos, SHA-256
`B70F7F6C5318EC54BBC879D8711B0624716768CE32A4D0C103422B71AD703E88`. As tarefas
UP/PVP estao Running em 8281/8282, servindo do executavel atualizado; `servtest.htm`
e os heartbeats `Site\.wyd-status\slot-1.heartbeat`/`slot-2.heartbeat` foram
atualizados. A raiz `C:\WYDCDK\account-review` e protegida por ACL para
SYSTEM/Administradores. Backup reversivel em
`C:\WYDCDK\Server\test-releases\backup-action-analysis-70c4d907`: runtime
anterior (202 arquivos, hashes 202/202 conferidos), wrappers, XML das tarefas e
ZIP original. Runtime novo tem 199 arquivos apos criar os dois logs de execucao,
zero PDB. Validacao remota e operacional; nao inclui login com conta real nem
smoke visual do WYD.exe. Site source/schema, producao, commit e push nao foram
alterados.

## MOVE-ACTION-ANALYSIS-003 — integracao concorrente UP/PVP local (28/09/2026)

Sem mudanca do runtime neste recorte: a meta foi fechar a evidencia TCP entre
dois hosts reais. Cada host usa uma raiz propria de contas (`UP/accounts` ou
`PVP/accounts`) e ambos recebem a mesma `--account-review-root`. Dois clientes
E2E independentes autenticam copias sinteticas da mesma identidade; caminhos de
estado e arquivos de barreira sao validados pelo parser contra a pasta isolada
do smoke. O harness segura o Action ate os dois clientes estarem prontos.

No run `Backup/Build/AutoTradeCloseSmoke/dual-world-analysis-64dd28c435ba434da3d724c8540e35d9`,
UP escutou em `127.0.0.1:18283` e PVP em `127.0.0.2:18283`. O runner manteve
aberto, com compartilhamento exclusivo, o `.account-review-lock-022.lck` da
conta normalizada `TESTE2`; os dois servidores registraram a chegada ao ramo
severo antes de persistir e nenhum marcou enquanto o lock externo estava retido.
Depois da liberacao, os resultados foram exatamente `Marked` e `AlreadyMarked`,
com um unico JSON `TESTE2.review.json` em `Analysis`. Os dois processos E2E
retornaram 0 e verificaram aviso, EOF e ausencia de relay Action; o autor nao
moveu e carry/cargo/Gold dos dois personagens permaneceram intactos. Hashes das
fixtures-fonte intactos; ambos os listeners foram encerrados e a porta ficou
livre. O mesmo diretorio
retém `engine-tests.log` com 296 PASS/0 FAIL.

A bateria single-host completa voltou a passar em
`Backup/Build/AutoTradeCloseSmoke/6a91756bb9b449de984a009b0728171e`; cobre
startup fail-closed, falha de escrita, marcacao duravel, relogin bloqueado antes
da senha, conta-controle e indisponibilidade de leitura. Build da solution:
0 avisos/0 erros; self-test dos caminhos/barreiras passou. Evidencia apenas
local e wire/headless: nao publica o runtime que esta no sandbox em ANALYSIS-001,
nao testa filesystem distribuido/entre maquinas nem substitui E2E visual. Site,
producao, commit e push ficaram intocados.

## MOVE-ACTION-ANALYSIS-004 - limites severos por TCP (28/09/2026)

O smoke `-ActionAnalysis` foi ampliado sem alterar o listener/runtime. Uma copia
isolada da conta de controle recebe `CurrentScore.AttackRun=255` no slot 0, valor
verificado no `CharacterLoginConfirmation` real. Speed15 (igual ao nibble
permitido) foi relayed ao autor e ao observador sem criar sidecar: score alto,
sozinho, nao marca. Speed19 produz uma violacao real contra `255 & 0xF == 15`,
mas fica abaixo do limite severo; o relay confirmou o clamp para Speed15, ambos
os clientes permaneceram conectados e nenhuma conta foi marcada. O caso
positivo Speed20 continua no mesmo smoke: aviso Analysis, sidecar duravel e EOF
antes de qualquer relay Action.

Isso fecha por rede o limiar inclusivo `requestedSpeed >= 20`, o requisito
`requestedSpeed > (AttackRun & 0xF)` e o controle de score maximo 255; nao alarga
o campo byte nem simula `AttackRun >= 300`. Continua deliberadamente sem copiar
`Ban/*.bin`; a semantica `SaveInfoDetect` nao foi auditada alem da evidencia
minima ja persistida pelo sidecar. O comparativo permanece: o clamp comum existe
no W2PP, o side effect severo e especifico da referencia 7.69.

`Engine.Tests`: 296 PASS/0 FAIL; `WydCdk.Engine.slnx`: build 0 avisos/0 erros;
self-test do harness e bateria TCP completa passaram. Evidencia retida:
`Backup/Build/AutoTradeCloseSmoke/4b071419ef1c4c01a127f4d55d10f615`; hashes das
fixtures-fonte intactos, listener filho encerrado e porta 18283 livre. O
readiness do smoke agora usa PID de `netstat`: foi reproduzido que
`Get-NetTCPConnection` retornava zero linhas mesmo com o listener real aceitando
conexao. Validacao continua local wire/headless; sem E2E visual, deploy, Site,
producao, commit ou push.

## MOVE-ACTION-ANALYSIS-005 - auditoria da evidencia SaveInfoDetect (28/09/2026)

### Fluxo observado na source 7.69

No ramo de violacao de velocidade de `_MSG_Action.cpp`, o servidor gera a
mensagem `etc,diffrent movement. <requestedSpeed>-<AttackRun>`, aplica o limite
severo e, quando atendido, chama `SendBanAccount(..., Banned::Analise)` seguido
de `SaveInfoDetect`. `SendBanAccount` define `Analyze`, registra uma primeira
linha em `LogType::Banned`, persiste o estado legado, envia a mensagem ao cliente
e chama `CloseUser`. Em uma sessao `USER_PLAY`, `CloseUser` inicia o save/quit e
`DeleteMob` marca o MOB como vazio/HP zero; em seguida o fluxo retorna a
`SaveInfoDetect`, que ainda le os campos da sessao e chama `MyLog` para uma
segunda linha. Essa ordem e comportamento estatico observado; nao foi executado
o binario C++ para confirmar os valores apos a desconexao.

`SaveInfoDetect` e generica e tambem aparece em branches de `Attack` e
`MessageChat`; esses gatilhos nao foram portados por esta auditoria. Para o ramo
Action, ela prefixa a conta e a descricao recebida e passa personagem, descricao
e IP a `MyLog(LogType::Banned, ...)`. `MyLog` formata data/hora usando `localtime`,
inclui o IP quando diferente de zero e escreve no handle `FBanned`. `StartLog`
abre `Log\Banned\Banned_DD_MM_YYYY_hh_mm_ss.txt` em modo `wt`; seu uso observado
e na inicializacao e no comando explicito de reinicio de logs. `LastLogBanned` e
atribuido, mas nao ha consumidor encontrado para rotacao automatica por dia.
O `SendBanAccount` tambem produz sua propria linha Banned antes de
`SaveInfoDetect`; portanto o legado pode emitir duas linhas para a deteccao.
Na source 7.69, a mensagem/log dessa rotina sobrescreve o texto inicial e chama
o caso `Analise` de `Tipo: Mac`, uma inconsistência textual de copy/paste que nao
foi adotada pelo port.

### Comparacao e decisao de escopo

O W2PP implementa `MyLog` com categoria/arquivo Banned e seu
`SendBanAccount(Analise)` registra uma linha dessa categoria, mas nao define ou
chama `SaveInfoDetect`; o handler Action W2PP mantem o clamp comum sem o side
effect severo 7.69. Logo, a evidencia extra e especifica do fluxo 7.69, nao uma
equivalencia herdada do port antigo.

O documento JSON versionado atual preserva conta, personagem opcional, endereco
remoto opcional, `reasonCode`, velocidade pedida e permitida, `AttackRun` e
`DetectedAtUtc`. Isso cobre de forma estruturada os fatos uteis da primeira
ocorrencia, sem copiar a formatacao textual nem o relogio local do logger. O
store e estado de revisao por conta: `AlreadyMarked` mantem a primeira evidencia
e nao acrescenta eventos. Portanto ele nao substitui um historico operacional
append-only equivalente ao `MyLog` quando houver repeticoes/concorrencia.

Decisao deste bloco: manter o sidecar atual como estado de Analysis e nao criar
um segundo logger sem requisito explicito de historico por ocorrencia. Se essa
necessidade for aprovada, tratar sink, retencao, concorrencia e privacidade em
recorte proprio. Nenhum runtime/schema/teste foi alterado; `Ban/*.bin`, outros
branches de deteccao e integracao Site continuam fora.

Fontes estaticas conferidas: TMProject 7.69, commit
`3ccc4d3377a1226648d58d59f87d7a020cabc8a1`; SHA-256 de `_MSG_Action.cpp`
`B4C1F469BF2A13616C7427904956131CF4AD417BFFA19A8040F49F0B28797907`,
`IlegalCMD.cpp` `3C14F2E759A3D9BBAFEDA281465435AD9134615C3E50B4D4B0475F9F813ADBC8`,
`SendFunc.cpp` `99C0B11FA212B4DE7F09F54B27E7378E39712C7C6B85FE1D366A93D8AD081E60`
e `Server.cpp` `F36EDBADD99529BA2E79143FA6933854EA30B1235B27CFBF17BCC0F47D4B3C5D`.
No W2PP local: `_MSG_Action.cpp`
`631E251BAB16F29D5015D02284F74112ED90FB5B4352A048B04A54A2B0483810`,
`SendFunc.cpp` `E1C02F89F8D4661E8D1C52A5EE5F8DC08AABB1FE7A0B2E9466F3BB2C3821045E`
e `Server.cpp` `08A444B0B13116C6ABCCECD42836A4F8948008884DFF1C0219D1DE1D291D10C4`.
