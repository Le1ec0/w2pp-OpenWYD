# Smoke E2E wire do cliente 7.670

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
