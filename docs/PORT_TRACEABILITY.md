# Matriz de rastreabilidade do port

## SKILL-SECOND-BAR-ACQUISITION-001 - aquisicao, wire e persistencia (27-29/10/2026)

Auditoria estatica local que orienta os ports registrados nesta secao. `R` significa
`Backup/Tools/ReferenceSources/TMProject2GlobalClient`, `C` significa
`R/Projects/TMProject` e `S` significa `R/Servidor/Source/Code`.
As buscas abrangeram `Learned[1]`, `LearnedSkill[1]`, `LearnedSkill2` e
`SecLearnedSkill` nos fontes C++ e os serializers/stores C# ativos. Ausencia
de escrita significa ausencia localizada nesse checkout, nao prova sobre
executaveis distribuidos ou servidores externos.

### Wire e consumidores confirmados

| Fronteira | Evidencia e contrato observado |
| --- | --- |
| Login servidor -> cliente | `C/Basedef.h:132` declara `unsigned int LearnedSkill[2]` no MOB; `C/TMFieldScene.cpp:18763` copia o MOB recebido para `m_stMobData`. `Server/src/WydCdk.Protocol/CharacterMobV769.cs:30,189-190` serializa os uint32 LE em +796/+800 do MOB de 1040 bytes. `CharacterLoginConfirmationV769.cs:12-29`: opcode `0x0114`, frame 1728, MOB em payload +4; portanto word 1 em payload +804/frame +816 (header 12). Nao sao offsets do blob W2PP. |
| UpdateEtc servidor -> cliente | `S/TMSrv/SendFunc.cpp:1313-1314` atribui `MOB.Learned[0/1]`, sem consultar Extra. `C/Basedef.h:965` declara os dois uint32 e `C/TMHuman.cpp:5196-5206` atualiza ambos e chama UpdateScoreUI. `UpdateEtcConfirmationV769.cs:11-22,62-63`: `0x0337`, frame 48/payload 36; words em payload +12/+16, frame +24/+28. |
| Skills no painel `S` | `C/TMFieldScene.cpp:13561-13635` percorre 24 controles base para `LearnedSkill[0]` e 12 adicionais para `LearnedSkill[1]`. `m_pSkillSecGrid2[i]` testa somente os bits `i=0..11` e cria o item `5400 + 12*Class + i`; `Class` escolhe o ID sintetico, nao desloca a mascara. `ResourceControl.h:1593-1616` mapeia os 24 controles base a secoes 1–3, slots 1–8; `:2187-2198` mapeia `i=0..3` a secao 1 slots 9–12, `i=4..7` a secao 2 e `i=8..11` a secao 3. `C/Basedef.cpp:1187-1194` converte item 5400+ em SkillData subtraindo 5200. Isto prova exibicao, nao aquisicao. |
| Consultas de validade | `C/Basedef.cpp:1198-1223`: excecoes 205/0x20, 233/0x200 e 238/0x4; demais 200..246 usam `1 << (4*((id-200)/4))`, e 247 e rejeitada. A formula nao equivale ao bit local da grade; a partir de 232 o shift chega a 32 ou mais, indefinido para int32 C++. Nao normalizar isso por modulo 32 nem inferir uma mascara universal. Ha consumidores reais em `TMFieldScene.cpp:5237-5366` (limites de especialidade). |
| Atalhos cliente -> servidor | `C/Basedef.h:1114`/`S/TMSrv/_MSG_SetShortSkill.cpp:3-8`: `0x0378` copia SkillBar[4] e CharShortSkill[16]. O comentario 105..152 descreve slots da barra, nao bits aprendidos. Salvar atalho nao concede skill. |

### Esclarecimento: paginas de atalhos versus skills aprendidas

Na UI 7.69, `Z` chama `OnKeySkillPage` e alterna os controles `GridSkillBelt2`
e `GridSkillBelt3` (`C/TMFieldScene.cpp:17245-17254`, `4395-4415`), cada um
com 10 slots (`UpdateSkillBelt`, `:14028-14064`). No login, o cliente monta
seus 20 atalhos copiando os quatro bytes `MOB.ShortSkill` e os 16 bytes do
bloco separado `CNFCharacterLogin.ShortSkill` (`C/TMSelectCharScene.cpp:1441-1442`).
Arrastar/remover atalhos envia o vetor de 20 bytes em `0x0378`
(`C/TMFieldScene.cpp:14269-14280`, `C/TMHuman.cpp:5179-5193`); o TMSrv separa
os primeiros quatro bytes em `MOB.SkillBar[4]` e os 16 restantes em
`CharShortSkill[16]` (`S/TMSrv/_MSG_SetShortSkill.cpp:21-26`). A chave `Z`
alterna a página visível localmente; o pacote salva as atribuições das duas
paginas, nao a pagina atualmente selecionada.

Esse estado ja tem caminho C# por personagem: o login carrega `ShortSkill[16]`
do slot do arquivo (`Server/src/WydCdk.World/LegacyFileAccountStore.cs:341`) e o adapter o envia junto
do MOB (`W2ppCharacterLoginV1Adapter.cs:31-39`); `0x0378` e aceito em USER_PLAY
e grava `SkillBar[4]` no MOB do slot e os outros 16 bytes no bloco ShortSkill
do mesmo slot (`Server/src/WydCdk.Server/Program.cs:2708-2716`,
`Server/src/WydCdk.World/LegacyFileAccountStore.cs:677-704`). Isso e separado da mascara
`LearnedSkill[1]`: a matriz `0x0337`/word 1 investiga quais skills estendidas
o cliente aceita/exibe, nao o conteudo ou a persistencia dos atalhos de cada
pagina. Nao usar evidencia de uma camada como prova da outra.

### Persistencia dos atalhos diante de snapshots antigos (28/10/2026)

O `0x0378` persistia corretamente os 20 atalhos, mas o snapshot `MOB` mantido
no participante podia estar desatualizado. No logout, o salvamento completo
reescrevia `SkillBar[4]` com esses quatro bytes antigos; a persistencia atomica
de estado usada pelo trade tinha o mesmo risco. Os stores de arquivo e MariaDB
agora preservam os quatro bytes ja armazenados ao aplicar um MOB completo,
tanto no salvamento normal quanto no atomico. `ShortSkillWireAndPersistence`
reproduz logout e salvamento atomico com snapshots antigos. A primeira execucao
da suite Engine com a DLL World atualizada passou 537/537. Na repeticao depois
do build completo, o caso `ShortSkillWireAndPersistence` e os demais casos de
persistencia passaram; o total foi 535/537 porque dois testes de loopback nao
abriram listener/conexao no runner (um reportou WSA 10013). O build completo de
`Server/WydCdk.Engine.slnx` passou em modo serial, com 0 avisos/erros e saida
isolada em `bin/CodexVerify`. `dotnet --info` identifica SDK 10.0.401, mas
termina com acesso negado ao Service Control Manager. A suite exercita o store
de arquivo; nao houve integracao MariaDB nem E2E visual nesta correcao.

### Aquisicao: caminhos distintos, sem ponte comprovada

`S/Basedef.h:647-655` declara union `Learned[2]` com aliases `LearnedSkill`
e `LearnedSkill2`. Nas buscas nao foi localizada escrita especifica no word 1
ou alias, apenas declaracao e leitura por SendEtc. O header privado
`S/TMSrv/Basedef.h:479` ainda declara MOB com um unico `long LearnedSkill`;
essa inconsistencia de revisoes impede tratar o sender como oracle de um
servidor 7.69 executado e compativel com o cliente.

O pedido comum de aprendizado `0x0277` (`C/Basedef.h:1280`,
`TMFieldScene.cpp:16588-16594`) envia BonusType=2/Detail/TargetID.
`S/TMSrv/_MSG_ApplyBonus.cpp:115-122` so admite Detail 5000..5095 e grava
`MOB.LearnedSkill` base; nao foi encontrado ramo 5400..5447 para a segunda
barra. O uso de item tem opcode `0x0373` (`C/Basedef.h:799`).

Em `S/TMSrv/_MSG_Use_Item.cpp:5869-6082`, os livros TK 5500/5504/5508
exigem nivel interno >199, classe 0, ClassMaster diferente de MORTAL/ARCH,
oitava skill base nos bits 7/15/23 e exclusao entre as tres nonas. Gravam
respectivamente **Extra.SecLearnedSkill 0x1/0x10/0x100**, consomem uma unidade
e chamam SendEtc. Nao escrevem `MOB.Learned[1]`; logo essa chamada nao fecha
a atualizacao da grade. Outro desencaixe: 5500 grava 0x1, mas o proc de
Protecao Divina/201 consulta **0x2** em `_MSG_Attack.cpp:717`; o C# segue esse
gate de combate em `LegacyMobExtra.DivineBlessingLearnedBit`. Nao corrigir o
bit de um lado por presuncao. `set seclearned` e `secclearskill` em
`S/TMSrv/_MSG_ProcessCommand.cpp:59-78` tambem alteram somente Extra; nao sao
prova de aquisicao normal nem de sincronizacao dos words.

O ramo Sephira (`S/TMSrv/_MSG_Use_Item.cpp:6085` em diante; W2PP
`Source/Code/TMSrv/_MSG_UseItem.cpp:1831-1851`) aceita Vol31..38 e grava
`1 << (Vol-7)` no word base. Nao e producer do word adicional. Os itens
5514-5524 tem `EF_VOLATILE=32` nos catalogos 7.69 e W2PP; 5514-5523 indicam
classe 2 e 5524 classe 4. Os nomes combinam com rows 214 (`Life_Wave_Book` /
`Life_Wave`), 215 (`Holy_Power_Book` / `Holy_Power`) e 217-224, inclusive
5517 `Livro_de_Skill_Congelamento_Proficiente`, 5518 `Thunder_Force_Book`,
5519 `Thunder_Lising_Book` e 5520
`Remover_Memoria`, mas isso e evidencia de intencao, nao prova de crosswalk
funcional: o ramo generico grava o mesmo bit base 25 para todos e nao escreve
`LearnedSkill[1]`. Para rows 214 e 215 o cliente consulta o bit 12; para rows
218 e 219, o bit 16 de
`LearnedSkill[1]`; o mesmo bit 16 vale para 217. Para 220, consulta o bit 20.
As associacoes item/row nao
ligam esses bits ao produtor observado. Os itens 5525-5528 tambem compartilham
Vol32/bit25 e nao comprovam crosswalk individual para rows 225-228.
Na W2PP nao foram localizados os handlers especificos 5500/5504/5508; o seu
`Basedef.h:626` preserva SecLearnedSkill e o uso de Espelho em
`Basedef.cpp:3874` esta comentado. `_MSG_ApplyBonus.cpp` permanece na arvore
base. Ausencia W2PP nao invalida os tres handlers comprovados na referencia.

### Port local dos livros 5500/5504/5508 (29/10/2026)

O C# porta separadamente os livros 5500, 5504 e 5508 em
`WorldHub.TryApplyDivineProtectionBook`, `WorldHub.TryApplyBloodHandsBook` e
`WorldHub.TryApplyMagicMirrorBook`. Todos validam nivel >199, ClassMaster nao
MORTAL/ARCH, classe 0, livro no inventario, skill base exigida e bits de
conflito/duplicidade conforme o ramo 7.69; consomem uma unidade e alteram
somente `MOBEXTRA.SecLearnedSkill`. O 5500 grava `0x1` com pre-requisito base
bit 7; o 5504 grava `0x10` com bit 15; e o 5508 grava `0x100` com bit 23.
`LegacyMobExtra.TrySetMagicMirrorLearnedBit` e
`TrySetDivineProtectionBookLearnedBit` preservam os demais bits.

O bit 5508 ja e consumido por `WorldHub.TryConsumeSkillManaLocked` para a taxa
adicional de Mana de skills da classe 0. O livro 5500 grava o bit `0x1`, mas o
consumidor de combate de Protecao Divina usa `0x2`; os comportamentos foram
portados como observados, sem ligar os bits por suposicao. Nenhum handler grava
`MOB.LearnedSkill[1]`, altera schema ou cria crosswalk. Engine.Tests passou
`540/540`; `dotnet build Server/WydCdk.Engine.slnx --no-restore` passou com 0
avisos/erros. `dotnet --info` identificou SDK
10.0.401, mas saiu com acesso negado ao Service Control Manager. O usuario
adiou o E2E visual; consumo no cliente e save/reload de Extra ainda nao foram
observados.

### Port local dos livros Sephira Vol31-38 (29/10/2026)

TMProject 7.69 (`S/TMSrv/_MSG_Use_Item.cpp:6096-6125`) e W2PP
(`W2PP/Source/Code/TMSrv/_MSG_UseItem.cpp:1831-1868`) concordam no ramo
generico: valida `EF_VOLATILE` 31..38, calcula `1 << (Vol-7)`, rejeita bit ja
aprendido, grava somente `MOB.LearnedSkill` base, envia `SendEtc`, chama
`SetAffect(44,20,20)` e consome uma unidade. Nao ha gate de classe/level neste
ramo, e ele nao escreve `MOBEXTRA` nem `LearnedSkill[1]`.

`WorldHub.TryApplySephiraSkillBook` porta esse caminho depois dos handlers
especificos 5500/5504/5508. O Type 9 de SkillData 44 usa valor 90 e tempo 15
apos o loader 7.69; `SetAffect` com delay/level 20 produz tempo 3 e level 20.
Quando os gates de SetAffect permitem, o C# atualiza o affect e reprojeta o
score preservando HP/MP. A aquisicao e o consumo prosseguem mesmo se o affect
nao puder ser aplicado, como no retorno ignorado pelo handler fonte. O Host
envia mensagem e `UpdateEtc`, seguindo as chamadas wire explicitas da fonte.
O teste cobre Vol31/32/38, limites fora da faixa, duplicidade, stack, Type 9,
score e ausencia de mutacao em `MOBEXTRA`. Engine.Tests passou `541/541`; build
completa `WydCdk.Engine.slnx --no-restore` passou com 0 avisos/erros. `dotnet
--info` identificou SDK 10.0.401 e saiu com acesso negado ao Service Control
Manager. E2E visual continua adiado; sem alteracao de schema, commit, push ou
deploy.

### Armazenamento e perda na projecao C#

Na referencia 7.69, `S/TMSrv/Server.cpp:8912-8945` copia MOB e Extra
separadamente para DBSaveMob; `S/DBSrv/CFileDB.cpp:1292-1308` grava
`File.Char[Slot]` e `File.mobExtra[Slot]` e chama DBWriteAccount.
`DBWriteAccount:2578-2635` escreve STRUCT_ACCOUNTFILE bruto em
`./account/<chave>/<conta>`. O retorno DB de login copia Char, mas a copia de
mobExtra esta comentada (`CFileDB.cpp:1167-1179`). Sao pontos de armazenamento
declarados, nao roundtrip validado: os headers comum/privado divergem e a
copia de Extra no handler TMSrv tambem esta comentada em
`DataServerPackageHandler.cpp:736`. Nao afirmar persistencia E2E 7.69.
W2PP usa a mesma separacao em `TMSrv/Server.cpp:7847-7877` e
`DBSrv/CFileDB.cpp:1184-1185`, com um unico word no MOB.

No C#, `LegacyAccountSnapshot.cs:33-49,72-73` fixa blob W2PP de 7945 bytes:
MOB do slot em `216+816*slot`, word base em MOB +780; **+784 e Magic**, nao
word de segunda barra. Extra em `5600+552*slot`, SecLearnedSkill em Extra +4
(uint32 LE). `LegacyFileAccountStore.cs:343,476-515` carrega/salva MOB e Extra;
`MariaDbWorldCharacterStore.cs:90,173-191,574,716` faz o mesmo no
`wyd_world_account_state.account_blob`, por conta/mundo. O campo Extra e
preservavel, mas nao existe ali um campo canonico de `MOB.Learned[1]`.

`W2ppCharacterMobV1Adapter.cs:129-130` projeta word base e zero para word 1.
No refresh, `UpdateEtcConfirmation.cs:25` escreve o uint32 base em um int64,
com metade alta zero; `W2ppUpdateEtcV1Adapter.cs:31-36` divide esse int64 em
dois words. O adapter UpdateEtc aceita uma metade alta nao nula, mas a origem
ativa nao a produz (`Program.cs:4054-4056`; `WorldHub.cs:14471`). Por isso,
alterar apenas o login permitiria que o proximo UpdateEtc apagasse a exibicao.
`LegacyMobExtra.cs` le os flags de combate e fornece setters para fixtures;
isso nao implementa uso de livro, concessao autoritativa ou aprendizado.

### Decisao e proximo passo executavel

Manter runtime e ambos os caminhos de projecao atuais. **Nao ligar diretamente
SecLearnedSkill a LearnedSkill[1]**, nao ocupar Magic/padding do blob e nao
criar schema/sidecar neste bloco. A source demonstra tres estados separados:
arvore base, word adicional de exibicao/consultas do cliente e flags Extra de
combate; nao demonstra sua sincronizacao por aquisicao.

### Prova dinamica inicial (28/10/2026)

Foi executada uma rodada com a fixture descartavel
`Backup/Build/SkillSecondBarVisual` e o cliente isolado identificado pelo SHA-256
`51BE30D3E55231D300ABD7BD41E046690DD43EE6A4DAF5F9CD9AF4A92EFF03E2`. O MOB
persistido na fixture tinha word base `0x80` e Extra `0x10`. O proxy local nao
alterou o `0x0114` (word 1 recebido/enviado `0x0`) e reescreveu somente o word 1
do `0x0337` de `0x0` para `0x10`, recalculando e conferindo o checksum. Host
registrou login do slot 0; o cliente chegou ao mapa, abriu a janela de tecnicas
e permaneceu conectado apos o refresh. Saida integral e observacao em
`Backup/Build/SkillSecondBarVisual/capture-2026-10-28.md`.

Essa rodada prova a entrega dos dois valores no wire e o recebimento do refresh
no cliente; nao atribui uma celula especifica ao bit 4, nao prova aquisicao ou
save/reload e nao estabelece equivalencia com Extra. Ainda faltam os casos de
controle e os limites/consumidores da grade.

### Hit-test do atalho versus espaço visual (28/10/2026)

Na sessão visual da fixture `TESTE`/`SKILLBAR`, o usuário confirmou quatro
arrastes do painel `S` mirando os espaços 1–4 da página 2. O cliente e o arquivo
persistido mostram destinos 1, 2, 3 e 5: o quarto destino pretendido (índice
global 13) ficou vazio e o índice 14 recebeu `0x07`. O handler salvou quatro
vetores; o proxy continuou como relay cru, sem captura do payload `0x0378`. O
usuário observa vários componentes fora de posição e levantou escala/resolução
como hipótese. Detalhes e bytes em
`Backup/Build/SkillSecondBarMatrix-20261028/capture-2026-10-28.md`.

O caminho da source 7.69 usa `SGridControl::OnMouseEvent` (`SGrid.cpp:146-150`)
para derivar `nCellX` com largura de célula convertida para `int`; a renderização
dos itens de `GRID_SKILLB` usa `m_nWidth / m_nColumnGridCount` em ponto flutuante
(`SGrid.cpp:564-567`). A largura do controle pode, portanto, renderizar passos
fracionários e usar passos inteiros no hit-test. `RenderDevice` calcula ratios na
criação (`RenderDevice.cpp:64-65`), o construtor de `SControl` escala posição e
tamanho (`SControl.cpp:31-36`), e `NewApp::MsgProc` encaminha `LOWORD/HIWORD(lParam)`
sem transformação (`NewApp.cpp:820-821`). Se o client rect mudar depois,
`D3DDevice::HandlePossibleSizeChange` atualiza o backbuffer mas não recalcula
ratios (`D3DDevice.cpp:730-767`). O `Config.bin` presente na fixture declara
`Config[0]=3`, mapeado pela source para 1024×768, e a captura mede 1026×800; não
foi confirmado que esse é o arquivo lido pelo processo nem se há DPI virtualization.
Arredondamento ou mismatch de escala é compatível com a observação, mas largura
real da grade, área visual e coordenadas do input ainda não foram medidas.

O PC havia entrado em modo de espera; depois de acordar, o cliente voltou a ficar
visível. O assistente tentou arrastar o mesmo ícone para o centro do espaço 4 na
página 2 e, depois, na página 1; nenhuma tentativa gerou nova linha `Short skills
saved` ou mudou o blob, cujo timestamp permaneceu 21:47:58. Essas ações
automatizadas não validam o hit-test. Próxima prova: medir os limites da grade e
comparar um drop de input confirmado nas duas páginas em 1024×768 e 800×600 na
fixture descartável; não corrigir cliente/runtime antes dessa medição.

### Prova de wire 0x100 e limite da cena visual (28/10/2026)

Na fixture `trial-batch-word1-0x100-extra-0x10`, o proxy de loopback registrou
em tres entradas do personagem `0x0114` com `word0=0x80` e `word1=0`, seguido
de `0x0337` com word 1 antigo `0` e enviado `0x100`. O Host aceitou a conta
sintetica `TESTE`, slot 0. Portanto `0x100` foi confirmado no wire; isso nao
fecha a observacao visual do bit.

O `WYD.log` do cliente isolado registra `Fail to Load TileMap :
env\Field0000.trn` em 14:30:15 e 14:33:03. A source 7.69 constrói o caminho em
`TMFieldScene.cpp:1381-1389` a partir de `HomeTownX/Y >> 7`. A conta sintetica
estava salva em `(2091,2093)`, que resulta em `Field1616`; `Field1616.trn/.dat`
existem na fixture do cliente. O mesmo log registra uma nova cena às 14:33:13
sem outra falha de TileMap. Nas tres respostas `0x0114` da bateria, o logger do
Host registrou os campos separados `PosX/PosY` como `(2096,2105)`, `(2091,2093)`
e `(2095,2105)`, todos nao zero. O handler do cliente copia o MOB e em seguida
sobrescreve `HomeTownX/Y` com esses campos separados
(`TMSelectCharScene.cpp:1436-1440`); a carga do mapa usa o `HomeTown` resultante
(`TMFieldScene.cpp:1381-1386`). Portanto, nessas entradas, `Field0000` nao foi
causado por coordenadas zero na resposta `0x0114` enviada pelo Host. A hipotese
de uma cena transitoria com valores zero permanece sem prova, e a fase exata que
tenta abrir `Field0000` ainda nao foi isolada. O `0x010A` redigido nao e a origem
das coordenadas usadas por esta carga da cena de jogo.

O caminho estatico e sincrono: `TMSelectCharScene.cpp:1436-1451` copia o MOB,
aplica `PosX/PosY` e chama `SetCurrentState(TM_FIELD_STATE)`;
`ObjectManager.cpp:714-751` cria e inicializa `TMFieldScene` diretamente; a
inicializacao calcula o mapa a partir do `HomeTown` em
`TMFieldScene.cpp:1380-1389`. A busca de escritas integrais localizou limpezas
do MOB no construtor de `ObjectManager` e no construtor de `TMSelectServerScene`,
nao entre a resposta de login e a troca normal para o mapa. Isso estreita a
divergencia, mas ainda nao explica o log: a proxima captura precisa registrar os
valores no inicio de `InitializeScene`/antes de `LoadTileMap`, no mesmo processo
de cliente que gera `WYD.log`.

O Host registra movimento aceito de `(2095,2105)` para `(2083,2093)`, pedido
recebido do cliente, nao um teleporte originado pelo Host. A captura visual
mostrou `Critical Data Error In Client`; nao foi possivel validar a grade para
`0x100`. Evidencia detalhada em
`Backup/Build/SkillSecondBarMatrix-20261028/trial-batch-word1-0x100-extra-0x10.md`.
Sem alteracao de runtime/schema e sem build/testes neste registro documental.

### Matriz loopback independente (28/10/2026)

Sete fixtures descartaveis variaram `LearnedSkill[1]` e
`Extra.SecLearnedSkill` independentemente, mantendo classe 0, personagem/slot,
word base `0x80`, atalhos zero e spawn de guilda constantes. O proxy injetou os
valores no login `0x0114` e refresh `0x0337`; em cada frame o word antigo era
zero e o enviado era o valor controlado. As combinacoes foram:

| `LearnedSkill[1]` | `Extra.SecLearnedSkill` | Resultado |
| ---: | ---: | --- |
| `0` | `0x10` | login/refresh headless passaram |
| `1` | `0x10` | login/refresh headless passaram |
| `2` | `0x10` | login/refresh headless passaram |
| `0x10` | `0x10` | login/refresh headless passaram |
| `0x100` | `0x10` | login/refresh headless passaram |
| `0x10` | `0` | login/refresh headless passaram |
| `0x10` | `0x100` | login/refresh headless passaram |

As sete coordenadas recebidas foram `(2088,2148)`, o GuildSpawn controlado de
Armia; este resultado nao valida o spawn normal lembrado pelo usuario como
`(2100,2100)`. O harness terminou `CLIENT_E2E_PASS` em todos os casos. Ele nao
abriu a cena 3D por variante. Os atalhos permaneceram zero e nao houve
`0x0378` no smoke; o fim do teste nao prova save/reload. `Extra` foi relido do
blob local com o valor independente da fixture. Logs completos e linhas exatas
em `Backup/Build/SkillSecondBarMatrix-20261028/capture-2026-10-28.md` e nos
diretorios `trial-headless-r7-*`.

Na sessao visual aberta separadamente, `Z` alternou a barra visivel de 1 para 2
e de volta para 1. A janela Tecnicas mostrou um unico icone em Confiança; como
os icones foram apontados como deslocados, nao foi inferido um ID/crosswalk nem
alterado um atalho. Nao houve relogin apos a alternancia. A source inicializa
`m_bSkillBeltSwitch=0`, mas o reset apos relogin nao foi repetido visualmente.

O contrato de espaco nao significa 20 skills preenchidas no nascimento. O
TMSrv inicializa `ShortSkill[16]` do slot novo em `0xFF` e copia o MOB base da
classe (`S/DBSrv/CFileDB.cpp:988-1002`). No cliente, os primeiros quatro bytes
vem de `MOB.ShortSkill` e os 16 seguintes de `CNFCharacterLogin.ShortSkill`
(`C/TMSelectCharScene.cpp:1441-1448`); cada pagina mostra dez posicoes. Os
quatro valores iniciais dependem do MOB base configurado para a classe, e a
matriz/headless desta rodada nao verificou esse template em uma criacao visual.
Assim, ha 20 espacos de atalho por personagem, mas esta prova nao sustenta que
20 skills distintas ja nascam atribuidas ou aprendidas.

Conclusao limitada: a matriz demonstra que os words controlados chegam
separadamente ao cliente e que Extra permanece um campo separado na fixture.
Ela nao demonstra origem canonica/persistencia de `LearnedSkill[1]`, relacao de
bits com skills exibidas, nem aquisicao historica. A regressao
`ShortSkillWireAndPersistence` passou no harness completo `537/537` e cobre os
20 atalhos por personagem contra MOB stale e save atomico.

### Tentativa com atalhos distinguiveis (29/10/2026)

Uma fixture visual nova foi preparada em
`Backup/Build/SkillSecondBarMatrix-20261028/trial-relogin-20261028-01/fixture-current/SkillSecondBarVisual`
com `MOB.SkillBar=01020304` e os 16 bytes `CNFCharacterLogin.ShortSkill`
distintos (`05060708090A0B0C0D0E0F1011121314`). O harness E2E foi reconstruido
em saida isolada (`bin/CodexVerifySecondBar/`), com build sem avisos/erros. A
copia do cliente reteve o hash validado `51BE30D3...EFF03E2` e serverlist
loopback. A sessao Windows estava bloqueada; nao houve login, cena, conexao do
cliente, captura de wire ou observacao visual. Host/proxy loopback foram
encerrados. O relogin e a pagina inicial seguem pendentes; retomar apos o
desbloqueio da sessao. Evidencia em
`Backup/Build/SkillSecondBarMatrix-20261028/capture-2026-10-28.md`.

### Proxima prova

Para o word adicional, concluir uma sessao visual controlada com 20 atalhos
distinguiveis, confirmar a emissao de `0x0378`, save/reload e pagina inicial apos
relogin; registrar como nao observado qualquer passo que a UI nao permita
reproduzir. Repetir os bits locais 0/4/8 nas outras classes e conferir limites
de especialidade sem tratar shifts indefinidos como contrato. Os handlers TK
5500/5504/5508 foram portados separadamente para Extra, sem decidir estado
canonico nem criar ponte para `LearnedSkill[1]`. O Sephira Vol31..38 foi
portado separadamente para o word base, tambem sem ponte. A divergencia 5500
`0x1` versus proc `0x2` fica como aceite separado. Injetar bits em fixture prova
consumidores; os testes dos tres livros TK provam apenas mutacao Extra e
consumo, enquanto o teste Sephira cobre somente o word base e o efeito Type 9.

Validacao estatica: comparacao das fontes, serializers e offsets. O runtime foi
alterado em recortes locais para portar os livros TK 5500/5504/5508 em
`MOBEXTRA.SecLearnedSkill` e o handler generico Sephira Vol31..38 no word base;
sem banco, schema ou ponte para o word adicional. A prova visual anterior usou
Host autenticado somente com conta sintetica e loopback, e cliente visual
isolado; ela nao verificou o uso/consumo visual dos livros nem save/reload.
O usuario adiou essa verificacao, sem deploy, commit ou push. A contagem de
skills completas nao mudou.

Continuacao autorizada (27/10): o executavel arquivado
`v769ClientRelease/WYD.exe` tem hash
`C248788036749D1728B2F2ABC5F7CE6934977350E24738021AFEC35AE6DBBE3F`.
Uma copia em %TEMP% foi examinada por Windows Defender via MpCmdRun e o
resultado foi "found no threats". Isso cobre somente aquele hash. Nenhum
executavel foi iniciado: o README requer Visual Studio C++ e o projeto grava
em `v769ClientRelease` por padrao; a regra local exige build isolada com
saida/intermediarios fora da source antes do primeiro smoke. O modulo
Get-MpComputerStatus tambem nao carregou por falta de suporte WS-Management,
entao a evidencia AV acima veio do MpCmdRun direto. Proximo passo: confirmar
instalacao/toolchain VS, copiar source/recursos necessarios para arvore
descartavel fora de Backup e compilar com OutDir/IntDir externos; escanear o
resultado identificado por hash antes de qualquer execucao loopback. Ainda sem
prova dinamica do cliente ou validacao de runtime.

Descoberta da toolchain: vswhere encontrou Visual Studio
`C:\\Program Files\\Microsoft Visual Studio\\18\\Community`, com MSVC
`14.51.36231`; a consulta do componente
`Microsoft.VisualStudio.Component.VC.v142.x86.x64` nao encontrou instalacao
v142, embora `TMProject.vcxproj` solicite `PlatformToolset=v142`. Nao forcei
override para v143/v145 nem instalei componentes. O usuario autorizou tentativa
isolada em 14.51 (v145).

A copia isolada ficou em
C:\Users\le1ec\.codex\visualizations\2026\10\27\01a17e8f-1b88-7a83-a30d-163b7c8d90fa\skill-second-bar
(somente Projects/TMProject e Dependencies/Directx; fonte+dependencias
44.017.467 bytes; hash do vcxproj
506CF3A40220983E4424E840B00D7E2EED6AC48C16349E76BF2375E795AB1A3C).
Release compilou translation units mas link falhou LNK1101 por versao incorreta
de MSPDB140.DLL. Debug repetiu LNK1101. Debug com
GenerateDebugInformation=false e LinkIncremental=false tambem repetiu LNK1101.
Ocorreram C5298 em DirectX dxfile.h U+001A e LNK4075 sobre
EDITANDCONTINUE/INCREMENTAL:NO. Os WYD.exe de saida sao parciais, sem build
concluida, e nao foram executados. MpCmdRun reportou no threats para uma copia
parcial de Release durante link; nao considerar isso aprovacao de binario. O
unico scan limpo e do hash do executavel arquivado citado acima.

Proxima dependencia: obter uma combinacao coerente de MSBuild, link.exe e
MSPDB140.DLL (ou v142), sem misturar componentes da instalacao. Depois repetir
build isolada, confirmar exit 0, hash e AV; somente entao configurar o loopback
e observar pacotes e grade. Nao ha ainda prova dinamica nem alteracao de runtime.

### Prova visual de LearnedSkill[1] (29/10/2026)

O usuario entrou manualmente na fixture isolada `TESTE/SKILLBAR` e chegou ao mapa. O proxy registrou `0x0114` e `0x0337` com `word0=0x00000080`, `word1-old=0` e `word1-sent=0x80001113`; a fixture mantinha `Extra.SecLearnedSkill=0x10`. No painel `S`, o usuario reportou Confiança nas celulas 8–10 (Destino, Proteção Divina e Benção Divina), Trans na celula 9 (Mãos Sangrentas) e Espada Mágica na celula 9 (Espelho Mágico).

O resultado corresponde ao mapeamento dos controles: o bit base `0x80` ocupa Confiança 8; os bits baixos 0 e 1 de `word1` ocupam Confiança 9 e 10; os bits 4 (`0x10`) e 8 (`0x100`) ocupam Trans 9 e Espada Mágica 9. Os bits 12 e 31 do sentinela não são lidos pelo loop de 12 controles desse painel. Os nomes visuais não estabelecem crosswalk com SkillData nem produtor de aquisição. Esta prova confirma somente renderização do valor injetado, não aquisição, persistência ou save/reload de `LearnedSkill[1]`.

O frame de login também trouxe `MOB.SkillBar=FFFFFFFF` e `CNFCharacterLogin.ShortSkill=FFFF07FFFFFFFFFFFFFFFFFFFFFFFFFF`; esta sessão não valida o vetor distinguível de 20 atalhos preparado antes. Nenhuma alteração de runtime ou schema.

### Port isolado do livro Mãos Sangrentas 5504 (29/10/2026)

TMProject 7.69 `_MSG_Use_Item.cpp:5944-5993` exige `BaseScore.Level > 199`,
`ClassMaster` diferente de MORTAL/ARCH, `MOB.Class == 0`, ausencia dos bits
`SecLearnedSkill` 0 e 8, e o bit 15 aprendido no word base. Se o bit 4 de Extra
ja estiver setado, rejeita duplicata; no sucesso grava somente `0x10`, envia
mensagem/`SendEtc` e consome uma unidade. Nos erros de nivel, classe, conflito,
pre-requisito e duplicata, devolve o item sem consumo. O handler W2PP
`_MSG_UseItem.cpp` nao possui ramo 5500/5504/5508.

O gate do livro testa `SecLearnedSkill & 0x1`, enquanto o consumidor C# existente
da habilidade de combate Proteção Divina usa `0x2`; o bit de Espelho Mágico e
`0x100`. O port mantém a verificacao literal do livro e nao altera o consumidor
de combate. O teste confirma que Extra `0x2` nao bloqueia o livro e sobrevive a
adicao de `0x10`, junto com outros bits nao conflitantes.

`WorldHub.TryApplyBloodHandsBook` valida slot, vida e tamanho dos snapshots,
aplica os gates, escreve apenas `MOBEXTRA.SecLearnedSkill |= 0x10` e desconta
uma unidade. `Program.cs` intercepta item 5504 no opcode `0x0373`, devolve o item
em recusas e envia aviso/refresh no sucesso. A atualizacao `UpdateEtc` ativa
continua com word adicional zero; nenhuma celula da segunda barra e esperada
como resultado deste livro neste port. `LegacyAccountSnapshot` mantem o blob
7945 bytes e `LearnedSkill[1]` nao e modificado.

Validacao local: Engine.Tests passou `538/538`; build de `WydCdk.Engine.slnx
--no-restore` passou com 0 avisos e 0 erros no SDK 10.0.401. `dotnet --info`
identificou SDK 10.0.401, mas encerrou com codigo 1 por acesso negado ao Service
Control Manager. A fixture sintetica separada
`Backup/Build/SkillSecondBarBloodHandsBook` foi preparada para validacao manual;
uso visual, persistencia/relogin de Extra e smoke cliente continuam pendentes.

## MOB-AI-001 - Skill tree 7.69, Skills 225-247 e 204 (27/10/2026)

### Contrato da arvore e politica das fontes

TMProject 7.69 define o contrato principal de conteudo e comportamento desta
release. W2PP e fonte complementar de comparacao para entender caminhos e
divergencias; a ausencia de um ramo na W2PP nao invalida uma mecanica comprovada
na 7.69.

TMProject `TMSrv/_MSG_ApplyBonus.cpp:118-122` calcula classe e posicao a partir
de `Detail` em blocos de 24: sao 24 definicoes base por classe, organizadas em
3 especialidades de 8. O cliente trata uma faixa adicional: os indices de barra
105-152 convertem-se nos IDs SkillData 200-247, em blocos de 12 por classe
(TK 200-211, Foema 212-223, BM 224-235, Hunter 236-247). O comentario em
`TMSrv/_MSG_SetShortSkill.cpp:12-32` chama a faixa de skills 9a a 12a da barra;
isso confirma as entradas e posicoes da segunda barra, sem provar que cada uma
tenha caminho de aprendizado e persistencia implementado.

Os tres livros confirmados em `TMSrv/_MSG_Use_Item.cpp:5869-6040` liberam para
TK uma skill 9a de cada especialidade (itens 5500, 5504 e 5508); o personagem
pode manter somente uma dessas tres via `MOBEXTRA.SecLearnedSkill`. Alem disso,
o handler generico de livros Sephira nas duas fontes aceita `Vol=31-38` e grava
`1 << (Vol-7)` em `MOB.LearnedSkill`. Os itens 5525
`Livro_de_Skill_Chama_Resistente`, 5526 `Resi_Decrease_Book`, 5527
`Manaburn_Master_Book` e 5528 `Unidade_Mental` tem `EF_VOLATILE=32`, portanto
todos gravam o bit 25. Essa rota confirma a aquisicao de um bit base, mas nao
comprova crosswalk individual para as rows 225-228; nenhum efeito row-specific foi ligado
ao bit 25. `MAX_SKILL=24` (TMProject `Basedef.h:125`; W2PP `Basedef.h:171`) e
`BASE_GetBonusSkillPoint` percorre somente os bits 0-23 (TMProject
`Basedef.cpp:989-998`; W2PP `Basedef.cpp:878-887`). O `_MSG_ApplyBonus`
encontrado cobre `Detail=5000-5095`.
O adapter C# zera o segundo word de `LearnedSkill`, e `SecLearnedSkill` e um
campo distinto. O fluxo de aquisicao/crosswalk funcional da segunda barra
continua pendente. O inventario confirma 24 definicoes base e 48 IDs adicionais
na SkillData, nao 48 aquisicoes funcionais.

### Status conservador da faixa estendida 200-247

A faixa tem 48 IDs-alvo, 12 por classe. No tracker skill-a-skill, quatro IDs
possuem caminho de gameplay explicitamente portado: 201 (handler excepcional,
sem row carregada no runtime), 204, 216 e 226. No C#, a row 229 pode seguir o caminho
generico `InstanceType=11`; com `InstanceValue=9`, solicita zero summons e nao
cria mob quando nao ha outro da mesma face. A carga dessa row na TMSrv 7.69 nao
esta confirmada pelos dados distribuidos. A row 235 pode seguir o caminho
generico Type 48; a aquisicao/crosswalk continua pendente, e a carga dessa row
na TMSrv 7.69 tambem nao esta confirmada. A row 246 tem somente o timer Type 47
portado; 203, 214, 215, 217, 218, 219, 220, 221, 222, 223, 224, 225, 227, 228, 230, 231, 232, 233, 234, 236, 237, 238, 239,
240, 241, 242, 243, 244, 245 e 247 foram auditadas sem producer de efeito proprio.
Todos os 48 IDs agora possuem auditoria individual; 44/48 nao estao fechadas como
skills completas.
Essa contagem mede cobertura explicita por ID, nao ausencia de suporte generico
nem aquisicao funcional. A row 225 pode ser armazenada pelo `SetTick` generico,
mas nenhum efeito Type 46 proprio foi localizado. O fluxo de aprendizado/crosswalk da segunda barra
continua pendente.

### SkillData 200 - Protecao_Divina / dados comparativos sem carga 7.69 confirmada

O artefato comparativo 7.59 e os dois snapshots W2PP concordam na row 200:
`SkillPoint=255`, `TargetType=0`, `ManaSpent=300`, `Delay=300`, `Range=0`,
`InstanceType=0`, `AffectType=6`, `AffectValue=0`, `AffectTime=1` e
`Passive Check=0`, com o nome `Protecao_Divina` (`artifacts/skilldata-reference/skilldata.csv:109`,
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:109` e
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:109`). A row estendida
TMProject 7.69 e toda zero/`Nova skill`
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Editores/SkillData/skilldata.csv:201`),
e os dados TMSrv distribuidos terminam no ID 150. A row antiga indica somente
uma definicao comparativa; nao confirma carga nem aquisicao na release-alvo.

As buscas por ID/nome nao localizaram producer proprio para a row 200 no
TMSrv, W2PP ou C#. `InstanceType=0` e `AffectType=6` podem compartilhar caminhos
genericos quando uma definicao e carregada, mas nao provam que a row 200 seja
alcancavel nem estabelecem crosswalk com o word adicional da barra. Sem
mudanca de runtime/schema ou inferencia de aprendizado.

### SkillData 202 - Blaze_Luncher / passiva sem producer proprio confirmado

O artefato comparativo 7.59 e os snapshots W2PP concordam na row 202:
`SkillPoint=350`, `Passive=1`, `TargetType=1`, `ManaSpent=500`, `Range=1`,
`TickType=12`, `TickValue=0` e `AffectType=0`; o export separado
W2-SkillDataEditor registra
`SkillPoint=320` e `Lancar_Chamas`, sem proveniencia independente
(`artifacts/skilldata-reference/skilldata.csv:113`,
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:113`,
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:113`, export em
`Backup/Tools/Reference759/SERVER/W2-SkillDataEditor-master/W2 - SkillData Editor/bin/Debug/SkillData.csv:109`).
Essas diferencas nao definem o runtime 7.69. A tabela estendida 7.69 deixa a
row 202 toda zerada como `Nova skill` (`Servidor/Server/Editores/SkillData/skilldata.csv:203`),
e a TMSrv distribuida termina no ID 150.

O ataque de TMProject e W2PP retorna para `Passive=1` antes do restante do
processamento de skill (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:114`,
W2PP `Source/Code/TMSrv/_MSG_Attack.cpp:121`); o gate C# tambem rejeita uma
definicao passiva (`LegacySkillAttackGate.cs:76`). O item 5502
`Blaze_Luncher_Book` tem `EF_CLASS=1` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3186`). Nao foi localizado handler
especifico para item 5502, nome ou row 202 em TMProject TMSrv, W2PP TMSrv ou
C#. O ramo generico Sephira aceita Vol31..38 e grava `1 << (Vol-7)` no word
base (`Servidor/Source/Code/TMSrv/_MSG_Use_Item.cpp:6085` em diante; W2PP
`Source/Code/TMSrv/_MSG_UseItem.cpp:1831` em diante); para Vol32 grava bit25,
sem provar que habilite a row 202 ou qualquer campo do word adicional: a grade
consulta o bit local 2 para o ID 202, enquanto `IsValidSkill(202)` consulta o
bit 0 pela formula agrupada de quatro IDs. Nenhum dos dois tem crosswalk
confirmado com o bit 25 do word base.

Decisao: nao inferir efeito, aquisicao ou crosswalk pelo nome do livro; sem
alteracao de runtime/build/testes nesta auditoria documental. Resta a row 200
sem auditoria individual; 44/48 continuam sem fechamento como skills completas.

### SkillData 205 - Mestre_das_Armas / livro Vol32 nao grava o bit consultado pelo cliente

O arquivo comparativo 7.59 marca a row 205 (`Mestre_das_Armas`) como passiva,
`SkillPoint=350` e sem campos de efeito; o export separado W2-SkillDataEditor
usa `SkillPoint=320`, tambem sem efeito e sem proveniencia independente. A row
estendida TMProject 7.69 esta zerada e marcada `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:206`); a TMSrv distribuida
termina no ID 150.

O item 5505 `Livro_de_Skill_Mestre_das_Armas` tem `EF_CLASS=1` e
`EF_VOLATILE=32` (`Servidor/Server/TMSrv/run/ItemList.csv:3189`). Nao foi
localizado caso especial para 5505: o Sephira Vol32 generico calcula
`1 << (Vol - 7)` e grava em `MOB.LearnedSkill` (TMProject
`Servidor/Source/Code/TMSrv/_MSG_Use_Item.cpp:6107-6116`; W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_UseItem.cpp:1842-1851`). Com Vol32, isso
marca bit25 do word base. A consulta cliente para `IsValidSkill(205)` e uma
excecao explicita a formula geral e testa `LearnedSkill[1] & 0x20`
(`Projects/TMProject/Basedef.cpp:1212-1213`); nenhum caminho localizado liga o
bit gravado ao word adicional. Desde 29/10, o handler C# generico para Vol31..38
grava o mesmo bit no word base e aplica o efeito 44; ele tambem nao preenche o
word adicional. Ver “Port local dos livros Sephira Vol31-38” acima.

O bloco W2PP `if (Vol == 205)` e o item Coraçao Doce, nao o livro 5505 nem a
SkillData 205. Nao foi encontrado producer row-specific no C++ ou C#.

Decisao: nao inferir que bit25 do word base conceda a row 205; sem mudanca de
runtime/schema. A proxima atividade continua sendo completar em bateria a
matriz wire/cliente e a variacao independente de Extra antes de portar um
handler de aquisicao.

### SkillData 206 - Fencing_Master / livro Vol32 grava o word base, sem producer do word adicional

O arquivo comparativo 7.59 marca a row 206 (`Fencing_Master`) como passiva,
`SkillPoint=300` e sem campos de efeito; o export separado W2-SkillDataEditor
usa `SkillPoint=320`, tambem sem efeito e sem proveniencia independente. A row
estendida TMProject 7.69 esta zerada e marcada `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:207`); a TMSrv distribuida
termina no ID 150.

O item 5506 `Fencing_Master_Book` tem `EF_CLASS=1` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3190`). Nao foi localizado caso
especial para 5506: o Sephira Vol32 generico calcula `1 << (Vol - 7)` e grava
em `MOB.LearnedSkill` (TMProject
`Servidor/Source/Code/TMSrv/_MSG_Use_Item.cpp:6107-6116`; W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_UseItem.cpp:1842-1851`). Com Vol32, isso
marca bit25 do word base. A grade/`IsValidSkill(206)` consulta bit4 de
`LearnedSkill[1]` (`Projects/TMProject/Basedef.cpp:1218-1223`); nenhum caminho
localizado liga o bit gravado ao word adicional. O C# nao tem handler
equivalente de uso de Sephira Vol32.

O bloco separado `if (Vol == 206)` nas duas fontes e o Selo das Almas, nao o
livro 5506 nem a SkillData 206; nao o tratamos como producer da skill.

Decisao: nao inferir que bit25 do word base conceda a row 206; sem mudanca de
runtime/schema. Proxima row ainda nao auditada: 205.

### SkillData 207 - WindStorm / Sephira Vol32 grava o word base, sem producer do word adicional

Os dados 7.59 da row 207 (`WindStorm`) marcam `Passive=1`, `SkillPoint=300`
e os campos de efeito sem efeito; o export separado W2-SkillDataEditor usa
`SkillPoint=320` e tambem nao tem proveniencia independente. A row estendida
TMProject 7.69 esta zerada e marcada `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:208`); a TMSrv distribuida
termina no ID 150.

O item 5507 `WindStorm_Book` tem `EF_CLASS=1`, `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3191`). Como nao ha caso especial por
5507, ele cai no Sephira Vol32 generico: TMProject calcula `1 << (Vol - 7)` e
grava em `MOB.LearnedSkill` (`Servidor/Source/Code/TMSrv/_MSG_Use_Item.cpp:6096-6134`);
W2PP faz o mesmo (`Server/W2PP/Source/Code/TMSrv/_MSG_UseItem.cpp:1831-1870`).
Com Vol32, o resultado e bit25 do word base. A grade/`IsValidSkill(207)` consulta
bit4 de `LearnedSkill[1]` (`Projects/TMProject/Basedef.cpp:1198-1223`); o livro
nao preenche esse word. O handler C# generico de Vol31..38 tambem grava
somente o word base; ver “Port local dos livros Sephira Vol31-38” acima.

Decisao: nao inferir que bit25 do word base conceda a row 207; sem mudanca de
runtime/schema. Proxima row ainda nao auditada: 206.

### SkillData 208 - Espelho_Magico / livro grava Extra, sem ponte para o word do cliente

O artefato 7.59 da row 208 (`Espelho_Magico`) marca `Passive=1`,
`SkillPoint=300` e campos de target, custo, Instance, Tick e Affect sem efeito.
O export separado W2-SkillDataEditor conserva os campos sem efeito, mas registra
`SkillPoint=255`; sua proveniencia nao foi estabelecida. A row estendida TMProject
7.69 esta zerada e marcada `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:209`), e a TMSrv distribuida
termina no ID 150.

O item 5508 `Espelho_Magico` tem `EF_CLASS=1` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3192`). O handler 7.69
(`Servidor/Source/Code/TMSrv/_MSG_Use_Item.cpp:6020-6078`) exige nivel acima
de 199, classe base 0, oitava skill base (bit 23) e ausencia das duas outras
skills estendidas; grava `Extra.SecLearnedSkill |= 0x100`, chama `SendEtc` e
consome o livro. Nao grava `MOB.Learned[1]`. O sender `SendEtc` continua lendo
`MOB.Learned[0/1]` (`Servidor/Source/Code/TMSrv/SendFunc.cpp:1313-1314`), e a
grade/`IsValidSkill(208)` consulta bit 8 do segundo word
(`Projects/TMProject/Basedef.cpp:1198-1223`). O numero do bit coincide, mas nao
foi localizada ponte que transfira o Extra para esse word. W2PP nao tem o caso
especial 5508 e deixa o livro cair no Sephira Vol32 generico, que grava
`MOB.LearnedSkill |= 1 << 25` (`Server/W2PP/Source/Code/TMSrv/_MSG_UseItem.cpp:1831-1852`);
isso tambem nao atualiza `LearnedSkill[1]`. C# preserva o campo Extra bruto, mas
nao tem handler de uso 5508 nem bridge para o word adicional.

Decisao: registrar a aquisicao historica no Extra sem inferir que ela desbloqueie
a row 208 no cliente; sem mudanca de runtime/schema, pois o crosswalk e a
persistencia canonica do word continuam sem prova. A bateria de wire/cliente
deve variar Extra bit `0x100` independentemente de `LearnedSkill[1]` antes de
portar o livro.

### SkillData 209 - Freezing_Connection / Type 45 passiva sem producer proprio confirmado

O artefato 7.59 da row 209 (`Conexao_de_Gelo`) marca `Passive=1`,
`SkillPoint=350`, `AffectType=45`, `AffectValue=0` e `AffectTime=2`; o export
separado W2-SkillDataEditor tambem lista Type 45/tempo 2, mas usa
`SkillPoint=320` e nao tem proveniencia independente. A row estendida TMProject
7.69 esta toda zerada como `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:210`); a TMSrv distribuida
termina no ID 150.

O item 5509 `Livro_de_Skill_Conexao_de_Gelo` tem `EF_CLASS=1` e
`EF_VOLATILE=32` (`Servidor/Server/TMSrv/run/ItemList.csv:3193`). Buscas por
nome, ID 209 e item 5509 nao localizaram producer row-specific em TMProject
TMSrv, W2PP ou C#. `IsValidSkill(209)` consulta bit 8 de `LearnedSkill[1]`
(`Projects/TMProject/Basedef.cpp:1198-1223`); nao ha crosswalk comprovado com
`SecLearnedSkill` ou formula de efeito proprio ligada a row 209.

Decisao: nao inferir efeito Type 45 ou aquisicao pelo livro; sem alteracao de
runtime/build/testes nesta auditoria documental. Proxima row ainda nao auditada:
208.

### SkillData 210 - Freezing_Counter / sem efeito ou producer proprio confirmado

As rows 210 dos artefatos 7.59 e do export separado W2-SkillDataEditor
concordam em `SkillPoint=320` e campos de target, custo, Instance, Tick e Affect
sem efeito; o 7.59 usa o nome `Freezing_Counter`, enquanto o export separado
usa `Congelando_Counter` sem proveniencia independente. A row estendida TMProject
7.69 e toda zero e marcada `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:211`), e a TMSrv distribuida
termina no ID 150.

O item 5510 `Freezing_Counter_Book` tem `EF_CLASS=1` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3194`), mas buscas por nome, ID 210 e
item 5510 nao localizaram producer row-specific em TMProject TMSrv, W2PP ou C#.
`IsValidSkill(210)` consulta bit 8 de `LearnedSkill[1]` pela formula comum do
cliente (`Projects/TMProject/Basedef.cpp:1198-1223`); nao ha crosswalk provado
com `SecLearnedSkill`.

Decisao: sem inferir efeito, aquisicao ou crosswalk pelo nome/livro; sem
alteracao de runtime/build/testes nesta auditoria documental. Proxima row ainda
nao auditada: 209.

### SkillData 211 - Seal_Master / sem efeito ou producer proprio confirmado

Os artefatos 7.59 e o export separado W2-SkillDataEditor listam a row 211 como
`Seal_Master`/`Mestre_do_Selo`, `SkillPoint=320` e sem campos de target, custo,
Instance, Tick ou Affect com efeito. O export nao tem proveniencia independente.
A row estendida TMProject 7.69 e toda zero e marcada `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:212`); a TMSrv distribuida
termina no ID 150.

O item 5511 `Seal_Master_Book` tem `EF_CLASS=1` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3195`). Buscas por nome, ID 211 e item
5511 nao localizaram producer row-specific em TMProject TMSrv, W2PP ou C#. A
formula de `IsValidSkill(211)` consulta bit 8 de `LearnedSkill[1]`
(`Projects/TMProject/Basedef.cpp:1198-1223`), sem ponte comprovada com
`SecLearnedSkill`.

Decisao: sem inferir efeito, aquisicao ou crosswalk pelo nome/livro; sem
alteracao de runtime/build/testes nesta auditoria documental. Proxima row ainda
nao auditada: 210.

### SkillData 212 - Cenote / sem efeito ou producer proprio confirmado

A row 212 do artefato 7.59 lista `Cenote`, `SkillPoint=355`,
`ManaSpent=120` e campos de Instance, Tick e Affect zerados; o export separado
do W2-SkillDataEditor usa `SkillPoint=255` e tambem nao fornece efeito
row-specific. Esses dados comparativos divergem e nao tem proveniencia
independente para definir o runtime 7.69. A tabela estendida TMProject 7.69
deixa a row zerada como `Nova skill`
(`Servidor/Server/Editores/SkillData/skilldata.csv:213`), e a TMSrv distribuida
termina no ID 150.

O item 5512 tambem se chama `Cenote`, com `EF_CLASS=2` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3196`). Buscas por nome, ID 212 e item
5512 nao localizaram producer row-specific em TMProject TMSrv, W2PP ou C#; o
nome correspondente nao prova que o item habilite esta row. `IsValidSkill(212)`
consulta bit 12 de `LearnedSkill[1]` pela formula comum do cliente
(`Projects/TMProject/Basedef.cpp:1198-1223`), sem ponte comprovada com
`SecLearnedSkill`.

Decisao: sem inferir efeito, crosswalk ou aquisicao pelo item; sem alteracao de
runtime/build/testes nesta auditoria documental. Proxima row ainda nao auditada:
211.

### SkillData 213 - Protecao_Absoluta / definicao divergente sem producer proprio confirmado

A linha 213 da tabela estendida TMProject 7.69 esta toda zerada e marcada
`Nova skill` (`Servidor/Server/Editores/SkillData/skilldata.csv:214`); a TMSrv
distribuida termina no ID 150, portanto esses dados nao provam que a row seja
carregada no runtime. A referencia 7.59 e o export separado do W2-SkillDataEditor
nomeiam `Protecao_Absoluta` como ativa e indicam `AffectType=6` e
`AffectTime=2`; divergem em `SkillPoint` (360 contra 320). O export nao tem
proveniencia independente. Nao transportar esses campos para o alvo 7.69.

O item 5513 `Livro_de_Skill_Protecao_Absoluta` tem `EF_CLASS=2` e
`EF_VOLATILE=32` (`Servidor/Server/TMSrv/run/ItemList.csv:3197`). A associacao
nominal nao comprova aquisicao: as buscas por item 5513, ID 213 e nome nao
localizaram producer row-specific em TMProject TMSrv, W2PP ou C#. O handler
explicito para os livros 5500/5504/5508 em TMProject
(`Servidor/Source/Code/TMSrv/_MSG_Use_Item.cpp:5869,5945,6020`) nao inclui 5513.
O cliente consulta bit 12 de `LearnedSkill[1]` para `IsValidSkill(213)` pela
formula comum (`Projects/TMProject/Basedef.cpp:1198-1223`), mas nao ha ponte
provada entre esse word e o `SecLearnedSkill` do item.

Decisao: sem inferir efeito, crosswalk ou aquisicao pelo nome do livro; sem
alteracao de runtime/build/testes nesta auditoria documental. Proxima row ainda
nao auditada: 212.

### SkillData 214 - Life_Wave / passiva sem producer proprio confirmado

O artefato de referencia (`artifacts/skilldata-reference/skilldata.csv:137`) e
os snapshots W2PP (`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:137` e
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:137`) concordam em
`SkillPoint=320`, `Passive=1` e campos de target, custo, delay, alcance,
Instance, Tick, Affect e flags de efeito zerados. O artefato de referencia
deriva da arvore 7.59 (`docs/migration/dados-skills.md:12`), nao e autoridade
para o runtime desta release. Um export separado do W2-SkillDataEditor
arquivado (`Backup/Tools/Reference759/SERVER/W2-SkillDataEditor-master/W2 -
SkillData Editor/bin/Debug/SkillData.csv:121`) tambem registra `SkillPoint=320`
e o nome traduzido `Onda_de_Vida`, sem estabelecer proveniencia independente.

A TMSrv 7.69 distribuida termina no ID 150 e o editor deixa a row 214 zerada
como `Nova skill` (`Servidor/Server/Editores/SkillData/skilldata.csv:215`), sem
provar que o runtime carregue essa definicao. Os gates comuns rejeitam cast se
a row passiva estiver carregada (TMProject `_MSG_Attack.cpp:114`, W2PP
`:121`, C# `LegacySkillAttackGate.cs:76`). Buscas por `Life_Wave` e ID 214
nao localizaram producer proprio na TMSrv 7.69, W2PP ou C#.

O item 5514 `Life_Wave_Book` tem `EF_CLASS=2` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3198`; snapshots W2PP concordam). A
associacao nominal nao prova crosswalk: o ramo Sephira Vol32 grava bit25 em
`LearnedSkill[0]`, enquanto `IsValidSkill(214)` consulta bit12 de
`LearnedSkill[1]` (`Projects/TMProject/Basedef.cpp:1198-1223`). Nao ha ponte
comprovada entre os words.

Decisao: nao inferir efeito, aquisicao ou crosswalk pela row/item; sem alteracao
de runtime, build ou testes nesta auditoria documental. A row 216 ja tem port
Type 42 registrado; proxima row ainda nao auditada: 213.

### SkillData 215 - Holy_Power / passiva sem producer proprio confirmado

O artefato de referencia (`artifacts/skilldata-reference/skilldata.csv:139`) e
os snapshots W2PP (`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:139` e
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:139`) concordam em
`SkillPoint=300`, `Passive=1` e campos de target, custo, delay, alcance,
Instance, Tick, Affect e flags de efeito zerados. O artefato de referencia
deriva da arvore 7.59 (`docs/migration/dados-skills.md:12`), nao e autoridade
para o runtime desta release. Um export separado do W2-SkillDataEditor
arquivado (`Backup/Tools/Reference759/SERVER/W2-SkillDataEditor-master/W2 -
SkillData Editor/bin/Debug/SkillData.csv:122`) mostra `SkillPoint=320` e o nome
`Poder_Sagrado`; sua proveniencia nao foi estabelecida, portanto fica como
divergencia, nao contrato.

A TMSrv 7.69 distribuida termina no ID 150 e o editor deixa a row 215 zerada
como `Nova skill` (`Servidor/Server/Editores/SkillData/skilldata.csv:216`), sem
provar que o runtime carregue essa definicao. Os gates comuns rejeitam cast se
a row passiva estiver carregada (TMProject `_MSG_Attack.cpp:114`, W2PP
`:121`, C# `LegacySkillAttackGate.cs:76`). Buscas por `Holy_Power` e ID 215
nao localizaram producer proprio na TMSrv 7.69, W2PP ou C#.

O item 5515 `Holy_Power_Book` tem `EF_CLASS=2` e `EF_VOLATILE=32`
(`Servidor/Server/TMSrv/run/ItemList.csv:3199`; snapshots W2PP concordam). A
associacao nominal nao prova crosswalk: o ramo Sephira Vol32 grava bit25 em
`LearnedSkill[0]`, enquanto `IsValidSkill(215)` consulta bit12 de
`LearnedSkill[1]` (`Projects/TMProject/Basedef.cpp:1198-1223`). Nao ha ponte
comprovada entre os words.

Decisao: nao inferir efeito, aquisicao ou crosswalk pela row/item; sem alteracao
de runtime, build ou testes nesta auditoria documental. A row 216 ja tem port
Type 42 registrado; proxima row ainda nao auditada: 214.

### SkillData 217 - Congelamento_Proficiente / passiva sem producer proprio confirmado

O arquivo `artifacts/skilldata-reference/skilldata.csv:143` e os snapshots
W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:143` e
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:143` concordam em
`SkillPoint=340`, `InstanceAttribute=2`, `AffectResist=2` e `Passive=1`.
`TargetType`, custo, delay, alcance, `InstanceType`, `TickType`, `AffectType`
e seus valores/tempos estao zerados. O artefato de referencia deriva da arvore
7.59 (`docs/migration/dados-skills.md:12`), portanto e comparativo, nao
autoridade do runtime desta release. Um export separado do
`W2-SkillDataEditor` arquivado em
`Backup/Tools/Reference759/SERVER/W2-SkillDataEditor-master/W2 - SkillData Editor/bin/Debug/SkillData.csv:124`
mostra `SkillPoint=320`; a proveniencia nao foi estabelecida e o valor nao
define o contrato.

A tabela TMSrv 7.69 distribuida termina no ID 150 e o editor deixa a row 217
zerada como `Nova skill` (`Servidor/Server/Editores/SkillData/skilldata.csv:218`);
nao ha evidencia de que o runtime 7.69 carregue a definicao estendida. Caso
carregada, os gates genericos rejeitam cast passivo em TMProject/W2PP
(`_MSG_Attack.cpp:114`/`:121`) e C# (`LegacySkillAttackGate.cs:76`). Buscas
por `Congelamento_Proficiente`, ID 217 e `InstanceAttribute` nao localizaram
producer ou consumidor de gameplay row-specific. `AffectResist=2` nao muda
essa conclusao: o gate de passiva ocorre antes do cast, e nao ha `AffectType`
ou `TickType` para aplicar.

O item 5517 `Livro_de_Skill_Congelamento_Proficiente` tem `EF_CLASS=2` e
`EF_VOLATILE=32` (TMProject `TMSrv/run/ItemList.csv:3201`; W2PP snapshot
`TMSrv/run/ItemList.csv:3666`), o que sugere associacao nominal. O ramo Sephira
grava bit25 em `LearnedSkill[0]`, enquanto `IsValidSkill(217)` consulta bit16
de `LearnedSkill[1]` (`Projects/TMProject/Basedef.cpp:1223`). A rota observada
nao conecta esses words.

Decisao: nao inferir bonus de congelamento, efeito ou crosswalk pelo nome e
nao alterar runtime/build/testes nesta auditoria documental. SkillData 216 ja
tem port Type 42 registrado; proxima row ainda nao auditada: 215.

### SkillData 218 - Thunder_Force / passiva sem efeito proprio confirmado

O arquivo `artifacts/skilldata-reference/skilldata.csv:145` e os snapshots
W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:145` e
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:145` apresentam a row 218
como `Thunder_Force`, `SkillPoint=350`, passiva, com target, custo, delay,
alcance, Instance, Tick e Affect zerados. O artefato de referencia foi extraido
da arvore 7.59, conforme `docs/migration/dados-skills.md:12`; e evidencia
comparativa, nao a autoridade de runtime desta release.
Um export separado do `W2-SkillDataEditor` arquivado em
`Backup/Tools/Reference759/SERVER/W2-SkillDataEditor-master/W2 - SkillData Editor/bin/Debug/SkillData.csv:125` mostra
`SkillPoint=320`; a proveniencia desse export nao foi estabelecida, entao o
valor fica registrado como divergencia e nao define o contrato.

Na TMProject 7.69, a tabela distribuida de TMSrv termina no ID 150 e a linha
218 do editor (`Servidor/Server/Editores/SkillData/skilldata.csv:219`) esta
zerada como `Nova skill`. Portanto, a definicao estendida do artefato 7.59 e
dos snapshots W2PP nao comprova que a TMSrv 7.69 carregue essa skill. Caso a
row passiva seja carregada, o gate generico de `_MSG_Attack.cpp` rejeita o cast
em TMProject/W2PP (`Passive == 1`); o gate C# tambem rejeita definicoes passivas
(`LegacySkillAttackGate.cs:76`). Buscas por `Thunder_Force`, pelo ID 218 e por
branch/produtor explicito nao localizaram comportamento proprio em TMSrv,
W2PP ou C#.

O item 5518 `Thunder_Force_Book` tem `EF_CLASS=2` e `EF_VOLATILE=32`
(TMProject `TMSrv/run/ItemList.csv:3202`; W2PP snapshot
`TMSrv/run/ItemList.csv:3667`), o que sugere intencao de associacao. O handler
Sephira grava `1 << 25` em `LearnedSkill[0]`, enquanto
`IsValidSkill(218)` consulta o bit 16 de `LearnedSkill[1]`
(`Projects/TMProject/Basedef.cpp:1223`). A rota observada nao conecta esses
words. Tambem nao confundir a linha TMSrv `15,218,...,Armadura_Critica`: nela
15 e o ID da skill base e 218 e o campo `SkillPoint`; o C# `ArmorCriticalSkillId`
e 15. Isso nao identifica o ID estendido 218, `Thunder_Force`.

Decisao: nao inferir efeito, aquisicao ou crosswalk pelo nome do livro; sem
mudanca de runtime/build/testes nesta auditoria documental. Proxima row para
auditoria: 217.

### SkillData 219 - Thunder_Lising / Type 1 passiva sem producer proprio confirmado

As rows 219 em `artifacts/skilldata-reference/skilldata.csv:147` e nos
snapshots W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:147` e
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:147` concordam:
`SkillPoint=350`, `Passive=1`, `AffectType=1`, `AffectValue=255`,
`AffectTime=8`, com target, custo, alcance, Instance e Tick zerados. O loader
divide AffectTime por quatro, resultando em dois. A TMSrv 7.69 distribuida
termina no ID 150, e o editor deixa a row 219 zerada como `Nova skill`; a
definicao W2PP estendida nao comprova carga pela TMSrv 7.69.

Os dois servidores rejeitam o cast de uma row passiva (`_MSG_Attack.cpp:114`
TMProject; `:121` W2PP), e o C# tambem o rejeita (`LegacySkillAttackGate.cs:76`).
Type 1 tem projecao generica de score em TMProject/W2PP e C#, mas nenhuma
conexao de producer especifico com a row 219 foi localizada. Buscas por
Thunder_Lising e ID 219 nao encontraram branch ou produtor row-specific.

O item 5519 `Thunder_Lising_Book` tem nome correspondente, `EF_CLASS=2` e
`EF_VOLATILE=32` (TMProject `TMSrv/run/ItemList.csv:3203`; W2PP
`TMSrv/run/ItemList.csv:3668`). Isso sugere intencao de associacao. O handler
Sephira generico grava `1 << 25` no word base para Vol32, enquanto o cliente
calcula para `IsValidSkill(219)` o bit 16 de `LearnedSkill[1]`
(`Projects/TMProject/Basedef.cpp:1223`). O caminho do item nao prova aquisicao
funcional nem liga os words. O `SkillPoint=350` tambem nao comprova bonus de
segunda barra: o calculo de pontos percorre os 24 bits base por classe e
`_MSG_ApplyBonus` atende somente `Detail=5000-5095`.

Decisao: nao inferir o efeito de Thunder_Lising pelo nome, nem adicionar
producer, crosswalk ou persistencia. Sem mudanca de runtime/build/testes nesta
auditoria documental. Proxima row para auditoria: 215.

### SkillData 220 - Remover_Memoria / Type 44 sem producer proprio confirmado

As rows 220 em `artifacts/skilldata-reference/skilldata.csv:149` e nos
snapshots W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:149` e
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:149` concordam:
`SkillPoint=310`, `Passive=1`, `AffectType=44`, `AffectValue=0`,
`AffectTime=2`, e os campos de target, custo, delay, alcance, Instance e Tick
zerados. O loader divide AffectTime por quatro, resultando em zero. A TMSrv
7.69 distribuida termina no ID 150, e o editor deixa a row 220 zerada como
`Nova skill`; a definicao no snapshot W2PP nao prova carga no runtime 7.69.

Se a row estiver carregada, TMProject 7.69 e W2PP rejeitam seu cast pelo gate
generico `Passive == 1` (`_MSG_Attack.cpp:114` TMProject; `:121` W2PP), assim
como o C# (`LegacySkillAttackGate.cs:76`). Buscas por nome/ID nao localizaram
producer proprio; o Type 44 da row nao tem projetor de score identificado no
C#. O `case 44` em `LegacySkillCombatMath`/`Basedef.cpp` e o calculo da skill
ativa de ID 44, nao uma regra para `AffectType=44` desta row.

O item 5520 `Remover_Memoria` coincide no nome com a row e tem
`EF_CLASS=2`, `EF_VOLATILE=32` (`ItemList.csv:3204`; W2PP
`TMSrv/run/ItemList.csv:3669`), o que sugere intencao de associacao. Porem o
handler Sephira grava `1 << 25` no word base (`_MSG_Use_Item.cpp:6109-6116`;
W2PP `_MSG_UseItem.cpp:1844-1851`) e chama `SendEtc`; nao grava o word
adicional. O cliente calcula para `IsValidSkill(220)` o bit 20 de
`LearnedSkill[1]` (`Projects/TMProject/Basedef.cpp:1223`), e `SendEtc` transmite
esse word a partir do estado separado (`TMSrv/SendFunc.cpp:1314`). Portanto,
nome/classe sugerem associacao, mas o caminho observado nao prova aquisicao
funcional da row 220. O `SkillPoint=310` tambem nao prova bonus na segunda
barra: o calculo percorre 24 bits base por classe e `_MSG_ApplyBonus` atende
somente `Detail=5000-5095`.

Decisao: nao inferir efeito de memoria pelo nome, nem criar producer,
crosswalk ou persistencia para a row. Sem mudanca de runtime/build/testes nesta
auditoria documental. Proxima row para auditoria: 215.

### SkillData 221 - Incapacitador / Type 5 com valor zero

As rows 221 em `artifacts/skilldata-reference/skilldata.csv:151` e nos snapshots
W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:151` e
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:151` concordam: ativa,
`SkillPoint=330`, `TargetType=1`, `ManaSpent=500`, `Delay=150`, `Range=4`,
`AffectType=5`, `AffectValue=0`, `AffectTime=50`, agressiva e com `Passive=0`.
O loader divide o tempo por quatro, resultando em 12. A tabela TMSrv 7.69
distribuida termina no ID 150 e o editor marca 221 zerada como `Nova skill`;
o snapshot W2PP Common contem a row, mas isso nao prova que a TMSrv distribuida
da referencia a carregue.

O caminho de ataque generico em TMProject/W2PP usa `SetAffect` para rows com
affect e nao tem branch proprio pelo ID 221 (`_MSG_Attack.cpp:1727` TMProject;
`_MSG_Attack.cpp:1228` W2PP). O Type 5 generico reduz DEX por
`(100-Value)/100`; com o `AffectValue=0` desta row, a formula e identidade
(`Basedef.cpp:3463` TMProject; `W2PP/Source/Code/Basedef.cpp:3976-3979`). O C# ja
tem essa formula no score projector (`LegacyCurrentScoreMath.cs:741-759,1786`),
mas a projecao imediata de ataque nao reconhece esta row e as allowlists dos
timers de NPC nao incluem Type 5 (`WorldHub.cs:1990,2224,7070`). Para os dados
atuais, o valor zero nao altera score; a diferenca de projecao/lifetime so se
torna observavel se a row estendida passar a ser carregada e usada. Os livros
5521-5524 compartilham Vol32/bit25 e nao estabelecem qual row se aprende.

Decisao: nao adicionar producer, crosswalk ou timer de row221 enquanto a
definicao nao estiver na tabela distribuida e a aquisicao da segunda barra nao
estiver provada. Registrar Type5 em NPC como pendencia condicional para quando
essa carga for ativada. Sem mudanca de runtime/build/testes nesta auditoria
documental. Proxima row para auditoria: 215.

### SkillData 222 - Special_Master passiva sem efeito proprio confirmado

As rows 222 em `artifacts/skilldata-reference/skilldata.csv:153` e nos snapshots
W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:153` e
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:153` concordam: `SkillPoint=300`,
`Passive=1`, e `TargetType`, `ManaSpent`, `Delay`, `Range`, Instance, Tick e
Affect zerados. A TMSrv 7.69 distribuida termina no ID 150; o editor
comunitario deixa a row 222 zerada como `Nova skill`. A definicao estendida nao
prova que a TMSrv distribuida a carregue.

Se carregada, TMProject 7.69 e W2PP rejeitam cast pela verificacao generica
`Passive == 1` (`_MSG_Attack.cpp:114` e `:121`); o C# faz o mesmo em
`LegacySkillAttackGate`. Buscas por `Special_Master`, ID 222 e branches
especificos nao localizaram producer proprio em TMSrv, W2PP ou C#.

O `SkillPoint=300` nao comprova aquisicao ou bonus na segunda barra: nas duas
fontes, `BASE_GetBonusSkillPoint` soma somente os 24 bits da arvore base da
classe (`Basedef.cpp:992-998` TMProject; `:878-884` W2PP; `MAX_SKILL=24`), e
`_MSG_ApplyBonus` aceita somente `Detail=5000-5095`. Sem inferir efeito passivo,
crosswalk ou aquisicao para o nome da row. Sem mudanca de runtime/build/testes
nesta auditoria documental. Proxima row para auditoria: 215.

### SkillData 223 - Another_Change sem producer proprio confirmado

As rows 223 em `artifacts/skilldata-reference/skilldata.csv:155` e nos snapshots
W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:155` e
`artifacts/w2pp-tmsrv-sandbox/Common/skilldata.csv:155` concordam: ativa,
`SkillPoint=320`, `TargetType=1`, `ManaSpent=800`, `Delay=300`, `Range=5`, sem
Instance, Tick ou Affect, `Aggressive=1`, `MaxTarget=1` e `Passive=0`. A TMSrv
7.69 distribuida termina no ID 150; o editor comunitario deixa a row 223 zerada
como `Nova skill`. Os CSVs estendidos, portanto, nao provam que a TMSrv
distribuida carregue essa definicao.

Quando carregada, os gates gerais de ataque aceitam a row ativa, mas nao foi
localizado producer ou branch proprio pelo ID/nome em TMProject, W2PP ou C#.
TMProject 7.69 nao exige learned bit para IDs 101 em diante; o
`LegacySkillAttackGate` segue esse comportamento. W2PP, por sua vez, passa o ID
223 pelo caminho geral de skills validas e verifica o bit `223 % 24 = 7` do word
base. Essa divergencia nao comprova aquisicao nem crosswalk com a segunda barra;
como o alvo e 7.69, o C# mantem a regra 7.69 sem esse gate herdado da W2PP.

No dano generico, `InstanceType=0` sem caso por ID resulta em zero nas duas
fontes: TMProject usa o `default` que retorna `Result` (TMSrv
`Basedef.cpp:6642`); W2PP deixa `dam` inicializado em zero no ramo
`instanceindex==0` (`Basedef.cpp:6998-7060`). `LegacySkillCombatMath` tambem
retorna zero no default para `InstanceType=0`. Sem efeito row-specific confirmado
ou divergencia de dano para esta definicao, nao ha mudanca de runtime nesta
auditoria. Sem build/testes por ser somente documental. Proxima row para
auditoria: 219.

### SkillData 224 - Anti_Magia / Type 43 sem semantica propria confirmada

As rows 224 em `artifacts/skilldata-reference/skilldata.csv:157` e no snapshot
W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:157` sao
identicas: ativa, `SkillPoint=260`, `TargetType=0`, `ManaSpent=255`,
`Delay=150`, `Range=0`, sem Instance ou Tick, `AffectType=43`,
`AffectValue=0`, `AffectTime=1`, `Aggressive=0` e `Passive=0`. Os loaders
dividem `AffectTime` por quatro, resultando em zero para essa row.

TMProject 7.69 e W2PP passam skills pelo caminho Buffs Time comum e chamam
`SetAffect`/`SetTick`; nao foi localizado branch pelo ID 224 nem regra de
score/dano especifica para Type 43. `SetAffect` grava o Type/Value/Time da row
quando o tipo e positivo. No C#, `TryApplyLegacyAffects` tambem aceita o tipo
positivo e o timer de jogador decrementa e limpa affects nao-zero; nao existe
projecao de score ou producer especifico da row 224. A TMSrv 7.69 distribuida
termina no ID 150; o editor comunitario tem a row 224 zerada como `Nova skill`.
Assim, os CSVs estendidos provam apenas a definicao de referencia, nao que a
TMSrv a carregue nem que exista aquisicao/crosswalk para a segunda barra. Nao
inferir efeito anti-magia pelo nome.

Decisao: manter o caminho generico existente sem atribuir semantica propria ao
Type 43. Sem mudanca de runtime ou testes/build nesta auditoria documental.
Proxima row para auditoria: 215.

### SkillData 225 - Chama_Resistente / Type 46 sem efeito proprio confirmado

As rows 225 em `artifacts/skilldata-reference/skilldata.csv:159` e no snapshot
W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:159` concordam: ativa,
`SkillPoint=300`, `TargetType=0`, `ManaSpent=600`, `Delay=10`, `Range=0`,
`InstanceType=0`, `TickType=46`, `TickValue=100`, `AffectType=0`,
`AffectTime=12`, `Aggressive=0`, `AffectResist=1` e `Passive=0`. Os loaders
TMProject (`TMSrv/Basedef.cpp:6326`), W2PP (`Basedef.cpp:6708`) e C#
(`LegacySkillData.cs:86-88`) dividem `AffectTime` por quatro; a row seria lida
com 3 se essa tabela estendida estivesse carregada.

TMProject `_MSG_Attack.cpp:1667-1737` e W2PP `_MSG_Attack.cpp:1172-1238`
usam o caminho Buffs Time comum, sem branch pelo ID 225; o `SetTick` generico
grava Type/Value/Level/Time e chama `GetCurrentScore` (TMProject
`Server.cpp:10419-10449`; W2PP `Server.cpp:9256-9283`). O `ProcessAffect`
decrementa e limpa affects finitos genericamente (TMProject `Server.cpp:6900-6920`;
W2PP `Server.cpp:5832-5851`). Buscas por Type 46 e pelo nome/ID nao acharam
formula de CurrentScore/dano nem producer row-specific em TMSrv, W2PP ou C#;
os `GetCurrentScore` das duas fontes nao tem ramo `Type == 46`. A unica
ocorrencia `case 46` em `_MSG_SingleControl.cpp` encaminha o grau do NPC para
`NTask_EventTrade`, sem relacao com affect. Nao se infere resistencia a fogo
apenas pelo nome.

O item 5525 `Livro_de_Skill_Chama_Resistente` tem `EF_VOLATILE=32` (TMProject
`ItemList.csv:3209`; snapshot W2PP `ItemList.csv:3669`). Assim como os livros
5526-5528, o handler Sephira generico grava `1 << 25` em `MOB.LearnedSkill`
(`_MSG_Use_Item.cpp:6107-6116`; W2PP `_MSG_UseItem.cpp:1842-1851`). O bit nao
distingue as quatro rows; `MAX_SKILL=24` e `BASE_GetBonusSkillPoint` percorre
somente bits 0-23, enquanto `_MSG_ApplyBonus` cobre `Detail=5000-5095`. As
tabelas TMSrv 7.69 distribuidas terminam no ID 150;
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Editores/SkillData/skilldata.csv:226`
tem a row 225 zerada como `Nova skill`. O crosswalk e a carga real da row na
TMSrv permanecem sem confirmacao.

No C#, `TryApplyLegacyAffects` armazena qualquer TickType positivo e o timer de
jogador decrementa/limpa todo affect nao-zero; o projetor de score nao tem
formula Type 46. Como nenhuma semantica de combate especifica foi comprovada,
esta foi uma auditoria documental: sem mudanca de runtime e sem testes/build.
Proxima row para auditoria: 215.

### SkillData 226 - Resi_Decrease / Type 3 por TickType

As rows 226 em `artifacts/skilldata-reference/skilldata.csv:161` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:161` sao identicas e ativas:
`TargetType=1`, `ManaSpent=300`, `Delay=300`, `Range=6`, `TickType=3`,
`TickValue=50`, `AffectType=0`, `AffectTime=4`, `Aggressive=1` e
`AffectResist=1`. Os loaders TMProject (`TMSrv/Basedef.cpp:6326`), W2PP
(`Basedef.cpp:6708`) e C# (`LegacySkillData.cs:86-88`) dividem `AffectTime`
por quatro; em runtime, a row usa 1.

TMProject `_MSG_Attack.cpp:1667-1736` e W2PP `_MSG_Attack.cpp:1172-1237`
processam a row no caminho Buffs Time comum: alvo hostil, mesmo lider/guilda,
`AffectResist`, `RSV_BLOCK` e Clan 6 determinam se `SetTick` pode ser aplicado;
quando um efeito muda, `GetCurrentScore` projeta o novo score. Os `SetTick`
TMProject (`Server.cpp:10419-10449`) e W2PP (`Server.cpp:9256-9283`) gravam
Type/Value/Level/Time e limitam Types 1/3/10 a dois ticks quando o tempo
calculado seria 3 ou mais. `CMob::GetCurrentScore` reduz as quatro resistencias
de Type 3 por `Value/2` se o item de cabeca tem indice abaixo de 50, ou por
`Value-10` nos demais casos (TMProject `Basedef.cpp:3503-3515`; W2PP
`Basedef.cpp:3955-3965`). Com Value 50, os deltas sao 25 ou 40.

O C# ja armazenava `TickType` no caminho generico, mas a row nao entrava no
matcher de score do jogador e o caminho generico NPC nao aplicava a transicao
de resistencias. O matcher estrito agora exige ID 226, row ativa, alvo 1,
InstanceType 0, agressiva, TickType 3/Value 50, sem AffectType, AffectTime 1
apos o loader e AffectResist 1. Para jogador, `WorldHub.cs:10105-10140` aciona
`ProjectPlayerScoreLocked`, preserva HP/MP atuais e deixa o timer existente
restaurar a base na expiracao; a formula Type 3 esta em
`LegacyCurrentScoreMath.cs:2075-2082`. Para NPC, `WorldHub.cs:10904-10945`
replica os gates agressivos, respeita o limite de merchant de `SetTick`, aplica
o delta imediatamente e usa `ApplyNpcPursuitAffectResistanceTransition` para
restaurar a base no expiry. O recast nao recebe o reset de quatro ticks do
`SetAffect` da skill 16: permanece o tempo de `SetTick` (2 para a row 226).

O catalogo lista 5526 `Resi_Decrease_Book` com `EF_VOLATILE=32` (TMProject
`ItemList.csv:3206`; snapshot W2PP `ItemList.csv:3670`). O handler Sephira
generico grava `1 << 25`, o mesmo valor dos itens 5525, 5527 e 5528. Isso nao prova
que o bit esteja ligado a esta row; a SkillData distribuida na TMSrv 7.69
termina no ID 150, e o crosswalk/aquisicao da segunda barra segue pendente.

Engine.Tests: RED reproduziu as duas falhas de score; GREEN `537/537`. Build
integral sem incremental `WydCdk.Engine.slnx --no-restore --no-incremental -m:1`:
0 avisos/erros. Smoke TCP local passou `MOB_AI_SMOKE_PASS` e marcadores
existentes de Type 3, sem marcador dedicado à row 226. SDK 10.0.401;
`dotnet --info` saiu 1 por acesso negado ao Service Control Manager. Sem Host
autenticado ou E2E visual especifico; sem package/publish/deploy, Site/producao,
commit ou push. As rows 225, 224, 223, 222, 221 e 220 foram auditadas;
proxima row para auditoria: 215.

### SkillData 227 - Manaburn_Master / passiva sem producer de efeito proprio

As rows 227 em `artifacts/skilldata-reference/skilldata.csv:163` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:163` sao identicas:
`SkillPoint=350`, `TargetType=0`, `ManaSpent=0`, `Delay=0`, `InstanceType=0`,
`TickType=0`, `AffectType=0`, `AffectValue=0`, `AffectTime=0` e `Passive=1`.
Buscas por `Manaburn_Master` e pelo ID 227 nao localizaram producer de efeito
proprio na TMSrv 7.69, W2PP ou C#.

TMProject `_MSG_Attack.cpp:114`, W2PP `_MSG_Attack.cpp:121` e C#
`LegacySkillAttackGate.cs:76-77` rejeitam a definicao passiva caso a row seja
carregada. O catalogo lista 5526 `Resi_Decrease_Book`, 5527
`Manaburn_Master_Book` e 5528 `Unidade_Mental`, todos com `EF_VOLATILE=32`
(TMProject `ItemList.csv:3206-3208`; snapshot W2PP
`ItemList.csv:3670-3672`). Os handlers genericos de livros Sephira
(TMProject `_MSG_Use_Item.cpp:6096-6116`; W2PP `_MSG_UseItem.cpp:1831-1851`)
gravam `1 << (Vol-7)`, portanto os tres itens gravam o bit 25 no word base
`MOB.LearnedSkill`. O nome do livro 5527 e sugestivo, mas o setter compartilha
o mesmo bit com 5526 e 5528 e nao foi localizado efeito row-specific para ele.
`MAX_SKILL=24` (TMProject `Basedef.h:125`; W2PP `Basedef.h:171`) e
`BASE_GetBonusSkillPoint` percorre somente os bits 0-23 (TMProject
`Basedef.cpp:989-998`; W2PP `Basedef.cpp:878-887`);
`_MSG_ApplyBonus.cpp` aceita `Detail=5000-5095` e calcula essas posicoes. As
tabelas server-side distribuidas com TMProject 7.69 terminam no ID 150. Logo,
a rota de item confirma apenas a gravacao desse bit; nao prova crosswalk ou
efeito para a row 227.

Decisao: nao inferir formula ou crosswalk pelo nome `Manaburn_Master`. Sem
alteracao de runtime; nenhum teste foi executado, pois nao mudou codigo.

### SkillData 228 - Unidade Mental / passiva sem producer de efeito proprio

As rows 228 em `artifacts/skilldata-reference/skilldata.csv:165` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:165` sao identicas:
`SkillPoint=255`, `TargetType=0`, `ManaSpent=0`, `Delay=0`, `InstanceType=0`,
`TickType=0`, `AffectType=0`, `AffectValue=0`, `AffectTime=0` e `Passive=1`.
Buscas por `Unidade_Mental` e pelo ID 228 nao localizaram producer de efeito
proprio na TMSrv 7.69, W2PP ou C#.

TMProject `_MSG_Attack.cpp:114`, W2PP `_MSG_Attack.cpp:121` e C#
`LegacySkillAttackGate.cs:76-77` rejeitam a definicao passiva caso a row seja
carregada. O ramo de `CMob::GetCurrentScore` para classe BM nas duas fontes C++
(TMProject `CMob.cpp:766-770`; W2PP `CMob.cpp:776-783`) usa os bits 17 e 19 do
word base `MOB.LearnedSkill` para ReflectDamage e AC (IDs base 65 e 67); nao foi
encontrada ligacao desses bits ou de `SecLearnedSkill` a row 228. O item 5528
`Unidade_Mental` compartilha `EF_VOLATILE=32` com os livros 5526/5527 e o handler
generico grava o mesmo bit 25 para os tres, sem efeito row-specific localizado
que o ligue a row 228. `BASE_GetBonusSkillPoint` percorre apenas os 24 bits da
arvore base (TMProject `Basedef.cpp:989-998`; W2PP `Basedef.cpp:878-887`). O C# projeta o bonus base do ID 67 em
`LegacyCurrentScoreLoginProjector.cs:194-211`, sem matcher para 228. As tabelas
server-side distribuidas com TMProject 7.69 terminam no ID 150; o
`_MSG_ApplyBonus.cpp:118-122` cobre somente `Detail=5000-5095`. A row nao tem
producer ou crosswalk funcional confirmado.

Decisao: nao inferir formula ou crosswalk pelo nome `Unidade_Mental`. Sem
alteracao de runtime; nenhum teste foi executado, pois nao mudou codigo.

### SkillData 229 - Invocação_Final / InstanceType 11 genérico

As rows 229 em `artifacts/skilldata-reference/skilldata.csv:167` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:167` sao identicas: ativa
(`Passive=0`), `TargetType=0`, `ManaSpent=600`, `Delay=100`, `InstanceType=11`,
`InstanceValue=9`, `TickType=0` e campos de affect zerados. Pelo agrupamento da
arvore estendida, 229 pertence a BM. As tabelas server-side distribuidas com
TMProject 7.69 terminam no ID 150; os CSVs de referencia e W2PP nao comprovam
que a TMSrv 7.69 carregue a row.

TMProject 7.69 `_MSG_Attack.cpp:1084-1114` e W2PP
`_MSG_Attack.cpp:809-840` tratam `InstanceType=11` pelo caminho generico. Para
`InstanceValue=9`, `summons` comeca em zero e nenhum ramo o altera; ambos usam
`SummonID=8` e zeram o dano. Em `GenerateSummon`, se houver um slot NPC livre
mas nenhum summon da mesma face na party, o loop de criacao nao executa e a
funcao retorna sucesso: nenhum summon aparece e a mana permanece debitada. Se
ja houver um summon da mesma face, `SummonNum >= 0` retorna falha e a source
restaura MP. A funcao tambem falha antes da busca na party quando
`GetEmptyNPCMob` nao encontra slot.

O C# ja tem esse caminho: `GetLegacySummonCount` mapeia 9 para zero e
`TryGenerateSummons` recebe `SummonID=8`; sem summon igual retorna `Created`
sem criar mob, e com um igual retorna `AlreadyPresent` e devolve o custo. A
source 7.69 tem uma divergencia de capacidade ainda nao modelada: o C# nao tem
equivalente ao slot fixo de `GetEmptyNPCMob`. No reembolso, o C# tambem atualiza
`RequestedMana`; W2PP atualiza `ReqMp`, enquanto TMProject deixa essa atribuicao
comentada.

Decisao: nao criar handler especifico; o comportamento ja segue o ramo generico
Type 11 existente, condicionado a row carregada. A carga da row e o crosswalk/
aprendizado da segunda barra continuam sem confirmacao. Nenhum codigo ou teste
foi alterado/executado nesta auditoria.

### SkillData 230 - Poworful Summon / passiva sem producer proprio

As rows 230 em `artifacts/skilldata-reference/skilldata.csv:169` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:169` sao identicas:
`Passive=1`, `TargetType=0`, `InstanceType=0`, `TickType=0`, `AffectType=0`,
`AffectValue=0` e `AffectTime=0`. Buscas por `Poworful_Summon`, pela grafia
`Powerful_Summon`, pelo ID 230 e por atribuicoes especificas na TMSrv 7.69,
W2PP e C# nao localizaram producer proprio. Os gates comuns rejeitam cast
passivo caso a definicao seja carregada.

A row 229 vizinha descreve uma skill ativa de summon, mas nenhuma das fontes
liga essa habilidade passiva a atributos ou comportamento dos summons. O
`SecLearnedSkill` e os caminhos de bonus de classe encontrados nao confirmam
um bit/crosswalk para a row 230. As tabelas server-side distribuidas com a
source TMProject 7.69 terminam no ID 150; portanto, a row do snapshot estendido
nao prova que a TMSrv 7.69 a carregue. O fluxo de aquisicao/crosswalk da segunda
barra continua pendente.

Decisao: nao inferir aumento de poder de summon pelo nome nem inventar formula
ou bit. Sem alteracao de runtime; nenhum teste foi executado, pois nao mudou
codigo.

### SkillData 231 - Ice Armor / passiva Type 1 sem producer proprio

As rows 231 em `artifacts/skilldata-reference/skilldata.csv:171` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:171` sao identicas:
`Passive=1`, `TargetType=0`, `InstanceType=0`, `TickType=0`, `AffectType=1`,
`AffectValue=255` e `AffectTime=4`. O loader TMProject divide `AffectTime` por
quatro, que resultaria em 1 se essa row fosse carregada. Buscas por `Ice_Armor`,
pelo ID 231 e por atribuicoes especificas na TMSrv 7.69, W2PP e C# nao
localizaram producer proprio. Os gates comuns rejeitam cast passivo:
TMProject `_MSG_Attack.cpp:114`, W2PP `_MSG_Attack.cpp:121` e C#
`LegacySkillAttackGate.cs:76-77`, caso a definicao seja carregada.

O Type 1 ja tem composicao generica de score, concordante em TMProject 7.69
`Basedef.cpp:3446-3458` e W2PP `Basedef.cpp:3938-3950`: subtrai Value de Run,
30 de Attack e, com item de cabeca acima do indice 50, mais 40 de Intelligence.
O C# possui a mesma etapa em `CalculateHolyTouchAffectStage`. Esse caminho
generico nao identifica `Ice_Armor` como producer; a row passiva ainda e
rejeitada no cast e nao ha bit de aprendizado ou fluxo de aquisicao ligado a
231. As tabelas server-side distribuidas com TMProject 7.69 terminam no ID
150; o `_MSG_ApplyBonus.cpp` localizado cobre a arvore base, sem estabelecer
crosswalk para a segunda barra.

Decisao: manter o suporte generico Type 1 existente sem atribui-lo a SkillData
231. Sem alteracao de runtime; nenhum teste foi executado, pois nao mudou
codigo.

### SkillData 232 - Concha Resistente / passiva sem producer

As rows 232 em `artifacts/skilldata-reference/skilldata.csv:173` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:173` sao identicas:
`Passive=1`, `TargetType=0`, `InstanceType=0`, `TickType=0`, `AffectType=0`,
`AffectValue=0` e `AffectTime=0`. Buscas por `Concha_Resistente`, pelo indice
232 e por atribuicoes especificas na TMSrv 7.69, na TMSrv W2PP e no C# nao
localizaram producer proprio. Os gates comuns rejeitam cast passivo:
TMProject `_MSG_Attack.cpp:114`, W2PP `_MSG_Attack.cpp:121` e C#
`LegacySkillAttackGate.cs:76-77`, caso a definicao seja carregada.

O bloco de CurrentScore para classe 2 nas duas fontes C++ trata bits do word
base de skills (bit 17 para ReflectDamage e bit 19 para AC com equipamento na
posicao 128); nao foi encontrada ligacao desses bits a row estendida 232. As
tabelas `Servidor/Server/Common/skilldata.csv` e
`Servidor/Server/TMSrv/run/skilldata.csv` distribuidas com TMProject 7.69
terminam no ID 150, entao a row dos CSVs de referencia estendida/W2PP nao prova
que a TMSrv 7.69 a carregue. O `_MSG_ApplyBonus.cpp` cobre `Detail=5000-5095`;
nao foi confirmado caminho de aquisicao ou crosswalk para a segunda barra.

Decisao: nao atribuir a Concha Resistente os bonuses de bits da arvore base nem
inventar formula ou bit. Sem alteracao de runtime. Nenhum teste foi executado,
pois nao mudou codigo.

### SkillData 233 - Espinhos Fortalecidos / passiva sem producer

As rows 233 em `artifacts/skilldata-reference/skilldata.csv:175` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:175` sao identicas:
`Passive=1`, `TargetType=0`, `InstanceType=0`, `TickType=0`, `AffectType=0`,
`AffectValue=0` e `AffectTime=0`. A busca por `Espinhos_Fortalecidos`, pelo
indice 233 e por atribuicoes especificas na TMSrv 7.69, na TMSrv W2PP e no C#
nao localizou producer proprio. Os gates comuns rejeitam cast passivo:
TMProject `_MSG_Attack.cpp:114`, W2PP `_MSG_Attack.cpp:121` e C#
`LegacySkillAttackGate.cs:76-77`, caso a definicao seja carregada.

Ha um ramo de `ReflectDamage` semanticamente proximo, mas sem ligacao provada
com esta row. Em `CMob.cpp`, TMProject 7.69 e W2PP usam o bit 17 do word base
`MOB.LearnedSkill` dentro do ramo `MOB.Class == 2` para somar
`(CurrentScore.Special[3] + 1) / 6` ao `ReflectDamage` (TMProject: linha 768;
W2PP: linha 779). Pela organizacao da arvore base em blocos de 24, classe 2 +
slot 17 mapeia ao ID base 65; nenhum dos fontes liga esse bit primario ao ID
estendido 233. Nao atribuir o bonus de reflexao a
`Espinhos_Fortalecidos` por semelhanca de nome.

Limite da evidencia: as tabelas `Servidor/Server/Common/skilldata.csv` e
`Servidor/Server/TMSrv/run/skilldata.csv` distribuidas com a source TMProject
7.69 terminam no ID 150. A row 233 no CSV de referencia estendida e no snapshot
W2PP nao prova que a TMSrv 7.69 carregue essa definicao. O
`_MSG_ApplyBonus.cpp` da 7.69 cobre `Detail=5000-5095`; nenhum caminho de
aquisicao/crosswalk ou bit para a faixa estendida foi confirmado.

Decisao: nao reaproveitar o `ReflectDamage` da habilidade base nem inventar
formula ou bit para a row 233. Sem alteracao de runtime; aquisicao/crosswalk da
segunda barra continua pendente. Nenhum teste foi executado, pois nao mudou
codigo.

### SkillData 234 - Weapon Power / passiva sem producer

As rows 234 em `artifacts/skilldata-reference/skilldata.csv:177` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:177` sao identicas:
`Passive=1`, `TargetType=0`, `InstanceType=0`, `TickType=0`, `AffectType=0`,
`AffectValue=0` e `AffectTime=0`. A busca por `Weapon_Power`, pelo indice 234 e
por atribuicoes especificas na TMSrv 7.69, na TMSrv W2PP e no C# nao localizou
um producer proprio. Os gates comuns rejeitam cast de definicoes passivas:
TMProject `_MSG_Attack.cpp:114`, W2PP `_MSG_Attack.cpp:121-122` e C#
`LegacySkillAttackGate.cs:76-77`.

Limite da evidencia: as tabelas `Servidor/Server/Common/skilldata.csv` e
`Servidor/Server/TMSrv/run/skilldata.csv` distribuidas com a source TMProject
7.69 terminam no ID 150. Portanto, a row 234 do CSV de referencia estendida e
do snapshot W2PP nao prova que a TMSrv 7.69 carregue essa definicao. O
`_MSG_ApplyBonus.cpp` da 7.69 cobre `Detail=5000-5095`, sem estabelecer
aquisicao para IDs 200-247; nenhum bit de aprendizado/crosswalk para 234 foi
confirmado.

Decisao: nao inferir bonus de arma pelo nome `Weapon_Power`, nao inventar um bit
de aprendizado e nao alterar runtime. A aquisicao/crosswalk da segunda barra
continua pendente. Nenhum teste foi executado, pois nao mudou codigo.

### SkillData 235 - Last Resistance / Type 48 generico

As rows 235 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas: ativa,
`TargetType=0`, `AffectType=48`, `AffectValue=15`, `AffectTime=1`, `TickType=0`.
O `SetAffect(conn, skillnum, time, Level)` de TMProject e W2PP e generico e
armazena os campos da row, mas isso sozinho nao prova que o handler tenha essa
definicao carregada. `BASE_InitializeSkill` da source TMProject le
`Server/Common/SkillData.csv` ou o fallback `TMSRV/Run/SkillData.csv`; as duas
tabelas distribuidas com esta source terminam no ID 150. O snapshot W2PP contem
a row 235, mas nao foi confirmada uma tabela de runtime equivalente para a
source TMProject. No C#, `TryApplyLegacyAffects` / `TrySetLegacyAffect` aceita
qualquer tipo positivo e armazena Type, Value, Level e Time, se a row estiver
carregada. Nenhuma das buscas localizou tratamento especial de score para
Type 48.

Decisao: manter o caminho generico C# sem alegar producer especifico na TMSrv
7.69 ate confirmar a tabela de runtime que carrega a row. A aquisicao/crosswalk
da barra estendida tambem continua pendente. Nenhum teste foi executado, pois
nao mudou codigo.

### SkillData 236 - Contra Ataque / passiva sem producer

As rows 236 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas e marcam
`Passive=1`, `InstanceType=0`, `TickType=0` e `AffectType=0`. Buscas por
`Contra_Ataque`, pelo indice 236 e por atribuicoes explicitas na source TMSrv
7.69, na TMSrv W2PP e no C# nao localizaram handler/produtor proprio. Os gates
comuns `_MSG_Attack` rejeitam a row passiva.

Decisao: nao inferir uma rotina de contra-ataque apenas pelo nome. Nenhum teste
foi executado, pois nao mudou codigo.

### SkillData 237 - Ataque Rapido Proficiente / passiva sem producer

As rows 237 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas e marcam
`Passive=1`, `InstanceType=0`, `TickType=0` e `AffectType=0`. Buscas por
`Ataque_Rapido_Proficiente`, pelo indice 237 e por atribuicoes explicitas na
source TMSrv 7.69, na TMSrv W2PP e no C# nao localizaram handler/produtor
proprio. Nenhum efeito foi inferido pelo nome.

### SkillData 238 - Skill Master / passiva sem producer

As rows 238 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas e marcam
`Passive=1`, `InstanceType=0`, `TickType=0` e `AffectType=0`. Buscas por
`Skill_Master`, pelo indice 238 e por atribuicoes explicitas na source TMSrv
7.69, na TMSrv W2PP e no C# nao localizaram handler/produtor proprio. O unico
texto semelhante e `_NN_Go_to_skill_master` em `Language.h`, sem chamada
associada a row 238.

### SkillData 239 - Freezing / passiva sem producer

As rows 239 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas e marcam
`Passive=1`, `InstanceType=0`, `TickType=0` e `AffectType=0`. Buscas por
`Freezing`, pelo indice 239 e por atribuicoes explicitas na source TMSrv 7.69,
na TMSrv W2PP e no C# nao localizaram handler/produtor proprio. Os gates comuns
`_MSG_Attack` rejeitam a row passiva.

Decisao: nao inferir efeito de congelamento pelo nome da row. Nenhum teste foi
executado, pois nao mudou codigo.

### SkillData 240 - Ponto do Mestre / passiva sem producer

As rows 240 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas e marcam
`Passive=1`, `InstanceType=0`, `TickType=0` e `AffectType=0`. Buscas por
`Ponto_do_Mestre`, pelo indice 240 e por atribuicoes explicitas na source TMSrv
7.69, na TMSrv W2PP e no C# nao localizaram handler/produtor proprio. Os gates
comuns `_MSG_Attack` rejeitam a row passiva.

Decisao: nao inventar efeito, formula ou bit de aprendizado para esta skill.
Nenhum teste foi executado, pois nao mudou codigo.

### SkillData 241 - Absorcao de Alma / InstanceType 0 sem efeito especial

As rows 241 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` concordam: `Passive=0`,
`InstanceType=0`, `TickType=0` e `AffectType=0`. Buscas por `Absorcao`, pelo
indice 241 e por atribuicoes explicitas na source TMSrv 7.69, na TMSrv W2PP e
no C# nao localizaram handler proprio. A `BASE_GetSkillDamage` 7.69 tem casos
InstanceType 0 somente para skills 11, 13, 41, 43, 44 e 45; o default retorna
zero. `LegacySkillCombatMath.GetSkillBaseDamage` usa o mesmo conjunto e fallback.

Decisao: manter o comportamento de fonte, sem inventar formula ou efeito para
esta row. O uso generico de `SetAffect`/`TryApplyLegacyAffects` nao produz
alteracao para a definicao, pois os campos de affect/tick sao zero. Nenhum teste
foi executado, pois nao mudou codigo.

### SkillData 242 - Plunder / passiva

As rows 242 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas e marcam
`Passive=1`. Buscas por `Plunder`, pelo indice 242 e por atribuicoes explicitas
de skill na source TMSrv 7.69, na TMSrv W2PP e em `src/tests` do C# nao
localizaram um producer especifico. O gate comum em TMProject
`_MSG_Attack.cpp:114` e W2PP `_MSG_Attack.cpp:121-122` rejeita cast de definicao
passiva.

Decisao: nao inventar efeito, formula ou bit de aprendizado para `Plunder`.
Esta entrada registra uma auditoria sem alteracao de runtime; aquisicao e
crosswalk da barra estendida continuam pendentes. Nenhum teste foi executado,
pois nao mudou codigo.

### SkillData 243 - Reinforce Soul / passiva

As rows 243 em `artifacts/skilldata-reference/skilldata.csv` e no snapshot
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` sao identicas e marcam
`Passive=1`. Buscas por `Reinforce_Soul`, pelo indice 243 e por atribuicoes
explicitas de skill na source TMSrv 7.69, na TMSrv W2PP e em `src/tests` do
C# nao localizaram um producer especifico. O gate comum em TMProject
`_MSG_Attack.cpp:114` e W2PP `_MSG_Attack.cpp:121-122` rejeita cast de definicao
passiva.

Decisao: nao inventar efeito, formula ou bit de aprendizado para `Reinforce_Soul`.
Esta entrada registra uma auditoria sem alteracao de runtime; aquisicao e
crosswalk da barra estendida continuam pendentes. Nenhum teste foi executado,
pois nao mudou codigo.

### SkillData 244 - Tiro Direto / Type 40 passiva

TMProject 7.69 e o snapshot comparativo
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` concordam na row 244:
`Passive=1`, `AffectType=40`, `AffectValue=0` e `AffectTime=1`. TMProject
`_MSG_Attack.cpp:114` e W2PP `_MSG_Attack.cpp:121-122` rejeitam casts de rows
passivas. O `MSG_ApplyBonus` 7.69 localizado so aceita `Detail=5000-5095` e
`skillpos=0-95`, sem comprovar aquisicao para a faixa 200-247.

Type 40 tambem aparece em um caminho distinto. TMProject
`_MSG_Attack.cpp:1958-1970` e W2PP `_MSG_Attack.cpp:1479-1491` aplicam Affect e
Tick Type 40 no alvo de ataque fisico quando `RSV_DRAIN` esta ativo e a rolagem
de 50% passa, usando `Special[1] + 150` e `Special[1]` como nivel. Nenhuma das
duas fontes conecta esse producer a SkillData 244. O `case 40` em
`_MSG_SingleControl.cpp:137` despacha NPC grade 40 para quest Helgardh; nao e
um affect. Nao foi localizado producer proprio, formula ou bit aprendido para
`Tiro_Direto`.

Decisao: nao associar `RSV_DRAIN` ou o NPC grade 40 a row 244 por coincidencia
de Type. O C# nao recebe nova regra nesta auditoria; aquisicao/crosswalk da
barra estendida continua pendente. Nenhum teste foi executado, pois nao mudou
codigo.

### SkillData 247 - Ambush / passiva Type 1

As linhas 7.69 e o snapshot comparativo
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` concordam na row 247:
`Passive=1`, `AffectType=1`, `AffectValue=50`, `AffectTime=4`. TMProject
`_MSG_Attack.cpp:114` e W2PP `_MSG_Attack.cpp:121-122` rejeitam skills passivas
no caminho de cast. No `MSG_ApplyBonus.cpp`, a faixa localizada de aprendizado
da source 7.69 valida `Detail=5000-5095` e usa `skillpos=0-95`, portanto cobre
as 96 skills base e nao demonstra aprendizado da faixa 200-247.

O varrimento das rotinas de skill/score de `TMSrv` nao encontrou producer que
aplique o Type 1 da row 247 nem uma referencia nominal/ID ao `Ambush`. A source
usa `MOB.LearnedSkill` de 32 bits e `MOBEXTRA.SecLearnedSkill` como campo
distinto; nao ha crosswalk verificado que ligue esta row a um bit C# persistido.
O gate C# comum ja rejeita casts passivos. Decisao: nao aplicar automaticamente
o Type 1 nem atribuir um bit especulativo. A row fica auditada, mas sem port de
gameplay ate existir evidencia do producer e do estado de aprendizado.

A busca read-only por `Ambush` no WYD.exe comunitario 7.69
(SHA-256 `C248788036749D1728B2F2ABC5F7CE6934977350E24738021AFEC35AE6DBBE3F`)
nao gerou evidencia: Ghidra excedeu o limite do provedor de 330 s; a nova
tentativa com prazo ampliado excedeu o limite de chamada Codex de 450 s. A
sessao REA foi fechada. Nenhum fato sobre o cliente binario foi inferido ou
usado no port.

### SkillData 245 - Garra Habilidosa / passiva sem affect

TMProject 7.69 e o snapshot comparativo
`artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv` concordam na row 245:
`Passive=1`; `TickType`, `AffectType`, `AffectValue` e `AffectTime` sao zero. A
busca delimitada aos fontes `TMSrv` nao encontrou referencia nominal/ID a
`Garra_Habilidosa` ou producer para essa row. O gate `_MSG_Attack` rejeita casts
passivos; `MSG_ApplyBonus` 7.69 trata `Detail=5000-5095` e posicoes `0-95`, sem
demonstrar aprendizado dos IDs 200-247. O C# ja rejeita o cast pelo gate comum,
mas nao tem um ramo especifico para ID 245.

Decisao: nao criar bonus de claw apenas pelo nome `Garra_Habilidosa`; a row nao
define um affect nem foi localizado calculo server-side correspondente. O
aprendizado/crosswalk persistente da barra estendida segue pendente. Esta fatia
e auditoria de source e nao altera gameplay; nenhum teste novo foi executado.

### SkillData 246 - Bleeding / timer generico Type 47

A row 246 de `artifacts/skilldata-reference/skilldata.csv` identifica `Bleeding`
com AffectType 47. TMProject 7.69
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/Server.cpp:6899-6920`
e W2PP `Server/W2PP/Source/Code/TMSrv/Server.cpp:5831` conduzem affects
finitos ao mesmo tail `LessAffect`: decrementam Time, zeram os campos do
registro ao expirar e solicitam recomposicao/refresh de score. Nao foi
encontrado um ramo Type 47 separado de dano ou score nessa trilha; o port
limita-se ao lifetime generico e nao inventa dano periodico.

`WorldHub.TryProcessPlayerTimerAffects` ja percorre o tail generico. A lacuna
estava nas listas de tipos aceitos pelos timers de NPC gerado e comum e pelo
timer de summons RouteType 5. `WorldHub.cs` agora inclui Type 47 nessas tres
listas. A expiracao limpa os oito bytes do slot e pede refresh de score, sem
alterar HP; nos summons, o timer tambem nao relaya score antes do expiry.

`LegacyMobAiRuntimeTests.ExtendedSkillBleedingTypeFortySevenUsesGenericNpcAndSummonTimerTail`
cobre countdown e expiry em NPC gerado, NPC comum e summon RouteType 5. A
regressao falhou antes da mudanca porque os timers ignoravam Type 47 e passou
depois. Engine.Tests `537/537`; rebuild integral
`dotnet build WydCdk.Engine.slnx --no-restore --no-incremental -m:1`: 0 avisos,
0 erros. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Nao houve smoke de rede nem Host autenticado/E2E
visual desta skill.

### Skill 204 - runtime de Maos Sangrentas

SkillData 7.69 row 204 e passiva. TMProject `_MSG_Attack.cpp:477-535` aplica-a
durante o segundo tipo de DoubleCritical quando o atacante e TK (Class 0), tem
`MOBEXTRA.SecLearnedSkill & 0x10`, e os dois slots de arma 6/7 contem garras
(`ItemData.Position=192`). A chance e `rand()%100 < 20`; a cura e
`Special3*10/2 + rand()%15`, limitada a MaxHp. A source envia a atualizacao de
score ao atacante e `MSG_SetHpDam` na view capturada. W2PP nao tem este ramo; o
port segue o contrato 7.69.

O C# agora calcula o proc no caminho de ataque fisico, atualiza HP, e enfileira
`UpdateScore`/`SetHpDam` como acao secundaria limitada aos viewers capturados.
Engine.Tests GREEN `537/537`; rebuild integral
`dotnet build WydCdk.Engine.slnx --no-restore --no-incremental -m:1`: 0 avisos,
0 erros. SDK 10.0.401; `dotnet --info` identificou o SDK e saiu 1 porque o
runner nao pode abrir o Service Control Manager. A cobertura verifica gates,
limite estrito `rand()%100 < 20`, formula e queue da relay. O codigo clampa HP
em MaxHp; falta um assert dedicado para esse limite. O
aprendizado pelo livro 5504 e a persistencia/crosswalk da segunda barra nao foram
incluidos nesta fatia. O loopback `--mob-ai-smoke` passou com
`MOB_AI_SMOKE_PASS`; nao ha marcador especifico de Maos Sangrentas nesse
harness, entao o relay desta skill nao foi exercitado na rede. Sem Host
autenticado ou E2E visual para a skill.

## MOB-AI-001 audit slice - Skill 203 Clod Attack / Type 41 (26/10/2026)

Os dados extraidos em `artifacts/skilldata-reference/skilldata.csv:115` e no
snapshot W2PP `artifacts/w2pp-tmsrv-sandbox2/Common/skilldata.csv:115` concordam:
ID 203, `Clod_Attack`, Passive=1, AffectType=41, AffectValue=0 e AffectTime=2.
O CSV do editor da source 7.69 tem um placeholder zerado para a mesma ID; ele
nao representa os dados extraidos e nao foi usado como contrato.

A source 7.69 e W2PP rejeitam no `_MSG_Attack` qualquer tentativa de cast de uma
row com `Passive=1`. A source 7.69 seta Type 41 em `NTask_CarbuncleWind.cpp:12`
e W2PP o seta no buff de Carbuncle em `_MSG_Quest.cpp:2383`, ambos pelo setter
generico; nao ha ligacao desses producers com Skill 203. O score da source tem
um gate de `Affect.Type == 36` por `EF_WTYPE == 41` (`Basedef.cpp:3666`), onde
41 e tipo de arma. A busca em C++ e C# nao encontrou producer ou efeito de
dano/score especifico da Skill 203; o lifetime generico de affect continua
aplicavel a qualquer Type 41 que outro caminho produza.

O servidor W2PP tem um unico word `LearnedSkill` e um `MOBEXTRA.SecLearnedSkill`
distinto; o adapter C# zera o word 1 do MOB 7.69 (skills 200-246), conforme a
matriz de ABI dos fixtures. Nao ha crosswalk comprovado que permita guardar a
Skill 203 nesse word sem um contrato de personagem versionado. Decisao: manter
sem port de gameplay ate uma fonte de servidor definir a semantica da passiva e
seu estado persistente. A analise REA de `TMSrv.exe` nao concluiu, portanto nao
foi usada para confirmar ou negar comportamento.

## MOB-AI-001 audit slice - Skill 46 Controle de Mana / Type 18 (26/10/2026)

SkillData 7.69 row 46 define `Controle_de_Mana` como ativa, self-target,
`InstanceType=0`, `TickType=0`, `AffectType=18`, `AffectValue=0`, CSV
`AffectTime=60` (15 ticks no loader) e `ManaSpent=130`. TMProject 7.69
`TMSrv/_MSG_Attack.cpp:1727` e W2PP `TMSrv/_MSG_Attack.cpp:1228` usam o tail
generico `SetAffect`/`SetTick`; `TMSrv/Server.cpp:10359` e
`W2PP/TMSrv/Server.cpp:9209` leem a metadata da SkillData. O Type 18 nao altera
CurrentScore no `BASE_GetCurrentScore`; seu uso e no ataque fisico contra o
portador do affect.

TMProject 7.69 `_MSG_Attack.cpp:2043-2062` aplica Mana Control somente quando
MP atual e estritamente maior que `(MaxMP / 100) * 20`, subtrai do MP o dano
recebido, adiciona eventual deficit ao dano e divide o resultado por 60, ou 55
com LearnedSkill bit 23. W2PP `_MSG_Attack.cpp:1540-1558` diverge no limite
`>10%` e usa divisores 55/50. `LegacyManaControlMath` ja seguia a referencia
7.69; a aplicacao generica de affects do `WorldHub` tambem aceitava a skill 46.
Logo, a auditoria nao encontrou gap de gameplay nessa row.

`WorldSkill46ManaControlCast` adiciona evidencia integrada: consome 130 MP
(400 -> 270), conjura a row 46 no proprio jogador e confirma o affect Type 18;
um ataque fisico seguinte drena mais 21 MP, aplica dano final 5 e deixa HP 995,
CurrentMP 249 e RequestedMP 270, conforme o comportamento 7.69. A primeira
versao do teste confundia o estado retornado pelo metodo de custo (pos-debito)
com o estado anterior; a assercao foi corrigida conforme o contrato existente.
Engine.Tests `536/536`; build integral
`dotnet build WydCdk.Engine.slnx --no-restore --no-incremental -m:1`: 0 avisos,
0 erros. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Como nenhum comportamento de runtime mudou, nao foi
executado novo smoke de rede; a auditoria completa SkillData permanece aberta.

## COMBAT-ATTACK-SKILL-96-TYPE4-001 - Poder Superior / score online (26/10/2026)

SkillData 7.69 row 96 identifica `Poder_Superior`: ativa (`Passive=0`),
self-target (`TargetType=0`), sem tick (`TickType=0`) e `AffectType=4`. O
estágio de score Type 4 já estava implementado no composer de login, mas o
`WorldHub` não incluía a skill 96 na reconstrução online de score após
`MSG_Attack`. O matcher foi limitado à definição ativa auditada da row 96.

TMProject 7.69 usa o helper comum ativo auditado em
`TMSrv/Basedef.cpp:3946-3970`: cada Type 4 aplica, com truncamento inteiro,
HP +15%, MP +10%, Damage +30% e Magic +20% do `MOB.Magic` de origem. W2PP
diverge em `Basedef.cpp:3970-3974`: omite HP/MP, soma DamageMulti +4, Damage +30
e Magic +5. O port segue TMProject 7.69. Essa comparação complementa a
auditoria de source e de limites já registrada na seção
`COMBAT-LOGIN-AFFECT-AUDIT-001` abaixo.

Há uma origem distinta para Magic durante uma sessão: o projetor do login
atualiza o `MOB.Magic` enviado ao cliente. O Host agora captura o Magic bruto
antes dessa projeção e passa o valor ao `WorldHub`, que o guarda no estado da
sessão. A reconstrução usa a origem guardada apenas quando a lista de affects
nova ou anterior contém Type 4. Isso mantém cast/recast idempotente e permite
que a expiração recomponha o baseline, sem alterar schema ou persistência.

`AttackPlayerSuperiorPowerAffectTests` cobre os deltas, HP/MP atuais, registro
Type 4, recast sem acúmulo e restauração integral no expiry. O fixture instala
MOB já processado pelo projetor de login e fornece separadamente a origem
Magic, reproduzindo o limite entre Host e WorldHub. RED Engine.Tests `534/535`
falhou na projeção/reaplicação; GREEN `535/535`. Rebuild integral de
`WydCdk.Engine.slnx` com SDK 10.0.401: 0 avisos/erros.

O smoke TCP sintético `--mob-ai-smoke` verifica a expiração Type 4 e terminou
com `player-type4-expiry=score-only-relayed` e `MOB_AI_SMOKE_PASS`. UpdateScore
chegou aos peers na view capturada; o peer externo não recebeu frame.
`dotnet --info` saiu 1 ao abrir o Service Control Manager (acesso negado), mas
identificou o SDK 10.0.401. Validação em WorldHub/loopback; sem Host
autenticado ou E2E visual desta skill.

## COMBAT-ATTACK-SKILL-216-TYPE42-001 - Magia Misteriosa / troca de HP-MP (26/10/2026)

TMProject 7.69 `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/Basedef.cpp:3591-3598`
e W2PP `W2PP/Source/Code/Basedef.cpp:4454-4460` concordam no Type 42. Para
cada registro ativo, a mana transferida e
`MaxMP / 5 + ((effectiveLevel + Affect.Level) / 2)`, com divisao inteira; o
efeito soma esse valor a MaxHP e subtrai de MaxMP. `effectiveLevel` e o nivel
atual quando MOBEXTRA class-master e ARCH (1) ou MORTAL (2), e `level + 399`
para os demais valores. SkillData row 216 identifica `Magia_Misteriosa` como
nao-passiva, self-target, sem tick e com AffectType 42/duracao 750; a row 216
conferida no W2PP tem os mesmos campos/formula.

O port estava ausente. `LegacyCurrentScoreMath` agora le o `Affect.Level` no
offset +2 do registro e compoe Type 42 em ordem de slot junto dos outros affects,
recebendo o nivel ja composto e o class-master. `WorldHub` reconhece somente a
definicao auditada da skill 216 (Passive 0, TargetType 0, InstanceType 0,
Aggressive 0, TickType 0, AffectType 42) para reconstruir o score online. O
projetor preserva HP/MP atuais durante cast/recast; o timer generico de jogador
ja recompunha o score ao expirar e restaura os maximos-base, sem empilhamento.

`AttackPlayerSoulSwapAffectTests` cobre projecao de login nas classes ARCH,
MORTAL e outra classe, formula com Affect.Level, cast/recast/expiry pela skill
216 e preservacao de HP/MP. `CurrentScoreAffectCompositionTests` fixa a interacao
na ordem dos slots entre Type 42 e Type 38. RED Engine.Tests `532/533`: o novo
caso falhou porque MaxHP/MaxMP nao refletiam o calculo de class-master e
Affect.Level. GREEN `533/533`. `dotnet build WydCdk.Engine.slnx --no-restore
--no-incremental` passou, exit 0.

O smoke TCP sintetico `--mob-ai-smoke` inclui expiry de Type 42 no helper de
affect generico e terminou com `player-type42-expiry=score-only-relayed` e
`MOB_AI_SMOKE_PASS`. UpdateScore de expiry chegou aos peers TCP na view e nenhum
frame foi enviado ao peer externo. A primeira execucao foi bloqueada pela
sandbox (SocketException 10013); a repeticao local autorizada passou, exit 0.
SDK 10.0.401; `dotnet --info` identificou a versao e saiu 1 por acesso negado
ao Service Control Manager. O smoke e sintetico em loopback, sem Host autenticado
nem E2E visual. Sem package/publish/deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-EQUIPMENT-GRADE-001 - graus de equipamento no dano fisico (26/10/2026)

TMProject 7.69 `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/CMob.cpp:812-910`
acumula os bonus por item equipado. `Basedef.h:118-129` define os graus: ForceDamage
recebe `20 + itemSanc*2` no grade 9, `30 + itemSanc*2` nos grades 20/22,
`60 + itemSanc*2` nos grades 24/25 e `80 + itemSanc*2` no grade 26; o grade 6
mantem o incremento legado `i == 20`, inalcançavel no loop 0..15. ReflectDamage
recebe +20 somente no grade 8, `20 + itemSanc*2` no grade 10, `30 + itemSanc*2`
nos grades 21/22, `60 + itemSanc*2` no grade 25 e `80 + itemSanc*2` no grade 26.
O mesmo bloco aplica gemas 1/3 por `isanc`; `Basedef.h:345-350` e
`Basedef.cpp:2242-2288` confirmam os REF `10,12,15,18,22,27` e a conversao da
santificacao codificada para esses seis niveis.

W2PP `Server/W2PP/Source/Code/TMSrv/CMob.cpp:821-874` diverge nos graus:
mantem apenas grade 5 DropBonus, grade 6 ForceDamage com o mesmo ramo sem efeito,
grade 7 ExpBonus e grade 8 ReflectDamage +20. As duas fontes concordam na tabela
REF e no calculo das gemas. O C# adota as formulas mais completas da autoridade
7.69 e preserva o ramo grade 6/slot 20 sem corrigir o comportamento legado.

`WorldHub.GetLegacyForceDamage` e `GetLegacyReflectDamage` aplicam os bonus ao
ataque fisico; o grade 6 nao recebe base de ReflectDamage, e a gema ForceDamage
tambem passou a traduzir santificacao para os seis REF corretos (antes lia
11..15 em sequencia e superestimava REF_11/12). O
teste `WorldEquipmentCombatGradeBonuses` verifica no ataque integrado do
WorldHub o total de ForceDamage 648 e ReflectDamage 394, incluindo os graus,
santificacoes e a gema de REF_11. Engine.Tests RED `531/532`: a primeira execucao
expos o ForceDamage dos graus ausente; uma regressao adicional tambem detectou
grade 6 recebendo indevidamente ReflectDamage +20. GREEN `532/532`.

Build `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental`: 0 avisos,
0 erros. Build de `WydCdk.Client.E2E`: 0 avisos/erros. O smoke TCP sintetico
`--mob-ai-smoke` passou `EQUIPMENT_GRADE_COMBAT_SMOKE_PASS force-damage=200
reflect-damage=40 attack=view-scoped=true` e `MOB_AI_SMOKE_PASS`: o AttackOne
com dano autoritativo chegou aos dois peers dentro da view e nenhum frame chegou
ao peer externo. A primeira tentativa de loopback foi bloqueada pela sandbox
(SocketException 10013); a execucao local autorizada depois passou, exit 0. SDK
10.0.401; `dotnet --info` identificou o SDK e saiu 1 por acesso negado ao Service
Control Manager. Smoke sintetico com WorldHub e TCP loopback; sem Host autenticado
ou E2E visual. Sem package/publish/deploy, Site/producao, commit ou push.

## MOB-AI-001 Task 5EA - Escudo do Tormento no score de login (26/10/2026)

TMProject 7.69 `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/CMob.cpp:770`
e W2PP `Server/W2PP/Source/Code/TMSrv/CMob.cpp:782-783` concordam: na classe
Beastmaster (`MOB.Class == 2`), o bit 19 aprendido habilita Escudo do Tormento;
se o item de `Equip[7]` tem `nPos == 128`, o CurrentScore.Ac recebe
`(BASE_GetItemAbility(&MOB.Equip[7], EF_AC) + 1) / 7`. SkillData 7.69 row 67
identifica Escudo do Tormento como passiva. Nao ha divergencia de formula/gate
entre as fontes.

O ramo C# estava ausente. `LegacyCurrentScoreLoginProjector` agora calcula o
bonus depois do coordenador CurrentScore, consulta a posicao e o EF_AC do item
no slot secundario e grava o AC resultante no MOB projetado. O calculo segue a
ordem de `CMob::GetCurrentScore`, depois de `BASE_GetCurrentScore`.

`CurrentScoreLoginProjectionTests` monta ItemList sintetica com shield de AC 28
e confirma diferenca de +4 AC com a passiva; tambem confirma que a classe errada
e item que nao seja shield nao recebem bonus. RED Engine.Tests `530/531`
falhou especificamente com AC `128` tanto com quanto sem skill. GREEN:
`531/531`. Build `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental`:
0 avisos, 0 erros. `dotnet --info` identificou SDK 10.0.401 e saiu 1 ao tentar
abrir o Service Control Manager (acesso negado).

O smoke headless existente `--current-score-login-projection-smoke` passou com
`CLIENT_E2E_PASS current-score-login-projection` em `127.0.0.1:18283`, usando
copia descartavel de contas sinteticas; verificou login, CreateMob, UpdateScore,
logout e relogin. Esse fixture e o cenario geral Type 35, nao configura o bit 19;
a formula de Escudo do Tormento e coberta diretamente pelo teste do projetor.
Host encerrado e porta liberada. Sem E2E visual, package/publish/deploy,
Site/producao, commit ou push.

## MOB-AI-001 Task 5DZ - RSV procs em ataques de skill (26/10/2026)

TMProject 7.69 `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1943-1972`
e W2PP `Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1466-1493` aplicam os
procs no tail comum de `MSG_Attack`, depois do gate de dano positivo. Cada
resistencia faz uma rolagem independente `rand()%2 == 0`; usa `Special2` como
nivel e `Special2 + 150` como delay. Ambos tentam `SetAffect` e `SetTick` para
as skills 36/40; o SkillData seleciona o registro compativel: Type 1 de
RSV_FROST (36) e Type 20 de RSV_DRAIN (40). As duas fontes concordam nesta
sequencia.

O port estende o ataque de skill para jogador, NPC e summon e reutiliza o helper
do ataque fisico. Isso conserva os gates de `SetAffect` (NPC ate 1000,
Merchant rejeitado) e `SetTick` (NPC 1001+ permitido, Merchant rejeitado apenas
acima desse limite), alem de `RSV_BLOCK`. Os rolls deterministas sao por
separado; nenhum proc ocorre em miss/dano final nao positivo. A aplicacao atualiza
o estado de affect e os snapshots de score/affect retornados para os relays
view-scoped existentes.

`AttackSkillSecondaryAffectTests` cobre Type 1 e score de NPC, limite
`SetAffect`, `RSV_BLOCK`, Type 20 em NPC 1001 e em summon, gate Merchant de
`SetTick`, rolls falhos, dano zero e Type 1 em jogador. Engine.Tests: `531/531`. Build integral
sem incremental e build E2E: 0 avisos/0 erros. `--mob-ai-smoke` passou
`RSV_FROST_SKILL_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true` e
`MOB_AI_SMOKE_PASS`; os dois peers dentro da view receberam UpdateScore e o peer
externo nao recebeu frame. O smoke e WorldHub/TCP sintetico em loopback, nao Host
autenticado ou E2E visual. SDK 10.0.401; `dotnet --info` identificou o SDK e
saiu 1 por NullReferenceException no bloco do Workload Installer. Sem
package/publish/deploy, Site/producao, commit ou push.

## MOB-AI-001 Task 5DY - passivas de classe Hunter 2/7/18/22 (26/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:4198-4238` fecha o ramo
da classe HT depois da composicao de score. Os acumuladores sao definidos antes
do loop em `Basedef.cpp:3323-3341`: `special1` equivale a CurrentScore.Special2
+ EF_SPECIAL2/EF_SPECIALALL; `special3` equivale a CurrentScore.Special4 +
EF_SPECIAL4/EF_SPECIALALL; ambos limitados a 255. O C# calcula sobre o score
ja composto e projeta:

- Skill 2 Agressividade: Damage += Special2 + 10 quando o unique da arma do
  slot 6 e 42 (bow) ou 43 (claw).
- Skill 7 Tempestade de Raios: Damage += Special2 * 2.
- Skill 22 Protecao das Sombras: AC += Special4 / 3 + 10 somente com unique 43.
- Skill 18 Visao do Cacador: Critical += Dexterity / 50.

W2PP `Basedef.cpp:3795-3869` diverge: Agressividade usa formulas de DEX/STR
para varios uniques; Tempestade usa +200 fixo; Visao usa outra formula com
minimo 4; Protecao usa bit 23 (comentario indica 22) e nao exige claw. A regra
7.69 permanece a autoridade do port.

`HunterClassPassiveSkillTests` cobre os quatro resultados, classe/bit, gates de
arma, unique ItemList 42/43/46 e divisao inteira. RED: 526/530; GREEN
Engine.Tests: 530/530. Uma execucao RED tambem teve falha isolada no teste Type
23; a repeticao mostrou somente os quatro RED esperados e a execucao GREEN
passou. Build integral sem incremental: 0 avisos/erros. `--mob-ai-smoke`
terminou com `MOB_AI_SMOKE_PASS`, mas nao tem marcadores proprios para essas
passivas; nao declarar cobertura TCP, Host autenticado ou E2E visual.

## MOB-AI-001 Task 5DX - skill 14 Nocao de Combate passiva (26/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:4193-4195` aplica no
ramo TK, com o bit 14 aprendido, `CurrentScore.Damage += special2 / 2`. O valor
local `special2` e formado em `Basedef.cpp:3328-3341` a partir de
`CurrentScore.Special[2] + EF_SPECIAL3 + EF_SPECIALALL`, limitado a 255; no C#,
corresponde a `CurrentScore.Special3` apos os estagios de score/equipamento.
W2PP `W2PP/Source/Code/Basedef.cpp:3434-3445` mantem o ramo inteiro dentro de
comentario e suas formulas inativas (multiplicadores de DEX/STR e +10% de dano)
nao equivalem a 7.69. A 7.69 permanece a autoridade do port.

`CombatAwarenessPassiveSkillTests` verifica bit/classe, divisao inteira de 51
para 25 e ausencia do bonus sem os gates. RED: 525/526; GREEN Engine.Tests:
526/526. Build integral sem incremental: 0 avisos/erros. `--mob-ai-smoke`
terminou com `MOB_AI_SMOKE_PASS`, mas nao tem marcador proprio para skill 14;
nao declarar cobertura TCP, Host autenticado ou E2E visual desta passiva.

## MOB-AI-001 Task 5DW - skill 15 Armadura Critica passiva (26/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:4184-4195` aplica no
ramo `MOB.Class == 0` (TK), se `LearnedSkill` tem o bit 15:
`CurrentScore.Ac = (int)(CurrentScore.Ac * 1.1f)` e `Critical += 25`. A etapa C# e
aplicada sobre o AC ja composto e incorpora +25 no Critical antes do clamp
final. W2PP `W2PP/Source/Code/Basedef.cpp:3309` tambem usa o bit 15 no ramo
Trans, mas aplica bonus de dano condicionado a arma; nao reproduz a multiplicacao
de AC nem os 25 de Critical da 7.69. A release 7.69 e a autoridade selecionada
para este port.

`ArmorCriticalPassiveSkillTests` verifica classe, bit aprendido, AC, Critical e
ausencia do bonus sem gate. RED: 524/525; GREEN Engine.Tests: 525/525. Build
integral sem incremental: 0 avisos/erros. O smoke TCP global `--mob-ai-smoke`
passou, mas nao contem caso proprio para esta passiva; nao atribuir a skill 15
cobertura de rede, Host autenticado ou E2E visual.

## MOB-AI-001 Task 5DV - correcao da skill 82 passiva e Hunter Proficiency (26/10/2026)

Na SkillData 7.69, row 82 `Pericia de Cacador` tem `Passive=1` e AffectType 29;
row 102 `Limite da Alma` tem `Passive=0` e AffectType 29. `_MSG_Attack.cpp:114`
rejeita skills passivas. Portanto, o matcher Type 29 aceita apenas skill 102,
que conserva cast, score e expiry portados; skill 82 nao cria affect Type 29 e
o gate comum retorna `PassiveSkill`.

O ramo HT em `Basedef.cpp:4209-4214` da TMProject 7.69 aplica para skill 82
`special1 + 12` ao dano quando o slot 6 tem arma e o slot 7 nao tem escudo.
`special1` e o acumulador de `CurrentScore.Special[1] + EF_SPECIAL2 +
EF_SPECIALALL`, limitado a 255, equivalente ao `CurrentScore.Special2` do C#.
A source usa `1 << 82`, mas `LearnedSkill` e um inteiro de 32 bits, logo o shift e
comportamento indefinido em C++. O port escolhe bit 10 porque o gate de skills
de classe usa `skillIndex % 24`; isto e uma inferencia deterministica, nao uma
observacao confirmada do executavel. W2PP nao tem esse ramo de Hunter
Proficiency. A tentativa de confirmar no TMSrv compilado nao produziu evidencia
de funcao; o provedor objdump nativo nao estava disponivel. Manter a origem do
bit 10 explicitamente como inferencia ate haver evidencia valida do binario.

Engine.Tests `524/524`; build integral sem incremental 0/0. O smoke TCP passou
`PLAYER_SOUL_TYPE29_SKILL102_SMOKE_PASS`,
`HUNTER_PROFICIENCY_SKILL82_SMOKE_PASS` e `MOB_AI_SMOKE_PASS`; este confirma
somente o comportamento sintetico e view-scoped exercitado pelo harness. Sem
Host autenticado ou E2E visual para essas skills; sem package/publish/deploy,
Site/producao, commit ou push.

## COMBAT-ATTACK-BR-ARENA-GATES-001 - gates de Battle Royale fora do combate (15/10/2026)

Auditoria: a TMProject 7.69 nao tem selecao/registro de vencedor nem placar de
Battle Royale. A recompensa e somente o `BRItem` criado no altar em 2->3
(`Server.cpp:7941-7955`), ja portado em COMBAT-ATTACK-BR-REWARD-001; quem o pega
e o vencedor de fato. W2PP tambem nao tem esses estados. A pendencia antiga de
"vencedor/recompensa completa/placar" fica encerrada por ausencia na fonte.

Gates portados (todos exigem `BrState != 0` e `BRItem > 0` e usam a posicao do
proprio jogador): pedido de party (`_MSG_SendReqParty.cpp:51`) e aceite
(`_MSG_AcceptParty.cpp:68`) sao descartados em silencio dentro do retangulo
inclusivo 2604..2648/1708..1744, depois do teste de nivel; Action aceito dentro
da arena faz `DoRecall` (Effect 1/Speed 2, visoes de origem e destino) quando
`BrGrid == 0` e nivel 100..999 ou `BrGrid == 1` e nivel 200..999
(`_MSG_Action.cpp:308-322`, depois do GridMulticast); chat na arena ou em
896..1150/1405..1538 troca os seis primeiros bytes por `??????`
(`_MSG_MessageChat.cpp:221-226`). `BrGrid = tm_min / 20` e publicado a cada tick
do agendador quando o servidor e elegivel, inclusive fora de BRHour
(`Server.cpp:7930-7935`). `LegacyBattleRoyaleArenaRules` concentra as regras.
Fora: `Server.cpp:4446` usa `BrState` como indice de jogador no CreateMob das
torres (alias de descompilacao ligado a `GTorreState`); nao portado. Mensagem
curta mascarada vira exatamente `??????` (o buffer do cliente apos o NUL e zero).

Testes `BattleRoyaleArenaGateTests` (5): limites inclusivos, segunda area do
chat, faixas de nivel por BrGrid, mascara, party invite/accept, recall com
posicao autoritativa, chat por posicao e publicacao de BrGrid so em servidor
elegivel. Nesta rodada tambem foram fixados `attackMissRoll` em testes antigos
que dependiam do miss aleatorio de 1% (world physical/skill attack, attack
authoritative pipeline, mixed player resurrection e BR friendly-fire, este com
argumentos posicionais que caiam em affect/instance/burn); 15 execucoes
seguidas sem falha. Engine.Tests 480/480; build `WydCdk.Engine.slnx` SDK
10.0.401: 0 erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`;
`dotnet --info` exit 0. Evidencias em
`Server/Backup/Build/BattleRoyaleArenaGates-20261015`. Sem smoke TCP/headless,
E2E visual, deploy, commit ou push; nenhum opcode novo.

## MOB-AI-001 - IA de monstros (parcial; atualizacao 20/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:2277-2339` e W2PP
`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1674-1721` concordam no tail depois de
dano vivo positivo: `SetBattle(idx, attacker)`, summons da party do atacante
contra o alvo e summons da party do alvo contra o atacante. `SetBattle` grava
Mode=MOB_COMBAT antes de `AddEnemyList`, sem mudar `CurrentTarget`; a lista tem
13 slots, limite retangular inclusivo de +/-23 por summon e filtros de jogador
hide/Merchant. O branch de ataque comum e o que consome o estado em
`MobAI`/ProcessSecTimer. A rotina tambem seleciona alvos passivos, percorre
rotas e segmentos, persegue, retorna ao spawn e executa habilidades do
`SkillBar`.

No C#, `WorldHub` ja implementa snapshots defensivos, `SetBattle` e insercao de
aggro por slot depois de persistir o estado do alvo; a propagacao cobre NPCs
gerados e summons reais das parties, sem agrupar NPCs por `GenerateIndex`.
`ProcessSecMinTimer.cpp` da TMProject 7.69 incrementa `SecCounter`, percorre
primeiro Peace com seis grupos (`SecCounter % 6`, indices a partir de
`MAX_USER + Sec6`) e depois Combat com quatro (`SecCounter % 4`, indices a
partir de `MAX_USER + Sec4`); W2PP mantem a mesma ordem. O timer inicia em
500 ms e muda para 200 ms quando `ServerDown == 120` (7.69
`ProcessSecMinTimer.cpp:104-114`, `Server.cpp:4977`). O host C# preserva seu
agendamento pronto de 200 ms e, a cada callback, calcula `tick % 6`, processa
Peace, e entao calcula `tick % 4` e processa Combat. O filtro de shard usa
`ConnectionId - 1000`, equivalente ao indice legado relativo a `MAX_USER`; no
grupo de quatro o offset nao muda a distribuicao pois 1000 e divisivel por 4.
Quando um NPC esta nos dois grupos do mesmo callback, o Combat ve o aggro
aplicado no estagio Peace anterior.

Na fase Peace, o runtime considera NPCs gerados nao-Clan-4 em `Peace`, planeja
com o seletor ja auditado e aplica somente decisoes `Aggro`. A aquisicao
passiva, o offset dos seis grupos, a visita de cada grupo e a ordem
Peace-before-Combat estao cobertos por teste de Engine; um mob no grupo
compartilhado entra em Combat na mesma callback.
Quando um leader gerado adquire alvo passivamente, a Task 5BH aplica `SetBattle`
nos followers gerados associados por `LeaderId`, se ativos, vivos e dentro do
retangulo inclusivo de +/-23 por eixo. TMProject 7.69 e W2PP fazem o mesmo para
os membros em `PartyList`; essa mudanca de estado nao emite frame por si so.
O loop posterior que chama `SetBattle` nos summons da party do alvo so roda
quando `Target < MAX_USER`; o caminho C# limita esse efeito ao alvo jogador.
Contra um alvo summon, os followers gerados recebem o alvo, mas a party do
summon nao. A remocao de follower morto e a limpeza de `PartyList` (`DeleteMob`)
nao fazem parte desta fatia.

No Combat, o ramo fisico comum continua restrito a NPC gerado Clan diferente de
4, com alvo explicito vivo dentro do alcance demonstrado. A oportunidade e
nominalmente 800 ms por NPC em combate e 1,2 s por NPC na fase Peace, sujeitas
ao tempo de trabalho do `PeriodicTimer`. O ramo fisico normal envia
`MSG_AttackOne` de 68 bytes; morte e ressurreicao usam os relays e frames
existentes, com recipients capturados pela view e erros de desconexao isolados.
Golpes fisicos e skills contra summon tambem alimentam as `EnemyList` dos
summons das parties do atacante e do alvo. Cada insercao aplica seu proprio
retangulo inclusivo de +/-23, preserva alvos anteriores e deduplica. No caminho
suportado que ataca um summon vivo, o tail tambem remove summons mortos das
duas parties com `RemoveMob` view-scoped antes do frame Attack. Os tails de
ataque fisico e de skill com dano suportado tambem limpam as parties quando o
tail da fonte e alcancado e o alvo continua vivo, seja jogador ou NPC.

Em 16/10 foi portada a fatia de chase comum. TMProject 7.69
(`CMob.cpp::GetTargetPos`, `Basedef.cpp::BASE_GetSpeed/BASE_GetRoute`,
`GetFunc.cpp::GetEmptyMobGrid/GetAction` e `ProcessSecMinTimer.cpp`) e W2PP
concordam nesse caminho generico: `AttackRun & 15 == 0` aponta diretamente
para `target-1`; no caminho de corrida, `BASE_GetSpeed(CurrentScore)` usa o
nibble baixo limitado a 1..6, calcula distancia com `+2` e `SECBATTLE=8`,
limita a rota a `MAX_ROUTE-1`, procura tile livre em raios 1..9 e recalcula a
rota com `BASE_GetRoute`. `GetAction` envia origem, destino, `Effect=0` e a
velocidade normal; o primeiro byte de `Route` e zero. Os corpos comuns de
`GetTargetPos` sao iguais nas duas referencias. C# usa
`LegacyMobAiMovementPlanner` e `LegacyActionRouteBuilder`, considera
participantes, summons e NPCs na ocupacao e revalida terreno, ocupacao, alvo e
snapshot dentro do gate de `WorldHub` antes de mudar a posicao.

A decisao aplicada captura as views 33x33 de origem e destino sob o mesmo gate.
Fora dele, `LegacyMobAiRelay` envia `Action` a view antiga, `RemoveMob` com
`RemoveType=0` a quem saiu e `CreateMob` 7.69 seguido de `Action` a quem entrou;
quem permaneceu na intersecao recebe um unico `Action` e quem ficou fora das
duas views nao recebe frame. O fallback de destino sem rota injeta dois valores
`rand()%5-3`; o restante do array `Route` do packet e zerado deterministicamente
no C#, pois a referencia so inicializa `Route[0]` nesse `GetAction`.

O chase comum roda fora de `RouteType=5`, Clan 4 e cabecas 219/220 cujo
`TargetX/Y` especial nao esta no snapshot atual. Em 16/10 foi portada tambem a
transicao de leash de Combat: TMProject 7.69 (`CMob.cpp::BattleProcessor`)
seleciona primeiro o alvo e passa pelo roll de INT; se a posicao ultrapassou
`SegmentX/Y +/-16`, limpa `CurrentTarget` e `EnemyList`, muda para Peace e chama
`GetNextPos(1)`. O retorno 16 chega a `ProcessSecMinTimer.cpp` e vira um `Action`
normal (`Effect=0`, velocidade de `BASE_GetSpeed`), apos nova busca de tile livre.

`GetNextPos(1)` usa `BASE_GetSpeed(CurrentScore)`, distancia
`speed*SECBATTLE/4` limitada a `MAX_ROUTE-1`, e reduz a distancia ate encontrar
rota/ocupacao validas com `BASE_GetRoute`. TMProject 7.69 inicia a busca em
`SegmentX/Y + rand()%5 - 2`; W2PP faz a mesma sequencia, mas inicia no segmento
exato. O port segue a 7.69: `LegacyMobAiMovementPlanner` recebe dois rolls
`0..4`, e `WorldHub` valida e aplica `ReturnToRoute` sob o gate, captura as views
de origem/destino e entrega o movimento a `LegacyMobAiRelay`. Se o nibble baixo
de `BaseScore.AttackRun` e zero, ou o NPC e uma cabeca 219/220 ou Kefra, a source
nao move nesse branch; o C# ainda aplica a limpeza/transicao Peace sem `Action`.

Task 5C cobre o reset de combate e o primeiro passo de retorno em Combat.
Task 5D, concluida em 16/10, integra o movimento Peace ate o segmento ativo e a
progressao de rota ao mesmo runtime de seis shards. `StandingByProcessor`
(`CMob.cpp:63`, `ProcessSecMinTimer.cpp:2017-2032` na TMProject; `CMob.cpp:80`,
`ProcessSecMinTimer.cpp:1933-1948` no W2PP) tenta primeiro a aquisicao passiva;
sem alvo, `GetNextPos(0)` caminha ao segmento e o timer envia Action normal.
`GetNextPos(0)` limita a distancia por `BaseScore.Strength` depois de calcular
`BASE_GetSpeed(CurrentScore)*SECBATTLE/4`; a 7.69 escolhe o ponto inicial com
`SegmentX/Y + rand()%5 - 2` (`CMob.cpp:1231-1270`), enquanto W2PP usa o centro
exato (`CMob.cpp:1189-1227`). O port segue TMProject 7.69, injeta dois rolls
`0..4`, respeita o limite de Strength, calcula o primeiro tile com
`BASE_GetRoute` e revalida terreno/ocupacao e o snapshot dentro do gate do
`WorldHub`. O relay reutiliza `Action` e as views capturadas de origem/destino.

O estagio Peace agora aplica aquisicao `Aggro`, decisoes de rota/espera e passos
de NPC gerado nao-Clan-4 fora de `RouteType=5`. Planner e runtime cobrem os
segmentos existentes; os limites de branches Unsupported permanecem. Clan 4,
seguidores RouteType 5, cabecas 219/220, Kefra e habilidades automatizadas
`SkillBar`/`Special` e efeitos continuam fora desta fatia.

TDD: o RED de Peace fechou 484/486: falhou porque o runtime anterior aplicava
somente Aggro e porque o planner descartava o destino quando nao recebia um
passo de rota. O fixture tambem fixa Strength em 1 para provar o limite de
distancia. GREEN: Engine.Tests passou 486/486, incluindo a caminhada Peace e o
relay. A build completa sem
incremental (`dotnet build WydCdk.Engine.slnx --no-restore --no-incremental
--nologo -v:minimal -m:1 -p:UseSharedCompilation=false`) terminou com 0 erros e
um aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`.
`dotnet --info` identificou SDK 10.0.401 e terminou com exit 1; mensagem:
`System.InvalidOperationException: Cannot open Service Control Manager on computer '.'. This operation might require other privileges. ---> System.ComponentModel.Win32Exception (5): Acesso negado.` O smoke loopback (`dotnet run --no-restore
--project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj -- --mob-ai-smoke`)
passou: `MOB_AI_SMOKE_PASS loopback=verified shards=explicit chase=relayed
return=relayed peace-route=relayed attack=positive death=relayed
resurrection=refresh view-scoped=true`. O harness usa WorldHub/runtime
sinteticos em loopback e cobre rota Type 0; nao exercita aquisicao passiva,
host autenticado ou cliente visual.

Task 5E, concluida em 16/10, integra o seguimento Peace de summons de party
`RouteType=5` no ciclo de seis shards. TMProject 7.69
(`CMob.cpp::StandingByProcessor`, `CMob.cpp:63-132`) valida Leader/Summoner,
vinculo da party e summoner ativo; a distancia >=13 devolve destino direto em
`NextX/Y`, entre 5 e 12 atualiza o segmento e chama `GetTargetPos`, e ate 4 fica
parado. `Server.cpp::GenerateSummon` atribui `RouteType=5`, Leader e Summoner ao
summon Type 11. W2PP (`W2PP/Source/Code/TMSrv/CMob.cpp:80-150`) tem a mesma
sequencia de limiares, mas o sub-ramo seguidor exige Affect 24 ou Face 315..345;
a TMProject tambem aceita `RouteType==5` diretamente. Os summons Type 11 deste
port recebem Affect 24, portanto esse caso tambem satisfaz a condicao W2PP.

O C# inicializa `RouteType=5` somente nos summons Type 11; summons capturados
permanecem com o default 0. `LegacyMobAiRuntime` atribui summons RouteType 5 a
um dos seis shards Peace pelo id relativo a `MAX_USER=1000`. `WorldHub` verifica
que o summon esta vivo, em Peace e com `EnemyList` vazia, que summoner e lider
estao ativos e que o vinculo de party continua valido. A lista vazia nao basta:
o fallback `TrySelectSummonTarget` grava o alvo em `summonTargets` sem preencher
`EnemyList`. Sob o mesmo gate, um alvo selecionado que ainda existe, esta vivo,
visivel e nao oculto impede o ramo Peace; alvo desconectado, morto, oculto ou
fora da janela de selecao e limpo para 0 antes de permitir o seguimento.

Para distancia >=13, aplica o destino do planner via `GetEmptyMobGrid`, com
busca ate raio 9 (`MAX_SERVER=10`), e relaya Action `Effect=1, Speed=2`, como
`DoTeleport`/`GetAction` da 7.69. Entre 5 e 12, calcula um passo com o planejador
existente e relaya Action com `Effect=0`; ate 4, nao altera estado. Movimento e
captura das views 33x33 de origem/destino ocorrem sob o gate. O relay envia Action
a viewers da origem, RemoveMob a quem saiu e CreateMob + Action a quem entrou. O
ramo Combat RouteType 5 >=20 continua fora desta integracao; o loop de ataque de
summons de 800 ms permanece separado.

TDD: a cobertura usa um summon real de `GenerateSummon` e caster que e membro,
nao lider, da party. O teste inicializou o caso sem movimento; apos a integracao,
o teste encontrou que o planner expressa o destino distante em `NextX/Y`, mas a
aplicacao lia somente `TargetX/Y`. A correcao usa os campos do tipo de decisao
retornado e mantem `TargetX/Y` para o passo intermediario. A revisao de codigo
encontrou dois casos adicionais: destino direto precisava preservar o teleport
`Effect=1`; e um alvo fallback em `summonTargets` com lista vazia precisava
suprimir o passo Peace. A comparacao de `DoTeleport` mostrou ainda `Speed=2` e
busca de tile livre ate raio 9; ambos foram fixados e cobertos com raio 1..4
bloqueado e destino livre em raio 5. O teste agora verifica alvo ativo e limpeza
apos desconexao, limiares 5..12/13/4, Action `Effect=0` na caminhada, teleport
`Effect=1, Speed=2` no destino direto, Action + RemoveMob para viewer exclusivo
da origem, CreateMob + Action para viewer exclusivo do destino e nenhum frame
para viewer fora das duas views. Engine.Tests passou 486/486.

Build sem incremental (`dotnet build WydCdk.Engine.slnx --no-restore
--no-incremental --nologo -v:minimal -m:1 -p:UseSharedCompilation=false`): 0 erros
e aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. O smoke
loopback passou com `MOB_AI_SMOKE_PASS loopback=verified shards=explicit
chase=relayed return=relayed peace-route=relayed attack=positive death=relayed
resurrection=refresh view-scoped=true`; esse harness cobre NPC RouteType 0 e nao
executa o seguidor Type 5. `dotnet --info` identificou SDK 10.0.401 e saiu com
exit 1: `System.InvalidOperationException: Cannot open Service Control Manager
on computer '.'. This operation might require other privileges. --->
System.ComponentModel.Win32Exception (5): Acesso negado.`

Task 5F, concluida em 16/10, integra o ramo Combat RouteType 5 para summon Type
11 no loop de batalha de 800 ms. TMProject 7.69 (`CMob.cpp::BattleProcessor`,
`CMob.cpp:216-259`) e W2PP (`CMob.cpp:233-276`) concordam: selecionam primeiro
o alvo; com alvo, validam Leader/Summoner e o vinculo da party; quando
`BASE_GetDistance(TargetX/Y, Summoner.TargetX/Y) >= 20`, definem `NextX/Y` para o
summoner e retornam `2`. `ProcessSecMinTimer.cpp` traduz esse bit em
`DoTeleport`. A implementacao 7.69 (`Server.cpp:10301-10327`) e W2PP
(`Server.cpp:9151-9174`) chamam `GetEmptyMobGrid`, depois `GetAction` e
`GridMulticast`; `GetAction` fixa `Speed=2` e `Effect=1` (7.69
`GetFunc.cpp:1534-1549`, W2PP `GetFunc.cpp:1365-1380`). A busca percorre raios
1 ate 9 porque o loop usa `z=TRUE; z<MAX_SERVER` com `MAX_SERVER=10` (7.69
`GetFunc.cpp:2231-2276`, W2PP `GetFunc.cpp:2027-2127`). Se nao acha tile, o
teleport nao ocorre e o ramo do `BattleProcessor` ainda nao executa ataque.

O C# aplica a oportunidade composta em `WorldHub.TryApplyAllSummonBattleOpportunities`:
seleciona o alvo antes de verificar o summon, percorre a lista do evocador (que
pode ser membro e nao lider), exige summon vivo, summoner vivo e vinculo com o
lider da party, e mede a distancia com a posicao autoritativa do WorldHub. Em
distancia >=20 tenta o tile livre ate raio 9, captura views de origem/destino
sob o gate e produz movimento `Effect=1, Speed=2`; a oportunidade e encerrada
mesmo se a busca falhar. Abaixo de 20 chama o ataque fisico existente. O Host
`RunSummonBattleLoopAsync` processa cada resultado na ordem capturada, relaya o
movimento com `LegacyMobAiRelay` e preserva o `NpcAttackConfirmation` atual para
ataques. Nenhum wire/opcode mudou.

TDD: a regressao inicial falhou porque o WorldHub nao oferecia a oportunidade
composta. A implementacao inicial do teste tambem mostrou que a lista pertence
ao evocador membro da party, nao apenas ao lider; a iteracao foi alinhada ao
loop anterior. Engine.Tests cobre distancia 19 (ataque), 20 (teleport sem
ataque), ataque na oportunidade seguinte quando o alvo continua selecionavel,
raio 9 totalmente bloqueado (sem movimento nem ataque), e frames `Action`,
`RemoveMob` na view de origem e `CreateMob`, `Action` na view de destino, sem
frames a viewer externo. O smoke TCP loopback agora inclui `route5-combat` e
verifica frames nos dois peers. Engine.Tests 486/486; build completa sem
incremental: 0 erros e aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; smoke:
`MOB_AI_SMOKE_PASS loopback=verified shards=explicit chase=relayed return=relayed
peace-route=relayed route5-combat=relayed attack=positive death=relayed
resurrection=refresh view-scoped=true`. O smoke usa WorldHub sintetico e TCP
loopback, sem Host autenticado ou cliente visual. `dotnet --info` identificou
SDK 10.0.401 e saiu com exit 1 por acesso negado ao Service Control Manager,
conforme mensagem registrada acima.

Task 5G, concluida em 16/10, porta a limpeza do `PartyList` no caminho
suportado de ataque a summon vivo. TMProject 7.69
(`_MSG_Attack.cpp:2277-2339`) e W2PP
(`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1674-1721`) percorrem, depois do
tail de dano, as parties do atacante e do alvo. Para cada ID de summon acima
de `MAX_USER` cujo MOB esta vazio ou com HP <= 0, chamam
`DeleteMob(summon, 1)` quando a estrutura ainda existe e liberam o slot antes
de relayar o Attack. `DeleteMob` envia RemoveMob a partir da view do tile do
summon (TMProject `Server.cpp:8841-8905`; W2PP `Server.cpp:7809`).

No C#, `WorldHub` faz a varredura e remove o estado morto sob seu gate,
capturando recipients da view 33x33 antes da mutacao. O resultado carrega a
ordem/slot e os recipients imutaveis para fora do gate. O Host envia
`RemoveMob(type=1)` ao atacante e aos demais viewers capturados antes do frame
Attack, isolando falhas individuais de conexao e sem broadcast global. Na Task
5G, a integracao cobria os caminhos fisico e skill suportados com summon viva;
a Task 5H abaixo estende o tail a jogador/NPC. A paridade geral do `PartyList`
permanece parcial.

RED falhou porque a summon morta continuava registrada. GREEN: Engine.Tests
487/487. O smoke
(`dotnet run --project tests/WydCdk.Client.E2E/WydCdk.Client.E2E.csproj --no-restore -- --mob-ai-smoke`)
passou com tres peers TCP loopback e WorldHub sintetico: o atacante e viewer de
borda recebem RemoveMob(type=1) antes do Attack, e o viewer fora da view nao
recebe frame. Marker:
`MOB_AI_SMOKE_PASS loopback=verified shards=explicit chase=relayed return=relayed peace-route=relayed route5-combat=relayed summon-cleanup=relayed attack=positive death=relayed resurrection=refresh view-scoped=true`.
Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401
e saiu com exit 1 por acesso negado ao Service Control Manager.

Limites ainda abertos: limpeza em branches de suporte/retorno antecipado,
outras habilidades `SkillBar`/`Special` automatizadas e seus efeitos, Host
autenticado, ramos Clan 4 fora deste Type 11, cabecas 219/220, Kefra e gameplay
E2E visual. A aquisicao passiva ja tem smoke de rede na Task 5I, e o bar Fire
Carbunkle tem smoke na Task 5J. `MOB-AI-001` permanece aberto e parcial.

Task 5H, concluida em 16/10, estende a mesma limpeza aos tails suportados de
ataque fisico e skill contra jogador ou NPC. TMProject 7.69 e W2PP percorrem as
parties do atacante e do alvo no tail de dano; o port remove summons vazios ou
mortos, libera o slot e captura a view do tile antes do relay. Em NPC, a coleta
e a remocao precedem `TrySetNpcBattle`, cuja propagacao interna podia descartar
summons mortos antes da captura dos viewers. O Host reutiliza o relay
view-scoped de Task 5G, enviando `RemoveMob(type=1)` antes de Attack, sem
broadcast global. Ramos de suporte, gates com retorno antecipado e branches em
que o tail de dano nao e alcançado permanecem fora desta limpeza.

RED identificou ausencia de cleanup nos tails de alvo jogador; GREEN:
Engine.Tests 488/488, cobrindo fisico e skill contra jogador e NPC, slots,
estado do mundo e ordem de relay no resultado. Build completa sem incremental:
0 erros e o CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. O smoke loopback de MOB-AI passou com o
marker `MOB_AI_SMOKE_PASS ... summon-cleanup=relayed ... view-scoped=true`;
ele cobre o ataque contra summon do Task 5G, enquanto os quatro novos caminhos
estao cobertos pelos testes de Engine, nao por esse smoke TCP. A Task 5I abaixo
acrescenta a aquisicao passiva ao harness de rede. SDK
10.0.401; `dotnet --info` saiu com codigo 1 por acesso negado ao Service Control
Manager. Sem Host autenticado, E2E visual, deploy, commit ou push.

Task 5I, concluida em 16/10, valida a aquisicao passiva no runtime e no relay
TCP. O fixture nao semeia alvo nem Combat: um Peace NPC detecta um jogador hostil
visivel num tile adjacente, grava o alvo na `EnemyList`, e o shard Combat emite
AttackOne positivo e refresh HP/MP para a view capturada do alvo. O segundo peer
fica fora dessa view e recebe zero frames. O teste fornece ItemList sintetico
com arma de alcance 1 para que a distancia adjacente alcance o ramo fisico; nao
carrega dados de conta ou endpoints externos.

O smoke passou com
`MOB_AI_SMOKE_PASS loopback=verified shards=explicit chase=relayed return=relayed peace-route=relayed passive-aggro=relayed route5-combat=relayed summon-cleanup=relayed attack=positive death=relayed resurrection=refresh view-scoped=true`.
Build da solution sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; Engine.Tests 488/488. Este e um harness
WorldHub/runtime sintetico em TCP loopback, nao valida Host autenticado nem
E2E visual com `WYD.exe`.

Auditoria inicial das habilidades automatizadas (16/10; sem port): em
TMProject `Servidor/Source/Code/TMSrv/GetFunc.cpp:1569` e W2PP
`W2PP/Source/Code/TMSrv/GetFunc.cpp:1401`, 7.69 seleciona o par ranged quando
a distancia e >=2, enquanto W2PP exige >=3. Para Clan 4, 7.69 le
`BaseScore.Special[2/3]` ou `[0/1]`; W2PP le `SkillBar` nos mesmos slots. Ambos
convertem o valor principal 121..128 em `SkillIndex`/`SkillParm` e usam o valor
secundario para escolher `Motion`.

`GetAttack` entao faz outro sorteio: slots brutos 0/1/2 cobrem faixas rand
0..49, 50..84 e 85..99; a selecao substitui `SkillIndex` pelo ID bruto e define
`Resist = InstanceType - 2`. O slot 3 tem branch adicional em rand 25..64:
quando seu InstanceType e 6 e HP do NPC/lider <=8 decimos, a funcao troca alvo
e dano e retorna. `ProcessSecMinTimer.cpp` tambem aplica `SetAffect`/`SetTick`
para skills validas com `SkillParm=0`. Portanto, mapear somente 121..128 nao
reproduz o ataque automatizado. A auditoria inicial deixou SkillBar nao vazia
como Unsupported ate encontrar um caso deterministico com selecao, dano e relay.

Task 5J, concluida em 16/10, porta um bar automatizado especifico comprovado por
template. Os arquivos de 816 bytes `Carbunkle`, `Carbunkle@@`, `Carbunkle_`,
`Carbunkle_Anct`, `Grande_Carb` e `Rei_Carbuncle` em `run/npc` usam
`SkillBar=[32,32,32,255]`; os seis Clans lidos sao 1 ou 2. `NPCGener.txt`
gera `Carbunkle` em RouteType 2 (entrada 1030, linhas 29281-29288). A linha 32
de `SkillData.csv` define `InstanceType=2` (`Ataque_de_Fogo`).

Em TMProject 7.69 (`GetFunc.cpp::GetAttack`, linha 1569) e W2PP
(`GetFunc.cpp::GetAttack`, linha 1401), para esse bar todos os sorteios 0..2
escolhem o ID 32 e `Resist=InstanceType-2=0`; os pares melee/ranged mantem
`SkillIndex=32`, `SkillParm=1` e `Motion=4`. O limiar ranged geral diverge
(7.69 >=2, W2PP >=3), mas essa configuracao e invariante nas duas selecoes.
Slot 3 fica vazio; `SkillParm=1` nao entra no ramo de `SetAffect`/`SetTick`
(`ProcessSecMinTimer.cpp:2218` em 7.69 e `:2186` no W2PP). GetAttack nao pede
MP (`CurrentMp/ReqMp=-1`) e aplica apos `BASE_GetDamage` a formula fonte
`(200 - target.Resist[0]) * damage / 100`.

O planner suporta somente esse padrao exato em nao-Clan-4 gerado, alem do bar
all-255 ja portado; qualquer outro bar e Clan 4 continuam Unsupported. O C# leva
os campos selecionados ate o frame `MSG_AttackOne` de 68 bytes e aplica Resist[0]
antes de reflexo, friendly fire e demais gates. RED: Engine.Tests 487/488; GREEN:
488/488. Build completa sem incremental: zero erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build E2E 0 avisos/erros. Smoke TCP sintetico
passou com dano 1477 no vetor Resist=50/fator 99, `SkillIndex=32`, `SkillParm=1`,
`Motion=4`, refresh HP/MP e nenhum frame para viewer fora da view:
`MOB_AI_SMOKE_PASS ... skillbar-type2=relayed ... view-scoped=true`. SDK
10.0.401; `dotnet --info` terminou com exit 0. Sem Host autenticado, E2E visual,
deploy, commit ou push.

Task 5K, concluida em 16/10, adiciona um segundo bar automatizado fixo e
estritamente limitado. A varredura dos 665 templates de 816 bytes em `run/npc`
encontrou `[52,52,52,255]` em `Lich_Batama`, `Lich_Crunt`, `Lich_Infernal`,
`Lich_Inferno` e `Verid`, todos Clan 1. `NPCGener.txt` referencia quatro deles
(`Lich_Batama`, `Lich_Crunt`, `Lich_Inferno` e `Verid`); `Lich_Infernal` nao tem
entrada de geracao nesse arquivo. A linha 52 de `skilldata.csv` identifica
`Furia_de_Gaia`, InstanceType 4.

Em `GetFunc.cpp::GetAttack`, a 7.69 usa o par ranged em distancia >=2 e W2PP
em >=3; como os tres slots de ataque sao 52, ambos selecionam sempre
`SkillIndex=52`, `SkillParm=1`, `Motion=4` e `Resist=InstanceType-2=2`. A formula
e `(200 - target.Resist[2]) * damage / 100`. O slot 3 e 255, e o branch do timer
que cura o mob/lider nao se aplica; `ProcessSecMinTimer` so chama SetAffect/SetTick
quando `SkillParm=0`. `CurrentMp/ReqMp` permanecem -1. O W2PP usa a mesma logica
de `InstanceType-2` e do parametro, mas nao contem runtime `SkillData.csv`; o
significado da linha 52 foi confirmado pela tabela primaria 7.69, nao por uma
tabela W2PP independente.

Na Task 5K, o C# passou a admitir somente `[52,52,52,255]` ou o bar Carbunkle `[32,32,32,255]`
no ramo elemental de NPC gerado nao-Clan-4, alem do all-255 fisico; barras mistas,
Clan 4 e efeitos nao auditados seguem Unsupported. RED: o teste de planner falhou
como esperado; a execucao RED completa teve `tests=488 passed=484 failed=4`;
falharam os novos casos de planner/WorldHub e dois outros testes, cujos nomes e
causas nao ficaram registrados na saida capturada. GREEN final apos build:
Engine.Tests `488/488`. Build completa sem incremental: 0 erros
e CS8604 preexistente; build E2E 0 avisos/0 erros. O smoke TCP sintetico passou
com Resist[2]=50, dano 1477, AttackOne `SkillIndex=52 / SkillParm=1 / Motion=4`,
refresh HP/MP e nenhum frame para viewer externo:
`MOB_AI_SMOKE_PASS ... skillbar-type4=relayed ... view-scoped=true`.
`dotnet --info` identificou SDK 10.0.401 e saiu com codigo 1 por acesso negado ao
Service Control Manager.

O publish self-contained win-x64 esta local em
`Backup/Build/MOB-AI-001-Task5K-20261016`; inventario 200 arquivos/83.973.835
bytes, sem nomes de segredo/chave. ZIP SHA-256
`E5CF0C413D6E722553C3E684B4C00B4E347E7D1716BB37E0F12548CF149BCF0C`.
Revalidacao do alias SSH `wyd-vps` retornou exit 255 (host inacessivel); nenhum
arquivo foi transferido e nenhum servico UP/PVP foi reiniciado ou verificado.
Sem Host autenticado, E2E visual, deploy remoto, commit ou push.

Task 5L (16/10) adiciona somente o bar fixo `[88,88,88,255]` no planner de NPC
gerado nao-Clan-4. A varredura dos 665 templates 7.69 de 816 bytes encontrou o
padrao em `HorizonCropper` e `Horizon_Cropper`, ambos Clan 1 e referenciados em
`NPCGener.txt`. SkillData row 88 identifica `Lamina_das_Sombras`, InstanceType 1.

TMProject 7.69 `GetFunc.cpp::GetAttack` usa `SkillBar` para nao-Clan-4 e W2PP
usa o mesmo campo. Os tres slots de ataque sao 88, entao a diferenca de alcance
ranged (7.69 >=2; W2PP >=3) nao altera a selecao. O 7.69 usa
`SkillIndex=88` / `SkillParm=1` e Motion 4 nas duas distancias: no par ranged
`special2=255` e nao-zero, portanto `if (special2)` restaura Motion 4. W2PP usa
a mesma condicao. Row 88 produz
`Resist=InstanceType-2=-1`, fora da faixa elemental 0..3, portanto o dano e
fisico sem modificador. O slot 3 e 255, portanto nao aciona
o branch de cura `InstanceType=6`. `ProcessSecMinTimer` somente aplica
SetAffect/SetTick quando `SkillParm=0`; o caminho 88 usa 1 e nao aplica esse
efeito. Nenhuma condicao especifica para SkillIndex 88 foi encontrada no timer
ou no handler de ataque das duas sources. W2PP nao contem `SkillData.csv`, assim
o significado do row 88 e autoridade da tabela primaria 7.69.

Referencias: TMProject `GetFunc.cpp:1569-1816` e
`ProcessSecMinTimer.cpp:2218-2239`; W2PP `GetFunc.cpp:1401-1635` e
`ProcessSecMinTimer.cpp:2186-2207`. A varredura considerou os templates 816-byte,
com `SkillBar[4]` no offset 796 e `Clan` no offset 16.

TDD RED: Engine.Tests `488/486/2`, com falhas somente nos novos testes do planner
e WorldHub. GREEN: `488/488`; o WorldHub aplica Resist[0] antes do tail comum. O
smoke TCP loopback usa runtime/estado sinteticos e validou Resist[0]=50, dano
1477, `AttackOne` de 68 bytes com `SkillIndex=88 / SkillParm=1 / Motion=4`,
refresh HP/MP e relay somente para o alvo visivel:
`MOB_AI_SMOKE_PASS ... skillbar-shadowblade=relayed ... view-scoped=true`.
Isso nao cobre Host autenticado nem `WYD.exe` visual. Build completa sem
incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `dotnet --info` identificou SDK 10.0.401,
mas saiu 1 por acesso negado ao Service Control Manager.

Publish self-contained win-x64 local em
`Backup/Build/MOB-AI-001-Task5L-20261016`, 200 arquivos/83.974.351 bytes e zero
nomes suspeitos de segredo/chave. ZIP SHA-256
`9A5581CF4B4EEAA36EBBC111E026D6AC198416E4B35C301B79EB107B6A9262DF`. A
conexao de teste `ssh -F C:\Users\le1ec\.ssh\config wyd-vps hostname` recebeu
`Permission denied` ao conectar na porta TCP/22; nao houve transferencia ou
restart/check UP/PVP. Sem deploy remoto, commit ou push.

### MOB-AI-001 Task 5M - SkillBar fixo Relampago (16/10/2026)

A auditoria dos 676 templates 7.69 de 816 bytes encontrou
`[33,33,33,255]` em `Hier-Carbunkle` (Clan 2), `Hier-Carbunkle_` (Clan 1) e
`Servo_Carbuncle` (Clan 2); os tres aparecem em `NPCGener.txt`. SkillData row
33 e Relampago, InstanceType 5 (`Resist[3]`). TMProject 7.69
(`GetFunc.cpp:1569-1816`) e W2PP (`GetFunc.cpp:1401-1635`) selecionam SkillIndex
33 / SkillParm 1 / Resist[3]. Motion e 4 em ambas as fontes e distancias:
quando o par ranged seleciona `special2=255`, `if (special2)` tambem restaura
Motion 4. Os tres slots iguais preservam SkillIndex e Resist apesar do limiar
ranged distinto (7.69 >=2; W2PP >=3). O slot 3 e 255; SkillParm 1 nao
aciona SetAffect/SetTick em `ProcessSecMinTimer` (7.69 `2218-2239`, W2PP
`2186-2207`). W2PP nao contem a tabela runtime SkillData; o significado do row
33 vem da referencia primaria 7.69.

TDD RED falhou apenas nos novos casos do planner e WorldHub; GREEN:
Engine.Tests `488/488`. Build completa sem incremental: 0 erros e CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; build E2E 0 avisos/erros.
Smoke TCP loopback validou Resist[3]=50, dano 1477, AttackOne de 68 bytes com
SkillIndex 33/SkillParm 1/Motion 4, refresh HP/MP e relay somente ao viewer na
view: `skillbar-lightning=relayed`. Nao valida Host autenticado nem WYD.exe
visual. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager.

Publish self-contained win-x64 serial em
`Backup/Build/MOB-AI-001-Task5M-20261016`, 200 arquivos/83.974.359 bytes, sem
nomes de credenciais. ZIP SHA-256
`0A9D8694585C25892A44BAC21726AB64627F33F4AC8A24BB25F0BE4384893F1B`. SSH
`wyd-vps` foi negado no sandbox e expirou na repeticao elevada; sem transferencia,
restart ou verificacao UP/PVP. Sem deploy, commit ou push.

### MOB-AI-001 Task 5N - SkillBar Relampago com quarto slot repetido (16/10/2026)

Dois templates 7.69 de 816 bytes usam `[33,33,33,33]`: `Conj_Caveira`
(Clan 1) e `Conj_Caveira_` (Clan 2), ambos referenciados em `NPCGener.txt`.
SkillData row 33 e Relampago, `InstanceType=5` (`Resist[3]`). TMProject 7.69
(`GetFunc.cpp:1569-1816`) e W2PP (`GetFunc.cpp:1401-1635`) sorteiam entre os
tres primeiros slots; com eles iguais, a divergencia do limiar ranged (`>=2`
vs `>=3`) nao altera SkillIndex nem Resist. Motion permanece 4 em todas as
distancias: o par ranged usa `special2=255`, que e nao-zero em `if (special2)`.
O ramo do quarto slot nos dois `GetAttack`
so seleciona cura se `InstanceType==6`; com row 33, mesmo HP proprio/lider
baixo e o roll elegivel preservam SkillIndex 33 / SkillParm 1 e Resist[3]. O
Motion e 4 nos quatro slots repetidos. O timer de ambos so aplica affects/ticks
com SkillParm 0. W2PP nao
contem SkillData runtime, portanto o significado do ID 33 vem da tabela 7.69.

O planner C# admite a quarta repeticao somente para Lightning; barras repetidas
nao auditadas como `[32,32,32,32]`, Clan 4 e outros layouts continuam
`Unsupported`. RED: Engine.Tests `488 passed=486 failed=2`, apenas planner e
aplicacao WorldHub do novo padrao; GREEN `488/488`. Build completa sem
incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build E2E 0 avisos/erros. Smoke TCP
loopback executou `[33,33,33,33]`, Resist[3]=50, dano 1477, AttackOne de 68
bytes com SkillIndex 33/SkillParm 1/Motion 4, refresh HP/MP e nenhum frame no
observer externo: `skillbar-lightning-slot4=relayed`. Continua sem validar Host
autenticado ou WYD.exe visual. `dotnet --info` identificou SDK 10.0.401 e saiu
1 por acesso negado ao Service Control Manager.

Publish self-contained win-x64 em
`Backup/Build/MOB-AI-001-Task5N-20261016`, 200 arquivos/83.974.359 bytes; ZIP
SHA-256 `FC2B6D83F2329A02B0FF5800514EDEE7404B2154853DFBDA77C23F38DF098D03`.
SSH `wyd-vps hostname` foi negado no sandbox e expirou na tentativa elevada;
nao houve transferencia, restart ou verificacao UP/PVP. Sem deploy remoto,
commit ou push.

### MOB-AI-001 Task 5O - SkillBar fixo Fenix de Fogo (17/10/2026)

Auditoria da source TMProject 7.69 (commit
`3ccc4d3377a1226648d58d59f87d7a020cabc8a1`): em
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc`,
seis templates de NPC com 816 bytes usam `[38,38,38,38]` — `ElfoNegroAbj`, `Elfo_ABJ__`, `Elfo_Negro@`,
`Elfo_Negro_Abj`, `Elfo_Negro_Abj_` e `Mestre_ABJ`; todos Clan 1 e referenciados
em `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/NPCGener.txt`.
O bar fica no offset 796 e Clan no 16. A tabela
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/skilldata.csv`
row 38 identifica `Fenix_de_Fogo`, `InstanceType=2`. Em
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/GetFunc.cpp::GetAttack`, TMProject seleciona o par
dos slots 2/3 a distancia >=2; W2PP `Server/W2PP/Source/Code/TMSrv/GetFunc.cpp`
usa >=3. Como os quatro slots sao 38, o par ranged e o sorteio entre slots 0..2
mantem SkillIndex 38, SkillParm 1, Motion 4 e Resist[0] nos dois caminhos. O
branch de baixo HP no slot 3 exige InstanceType 6, portanto row 38 nao cura; o
timer so aplica SetAffect/SetTick com SkillParm 0. W2PP nao inclui a tabela
runtime SkillData.csv, entao a semantica da row 38 vem da tabela 7.69.

RED: Engine.Tests `488 passed=486 failed=2`, somente os novos casos de planner e
WorldHub para o padrao ainda nao suportado. GREEN: `488/488`. O planner C# aceita
somente o padrao exato nos NPCs nao-Clan-4; layout misto, quarta posicao 255 e
Clan 4 continuam Unsupported. O smoke TCP sintetico em tres peers loopback
validou Resist[0]=50, dano 1477, AttackOne de 68 bytes com SkillIndex 38 /
SkillParm 1 / Motion 4, refresh HP/MP e ausencia de frames fora da view:
`skillbar-phoenix=relayed`. O dano de dominio no teste WorldHub e 1492. Nao e
Host autenticado nem E2E visual com WYD.exe.

Validacao final: Engine.Tests 488/488; build completa da solution sem incremental
com 0 erros e o CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build E2E 0 avisos/erros; `dotnet --info`
identificou SDK 10.0.401 e saiu 0. Publish self-contained win-x64 em
`Backup/Build/MOB-AI-001-Task5O-20261017`, 200 arquivos/83.974.407 bytes, sem
nomes de credenciais; ZIP SHA-256
`DDC7483E66A7F4BB3D44A84CDA1909D566FB9F4DD26E339752CBC6E9DF43DC98`. SSH
`wyd-vps hostname` expirou na conexao TCP/22, inclusive na tentativa elevada
somente de leitura; sem transferencia, restart ou verificacao UP/PVP. Sem deploy,
commit ou push.

### MOB-AI-001 Task 5P - SkillBar fixo repetido Nevoa Venenosa (17/10/2026)

Auditoria TMProject 7.69 (commit
`3ccc4d3377a1226648d58d59f87d7a020cabc8a1`): os templates
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc/Arvak`
e
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc/Treant_Amald`
medem 816 bytes, Clan 1, SkillBar `[40,40,40,40]`
(offset 796; Clan offset 16), e ambos sao referenciados em
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/NPCGener.txt`.
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/skilldata.csv`
row 40 e `Névoa_Venenosa`, `InstanceType=4`.

Em
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/GetFunc.cpp::GetAttack`, abaixo de distancia 2
`special/special2` vem de SkillBar[0]/[1]; em >=2 vem de [2]/[3]. W2PP
`W2PP/Source/Code/TMSrv/GetFunc.cpp` muda o limiar para >=3. Os quatro valores
iguais tornam a selecao do par invariavel nas duas fontes e no trecho entre os
limiares. O switch de `special=40` usa o default, emitindo SkillIndex 40 e
SkillParm 1; `special2=40` tambem cai no default e deixa Motion 4 / SkillParm 1.
Os casos 125..127 sao valores especiais distintos que mapeiam para SkillIndex
40 com parms 1/2/3; o bar literal 40 nao usa esse mapeamento. O sorteio posterior
entre slots 0..2 sempre seleciona 40 e calcula Resist=`InstanceType-2=2`.
O branch do quarto slot so cura para InstanceType 6, e row 40 e 4. Ambos os
`ProcessSecMinTimer.cpp` aplicam SetAffect/SetTick apenas com SkillParm 0; este
caminho usa 1. W2PP nao inclui SkillData runtime, entao a row vem da tabela 7.69.

RED Engine.Tests `488 passed=486 failed=2` (Nevoa_Venenosa);
GREEN `488/488`. O planner aceita somente `[40,40,40,40]` nao-Clan-4; a barra
incompleta `[40,40,40,255]`, layout misto e Clan 4 continuam Unsupported. O
WorldHub validou Resist[2]=50 e dano 1492. O smoke TCP sintetico de tres peers
loopback validou dano 1477, AttackOne de 68 bytes com SkillIndex 40 / SkillParm
1 / Motion 4, HP/MP refresh, view-scoping e
`skillbar-poison-mist=relayed`. Nao e Host autenticado nem E2E visual WYD.exe.

Validacao: Engine.Tests 488/488; build completa sem incremental com 0 erros e
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; E2E incluida na
solution build; smoke passou. `dotnet --info` SDK 10.0.401, exit 0. Publish
self-contained win-x64 local em `Backup/Build/MOB-AI-001-Task5P-20261017`, 200
arquivos/83.974.415 bytes, zero nomes suspeitos de segredo; ZIP SHA-256
`AEDA3589C5A203299E76D16DA400B7DF58C15E450DA071A7C0CC39E1ACB78F1A`. SSH
`wyd-vps hostname` foi negado no sandbox e expirou na tentativa read-only elevada;
nao houve transferencia, restart ou verificacao UP/PVP. Sem deploy, commit ou push.

### MOB-AI-001 Task 5Q - SkillBar repetido de Ataque de Fogo (17/10/2026)

Auditoria TMProject 7.69 (commit
`3ccc4d3377a1226648d58d59f87d7a020cabc8a1`): os templates
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc/Golem_Inferno`
e `.../npc/Lagarto_Inferno` medem 816 bytes, Clan 1, SkillBar
`[32,32,32,32]` (offset 796; Clan offset 16), e ambos aparecem como Leader e
Follower em `NPCGener.txt`. SkillData row 32 e `Ataque_de_Fogo`,
`InstanceType=2`.

TMProject `GetFunc.cpp::GetAttack` escolhe SkillBar[0]/[1] abaixo da distancia 2
e SkillBar[2]/[3] a partir de 2; W2PP usa distancia >=3 para o par ranged. Os
quatro valores iguais mantem `special/special2=32` nos dois fontes. O default
seleciona SkillIndex 32, SkillParm 1 e Motion 4; o sorteio entre slots 0..2
tambem seleciona 32 e aplica Resist=`InstanceType-2=0`. O quarto slot so cura
com InstanceType 6, portanto row 32 nao cura em HP baixo. `ProcessSecMinTimer`
so aplica SetAffect/SetTick com SkillParm 0; este caminho usa 1. W2PP nao inclui
SkillData runtime, entao a row vem da tabela 7.69.

O planner agora permite somente `[32,32,32,32]` nao-Clan-4 alem do bar
Carbunkle `[32,32,32,255]` ja portado. Testes mantem layouts mistos e Clan 4
como Unsupported. RED Engine.Tests `488 passed=486 failed=2` (planner e WorldHub);
RED smoke falhou no ataque repetido esperado. GREEN Engine.Tests `488/488`; o
WorldHub validou Resist[0]=50 e dano 1492. O smoke TCP sintetico de tres peers
loopback validou dano 1477, AttackOne de 68 bytes com SkillIndex 32 / SkillParm
1 / Motion 4, HP/MP refresh, view-scoping e `skillbar-fire-slot4=relayed`. Nao e
Host autenticado nem E2E visual WYD.exe.

Validacao: build completa sem incremental com 0 erros e um CS8604 preexistente
em `AttackDivineBlessingTests.cs:256`; Engine.Tests 488/488; E2E build incluida
na solution e `--mob-ai-smoke` passou. `dotnet --info` identificou SDK 10.0.401,
mas saiu 1 por acesso negado ao Service Control Manager. Publish self-contained
win-x64 local em `Backup/Build/MOB-AI-001-Task5Q-20261017`, 200 arquivos e
83.974.415 bytes; zero nomes suspeitos de credencial; ZIP SHA-256
`775A2DD9FB189CAF07171BCA7DCF36509F2094C39EC1B8BFE0ED7F0010058BD1`. Probe
SSH read-only `wyd-vps hostname` expirou em TCP/22; sem transferencia, restart,
verificacao UP/PVP ou sincronizacao de source, documentos e segredos. Sem Site,
producao, commit ou push.

### MOB-AI-001 Task 5R - SkillBar repetido Inferno (17/10/2026)

Auditoria TMProject 7.69 (commit
`3ccc4d3377a1226648d58d59f87d7a020cabc8a1`): o template
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc/Imp_Inferno`
mede 816 bytes, Clan 1, SkillBar `[39,39,39,39]` (offset 796; Clan offset 16)
e aparece como Leader/Follower ativo em `NPCGener.txt`. SkillData row 39 e
`Inferno`, `InstanceType=2`.

TMProject `GetFunc.cpp::GetAttack` usa SkillBar[0]/[1] abaixo da distancia 2 e
SkillBar[2]/[3] a partir de 2; W2PP usa o par ranged a partir de 3. Os quatro
valores iguais mantem `special/special2=39` em ambas as faixas e fontes. O
default resulta em SkillIndex 39, SkillParm 1 e Motion 4; o sorteio nos slots
0..2 tambem seleciona 39 e aplica Resist=`InstanceType-2=0`. O branch de cura
do slot 3 exige InstanceType 6, portanto row 39 nao cura com HP baixo.
`ProcessSecMinTimer` aplica SetAffect/SetTick somente com SkillParm 0; este
ataque permanece com 1. W2PP nao inclui SkillData runtime, entao a row vem da
tabela primaria 7.69.

O planner permite somente `[39,39,39,39]` nao-Clan-4 para este layout. Testes
mantem barra incompleta, mista e Clan 4 como Unsupported. RED Engine.Tests
`488 passed=486 failed=2` (planner e WorldHub); RED smoke falhou no novo ataque.
GREEN Engine.Tests `488/488`; WorldHub validou Resist[0]=50 e dano 1492. O smoke
TCP sintetico de tres peers loopback validou dano 1477, AttackOne de 68 bytes
com SkillIndex 39 / SkillParm 1 / Motion 4, HP/MP refresh, view-scoping e
`skillbar-inferno=relayed`. Nao e Host autenticado nem E2E visual WYD.exe.

Validacao: build completa sem incremental com 0 erros e um CS8604 preexistente
em `AttackDivineBlessingTests.cs:256`; Engine.Tests 488/488; `--mob-ai-smoke`
passou. `dotnet --info` identificou SDK 10.0.401, mas saiu 1 por acesso negado
ao Service Control Manager. Publish self-contained win-x64 local em
`Backup/Build/MOB-AI-001-Task5R-20261017`, 200 arquivos e 83.974.419 bytes;
ZIP de 37.044.143 bytes, SHA-256
`4CED79AD3EE63F992DB37FA458ED2BA097685172998952B1F500254E8F0EA66C`. Probe SSH
read-only `wyd-vps hostname` expirou em TCP/22; sem transferencia, restart ou
verificacao UP/PVP. Sem deploy, Site, producao, commit ou push.

### MOB-AI-001 Task 5S - SkillBar Fera Flamejante (17/10/2026)

Auditoria TMProject 7.69 (commit
`3ccc4d3377a1226648d58d59f87d7a020cabc8a1`): cinco templates ativos de 816 bytes
em `NPCGener.txt` usam `[48,48,48,51]`: `Orc_Mago`, `Orc_Sha_Trooper`,
`Orc_Shama@@`, `Orc_Shaman` e `Orc_Shaman_` (Clan 1/2; SkillBar offset 796,
Clan offset 16). SkillData row 48 e `Fera_Flamejante`, InstanceType 2;
row 51 e `Enfraquecer`, InstanceType 0.

Em `GetFunc.cpp::GetAttack`, a 7.69 seleciona o par SkillBar[0]/[1] abaixo de
distancia 2 e SkillBar[2]/[3] a partir de 2; W2PP troca para o par ranged a
partir de 3. O layout resulta em `special/special2` 48/48 no par proximo e
48/51 no ranged. Em ambos, o default fixa SkillIndex 48, SkillParm 1 e Motion
4; o sorteio dos slots 0..2 tambem seleciona 48 e aplica Resist=`2-2=0`.
Apesar de o quarto slot ser 51, o branch de cura exige InstanceType 6 e row 51
tem InstanceType 0. `ProcessSecMinTimer` aplica SetAffect/SetTick apenas com
SkillParm 0; este caminho permanece com 1. W2PP nao contem SkillData runtime, por
isso as rows vem da tabela primaria 7.69.

O planner libera somente o layout exato em mob nao-Clan-4. RED Engine.Tests
`488 passed=486 failed=2` (planner e WorldHub) e RED smoke falhou no novo ataque.
GREEN Engine.Tests `488/488`; WorldHub validou Resist[0]=50 e dano 1492. O smoke
TCP sintetico de tres peers loopback validou dano 1477, AttackOne de 68 bytes
com SkillIndex 48 / SkillParm 1 / Motion 4, refresh HP/MP, view-scoping e
`skillbar-fera-flamejante=relayed`. Nao e Host autenticado nem E2E visual WYD.exe.

Validacao: build completa sem incremental, 0 erros e um CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; Engine.Tests 488/488; `--mob-ai-smoke`
passou. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Sem package/publish, deploy, Site, producao, commit ou
push.

### MOB-AI-001 Task 5T - SkillBar repetido Exterminar (17/10/2026)

O template ativo `KalintzAmald_` mede 816 bytes, tem Clan 1 no offset 16 e
`SkillBar=[22,22,22,22]` no offset 796. `NPCGener.txt` usa esse template em
cinco grupos de spawn. SkillData row 22 define Exterminar com InstanceType 2.
No `GetFunc.cpp::GetAttack` da TMProject 7.69, ambos os pares de distancia
selecionam 22; o default resulta em SkillIndex 22 / SkillParm 1 / Motion 4. A
selecao ranged do 7.69 comeca em distancia >=2 e a do W2PP em >=3, mas o bar
repetido deixa `special/special2` em 22/22 nos dois casos. O calculo
`InstanceType - 2` produz `Resist[0]`. Slot 3 nao cura, pois isso exige
InstanceType 6; `ProcessSecMinTimer` tambem so aplica SetAffect/SetTick quando
SkillParm e zero.

As fontes divergem no miss/parry do ataque de NPC: TMProject 7.69 compara o
roll com `GetAttackMiss`; W2PP aplica `GetParryRate` e reduz a taxa a 30% para
SkillIndex 22. O C# usa `LegacyAttackMissMath` no caminho de NPC gerado e nao
chama `ResolveLegacyParry` ali, portanto este port manteve o comportamento
canonico 7.69 e nao importou a regra W2PP.

Planner RED Engine.Tests `488 passed=486 failed=2`; o smoke novo tambem falhou
antes do suporte por nao produzir ataque. GREEN Engine.Tests `488/488`. O
WorldHub validou Resist[0]=50 e dano 1492. Smoke TCP sintetico de tres peers
loopback validou dano 1477, AttackOne 68 bytes com SkillIndex 22 / SkillParm 1 /
Motion 4, refresh HP/MP, view-scoping e `skillbar-exterminar=relayed`; nao e
Host autenticado nem E2E visual WYD.exe.

Validacao: `dotnet build WydCdk.Engine.slnx --no-incremental --no-restore`
passou com 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; Engine.Tests 488/488; `--mob-ai-smoke`
passou. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Sem package/publish, deploy, Site, producao, commit ou
push.

### MOB-AI-001 Task 5U - SkillBar repetido Nevasca (17/10/2026)

O template ativo `Serva_de_Odin` mede 816 bytes, tem Clan 1 no offset 16 e
`SkillBar=[36,36,36,36]` no offset 796. `NPCGener.txt` referencia o template
como Leader e Follower em Moinho Abandonado. SkillData row 36 define Nevasca com
InstanceType 3 (`Resist[1]`).

No `GetFunc.cpp::GetAttack` da TMProject 7.69 e do W2PP, os slots repetidos
selecionam skill 36 nos pares melee/ranged e nos sorteios dos slots 0..2. O
resultado e SkillIndex 36 / SkillParm 1 / Motion 4; `InstanceType - 2` aplica
`Resist[1]`. Os limiares ranged divergem (`>=2` no 7.69 e `>=3` no W2PP), mas
nao mudam esse resultado. A cura do quarto slot exige InstanceType 6, logo nao
ocorre; `ProcessSecMinTimer` aplica SetAffect/SetTick apenas com SkillParm zero,
portanto os efeitos tambem ficam fora deste bar.

Planner RED Engine.Tests `488 passed=486 failed=2`; os casos falharam somente
no layout ainda Unsupported. O smoke RED tambem nao produziu o novo ataque.
GREEN Engine.Tests `488/488`; testes cobrem distancias 1..4, HP baixo, bar
incompleto/misto e Clan 4 ainda Unsupported. WorldHub validou Resist[1]=50 e
dano 1492. Smoke TCP sintetico de tres peers loopback validou dano 1477,
AttackOne de 68 bytes com SkillIndex 36 / SkillParm 1 / Motion 4, refresh HP/MP,
view-scoping e `skillbar-nevasca=relayed`; nao e Host autenticado nem E2E visual
WYD.exe.

Validacao: `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental`
passou com 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build dedicado de `WydCdk.Client.E2E` passou
com 0 avisos/erros; Engine.Tests `488/488`; `--mob-ai-smoke` passou.
`dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service
Control Manager. Sem package/publish, deploy, Site, producao, commit ou push.

### MOB-AI-001 Task 5V - SkillBar misto Exterminar (17/10/2026)

Os cinco templates ativos `Freak`, `Freak_Clone`, `Kirei_Clone`, `Noah` e
`Noah_Clone` medem 816 bytes, Clan 1 no offset 16 e
`SkillBar=[255,255,22,255]` no offset 796. `NPCGener.txt` os referencia em
seis grupos Boss/Clone; o template base `Kirei` tem outro bar e fica fora desta
fatia. SkillData row 22 e Exterminar, InstanceType 2.

TMProject 7.69 `GetFunc.cpp::GetAttack` (`Servidor/Source/Code/TMSrv/GetFunc.cpp:1569`)
escolhe slots 0/1 abaixo da distancia 2, slots 2/3 a partir da distancia 2,
depois calcula o seletor de ataque `rand_` em 0..99. Como os slots 0 e 1 sao
255, rolls 0..84 preservam o SkillIndex base: -1 na distancia 1 e 22 a partir
da distancia 2, com Resist -1. Rolls 85..99 selecionam SkillBar[2]=22 e
InstanceType-2=Resist[0], independente da distancia. O `special2=255` faz
Motion 4 / SkillParm 1. O timer exige SkillParm 0 para affects/ticks e o slot 3
esta vazio, logo nao ha efeito nem cura neste layout.

W2PP `Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1401` usa o limiar ranged >=3,
mas a mesma tabela de sorteio 85..99 para o slot 2. Em distancia 2 e roll <85,
W2PP deixa SkillIndex -1, enquanto 7.69 preserva 22; o C# segue 7.69. O
`LegacyMobAiInput` agora carrega o seletor explicitamente, e o runtime passa o
mesmo valor para o planner, snapshot do WorldHub e revalidacao antes da
aplicacao.

Planner RED Engine.Tests `488 passed=487 failed=1`; GREEN `488/488`. Casos
cobrem distancias 1/2/3, rolls 0/84/85/99, layout exato, outro bar misto e Clan
4. O smoke TCP de tres peers força roll 85, aplica Resist[0]=50 e valida dano
1477, AttackOne de 68 bytes (SkillIndex 22 / SkillParm 1 / Motion 4), refresh
HP/MP e relay so ao viewer da view (`skillbar-exterminar-mixed=relayed`). O NPC
sintetico sem arma tem alcance 0; o caso de rede coloca alvo e NPC no mesmo
tile. Um primeiro fixture a distancia 2 nao atacou por estar fora do alcance;
as distancias do contrato sao exercitadas no planner.

Validacao final: Engine.Tests `488/488`; `dotnet build WydCdk.Engine.slnx
--no-restore --no-incremental` passou com 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke` passou com o marcador acima.
`dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service
Control Manager. E evidencia de runtime sintetico/loopback, nao Host autenticado
nem E2E visual WYD.exe. Sem package/publish, deploy, Site, producao, commit ou
push.

### MOB-AI-001 Task 5W - SkillBar misto Flash (18/10/2026)

Quatro templates ativos `Sombra_Negra`, `Sombra_Negra_`, `Kirei` e `Inv_Boss`
medem 816 bytes, Clan 1 no offset 16, e usam
`SkillBar=[255,255,26,255]` no offset 796. `NPCGener.txt` referencia os quatro.
SkillData row 26 e Flash / InstanceType 7.

TMProject 7.69 `GetFunc.cpp::GetAttack`
(`Servidor/Source/Code/TMSrv/GetFunc.cpp:1569`) escolhe slots 0/1 abaixo da
distancia 2 e slots 2/3 a partir de 2. O valor-base fica SkillIndex -1 na
distancia 1 e 26 a partir da distancia 2, com SkillParm 1 / Motion 4: o par
selecionado tem `special2=255`, que e verdadeiro em `if (special2)`. O seletor
0..84 preserva o valor-base; 85..99 seleciona SkillBar[2]=26 e grava
`Resist=InstanceType-2=5`. O dano so aplica o multiplicador se Resist estiver
entre 0 e 3, portanto Flash nao multiplica dano por resistencia. O slot 3 esta
vazio e `ProcessSecMinTimer` so aplica affects/ticks quando SkillParm=0.

W2PP `Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1401` usa o limiar ranged >=3.
Na distancia 2, roll 0..84 conserva SkillIndex -1 em W2PP, enquanto 7.69
preserva SkillIndex 26; o C# segue a fonte 7.69. Em distancia 1 com roll
85..99, ambos selecionam 26 e Resist 5.

TDD RED Engine.Tests `488 passed=487 failed=1`; GREEN `488/488`. O planner cobre
distancias 1/2/3, rolls 0/84/85/99, layout exato, outro layout misto, HP baixo e
Clan 4. O loopback TCP de tres peers força roll 85 e valida AttackOne de 68
bytes (SkillIndex 26 / SkillParm 1 / Motion 4), dano 985 com Resist[0]=50,
refresh HP/MP e relay so ao alvo (`skillbar-flash=relayed`). O dano permanece
985 porque Resist 5 fica fora da faixa elemental 0..3. Fixture WorldHub e NPC
sao sinteticos e colocados no mesmo tile para o alcance 0 sem arma.

Validacao final: Engine.Tests `488/488`; build completa sem incremental com 0
erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; build E2E
com 0 avisos/erros; `--mob-ai-smoke` passou com o marcador acima. `dotnet
--info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service Control
Manager. Evidencia sintetica/loopback, sem Host autenticado nem E2E visual
WYD.exe. Sem package/publish, deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5X - SkillBar misto Enfraquecer (18/10/2026)

O template ativo `Carbuncle_Ouro` mede 816 bytes, tem Clan 1 no offset 16 e
`SkillBar=[255,255,51,255]` no offset 796; `NPCGener.txt` referencia o template.
SkillData row 51 e Enfraquecer / InstanceType 0.

TMProject 7.69 `GetFunc.cpp::GetAttack`
(`Servidor/Source/Code/TMSrv/GetFunc.cpp:1569`) escolhe slots 0/1 abaixo da
distancia 2 e slots 2/3 a partir de 2. O SkillIndex-base e -1 na distancia 1 e
51 a partir de 2, sempre com SkillParm 1 / Motion 4. Rolls 0..84 preservam esse
resultado; rolls 85..99 selecionam SkillBar[2]=51 e calculam
`Resist=InstanceType-2=-2`. O dano so aplica multiplicador quando Resist esta
entre 0 e 3, portanto Resist=-2 nao altera dano. O slot 3 esta vazio e
`ProcessSecMinTimer` exige SkillParm=0 para aplicar affects/ticks.

W2PP `Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1401` usa o limiar ranged >=3.
Na distancia 2, rolls 0..84 conservam SkillIndex -1 em W2PP e 51 em 7.69; o C#
segue a fonte 7.69. Em distancia 1, roll 85..99 seleciona SkillIndex 51 /
Resist=-2 nos dois.

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner e
WorldHub cobrem distancias 1/2/3, rolls 0/84/85/99, layout exato, outro layout
misto e Clan 4. O loopback TCP de tres peers forca roll 85 e valida AttackOne
de 68 bytes (SkillIndex 51 / SkillParm 1 / Motion 4), dano 985 com Resist[0]=50,
refresh HP/MP e relay somente ao alvo
(`skillbar-enfraquecer-mixed=relayed`). Fixture sintetica usa o mesmo tile
para o alcance 0 do NPC sem arma.

Validacao final: Engine.Tests `488/488`; build completa sem incremental com 0
erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke`
passou com o marcador atualizado. `dotnet --info` identificou SDK 10.0.401 e
saiu 1 por acesso negado ao Service Control Manager. Evidencia sintetica/loopback,
sem Host autenticado nem E2E visual WYD.exe. Sem package/publish, deploy,
Site/producao, commit ou push.

### MOB-AI-001 Task 5Y - SkillBar misto Toque Sagrado (18/10/2026)

Os templates ativos `Simio_Anciao`, `Guerreiro_Elfo` e `Batorero_Inf` medem
816 bytes, sao Clan 1, tem `[255,255,1,255]` no offset 796 e aparecem em
`NPCGener.txt`. SkillData row 1 e Toque Sagrado / InstanceType 0. O loader
7.69 em `Servidor/Source/Code/TMSrv/Basedef.cpp:6277-6321` atribui a coluna 7
(`InstanceType`); o valor 2 visto na linha e `InstanceAttribute`, nao
`InstanceType`.

TMProject 7.69 `GetFunc.cpp::GetAttack`
(`Servidor/Source/Code/TMSrv/GetFunc.cpp:1569`) usa slots 0/1 abaixo da
distancia 2 e 2/3 a partir de 2. Rolls 0..84 preservam o ramo de distancia:
SkillIndex -1 abaixo de 2 e 1 a partir de 2, com Resist -1. Rolls 85..99
selecionam SkillBar[2]=1 em qualquer distancia e definem
Resist=`InstanceType-2=-2`. Todos os caminhos selecionados usam SkillParm 1 /
Motion 4; Resist -1 e -2 ficam fora da faixa elemental 0..3, portanto nao
alteram o dano fisico. O slot 3 vazio nao cura e `ProcessSecMinTimer` exige
SkillParm=0 para aplicar affects/ticks.

W2PP `Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1401` usa o limiar ranged >=3,
divergindo da fonte 7.69 em distancia 2; o C# preserva o contrato 7.69. W2PP
tambem calcula parry por `GetParryRate`, enquanto 7.69 usa `GetAttackMiss`; o
port segue o metodo da fonte primaria.

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner e
WorldHub cobrem distancias 1/2/3, rolls 0/84/85/99, layout exato, outro layout
misto e Clan 4. Retificacao: a primeira leitura confundiu InstanceAttribute com
InstanceType e esperava Resist 0; a revalidacao com o campo carregado pelo
7.69 confirma Resist -2 e dano fisico 985 com Resist[0]=50. O loopback TCP de
tres peers forca roll 85 e valida AttackOne de 68 bytes (SkillIndex 1 /
SkillParm 1 / Motion 4), refresh HP/MP e relay somente ao alvo
(`skillbar-toque-sagrado-mixed=relayed`).

Validacao final: Engine.Tests `488/488`; build completa sem incremental com 0
erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke`
passou com o marcador atualizado. `dotnet --info` identificou SDK 10.0.401 e
saiu 1 por acesso negado ao Service Control Manager. Evidencia sintetica/loopback,
sem Host autenticado nem E2E visual WYD.exe. Sem package/publish, deploy,
Site/producao, commit ou push.

### MOB-AI-001 Task 5Z - SkillBar misto Golpe Duplo (18/10/2026)

Os templates ativos `Templario_Amald` e `Templario_Inf` medem 816 bytes, sao
Clan 1, contem `[255,255,2,255]` no offset 796 e aparecem em `NPCGener.txt`.
SkillData row 2 e Golpe Duplo / InstanceType 1.

TMProject 7.69 `GetFunc.cpp::GetAttack` usa slots 0/1 abaixo da distancia 2 e
2/3 a partir de 2; W2PP usa limiar ranged >=3. Rolls 0..84 preservam a escolha
por distancia, enquanto 85..99 selecionam SkillBar[2]=2 em qualquer distancia.
Todos os caminhos selecionados usam SkillParm 1 / Motion 4 e Resist=-1
(`InstanceType-2`); fora da faixa 0..3, nao modifica o dano fisico. O slot 3
vazio nao cura e o timer nao aplica affects/ticks com SkillParm 1. O C# segue
TMProject 7.69.

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner e
WorldHub cobrem distancias 1/2/3, rolls 0/84/85/99, layout exato e limites de
Clan 4. O smoke TCP de tres peers forca roll 85 e valida AttackOne de 68 bytes
(SkillIndex 2 / SkillParm 1 / Motion 4), dano 985 com Resist[0]=50, refresh
HP/MP e relay somente ao alvo (`skillbar-golpe-duplo-mixed=relayed`). Build
completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; Engine.Tests `488/488`; `--mob-ai-smoke`
passou. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Evidencia sintetica/loopback, sem Host autenticado nem
E2E visual WYD.exe. Sem package/publish, deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AA - SkillBar misto Espada da Fênix (18/10/2026)

Os templates ativos `Talos` e `Talos_Clone` medem 816 bytes, sao Clan 1,
contem `[255,255,12,255]` no offset 796 e aparecem em `NPCGener.txt`. Os hashes
SHA-256 sao `D86A532C798B84C71A4C8AA905D06428443F810622FB4E91901673ADECB2DA1C`
e `9573A6A9660D2126EAC28C324F080DE91AC56176ADC71B48241F789A19A2EE84`.
SkillData row 12 e Espada da Fênix / InstanceType 1.

TMProject 7.69 `GetFunc.cpp::GetAttack`
(`Servidor/Source/Code/TMSrv/GetFunc.cpp:1569`) usa slots 0/1 abaixo da
distancia 2 e 2/3 a partir de 2; W2PP
(`Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1401`) usa limiar ranged >=3.
Rolls 0..84 preservam o ramo por distancia: SkillIndex -1 abaixo de 2 e 12 a
partir de 2. Rolls 85..99 selecionam SkillBar[2]=12 em qualquer distancia e
definem Resist=`InstanceType-2=-1`. Todos os caminhos selecionados usam
SkillParm 1 / Motion 4 porque `special2=255` e nao-zero nos dois pares; Resist -1
fica fora da faixa elemental 0..3 e nao altera
o dano fisico. O slot 3 vazio nao cura e `ProcessSecMinTimer` exige SkillParm=0
para aplicar affects/ticks. O C# segue TMProject 7.69.

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner e
WorldHub cobrem distancias 1/2/3, rolls 0/84/85/99, layout exato e limites de
Clan 4. O smoke TCP de tres peers forca roll 85 e valida AttackOne de 68 bytes
(SkillIndex 12 / SkillParm 1 / Motion 4), dano 985 com Resist[0]=50, refresh
HP/MP e relay somente ao alvo (`skillbar-espada-fenix-mixed=relayed`). Build
completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; Engine.Tests `488/488`; `--mob-ai-smoke`
passou. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Evidencia sintetica/loopback, sem Host autenticado nem
E2E visual WYD.exe. Sem package/publish, deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AB - SkillBar duplo Troll (18/10/2026)

Os templates ativos `Troll_Caido@@`, `Troll_Mago_` e `Troll_Mago` medem 816
bytes, contem `[32,35,32,35]` no offset 796, e aparecem em `NPCGener.txt`.
Seus SHA-256 sao `21BBBBAF3BCED0EF13E39C4BCFBFF2884C601610BBADEB19DA313FEE8E24FA29`,
`84B8F516F64CA639EA7086B4AD71C098B87AB6BE828E8E1B1607D9C878F97CCA` e
`50A645E0E251F827479E6B252DD01C44FD41BCCDB3146F5503F4C4ED4A92F8E8`.
Clan dos templates: 1, 1 e 2. SkillData rows 32/35 sao Ataque de Fogo e
Tempestade de Meteoros, ambos InstanceType 2.

TMProject 7.69 `GetFunc.cpp::GetAttack`
(`Servidor/Source/Code/TMSrv/GetFunc.cpp:1569`) escolhe os slots 0/1 abaixo da
distancia 2 e 2/3 a partir de 2; W2PP
(`Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1401`) usa limiar ranged >=3.
Como os pares do bar sao ambos `[32,35]`, o limiar nao altera a selecao. O roll
0..49 escolhe SkillBar[0]=32, 50..84 escolhe SkillBar[1]=35 e 85..99 escolhe
SkillBar[2]=32, em qualquer distancia. Todas as saidas usam SkillParm 1 / Motion
4 / Resist[0]. Slot 3 e row 35 / InstanceType 2, portanto nao satisfaz o ramo de
cura que exige InstanceType 6; o timer tambem exige SkillParm 0 para
affects/ticks. O C# aceita somente o layout exato e segue TMProject 7.69.

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner e
WorldHub cobrem distancias 1/2/3, rolls 0/49/50/84/85/99, layout exato e limites
de Clan 4. O teste WorldHub de dominio valida dano 1492 na sua fixture; o smoke
TCP sintetico, cuja fixture tem baseline de alvo diferente, forca roll 50 e
valida AttackOne de 68 bytes (SkillIndex 35 / SkillParm 1 / Motion 4), dano
1477 com Resist[0]=50, refresh HP/MP e relay somente ao alvo
(`skillbar-troll-dual=relayed`). Build completa sem incremental: 0 erros e
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; TDD GREEN Engine.Tests
`488/488`; `--mob-ai-smoke` passou. Duas execucoes de revalidacao posteriores
terminaram `487/488` por timeout variavel em `authenticated startup requires
ItemList before bind` (uma vez no caso sem `--item-data`, outra no caminho de
ItemList inexistente). Invocacoes diretas do DLL atual rejeitaram ambos os
cenarios com exit 1; a causa da divergencia no subprocesso do harness nao foi
determinada. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso
negado ao Service Control Manager. Evidencia sintetica/loopback, sem Host
autenticado nem E2E visual WYD.exe. Sem package/publish, deploy, Site/producao,
commit ou push.

### MOB-AI-001 Task 5AC - SkillBar repetido Espada Flamejante (18/10/2026)

O template ativo `Rei_Glantuar` (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc/Rei_Glantuar`)
mede 816 bytes, tem Clan 8 no offset 16 e `SkillBar=[17,17,17,17]` no offset
796. SHA-256: `6258F476B187BD7C6D92F2A79961828D27D32AB441B1E5AEFFB65E25436B9608`.
`NPCGener.txt:202-203` referencia esse template como Leader e Follower. SkillData
row 17 (`skilldata.csv:18`) e Espada Flamejante / InstanceType 2.

TMProject 7.69 `GetFunc.cpp::GetAttack`
(`Servidor/Source/Code/TMSrv/GetFunc.cpp:1603-1625`) troca slots 0/1 pelos
slots 2/3 na distancia >=2; W2PP
(`Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1435-1445`) usa >=3. Para Clan 8,
as duas fontes leem `SkillBar`. Como os quatro slots sao 17, a distancia nao
altera o skill primario. O `special=17` cai no default e usa SkillIndex 17 /
SkillParm 1; `special2=17` deixa Motion 4 e SkillParm 1. O ramo opcional do
slot 3 (`GetFunc.cpp:2072-2109` em 7.69) so retorna para cura quando
InstanceType=6, logo nao interrompe esse caso. O override posterior escolhe
SkillBar[0], [1] ou [2] em faixas 0..49, 50..84 e 85..99 e calcula
`Resist=InstanceType-2=0`, mantendo SkillIndex 17 em todos os rolls. W2PP tem
os mesmos slots e override (`Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1608-1628`).
O timer de ambas as fontes so aplica affects/ticks quando `SkillParm==0`
(`ProcessSecMinTimer.cpp:2218` 7.69; W2PP `:2186`); este caminho usa parm 1.
C# aceita somente `[17,17,17,17]` neste novo branch, para mob nao-Clan-4.

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner e
WorldHub cobrem distancias 1/2/3/4, rolls 0/49/50/84/85/99, HP baixo, layout
exato e fronteira Clan 4; WorldHub valida dano 1492 com Resist[0]=50. Smoke TCP
sintetico usa tres peers loopback e valida AttackOne de 68 bytes (SkillIndex 17 /
SkillParm 1 / Motion 4), dano 1477 com Resist[0]=50, refresh HP/MP e relay
somente ao alvo (`skillbar-espada-flamejante=relayed`). Engine.Tests `488/488`;
build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke` passou. `dotnet --info`
identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager.
Evidencia sintetica/loopback, sem Host autenticado nem E2E visual WYD.exe. Sem
package/publish, deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AD - SkillBar misto Tempestade de Raios (18/10/2026)

O template ativo `Arqueiro_Elfo`
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc/Arqueiro_Elfo`)
mede 816 bytes, tem Clan 1 no offset 16 e `SkillBar=[255,255,79,255]` no
offset 796. SHA-256:
`7154FECD018FE4480CF6EA974C009EBCE88731D47141AB20C8F1C639BC9EBB96`.
`NPCGener.txt` referencia o template 26 vezes como Leader e 26 como Follower.
SkillData row 79 (`Servidor/Server/Common/skilldata.csv:80`) e
`Tempestade_de_Raios`, InstanceType 1.

TMProject 7.69 `GetFunc.cpp::GetAttack` usa os slots ranged a partir da distancia
>=2 (`Servidor/Source/Code/TMSrv/GetFunc.cpp:1604`); W2PP usa >=3
(`Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1436`). Portanto, em distancia 2 e
roll 0..84, o contrato 7.69 seleciona SkillIndex 79, enquanto W2PP mantem o
primario SkillIndex -1; em distancia 1 ambos mantem -1, e o seletor 85..99
escolhe SkillBar[2]=79 em ambas as fontes. O `special2=255` e nao-zero, entao
`if (special2)` fixa Motion 4 e SkillParm 1. A selecao aleatoria calcula
`Resist=InstanceType-2=-1`; o dano continua fisico sem modificador elemental.
O slot 3 esta vazio, sem ramo de cura; SkillParm 1 nao ativa affects/ticks do
timer (`ProcessSecMinTimer.cpp:2218` na 7.69, `:2186` no W2PP). C# aceita
somente este layout exato para mob nao-Clan-4.

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner cobre
distancias 1/2/3, rolls 0/84/85/99, layout exato e fronteira Clan 4; WorldHub
valida SkillIndex 79 / SkillParm 1 / Motion 4 / Resist -1 e dano 995 mesmo com
Resist[0]=50. Smoke TCP sintetico de tres peers loopback valida AttackOne de 68
bytes, dano 985, refresh HP/MP e relay somente ao alvo
(`skillbar-arqueiro-elfo-mixed=relayed`). Build completa sem incremental:
exit 0, 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke` exit 0. `dotnet --info`
identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager.
Sem Host autenticado ou E2E visual WYD.exe.
Publish self-contained local: 200 arquivos / 83.977.091 bytes; ZIP SHA-256
`3EEE4E2FDD69C2333C795A64AFD3B665ADC4258EE141C1B4054A7A1D99C7037C`.
SSH `wyd-vps` foi negado no sandbox e expirou com acesso elevado; sem transferencia,
restart ou verificacao atual de UP/PVP.

### MOB-AI-001 Task 5AE - SkillBar misto Relampago/Trovao (18/10/2026)

Os templates ativos `Kalitz_Mago` e `Kalitz_Mago_`
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/npc/`)
medem 816 bytes, sao Clan 1 e usam `SkillBar=[33,33,37,255]` no offset 796;
Clan esta no offset 16. SHA-256 de `Kalitz_Mago`:
`0CCC4CB7C691EE3DE5FAC4218C5C38FEB4ABDF759D068D49E9A298E0531CB5A7`;
de `Kalitz_Mago_`:
`F0BF7AB66DD41F2F98589C13342F9AC52CE6C7B7C076F1CE507FA0102790F0C4`.
`NPCGener.txt` contem 28 referencias combinadas desses templates. SkillData
rows 33/37 sao `Relampago` / InstanceType 5 e `Trovao` / InstanceType 0,
resultando em Resist[3] e Resist -2, respectivamente.

TMProject 7.69 `GetFunc.cpp::GetAttack` seleciona os slots ranged em distancia
>=2 (`Servidor/Source/Code/TMSrv/GetFunc.cpp:1604`); W2PP usa >=3
(`Server/W2PP/Source/Code/TMSrv/GetFunc.cpp:1436`). O seletor final 0..49 escolhe
SkillBar[0]=33, 50..84 escolhe SkillBar[1]=33, e 85..99 escolhe SkillBar[2]=37;
logo o SkillIndex final independe da distancia. O seletor bruto calcula
Resist[3] para Skill 33 e Resist -2 para Skill 37; apenas Resist[3] aplica
modificador elemental. Motion e 4 nos dois pares em ambas as fontes: perto,
`special2=33` consome `rand()%4` e cai no default; ranged, `special2=255` tambem
satisfaz `if (special2)` e mantem Motion 4. O limiar muda qual par e avaliado em
d=2, mas o seletor bruto define o SkillIndex final e Motion nao diverge. O seletor
e modelado por roll explicito no C#; esse sorteio nao altera o resultado AttackOne.
SkillParm permanece 1; o quarto slot e 255 (sem cura) e o
timer so aplica affects/ticks com SkillParm 0 (`ProcessSecMinTimer.cpp:2218` na
7.69, `:2186` no W2PP).

TDD RED Engine.Tests `488 passed=486 failed=2`; GREEN `488/488`. Planner cobre
distancias 1/2/3, rolls 0/49/50/84/85/99, layout exato e fronteira Clan 4;
WorldHub valida Skill 37 / SkillParm 1 / Motion 4 / Resist -2 sem modificador
elemental; o baseline de dano e mantido. Smoke TCP sintetico de tres peers
loopback força o seletor 85 e valida Skill 37 / Motion 4, Resist -2 sem
modificador elemental, AttackOne de 68 bytes,
refresh HP/MP e relay apenas ao alvo (`skillbar-kalitz-mago-mixed=relayed`). Build
completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke` exit 0. `dotnet --info`
identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager.
Evidencia sintetica/loopback, sem Host autenticado ou E2E visual WYD.exe.
Publish self-contained local: 200 arquivos / 83.977.679 bytes; ZIP SHA-256
`DC4BB0F1F187F06394A41D773790BC0889939F9F2649A7F33DE1CEE14AA92B2E`, sem nomes
suspeitos de segredo/credencial. SSH `wyd-vps` teve Permission denied no sandbox
e timeout na repeticao elevada; sem transferencia, restart ou verificacao atual
de UP/PVP.

### MOB-AI-001 Task 5AF - SkillBar misto Bruxa Elfica (19/10/2026)

Os templates ativos `Bruxa_Elfica`, `Bruxa_Elfica_` e `Bruxa_Elfica__` medem
816 bytes e usam `[38,34,38,51]` no offset 796; Clan fica no offset 16.
Os Clans sao 1, 2 e 1, com 56 referencias Leader/Follower combinadas em
`NPCGener.txt`. SHA-256: `Bruxa_Elfica`
`301816376E743323BF21BA36430EF340E44DB1ADF4EFDDA7412FD97C6F9737CF`,
`Bruxa_Elfica_`
`C1E3070B10EAA00B305EE29BA8C033A3C2A45066B2A2BD80F755C3EE381B68FA`,
`Bruxa_Elfica__`
`A1527BF5F43C752F5300EB4D563454956E7C9A8C2238A5C3994240A10DD9DB5B`.

SkillData 7.69 rows 38/34/51 sao Fenix_de_Fogo / InstanceType 2,
Lanca_de_Gelo / InstanceType 3 e Enfraquecer / InstanceType 0. TMProject
7.69 seleciona o par ranged em distancia >=2 e W2PP em >=3. Os dois pares
`special2` (34/51) consome `rand()%4` antes do seletor bruto, mas mantem Motion
4 pelo default; o seletor 0..49 e 85..99 escolhe slot 0/2 (Skill 38, Resist[0]),
e 50..84 escolhe slot 1 (Skill 34, Resist[1]) em todas as distancias. O planner
recebe o seletor explicitamente e nao reproduz a sequencia global de `rand()`
do C++. O slot 3 nao cura porque row 51 nao e
InstanceType 6; `SkillParm=1` tambem nao ativa affects/ticks no timer. Clan 4
permanece Unsupported por divergencia entre `BaseScore.Special` 7.69 e
`SkillBar` W2PP.

TDD RED Engine.Tests `488 passed=485 failed=3`: planner e WorldHub foram as
duas falhas esperadas; uma falha de startup resultou do bloqueio de sockets na
sandbox. GREEN Engine.Tests `488/488` com permissao de loopback. Planner cobre
distancias 1/2/3, rolls 0/49/50/84/85/99, layout exato, slot 3 alterado,
Clan 2 e fronteira Clan 4; um caso de HP baixo com roll 50 confirma que o slot 3
nao substitui o ataque. WorldHub confirma Skill 34 / SkillParm 1 / Motion 4 /
Resist[1] e dano 1492 com Resist[1]=50. O smoke TCP sintetico de tres peers
confirma Skill 34, dano 1477, AttackOne 68 bytes, refresh HP/MP e relay somente
ao alvo (`skillbar-bruxa-elfica-mixed=relayed`). Build completa sem incremental:
0 erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; build E2E
0 avisos/erros. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso
negado ao Service Control Manager. Restore/publish emitiu NU1900 por indisponibilidade
do feed de auditoria NuGet; a verificacao de vulnerabilidades ficou inconclusiva.
Evidencia sintetica/loopback, sem Host autenticado nem E2E visual WYD.exe.
Publish self-contained local: 200 arquivos / 83.977.727 bytes; ZIP SHA-256
`221B9C8097B978BDFBBFB1786D2B8292F5CC4A8AD4558ED3C8FE57DC1B3DF972`; zero
nomes suspeitos de segredo/credencial. SSH `wyd-vps` foi negado no sandbox e
expirou com acesso elevado; sem transferencia, restart ou verificacao UP/PVP.
### MOB-AI-001 Task 5AG - SkillBar misto Lich_Demo (19/10/2026)

O template ativo `Lich_Demo` tem 816 bytes, Clan 1, e `[52,52,51,255]` no
offset 796. Ha 38 referencias Leader/Follower em `NPCGener.txt`. SHA-256:
`FEF4A3A705C7FAAD709C6146773D0A50F4F8B31EE0A62F713AF8849CAD2C3213`.

SkillData 7.69 row 52 e `Furia_de_Gaia` / InstanceType 4 (Resist[2]); row 51 e
`Enfraquecer` / InstanceType 0 (Resist -2, fora da faixa elemental 0..3).
TMProject 7.69 e W2PP aplicam o seletor bruto apos o mapeamento principal:
0..84 seleciona Skill 52 / Resist[2], 85..99 seleciona Skill 51 / Resist -2.
`special2=52` consome `rand()%4` e cai no default, mantendo Motion 4. O par ranged
seleciona `special2=255`, tambem nao-zero, e preserva Motion 4; o limiar 7.69
>=2 / W2PP >=3 so muda qual par e avaliado em d=2. O port segue o threshold 7.69 e
recebe o seletor explicitamente, sem reproduzir o stream global de `rand()`.
Slot 3 e 255, sem cura; `SkillParm=1` nao ativa affects/ticks no timer. Clan 4
permanece fora do layout suportado.

TDD RED Engine.Tests `488 passed=487 failed=1`; GREEN `488/488`. Planner cobre
distancias 1/2/3, seletores 0/49/50/84/85/99, layout exato, layout alternativo,
Clan 4 e HP baixo. WorldHub valida Skill 52 / Resist[2] / dano 1492 para seletor
49 e Skill 51 / Resist -2 / dano 995 para seletor 85; ambos usam Motion 4.
Smoke TCP sintetico de tres peers usa seletor 49 e alvo adjacente, valida Skill
52, dano 1477, AttackOne 68 bytes, refresh HP/MP e relay somente ao alvo
(`skillbar-lich-demo-mixed=relayed`). Build completa sem incremental: 0 erros,
2 NU1900 pelo feed de auditoria NuGet indisponivel e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `dotnet --info` SDK 10.0.401, exit 0.
Evidencia loopback sintetica, sem Host autenticado nem E2E visual WYD.exe.
Publish self-contained local: 200 arquivos / 83.977.799 bytes; ZIP SHA-256
`AE1578961A459203EE2F9EFADD206FF721F7233DAF5F6C4F8277896059843B6C`, sem
nomes suspeitos de segredo/credencial. SSH `wyd-vps` foi negado no sandbox e
expirou com acesso elevado; sem transferencia, restart ou verificacao UP/PVP.
### MOB-AI-001 Task 5AH - Mago_Inf mixed SkillBar (19/10/2026)

O template ativo `Mago_Inf` mede 816 bytes, tem Clan 1 no offset 16 e usa
`SkillBar=[36,34,39,255]` no offset 796. `NPCGener.txt` contem 26 referencias
combinadas Leader/Follower. SHA-256:
`B36559A0BCACBDA1AD63C56C9043C257EF7483C109B0689797D9A42E2F92A96D`.
SkillData rows 36/34/39 sao Nevasca / Lanca_de_Gelo / Inferno, com
InstanceType 3/3/2 e Resist[1]/Resist[1]/Resist[0].

TMProject 7.69 seleciona o par ranged em distancia >=2; W2PP usa >=3. O par
primario perto `36/34` consome `rand()%4`; o default mantem Motion 4. No par
ranged, `special2=255` tambem satisfaz `if (special2)` e resulta em Motion 4.
O seletor bruto escolhe Skill 36 em 0..49, Skill 34 em 50..84 e Skill 39 em
85..99. SkillParm e 1 em todos os caminhos; o limiar 7.69 >=2 / W2PP >=3 muda
qual par e avaliado em d=2, mas nao altera os campos finais deste layout. A skill final vem do seletor bruto,
independente do par primario. O slot 3 vazio nao cura, e o timer exige
SkillParm 0 para affects/ticks. Clan 4 permanece Unsupported.

TDD RED Engine.Tests `488/486/2`; GREEN `488/488`. Planner e WorldHub cobrem
layout exato, seletores 0/49/50/84/85/99, distancias e fronteira Clan 4, alem
de Resist[1]=50 para Skills 36/34 e Resist[0]=50 para Skill 39. O smoke TCP
sintetico de tres peers valida seletor 50, Skill 34 / SkillParm 1 / Motion 4,
dano 1477 com Resist[1]=50, AttackOne de 68 bytes, refresh HP/MP e relay
somente ao alvo (`skillbar-mago-inf-mixed=relayed`).

Durante a revalidacao, o caso passivo existente foi corrigido: o alvo estava no
mesmo tile do NPC, mas a aquisicao passiva 7.69 exclui o proprio tile. O fixture
agora coloca o jogador em `(11,10)` adjacente ao NPC `(10,10)`; `passive-aggro`
passou novamente.

Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build E2E 0 avisos/erros; Engine.Tests
`488/488`; `--mob-ai-smoke` exit 0 com todos os markers, incluindo
`skillbar-mago-inf-mixed=relayed`. `dotnet --info` identificou SDK 10.0.401 e
saiu 1 por acesso negado ao Service Control Manager. Evidencia sintetica/loopback,
sem Host autenticado ou E2E visual WYD.exe. Sem package/publish, deploy,
Site/producao, commit ou push.

### MOB-AI-001 Task 5AI - Shama_Amald mixed SkillBar (19/10/2026)

O template ativo `Shama_Amald` mede 816 bytes, tem Clan 1 no offset 16 e usa
`SkillBar=[19,33,48,255]` no offset 796. `NPCGener.txt` contem cinco
referencias combinadas Leader/Follower. SHA-256:
`9BA14A29D0BCC0CFC2F9F8A71A3A4DA79B8521AD019570EC2B4F3B158AC7B03D`.
SkillData rows 19/33/48 tem InstanceType 3/5/2, logo Resist[1]/Resist[3]/Resist[0].

TMProject 7.69 seleciona o par ranged em distancia >=2; W2PP usa >=3.
O seletor bruto escolhe Skill 19 em 0..49, Skill 33 em 50..84 e Skill 48 em
85..99, independentemente do par de distancia. O par perto `19/33` consome
`rand()%4` por `special2=33`, cujo default preserva Motion 4 e SkillParm 1;
o par ranged `48/255` tambem tem `special2=255`, que define Motion 4. O slot 3 vazio nao cura, e o timer so
aplica affects/ticks com SkillParm 0. Clan 4 permanece Unsupported.

TDD RED Engine.Tests `488/486/2`; GREEN `488/488`. Planner cobre distancias
1/2/3, limites do seletor, layout exato, variante e Clan 4. WorldHub cobre
as tres skills e Resist[1]/[3]/[0] com dano 1492 no fixture de dominio.
O smoke TCP sintetico de tres peers valida seletor 50, Skill 33 / SkillParm 1 /
Motion 4, Resist[3]=50, dano 1477, AttackOne de 68 bytes, refresh HP/MP e
relay somente ao alvo (`skillbar-shama-amald-mixed=relayed`). Como o harness
nao carrega ItemList nem arma no NPC, o caso de runtime usa tile compartilhado;
as distancias 1/2/3 sao cobertas por Planner/WorldHub.

Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build E2E 0 avisos/erros; Engine.Tests
`488/488`; `--mob-ai-smoke` exit 0 com todos os markers. `dotnet --info`
identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager.
Evidencia sintetica/loopback, sem Host autenticado ou E2E visual WYD.exe. Sem
package/publish, deploy, Site/producao, commit ou push.


### MOB-AI-001 Task 5AJ - Mago_Amald mixed SkillBar (19/10/2026)

O template ativo `Mago_Amald` mede 816 bytes, tem Clan 1 no offset 16 e usa
`SkillBar=[36,34,26,255]` no offset 796. `NPCGener.txt` tem dez referencias
Leader/Follower. SHA-256: `C154BE6F52996DEE63C088E2529A6AA7B6A8DD1F26576EEE6D5E9452918869F1`.
SkillData rows 36/34/26 tem InstanceType 3/3/7 e Resist[1]/Resist[1]/Resist[5].

TMProject 7.69 escolhe slots ranged em distancia >=2; W2PP em >=3. O seletor
bruto, aplicado depois, escolhe Skill 36 em 0..49, 34 em 50..84 e 26 em 85..99,
independente do par de distancia. `special2=34` perto consome `rand()%4` e seu
default mantem Motion 4 / SkillParm 1; `special2=255` ranged tambem define
Motion 4. Resist 5 fica fora da faixa elemental 0..3 e nao altera dano. Slot 3
vazio nao cura, SkillParm 1 pula efeitos/ticks e Clan 4 permanece Unsupported.

TDD RED Engine.Tests `488/486/2`; GREEN `488/488`. Planner cobre distancias
1/2/3, limites do seletor, layout alternativo e Clan 4. WorldHub confirma
InstanceType 3 com Resist[1]=50/dano 1492 e InstanceType 7 / Resist 5 sem
multiplicador/dano 995. O smoke TCP sintetico de tres peers seleciona Flash 26,
confirma AttackOne de 68 bytes / Motion 4 / dano 985, refresh HP/MP e relay
somente ao alvo (`skillbar-mago-amald-mixed=relayed`).

Build completa sem incremental: exit 0, 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build E2E exit 0, 0 avisos/erros; Engine.Tests
`488/488`; `--mob-ai-smoke` exit 0. `dotnet --info` identificou SDK 10.0.401 e
saiu 1 por acesso negado ao Service Control Manager. Evidencia sintetica/loopback,
sem Host autenticado ou E2E visual WYD.exe. Sem package/publish, deploy,
Site/producao, commit ou push.

### MOB-AI-001 Task 5AK - Dark_Shadow_ primary-only Flash SkillBar (19/10/2026)

O template ativo `Dark_Shadow_` mede 816 bytes, tem Clan 1 no offset 16 e usa
`SkillBar=[26,255,255,255]` no offset 796. `NPCGener.txt` tem duas referencias
Leader/Follower. SHA-256: `8FE2AD15130ED655E70FC47856E8279ED2EB6260EEBF2164BCBC28E089F7F9F8`.
SkillData row 26 e Flash, InstanceType 7, Resist 5.

TMProject 7.69 troca para os slots ranged em distancia >=2; W2PP em >=3. A
selecao primaria e Skill 26 no alcance curto. Depois, o seletor bruto de
SkillBar[0..2] escolhe Skill 26 / Resist 5 em rolls 0..49 e Skill 26 / Resist -1
em 50..99. No par ranged, ambos os slots sao 255: rolls 0..49 ainda selecionam
Skill 26 / Resist 5; rolls 50..99 deixam SkillIndex -1 / Resist -1. Nos caminhos
com `special2=255`, Motion 4 / SkillParm 1 permanecem. Resist 5 fica fora da
faixa elemental 0..3 e nao modifica o dano; o slot 3 vazio nao cura, e
SkillParm 1 pula efeitos/ticks. Clan 4 permanece Unsupported.

TDD RED: Engine.Tests `488/450/38`, com os dois fails esperados da nova fatia e
36 falhas ambientais de filesystem/socket/listener no sandbox. A execucao local
elevada passou `488/488`. Planner cobre distancias 1/2/3, limites do seletor,
layout alternativo e Clan 4; WorldHub confirma SkillIndex/Resist e dano fisico
995 apesar de Resist[5]. O smoke TCP sintetico de tres peers confirma AttackOne
de 68 bytes / Motion 4, dano 985, refresh HP/MP e relay somente ao alvo
(`skillbar-dark-shadow-primary=relayed`). Build completa sem incremental: 0
erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; build E2E 0
avisos/erros; `--mob-ai-smoke` exit 0. `dotnet --info` identificou SDK 10.0.401
e saiu 1 por acesso negado ao Service Control Manager. Evidencia sintetica em
loopback, sem Host autenticado ou E2E visual WYD.exe; sem package/publish,
deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AL - Ranger_Inf mixed SkillBar (19/10/2026)

O template ativo `Ranger_Inf` mede 816 bytes, tem Clan 1 no offset 16 e usa
`SkillBar=[255,79,73,255]` no offset 796. `NPCGener.txt` tem 24 referencias
Leader/Follower. SHA-256: `6A19B0C7A29DC23C159C34A53C056F8CE6BCFD05491627520DF4C0CE1351F22E`.
Na tabela primaria `SkillData.csv`, row 79 (`Tempestade_de_Raios`) tem
InstanceType 1 / Resist -1; row 73 (`Ilusão`) tem InstanceType 0 / Resist -2.

TMProject 7.69 `GetFunc.cpp:1603-1619` muda para as slots `[2]/[3]` em distancia
>=2; W2PP `GetFunc.cpp:1435-1450` muda em >=3. A preparacao de SkillIndex e
Motion/SkillParm vem de `GetFunc.cpp:1630-1679`, e o seletor bruto em
`GetFunc.cpp:1789-1808` (7.69) / `1910-1928` (W2PP) escolhe a slot 1 para rolls
50..84 e a slot 2 para 85..99; 0..49 preserva a skill primaria. Assim, em
d<2, SkillIndex fica -1 / 79 / 73 por faixa de roll; a partir de d=2 na 7.69
(d=3 no W2PP), fica 73 / 79 / 73. Motion 4 e SkillParm 1 em todos os casos.
Como Resist -1/-2 ficam fora de 0..3, nao ha modificador elemental. O slot 3
vazio nao ativa cura; `ProcessSecMinTimer.cpp:2218` (7.69) e `:2186` (W2PP)
limitam efeitos a SkillParm 0. Clan 4 permanece Unsupported no recorte.

TDD RED: Engine.Tests `488/450/38`; dois fails esperados da tarefa e 36 falhas
ambientais de filesystem/loopback/listener no sandbox. Execucao local elevada:
`488/488`. Planner cobre distancias 1/2/3, limites 0/49/50/84/85/99, layout
alternativo e Clan 4; WorldHub confirma dano 995 com Resist[1]=50. Build
completa sem incremental: exit 0, 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; o E2E foi compilado na solution. O smoke TCP
sintetico de tres peers confirma SkillIndex 73 / Resist -2, dano 985, AttackOne
de 68 bytes, refresh HP/MP e relay somente ao alvo
(`skillbar-ranger-inf-mixed=relayed`). `--mob-ai-smoke` exit 0. `dotnet --info`
identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager.
Evidencia sintetica/loopback, sem Host autenticado ou E2E visual WYD.exe. Sem
package/publish, deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AM - Shaman_Inf mixed SkillBar (19/10/2026)

O template ativo `Shaman_Inf` mede 816 bytes, tem Clan 1 no offset 16 e usa
`SkillBar=[52,55,63,255]` no offset 796. `NPCGener.txt` tem 24 referencias
Leader/Follower. SHA-256: `D1A9DD297529AF7888E1DDF66F1297B75C6386018432874E35F5701FF181D427`.
Na tabela primaria `SkillData.csv`, rows 52 (`Furia_de_Gaia`) e 55
(`Espirito_Vingador`) tem InstanceType 4 / Resist[2]; row 63 (`Evocar_Succubus`)
tem InstanceType 11 / Resist 9.

TMProject 7.69 `GetFunc.cpp:1603-1619` muda o par base para slots `[2]/[3]` em
distancia >=2; W2PP `GetFunc.cpp:1435-1445` muda em >=3. O ramo `special2` usa
55 perto e 255 no par ranged; ambos entram no bloco nao-zero, consomem
`rand()%4`, definem Motion 4 e caem no default que mantem SkillParm 1. Em
seguida, o seletor bruto 7.69 (`GetFunc.cpp:1789-1808`) / W2PP
(`GetFunc.cpp:1907-1926`) seleciona SkillBar[0] para 0..49, [1] para 50..84 e
[2] para 85..99, sobrepondo a selecao base em todas as distancias. Os campos
finais sao SkillIndex 52/55/63, SkillParm 1, Motion 4 e Resist 2/2/9. Resist 2
usa Resist[2]; Resist 9 fica fora da faixa elemental 0..3. Slot 3 e 255 e nao
ativa o ramo de cura InstanceType 6; `ProcessSecMinTimer.cpp:2218` (7.69) e
`:2186` (W2PP) aplicam efeitos/ticks somente quando SkillParm e zero. Clan 4
permanece Unsupported.

TDD RED: Engine.Tests `488/486/2` (planner e WorldHub); o smoke loopback tambem
falhou antes do suporte com `attacks=[]`. GREEN Engine.Tests `488/488`. Planner e
WorldHub cobrem distancias 1/2/3, limites 0/49/50/84/85/99, dano com Resist[2]
e sem multiplicador em Resist 9, layout alterado e Clan 4. O smoke TCP sintetico
de tres peers forca roll 50 e valida Skill 55 / Resist[2]=50, dano 1477,
AttackOne de 68 bytes, refresh HP/MP e relay somente ao alvo
(`skillbar-shaman-inf-mixed=relayed`). Build completa sem incremental: 0 erros
e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `--mob-ai-smoke`
exit 0. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Evidencia sintetica/loopback, sem Host autenticado ou
E2E visual WYD.exe. Publish self-contained local: 200 arquivos / 83.979.739
bytes; ZIP `Backup/Build/MOB-AI-001-Task5AM-20261019.zip`, SHA-256
`BFBFC64E1591A54D176670A919A64E8FDA937CDE605FDAE2344FD03D88527E6`, zero nomes
suspeitos de credencial. SSH `wyd-vps` foi negado no sandbox e expirou na
tentativa elevada read-only; sem transferencia, restart ou verificacao UP/PVP.
Sem deploy remoto, Site/producao, commit ou push.

### MOB-AI-001 Task 5AN - Orc_Medico slot 3 (19/10/2026)

Os templates ativos `Orc_Medico` (Clan 2, SHA-256
`55FA2315833965224426D2F42E2DF65ADE18D55AC4CA7DDB36C169206A2C13DA`) e
`Orc_Medico_` (Clan 1, SHA-256
`2C9544BA32074DF74AEB00385E17201858215D76A04B6F07C3332750DE1F5B07`) medem
816 bytes, tem `SkillBar=[40,40,51,29]` no offset 796 e 72 referencias
Leader/Follower em `NPCGener.txt`. SkillData row 29 e Recuperar / InstanceType 6;
rows 40 e 51 sao Nevoa Venenosa / InstanceType 4 / Resist[2] e Enfraquecer /
InstanceType 0 / Resist -2.

TMProject 7.69 `GetFunc.cpp:1603-1619` e W2PP `GetFunc.cpp:1435-1445` concordam:
o ramo do slot 3 dispara com roll 25..64 quando o HP inteiro do mob ou leader,
calculado por `CurrentHp*10/(MaxHp+1)`, e <=8. A source usa o proprio mob apenas
quando `Leader<=0`; para ID positivo, dereferencia o indice sem validar existencia
ou HP. Portanto, nao ha fallback da source para um leader positivo invalido. O
C# segue somente quando um ID positivo resolve para NPC gerado ativo e vivo; ID
ausente, morto, persistente ou summon retorna `Unsupported` antes da aplicacao.
ID <=0 e ID positivo igual ao proprio NPC usam o proprio mob. Com leader valido,
o ataque seleciona o leader somente quando `mobPercent > leaderPercent` (empate
fica no mob). O ataque seleciona `SkillIndex=6` e calcula `MaxHp/10`; o C# falha
fechado como `Unsupported` se isso resultar em zero, sem alterar HP. Embora o
nome seja Recuperar, `InstanceType=6` faz o cliente subtrair dano positivo do HP.
No caminho normal, rolls 0..84 selecionam row 40 e 85..99 row 51 em qualquer
distancia; SkillParm 1 nao aciona efeitos/ticks. As diferencas de limiar ranged
7.69 >=2 / W2PP >=3 nao mudam os campos deste bar. Clan 4 continua Unsupported.

Planner cobre limites 24/25/64/65, HP <=8 e acima do limiar, escolha self/lider
por percentual, empate e rejeicao de leader positivo morto/nao-gerado; WorldHub
repete a rejeicao e protege o dano zero para nao alterar HP. Tambem verifica
recipients no alvo NPC. O frame
AttackOne aceita TargetID NPC e a relay envia UpdateScore ao alvo sobrevivente,
ou RemoveMob(type=1) quando morre. TDD RED: Engine.Tests `487/488` no planner;
depois `487/488` no WorldHub pela selecao errada de alvo; a mudanca de contrato
ficou `486/488` por essa falha mais o limite antigo de TargetID; GREEN `488/488`.
Smoke loopback sintetico de tres peers passou com
`skillbar-orc-medico-row29=relayed`, incluindo o NPC fora da view recebendo zero
frames. Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; smoke `--mob-ai-smoke` exit 0. SDK 10.0.401;
`dotnet --info` exit 0. Isso e evidencia sintetica TCP, sem Host autenticado ou
E2E visual WYD.exe.

A criacao atual de NPCs gerados registra leaders apenas; esta fatia suporta
`leaderConnectionId` no registro do WorldHub, mas nao adicionou spawn de
followers. A escolha do lider esta testada com NPCs prelinkados e ainda nao e
atingida pelo gerador ativo.

Revisao posterior: leader positivo morto/nao-gerado e dano `MaxHP/10 == 0` agora
falham fechados antes de alterar HP; HP inteiro 9/10 confirma o limite superior
do ramo. Engine.Tests `488/488`; build completa sem incremental: 0 erros e o
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; smoke
`--mob-ai-smoke` passou com `skillbar-orc-medico-row29=relayed`. `dotnet --info`
identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service Control Manager.
Novo publish self-contained local: 200 arquivos / 83.985.851 bytes; ZIP
`Backup/Build/MOB-AI-001-Task5AN-ReviewFix-20261019.zip`, SHA-256
`EE725EE2231F3761A2E830C18E9CE03DD479360240128E1F75E9856BD30E7D5B`; zero nomes
suspeitos de credencial. O ZIP Task5AN original foi preservado. SSH `wyd-vps`
expirou no sandbox e na tentativa elevada read-only em TCP/22; sem transferencia,
restart ou verificacao UP/PVP. Sem Site, producao, commit ou push.

### MOB-AI-001 Task 5AO - SkillBar da Feiticeira e slot 3 (19/10/2026)

Os templates ativos `Feiticeira` (Clan 7, 27 referencias em `NPCGener.txt`, SHA-256
`8B6BA5CED22CC8DF91A9D91C50F4D6FB9E0BDA01BB06261B67089D1124991BAD`) e
`Feiticeira_` (Clan 8, 28 referencias, SHA-256
`9293FF227CD5B012E2E300E8353A01CBAEA7F7D191EAADE09CE8E2FD0B3C1057`) medem
816 bytes e usam `SkillBar=[33,36,38,27]` no offset 796. SkillData row 27 e Cura
(`InstanceType=6`); rows 33/36/38 selecionam Relampago / Nevasca / Fenix de
Fogo, com Resist[3]/[1]/[0].

TMProject 7.69 `GetFunc.cpp:1569-1816` troca para slots 2/3 em distancia >=2;
W2PP `GetFunc.cpp:1401-1635` usa >=3. `special2=36` no par proximo e
`special2=27` no par ranged; ambos sao nao-zero e consomem `rand()%4`. O caso
proximo deixa Motion 4; o caso ranged mapeia 0/1/2/3 para Motion 4/5/7/8. O
seletor bruto posterior escolhe SkillIndex 33 em 0..49, 36 em 50..84 e 38 em
85..99. Antes desse resultado final, slot 3 pode substituir a skill quando o
roll e 25..64 e o HP inteiro do mob ou leader e <=8/10: seleciona SkillIndex 6
e aplica dano positivo `MaxHP/10` ao proprio mob ou ao leader com percentual
menor, como em Task 5AN. O C# exige leader positivo ativo, vivo e gerado; falha
fechado para vinculos invalidos. SkillParm 1 nao aciona efeitos/ticks do timer.
Clan 4 segue Unsupported; no intervalo de distancia 2 o C# segue o contrato 7.69.

Planner e WorldHub cobrem distancias 1/2/3, os quatro valores de Motion,
limites do seletor e do roll de slot 3, as classes Clan 7/8, layout alterado,
Clan 4 e dano em self/leader. O smoke TCP sintetico usa uma arma/tabela de itens
de teste com alcance 2 para alcancar distancia 2; verifica AttackOne de 68 bytes,
Motion 7, dano proprio de 100 e relay apenas ao viewer dentro da view
(`skillbar-feiticeira-row27=relayed`). Engine.Tests `488/488`; build completa
sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke` exit 0. SDK 10.0.401;
`dotnet --info` saiu 1 por acesso negado ao Service Control Manager. O restore
RID `win-x64` para o publish exigiu `NuGetAudit=false`; a auditoria de
vulnerabilidades desse restore fica inconclusiva. Publish self-contained local:
200 arquivos / 83.987.795 bytes; ZIP
`Backup/Build/MOB-AI-001-Task5AO-20261019.zip`, SHA-256
`103C666A80B1E8DB112D2239734FF41B9D0D4F65106BE0357736716D9C173483`, sem nomes
suspeitos de credencial. Smoke sintetico nao valida Host autenticado nem E2E
visual WYD.exe. Sem transferencia/restart/check UP-PVP, Site/producao, commit ou
push.

### MOB-AI-001 Task 5AP - Bruxa slot 3 (19/10/2026)

Os templates ativos `Bruxa` (Clan 7, 25 referencias Leader/Follower em
`NPCGener.txt`, SHA-256
`D2E25AC3941DDDBA8D59FA558AAA45FD050F862D535290D4ADA81F7BDF57247F`) e
`Bruxa_` (Clan 8, 25 referencias, SHA-256
`92BC0499C4ACA229A5DD336D8BBD9841201CE64C13DEE2D41CA8F38D7A983402`) medem
816 bytes e usam `SkillBar=[24,27,28,27]` no offset 796. SkillData rows 24/27/28
sao Flecha Magica / Cura / Choque Divino, com InstanceType 4/6/4; o selector
deriva Resist 2/4/2, e Resist 4 fica fora do modificador elemental 0..3.

TMProject 7.69 `GetFunc.cpp:1569-1816` seleciona o par ranged em distancia
>=2; W2PP `GetFunc.cpp:1401-1635` em >=3. Ambos usam `special2=27` nos pares
proximo e ranged, consumindo `rand()%4` e mapeando 0/1/2/3 para Motion 4/5/7/8.
O sorteio bruto posterior escolhe SkillIndex 24 em 0..49, 27 em 50..84 e 28
em 85..99. Antes disso, o slot 3 (row 27 / InstanceType 6) pode substituir o
ataque em rolls 25..64 quando `CurrentHP*10/(MaxHP+1) <= 8`, enviando
`SkillIndex=6` e dano positivo `MaxHP/10` ao proprio NPC ou ao leader valido
com percentual menor. O C# exige leader positivo gerado, ativo e vivo; vinculo
invalido falha fechado. Clan 4 permanece Unsupported.

Planner e WorldHub cobrem Clans 7/8, distancias 1/2/3, todos os valores de
Motion e limites do selector/slot 3, dano self/leader, layout alterado e Clan 4.
Engine.Tests `488/488`; build completa sem incremental: 0 erros e CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke` passou com
`skillbar-bruxa-row27=relayed`, Motion 7, dano proprio 100, AttackOne de 68 bytes,
UpdateScore e view-scoping. `dotnet --info` identificou SDK 10.0.401 e saiu 1
por acesso negado ao Service Control Manager. Restore RID `win-x64` usou
`NuGetAudit=false`, deixando inconclusiva a auditoria de vulnerabilidades.
Publish self-contained local: 200 arquivos / 83.989.603 bytes; ZIP
`Backup/Build/MOB-AI-001-Task5AP-20261019.zip`, SHA-256
`912B6200B0AEDCA0352801E2D6A223536E290D87E9675F17ABD54DF26BF74C6F`, sem nomes
suspeitos de segredo/credencial. `ssh wyd-vps hostname` foi negado no sandbox e
expirou na tentativa elevada somente de leitura em TCP/22; sem transferencia,
restart ou verificacao UP/PVP. Smoke sintetico, sem Host
autenticado nem E2E visual WYD.exe. Sem Site/producao, commit ou push.

### MOB-AI-001 Task 5AQ - Torre_Runica slot 3 (19/10/2026)

O template ativo `Torre_Runica` mede 816 bytes, Clan 1, e usa
`SkillBar=[255,255,255,27]` no offset 796 (SHA-256
`C66EAFA22CB28CCB2EA5803AF0B3D1C5C28BCA590F0552A46C80B456CCA9E4F5`). A
entrada [1016] de `NPCGener.txt` referencia esse template em Leader e Follower
(duas referencias), `RouteType=1`, `MaxNumMob=1`. SkillData row 27 e Cura /
`InstanceType=6`; os slots 0..2 vazios preservam a base fisica
`SkillIndex=-1`, `SkillParm=1`, `Resist=-1`.

TMProject 7.69 `GetFunc.cpp:1601-1816` troca para slots ranged em distancia
>=2; W2PP `GetFunc.cpp:1433-1635` em >=3. Perto, `special2=255`; no par ranged,
`special2=27`. Ambos consomem `rand()%4` quando `special2` e nao-zero; 255
mantem Motion 4, e row 27 mapeia 0/1/2/3 para Motion 4/5/7/8. O C# segue o
limiar 7.69, portanto distancia 2 ja usa o Motion ranged. Depois, ambos rodam o
sorteio de slot 3: rolls 25..64, InstanceType 6, e
`CurrentHP*10/(MaxHP+1)<=8` podem substituir o ataque, enviar `SkillIndex=6`
e dano positivo `MaxHP/10` ao proprio mob ou ao leader de menor percentual. A
source dereferencia Leader positivo sem validar; o C# exige NPC gerado, ativo e
vivo, e falha fechado se o vinculo positivo nao resolver. Clan 4 permanece
Unsupported.

Engine.Tests `488/488`; build completa sem incremental: 0 erros e CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; smoke loopback passou com
`skillbar-torre-runica-row27=relayed`, distancia 2, Motion 7, dano self 100,
AttackOne de 68 bytes, UpdateScore e view-scoping. SDK 10.0.401;
`dotnet --info` exit 0. Restore RID usou `NuGetAudit=false`, deixando
inconclusiva a auditoria de vulnerabilidades. Publish self-contained local:
200 arquivos / 83.990.847 bytes; ZIP final
`Backup/Build/MOB-AI-001-Task5AQ-20261019-final.zip`, SHA-256
`3EECFAA04C5FBDD0C2B28FA34AFE1FDB4AE3B319D206B76411F10AD6D8C3288A`; zero
nomes suspeitos de credencial. O ZIP sem sufixo `-final` e o primeiro artefato
local, anterior a esta revisao de Motion; manter somente o ZIP `-final` como
pacote validado. Sem deploy remoto: o ultimo probe read-only `wyd-vps` na Task
5AP expirou em TCP/22 depois do bloqueio no sandbox; sem transferencia/restart/
check UP-PVP. Smoke sintetico, sem Host autenticado ou E2E visual WYD.exe. Sem
Site/producao, commit ou push.

### MOB-AI-001 Task 5AR - duas barras mistas Clan 1 (19/10/2026)

Foram agrupados dois layouts ativos com o mesmo fluxo de `GetAttack`, em vez
de tratar cada criatura como uma etapa. `Mago_Ciclope` (816 bytes, Clan 1,
SHA-256 `1712BEA423517BE3C6923630D196FB78DC5F506F63867B98DC244C3747B70B1A`)
usa `[33,36,27,40]` e tem uma referencia `Follower` em `NPCGener.txt:50`.
`Bruxa_Negra` (816 bytes, Clan 1, SHA-256
`BA13A265F71A950AF8E774FA6580B838E2DAD803DEAD8C3641083831D01343DC`) usa
`[33,39,38,36]` e aparece como `Leader` e `Follower` nas linhas 107558-107559.

Em TMProject 7.69, `GetFunc.cpp:1569` escolhe o par inicial por distancia,
com slots ranged em `d>=2`; W2PP `GetFunc.cpp:1401` muda esse limite para
`d>=3`. Nos dois casos o sorteio comum posterior escolhe os valores brutos de
SkillBar[0], [1] e [2] em 0..49, 50..84 e 85..99, sobrepondo o skill inicial.
O ramo separado do slot 3 exige roll 25..64 e `InstanceType=6` com HP <=8/10.
As linhas 40 (`InstanceType=4`) e 36 (`InstanceType=3`) nao ativam esse ramo;
no Mago_Ciclope, row 27/Cura esta no slot 2 e tambem nao e efeito de slot 3.
O C# aceita apenas os dois layouts exatos Clan 1 nesse caminho compartilhado.
Skills selecionadas preservam `SkillParm=1` e `Motion=4`; resistencias derivam
de `InstanceType-2` (row 27 resulta em Resist 4, fora da faixa elemental).

Planner cobre distancias 1/2/3 e limites do seletor para ambas as barras,
resistencia e rejeicao de barras/Clan alterados. Engine.Tests `488/488`. O
smoke TCP sintetico percorre ambas: Mago_Ciclope seleciona row 36 com
Resist[1]=50; Bruxa_Negra seleciona row 39 com Resist[0]=50. Os dois relays
produzem dano 1477, AttackOne, refresh HP/MP, mantem o NPC com HP cheio e nao
enviam frame ao observer fora da view (`skillbar-clan1-mixed-selector=relayed`).
Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build do E2E isolada: 0 avisos/erros.
SDK 10.0.401; `dotnet --info` exit 1 por acesso negado ao Service Control
Manager. O usuario confirmou Host autenticado na VPS via `Backup/VPS.rdp`; esta
fatia so tem evidencia de loopback, sem sessao visual do WYD.exe. Sem publish,
deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AS - GetAttack generico por SkillData (19/10/2026)

A direcao aprovada pelo usuario prioriza cobrir classes, metodos e fluxos antes
do refinamento por template. O planner nao reconhece mais SkillBars por lista
exata. Para mob gerado nao-Clan-4 com quatro slots, `GetAttack` usa o par
`[0,1]` em distancia <2 e `[2,3]` a partir de 2 (TMProject 7.69; W2PP muda o
limite para 3). O `special` comum inicia `SkillParm=1`; `special2=0` preserva
Motion 0 e os `special2` conhecidos mapeiam o draw 0..3 para Motion. O sorteio
0..49/50..84/85..99 seleciona os slots 0/1/2 sem substituir um slot vazio. A
resistencia e `SkillData.InstanceType - 2` da habilidade selecionada. Slot 3,
roll 25..64 e `InstanceType=6` ativam dano `MaxHp/10` quando o HP inteiro
`CurrentHp*10/(MaxHp+1) <= 8`, em self ou leader ativo de percentual menor.

`LegacyMobAiInput` leva a tabela SkillData injetada pelo WorldHub; o planner
continua puro e deterministico. Linha selecionada ausente falha fechado.
Clan 4 (usa `BaseScore.Special`), specials 121..128, e o tail de ataque normal
contra NPC continuam explicitamente Unsupported. Para cura de slot 3, somente
`AttackTargetId` recebe o NPC alvo; ataques normais contra jogador preservam
`AttackTargetId=null` para o caminho de dano correto no WorldHub.

`MobAiSkillDataFixture` guarda apenas InstanceType das rows 0..150 de
`TMProject2GlobalClient/Servidor/Server/Common/SkillData.csv`; comparacao local
confirmou 151 rows, sem divergencia no fixture. Planner cobre `[17,255,40,255]`
sem entrada em lista, seletor e limites, distancia, sete mapeamentos `special2`,
`special2=0`, SkillData ausente, slot 3, Clan 4 e boundaries nao portados. Os
casos anteriores organizados por template sairam do conjunto ativo de planner e
do smoke; os registros historicos de comportamento continuam neste documento.

Engine.Tests `488/488`; rebuild completa sem incremental: 0 erros e aviso
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `--mob-ai-smoke`
passou com barra sintetica nao listada, AttackOne 68 bytes, Skill 17, dano 1477,
refresh HP/MP e relay apenas ao alvo (`getattack-generic=relayed`). O loopback
tambem cobre chase/leash, rota Peace, aggro passiva, RouteType 5 e limpeza de
summon. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao
Service Control Manager. Sem Host autenticado, E2E visual do WYD.exe, deploy,
Site/producao, commit ou push.

### MOB-AI-001 Task 5AT - Clan 4 e specials 121..128 em GetAttack (19/10/2026)

TMProject 7.69 `GetAttack` seleciona o par de Clan 4 por
`BaseScore.Special[0..3]`; W2PP diverge e consulta `SkillBar` tambem para Clan 4.
O C# agora transporta os quatro campos Special do snapshot `LegacyScore` ate o
planner. Depois do par, aplica o mapa comum 121..128, seletor dos slots 0..2,
resistencia e ramo do slot 3. A rotina nao altera o tail de ataque normal contra
NPC, que segue `Unsupported` ate a referencia comportamental desse caminho.
Planner/WorldHub cobrem o mapeamento, leitura isolada do snapshot, override por
SkillBar, slot 3 e input Clan 4 sem `BaseScore.Special` fail-closed.

### MOB-AI-001 Task 5AU - inicializacao de rota em GenerateMob (19/10/2026)

TMProject 7.69 (`TMSrv/Server.cpp::GenerateMob`, funcao iniciada na linha 4253)
e W2PP (linha 3442) resolvem os cinco pontos ao criar a MOB: se X e zero, zeram
X/Y; se ha range, sorteiam cada coordenada de `base-range` a `base`; caso
contrario copiam as coordenadas. O gerador copia waits somente para pontos
ativos, inicializa `WaitSec` com `SegmentWait[0]` e permite que `PosX/PosY`
ancorem todos os cinco pontos. A divergencia relevante e de ordem: o C# antes
resolvia apenas a posicao inicial e mantinha os demais pontos/ranges crus.

`LegacyNpcGenerationCatalog.TryCreateLeader` agora resolve cada ponto uma vez,
guarda o resultado na rota runtime e usa o Start resolvido como posicao inicial.
`WorldHub.TrySpawnGeneratedNpc` inicia segmento/progresso/espera pelo Start; o
override de posicao usado pela entrada Pista reancora todos os pontos. O teste
por metodo cobre ranges independentes, X=0, espera, Start/posicao iguais e
override. Engine.Tests `488/488`; rebuild completo sem incremental: 0 erros e
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `--mob-ai-smoke`
passou em loopback com `getattack-generic=relayed`, rotas e view-scoping.
`dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service
Control Manager. Tasks 5AV-5AY foram validadas localmente: spawn de grupos,
busca de tile 7.69, agenda/relocacao e loot temporizado `DUNGEONEVENT`;
Engine.Tests `490/490`, build sem incremental com 0 erros e smoke TCP de tres
peers para NPCs, mais smoke de cinco premios `CreateItem`. O C# conta capacidade
por `GenerationOwnerIndex`; a reconciliacao de `CurrentNumMob` da referencia e
inconsistente para mobs relocacionados, como detalhado na Task 5AX.
Sem Host autenticado, E2E visual,
deploy, Site/producao, commit ou push.

Task 5AX completou agenda e capacidade por origem de `CNPCGenerator`/
`GenerateMob`; Task 5AY cobre o drop `DUNGEONEVENT`. Permanecem ramos especiais
de spawn; depois completar metodos ativos de `CMob`, timer e `GetFunc`. Manter
validacao por metodo e nao reabrir uma bateria por template.

### MOB-AI-001 Task 5AV - geracao generica de grupos (concluida localmente 20/10/2026)

TMProject 7.69 `TMSrv/Server.cpp::GenerateMob` (linha 4253) e W2PP (linha 3442)
selecionam a quantidade de followers entre `MinGroup..MaxGroup`, limitam pelo
`MaxNumMob` antes de contabilizar o leader, criam ate `MAX_PARTY=12` membros,
associam o leader, copiam rota e aplicam offsets de `g_pFormation`. Na entrada
Pista com posicao forcada, cada ponto do follower recebe `rand()%5-2` por eixo.

O C# adiciona selecao de quantidade e construcao de follower em
`LegacyNpcGenerationCatalog`, `WorldHub.TrySpawnGeneratedNpcGroup` e atualiza
consumidores do Host/Pista/castelo/startup para recolher e relayar todos os
membros do grupo. A tabela 7.69 declara `g_pFormation[5][12][2]` (120 inteiros),
mas indexa a terceira dimensao pelo campo `Formation`; ha templates com valor 4.
O C# emula o endereco flattened apenas quando continua dentro da tabela e usa
zero fora dela. Isso e uma adaptacao para comportamento indefinido/fora da
dimensao em C++, nao uma equivalencia validada.

Os testes cobrem quantidade, ordem do limite de grupo, teto `MAX_PARTY`, rota/
formacao e jitter da Pista. A primeira execucao revelou fixtures usando a chave
`Wolf_Follower` em vez de `Wolf_Follower@`; corrigimos tres definicoes mantendo
as assercoes. `WydCdk.Engine.Tests.csproj` passou `489/489`. A build completa
sem incremental teve 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401 e
saiu 1 por acesso negado ao Service Control Manager.

TMProject 7.69 e W2PP usam `GridMulticast` por mob gerado. O Host compartilha
`LegacyNpcGenerationRelay` entre spawn por ataque e entrega de Pista/castelo; o
helper envia cada CreateMob aos participantes da view inclusiva 33x33 do NPC.
O smoke TCP sintetico de tres peers confirma entrega em ordem leader/followers
ao alvo e ao peer na fronteira inclusiva (115,115), e nenhum frame ao peer
externo (118,100). Apos Task 5AW, o leader foi reposicionado de (100,100) para
(99,99) porque o alvo ocupa o tile pedido. O marcador foi
`generated-group=relayed` com `view-scoped=true`.
Esse smoke exercita o helper usado pelos dois caminhos do Host, sem autenticar
o Host nem iniciar o cliente visual.

`g_pFormation[5][12][2]` declara 120 inteiros nas duas fontes; `NPCGener.txt`
possui 23 entradas ativas com `Formation=4`. O teste caracteriza a adaptacao C#:
follower index 4 le offsets flattened ainda dentro da tabela e index 5 usa zero
depois do limite. Isso nao prova equivalencia com o acesso C++ fora da dimensao.
Contabilizacao por origem e agenda foram cobertas na Task 5AX; o drop
`DUNGEONEVENT` foi concluido na Task 5AY e ramos especiais seguem pendentes.
Task 5AW trata separadamente a busca de tile. Sem Host autenticado, E2E visual,
deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AW - tile livre para NPC gerado (concluida localmente 20/10/2026)

TMProject 7.69 `GetFunc.cpp::GetEmptyMobGrid` (linha 2231) aceita a celula pedida
quando livre e com terreno diferente de 127; caso contrario percorre raios 1..9,
y externo e x interno, verificando ocupacao e terreno da celula candidata. W2PP
`GetFunc.cpp` (linha 2027) percorre apenas raios 1..4 e, no fallback, consulta o
terreno da origem em vez do candidato. A implementacao segue 7.69 usando o
`LegacyMobGridSearch` existente com `maximumRadius=9` e
`checkCandidateTerrain=true`; a ocupacao inclui participantes, summons e NPCs,
e o terreno e consultado quando `LegacyMapGrid` esta carregado.

`WorldHub.TrySpawnGeneratedNpcGroup` aplica a busca ao leader e a cada follower.
Quando o leader e reposicionado, o Start da rota runtime e atualizado antes de
construir followers; cada follower tambem atualiza seu proprio Start e posicao.
Leader sem celula livre nao deixa grupo parcial; follower sem celula e ignorado.
Overrides explicitos sem `forceGenerationRoutePosition` preservam o contrato C#
de rota existente; a entrada Pista forcada reancora a rota antes da busca.

TDD RED: o teste novo de colisao falhou antes da implementacao. GREEN: Engine.Tests
`489/489`, cobrindo colisao de leader/follower, raio 9 e terreno bloqueado do
candidato. Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build E2E: 0 avisos/erros. O smoke TCP
`--mob-ai-smoke` passou com `generated-group=relayed`,
`relocated-generated-group=relayed`, `view-scoped=true`, ataque
positivo, morte, refresh de ressurreicao, movimento e aquisicao passiva. SDK
10.0.401; `dotnet --info` saiu 1 por acesso negado ao Service Control Manager.
Sem Host autenticado ou E2E visual de `WYD.exe`; sem deploy, Site/producao,
commit ou push. A agenda e a capacidade por origem foram cobertas na Task 5AX.

### MOB-AI-001 Task 5AX - agenda e relocacao de NPCGener (concluida localmente 20/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:2818-2897` e W2PP
`ProcessSecMinTimer.cpp:2721-2791` percorrem a tabela em ordem de indice,
ignoram 0, 1, 2, 5, 6 e 7, e executam quando
`MinCounter % MinuteGenerate == index % MinuteGenerate`. Apos `GenerateMob`, os
intervalos 500..999, 1000..1999, 2000..3799 e >=3800 recebem os rerolls
correspondentes; a agenda ainda rerolla quando `GenerateMob` retorna sem spawn.
No fechamento da Task 5AX, o branch de loot `DUNGEONEVENT` que sucede o
intervalo >=3800 estava deferido; a conclusao posterior consta na Task 5AY.

Em `Server.cpp::GenerateMob`, ambas as fontes fazem `nindex = index`; para
`MinuteGenerate >= 500`, leem StartX/StartY como `segx/segy`, retornam se
`segx-segy <= 0` e selecionam `reloc = rand() % segx + segy`. A tabela de
origem fornece templates, grupo, capacidade e RouteType; a rota selecionada
fornece os pontos e o `GenerateIndex` gravado nos mobs. 7.69 e W2PP incrementam
`CurrentNumMob` por `nindex`, mas `DeleteMob` decrementa pelo `GenerateIndex`.
`RebuildGenerator` tambem reconcilia por `GenerateIndex`; essa deriva e uma
inconsistencia compartilhada das fontes, nao uma regra de capacidade coerente.

O C# separa esses papeis em `LegacyNpcGenerationScheduledGroup.GenerateIndex`
(origem) e `RouteGenerateIndex` (rota). `LegacyWorldNpc.GenerateIndex` conserva
a identidade/rota do mob; `GenerationOwnerIndex` guarda a origem e
`WorldHub.TrySpawnGeneratedNpcGroup` conta os vivos pela origem ao aplicar
`MaxNumMob`. A mesma separacao alimenta leader e followers: templates, grupo,
formacao e RouteType vem da origem; pontos, waits e identidade vem da rota.
Assim a capacidade nao migra de gerador quando um NPC relocacionado morre. Essa
e uma divergencia deliberada em relacao ao decremento defeituoso do legado.

O C# preserva a formula `rand()%StartX + StartY` e a ordem dos rolls de
relocacao, quantidade, rota e novo intervalo. StartX <= 0, StartX-StartY <= 0
ou rota sem definicao no catalogo nao geram NPC; a agenda mantem o reroll
externo do intervalo. Um valor invalido do callback de randomizacao e rejeitado
com excecao, pois nao pode ocorrer com `rand()` real. As tres `NPCGener.txt` locais auditadas tem duas
entradas >=500 cada; ambas abortam na guarda StartX-StartY em 7.69 e W2PP. A
source apenas registra e continua se o resultado aponta fora do catalogo; o C#
falha fechado para nao acessar uma rota indefinida.

TDD RED: os casos novos falharam ao compilar, pois o resultado ainda nao expunha
`RouteGenerateIndex` nem os NPCs guardavam a origem de capacidade. GREEN:
Engine.Tests `490/490`. A build completa sem incremental passou com 0 erros e o
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info`
identificou SDK 10.0.401 e saiu 0.
O smoke TCP de tres peers passou com grupo relocacionado entregue em ordem a
dois peers dentro da view 33x33 e nenhum frame ao peer externo
(`relocated-generated-group=relayed`). Sem Host autenticado ou E2E visual de
`WYD.exe`; sem deploy, Site/producao, commit ou push.

Proximo: auditar ramos especiais restantes de `GenerateMob`; depois completar
metodos ativos de `CMob`, timer e `GetFunc`.
### MOB-AI-001 Task 5AY - drop temporizado DUNGEONEVENT (concluida localmente 20/10/2026)

TMProject 7.69 (`TMSrv/ProcessSecMinTimer.cpp:2865-2887`) e W2PP
(`ProcessSecMinTimer.cpp:2762-2782`) executam o evento depois do reroll de
`MinuteGenerate >= 3800`, ainda que `GenerateMob` nao crie grupo. Ambas usam a
mesma tabela de 30 coordenadas e o mesmo vetor ponderado de itens:
`[421,422,423,419,420,685,691,696,696,696]`. A ordem de aleatoriedade e: reroll
do intervalo, posicao, quantidade 5..9, item ponderado, `SetItemBonus`, rotacao
0..3 e tentativa de `CreateItem`, para cada premio. `DUNGEONEVENT` inicia em 1
nas duas sources.

O C# executa esse ramo em `LegacyNpcGenerationRuntime` somente quando o evento
esta habilitado e o `ItemList` foi carregado. A opcao `--dungeon-event true|false`
tem default `true`; com ItemList ausente o Host registra que os premios serao
ignorados, sem abrir login adicional ou falhar o scheduler. Cada item inicia
zerado, recebe `LegacyItemBonusMath.Apply(level: 0, dropBonus: 0)` e usa a mesma
fonte de rolls do schedule. A posicao usa `WorldHub` e o equivalente de
`GetEmptyItemGrid`: primeiro o tile pedido, depois raio 1 com y externo/x
interno; a colocacao rejeita terreno 127, itens de mapa estaticos e drops
dinamicos ocupados. Cada item usa estado dinamico com `Delay=90` e captura os
viewers inclusivos 33x33 no momento do spawn; `LegacyGroundItemRelay` emite
`CreateItemConfirmation` somente a esses recipients.

TDD RED confirmou a ausencia do efeito no scheduler de 3800; GREEN: Engine.Tests
`490/490`, incluindo ordem dos rolls, ponderacao e bonus do item 419, opt-out,
ItemList ausente, tile ocupado/estatico, terreno bloqueado e captura da view.
Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. `--dungeon-event-smoke` passou por TCP
loopback com cinco itens 696, `CreateItem` valido, peer na borda 33x33 e nenhum
frame no peer externo. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por
acesso negado ao Service Control Manager. Sem Host autenticado, E2E visual de
`WYD.exe`, deploy, Site/producao, commit ou push.

### MOB-AI-001 Task 5AZ - HP inicial de NewbieEventServer (concluida localmente 20/10/2026)

TMProject 7.69 (`TMSrv/Server.cpp:4485-4486`, `4624-4625`) e W2PP
(`Server.cpp:3616-3617`, `3755-3756`) aplicam `3 * MaxHp / 4` ao HP atual do
leader e de cada follower gerado quando `NewbieEventServer` esta ativo e o nivel
e menor que 120. O C# isola a regra em `LegacyNewbieEventNpcHpMath` e aplica o
resultado ao snapshot depois da inicializacao de score, sem alterar MaxHp.

TDD RED: Engine.Tests `489/490`, falha reproduzida no HP inicial; GREEN
`490/490`. Build completa sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `--mob-ai-smoke` passou como regressao de
grupo/relay. `dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso
negado ao Service Control Manager. Sem Host autenticado, E2E visual, deploy,
Site/producao, commit ou push.

### MOB-AI-001 Task 5BA - variacao de Clan durante GenerateMob (concluida localmente 20/10/2026)

TMProject 7.69 (`TMSrv/Server.cpp:4493,4633`) e W2PP
(`TMSrv/Server.cpp:3623,3763`) aplicam a regra depois da inicializacao do
score/HP e antes de `GetEmptyMobGrid`: quando `MOB.Clan==1`, consomem um
`rand()%10`; o resultado 1 muda Clan para 2. O short-circuit de `&&` preserva
o estado do RNG para os demais Clans. Cada leader/follower elegivel recebe seu
proprio roll.

`WorldHub` aplica o roll depois do ajuste de HP de NewbieEvent e antes da busca
de tile; `TryCreateNamedNpc` nao passa por essa regra. O callback valida o
intervalo `[0,9]`, e clans diferentes de 1 nao consomem roll. Os testes
deterministas cobrem conversao 1/10, roll sem conversao, ordem/contagem por
membro e ausencia de roll para outros clans.

Auditoria dos demais ramos pre-geracao: Sombra Negra, BossCamp, Altar of King e
configuracao de torre RvR dependem de estado dos respectivos eventos, ausente
da API C# atual. TMProject `WarOfTower::CreateTower` apenas registra guild/level
da torre ativa; W2PP `CWarTower::GGenerateMob` tambem forca HP 10.000, sem
equivalente no contrato 7.69. Esses ramos ficam adiados ate os donos de estado
serem portados e testados.

TDD RED: Engine.Tests `489/490`, com a falha esperada no caso novo de variacao
de Clan; GREEN `490/490`. Build completa sem incremental: 0 erros e CS8604 preexistente
em `AttackDivineBlessingTests.cs:256`. O smoke TCP `--mob-ai-smoke` passou com
geracao de grupo e relay view-scoped como regressao; nao valida diretamente o
campo Clan no wire. `dotnet --info`: SDK 10.0.401, exit 0. Sem Host autenticado,
E2E visual de `WYD.exe`, deploy, Site/producao, commit ou push.

Task 5BB logo abaixo fecha o despawn terminal de `RouteType=3`; depois dele,
auditar outra chamada ativa de `CMob`/`ProcessSecMinTimer` e mapear seu dono de
estado em C#.

### MOB-AI-001 Task 5BB - RouteType 3 terminal despawn (concluida localmente 20/10/2026)

TMProject 7.69 `CMob::StandingByProcessor` (`CMob.cpp:153-163`) e W2PP
(`CMob.cpp:170-180`) concordam no endpoint de `RouteType=3`: com
`SegmentProgress==4` e posicao no `Segment`, `WaitSec>0` subtrai 6 e retorna sem
avancar a rota; quando `WaitSec<=0`, retorna o flag `0x10000`. O
`ProcessSecMinTimer` trata esse flag (`TMProject:2088-2099`, `W2PP:2004-2015`):
para `GenerateIndex` 4291..4301 (`RUNEQUEST_LV6_MOB_INITIAL..END`), incrementa
`Pista[6].Party[0].MobCount` apenas se a contagem for 1..99; em seguida chama
`DeleteMob(index,3)`. `DeleteMob` envia `MSG_RemoveMob` por `GridMulticast`.

O planner agora emite `Despawn` para esse estado terminal. Sob o lock do
`WorldHub`, a aplicacao captura recipients na view inclusiva 33x33, remove o
NPC gerado, limpa referencias-alvo, e atualiza a contagem do Pista quando o
indice e elegivel. `LegacyMobAiRelay` transmite o frame `RemoveMob(type=3)` para
os recipients capturados. NPCs com outros indices ainda sao removidos, sem
alterar o contador; contagens 0 e 100 tambem nao incrementam.

TDD RED: Engine.Tests `489/490`, com falha no resultado terminal do planner;
GREEN `490/490`, incluindo indices 4291/4301, contagens 0/1/99/100,
remocao, captura da view e contador. Build completa sem incremental: 0 erros e
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; o smoke TCP
`--mob-ai-smoke` passou com `route3-despawn=relayed`, dois viewers na borda
inclusiva e nenhum frame fora da view. `dotnet --info` identificou SDK
10.0.401 e saiu 1 por acesso negado ao Service Control Manager. Publish
self-contained win-x64 local: 200 arquivos/84.018.579 bytes em
`Server/Backup/Build/MOB-AI-001-Task5BB-20261020`; ZIP SHA-256
`E186ADDD004005E5A15C4CCC5D51EBE6726566E10266EE26F4E689E27B127151`.
SSH `wyd-vps` expirou em TCP/22; sem transferencia, restart ou check UP/PVP.
Sem Host autenticado ou E2E visual do `WYD.exe`, Site/producao, commit ou push.

## MOB-AI-001 Task 5BC - recuo de NPC ranged (20/10/2026)

TMProject 7.69 `CMob::BattleProcessor` (`Backup/Tools/ReferenceSources/
TMProject2GlobalClient/Servidor/Source/Code/TMSrv/CMob.cpp:296`) e W2PP
(`W2PP/Source/Code/TMSrv/CMob.cpp:312`) concordam no gate: quando
`AttackRange >= 4`, distancia <=4 e `Rand > BaseDex`, retornam `0x100` para
que `ProcessSecMinTimer` trate o recuo. `GetTargetPosDistance` nas duas fontes
(`TMProject:929-991`, `W2PP:892-954`) calcula offsets independentes de 1..2
para longe do alvo, chama `GetEmptyMobGrid(0)`, tenta as distancias de rota do
speed ate zero, e testa ocupacao/rebusca. Se `AttackRun & 0xF` e zero, ou se a
rota nao gera primeiro passo, `NextX/Y` fica na posicao atual. O timer compara
com a posicao corrente e nao envia Action nesse caso. Para um destino valido,
envia Action com speed atual e `Effect=0` (`TMProject:
ProcessSecMinTimer.cpp:2439-2455`, `W2PP:2423-2439`).

O C# acrescenta `Retreat` ao planner, resolve a rota no snapshot do WorldHub e
revalida o movimento sob o gate antes de aplicar a posicao. O relay comum envia
Action para a view de origem, RemoveMob(type=0) para quem sai, e CreateMob mais
Action para quem entra. O fallback sem rota foi alinhado ao legado: nao inventa
offset ao redor do alvo e nao aplica movimento.

RED/GREEN Engine.Tests `489/490` -> `490/490`, cobrindo gate DEX/alcance/
distancia, passo e recipients, e rota bloqueada sem movimento. Build completa
sem incremental: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. O smoke TCP `--mob-ai-smoke` passou com
`ranged-retreat=relayed`; o peer na borda recebe Action seguido de
RemoveMob(type=0), o alvo recebe Action e o peer fora da view nao recebe frame.
`dotnet --info` identificou SDK 10.0.401 e saiu 1 por acesso negado ao Service
Control Manager. Publish self-contained win-x64 local: 197 arquivos e
83.577.979 bytes em `Server/Backup/Build/MOB-AI-001-Task5BC-20261020`; ZIP
SHA-256 `6564B0B167272013877A864DC991055F227D34863E1F31B9909D7197BCA05D79`.
SSH `wyd-vps` expirou em TCP/22; sem transferencia, restart ou check UP/PVP.
Sem Host autenticado ou E2E visual do `WYD.exe`, deploy, Site/producao, commit
ou push.

## MOB-AI-001 Task 5BD - RouteType 5 gerado com Leader invalido (20/10/2026)

TMProject 7.69 `CMob::BattleProcessor` (`Backup/Tools/ReferenceSources/
TMProject2GlobalClient/Servidor/Source/Code/TMSrv/CMob.cpp:216-236`) e W2PP
(`W2PP/Source/Code/TMSrv/CMob.cpp:233-253`) chamam primeiro
`SelectTargetFromEnemyList`. Sem alvo selecionado, entram em Peace e retornam;
nao ha despawn. Com alvo, `RouteType==5` e `Leader<=0 || Leader>=MAX_USER`
retornam `0x20`; `ProcessSecMinTimer` converte o sinal em
`DeleteMob(index,3)` (TMProject `:2173-2179`; W2PP `:2089-2095`). A verificacao
precede BaseInt e demais rolls de combate nas duas fontes.

No port, `LegacyMobAiRuntime` deixa o shard executar somente os NPCs gerados
RouteType 5 cujo Leader e invalido; a captura/planner do `WorldHub` gera
`Despawn` e aplica a remocao sob o gate, com recipients da view capturados para
`RemoveMob(type=3)`. O ramo nao consome RNG. Summons de jogador continuam no
fluxo dedicado `TryApplyAllSummonBattleOpportunities`; RouteType 5 gerado com
Leader valido permanece fora deste recorte. O caso sem alvo fica na transicao
Combat -> Peace ja coberta pelo planner.

TDD RED/GREEN: a primeira assercao encontrou `Unsupported` para Leader=-1; a
correcao alcançou Engine.Tests `490/490`. Os testes cobrem Leader -1/0/1000,
Leader=1, ausencia de alvo, aplicacao WorldHub/view e execucao pelo shard sem
sortear RNG. O full build sem incremental concluiu com 0 erros e CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; build E2E, 0 avisos/erros.
Smoke TCP em loopback passou com `route5-invalid-leader-despawn=relayed`: dois
peers na view receberam RemoveMob(type=3), o peer fora nao recebeu frame.
`dotnet --info`: SDK 10.0.401, exit 0. Publish self-contained win-x64 local:
200 arquivos/84.021.447 bytes em
`Server/Backup/Build/MOB-AI-001-Task5BD-20261020`; ZIP SHA-256
`A943BBCA72B14FCDEA76AC9406C812974F36ECABFD08B712BC880C922566AEB2`.
SSH `wyd-vps hostname` recebeu Permission denied no sandbox e expirou na
tentativa elevada; sem transferencia, restart ou check UP/PVP. Sem Host
autenticado, E2E visual, Site/producao, commit ou push.

## MOB-AI-001 Task 5BE - Runequest Lv6 area cleanup (20/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:1940,2133` and W2PP
(`ProcessSecMinTimer.cpp:1856,2049`) remove active mobs with
`DeleteMob(index,3)` inside the inclusive rectangle X=3423..3442 and
Y=1492..1511, except the Runequest Lv6 boss. The Peace check runs after the
zero-HP deletion and `ProcessAffect`, before `StandingByProcessor`; the Combat
check runs after zero-HP deletion and the scheduled affect tick, before target
selection and `BattleProcessor`.

The source constants intentionally diverge. TMProject's active TMSrv
`Basedef.h:365-367` sets boss 4283 and the second-wave counter range 4284..4290.
W2PP `Basedef.h:462-464` uses boss 5767 and range 5768..5774. The port follows
the selected 7.69 contract. The existing RouteType 3 terminal counter remains
4291..4301; the planner now records why a mob despawned so the two Pista counter
ranges stay isolated and an unrelated RouteType 5 despawn cannot increment
either one.

Generated NPCs reach the area predicate before Peace/Combat decisions. The
runtime allows area cleanup through its ordinary Clan 4, RouteType 5, and
empty-target filters and supplies zero-valued rolls, preserving the source's
no-random-draw ordering. `WorldHub` removes under its gate and captures the
origin view for `RemoveMob(type=3)`. The separate Type 11 summon paths perform
the same pre-follow/pre-target cleanup in Peace and Combat, then relay the
captured removal view; summons have no generated-NPC index and do not increment
the Pista counter. Other non-generated mob categories remain outside these
two C# runtime paths.

TDD RED/GREEN: Engine.Tests `490/490`. Coverage includes inclusive corners,
four outside edges, boss exemption, Peace/targetless Combat, the second-wave
counter bounds, RouteType 3 counter isolation, invalid-Leader counter
isolation, zero RNG draws, and Type 11 summon removal in both modes. Full
solution build without incremental completed with exit 0 and the pre-existing
CS8604 at `AttackDivineBlessingTests.cs:256`; `dotnet --info` identified SDK
10.0.401 and exited 0. TCP loopback `--mob-ai-smoke` passed with
`pista-lv6-bounds-despawn=relayed`: both in-view peers received
`RemoveMob(type=3)`, and the outside peer received no frame. Local
self-contained win-x64 publish: 200 files/84,024,407 bytes in
`Server/Backup/Build/MOB-AI-001-Task5BE-20261020`; ZIP SHA-256
`3ACCAEE0855BA17434BF89708F656E38AD6152D1BE41398771C88B5542771E6D`.
The sandbox SSH check to `wyd-vps` was denied; the prior elevated retry timed
out, so no transfer, restart, or UP/PVP check occurred. No authenticated Host,
visual `WYD.exe` E2E, Site/production change, commit, or push.

## MOB-AI-001 Task 5BF - Runequest Lv2 movement exit cleanup (20/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:2042-2045` and W2PP
(`ProcessSecMinTimer.cpp:1958-1961`) perform the check in the Peace movement
branch after `GridMulticast`. That call updates `TargetX/TargetY` to the step
just executed (`SendFunc.cpp` in each source), so the deletion occurs when the
NPC enters inclusive X=3373..3407, Y=1420..1453, rather than merely when its
final route target lies there. TMProject TMSrv `Basedef.h:348-349` defines
generated IDs 4306..4364; W2PP `Basedef.h:445-446` diverges to 5790..5848.
The port follows the selected 7.69 values. This movement exit is independent
of the Lv6 area cleanup and does not increment its counter.

The C# path is gated on the original Peace mode, checks the post-move tile and
generated ID range under `WorldHub`, captures the destination view, and returns
movement and type-3 removal in one outcome. `LegacyMobAiRelay` relays Action
before `RemoveMob(type=3)`: viewers in both views see Action then removal;
viewers entering on this step receive CreateMob, Action, then removal; peers
outside both origin and destination views receive no frame. Other movement modes
are excluded.

TDD RED/GREEN: Engine.Tests `490/490`, including both inclusive corners,
outside IDs/bounds, view recipients, and removal only after eligible movement.
Full solution build without incremental exited 0 with the pre-existing CS8604
at `AttackDivineBlessingTests.cs:256`; E2E project build had 0 warnings/errors.
TCP loopback `--mob-ai-smoke` passed with
`pista-lv2-exit=movement-then-remove` and view-scoping. `dotnet --info`
identified SDK 10.0.401 and exited 0. Local self-contained win-x64 publish:
200 files/84,025,083 bytes at
`Server/Backup/Build/MOB-AI-001-Task5BF-20261020`; ZIP SHA-256
`56E5B7D381AEF7055AB3648276454E47B1CB96BADA92060D0CF743056392588C`.
No transfer/restart/UP-PVP check, authenticated Host, visual `WYD.exe` E2E,
Site/production change, commit, or push.

## MOB-AI-001 Task 5BG - RouteType 0 Merchant activation (20/10/2026)

TMProject 7.69 `TMSrv/CMob.cpp:542-567` and W2PP
`Server/W2PP/Source/Code/TMSrv/CMob.cpp:556-581` agree that
`CMob::SetSegment` handles `RouteType==0` when `SegmentProgress==5`: it returns
to progress 4, sets mode 4, copies `MOB.Merchant` to `MOB.BaseScore.Merchant`,
then calls `GetCurrentScore(MAX_USER)`. TMProject `Basedef.cpp:4290` and W2PP
`Basedef.cpp:4673` compose current Merchant as
`(CurrentScore.Merchant & 0xF0) | MOB.Merchant`.

The C# planner already stops at the final RouteType 0 point but did not apply
the score transition. It now marks that decision; `WorldHub` revalidates it and
updates only the generated NPC snapshot, copying the top-level Merchant into
BaseScore and preserving the current score's high nibble. RouteType 1 and other
route types do not receive this transition. An adjacent audit found the
`Route[len]` suffix path has no effect on normal generated routes: both
TMProject `Basedef.cpp:5812` and W2PP `Basedef.cpp:6194` zero the full route
buffer in `BASE_GetRoute`, so `strlen(Route)` points at NUL and the derived
value is rejected by `SetSegment`; `_MSG_Action` has the same rejection. No wire
frame or commerce/payment flow is introduced.

The 23 active `Formation=4` records in the selected 7.69 `NPCGener.txt` have
`MinGroup=MaxGroup=1` or both zero. The spawn loop therefore creates at most
follower index 0; its five route-point lookups flatten to table indexes 4, 6,
8, 10, and 12 within the 120-byte `char[5][12][2]`. The C# catalog uses the
same linear offsets for these active cases. Its zero fallback applies only to
hypothetical out-of-array inputs and does not claim equivalence to C++ UB.

TDD RED/GREEN: planner coverage asserts that only RouteType 0 terminal stop sets
the marker. WorldHub coverage walks all five route points and verifies the
merchant score stays inactive before terminal, then checks BaseScore Merchant
and the composed CurrentScore after terminal. Engine.Tests `490/490`; full
no-incremental solution build exited 0 with pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`; E2E build 0 warnings/errors. TCP
`--mob-ai-smoke` passed as general relay regression, but does not exercise this
Merchant transition. SDK 10.0.401; `dotnet --info` identified the SDK and
exited 0. No package/publish, authenticated Host, visual
`WYD.exe` E2E, Site/production change, commit, or push.

## MOB-AI-001 Task 5BH - passive generated-group aggro (20/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:1972` e W2PP
`ProcessSecMinTimer.cpp:1888` chamam `SetBattle` nos membros da `PartyList`
depois que o leader gerado adquire alvo no estagio Peace. Os callsites do timer
exigem `Mode != 0` e HP positivo antes de invocar `SetBattle`; a funcao rejeita
modo vazio, aplica o limite retangular inclusivo de +/-23 em cada eixo, muda
para `MOB_COMBAT` e tenta adicionar o alvo a `EnemyList`. Ambas as referencias
concordam.

O C# representa essa relacao com `LegacyWorldNpc.LeaderId`. Depois do sucesso do
`TrySetNpcBattle` do leader no branch passivo `Aggro`, `WorldHub` aplica aos
followers gerados sob o mesmo lock o estado Combat e a insercao do alvo com os
mesmos gates de HP e distancia. O teste `PassiveGeneratedNpcLeaderAggroFansOutToItsFollowers`
confirma o alvo compartilhado, a exclusao de outro grupo dentro do alcance, os
limites de 23/24 tiles e a exclusao de follower morto. O caso nao altera ataques
ja em combate e nao cria relay, pois a
rotina legada tambem so muda o estado interno.
O loop legado separado para os summons da party do alvo e limitado a
`Target < MAX_USER`. No C#, `TrySetNpcBattle` do leader representa esse efeito
uma vez somente para alvo jogador; a chamada passiva suprime-o para alvo summon.
O fan-out aos followers nao chama `TrySetNpcBattle` para cada um, pois isso
duplicaria o efeito de party. Essa adaptacao preserva a elegibilidade do alvo
sem declarar equivalencia de ordem entre os dois loops.

O teste de regressao tambem seleciona um summon como alvo passivo e confirma
que o follower recebe esse alvo, mas os outros summons da party do alvo nao
recebem o NPC na `EnemyList`. `TrySetNpcBattle` publico mantem seu comportamento
padrao para os outros chamadores. A limpeza de follower morto e de `PartyList`
permanece fora do escopo aprovado.

Engine.Tests `490/490`; build completa sem incremental exit 0, com um CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; build E2E exit 0 com 0 avisos
e erros. `dotnet --info` identificou SDK 10.0.401 e saiu 0. O
`--mob-ai-smoke` existente e apenas regressao geral de rede/relay e nao cobre
essa transicao interna. Sem Host autenticado ou E2E visual.

## MOB-AI-001 Task 5BI - passive cleanup of dead generated followers (21/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:1972-1984` e W2PP
`ProcessSecMinTimer.cpp:1888-1900` percorrem `PartyList` depois que o leader
adquire alvo passivamente. Para cada membro com ID de NPC, ambos chamam
`SetBattle` somente quando `Mode != 0` e HP positivo. No ramo restante, executam
`DeleteMob(type=1)` se o modo ainda esta ativo e zeram o slot. Esse ramo de
limpeza nao verifica distancia ao alvo; o limite espacial pertence ao caminho
`SetBattle` dos membros vivos.

`DeleteMob` em TMProject 7.69 (`Server.cpp:8841`) e W2PP (`Server.cpp:7809`)
monta RemoveMob e usa `GridMulticast` na posicao do NPC; para tipo diferente de
0 tambem zera HP, desativa o modo, limpa a grade e chama `RemoveParty`. O C# nao
mantem array `PartyList`: a relacao de membro gerado e `LegacyWorldNpc.LeaderId`
e o total ativo do gerador e calculado a partir dos NPCs registrados. Por isso,
o branch ativo morto remove o follower do WorldHub, o que tira sua relacao da
enumeracao do leader e reduz a contagem derivada.

O branch passivo do leader agora processa mortos antes do range check dos vivos.
Para cada follower com `Mode != LegacyNpcMode.Empty` e HP zero, captura os
recipients da sua propria view, remove-o via `RemoveNpcLocked` e acrescenta um
`LegacyGeneratedNpcRemovalOutcome` de tipo 1 ao `AdditionalRemovals`. O relay
serializa cada resultado como `RemoveMob(type=1)` sem expandir a view capturada.
Followers vivos ainda usam o range inclusivo de +/-23 em cada eixo; membros de
outro grupo continuam excluidos.

O ramo fonte `Mode == 0` apenas limpa o slot sem chamar `DeleteMob`. C# modela
essa membership separada por `IsGeneratedGroupPartyMember`, independente de
`LeaderId`: o ramo muda o flag para false e preserva NPC e `LeaderId`, sem
serializar um array nem a ordem dos slots da `PartyList`. O teste reativa o NPC
em modo Peace e repete o aggro; ele nao reentra no fan-out do leader.

TDD RED/GREEN: o teste WorldHub reproduz dois followers mortos ainda presentes
antes da correcao e um follower vazio que volta a receber aggro apos reativacao;
GREEN remove os dois mortos e limpa a membership do terceiro sem apagar o NPC.
Um morto esta fora da distancia do alvo; cada um tem sua view capturada.
`Engine.Tests` `490/490`. O `--mob-ai-smoke` TCP usa tres peers loopback: alvo e observer proximo
recebem somente a remocao do follower proximo; o observer distante recebe
somente a remocao do follower distante; nenhuma view recebe um frame excedente.
Marcador: `passive-group-dead-cleanup=relayed`. Build completa sem incremental:
exit 0, 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; E2E smoke passou; `dotnet --info` identificou
SDK 10.0.401 e saiu 0. Publish self-contained local em
`Backup/Build/MOB-AI-001-Task5BI-20261021`: 200 arquivos / 84.029.439 bytes;
ZIP SHA-256 `263607EAE6C00DD9F228896C1A6F3EC2D7C439B87B574E3C79E3DEC3464DBA31`.
O scanner local nao encontrou nome suspeito de credencial nem valor de segredo
em arquivos de configuracao publicados. Probe SSH read-only `wyd-vps` ficou
inacessivel no sandbox e elevado; sem transferencia, restart ou check UP-PVP.
Nao houve Host autenticado, E2E visual do `WYD.exe`, Site/producao, commit ou
push.

## MOB-AI-001 Task 5BJ - cleanup of dead generated Idle/Peace NPCs (21/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:1913-1938` and W2PP
`ProcessSecMinTimer.cpp:1832-1853` scan generated mob IDs across six timer
shards. In both sources, before affect processing or Peace AI, `MOB_IDLE` and
`MOB_PEACE` with `CurrentScore.Hp <= 0` call `DeleteMob(index, 1)` and skip the
remaining branch. `Server.cpp::DeleteMob` in TMProject 7.69 (`:8841`) and W2PP
(`:7809`) multicasts `RemoveMob(type=1)` at the NPC tile, clears its mob-grid
entry, marks it empty and removes it from the party/generator count. The
behavior is limited to these two modes; Combat has its own timer path.

C# adds `LegacyNpcMode.Idle=3` and checks dead generated NPCs at the start of
each Peace shard visit. `WorldHub.TryRemoveDeadGeneratedNpc` validates that the
NPC is still registered, generated, not autotrade, in Idle/Peace and at HP
zero; under the world gate it captures the current view, removes the NPC and
returns a type-1 removal outcome. The runtime relays that captured outcome
before moving on. This check precedes the Clan 4 filter, matching the source's
order. Alive Idle NPCs remain outside this port slice; HP-zero Combat NPCs are
not removed by this branch. The C# shard cadence continues to use the existing
runtime interval.

TDD RED reproduced a dead Peace NPC retained after its planner returned Idle,
and a dead Idle NPC skipped by the runtime (Engine.Tests `489/490`). GREEN
removes both with type 1 and only the recipients in each captured view;
Engine.Tests `490/490`. The TCP loopback smoke runs IDs aligned to the same
Peace shard, verifies both modes, target and in-view observer frames, no frame
to the outside peer, and marker `dead-peace-idle-cleanup=relayed`. Full
no-incremental solution build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`; the E2E smoke command passed. SDK 10.0.401;
`dotnet --info` exited 1 because the runner cannot open Service Control
Manager.

Self-contained local publish: 200 files / 84,030,139 bytes; ZIP SHA-256
`A843966F9D5C3FA726C8CA023ECF17DC9881AB44142C78C88C8E59628C40E6CD`. The
publish restore used `NuGetAudit=false`, so its vulnerability audit is
inconclusive; the local config scan found zero credential-like names and zero
secret-value matches. Read-only SSH `wyd-vps` was denied in the sandbox and
timed out when elevated; no transfer/restart/UP-PVP check. No authenticated
Host, visual `WYD.exe` E2E, Site/production change, commit or push.

## MOB-AI-001 Task 5BK - generated NPC Life Aura Type 17 (21/10/2026)

TMProject 7.69 `Server.cpp::ProcessAffect` and W2PP `Server.cpp::ProcessAffect`
agree on the Type 17 timer effect: add `AffectLevel / 2 + AffectValue` to HP,
clamp to `[1, MaxHp]`, record the HP delta, and request score refresh only when
HP changes or the affect expires. For durations below `32400000`, decrement
`Affect.Time`; when its unsigned result reaches zero, clear the affect fields.
The timer calls `ProcessAffect` for `MOB_IDLE` and `MOB_PEACE` after dead-NPC
cleanup and before the remaining AI branch.

C# applies only Type 17 slots to generated, alive NPCs in Idle/Peace, inside an
atomic `WorldHub` transition. It relays `SetHpDam` first, then the W2PP
`UpdateScore` layout adapted to the 7.69 contract, only when HP changed or the
effect expired. Recipients are captured at the NPC view under the world gate.
Other affect types, Combat timer calls, and non-generated NPCs are outside this
slice.

TDD RED reproduced the unprocessed HP/timer on a generated Peace NPC; GREEN
Engine.Tests `490/490` checks healing, max-HP clamp, timer decrement/expiry,
field clearing and score-refresh decision. The three-peer TCP loopback smoke
confirms `SetHpDam` then `UpdateScore` reaches only in-view peers and emits
`life-aura=hp-score-relayed`. Full no-incremental solution build: 0 errors and
pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exited 1 because the runner cannot open Service Control
Manager. No package, publish, transfer, authenticated Host, visual `WYD.exe`
E2E, Site/production change, commit or push.

## MOB-AI-001 Task 5BL - generated NPC poison Type 20 timer (21/10/2026)

TMProject 7.69 `Server.cpp::ProcessAffect` and W2PP `Server.cpp::ProcessAffect`
agree on the generated-NPC branch for Type 20; active SkillData rows 20, 21,
and 40 select this timer type. The branch starts with
`AffectValue=100`, subtracts signed `MOB.LearnedSkill / MAX_USER` for an NPC,
then divides the value by 10. `HpLose` is the truncated integer result of
`(100 - AffectValue) / 100.0`; NPC `Perda` is
`10 * (100 - LearnedSkill / MAX_USER)`, `Perdamax` is `Hp - Perda`, and HP is
multiplied by `HpLose`, raised to `Perdamax` when needed, then clamped to
`[1, MaxHp]`. Both sources use the same NPC calculation; their different
player defaults (`Perda=500` in 7.69 and `Perda=1000` in W2PP) do not affect
this port.
As in Type 17, finite time below `32400000` decrements once and the affect is
cleared when its unsigned time reaches zero. The timer processes Idle/Peace
NPCs after dead-NPC cleanup.

C# applies only Type 20 slots to generated, alive NPCs in Idle/Peace, inside
the atomic WorldHub timer transition. It reads `LearnedSkill` as signed
little-endian 32-bit from the legacy MOB snapshot. The Host relays `SetHpDam`
with the signed HP delta, then the 7.69 `UpdateScore` adapter when HP changed
or the effect expired; recipients are captured under the world gate. Type 17
remains supported by the same transition. Other affect types, Combat timer
calls, and non-generated NPCs remain outside this slice.

TDD RED reproduced the unprocessed Type 20 HP and duration; GREEN Engine.Tests
`490/490` verifies the NPC damage cap, signed delta, timer decrement/expiry,
field clearing and score-refresh decision. The three-peer TCP smoke confirms
the negative `SetHpDam` followed by `UpdateScore` reaches only peers in the
captured view and emits `poison=hp-score-relayed`. Full no-incremental solution
build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exited 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BM - peaceful summon Type 24 lifetime (21/10/2026)

TMProject 7.69 `GenerateSummon` (`Server.cpp:4000-4006`) and W2PP
(`Server.cpp:3193-3199`) create ordinary skill summons with affect Type 24,
value/level zero and time 20; summon IDs 28..37 use time `2000000000`. Both
`ProcessAffect` implementations decrement finite affect time below
`32400000`; at zero, Type 24 on a mob (`idx >= MAX_USER`) calls
`DeleteMob(idx, 3)`, clears the affect and requests score refresh. The Peace
timer calls `ProcessAffect` before its route/follower branch. Combat timer
calls remain a separate cadence and are not part of this slice.

C# already created Type 24 in `TryGenerateSummons`, but the route-five Peace
step did not consume its lifetime. The step now decrements finite Type 24 once
for each selected peaceful summon opportunity, preserves the long persistent
duration, and removes an expired summon atomically through `RemoveSummonLocked`.
It captures view recipients and relays `RemoveMob(type=3)` before any follower
movement. Summons with enemies or a live selected target stay out of the Peace
branch; Combat lifetime processing is still open.

TDD RED reproduced the summon remaining after its twentieth Peace timer
opportunity; GREEN Engine.Tests `490/490` verifies it remains through the
nineteenth and is removed on the twentieth with only captured-view recipients.
The three-peer TCP smoke confirms the two in-view peers receive
`RemoveMob(type=3)`, the outside peer receives no frame, and emits
`summon-expiry=type3-relayed`. Full no-incremental solution build: 0 errors and
pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exited 1 because the runner cannot open Service Control
Manager. No package, publish, transfer, authenticated Host, visual `WYD.exe`
E2E, Site/production change, commit or push.

## MOB-AI-001 Task 5BN - Combat summon Type 24 lifetime (21/10/2026)

TMProject 7.69 processes affects for a Combat mob when
`index % 16 == SecCounter % 16` (`ProcessSecMinTimer.cpp:2130-2131`); W2PP does
the same (`:2046-2047`). Both timer paths use a 200 ms steady interval and
`ProcessAffect` decrements finite affect times below `32400000`; Type 24 at zero
deletes a mob with `DeleteMob(idx, 3)` (`Server.cpp:6913` / W2PP `:5843`). This
gives each Combat mob one affect opportunity per 16 timer ticks, about 3.2 s.

The C# summon battle loop is 800 ms. `TryApplyAllSummonBattleOpportunities`
now advances Type 24 every fourth loop call, after selecting a live target and
before the route-five teleport or attack branch. Finite durations decrement;
the persistent threshold is unchanged. On expiry, WorldHub captures recipients,
removes the summon and owner link, and returns `RemoveMob(type=3)` without an
attack outcome. This applies to summons of any route type while target-selected
in Combat. Summons without a live selected target remained outside the Combat
branch in Task 5BN; Task 5BQ extends Type 24 processing before target
selection. The C# groups eligible summons on a four-call phase rather than
preserving the source's per-mob 16-phase offset; the interval is aligned, while
the exact phase distribution remains a known difference.

TDD RED reproduced a Type 24 summon continuing to attack after its twentieth
Combat affect opportunity. GREEN Engine.Tests `490/490` verifies attack through
the first 19 affect opportunities and removal before attack on the twentieth;
the loopback smoke confirms two in-view peers receive `RemoveMob(type=3)` and
the outside peer receives no frame (`summon-combat-expiry=type3-relayed`). Full
no-incremental solution build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1 because
the runner cannot open Service Control Manager. No package, publish, transfer,
authenticated Host, visual `WYD.exe` E2E, Site/production change, commit or push.

## MOB-AI-001 Task 5BO - Combat Type 17/20 timer effects for generated NPCs (21/10/2026)

TMProject 7.69 `ProcessAffect` applies Type 17 and Type 20 after the optional
mount-attack branch, and the Combat timer calls it before the rest of the mob
battle processor when `index % 16 == SecCounter % 16`
(`ProcessSecMinTimer.cpp:2130-2131`, `Server.cpp:6033+`). W2PP uses the same
Combat callsite (`ProcessSecMinTimer.cpp:2046-2047`) and the same NPC formulas
in `ProcessAffect` (`Server.cpp:4968+`). The 7.69/W2PP player poison cap differs
but is outside this generated-NPC slice; their NPC `LearnedSkill` reduction
and `Perda` cap agree. Type 17 healing also agrees.

The C# timer logic for these affects was limited to generated alive NPCs in
Idle/Peace. `LegacyMobAiRuntime` now calls the combat timer path for each live
generated NPC before target/EnemyList filtering and before planning an attack.
Each NPC consumes one affect opportunity on its fourth Combat shard visit; an
NPC with an empty EnemyList therefore still advances its Combat affects without
producing an attack. The 4x800 ms cadence matches the source's 16x200 ms
interval. C# starts each per-NPC phase on its first Combat visit instead of
preserving the source's global `SecCounter` offset by legacy mob index.

WorldHub captures recipients with the HP/affect mutation. The server relays
signed `SetHpDam`, then the adapted 7.69 `UpdateScore`, only to those recipients.
TDD RED reproduced the skipped Combat effect; GREEN Engine.Tests `490/490`
covers Life Aura and Poison through an empty enemy list, the first three
non-consuming visits, the fourth-visit tick, expiry and no attack. The
three-peer loopback smoke confirms both effects, frame order and view-scoping
with marker `combat-affects-no-target=hp-score-relayed`. Full no-incremental
solution build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BP - Type 22 affect timer for generated NPCs (21/10/2026)

TMProject 7.69 `ProcessAffect` handles Type 22 in `Server.cpp:6279-6895`;
W2PP has the matching branch at `Server.cpp:5213-5827`. Both sources break
their target scans when `idx >= MAX_USER`, and only call
`ProcessClientMessage` for player indices. Thus a mob does not produce the
Thunder `MSG_Attack`; it still reaches common `LessAffect`, decrements finite
time below `32400000`, clears the full affect record at expiry, sets `upScore`,
and sends `UpdateScore`. W2PP and 7.69 agree on this mob behavior. SkillData row
37 is `Trovao` with AffectType 22; the task processes an already-present
Type 22 record and does not alter skill-bar selection or affect application.

`WorldHub.TryProcessGeneratedNpcTimerAffects` now tracks Type 22 in generated,
alive NPCs across Idle/Peace and Combat. While time remains, the snapshot is
updated without a client frame. At expiry the captured-view relay sends only
the existing adapted `UpdateScore`; `HpUpdateRequired=false` suppresses the
`SetHpDam` frame that the Type 17/20 branches require. No wire layout changed.

TDD RED reproduced the missing countdown on a generated Combat NPC. GREEN
Engine.Tests `490/490` covers the 4-shard cadence, timer decrement, no HP
change, no synthesized attack, expiry and score-only result. The three-peer
loopback smoke confirms score-only relay to the captured view, no frame to the
outside peer, and marker `combat-thunder=score-only-relayed`. Full
no-incremental solution build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BQ - Combat Type 24 with no selected target (21/10/2026)

TMProject 7.69 calls `ProcessAffect` for a live Combat mob on the matching
16-tick phase before reading `CurrentTarget` and running `BattleProcessor`
(`ProcessSecMinTimer.cpp:2130-2131`); if the battle result requires an attack
but has no valid target, the later guard continues at `:2181-2184`. W2PP has
the same order (`ProcessSecMinTimer.cpp:2046-2047`, `:2097-2100`). Type 24's
finite decrement, persistent-time threshold and `DeleteMob(type=3)` expiry are
the common mob behavior recorded in Task 5BM.

`WorldHub.TryApplyAllSummonBattleOpportunities` now processes the summon timer
on its existing fourth-call phase before `TrySelectSummonTarget`. A summon
without a live target therefore stays idle while its finite timer advances;
at zero, WorldHub captures its view, removes the summon and its owner link, and
returns a type-3 removal before any movement or attack. Summons 28..37 retain
their persistent time. This closes the no-target gap from Task 5BN; the shared
C# four-call phase remains an approximation of the source's per-index 16-tick
offset.

TDD RED reproduced the expiry call returning no outcome and retaining a
targetless summon after 20 Type 24 opportunities. GREEN Engine.Tests
`490/490` confirms no target, survival through opportunity 19 and removal on
20, with no attack. The three-peer loopback smoke verifies the two in-view
peers receive `RemoveMob(type=3)` and the outside peer receives no frame; its
marker is `summon-combat-no-target-expiry=type3-relayed`. Full no-incremental
solution build: 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BR - Type 23 affect timer for generated NPCs (21/10/2026)

TMProject 7.69 `ProcessAffect` handles Type 23 in
`Server.cpp:6589-6895`; W2PP has the corresponding `Aura Bestial` branch in
`Server.cpp:5522-5827`. Both branches construct Skill 52's area attack but call
`ProcessClientMessage` only for `idx < MAX_USER`. Generated NPC indices skip
that player attack path and continue through common `LessAffect`: finite time
decrements below `32400000`; expiry clears Type, Time, Value and Level, sets
`upScore`, then recalculates and sends score. Neither source applies HP change
in this Type 23 mob path. The timer callsites cover generated Idle/Peace and
Combat (`ProcessSecMinTimer.cpp:1922,1938,2131` in 7.69;
`W2PP/ProcessSecMinTimer.cpp:1840,1854,2047`).

`WorldHub.TryProcessGeneratedNpcTimerAffects` now recognizes Type 23 as a
timer-only affect beside Type 22. Idle/Peace advances on the existing timer
visit; Combat advances on the existing fourth 800 ms shard opportunity. While
the affect remains active, no frame is sent. At expiry, the existing captured
view receives only adapted `UpdateScore`; no `SetHpDam` or attack is generated.
No wire structure or player Type 23 path changed.

TDD RED reproduced an unchanged timer in the Peace and Combat NPC paths, and
the three-peer loopback smoke reproduced a Combat Type 23 that failed to
expire. GREEN Engine.Tests `490/490` covers both modes, unchanged HP, no attack,
timer decrement and score-only expiry. Smoke marker
`combat-bestial-aura=score-only-relayed` verifies `UpdateScore` for the two
in-view peers, no `SetHpDam`/attack and no frame to the outside peer. Full
no-incremental solution build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BS - Type 23 affect timer for summons (21/10/2026)

TMProject 7.69 `ProcessAffect` handles Type 23 at
`Server.cpp:6589-6895`; W2PP has the corresponding branch at
`Server.cpp:5522-5827`. Both call `ProcessClientMessage` for Skill 52 only when
`idx < MAX_USER`. Summon indices follow common `LessAffect`: finite time
decrements below `32400000`; expiry clears Type, Time, Value and Level, sets
`upScore`, and sends refreshed score. This mob path does not change HP or create
the player area attack.

`WorldHub.TryApplyRouteTypeFiveSummonPeaceStep` now processes Type 23 on its
existing peaceful summon opportunity. `TryApplyAllSummonBattleOpportunities`
processes it alongside the summon lifetime timer before target selection and
attack, once every fourth 800 ms opportunity. While active, the timer snapshot
is persisted without a frame. At expiry, the captured view receives only the
adapted `UpdateScore`; `SetHpDam`, attack and despawn are not produced by this
affect. No wire structure changed. The peaceful summon follower path remains
RouteType 5; other Peace summon routes are not included in this slice.

TDD RED reproduced the unchanged Type 23 timer on a peaceful summon. GREEN
Engine.Tests `490/490` covers Peace and Combat expiry, cleared affect, unchanged
HP and score-only outcome. TCP loopback smoke markers
`summon-peace-bestial-aura=score-only-relayed` and
`summon-combat-bestial-aura=score-only-relayed` verify `UpdateScore` reaches
in-view peers and no frame reaches the outside peer. Full no-incremental
solution build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BT - Type 17 Life Aura for RouteType 5 summons (21/10/2026)

TMProject 7.69 `ProcessAffect` applies the Type 17 heal at
`Servidor/Source/Code/TMSrv/Server.cpp:6189`; the common finite timer and expiry
are in `Server.cpp:6899`. Its Peace and Combat timer callsites are
`ProcessSecMinTimer.cpp:1938` and `:2131`. W2PP has the corresponding Type 17
branch at `Server/W2PP/Source/Code/TMSrv/Server.cpp:5124`, common expiry at
`:5831`, and timer callsites at `ProcessSecMinTimer.cpp:1854` and `:2047`.
Both references heal by `AffectLevel / 2 + AffectValue`, clamp HP to
`[1, MaxHp]`, record the actual HP delta, decrement finite affect time below
`32400000`, and clear Type, Time, Value and Level at expiry. Combat processes
the affect before selecting or executing the target attack.

The C# port applies Type 17 to live RouteType 5 summons on their existing Peace
and Combat affect opportunities. Peace combines the heal result with follower
movement when a step is due, and still returns the affect result when no step is
available. HP changes relay `SetHpDam` before `UpdateScore` to the view captured
for the summon; expiry while HP is already capped relays only `UpdateScore`.
Combat consumes the timer every fourth 800 ms opportunity and preserves a valid
attack in that same outcome. No wire structure changed; other summon routes and
other affect types remain outside this slice.

TDD RED/GREEN Engine.Tests: `490/490`. The TCP loopback smoke verifies Peace
healing, movement, expiry without movement, Combat healing/expiry, preserved
attack behavior in unit coverage, and no frames to the out-of-view peer. Markers:
`summon-peace-life-aura=hp-score-movement-expiry-relayed` and
`summon-combat-life-aura=hp-score-expiry-relayed`. Full no-incremental solution
build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BU - Type 20 Poison for RouteType 5 summons (22/10/2026)

TMProject 7.69 `ProcessAffect` applies Type 20 at
`Servidor/Source/Code/TMSrv/Server.cpp:6210-6253`; W2PP has the matching mob
calculation at `Server/W2PP/Source/Code/TMSrv/Server.cpp:5145-5187`. Both use
`LearnedSkill / MAX_USER`, `HpLose = (int)((100 - AffectValue) / 100.0)`, and a
mob damage cap of `10 * (100 - LearnedSkill / MAX_USER)`, then clamp HP to
`[1, MaxHp]`. The player default cap differs (`500` in 7.69, `1000` in W2PP),
but each source replaces it for mob indices, so this RouteType 5 summon slice
has no behavioral divergence. The Peace/Combat timer callsites are 7.69
`ProcessSecMinTimer.cpp:1938,2131` and W2PP `:1854,2047`; Combat processes the
affect before target selection and attack.

The C# port handles Type 20 on the existing RouteType 5 Peace opportunity and
every fourth Combat opportunity. It decrements finite time below `32400000`,
clears all eight affect bytes at expiry, relays `SetHpDam` before `UpdateScore`
to captured in-view peers, and keeps a valid Combat attack in the same
opportunity. Clan 4 summons also run the proven `TryLinkMountHpLocked` match:
face `315..344` must match owner equipment slot 14, whose index is `2330 +
face - 315`; the owner's encoded mount HP is updated and a private `SendItem`
is sent before the shared HP/score frames. No wire layout changed.

TDD RED reproduced the missing Peace summon Poison tick. GREEN Engine.Tests
`490/490` covers the default 1000 HP cap, the `LearnedSkill=40000` / `MAX_USER=1000`
600 HP cap, the HP floor of 1, finite timer decrement and expiry, Peace movement,
Combat attack preservation, and Clan 4 mount state. TCP loopback smoke marker
`summon-poison-mount=send-item-then-hp-score-relayed` verifies the owner item
frame precedes `SetHpDam` and `UpdateScore`; both in-view peers receive the
damage/score frames and the outside peer receives none. Full no-incremental
solution build: 0 errors and pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, publish,
transfer, authenticated Host, visual `WYD.exe` E2E, Site/production change,
commit or push.

## MOB-AI-001 Task 5BV - Type 22 Thunder for RouteType 5 summons (22/10/2026)

TMProject 7.69 `ProcessAffect` handles Type 22 in the thunder branch at
`Servidor/Source/Code/TMSrv/Server.cpp:6279-6899`; W2PP has the corresponding
branch at `Server/W2PP/Source/Code/TMSrv/Server.cpp:5213-5831`. The area attack
is emitted only for player indices. For mob indices, both sources continue to
`LessAffect`, which decrements finite time below `32400000` and clears the affect
when it reaches zero. The existing timer callsites are 7.69
`ProcessSecMinTimer.cpp:1938,2131` and W2PP `:1854,2047`.

`WorldHub.TryProcessSummonTimerAffectsLocked` now accepts Type 22 alongside the
already-porting timer-only affects. RouteType 5 Peace consumes the timer during
its existing follower step; Combat consumes it every fourth 800 ms opportunity,
before target selection. HP stays unchanged, no lightning attack is synthesized,
and unchanged countdowns emit no frame. Expiry clears all eight affect bytes
and sends only `UpdateScore` to the view captured at the summon position. No
wire layout changed.

TDD RED reproduced missing Peace expiry (`489/490`); GREEN Engine.Tests
`490/490` covers Peace/Combat countdown and expiry, score-only outcomes, and
preservation of an ordinary Combat attack. TCP loopback smoke
markers `summon-peace-thunder=score-only-relayed` and
`summon-combat-thunder=score-only-relayed` verify two in-view peers receive the
expiry score and the outside peer receives no frame. The smoke has no live Host
or visual client. Full no-incremental solution build succeeded with 0 errors
and pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exits 1 because the runner cannot open Service Control Manager.
SSH `wyd-vps` was denied in the sandbox and timed out during the elevated TCP/22
probe; no package, transfer, restart or UP/PVP check was completed. No publish,
authenticated Host, visual `WYD.exe` E2E, Site/production change, commit or push.

## MOB-AI-001 Task 5BW - Type 24 timer on generated NPCs (22/10/2026)

TMProject 7.69 `ProcessAffect` decrements finite affects below `32400000` and
calls `DeleteMob(idx, 3)` when Type 24 expires for `idx >= MAX_USER`
(`Servidor/Source/Code/TMSrv/Server.cpp:6913`). W2PP matches this branch
(`Server/W2PP/Source/Code/TMSrv/Server.cpp:5844`). The second/minute timer calls
`ProcessAffect` for generated Idle/Peace mobs at 7.69 `ProcessSecMinTimer.cpp`
`:1922,1938` and W2PP `:1840,1854`; Combat calls it on its index-mod-16 phase
at 7.69 `:2131` and W2PP `:2047`. No source divergence was found for this
branch.

`WorldHub.TryProcessGeneratedNpcTimerAffects` now includes Type 24 for live
generated NPCs in Idle/Peace and Combat. Peace follows its existing shard visit;
Combat decrements on every fourth 800 ms shard opportunity, before target
selection, including an empty enemy list. Finite time decrements while below
the persistent threshold; at zero, WorldHub captures the NPC view, removes it,
and returns `RemoveMob(type=3)`. The runtime skips the remaining Peace AI work
for the removed snapshot. Timer-only decrements send no frame, and no wire
layout changed.

TDD RED reproduced the missing Type 24 countdown. GREEN Engine.Tests `490/490`
covers the finite Peace countdown/expiry and targetless Combat cadence/expiry.
TCP loopback smoke marker `npc-type24-expiry=type3-relayed` verifies the two
in-view recipients receive a valid `RemoveMob(type=3)` and the outside peer
receives no frame. This is synthetic loopback evidence, without authenticated
Host or visual client. Full `WydCdk.Engine.slnx --no-restore --no-incremental`
build succeeded: 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager.

Local self-contained publish: 200 files/84,044,287 bytes in
`Server/Backup/Build/MOB-AI-001-Task5BW-20261022`; ZIP SHA-256
`313F043F483343E2316A3F1D46C703987A7D1DD826A8FA45BA3C76DC8F27F2D8`.
Filename/config scan found no credential-like filenames or JSON credential
keys. Read-only `ssh wyd-vps hostname` was denied in the sandbox and timed out
in the elevated probe; no transfer, restart or UP/PVP check. No visual E2E,
Site/production change, commit or push.

## MOB-AI-001 Task 5BX - Type 32 generic timer on generated NPCs and summons (22/10/2026)

TMProject 7.69 `ProcessAffect` and W2PP have no Type 32-specific branch; Type 32
falls through to the common `LessAffect` block. The target source decrements
finite time below `32400000`, clears Type/Value/Level/Time at expiry and sets
`upScore=1` (`Servidor/Source/Code/TMSrv/Server.cpp:6899-6920`;
`W2PP/Source/Code/TMSrv/Server.cpp:5831-5851`). Skill 47 uses Type 32 as its
generic fallback when the target has no Type 19.

`WorldHub.TryProcessGeneratedNpcTimerAffects` and
`TryProcessSummonTimerAffectsLocked` now include Type 32. The existing Peace
shard and fourth Combat opportunity drive the timer for generated NPCs and
RouteType 5 summons. A non-expiring decrement updates the snapshot without a
frame; expiry clears all eight bytes and emits the existing score refresh. It
does not change HP or suppress summon attack or movement. No wire layout changed.

TDD RED reproduced the missing Type 32 countdown. GREEN Engine.Tests `490/490`
covers generated NPC and summon timers in Peace and Combat. TCP loopback smoke
marker `npc-type32-expiry=score-only-relayed` verifies two in-view peers receive
only a valid `UpdateScore` with the cleared affect and the outside peer receives
no frame. This is synthetic loopback evidence without authenticated Host or
visual client. Full `WydCdk.Engine.slnx --no-restore --no-incremental` build
succeeded with 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, transfer,
restart or UP/PVP check was completed; the prior SSH probe was denied in the
sandbox and timed out at TCP/22. No authenticated Host, visual E2E, Site or
production change, commit or push.

## MOB-AI-001 Task 5BY - Type 16 player affect expiry (22/10/2026)

TMProject 7.69 calls `ProcessAffect(i)` for a live `USER_PLAY` player on the
matching `SecCounter % 16` user slot (`ProcessSecMinTimer.cpp:1711`); W2PP uses
the same cadence (`:1655`). In the common expiry block, Type 16 sets
`FaceChange=1`, the record clears, and `upScore=1` (`Server.cpp:6905-6920` in
7.69; `:5838-5851` in W2PP). Both recalculate/send score and then call
`SendEquip(idx,0)` (`Server.cpp:6946-6952` / `:5881-5882`). 7.69 additionally
sets FaceChange for Type 33 at `Server.cpp:6908`; W2PP has no Type 33 branch.
At this point in the ledger, the C# Type 33 application was not yet ported.

`LegacyPlayerAffectRuntime` rotates 16 connection-slot shards at one shard per
second. `WorldHub.TryProcessPlayerTimerAffects` decrements finite
Type 16 time below `32400000`, preserves unsigned wrap at zero, clears the full
eight-byte record at expiry, and rebuilds the audited score projection. It
captures the 33x33 player view under the WorldHub gate. The Host relay sends the
7.69 `UpdateScore` frame followed by `UpdateEquip` to those captured recipients;
ordinary countdown updates send no packet. Other player affect types and Type
33 were outside that slice.

TDD RED reproduced the missing timer and shard API; GREEN Engine.Tests
`491/491` covers countdown, the `32400000` cap, expiry, score/equipment outcome,
slot sharding, and captured recipients. Loopback TCP smoke marker
`player-type16-expiry=score-equip-relayed` verifies the actor and an in-view
peer receive valid `UpdateScore` then `UpdateEquip`, while the outside peer
receives no frame. This uses synthetic WorldHub state and the relay, without an
authenticated Host or visual client. Full no-incremental solution build
succeeded with 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. No package, transfer,
restart or UP/PVP check; no authenticated Host, visual E2E, Site/production
change, commit or push.

## MOB-AI-001 Task 5CD - player Type 22 Thunder countdown/expiry (23/10/2026)

TMProject 7.69 calls player `ProcessAffect` on the 16-slot second at
`ProcessSecMinTimer.cpp:1711`; W2PP does so at `:1655`. Type 21 remains
commented out in both `Server.cpp` implementations. The active Type 22 player
branches start at TMProject `:6279` and W2PP `:5213`: each scans 2x2 then
5x5 tiles around `TargetX/TargetY`, selects up to six targets, builds a
`MSG_Attack` with `SkillIndex=33`, and calls the ordinary
`ProcessClientMessage`. W2PP omits the redundant `Merchant == 1` check in the
first scan. Both then reach the common timer tail, which decrements finite time,
clears the affect record at zero, and requests a score refresh.

This slice ports only that common player timer tail into
`WorldHub.TryProcessPlayerTimerAffects`: finite Type 22 time decrements below
32,400,000, expiry clears all eight bytes and relays only view-scoped
`UpdateScore`. It does not change HP or equipment. The area attack remains
explicitly pending: the C# player timer does not yet submit server-created Skill
33 actions through the established attack dispatcher, so this task does not
claim complete Type 22 parity.

TDD RED reproduced the missing countdown; GREEN Engine.Tests `491/491` covers
finite decrement, persistent threshold, full-record expiry, unchanged HP,
score-only flags and captured recipients. The loopback smoke marker
`player-type22-expiry=score-only-relayed` verifies no frame on countdown, then
valid score frames to the actor and in-view peer, with no frame outside the
view. Full no-incremental solution build: 0 errors and the pre-existing CS8604
at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager.

Local self-contained win-x64 publish: 200 files/84,062,551 bytes in
`Backup/Build/MOB-AI-001-Task5CD-20261023`; ZIP 37,075,536 bytes, SHA-256
`6B9102C32EEA44BD03EF600AD46BDD5B3BF8963C1FEA7FAD90D168835073A7E0`.
Credential-like filenames: zero. The elevated read-only `wyd-vps` SSH probe
timed out connecting to `124.198.128.225:22`; no transfer, restart, RDP
session, or UP/PVP check occurred. No authenticated Host, visual client E2E,
Site/production change, commit or push.

## MOB-AI-001 Task 5BZ - Type 33 transformation items and player expiry (22/10/2026)

TMProject 7.69 `_MSG_Use_Item.cpp:7049-7285` has Type 33 branches for items
3418-3425 only; items 3426-3427 are not handled there. Values are
`itemIndex - 3417` (1..8). Items 3418-3424 use `AFFECT_1H=450`, while 3425 uses
time 4. The source selects `GetEmptyAffect(conn,33)`, rejects a second active
Type 33 or a full affect array without consuming the item, writes Type/Value/
Level=1/Time, recalculates score, refreshes equip slot 0, and consumes one item.
W2PP has no matching item-use branch. Its timer also lacks the Type 33
`FaceChange` expiry branch; TMProject 7.69 sets it at `Server.cpp:6908`. This
slice follows 7.69 for use and expiry.

The C# handles 3418-3425 in `TryApplyAffectConsumable`, preserving the item on
an existing transformation or a full affect array. Successful use sends the
slot-0 equipment confirmation and score refresh. The existing 16-shard player
timer now counts down Type 33 with Type 16; on expiry it clears the affect,
reprojects score/equipment, and relays `UpdateScore` then `UpdateEquip` only to
the captured player view. No per-second countdown frame is sent.

The 7.69 item branch calls `GetCurrentScore` after setting Type 33. Type 33 has
no direct branch in server `BASE_GetCurrentScore`; this C# path preserves its
already-authoritative `CurrentScore` while sending `UpdateScore`. It does not run
the partial score projector on use, because that could discard score effects
whose parity is still open. Full score recomputation on this path remains
pending the broader `BASE_GetCurrentScore` port.

TDD RED reproduced unsupported item use and missing Type 33 expiry. GREEN
Engine.Tests `491/491` covers the eight item values/durations, item consumption,
rejection without consumption, and timer expiry. Loopback TCP smoke passed with
`player-type16-type33-expiry=score-equip-relayed`: target and in-view peer
receive `UpdateScore` then `UpdateEquip`; outside peer receives no frame. Full
no-incremental solution build succeeded with 0 errors and the pre-existing
CS8604 warning at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exits 1 because the runner cannot open Service Control Manager.
Local self-contained win-x64 publish: 200 files/84,059,631 bytes in
`Backup/Build/MOB-AI-001-Task5BZ-20261022`; ZIP SHA-256
`5BB3B7152E3E3099186195EB12EC6E4616802CA2E4EC148684437159F89CE963`.
Filename scan found no credential-like files. SSH alias `wyd-vps` was denied in
the sandbox and timed out at TCP/22 in the elevated read-only probe; no package
was transferred or service restarted/checked. No authenticated Host, visual
client E2E, Site/production change, commit or push.

## MOB-AI-001 Task 5CA - Type 32 player timer expiry (22/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:1711` and W2PP `:1655` call
`ProcessAffect` for live `USER_PLAY` players on the matching 16-slot second.
Both `Server.cpp` implementations route Type 32 through generic `LessAffect`:
finite time below `32400000` decrements; at expiry the full eight-byte record is
cleared and `upScore=1` (`7.69:6899-6920`, `W2PP:5831-5851`). Neither source
sets `FaceChange` for Type 32; only Type 16 and, in 7.69, Type 33 refresh
equipment. Type 32 is also the generic fallback affect for skill 47 when Type
19 is absent.

`LegacyPlayerAffectRuntime` keeps the existing one-shard-per-second cadence.
`WorldHub.TryProcessPlayerTimerAffects` now includes Type 32 with Types 16/33,
preserving the 32,400,000 cap and unsigned timer behavior. Expiry runs the
existing score projector and captures the player view under the WorldHub gate;
the outcome requests `UpdateEquip` only if an expiring Type 16/33 needs the
transformation refresh. Type 32 therefore relays `UpdateScore` only. Other
affect types remain outside this player timer.

TDD RED reproduced that Type 32 was ignored by the player timer; GREEN
Engine.Tests `491/491` covers decrement, cap, clearing and score-only outcome.
TCP loopback smoke passed with `player-type32-expiry=score-only-relayed`: the
target and in-view peer receive a valid `UpdateScore`, the outside peer receives
no frame, and neither in-view peer receives `UpdateEquip`. The existing
Type16/33 score-then-equipment smoke remains active. Full no-incremental solution
build succeeded with 0 errors and the pre-existing CS8604 warning at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager.

Local self-contained win-x64 publish: 200 files/84,059,711 bytes in
`Backup/Build/MOB-AI-001-Task5CA-20261022`; ZIP size 37,074,458 bytes, SHA-256
`91235BE59BAC5A4BF86F5E13B5528A58A8A5FA8A37B76E5DB9B8C278B390BF41`.
Filename scan found no credential-like files. SSH `-G` resolves `wyd-vps` to
`124.198.128.225:22` as `Administrator`; elevated TCP connect timed out before
the SSH handshake, while TCP/3389 responded. No SSH authentication, direct RDP
session, artifact transfer, restart, or UP/PVP check occurred. No authenticated
Host, visual client E2E, Site/production change, commit or push.

## MOB-AI-001 Task 5CB - player Life Aura Type 17 timer (23/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:1711` and W2PP `:1655` call
`ProcessAffect` for live `USER_PLAY` characters on the matching 16-slot second.
Both `Server.cpp` implementations apply Type 17 (`7.69:6189-6206`,
`W2PP:5124-5141`): add `AffectLevel / 2 + AffectValue`, clamp HP to
`[1, MaxHp]`, decrement finite time below `32400000`, and clear all eight bytes
when it expires. Type 17 sets `regen` on every visit, so both sources emit
`SetHpDam` even at full HP with delta zero. `upScore` is set when HP changes or
when the affect expires; neither source sets `FaceChange` for Type 17.

`LegacyPlayerAffectRuntime` keeps its one-slot-per-second cadence. The
`WorldHub.TryProcessPlayerTimerAffects` path now applies the heal before the
common timer decrement, records the signed HP delta, updates the player's
requested HP/MP floors, and captures recipients under the world gate. The relay
sends view-scoped `SetHpDam` first, then `UpdateScore` only when requested; it
does not send `UpdateEquip` for Type 17. Type 16/32/33 behavior remains covered
by the same player timer.

TDD RED reproduced the missing Type 17 branch; GREEN Engine.Tests `491/491`
covers heal amount, finite countdown, expiry, no equipment refresh, and the
full-HP zero-delta case. TCP loopback smoke passed with
`player-type17-life-aura=hp-score-relayed`: actor and in-view peer receive
`SetHpDam` then `UpdateScore`, the outside peer receives no frame. Full
no-incremental solution build succeeded with 0 errors and the pre-existing
CS8604 warning at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exits 1 because the runner cannot open Service Control Manager.

Local self-contained win-x64 publish: 200 files/84,062,343 bytes in
`Backup/Build/MOB-AI-001-Task5CB-20261023`; ZIP 37,075,280 bytes, SHA-256
`65CD4F79767E1CEDE502E2ACC0E51FD6C7E11396240FA952D8835CC520F0C88C`. No
credential-like filenames were found. The elevated read-only `wyd-vps` SSH
`hostname` probe timed out before the handshake; a TCP-only port 3389 probe
responded, but no RDP session was opened. No authentication, transfer, restart,
or UP/PVP check occurred. No authenticated Host, visual client E2E,
Site/production change, commit or push.

## COMBAT-ATTACK-SKILL-NPC-SUMMON-PARTY-001 - GenerateSummon contra a party (15/10/2026)

TMProject 7.69 `GenerateSummon` (`Servidor/Source/Code/TMSrv/Server.cpp:3763-4053`)
e W2PP (`Server/W2PP/Source/Code/TMSrv/Server.cpp:2956`) diferem em dois pontos:
7.69 compara os membros da party so com as faces dos 8 primeiros BaseSummon
(W2PP: `MAX_SUMMONLIST`), e a copia de Guild do W2PP e uma auto-atribuicao sem
efeito. O port segue a 7.69. O C# anterior divergia: olhava so os summons do
proprio caster, conflitava com qualquer face diferente, checava a vaga de party
de uma vez antes de criar, nao copiava Guild/capa, nao puxava summons fora de
visao e nao enviava `SendAddParty`.

Agora `TryGenerateSummons` percorre `Leader.PartyList` (jogadores da party exceto
o lider, depois summons do lider; a 7.69 tem buracos de slot, o port nao):
- membro com face de um dos 8 primeiros templates diferente da pedida retorna
  TypeConflict (MP devolvido);
- membro da mesma face conta como existente; se estiver fora de `GetInView`
  (VIEWGRID 33) e houver grade livre perto do caster, e movido com Action
  Effect 8/Speed 6 para as visoes de origem e destino; ao atingir `Num`, retorna
  AlreadyPresent (MP devolvido, como o `return 0` da source);
- cada novo summon checa a vaga (12, contando jogadores e summons do lider); sem
  vaga envia Language.txt:33 ao caster e retorna PartyFull, mantendo os summons
  ja criados e devolvendo o MP;
- copia `MOB.Guild` do caster quando nao ha `guildoff`, mapeia a capa
  543/545->734, 544/546->735, 548/549->550, e envia CreateMob a visao do tile do
  summon (antes: broadcast global);
- `SendAddParty` aos usuarios: lider-a-si-mesmo quando `SummonNum` (summons
  existentes da mesma face) e zero, repetido por summon como na source; o summon
  ao lider; o summon a cada jogador da party.

A captura do InstanceType 12 passou a usar o mesmo emissor de AddParty no Host.
Limites: ramo `sItem` (summon de montaria via item) nao usa este caminho no C#;
`GetEmptyNPCMob` sem slot nao tem equivalente; `CMob::GetCurrentScore(MAX_USER)`
de mob continua aproximado como antes. A faixa `InstanceValue` 1..50 do caller
nao foi alterada (fora de faixa ainda devolve MP no C#).

Testes `AttackNpcSummonPartyTests` (5): sequencia de AddParty com dois summons,
Guild/capa e `guildoff`, conflito com summon de outro membro da party, puxada
fora de visao e nenhuma puxada dentro da visao, party cheia com criacao parcial,
aviso e AddParty a 12 usuarios. Os testes antigos de InstanceType 11 seguem
passando. Sem RED separado: os testes usam campos novos do outcome; o conflito
via party nao era detectado pelo codigo anterior. Engine.Tests 475/475; build
`WydCdk.Engine.slnx` SDK 10.0.401: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; `dotnet --info` exit 0. Evidencias em
`Server/Backup/Build/AttackNpcSummonParty-20261015`. Sem smoke TCP/headless, E2E
visual, deploy, commit ou push; nenhum opcode novo.

## COMBAT-ATTACK-SKILL-ETHEREAL-FLAME-NPC-CAPTURE-001 - captura de mob por Chamas Etereas (15/10/2026)

Fontes: so a reconstrucao TMProject 7.69 tem o ramo NPC do InstanceType 12
(`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1115-1316`). W2PP
(`_MSG_Attack.cpp:841-884`) e a source 7.59
(`Backup/Tools/Reference759/ServidorSource/Code/TMSrv/_MSG_Attack.cpp:1616-1666`)
so tratam alvo jogador. O `TMSrv.exe` em `Servidor/Server/TMSrv/run` (SHA-256
`EDB469C124130571D6202EA8240D07B0680B9773320A4BD97D472F93FF247258`) e build debug
da propria reconstrucao: em `0x432E8F`, endereco citado no comentario da source,
ha apenas padding `int3`, sem evidencia da formula original. O C# antigo limpava
affects 18/16/14/19 de NPC, comportamento sem fonte, agora removido.

Contrato portado: para `idx > MAX_USER` e `Merchant == 0`, cinco retangulos
inclusivos de posicao do mob e 25 nomes exatos (`strcmp`, incluindo
"Torre Guardia " com espaco) fazem `continue`; depois o roll `rand()%100`, HP
abaixo de MaxHp ou Clan 6 fazem `continue`. Sem vaga em `Leader.PartyList`
(MAX_PARTY 12, contando jogadores e summons do lider), envia Language.txt:33
"Grupo está cheio. Não é possível convocar mais membros." e retorna do handler:
o lote aborta sem relay agregado nem finalizacao. Na captura: BaseScore.Level =
min(Level do caster, 399), `strcat(MobName, "^")` e `'_' -> ' '`, affects
zerados, Clan 4, HP = MaxHp, Leader = lider da party do caster, Summoner =
caster; `CreateMob` comum (sem CreateType de summon) para a visao do mob e os
`SendAddParty` aos usuarios: lider-a-si-mesmo se for o primeiro membro, o mob
ao lider e o mob a cada jogador da party. Mob com Merchant != 0 cai nos dois
ramos e escreve `Dam = 0`; os `continue` preservam o marcador -1 no relay.

Divergencia deliberada (decisao do usuario em 15/10/2026): a source calcula
`calcLevel = ((alvo + 10 - caster) << 2) - 100` quando o alvo esta acima de
`caster - 10`, o que da ~0% para mobs ate ~15 niveis acima e captura garantida
para 40+ niveis acima. O port usa `100 - 4d`
(`WorldHub.GetEtherealFlameCaptureThreshold`), mantendo a comparacao
`rand()%100 > threshold`: chance 100% ate d=0, 97% em d=1, 1% em d=25 e zero a
partir de d=26. E suposicao de intencao, sem evidencia binaria; reverter exige so
trocar essa funcao.

Limites: `CMob::GetCurrentScore(MAX_USER)` de mob nao esta portado; como no
`GenerateSummon` do port, o score atual mantem o template com o nivel ajustado
e HP cheio. O summon C# nao guarda GenerateIndex, Formation/RouteType 5,
segmentos nem lista de inimigos; contadores de gerador nao continuam contando o
mob capturado. Dono offline de autotrade (NPC C#) fica sem efeito. Recaptura de
summon de outro jogador nao e tratada (summons nao passam por este caminho). A
7.69 envia CreateMob/AddParty dentro do loop, antes do Attack agregado; o Host
emite no relay do slot. O C# continua sem `SendAddParty` no InstanceType 11
(lacuna fechada em COMBAT-ATTACK-SKILL-NPC-SUMMON-PARTY-001).

Testes: `AttackEtherealFlameCaptureTests` (6 casos: limiar; captura solo com
nome/Clan/nivel/HP/affects/AddParty; cada gate com marcador -1 e Merchant com
dano 0; captura por membro de party com lider/summoner e AddParty; party cheia
com aviso e abort de lote depois de uma captura ocupar a 12a vaga; marcador -1
no burn de jogador). `world npc skill` passou a exigir NPC intacto quando o roll
falha. Nao houve RED separado: os testes novos dependem da API nova; o
comportamento substituido estava codificado no teste antigo de NPC. Engine.Tests
470/470; build `WydCdk.Engine.slnx` SDK 10.0.401: 0 erros e CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info` exit 0.
Evidencias em `Server/Backup/Build/AttackEtherealFlameNpcCapture-20261015`. Sem
smoke TCP/headless, E2E visual, deploy, commit ou push; nenhum opcode novo.

## COMBAT-ATTACK-SKILL-ETHEREAL-FLAME-PLAYER-001 - Chamas Etereas contra jogador (14/10/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1115-1362`) divide o
InstanceType 12 em dois ramos. Contra jogador (`idx < MAX_USER`, linhas
1321-1362): `chance = (BaseScore.Special[1] + 1) / 7`; se `rand()%100 > chance`,
reduz somente `CurrentScore.Mp` em `((Mp + 1) / 100) * (10 + rand()%10)` (piso 0),
chama `SendSetHpMp`/`SendScore` e continua; senao limpa os quatro campos de
cada affect Type 18/16/14/19/32, recalcula/envia score a cada remocao e, ao
final, sempre chama `SendEquip(idx, 0)`, `GetCurrentScore` e `SendScore`. W2PP
(`_MSG_Attack.cpp:841-884`) tem o mesmo ramo, mas grava `pUser.ReqMp` no burn,
nao limpa Type 32 e zera so o campo Type. O C# seguia o W2PP.

Agora o burn preserva `RequestedMana` (precedente de Renascimento/Exterminar) e
o Host continua enviando UpdateScore + SetHpMp. O cleanse limpa o registro de 8
bytes de 18/16/14/19/32, recompõe o MOB pelo `LegacyCurrentScoreLoginProjector`
e marca `TargetEquipmentChanged`, entao o Host relaya UpdateScore/UpdateEquip na
visao e UpdateAffect ao alvo, como na transformacao. Sem ItemList ou com falha de
projecao o ramo falha fechado (`CombatStateUnavailable`) sem mutar o alvo.
Divergencia aceita: os SendScore intermediarios por affect removido viram um
unico UpdateScore final com o mesmo estado; a ordem final 7.69 e Equip -> Score,
o Host emite Score -> Affect -> Equip.

O ramo NPC foi portado em COMBAT-ATTACK-SKILL-ETHEREAL-FLAME-NPC-CAPTURE-001
(secao acima). Os dois ramos de jogador fazem `continue` antes de
`m->Dam[i].Damage = dam`; desde aquele recorte o relay preserva o marcador -1
do cliente nesse slot.

RED: com o handler anterior, Engine.Tests 463/464 falhou em "preserving
RequestedMana". GREEN: Engine.Tests 464/464 (cobre burn sem ReqMp, cleanse de
registros completos 18/32, Type 2 preservado, relay de equipamento e falha
fechada sem ItemList). Build `WydCdk.Engine.slnx` SDK 10.0.401: 0 erros e CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info` exit 0.
Evidencias em `Server/Backup/Build/AttackEtherealFlamePlayer-20261014`. Sem smoke
TCP/headless, E2E visual, deploy, commit ou push; wire intacto.

## ITEM-EQUIPMENT-SLOT-RANGE-001 - faixa efetiva dos slots de equipamento (14/10/2026)

TMProject 7.69 usa 18 slots no `Basedef.h` comum (`MAX_EQUIP=18`). O projeto
`TMSrv.vcxproj` compila `../Basedef.cpp`; `ProcessClientMessage.h`, `Server.h` e
`CMob.h` incluem `../Basedef.h` comum. O `TMSrv/Basedef.h` adjacente declara
16/128, mas nao aparece nesses caminhos. Assim, `CMob::GetCurrentScore` e
`SendEquip` iteram 18 e o `UpdateEquip` ativo tem 68 bytes. No handler
`_MSG_Move_Item.cpp:93,127`, o limite `position < MAX_EQUIP - 1` permite destino
0..16 e origem 1..16; o helper cliente/servidor `BASE_CanEquip` ainda rejeita
colocacao na posicao 15 e aplica `EF_POS` nas demais.

W2PP (`Basedef.h:135`, `_MSG_TradingItem.cpp:144`) declara 16 e usa o mesmo
limite, portanto permite destino 0..14 e origem 1..14. O C# conserva o blob de
conta W2PP de 16 slots e a lista versionada de 18 para o contrato cliente.
`WorldHub.IsValidTradeSlot` aplica `equipmentCount - 1`: slot 16 e movivel somente
quando a sessao recebeu explicitamente a extensao persistida; sem ela, inclusive
com sidecar ausente no file store ou linha MariaDB ausente, usa o limite de 16 slots W2PP. Slot 17 e
sempre rejeitado na origem e no destino, e `LegacyEquipmentRules` rejeita a
colocacao na posicao 15. Dados 16/17 carregados continuam na appearance de 18
campos, mas o slot 16 nao pode ser alterado sem uma persistencia correspondente.

No caminho C#, `MariaDbWorldCharacterStore` le e grava os 18 itens na tabela
versionada `wyd_client_equipment_769`; desde 29/10/2026,
`LegacyFileAccountStore` tambem persiste a extensao num sidecar local versionado.
A cadeia de CharacterLogin passa `ClientEquipment` ao adapter, `WorldHub`,
`CreateMobV769` e `UpdateEquipV769`.

ITEM-EQUIPMENT-EXTENSION-LIFECYCLE-001 (14/10/2026) fechou a lacuna de ciclo de
vida no MariaDB: `TryDeleteCharacterAsync` e `TryWriteNewCharacterAsync` apagam a
linha `(world_key, account_name, character_slot)` do sidecar na mesma transacao
serializavel do blob e da reserva de nome. A criacao tambem limpa linhas
orfas gravadas antes da correcao. A tabela segue opcional: a presenca e
consultada em `information_schema` antes do DELETE, entao tabela ausente nao
quebra o CRUD, mas falha real do DELETE aborta a mutacao inteira. Nao ha
equivalente nas sources (7.69 grava o MOB de 18 slots no proprio arquivo;
W2PP so tem 16): e regra de persistencia do port, sem mudar blob, schema ou
wire. Na data deste recorte, `LegacyFileAccountStore` ainda nao persistia os slots extras;
essa lacuna foi fechada por `ITEM-EQUIPMENT-FILE-EXTENSION-001` em 29/10/2026.
Teste `tests/WydCdk.Character.Integration` contra o schema isolado local
`wyd_cdk_autotrade_close_test` (loopback, MariaDB 11.8): RED com as chamadas
desativadas falhou em "Delete left the sidecar row"; GREEN cobre delete/recreate
no mesmo slot, delete recusado por senha, readback por nova conexao, linha orfa
limpa na criacao, linha PVP preservada, tabela ausente (renomeada e restaurada)
e rollback conjunto por trigger de falha. Tabelas do schema ficaram vazias e sem
trigger apos as execucoes.

TDD RED primeiro reproduziu a retirada aceita do slot 17 e depois o movimento
para slot 16 sem extensao persistida. GREEN cobre faixa condicional, rejeicao
do slot 17 em ambos os sentidos, ausencia de mutacao e slot 16 persistido.
Engine.Tests 464/464; build `WydCdk.Engine.slnx`, SDK
10.0.401, 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. `dotnet --info` exit 0 confirmou SDK
10.0.401; `git diff --check` passou com avisos LF/CRLF preexistentes. Sem
smoke TCP/headless, E2E visual, deploy, commit ou push; wire e blob W2PP intactos.

### ITEM-EQUIPMENT-FILE-EXTENSION-001 (29/10/2026)

`LegacyFileAccountStore` persiste os slots 16/17 fora do blob W2PP de 7945 bytes,
em `.wyd-client-equipment-v1/<partition>/<account>/<slot>.bin`. O sidecar tem
cabecalho de 12 bytes (`WYDEQ769`, versao little-endian 1) seguido dos 144 bytes
de `ClientEquipmentStateV769`; gravações usam o writer atomico existente. Arquivo
ausente significa extensao indisponivel e preserva o fallback de 16 slots; magic,
versao ou tamanho desconhecido falha fechado com `InvalidDataException`, sem
interpretar bytes incompatíveis. Login hidrata o estado para o adapter/WorldHub.

Delete grava tombstone versionado antes de permitir reutilizacao do slot; create
em slot vazio tambem grava tombstone, evitando herdar equipamento de personagem
anterior. Leitura de login, leitura explicita, save, delete e create compartilham
o gate do sidecar para nao observar uma transicao parcial. O blob de conta, schema
MariaDB, protocolo e wire permanecem inalterados; MariaDB continua usando a tabela
`wyd_client_equipment_769`.

RED confirmou que a interface de extensao ainda nao existia no store de arquivo;
GREEN cobre round-trip dos 18 itens por nova instancia/store, bytes de magic,
versao e comprimento, rejeicao de versao desconhecida, slot vazio/invalido,
delete/recreate sem heranca e blob de 7945 bytes inalterado. `Engine.Tests`
542/542; `dotnet build WydCdk.Engine.slnx --no-restore` passou com 0 avisos e
0 erros. SDK 10.0.401; `dotnet --info` identificou a versao, mas encerrou com
codigo 1 por acesso negado ao Service Control Manager. Sem E2E visual, banco
MariaDB ao vivo, deploy, commit ou push.

## COMBAT-ATTACK-ZARD-QUEST-001 - contador e runa do Zard (14/10/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:2072-2089`)
compara o nome do MOB com `Zard`, define `pMob[a_iConn].MOB.Equip[12].EF1=51`,
incrementa `EFV1` e envia `SendClientSignalParm(..., _MSG_MobLeft, EFV1)`. Quando
o valor chega a 80, cria `STRUCT_ITEM` zerado de indice 475, chama `PutItem` e
zera `EFV1` independentemente do sucesso de insercao. `PutItem`
(`Server.cpp:1037-1068`) exige `USER_PLAY`, escolhe o primeiro carry vazio ate
`MaxCarry` e, se nao houver espaco, envia `_NN_You_Have_No_Space_To_Trade`
(`Language.txt:86`). W2PP `_MSG_Attack.cpp` nao contem um branch Zard; o port
segue a referencia 7.69.

O branch ocorre depois da montagem de `m->Dam[i].Damage`; em seguida, a source
define a variavel local `dam=0`. A checagem de absorcao de HP Type 8 em
`_MSG_Attack.cpp:2230` depende de `dam >= 1`, mas o dano do alvo ja esta no slot
wire. O C# reproduz o estado do quest nos caminhos de ataque fisico, skill contra
jogador, summon e NPC: `Equip[12]` recebe `Effect1=51` e o byte `Value1` avanca;
o valor enviado e capturado antes do reset. O dano final do alvo permanece
inalterado e so a absorcao Type 8 do atacante e omitida para o MOB de nome exato
`Zard`.

O Host envia a confirmacao `_MSG_MobLeft` existente (`0x03B0`) somente ao
atacante. A runa entra no primeiro carry vazio e segue por `SendItem`; no carry
cheio, o contador tambem reseta e o Host envia aviso privado. A insercao e os
avisos sao agregados com os demais resultados, inclusive quando a morte do NPC
passa pelo finalizador de ataque comum. Nenhum campo ou opcode wire mudou.

RED confirmou que o ataque fisico nao alterava `Equip[12]`. Engine.Tests passou
463/463: progresso por ataque fisico e skill, dano preservado, Type 8 suprimido,
recompensa no 80o ataque, carry cheio e runa preservada na finalizacao de morte
do NPC. Build de `WydCdk.Engine.slnx`, SDK 10.0.401: 0 erros e CS8604 preexistente
em `AttackDivineBlessingTests.cs:256`. O
`dotnet --info` identificou SDK 10.0.401, mas terminou com exit 1 por acesso
negado ao Service Control Manager. A execucao dos testes mostrou CS8604
preexistente em `AttackDivineBlessingTests.cs:256`. Engine.Tests foi headless;
sem smoke TCP do Host, E2E visual, deploy, commit ou push; wire sem alteracao.

## COMBAT-GUILD-WAR-CONTROL-001 - controle de guerra de guildas (14/10/2026)

TMProject 7.69 `DoWar` (`Servidor/Source/Code/TMSrv/Server.cpp:9679`) e W2PP
(`Server/W2PP/Source/Code/TMSrv/Server.cpp:8543`) usam o mesmo mapa dirigido:
uma declaracao grava somente `Guild -> Enemy`; o pedido reciproco aceita a
guerra; cancelar uma declaracao unilateral limpa somente a origem, e cancelar
guerra reciproca limpa os dois sentidos. TMProject envia `_MSG_SendWarInfo` em
`SendFunc.cpp:1530` e W2PP em `SendFunc.cpp:1543`; os arrays fallback
`GuildIndex`/`GuildMark` estao em TMProject `Basedef.cpp:287,294` e W2PP
`Basedef.cpp:297,304` (36 e 132 bytes). Ambos os arrays do port correspondem
byte a byte a TMProject e W2PP. O port segue o comportamento de TMProject 7.69.

O protocolo recebe `MSG_War` em `0x0E0E`/20 bytes, com IDs de guilda validos.
O Host exige sessao `Playing`; o WorldHub deriva Guild/GuildLevel do personagem
ativo, exige GuildLevel 9 e que `Parm1` corresponda a guilda autenticada. O
pedido nao permite `Enemy=0`; cancelamento e uma operacao interna confiavel. A
transicao pura nao faz I/O, e o WorldHub atualiza sob lock e publica uma copia
read-only de `GuildWarPartners`, que alimenta os gates PK existentes. O Host
serializa cada transicao junto com todo o lote de avisos/`SendWarInfo`, mantendo
a ordem entre mudanca de estado e relays. Assim,
pedido unilateral nao libera PK; somente estado reciproco o faz, e cancelamento
remove a excecao imediatamente.

Os avisos usam as entradas 210, 211, 212 e 221 do `Language.txt` 7.69, nomes de
`Guilds.txt` quando disponiveis e as tabelas fallback da source. Para membros
ativos de guildas afetadas, o Host envia `SendWarInfoConfirmation` em
`0x03A8`/24 bytes (`ID=30000`; parametros: guilda oponente, `GuildZone[4].Clan`
e aliada zero). O aviso de guerra iniciada e global conforme o efeito 7.69.

RED/GREEN: os testes de protocolo, nome, transicao e WorldHub verificam frame,
check de lideranca, request unilateral/reciproco, cancelamento e efeito no gate
PK. O teste deterministico `--self-test-guild-war-relay` segura o primeiro lote
de relay e confirma que a segunda transicao so entra depois da liberacao. Foi
adicionado depois da revisao encontrar que handlers concorrentes podiam enviar
os relays fora da ordem das transicoes. Engine.Tests final 462/462. Build da
solution com SDK 10.0.401: 0 erros e um aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. O smoke TCP loopback final passou com
declaracao falsa sem mutacao, declaracao reciproca, aviso global e
`SendWarInfo` com Clan=37; fixtures geradas em `%TEMP%`, sem banco externo.

Limites: o smoke e headless/TCP; nao comprova que o cliente visual envie
`MSG_War` ou exiba os avisos. O estado e volatil e local a um processo Host;
replicacao entre UP/PVP, persistencia e integracao Site nao foram incluidos.
Sem alteracao de layout wire, E2E visual, deploy, commit ou push.

## MOVE-ACTION-TELEPORT-POSITION-AUDIT-001 - alinhamento da posicao (14/10/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/Server.cpp:9614,10301`) e W2PP
(`W2PP/Source/Code/TMSrv/Server.cpp:8497,9151`) implementam `DoRecall` e
`DoTeleport` criando uma Action e chamando `GridMulticast`. Os helpers gravam o
destino em `pMob[conn].TargetX/TargetY` (TM `SendFunc.cpp:924-925`; W2PP
`SendFunc.cpp:929-930`). Portanto, o campo autoritativo consultado pelos gates
legados acompanha os destinos efetivamente aceitos pelo servidor.

No port, `WorldHub.Participant.PositionX/PositionY` e a representacao
autoritativa equivalente. A Action aceita atualiza essa posicao; correcoes de
destino ocupado sao privadas e nao movem. Os recalls de origem/mapa, os
consumiveis de movimento e o recall da skill 99 alteram o WorldHub e sincronizam
`LoginSessionRegistry`; o relay da limpeza de area Battle Royale tambem
sincroniza a sessao. Teleportes de entrada/saida/conclusao da Pista alteram o
WorldHub e enviam Action, mas nao atualizam imediatamente o cache de sessao. O
Host reconcilia esse cache do snapshot autoritativo antes de avaliar o proximo
movimento. Salvamento de desconexao tambem usa o snapshot do WorldHub.

Conclusao: nao foi encontrada divergencia funcional de coordenadas nos fluxos
implementados e nao foi necessaria mudanca de codigo. Auditoria estatica de
fonte/codigo; nao inclui teste, build, smoke ou E2E visual.

## COMBAT-ATTACK-SKILL-PK-GATE-001 — gate PK de skills hostis (14/10/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1811-1858`) resolve
`Summoner=idx` e substitui pelo dono somente se o alvo for summon Clan 4 com
conexao valida em jogo. O gate compara PK points desse alvo efetivo com os do
atacante e consulta as coordenadas `pMob[attacker].TargetX/TargetY` do atacante. Ele so roda
fora das cinco cidades (`BASE_GetVillage`, `Basedef.cpp:4695-4704`), das cinco
areas de arena (`BASE_GetArena`, `Basedef.cpp:4684-4693`), dos mapas PK
`(1,31)`/`(19,30)`, de guerra reciproca entre GuildIds validos e do mapa
`(8,13)` quando `CastleState != 0`. As areas de arena inclusivas vem dos campos
`WarAreaX1/Y1/X2/Y2` em `Basedef.cpp:61-67` e `Basedef.h:1094-1113`:
`197..238/213..230`, `197..238/149..166`, `141..182/213..230`,
`141..182/149..166` e `4000..4010/4000..4010`.

Dentro da area protegida, atacante com PK <= 10 contra alvo PK > 10 recebe
`_DN_CantKillUser` formatado com os pontos PK do atacante e `dam=0`. O ramo e
exclusivo: se a negacao nao ocorre, `(targetPK > 10 && dam > 0) || skillnum ==
6` grava `Guilty=8` somente no atacante. `GetCreateMob`/`GridMulticast` so
ocorrem quando o Guilty anterior era zero. A checagem de miss vem imediatamente
depois e pode converter o dano negado em `-3`; o C# conserva esse caso.

W2PP (`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1347-1405`) diverge: marca
somente `(1,31)` como PK map, nao inclui `skillnum==6` e, no ramo culpado, tambem
marca/relaya o alvo. O port segue TMProject 7.69.

`LegacySkillPkGate` aplica a regra ao caminho atual de skill contra jogador,
depois de ForceDamage/PvPDamage e friendly-fire, antes do miss. Usa os limites
de cidade existentes em `LegacyMovementMapRules`, inclui as cinco areas WarArea
da fonte e consulta `LegacyCombatWorldState.GuildWarPartners`. O estado exige
ambos os sentidos da relacao, e `SetCombatWorldState` faz copia defensiva para
que mutacoes da configuracao recebida nao mudem o combate ativo. A primeira
transicao para Guilty=8 produz `LegacyCrimeStateUpdate`; o Host envia CreateMob
ao proprio jogador e aos observadores. A negacao produz `LegacyPrivateNotice`
que o Host envia via MessagePanelConfirmation. O finalizador de skill preserva
esse aviso ao agregar resultados de morte/XP. Nenhum campo/opcode wire mudou.

RED/GREEN: o teste falhou antes da implementacao, mostrando 72 de dano em PK
10 contra PK 11; depois cobriu PK 10/11, miss -3 apos negacao, limites de
cidade/arena, ambos os mapas 7.69, castelo, guerra reciproca e unidirecional,
Guilty somente no atacante e excecao da skill 6. O teste do aviso compara a
frase completa localizada em Language.txt:209, com os pontos PK formatados.

No fluxo de movimento aceito, as coordenadas usadas pelo gate estao alinhadas:
_MSG_Action.cpp:281 chama GridMulticast depois dos gates e SendFunc.cpp:924-925
grava tx/ty em pMob[conn].TargetX/TargetY. O host C# em Program.cs:2372-2375
chama WorldHub.UpdatePosition com action.TargetX/Y depois de Accepted. Os recalls/teleportes implementados foram auditados em MOVE-ACTION-TELEPORT-POSITION-AUDIT-001.

Engine.Tests passou 456/456. A primeira execucao completa teve timeout de 12s
em CurrentScoreLoginStartupTests; a chamada direta ao host confirmou a rejeicao
de --item-data antes do bind e a repeticao completa passou. Build
WydCdk.Engine.slnx com SDK 10.0.401: 0 erros e aviso CS8604 preexistente em
AttackDivineBlessingTests.cs:256. dotnet --info identificou SDK 10.0.401, mas
saiu 1 por acesso negado ao Service Control Manager. Sem smoke TCP/headless,
E2E visual, deploy, commit ou push; wire intacto.

O estado reciproco de `GuildWarPartners` passou a ser alimentado pelo fluxo
`COMBAT-GUILD-WAR-CONTROL-001`. A regra PK simplificada do ataque fisico
existente nao foi alterada neste recorte.

## COMBAT-ATTACK-SKILL-SUMMON-PK-GATE-001 - dano de skill em summon Clan 4 (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1767-1858`)
inclui jogadores e summons Clan 4 no bloco de dano. Para o gate PK, define
`Summoner=idx` e troca pelo owner apenas quando `Summoner` e valido e
`pUser[Summoner].Mode == USER_PLAY`. O gate de jogador so roda quando o indice
efetivo e menor que `MAX_USER`; se o owner nao esta ativo, o summon continua um
MOB para esse trecho e a checagem PK e pulada. PK <= 10 contra owner PK > 10
zera dano e notifica; senao, `(ownerPK > 10 && dam > 0) || skillnum == 6`
marca Guilty=8 somente no atacante. Se o dano ja nao e positivo antes deste
bloco, o handler continua o loop e pula PK/miss, inclusive para skill 6. Quando
o gate PK zera dano positivo, o miss segue imediatamente e pode transformar o
dano bloqueado em -3.

W2PP (`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1300-1405`) tambem resolve
owner ativo, mas diverge nos mapas PK, nao tem a excecao da skill 6 e marca
guilty/relaya tambem o alvo. O C# segue TMProject 7.69.

`WorldHub.TryApplySummonSkillAttackLocked` usa o snapshot MOB e HP do summon,
as formulas de dano e resistencia de alvo MOB, ForceDamage, friendly-fire,
Clan 6, ForceMobDamage, teto de dano e miss. `LegacySkillPkGate.ResolveSummon`
recebe PK/Guild do owner conectado; a ausencia de participante passa owner nulo
e ignora todo o gate PK de jogador, inclusive a excecao da skill 6. A negacao
gera aviso privado; acerto permitido cria `LegacyCrimeStateUpdate` somente para
o atacante. O HP e salvo no summon e dano letal passa pelo finalizador existente,
que remove Clan 4 sem distribuir XP ou drops. Para mount, o estado do item segue
ao owner. O Host usa os relays existentes de Attack, RemoveMob e SendItem; nenhum
campo ou opcode wire mudou.

`AttackSkillPkGateTests` cobre owner conectado com PK 11 contra atacante PK 10
(dano zero e aviso), hit permitido com ambos PK > 10 (Guilty=8 somente no
atacante), o resolver sem owner conectado (gate inteiro ignorado), zero-damage
skill 6 ignorando PK/miss e remocao letal sem XP/drop. RED confirmou
`UnsupportedSkillType` no fluxo anterior.
Engine.Tests passou 458/458. Build de `WydCdk.Engine.slnx`, SDK 10.0.401:
0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`.
`dotnet --info` identificou SDK 10.0.401, mas saiu 1 por acesso negado ao
Service Control Manager. Sem smoke TCP/headless ou E2E visual, deploy,
commit ou push.

## COMBAT-ATTACK-SAME-CLAN-RVR-GATE-001 - bloqueio de mesmo Clan em RvR (14/10/2026)

TMProject 7.69 (Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:2009-2018) executa
o gate somente quando atacante e alvo sao jogadores. Com g_pRvrWar.Status == 1,
se pMob[idx].TargetX/TargetY esta no retangulo inclusivo de
Servidor/Source/Code/Basedef.h:528-531 (X=1679..1774, Y=1940..2023) e os Clans
coincidem, grava Damage=0 e TargetID=0. O bloco vem depois do gate de mesmo
Clan na area X=1017..1290/Y=1911..2183. W2PP nao tem este bloqueio de alvo em
_MSG_Attack; seu gate global e a area de Basedef.h nao sao equivalentes.

LegacyAttackRvrSameClanGate valida os dois indices de jogador, RvrWarActive,
limites inclusivos, Clan igual e dano positivo. O WorldHub chama o gate em
ataque fisico e skill contra jogador; ataques a NPC ficam fora. A representacao
de coordenadas equivale no movimento aceito: _MSG_Action.cpp:281 chama
GridMulticast e SendFunc.cpp:924-925 grava tx/ty em pMob[conn].TargetX/TargetY;
Program.cs:2372-2375 chama WorldHub.UpdatePosition com action.TargetX/Y depois
de MovementResult.Accepted. Os recalls/teleportes implementados foram auditados em MOVE-ACTION-TELEPORT-POSITION-AUDIT-001. Nenhum campo/opcode wire mudou.

Este branch nao executa continue: depois de zerar dam e os campos do slot, a
source prossegue para mount e ManaControl. No caso de mount adulto, _pDamage
derivado de zero e elevado para 1, atualizando Damage sem restaurar TargetID; o
C# preserva esse efeito e continua a resolver ManaControl. O gate anterior de
mesmo Clan na area protegida continua sendo terminal e nao percorre essas etapas.

AttackRvrSameClanGateTests cobre dano fisico e skill, limites inclusivos/
exclusivos, guerra inativa, Clans diferentes, alvo NPC e continuacao por
mount/ManaControl. Os cinco testes novos passaram; Engine.Tests fechou 456/456.
Build completa WydCdk.Engine.slnx com SDK 10.0.401: 0 erros e CS8604
preexistente em AttackDivineBlessingTests.cs:256. dotnet --info identificou SDK
10.0.401, mas saiu 1 por acesso negado ao Service Control Manager. Sem smoke
TCP/headless, E2E visual, deploy, commit ou push.

## COMBAT-ATTACK-SAME-CLAN-AREA-GATE-001 - area protegida entre Clans (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:2001-2007`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1512-1518`) concordam: quando `dam > 0`, o atacante e o alvo tem o mesmo `MOB.Clan`, e a posicao do atacante esta no retangulo inclusivo X=1017..1290/Y=1911..2183, o handler grava `Damage=0`, `TargetID=0` e continua o loop. O gate ocorre antes da aplicacao ao HP; no caminho fisico, tambem precede a divisao de dano de montaria e ManaControl, que ficam abaixo do `continue` nas duas sources.

`LegacyAttackSameClanAreaGate.ShouldClearTargetId` expressa os quatro limites inclusivos, igualdade de Clan e requisito de dano positivo. `WorldHub` usa o gate em ataque fisico, skill contra jogador e skill contra NPC antes de escrever HP; mantem o resultado como slot aceito e marca `ClearTargetIdOnWire`. O lote transforma essa marca em `LegacyPhysicalAttackBatchWireSlotOverride(0, 0)`, aplicado pelo `Program` ao `AttackConfirmation` existente. Nenhum campo ou opcode foi alterado.

`AttackSameClanAreaGateTests` cobre dano fisico, skill em jogador, skill em NPC, limites, comparacoes negativas e lote misto fisico/skill com ambos TargetID removidos no wire. TDD RED reproduziu quatro lacunas do gate e, depois da primeira implementacao, a regressao de ordem: Type18 ManaControl ainda alterava o MP do alvo. GREEN: os seis testes especificos passaram, incluindo a verificacao de que o `continue` do gate impede esse efeito. A ultima execucao de Engine.Tests terminou em 414/450, com 36 falhas ambientais nao relacionadas por acesso negado a arquivos/sockets; os seis novos testes passaram. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas exit 1 por acesso negado ao Service Control Manager. Sem smoke TCP/headless ou E2E visual; sem deploy, Site/producao, commit ou push.

Limite de posicao: a source usa `pMob[attacker].TargetX/TargetY`, enquanto o C# usa `Participant.PositionX/PositionY`; a equivalencia foi confirmada para movimento Action aceito; recalls/teleportes implementados foram auditados em MOVE-ACTION-TELEPORT-POSITION-AUDIT-001. As fontes usam a mesma comparacao direta de Clan, inclusive Clan 0. A regra afeta somente o slot atual e deixa o processamento do lote seguir.

## COMBAT-ATTACK-FRIENDLY-FIRE-GATE-001 - dano de skill entre aliados (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:404-428`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:363-387`) resolvem o Leader de atacante/alvo e a guilda efetiva. GuildDisable zera a guilda; quando ambas sao zero, o atacante e normalizado para -1; Battle Royale ativo com item > 0 e atacante em X=2604..2648/Y=1708..1744 tambem define a guilda efetiva do atacante como -1. A comparacao de dano aparece somente no ramo de skill hostil: 7.69 `:1670-1675` e `:1776-1801`; W2PP `:1174-1179` e `:1309-1335`. ForceDamage e PvPDamage precedem o reset final `dam=0` nos dois sources. Os ramos fisicos 7.69 (`:461-572`) e W2PP (`:421-485`) calculam dano sem comparar Leader/guilda; os dois matches `leader == mobleader || Guild == MobGuild` estao no caminho de skill.

`LegacyAttackFriendlyFireGate` compara os Leaders de party e as guildas efetivas. O `WorldHub` usa esse resultado no finalizador de skill contra jogador e NPC, apos os modificadores de dano, zerando dano sem rejeitar o slot. O caminho de skill contra summon continua sem resolucao de dano no port. Elegibilidade de efeitos segue no helper existente e nao foi alterada nesta fatia. O bloqueio fisico por guilda que existia no C# divergia das duas sources e foi removido; ataques fisicos continuam aplicando dano entre membros da mesma party ou guilda.

Limite do override BR: as sources consultam `pMob[conn].TargetX/TargetY`, enquanto o C# passa `Participant.PositionX/PositionY` ao resolver a guilda efetiva. Os testes cobrem a inclusividade do retangulo sobre a posicao mantida no WorldHub; o mapeamento para TargetX/Y foi confirmado para movimento Action aceito; recalls/teleportes implementados foram auditados em MOVE-ACTION-TELEPORT-POSITION-AUDIT-001.

`AttackFriendlyFireGateTests` cobre dano fisico mantido para mesma party/guilda em jogador e NPC, dano de skill zerado por party/guilda, GuildDisable e os limites inclusivos do override de Battle Royale. RED da correcao fisica: 442/444; GREEN: Engine.Tests 444/444. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas exit 1 por acesso negado ao Service Control Manager. Sem smoke TCP/headless ou E2E visual; sem deploy, Site/producao, commit ou push. Nenhum campo ou opcode wire mudou.

## COMBAT-ATTACK-FORCE-MOB-DAMAGE-001 - bonus Type 30 contra mobs (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1932-1936`, `TMSrv/Basedef.cpp:3847-3848`, `TMSrv/CMob.cpp:667-694`) soma `ForceMobDamage` depois do gate Clan 6 e somente quando `idx > MAX_USER` e `dam >= 1`; a variavel e recomposta de zero em `CMob::GetCurrentScore`. O branch Type 30 soma `STRUCT_AFFECT.Level` (`unsigned short`). W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1455-1459`, `W2PP/Source/Code/Basedef.cpp:4427-4428`, `W2PP/Source/Code/TMSrv/CMob.cpp:699-709`) tem a mesma regra e ordem.

`LegacyForceMobDamageMath` soma o campo Level de todos os 32 affects Type 30 no snapshot do atacante. O C# aplica o resultado depois do gate Clan 6 e antes de `LegacyAttackDamageCap` para dano fisico contra NPC/summon e no finalizador de skills contra NPC. O caminho de skill contra summon ainda nao resolve dano; esta fatia nao criou esse caminho. Jogadores nao recebem o bonus. O bonus so e aplicado a dano positivo e nenhum campo/opcode wire mudou.

`AttackForceMobDamageTests` compara o dano base ao dano com dois affects Type 30 (total +75), cobre fisico contra NPC e skill contra NPC, verifica que o bonus nao afeta skill contra jogador e que Type 30 nao desfaz o gate Clan 6. TDD RED: 435/437, com falha somente nos dois caminhos mob ainda ausentes; GREEN: Engine.Tests 438/438. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas saiu com codigo 1 por acesso negado ao Service Control Manager. `git diff --check` passou com avisos de LF/CRLF. Sem smoke TCP/headless ou E2E visual; sem deploy, Site/producao, commit ou push. Wire intacto.

## COMBAT-ATTACK-DAMAGE-CAP-001 - teto comum de dano em MSG_Attack (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1935-1936`, `TMSrv/Basedef.h:219`) limita `dam >= MAX_DAMAGE` a `1.000.000`, apos o gate Clan 6. W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1458-1459`, `W2PP/Source/Code/Basedef.h:265`) aplica o mesmo branch, mas sua constante e `1.000.000.000`. O C# segue o limite da referencia ativa 7.69. O teto e somente superior e preserva zero e marcadores negativos.

`LegacyAttackDamageCap.Apply` e chamado nos caminhos de dano fisico, skill contra jogador e skill contra NPC, apos os gates comuns Clan 6/Type 6 e antes de efeitos secundários, parry ou atualizacao de HP. ForceDamage/PvPDamage atualmente modelados ja entram antes do teto. O bonus `ForceMobDamage` de Type 30 foi incluido antes deste cap em `COMBAT-ATTACK-FORCE-MOB-DAMAGE-001`. Sem mudanca de campo ou opcode.

`AttackDamageCapTests` força dano acima do teto nos tres caminhos e verifica que dano final e HP usam o valor limitado. TDD RED reproduziu 7.920.000 no fisico e 8.910.027/9.900.031 nas skills; GREEN: Engine.Tests 434/434. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas terminou com codigo 1 por acesso negado ao Service Control Manager. Sem smoke TCP/headless ou E2E visual; sem deploy, Site/producao, commit ou push. Wire intacto.

## COMBAT-ATTACK-CLAN6-LEVEL-GATE-001 - bloqueio de dano contra Clan 6 por Level (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:177,1929-1930`, `TMSrv/Basedef.h:131`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:154,1452-1453`, `W2PP/Source/Code/Basedef.h:177`) zeram `dam` para qualquer alvo com Clan 6 quando `Level < MAX_LEVEL` (399). O gate nao rejeita o slot. No handler, `Level` inicia em zero; fisico e skill 101+ mantem zero, skills 0..95 usam `CurrentScore.Special[((skillnum % 24) / 8) + 1]` e skills 96..100 usam `CurrentScore.Level`. Essa semantica local do handler e diferente de simplesmente comparar o nivel do personagem em todo ataque.

`LegacyAttackClanSixDamageGate` aplica a comparacao apos miss e protecao Type 6 nos caminhos de dano fisico e de skill contra jogador. Skills contra NPC passam o `LegacySkillDefinition` ao finalizador, que reutiliza `LegacySkillCombatMath.GetSkillSpecial` antes de resolver HP. O branch de `ForceMobDamage` da source vem depois do gate e so soma quando o dano continua positivo. Clans diferentes seguem o fluxo normal. Nenhum campo ou opcode wire mudou.

`AttackClanSixDamageGateTests` cobre ataques fisicos contra jogador/NPC mesmo com o personagem no nivel 399 (o Level do handler fisico continua zero), skills abaixo e no limite 399 contra jogador/NPC, e controles de alvo fora do Clan 6. TDD RED confirmou a ausencia do bloqueio nos tres caminhos; GREEN: Engine.Tests 431/431. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas terminou com codigo 1 por acesso negado ao Service Control Manager. `git diff --check` passou. Sem smoke TCP/headless ou E2E visual; sem deploy, Site/producao, commit ou push. Wire intacto.

## COMBAT-ATTACK-SAME-CLAN-NPC-GATE-001 - dano contra NPC do mesmo Clan (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:433-436,1803-1804`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:392-395,1337-1338`) capturam os Clans do atacante e do alvo e zeram `dam` quando ambos sao 7 ou ambos sao 8 e `idx >= MAX_USER`. O gate nao rejeita o slot. O C# compara o ID com `MaxUserConnectionExclusive` (1000) e os bytes de Clan; aplica depois dos modificadores `ForceDamage`/`PvPDamage` no caminho fisico e depois de `ForceDamage` no caminho de skill contra NPC. A HP nao e alterada quando o dano fica zero; jogadores e Clans diferentes seguem os gates posteriores.

`AttackSameClanNpcGateTests` cobre os Clans 7 e 8 em fisico/skill, o controle de Clans diferentes e o controle de alvo jogador. TDD RED reproduziu dano positivo nos dois casos NPC antes da implementacao; GREEN: Engine.Tests 428/428. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas terminou com codigo 1 por acesso negado ao Service Control Manager. Sem smoke TCP/headless ou E2E visual; sem deploy, Site/producao, commit ou push. Wire sem alteracao.

## COMBAT-ATTACK-PEACEFUL-MAP-GATE-001 - gate comum de mapa pacifico em MSG_Attack (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:445-459`) deixa comentado o bloco comum que impediria dano fisico ou skill agressiva contra outro jogador nao-PK/nao-guilty quando o atacante esta no AttributeMap `0x40`. W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:405-416`) mantem esse bloco ativo fora de RvR, castelo, torre e evento Newbie. O port segue a 7.69: removeu as chamadas ao bloqueio comum nos caminhos fisico e de skill agressiva contra jogador.

O recorte nao remove `LegacyAttackRvrGateRules`, a protecao distinta por pontos PK, verificacoes de guilda nem gates `GetAttribute` especificos de skills. Nenhum campo ou opcode mudou. Os casos existentes `WorldPhysicalAttack` e `WorldSkillAttack` agora exigem dano positivo em AttributeMap `0x40`, com AttackMissRoll fixo para resultado deterministico.

Engine.Tests: 426/426. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. A repeticao final de `dotnet --info` confirmou SDK 10.0.401 com exit 0. Smoke TCP/headless loopback com mapa de teste cujo AttributeMap inteiro e `0x40` confirmou ataque fisico positivo (dano 1; HP 78->77), RemoveMob(type 1) para slot vazio 24999 antes do Attack, slot preservado e indice 25000 sem remocao. O primeiro harness multi-alvo recusou a fixture antes de enviar ataque por exigir HP do alvo >=100000; o smoke apropriado de slot vazio passou. Host encerrado cooperativamente e porta 18283 livre. Sem WYD.exe visual, deploy, Site/producao, commit ou push. Evidencias em `Server/Backup/Build/AttackPeacefulMapGate-20261014/validation.txt`.

## COMBAT-ATTACK-EMPTY-TARGET-SLOT-001 - IDs invalidos e MOB_EMPTY (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:341-353`, `TMSrv/Basedef.h:121`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:309-321`, `Source/Code/Basedef.h:167`) definem `MAX_MOB=25000`. Cada slot com `TargetID <= 0` ou `TargetID >= MAX_MOB` e ignorado antes de acessar `pMob`. Para indice valido, as duas fontes chamam `DoRemoveHide` se Class 3; em seguida, se `Mode == MOB_EMPTY`, enviam `SendRemoveMob` privado ao atacante com type 1 e continuam o lote. O branch nao reescreve `TargetID` nem `Damage`. Essa ordem e anterior ao gate de NPC/cabeca/area, alvo morto, Merchant, visao, marcador e Type24.

`LegacyAttackTargetSlotGate` classifica os limites e a ocupacao dos mapas de participante, NPC e summon. O Host aplica a classificacao antes do gate de marcador: ID valido ausente recebe `RemoveMob(type=1)`; indice invalido nao recebe remocao; ambos preservam o slot e nao chegam a type77. Slots de acao seguem no batch e a confirmacao agregada restaura os slots preprocessados. As remocoes preprocessadas e as remocoes de visao do batch sao ordenadas pelo indice antes do Attack. Os batches internos do WorldHub nao tratam IDs desconhecidos como slots vazios, pois sao usados por chamadas de dominio que nao representam um pacote recebido. O alvo vazio nao tem snapshot de mob/affect no WorldHub, portanto nao ha estado de Hide a limpar; a referencia chama o helper de classe 3 antes do teste MOB_EMPTY.

`AttackViewGateTests` cobre ID 0, 24999 vazio, 25000 fora do limite e alvo ativo. Engine.Tests: 426/426. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info` exit 0 confirmou SDK 10.0.401. Smoke TCP/headless loopback passou: RemoveMob privado antes do Attack, Damage 75 do alvo vazio preservado, slot seguinte causou dano, e ID 25000 foi preservado sem RemoveMob. As contas descartaveis foram salvas apos logout. Sem E2E visual do WYD.exe, deploy, Site/producao, commit ou push; sem mudanca wire.

## COMBAT-ATTACK-VIEW-GATE-001 - gate de visao em MSG_Attack (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:379-382`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:347-350`) comparam a posicao do atacante com a do alvo em X/Y usando `VIEWGRIDX/Y=33` (7.69 `TMSrv/Basedef.h:109`; W2PP `Basedef.h:155`). A comparacao e inclusiva: delta ate 33 passa pelo gate e delta 34 em qualquer eixo envia `SendRemoveMob(atacante, alvo, 1, 0)` e continua o lote. Skill 42 e isenta deste branch. O gate vem depois de NPC/head-area, HP e Merchant, antes do Damage marker. O branch nao limpa nem sobrescreve TargetID/Damage do slot. O relay final usa `GridMulticast` na posicao do atacante, exceto para skill 102 (`_MSG_Attack.cpp:2370-2371` em 7.69; `:1749-1750` em W2PP).

`LegacyAttackViewGate` modela os limites e a excecao. O `WorldHub` produz um efeito de remocao por slot fora de visao nos batches fisico/skill; o Host tambem o aplica a slots que seriam rejeitados por type77. A remocao e enviada diretamente ao atacante antes do Attack agregado. Como `AttackConfirmation` inicia com slots vazios, o Host recoloca TargetID e marcador originais nos slots que passam por esse gate. O multicast agregado e restrito aos participantes na visao do atacante e exclui o proprio atacante, que recebe sua confirmacao diretamente; skill 102 mantem a ausencia de multicast da referencia. Sem alteracao de layout, campo ou opcode.

`AttackViewGateTests` cobre borda inclusiva 33, fora de visao em 34, skill 42, slots fisico/skill, continuacao do lote e preservacao do slot. Engine.Tests: 426/426. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false -v minimal`, SDK 10.0.401: 0 erros e um CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas terminou com codigo 1 por acesso negado ao Service Control Manager. Smoke TCP/headless loopback passou com os marcadores -2 e 75: RemoveMob privado(type 1) antes do Attack agregado, slots preservados e nenhum frame recebido pelo alvo. Comando e saida em `Server/docs/CLIENT_E2E_SMOKE.md`. Nao executamos WYD.exe visual, deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-MERCHANT-GATE-001 - gate comum de Merchant em MSG_Attack (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:372`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:340`) limpam `TargetID` e `Damage` e continuam o loop quando `MOB.Merchant` e 1, 4, 43 ou 100. O gate vem depois de NPC/head-area e HP, mas antes de visao/distancia, Damage marker e do gate Type24. Merchant 72 nao pertence a essa lista e permanece elegivel ao fluxo posterior.

`LegacyAttackMerchantGate` centraliza os quatro valores. O `WorldHub` consulta Merchant nos snapshots de participante, NPC e summon, e o batch produz override `(TargetID=0, Damage=0)` antes de executar o slot; slots seguintes continuam. No Host, o gate tambem antecede type77 para marcador invalido. As checagens especificas de Merchant que ja existiam em algumas skills permanecem, agora precedidas pela regra comum. Nao houve alteracao no wire.

`AttackMerchantTargetGateTests` cobre os quatro valores bloqueados, Merchant 72, limpeza wire e continuacao para alvo vivo. TDD RED confirmou a ausencia do helper/query/resultado fisico; GREEN: Engine.Tests 425/425. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false`: 0 erros e 1 CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; SDK 10.0.401. `dotnet --info` terminou em exit 1 por acesso negado ao Service Control Manager, embora tenha identificado o SDK. Smoke TCP/headless nao executado: faltam as fixtures `Backup/Build/E2E7670/account/T/TESTE`, `TESTE2` e `Client/7670/ItemList.bin` que o harness exige. E2E visual, deploy, Site/producao, commit e push nao executados.

## COMBAT-ATTACK-DEAD-TARGET-CLEANUP-001 - limpeza de alvos mortos em MSG_Attack (14/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:363`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:331`) limpam `TargetID` e `Damage` quando o HP do alvo e `<= 0`, exceto `SkillIndex` 31 e 99, e continuam o loop. Esse gate vem depois da limpeza Type 28 e do bloqueio NPC/cabeca/area, mas antes de Merchant, visao/distancia e validacao do Damage marker. O W2PP envia `RemoveMob`; na 7.69 a chamada esta comentada, portanto o C# segue 7.69 e nao envia essa remocao.

`LegacyAttackDeadTargetGate` concentra a condicao de HP e as duas excecoes. `WorldHub` consulta snapshots de participante, NPC e summon sob lock; no batch, limpa slot morto na ordem do loop, produz override `(TargetID=0, Damage=0)` e continua, com interrupcao terminal mantida para falha de summon `InstanceType 9`. No Host, o mesmo gate precede `type77` para slots que ja chegam com HP nao positivo. Type24, NPC head-area e Hide mantem a precedencia existente. Nao houve mudanca no protocolo.

`AttackDeadTargetCleanupTests` cobre HP zero/negativo, as skills 31/99, alvo NPC morto com limpeza wire e continuacao do slot seguinte. A regressao Type 9 foi reproduzida e corrigida mantendo o `break` terminal legado. Engine.Tests: 424/424. Build completa `WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false`: 0 erros e 1 CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; SDK 10.0.401. `dotnet --info` terminou em exit 1 por acesso negado ao Service Control Manager, embora tenha identificado o SDK. O smoke TCP/headless nao foi executado: faltam as fixtures `Backup/Build/E2E7670/account/T/TESTE`, `TESTE2` e `Client/7670/ItemList.bin` que o harness exige; porta 18283 estava livre.

Limite conhecido: o Host classifica marcadores invalidos antes de executar o batch. Se um slot valido anterior matar o mesmo alvo no mesmo frame e um slot posterior desse alvo tiver marcador invalido, o precheck ainda pode observar o HP anterior e contar type77, diferindo da ordem sequencial 7.69. E2E visual, deploy, Site/producao, commit e push nao executados.

## COMBAT-ATTACK-SAMARITANO-CLEANUP-001 - limpeza de Type 24 em MSG_Attack (13/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:298`, `Server.cpp:10279-10299`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:274`, `Server.cpp:9129-9149`) chamam `DoRemoveSamaritano` para o atacante apos o debito de mana e a limpeza de Hide, antes do loop de alvos. Os helpers coincidem: percorrem os slots em ordem, limpam os oito bytes do primeiro affect Type 24, recalculam `GetCurrentScore`, enviam `SendScore` e encerram o loop.

O score loop aplica cada Type 24 sequencialmente a ArmorClass, somando `Ac / 4 + Value` (7.69 `Basedef.cpp:3588-3592`; W2PP `Basedef.cpp:4225-4229`). O estagio C# `CalculateSamaritanArmorClassAffectStage` ja reproduz essa formula. `WorldHub.TryClearFirstSamaritanAffect` limpa apenas o primeiro registro e recompõe o snapshot pelo projetor de score existente; o Host envia UpdateScore ao atacante e recria a confirmacao com o MOB atualizado antes de processar os slots. Diferente de `DoRemoveHide`, nao ha gate de classe para Type 24. Falha de ItemList/projecao rejeita a tentativa sem alterar o estado. Nenhum campo ou opcode wire mudou.

`AttackSamaritanoAffectTests` cobre limpeza de um registro por chamada, preservacao dos demais slots, re-projecao do score, estado autoritativo e falha fechada sem ItemList. RED confirmou a ausencia da API; GREEN passou Engine.Tests 423/423. Build `WydCdk.Engine.slnx --no-restore --no-incremental -m:1`, SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info` exit 0 confirmou SDK 10.0.401. Evidencias em `Server/Backup/Build/AttackSamaritanCleanup-20261013`. Sem smoke TCP/headless ou E2E visual, deploy, commit ou push.

## COMBAT-ATTACK-HIDE-BREAK-001 - remocao de invisibilidade em MSG_Attack (13/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:295-296,346-347`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:271-272,314-315`) chamam `DoRemoveHide` no atacante e em cada alvo valido. `Server.cpp` nos dois codigos remove o primeiro registro de oito bytes com `Type == 28`, chama `GetCurrentScore` e envia `SendScore` (7.69 `Server.cpp:10257-10273`; W2PP `Server.cpp:9107-9123`). Ambos limitam o helper a conexoes de jogador e removem o efeito apenas para Class 3.

Os score loops adicionam `RSV_HIDE` (`0x10`) para Type 28: TMProject 7.69 (`Basedef.cpp:3582-3583`) e W2PP (`W2PP/Source/Code/Basedef.cpp:4424-4425`). `LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage` agora projeta o bit. `WorldHub.TryClearFirstClassThreeHideAffect` limpa exatamente o primeiro registro e recompõe o snapshot pela projecao de score auditada. O Host envia UpdateScore ao atacante depois do debito de mana e ao alvo antes de validar head, HP, Merchant, distancia ou Damage marker. Alvos que nao sao participantes nao passam pelo helper. Nenhum opcode ou tamanho wire mudou.

`AttackHideAffectTests` cobre o bit Type 28, limpeza de um registro por chamada, preservacao dos affects e RSV restantes, HP/MP, nao aplicabilidade a outras classes e falha fechada sem ItemList. RED confirmou a ausencia da API; GREEN passou Engine.Tests 422/422. Build `WydCdk.Engine.slnx --no-restore --no-incremental -m:1`, SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info` exit 0 confirmou SDK 10.0.401. Evidencias em `Server/Backup/Build/AttackHideAffectBreak-20261013`. Sem smoke TCP/headless, E2E visual, deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-NPC-HEAD-AREA-001 - gate comum de cabeca NPC por tile (13/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:353-361`) e W2PP (`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:323-331`) coincidem: para `idx >= MAX_USER`, cabeca `MOB.Equip[0].sIndex` 219 ou 220 e coordenadas em tile `(26,12)` (`TargetX / 128 == 26` e `TargetY / 128 == 12`), limpam `TargetID` e `Damage` e executam `continue`. O branch vem antes dos gates de HP, Merchant, visao/distancia e do Damage marker; nao soma crack-error.

`LegacyAttackNpcHeadAreaGate` replica os indices e a divisao inteira por 128. O `WorldHub` consulta a cabeca dos snapshots NPC/summon e as posicoes autoritativas, sem aplicar a regra a jogadores. Os caminhos fisico e skill retornam sem outcome antes de efeitos; o lote segue para os slots seguintes. No Host, a consulta tambem precede o registro do type77 para um marcador invalido, mantendo a ordem do branch comum. A confirmacao ja inicializada limpa TargetID/Damage, e nao ha mudanca de campo ou opcode wire.

`AttackNpcHeadAreaGateTests` cobre as duas cabecas, limites do tile e casos negativos de jogador, outra cabeca e tile adjacente. O batch bloqueia um alvo fisico e um de skill, preserva HP e continua ate um alvo comum que recebe dano. RED falhou pela ausencia da regra e dos resultados dedicados; GREEN passou Engine.Tests 421/421. Build `WydCdk.Engine.slnx --no-restore --no-incremental -m:1`, SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` exit 0 confirmou o SDK 10.0.401. Evidencias locais em `Server/Backup/Build/AttackNpcHeadArea-20261013`. Sem smoke TCP/headless ou E2E visual, deploy, Site/producao, commit ou push.

## COMBAT-ATTACK-TYPE24-001 - alvo fisico adicional em MSG_Attack curto (13/10/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:471-475`) e W2PP
(`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:431-435`) coincidem: depois
dos gates de alvo, marcador, elegibilidade e distancia, um slot fisico com
`i > 0` em pacote menor que `sizeof(MSG_AttackTwo)` soma
`AddCrackError(10,24)` e continua o loop quando o atacante nao e classe 3 e
nao tem `LearnedSkill & 0x40`. O branch nao escreve o `Dam[i]` antes do
`continue`; o relay final conserva o `TargetID` e marcador `-2` desse slot.

A medicao x86 do header efetivo do cliente TMProject (`Projects/TMProject/Basedef.h`;
`TMFieldScene.cpp:9287-9297` envia `sizeof(MSG_AttackOne)`) deu
`MSG_Attack=168`, `MSG_AttackOne=72`, `MSG_AttackTwo=80`,
`STRUCT_DAM=8`. O header servidor 7.69/W2PP declara `MSG_AttackOne=68` sob
`pack(1)`, mantendo offsets dos campos; os 4 bytes extras do One do cliente
sao padding final. O parser C# aceita 72 apenas para o opcode One, le um slot
e o relay preserva 72 bytes limpando o padding. Para o opcode Two, aceita 76..80:
76 e o menor frame que contem os dois `STRUCT_DAM` completos e permite executar
com seguranca o gate type24; tamanhos abaixo de 76 continuam rejeitados para
nao ler fora do frame. A adaptacao e por opcode/estrutura comprovada, nao por
inferir decoder somente pelo tamanho.

`LegacyAttackType24Gate` aplica a condicao depois da resolucao do alvo e do
alcance no WorldHub. Classe 3 e bit aprendido `0x40` permitem o segundo hit;
caso contrario o outcome bloqueia somente esse alvo. O Host soma 10/type24 e
preserva `TargetID/-2` na confirmacao agregada, enquanto o primeiro slot segue
normalmente. Testes cobrem tamanhos/opcodes, padding, marcador no relay, contador,
excecoes e ausencia de mutacao no segundo alvo. Engine.Tests 420/420; build da
solution SDK 10.0.401: 0 erros e CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. Evidencias em
`Server/Backup/Build/AttackType24-20261013`. O ZIP runtime-only de 197 arquivos tem SHA-256 `A4E459C038B121F3212852117E64D100E4B047085C6C4550C5EC63D4524966EF`; readback independente confirmou 197/197 hashes no sandbox. Backup remoto `C:\WYDCDK\Server\test-releases\backup-combat-type24-20261013-2f6c647b` preserva os 201 arquivos do runtime anterior e os dois scripts, com hashes conferidos. UP Running em 8281; PVP permaneceu Ready, sem listener 8282. Sem smoke Host/TCP ou E2E visual; sem mudanca de campo/opcode wire, Site/producao,
commit ou push.

## COMBAT-ATTACK-DAMAGE-MARKER-001 - validação de Damage[] no MSG_Attack (13/10/2026)

TMProject 7.69 (`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:386-398`)
e W2PP (`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:354-366`) leem o dano
declarado de cada slot. Os marcadores -2 (físico), -1 (skill) e 0 passam sem
erro. Qualquer outro valor não zero é substituído por zero, chama
`AddCrackError(10, 77)` e continua ao próximo alvo; o TargetId permanece no
slot. As duas fontes coincidem neste gate.

`LegacyAttackDamageMarkerGate` classifica os marcadores antes de criar o lote
de ações. O Host soma o erro ao contador compartilhado da sessão, escreve
Damage=0 preservando o TargetId na confirmação inicial e reaplica esses slots
na resposta agregada quando o lote termina normalmente. Os demais alvos
continuam sendo processados. O harness TCP/headless de slots mistos agora envia
um marcador inválido junto a físico e skill e exige `TargetId` com dano zero na
resposta agregada; ele não foi executado porque não havia listener local em
8281/8282. Nenhum campo ou opcode wire mudou.

Engine.Tests: 419/419, incluindo os três marcadores aceitos e valores inválidos
positivos/negativos nos limites de `int`. RED antes da implementação falhou na
compilação pela ausência da regra. Build `WydCdk.Engine.slnx --no-restore
--no-incremental -m:1`, SDK 10.0.401: 0 erros e CS8604 preexistente em
`tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256`. `dotnet --info`
identificou o SDK, mas o runner recebeu acesso negado ao Service Control
Manager. O harness de rede foi compilado, mas não executado; não houve smoke
TCP/headless, E2E visual, deploy, commit ou push.
O type24 comum de alvos foi concluido em COMBAT-ATTACK-TYPE24-001.

## COMBAT-ATTACK-SKILL-FURIA-DIVINA-001 - Fúria Divina skill 6 (13/10/2026)

TMProject 7.69 (Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1367-1445)
e W2PP (Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:887-963) compartilham os
gates: Merchant de NPC diferente de zero, heads 219/220/362,
GenerateIndex 8/9 e Clan 6 encerram o lote. O destino inicial da ação começa na
posição do atacante e avança um tile em cada eixo na direção do alvo.
GetEmptyMobGrid aceita a célula inicial se vazia ou ocupada pelo próprio alvo;
caso contrário busca em anéis de raio 1..9 e exige altura diferente de 127
(Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/GetFunc.cpp:2231-2273).

TMProject define dano 1 antes da busca/chance. A chance base é Special[1]/10+20
contra jogador e Special[1]/5+40 quando idx > MAX_USER; soma a diferença de
nível ajustada por ClassMaster e deslocada à direita em dois bits, com teto de
50 e teste rand()%100 < chance. Chance falha mantém o dano e permite os próximos
alvos. W2PP omite a atribuição de dano 1 e encerra o lote quando a rolagem falha.
O port segue a referência 7.69.

O C# concentra a fórmula em LegacyFuryDivineMath e usa a busca compartilhada de
grade com raio 9, cuja ocupação inclui jogadores, summons e NPCs. Em sucesso, o
Host envia a Action existente com o alvo como ator, Effect=2 e Speed=6,
diretamente ao alvo jogador e à sua visão. Para NPC dentro da visão de 23 tiles,
também registra Mode=Combat, CurrentTarget e EnemyList no estado disponível do
WorldHub. Nenhum campo ou opcode wire mudou.

Engine.Tests: 418/418. RED antes do port: 417/418, com falha pelo dano genérico
5 em vez de 1. Build WydCdk.Engine.slnx --no-restore --no-incremental -m:1,
SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em
tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256. dotnet --info
identificou o SDK, mas o runner recebeu acesso negado ao Service Control
Manager. Cobertura inclui chance/boundary, falha que continua o lote, gates
terminais, relay de jogador e estado de combate NPC.

Limites deste recorte: NPC usa ClassMaster Mortal porque LegacyWorldNpc ainda
não expõe Extra.ClassMaster; o modelo não persiste Route[0] do servidor legado.
Os testes usam estado headless sem mapa de altura real. Não houve smoke TCP,
E2E visual com Client\7670\WYD.exe, deploy, commit ou push.

## COMBAT-ATTACK-SKILL-POST-DAMAGE-MODIFIERS-001 - ForceDamage e PvPDamage em skills (13/10/2026)

TMProject 7.69 (`TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1776-1794`) e W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1309-1328`) aplicam `ForceDamage` depois do dano da skill: se `dam <= 1`, substituem o dano por ForceDamage; se `dam > 1`, somam o valor. Em seguida, somente contra jogador, aplicam PvPDamage: para `dam <= 1`, `dam += dam * PvPDamage / 100`; para dano maior, a ordem inteira e `dam += dam / 100 * PvPDamage`. O estagio vem antes dos gates PK e de miss na referencia.

O C# usa `GetLegacyForceDamage` e `GetLegacyPvpDamage` no caminho de skill contra jogador antes de `ResolveLegacyAttackMiss`; contra NPC aplica somente ForceDamage no fechamento do dano, inclusive no caminho direto da skill 79. Nenhum campo wire mudou. A regressao deterministica usa skill 79 com 59 de dano, ForceDamage=80 e PvPDamage=5: jogador recebe 144 (139 + (139 / 100 * 5)); NPC recebe 139.

Engine.Tests: 417/417. RED anterior a implementacao: 416/417, caso novo encontrou dano 59 sem modificadores. Build `WydCdk.Engine.slnx --no-restore --no-incremental -m:1`, SDK 10.0.401: 0 erros e aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info` confirmou o SDK, com erro de permissao do runner ao consultar o Service Control Manager. Publish self-contained `win-x64`: 200 arquivos, ZIP SHA-256 `DDE0B66AE4E50501CB8E2EBB45BD4DF248A3C9C2FCD82DAEC85395ECC97C3E0B`; hashes locais Core `4C37747F4CB0D9A49C2C14CA680BE18AF329BFC822D0B834A6FEA69BED5A8AB7`, World `797A5B013989323183B3378F6AB246629691FE161CFCE90531B01C5DE7F3F648`, Protocol `C46B13D70774820AA0A833B1C1E0F11AA3CD0699B79958FEC2E66C28F7464A39`.

Na primeira tentativa de deploy, o runtime encerrou porque a autenticacao exige `--item-data`. Com autorizacao, `run-up-20260924.cmd` e `run-pvp-20260924.cmd` foram ajustados para usar `C:\WYDCDK\Server\test-releases\20260924-v7670\data\ItemList.bin`, copiado de `Client/7670/Runtime/Interface0/ItemList.bin` (1.066.004 bytes; SHA-256 `8B2407F9055FE4FD92F29BDBD4A943559CC0C8711753B9FE67BE09C080ABB625`). O runtime self-contained passou 200/200 hashes no staging e no caminho ativo, e UP reiniciou com `WydCdk.Server.Core.exe` correto escutando em 8281. PVP continua `Ready`, sem listener em 8282, como antes. O runtime anterior, scripts originais, staging e manifesto foram preservados em `C:\WYDCDK\Server\test-releases\20261013-attack-skill-post-damage-run2`. ZIP, publish e logs locais estao em `Server/Backup/Build/AttackSkillPostDamageModifiers-20261013`. Sem E2E visual, commit ou push.

## COMBAT-ATTACK-TRANSFORMATION-SKILLS-001 - skills de transformação Type 16 (13/10/2026)

TMProject 7.69 (`TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1743-1747`) reconhece skills 64, 66, 68, 70 e 71 como transformação; `SkillData.csv` define os valores Type 16 de 1 a 5 para Lobisomem, Homem-Urso, Astaroth, Titã e Éden, respectivamente. W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1243-1247`) possui o ramo equivalente, mas diverge nos bonuses Type 16 calculados pelo score; o C# segue TMProject 7.69.

O WorldHub trata a mudança do próprio personagem e as transformações de outro alvo, grava Type 16 e recompõe score/equipamento pelo `LegacyCurrentScoreLoginProjector`. A operação exige ItemList e falha fechado se a projeção não puder ser concluída. O Host envia UpdateScore e UpdateEquip ao alvo e aos participantes na visão dele; UpdateAffect segue diretamente ao alvo transformado. Nenhum campo ou opcode wire foi alterado.

O logout agora captura e salva os 256 bytes de Affect junto ao MOB/MOBEXTRA nos stores de arquivo e MariaDB. Isso acompanha TMProject 7.69 (`Server.cpp:8910-8945`) e W2PP (`Server.cpp:7845-7877`), que incluem Affect no `MSG_DBSaveMob`; antes desta alteração o C# salvava o head transformado mas descartava o Affect, quebrando o relogin. Engine.Tests cobre os cinco valores e persistência/releitura, inclusive tamanho inválido; o smoke de rede cobre a skill 64, relay visual `UpdateEquip` `0x9016` ao ator e observador, logout, bytes persistidos e relogin com head/Type 16 restaurados.

Engine.Tests: 416/416. Build `WydCdk.Engine.slnx --no-restore --no-incremental -m:1`, SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. Build do harness E2E: 0 avisos/erros. Host/TCP headless em 127.0.0.1:18283 passou; logs e fixture descartável em `Server/Backup/Build/AttackTransformationSkills-20261013/`. `dotnet --info` está registrado no mesmo diretório. Não executamos WYD.exe visual, MariaDB real, deploy, commit ou push.

## COMBAT-ATTACK-SKILL-102-DELAY-001 - duracao de Affect/Tick da skill 102 (13/10/2026)

TMProject 7.69 (`TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:177-178,1707-1727`) inicializa `Delay=100` e `Special=0`; para a skill 102, Mortal define Delay=200, Arch deixa o padrao 100 e os demais ClassMasters definem 200, reduzindo quatro por nivel abaixo de 39, mantendo 200 entre 39 e 198 e usando 400 a partir de 199. O handler passa o mesmo Delay a `SetAffect` e `SetTick`. W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:155-156,1208-1227`) tem a mesma formula e ordem.

`LegacySkillCombatMath.GetSkillAffectDelay` concentra essa regra. O WorldHub usa o helper nos caminhos de efeito de jogador; para o alvo NPC da skill 102, grava Affect e Tick juntos, mantendo as verificacoes de merchant e `AffectResist`. A regressao cobre Mortal, Arch, niveis 38/39/198/199, o par Affect/Tick em NPC e o delay generico de uma skill diferente. O wire e os relays existentes nao mudaram.

Engine.Tests: 415/415. Build `WydCdk.Engine.slnx --no-restore --no-incremental -m:1`, SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`; `dotnet --info` confirma SDK 10.0.401. Evidencias em `Server/Backup/Build/AttackSkill102Delay-20261013/`. Sem smoke TCP/Host ou E2E visual, deploy, commit ou push.

## COMBAT-ATTACK-SKILL-EXTERMINAR-001 - Exterminar skill 22 (13/10/2026)

TMProject 7.69 aplica a resistencia elemental comum em `_MSG_Attack.cpp:681-694`
antes do branch Exterminar (`:1449-1481`). O branch soma
`(dam + CurrentScore.Mp) + (Int / 2)`, zera `CurrentScore.Mp` e
`MSG_Attack.CurrentMp`, e deixa a atribuicao de ReqMp comentada. Para a acao,
parte de `pMob[idx].TargetX/TargetY`, busca uma grade vazia, pula os heads 219 e
220, chama `GetAction` para o caster com Type=2 e Speed=2, faz GridMulticast e
envia uma copia ao caster.

W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:969-1018`) reimplementa o calculo
no branch: multiplica AC por 3 contra jogador, zera tambem `ReqMp`, constroi a
acao para o alvo e encerra o loop apos encontrar grade livre. O port segue o
contrato 7.69: o C# usa o calculo comum e a resistencia ja aplicada, preserva
RequestedMana, e relaya uma Action do caster com Effect=2/Speed=2 para os
observadores e para o proprio caster. A habilidade nao interrompe outros
slots por causa do relay; SkillData 7.69 define MaxTarget=1.

WorldHub aplica o bonus e o consumo de MP tanto contra jogadores como NPCs.
`LegacyMobGridSearch` considera ocupacao de jogadores, summons e NPCs, e usa o
mapGrid opcional com verificacao do terreno candidato; sem mapGrid, a busca
trata o terreno como livre. O relay usa a posicao atual autoritativa do alvo
como origem da busca: este WorldHub ainda nao armazena `TargetX/TargetY` de cada
participante/NPC, entao esse detalhe pode divergir de um alvo em movimento na
referencia. Os heads 219/220 continuam recebendo dano e consumindo MP, mas nao
recebem a acao visual. Nenhum campo wire foi adicionado; o host cria
`ActionRequest` com o contrato existente.

Engine.Tests cobre o dano pos-resistencia, o consumo de Current MP com
RequestedMana preservado, os dois tipos de alvo e a supressao visual para ambos
os heads especiais. Resultado: 414/414. Build `WydCdk.Engine.slnx --no-restore
--no-incremental -m:1`, SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. `dotnet --info` registrou SDK 10.0.401.
Evidencias em `Server/Backup/Build/AttackExterminar-20261013/`. Sem smoke
TCP/Host, E2E visual, deploy, commit ou push.

## COMBAT-ATTACK-SKILL-RENASCIMENTO-001 - Renascimento skill 31 (13/10/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1492-1531`)
calcula primeiro `(rand() % 10 + 10) * ((caster.MaxHp + 1) / 100)`, zera
`CurrentScore.Mp` e `MSG_Attack.CurrentMp`, envia SetHpMp ao caster e so entao
rola sucesso `< 70`. Em sucesso, grava o HP do alvo, zera `NumError`, envia
Score/SetHpMp/Etc e procura Type 55. Se o affect ja existe ou nao ha slot livre,
o alvo permanece revivido e o branch continua sem substituir affects. Caso
contrario, grava Type 55 com Value=0, Level=0, Time=2, chama GetCurrentScore,
envia novo Score e faz multicast de CreateMob. A linha SkillData 31 tem
TargetType=1 e InstanceType=8. O W2PP (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1038-1057`)
preserva a ordem das rolagens, a formula, o custo de MP e o reset de NumError,
mas nao possui a etapa Type 55/GetCurrentScore/CreateMob; o port segue a 7.69.

O WorldHub implementa a habilidade apenas para jogador-alvo, inclusive alvo
morto, com rolagens injetaveis nos testes. O HP usa MaxHP do caster e a rolagem
de HP ocorre antes da chance; sucesso/falha zera apenas o MP atual do caster e
preserva RequestedMana, conforme o helper SetHpMp da referencia. O sucesso
restaura HP mesmo com Type 55 preexistente ou tabela de affects cheia; quando
ha slot livre, adiciona Type 55 sem alterar um affect anterior. O Host envia
SetHpMp do caster tambem na falha; em sucesso zera o contador de erro da sessao
do alvo, envia score/recursos/Etc e, para affect novo, UpdateAffect e CreateMob
para observadores. O handler atual emite esses relays depois do Attack agregado,
enquanto a 7.69 envia as atualizacoes durante o branch. Nao houve mudanca de
wire.

TDD RED reproduziu `TargetNotAlive`; depois da implementacao, Engine.Tests
passou 413/413. Build da solution SDK 10.0.401: 0 erros e 1 aviso CS8604
preexistente em `AttackDivineBlessingTests.cs:256`. Evidencias em
`Server/Backup/Build/AttackRenascimento-20261013/`. Nao foi executado smoke
TCP/Host nem E2E visual. O C# ainda nao reproduz o GetCurrentScore completo da
7.69; Type 55 nao tem ramo proprio no score loop auditado. Sem deploy, commit
ou push.

## COMBAT-ATTACK-SKILL-JUDGMENT-DIVINE-001 - HP inicial e custo por alvo (13/10/2026)

TMProject 7.69 captura o HP do atacante antes do loop (`TMSrv/_MSG_Attack.cpp:305`), aplica `dam += hp` no branch da skill 30 (`:1483-1488`) e envia um `SendSetHpMp` depois do relay agregado (`:2373-2374`). `SendSetHpMp` recalcula ReqHp a partir do HP atual (`TMSrv/SendFunc.cpp:1725`). W2PP usa o mesmo HP inicial e custo `CurrentScore.Hp / 6 + 1` por alvo, e grava `ReqHp` no proprio branch (`W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1028-1033,1752-1753`).

O WorldHub agora captura uma vez o HP inicial por lote, soma esse valor ao dano depois da resistencia elemental e antes da rolagem de miss, e cobra `HP / 6 + 1` por alvo valido em jogadores e NPCs. CurrentScore.Hp e RequestedHp recebem o valor calculado em cada slot; os outcomes marcam a mutacao de recurso. O Host suprime os relays por slot dessa skill e envia um unico SetHpMp ao atacante depois do Attack agregado quando o lote termina normalmente. Nenhuma alteracao de wire.

Engine.Tests inclui um lote jogador/NPC/jogador: cada dano usa os mesmos 800 HP iniciais, enquanto o HP do caster segue 800 -> 134 -> 23 -> 4. Outro caso confirma a cobranca de HP mesmo quando a rolagem resulta em miss. Resultado final: Engine.Tests 375/411; 36 falhas de acesso a arquivos, sockets ou startup restritos do runner. Build completa da solution SDK 10.0.401 sem erros; CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. `dotnet --info` identificou SDK 10.0.401, mas falhou ao consultar o Service Control Manager. Sem smoke TCP especifico desta skill, E2E visual, deploy, commit ou push. Evidencias em `Server/Backup/Build/AttackJudgmentDivine-20261013`.

## COMBAT-ATTACK-SKILL-MORTAR-001 - Morteiro skill 97 (12/10/2026)

TMProject 7.69 `TMSrv/_MSG_Attack.cpp:206-229` trata skill 97 no bloco
96..100. Antes do debito comum de mana, exige `PosX` e `PosY` estritamente entre
0 e 4096, consulta `pItemGrid[PosX][PosY]`, exige um slot valido cujo item tem
`sIndex == 746` e define `m->Motion = 1`; coordenada/item invalido encerra o
handler. A indexacao e assimetrica: a grade do mundo armazena coordenadas no
sentido `[Y][X]`, mas este branch le `[PosX][PosY]`.

No W2PP, `_MSG_Attack.cpp:158-194` coloca o trecho de morteiro dentro de
`skillnum < 0 || skillnum >= MAX_SKILLINDEX`. O handler ja trata indices validos
no bloco `0 <= skillnum < MAX_SKILLINDEX` (`:113-152`), entao skill 97 segue o
caminho generico e nao atinge a verificacao de morteiro. O port segue o branch
ativo da 7.69.

O gate C# agora sinaliza `RequiresMortarItem` sem ignorar a verificacao de skill
aprendida. O Host consulta o `WorldHub` depois dos gates de timing/classe e antes
da mana; o WorldHub procura item 746 no ponto transposto entre itens dinamicos e
itens de mapa, com limites 1..4095 e sem retirar o item. Em relay valido, o
`AttackConfirmation` existente recebe Motion=1 tanto no frame inicial quanto no
agregado. O byte Motion ja pertence ao pacote `MSG_Attack`; tamanho e formato nao
mudaram.

TDD: o teste inicial falhou porque o gate retornava `MortarNotPorted`; os novos
casos cobrem a sinalizacao do gate e learned-bit, coordenada transposta e
assimetrica, item errado, limites, consulta sem consumo e byte Motion no wire.
Engine.Tests 410/410. `dotnet build WydCdk.Engine.slnx --no-restore
--no-incremental -m:1`: 0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`, SDK 10.0.401. `dotnet --info` confirmou o
SDK, mas saiu com acesso negado ao Service Control Manager. Sem smoke TCP/E2E
visual, consumo do item, deploy, commit ou push. Evidencias locais em
`Server/Backup/Build/AttackMortar-20261012`.

## COMBAT-ATTACK-SKILL-RESURRECTION-MP-001 - Mana da skill 99 (12/10/2026)

### Referencia e comportamento

Em TMProject 7.69, `TMSrv/_MSG_Attack.cpp:252-279` executa o debito comum antes
dos ramos de efeito: calcula `BASE_GetManaSpent`, verifica MP suficiente,
subtrai o custo e chama `SetReqMp`. O ramo skill 99 (`:1635-1665`) altera HP e
faz recall, sem atribuir MP. A 7.59 segue a mesma ordem (`_MSG_Attack.cpp:473-486`;
skill 99 em `:2102-2129`) e tambem nao atribui MP no ramo. W2PP diverge no ramo
99 (`:1252-1281`): sorteia MP entre 1% e 50% de MaxMp e o grava.

O `TMSrv.vcxproj` seleciona o `Basedef.cpp` comum. Nele,
`BASE_InitializeBaseDef` chama `BASE_InitializeSkill`, que le
`../../Common/SkillData.csv` ou, como fallback, `../../TMSRV/Run/SkillData.csv`
para preencher `g_pSpell`; `BASE_GetManaSpent` usa `g_pSpell[skillnum].ManaSpent`
com `Special` e `SaveMana`. Portanto a tabela e entrada runtime dessa source,
embora o arquivo efetivamente usado por um servidor instalado ainda dependa do
ambiente. As linhas 99 das copias locais verificadas mostram `ManaSpent=0`; isso
nao prova qual arquivo esta instalado em outro runtime. No C#, o custo chega da
tabela explicitamente passada por `--skill-data` e e aplicado antes de
`TryApplySkillAttack`.

### Port e verificacao

O C# retirou o sorteio W2PP do ramo 99. O ramo preserva o `CurrentScore.Mp` e o
`RequestedMana` ja resultantes do debito comum: 321 permanece 321 com custo zero;
MP zero permanece zero; e o caso com custo igual ao saldo zera MP antes da skill,
que continua zero apos HP/recall. Nenhuma logica foi movida para
`GetCurrentScore`; wire e persistencia nao mudaram. As fontes originais ficaram
intactas.

Build da solution (`dotnet build WydCdk.Engine.slnx --no-restore -m:1`): 0 erros,
0 avisos. SDK `10.0.401`; `dotnet --info` registrou o SDK, mas terminou com acesso
negado ao Service Control Manager. Com `TEMP` e `TMP` locais no workspace,
Engine.Tests ficou 406/408: os testes deste recorte passaram; dois testes de
loopback foram bloqueados por `SocketException 10013`. Evidencias em
`Server/Backup/Build/AttackSkillResurrectionMp-20261012`. Sem E2E visual,
publicacao, commit ou push.

## COMBAT-ATTACK-SKILL-VINHA-001 - Book Vinha (12/10/2026)

### Referencia e divergencia

TMProject 7.69 TMSrv/_MSG_Attack.cpp:1618-1631 valida TargetX/TargetY,
chama CreateMob("Vinha", TargetX, TargetY, "npc", 3) e encerra o lote.
W2PP _MSG_Attack.cpp:1153-1168 segue o mesmo fluxo, mas chama
CreateMob(..., "Boss", 3); a release C# segue a pasta npc da referencia
7.69. A condição de atributo mascara o destino com 0x40, mas compara o
resultado com 1, por isso o continue nunca ocorre. O wire declara os
campos como unsigned short em Basedef.h:2553-2573; a grade ativa mede 4096 x
4096. O template de 816 bytes existe em
Servidor/Server/TMSrv/run/npc/Vinha da referência local. Ele não foi copiado
para a árvore ativa.

### Port C# e verificacao

O Host passa as coordenadas UInt16 do AttackRequest ao lote. Para a skill 98,
WorldHub cria um LegacyWorldNpc quando as coordenadas estão dentro da
grade e o catálogo contém o template Vinha; o MOB zera Merchant, recebe HP
igual a MaxHP, affect vazio, GenerateIndex=-1 e RouteType=3. O lote encerra após
o primeiro slot e devolve o NPC para o Host enviar/broadcastar o CreateMob.
WorldHub também exige participante atacante registrado, MOB completo e HP
positivo antes de criar a Vinha.
Quando --npc-root não contém Vinha (ou o catálogo está desativado), a
ação termina sem NPC, como uma falha de CreateMob também não impediria o
break da referência.

O teste cobre carga opcional do template, criação e relay no outcome,
coordenadas válidas e fora da grade, HP/affect, RouteType e parada do lote.
O C# preserva o CurrentScore lido do template; o GetCurrentScore da referência
não é recalculado por este caminho.
dotnet build WydCdk.Engine.slnx --no-restore -m:1 passou com 0 erros e
0 avisos. Engine.Tests ficou
406/408: Skill 98 passou; os dois testes de loopback foram negados pelo sandbox
(SocketException 10013). dotnet --version retornou 10.0.401;
dotnet --info terminou com acesso negado ao Service Control Manager. Os
deslocamentos aleatórios de segmentos, a seleção de célula livre via
GetEmptyMobGrid e a IA/movimento não estão implementados neste recorte; a
posição C# fica exatamente no alvo. Sem E2E visual, publicação, commit ou push.
Logs em Server/Backup/Build/AttackVineSummon-20261012.

## COMBAT-ATTACK-SKILL-CANCELAMENTO-001 - Type 19 e fallback Type 32 (12/10/2026)

### Referencia e divergencia

TMProject 7.69 `_MSG_Attack.cpp:1592-1617` e W2PP
`_MSG_Attack.cpp:1126-1151` seguem o mesmo branch para a skill 47. O loop
procura o primeiro affect Type 19, zera Type/Value/Level/Time, chama
`GetCurrentScore` e `SendScore`, e interrompe os demais alvos do lote. Quando
nao encontra Type 19, o branch nao interrompe o fluxo generico da skill. A linha
47 da tabela 7.69 define AffectType 32, Aggressive=1 e MaxTarget=5, portanto o
fallback generico aplica Type 32.

No score loop, TMProject 7.69 `Basedef.cpp:3678-3680` e W2PP
`Basedef.cpp:4211-4212` fazem Type 19 contribuir com `RSV_BLOCK` (`0x80`).
Nao foi encontrada divergencia entre as duas fontes neste recorte.

### Port C# e verificacao

`LegacyCurrentScoreMath` agora inclui Type 19 na composicao de RSV. Para a skill
47, `WorldHub` remove e zera o primeiro registro Type 19 de oito bytes. O MOB
recalcula apenas o bit RSV_BLOCK a partir dos Type 19 restantes e preserva os
demais bits de RSV. A remocao retorna snapshots atualizados e marca o outcome
para encerrar os slots posteriores do lote, mantendo a finalizacao normal. Sem
Type 19, o caminho generico continua aplicando Type 32 a todos os alvos aceitos.
O mesmo comportamento de estado e coberto para alvos NPC; o relay de rede NPC
nao foi validado neste recorte.

O teste do recorte passou, cobrindo composicao, remocao do primeiro slot,
preservacao de outros affects/RSV, parada do lote, fallback Type 32 e snapshot
NPC. `dotnet build WydCdk.Engine.slnx --no-restore -m:1` passou. Na ultima
execucao, Engine.Tests ficou 405/407: dois testes de loopback nao passaram sob
as permissoes do sandbox, incluindo `SocketException 10013`; os outros 405
passaram. Um rebuild anterior reportou CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. O SDK e 10.0.401. `dotnet --info` foi
registrado, mas o CLI recebeu acesso negado ao Service Control Manager nessa
tentativa. Evidencias em `Server/Backup/Build/AttackCancelAffect-20261012`.
Sem E2E visual, publicacao, commit ou push.

## COMBAT-ATTACK-SUPPORT-SKILLS-41-44-001 - fanout e score (12/10/2026)

### Referencia e divergencia

TMProject 7.69 `_MSG_Attack.cpp:1533-1589` e W2PP
`_MSG_Attack.cpp:1065-1122` implementam as skills 41 (Velocidade) e 44 (Arma
Mágica) com `skill_target = (Special / 25) + 2`, máximo 13 e mínimo 2. O caster
usa `CurrentScore.Special[skillnum % 24 / 8 + 1]`; para ambas as skills isso é
Special4. Cada slot válido passa por `SetAffect`; somente o sucesso chama
`SendScore`. As linhas ativas de TMProject `Basedef.cpp:3827-3860` e W2PP
`Basedef.cpp:3950-4017` coincidem no Type 2: adiciona Value ao Run e define
RSV_HASTE. Type 9 compartilha `add = (Level * 5 / 20 + Value) * 3 / 2`, +5 ao
DAMAGEMULTI e `Damage += add`; em Foema com LearnedSkill `0x80000`, triplica
`add` e soma outros dez ao multiplicador. W2PP também executa `magic += 5`
(`Basedef.cpp:4016`), que TMProject 7.69 não executa. O port segue 7.69 e mantém
Magic do target sem esse incremento. O mapeamento de RSV_HASTE do port é `0x20`.

A tabela 7.69 ativa (`Servidor/Server/Common/skilldata.csv`) define AffectType
2/9 e Value 1/90, `MaxTarget=13`, `PartyCheck=1`, `AffectTime=60` e Range 5.
O fanout e o valor de Type 2/9 concordam; a diferença de `magic += 5` do W2PP
fica documentada e nao foi portada, conforme a source 7.69 escolhida.

### Port C# e verificacao

`LegacySupportSkillTargetLimit` implementa a fórmula e o clamp, limitada às
skills 41/44. `WorldHub` usa o estágio de Special da habilidade (Special4 para
ambas) e ignora slots a partir do limite. Depois de aplicar o affect, recompõe
o snapshot do alvo por `LegacyCurrentScoreLoginProjector`, usando ItemList,
equipamento, MOBEXTRA e affect. Assim o `TargetMobSnapshot` já inclui o score
recalculado. `Program.cs` consome esse resultado no relay existente e envia
UpdateScore seguido de UpdateAffect.

Essa recomposição usa o subconjunto auditado do projetor de login e não declara
paridade completa de `BASE_GetCurrentScore`. Sem ItemList ou em falha de
projeção/narrowing, o caminho retorna `CombatStateUnavailable` sem confirmar a
alteração do affect. Os testes cobrem o limite 2..13, Special=0/25, o RSV_HASTE
e o aumento de Damage do Type 9. Engine.Tests 406/406.

A build padrão `dotnet build WydCdk.Engine.slnx --no-restore` e o restore não
puderam ser repetidos nesta revisão: o runner retorna `NETSDK1060`/`path1` nulo
ao resolver `CommonApplicationData` (o `dotnet --info` também falha no bootstrap
de InstallerBase). Para validar as alterações, World, Server.Core e Engine.Tests
foram compilados diretamente com Roslyn 5.9.0 do SDK 10.0.401; a suíte completa
passou 406/406. O log do build solution 0/0 em `solution-build-pass.log` é da
etapa anterior à recomposição runtime e não é evidência de build final desta
revisão. Evidências atuais em `Backup/Build/AttackSupportSkillTargets-20261012`,
incluindo `manual-full-tests-run.log`. Sem TCP/E2E da skill, E2E visual,
publicação, Site/produção, commit ou push.

## COMBAT-ATTACK-PVP-JEWEL-ABS-001 - recuperacao de HP Type 8 bit 3 (08/10/2026)

### Referencia e divergencia

TMProject 7.69 `_MSG_Attack.cpp:2229-2265` percorre os 32 affects do atacante
depois de fechar o dano do alvo. Para cada `Affect.Type=8` com `Level & (1<<3)`,
calcula `dam * 2 / 100` com truncamento inteiro e teto de 350 HP por slot; HP
igual/acima de MaxHP primeiro zera a recuperacao. Em seguida, se `newHP` ainda
passa MaxHP, a source subtrai MaxHP e usa a diferenca como `RecHP`, inclusive
restabelecendo valor positivo quando o HP ja estava acima do teto. Esse ajuste
pode aumentar o excesso em hits seguintes. O W2PP nao tem branch ativo equivalente
no handler de ataque; o port segue 7.69.

### Port C# e verificacao

`LegacyPvpJewelAttackMath.ApplyAbsorption` implementa a aritmetica pura e
`WorldHub` aplica o resultado ao atacante depois do dano final em ataque fisico,
skill contra jogador e skill contra NPC. O dano usado ja inclui os
modificadores anteriores do handler. Atualiza o HP autoritativo e o HP solicitado
do ator; o wire existente carrega o snapshot final, sem alterar contrato ou
encoder. A injecao antiga `absorptionRoll` permanece por compatibilidade de API,
mas nao altera a recuperacao deterministica 7.69.

`AttackPvpJewelAbsorptionTests` cobre RED/GREEN para dano final, skills contra
jogador/NPC, ordem dos slots e teto por affect, dano nao positivo, HP cheio e o
ajuste historico de MaxHP inclusive quando o HP ja passou do teto. Engine.Tests
400/400. Uma execucao inicial encontrou miss aleatorio
de 1% no teste preexistente `world skill attack`; o rerun passou sem mudancas
nesse teste. Build completa de `WydCdk.Engine.slnx`, SDK 10.0.401: 0 erros e 1
aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. Logs e
`dotnet --info` em `Backup/Build/AttackPvpJewelAbsorb-20261008`. Sem smoke TCP,
E2E visual, publicacao, Site/producao, commit ou push.

## COMBAT-ATTACK-PVP-JEWEL-AGILITY-AUDIT-001 - bit 0 sem efeito no gate (08/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:140-158` calcula `skilldelay` a partir de
`g_pSpell[skillnum].Delay`, subtrai por `RSV_CAST` e por cada Type 8 com
`Level & (1<<0)`, e multiplica o resultado por 1000. A decisao logo depois,
porém, compara `isTime < 700`; `skilldelay` nao participa do resultado. Assim,
o bit 0 nao altera o limite efetivo de 700 ms. W2PP `_MSG_Attack.cpp:128-145`
nem percorre o bit 0 e tambem usa o mesmo limite fixo. O C# preserva esse
comportamento observavel em `LegacySkillAttackGate.SkillDelayMinimumMilliseconds`.

Auditoria somente de source e contrato, sem alteracao de codigo, teste ou build.
Os vetores existentes do gate cobrem a fronteira 699/700 ms. Nao aplicar uma
reducao de atraso pelo bit 0 sem nova evidencia de uma revisao vigente que use o
valor calculado.

## COMBAT-ATTACK-PVP-JEWEL-PARRY-AUDIT-001 - efeito de ataque do bit 6 (08/10/2026)

### Referencia e divergencia

TMProject 7.69 `_MSG_Attack.cpp:1981-1989` percorre os 32 affects do atacante
quando o alvo e jogador e o dano continua positivo. Para cada Affect Type 8 cujo
Level tem `1<<6`, executa `pMob[idx].Parry -= 30` no alvo. Esse efeito nao e o
roll de parry do ataque jogador-jogador: o bloco de `GetParryRate` nesse handler
esta comentado, e o hit usa o fluxo `GetAttackMiss`.

`pMob[idx].Parry` e estado runtime recalculado como
`BASE_GetMobAbility(&MOB, EF_PARRY)` em `CMob.cpp:660`, dentro de
`GetCurrentScore`. O decremento se acumula entre hits elegiveis ate uma nova
recomputacao de score. O caminho `MobAttack` aplica `GetParryRate` ao alvo
jogador usando esse campo (`Server.cpp:11041-11112`). Portanto, o efeito do bit
6 reduz o parry em ataques automaticos posteriores; nao muda a decisao de miss
do hit PvP que executou o decremento.

O score loop e uma segunda ocorrencia independente do mesmo bit:
`Basedef.cpp:4052-4056` define `MOB.LearnedSkill` com `1 << (35 - 7)`. A
`Servidor/Server/Common/skilldata.csv` identifica skill 35 como
`Tempestade_de_Meteoros` e skill 100 como `Concentracao`; esses nomes nao
substituem o comportamento literal dos dois ramos Type 8. O W2PP diverge nos
dois pontos: seu score loop adiciona `Accuracy +50` (`Basedef.cpp:4538-4539`),
e seu handler `_MSG_Attack.cpp:1418+` usa `GetParryRate` para ataques de jogador,
sem o decremento Type 8 encontrado na 7.69.

## COMBAT-ATTACK-PVP-JEWEL-PARRY-INTEGRATION-001 - estado runtime do Parry (08/10/2026)

### Port C# integrado e limites atuais

O C# segue o miss 7.69 nos ataques de jogador. Em hits fisicos contra jogador,
o `WorldHub` aplica o efeito depois do ajuste/floor de ReflectDamage e antes de
ReflectPvP. Na rota de skill aplica depois do miss e do gate Type 6, antes da
mutacao de HP. Ambas chamam `LegacyPvpJewelParryMath.ApplyParryPenalty` somente
quando o dano continua positivo e o alvo e jogador; NPC, miss e dano zero nao
alteram estado.

`Participant.RuntimeParryAdjustment` guarda a soma negativa sem persistencia ou
campo wire. `ResolveLegacyParry`, usado hoje pela rota de ataque automatico de
summon (`WorldHub.cs:3694,7251-7280`), soma o ajuste ao Parry bruto derivado do
equipamento antes do `GetParryRate`. O ajuste zera em `SetCharacterState`, em
escritas de equipamento pelo `WriteLiveItem` e apos o catalisador de montaria no
slot 14. O catalisador tambem atualiza `ClientEquipment[14]`, mantendo o estado
runtime e a lista de equipamento sincronizados.

O C# ainda nao tem um `GetCurrentScore` central ou um caminho geral `MobAttack`.
Logo, o reset cobre os pontos de substituicao de personagem/equipamento modelados
hoje, e o consumo e limitado ao ataque automatico de summon existente. Nao ha
integracao em login, persistencia ou wire.

### Estagio aritmetico e validacao da integracao

`LegacyPvpJewelParryMath.ApplyParryPenalty` implementa somente a aritmetica
pura do ramo 7.69: valida os 32 registros, exige dano positivo e alvo jogador,
e subtrai 30 por cada affect Type 8 com `Level & (1<<6)`. Retorna o Parry bruto,
sem clamp. `AttackPvpJewelParryTests` cobre multiplos affects, tipo/bit diferente,
alvo NPC, dano nao positivo, valor abaixo de zero e snapshot truncado. O teste de
integracao `WorldPvpJewelParryStatefulIntegration` observa o efeito pelo ataque
automatico posterior e cobre hit fisico e skill, dois affects, miss, Type 6,
NPC, substituicao de estado, troca equipada e catalisador.

RED: Engine.Tests 401/402, falhando no comportamento de hit fisico ainda ausente.
GREEN: Engine.Tests 402/402. Build final da solution registrada com SDK
10.0.401; o runner ainda emite o aviso preexistente `CS8604` em
`AttackDivineBlessingTests.cs:256`. Evidencias isoladas em
`Backup/Build/PvpJewelParry-20261008` e de integracao em
`Backup/Build/PvpJewelParryIntegration-20261008`. O recorte nao altera o gate de
700 ms, o roll `GetAttackMiss`, login, persistencia ou wire. O runtime
self-contained foi publicado no sandbox de teste; 197/197 hashes conferem e as
tarefas UP/PVP estao `Running`, com listeners 8281/8282 no executavel esperado.
O backup dos 197 arquivos anteriores e seu manifesto SHA foram preservados em
`C:\WYDCDK\Backup\PvpJewelParryIntegration-20261008`; evidencias de publicacao em
`Backup/Build/PvpJewelParryIntegration-20261008/sandbox-deployment.txt`. SHA-256
do ZIP: `0D8D4013B6012F561C843CD4B27EBC8CFA557A86E175FB14F7CB140242B58FDD`. Nao
houve smoke TCP nem E2E visual.

## COMBAT-ATTACK-SUMMON-NOTICES-001 - avisos privados e retorno do Type 9 (08/10/2026)

### Referencia e divergencia

TMProject 7.69 `_MSG_Attack.cpp:987-1063` envia `SendClientMsg` ao ator durante
o loop de alvos do InstanceType 9. Alvo morto envia aviso e usa `break`, que
encerra os slots seguintes, mas ainda cai na cauda normal do handler: preenche
os campos autoritativos e faz `GridMulticast` do Attack (`_MSG_Attack.cpp:2362-2371`),
alem da finalizacao normal. Zona de invocacao proibida e nivel superior enviam
aviso e continuam. Posicao
zerada, Kefra (`2335..2395 / 3896..3955`), RvR (`1710..1755 / 1968..1995`),
Dungeon Zumbi (`x/128==13 || y/128==16`) e Coliseu N (`x/128==27 || y/128==11`)
enviam `_NN_Cant_Use_That_Here` e retornam do handler, portanto pulam a cauda
normal e suprimem o Attack agregado. Os avisos sao privados e precedem a
confirmacao agregada normal quando o loop termina.

W2PP `_MSG_Attack.cpp:751-785` mantem o aviso de alvo morto e a regra de
`GetAttribute`, mas ainda aplica o limite antigo `target.Hp > actor.Hp + Special
+ 30` (comentado na 7.69), nao inclui os gates 7.69 de nivel/RvR/Kefra/Zumbi/
Coliseu e so retorna para coordenadas zero. A release segue o comportamento
TMProject 7.69 e nao importa esse limite HP/Special.

### Port C# e verificacao

`LegacySkillSummonNoticeMapper` converte `TargetNotAlive`,
`SummonNotAllowedHere`, `TargetTooHighToSummon`, `SummonInvalidPosition` e
`SummonZoneBlocked` para as mensagens privadas correspondentes. No batch,
`TargetNotAlive` termina o loop de slots, mas mantem `CompletedNormally=true`:
preserva finalizacao/relays dos slots anteriores e permite a confirmacao Attack
agregada da cauda normal. `SummonInvalidPosition` e `SummonZoneBlocked` definem
`AbortSlotIndex`, preservam mutacoes/relays anteriores e suprimem slots seguintes,
finalizacao normal e Attack agregado, como o `return` 7.69. O Host escreve cada
`MessagePanel` no instante do slot; a confirmacao agregada Type 9 fica depois dos
relays e so e enviada quando o batch termina normalmente. Outros recusados que
usam `continue` preservam a sequencia.

`AttackSkillSummonNoticeTests` cobre mapeamento das mensagens, `break` de alvo
morto com cauda agregada normal, `return` terminal de zona e continuidade de
alvo com nivel superior. RED/GREEN confirmou que a morte para slots posteriores
sem suprimir a cauda normal. O smoke
`Test-AutoTradeClose.ps1 -AttackSummonNotice` usa duas contas copiadas, loopback
`127.0.0.1:18283`, Foema com skill 42 e Guild sintetica compartilhada para passar
`bParty`. O ator recebeu o `MessagePanel` exato com checksum valido; o peer nao
recebeu aviso, nao houve Attack agregado nem alteracao HP/MP. Engine.Tests
399/399 na execucao final; algumas execucoes intermediarias falharam no teste
preexistente `world physical attack` quando o roll aleatorio de miss de 1% impediu o ataque
letal. A reproducao usa DEX igual e deixa `attackMissRoll` no RNG padrao; rerun
passou sem mudanca de codigo. Build completa SDK 10.0.401 com 0 erros e o aviso
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. Smoke passou, hashes
das contas originais intactos e porta liberada. Evidencias em
`Server/Backup/Build/AttackSummonNotice-20261008` e
`Backup/Build/AutoTradeCloseSmoke/9e6d0233d5774d0db2d7d413881637ab`.
Sem integracao em login/persistencia, E2E visual, publicacao, Site/producao,
commit ou push.

## COMBAT-LOGIN-AFFECT-COMPOSITION-001 - composicao pura de score (08/10/2026)

### Fonte e recorte

O `TMSrv.vcxproj` seleciona `..\Basedef.cpp`, isto e, o helper comum
`Servidor/Source/Code/Basedef.cpp` da TMProject 7.69; `TMSrv/Basedef.cpp` e uma
copia local diferente e nao compilada. No Type 35, o helper comum aplica
`MaxHp += MaxHp * 15 / 100` e `MaxMp += MaxMp * 7 / 100` por affect, com
truncamento depois da multiplicacao. O source compara cada incremento com
`MAX_HP/MAX_MP=100.000`: se o incremento alcanca o limite, atribui 100.000 ao
campo; abaixo dele, soma mesmo que o resultado ultrapasse 100.000. W2PP e a
copia TMSrv nao compilada usam 10%/10%; essa divergencia muda o helper isolado
anterior e foi corrigida para a fonte incluida pelo projeto.

Type 34 e Type 8 tambem seguem o helper comum 7.69 e preservam suas formulas ja
auditadas. O coordenador puro percorre os 32 slots uma vez e aplica as parcelas
de score dos Types 4/34/35/8 na ordem original. Recebe `MOB.Magic` de
origem para os deltas por affect e mantem um acumulador Magic local. A passada AC dedicada projeta Type 24/31/25/16/11 primeiro e Type 12/Type 8 bit 4 depois;
`AfterScoreAffects` consome esse AC junto a MaxHP/MaxMP/Damage/Magic/LearnedSkill e
RSV antes de Soul/Kibita. O bit 1 do Type 8 continua no estagio de resistencia;
bits 0/3 nao entram nesta composicao, e bit 2 fornece RSV_VISION. `RsvWithAffects`
une essa contribuicao a Haste sem alterar o snapshot de entrada.

Type 4 usa a autoridade de source definida pela auditoria do call graph:
`TMSrv.vcxproj` compila `..\\Basedef.cpp`; esse arquivo inclui o header comum
`Basedef.h`, e `TMSrv/CMob.h` tambem inclui `..\\Basedef.h`. O caminho
`CMob::GetCurrentScore` -> `BASE_GetCurrentScore` usa, portanto, a estrutura
comum de 18 slots. A copia privada `TMSrv/Basedef.cpp`, que inclui o header
privado de 16 slots, nao esta selecionada pelo projeto. A autoridade da source
ativa esta resolvida para o helper comum; o binario legado nao foi recompilado
nem verificado. Cada affect Type 4 aplica HP +15%, MP +10%, Damage +30% e Magic
+20% do `MOB.Magic` de origem, com truncamento inteiro. Os deltas de HP, MP,
Damage e Magic sao comparados individualmente com os limites ativos: HP 400.000,
MP 100.000, Damage 1.000.000 e Magic 190.000.000. O W2PP diverge: sem deltas
de HP/MP, soma `DAMAGEMULTI +4`, Damage +30 e Magic +5. A implementação C# esta
em COMBAT-LOGIN-AFFECT-TYPE4-001; a fonte foi auditada, mas o binario legado nao
foi recompilado ou verificado.

### Port C# e verificacao

`CalculateCurrentScoreAffectCompositionStage` compartilha os kernels privados
com os helpers isolados, faz uma unica varredura de slots e
retorna os valores sem mutar `MOB` ou affects. O coordenador local preserva a
ordem intercalada antes da Soul e e consumido pelo login em
COMBAT-LOGIN-CURRENT-SCORE-001. Type 4 usa os deltas sequenciais da source 7.69;
para HP seu limite e 400.000, distinto do limite de 100.000 usado por outros
estagios. Persistencia e wire/encoder nao foram alterados por este recorte.

Os testes cobrem Type 4, Type 35, Type 34 e Type 8 mask 239 intercalados, um
affect desconhecido e Haste. HP 102 termina em 266 pela ordem dos slots; MP/AC/
Damage ficam 70/111/172 e Magic 350 no coordenador. Um teste isolado usa origem
Magic 105 e acumulador local 5 para provar que cada Type 4 le a origem sem
substitui-la; outro verifica o teto proprio de HP 400.000. LearnedSkill bit 28
e preservado, RSV_VISION une a Haste em `0x24`, a resistencia ganha +25 no
estagio separado e o snapshot fica intacto. RED reproduziu a ausencia de Type 4
na passagem ordenada; GREEN e a validacao completa constam em
COMBAT-LOGIN-AFFECT-TYPE4-001.

Build completa de `WydCdk.Engine.slnx` com `--no-incremental`, SDK 10.0.401:
0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. Comandos, saidas e `dotnet --info` em
`Backup/Build/CurrentScoreAffectComposition-20261008`. No recorte original, sem
login, persistencia, wire, smoke TCP, E2E visual ou publicacao; a composicao foi
integrada ao login depois em COMBAT-LOGIN-CURRENT-SCORE-001.

## COMBAT-LOGIN-AFFECT-TYPE4-001 - Type 4 na composicao ordenada (08/10/2026)

Type 4 foi ligado ao composer de score que percorre os 32 affects uma vez,
preservando a ordem dos slots e a projeção compartilhada do login existente.
Para cada slot Type 4, HP recebe +15%, MP +10%, Damage +30% e o acumulador
local Magic recebe +20% do `MOB.Magic` original; todos os deltas usam truncamento
inteiro. O helper compara cada delta com seu teto antes de somar. Para HP Type 4
usa `MAX_HP=400.000`, diferente dos estágios Type 34/35/8 que usam 100.000.

Os testes cobrem Type 4 intercalado com Type 34/35/8, preservação da ordem,
Magic baseada na origem, dois affects Type 4 e o limite próprio de HP. O golden
do coordenador e o golden de projeção final do login incluem esse affect. RED
reproduziu os valores sem Type 4; GREEN: Engine.Tests 405/405. Build completa
de `WydCdk.Engine.slnx` com SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente
em `AttackDivineBlessingTests.cs:256`. Logs e `dotnet --info` em
`Backup/Build/CurrentScoreType4-20261008`.

O Type 4 introduz Magic derivada de `MOB.Magic` persistido e pode reaplicar o
delta após logout/relogin, como Type 34 Magic. Não foi mudado schema, base
persistida, wire ou encoder; os demais branches de score permanecem pendentes.

## COMBAT-LOGIN-AFFECT-TYPE31-001 - passada AC ordenada e projecao de login (09/10/2026)

### Fonte e divergencia

No helper comum TMProject 7.69 `Servidor/Source/Code/Basedef.cpp`,
`BASE_GetCurrentScore` aplica Type 24/31/25/16/11/21 na primeira passagem pelos
affects. Uma passagem posterior aplica Type 12 e Type 8 bit 4 na ordem dos
slots. Type 31 soma `Level / 2 + Value` ao AC corrente.

W2PP `Server/W2PP/Source/Code/Basedef.cpp` tambem contem Type 31 com a mesma
formula. Sua rotina processa os branches em um unico loop por slot; Type 25
adiciona resistencias, mas nao AC. Portanto a ordem global pode produzir outro
AC quando Type 21/24/31/25/16/11/12 ou Type 8 bit 4 intercalam. O golden W2PP do
Type 31 foi corrigido para aplicar a formula que existe no source, sem declarar
o branch ausente.

### Port C# e verificacao

`CalculateCurrentScoreArmorClassAffectCompositionStage` aplica as duas
passagens aprovadas. Reusa kernels por affect Type 16, Type 25 e Type 21, e
preserva a ordem de slot dentro de cada passagem. Type 21 subtrai
`Level / 3 + 10` no primeiro passe. O coordenador fornece esse AC ao composer e
desativa apenas o delta AC Type 8 bit 4 da chamada geral, evitando dupla
aplicacao; o mesmo resultado chega a `LegacyCurrentScoreLoginProjector`. O ajuste
`DAMAGEMULTI` de Type 21 tambem chega ao multiplicador final do login pela formula
7.69 (`Level / 25 + Value`); a divergencia W2PP (`Level / 10 + Value`) permanece
registrada. Outros branches AC e multiplicadores ainda nao portados continuam fora.

O teste do coordenador cobre Type 24/31/25/16/11/21 na primeira passagem e
Type 8/12 em ordens opostas na segunda. Tambem verifica a ordem Type 21/Type 24.
Os goldens do coordenador e do login incluem Type 31 no slot 7, Type 25 no slot
8 e Type 21 no slot 9; a projecao do caso intercalado termina em AC 271 (base
101, Type 31 +15, Type 25 +157, Type 21 -26, Type 8 bit 4 +24; o Type 8 nivel
239 nao tem bit 4). Type 21 no slot 9 tambem adiciona `50 / 25 + 10 = 12` ao
`DAMAGEMULTI`: o dano 172 apos os affects termina em 192 no login (`172 * 112 / 100`,
divisao inteira). Os sete vetores isolados confirmam a formula AC de Type 21 em
7.69 e W2PP e a divergencia entre as formulas de DAMAGEMULTI.

O teste de startup autenticado agora drena stdout/stderr dos processos filhos enquanto espera, evitando bloqueio dos pipes; continua verificando rejeicao antes do listener.

Engine.Tests 405/405. Build completa de `WydCdk.Engine.slnx` com SDK 10.0.401,
`--no-incremental`: 0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. Saidas e `dotnet --info` em
`Backup/Build/CurrentScoreType31-20261009`. Sem nova logica de persistencia,
alteracao de schema, wire/encoder, smoke TCP ou E2E visual.

## COMBAT-LOGIN-AFFECT-TYPE25-001 - AC de Protecao Elemental no login (09/10/2026)

### Fonte e divergencia

TMProject 7.69 `Servidor/Source/Code/Basedef.cpp:3691` aplica Type 25 durante a
primeira passagem de `BASE_GetCurrentScore`: `Level / 2 + Value`, com mais 30
quando `Level >= 255`. A mesma passagem preserva a ordem dos affects por slot,
portanto Type 25 pode alterar a entrada usada pelo Type 16 que aparece em slot
posterior.

W2PP `Server/W2PP/Source/Code/Basedef.cpp:4232` atualiza apenas resistencias
elementais nesse branch; nao adiciona ArmorClass. A diferenca e mantida segundo
a autoridade 7.69.

### Port C# e verificacao

O kernel por affect `ApplyElementalProtectionArmorClassAffect` e compartilhado
pela etapa isolada e por `CalculateCurrentScoreArmorClassAffectCompositionStage`.
O Type 25 entra no primeiro passe, e a etapa ja existente de resistencias
continua processando o mesmo affect sem mudanca de comportamento.

Os testes compostos cobrem os limites 254/255 e a ordem Type 25/Type 16: AC
inicial 100 resulta em 372 quando Type 25 precede Type 16 e 302 na ordem
inversa. Os goldens coordenador e login verificam Type 31 no slot 7, Type 25 no
slot 8 e Type 8 bit 4 no slot 0, com AC final 300.

Engine.Tests 405/405; build completa de `WydCdk.Engine.slnx`, SDK 10.0.401 e
`--no-incremental`: 0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. Uma execucao intermediaria falhou apenas em
`attack authoritative pipeline`: o teste nao injeta `attackMissRoll` e o caminho
usa sorteio com taxa minima de miss de 1%; o rerun completo passou 405/405 sem
alterar esse teste alheio. Logs e `dotnet --info` em
`Backup/Build/CurrentScoreType25-20261009`. Sem persistencia, schema, wire,
smoke TCP, E2E visual, deploy, commit ou push.

## COMBAT-LOGIN-AFFECT-TYPE38-001 - transferencia de MP para HP (08/10/2026)

### Fonte e comparacao

No helper comum TMProject 7.69 `Servidor/Source/Code/Basedef.cpp:3627-3634`,
cada slot Type 38 calcula `mana = MOB.CurrentScore.MaxMp / 2`, soma `mana` a
MaxHp e subtrai o mesmo `mana` de MaxMp. A divisao C++ e inteira; cada slot le o
MaxMp atualizado pelo slot anterior. O branch nao consulta Value/Level nem tem
clamp proprio.

O W2PP `Server/W2PP/Source/Code/Basedef.cpp:4443-4449` tem o mesmo branch dentro
de `BASE_GetCurrentScore`, com a mesma formula e mutacao sequencial. Para Type 38,
nao ha divergencia comportamental entre as duas fontes.

### Port e verificacao

`LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage` aplica Type
38 na passagem comum por slot, preservando a posicao relativa aos demais affects.
O teste direto cobre MP impar 101, dois Type 38 separados por Type 35, Value/Level
ignorados e snapshot imutavel: HP 100/MP 101 termina HP 199/MP 27. Os goldens do
coordenador e da projecao de login incluem Type 38 depois da composicao anterior
HP 266/MP 70, resultando em HP 301/MP 35.

RED reproduziu a ausencia do branch: o composer retornou HP 115/MP 108 e os dois
goldens falharam nos MaxHP/MaxMP. GREEN: Engine.Tests 405/405. Build completa de
`WydCdk.Engine.slnx` com SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. Logs e `dotnet --info` em
`Backup/Build/CurrentScoreType38-20261008`.

O composer ja e consumido pelo coordenador e pela projecao de login. Nao houve
mudanca adicional de persistencia, schema, wire ou encoder; E2E visual permanece
fora deste recorte.

## COMBAT-LOGIN-CURRENT-SCORE-001 - projecao do score no login 7.69 (08/10/2026)

### Fonte e recorte

O login agora recompõe o subconjunto auditado pelo
`LegacyCurrentScoreLoginProjector` a partir do MOB persistido, ItemList carregada
e snapshot de equipamento/affects. A autoridade escolhida continua sendo o
helper comum `Servidor/Source/Code/Basedef.cpp::BASE_GetCurrentScore` compilado
por `TMSrv.vcxproj`, em conjunto com `TMSrv/CMob.cpp::GetCurrentScore`; a cópia
privada `TMSrv/Basedef.cpp` não compilada e o W2PP foram comparados e as
divergências dos blocos Type 4/34/35/8 permanecem nas auditorias anteriores.
Type 4 foi integrado em COMBAT-LOGIN-AFFECT-TYPE4-001; branches ainda nao
auditados tambem ficam fora. Portanto isto nao e paridade integral de
`BASE_GetCurrentScore`.

O Host projeta uma vez após carregar o personagem e antes de efeitos de login,
preflighta os adapters 7.69 e usa o mesmo registro no `CharacterLogin`
0x0114, `CreateMob`, estado do `WorldHub`, `UpdateEtc`, `UpdateScore` e
`UpdateAffect`. O fluxo existente de logout persiste esse estado projetado. Se
projeção ou narrowing falhar, o Host retorna à seleção sem instalar participante
nem emitir confirmação/spawn. `--accounts-root` e `--mariadb-account-config`
agora exigem `--item-data`; o arquivo é carregado antes do listener. Modo
diagnóstico sem autenticação continua podendo iniciar sem ItemList.

### Persistência e limite conhecido

O smoke de logout/relogin exercita Type 35 isolado: seus MaxHP/MaxMP derivados
são persistidos e o próximo login não os multiplica novamente. A auditoria do
loop 7.69 mostra um limite diferente para Type 34 Magic: cada affect deriva o
delta de `MOB.Magic` de entrada, enquanto a cauda grava o acumulador em
`MOB.Magic`; como o logout salva esse valor e o blob não guarda a base original,
um relogin pode derivar Magic novamente do valor já projetado. Para personagens
nível >= 2000, `CMob::GetCurrentScore` também inicia Resist do `MOB.Resist`
persistido antes de Type 8 bit 1, permitindo acumulação após logout/relogin.
Esses comportamentos foram preservados conforme a source; não se afirma
idempotência de relogin para Type 34 Magic nem Type 8 Resist. Uma correção exige
decisão própria sobre a base autoritativa/persistida, sem inventar novo schema.

### Verificação

Engine.Tests 405/405 cobre o projetor puro e o startup autenticado. O smoke headless
loopback `current-score-login-projection` validou login, spawn observado,
UpdateEtc/UpdateScore, logout, bytes salvos, relogin Type 35 e hashes de fixtures
e ItemList. `current-score-login-projection-reject` provocou narrowing fora do
wire e confirmou ausência de confirmação/spawn, ausência de CreateMob ao
observador e conta sem alteração. Evidências completas e `dotnet --info` em
`Backup/Build/CurrentScoreLoginProjection-20261008`. Build solution sem
incremental: 0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`; build do harness E2E: 0 avisos/erros.
Ambos os harnesses terminaram exit code 0. Isto é validação headless de
wire/login; o E2E visual com `Client/7670/WYD.exe` permanece pendente.

Sem mudança de ABI/wire, schema, Site, deploy, commit ou push.

## COMBAT-ATTACK-LOGIN-VIGOR-001 - Type 35 Vigor isolado (07/10/2026)

O helper comum selecionado pelo `TMSrv.vcxproj`,
`Servidor/Source/Code/Basedef.cpp:3975-3987`, aplica cada Affect Type 35 em
ordem de slot: `MaxHp += MaxHp * 15 / 100` e `MaxMp += MaxMp * 7 / 100`, com
truncamento inteiro depois da multiplicacao. O source compara o incremento (nao
o resultado) com `MAX_HP/MAX_MP=100.000`: se alcanca o limite, atribui 100.000;
abaixo dele, soma sem limitar o resultado. Isso permite MaxHp/MaxMp acima de
100.000 quando o incremento e menor que o limite.
W2PP `Basedef.cpp:4582-4594` e a copia privada nao compilada
`TMSrv/Basedef.cpp` usam 10%/10%; os tetos W2PP sao 1.000.000.000. O helper
isolado anterior foi corrigido para seguir o arquivo comum incluido pelo build.

`LegacyCurrentScoreMath.CalculateVigorAffectStage` percorre os 32 registros de
8 bytes, ignora outros tipos e aplica Type 35 sequencialmente sobre os valores
anteriores, sem ler `Value/Level` nem mutar MOB/Affect. A aritmetica e reutilizada
pelo composer COMBAT-LOGIN-AFFECT-COMPOSITION-001, integrado ao login depois em
COMBAT-LOGIN-CURRENT-SCORE-001. A etapa isolada original nao tratou persistencia
adicional ou encoder.

RED atualizou o contrato para 15%/7% contra o helper anterior 10%/10% e registrou
401/402, com os valores antigos 1089/111. Uma revisao posterior identificou que o
limite 100.000 se compara ao incremento: o teste primeiro falhou em 402/403, pois
o helper limitava o resultado de 120.000 para 100.000 em vez de produzir 138.000.
Os vetores cobrem truncamento, ordem de multiplos efeitos, ambos os lados do
limite de incremento, outros tipos e snapshot truncado. Engine.Tests 396/396 na
execucao original. A primeira execucao completa teve um
miss aleatorio de 1% no teste preexistente `world skill attack`; o rerun passou.
Build de `WydCdk.Engine.slnx` no SDK 10.0.401: 0 avisos/0 erros. Logs e
`dotnet --info` em `Backup/Build/CurrentScoreVigor-20261007`. Sem smoke TCP,
E2E visual ou publicacao.

## COMBAT-LOGIN-AFFECT-AUDIT-001 - Type 4, 34 e 8 (07/10/2026)

Auditoria comparativa dos blocos ativos de `BASE_GetCurrentScore` e dos recortes
C# correspondentes:

- Type 4 (Poder Superior) esta ativo no helper comum 7.69, e o call graph do
  projeto confirma essa autoridade de source: `TMSrv.vcxproj:101` compila
  `..\\Basedef.cpp`, que inclui o `Basedef.h` comum; `TMSrv/CMob.h:5` tambem
  inclui `..\\Basedef.h`, e `CMob.cpp:619,694` chama
  `BASE_GetCurrentScore`. Esse caminho usa `MAX_EQUIP=18` do header comum; o
  `TMSrv/Basedef.cpp` privado, que inclui `TMSrv/Basedef.h` com 16 slots, nao e
  selecionado pelo projeto. No helper ativo, cada affect Type 4 aplica
  `MaxHp += 15%`, `MaxMp += 10%`, `Damage += 30%` e `Magic += 20% de MOB.Magic`,
  com truncamento inteiro (`Basedef.cpp:3946-3970`). Os deltas de HP/MP/Damage
  sao comparados com `MAX_HP=400000`, `MAX_MP=100000` e `MAX_DAMAGE=1000000`;
  ao atingir o teto, `CurrentScore` recebe o teto, caso contrario soma o delta.
  Magic compara `20% de MOB.Magic` com `MAX_DAMAGE_MG=190000000` e atribui o
  limite ao acumulador local `Magic` ou soma o delta. Os limites vem de
  `Basedef.h:352-355`. W2PP diverge
  (`Basedef.cpp:3970-3974`): soma `DAMAGEMULTI +4`, `Damage +30` e `magic +5`,
  sem os deltas de HP/MP. A evidencia resolve a source ativa do projeto, mas nao
  comprova um binario legado recompilado. O port Type 4 aplica esses deltas na
  composicao ordenada por slot e no login em COMBAT-LOGIN-AFFECT-TYPE4-001.
- Type 34 (Divina) existe nas duas fontes. A 7.69 (`Basedef.cpp:3921-3950`)
  aumenta MaxHP/MaxMP em 20% e Damage/magic em 10%. Calcula cada delta pela
  formula `valor * percentual / 100`; para HP/MP/Damage compara o delta, nao a
  soma, com o respectivo teto antes de adicionar. Assim, a saida intermediaria
  pode ultrapassar o teto quando o delta ainda e menor. O delta de magic vem de
  `MOB.Magic`, enquanto o destino e o acumulador local `magic`. O W2PP
  (`Basedef.cpp:4551-4579`) aplica 20% aos quatro valores e calcula/clampa o
  valor resultante; tambem muta `MOB.Magic`. Os tetos 7.69 em `Basedef.h:217-220`
  sao HP/MP 100.000, Damage 1.000.000 e Magic 190.000.000; o W2PP define
  1.000.000.000 para todos em `Basedef.h:263-266`.
- Type 8 (joias PvP) ja tinha no C# o bit `1<<1`, que acrescenta 25 a cada
  resistencia em `ApplyResistanceAffectStage`. A 7.69 tambem define visao no bit
  `1<<2`, HP/AC no bit `1<<4`, HP/dano/magia no bit `1<<5`, o bit 28 de
  `LearnedSkill` no bit `1<<6` e conversao de metade do MaxMP em MaxHP no bit
  `1<<7`. O bit 6 tambem reduz o Parry runtime do alvo por hit PvP positivo,
  conforme COMBAT-ATTACK-PVP-JEWEL-PARRY-AUDIT-001; bits 0 e 3 tambem aparecem
  no fluxo de ataque. O bit 0 e inefetivo no gate
  7.69 porque a condicao usa 700 ms fixos, conforme
  COMBAT-ATTACK-PVP-JEWEL-AGILITY-AUDIT-001. O W2PP usa `RSV_CAST` em vez
  de `RSV_VISION` no bit 2, soma `Accuracy +50` no bit 6 e diverge em
  percentuais, ordem e clamps nos bits 4/5/7. A etapa isolada de score foi
  concluida em COMBAT-LOGIN-PVP-JEWEL-001.

Auditoria comparativa atualizada em 08/10/2026 com a correcao do call graph e da
autoridade de source Type 4. Os estagios Type 4/34/35/8 seguem a fonte comum
selecionada pelo projeto, foram compostos em
COMBAT-LOGIN-AFFECT-COMPOSITION-001 e integrados ao login em
COMBAT-LOGIN-CURRENT-SCORE-001; Type 4 entrou em recorte proprio
COMBAT-LOGIN-AFFECT-TYPE4-001. Persistencia adicional para Magic Type 4/34 e Resist
Type 8, branches nao auditados e paridade integral permanecem pendentes.

## COMBAT-LOGIN-PVP-JEWEL-001 - Type 8 score stage isolado (08/10/2026)

### Referencia e divergencia

TMProject 7.69 `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp:3992-4076`
percorre os 32 affects na ordem dos slots. Para cada Type 8, o bit `1<<2`
acrescenta `RSV_VISION` (`Basedef.h:335`, valor `0x04`). Os bits 4 e 5 aplicam
os aumentos de HP/AC e HP/dano/magia na ordem do codigo. O bit 6 define
`MOB.LearnedSkill |= 1 << (35 - 7)`; o bit 7 move `MaxMp / 2` para MaxHp e
substitui MaxMp por esse valor, com os clamps 7.69 de 100.000.

No bit 4, HP e AC usam `valor * 10 / 100`; o ganho de HP e zeroado somente se
MaxHp ja for pelo menos 100.000 antes da soma. No bit 5, HP usa o mesmo calculo,
Damage recebe `Damage * 20 / 100` e Magic recebe `MOB.Magic * 5 / 100`. Os
limites de Damage e Magic comparam o delta antes de somar; Magic usa o MOB.Magic
de origem e acumula no `magic` local. O bloco pode deixar valores intermediarios
acima dos tetos quando o delta ainda e menor que o teto. O bit 7 limita o MaxHp
resultante a 100.000 e limita a metade transferida para MaxMp a 100.000.

W2PP `W2PP/Source/Code/Basedef.cpp:4479-4549` diverge: o bit 2 grava
`RSV_CAST` (`Basedef.h:250`, `0x40`), o bit 3 soma AbsHP dentro desse loop, o bit
4 calcula HP/AC com divisao antes da multiplicacao e limita HP pelo resultado,
o bit 5 aplica 10% ao Damage e 20% a `MOB.Magic` mutando o campo de origem, o
bit 6 soma 50 a Accuracy e o bit 7 transfere `(MaxMp + 1) / 2` sem os clamps
7.69. O port segue o contrato TMProject 7.69.

### Port C# e verificacao

`LegacyCurrentScoreMath.CalculatePvpJewelAffectStage` recebe score inicial,
`sourceMobMagic`, acumulador local `startingMagic`, `LearnedSkill` e percorre
os 32 registros sem mutar MOB/Affect. Retorna separadamente `RsvFlagsToAdd`.
O bit 1 continua no estagio existente `ApplyResistanceAffectStage`; os bits 0
e 3 e encoder wire permanecem fora deste recorte. O helper e reutilizado pelo
composer puro COMBAT-LOGIN-AFFECT-COMPOSITION-001, que preserva slots intercalados
Type 4/34/35/8 e foi integrado ao login depois em COMBAT-LOGIN-CURRENT-SCORE-001.
Persistencia adicional segue fora. O efeito de ataque do bit 6 e integrado nos
caminhos C# atuais em COMBAT-ATTACK-PVP-JEWEL-PARRY-INTEGRATION-001, separado do
score stage e do LearnedSkill definido no loop de score.

`CurrentScorePvpJewelTests` cobre os bits combinados com valores que distinguem
7.69 do W2PP, efeitos repetidos em ordem, uso do MOB.Magic de origem, deltas e
limites, transferencia bit 7, bits de ataque/tipos alheios e snapshot truncado.
RED apontou a API ausente; GREEN: Engine.Tests 398/398. Build completa de
`WydCdk.Engine.slnx`, SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. Logs e `dotnet --info` em
`Backup/Build/CurrentScorePvpJewel-20261008`. Este recorte adicionou o estagio
isolado; o login passou a consumi-lo depois pelos recortes de composicao e
projecao. Persistencia adicional, smoke TCP e E2E visual nao fizeram parte deste
estagio.

## COMBAT-LOGIN-DIVINA-001 - Type 34 Divina isolado (07/10/2026)

### Referencia e divergencia

TMProject 7.69 `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.cpp:3921-3950`
percorre os slots em ordem; `TMSrv.vcxproj` inclui o helper comum por
`..\Basedef.cpp`, nao a copia `TMSrv/Basedef.cpp`. Para cada Type 34, calcula HP e MP como
`valor * 20 / 100`, Damage como `valor * 10 / 100`, comparando cada delta ao
teto antes de adiciona-lo ao score corrente. Por isso, HP/MP/Damage
intermediarios podem superar o teto quando o delta ainda e menor que ele. A
operacao multiplica antes de dividir e trunca em cada passo.

O delta de Magic e sempre `MOB.Magic * 10 / 100`; quando abaixo do teto, soma ao
acumulador local `magic`, sem alterar o MOB.Magic de origem. O clamp final do
acumulador ocorre depois do loop, fora deste estagio. Os limites 7.69 sao
HP/MP 100.000, Damage 1.000.000 e Magic 190.000.000
(`Servidor/Source/Code/Basedef.h:217-220`).

W2PP `Server/W2PP/Source/Code/Basedef.cpp:4551-4579` usa 20% em HP, MP,
Damage e Magic, calcula o resultado somado e o compara ao teto; seu branch
tambem grava o Magic calculado em `MOB.Magic`. `W2PP/Source/Code/Basedef.h:263-266`
define 1.000.000.000 para os quatro limites. O port segue TMProject 7.69.

### Port C# e verificacao

`LegacyCurrentScoreMath.CalculateDivinaAffectStage` recebe separadamente os
valores correntes de score, `sourceMobMagic` e o acumulador `startingMagic`.
Percorre 32 registros de oito bytes, ignora outros tipos e aplica as
atualizacoes sequencialmente. Sua aritmetica e reutilizada pelo composer puro
COMBAT-LOGIN-AFFECT-COMPOSITION-001 e integrada ao login depois em
COMBAT-LOGIN-CURRENT-SCORE-001. O logout persiste o MOB projetado; Magic pode
ser reaplicada no relogin porque a base original nao e preservada.

Os seis casos em `CurrentScoreDivinaTests` cobrem ordem inteira de
multiplicacao/divisao, Magic de origem separado do acumulador, efeitos
repetidos, comparacao dos deltas com os tetos, clamp final posterior, tipos
ignorados e snapshot truncado. O RED identificou a API ausente; GREEN:
Engine.Tests 397/397. Rebuild completo de `WydCdk.Engine.slnx`, SDK 10.0.401:
0 erros e 1 aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`, fora
deste recorte. Logs e `dotnet --info` em
`Server/Backup/Build/CurrentScoreDivina-20261007`. Este estagio foi isolado na
data original; o login passou a consumi-lo depois em
COMBAT-LOGIN-CURRENT-SCORE-001. Persistencia adicional, smoke TCP e E2E visual
nao fizeram parte do estagio original.

## COMBAT-ATTACK-MAGIC-MIRROR-MANA-001 - surcharge de mana (06/10/2026)

### Comparacao TMProject 7.69 / W2PP

TMProject `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:256-275`
calcula `ManaSpent` com `BASE_GetManaSpent`, rejeita somente quando
`CurrentScore.Mp - ManaSpent < 0`, debita o valor-base e chama `SetReqMp`. Em
seguida, para `MOB.Class==0` e `(Extra.SecLearnedSkill & 0x100)`, debita
`ManaSpent / 2` e chama `SetReqMp` novamente. A divisao C++ inteira trunca em
direcao a zero; a segunda cobranca nao tem novo gate de MP e pode deixar MP
negativo. `SetReqMp` limita apenas o teto `MaxMp` nesse arquivo.

W2PP `_MSG_Attack.cpp:238-252` cobra apenas o valor-base, atualiza `ReqMp` e
chama `SetReqMp`; nao possui o bloco Magic Mirror. Para esta regra, o C# segue
TMProject 7.69.

### Port C# e limites

`LegacyMobExtra.MagicMirrorLearnedBit` nomeia `0x100`; o WorldHub le o word
`SecLearnedSkill` no offset medido 4 do MOBEXTRA de 552 bytes. A rota publica
`TryConsumeSkillMana` habilita o efeito somente para classe 0 com o bit ativo.
Ela verifica a suficiencia do custo-base e atualiza MP/RequestedMana; depois
subtrai `ManaSpent / 2`, aplica novamente apenas o teto de MaxMp e preserva o
resultado negativo de MP, sem uma segunda rejeicao. A cobranca interna da cura
recursiva de Divine Blessing usa a mesma funcao-base, mas passa explicitamente
`applyMagicMirror=false`, pois nao e outro `Exec_MSG_Attack` do cliente.

TDD: o RED corrigido com o override desativado observou MP 95 em vez dos 93
esperados para custo-base 5 + surcharge truncado 2. GREEN verifica classe 0 e
bit `0x100`, truncamento depois de SaveMana, classe diferente, bit ausente,
rejeicao sem mutacao quando falta o custo-base e ausencia de uma segunda
verificacao quando o custo-base iguala o MP inicial. Tambem confirma que a
fixture externa de MOBEXTRA nao e mutada. Engine.Tests 395/395; o runner emitiu
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. Build completa
`WydCdk.Engine.slnx` no SDK 10.0.401: 0 avisos/0 erros. Logs RED/GREEN, build e
`dotnet --info` em `Backup/Build/AttackMagicMirrorMana-20261006`.

Fora deste recorte: aprender a skill via item 5508, bonus persistido de MaxMP
no recalculo do score e o decremento local de `parryretn` por Special4 que a
source 7.69 sobrescreve antes de decidir o miss. Sem smoke TCP, E2E visual,
publicacao sandbox, Site/producao, commit ou push.

## COMBAT-ATTACK-BR-CREATE-MOB-MASK-001 - anonimização visual no Battle Royale (06/10/2026)

### Comparação TMProject 7.69 / W2PP

TMProject `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/GetFunc.cpp:1193,1355-1362`
e W2PP `W2PP/Source/Code/TMSrv/GetFunc.cpp:1073,1186-1193` aplicam a mesma
projeção em `GetCreateMob`: com `BrState != 0` e coordenadas inclusivas
X=2604..2648/Y=1708..1744, zeram os 16 bytes de `MobName`, copiam seis `?`,
zeram `Equip[15]` e `Guild`. A condição não consulta `BRItem`. `GetCreateMobTrade`
é um builder distinto e não contém essa máscara nas referências; o port conserva
essa fronteira.

### Port C# e limite de escopo

`LegacyBattleRoyaleCreateMobVisualRelay.Apply` valida checksum, tipo e tamanho do
frame regular 7.69, lê as coordenadas serializadas e, quando ativo/no retângulo,
clona o payload, aplica os mesmos valores visuais e recodifica o frame com o
cabeçalho original. Nenhum byte do MOB/snapshot autoritativo é alterado.
`WorldHub.TryBuildCreateMobFrame` usa o estado publicado; o refresh de grade usa o
estado da transição; `BuildClientV769CreateMobFrame` cobre os produtores regulares
do Host (login, NPCs, summons/mobs gerados, atualização visual de crime e Pista).
Frames `CreateMobTrade` continuam fora dessa transformação, como no legado.

TDD cobre estado inativo e estados 1/2/3, inclusive os quatro limites/cantos e
posições imediatamente externas; também confirma que `BRItem=0` não desativa a
máscara, campos visuais mascarados, checksum válido, NPC na arena e preservação
dos snapshots originais. A regressão do refresh confirma máscara para membro e
NPC dentro da região e preserva nome/Guild/equipamento de um outsider. Engine.Tests
passou 394/394. Um run intermediário falhou apenas com o miss aleatório de 1% em
dois testes preexistentes (`world physical attack` e `world skill attack`); ambos
retornaram `Damage=-3` e a execução seguinte passou sem modificar esses testes.
Build completo de `WydCdk.Engine.slnx` com SDK 10.0.401: 0 avisos/0 erros.
`dotnet --info`, teste final e build estão em
`Backup/Build/AttackBattleRoyaleCreateMobVisual-20261006/`; `git diff --check`
passou, com avisos de normalização LF/CRLF do checkout.

Não houve smoke TCP em fase real, E2E visual ou publicação. Os builders de
CreateMob nas referências não registram vencedor nem placar separadamente; esta
fatia não introduz esses estados nem altera recompensa, Site/produção, commit ou
push.

## COMBAT-ATTACK-BR-ENVIRONMENTAL-001 - dano ambiental do Coliseu (06/10/2026)

### Comparacao TMProject 7.69 / W2PP

TMProject `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/ProcessSecMinTimer.cpp:1661,1761-1780`
e W2PP `W2PP/Source/Code/TMSrv/ProcessSecMinTimer.cpp:1642,1680-1699`
guard the environmental step with `BrState==2`, `BRItem>0` and `SecCounter%16==0`.
At the end of the preceding loop `i==MAX_USER==1000`, so `i%8==0` and this is
the 16-second cadence. `BrMod` comes from local `minute%20` (TMProject
`Server.cpp:7934`). Phases 7/8 damage and display the inclusive rectangles
(2608,1708)-(2622,1711) and (2608,1739)-(2622,1743); phase 9 onward widens them
to Y=1708..1718 and 1733..1743.

Both `SendDamage` implementations select `USER_PLAY`, living players in the
inclusive rectangle, reduce HP by 2000 and multicast `MSG_SetHpDam` around the
target. TMProject `Server.cpp:7040-7092` and W2PP `Server.cpp:5970-6020` use the
same arithmetic: exactly 2000 HP becomes zero, while lower positive HP becomes
1. The approved C# behavior instead keeps every living target at HP>=1, so the
exact-2000 boundary is deliberately different. TMProject leaves the direct
`ReqHp=dam` assignment commented; W2PP actively assigns it before `SetReqHp`.
The port follows 7.69. Both call `SetReqHp`/`SetReqMp` after changing HP; MP above
MaxMp is clamped.

`SendEnvEffect` uses `ESCENE_FIELD=30000`, `Effect=32`, `EffectParm=0` and
multicasts around each rectangle center (TMProject `SendFunc.cpp:461-498`, W2PP
`SendFunc.cpp:456-493`).

### Port C# e limite de escopo

`LegacyBattleRoyaleEnvironmentalSchedule.TryCreate` gates state 2, positive
BRItem, phases 7..19 and a 16-second counter, choosing the narrow or widened
bands. The Host's existing two-second loop checks elapsed monotonic windows even
when no phase transition occurs. `WorldHub.TryApplyBattleRoyaleEnvironmentalDamage`
holds the world lock, selects only living participants in the inclusive bands,
updates HP and clamps MP above MaxMp. The approved floor at 1 avoids an HP-zero
environmental tick without normal death processing.

`LegacyBattleRoyaleEnvironmentalRelay` sends all target `SetHpDamConfirmation`
frames before the two effects. The damage frame is 20 bytes, type `0x018A`, ID
equal to the target, with final HP and signed HP delta. `EnvironmentEffectConfirmation`
is 24 bytes, type `0x03A2`, ID 30000, six signed shorts containing bounds and
effect 32/parameter 0. Damage recipients use the inclusive 33x33 view centered
on each target; effects use the 33x33 view centered on each area. This preserves
the distinct recipient sets and damage-before-effect order from the source.

Este recorte nao seleciona/registra o vencedor nem implementa o placar e o fluxo
restante de recompensa.

### Validacao local

TDD RED/GREEN cobre estados/item/fase/cadencia, os limites das duas faixas,
alvos vivos/mortos, HP 5000/2000/1500/1, posicoes sem dano, clamp de MP, bytes
wire, visoes dos destinatarios e ordem de envio. Engine.Tests passou 393/393.
Build completo de `WydCdk.Engine.slnx` com SDK 10.0.401 terminou com 0 erros e
1 aviso CS8604 preexistente em `AttackDivineBlessingTests.cs:256`. Logs e
`dotnet --info` estao em
`Backup/Build/AttackBattleRoyaleEnvironmental-20261006/`. Nao houve smoke TCP
em fase real, E2E visual, publicacao, alteracao do Site/producao, commit ou push.

## COMBAT-ATTACK-BR-DOORS-001 - portas e ocupantes do Coliseu (06/10/2026)

### Comparação TMProject 7.69 / W2PP

TMProject `Servidor/Source/Code/TMSrv/Server.cpp:7523-7568` usa slots estáticos
13/14 (`i+12`, com `i=1,2`) para item 472 em (2603,1717) e (2603,1733), conforme
`Servidor/Server/TMSrv/run/InitItem.csv:14-15`. Em
1->2, `SetColoseumDoor(3)` roda antes do aviso e novamente depois da limpeza de
área; em 3->0, `SetColoseumDoor(1)` roda antes de zerar o estado. 0->1 e 2->3
não chamam as portas. O método sempre envia `CreateItem` e zera `Delay`, mesmo
quando a segunda chamada encontra a máscara já aplicada.

`UpdateItem` 7.69 (`Server.cpp:9528-9609`) aplica a máscara somente quando
`BASE_UpdateItem` aceita a mudança e percorre as células marcadas. Ocupante com
`Equip[0].sIndex==220` recebe `DeleteMob(...,3)`; os demais passam por
`GetEmptyMobGrid`
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/GetFunc.cpp:2231-2274`),
zeram a rota e recebem `GetAction`. O helper considera livre
para o próprio mob a célula que ele já ocupa, então esse `Action` pode preservar
a mesma posição. `DeleteMob` 7.69 (`Server.cpp:8841-8905`) envia `RemoveMob(3)`, zera
HP, libera a grade e chama `RemoveParty` (`Server.cpp:9242-9357`), que escolhe o
próximo líder e recompõe a party restante por convites internos. `GridMulticast`
(`SendFunc.cpp:946-988`) exclui o próprio usuário do `RemoveMob`; `RemoveParty`
ainda envia `PartyRemove` diretamente a ele quando aplicável.

W2PP `Server.cpp:6443-6488` usa slots 12/13, ignora estado igual e, ao abrir,
envia `UpdateItem`; o 7.69 deixa esse ramo comentado e usa `CreateItem` em ambas
as direções. Este port segue o contrato 7.69, sem combinar as variantes.

### Port C# e limite de escopo

`LegacyBattleRoyaleTransition.DoorCalls` representa as três chamadas com seus
marcos. O controller adia a publicação de estado em 1->2/3->0 até depois da
primeira chamada; o Host envia o aviso entre o primeiro e o segundo fechamento,
executa `AfterAreaClear` depois do relay de limpeza e antes do refresh de grade.
Em 2->3 não há mutação de porta.

`WorldHub.TryUpdateBattleRoyaleDoors` aplica/remove a máscara e altura sob lock,
zera `Delay` e captura efeitos de ocupantes antes de cada `CreateItem`. O relay
usa a visão inclusiva 33x33 por célula. Jogador, summon ou NPC com `Equip[0]=220`
é removido da grade autoritativa; jogador fica com HP zero, não recebe seu próprio
`RemoveMob`, sai da party e, se era líder, os restantes são recompostos sob o
primeiro membro elegível. `PartyRemove` continua sendo enviado diretamente ao
jogador removido quando aplicável; um ocupante conectado sem party não recebe
esse frame. Outros ocupantes usam o
`GetEmptyMobGrid` portado: célula ainda ocupada pelo próprio actor gera `Action`
com rota zerada no mesmo tile. Summons vinculados ao jogador removido também
recebem remoção. Persistência do estado das portas não foi adicionada.

### Validação local

TDD verificou ordem das chamadas, estados/máscaras, `Delay`, bytes e destinatários
dos `CreateItem`/`RemoveMob`, exclusão do ator e party direta, repetição do
fechamento sem reaplicar a máscara, abertura,
ocupantes 220, `Action` no mesmo tile, promoção/recomposição da party, ausência
de `PartyRemove` quando não há party e transições sem chamada de porta. A
regressão para o frame de party indevido foi observada em RED e corrigida.
Engine.Tests passou 389/389 no rerun; uma primeira execução completa falhou no
miss aleatório de 1% do teste preexistente `world skill attack` (`Damage=-3`),
que passou na execução seguinte. Build de `WydCdk.Engine.slnx` com SDK 10.0.401:
0 avisos/0 erros. `dotnet --info`, build e ambas as execuções estão em
`Backup/Build/AttackBattleRoyaleDoors-20261006/`. Não houve smoke TCP na fase
real, E2E visual ou publicação.

## COMBAT-ATTACK-BR-GRID-PARTY-CLEANUP-001 - refresh de grade e saída de party no Coliseu (06/10/2026)

### Comparação TMProject 7.69 / W2PP

TMProject `Servidor/Source/Code/TMSrv/Server.cpp:7981-7990,8012-8020` e W2PP
`W2PP/Source/Code/TMSrv/Server.cpp:6892-6900,6923-6931` executam o mesmo bloco
nas transições 0->1 e 1->2: depois de `ClearAreaLevel` quando aplicável,
percorrem jogadores USER_PLAY dentro de X=2604..2648/Y=1708..1744 inclusivos,
chamam `SendGridMob(k)` e removem a party somente quando `pMob[k].Leader` é
não-zero (membro, nunca o líder). O terceiro bloco de 20 minutos não chama
ClearAreaLevel, mas mantém refresh e limpeza.

`SendGridMob` percorre a visão 33x33 (33x33, metade 16), exclui o próprio
receptor e envia `CreateMob`, seguido de `PKInfo` quando o ID é slot de jogador,
além de `CreateItem` para cada item ativo na grade; IDs de mob/item obsoletos
são limpos. Os dois `Basedef.h` definem `MAX_USER=1000` (TMProject linha 70;
W2PP linha 116), e `SendPKInfo` valida `target < MAX_USER`, então NPCs e summons
recebem CreateMob sem PKInfo. `RemoveParty` envia a remoção para o membro e o
roster original; no wire C# usado aqui, o membro recebe leader=0 e os peers
recebem o ID removido.

### Port C# e limite de escopo

`LegacyBattleRoyaleTransition.RefreshGridAndCleanupParties` marca apenas 0->1
e 1->2 em todos os três blocos. Depois do aviso e de eventual relay de
ClearAreaLevel, o Host cria o grid snapshot sob o lock do `WorldHub` e envia,
por receptor ainda no Coliseu, CreateMob/PKInfo/CreateItem somente para sua
visão. O snapshot inclui players, NPCs, summons, itens estáticos e drops de chão;
PKInfo fica restrito a IDs <1000 e calcula PK mode, guilty, RvR, Castle, Guild
Tower e NewbieEvent com os controles globais de `SendPKInfo`. CreateMob usa o
adapter 7.69 e preserva a visualização de autotrade.

Depois que cada receptor recebe a grade, `TryRemoveBattleRoyalePartyMember`
revalida posição e liderança sob o lock: membro ainda no Coliseu sai da party,
líder e membros fora não são removidos. O relay usa o roster capturado antes da
mutação; membro recebe leader=0 e os demais recebem o ID removido. Assim, quem
já foi chamado de volta pelo ClearAreaLevel não é selecionado para a limpeza de
party; no terceiro bloco a limpeza ainda ocorre sem ClearAreaLevel.

Naquele recorte, portas, dano ambiental, seleção/registro do vencedor, entrega
completa da recompensa e placar ficaram para etapas posteriores; portas foram
portadas em `COMBAT-ATTACK-BR-DOORS-001`. Não houve smoke Host/TCP
no instante da fase, E2E visual ou publicação.

### Validação local

TDD registrou RED para o marcador, relay e operação WorldHub ausentes; a
regressão específica de `MAX_USER` também falhou ao detectar PKInfo indevido
para NPC e passou depois do guard. Uma execução inicial da suíte caiu no teste
aleatório preexistente `world skill attack`; a repetição passou. `ScheduleCarriesGridRefreshAndPartyCleanupScope`
cobre os três blocos e as transições; `GridRefreshPrecedesArenaPartyMemberRemoval`
cobre ordem por receptor, players/NPCs/PKInfo/itens/visibilidade, membros/leader,
roster e estado; `RecalledPartyMemberIsNotRemovedByTheFollowingGridPass` cobre o
limite após recall. Engine.Tests passou 385/385. O runner mantém CS8604
preexistente em `AttackDivineBlessingTests.cs:256`; build completo da solution,
SDK 10.0.401, passou com 0 avisos/0 erros. `dotnet --info`, build, execução
final e RED de MAX_USER estão em
`Backup/Build/AttackBattleRoyaleGridRefresh-20261006/`. `git diff --check`
passou; avisos de LF/CRLF do checkout foram preservados.

### Revisao independente e validacao final de COMBAT-ATTACK-BR-GRID-PARTY-CLEANUP-001

A revisao confirmou que `RemoveParty` 7.69 tambem chama `DeleteMob(..., 3)` para
summons do membro expulso. O plano C# agora remove esses summons do estado
autoritativo, limpa o alvo/registro do summoner e envia `RemoveMob` aos jogadores
na visao 33x33 do summon. Como os snapshots da fase sao capturados antes do envio,
o relay filtra `CreateMob` obsoleto de summons ja removidos para os destinatarios
que ainda nao foram processados.

Falhas `IOException`/`ObjectDisposedException` passam a ser isoladas por
destinatario: o relay para de enviar ao endpoint com erro, continua a limpeza de
party e os demais endpoints, e devolve a lista de IDs falhos para o Host registrar.
Cancelamento solicitado continua propagando. TDD RED reproduziu a falta de
despawn, a interrupcao na primeira falha e a ordem stale `RemoveMob` seguido de
`CreateMob`; as tres regressões passaram no GREEN. Engine.Tests passou 386/386;
build final da solution com SDK 10.0.401 terminou com 0 avisos/0 erros. Evidencias
em `engine-tests-review-red.log`, `engine-tests-stale-create-red.log`,
`engine-tests-review-final.log` e `solution-build-review-final.log` no diretorio
de build acima. Continua sem smoke TCP em fase de horario real e sem E2E visual.

## COMBAT-ATTACK-BR-AREA-CLEAR-001 - limpeza por nível no Battle Royale (06/10/2026)

### Comparação TMProject 7.69 / W2PP

TMProject `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/Server.cpp:7391-7418`
e W2PP `W2PP/Source/Code/TMSrv/Server.cpp:6311-6338` têm corpos equivalentes
em `ClearAreaLevel`: percorrem usuários em `USER_PLAY`, selecionam posição e
nível nos limites inclusivos, restauram HP<=0 para 2 com `SendScore` e depois
chamam `DoRecall`. A agenda invoca a limpeza nas transições Ready/Start de
0->1 e 1->2: minuto 0/5 usa níveis 100..400; minuto 20/25 usa 200..400. No
terceiro bloco de vinte minutos não há limpeza. As chamadas foram conferidas
em TMProject `Server.cpp:7969,7977,8001,8010` e W2PP
`Server.cpp:6880,6888,6912,6921`.

### Port C# e limite de escopo

`LegacyBattleRoyaleSchedule` carrega esses limites apenas nas transições
0->1/1->2 dos dois primeiros blocos. O Host envia primeiro o aviso da fase e
depois aplica a limpeza. `WorldHub` seleciona jogadores pela posição autoritativa
e nível, revive os mortos para HP 2 e captura os recalls sob lock. O relay envia
`UpdateScore` V769 antes do `Action` apenas para quem foi revivido; os Actions
seguem o relay espacial de `LegacyRecallRelay`, e as posições da sessão são
sincronizadas com o destino do WorldHub.

Não foi portada nesta fatia a limpeza de portas, dano
ambiental, seleção/registro do vencedor, entrega completa da recompensa ou
placar. A agenda não foi exercitada em horário real por TCP, e não houve E2E
visual nem publicação. A limpeza de party e o refresh da grade foram concluídos
separadamente em `COMBAT-ATTACK-BR-GRID-PARTY-CLEANUP-001`.

### Validação local

`ScheduleCarriesLegacyAreaClearLevels` cobre os limites por bloco/transição;
`AreaClearRecallsOnlyPlayersInTheInclusiveLevelBand` cobre níveis 99/100/400/401,
limites de área, HP morto/vivo, atualização de score, recall, destinatários por
visão e sincronização de posição de sessão. Engine.Tests passou 382/382. O build
completo da solution com SDK 10.0.401 passou sem erros; permanece o aviso
preexistente CS8604 em `AttackDivineBlessingTests.cs:256`. Não houve smoke TCP
em horário real, E2E visual, publicação sandbox, Site/produção, commit ou push.
`dotnet --info`, build e saída completa dos testes estão em
`Backup/Build/AttackBattleRoyaleAreaClear-20261006/`.

## COMBAT-ATTACK-BR-REWARD-001 - spawn e relay do prêmio no altar (06/10/2026)

### Comparação TMProject 7.69 / W2PP

TMProject `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/Server.cpp:7951-7956`
e W2PP `W2PP/Source/Code/TMSrv/Server.cpp:6862-6867` fazem o mesmo fluxo ao
entrar na fase 13 com `BrState==2`: para `0 < BRItem < MAX_ITEMLIST`, limpam um
`STRUCT_ITEM`, atribuem o índice e chamam `CreateItem(2621,1726,...,rand()%4,1)`;
depois publicam `BrState=3` e enviam `_NN_BR_Rewarded` a
X=2580..2665/Y=1688..1765. Ambas as `Basedef.h` definem
`MAX_ITEMLIST=6500` (TMProject: linha 288; W2PP: linha 199). O texto da tabela
TMProject `Servidor/Server/TMSrv/run/Language.txt:249` é
`Prêmio do vencedor está no altar.`

### Port C# e limite de escopo

`LegacyBattleRoyaleRewardRelay.TryCreate` aceita somente a transição 2->3,
índice de item no intervalo legado e rotação 0..3. `WorldHub` registra o item
limpo no altar com duração de drop existente e captura, sob o mesmo lock, os
jogadores dentro da visão inclusiva 33x33. O plano envia `MSG_CreateItem` a essa
visão antes de `MessagePanel` aos jogadores no retângulo inclusivo do aviso. O
controller só publica `BattleRoyaleState=3` depois da tentativa de inserir o
item; se o índice for inválido ou não houver slot de chão disponível, o estado
segue a agenda legada, mas o relay e o aviso são suprimidos. O Host também não
continua para o aviso quando o envio do frame de criação falha.

O aviso originalmente adiado em `COMBAT-ATTACK-BR-NOTICES-001` agora acompanha
o spawn concluído. A limpeza por nível foi concluída em
`COMBAT-ATTACK-BR-AREA-CLEAR-001`; o refresh de grade e limpeza de party foram
concluídos em `COMBAT-ATTACK-BR-GRID-PARTY-CLEANUP-001`. Permanecem
seleção/registro de vencedor, dano ambiental e placar; portas foram concluídas
em `COMBAT-ATTACK-BR-DOORS-001`.

### Validação local

TDD registrou ausência do relay e ausência da publicação adiada como RED. O
teste `RewardSpawnCreatesAltarItemBeforeRewardNotice` valida o item autoritativo,
bytes/rotação de `MSG_CreateItem`, bytes do `MessagePanel`, destinatários distintos
e ordem observada nos callbacks reais de `WorldHub.SendAsync`. Engine.Tests
380/380; o build completo da solution com SDK 10.0.401 passou com 0 avisos e 0
erros. A compilação do runner de testes ainda reporta o CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. `dotnet --info` e logs estão em
`Backup/Build/AttackBattleRoyaleReward-20261006/`. Não houve smoke TCP com a
agenda em horário real, cliente visual, publicação sandbox, Site/produção,
commit ou push.

## COMBAT-ATTACK-BR-NOTICES-001 - avisos de fase em Battle Royale (05/10/2026)

### Comparacao TMProject 7.69 / W2PP

TMProject `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/Server.cpp:7928-8028`
e W2PP `W2PP/Source/Code/TMSrv/Server.cpp:6840-6940` usam os mesmos avisos nas
transicoes 0->1, 1->2 e 2->3; a transicao 3->0 nao emite aviso. Os indices
246-252 de `Servidor/Server/TMSrv/run/Language.txt` sao `_NN_BR_Start1/2/3`,
`_NN_BR_Rewarded` e `_NN_BR_Ready1/2/3`; os IDs correspondentes tambem estao em
`W2PP/Source/Code/TMSrv/Language.h:266-272`. `SendNoticeArea` envia
`MSG_MessagePanel` aos jogadores `USER_PLAY` contidos nos retangulos inclusivos
(TMProject `Servidor/Source/Code/TMSrv/SendFunc.cpp`, W2PP
`W2PP/Source/Code/TMSrv/SendFunc.cpp:247`).

| Transicao | Textos `Language.txt` por faixa | Area inclusiva X/Y |
| --- | --- | --- |
| 0->1 | `Batalha Real para Nv < 100 inicia no coliseu em 2min.` / `Batalha Real para Nv < 200 incia no coliseu em 2min.` / `Batalha Real para qualquer nível inicia no coliseu em 2min.` | 2580..2665 / 1708..1765 |
| 1->2 | `Batalha Real está iniciando para Nv < 100.` / `Batalha Real está iniciando para Nv < 200.` / `Batalha Real está iniciando para qualquer nível.` | 2580..2665 / 1688..1765 |
| 2->3 | `Prêmio do vencedor está no altar.` (`_NN_BR_Rewarded`) depois do spawn | 2580..2665 / 1688..1765 |
| 3->0 | sem aviso | — |

As strings, inclusive o typo legado `incia` em Ready2, estao preservadas
literalmente. O texto por faixa e derivado de `minutoDaHora / 20`, limitado a
0..2.

### Port C# e limite de escopo

`LegacyBattleRoyaleSchedule` associa notices a 0->1, 1->2 e 2->3. O aviso
`_NN_BR_Rewarded` só acompanha uma tentativa de 2->3 quando o spawn foi aceito
em `COMBAT-ATTACK-BR-REWARD-001`; o relay dessa fase envia `MSG_CreateItem`
antes do `MessagePanel`. O loop do Host codifica Ready/Start com
`MessagePanelConfirmation` e consulta `WorldHub.GetParticipantIdsInArea` antes
do envio. A área Ready é distinta da área Start/Rewarded; nenhum aviso usa
`MagicTrumpet`. A transição 3->0 continua sem aviso.

### Validacao local

Na etapa de 05/10, TDD RED reproduziu a ausencia de `AreaNotice` inicial e uma
segunda RED em `engine-tests-review-red.log` detectou o aviso `Rewarded`
prematuro. O GREEN daquela etapa cobre texto/retangulo para as tres faixas de nivel, estado de
cada transicao e selecao inclusiva de
destinatarios, incluindo as bordas superior/direita e o limite Y distinto entre
Ready e Start. Para cada aviso, o teste tambem decodifica o frame e confere tipo,
tamanho, ID, checksum, terminador NUL e bytes Latin-1 do texto 7.69;
`MessagePanelWire` mantem a cobertura generica do contrato. Engine.Tests 379/379;
a execucao registra somente o CS8604 preexistente em
`AttackDivineBlessingTests.cs:256`. Build completa da solution com SDK
10.0.401: 0 avisos/0 erros. Evidencias em
`Backup/Build/AttackBattleRoyaleNotices-20261005/`. Nao houve smoke TCP
especifico de envio BR, E2E visual ou publicacao naquela etapa. A limpeza por
nivel foi concluida em `COMBAT-ATTACK-BR-AREA-CLEAR-001`; portas, parties, dano
ambiental, selecao do vencedor e placar permanecem fora.

Decisao de escopo de 05/10: adiar o aviso ate o item existir. O follow-up
`COMBAT-ATTACK-BR-REWARD-001` concluiu essa dependencia em 06/10 e registrou a
ordem `BRItem` -> estado 3 -> `MSG_CreateItem` -> `MessagePanel`.

## COMBAT-ATTACK-BR-SCHEDULE-001 - agenda e estado Battle Royale no Host (05/10/2026)

### Comparacao TMProject 7.69 / W2PP

TMProject `Server.cpp:7928-8028` e W2PP `Server.cpp:6840-6940` compartilham a
selecao diaria `(diaDoMes - 1) % NumServerInGroup`, os bypasses explicitos de
TESTSERVER/LOCALSERVER, `BRItem > 0`, a hora local `BRHour` e o ciclo dirigido
pelo estado anterior: 0->1 na fase 0, 1->2 na fase 5, 2->3 na fase 13 e 3->0
quando a fase e >=14 (`minuto % 20`). Nenhuma divergencia deste recorte foi
encontrada.

### Port C# e limite de escopo

`LegacyBattleRoyaleConfiguration` exige `BRItem`, `BRHour`, `ServerIndex`,
`NumServerInGroup`, `TestServer` e `LocalServer`; valida os intervalos da
referencia e nao habilita configuracao inferida de bind/ambiente. Zero em
`BRItem` desliga a agenda. `LegacyBattleRoyaleSchedule` inicia em estado zero,
observa hora/fase elegiveis sem recuperar transicoes perdidas e conserva o
estado em memoria. `LegacyBattleRoyaleScheduleController` publica as transicoes
no mesmo lock do WorldHub, preservando os demais campos. A transicao 2->3 fica
pendente até o Host tentar criar o item do altar; só depois disso o controller
publica estado 3.

O Host aceita `--battle-royale-config <arquivo.json>` opcional, le/valida o
arquivo antes de construir/iniciar o listener e, quando presente, executa um
`PeriodicTimer` de dois segundos usando `DateTime.Now`; sem opcao, nao inicia o
loop. Cada mudanca e registrada no log. A etapa original alterava somente
`BattleRoyaleState`/`BattleRoyaleItem`; os avisos Ready/Start foram adicionados
em `COMBAT-ATTACK-BR-NOTICES-001`, spawn/Rewarded em
`COMBAT-ATTACK-BR-REWARD-001`, limpeza por nível em
`COMBAT-ATTACK-BR-AREA-CLEAR-001` e refresh de grade/limpeza de party em
`COMBAT-ATTACK-BR-GRID-PARTY-CLEANUP-001`. Os efeitos restantes continuam fora:
limpeza de personagens/mapa, dano ambiental, seleção do vencedor,
recompensa além do item BRItem e placar.

### Validacao local

Engine.Tests 377/377; `dotnet build WydCdk.Engine.slnx` com SDK 10.0.401, 0
avisos/0 erros; build do Host 0 avisos/0 erros; publish self-contained
`win-x64` concluido. Smokes Debug e self-contained em `127.0.0.1:18283`
confirmaram agenda desativada sem config e carga explicita com item 700/hora
23/servidor 0 de 3/bypass TESTSERVER, alem de conexao e shutdown local.
JSON malformado, arquivo ausente e hora 24 encerraram antes do bind; a porta
ficou livre. O estado 0/1/2/3 e as fases 0/5/13/14 sao exercitados pelos testes
deterministicos do WorldHub/controller. Evidencias em
`Backup/Build/AttackBattleRoyaleStateSchedule-20261005/` (SDK, testes, builds,
publish e logs de cada caso). Uma execucao intermediaria do conjunto teve uma
falha isolada em `attack authoritative pipeline`: o teste legado deixa o roll
de miss aleatorio; DEX igual tem 1% de miss. A falha nao se repetiu nas duas
execucoes seguintes (376/376 e 377/377); nenhum codigo de combate fora do
escopo foi alterado. Apos a revisao, outra execucao isolada falhou em
`world physical attack`; o fixture do scroll de ressurreicao tambem deixa o
`attackMissRoll` aleatorio (padrao -1). A repeticao passou 377/377, e o build
final da solucao passou com 0 avisos/erros. O teste do gate `bParty` agora
exercita explicitamente os estados ativos 1, 2 e 3. E2E visual e efeitos
completos do evento permanecem pendentes; sem publicacao sandbox, Site/producao,
commit ou push.

## COMBAT-ATTACK-BR-GUILD-001 - override de Guild Battle Royale no `bParty` (05/10/2026)

### Comparacao TMProject 7.69 / W2PP

TMProject 7.69 `_MSG_Attack.cpp:416-428` e W2PP
`_MSG_Attack.cpp:375-387` fazem a mesma sequencia antes de avaliar `bParty`:
aplicam `GuildDisable`, normalizam os Guilds efetivos ambos-zero para Guild -1
do atacante, e depois repetem a atribuicao para Guild -1 quando `BrState` e
verdadeiro, `BRItem > 0` e `TargetX/Y` estao no retangulo inclusivo
X=2604..2648, Y=1708..1744. O gate seguinte, TMProject:590-597 e W2PP:504-511,
continua elegivel pela party/lider ou pelo Guild efetivo igual; fora disso soma
10/type27. Nao ha divergencia entre as fontes neste ramo.

### Port C# e evidencias

`LegacyCombatWorldState` agora carrega `BattleRoyaleState` e `BattleRoyaleItem`
como campos opcionais no fim do record; ambos defaultam a zero. O helper
`LegacyPartySkillTargetGate.ResolveAttackerGuildId` aplica a ordem acima, com
limites inclusivos. `WorldHub` usa o `PositionX/Y` autoritativo do atacante nos
gates `bParty` de jogador, NPC, summon e na validacao secundaria. O Host atualiza
essa posicao a partir de `Action.TargetX/Y` somente apos o movimento ser aceito
(`Program.cs:2337-2340`), correspondendo ao ultimo alvo Action valido que a
fonte consulta. Party comum segue elegivel dentro da arena; o override nao abre
elegibilidade para Guild diferente.

TDD RED: mesmo Guild em (2604,1708) retornou `Accepted` antes da regra.
GREEN: Engine.Tests 368/368 cobre os quatro cantos inclusivos, um ponto externo
em cada lado, `BrState=0`, `BRItem=0`, alvo NPC na borda oposta, Guild diferente
e mesma party. O run do projeto de teste reportou o CS8604 ja existente em
`AttackDivineBlessingTests.cs:256`; build de `WydCdk.Engine.slnx` com SDK
10.0.401 terminou com 0 avisos/0 erros. Logs e `dotnet --info` em
`Backup/Build/AttackBattleRoyale-20261005`.

Contexto historico: na conclusao de BR-GUILD, `BrState/BRItem` ainda nao tinham
produtor no Host e o gate nao podia ser ativado em runtime. A etapa posterior
COMBAT-ATTACK-BR-SCHEDULE-001 adicionou o produtor opcional e o validou em
loopback; os efeitos completos do evento e o E2E visual seguem pendentes.

## COMBAT-ATTACK-SKILL-TYPE6-NPC-001 - Type 6 contra dano de skill em NPC (05/10/2026)

### Comparacao TMProject 7.69 / W2PP

O gate comum de Protecao Divina/Absoluta em TMProject 7.69
`_MSG_Attack.cpp:1910-1928` roda depois do calculo dos ramos de dano. Para alvo
classe 0 ou 1, verifica os 32 `Affect` e troca `dam` por zero somente se
`Affect.Type==6` e `dam>0`; o teste nao restringe o alvo a jogador. W2PP nao
possui gate ativo equivalente no dano de `MSG_Attack`.

### Port C# e evidencias

O caminho de NPC persistente chega a `FinishNpcSkillAttackLocked` tanto para
skill 79 quanto para dano elemental. O finalizador agora consulta a classe do
snapshot e `npc.AffectSnapshot` antes de calcular HP restante, remover o NPC ou
executar XP/drop. Assim, Type 6 conserva HP e impede a morte por dano positivo
de skill nas classes 0/1. Dano nao positivo, classe 2 e classes 0/1 sem Type 6
seguem sem protecao. O gate nao muda o wire nem os outros efeitos.

TDD RED: `AttackSkillType6Tests` reproduziu dano 100 e HP 1000->900 para NPC
classe 0 com Type 6 (367/368 grupos da Engine.Tests passaram). GREEN cobre NPC
classes 0/1 protegidos e controles classe 2 com Type 6 e classe 1 com Type 5,
conferindo o HP autoritativo do NPC. Engine.Tests 368/368; build da solution
`WydCdk.Engine.slnx`, SDK 10.0.401: 0 avisos/0 erros; self-test do harness
passou. O recorte foi exercitado diretamente pelo `WorldHub`; nao houve smoke
TCP especifico de NPC, alteracao Host/wire, publicacao sandbox ou E2E visual.
Logs em `Backup/Build/AttackNpcType6-20261005`.

## COMBAT-ATTACK-SKILL-TYPE6-001 - protecao Type 6 no dano de skill (05/10/2026)

### Comparacao TMProject 7.69 / W2PP

TMProject 7.69 `_MSG_Attack.cpp:1910-1928` aplica Protecao Divina/Absoluta
depois da resolucao do miss. Para classe 0 ou 1, percorre `MAX_AFFECT` (32) e
troca `dam` por zero somente quando `Affect.Type==6` e `dam>0`. Como miss `-3`
ja e negativo, permanece intacto; o mesmo gate nao troca dano de cura negativo.
O bloco ocorre no calculo de dano comum do `MSG_Attack`, inclusive no slot de
skill; nao e uma validacao por tamanho de pacote.

O handler ativo W2PP `_MSG_Attack.cpp` nao possui essa protecao de classe/affect.
As ocorrencias de `InstanceType==6` ali sao o efeito de cura, sem equivalencia
com o gate defensivo. Segue-se o contrato 7.69 escolhido para a release.

### Port C# e evidencias

`WorldHub` aplica o gate no ramo de dano de skill depois de
`ResolveLegacyAttackMiss` e antes de escrever o HP restante, apenas para
`CharacterClass` 0/1 com dano positivo e Type 6 em qualquer slot. Efeitos
negativos permanecem sob a logica existente. A mudanca nao afeta outros opcodes.

TDD RED: `AttackSkillType6Tests` falhou com dano 100/HP 900 para classe 0 com
Type 6 (367/368 passaram); GREEN cobre classes 0/1, classe 2, affect diferente,
miss `-3`, e cura `-320` que eleva HP 500->820. Engine.Tests final 368/368.
Build completa da solution com SDK 10.0.401: 0 avisos/0 erros; self-test do
harness passou.

O smoke TCP headless usou duas contas de teste em `127.0.0.1:18283`: atacante
classe 3 e alvo classe 0 com Type 6. Slots fisicos 1/2 e skill 82 foram
retransmitidos iguais para ambos os clientes, com dano zero e sem `SetHpMp`;
HP do alvo continuou 100000. No logout normal, somente campos de posicao nas
copias de trabalho diferiram; as copias-fonte permaneceram intactas e nao foram
usadas pelo host.
Host encerrado cooperativamente e porta liberada. Logs, RED/GREEN e SDK:
`Backup/Build/AttackSkillType6-20261005`. Sem republicacao sandbox ou E2E visual.

## COMBAT-ATTACK-HEAL-EXP-001 - FakeExp por cura no MSG_Attack (04/10/2026)

### Comparacao 7.69 / W2PP

TMProject 7.69 _MSG_Attack.cpp:933-950 e W2PP _MSG_Attack.cpp:693-709 calculam
calc_exp pela variacao real de HP do alvo dividida por oito, limitam cada
contribuicao a 120 e ignoram valores nao positivos. A contribuicao entra em UNK2
somente com conjurador valido em USER_PLAY, alvo diferente do conjurador e alvo
fora das cinco zonas de cidade. UNK2 conserva o maior valor entre os slots.
No final, ambas as fontes substituem o EXP do ataque pelo UNK2 positivo e
limitam o FakeExp entre 0 e 200.

A diferenca esta no DoubleMode: W2PP multiplica FakeExp por 2 quando
DOUBLEMODE e verdadeiro; a linha 7.69 multiplica diretamente por DOUBLEMODE.
O TMSrv 7.69 carrega esse inteiro da configuracao e do evento AutoDouble
(retorna a 1 ao terminar). COMBAT-ATTACK-DOUBLEMODE-MULTIPLIER-001 substituiu
o estado bool C# por um inteiro default 1 e aplicou-o aos consumidores
existentes de FakeExp de cura e XP generica de grupo. W2PP tambem diverge na XP
de grupo: seu handler usa flag/2x, enquanto o TMSrv 7.69 multiplica pelo inteiro.
A integracao semanal da configuracao e da agenda no Host C# esta registrada em
`COMBAT-ATTACK-AUTODOUBLE-001`.

7.69 tambem tem um ramo anterior para DivineBlessing/RateRegen
(_MSG_Attack.cpp:752-845), agora portado separadamente em
`COMBAT-ATTACK-DIVINE-BLESSING-001`. O W2PP declara campos homonimos, mas nao
tem uso ativo equivalente no ataque; por isso a regra implementada segue o
ramo efetivo 7.69 e mantem o blob bruto via adapter explicitamente medido.

### Implementacao e evidencia

LegacyAttackHealExperienceMath calcula com HP anterior e HP final depois do
teto de MaxHp e da divisao de montaria. WorldHub agrega o maior resultado
e aplica o DoubleMode e o teto 200 somente quando o batch termina normalmente;
um abort preserva o comportamento de suppressao do relay e descarta o override.
O Host escreve o resultado no FakeExp autoritativo da confirmacao 7.69. Para
uma cura pura sem contribuicao elegivel, escreve zero e nao repassa um valor
forjado pelo cliente. MOB.Exp / CurrentExp persistido nao e alterado.

AttackHealingExperienceTests valida teto 120, cidade e auto-cura excluidas,
dois alvos (79 e 12) conservando 79, DoubleMode (158), teto final 200,
persistencia de MOB.Exp e supressao do FakeExp no abort. Engine.Tests 349/349.
Os smokes TCP Debug e self-contained originais validaram Cura 27 Mortal, HP
1000->1638, slot -638, relays iguais, slots excedentes, ausencia de crack errors
e FakeExp=0 na fixture urbana, apesar do sentinel 77777 enviado pelo cliente.
A extensao COMBAT-ATTACK-DOUBLEMODE-MULTIPLIER-001 acrescentou smoke TCP Debug
com fator 3: Action move os participantes para fora da cidade e o relay confirma
79*3 limitado a FakeExp=200, Cura -638 e HP 1638; evidencia
`Backup/Build/AutoTradeCloseSmoke/6fc7f69d01ee429c880932c84de7a4f3`.

TDD RED do Host: o smoke falhou porque a confirmacao devolvia FakeExp=77777.
GREEN e evidencias:
Backup/Build/AutoTradeCloseSmoke/9728cdbbb0914320b0f7c03ec270ddc4,
7f4e4ef48d1a4c48b3f6f13f4feefd46 e
c1350a3ce85f4f1fb7401dbc37597d41. As duas execucoes GREEN indicam
ExitCode=0, AssertionsPassed=true, CleanupCompleted=true, hashes originais
intactos e porta 18283 liberada. Build completa SDK 10.0.401: 0 avisos/0 erros;
dotnet --info: Backup/Build/COMBAT-ATTACK-HEAL-EXP-001/dotnet-info.log.

Runtime-only self-contained em
Server/artifacts/publish-combat-attack-heal-exp-20261004/runtime.zip: 197
arquivos sem PDB/source/docs/segredos; SHA-256
E5AD645E63DC2823F94CF0B4FD91AF120E91FE28E8E2E6C7BFB92AC8A76B9A09. Core
3CC8035F5320FA80D2C452E0BCA290A756C6F3A68A79B0BE6CB8E8D6780440D6, World
7AA125BB10CA0C742AA440A175E1EA9FFC9FCEF94043851A90C86A2302C5C788, Protocol
9334E4763700F6D1C632A3C0175C5F5DC96EA2BBDF8BAF445319737AAF36A033.
Publicado somente no sandbox aprovado em
C:/WYDCDK/Server/test-releases/20260924-v7670/WydCdk.Server-v7670-20260924.
Readback independente confirmou o fingerprint dos 197 arquivos
FDAEC20B62AB904CDDDFAC3BADD627D47B1A56F91A87C2047EFF11A6EC165EA7, tarefas
UP/PVP Running, listeners 8281/8282 no executavel aprovado e backup anterior
backup-autotrade-close-heal-exp-20261004-22c42faf14b140e1ad12e86d5dd9af20
com 197 arquivos. Staging removido. Sem Site/producao, commit/push ou E2E visual.

## COMBAT-ATTACK-DOUBLEMODE-MULTIPLIER-001 - multiplicador inteiro (04/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:2347-2358` multiplica `Exp` diretamente por
`DOUBLEMODE`, depois normaliza resultados nao positivos para zero e limita a
200. `ConfigIni.cpp` carrega o inteiro; `AutoEvent.cpp::fAutoDouble` define
`MultEXP` no inicio e restaura 1 no fim. W2PP `_MSG_Attack.cpp:1730-1732`
trata o valor como flag e multiplica por 2.

O uso em XP de grupo tambem foi comparado: TMSrv 7.69 `MobKilled.cpp:517-518`
usa `exp *= DOUBLEMODE`; W2PP `MobKilled.cpp:540-541` usa `if (DOUBLEMODE)` e
`exp *= 2`. `LegacyCombatWorldState.DoubleModeMultiplier` agora e inteiro
(default 1), e tanto `LegacyAttackHealExperienceMath` quanto
`LegacyPartyExperienceMath` recebem o mesmo estado. O FakeExp multiplica a
contribuicao maxima e depois aplica a faixa 0..200 usando intermediario `long`;
entrada sem contribuicao continua sem override. A multiplicacao da XP de grupo
segue a ordem do TMSrv 7.69 antes dos gates Kefra e da reducao/incremento de
evento iniciante.

`AttackHealingExperienceTests` cobre fatores 0, 1, 2 e 3, teto 200 e o estado
passado pelo WorldHub; `PartyExperienceModifiers` e o mesmo teste cobrem a
multiplicacao escalar na XP de grupo. A integracao de configuracao e agenda do
Host e documentada em `COMBAT-ATTACK-AUTODOUBLE-001`; nenhuma republicacao
sandbox foi feita nesta extensao.

## COMBAT-ATTACK-AUTODOUBLE-001 - agenda semanal EVENT.DOUBLE (04-05/10/2026)

TMProject 7.69 `ConfigIni.cpp` carrega `EVENT.DOUBLE` e
`AutoEvent.cpp::fAutoDouble` compara `tm_wday` (domingo=0), hora e minuto.
Quando o inicio coincide com dia/hora, `MinStart` ja passou e o evento esta
inativo, define `DOUBLEMODE=MultEXP`, emite `MSGStart` e marca o evento ativo.
Quando inicio e fim compartilham dia/hora, o codigo ignora um inicio tardio
depois de `MinEnd`. No dia/hora final, a partir de `MinEnd`, restaura
`DOUBLEMODE=1`, emite `MSGEnd` e limpa o estado. W2PP tem comandos e consumidores
de `DOUBLEMODE` como flag, mas nao possui agenda `fAutoDouble` equivalente; a
agenda implementada segue o TMSrv 7.69.

O Host aceita `--double-mode <int>` (default 1) e a opcao de startup
`--auto-double-config <eventAuto.json>`. O parser tipado exige os nove campos
de `EVENT.DOUBLE` e valida dias 0..6, horas 0..23 e minutos 0..59. Um
`PeriodicTimer` de 2 segundos consulta `DateTime.Now`, aplica o multiplicador
ao `WorldHub` e, ao fim, restaura literalmente 1. A cada transicao,
`BroadcastAutoDoubleTransitionAsync` envia primeiro `MessagePanelConfirmation`
e depois `MagicTrumpetConfirmation` pelo broadcast global. O segundo frame
reproduz `MSG_ChatColor`: type `0x0D1D`, 92 bytes de mensagem mais `int32`,
prefixo `[Event:]> ` e `TNColor::Speak` (`0xFF00CD00`). TMProject 7.69
`AutoEvent.cpp::fAutoDouble` envia esse par tanto para `MSGStart` como para
`MSGEnd`; W2PP confirma tipo, layout e dispatcher global, mas nao tem a agenda
`fAutoDouble` equivalente. Falhas de envio sao registradas sem terminar o
loop; o arquivo e carregado somente no startup, sem hot reload.

`AutoDoubleTests` cobre leitura/validacao do JSON, dias da semana, inicio e fim
nas fronteiras de minuto, duplicidade, trava de mesma hora e atualizacao do
`WorldHub`; `MagicTrumpetTests` valida o layout e a cor do frame. Engine.Tests
367/367; build solution SDK 10.0.401, 0 avisos/0 erros; self-test do harness
passou. O smoke Host em `127.0.0.1:18284` leu a config base 1, ativou
MultEXP=3 e encerrou com `shutdown` pela entrada do terminal; readback confirmou
porta liberada. Evidencia: `Backup/Build/AutoDouble-20261004/host-schedule-smoke-result.txt`.
O smoke TCP Debug em `127.0.0.1:18283` manteve dois clientes online durante
inicio e fim e verificou, para ambos, os pares MessagePanel/MagicTrumpet na
ordem, textos e tamanhos esperados, checksums e cor Speak. Evidencia:
`../Backup/Build/AutoTradeCloseSmoke/2db4e8c9c1cf48a7a0da930bfeda608a`
(`ExitCode=0`, `AssertionsPassed=true`, `CleanupCompleted=true`, hashes dos
fixtures-fonte intactos e porta liberada).

O smoke e headless TCP; nao inicia `WYD.exe` e nao prova E2E visual. Nenhuma
republicacao sandbox foi feita nesta extensao; sem Site/producao, commit ou push.

## COMBAT-ATTACK-DIVINE-BLESSING-001 - skill 201 / RateRegen (04/10/2026)

### Comparacao e ABI

TMProject 7.69 `_MSG_Attack.cpp:752-845` verifica
`Extra.SecLearnedSkill & 0x2` no caminho anterior aos slots normais. Quando o
bit esta ativo, cria um `MSG_AttackOne` interno para skill 29, com autor como
alvo, `CurrentMp=-2` e `Special=0`; o processamento de cura Type 6 usa
`DivineBlessing/RateRegen`, desconta mana e grava o dano assinado original no
slot wire. W2PP contem campos com esses nomes, mas a busca do handler ativo
nao encontrou equivalente para o gatilho/subacao. Esta fatia preserva esse
estado distinto da learned-skill word serializada no `MOB`.

O probe Win32 foi executado nos headers efetivos do cliente 7.69, DBSrv 7.69,
TMSrv 7.69 e W2PP: `MOBEXTRA=552`, `offsetof(SecLearnedSkill)=4`, largura 4;
os tres layouts de servidor concordaram em `552:4:4`. O TMSrv efetivo inclui
`Servidor/Source/Code/Basedef.h`; a copia vizinha sob TMSrv e obsoleta para
este campo. `LegacyMobExtra` le o uint32 bruto no offset medido e nao o confunde
com `MOB.Learned[1]`.

### Implementacao e evidencia

O Host resolve o descritor pai code-only da skill 201. Sob o mesmo lock/batch,
WorldHub consulta o bit no snapshot bruto e, quando elegivel, processa a
subacao interna de skill 29 com `Special=0`. Mana insuficiente captura
`SetHpMp` privado antes do pai, sem curar; cura bem-sucedida captura HP/MP e
AttackOne de 68 bytes, `CurrentMp`/`ReqMp` autoritativos e participantes da
view. O Host envia subacoes na ordem antes do pai e fora do lock. `MOB.Exp` e
FakeExp persistido nao sao alterados.

`AttackDivineBlessingTests` cobre bit, custo de mana/SaveMana, cura e divisores
de montaria, Clan 4/mapa proprio, HP zero/negativo, ausencia de EXP e abort
posterior. Engine.Tests: 364/364; solution build SDK 10.0.401: 0 avisos/0
erros. Smokes TCP em `127.0.0.1:18283` passaram em Debug e self-contained:
`Backup/Build/AutoTradeCloseSmoke/96c4f93a6a8540a08101215711948826` e
`Backup/Build/AutoTradeCloseSmoke/3e5f1367ec17424591080ec527121688`.
Ambos `ExitCode=0`, assertions e cleanup verdadeiros, hashes originais iguais
e porta liberada; cobrem cura 900->1000/MP 500->470, mana insuficiente, ordem
dos relays e isolamento do observador distante. O primeiro Debug terminou
antes do ataque porque o spawn aleatorio colocou observadores juntos; ajustar
somente a fixture distante resolveu o setup.

Runtime-only self-contained local: 197 arquivos; ZIP SHA-256
`11B038E428A82BEEC39A41DBABD684BA785FC1D356FFEA239086C3B307EE52BF`. Core
DLL `FCD5264A9A544D27CE9530A74C5C75EDAF99877CF2994A7F4F6C6B957A5D9CF3`,
World `D9CA0E47ADF8E8ACAE0B65483A6650331A5B65D52110528A7DE6BA494CED6C3B`,
Protocol `6E49A4D919C33BA10F6E823F48D937FFF44C74AC19113EFF246B7C3A0D49E604`.
O runtime esta pronto para o sandbox aprovado; readback remoto fica pendente
enquanto o SSH recusa novos handshakes (`MaxStartups`/timeout). Nenhum arquivo
foi enviado nesta tentativa. `WYD.exe` visual, Site/producao, commit e push
estao fora deste recorte.

## COMBAT-ATTACK-MIXED-SLOTS-001 - batch unico de slots fisicos e skill (03/10/2026)

TMProject 7.69 e W2PP percorrem os slots de `MSG_Attack` em ordem e escolhem
ramo pelo marcador de cada `STRUCT_DAM`: `Damage==-2` e fisico; `Damage==-1`
e skill. A source TMProject do cliente tem um produtor `Range==-1` em
`TMFieldScene`, mas os dados base 7.69 observados usam Range 5/6 nas skills
relevantes; portanto o produtor de pacote misto fica dormente nessa base e a
rota e exercitada por vetores de wire/headless.

O C# agora converte os marcadores por slot para `LegacyAttackActionBatchRequest`
e executa fisicos e skills validas em ordem sob um unico lock do `WorldHub`.
`DoubleCritical`, `Progress`, overrides de slot e recursos autoritativos sao
compartilhados pela acao. Os wrappers publicos fisico/skill de um slot continuam
como adapters de compatibilidade. Um indice de skill invalido nao autoriza o
ramo `-1`, mas preserva esse marcador no relay e ainda permite os slots fisicos
`-2`. Efeitos de skill nao portados permanecem desligados por
`ApplySkillEffects=false`.

Ao concluir normalmente, o Host monta uma unica confirmacao agregada e a envia
antes dos relays secundarios; todo I/O ocorre fora do lock. Capturas por slot
seguem a ordem original. Remocao de NPC, XP e drops diferidos sao finalizados e
enviados ordenados pelo slot somente no fim normal. Se um slot fisico aborta por
alcance, o relay agregado e a finalizacao diferida sao suprimidos; mutacoes e
efeitos imediatos de slots anteriores permanecem.

RED reproduziu no TCP o slot fisico `-2` descartado quando `SkillIndex` indicava
skill. Engine.Tests passou 345/345; self-test do harness passou; build completa
SDK 10.0.401 terminou com 0 avisos/0 erros. Smokes Debug e self-contained em
`127.0.0.1:18283` cobriram ambos os sentidos em pacote de 13 slots, ordem de
relays, `Progress`, MP final/`ReqMp=4963`, marcador fisico com indice de skill
invalido e abort depois de cura: a cura anterior permaneceu e nao houve
`AttackConfirmation`. Evidencias em
`Backup/Build/AutoTradeCloseSmoke/d5c1724f8530453ab168163480dabbfa` e
`Backup/Build/AutoTradeCloseSmoke/1a10e562fde94fcf871b71dd07c2e997`; fixtures
originais intactas, cleanup completo e porta liberada. A validacao e headless;
nao inicia `WYD.exe`. E2E visual, novos efeitos de skills e outros gates comuns
de ataque permanecem fora deste recorte.

## COMBAT-GUILD-DISABLE-001 - comandos e interacao com `bParty` (02/10/2026)

TMProject 7.69 `_MSG_MessageChat.cpp:31-60` e W2PP
`_MSG_MessageChat.cpp:38-61` tratam `guildon`/`guildoff` pelo primeiro token
exato e somente em `USER_PLAY`. Cada comando e consumido; estado repetido nao
envia resposta. Na transicao, ambos chamam `SendScore` e enviam
`MSG_GuildDisable` privado com Parm 0/1 (`Basedef.h:2544`).
`SendFunc.cpp:1233-1284` monta
`MSG_UpdateScore` para `GridMulticast`, projetando Guild=0 somente quando
`GuildDisable` esta ligado, e em seguida envia `MSG_UpdateAffect` privado.

No C#, `GuildDisabled` vive no participante/sessao e volta a false quando o
estado do personagem e instalado; `TrySetGuildDisabled` copia score e affect sob
lock, sem alterar o MOB persistido, e captura os jogadores visiveis. O Host envia
`MSG_UpdateScore` 7.69 (`0x0336`, 152 bytes) aos visiveis e, ao autor,
`MSG_UpdateAffect` (`0x03B9`, 268 bytes) e `MSG_GuildDisable` (`0x03A4`, 16 bytes,
ID `ESCENE_FIELD=30000`, Parm int32). Repetir o estado consome o chat sem relays.

O gate de skill `bParty` tambem foi conferido em TMProject `_MSG_Attack.cpp:416-428,590-597`
e W2PP `_MSG_Attack.cpp:375-386,504-511`. As duas fontes zeram o Guild efetivo
de cada lado com `GuildDisable`; se ambos ficam zero, normalizam o Guild do
atacante para -1 antes de comparar party/lider e Guild. Assim, dois personagens
com `guildoff` nao viram Guild comum; outsider continua bloqueado com 10/type27,
mas membros da mesma party permanecem elegiveis. O helper C# mantem zero fora da
regra de Guild comum e consulta o lider de party. O override Battle Royale foi
portado separadamente em `COMBAT-ATTACK-BR-GUILD-001`; o produtor Host dos sinais
do evento continua pendente. O recorte GuildDisable cobre jogadores; GuildDisable
dinamico em NPCs permanece fora de escopo.

TDD cobre projecao Guild=0/restauracao, persistencia do MOB, recipients visiveis,
no-op, ambos os comandos e sinais wire, bloqueio quando um lado esconde Guild,
outsiders guildoff antes de formar party e elegibilidade apos a party. O smoke TCP
em `127.0.0.1:18283` tambem confere frames nos dois clientes, ausencia de relay de
chat publico, UpdateAffect privado ausente no peer, HP inalterado nos gates e
cura autorizada depois do convite/aceite.
Engine.Tests 328/328, harness self-test passou e solution build SDK 10.0.401 teve
0 avisos/0 erros. Smokes Debug/self-contained:
`Backup/Build/AutoTradeCloseSmoke/5096c37d3a904e5082e5ba5e3d96703d` e
`Backup/Build/AutoTradeCloseSmoke/a844228f88714f9f9d9d86446773a128`; contas
originais intactas e porta 18283 liberada.

ZIP runtime-only de 197 arquivos, SHA-256
`F78CDCB4A9ED05FC49E41663AE421EB20ED82DD876987FAD81AF2215E5E55BD5`; Core
`7D662D334B4F6C99ADCD4CFCEAC49462BFA8A3CBEFF80DD24B4DB59A1BF85F6E`, World
`A8259F996BDD39672610D277912C77EE8747D812F50782B4B6022331A2A1F951`, Protocol
`1FCAA4AF4ABD7B2CE994DBE004382AFF5D2A2FC347D3DF391B2923F5957083D1`. Readback
independente confirmou o executavel esperado, UP/PVP Running e portas 8281/8282
Listen. Backup remoto `backup-autotrade-close-f2086228d5a94e488c4b9622c19799f1`
preserva 197 arquivos; staging foi removido. A build self-contained local esta em
`Backup/Build/GuildDisable-20261002-runtime`. A execucao remota do helper encerrou
com erro apenas ao imprimir o marcador final, por quoting; verificacao remota
independente confirmou hashes e servicos depois da copia/reinicio. Sem
Site/producao, commit ou push; E2E visual continua pendente.

## COMBAT-ATTACK-PARTY-001 - elegibilidade `bParty` em jogadores e NPCs registrados (02/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:409-428,590-597` e W2PP
`_MSG_Attack.cpp:368-386,504-511` usam o lider efetivo da party e o Guild dos
dois MOBs. Com `bParty != 0`, o alvo passa se `leader == mobleader` ou
`Guild == MobGuild`; caso contrario o handler soma `AddCrackError(10,27)` e
continua para o proximo slot. O type28/MaxTarget ocorre primeiro nas duas
fontes, portanto o excesso conserva prioridade. `skilldata.csv:29` define
Recuperar (skill 29) com `PartyCheck=1` e `MaxTarget=13`.

O C# aplica o gate antes do efeito a jogadores e alvos NPC registrados no
`WorldHub`.
Jogadores na mesma party ou no mesmo Guild nao zero passam; alvos sem party e
sem Guild comum recebem 10/type27 por slot, relayed com ID e `Damage=-1`, sem
efeito. Para NPC, o Guild vem do MOB; como o snapshot C# nao possui o lider
dinamico do NPC, o ID de conexao do NPC e usado como seu lider efetivo ate
portar esse estado. Testes cobrem NPC fora do Guild, NPC no mesmo Guild,
precedencia de MaxTarget e validacao de skills cujo efeito ainda nao foi portado.

O smoke TCP usa um NPC sintetico em loopback: NPC e jogador outsider
foram bloqueados, com dois registros 10/type27 e total 20; o relay reteve o
slot do NPC como ID/-1 e nao enviou sincronizacao de HP/MP por esse efeito.
Depois o harness formou uma party por convite/aceite TCP e a skill 29 curou o
membro de 1000 para 1150 HP. O passe de NPC no mesmo Guild esta coberto em
teste de dominio, nao pelo smoke TCP.

O recorte de `GuildDisable` para comandos, players e a interacao com `bParty`
esta em `COMBAT-GUILD-DISABLE-001`; ainda nao existe estado dinamico de lider de
NPC no snapshot. O override de Guild Battle Royale esta em
`COMBAT-ATTACK-BR-GUILD-001`; o produtor opcional state-only esta em
`COMBAT-ATTACK-BR-SCHEDULE-001`. Efeitos completos do evento permanecem fora.

Engine.Tests: 327/327; self-test do harness passou; solution build SDK 10.0.401:
0 avisos/0 erros. Smokes TCP Debug e self-contained passaram em
`Backup/Build/AutoTradeCloseSmoke/3a2244dbc2354468839bbe0a30166266` e
`Backup/Build/AutoTradeCloseSmoke/168b00db40a1488bab0e877691be68ec`. O ZIP
runtime-only local de 197 arquivos, `WydCdk.Server-v7670-attack-party-npc-20261002.zip`,
tem SHA-256 `41E281FCC93EC7CDD0E345BC60B4109C53EEE47066C4E6C301786490C1817F49`.
Foi publicado somente no sandbox autorizado; readback independente confirmou
UP/PVP `Running`, portas 8281/8282 no executavel esperado e hashes Core
`428CD9E6EEE600860D62458FF3B35323FE6BE65D47CE50E78DA0DD4FB2D1BFEF`, World
`4A425700D01BF3103ABA84FDB073E3766F1A1DA949BD79471A8FE2B811CC0B9C` e Protocol
`B17A09C55AE7E8891269B615260E5C95665C8FFB1212F62379F45BBCC7DCAF81`. Backup
`backup-attack-party-npc-20261002-41e281fc` preserva os 197 arquivos runtime;
fingerprint SHA-256 do manifesto:
`EECC6EAF0FD479C1CEBB4AAE36F073E8E1C3845396CBBE6ED03C8A0C657B1EC9`. ZIP de
transferencia e staging temporario foram removidos; as contas originais e a
porta local 18283 permaneceram intactas. E2E visual continua pendente.

## COMBAT-ATTACK-SUMMON-PARTY-001 - lider da party e bParty para summons (03/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:409-412,590-596` e W2PP
`_MSG_Attack.cpp:368-371,504-510` leem `pMob[idx].Leader` para o alvo e usam
`idx` quando o valor e zero. Nas duas fontes, MaxTarget/type28 antecede
`bParty`/10-type27. `Server.cpp:3765,3917,4051` da 7.69 e
`Server.cpp:2958,3110,3244` do W2PP distinguem o lider efetivo da party do
`Summoner=conn` que criou o summon.

O C# agora resolve o lider da party do invocador ao criar cada `LegacySummonedMob`
e preserva `SummonerConnectionId` como o criador. A lista `Participant.SummonIds`
continua pertencendo ao invocador para movimento e limpeza; remocao por morte
retira o ID dessa lista via `SummonerConnectionId`, nao da lista do lider.
`TryApplySkillAttack`, `TryValidateSkillTargetLimit` e
`TryValidatePartySkillTarget` reconhecem o summon como alvo: verificam
MaxTarget primeiro, comparam o lider autoritativo do atacante ao
`LeaderConnectionId` do summon e usam o Guild do snapshot do MOB.

Outsider sem party/Guild comum recebe 10/type27 e relay com `summonId/-1`.
Alvo elegivel passa pelo gate, mas esta fatia nao implementa cura ou efeitos no
summon: `AttackConfirmation` conserva o slot vazio `(0,0)` e nao ha sincronizacao
de HP/MP. Os testes criam summon a partir de um membro e provam que seu lider e
diferente do invocador; cobrem lider da party, outsider, slot acima de
MaxTarget/type28 e ausencia de efeito.

O smoke TCP headless criou um Condor real de `BaseSummon` pela skill 56. Antes da
party, o ator externo foi bloqueado no summon; depois do convite/aceite, repetiu
como membro elegivel. O primeiro relay reteve `ID/-1`; o segundo ficou vazio e
nao curou o summon. Os quatro casos outsider do fluxo (NPC, mesmo-Guild com um
lado `guildoff`, jogador com ambos `guildoff` e summon) registraram 10/type27,
com totais 10/20/30/40. Marker:
`CLIENT_E2E_PASS attack-party-skill guilddisable-player-npc-summon-party-eligibility-without-summon-effect`.
Engine.Tests 330/330; build da solucao SDK 10.0.401, 0 avisos/0 erros; smokes
Debug e self-contained em `Backup/Build/AutoTradeCloseSmoke/30458cc7c8ae4ae3bc5f2440d7fa9bbb`
e `Backup/Build/AutoTradeCloseSmoke/a60da7e60f044309b763a0d8b8ea22c1`.
Contas originais intactas e porta 18283 liberada. Na data deste smoke, a
publicacao aguardava o recorte de slots mistos. O dispatcher foi corrigido e o
runtime atualizado posteriormente em COMBAT-ATTACK-MIXED-SLOTS-001, registrado
no inicio desta matriz e em `CLIENT_E2E_SMOKE.md`. E2E visual e efeitos de skill
em summons continuam pendentes.

## COMBAT-ATTACK-TYPE6-001 - protecao Type 6 em `MSG_Attack` (02/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:1910-1925` aplica, logo apos resolver o miss,
`Affect.Type == 6` somente quando o alvo e classe 0 (Protecao Divina) ou classe
1 (Protecao Absoluta). O ramo percorre os 32 registros de affect e troca `dam`
para zero somente se ainda for positivo; miss `-3` permanece. O gate precede
RSV_FROST/RSV_DRAIN, reflexao, montaria, ManaControl e mutacao de HP.

O handler ativo W2PP `_MSG_Attack.cpp` nao tem o gate de classe/Affect.Type 6;
seu ramo de mana Type 18 aparece mais adiante (aprox. linha 1540). Seguimos o
contrato escolhido 7.69. A investigacao daquela etapa nao identificou o gate
no dano de skill, e a mudanca C# ficou restrita ao ataque fisico
`MSG_Attack` (`TryApplyPhysicalAttackLocked`). A extensao posterior ao ramo de
dano de skill esta documentada em `COMBAT-ATTACK-SKILL-TYPE6-001`; outros opcodes
continuam fora do escopo.

TDD: o teste novo falhou antes da implementacao porque classe 0/Type 6 recebeu
21 de dano (HP 100 -> 79). O teste cobre classes 0/1, o registro 32, controles
de classe e affect, e miss `-3`. A primeira execucao completa tambem encontrou
uma falha aleatoria pre-existente em `WorldPhysicalAttack` (HpAbs recebeu miss
`-3`); a segunda passou com Engine.Tests 325/325. Build da solution com SDK
10.0.401: 0 avisos/0 erros. O smoke TCP em loopback enviou slots fisicos 1/2,
recebeu dano zero para ator e alvo sem SetHpMp; hashes das contas originais
intactos e porta 18283 liberada. Manifesto e logs:
`Backup/Build/AutoTradeCloseSmoke/53a71bca3e5e4473b58e606f0cb09162`.
E2E visual do cliente permanece pendente. O ZIP runtime-only de 197 arquivos foi
publicado no sandbox (SHA-256
`0C970F828C0175EE73E3D91F946EDF25A2654D8E38F80F44B41C81346267F604`). Backup
`backup-attack-type6-add1b6ed044c4c43a1e09e445a593710` preserva 197 arquivos.
Readback independente conferiu hashes Core/World/Protocol, UP/PVP `Running`,
portas 8281/8282 em `Listen` e staging removido. Sem Site/producao, commit ou
push.

## COMBAT-ATTACK-REQMP-001 - `ReqMp` no relay de `MSG_Attack` (02/10/2026)

No handler TMProject 7.69, as atribuicoes do bloco de mana a `ReqMp` estao
comentadas (`_MSG_Attack.cpp:254-279`), mas no fim do fluxo comum ele escreve
`m->CurrentMp` do estado final do jogador e depois `m->ReqMp = m->CurrentMp`
(`:2364-2365`). O wire 7.69 portanto nao ecoa o `ReqMp` interno nem o campo do
pedido. O W2PP escreve `CurrentMp` e `pUser[conn].ReqMp` juntos no bloco inicial
(`_MSG_Attack.cpp:254-255`) e nao sobrescreve `ReqMp` no fim. O C# anterior usava
`RequestedMana`, seguindo esse relay do W2PP.

`AttackConfirmation` agora serializa `CurrentMp` do snapshot final e faz a
conversao x86 para `short` assinada com `unchecked((short)currentMp)`, igual ao
campo `short ReqMp` dos tres wires 7.69 (168/68/80 bytes). O host deixou de
passar `RequestedMana` para a confirmacao; esse estado continua usado em
sincronizacoes privadas `SetHpMp` e nao foi redesenhado nesta fatia.

TDD RED antes da implementacao: `AttackResponseWire` falhou porque MP79 era
relayed com o ReqMp interno divergente; resumo do resultado em
`Backup/Build/AutoTradeCloseSmoke/combat-attack-reqmp-20261002/tdd-red-summary.md`.
GREEN: Engine.Tests 324/324; vetores
cobrem os tres tamanhos, separacao entre o valor declarado e o autoritativo, e
MP40000 convertido para ReqMp -25536. Build completa `WydCdk.Engine.slnx`, SDK
10.0.401, 0 avisos/0 erros. Os smokes de rede/headless de skill (68 bytes),
multi-alvo (168) e ManaControl (80) passaram em Debug e no self-contained local;
hashes originais intactos e porta 18283 liberada. Evidencias em
`Backup/Build/AutoTradeCloseSmoke/7ac84212595645139731ae5a22412e06`,
`c5d7f1261b7245fe9eac03ad08c00e8c`, `38a5d4ce61ca429590f4e394d5c42eee`,
`8655d7f5fce14018aad830da8b0307af`, `a2634202d3104df1a24793ec06dc45c2` e
`97b2c76eaa684a10b9ec0a725dc0a96c`.

Runtime-only self-contained: 197 arquivos, ZIP SHA-256
`385EF07DA8DF67CA3443BDAB93A4527A21D38A9EB55EE06184BDD256BD17C710`; Core
`7C4B016E437C478AFB1873443EB020A8D44601C3F8C634773597B1A61C6ECFF1`, World
`7F99625904BA2FA1216E8E521B43DAF980BDFEA75B6D05D816ED5065FB431C53`, Protocol
`FC06269E22E7C971CC6385BC255D79F58A54AF2FA7D2680282B8DB0EA80D163F`. Publicado
no sandbox autorizado apos aprovacao do usuario para usar a identidade SSH
existente. Alvo `C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`;
backup `C:\WYDCDK\Server\test-releases\backup-autotrade-close-c57914003fb74f489e9970c0c3285830`
com 197 arquivos e hashes anteriores conferidos. UP/PVP Running, 8281/8282
Listen nos executaveis aprovados; ZIP temporario removido. E2E visual permanece
pendente; sem Site/producao, commit ou push.

## COMBAT-ATTACK-MISS-001 - chance de miss em `MSG_Attack` (02/10/2026)

TMProject 7.69 implementa `GetAttackMiss` em
`Servidor/Source/Code/TMSrv/GetFunc.cpp:132`; `_MSG_Attack.cpp:1861-1879`
sortear `rand()%100` e define `dam=-3` quando `Pop < GetAttackMiss(...)`. A
formula usa DEX do alvo como Min e DEX do atacante como Max. Rsv do atacante
soma +100 para cada bit `0x20`/`0x40` e +50 para cada bit `0x80`/`0x200`.
Comeca em 1%; se Min>Max, `((Min-Max)/100)*3`, limitado a 75%; se Max>Min,
`(Max-Min)/100`, limitado a 10%. A divisao inteira antes da multiplicacao e
preservada: diferenca do alvo menor que 100 da 0%, DEX igual da 1%, e atacante
mais forte tambem pode ter miss positiva ate 10%. O ramo fica sob
`Summoner < MAX_USER` e exclui o proprio atacante: cobre jogador/summon ativo
de jogador, mas nao NPC comum. Executa depois do ramo PK. O decremento local de
`parryretn` por Special4/Mirror Magic nao chega a afetar a decisao de miss na
source examinada.

W2PP tem helper de formula semelhante em `W2PP/Source/Code/TMSrv/GetFunc.cpp`,
mas o handler ativo `_MSG_Attack.cpp:1418+` usa `GetParryRate`, DEX/gear/Rsv e
`rand()%1000+1`; Rsv defensivo `0x200` pode produzir `dam=-4`. O port segue o
handler 7.69 para ataques originados por jogador. Os caminhos automaticos
existentes de parry de NPC/summon continuam em `ResolveLegacyParry`.

`LegacyAttackMissMath.GetRatePercent` espelha a aritmetica 7.69. WorldHub usa
roll injetavel `AttackMissRoll` (`-1` para RNG; senao 0..99) na rota fisica e
na skill de jogador contra jogador/summon. NPC comum nao recebe esse gate. A
resposta negativa preserva HP; zero de dano produzido antes pelo gate PK pode
ser trocado para -3 quando o roll falha. O helper usa RNG
`RandomNumberGenerator.GetInt32(0,100)` no caminho de runtime.

`AttackMissTests` cobre vetores literais da formula, Rsv do atacante,
truncamento/caps, fronteira roll 2/3 para taxa 3%, fisico, skill e exclusao de
NPC comum. Engine.Tests: 324/324. Build completa `WydCdk.Engine.slnx`, SDK
10.0.401: 0 avisos/0 erros. O smoke TCP headless de regressao multi-alvo
`Backup/Build/AutoTradeCloseSmoke/c3aa25247dd349d8a97f95e04821eb3a` passou,
com fixtures originais intactas e porta 18283 liberada. O harness usa atacante
DEX 1001/alvo 1000, taxa 0%; portanto comprova o batch wire, nao um miss TCP.
O resultado -3 foi exercitado deterministicamente no WorldHub. WYD.exe e E2E
visual nao foram iniciados.

Runtime Release self-contained, somente arquivos runtime: ZIP de 197 arquivos,
SHA-256 `6069F1DC60BACDF54D9155E5E3A6A591C35E9A25A7DACAE730E41D40A2536E4B`.
Pacote preservado localmente em `Server/artifacts/publish-attack-miss-20261002/runtime.zip`.
Assemblies locais/remotos: Core
`D0D908D79E7837FFFBA3030A965387893C804838B05CC0A17F0D97D21238BF1C`, World
`65D546A04089D397C58DD77D87C08E75AB8BF680949D81E3CBE1D02984453842`, Protocol
`E8DD33C2AA27D6DBC0FC89E59211D90CB94A295B7C48D1B9E34D41312B6C046B`.
Publicado em `C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`.
Backup remoto `backup-autotrade-close-de8d802890b34dadad54bd12fb0bc76a`, 197
arquivos; Core/World/Protocol anteriores conferidos contra os hashes do runtime
Tower. UP/PVP `Running`, portas 8281/8282 `Listen` no executavel aprovado;
staging removido. Nenhuma source, documentacao ou segredo transferido. Sem
Site/producao, commit ou push.

## COMBAT-ATTACK-TOWER-001 - elegibilidade da torre (01/10/2026)

TMProject 7.69 chama `WarOfTower::AttackTower` em `_MSG_Attack.cpp:438`; a
implementacao em `WarOfTower.cpp:49-56` permite alvos cujo GenerateIndex nao
seja `GTORRE`, e bloqueia a torre quando a guilda do atacante e zero ou igual a
guilda da torre. O bloqueio zera TargetID/Damage e continua o loop, sem dano,
relay de slot nem AddCrackError. W2PP tambem contem o gate: `_MSG_Attack.cpp:398`
chama `CWarTower::TowerAttack`, implementado ativamente em `CWarTower.cpp:311-319`
com a mesma regra de guilda. A constante diverge: `GTORRE=1076` na 7.69 e
`GTORRE=1078` no W2PP. O port segue a referencia ativa da release, 1076.

`LegacyAttackTowerGate` le `MOB.Guild` como UInt16 little-endian nos snapshots
autoritativos do atacante e NPC, sem confiar no cache `Participant.GuildId`.
WorldHub aplica a regra antes de dano fisico, qualquer efeito de skill e do
resultado `TargetLimitExceeded`, inclusive no validador independente de type28.
O resultado bloqueado e nao fatal para o batch fisico, sem outcome; a confirmacao
agregada preserva o slot vazio. Nenhum ponto de crack-error e adicionado.

`AttackTowerGateTests` cobre a matriz 0/propria/inimiga, o indice 1076 contra
1078, a divergencia do cache de guilda, dano fisico, skill, HP NPC inalterado
quando bloqueado e precedencia sobre type28. TDD RED comprovou que o gate e os
resultados ainda nao existiam; GREEN passou com Engine.Tests 323/323 e
`dotnet build WydCdk.Engine.slnx --no-restore`
em SDK 10.0.401, 0 avisos/0 erros. O smoke TCP headless de regressao do batch
multi-alvo passou (`990b88eea0554e71ac3756c4b7e1151c`), fixtures originais
intactas e porta 18283 liberada. O harness ainda nao injeta uma torre NPC, entao
nao houve smoke TCP especifico do gate; essa limitacao permanece registrada.
Runtime self-contained publicado somente no sandbox aprovado: ZIP com 197
arquivos, SHA-256
`CAB8B5E38B7F5DA9D633346D78C257B92CAD7C66D1962BFD0B8975A434B6518D`;
Core `DD6897340A06BB737D37719AC1D2A1BA717FD2C1E96F82CCFDE0922E6C8D7A84`,
World `85BD81F4CA9B9CB30F8549761D8AD865B015777B69A19E6E798B40B1AFF08248`,
Protocol `E8DD33C2AA27D6DBC0FC89E59211D90CB94A295B7C48D1B9E34D41312B6C046B`.
Backup remoto `backup-autotrade-close-41c805543dea44898d08a1160c1dbc4d`
preserva 197 arquivos; UP/PVP Running, 8281/8282 Listen e staging removido.
Somente runtime; sem Site/producao, source remota, commit ou push.

## COMBAT-ATTACK-MAXTARGET-001 - MSG_Attack 10/type28 (01/10/2026)

A auditoria comparou TMProject 7.69 em
Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:578-588
com W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:492-502. No ramo de skill com
Damage==-1, ambas leem MaxTarget e, quando ClientTick != SKIPCHECKTICK e
i > MaxTarget, somam AddCrackError(10,28), registram o evento e fazem continue.
O indice MaxTarget e inclusivo. Em ambas as fontes, a verificacao skill-specific
bParty/type27 aparece depois do type28 (7.69:590-596; W2PP:504-510); portanto
um alvo excedente do mesmo grupo/clan recebe type28 primeiro. O teste C# de
mesmo clan protege essa precedencia. A revisao corrigiu a ordem do port, que
inicialmente deixava o filtro posterior suprimir type28.

Os filtros gerais do alvo sao avaliados antes do ramo da skill. O gate de torre
das duas fontes, incluindo o ponto anterior ao type28, esta portado em
COMBAT-ATTACK-TOWER-001. No port, a regra de elegibilidade verifica os dados
autoritativos de jogador/NPC, morte conforme as excecoes de skill, merchant e
alcance antes de somar type28.

WorldHub aplica o gate antes do efeito em TryApplySkillAttack. Para efeitos de
skill ainda nao portados, TryValidateSkillTargetLimit aplica somente a
elegibilidade, sem executar o efeito. Cada slot excedente elegivel soma
10/type28 ao contador compartilhado da sessao. AttackConfirmation limpa os
slots do cliente, entao o Host restaura no slot correspondente o ID recebido e
Damage=-1 para manter o relay; o alvo excedente nao recebe o efeito. O contador
reutiliza o limite de logout existente.

AttackTargetLimitTests cobre MaxTarget inclusivo, marcadores/tick excluidos,
contador e limite de logout, elegibilidade, falta de efeito em skills nao
portadas, relay do ID/-1 e precedencia same-clan. O RED TCP
0e7ccbf52fb642eba0e97eed75695e71 mostrou slots excedentes zerados no Host
anterior. A regressao same-clan tambem falhou antes da correcao de ordem.
GREEN debug 1885771e01934bd39cb85ee1fc781e85 e self-contained
64098822f50146608af0d46e0d273bce passaram: dois incrementos type28 (10/20),
relay igual para ator/observador com slots 2/3 preservados, cura permitida
638, HP 1000->1638 e blobs originais intactos; porta 18283 liberada.
Engine.Tests 322/322; build completa, SDK 10.0.401, 0 avisos/0 erros.

Runtime-only self-contained publicado no sandbox autorizado em
C:/WYDCDK/Server/test-releases/20260924-v7670/WydCdk.Server-v7670-20260924.
ZIP com 197 arquivos, SHA-256
EFE9A40F3DD872F06FF1909E0F9BD2DC716603765CCA8F566A73F659CC66CC88.
Core 8132E9FB41EB046ADA3B711CFF14ACE9D22BF2F2513F4F223D8DD224D8834221,
World 26500E6F5226DF4560E1FD7BB5D67FBF53B5DD7885B82725E23DADA642A93727,
Protocol E8DD33C2AA27D6DBC0FC89E59211D90CB94A295B7C48D1B9E34D41312B6C046B.
Backup do runtime substituido, backup-autotrade-close-7358317a80fe4e3b89ce4faae2d5bdb3,
conferido 197/197 contra os hashes locais anteriores. Hashes centrais atuais
conferidos; UP/PVP Running e 8281/8282 Listen; staging remoto removido.
Smokes TCP/headless, sem iniciar WYD.exe; E2E visual e outros gates/ramos de
Attack continuam pendentes. Sem source/docs/segredos remotos, Site/producao,
commit ou push.

## COMBAT-ATTACK-MANACONTROL-001 - override fisico Affect.Type 18 (01/10/2026)

TMProject 7.69 `_MSG_Attack.cpp` (regiao `Controle de Mana`, cerca da linha
2038) percorre os affects do alvo e so aplica Type 18 quando
`CurrentScore.Mp > (MaxMp / 100) * 20`. Inicializa `tDamage` com `_pDamage`,
debita esse valor do MP e, se faltar MP, soma o deficit a `tDamage` antes de
zerar o MP. Depois calcula `((tDamage >> 1) + (tDamage << 4)) / abs`, com
`abs=55` para `LearnedSkill & (1 << 23)` ou 60 caso contrario. Esse `tDamage` e
usado tanto no dano aplicado ao HP quanto no `Dam[i].Damage`; com
`DoubleCritical & 4`, o codigo tambem sobrescreve `Dam[1].Damage`. `SendSetHpMp`
chama `SetReqMp`, que limita CurrentScore.Mp a MaxMp, mas nao debita `ReqMp`.

W2PP `_MSG_Attack.cpp` tem ramo comparavel, porem diferente: exige 10% MaxMp,
subtrai `tDamage` de `ReqMp` e usa divisor 50/55. A decisao segue a referencia
primaria 7.69. O C# procura Type 18 no snapshot autoritativo, executa a regra sob
o lock do `WorldHub` depois do split da montaria adulta, atualiza o MP corrente,
preserva `RequestedMana`, aplica o dano reduzido ao HP e sinaliza ao host a
sincronizacao `SetHpMp`. O segundo slot recebe exatamente o dano reduzido quando
o bit 4 estiver ativo.

TDD: os vetores passaram a cobrir ausencia do affect, limiar exato (sem ativacao),
debito normal, divisor 55/60, deficit sem mana suficiente, preservacao do segundo
slot sem bit 4, override com bit 4 e mutacao integrada de MP/HP/ReqMp. RED com o
helper/outcome ainda ausentes em
`Backup/Build/ManaControlSmoke/tdd-red/stdout.log`; Engine.Tests GREEN 312/312.
Build de `WydCdk.Engine.slnx`, SDK 10.0.401: 0 avisos/0 erros. Smoke TCP/headless
em `Backup/Build/AutoTradeCloseSmoke/54c502a7fffd4f79ad470686c20f9732` enviou
25 MSG_AttackTwo, recebeu 25 relays iguais nos dois clientes e 25 sincronizacoes
SetHpMp no alvo; MP 100000->81795, HP 100000->94550, divisor 55 e 9/25 procs
com segundo slot igual ao primeiro. Hashes das fixtures originais intactos,
CleanupCompleted=true e porta 18283 liberada. A primeira tentativa do runner
descartou ataques por iniciar o host sem `--skill-data`; isso foi corrigido no
runner, e a tentativa final passou. Smoke wire/headless nao inicia `WYD.exe`.

Runtime self-contained publicado somente no sandbox em 01/10, no alvo
`C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`.
Backup remoto preservado em
`C:\WYDCDK\Server\test-releases\backup-autotrade-close-4d16d3cea8c14910b9ff9a7b96abb3cc`;
UP/PVP Running, portas 8281/8282 Listen e hashes de Core/World/Protocol iguais
aos artefatos locais. Pasta de staging removida. Fora desta fatia: processamento
integral do loop multi-alvo, demais gates pos-dano e E2E visual. Sem Site/producao,
commit ou push.

## COMBAT-ATTACK-DOUBLECRITICAL-002 - bits base e Progress (01/10/2026)

`Servidor/Source/Code/TMSrv/TMSrv.vcxproj` compila `..\Basedef.cpp`, isto e,
`Servidor/Source/Code/Basedef.cpp`. Esse helper do servidor e o helper do
`Server/W2PP/Source/Code/Basedef.cpp` usam a tabela `g_pHitRate[1024]` e a mesma
regra base. A formula encontrada no `Projects/TMProject/Basedef.cpp` e de
cliente (`4 * (unsigned char)Critical`) e diverge do servidor; nao foi usada
como contrato do TMSrv. W2PP confirma a implementacao servidor, mas nao e a
autoridade da release.

O primeiro bit e `g_pHitRate[Progress] < 100 * ((CurrentScore.AttackRun >> 4) - 5)`.
A tabela segue os quatro quadrantes de `BASE_InitializeHitRate`, com `g[0]`
forcado a 512. O segundo bit compara `rand()%255` (0..254) com
`MOB.Critical`, que e `char` signed no ABI x86 e fica no offset 794 do blob MOB.
Se o contador de sessao diverge do `Progress` do pacote, TMSrv copia o contador
do cliente para a sessao, consulta aquele indice e incrementa ambos. O trecho
posterior de diferenca menor que cinco nao altera o resultado, pois os valores
ja foram igualados. Ha tambem uma guarda para valores >=1024 cuja comparacao
`<0` e impossivel para `unsigned short`; o C# mascara ambos os contadores para
10 bits antes de indexar, e relaya o proximo valor 0..1023.

Com bit 2, o dano-base fisico recebe `(13 + rand()%2) * dam / 10` para jogador
e `(15 + rand()%2) * dam / 10` para NPC, antes de `BASE_GetDamage`. Bit 1 dobra
o dano depois do calculo de base e da eventual soma passiva do bit 4, antes da
penetracao. Portanto bits base 1/2 podem se combinar com bit 4. O C# mantem o
contador no participante, zera-o em `SetCharacterState` de login, calcula os
bits sob o lock do `WorldHub` e escreve `NextProgress` na confirmacao do ataque,
inclusive quando o snapshot atualizado do atacante substitui a confirmacao.

Engine.Tests 311/311 PASS cobrem tabela/indice, normalizacao de 1025 para 1,
avanco para 2, comparacao signed e limites do roll, multiplicadores 1.3/1.4
PvP e 1.5/1.6 NPC, combinacao dos bits 1/2 e dano final. RED anterior ao port:
310/311, com `AttackRun=0xF0` ainda retornando bit 0 e dano 243. O loopback RED
tambem viu `Progress=6` voltar como 6. Build completa de `WydCdk.Engine.slnx`
com SDK 10.0.401 terminou em 0 avisos/0 erros. A smoke TCP GREEN enviou 25
pacotes, observou `6->7` e `1025->2`, recebeu os mesmos relays de 80 bytes no
ator e observador, e observou 6/25 procs passivos. As fixtures originais
permaneceram intactas e a porta 18283 foi liberada. Manifesto/logs da rede em
`Backup/Build/AutoTradeCloseSmoke/9eb6ad1dd00b4cb8ba7dfe2ae1117214`; RED/GREEN,
SDK e logs de build/suite em
`Backup/Build/DoubleCriticalSmoke/COMBAT-ATTACK-DOUBLECRITICAL-002/final-verification`.

Fora desta fatia: processamento integral do loop multi-alvo, demais gates
pos-dano e E2E visual com `WYD.exe`. Sem deploy, Site, commit ou push.

## COMBAT-ATTACK-DOUBLECRITICAL-001 - passivo DoubleCritical fisico classe 3 (01/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/_MSG_Attack.cpp` calcula o ramo
somente no primeiro alvo (`i==0`) quando o pacote tem `MSG_AttackTwo`, o atacante
e classe 3, possui `LearnedSkill & 0x200000` e `rand()%4==0`. O valor inicial e
`BASE_GetDamage(Special[3] + Str, alvo.CurrentScore.Ac, master)`; dano positivo
e dividido por dois, e valor abaixo de 60 e substituido por `rand()%60+10`. A
source limpa o alvo do segundo slot, grava o dano, soma-o ao dano principal e
marca `DoubleCritical |= 4`. Na penetracao posterior contra jogador ou NPC clan
4, o dano final usa `Dam[1].Damage + (dam >> 2)`; outros alvos mantem o dano
combinado. O byte `DoubleCritical` recebido do cliente nao define o resultado.

O port C# aplica essa condicao ao slot fisico principal quando o frame aceita
slot secundario; o resultado traz `DoubleCritical` e `SecondaryDamage`, e o
host escreve flag e segundo slot na confirmacao reconstruida. `TryCreate`
limpa a flag e slots recebidos antes de preencher dados autoritativos. O vetor
deterministico cobre elegibilidade, rolagem 1/4, formula para jogador e clan 4,
ausencia de slot, classe/bit invalidos e piso. Divergencia decidida a favor da
7.69: W2PP `_MSG_Attack.cpp` usa piso fixo 60, enquanto 7.69 usa `rand()%60+10`.

Engine.Tests 311/311 PASS; build de `WydCdk.Engine.slnx` com SDK 10.0.401,
0 avisos/0 erros. Smoke TCP loopback isolado enviou 24 pacotes `MSG_AttackTwo`
com `DoubleCritical=0xFF` e alvo/dano hostis no segundo slot; atacante e
observador receberam os mesmos 24 relays de 80 bytes, flag somente 0/4,
segundo alvo zero e dano secundario presente apenas nos procs. A execucao final
observou 8/24 procs. Evidencia em
`Backup/Build/AutoTradeCloseSmoke/80be119128074c9e8e43230ceafc4903`; hashes das
fixtures originais intactos e porta 18283 liberada. Os logs da build e da suite
final estao em
`Backup/Build/DoubleCriticalSmoke/COMBAT-ATTACK-DOUBLECRITICAL-001/final-verification`.

Fora desta fatia: bits base 1/2 de `BASE_GetDoubleCritical` (portados em
COMBAT-ATTACK-DOUBLECRITICAL-002), processamento integral do loop multi-alvo,
demais gates pos-dano e E2E visual com `WYD.exe`. Override ManaControl concluido
em COMBAT-ATTACK-MANACONTROL-001. Sem deploy, Site, commit ou push.

## COMBAT-ATTACK-PENETRATION-CLAN4-001 - penetracao fisica contra NPC clan 4 (01/10/2026)

TMProject 7.69 `_MSG_Attack.cpp` (regiao `Perfuracao`, linha 1767) e W2PP
`_MSG_Attack.cpp` (linha 1300) aplicam o quarto de dano quando o alvo e jogador
(`idx < MAX_USER`) ou tem `MOB.Clan == 4`. O `TryApplyPhysicalAttack` do C# ja
aplicava o quarto contra jogadores, mas deixava o dano integral contra NPCs clan
4. Agora verifica o byte Clan do snapshot autoritativo do alvo dentro do mesmo
lock de combate e aplica o divisor tambem nesse caso. NPCs de outros clans
continuam sem esse divisor. O ramo legado `DoubleCritical & 4` que combina
`Dam[1] + (dam >> 2)` nao faz parte desta fatia.

TDD: antes da correcao, o vetor headless com atacante, NPC de 1000 HP/clan 4 e
rolagem fixa falhou com dano 94/HP 906; a regra da source para o mesmo dano base
produz `94 >> 2 = 23`/HP 977. Depois da correcao, Engine.Tests 310/310 PASS.
Build da solucao com SDK 10.0.401: 0 avisos/0 erros, executada oculta com logs
redirecionados. O smoke loopback AttackSkillGate tambem passou para o pacote
fisico (`SkillIndex=-1`) e relay, evidencia
`Backup/Build/AutoTradeCloseSmoke/8a0dc7440e744e68924fb087251333ff`; ele nao
atinge um NPC clan 4, que fica coberto pelo teste de WorldHub. Evidencias locais
RED/GREEN/build em
`Backup/Build/AutoTradeCloseSmoke/COMBAT-ATTACK-PENETRATION-CLAN4-001`. Nenhum
deploy, Site, commit ou push.

## COMBAT-SKILL-DAMAGE-001 - dano, resistencia, cura e summon de skill 7.69 (30/09/2026)

O `Basedef.cpp` comum compilado pelo TMSrv 7.69 guarda a formula do W2PP numa
regiao `Not Usage`; o C# ainda usava essa formula. `GetSkillBaseDamage` agora
porta o `BASE_GetSkillDamage(skillnum, mob, weather, weapondamage)` 7.69:
- **Base:** `rc2 = Special[Stage] + ((skill%24)>>3)`, com Level limitado a 0..399.
- **InstanceType 1..5:** 97 -> `Level*15+InstanceValue`. Classe 0 ->
  `IV+rc2+Level>>1+Int/2+(arma+1)/2`. Classes 1/2 -> `IV+rc2+Level>>1+Int/2`.
  Classe 3 -> `IV+rc2+Level>>1+Str>>1+arma*3`.
- **Clima:** keyed pelas skills 2 (90%), 5 (130%) e 3 (120%).
- **Retorno 1..5:** classe 3 e TK com `skill>>3==1` retornam `*5>>2`; os demais
  aplicam `(Magic*2+100)` antes do `*5>>2`.
- **Outros tipos:** InstanceType 6 = `rc2*3+IV`, 11 = IV, e os demais `Magic`.
  Nos casos listados de InstanceType 0 (11/13/41/43/44/45) o `switch` sai com
  `break` e o valor e sobrescrito por `Magic`, reproduzido literalmente. A skill
  79 retorna `CurrentScore.Damage` do atacante.
- **Magic:** e `char` no `STRUCT_MOB` do header comum, entao a formula estreita o
  Magic W2PP para `sbyte`. Decisao registrada; um Magic persistido entre 128 e
  255 vira negativo, como na 7.69.

Em `BASE_GetSkillDamage(dam, ac, combat)` a 7.69 usa `ac >> 1` e a faixa baixa
0..50 (W2PP/C# usavam 0..45). No handler:
- **Defesa:** dobra contra jogador; alvo classe 1 recebe `*3/2`.
- **InstanceType 1:** ResistPoint 10 para jogador, +32 (ClassMaster >= Celestial),
  +16 (Arch) ou +15 acima do nivel 354; NPC usa `LearnedSkill/1000`. O dano fica
  `(100-RP)*dam/100`.
- **InstanceType 2..5 (exceto 97):** `Resist[t-2]` signed, pela metade contra
  jogador, e `dam*(200-resist)/100`. O C# usava `150-resist` com o indice 0 para
  raio.
- **Skill 79:** o dano vira o `CurrentScore.Damage` do alvo; o C# usava `*180/100`,
  `Ac*3` e `/2`.
- **Clima de regiao:** helper `x/128<12 && y/128>25` pronto; hoje o clima e sempre 0.

Cura (InstanceType 6): `Special*3+IV` (27: `*2`); dobra fora de MORTAL/ARCH, com
1090+ -> 1100 nesse ramo e 638 para Mortal/Arch; uma cura de 1..5 vira 6. Antes
eram `*3/2`, teto `min(2200/1100)` e minimo 6 sempre. O caminho de jogador ganhou
o gate de mapa `0x40` que ja existia para NPC. Divisores de montaria inalterados.
Summon (InstanceType 9): removido o check `Hp > Hp+Special+30`, comentado na 7.69.
Entram o setor (13,15) do atacante, o alvo de nivel maior e as zonas Kefra/RvR e
as faixas Zumbi (`x/128==13 || y/128==16`) e Coliseu (`x/128==27 || y/128==11`),
com os `||` literais da fonte e o novo resultado `SummonZoneBlocked`. As mensagens
de summon continuam sem relay, como antes.

Engine.Tests 310 PASS, com vetores novos: formula por classe/tipo, Magic signed,
IT0 sobrescrito, clima, faixa 46..50, `ac>>1`, resistencias, cura, zonas e gates
de summon. Testes que refletiam o W2PP foram recalculados a mao com a 7.69 (TK
313, Foema 590, fixture 195/125 com HP 4875, curas 320 e 638). Build
`--no-incremental` 0/0; self-test do harness passou. Smoke `-AttackSkillHeal`:
Foema Mortal, skill 27, Special 300 -> 700 limitado a 638.
RED (runtime anterior ao bloco, compilado fora da arvore):
`Backup/Build/AutoTradeCloseSmoke/ae46f15c6f444894aaa9dda6486fb524`, relay `(2,-700)`.
GREEN: `10ba01d51da74b84a61e7faa586244ad`, relay `(2,-638)` e SetHpMp HP 1638.
Regressoes: skill gate `3b58366a58d24d36be05c743c494db61`, ganho de mana
`1b2bc48dbeb54f73b4c0e5a9197aa361`, timing `fc552441893d4c2e9e342c922eb4caf4`,
HP0 `fba6e9ea40f9441a993ed6bcf5d0e9b2`, RvR `60e8d2c555464ad8b9cd638d1cb20f7c`,
Action3 mana `a175f66beb74477a99663475c3f8f84e`.
O dano de skill por TCP nao tem smoke deterministico (fator 90..110 e parry
aleatorios); fica coberto por unidade com rolagem fixa. Pendente, fora deste
bloco: restante das etapas pos-dano 7.69, incluindo ataques de skill, PvP, PK e
Miss com `GetAttackMiss` no lugar do `GetParryRate` do W2PP. O divisor fisico
padrao contra NPC clan 4 foi concluido separadamente em
`COMBAT-ATTACK-PENETRATION-CLAN4-001`; o passivo `DoubleCritical & 4` foi
concluido em `COMBAT-ATTACK-DOUBLECRITICAL-001`. Bencao Divina
(201/RateRegen) e EXP de cura foram portadas em recortes posteriores; mensagens
de summon continuam pendentes; a cobranca de mana do Espelho Magico foi
concluida em COMBAT-ATTACK-MAGIC-MIRROR-MANA-001, com item 5508 e bonus
MaxMP fora deste recorte. Escudo Dourado 85 foi portado
em `COMBAT-ATTACK-GOLDEN-SHIELD-001`.
Sem Site, commit ou push.

## COMBAT-SKILL-SPECIAL-001 - indice de Special por estagio de skill (30/09/2026)

`STRUCT_SCORE.Special[4]` fica no offset 40; no C#, `LegacyScore.Special1..4`
correspondem a `Special[0..3]`. Os usos diretos ja seguiam isso (por exemplo,
`Special[2]/20` do master TK usa `Special3`, e o burn `BaseScore.Special[1]` usa
`Special2`). Porem o helper privado `GetSpecial(stage)` devolvia `Special1..3`
para os estagios 1..3, enquanto 7.69 (`Exec_MSG_Attack`, `BASE_GetSkillDamage`)
e W2PP leem `Special[skill%24/8+1]`, ou seja `Special[1..3]`. Era um erro de
indice contra as duas sources, e nao uma divergencia de versao. Ele afetava o
custo de mana, o `special` do dano de skill e o nivel usado em cura, affects e
ticks.

`GetStageSpecial` mapeia os estagios 1/2/3 para `Special2/3/4` e e usado em
`GetSkillBaseDamage`, pois a 7.69 aplica `Special[Stage]` a todas as skills.
`GetSkillSpecial` passou a ser o `Special` local do handler 7.69: `Special[Stage]`
para 0..95, Level para 96..100 e 0 para 101+. `GetManaSpent` reutiliza esse
valor. W2PP usa `Special[kind]` para todos os IDs; a decisao para 96+ segue 7.69,
como em SKILLGATE.

Testes antigos fixavam o indice errado (valor do estagio 2 em `Special2`, do
estagio 1 em `Special1`). As fixtures foram corrigidas para o campo certo,
mantendo os resultados esperados das formulas. Um vetor novo com Special1..4 =
11/22/33/44 cobre os estagios 1/2/3, 96..100, 101+ e o dano da skill 11.
Engine.Tests 310 PASS; build `--no-incremental` SDK 10.0.401 0/0; self-test do
harness passou. O runner `-AttackSkillGate` grava `Special[1]=200` e
`Special[2]=40` na copia: a skill 10 (estagio 2) precisa ler 40.
RED (codigo atual com o indice antigo, compilado fora da arvore):
`Backup/Build/AutoTradeCloseSmoke/fde1b00d8f6b47d0acb88081e27c750c`, MP
5000/4909/4773/4773. GREEN: `bf32cba3c81e441fa5c37aac454180f9`, MP
5000/4946/4810/4810. Regressoes: ganho de mana `5dc53bdb2d554c39ac27cdae549167aa`,
timing `f63c31bdec2f435fa66079bccf733643`, HP0 `35807cb243ab41968f02a97b29b22502`,
RvR `107875d267a4471c8b822ffab618dacd`, Action3 mana `7ee4f60b0886448e810bafab4864f29e`.

Fora deste recorte: o `GetSkillBaseDamage` inteiro segue o W2PP. O
`BASE_GetSkillDamage` 7.69 do `Basedef.cpp` comum compilado pelo TMSrv difere:
usa `TickValue` (nao `AffectValue`) nos casos InstanceType 0, soma `(skill%24)>>3`
ao Special, e tem outras formulas por classe. O handler 7.69 cura com
`Special*2`/`Special*3 + InstanceValue` (o C# usa `*3/2`), e o check
`Hp > Hp+Special+30` esta comentado na 7.69, mas ativo no C#. Esses pontos
pedem recortes proprios com vetores.

## COMBAT-ATTACK-MANA-001 - SaveMana com sinal e bloco de mana de MSG_Attack (30/09/2026)

Os 44 arquivos do TMSrv 7.69 incluem `..\Basedef.h` comum, que declara
`STRUCT_MOB.SaveMana` como `char` com sinal; o `TMSrv/Basedef.h` privado
(`unsigned char`) nao e incluido pelos callers, e o W2PP usa `unsigned char`.
Decisao: 7.69. `LegacyMobCombatState.SaveMana` passou a `sbyte`; `0xFF` vale -1
(custo 101%) e `0x80` vale -128 (228%), e com `SaveMana>100` o custo fica
negativo. As duas sources so testam `Mp - ManaSpent < 0`: custo negativo e aceito
e aumenta o MP. Depois do debito, as duas chamam `SetReqMp`, que limita
`CurrentScore.Mp` a `MaxMp`. O C# rejeitava custo negativo como
`InsufficientMana` e nao aplicava o teto; `WorldHub.TryConsumeSkillMana` agora
segue as duas regras. O sidecar C# `RequestedMana` ainda segue a formula W2PP
(`max(0, ReqMp-custo)`, e depois ao menos o MP ja limitado) para os caminhos que
sincronizam esse estado privadamente. No sucesso de `MSG_Attack`, contudo, a
7.69 sobrescreve `ReqMp` no fim do handler com `CurrentMp`; essa diferenca de
wire foi corrigida em COMBAT-ATTACK-REQMP-001. Action3 (custo fixo da skill 73)
nao chama `SetReqMp` na 7.69 e nao mudou.

Risco residual: o teto usa o `CurrentScore.MaxMp` persistido, porque o
recalculo de score no login ainda nao esta integrado. Um blob com MaxMp
defasado limitaria o MP a esse valor. A fixture unitaria antiga tinha MaxMp 0 e
foi corrigida.

Engine.Tests 310 PASS; `world skill mana mutation` cobre 0xFF, 0x80, ganho com
MP 0, teto no ganho e no debito normal, e rejeicao `Mp-custo<0`. Build
`--no-incremental` SDK 10.0.401 0/0; self-test do harness passou.
`-AttackSkillGate` passou a usar SaveMana 0x80 e confere o MP exato
5000/4955/4819/4819. O novo `-AttackManaGain` (SaveMana 120, MP100, MaxMp107,
skill 10 aprendida) confere MP 104 e 107 relayado ao ator e ao observador, sem
crack nem rejeicao.
RED (codigo do SKILLGATE sem a correcao de mana, compilado fora da arvore):
skill gate `Backup/Build/AutoTradeCloseSmoke/5df3afccb1814ff3a91afcf6c0705431` e
ganho `88d6ee7b880e44adbe0a50df6e7c7d5e`, ambos com `InsufficientMana`. GREEN:
skill gate `37a68ee78f814b65ba35c340377585fb`, ganho `a310436742db4f568c1e99c86e93ddca`.
Regressoes: timing `94474b7f39dc4aa994bf8a5a49443645`, HP0
`ae977f8258b64441a87ac9db7507512f`, RvR `3c9d3358093049ceb02c4ee8bb0dbfad`,
Action3 mana `3d2705d8babd4217ab757b0ebd1bb770`.
Hashes originais intactos, porta liberada. Sem deploy, Site, commit ou push.

## COMBAT-ATTACK-SKILLGATE-001 - bloco de skill de MSG_Attack (30/09/2026)

O cliente 7.69 envia `SkillIndex=-1` no ataque fisico
(`TMFieldScene.cpp:11317`, `TMHuman.cpp:10783/15219`). O C# rejeitava esse valor
como `InvalidSkillId`, logo nenhum ataque fisico do cliente real chegava ao
calculo de dano; smokes antigos usavam a skill 103 como contorno. Na TMProject
7.69, depois do timing, o bloco de skill so roda para `0 <= skill < MAX_SKILLINDEX`:
Passive==1 retorna em silencio; `isTime=(int)(ClientTick-LastAttackTick local)<700`
soma `AddCrackError(10,30)` e retorna; `skill/24<=3 && skill/24!=Class` retorna
em silencio. O bit aprendido e depois conferido: 0..95 usa `skill%24` e
`AddCrackError(8,10)`; 96/98/99/100 usam `skill-72` e `AddCrackError(208,1)`.
Nos dois casos o ataque so para se o contador cruzar o limite de logout. 97
(morteiro) exige item 746 em `pItemGrid[PosX][PosY]`; 101..247 nao tem checagem de
classe nem de bit e usam Special 0 no custo de mana. W2PP difere: todas as skills
0..247 usam o bit `skill%24`, type10 e `Special[skill%24/8+1]`, porque seu ramo
96+ e inalcancavel. Decisao: manter 7.69.

`LegacySkillAttackGate.Evaluate` segue essa ordem. `-1` pula o bloco e a mana
(`WorldHub.TryCapturePhysicalAttackSnapshot` copia MP/ReqMp sem alterar).
`TryAcceptAttackTiming` expoe o LastAttackTick local, zero no primeiro ataque. O
host soma type30 e para; nos crack errors de bit aprendido, so interrompe no
limite, como fazem as duas sources e o precedente Action3 type28. Antes, o C# era
mais estrito e rejeitava skill nao aprendida. `GetManaSpent` usa agora Special 0
para 101+ (antes usava Level). Continuam fail-closed: indices < -1 ou >= 248,
definicao ausente e o morteiro 97 (`MortarNotPorted`), ate portar a consulta de
item no chao. O check `hitposition` e codigo morto nas duas sources
(`tx=m->TargetX`).

O type30 so e alcancavel no primeiro ataque apos o login, com LastAttackTick
local 0 e `ClientTick<700` ou `>=2^31`. Com ataque anterior, o gate de 800 ms o
torna inalcancavel. Como CurrentTime/TickCount acompanham o uptime, entre 24,8 e
49,7 dias de cada ciclo de 49,7 dias o primeiro skill apos o login e descartado
com 10/type30 nas duas sources e no port. Esse ramo tem cobertura unitaria; o
uptime local de 6 dias nao permite reproduzi-lo por TCP.

O SaveMana com sinal e o custo negativo foram resolvidos em COMBAT-ATTACK-MANA-001.
O gate Damage inválido type77 foi concluído em COMBAT-ATTACK-DAMAGE-MARKER-001;
type24 foi concluído em COMBAT-ATTACK-TYPE24-001. Os gates type27/type28 têm
recortes posteriores nesta matriz. Morteiro foi concluído em
COMBAT-ATTACK-SKILL-MORTAR-001.
O custo de mana do Espelho Magico foi concluido em
COMBAT-ATTACK-MAGIC-MIRROR-MANA-001; aprendizado por item 5508 e bonus MaxMP
seguem fora do recorte. A cobranca
de coin do Escudo Dourado foi concluida em `COMBAT-ATTACK-GOLDEN-SHIELD-001`.

Engine.Tests 310 PASS, com vetores novos de ordem, limites 699/700, wrap signed,
-1, bits 24/28 e mana Special. Build `--no-incremental` SDK 10.0.401 0/0;
self-test do harness passou. O smoke `-AttackSkillGate` usa loopback 18283 e
copias isoladas (classe0, LearnedSkill0, MP5000; SaveMana0 nesta etapa, 0x80 desde MANA-001). O servidor rejeitou
30/9/97/-2 sem relay, aceitou -1/10/96/103 com relay identico ao ator e ao
observador, e reduziu MP 5000->4980->4920->4920. Registrou 8/type10 total8 e
208/type1 total216. O smoke de timing passou a usar -2 como skill invalida.
RED (runtime do checkpoint `d15acf39` compilado fora da arvore):
`Backup/Build/AutoTradeCloseSmoke/c1cb88947fb540918d0fbbddb6d48136`, com -1/10/96/103
rejeitados e o cliente falhando na assercao de relay. GREEN:
`Backup/Build/AutoTradeCloseSmoke/a67d5869cb67488cb0ff66271d949277`. Regressoes:
timing `fc62fc334a93478aa1f216f6b3b2a7d5`, HP0 `2d97d00fb6614a66ba13d7e41e024910`,
RvR `6986b6a2b7834d69981205945cf23554`.
Hashes originais intactos, porta liberada. E2E visual pendente; sem deploy, Site,
commit ou push.

## COMBAT-ATTACK-SUMMON-HEAL-AUDIT-001 - bloqueio de cura em Clan 4 (12/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:848-851` e W2PP `:607-611` executam
`continue` na cura (`InstanceType == 6`) quando o alvo tem Clan 4. A propria
source 7.69 atribui Clan 4 ao summon no ramo de summon (`:1278`); o C# tambem
registra `MobClanOffset = 4` em `WorldHub.cs:8016`. Assim, summons criados por
esses caminhos nao podem ser curados por essa skill. O port C# preserva o gate
`TargetClanBlocked`. A tentativa provisoria de implementar cura foi revertida;
nenhuma excecao, codigo ou teste especifico foi mantido. Auditoria somente, sem
build ou testes finais associados.

## COMBAT-ATTACK-SKILL-CLASS1-TYPE6-001 - limpeza de Type 6 no atacante (12/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:116-137` verifica o bloco somente para skill
ativa valida e `ClientTick != SKIPCHECKTICK`; depois do retorno de skill passiva,
classe 1 remove o primeiro affect Type 6 do proprio atacante, zera os oito bytes,
chama `GetCurrentScore` e `SendScore`, e so entao processa atraso, classe e bits
aprendidos. W2PP nao tem esse bloco correspondente. O Type 6 altera Dexterity no
score loop; a integracao usa a projecao parcial ja auditada do C# e nao declara
paridade completa de `GetCurrentScore`.

`LegacySkillAttackGate` sinaliza essa limpeza na mesma fronteira (classe 1,
skill ativa, tick normal), mesmo quando atraso/classe/aprendizado depois rejeitam
a tentativa. O `WorldHub` remove atomicamente somente o primeiro registro de 8
bytes, recompõe score com o `LegacyCurrentScoreLoginProjector` e nao muda estado
se ItemList/projecao estiver indisponivel. O Host envia o UpdateScore existente
ao atacante antes de tratar os rejects posteriores. Passiva, SkillIndex -1,
classe diferente de 1, tick reservado, ausência de Type 6 e falha de projeção
estao cobertos.

RED confirmou o sinal/operacao ausentes; GREEN: teste de gate e de estado/score
passou. Engine.Tests 373/409; 36 falhas de ambiente em permissao de arquivos e
loopback, incluindo SocketException 10013, listener local indisponivel e teste
de persistencia. O novo teste passou. Build
`WydCdk.Engine.slnx --no-restore --no-incremental -m:1`: 0 erros e 1 aviso
CS8604 preexistente em `AttackDivineBlessingTests.cs:256`, SDK 10.0.401.
`dotnet --info` mostrou SDK 10.0.401, mas falhou na consulta ao Service Control
Manager (acesso negado). Sem smoke TCP,
E2E visual, persistencia nova, deploy, commit ou push. Evidencias em
`Backup/Build/AttackClassOneType6-20261012`.

## COMBAT-ATTACK-GOLDEN-SHIELD-001 - custo da skill 85 (02/10/2026)

Auditoria de `_MSG_Attack.cpp` nas duas referencias: TMProject 7.69 atribui
`Level = CurrentScore.Special[getSkillMaster]` em 234-238 e cobra `100 * Level`
para skill 85 em 241-249; W2PP usa `Special[skillkind]` em 217-220 e repete a
cobranca em 223-231. Para skill 85, o indice e `Special[2]`, que o C# representa
como `CurrentScore.Special3`. Em ambas as fontes, Gold insuficiente retorna em
silencio; Gold suficiente e debitado com `SendEtc` antes do gate de mana. Logo,
uma falha posterior de mana nao estorna a moeda.

`LegacyGoldenShieldCoinMath` calcula o custo e `WorldHub.TryConsumeGoldenShieldCoin`
aplica a mutacao autoritativa de `MOB.Coin` sob o lock, sincronizando o sidecar
Coin. O host envia `UpdateEtc` privado imediatamente apos o debito e antes de
`TryConsumeSkillMana`. O teste de dominio cobre `Special[2]`, custo exato 300,
Gold suficiente/insuficiente e ausencia de mutacao/MP no retorno insuficiente.
TDD: teste RED falhou por API/calculo ausentes; depois da implementacao, Engine.Tests
326/326, solution build SDK 10.0.401 com 0 avisos/0 erros e self-test do harness
passaram.

Smoke TCP headless em `127.0.0.1:18283`, usando apenas copias descartaveis: com
stage 3, Gold 600 e MP 121, a primeira chamada enviou UpdateEtc Gold 300 antes do
relay e zerou MP; a segunda cobrou os 300 restantes e enviou UpdateEtc Gold 0
antes de SetHpMp por MP insuficiente, sem relay; a terceira, sem Gold, retornou
sem frames e antes da mana. Depois do logout, Gold 0 ficou persistido no MOB e na
conta, enquanto carry/cargo ficaram intactos. O UpdateEtc nao chegou ao observador;
hashes das fixtures originais intactos e porta liberada. Evidencia:
`Backup/Build/AutoTradeCloseSmoke/057ac1b0c3e645dc850aa4f33a20aeed`.
Smoke Debug e self-contained passaram; a solution foi compilada com SDK 10.0.401
(0 avisos/erros). ZIP runtime-only de 197 arquivos (sem logs, PDB, fontes ou
docs), SHA-256
`18EAEB6D90D1016CACEDF7B8932849E120F45FB987255FDCE3352642AA2F5069`, publicado
no alvo sandbox
`C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924`.
O backup `backup-attack-golden-shield-20261002-18eaeb6d` preserva e verifica
199 arquivos, inclusive os dois logs operacionais. Readback independente
conferiu Core
`186ECB6636F1C69CC873BAA4A915CF46362649862D8569B8BD32B22B644A430D`, World
`0FCD1F7C1E28DC6C7EA7665F31766302C74B57928B869FC83204C0AE13F6349D`, Protocol
`B17A09C55AE7E8891269B615260E5C95665C8FFB1212F62379F45BBCC7DCAF81`, tarefas
UP/PVP `Running`, portas 8281/8282 `Listen`; ZIP, helper e staging temporarios
remotos foram removidos. E2E visual segue pendente. Sem Site/producao, commit ou
push.

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

## MOVE-ACTION-OCCUPIED-DESTINATION-001 — correcao privada de destino ocupado (14/10/2026)

TMProject 7.69 (`Servidor/Source/Code/TMSrv/_MSG_Action.cpp:235,244-271`) grava
`LastMove` antes de testar `pMobGrid[TargetY][TargetX]`. Se outro MOB ocupa o
destino, chama `GetEmptyMobGrid`, depois executa `BASE_GetRoute` para distancias
decrescentes de `strlen(Route)` ate zero. O loop termina em `i == -1`, envia o
Action resultante somente ao autor com `Effect=0` e retorna antes do
`GridMulticast`; a posicao do jogador nao e atualizada. W2PP
(`Server/W2PP/Source/Code/TMSrv/_MSG_Action.cpp:240,249-276`) tem a mesma ordem
e o mesmo retorno. Os corpos de `BASE_GetRoute` em TMProject
(`Basedef.cpp:5812-6100`) e W2PP foram normalizados e comparados: sao identicos.

O `GetEmptyMobGrid` de TMProject (`GetFunc.cpp:2231-2273`) busca raio 1..9, com
Y crescente e depois X crescente, filtrando ocupacao e terreno do tile candidato.
W2PP (`GetFunc.cpp:2027-2127`) busca apenas raios 1..4 e verifica o terreno do
tile original nos ramos de fallback. A implementacao segue a busca TMProject
7.69. A ocupacao C# vem de jogadores, summons e NPCs; participantes retirados
da grade da Battle Royale sao excluidos. Sem map grid, usa altura plana zero e
terreno livre, em linha com o modo sem mapa do WorldHub.

`LegacyActionRouteBuilder` reproduz a ordem e os limites de passos de
`BASE_GetRoute` (24 bytes, ate 23 direcoes, diferenca de altura estritamente
menor que 8). O Host chama a correcao depois dos gates anteriores de Action e
antes de `TryApplyMovement`; grava `LastMoveType`, preserva o `ClientTick` recebido
e responde somente ao socket do autor. As coordenadas corrigidas nao sao
gravadas na sessao nem no WorldHub. O teste fixa o caso legado em que a busca acha uma celula
livre, mas o recorte de rota termina no tile intermediario ocupado, e verifica
`Effect=0`, opcode/rota, posicao sem mutacao e `LastMoveType` para o proximo Stop.

Engine.Tests: 458/458. Build `WydCdk.Engine.slnx` com SDK 10.0.401: 0 avisos e
0 erros. `dotnet --info` identificou SDK 10.0.401, mas saiu 1 por acesso negado
ao Service Control Manager. Sem smoke TCP/headless, E2E visual, deploy, commit
ou push; nenhum campo/opcode wire mudou.

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
O C# porta apenas a formula-alvo; a etapa integra o coordenador/login projector. Task 5DI liga o produtor online da skill 3 ao score do jogador.
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
RegenHP e dano fisico nao sao alterados nesses branches. A formula Type 14 do
C# integra o coordenador/login projector e o produtor online da skill 3 esta
concluido em Task 5DI; Type 24 segue como etapa pura sem produtor online.
O recorte [`character-login-frozen-blade.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-frozen-blade.manifest.md)
fecha o `Affect.Type=7` (Lamina Congelada) em sete vetores: alvo e W2PP coincidem em subtrair
`Level / 10 + 10` de AttackSpeed e, quando `headItemIndex > 50`, `Level / 10 + 20` de Int.
O recorte [`character-login-weaken.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-weaken.manifest.md)
fecha o `Affect.Type=10` (Enfraquecer) em sete vetores: alvo e W2PP coincidem na subtracao
`Level / 5 + Value` de Damage.
O recorte [`character-login-meditation.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-meditation.manifest.md)
fecha o `Affect.Type=21` (Meditacao) em sete vetores: a reducao sequencial de AC `Level / 3 + 10`
coincide entre as sources, mas o incremento no multiplicador de dano diverge (`Level / 25 + Value`
no alvo contra `Level / 10 + Value` no W2PP). COMBAT-LOGIN-AFFECT-TYPE21-AC-001 integrou
a reducao de AC na primeira passagem ordenada do coordenador/login. COMBAT-LOGIN-AFFECT-TYPE21-DAMAGEMULTI-001 expõe o estágio no coordenador e soma o multiplicador 7.69 ao valor final de login.
O vetor intercalado mantém AC 271, registra DAMAGEMULTI +12 e termina em Damage 192; o teste do coordenador também verifica as duas partes do estágio. Engine.Tests 405/405; build completa do
`WydCdk.Engine.slnx` com SDK 10.0.401: 0 erros e 1 aviso CS8604 preexistente. Evidencias em
`Backup/Build/CurrentScoreType21DamageMultiplier-20261009`; sem persistencia ou mudanca de wire.
Na primeira rodada GREEN, o teste revelou `regenMp` ausente no golden intercalado; o campo
esperado 0 foi adicionado antes do rerun final. O RED inicial reproduziu a diferença esperada
em `CurrentScore.Damage` antes da integração.
O recorte [`character-login-golden-shield.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-golden-shield.manifest.md)
fecha o `Affect.Type=31` (Escudo Dourado) em sete vetores: o alvo soma `Level / 2 + Value` ao AC,
o W2PP tambem soma `Level / 2 + Value`, mas processa os branches em um loop unico por slot, divergindo da ordem em duas passagens 7.69.
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
O helper comum 7.69 e usado nos caminhos TMSrv auditados e percorre 18 itens;
W2PP preserva o MOB canonico de 16. C# aceita o estado cliente de 18 itens,
mas a movimentacao do slot 16 exige extensao persistida; o fluxo sem sidecar
continua limitado a 16. Os vetores desta secao nao auditam esse ciclo de vida.

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

Sondagem de cobertura (05/11/2026): um scan textual de declaracoes de tipos em
`Server/src` encontrou 816 entradas; a primeira coluna da matriz atual lista 179
simbolos e 643 entradas nao tiveram correspondencia nominal direta (573 em
`WydCdk.World`, 35 em `WydCdk.Protocol`, 35 em `WydCdk.Server`). Como a matriz
agrupa tipos e o scan nao resolve aliases, tipos aninhados ou responsabilidade,
esses numeros sao candidatos de auditoria, nao um total de classes faltantes ou
um criterio de aceite. Usar a lista de candidatos para fechar a cobertura por
modulo, mantendo a verificacao semantica nas tres arvores.

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
| `Equip` / `Carry` | Equip comeca em +140 nos dois layouts, mas W2PP tem 16 itens e o client 18; copiar 0..15 e manter 16/17 em extensao versionada. Carry e 64 itens em ambos, deslocado de +268 para +284. | `CharacterMobV769`/`ClientEquipmentStateV769` cobrem os 18 itens; o `WorldHub` projeta appearance de 18 e mantem a extensao separada. Movimento no slot 16 exige estado sidecar carregado; sem extensao persistida, o limite permanece W2PP 16 + dois vazios. Slot 17 e sempre rejeitado para movimento. MariaDB usa `wyd_client_equipment_769` (migration 004); `LegacyFileAccountStore` usa `.wyd-client-equipment-v1`, magic `WYDEQ769`, versao 1. |
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
| `LegacyCharacterTemplateStore` | `DBSrv/Server.cpp::ReadBaseMob`, `ConfigIni.cpp::ReadbaseMob`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter`; cliente `TMHuman.cpp` | `DBSrv/Server.cpp` le `BaseMob/TK|FM|BM|HT`; `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter` | 7.69 carrega `Config/baseMob.json`, cujos templates atuais so preenchem indices 0..15; o cliente desenha 16/17 como `NewSlot1/2`. Os caminhos TMSrv auditados usam `Basedef.h` comum de 18 slots; o header privado 16/128 nao entra nesses includes. Cliente/DBSrv `STRUCT_MOB` = 1040, Equip +140, Carry +284; W2PP = 816, Carry +268. C# le templates W2PP e inicializa as extensoes vazias; slot 16 so e movivel com sidecar persistido. | fixture atual sintetica; defaults do slot 16, elegibilidade EF_POS, sidecar delete/recreate e E2E pendentes |
| `LegacyCharacterStorageDefaults` | `Basedef.cpp::BASE_ClearMob/BASE_ClearMobExtra`, `TMSrv/ProcessDBMessage.cpp` inicializacao de Kill Mark | mesmos metodos/etapa de login | manter defaults persistidos do slot vazio em 2112,2112 e `ClassMaster=MORTAL`, alem de Kill Mark inicial ausente; a rotina agrega trechos de pontos diferentes do legado. | auditoria estatica concluida; fixtures de selecao/delete testadas |
| `LegacyAccountSnapshot` | `DBSrv/CFileDB.cpp::DBReadAccount/DBGetSelChar`, `Basedef.h::STRUCT_ACCOUNTFILE` | mesmos simbolos em `DBSrv/CFileDB.cpp` e `Basedef.h` | **Sao formatos de storage diferentes, nao uma extensao simples de slots.** W2PP: `sizeof=7952`, Char +216, Cargo +3480, Coin +4504, `IsBlocked` +7944; o parser C# aceita minimo de 7945 bytes e le MOB816/16 equips/Cargo128. Header comum DBSrv 7.69: `sizeof=8792`, Char +216, Cargo +4376, Coin +5336, Donate +8640, extensao opaca de 8 bytes antes de TempKey +8652, `IsBlocked` +8784; MOB1040/18 equips/Cargo120. Nao adotar o blob cru 7.69 como modelo canonico nem deslocar campos por aritmetica; manter `W2ppAccountFileV1` isolado para compatibilidade/importacao e preferir storage C# versionado. | ABI `sizeof/offsetof` medida; offsets W2PP/7.69 diferem; adapter/importacao e golden sintetico de storage pendentes |
| `IAccountStore` / `ICharacterStore` / `IAccountSnapshotStore` / `LegacyFileAccountStore` / `MariaDbWorldCharacterStore` | `STRUCT_ACCOUNTFILE`, `DBSrv/CFileDB.cpp` e `DBGetSelChar` | `STRUCT_ACCOUNTFILE`, `DBSrv/CFileDB.cpp` e `DBGetSelChar` | contratos/adapters nao correspondem a classes C++ individuais. A persistencia C# atual usa layout W2PP versionado implicitamente (blob MariaDB de 7945 bytes; parser minimo 7945, embora `sizeof` nativo seja 7952), nao o arquivo bruto 7.69 de 8792 bytes. Isolar o formato atual como `W2ppAccountFileV1Adapter`; um importador 7.69 so sera criado se houver requisito de migracao e teste. Persistencia nova deve mapear entidade canonica via adapter, sem expor offsets no dominio. | adapters atuais auditados; versao do contrato precisa ficar explicita; golden de storage e concorrencia deterministica pendentes |
| `Cargo` / owner account-scoped proposto | TMProject TMSrv privado `Basedef.h::STRUCT_ACCOUNTFILE.Cargo` / `Server.cpp::SaveUser`; header comum `DBSrv/CFileDB.cpp::_MSG_DBSaveMob` | W2PP `Basedef.h::STRUCT_ACCOUNTFILE.Cargo`, `Server.cpp::SaveUser`, `DBSrv/CFileDB.cpp::_MSG_DBSaveMob` | Cargo e estado da conta, nao do MOB: W2PP e o header privado TMSrv 7.69 usam `STRUCT_ITEM[128]` no offset 3480. O header comum/DBSrv 7.69 declara outro ABI (`Cargo[120]`, offset 4376), portanto o `SaveUser` TMSrv e o consumidor DB nao podem ser presumidos compativeis sem confirmar a unidade/ABI compilada; a source 7.69 tem divergencia entre processos. C# `LegacyAccountSnapshot` projeta 128 itens do blob W2PP e `LegacyNpcSellAuthoritativeLookup` apenas os lê; `WorldHub.Participant` só possui estado do personagem/conexão e não possui Cargo mutável. Contrato de 05/11: owner runtime único por identidade canônica da conta e mundo, compartilhado pelas sessões; Cargo completo + MOB/Coin do vendedor compõem o CAS, com conflito explícito e patch somente nos campos proprietários. O modelo é independente de `LegacyItem`/offsets; preservar os campos alheios. | proveniência e contrato focado confirmados; inventário geral BL-000, implementação dos adapters e integração MariaDB de escrita/rollback/concorrência pendentes |
| `AccountAuthenticationStatus` / `AccountAuthenticationResult` / `DeleteCharacterStoreResult` / `CharacterPositionSaveResult` / `CharacterStateSaveResult` / `CharacterGuildSaveResult` / `CharacterShortSkillSaveResult` / `DonateBalanceSaveResult` | retornos booleanos/estados de `DBSrv/CFileDB.cpp` e handlers | mesmos retornos/handlers | tipos de resultado C# para coordenacao e adapters, sem DTO C++ homonimo; preservar a traducao para as confirmacoes/falhas wire, nao os numeros internos do adapter. | contratos locais mapeados; matriz de falhas por operacao pendente |
| `MariaDbConnectionOptions` / `MariaDbConnectionFactory` | sem equivalente: conexao MariaDB configurada pelo novo host | sem equivalente: conexao MariaDB configurada pelo novo host | manter fora do dominio legado; configura provider a partir de JSON, cria conexoes parametrizadas e nao deve expor credenciais em log/docs. O provider C# `MySqlConnector` 2.6.2 executou `SELECT 1` pela VPS em túnel SSH restrito a localhost em 05/11/2026; túnel permanece nesta sessão em `127.0.0.1:13306`, sem abrir firewall ou alterar banco. O JSON em 3306 continua inalterado, portanto a conexão de probe usa override de porta. | conectividade confirmada; CRUD, transações, rollback, schema e concorrência dos stores permanecem sem validação remota |
| `IDonateBalanceStore` / `IDonatePixStore` / `IDonateCatalogStore` / `MariaDbDonateShopStore` | saldo/cadastro nas rotinas DB 7.69, import de catalogo comentado | saldo/import DB ativo, sem adapter C# equivalente | persistencia de saldo so continua se integrar a conta Cash do Site; catalogo/painel de compra in-game fica estacionado. Definir fonte de verdade, transacao e entrega no bau antes de reativar qualquer adapter comercial. | adapter analisado; schema ativo/atomicidade e contrato Site pendentes |
| `ServerWireLog` / `TeeTextWriter` | sem equivalente: logging de host novo | sem equivalente: logging de host novo | manter como observabilidade, nunca como parte do protocolo. Divergencia de seguranca: o redator atual nao inclui `DeleteCharacterRequest` (`0x0211`, contem `AccountPassword`) nem `AccountLoginConfirmation` (`0x010A`, inclui chaves e `BlockPassword`); ambos podem ter payload gravado em claro. Corrigir a classificacao antes de manter logs em ambiente com contas reais. | auditoria estatica; redacao incompleta confirmada; sem teste de segredo nos logs |
| `ServerStatusFilePublisher` | sem equivalente: health/status do projeto, fora do protocolo do servidor 7.69 | sem equivalente: health/status do projeto, fora do protocolo W2PP | manter o adapter separado: atualiza `servtest.htm` na inicializacao/cadencia de 60 s, preserva `0` jogadores e marca `-1` no shutdown cooperativo; timestamp fica em `.wyd-status`. A expiracao apos 120 s pertence ao watchdog PowerShell, nao ao processo C#. O watchdog agora consulta e grava `values[slot]`, mantendo slots independentes. | implementacao e teste local executados; script/live site ainda nao sao E2E |
| `ServerOptions` | sem equivalente: argumentos/configuracao do host C# | sem equivalente: argumentos/configuracao do host C# | manter como configuracao operacional, sem misturar com politica de wire/dominio. Validacao de paridade dos argumentos com deployment continua em checklist proprio. | parser auditado; cobertura integral de combinacoes pendente |
| `Program` / `MainForm` (`WydCdk.Server.ControlPanel`) | sem equivalente: UI WinForms nova | sem equivalente: UI WinForms nova | manter fora do core e tratar como host local. O core aceita `stop`/`shutdown` pela entrada padrao e executa o `finally`/status offline; o painel solicita essa parada e usa `Process.Kill` somente apos timeout de cinco segundos. | encerramento cooperativo implementado; build/UI e fluxo real do painel pendentes |
| `ClientPatcher` (`PatchDefinition`, `NativeHookPayload`, `X86Emitter`, `RelFixup`, `AbsoluteFixup`, `PeSection`, `PeLayout`) | sem equivalente no servidor 7.69; utilitario nativo historico do checkout C# | sem equivalente no W2PP servidor | estacionar como ferramenta de laboratorio/historico. O projeto esta listado na solution, mas nao e referencia do core; nao distribuir nem executar contra o cliente-alvo 7.670, cuja estrategia aprovada e source compilada sem DLL/hooks. | escopo auditado; exclusao do pacote de release precisa de gate automatizado/documentado |
| `SetShortSkillRequest` | `Basedef.h::MSG_SetShortSkill`, `TMSrv/_MSG_SetShortSkill.cpp` | mesmos simbolos/handler | manter os 20 bytes de payload: `SkillBar[4]`, `ShortSkills[16]`; ambos os legados apenas copiam os vetores ao estado do personagem. O C# persiste de imediato depois de validar `USER_PLAY`, decisao de durabilidade que nao altera o wire. | auditado; parser e persistencia testados |
| `PkInfoConfirmation` | `Basedef.h::_MSG_PKInfo` como `MSG_STANDARDPARM`, `TMSrv/_MSG_PKMode.cpp` | mesmos simbolos/handler | manter frame de 16 bytes com `state` int32 e ID do alvo, multicast na grade. O calculo comum cobre culpa/PK/castelo/torre/NewbieEvent; a extensao RvR existe no W2PP, nao na 7.69. Action-origin usa esse estado para sincronizar um jogador que entra na visao dos dois lados; os demais fluxos PK seguem suas fichas proprias. | wire e relay Action-origin testados por TCP; RvR permanece fora do alvo |
| `ActionRequest` | `Basedef.h::MSG_Action`, `TMSrv/_MSG_Action.cpp` | mesmos simbolos | manter layout de 52 bytes e opcodes `Action`/`Action2`/`Action3`. O DTO preserva `WireType`; HP==0 tem precedencia sobre score de velocidade ausente e envia HpMode20/Mode22 +5/type3; estado WorldHub ausente ou HP/AttackRun ausente para personagem vivo rejeita. Speed permitido e `CurrentScore.AttackRun & 0xF`; excesso soma `5/type4` no contador da sessao e corrige somente Speed antes do gate de range; depois de somar, revalida USER_PLAY para nao continuar apos logout por threshold. O side effect 7.69 de `Speed>=20` ou `AttackRun>=300` marca Analysis e fecha antes do relay; auditoria do log `SaveInfoDetect` esta em MOVE-ACTION-ANALYSIS-005. O gate de origem MOVE-ACTION-RECALL-001 precede velocidade/alcance: setores (9,1)/(8,2)/(10,2) para classMaster 3/4/5 com level>=180, ou setor (13,15) quando `MOB.Clan==0`. W2PP comenta o gate Celestial e nao tem o gate sem clan; 7.69 e a autoridade. `TryRecallForActionOriginWithViews` captura a visao de origem/destino na mesma mutacao protegida por lock; `LegacyActionOriginRecallRelay` envia Action apenas a visiveis, remove jogadores que deixam a janela e envia CreateMob/PKInfo a ambos os lados na entrada. O fluxo nao usa `BroadcastAsync`. Destino fora da janela inclusiva `VIEWGRID=33` e rejeitado pela posicao autoritativa; somente >66 em qualquer eixo envia Action3/Speed6 privado. Movimento aceito relaya ao autor e a jogadores na janela 33x33. `LegacyMovementMapRules`/`WorldHub` aplicam `0x80`/`0x20`, `BASE_GetGuild` para cinco cidades e `DoRecall`; indices46/47 de `Language.txt` precedem o recall por mapa. | Engine.Tests 299 PASS/0 FAIL, incluindo 11 vetores de origem e teste da captura espacial; build 0/0. TCP quatro contas confirmou Action/RemoveMob/CreateMob/PKInfo, negou relay distante e verificou posicoes no logout; `-ActionRange` regressou. Os fluxos de recall/teleporte implementados foram auditados em MOVE-ACTION-TELEPORT-POSITION-AUDIT-001; E2E visual permanece pendente |
| `MotionRequest` / `SetHpModeConfirmation` | `Basedef.h::MSG_Motion`, `Basedef.h::MSG_SetHpMode`, `TMSrv/_MSG_Emotion.cpp`, `SendFunc.cpp::SendHpMode` | `TMSrv/_MSG_Motion.cpp`, demais simbolos iguais | manter Motion20/relay; HP==0 ou fora USER_PLAY soma4/type6 por sessao; HpMode20/Mode22 privado somente playing; HP negativo relaya | MOVE-CRACK-001:263 PASS/build0/0; rede HP0/-1, Accepted/Selection, shared-counter e carry/cargo/Gold/relogin; sandbox runtime-only publicado; visual/threshold TCP pendentes |
| `LoginSessionRegistry` | `pUser[conn].Mode`, `pMob[conn].TargetX/TargetY`, `LastActionTick` nos handlers TMSrv | mesmos campos/handlers | adaptador arquitetural C#, sem classe C++ equivalente. Manter transicoes de login; separar futuramente politica de movimento e timing de combate, hoje acopladas indevidamente ao registro de sessao. | auditado; transicoes/movimento/timing testados; extracao bloqueada ate caracterizacao |
| `AttackRequest` / `AttackConfirmation` | `Basedef.h::MSG_Attack/MSG_AttackOne/MSG_AttackTwo`, `TMSrv/_MSG_Attack.cpp` | mesmos structs e handler | manter os tres wires: 168/68/80 bytes, offsets de posicao/alvo/atacante/skill e slots `STRUCT_DAM`. O cliente so declara marcador de dano (`-2` fisico, `-1` skill); HP, EXP, MP, ReqMp, cena e dano sao reescritos pelo servidor. C# consulta a listing ativa (equivalente a `TradeMode`) antes de USER_PLAY e envia MessagePanel 170 privado `0x0101`/140 com cabecalho zero e texto Latin-1 de `Language.txt`; fora desse gate, exige USER_PLAY, aplica o RvR 7.69 a Clan zero no setor (13,15), depois rejeita HP exatamente0 salvo skill99 antes do timing/mana/dano, enviando SetHpMode privado 20/22 com cabecalho zero e AddCrackError(1,8). HP negativo nao aciona o gate de morte. TooSoon (< LastAttackTick+800) e tick fora da janela 120s/15s somam 1/type107 em 7.69 e W2PP; depois do gate de 800 ms, `ClientTick < (uint)LastAttackTick-100` soma 4/type7 sem retorno, antes da atualizacao do tick e da janela. C# reporta o type7, preserva a aritmetica unsigned e compartilha contador/limite; wrap pode produzir type7 seguido de type107. A 7.69 tambem bane e grava SaveInfoDetect nos ramos type107; W2PP nao, diferenca pendente de decisao, sem aplicar Analise fora de Action. RvR nao existe em W2PP e foi mantido pela referencia 7.69. Depois do timing, o bloco de skill 7.69 aceita SkillIndex -1 como fisico sem skill/mana; passiva/outra classe sao silenciosas; delay <700 ms soma 10/type30; bits aprendidos somam 8/type10 ou 208/type1 e so param no limite; 101+ nao e checado e usa Special 0 (W2PP diverge em 96+); morteiro 97 e indices invalidos ficam fail-closed. | COMBAT-ATTACK-DEAD-001 + COMBAT-ATTACK-TRADEMODE-001 + COMBAT-ATTACK-RVR-001 + COMBAT-ATTACK-TIMING-001 + COMBAT-ATTACK-SKILLGATE-001 (Engine.Tests 310 PASS; GREEN `Backup/Build/AutoTradeCloseSmoke/a67d5869cb67488cb0ff66271d949277`): Engine.Tests 309 PASS, solution build SDK 10.0.401 com 0 erros/1 aviso CS8321 em `ExperienceBonus` local sem uso, smokes wire/headless; timing confirmou TooSoon, duas rejeicoes OutsideServerWindow/type107 e 4/type7 total6 apos wrap, sem relay/HP/MP. Evidencias RED `Backup/Build/AutoTradeCloseSmoke/43a5a971c89b4a779ae06860223638b9`, GREEN `Backup/Build/AutoTradeCloseSmoke/2c94658ad5b643f59052b752171f9a83`; regressao Attack HP0/RvR aprovada. Outros gates de mapa/PvP/efeitos/anti-cheat pendentes; E2E visual pendente |
| `LegacyAttackActionBatch` / Host `MSG_Attack` misto | TMProject 7.69 `TMSrv/_MSG_Attack.cpp` (loop, ramo fisico, relay e mortes); `CMob.cpp` (range 23) | W2PP `_MSG_Attack.cpp`, `CMob.cpp` | `Damage==-2` fisico e `-1` skill sao classificados independentemente em frames com 1/2/13 slots e executados em ordem sob um lock, preservando indices e alvos repetidos; uma confirmacao usa recursos finais. SkillIndex invalido preserva `-1` no relay e ainda processa `-2`; efeitos nao portados ficam desabilitados. O contrato misto e detalhado em COMBAT-ATTACK-MIXED-SLOTS-001. `DoubleCritical/Progress` sao compartilhados e so calculados no slot 0; passivo classe3/bit `0x200000` reserva slot1. Mortes finalizam depois da acao normal; OOR preserva mutacoes, interrompe e suprime relay/finalizacao. Ambas as fontes concordam quanto a ordem, estado de acao, passivo e mortes. Divergencias mantidas: 7.69 sorteia piso secundario 10..69, W2PP fixa 60; C++ mede range por coordenadas do pacote, C# mantem `>23` por posicoes autoritativas. | Primeira validacao Engine.Tests 320/320 (runtime depois corrigido em review); build SDK 10.0.401 0 avisos/erros; TCP/headless RED `e3572fac1d82473189e079d7ba966df0`, GREEN debug `93da451534ec4f3499b2bb028ff163c0` e self-contained `2e6a52a615684949afb4f34f0403c9f4`, blobs intactos/porta liberada. Primeiro ZIP publicado no sandbox (superado pelo runtime corrigido abaixo): 197 arquivos, SHA-256 `972C57AAF311D9B491EC3FABCB0D39190203DA44AAC69E3FE2AC476E25BDCBD5`; backup de 199 arquivos `backup-combat-attack-multitarget-20261001-93da4515`; UP/PVP Running e 8281/8282 Listen. Headless; E2E visual, gates comuns e outros ramos Attack pendentes; Review posterior corrigiu tres findings Important: dano secundario/ManaControl e wire slot compartilhados, HP de montaria summon em hit nao letal e selecao skill/fisico. Engine.Tests 321 PASS; solution build SDK 10.0.401 com 0 avisos/erros. Smokes debug: multi-alvo `f4566aa86d92430287662ddfb45d09de`, skill mista `0442192caad249b39df8596d2555a180`, ManaControl `9979f7bba97046268c8a43304f35ef9b`; self-contained: `72619534043c4fcb8277baf9c27ae400`, `49d6e484af894cbc987dca346b9a5e74`, `bc93db88392f4ddc8f86ed35d5aa5c74`. Runtime final 197 arquivos, SHA-256 `5B3260C1321C1F4D185488273D3F5660E023FBA1FDA7F255ECDAC2A8A87CAFAD`; Core `710364C557AA420AC91C2120F9316144B5FBD3566B5C5C686B3FCAAA6B4D3DBB`, World `6633926DB0402C6E7EB450943917571F9217A93BD567DA62B2C2C0B9EE421912`, Protocol `E8DD33C2AA27D6DBC0FC89E59211D90CB94A295B7C48D1B9E34D41312B6C046B`. Backup corrigido de 199 arquivos `backup-combat-attack-multitarget-reviewfix-20261001-5b3260c1`; remote hashes verificados, UP/PVP Running e 8281/8282 Listen, staging limpo. E2E visual pendente. |
| `NpcAttackConfirmation` | `Basedef.h::MSG_AttackOne`, `TMSrv/GetFunc.cpp::GetAttack` | mesmos simbolos/funcao | reutilizar o `MSG_AttackOne` de 68 bytes, `ID=ESCENE_FIELD`, atacante/posicoes/alvo autoritativos e dano no primeiro `STRUCT_DAM`. `GetAttack` define `SkillParm=0`, `SkillIndex=-1` e motion aleatorio 4..6; o C# ainda usa valores simplificados para summon, que precisam ser cotejados antes de afirmar equivalencia visual. | wire auditado; ataque autonomo C# testado; sem equivalencia integral de semantica/cliente |
| `SetHpDamConfirmation` | `Basedef.h::_MSG_SetHpDam` (`0x018A`), emissor `TMSrv/_MSG_Attack.cpp` | mesmo struct/frame de 20 bytes; o ramo Type 204 de Maos Sangrentas nao esta no `_MSG_Attack.cpp` W2PP | Header de 12 bytes + `Hp` int32 + `Dam` int32 nos offsets de payload 0/4. No Type 204, 7.69 usa ID do atacante curado, HP autoritativo e dano assinado e faz `GridMulticast` na view; C# preserva o frame para a acao secundaria/view capturada. A coincidencia do struct nao implica equivalencia do ramo W2PP. | `EnvironmentalDamageAndEffectFramesMatch769Wire` confere tamanho/opcode/ID/HP/dano assinado; fichas Type 204 e efeitos temporizados cobrem os usos/relays; sem golden byte-vector isolado ou smoke de rede Type 204; E2E visual pendente |
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
| `LegacyMobAbilityMath` / `LegacyCurrentScoreMath` / `LegacyEquipmentRules` / `LegacyExperienceMath` | cliente `Projects/TMProject/Basedef.cpp::BASE_GetMobAbility/BASE_CanEquip`; servidor `Servidor/Source/Code/Basedef.cpp`; TMSrv `CMob.cpp::GetCurrentScore` e `SendFunc.cpp::SendEquip` | `Basedef.cpp` ability/clear loops, `TMSrv/CMob.cpp::GetCurrentScore` | O TMSrv ativo inclui o header comum 18: `TMSrv.vcxproj` compila `..\Basedef.cpp`, e os caminhos `ProcessClientMessage.h`, `Server.h` e `CMob.h` incluem `..\Basedef.h`; o `TMSrv/Basedef.h` adjacente 16/128 nao entra nesses caminhos. `_MSG_Move_Item.cpp` permite destino 0..16 e origem 1..16 por `MAX_EQUIP-1`; `BASE_CanEquip` rejeita colocacao na posicao 15 e aplica `EF_POS` nas outras. W2PP com MAX_EQUIP=16 permite destino 0..14 e origem 1..14. C# espelha a faixa alvo para estado 18-slot e preserva o blob canonico W2PP 16-slot; os loops de ability/score recebem explicitamente 16 ou 18 conforme o estado. Paridade integral do score permanece pendente. | comparacao de faixa C#↔7.69/W2PP e teste RED/GREEN executados; slot 16 preservado, 17 bloqueado; ciclo do sidecar MariaDB e extensao file store cobertos; score integral e E2E cliente pendentes |
| `GetItemRequest` / `GetItemConfirmation` / `DecayItemConfirmation` | `Basedef.h::MSG_GetItem/MSG_CNFGetItem/MSG_DecayItem`, `TMSrv/_MSG_GetItem.cpp` | mesmos simbolos/handler | manter wire e pickup autoritativo; o destino e somente carry e a validade/alcance vem do item de chao, nao do cliente. | auditado; wire/pickup/decay testados |
| `DropItemRequest` / `DropItemConfirmation` / `CreateItemConfirmation` | `Basedef.h::MSG_DropItem/MSG_CNFDropItem/MSG_CreateItem`, `TMSrv/_MSG_DropItem.cpp` | mesmos simbolos/handler | manter wire e escolha autoritativa da celula; a origem e validada no servidor. Cargo permanece fora do recorte atual. | auditado; wire/drop/chao testados |
| `DeleteItemRequest` | `Basedef.h::MSG_DeleteItem`, `TMSrv/_MSG_Clear_Item.cpp` | `Basedef.h::MSG_DeleteItem`, `TMSrv/_MSG_DeleteItem.cpp` | manter wire de 20 bytes e limpeza autoritativa do carry; a confirmacao visual ocorre por `SendItem`, nao por resposta dedicada. | auditado; wire/delete carry testados |
| `SplitItemRequest` | `Basedef.h::MSG_SplitItem`, `TMSrv/_MSG_SplitItem.cpp` | mesmos simbolos/handler | manter wire de 24 bytes, faixa 1..119 e insercao no primeiro carry livre. A elegibilidade vem de catalogo configuravel: 7.69 usa `groupItens`, W2PP traz lista fixa; nao transplantar lista sem evidencia da release 7.670. | auditado; wire/split testados |
| `UseItemRequest` | `Basedef.h::MSG_UseItem`, `TMSrv/_MSG_Use_Item.cpp` | `Basedef.h::MSG_UseItem`, handler equivalente | manter wire de 36 bytes e autoridade de origem/destino/grade no servidor. O voucher Vol 184 que soma `EF_DONATE` e top-up por item nas duas fontes e comportamento distinto de comprar em loja; confirmar se entra no modelo Cash do Site antes de manter. Compra de Cash por NPC/painel esta excluida. | auditado; wire/familias portadas testados; decisao do voucher pendente |
| `SendItemConfirmation` | `Basedef.h::MSG_SendItem`, `TMSrv/SendFunc.cpp::SendItem` | mesmos simbolos | manter 24 bytes e atualizacao de slot por `invType`, `Slot`, `STRUCT_ITEM`; e a resposta comum a mutacoes de inventario, nao confirmacao exclusiva de uma rota. | auditado; wire testado |
| `UpdateCarryConfirmation` | cliente 7.69 `Projects/TMProject/Basedef.h::MSG_Carry`/`TMHuman::OnPacketCarry`, servidor `Servidor/Source/Code/TMSrv/SendFunc.cpp::SendCarry` | `MSG_UpdateCarry` / `MSG_Carry` (`0x0185`) | manter frame de 528 bytes: 64 `STRUCT_ITEM` de 8 bytes no payload e `Coin` no offset 512; e o refresh completo usado pela conclusao de trade, nao uma sequencia de `MSG_SendItem`. | DTO/encoder, fixture de contrato byte a byte e relay para os dois participantes testados; E2E pendente |
| `EquipmentAppearanceV769` / `W2ppEquipmentAppearanceV1Adapter` | cliente `Projects/TMProject/Basedef.h::MSG_UpdateEquip`, opcode `0x036B`, `TMHuman`; TMSrv ativo `SendFunc.cpp::SendEquip` | W2PP `MSG_UpdateEquip` (`Equip[MAX_EQUIP]`, `AnctCode[MAX_EQUIP]`) e `SendEquip` | O contrato cliente/TMSrv comum 7.69 mede 68 bytes: `sEquip` +12 e `Equip2` +48 (18 slots); `TMSrv/Basedef.h` privado de 16 slots esta fora dos includes auditados. W2PP mede 60 bytes: vetores de 16 e `AnctCode` +44. O C# possui DTO isolado de 18 slots e adapter W2PP 16->18 ou usa a lista versionada quando presente. | probe Win32, golden/adapter e relay de appearance de 18 campos testados; movimento no slot 16 somente com sidecar carregado; movimento no slot 17 rejeitado; E2E cliente e ciclo persistente completo pendentes |
| `UpdateItemRequest` / `UpdateItemConfirmation` | `Basedef.h::MSG_UpdateItem`, `TMSrv/_MSG_UpdateItem.cpp`, timers/portoes | mesmos simbolos, `CCastleZakum` e timers | manter wire de 20 bytes e mutacao autoritativa do estado de item de mapa; mascaras, chave, altura, gate/timer e multicast pertencem ao dominio de mundo. | auditado; wire, mascara, timer e portoes testados |
| `LegacyItemDataTable` / `LegacyServerItemDataTable` / `LegacyItemDefinition` / `LegacyServerItemDefinition` / `LegacyItemStaticEffect` / `LegacyItemEffect` | loader server 7.69 em `Servidor/Source/Code/Basedef.cpp`, tipos/constantes em `Basedef.h`; layout client em `Projects/TMProject` | loaders/tipos equivalentes em `Source/Code/Basedef.cpp/.h` | os leitores C# mantem separados o layout client/converter de 164 bytes e o runtime server de 140 bytes. A habilidade C# continua subconjunto no leitor cliente, nao `BASE_GetItemAbility` completo; o leitor server-side ainda e somente formato. | parser client e server 7.69/W2PP testados; fonte autoritativa do arquivo, consumidores server-side e ramos restantes de habilidade pendentes |
| `LegacySkillDataTable` / `LegacySkillDefinition` | `Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill`, `Basedef.h::STRUCT_SPELL` | mesmos simbolos; `MAX_SKILLINDEX=248` | manter `/4` em `AffectTime` e a semantica da segunda coluna de `Act`; C# rejeita linha malformada em faixa ao inves de aceitar parcial via `sscanf`. | comparacao estatica concluida; fixture/parser testados |
| `LegacySummonCatalog` / `LegacySummonTemplate` / `LegacySummonBonus` | `TMSrv/CNPCGene.cpp::CNPCSummon::Initialize`, `Server.cpp::GenerateSummon`, `Basedef.cpp::pSummonBonus` | mesmos simbolos/funcoes | C# carrega 43/50 slots por padrao, incluindo `Cav._Arcano`, `Arq._Arcano` e `Mag._Arcano` nos slots 40..42. A leitura historica de 7.59 continua disponivel somente com limite explicito de 40 slots; slots 43..49 permanecem indisponiveis. | catalogo 7.69/W2PP carregado e nomes/slots testados; E2E de combate/relay especifico dos slots Arcano pendente |
| `LegacyNpcGenerationCatalog` / `LegacyNpcGenerationDefinition` / `LegacyGeneratedNpc` / `LegacyNpcGenerationBuilder` | `TMSrv/CNPCGene.cpp::CNPCGenerator::ParseString`, `Server.cpp::GenerateMob`, `ProcessSecMinTimer.cpp` | mesmos parser e algoritmo de cinco segmentos; W2PP tambem copia waits, grupos e formacao | parser, rota, grupos/followers, relocacao, agenda, capacidade por origem, `GetEmptyItemGrid`, relay e loot `DUNGEONEVENT` estao implementados; adaptacao `g_pFormation` fora da dimensao requer revisao. Ramos especiais ainda faltam. | Engine.Tests `490/490`, build sem incremental (0 erros, CS8604 preexistente), `--mob-ai-smoke` e `--dungeon-event-smoke` TCP loopback; cinco CreateItem, view 33x33 | C2 |
| `ShopSellMessage` | cliente `Projects/TMProject/Basedef.h::MSG_Sell` (`0x037A`), servidor `TMSrv/Basedef.h::MSG_Sell` / `_MSG_Sell.cpp` | `Basedef.h::MSG_Sell` / `TMSrv/_MSG_Sell.cpp` | TargetID ushort e MyType/MyPos short; o struct Win32 mede 20 bytes com 2 bytes finais de alinhamento. O parser C# exige checksum, opcode e tamanho exato e preserva esse campo. | Host dispatcher ativo; wire tests cobrem tamanho, signedness, checksum e truncamento; venda de Cargo usa owner de conta e CAS; integração MariaDB pendente |
| `ShopBuyMessage` | cliente `Projects/TMProject/Basedef.h::MSG_Buy` (`0x0379`), servidor `TMSrv/Basedef.h::MSG_Buy` / `_MSG_Buy.cpp` | `Basedef.h::MSG_Buy` / `TMSrv/_MSG_Buy.cpp` | TargetID, duas posicoes short e Coin int ocupam 24 bytes; os 2 bytes de alinhamento antes de Coin estao representados explicitamente. A classe C# e parser/encoder sem dispatch Host. | wire tests cobrem offsets e tamanho; manter estacionada ate a auditoria e o aceite de compra NPC/estoque |
| `TradingItemRequest` / `TradingItemConfirmation` | cliente 7.69 `Projects/TMProject/Basedef.h::MSG_SwapItem` (`0x0376`) e servidor `TMSrv/_MSG_Move_Item.cpp` | `Basedef.h::MSG_TradingItem` / `TMSrv/_MSG_TradingItem.cpp` | Ambos frames medem 20 bytes, mas os 8 bytes de payload divergem: 7.69 usa ordem Source/Destination, `TargetID` ushort e 2 bytes de alinhamento; W2PP usa Destination/Source e `WarpID` int. C# preserva o wire 7.69 e padding; Host ativo suporta carry/equipamento, enquanto Cargo permanece estacionado. | wire e regras de troca carry/equipamento cobertas; Cargo e E2E visual pendentes |
| `ItemSoldConfirmation` | `Basedef.h::_MSG_ItemSold`, `MSG_STANDARDPARM2`; emissor `TMSrv/_MSG_ReqBuy.cpp` | mesmos simbolos e emissor | 20 bytes: header, ID `ESCENE_FIELD` (30000), `Parm1=SellerId`, `Parm2=Position`; adapter C# expoe os dois parametros como int32 e o relay AutoTrade emite a notificacao. | wire e relay cobertos por testes; validacao visual da linha AutoTrade pendente |
| `AutoTradeStartRequest` / `AutoTradeListConfirmation` | cliente `Projects/TMProject/Basedef.h::MSG_AutoTrade`; servidor `TMSrv/Basedef.h::MSG_SendAutoTrade`, `_MSG_SendAutoTrade.cpp`, `SendFunc.cpp::SendAutoTrade` | `Basedef.h::MSG_SendAutoTrade`, `_MSG_SendAutoTrade.cpp`, `SendFunc.cpp::SendAutoTrade` | Um wire de 196 bytes (`Desc/Title[24]`, 12 `STRUCT_ITEM`, 12 carry positions, 12 prices, Tax e Target/Index). O server zera os 2 ultimos bytes do titulo e substitui Tax por CityTax; C# tambem limita titulo a 22 bytes ASCII e calcula tax pela cidade, ignorando Tax/TargetID fornecidos na requisicao. C# separa DTO de request e resposta; o word final server `Index` e lido pelo cliente como `TargetID`, mesmos bits. W2PP tem gate `NewbieEventServer` ausente na source-alvo 7.69; manter o comportamento C# alinhado a 7.69. | wire/start/list relay e casos de validacao cobertos pelos testes AutoTrade; a reservation live e o CAS de Cargo continuam pendentes |
| `AutoTradePurchaseRequest` | cliente `Projects/TMProject/Basedef.h::MSG_ReqBuy`; servidor `TMSrv/Basedef.h::MSG_ReqBuy`, `_MSG_ReqBuy.cpp` | `Basedef.h::MSG_ReqBuy`, `TMSrv/_MSG_ReqBuy.cpp` | O wire mede 36 bytes: Pos int, TargetID ushort, 2 bytes de alinhamento x86, Price int, Tax int e STRUCT_ITEM de 8 bytes. O DTO C# preserva explicitamente o padding e o Host o despacha para o coordenador AutoTrade; a regra do seller Cargo/banco e buyer MOB/Coin esta no crosswalk SHOP-005 abaixo. | offsets, padding, roundtrip, coordenador, commit de arquivo e relay cobertos pelos testes AutoTrade; MariaDB de escrita e concorrencia pendente |
| `RepurchaseItemsMessage` / `RepurchaseItemEntry` | cliente 7.69 `Projects/TMProject/Basedef.h::MSG_RepurchaseItems` (`0x03E8`), `SGrid.cpp`, `TMFieldScene.cpp::OnPacketUndoSellItem`; server `TMSrv/Basedef.h::MSG_Recycle`, `PacketHandler.cpp` | W2PP mantém `MSG_Recycle` e `SendRecycle`, sem receive `0x03E8` localizado | O DTO C# implementa o contrato client-facing de 176 bytes (header 12 + `target` 4 + dez entradas de 16); a ABI interna server `MSG_Recycle` tem 184 bytes e offsets incompatíveis, então o retorno usa projeção explícita dos dez itens de sessão. O Host valida `USER_PLAY`/header ID e dispatcha a listagem; buyback continua sem implementação. | wire e projection/session handler cobertos nos testes registrados em SHOP-005; sem compatibilidade por reinterpretar o struct interno; compra de volta estacionada |

**Proximo lote da auditoria BL-000:** uma varredura sintatica das declaracoes
diretas em `WydCdk.Protocol` encontrou nove tipos sem mencao exata na matriz;
eles estao classificados na fatia abaixo. A fatia World registra o lease de
operacao AutoTrade e sua fronteira, mas nao fecha os writers concorrentes nem
o inventario de classes. Isso nao fecha as 643 candidatas nominais do
inventario global: a contagem continua heuristica e nao foi recalculada.
Proximo: auditar os demais writers de estado que podem cruzar o checkpoint,
alem do subconjunto direto de Gold em `WorldHub`; fechar o contrato atomico
do par de guild invite antes de aplicar a ordem da reservation; e continuar o
crosswalk dos ramos `_MSG_Attack` pendentes. A sondagem de nomes nao substitui
a leitura de codigo nas tres arvores. A validacao visual do cliente 7.670 segue
pendente.

### BL-000 — fatia de tipos Protocol sem mencao exata na matriz (05/11/2026)

O inventario foi limitado aos tipos diretamente declarados nos arquivos C# de
`WydCdk.Protocol` que nao apareciam pelo nome na matriz anterior. A decisao
abaixo classifica cada declaracao, sem promover serializers de teste a um fluxo
ativo e sem fechar o inventario global.

| Classe C# | Proveniencia e responsabilidade | Decisao para este recorte | Evidencia existente |
|---|---|---|---|
| `CharacterSelectionSlotV769` / `CharacterSelectionConfirmationV769Base` | `Projects/TMProject/Basedef.h::STRUCT_SELCHAR`, `MSG_CNFNewCharacter`, `MSG_CNFDeleteCharacter`; W2PP `Basedef.h::STRUCT_SELCHAR` | Manter como tipos auxiliares da projecao cliente 7.69. O slot e um registro wire; a base aplica o alinhamento x86 de 4 bytes antes de `STRUCT_SELCHAR` nas confirmacoes New/Delete. Nao sao um novo contrato de storage. | Golden Win32 de `STRUCT_SELCHAR` 904 bytes e testes existentes das confirmacoes de 920 bytes; E2E do cliente pendente. |
| `ClientAffectV769` | Cliente `Projects/TMProject/Basedef.h::STRUCT_AFFECT` / `MSG_UpdateAffect`; W2PP `Basedef.h::STRUCT_AFFECT` / `MSG_SendAffect` | Manter como entrada wire especifica do cliente 7.69. Cada entrada tem 8 bytes e ordem `Type, Level, Value, Time`; o adapter para W2PP precisa trocar `Level` e `Value`, e rejeita valores que nao cabem no contrato cliente. | Golden Win32 e testes byte a byte do adapter `W2ppUpdateAffectV1Adapter`; relay usa o snapshot autoritativo. |
| `CloseShopSignal` | TMProject e W2PP `TMSrv/_MSG_Sell.cpp` e `_MSG_Buy.cpp`, via `SendClientSignal(ESCENE_FIELD, _MSG_CloseShop)` | Manter o sinal vazio `0x0196`, ID `30000`, frame de 12 bytes. O Host C# usa-o para venda NPC quando o alvo sai da visao; `ShopBuyMessage` continua sem dispatch. | `LegacyNpcSellRejectionNoticeTests` confere checksum, tamanho, ID e payload vazio; smoke visual pendente. |
| `GuildDisableConfirmation` | TMProject e W2PP `TMSrv/_MSG_MessageChat.cpp`, comandos `guildon`/`guildoff`, via `SendClientSignalParm` | Manter `0x03A4`, `ESCENE_FIELD`, `Parm` int32 0/1 (frame de 16 bytes), depois da atualizacao de score. O tipo C# e somente o encoder do sinal; a regra de visibilidade permanece no estado de sessao. | `GuildDisableTests.GuildDisableSignalMatchesRetailWire`; testes existentes cobrem toggle, score/affect e efeito no combate. |
| `GuildWarRequest` | TMProject e W2PP `TMSrv/Basedef.h::_MSG_War` / `_MSG_War.cpp`, com relay por `DBSrv/CFileDB.cpp` e retorno para TMSrv | Manter o wire `MSG_STANDARDPARM2` de 20 bytes (`0x0E0E`, dois int32 positivos ate 65535). Os dois handlers validam que o MOB ativo pertence a `Parm1` e tem `GuildLevel == 9`, depois encaminham os dois parametros ao DBSrv. O C# faz essa autorizacao no `WorldHub` e aplica a transicao no estado local em vez de reproduzir o hop entre processos; cancelamento interno com alvo 0 fica separado, pois o request de cliente rejeita zero. | `GuildWarProtocolTests` cobre frame, checksum e limites; `GuildWarWorldTests` cobre autorizacao, declaracao, aceite e cancelamento. Integracao do ciclo de evento/cliente segue pendente. |
| `LegacyMobAttackConfirmation` | TMProject e W2PP `TMSrv/ProcessSecMinTimer.cpp`/`Server.cpp` usam `MSG_AttackOne` e `GetAttack`; cliente `Projects/TMProject/Basedef.h::MSG_AttackOne` | Manter como emissor ativo do `LegacyMobAiRelay` para outcomes positivos, sem miss, dos ramos de IA suportados. Emite 68 bytes no wire de servidor; os campos do header cliente e do servidor diferem em nomes/ordem, portanto tamanho igual nao basta para alegar equivalencia visual de cada offset. Nao generalizar para todos os ramos de combate/NPC. | `LegacyMobAttackConfirmationTests` fixa os campos do serializer; `LegacyMobAiRuntimeTests` e `MobAiNetwork` cobrem relay, dano e escopo de visao no caminho ativo. E2E visual no cliente 7.670 continua pendente. |
| `LegacyMobDeathConfirmation` | TMProject e W2PP `TMSrv/MobKilled.cpp` montam `MSG_CNFMobKill` (`0x0338`); cliente `Projects/TMProject/Basedef.h::MSG_CNFMobKill` | Manter como sinal ativo para morte nao revertida de jogador causada pelos ramos de IA suportados. O wire tem 32 bytes; `FakeExp` cliente e `Hold` servidor ocupam o primeiro int, seguido por IDs ushort e `Exp` alinhado. C# usa o mesmo contrato em um caso mais estreito que o handler geral `MobKilled`. | `LegacyMobAiRelay` envia o sinal apos o frame de ataque quando `TargetDied && !TargetRevived`; `MobAiNetwork` confere IDs, tamanho, checksum e visao capturada. E2E visual no cliente 7.670 continua pendente. |
| `MobCountConfirmation` | TMProject TMSrv `_MSG_MobCount` (`187`, `MSG_STANDARDPARM2`); W2PP declara o sinal, mas o caminho de sala encontrado usa `Party.MobCount` e `_MSG_MobLeft` | Manter a confirmacao C# de dois int32 como comportamento 7.69 especifico para Pista LV6 (`MobsLeft` e meta 100); nao inferir equivalencia pelo nome/constante W2PP, nem substituir o wire independente de `_MSG_MobLeft`. | Testes existentes verificam os dois parametros no sinal inicial e periodico; cobertura do catalogo de emissores e eventos W2PP continua pendente. |

Esta fatia foi documental e nao alterou codigo. Os dois serializers de Mob AI
tem callsites ativos no Host; a matriz global BL-000 continua aberta e a
validacao visual dos frames no cliente ainda falta.

### Politica de versao da release

`7.670` e o identificador da release-alvo e corresponde ao inteiro obrigatorio
`7670` em `MSG_AccountLogin.ClientVersion`. A constante deve ser emitida pela
build do cliente; o servidor apenas a valida. As fontes registram valores antigos
distintos (`APP_VERSION` 1001/1059 na arvore 7.69 e 7640 no W2PP), que devem ser
substituidos na migracao do cliente e rejeitados pelo listener 7.670. Adaptadores
de opcode/layout continuam possiveis, mas nao dispensam a versao exigida.

#### Auditoria da configuracao e do executavel (25/10/2026)

O servidor le `ClientVersion` como int32 little-endian no offset 80 do payload e
`Program.cs` aplica a politica antes de autenticar. Em 25/10, as duas rotas
TMProject e o launcher ainda continham literais/configuracoes separadas; esse
registro historico foi atualizado abaixo em 29/10.

O executavel presente em `Client/7670/WYD.exe` mede 1.667.584 bytes e tem SHA-256
`DA1BDF2322946C96E173AD08848E4C03234C348E31AFC8D5E6DE09BE21DAF6D6`; o manifesto
canonico do launcher espera `18364BBD894D367B745FAF4F41D3AB0773C4F68624D4BAC189453678B7F30EAD`.
Esse hash e diferente e a proveniencia deste executavel segue sem confirmacao.
Nao atualizar o manifesto para aceitar esse binario antes de validar a build e
o valor que ele realmente envia.

A analise REA/Ghidra anterior da referencia arquivada, SHA-256
`C248788036749D1728B2F2ABC5F7CE6934977350E24738021AFEC35AE6DBBE3F`, nao se
aplica ao executavel ativo. Nessa referencia, `search_bytes` nao encontrou
`F6 1D 00 00` na memoria inicializada (Evidence
`ev_8af15e1f7cc0c97c1e8aadf1e7bd575d890b93df7efc0bfe5d24b7848eee84c4`). Para
o ativo, Ghidra 12.1.4 analisou o PE x86 identificado pelo hash acima e buscou
27.550.224 bytes em memoria inicializada, sem blocos ignorados. Encontrou duas
ocorrencias de `F6 1D 00 00` na secao `.text`: `0x4b9572` em
`FUN_004b9450` (offset de arquivo 756082) e `0x518457` em `FUN_00518080` (offset
1144919), Evidence `ev_e0be9f4f3e4269a729b6b118e1bb31bc3ff5e4a172cb7d80d51d1951483b2d72`.
A funcao `FUN_004b9450` foi descompilada (Evidence
`ev_e37547edea4e2df42aefd9222be9a340729adce5774f0d0b2cd4e66fe41fdee5`), mas
ainda nao foi estabelecido que qualquer ocorrencia inicializa
`MSG_AccountLogin.ClientVersion`; a busca literal prova presenca do valor no
codigo, nao o campo nem a execucao do caminho de login. A sessao REA/MCP ativa
continua sem `GHIDRA_INSTALL_DIR`, embora as configuracoes globais dos tres
clientes estejam corretas; a analise do ativo foi feita pelo CLI com o ambiente
ja configurado. Fechar a auditoria exige rastrear uma das ocorrencias ate o
payload no offset 80 e confirmar o valor transmitido em build isolada.

#### Fonte compartilhada e prova estatico-wire (29/10/2026)

`Client/Release/ClientVersion.props` agora define o unico valor de build
`WydClientVersion=7670`. `ClientVersion.targets` importa a propriedade e adiciona
`ClientReleaseVersion` aos assemblies do Protocol e do launcher por metadado de
assembly. `ClientReleasePolicy.RequiredClientVersion` e o launcher leem esse
valor; `interface-manifest.json` usa `clientVersion: "required"`, mantendo tambem
aceitacao de manifestos numericos legados somente quando iguais ao valor de build.

`Client/Release/Build-IsolatedClient.ps1` copia `Dependencies` e
`Projects/TMProject` para `Backup/Build`; somente a copia recebe o valor de
`ClientVersion.props` nas duas atribuicoes `stAccountLogin.Version`, com `OutDir`
e `IntDir` fora da copia-fonte. Para esta build, o HEAD e o estado foram
verificados manualmente: commit `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`, com
alteracoes preexistentes em `Basedef.cpp`, `TMFieldScene.cpp`, `TMProject.vcxproj`
e `TMSelectServerScene.cpp`; nenhuma delas foi editada por esta etapa. O script
agora automatiza a verificacao do commit e grava o estado local nas proximas
copias de build.

O build isolado Release|Win32, toolset v145, concluiu com 2044 funcoes, zero
erros e aviso C5298 em `Dependencies/Directx/Include/dxfile.h`. O EXE gerado em
`Backup/Build/Client7670-SharedVersion-20261029-204216/Artifacts/WYD.exe` tem
SHA-256 `47d8a5a753b70a412967de6a5a5279c4f8cfdba933aaae574bdc27d35bc14255`.
Ghidra 12.1.4 encontrou exatamente duas ocorrencias de `F6 1D 00 00` na `.text`
(`0x4b94b2`/offset 755890 e `0x5183a7`/offset 1144743; Evidence
`ev_2b67c7740c64a36d294490adb9eb6f5183694282b931864e6368e468a859f3cf`). As
funcoes em `0x4b9390` e `0x517fd0` zeram blocos de 116 bytes, definem `Header.Type`
como `0x020D`, `Version` como `0x1DF6` e `Force` como 1 (Evidence
`ev_8cc3da6124325d0701fd295e43dc65ecba61b1dd5420e209ee322b162d11dbf5`,
`ev_126ba26e99aed4c3706ea683b2e243fc4b3702fdbbdfa3583ca0ac4de48c9f`,
`ev_6f105851cf906b3043ac6735dfef298e4d20455195d3168af1898dfd2722f4de`,
`ev_482abfae6035d632148c2107e0447214ff21e42fc63a43b5b1edc491e88f9f84`). Pela
layout de `MSG_AccountLogin` em `Projects/TMProject/Basedef.h`, o `MSG_STANDARD`
tem 12 bytes, seguido de `AccountPass[12]`, `AccountName[16]` e `TID[52]`; assim
`Version` inicia no byte 92 da estrutura, offset 80 do payload. A busca exaustiva
na `.text` e as duas atribuicoes no campo confirmam as duas rotas do login.

Essa prova e estatica e vale para o hash listado. O executavel nao foi escaneado
nem executado. `Client/7670/WYD.exe` e seu hash no manifesto canonico continuam
sem alteracao; scan Defender, smoke e E2E visual do artefato gerado ainda faltam.
Build server terminou com 0 avisos/erros, Engine.Tests com 541/541, launcher
net10.0 com 0 avisos/erros. A validacao `--validate-only` aceitou o marcador
`required` em manifesto sintetico. `dotnet --info` identificou SDK 10.0.401 mas
saiu com codigo 1 porque o processo nao abriu o Service Control Manager.

## Dados estaticos e formatos de referencia

| ID | Referencia 7.69 | C# | Contrato e diferenca W2PP | Teste/evidencia | Nivel |
|---|---|---|---|---|---|
| ITEMDATA-001 | `Servidor/Source/Code/Basedef.h::STRUCT_ITEMLIST/BASE_ReadItemList`; `Projects/TMProject/Basedef.h` e `Tools/ItemListConverter/WYDConverter.h` | `LegacyItemDataTable` + `LegacyServerItemDataTable` | o contrato cliente/converter le 6500 registros de 164 bytes (XOR `0x5A`, trailer opcional), com `nPos` int no offset 136. A source do servidor 7.69 e o W2PP definem 6500 registros de 140 bytes, com `nPos` short em 134, `Extra` em 136 e `Grade` em 138; ambos leem trailer de checksum e decodificam o corpo com o mesmo XOR. | os dois leitores C# possuem fixtures e rejeicao de trailer parcial; o probe Win32 confirmou 164 no cliente e 140 no DBSrv/TMSrv 7.69 e W2PP; arquivo autoritativo e consumidores server-side ainda precisam de decisao | C2 |
| ITEMDATA-002 | `STRUCT_ITEMLIST` e efeitos `EF_*` em `Servidor/Source/Code/Basedef.h`; habilidade `BASE_GetItemAbility/Sanc/Gem` em `Basedef.cpp` | `LegacyItemDefinition` / `LegacyServerItemDefinition` / `LegacyItemStaticEffect` / `LegacyItemEffect` / metodos de `LegacyItemDataTable` | os DTOs C# mantem os formatos separados, sem converter 32-bit `nPos` do cliente em `short` por presuncao. `GetItemAbility` continua um subconjunto no leitor cliente: faltam ramos presentes nas duas sources como restricoes por `nUnique`, substituicao `EF_CRITICAL2`/`EF_ACADD2` e bonuses de montaria; o leitor server-side ainda nao foi ligado a habilidade autoritativa. | parser/offsets dos dois contratos possuem fixtures; comparacao do runtime server-file, escolha da fonte e ramos restantes pendentes | C2 |
| SKILLDATA-001 | `Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill`, `Basedef.h::STRUCT_SPELL` | `LegacySkillDataTable` / `LegacySkillDefinition` | ambas as sources usam `MAX_SKILLINDEX=248`, convertem `AffectTime / 4` e passam o mesmo buffer `skilldata` para as duas colunas de acao; a segunda coluna prevalece no `Act[0..5]`, e `Act[6..7]` fica zero. O C# preserva as duas colunas para auditoria e expoe `Action2` como efetiva; parsing de linha valida e deliberadamente mais estrito que `sscanf`. | parser/tempo/acoes cotejados; teste de fixture existente; dados reais completos nao estao no checkout de referencia |
| SUMMONDATA-001 | `TMSrv/CNPCGene.cpp::CNPCSummon::Initialize`, `Server.cpp::GenerateSummon`, `Basedef.cpp::pSummonBonus` | `LegacySummonCatalog` / `LegacySummonTemplate` / `LegacySummonBonus` | C# carrega 43 templates em slots 0..42 por padrao; 7.69 e W2PP inicializam os mesmos 43, incluindo os tres templates Arcano. Os 50 slots continuam sendo o limite do wire/tabela; bonuses nao zero estao somente nos primeiros oito em ambas as fontes. | discrepancia de tres templates resolvida no leitor/catalogo; E2E de summon e cobertura de bonus especifica ainda pendentes |
| NPCDATA-001 | `TMSrv/CNPCGene.cpp::CNPCGenerator::ParseString/ReadNPCGenerator`, `Server.cpp::GenerateMob`, `ProcessSecMinTimer.cpp`, `GetFunc.cpp::GetEmptyItemGrid` | `LegacyNpcGenerationCatalog` / `LegacyNpcGenerationDefinition` / `LegacyGeneratedNpc` / scheduler de NPC | parser, rota, grupos/followers, aquisicao passiva propagada do leader aos followers vivos dentro de +/-23, agenda, relocacao, capacidade por origem, relay view-scoped, busca de tile 7.69 e `DUNGEONEVENT` implementados. `Formation=4` e caracterizada para fallback C# sem alegar equivalencia ao acesso C++ fora da dimensao. Ramos especiais e tails de timer faltam. | Engine.Tests `490/490`, build sem incremental (0 erros, CS8604 preexistente), `--mob-ai-smoke` e `--dungeon-event-smoke` TCP loopback; E2E visual pendente | C2 |

## Rotas de entrada atualmente ligadas

| ID | Referencia legada | Entrada C# / opcode | Handler ou metodo de dominio | Estado/persistencia | Saida principal | Teste/evidencia | Nivel |
|---|---|---|---|---|---|---|---|
| AUTH-001 | `_MSG_AccountLogin` / `CFileDB` | `AccountLoginRequest` `0x020D` | `AccountLoginCoordinator.HandleAsync` | sessao; arquivo/MariaDB | `AccountLoginConfirmationV769` `0x010A` ou `MessagePanel` | parser, coordinator, adapter/golden 1928, frame projetado no listener, failure notice | C4 |
| AUTH-002 | `_MSG_AccountSecure` | `AccountSecureRequest` `0x0FDE` | `AccountSecureCoordinator.HandleAsync` | PIN/security store | `AccountSecureSignal` | parser, signal e coordinator | C3 |
| CHAR-001 | `_MSG_CreateCharacter` | `CreateCharacterRequest` `0x020F` | `CreateCharacterCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_SELCHAR`; snapshot, nome, template | `NewCharacterConfirmationV769` `0x0110`, layout alvo Win32 920 bytes, `SelChar` +16 | adapter de seleção, contrato/golden 920, template, create fixture e sucesso integrado ao listener; falha/E2E pendentes | C4 |
| CHAR-002 | `_MSG_DeleteCharacter` | `DeleteCharacterRequest` `0x0211` | `DeleteCharacterCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_SELCHAR`; slot limpo/defaults | `DeleteCharacterConfirmationV769` `0x0112`, layout alvo Win32 920 bytes, `SelChar` +16 | adapter de seleção, contrato/golden 920, fixture de refresh e sucesso integrado ao listener; falha/E2E pendentes | C4 |
| CHAR-003 | `_MSG_CharacterLogin` | `CharacterLoginRequest` `0x0213` | `CharacterLoginCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_PLAY`; rejeicao retorna a `USER_SELCHAR`, MOB/MOBEXTRA | `CharacterLoginConfirmationV769` `0x0114`, `CreateMobV769`, `UpdateEtcV769`, `UpdateScoreV769` | parser, transicao e composer W2PP -> 7.69 testados; cadeia integrada no listener com frames 1728/236/48/152; relays de CreateMob/UpdateEtc/UpdateScore cobrem consumiveis, combate, summons/NPCs, guild refresh e ressurreicao; UpdateAffect e UpdateEquip cobrem login/mutacoes; estado cliente 18-slot vem opcionalmente de `wyd_client_equipment_769` em MariaDB ou do sidecar versionado de `LegacyFileAccountStore`; delete/recreate limpa o estado de extensao, por transacao MariaDB ou tombstones no file store; demais recalculos TMSrv, narrowing incompativel e E2E pendentes | C4 |
| CHAR-004 | `_MSG_CharacterLogout` | `CharacterLogoutRequest` `0x0215` | `LoginSessionRegistry` + save | SPX/SPY e estado | `CharacterLogoutConfirmation` `0x0116` | logout wire e transicoes | C4 |
| MOVE-001 | `_MSG_Action` | `ActionRequest` `0x036C` e variantes | `TryApplyMovement`, summons | sessao/posicao | relay de action/create mob | parser, movement, world broadcast | C3 |
| MOVE-002 | `_MSG_Motion` | `MotionRequest` `0x036A` | relay de movimento; `MSG_SetHpMode` quando HP=0 | sessao/HP | frame de motion ou SetHpMode | motion wire e gate de vida | C3 |
| SKILL-001 | `_MSG_SetShortSkill` | `SetShortSkillRequest` `0x0378` | save de short skills | estado do personagem | sem confirmacao dedicada | wire 32 bytes e persistencia | C3 |
| WORLD-001 | `_MSG_PKInfo` | `PkInfoConfirmation` `0x0366` | politica PK/multicast de visao | flag visual de PK/culpa | `MSG_STANDARDPARM` | wire 16 bytes; politica/multicast pendentes | C2 |
| COMBAT-001 | `_MSG_Attack` | `AttackRequest` `0x0367/039D/039E` | `LegacyAutoTradeBook.HasListing`, `LegacyAttackRvrGateRules`, `WorldHub.TryGetAttackState`, `TryConsumeGoldenShieldCoin`, `TryConsumeSkillMana`, `TryApplyPhysicalAttack` | listing, posicao/Clan, HP/MP, Gold, alvo, drops, XP | `MessagePanelConfirmation`, `AttackConfirmation`, `SetHpMpConfirmation`, `UpdateEtc`, `SetHpModeConfirmation` | wire 168/68/80; TradeMode responde MessagePanel 170 privado antes de USER_PLAY; gate 7.69 RvR bloqueia Clan zero no setor `(13,15)` com MessagePanel privado antes de HP0/tick/mana; W2PP nao tem esse gate. Skill 85 cobra `100*Special[2]` e envia UpdateEtc privado antes da mana, inclusive quando MP falha; smoke headless confirmou ordem, persistencia e retorno silencioso sem Gold. Demais gates/branches pendentes. Ver `CLIENT_E2E_SMOKE.md` | C3 |
| COMBAT-002 | ataque de skill | `AttackRequest` + skill index | `TryApplySkillAttack`, `TryConsumeGoldenShieldCoin` | HP/MP, Gold, efeitos, summon | ataque, recursos, morte, `UpdateEtc` | gate/formulas e ramos iniciais de NPC testados; custo/ordem da skill 85 validado em `COMBAT-ATTACK-GOLDEN-SHIELD-001`; delays, demais efeitos e equivalencia de area pendentes | C3 |
| ITEM-001 | `_MSG_UseItem` | `UseItemRequest` `0x0373` | `TryApplyPotion` | MOB/carry/HP/MP | `SetHpMpConfirmation`, `SendItem` | potion, reset, item families | C3 |
| ITEM-002 | `_MSG_DropItem` | `DropItemRequest` `0x0272` | `TryDropItem` | carry -> ground item | drop/create/broadcast | wire e ground drop | C3 |
| ITEM-003 | `_MSG_GetItem` | `GetItemRequest` `0x0270` | `TryGetGroundItem` | ground -> carry | get/decay/send item | wire e pickup | C3 |
| ITEM-004 | 7.69 `Basedef.h::MSG_SwapItem`, `SGrid.cpp`, cliente `TMHuman.cpp` e `TMSrv/_MSG_Move_Item.cpp` (`0x0376`) | `TradingItemRequest` / `TradingItemConfirmation`, payload 8 bytes / frame 20 bytes | `WorldHub.TryTradeItems` | carry 0..59 e carry↔equipamento; destino Equip 0..16 e origem Equip 1..16 somente com extensao 18-slot persistida; sem ela destino 0..14/origem 1..14; cargo estacionado | echo + dois `SendItem` | C# preserva a ordem de campos 7.69 no request e aplica `MAX_EQUIP-1` ao estado persistido; sem sidecar usa a faixa W2PP 16. W2PP usa ordem inversa e `WarpID` int. `BASE_CanEquip` rejeita posicao 15; slot 17 e sempre bloqueado. | C3; faixa condicional comparada e testada; blob canonico W2PP 16-slot preservado; persistencia MariaDB/file sidecar coberta; E2E visual pendente |
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
| ITEM-008 | `_MSG_CombineItem*`; neste recorte, `GetMatchCombineTiny` / `_MSG_CombineItemTiny` | `TinyCombineRequest` porta `0x03C0`, payload 72/frame 84: oito `STRUCT_ITEM` de oito bytes seguidos por oito posições assinadas; parser/encoder valida tipo, tamanho e checksum. `LegacyTinyCombineRules.TryGetSuccessThreshold` porta o matcher 7.69: veto do item 747, índices válidos nos três primeiros, alvo ARCH, grades 5..8, `EF_ITEMTYPE != 1`, posição 64/192 compartilhada e limites de sanção; calcula `CHANCEBASE + EF_ITEMLEVEL * 5`, tratando zero como sentinela de sem match. `LegacyTinyCombinePlanBuilder` + `WorldHub.TryPrepareTinyCombine` validam shape, capacidade atual (30/45/60), posições assinadas e igualdade completa dos itens contra o carry autoritativo sob lock; exigem 100M e devolvem cópias do pedido mais snapshots de MOB/Gold sem mutação. `LegacyTinyCombineOutcomeBuilder.TryBuild` normaliza `rand()%115` (100..114 vira 85..99), decide `roll <= threshold || LOCALSERVER` e cria um outcome sem mutar o mundo: o sucesso copia os três efeitos do atributo, aplica `BASE_SetItemSanc(7,0)` e limpa os itens 1 e 2; a falha preserva alvo/atributo. Ambos consomem os slots ocupados 2..7, descontam 100M e mantêm snapshots por cada envio de item, inclusive a repetição do slot 2. `WorldHub.TryApplyTinyCombine` compara MOB completo e Gold sob lock antes de publicar; `TryRollbackTinyCombine` restaura o MOB/Gold anterior somente se o resultado ainda estiver intacto. `TinyCombineCompleteConfirmation` emite `0xC0A7` com parâmetro int32 e cena 30000. `LegacyTinyCombineRelay` preserva sucesso (updates, UpdateEtc, mensagem, resultado 1, alvo) e falha (updates, mensagem, resultado 2, UpdateEtc). `InspectConnectionAsync` despacha apenas sessão Playing, carrega `Tiny ChanceBase` de `CompRate.txt` por `--comp-rate` (default 15); parser compara seção/chave sem diferenciar caixa para honrar `ChanceBase` no arquivo distribuído; source/W2PP compara a chave como `CHANCEBASE` exato e ignora essa capitalização e `--tiny-chance-base` (0..100) substitui o valor do arquivo, envia mensagens da `Language.txt` e rejeita stale state com resultado 0. `LOCALSERVER` fica falso: a condição de endereço externo da referência não foi copiada. O MOB do personagem segue pelo save normal de saída. O crédito ao tesoureiro fica em memória até o checkpoint periódico; falhas de disco são tentadas novamente na próxima passagem. A referência não rejeita posições duplicadas, regra mantida. W2PP diverge no matcher (`EF_ITEMTYPE != 4 || != 5` e gates de sanção); seu sucesso também omite a limpeza/update repetido do slot 2 e usa `ItemLog`; mantido o comportamento 7.69. Build `WydCdk.Engine.slnx --no-restore -m:1`: 0 avisos/erros. Engine.Tests: 553/553. `dotnet --info` identifica SDK 10.0.401, mas saiu 1 por acesso negado ao Service Control Manager. Em posição inválida, 7.69/W2PP chamam `RemoveTrade(conn)` sem gate de HP. Em `USER_PLAY`, a função envia o signal 900 e, com `TradeMode`, emite `CreateMob` e zera o modo. O C# agora usa `CloseForTinyCombineInvalidPositionAndCaptureAsync`, preservando gates de sessão/identidade, mas permitindo HP 0/-1; o coordenador fecha o par bilateral e remove lojas persistidas por expected-delete. O relay emite `0x0384` para ambos, `CreateMob` das lojas fechadas e PKInfo. Isso adapta a limpeza local do C# e não reproduz a limpeza unilateral nem o reset completo de `TradeMode`/`AutoTrade` da rotina 7.69. Engine.Tests: 553/553, cobrindo HP 0/-1 e fechamento/remoção persistente local. E2E visual para signal/CreateMob/TradeMode pendente. No sucesso, 7.69 credita 10M em `MOB.EXP` do NPC `Merchant=6` registrado em `GuildImpostoID` por zona; o handler também exige `MOB.Guild == ChargeGuild` e `EXP < 200000000000`. `MOB.Merchant` (offset 17) sobrevive ao gerador; `BaseScore.Merchant` (offset 56) é o campo zerado. `WorldHub` registra por `CurrentScore.Level` o último NPC `Merchant=6` criado em cada zona e lhe atribui `ChargeGuild`, como `GuildImpostoID` no 7.69. No sucesso, acrescenta 10M a `MOB.EXP` se o valor atual for menor que 200000000000, inclusive o overshoot possível; o crédito só ocorre com o NPC ativo (`Mode != Empty`) e o rollback CAS restaura também o snapshot do tesoureiro. As rotas de remoção invalidam os registros, e a gravação exige que o ID continue como destinatário registrado da zona. `LegacyGuildTreasuryFileStore` grava atomicamente o MOB bruto em `npc/<MobName>` sob `--npc-root` pelo loop periódico de 12 s sobre os tesoureiros registrados. Falha de disco mantém o crédito em memória, registra o erro e a próxima passagem tenta novamente. O host registra `category=Combines` e eventos Succeeded/Failed/StaleItem na saída do processo; quando `ServerWireLog` abre, o tee os persiste. Isso cobre os três `MyLog(LogType::Combines)` do 7.69. Não portei o dump `ItemLog`/códigos de item do W2PP: a referência 7.69 monta a string, mas não a envia ao `MyLog`. A cobertura local inclui NPC inativo/removido/substituído, retry após falha de disco e nome legado com espaço final; E2E visual permanece pendente. | validar visualmente `TradeMode`/`AutoTrade` e `CreateMob`; E2E da combinação e variantes |
| ITEM-009 | `_MSG_ApplyBonus` | `ApplyBonusRequest` `0x0119`, frame 20/payload 8; `WorldHub` cobre `BonusType=0` (ScoreBonus), `1` (Special) e `2` (skill base); gates, mensagens, limites e ordem dos relays 7.69 | origem/crosswalk/persistência de `LearnedSkill[1]`, paridade integral de `BASE_GetCurrentScore` e E2E visual |
| TRADE-001 | `_MSG_ReqTradeList` / `_MSG_SendAutoTrade` | `TradeListRequest` isolado; parser/encoder de `MSG_STANDARDPARM` (`0x039A`, 16 bytes) testado; `LegacyAutoTradeListRules` cobre solicitante vivo/em `USER_PLAY`, alvo existente/ativo/em `USER_PLAY` e janela inclusiva `VIEWGRIDX/Y=33`; `LegacyAutoTradeLocationRules` reproduz os cinco limites de vila, a área protegida de Armia e a taxa por `CityTax`; `AutoTradeListConfirmation` e `AutoTradeStartRequest` compartilham e testam o layout client/server de `MSG_SendAutoTrade`/`MSG_AutoTrade` (`0x0397`, 196 bytes) com 12 slots; `LegacyAutoTradeBook` valida uma oferta autoritativa em memoria, incluindo cargo 7.69 de 120 slots, preço, taxa, título, conta bloqueada e `EF_NOTRADE`; o dispatcher abre a loja, consulta o book, reaplica os gates e `LegacyAutoTradeListRelay` entrega a resposta ao solicitante; `LegacyAutoTradeVisualRelay` projeta o MOB/affects autoritatorios e o titulo para `MSG_CreateMobTrade` 7.69 de 260 bytes, enviando ao dono, à vizinhanca e ao solicitante de lista; `LegacyAutoTradeRemovalRelay` monta `MSG_RemoveMob` de 16 bytes no logout/desconexao, enviado somente à vizinhanca 33x33 antes de `WorldHub.Leave`; `LegacyAutoTradeFileStore` persiste owner/listing, localizacao e taxa em documento versionado quando `--autotrade-state` esta ativo, e o host salva na abertura e remove na saida; `MariaDbAutoTradeStateStore` usa `wyd_autotrade_state` no modo MariaDB; `LegacyAutoTradeRehydrator` restaura o MOB offline no boot e projeta `MSG_CreateMobTrade` para novos logins | migração e round-trip local no MariaDB aprovados; banco externo, política de reconexão do dono e E2E pendentes |
| TRADE-005 | `_MSG_ReqBuy` / `MSG_ReqBuy` / `_MSG_ItemSold` | `AutoTradePurchaseRequest` preserva `0x0398`, payload 24/frame 36, `Pos`, `TargetID`, padding ABI, `Price`, `Tax` e `STRUCT_ITEM`; `LegacyAutoTradePurchaseRules` valida alvo/alcance, loja ativa, posição, preço/taxa/item do request contra o listing, cargo do vendedor contra o item, Gold do comprador, espaço 0..59, vila 0..4 e limite de 2G do vendedor, inclusive alvo offline explicitamente reidratado; `ItemSoldConfirmation` preserva `0x039B`, standard-parm2 de 20 bytes e `ESCENE_FIELD`; `LegacyAutoTradeSettlementMath` reproduz o limiar de 100 mil e o truncamento inteiro do imposto; `LegacyAutoTradePurchasePlanBuilder` produz cópias do carry/cargo, saldos e listing pós-venda sem mutar as fontes; `LegacyAutoTradePurchaseCommitRequest` separa o MOB runtime do comprador do MOB persistido e fecha os dois blobs com o listing na mesma transação serializável no MariaDB ou via journal/recovery no file store; `LegacyAutoTradeBook` protege o slot, `WorldHub` aplica/rollbacka o comprador, `LegacyAutoTradePurchaseCoordinator` prepara o request do host e `LegacyAutoTradePurchaseExecutor` coordena book/NPC offline/comprador/commit com rollback reverso; o dispatcher emite `MSG_UpdateCarry`, `MSG_ItemSold` e o visual offline somente depois do commit | 211 testes deterministas, incluindo coordinator, relay pós-commit, CAS distintos, commit/recovery do file store e a fatia 18-slot; smoke local do MariaDB passou com compra, verificação pós-commit e replay CAS; banco remoto, interrupção e E2E continuam pendentes |
| TRADE-002 | `_MSG_Trade` / `_MSG_CNFCheck` | `TradeOfferRequest` e `TradeCheckConfirmation` testados; `WorldHub.TryStageTradeOffer` cobre estado bilateral em memoria, carry autoritativo, saldo, duplicidade, `EF_NOTRADE`, guilda, PK e checks; `LegacyTradeOfferRelay` encaminha a oferta e o dispatcher conclui os dois checks com `TryCompleteTrade` + `LegacyTradeCompletionRelay`/`UpdateCarryConfirmation` para os dois participantes; `LegacyTradePersistence` monta os dois estados e o host chama `IAtomicCharacterStateStore` quando disponivel; `MariaDbWorldCharacterStore` e `LegacyFileAccountStore` fazem o commit dos dois blobs em transacao serializavel ou journal/recovery, respectivamente | transacao MariaDB ainda sem teste contra banco vivo; mesma conta, ciclo completo e E2E |
| TRADE-003 | `_MSG_QuitTrade` / `RemoveTrade` 7.69 e W2PP; `GetGuilty` / `GridMulticast` | `TradeCloseRequest` `0x0384`/12; HP <=0 retorna SetHpMode sem limpar par; aceite envia cancelamento a ambos e PK `0x0366`/16 ao autor/vizinhos 33x33; sem par, NoActiveTrade carrega plano com OpponentId0: sinal proprio e PK sem cancelar ex-parceiro. `LegacyTradePkRules` usa guilty/PKMode/Newbie/Castle/Tower sob lock, sem RvR extra W2PP | 220 PASS/build limpa; rede HP0/-1 preserva par, sem PK/cancelamento; redes PK0/1 por guilty e dois reenvios idle passam com carry/Gold completos intactos nos blobs/relogin. Controles negativos dominio/rede antigos falham. Deploy sandbox com backup/hashes UP/PVP conferidos; ver CLIENT_E2E_SMOKE. Pendentes: PK1 por PKMode/Newbie/Castle/Tower e exclusao do distante na rede, AddCrackError, ISTradTimer, concorrencia fora do fechamento coordenado e E2E visual. O reset/visual normal para loja online no Action esta no recorte MOVE-ACTION-TRADEMODE-001. |
| TRADE-AFK-001 | 7.69 `CUser.h`, `_MSG_SendAutoTrade.cpp:112`, `_MSG_QuitTrade.cpp:16`, `ProcessSecMinTimer.cpp:928-988`; W2PP `CUser.h`, `_MSG_SendAutoTrade`, `ProcessSecMinTimer.cpp:912-972` | auditoria sem implementacao: ISTradTimer e timestamp de abertura de autotrade, nao contador/cooldown bilateral; 7.69 grava timeGetTime e zera no QuitTrade; consumidor usa LojinhaTimer, MAC e escolha por timestamp no evento AFK. W2PP tem consumidor, mas nenhuma atribuicao no handler de abertura auditado | C# sem equivalente; nao adicionar estado sem consumidor. Premio 7.69 premioLojaAfk/OnlyTrade difere do W2PP titulo #VemProCdK/item475. Evento, recompensa, cadencia real, selecao por MAC e reset ficam pendentes em trilha propria, sem port automatico ou mudanca de runtime |
| TRADE-CLOSE-001 | `_MSG_QuitTrade` / `RemoveTrade` 7.69 e W2PP; extensao C# de loja persistida | T1..T6 no recorte: reserva/HP/recovery, captura sob leases e host QuitTrade; registry compartilhado/concurrente; planos sinais/PK/CreateMob236/view33x33; recovery por AffectedOwners; abertura/consulta196+260; logout/disconnect save/Leave protegidos contra compra/rollback, sockets depois da lease; cleanup detached expected-delete/barreira atomica owner sem tocar ID reutilizado; ver ledger | 259 PASS/build limpa/MariaDB isolado; smoke TCP real debug e self-contained abriu/consultou loja, fechou normal236 sem RemoveMob, rejeitou compra posterior e dois retries/relogin preservaram carry/cargo/Gold. Controle negativo anterior falhou. Dois Important do review do harness corrigidos RED->GREEN (porta18283/barreira comprador), dois minors deferidos no ledger. Runtime-only publicado no sandbox com backup/UP-PVP Running/8281-8282 Listen/hashes iguais. Politica ordinaria de remocao preservada, E2E visual cliente3D e port inteiro nao concluidos |
| TRADE-004 | `_MSG_TradingItem` completo (cliente 7.69 `MSG_SwapItem`) | apenas carry 0..59 | cargo, equipamento, merge, validade e regras especiais |
| SHOP-005 | `_MSG_Buy` / `_MSG_Sell` | `ShopBuyMessage` and `ShopSellMessage` cover buy/sell wire; `0x03E8` uses transient `WorldHub.Participant`; NPC sale prep/commit covers equipment/carry, Coin, recycle bin, treasury EXP and conditional rollback; authenticated success order `0x5000 -> 0x037A -> [0x036B] -> 0x0337`; localized `MSG_MessagePanel` gates 170/116/167; out-of-view close signal `0x0196`/ID 30000; empty-carry `SendItem` resync `0x0182`; player checkpoint ring `1..999`/4 s, process-local account-root gate shared with AutoTrade file writes, and separate 12 s treasury checkpoint with shutdown drain/final sweep | Full build 0 warnings/errors; Engine.Tests `590/592` with workspace TEMP/TMP, the new account-root gate test passed and only two loopbacks failed with `SocketException 10013`; prior authenticated headless smokes under `Backup/Build/shop-005-carry-resync-20261104/`, `Backup/Build/npc-sell-empty-carry-resync-20261104/` and `Backup/Build/npc-sell-close-shop-20261104/` verify sale/checkpoint order and bytes, empty `0x0196` close signal, and `0x0182` resync for carry index 0. Local MariaDB port closed; last provider 1042 before DDL, no schema/data change. Open: MariaDB, live authoritative Cargo owner/CAS sale, buyback, `MSG_Buy`, stock and visual E2E; account roots remain single-process for the file adapter |
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
## MOB-AI-001 Task 5CC - player Poison Type 20 timer (23/10/2026)

TMProject 7.69 `ProcessSecMinTimer.cpp:1711` and W2PP `:1655` call
`ProcessAffect` for live players on the matching 16-slot second. Type 20 is in
7.69 `Server.cpp:6210-6253` and W2PP `:5145-5187`. The player branch sets
`AffectValue=100`, divides it by ten, then casts `(100 - AffectValue) / 100.0`
to integer; 0.9 truncates to zero. 7.69 uses `Perda=500`, while W2PP uses
`Perda=1000`. The port follows the selected 7.69 contract: calculate
`Perdamax = Hp - 500`, apply the truncated multiplier, then clamp HP to
`[1, MaxHp]`. For example, 900 HP becomes 400; 200 HP becomes 1. The W2PP
cap difference is retained as an explicit source divergence.

Both sources decrement finite time below `32400000`, clear the full 8-byte
affect when the timer reaches zero, and set `regen=1` for every Poison tick,
so `SetHpDam` is sent even when the signed delta is zero. Score is refreshed
only if HP changes or the affect expires. Neither path changes equipment. The
7.69 function adjusts `pUser.HP` by the signed delta; W2PP adjusts `ReqHp`.
The C# stages its requested-HP delta locally and commits it only with the
confirmed world snapshot, flooring the request at current HP.

TDD RED reproduced the missing player Type 20 branch; GREEN Engine.Tests
`491/491` covers the 500 HP cap, integer truncation/floor, expiry, signed delta,
requested HP, equipment flag and captured recipients. The three-peer loopback
smoke marker `player-type20-poison=hp-score-relayed` confirms `SetHpDam` before
`UpdateScore` for the actor and in-view peer, with no frame to the outside peer.
Full no-incremental solution build: 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager.

Local self-contained win-x64 publish: 200 files/84,062,527 bytes in
`Backup/Build/MOB-AI-001-Task5CC-20261023`; ZIP 37,075,501 bytes, SHA-256
`228D8B38A8F705C8405519A9C7A50ED172BEB41CDB0A1D1BE649C7D8086DA23A`. No
credential-like filenames were found. The latest elevated read-only `wyd-vps`
SSH probe timed out before the handshake; a TCP-only port 3389 probe responded,
but no RDP session was opened. No authentication, transfer, restart, or UP/PVP
check occurred. No authenticated Host, visual client E2E, Site/production
change, commit or push.

## MOB-AI-001 Task 5CE - common player affect timer tail (23/10/2026)

TMProject 7.69 `ProcessAffect` at `Server.cpp:6033` and W2PP at `:4968`
skip affect records with `Type <= 0`. After the special Type 17, 20, 22 and 23
branches, remaining active records reach `LessAffect` (7.69 `:6899-6920`,
W2PP `:5831-5852`). `STRUCT_AFFECT.Time` is `unsigned int` in both `Basedef.h`
files. Therefore a finite timer below 32,400,000 is decremented with unsigned
arithmetic; if that decrement reaches zero, the full record is cleared and score
refresh is requested. A starting time of zero wraps to `UINT_MAX` and does not
expire. TMProject 7.69 additionally marks FaceChange for Type 33, matching the
already selected C# equipment refresh behavior; W2PP only marks Type 16.

The player timer previously filtered to a short supported-type list. It now
applies the common countdown/expiry tail to every nonzero affect while retaining
the special Type 17/20 HP branches and Type 16/33 equipment flag. This generic
tail slice did not itself emulate the active Type 22/23 area attacks; Task 5CF
below now covers that separate dispatch and relay.

TDD RED reproduced the missing Type 18 countdown; GREEN Engine.Tests `491/491`
covers generic finite countdown, expiry, score-only outcome, persistent-time
threshold and unsigned zero wrap. The loopback smoke marker
`player-generic-affect-expiry=score-only-relayed` checks silent countdown,
view-scoped `UpdateScore`, and no outside frame. Full no-incremental solution
build used a temporary output path to avoid a loaded runtime assembly: 0 errors
and the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exits 1 because the runner cannot open Service Control Manager.
No package or deploy. The read-only SSH `wyd-vps` probe was denied in the
sandbox and timed out elevated before TCP/22 handshake; no RDP, transfer,
restart or UP/PVP check. No authenticated Host, visual client E2E, Site change,
commit or push.

## MOB-AI-001 Task 5CF - player Type 22/23 timer area attacks (23/10/2026)

TMProject 7.69 `Server.cpp:6279` and W2PP `Server.cpp:5213` implement the
player Type 22 branch; Type 23 begins at TMProject `:6589` and W2PP `:5522`.
Both branches first scan the upper-left 2x2 area around the stored target tile,
then the upper-left 5x5 area, preserve row-major Y/X order, and keep at most six
distinct targets. Each builds a zeroed `MSG_Attack`, sets Motion 254 and Skill
33 (Type 22) or 52 (Type 23), places the selected IDs in the damage slots with
damage `-1`, and invokes the normal server attack dispatcher. Mana is therefore
charged by the normal authoritative skill path before the common affect timer
tail executes.

The C# timer now routes these two affect types through `TryApplyAttackActionBatch`
using `ClientTickPolicy.SkipCheckTick`. It selects the same six slots and
ordering, spends mana from loaded SkillData, retains the batch's authoritative
damage/resource/death outcomes, and then decrements or expires the affect. The
relay emits a server-generated 0x0367 Attack with authoritative target IDs and
damage to the caster's captured view, plus supported target resource, removal,
experience and item-drop updates. `g_pGuildAlly` is consulted in both C++ trees
to protect allied guilds; the current C# world has no equivalent alliance
mapping, so this filter is still incomplete and must not be described as full
parity. The source centers the scan on `pMob[idx].TargetX/Y`; WorldHub has no
separate movement-target state, so the C# implementation uses participant
`PositionX/Y` as a proxy. Equivalence while movement is in progress is open.

When the legacy scan finds no eligible first target, it skips the internal
attack call and mana charge but still reaches the common timer tail. The C# port
preserves that condition: no area outcome or wire frame is emitted, mana is
unchanged, and the affect countdown still proceeds.

TDD RED reproduced the missing Type 22 dispatch. GREEN Engine.Tests `491/491`
covers Type 22/23 damage and mana debit, countdown, no-target/no-debit, and the
2x2-then-5x5 six-target cap/order. The three-peer loopback smoke marker
`player-type22-23-area-attack=view-scoped` confirms valid Skill 33/52 Attack
frames and `RemoveMob(type=3)` on NPC death to the actor and in-view observer,
with no frame to an outside peer.
Full no-incremental solution build: zero errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1
because the runner cannot open Service Control Manager. This is synthetic
loopback evidence, not authenticated Host or visual TMProject client E2E.
No publish/deploy, Site change, commit or push.
Task 5CG below adds the Pista transition and Zard `MobLeft` relays. Player
target resource changes are emitted by the relay code but still lack a
dedicated loopback case; the `g_pGuildAlly` filter gap also remains.

## MOB-AI-001 Task 5CG - player timer Pista/Zard outcome relay (23/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:2073-2087` handles
an attack on the exact NPC name `Zard`: it sets equipment slot 12 effect 1 to
51, increments its value, sends `_MSG_MobLeft` to the attacking connection,
and on value 80 inserts rune 475 and resets the counter. The C# combat outcome
already contains `ZardMobLeftCount` and `ItemDrops`; the player timer relay now
sends the counter only to `AttackerConnectionId` before the area Attack and the
item reward after Attack.

The timer relay also forwards every field already present in
`LegacyPistaTransition`: generated NPC groups, listed NPC removals, and
teleports. Removals follow the synthesized Attack with `RemoveMob(type=3)` to
the captured area recipients. Generated groups use the existing
`LegacyNpcGenerationRelay` and each spawned NPC's inclusive view. Teleports
follow the normal Host order: Action to the moved participant, then broadcast
to the other connected participants. This changes relay only; it does not
change how quest transitions are computed.

The loopback Pista case uses the currently modeled C# transition for generated
IDs `5775..5785 -> 5767`. That behavior matches W2PP `Basedef.h:460-464` and
`MobKilled.cpp:2458-2468`, but not the selected TMProject 7.69 constants
`4291..4301` with boss `4283` in `Basedef.h:363-367`. The relay is covered for
the currently modeled next-wave spawn; migrating the Pista quest logic to the
7.69 IDs remains open. The smoke does not separately execute the Pista reset
and teleport variants.

TDD RED failed because the attacker received the area Attack without Zard's
`MobLeft`; GREEN now covers Type 22/23, the 80th Zard signal and rune reward,
the Pista Lv6 next-wave spawn, and view-scoping across three loopback peers.
Marker: `player-area-quest-effects=mobleft-reward-pista-spawn-relayed`.
Engine.Tests `491/491`; full no-incremental solution build: 0 errors and the
pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exits 1 because Service Control Manager access is denied.
This remains local synthetic loopback evidence, without authenticated Host or
visual client E2E. No publish/deploy, Site change, commit or push.

### MOB-AI-001 Task 5CH - Pista Lv6 7.69 generator IDs (concluida localmente 23/10/2026)

TMProject 7.69 `TMSrv/Basedef.h:363-367` defines the Lv6 first-wave death IDs
as `4291..4301`, the second-wave/area IDs as `4284..4290`, and boss `4283`.
W2PP `Basedef.h:460-464` uses `5775..5785`, `5768..5774`, and boss `5767`.
`MobKilled.cpp:2461-2472` in TMProject counts kills from the first range and
generates boss 4283; W2PP `MobKilled.cpp:2458-2471` decrements its remaining
count and generates 5767. This slice changes C# death selection to `4291..4301`
and boss 4283, recognizes `4283..4301` as generated Pista identities for room
cleanup/classification, and keys the +6 rune reward off 4283.

The C# countdown is still the W2PP `MobCount` model. TMProject has a separate
`Pista[6].MobsLeft` incrementing to 100, keeps `Party[0].MobCount` for route
cleanup, and sends `_MSG_MobCount` with two parameters. This slice does not
claim full Pista Lv6 equivalence; that state/wire split remains open.

TDD RED: Engine.Tests `492 total / 489 passed / 3 failed`, with the transition,
boss reward, and reset cases failing against the old IDs. GREEN: `492/492`. The full solution build
without incremental outputs passed with 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1 due
Service Control Manager access denial. The three-peer `--mob-ai-smoke` passed
with `player-area-quest-effects=mobleft-reward-pista-spawn-relayed` and the
updated Pista IDs, confirming generated-NPC relay stays view-scoped. Local
self-contained win-x64 publish: 200 files/84,116,287 bytes in
`Backup/Build/MOB-AI-001-Task5CH-20261023`; ZIP 37,085,999 bytes, SHA-256
`FD976188C65656CF1E83DAEE8EF8B343A2819027F645106C57BFA9EF51EC8BEA`. No
credential-like filenames were found. `wyd-vps` SSH was denied in the sandbox
and timed out elevated; no transfer, restart, or UP/PVP check. No authenticated
Host, visual `WYD.exe` E2E, Site/production change, commit, or push.

### MOB-AI-001 Task 5CI - separate Pista Lv6 counters and status wire (concluida localmente 23/10/2026)

TMProject 7.69 `TMSrv/Basedef.h:711-724` stores `Party[0].MobCount` separately
from `Pista[6].MobsLeft` (`INT16`). `MobKilled.cpp:2461-2469` increments
MobsLeft for deaths in the first-wave range `4291..4301`, caps progress at 100,
and generates boss 4283 on the 100th kill. The route timer still uses
Party[0].MobCount for generated-NPC cleanup. `ProcessSecMinTimer.cpp:518-535`
sends the room status every twelve 200 ms ticks through
`SendShortSignalParm2Area(3330,1475,3448,1525,...)`.

The signal is `_MSG_MobCount=187 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME` in
`TMSrv/Basedef.h:2404`; `SendFunc.cpp:357-367` constructs
`MSG_STANDARDSHORTPARM2` with two 32-bit parameters. C# now emits type `0x03BB`,
scene `ESCENE_FIELD`, and payload `(MobsLeft,100)` every 2.4 seconds to players
inside the same exclusive rectangle. MobsLeft is held independently of active
room state and remains after room cleanup, matching the source's global field
(no reset was found in the reference call sites). W2PP instead decrements
Party[0].MobCount for kills and sends `_MSG_MobLeft` with one parameter; that
variant is kept out of this 7.69 path.

TDD RED: Engine.Tests `492 total / 490 passed / 2 failed` against the old
countdown and one-parameter packet. GREEN: `492/492`. Coverage checks the first
kill (`0 -> 1`), the 100th kill (`99 -> 100`, boss 4283), independent party and
progress counters, the two-parameter packet, the 2.4 second cadence, area bounds,
and progress after room reset. Full no-incremental solution build passed with 0
errors and the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`; SDK
10.0.401, `dotnet --info` exit 0. `--mob-ai-smoke` passed with
`player-area-quest-effects=mobleft-reward-pista-spawn-relayed` and
`view-scoped=true`.

Local self-contained win-x64 publish: 200 files/84,117,051 bytes in
`Backup/Build/MOB-AI-001-Task5CI-20261023`; ZIP 37,086,386 bytes, SHA-256
`42BCD5641F7B81318DD2090F93E6D80CCDCB56911E3AE51FBF93C776A3455878`. No
credential-like filenames were found. `wyd-vps` SSH was denied in the sandbox
and timed out elevated; no transfer, restart, or UP/PVP check. No authenticated
Host, visual `WYD.exe` E2E, Site/production change, commit, or push. Remaining
Earlier MOB-AI gaps included `g_pGuildAlly` and separate `TargetX/Y` state.
Task 5CJ below closes player-target resource loopback coverage; Task 5CK ports
the directed alliance state and filter. Separate `TargetX/Y` remains open.

## MOB-AI-001 Task 5CJ - player Type 22/23 target resource loopback (23/10/2026)

`LegacyPlayerAffectRelay` already sends `SetHpMp` when the area-skill batch
changes a player target's HP. The three-peer loopback smoke now executes both
Type 22/Skill 33 and Type 23/Skill 52 against a player target in the actor view,
with the third peer outside. It checks a valid `0x0181` packet of 28 bytes with
the target connection ID and authoritative HP/MP before the Attack frame. The
same area Attack reaches actor and target with an identical payload; the outside
peer receives neither a resource frame nor the Attack.

No gameplay code changed in this slice; it closes a relay-evidence gap only.
Marker: `player-type22-23-target-resource=target-only`; the full `--mob-ai-smoke`
passed with exit 0. Engine.Tests first returned `491/492` because
`CurrentScoreLoginStartupTests` hit its known variable subprocess timeout. After
the solution build refreshed the Host DLL, the no-build rerun passed `492/492`.
Full solution build `dotnet build WydCdk.Engine.slnx --no-incremental
--no-restore`: 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exits 1 because
Service Control Manager access is denied. No package/deploy, authenticated Host,
visual client E2E, Site/production change, commit, or push. Remaining MOB-AI
gaps are `g_pGuildAlly` and separate `TargetX/Y` state.

## MOB-AI-001 Task 5CK - directed GuildAlly state and Type 22/23 friendly filter (23/10/2026)

TMProject 7.69 `TMSrv/_MSG_GuildAlly.cpp` and W2PP
`TMSrv/_MSG_GuildAlly.cpp` accept `_MSG_GuildAlly` only when the request's Guild
matches the active MOB and `GuildLevel == 9`; each validates Guild and Ally in
`1..65535`. The frame is type `0x1212` with an 8-byte payload of signed 32-bit
Guild and Ally IDs. Both `TMSrv/Server.cpp::DoAlly` implementations use a
directed `g_pGuildAlly[Guild]` pointer: a valid first request sets the requested
ally; when the requester already has a valid ally, the next request clears that
pointer and ignores its newly requested target. The opposite guild pointer is
unchanged. The source emits global Language.txt notices 287/286 and refreshes
`SendWarInfo` for active members of the declaring guild only. The C++ DB relay
also maintains/replicates this table between server processes; the C# single
monolith keeps the state in its existing volatile world state, with no new
database contract.

`GuildAllyRequest` parses and encodes the exact frame. `WorldHub` authorizes the
active leader, applies the directed toggle under its lock, snapshots the map
read-only, preserves `GuildWarPartners`, and returns global notice recipients
plus `SendWarInfo` updates. The server serializes this handler with GuildWar
transitions and relays the Language.txt message followed by the updated
`0x03A8` tuple. The `SendWarInfo` ally value now comes from the current map.

TMProject 7.69 `TMSrv/Server.cpp:6354,6465,6664,6775` and W2PP
`TMSrv/Server.cpp:5286,5398,5595,5707` use the same area target predicate:
normalize disabled guild IDs to zero, then skip the same party leader, same
guild, or the directed ally. C# now applies the guild and alliance skips to
player Type 22/23 area target selection. Regression coverage checks Type 22 and
23, an excluded directed ally, an allowed enemy, and the un-inferred reverse.

Engine.Tests `495/495`. The no-incremental solution build passed with 0 errors
and the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exits 1 because Service Control Manager access is denied. The
Host loopback `--guild-war-smoke` passed with generated accounts and a synthetic
ItemList, no external database, and markers
`GUILD_ALLY_DIRECTED_DECLARATION_PASS` / `GUILD_ALLY_TOGGLE_CANCELLATION_PASS`.
No package/deploy, authenticated Host, visual client E2E, Site/production
change, commit, or push. At Task 5CK completion, separate movement `TargetX/Y`
still needed source review; Task 5CL below closes that timer-path question.

## MOB-AI-001 Task 5CL - verify Type 22/23 movement target state (23/10/2026)

TMProject 7.69 `_MSG_Action.cpp` validates a changed destination and invokes
`GridMulticast(conn, TargetX, TargetY, ...)` on the accepted movement path.
`SendFunc.cpp::GridMulticast` moves the authoritative grid occupancy and writes
`pMob[conn].TargetX/Y` to the accepted destination before returning. Its
occupied-destination correction returns earlier and does not update the
authoritative tile. W2PP `_MSG_Action.cpp` and `SendFunc.cpp` use the same
transition.

The C# handler follows the same boundary: only `MovementResult.Accepted` calls
`WorldHub.UpdatePosition`, and rejected/corrected movement leaves that state
unchanged. `TryRunPlayerAffectAreaAttackLocked` reads the locked participant
tile, so after the accepted transition it uses the same cell as legacy
`pMob.TargetX/Y`. The client `Route` describes animation steps and is not a
separate pending tile in these server timer paths. Therefore adding another
TargetX/Y state field would duplicate the authoritative coordinate and risk
drift; no runtime change is needed for this gap.

Regression coverage runs Type 22 and Type 23 after changing the actor tile and
asserts only the target around the new tile is hit while the old tile remains
untouched. Engine.Tests `495/495`. The three-peer `--mob-ai-smoke` additionally
passed `player-type22-23-movement-target=accepted-tile`: actor and in-view peer
received the same Attack identifying the target at the accepted tile, while the
outside peer received no frame. The no-incremental solution build passed with 0
errors and the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK
10.0.401; `dotnet --info` exits 1 because Service Control Manager access is
denied. The Host loopback `--guild-war-smoke` passed in Task 5CK. SSH
`wyd-vps` was confirmed by a read-only probe; no remote changes. No package/deploy,
authenticated Host, visual client E2E, Site/production change, commit, or push.

## MOB-AI-001 Task 5CM - g_pFormation with active 7.69 data (23/10/2026)

TMProject 7.69 declares `char g_pFormation[5][12][2]` in
`TMSrv/Basedef.cpp:183` and reads `g_pFormation[i][j][Formation]` in
`TMSrv/Server.cpp:4586-4588`. W2PP declares the same shape and identical 120
initializer values in `Basedef.cpp:193`; `Server.cpp:3718-3720` uses the same
index order. `NPCGener.txt` in the selected 7.69 source has 23 records with
`Formation=4`. Every such record has `MinGroup` 0 or 1, and all five route-range
fields are zero. The `if (SegmentRange[j])` branch that reads `g_pFormation`
therefore never runs for these active records.

The C# keeps its current linear lookup bounded to the 120 initialized values
and returns zero beyond that object. For future generator data with a nonzero
route range and `Formation >= 2`, the C++ expression indexes outside the last
dimension; any read beyond the complete object is undefined and has no portable
contract. Do not copy adjacent process memory to mimic it. This decision closes
the concern for the selected active 7.69 data, without claiming equivalence for
arbitrary future data that exercises those invalid indices. No runtime code
changed; the existing synthetic `Formation=4` test continues to characterize
the deterministic bounded fallback. Engine.Tests `495/495`, full build, and
`--mob-ai-smoke` were green after Task 5CL. SSH `wyd-vps` was confirmed with a
read-only probe; no remote changes, package/deploy, authenticated Host, visual
client E2E, Site/production change, commit, or push.

## MOB-AI-001 Task 5CN - player Fairy timer attacks (23/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/Server.cpp:6033-6163` and W2PP
`Server.cpp:4968-5098` both process the equipped Fairy in player `ProcessAffect`
before affect countdown. Equip slot 13 maps item indexes 753, 769, and 1726 to
skills 32, 34, and 36. The timer requires an active current target and applies
the source checks for target mode/map, PK mode, distinct leader/guild, player
RSV/merchant, clan 7/8, and target coordinates. Each source constructs an
`_MSG_AttackOne` for one target, uses motion 254 and the `0xE0A1ACA` skip-tick
sentinel, and runs it through `ProcessClientMessage`. Both
`_MSG_Attack.cpp:2283` (TMProject) and `Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1678`
set the attacker's `CurrentTarget` in the alive-target tail.

The C# now retains the last living target reached by an accepted physical or
skill attack batch in slot order. On the player timer it maps only the three
audited item indexes, checks target/mode/clan/party/guild/merchant/coordinate
gates, charges skill mana with the existing Magic Mirror rule, then invokes the
shared authoritative attack batch for that target. The generated request uses
the source skip-tick policy and motion 254; the relay sends the resulting
68-byte one-target Attack frame to the captured view and target HP/resource
refresh to the target. No client-provided damage is used. Invalid/dead targets
are cleared; insufficient mana produces the normal actor resource refresh.

- [x] Engine coverage for each item-to-skill mapping, the remembered living target, target HP/mana, insufficient mana, no/dead target, PK disabled, and Clan 4 target. RED before the runtime change; final Engine.Tests `495/495`.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-incremental`: 0 errors; one existing `CS8604` at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; a final `dotnet --info` returned exit 0.
- [x] TCP loopback `--mob-ai-smoke`: marker `player-fairy-753-769-1726=attackone-view-scoped`; attacker and in-view peer receive the same one-target Attack, target gets HP refresh, outside peer receives no frame.
- [x] Update the project handoff and backlog. No package/publish/deploy, authenticated Host, visual `WYD.exe` E2E, Site/production change, commit, or push.

Remaining scope: the legacy timer still has other affect/item branches; audit and port them individually against both sources. The loopback harness is synthetic and does not establish authenticated-Host or visual client compatibility.

## MOB-AI-001 Task 5CO - non-generated NPC Type 22 timer (23/10/2026)

TMProject 7.69 `TMSrv/Server.cpp:6279-6895` and W2PP
`Server.cpp:5213-5827` skip NPC attack dispatch for Type 22. Both then reach the
common finite affect countdown: decrement time below `32,400,000`, clear the
complete 8-byte affect when it reaches zero, and request a score refresh without
changing HP. `ProcessAffect` is invoked for Peace and Combat NPCs in the 7.69
`ProcessSecMinTimer.cpp:1922,1938,2131` paths and matching W2PP
`ProcessSecMinTimer.cpp:1840,1854,2047` paths.

The C# timer runtime previously covered generated NPCs and the separately
modeled summons. This slice adds only persistent, alive NPCs whose
`GenerateIndex < 0`, `LeaderId <= 0`, and AutoTrade state is absent. The stable
connection-ID shard query prevents overlap. Peace consumes the affect on its
assigned shard visit; Combat increments the existing phase and consumes the
affect on the fourth visit. This path does not invoke the AI planner, movement,
combat, summon, or AutoTrade processing. Expiry returns the existing score-only
`AffectTick` outcome and captures the NPC view for UpdateScore relay.

- [x] Engine tests cover Peace decrement/expiry and Combat cadence, all affect bytes cleared, unchanged HP, no attack, and score-only expiry. RED was observed before implementation; final Engine.Tests `495/495`.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-incremental`: 0 errors and one existing CS8604 at `AttackDivineBlessingTests.cs:256`.
- [x] SDK evidence: .NET 10.0.401; `dotnet --info` exit 0.
- [x] TCP loopback `--mob-ai-smoke` passed `NON_GENERATED_NPC_TYPE22_SMOKE_PASS score-only=true view-scoped`; both in-view peers receive UpdateScore at expiry, the outside peer receives no frame, and the final `MOB_AI_SMOKE_PASS` marker is present.
- [x] Handoff, progress, and backlog updated in place. Type 23 NPC timer remains a separate source audit. No package/publish/deploy, authenticated Host, visual client E2E, Site/production change, commit, or push.

The loopback harness uses a synthetic WorldHub and peers; it does not prove
authenticated Host or visual client behavior.

## MOB-AI-001 Task 5CP - non-generated NPC Type 23 timer (23/10/2026)

TMProject 7.69 `TMSrv/Server.cpp:6589-6895` and W2PP
`Server/W2PP/Source/Code/TMSrv/Server.cpp:5522-5827` contain the Type 23
Bestial Aura area attack. In both, the scan exits at `idx >= MAX_USER`
(`6637-6638` / `5568-5569`) before the player attack-target logic, and the final
`ProcessClientMessage` dispatch runs only when `idx < MAX_USER`
(`6893-6894` / `5825-5826`). Thus an NPC does not attack or damage nearby
entities from this branch. It continues to the common `LessAffect` tail:
decrement finite time below `32,400,000`; at `Time <= 0`, clear Type, Time,
Value, and Level, then request `upScore`. W2PP matches the same timer behavior.

The Peace and Combat call sites already recorded for Task 5CO invoke
`ProcessAffect` for these NPC modes in both sources. C# now allows Type 22 and 23
through the same score-only helper for persistent NPCs with no generation index,
no leader, and no AutoTrade state. Existing Peace shard visits and fourth-visit
Combat affect cadence remain in place. Generated NPC and summon Type 23 area
attack implementations are unchanged.

- [x] Engine tests cover Type 23 countdown and expiry in Peace and Combat, including a Combat NPC without a target; all affect bytes clear, HP stays unchanged, no attack is synthesized, and expiry requests score only. RED reproduced before implementation; final Engine.Tests `495/495`.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-incremental`: 0 errors and one existing CS8604 at `AttackDivineBlessingTests.cs:256`.
- [x] SDK evidence: .NET 10.0.401; `dotnet --info` exit 0.
- [x] TCP loopback `--mob-ai-smoke` passed `NON_GENERATED_NPC_TYPE22_SMOKE_PASS` and `NON_GENERATED_NPC_TYPE23_SMOKE_PASS`, each score-only and view-scoped; the final `MOB_AI_SMOKE_PASS` marker is present.
- [x] Handoff, progress, and backlog updated in place. No package/publish/deploy, authenticated Host, visual client E2E, Site/production change, commit, or push.

The smoke uses synthetic WorldHub state and TCP peers; authenticated Host and
visual-client behavior are still unverified.

Independent review approved this slice with no critical or important findings.
One formatting-only indentation issue on the Type 23 constants in
`WorldHub.cs` is deferred. A generated/leader/AutoTrade exclusion regression was
suggested as optional follow-up; those paths remain outside this slice.

## MOB-AI-001 Task 5CQ - non-generated NPC Type 17 Life Aura (23/10/2026)

TMProject 7.69 `TMSrv/Server.cpp:6189-6206` and W2PP
`Server/W2PP/Source/Code/TMSrv/Server.cpp:5124-5141` agree on the mob Type 17
branch: add `AffectLevel / 2 + AffectValue`, clamp HP to `[1, MaxHp]`, record
the signed delta, set the HP relay flag, then enter `LessAffect`. The shared
tail decrements finite time below `32,400,000`; expiry clears Type, Time, Value,
and Level and requests a score refresh. The outer relay sends `SetHpDam` before
`UpdateScore`. The Peace and Combat `ProcessAffect` callsites are the same ones
recorded in Task 5CO (`ProcessSecMinTimer.cpp:1922,1938,2131` in 7.69 and
`1840,1854,2047` in W2PP).

`WorldHub.TryProcessNonGeneratedNpcTimerAffects` now handles Type 17 alongside
22/23 for persistent live NPCs with `GenerateIndex < 0`, no leader, and no
AutoTrade state. Peace uses its existing shard visit; Combat keeps its
four-opportunity cadence. Each Type 17 opportunity applies the source heal,
updates the MOB snapshot, and returns the signed delta for `SetHpDam`, including
a zero delta at capped HP as the source `regen` flag does. `UpdateScore` is
requested only when HP changes or the affect expires. Recipients are captured
at the NPC view under the WorldHub lock. Generated NPC, summon, player,
attack, and movement paths are unchanged.

TDD RED reproduced the unprocessed Type 17 in the Peace and Combat unit tests;
the loopback RED showed a non-generated NPC remaining at 500 HP with its affect
uncleared. GREEN Engine.Tests `495/495` covers Peace max-HP clamp, expiry, the
Combat fourth opportunity without a target, and no synthesized attacks. The
three-peer TCP smoke passes
`NON_GENERATED_NPC_TYPE17_SMOKE_PASS hp-score=true view-scoped=true`: in-view
peers receive the 7.69 `SetHpDam` followed by `UpdateScore`, and the outside
peer receives no frame. Full no-incremental solution build: 0 errors and the
pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exits 1 because the runner cannot open Service Control Manager.
The smoke uses synthetic WorldHub state and TCP peers; authenticated Host and
visual client behavior remain unverified. No package/publish/deploy,
Site/production change, commit, or push.

Independent read-only review approved the slice with no critical or important
findings. Two minor test-coverage suggestions are deferred: a full-HP Type 17
tick with remaining duration should assert zero-delta `SetHpDam` without
`UpdateScore`, and a multi-slot case should characterize multiple Type 17
records/mixed supported affects. The implementation's HP, timer, and slot-loop
behavior was found consistent with both source trees.

## MOB-AI-001 Task 5CR - non-generated NPC Poison Type 20 timer (23/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/Server.cpp:6210-6253` and W2PP
`Server/W2PP/Source/Code/TMSrv/Server.cpp:5145-5187` agree on the Type 20 mob
branch: start `AffectValue` at 100, subtract signed
`LearnedSkill / MAX_USER`, divide by 10 using integer truncation, then compute
`HpLose = (int)((100 - AffectValue) / 100.0)`. Mob `Perda` is
`10 * (100 - LearnedSkill / MAX_USER)`. HP is multiplied by `HpLose`, raised
to `Hp - Perda` if needed, then clamped to `[1, MaxHp]`; `Damage` is the signed
delta and `regen=1` requests the HP relay on every tick. Finite time below
`32,400,000` decrements once; expiry clears all eight bytes and requests score
refresh. The two sources differ only in fallback player cap (`500` in 7.69,
`1000` in W2PP), which is overwritten by the mob branch.

`WorldHub.TryProcessNonGeneratedNpcTimerAffects` now processes Type 20 together
with Types 17/22/23 for eligible persistent live NPCs with `GenerateIndex < 0`,
no leader, and no AutoTrade state. It uses the existing Peace shard visit or
fourth Combat opportunity, preserves the signed delta, requests SetHpDam for
each Poison tick, and requests UpdateScore only when HP changes or the affect
expires. The Host relay sends SetHpDam before UpdateScore to recipients captured
at the NPC view. No attack, movement, generated-NPC, summon, player, or mount
linkage behavior changed.

TDD RED reproduced the missing Peace Type 20 tick and loopback HP/expiry.
GREEN Engine.Tests `495/495` covers a LearnedSkill-derived 600 HP cap,
countdown/expiry, signed relay flags, HP floor at 1, a zero-delta tick without
score refresh before expiry, and targetless Combat fourth-opportunity cadence
with no synthesized attack. Independent read-only review found no critical or
important issues; its HP-floor/zero-delta coverage suggestion was addressed and
confirmed in follow-up review. The three-peer TCP smoke passes
`NON_GENERATED_NPC_TYPE20_SMOKE_PASS hp-score=true view-scoped=true`: the two
in-view peers receive SetHpDam then UpdateScore, and the outside peer receives
no frame. Full no-incremental solution build: 0 errors with the pre-existing
CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info`
completed with exit 0 in final validation. The smoke uses
synthetic WorldHub state and TCP peers; authenticated Host and visual client
behavior remain unverified. No package/publish/deploy, Site/production change,
commit, or push.

## MOB-AI-001 Type 24 applicability audit for ordinary NPCs (23/10/2026)

The Type 24 lifetime affect has no ordinary persistent-NPC producer in either
server reference. TMProject 7.69 assigns `Affect[0].Type = 24` only inside the
no-item summon creation path (`Server.cpp:4000-4008`); W2PP has the same path
(`Server.cpp:3193-3201`). The normal summon receives 20 timer units, while
SummonID 28-37 receives the persistent sentinel `2,000,000,000`.

Both `ProcessAffect` implementations decrement finite timers and call
`DeleteMob(idx, 3)` when a Type 24 affect expires for any mob slot
(TMProject `Server.cpp:6913-6919`; W2PP `Server.cpp:5843-5850`). This generic
expiry predicate does not establish an ordinary-NPC producer: the only Type 24
assignment found in either `TMSrv/Server.cpp` is the summon path. The C# port
already handles this actual producer through the RouteType 5 summon Peace and
Combat lifetime timers, including targetless Combat expiry. Therefore no new
Type 24 branch is added to the ordinary non-generated NPC timer. Next, inventory
other source-backed affect producers that can reach ordinary NPCs before choosing
another timer slice.

## MOB-AI-001 Task 5CS - non-generated NPC Type 32 timer (23/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:1591-1617` and W2PP
`_MSG_Attack.cpp:1126-1152` special-case skill 47 `Cancelamento` to clear
Type 19. When Type 19 is absent, the generic skill path continues to
`SetAffect` (`TMProject:1727`, `W2PP:1228`). TMProject SkillData row 47
(`Servidor/Server/TMSrv/run/skilldata.csv:48`) defines AffectType 32. Both
`SetAffect` implementations reject `conn > MAX_USER` (TMProject
`Server.cpp:10359`; W2PP `Server.cpp:9209`); this task does not widen that
producer rule.

Type 32 itself has no special `ProcessAffect` branch in either reference. It
falls through the common `LessAffect` tail: time below 32,400,000 decrements;
expiry clears Type/Value/Level/Time and requests score refresh
(TMProject `Server.cpp:6899-6920`; W2PP `Server.cpp:5831-5851`). The common
timer helper already applied this behavior to players, generated NPCs, and
RouteType 5 summons. This task extends the eligible ordinary persistent NPC
helper only, keeping its existing Peace shard and fourth Combat opportunity
cadence. It changes no HP, attack, movement, or despawn state; expiry relays
only UpdateScore to the captured view.

TDD RED reproduced the ignored affect in Peace and targetless Combat Engine
tests and in the three-peer loopback smoke. GREEN Engine.Tests `495/495`.
The smoke passes `NON_GENERATED_NPC_TYPE32_SMOKE_PASS score-only=true
view-scoped=true`: both in-view peers receive UpdateScore after expiry, while
the outside peer receives no frame. Full `dotnet build WydCdk.Engine.slnx
--no-restore --no-incremental --verbosity minimal` completed with 0 errors and
the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` identified the SDK and exited 1 because the runner could not
open Service Control Manager. Smoke evidence is synthetic WorldHub plus local
TCP peers; authenticated Host and visual client remain unverified. No
package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DU - Ligacao Espectral Type 37 ForceDamage (26/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv:82` defines skill 81
`Ligacao_Espectral`: `TargetType=0`, `InstanceType=0`, `AffectType=37`,
`AffectValue=0`, CSV `AffectTime=60`, `Aggressive=0`, and `MaxTarget=1`. The
player self-cast matcher accepts only skill 81 with this metadata. Recast
refreshes one Type 37 record; the fixture with Special3=20 expires after 19
timer ticks.

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:3328-3340` composes its
local `special2` from `MOB.CurrentScore.Special[2]`, `EF_SPECIAL3`, and
`EF_SPECIALALL`, then caps the value at 255. The score field maps to C#
`CurrentScore.Special3`, which already includes the composed item abilities.
For each active Type 37 record,
`Servidor/Source/Code/TMSrv/Basedef.cpp:3538-3540` adds `special2` to
`ForceDamage`. W2PP `Source/Code/Basedef.cpp:4440-4441` agrees. Type 37 does not
change the serialized score; the C# contribution is calculated in the shared
ForceDamage helper used by player physical and skill damage paths.

- [x] TDD RED/GREEN: Engine.Tests `521/522` before and `522/522` after. Coverage
  verifies per-record Special3 arithmetic, skill 81 cast and recast, 19-tick
  lifetime, +20 ForceDamage on the physical hit, and 0 contribution after
  expiry.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` and
  E2E project build: 0 warnings and 0 errors. SDK 10.0.401; `dotnet --info`
  identified the SDK and exited 1 because the runner cannot open Service
  Control Manager.
- [x] Three-peer TCP `--mob-ai-smoke` passed
  `SPECTRAL_LINK_PLAYER_TYPE37_SMOKE_PASS force-damage=20
  caster-affect-only=true attack=view-scoped=true` and global
  `MOB_AI_SMOKE_PASS`. The caster received Type 37 UpdateScore/UpdateAffect;
  AttackOne with the +20 damage reached the caster and in-view observer, with
  no frame to the outside peer.
- [x] No REA capture was needed: TMProject 7.69, W2PP, and the existing
  UpdateScore/UpdateAffect adapters settle the behavior. Synthetic loopback
  only; no authenticated Host or visual E2E for skill 81. No package/publish/
  deploy, Site/production change, commit, or push.

Continue the full SkillData 7.69 inventory and compare cast gates, targeting,
mana, damage/heal, affects/ticks, score projection, timers, and wire against
W2PP and C#. Use REA when source and existing wire evidence do not settle the
behavior; do not treat this slice as completing all skill 81 client E2E.

## MOB-AI-001 Task 5DT - Olhos de Aguia Type 36 player score and RSV wire (26/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv:93` defines skill 92
`Olhos_de_aguia` with `TargetType=0`, `InstanceType=0`, `AffectType=36`,
`AffectValue=1`, CSV `AffectTime=12`, `Aggressive=0`, and `MaxTarget=1`. The
loader stores three ticks. Skill 92 reads Special3; Special3=20 gives Level 20,
delay 120, and four affect ticks in the timer fixture.

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:3666-3667` adds
`RSV_DRAIN=0x0002` only for active Type 36 with
`Equip[6].EF_WTYPE=41`. W2PP `Source/Code/Basedef.cpp:4437-4438` sets the flag
for every Type 36, omitting the weapon gate; the C# port follows the 7.69
contract. `LegacyCurrentScoreMath` projects Type 36 from the affect snapshot
and decoded slot-6 ItemList record. The active player cast matcher admits only
skill 92 with `InstanceType=0`, `Aggressive=0`, and `AffectType=36`. The score
rebuild preserves current HP/MP, recast retains one record, and the existing
timer clears the affect and restores baseline RSV on expiry. The caster receives
the resulting `UpdateScore` and `UpdateAffect`.

- [x] TDD RED reproduced missing `RSV_DRAIN` (`520/521`); GREEN Engine.Tests
  `521/521`, covering active Type 36 plus weapon type 41, rejected type 40,
  absent affect, cast, HP/MP, recast, four-tick expiry, and baseline restore.
- [x] Full solution build `dotnet build WydCdk.Engine.slnx --no-restore
  --no-incremental`: 0 warnings and 0 errors. E2E project build passed 0/0.
  SDK 10.0.401; `dotnet --info` identified the SDK and exited 1 because the
  runner cannot open Service Control Manager.
- [x] Three-peer TCP `--mob-ai-smoke` passed
  `EAGLE_EYES_PLAYER_TYPE36_SMOKE_PASS drain-affect-caster-only=true`, the
  Type 26/28 markers, and global `MOB_AI_SMOKE_PASS`; only caster received the
  score/affect frames. The full smoke's Fairy 1726 fixture now has DEX 11 vs 10
  to make its 7.69 miss rate zero and the run deterministic; gameplay is
  unchanged.
- [x] No REA capture was needed: TMProject 7.69, W2PP, and the existing 7.69
  UpdateScore/UpdateAffect adapters settle this slice. Synthetic loopback only;
  no authenticated Host or visual E2E for skill 92. No package/publish/deploy,
  Site/production change, commit, or push.

Continue with the full SkillData inventory and compare cast gates, targeting,
mana, damage/heal, affects/ticks, score projection, timers, and wire relay
against W2PP and C#. Use REA when source and existing wire evidence do not settle
the behavior; do not treat this slice as completing all of skill 92.

## MOB-AI-001 Task 5DS - Evasao Aprimorada Type 26 player score and RSV wire (26/10/2026)

TMProject 7.69 `Servidor/Server/TMSrv/run/SkillData.csv:90` defines skill 89
`Evasao_Aprimorada` with `TargetType=0`, `InstanceType=0`, `AffectType=26`,
`AffectValue=1`, CSV `AffectTime=30`, `Aggressive=0`, and `MaxTarget=1`. The
loader stores seven ticks. Skill 89 reads Special4; Special4=20 gives Level 20,
delay 120, and nine affect ticks.

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:3572-3573` sets
`RSV_PARRY=0x0008` for Type 26. W2PP `Server/W2PP/Source/Code/Basedef.cpp:4418-4419`
agrees. The C# affect composer had no Type 26 branch, and the online player
effect path had no exact skill matcher. It now adds the flag for active Type 26
and admits only skill 89 with `InstanceType=0`, `Aggressive=0`, and
`AffectType=26`; score projection preserves current HP/MP, recast keeps one
record, and expiry recomputes the baseline RSV.

- [x] TDD RED reproduced missing `RSV_PARRY` (`519/520`); GREEN Engine.Tests
  `520/520`, including direct score projection, cast, current HP/MP, recast,
  nine-tick expiry, and baseline restoration.
- [x] Full solution build `dotnet build WydCdk.Engine.slnx --no-restore
  --no-incremental`: 0 warnings and 0 errors. E2E project build passed 0/0.
  SDK 10.0.401; `dotnet --info` identified the SDK and exited 1 because the
  runner cannot open Service Control Manager.
- [x] Three-peer TCP `--mob-ai-smoke` passed
  `EVASION_PLAYER_TYPE26_SMOKE_PASS parry-affect-caster-only=true` and global
  `MOB_AI_SMOKE_PASS`; only the caster received UpdateScore/UpdateAffect.
- [x] No REA packet capture was needed: TMProject 7.69, W2PP, and the existing
  7.69 wire adapters settle the score and affect fields. Synthetic loopback
  only; no authenticated Host or visual E2E for skill 89. No package/publish/
  deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DR - Invisibilidade Type 28 player score and RSV wire (26/10/2026)

TMProject 7.69 `Servidor/Server/TMSrv/run/SkillData.csv:96` defines skill 95
`Invisibilidade` with `TargetType=0`, `InstanceType=0`, `AffectType=28`,
`AffectValue=1`, CSV `AffectTime=1`, `Aggressive=0`, and `MaxTarget=1`. The
loader stores zero ticks after integer division by four. Skill 95 reads
Special4; Special4=20 gives Level 20, delay 120, and one affect tick.

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:3582-3583` sets
`RSV_HIDE=0x0010` for Type 28. W2PP `Server/W2PP/Source/Code/Basedef.cpp:4424-4425`
agrees. The C# affect composer already knew Type 28, and the mob-AI input already
derived `Hidden` from `MOB.Rsv`; the active player skill path did not request a
score rebuild. Its exact matcher now accepts skill 95 with `InstanceType=0`,
`Aggressive=0`, and `AffectType=28`, then projects `RSV_HIDE` while preserving
current HP/MP. Recast keeps one Type 28 record and expiry recomputes the baseline
RSV. The Host sends the resulting `UpdateScore` and `UpdateAffect` to the caster.

- [x] TDD RED reproduced the missing online projection (`518/519`); GREEN
  Engine.Tests `519/519`, covering cast, RSV, current HP/MP, recast, one-tick
  expiry, and baseline restoration.
- [x] Full solution build `dotnet build WydCdk.Engine.slnx --no-restore
  --no-incremental`: 0 warnings and 0 errors. E2E project build also passed
  0/0. SDK 10.0.401; `dotnet --info` identified the SDK and exited 1 because the
  runner cannot open Service Control Manager.
- [x] Three-peer TCP `--mob-ai-smoke` passed
  `INVISIBILITY_PLAYER_TYPE28_SMOKE_PASS hide-affect-caster-only=true` and the
  global `MOB_AI_SMOKE_PASS`; only the caster received the score/affect frames.
  This is synthetic WorldHub/loopback evidence, not authenticated Host or
  visual-client E2E.
- [x] No REA packet capture was needed: the 7.69 and W2PP source, the target
  affect composer, and the 7.69 UpdateScore/UpdateAffect adapters settle this
  slice. No package/publish/deploy, Site/production change, commit, or push.

Continue with the full SkillData inventory and compare cast gates, targeting,
mana, damage/heal, affects/ticks, score projection, timers, and wire relay
against W2PP and C#. Use REA when source and existing wire evidence do not settle
the behavior; do not treat this slice as completing all of skill 95.

## MOB-AI-001 Task 5DQ - Encantar Gelo Type 27 player score and RSV wire (26/10/2026)

TMProject 7.69 `Servidor/Server/TMSrv/run/SkillData.csv:76` defines skill 75
`Encantar_Gelo` with `TargetType=0`, `InstanceType=0`, `AffectType=27`,
`AffectValue=1`, CSV `AffectTime=60`, `Aggressive=0`, and `MaxTarget=1`. The
loader stores 15 ticks. For skill 75, `GetSkillSpecial` reads `Special2`; the
fixture's Special2=20 yields affect Level 20, delay 120, and 19 ticks.

TMProject 7.69 `Servidor/Source/Code/TMSrv/Basedef.cpp:3658-3659` adds
`RSV_FROST` only when the active affect is Type 27 and `Equip[6]` resolves
`EF_WTYPE=101`. W2PP `Source/Code/Basedef.cpp:4421-4422` comments out the weapon
gate and sets the bit for every Type 27. The C# port follows 7.69: the score
stage checks the full affect snapshot and decoded ItemList weapon type from slot
6. The explicit online matcher admits only skill 75 with the expected
`InstanceType`, `Aggressive`, and `AffectType`; recast refreshes the affect, and
expiry recomputes the score to restore baseline RSV. Current HP/MP are preserved.

The loopback check exposed a second wire gap: W2PP's packed UpdateScore payload
does not contain MOB.Rsv, and the adapter therefore emitted zero in the 7.69
target-only RSV field. The adapter now accepts authoritative RSV separately;
Host, player/NPC affect, mob-AI, and Battle Royale score relays pass it from the
full MOB snapshot. Engine.Tests verifies the adapter field and encoded payload.

- [x] TDD RED observed the missing Type 27 score projection; GREEN Engine.Tests
  `518/518`, including wrong weapon, no affect, cast, recast, 19-tick expiry,
  baseline RSV restoration, current HP/MP preservation, and adapter RSV mapping.
- [x] `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` passed with
  0 warnings and 0 errors. E2E is included in that build. SDK 10.0.401;
  `dotnet --info` exited 1 because the runner cannot open Service Control Manager.
- [x] Three-peer TCP `--mob-ai-smoke` passed
  `ICE_ENCHANT_PLAYER_TYPE27_SMOKE_PASS score-affect-caster-only=true` and the
  global `MOB_AI_SMOKE_PASS`; only the caster received UpdateScore and
  UpdateAffect. This is synthetic WorldHub/loopback evidence, not authenticated
  Host or visual-client E2E.
- [x] No packet capture was needed for this slice: the 7.69 source and wire
  adapters establish the affect, score bit, and target packet fields. No
  package/publish/deploy, Site/production change, commit, or push.

Next, inventory every populated 7.69 SkillData row and compare cast gates,
targeting, mana, damage/heal, affects/ticks, score projection, timers, and wire
relay against W2PP and the C# implementation. Work the gaps in bounded slices;
use REA on captures or binaries when source and existing wire evidence do not
settle the behavior.

## MOB-AI-001 Task 5DP - Troca de Espiritos Type 38 score projection for player self-targets (26/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv:88` defines skill 87
`Troca_de_Espiritos` with `TargetType=0`, `InstanceType=0`, `AffectType=38`,
`AffectValue=0`, CSV `AffectTime=14`, `Aggressive=0`, and `MaxTarget=1`. The
loader converts duration 14 to three ticks. TMProject 7.69
`Servidor/Source/Code/Basedef.cpp:3628` and W2PP
`Server/W2PP/Source/Code/Basedef.cpp:4443` agree on Type 38:
`mana = MaxMp / 2; MaxHp += mana; MaxMp -= mana`.

The C# login score composer already applied Type 38, but the online player
self-skill matcher did not rebuild score after the affect was set. `WorldHub`
now matches skill 87 only with `InstanceType=0`, `Aggressive=0`, and
`AffectType=38`, reusing the existing projector while preserving live HP/MP.
Recast does not accumulate the stat transfer; the generic player timer clears
the affect and restores the baseline score on expiry. The regression test is
`tests/WydCdk.Engine.Tests/AttackPlayerSpiritExchangeAffectTests.cs`.

- [x] TDD RED/GREEN: Engine.Tests `516/517` before and `517/517` after. RED applied Type 38 while MaxHp/MaxMp stayed unchanged; coverage checks transfer, HP/MP preservation, recast, and expiry restoration.
- [x] E2E project build with `--no-restore --no-incremental -m:1 -p:UseSharedCompilation=false`: 0 warnings/errors. Three-peer TCP `--mob-ai-smoke` passed `SPIRIT_EXCHANGE_PLAYER_TYPE38_SMOKE_PASS score-affect-caster-only=true`; only the caster received UpdateScore/UpdateAffect, and the in-view observer/outside peer received no frames. Global `MOB_AI_SMOKE_PASS` completed.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false`: 0 warnings and 0 errors. SDK 10.0.401; `dotnet --info` identified the SDK and exited 1 because the runner could not open Service Control Manager.
- [x] Synthetic loopback only; no authenticated Host or visual-client E2E for skill 87. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DO - Explosao Eterea Type 31 score projection for player self-targets (26/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv:86` defines skill 85
`Explosao_Eterea` with `TargetType=0`, `InstanceType=0`, `AffectType=31`,
`AffectValue=150`, CSV `AffectTime=30`, `Aggressive=0`, and `MaxTarget=1`.
The loader converts the duration to seven ticks. Skill 85 selects
`CurrentScore.Special3`; with Special3=50, the generic handler passes delay
150 and `SetAffect` stores `(7 + 1) * 150 / 100 = 12` ticks.

TMProject 7.69 `_MSG_Attack.cpp:1667-1737` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1205-1238` use the generic
effect tail to apply `SetAffect`, then rebuild and send CurrentScore when the
affect changes. Their score implementations agree on Type 31:
`MOB.CurrentScore.Ac += Level / 2 + Value` (TMProject
`Servidor/Source/Code/Basedef.cpp:3636-3640`; W2PP
`Server/W2PP/Source/Code/Basedef.cpp:4430-4434`). At level 50/value 150 the
score adds 175 AC.

The C# score composer already handled Type 31, but the effect-only player skill
path did not rebuild score for skill 85. `WorldHub` now matches skill 85 only
when `InstanceType=0`, `Aggressive=0`, and `AffectType=31`, then reuses the login
projector while preserving live HP/MP. Recast refreshes the existing Type 31
record without stacking AC; the generic player timer clears the affect and
restores the baseline score on expiry.

- [x] TDD RED/GREEN: Engine.Tests `515/516` before and `516/516` after. RED showed Type 31 present while AC remained 100 rather than the expected 275. Coverage includes affect value/level/time, live HP/MP, recast, and expiry restoration.
- [x] Three-peer TCP `--mob-ai-smoke` passed `GOLDEN_SHIELD_PLAYER_TYPE31_SMOKE_PASS score-affect-caster-only=true`; UpdateScore and UpdateAffect reached only the caster. The in-view observer and outside peer received no frames; global `MOB_AI_SMOKE_PASS` completed.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental -m:1 -p:UseSharedCompilation=false`: 0 warnings and 0 errors. SDK 10.0.401; `dotnet --info` identified the SDK and exited 1 because the runner could not open Service Control Manager.
- [x] Synthetic loopback only; no authenticated Host or visual E2E for skill 85. No package/publish/deploy, Site/production change, commit, or push.

Evidence: `Backup/Build/GoldenShieldType31-20261026-01/`.

## MOB-AI-001 Task 5DN - Imunidade Type 19 score projection for player self-targets (26/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv` row 76 defines skill
`Imunidade` with `TargetType=0`, `InstanceType=0`, `AffectType=19`,
`AffectValue=15`, CSV `AffectTime=60`, `Aggressive=0`, and `MaxTarget=1`.
TMProject `Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1667-1737` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1205-1238` share the generic buff
tail: apply `SetAffect`, then call `GetCurrentScore`/`SendScore` after an affect
change. TMProject `Servidor/Source/Code/Basedef.cpp:3678-3680` and W2PP
`Server/W2PP/Source/Code/Basedef.cpp:4211-4212` agree that Type 19 sets
`MOB.Rsv |= RSV_BLOCK`.

`LegacyCurrentScoreMath` already adds `BlockRsvFlag=0x0080` when Type 19 is
present. The missing path was the effect-only player-skill allowlist in
`WorldHub`: it did not request a score rebuild for skill 76. C# now matches
skill 76 only when `InstanceType=0`, `Aggressive=0`, and `AffectType=19`, then
reuses the existing player login projector while preserving live HP/MP. The
effect refresh does not add a duplicate record; the generic player affect timer
clears Type 19 and rebuilds the baseline RSV on expiry.

- [x] TDD RED/GREEN: Engine.Tests `514/515` before and `515/515` after. RED showed the affect slot set while `MOB.Rsv` remained `0x0000`; the expected Type 19 projection is `0x0080`. Coverage includes value/level, current HP/MP, recast, and expiry restoration.
- [x] E2E project build succeeded with 0 warnings/errors using `-m:1`. Three-peer TCP `--mob-ai-smoke` passed `IMUNIDADE_PLAYER_TYPE19_SMOKE_PASS score-affect-caster-only=true`; only the caster received UpdateScore/UpdateAffect. Observer and outside peers received no frame; the global `MOB_AI_SMOKE_PASS` completed.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-incremental -m:1`: 0 errors and one pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exited 1 because the runner could not open Service Control Manager.
- [x] Synthetic loopback only, not authenticated Host or visual-client E2E. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DM - Protecao Elemental Type 25 score projection for player self-targets (26/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv` row 53 defines
`Protecao_Elemental` with `TargetType=0`, `InstanceType=0`, `AffectType=25`,
`AffectValue=10`, CSV `AffectTime=60`, `Aggressive=0`, and `MaxTarget=1`.
`BASE_InitializeSkill` divides AffectTime by four, yielding 15 runtime ticks.
Skill 53 reads `CurrentScore.Special2`; Special2=50 gives delay 150 and
`SetAffect` stores `(15 + 1) * 150 / 100 = 24` ticks on the first cast. The
generic attack tail invokes `SetAffect`, then `GetCurrentScore`/`SendScore` when
the affect changes.

Compiled TMProject 7.69 `Servidor/Source/Code/Basedef.cpp` Type 25 adds
`Level / 2 + Value` to AC and `(Value + Level / 4) / 10` to each of the four
resistances: Holy, Thunder, Fire, and Ice. At affect level 50 and value 10,
this gives +35 AC and +2 to each resistance. W2PP
`Server/W2PP/Source/Code/Basedef.cpp` applies only Fire/Thunder/Ice resistance;
it does not change AC or Holy resistance. The selected port contract follows
TMProject 7.69.

C# now recognizes only skill 53 with `InstanceType=0`, `Aggressive=0`, and
`AffectType=25` in the effect-only player score rebuild. It reuses the existing
Type 25 login projector and preserves live HP/MP. Recast replaces the affect
without stacking score, and the existing player timer clears Type 25 and
restores baseline AC/resistances at expiry.

- [x] TDD RED/GREEN: Engine.Tests `513/514` before and `514/514` after. The regression covers value/level/time, AC and all four resistances, HP/MP, recast, and expiry; RED left AC at 100 before the runtime matcher.
- [x] Three-peer TCP `--mob-ai-smoke` passed `PROTECAO_ELEMENTAL_PLAYER_TYPE25_SMOKE_PASS score-affect-caster-only=true`; UpdateScore/UpdateAffect reached only the caster. Observer and outside peers received no frame; global `MOB_AI_SMOKE_PASS` completed.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` after the test-only nullable-flow fix: 0 warnings and 0 errors; Engine.Tests `515/515`. The `CS8604` at `AttackDivineBlessingTests.cs:256` is fixed with an explicit null guard and separate assertion. SDK 10.0.401; the latest `dotnet --info` identified the SDK but exited 1 because the runner could not open Service Control Manager.
- [x] Authenticated Host loopback passed `HOST_AUTHENTICATED_TYPE25_PASS auth=account-file host=127.0.0.1 skill=53 score-before-affect=true generated-fixture=true external-database=none`; account, secure code, character login, and `UpdateScore`-before-`UpdateAffect` were exercised through the real Host.
- [x] Visual fixture class invariant now matches the client's face-index rule (`Equip[0].sIndex=32` => class 2). The isolated client connected to loopback, authenticated, and entered the world as `PROTECT25` / `BeastMaster`.
- [x] Visual skill 53 E2E passed in the isolated client build with the local account-file Host. The successful hotbar cast sent `0x039D` (72 bytes), skill 53, self-target ID 1; Host logs show attack resolution and MP=869. Decoded `0x0336` is AC 135, HP 1000, MP 869, Resist 2/2/2/2, with Type 25; `0x03B9` carries Type 25/Level 50/Value 10/Time 24. The visible status panel matched these values.
- [x] Root cause and asset correction: source `STRUCT_SPELL` is 96 bytes and `BASE_ReadSkillBin` reads 248 records (23,808 bytes); the packaged 25,796-byte file used 104-byte records plus a four-byte trailer, leaving skill 53 `MaxTarget=0` at the client stride. Repacked each row to its first 96 XOR-encoded bytes. The corrected 23,808-byte hash `5239C6C702B76E85BEEB3F71374D0D75D9852E408AE70729D05800D94E32EFB1` now occupies the root plus Interface0..3 in `Client/7670`; original hash `9A294F0C1194BB3D0342F7F6146ACBAAFE50C07F6C7D5AB053F095936FF74D5C` is preserved under `Backup/Build/Type25AuthE2E-20261026-05/`. The fixture's initial Field 15,15 had no map files; the resaved Field 16,16 loaded correctly. E2E used isolated client hash `51BE30D3E55231D300ABD7BD41E046690DD43EE6A4DAF5F9CD9AF4A92EFF03E2`; active `Client/7670/WYD.exe` has different unverified provenance and was not exercised.
- [x] Local-only validation. No package/publish/deploy, Site/production change, commit, or push.

Evidence: `Backup/Build/Type25AuthE2E-20261026-01/` contains build, Engine.Tests,
and authenticated-Host outputs; `Backup/Build/Type25AuthE2E-20261026-05/`
contains visual Host output, fixture, and original/corrected SkillData files.

## MOB-AI-001 Task 5DL - Meditacao Type 21 score projection for player self-targets (25/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv` row 77 defines skill
`Meditacao` with `TargetType=0`, `InstanceType=0`, `AffectType=21`,
`AffectValue=15`, CSV `AffectTime=60`, `Aggressive=0`, and `MaxTarget=1`.
`BASE_InitializeSkill` divides AffectTime by four, yielding 15 runtime ticks.
Skill 77 reads `CurrentScore.Special2`; with Special2=50, Exec_MSG_Attack passes
delay 150 and `SetAffect` stores `(15 + 1) * 150 / 100 = 24` ticks on the
first cast. The generic attack tail calls `SetAffect`, then
`GetCurrentScore`/`SendScore` when the affect changes.

Compiled TMProject `Servidor/Source/Code/Basedef.cpp` Type 21 stage applies
`AC -= Level / 3 + 10` and `DamageMulti += Level / 25 + Value`. W2PP
`Server/W2PP/Source/Code/Basedef.cpp` uses `/10` for the damage multiplier;
the port follows the selected 7.69 formula. The existing login projector
already composes Type 21 from the stored affect fields.

C# now recognizes skill 77 only when `InstanceType=0`, `Aggressive=0`, and
`AffectType=21` in the effect-only player score rebuild. It reuses the Type 21
stage and login projector while preserving live HP/MP. The player timer clears+the affect and restores baseline AC/damage multiplier at expiry. Recast refreshes+the source-derived 24-tick duration without stacking the score delta.

- [x] TDD RED/GREEN: Engine.Tests `512/513` before and `513/513` after; covers affect fields, AC and multiplier projection, resource preservation, recast, and expiry restoration.
- [x] E2E project build passed with 0 warnings/errors. Three-peer TCP `--mob-ai-smoke` passed `MEDITACAO_PLAYER_TYPE21_SMOKE_PASS score-affect-caster-only=true`; caster received UpdateScore/UpdateAffect, observer and outside peer received no frame.
- [x] Full no-incremental solution build: 0 errors and one pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` identified the SDK but exited 1 because the runner could not open Service Control Manager.
- [x] Synthetic loopback only, not authenticated Host or visual-client E2E. The smoke entrypoint catches exceptions and reports them to the console. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DK - Toque de Athena Type 15 score projection for player self-targets (25/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv` row 45 defines skill
`Toque_de_Athena` with `TargetType=2`, `InstanceType=0`, `AffectType=15`,
`AffectValue=7`, CSV `AffectTime=60`, `Aggressive=0`, and `MaxTarget=1`.
`BASE_InitializeSkill` divides AffectTime by four, yielding 15 runtime ticks.
Skill 45 reads `CurrentScore.Special4`; with Special4=50, Exec_MSG_Attack passes
delay 150 and `SetAffect` stores `(15 + 1) * 150 / 100 = 24` ticks on the
first cast. The generic attack tail calls `SetAffect` and then
`GetCurrentScore`/`SendScore` when the affect changes.

Compiled TMProject `Servidor/Source/Code/Basedef.cpp:3861-3890` applies
`Level / 10 + Value` to each of `Special[0..3]`, capping every value at 255.
The compiled `TMSrv.vcxproj` uses this parent `Basedef.cpp`. W2PP
`Server/W2PP/Source/Code/Basedef.cpp:4067-4095` uses the same addition but caps
Special1 at 200; the target port keeps the 7.69 cap.

C# now recognizes skill 45 with `InstanceType=0`, `Aggressive=0`, and
`AffectType=15` in the effect-only player score rebuild, reusing the existing
Type 15 stage and login projector while preserving current HP/MP. The generic
player timer clears the affect and restores baseline Specials at expiry. On
recast after five ticks, the caster's buffed Special4=62 becomes the new stored
Level; delay 162 yields 25 ticks and the rebuilt score becomes Special4=63.
This uses the source's recast level/duration rule without stacking the delta on
the already-projected CurrentScore.

- [x] TDD RED/GREEN: Engine.Tests `511/512` before and `512/512` after; covers affect fields, Type 15 projection, resource preservation, recast, and expiry restoration.
- [x] E2E project build passed with 0 warnings/errors. Three-peer TCP `--mob-ai-smoke` passed `TOQUE_DE_ATHENA_PLAYER_TYPE15_SMOKE_PASS score-affect-caster-only=true`; caster received UpdateScore/UpdateAffect, observer and outside peer received no frame.
- [x] Full no-incremental solution build: 0 errors, one pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exit 0.
- [x] The `--mob-ai-smoke` entrypoint catches exceptions and prints `FAIL` to the console. The sandbox blocked its first loopback attempt with SocketException 10013; the authorized local rerun passed without a dialog. Synthetic loopback only, not authenticated Host or visual-client E2E. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DJ - Escudo Magico Type 11 score projection for player targets (25/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv` row 43 defines skill
`Escudo Magico` with `TargetType=2`, `InstanceType=0`, `AffectType=11`,
`AffectValue=15`, `AffectTime=60`, `Aggressive=0`, and `MaxTarget=1`.
`Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill` divides AffectTime by
four, yielding 15 runtime ticks. Skill 43 reads `CurrentScore.Special4`; with
Special4=50, Exec_MSG_Attack passes delay 150 and `SetAffect` stores
`(15 + 1) * 150 / 100 = 24` ticks.

The compiled `TMSrv/TMSrv.vcxproj:101` includes parent
`Servidor/Source/Code/Basedef.cpp` (and `_MSG_Attack.cpp` at line 174), rather
than the duplicate `TMSrv/Basedef.cpp`. Compiled Type 11 adds
`Level / 5 + Value` to AC (`Basedef.cpp:3834-3839`). The generic attack tail
`TMSrv/_MSG_Attack.cpp:1727-1735` calls `SetAffect` and, after a successful
change, `GetCurrentScore` then `SendScore`. W2PP uses `Level / 3 + Value`
(`Server/W2PP/Source/Code/Basedef.cpp:4024-4028`); this is a deliberate target
divergence and remains comparison evidence only.

C# now recognizes only skill 43 with `InstanceType=0`, `Aggressive=0`, and
`AffectType=11` in the effect-only player score rebuild. It reuses
`LegacyCurrentScoreLoginProjector` and `CalculateMagicShieldAffectStage`,
preserves current HP/MP, refreshes the source duration on recast without
stacking AC, and lets the player affect timer restore baseline AC at expiry.

- [x] TDD RED/GREEN: Engine.Tests `510/511` before and `511/511` after; covers Type 11 value/level/time, AC projection, resource preservation, recast, and expiry restoration.
- [x] E2E project build: 0 warnings/errors. Three-peer TCP `--mob-ai-smoke` passed `ESCUDO_MAGICO_PLAYER_TYPE11_SMOKE_PASS score-affect-caster-only=true`; caster received UpdateScore/UpdateAffect, observer and outside peer received no frame.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-incremental --no-restore`: 0 errors; one pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exited 1 because the runner could not open Service Control Manager.
- [x] Synthetic loopback only, not authenticated Host or visual-client E2E. The sandbox denied the first loopback attempt (SocketException 10013); the project-authorized rerun passed. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DI - Possuido Type 14 score projection for player self-targets (25/10/2026)

TMProject 7.69 `Servidor/Server/Common/skilldata.csv` row 3 defines skill
`Possuido` with `TargetType=0`, `InstanceType=0`, `AffectType=14`,
`AffectValue=10`, `AffectTime=60`, `Aggressive=0`, `MaxTarget=1`, and
`Passive=0`. `Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill` divides
`AffectTime` by four, yielding 15 runtime ticks. `TMSrv/_MSG_Attack.cpp:1727-1735`
is the compiled generic effect tail: it invokes `SetAffect`, then calls
`GetCurrentScore` and `SendScore` when an affect changes. `TMSrv/Server.cpp`
`SetAffect` computes `(AffectTime + 1) * Delay / 100`; skill 3 uses Special2,
so Special2=50 gives Delay 150 and a 24-tick affect.

The compiled project file `TMSrv/TMSrv.vcxproj:101` includes parent
`Servidor/Source/Code/Basedef.cpp` (and `_MSG_Attack.cpp` at line 174), not the
duplicate `TMSrv/Basedef.cpp`. The compiled Type 14 score stage calculates
`value = Level * 3 / 4 + Value`, doubles it for Mortal/Arch and triples it for
other classes, then applies `MaxHp += value * 2` and `Con += value`.
`Server/W2PP/Source/Code/Basedef.cpp` diverges: `MaxHp += value * 22` and
`Con += (Con + value) * 125 / 100`. That W2PP arithmetic remains comparison
evidence and is not used by this task.

C# now admits only skill 3 with `InstanceType=0`, `Aggressive=0`, and
`AffectType=14` in the effect-only player score rebuild. It reuses
`LegacyCurrentScoreLoginProjector` and the existing Type 14 coordinator stage,
preserves live HP/MP, recalculates the 24-tick duration on recast without
stacking MaxHp/Constitution, and lets the player affect timer restore baseline
MaxHp/Constitution at expiry.

- [x] TDD RED/GREEN: Engine.Tests `509/510` before and `510/510` after; covers Mortal/non-Mortal formulas, Type 14 value/level/time, resource preservation, recast, and expiry restoration.
- [x] E2E project build: 0 warnings/errors. Three-peer TCP `--mob-ai-smoke` passed `POSSUIDO_PLAYER_TYPE14_SMOKE_PASS score-affect-caster-only=true`; caster received UpdateScore/UpdateAffect, observer and outside peer received no frame.
- [x] Full `dotnet build WydCdk.Engine.slnx --no-incremental --no-restore`: 0 errors; one pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exited 1 because the runner could not open Service Control Manager.
- [x] Synthetic loopback only, not authenticated Host or visual-client E2E. The sandbox denied the first loopback attempt (SocketException 10013); the project-authorized local rerun passed. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DH - Assalto Type 13 score projection for player self-targets (25/10/2026)

TMProject 7.69 SkillData row 11
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv:12`)
defines `Assalto` as an effect-only self skill with `InstanceType=0`,
`AffectType=13`, `AffectValue=7`, `AffectTime=60`, and `Aggressive=0`. The
loader's existing `/4` conversion stores 15 ticks. Skill 11 reads the caster's
`CurrentScore.Special3`; with Special3=50, the handler delay is 150 and
`SetAffect` stores `(15 + 1) * 150 / 100 = 24` timer ticks.

The compiled TMProject server project (`Servidor/Source/Code/TMSrv/TMSrv.vcxproj:101`)
includes the parent `Servidor/Source/Code/Basedef.cpp`; `_MSG_Attack.cpp:1727-1737`
reaches generic `SetAffect`, `GetCurrentScore`, and `SendScore` for this
successful effect. The compiled score path in
`Servidor/Source/Code/Basedef.cpp:3581-3585` adds `Level / 10 + Value` only to
`DAMAGEMULTI`. The separate `Servidor/Source/Code/TMSrv/Basedef.cpp:3483-3491`
copy contains a Type 13 `/23` formula, but the project file does not compile
that copy. W2PP `Server/W2PP/Source/Code/Basedef.cpp:4036-4051` uses `/10` too,
then additionally increases raw Damage by 15% and reduces MaxHp to 90%; those
W2PP-only mutations are excluded from this 7.69 port.

The player effect-only path now explicitly recognizes skill 11 only when its
instance, aggressive flag, and affect type match the 7.69 row. It reuses the
existing Assault score stage and login projector, preserves current HP/MP, and
lets the existing player timer rebuild the baseline when Type 13 expires.
Recast recomputes the source-derived 24-tick duration; it does not use the
four-tick recast rule reserved for Types 1/3/10.

TDD RED reproduced the missing score projection (`508 passed / 1 failed`);
GREEN Engine.Tests passed `509/509`. The regression checks the Special3-derived
level, Type 13 value, 24-tick duration, multiplier-only score output, preserved
HP/MP, non-stacking recast, and expiry restoration. E2E build passed with 0
warnings/errors. The synthetic three-peer TCP `--mob-ai-smoke` passed
`ASSALTO_PLAYER_TYPE13_SMOKE_PASS score-affect-caster-only=true` and the full
`MOB_AI_SMOKE_PASS`; only the caster received Type 13 UpdateScore/UpdateAffect,
while the in-view observer and outside peer received no frame. Full
no-incremental solution build passed with 0 errors and the pre-existing CS8604
at `tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exited 1 because the runner could not open Service Control
Manager. This is synthetic loopback evidence, not authenticated Host or visual
client E2E. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DG - Enfraquecer Type 10 score projection for player targets (25/10/2026)

TMProject 7.69 SkillData row 51
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv:52`)
defines skill 51 `Enfraquecer` as an aggressive effect-only skill with
`InstanceType=0`, `AffectType=10`, `AffectValue=10`, and CSV `AffectTime=5`.
The existing loader divides the time by four, so this runtime skill has one
tick before applying the handler delay. TMProject 7.69
`Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:1727` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1228` route a successful generic
`SetAffect` through `GetCurrentScore` and `SendScore`. Player connection slots
are accepted by `SetAffect`; merchant, `conn > MAX_USER`, aggressive
`RSV_BLOCK`, guild/leader, resistance-roll, and Clan 6 source gates remain in
force. The C# path adds only the audited skill 51 player producer.

TMProject `Servidor/Source/Code/Basedef.cpp:3683-3688` and W2PP
`Server/W2PP/Source/Code/Basedef.cpp:4018-4023` agree that Type 10 subtracts
`Level / 5 + Value` from `MOB.CurrentScore.Damage`. In the 7.69 score path this
mutation occurs before the terminal damage multiplier. C# now applies the
existing `CalculateWeakenAffectStage` to the player projection before
`ApplyFinalDamageStage`; the projection preserves live HP/MP. Reapplying the
skill resets Type 10 duration to four timer ticks, matching the `SetAffect`
special case for Types 1/3/10. The existing player timer clears the affect and
rebuilds the score at expiry, restoring baseline damage.

TDD RED reproduced the missing player score projection (`507 passed / 1
failed`); GREEN Engine.Tests passed `508/508`, covering the exact 20-point
reduction for the level-50/Special2-50 fixture, zero direct damage, preserved
HP/MP, initial three-tick duration, four-tick recast, and expiry restoration.
The E2E project build passed with 0 warnings/errors. Full no-incremental
solution build passed with 0 errors and the pre-existing CS8604 at
`tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exited 1 because the runner could not open Service Control
Manager.

The synthetic three-peer TCP `--mob-ai-smoke` passed
`ENFRAQUECER_PLAYER_TYPE10_SMOKE_PASS score-affect-target-only=true` and the
full `MOB_AI_SMOKE_PASS`. The target received Type 10 UpdateScore and
UpdateAffect; the attacker and outside peer received no frame. This is local
loopback evidence, not authenticated Host or visual-client E2E. No
package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DF - Lamina Congelante Type 7 score projection for player targets (25/10/2026)

TMProject 7.69 SkillData row 19
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv:20`)
defines aggressive damaging skill 19 `Lamina_Congelante` with `InstanceType=3`,
`AffectType=7`, `AffectValue=1`, `AffectResist=2`, and CSV `AffectTime=2`.
The loader's existing division by four stores zero, producing a one-tick affect
for this attack delay. TMProject `_MSG_Attack.cpp:1667-1737` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1170-1238` run the generic
aggressive affect tail after damage and recompute/send score when `SetAffect`
changes the record. `SetAffect` accepts player connection slots and rejects
merchants, `conn > MAX_USER`, and `RSV_BLOCK` for aggressive skills. The same
guild/leader, resistance-roll, Clan 6, and block gates already used by the C#
player attack path remain in force.

The Type 7 formula agrees in TMProject `Basedef.cpp:3609-3621` and W2PP
`Basedef.cpp:3986-3997`: subtract `Level / 10 + 10` from raw AttackSpeed and,
when head item index exceeds 50, subtract that amount plus 10 from Intelligence.
Final AttackRun packing occurs later in the score calculation. The source
`SetAffect` recast rule keeps the greater existing duration and level; Type 7
does not use the four-tick reset reserved for Types 1/3/10.

C# now projects the explicit aggressive skill 19 Type 7 producer when it hits a
player, through `LegacyCurrentScoreLoginProjector`, preserving live HP/MP. The
player timer already clears the finite record and recomputes score at expiry.
The shared affect writer now retains Type 7's greater existing time and level
on recast, matching both source helpers and the NPC skill path.

TDD RED reproduced missing player score projection (`506 passed / 1 failed`);
GREEN Engine.Tests passed `507/507`, covering score `AttackRun=0x86` and
Intelligence 75 for the level-50/head-index-51 fixture, live HP/MP, one-tick
expiry/restoration, recast without a four-tick reset, and preservation of a
higher pre-existing Type 7 level/duration. Full no-incremental solution build
passed with 0 errors and the existing CS8604 at
`tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exited 0.

The synthetic three-peer TCP `--mob-ai-smoke` passed
`LAMINA_CONGELANTE_PLAYER_TYPE7_SMOKE_PASS score-affect-target-only=true` and
the full `MOB_AI_SMOKE_PASS`. The target received UpdateScore and UpdateAffect;
the attacker and outside peer received no frame. This is loopback evidence, not
authenticated Host or visual-client E2E. No package/publish/deploy,
Site/production change, commit, or push.

## MOB-AI-001 Task 5DE - Perseguicao Type 3 score projection for player targets (25/10/2026)

TMProject 7.69 SkillData row 16 (`Servidor/Server/Common/skilldata.csv:17`)
defines `Perseguicao` as an aggressive, effect-only skill with
`InstanceType=0`, `AffectType=3`, `AffectValue=60`, and `AffectResist=2`. The
generic `_MSG_Attack.cpp` tail in TMProject 7.69 and W2PP calls `SetAffect`,
then recomputes `GetCurrentScore` and sends `SendScore` when the affect changes.
The target-player path therefore applies a score change despite doing no skill
damage.

C# now projects Type 3 for the explicitly audited player producer, while
preserving live HP/MP. It keeps the unmodified four-resistance baseline in the
WorldHub participant and login data so subsequent score projections, recasts,
timer expiry, and Detox rebuild from the same baseline. Persistence snapshots
retain baseline MOB resistance while the temporary penalty is active. The
calculation also handles resistance values that clamp at zero without losing
the original value.

RED reproduced the missing player score projection (`505 passed / 1 failed`);
GREEN Engine.Tests passed `506/506`, including initial score projection,
resistance saturation, recast without stacking, expiry, Detox, and persistence /
login baseline handling. The standalone E2E project build passed with 0
warnings/errors. The full no-incremental solution build passed with 0 errors and
the pre-existing CS8604 at
`tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exited 0.

The three-peer TCP loopback `--mob-ai-smoke` passed
`PERSEGUICAO_PLAYER_TYPE3_SMOKE_PASS score-affect-target-only=true`. The player
target received UpdateScore and UpdateAffect; attacker and outside peer received
no frame. This is synthetic loopback evidence, not authenticated Host or visual
client E2E. No package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DD - Type 1 score projection for offensive player skills (25/10/2026)

TMProject 7.69 SkillData rows 1, 23, 34, and 36 define the audited aggressive
Type 1 producers. In `_MSG_Attack.cpp`, the shared generic tail calls
`SetAffect` and then `GetCurrentScore`/`SendScore` when the affect changes
(TMProject around lines 1727-1737; W2PP around 1228-1238). This covers
effect-only skill 1 and damage skills 23/34/36 against player targets. The
existing C# player path stored those affects but did not consistently rebuild
the target CurrentScore; NPC Type 1 production had already been ported in
Tasks 5CW/5CX/5DA.

C# now projects the player score with `LegacyCurrentScoreLoginProjector` for
these explicit skill IDs. The transition preserves live HP/MP across the
projector, resets an existing Type 1 record to the source four-tick recast
duration, and commits the projected MOB/affect atomically. The player timer
already clears the full record and reprojects the remaining affects at expiry.
The Host's existing skill outcome path sends UpdateScore and UpdateAffect to
the affected player. No unverified Type 1 producer IDs were added.

TDD RED reproduced the stale player score (`504 passed / 1 failed`): skill 1
applied Type 1 while AttackRun stayed `0x96` and Intelligence stayed 100.
GREEN Engine.Tests passed `505/505`, covering skills 1/23/34/36, score
projection, recast without stacking, and score restoration at expiry. Full
`dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` succeeded with
0 errors and the pre-existing CS8604 at
`tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256`; standalone E2E
build succeeded with 0 warnings/errors. SDK 10.0.401; `dotnet --info` exited 1
because the runner could not open Service Control Manager. The synthetic
three-peer TCP `--mob-ai-smoke` passed
`LANCA_DE_GELO_PLAYER_TYPE1_SMOKE_PASS score-affect-target-only=true`; the
player target received valid UpdateScore/UpdateAffect frames with AttackRun
`0x64` and Intelligence 60, while attacker and outside peer received no frame.
This is loopback evidence, not authenticated Host or visual-client E2E. No
package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DA - Type 1 from damage skills 23 and 36 (25/10/2026)

TMProject 7.69 SkillData rows 23 and 36 (`Servidor/Server/Common/skilldata.csv:24,37`)
define `Tempestade_de_Gelo` and `Nevasca` as aggressive `InstanceType=3` damage
skills with `AffectType=1`, `AffectValue=2`, `AffectResist=2`, and CSV
`AffectTime=3/1`. The SkillData loader divides these times by four; the
post-damage `SetAffect` calculation therefore starts the effects at four and two
timer ticks. TMProject `_MSG_Attack.cpp:1727` and W2PP
`_MSG_Attack.cpp:1228` reach the same generic aggressive affect tail after
damage. Their shared gates compare leader/guild, resistance roll, `RSV_BLOCK`,
and player-caster-vs-Clan-6; `SetAffect` admits only the first NPC index 1000.

C# extends the explicit damaging Type 1 allowlist to skills 23, 34, and 36 in
`WorldHub.TryApplyNpcAggressiveType1AffectLocked`. It reuses the existing gates,
score baseline, recast and timer paths, and rejects NPC 1001+ without a generic
Type 1 fallback. `AttackNpcType1DamageSkillAffectTests` covers both rows,
duration, score projection, cross-skill recast, and the first-NPC boundary.

TDD RED reproduced the missing producer (`501/502` Engine.Tests); GREEN passed
`502/502`. The full no-incremental solution build exited 0 with no errors and
the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. SDK 10.0.401 was
identified; `dotnet --info` exited 1 because the runner could not open Service
Control Manager. Three-peer TCP `--mob-ai-smoke` passed
`TEMPESTADE_DE_GELO_NPC_TYPE1_SMOKE_PASS`,
`NEVASCA_NPC_TYPE1_SMOKE_PASS`, and
`LANCA_DE_GELO_NPC_TYPE1_SMOKE_PASS`: the actor and in-view peer received
`UpdateScore`, while the outside peer received no frame. This is synthetic
loopback evidence, not authenticated Host or visual-client E2E. No
package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5DB - physical RSV_FROST/RSV_DRAIN affects (25/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:1943-1968` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1466-1489` apply the same
secondary effects after positive physical damage. RSV_FROST rolls once and
calls `SetAffect` and `SetTick` for skill 36; RSV_DRAIN does the same for skill
40. SkillData row 36 (`Nevasca`) defines Affect Type 1, while row 40
(Nevoa_Venenosa) defines Tick Type 20.

The helper gates differ and are preserved separately. TMProject
`Server.cpp:10359-10418` and W2PP `Server.cpp:9209-9255` reject `SetAffect` for
Merchant or `conn > MAX_USER`, and reject `RSV_BLOCK` for aggressive skills.
`SetTick` at TMProject `Server.cpp:10419-10458` and W2PP `Server.cpp:9256-9295`
has no NPC index ceiling; it rejects Merchant only when the NPC index exceeds
`MAX_USER`, and also honors `RSV_BLOCK` for aggressive skills. This permits
RSV_FROST Type 1 only through NPC 1000 while RSV_DRAIN Type 20 can reach NPC
1001 unless it is a Merchant.

C# now applies the two source operations independently. `SetAffect` recasts of
Types 1/3/10 reset their duration to four ticks; Type 1 on NPC 1000 reuses the
saved Holy Touch score baseline, and the existing NPC timer restores that score
when the affect expires. The physical attack outcome captures the current NPC
view recipients, and the Host relays the resulting UpdateScore to those peers
only. Existing Type 20 expiry handles the RSV_DRAIN tick.

TDD RED reproduced the missing Type 1 score projection (`502 passed / 1
failed`). GREEN Engine.Tests passed `503/503`, covering Type 1 projection,
four-tick recast and restoration, the `SetAffect` NPC 1000 bound, `RSV_BLOCK`,
and Type 20 `SetTick` behavior at NPC 1001 including the Merchant boundary.
The full `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` passed
with 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exited 1
because the runner cannot open Service Control Manager. Three-peer TCP
`--mob-ai-smoke` passed `RSV_FROST_NPC_TYPE1_SMOKE_PASS
attackrun-view-scoped=true`; the attacker and in-view observer received valid
Type 1 UpdateScore frames with AttackRun `0x64` and Intelligence 60, while the
outside peer received no frame. This is synthetic loopback evidence, not
authenticated Host or visual-client E2E. No package/publish/deploy,
Site/production change, commit, or push.

## MOB-AI-001 Task 5DC - RSV_FROST Type 1 score on player targets (25/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:1943-1954` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1466-1477` apply skill 36
`SetAffect` and `SetTick` after positive physical damage when `RSV_FROST`'s
roll succeeds. Both handlers call `SendScore(idx)` for each successful helper.
SkillData row 36 supplies Type 1; `SetAffect` accepts ordinary player slots.
The score formula is the shared Type 1 stage already covered by the login
projector: each record subtracts its Value from Run and 30 from Attack, and
subtracts 40 Intelligence when head item index is above 50.

C# previously stored the affect for player targets but left their authoritative
CurrentScore stale. When Type 1 becomes active on a player, the physical attack
path now invokes `LegacyCurrentScoreLoginProjector` with that player's MOB,
affects, MOBEXTRA, Donate, position, equipment, and the request's ItemData. The
existing `TryProcessPlayerTimerAffects` countdown clears the full eight-byte
record at expiry and runs the same projector over the remaining affects, which
restores the baseline. Recasts of an already-active Type 1 do not reapply its
score delta. The Host already sends the physical outcome's UpdateScore and
UpdateAffect to the player target.

TDD RED reproduced the stale player score (`503 passed / 1 failed`): the target
retained Intelligence 100 instead of the projected 60. GREEN Engine.Tests
passed `504/504`, including expiry restoration to the no-affect score. The full
`dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` passed with 0
errors and the pre-existing CS8604 at
`tests/WydCdk.Engine.Tests/AttackDivineBlessingTests.cs:256`. SDK 10.0.401;
`dotnet --info` exited 1 because the runner could not open Service Control
Manager. The three-peer TCP `--mob-ai-smoke` passed
`RSV_FROST_PLAYER_TYPE1_SMOKE_PASS score-affect-target-only=true`; the player
target received valid UpdateScore/UpdateAffect frames with Type 1, AttackRun
`0x64`, and Intelligence 60, while attacker and outside peer received no
affect score frames. This is synthetic loopback evidence, not authenticated Host
or visual-client E2E. No package/publish/deploy, Site/production change, commit,
or push.

## MOB-AI-001 Task 5CV - Perseguicao Type 3 producer and NPC expiry (23/10/2026)

TMProject 7.69 SkillData row 16 (`Servidor/Server/TMSrv/run/skilldata.csv:17`)
defines skill 16 `Perseguicao` with `AffectType=3`, `AffectValue=60`, and
`AffectResist=2`. The matching W2PP skill row agrees. Both `_MSG_Attack.cpp`
handlers use the same aggressive affect gates: different leader/guild, target
not merchant, no `RSV_BLOCK`, player caster cannot affect Clan 6, and the
resistance roll compares `rand()%100` with target `RegenMP + AffectResist +
difLevel` (`TMProject _MSG_Attack.cpp:1677+`, `W2PP _MSG_Attack.cpp:1181+`).
Both `SetAffect` functions reject only `conn > MAX_USER`, so with `MAX_USER=1000`
this producer reaches the first NPC index and no higher NPC index
(`TMProject Server.cpp:10359`, `W2PP Server.cpp:9209`). Recasting Type 3 sets
four ticks (`sType == 1 || 3 || 10`) in both implementations.

The Type 3 score projection in TMProject `Basedef.cpp:3593-3609` and W2PP
`Basedef.cpp:3955-3971` subtracts from the four elemental resistances. It uses
`AffectValue / 2` when `MOB.Equip[0].sIndex < 50`, otherwise `AffectValue - 10`.
Skill row 16 has head-item dependent attack data, so C# derives the NPC's four
authoritative `MOB.Resist` bytes from that same projection. It retains the
pre-effect baseline while Type 3 is active because subtract-then-clamp cannot
be reversed after a resistance saturates at zero; recast recalculates from the
baseline and expiry restores all four bytes. The first application uses the
finite skill duration produced by the existing data loader; the legacy
Type 3 recast rule is four server ticks. Generated and non-generated NPC timer
paths clear the full 8-byte affect slot, restore the baseline, and relay
`UpdateScore` to the captured view.

TDD RED reproduced the missing NPC 1000 effect (`496/497` Engine.Tests).
GREEN passed `497/497`, covering NPC 1001 exclusion, source resistance threshold,
leader/guild/merchant/Clan 6/`RSV_BLOCK` gates, all four resistances, head-item
boundary, recast, and expiry/restoration for generated and non-generated NPCs.
The E2E project builds with 0 warnings/errors. The synthetic three-peer TCP
`--mob-ai-smoke` passes `PERSEGUICAO_NPC_TYPE3_SMOKE_PASS
resist-view-scoped=true`: actor and observer receive valid UpdateScore with the
new resistances; the outside peer receives no frame. Full no-incremental
solution build succeeded with 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` identified
the SDK and exited 1 because the runner could not open Service Control Manager.
This does not exercise authenticated Host or visual client behavior. No
package/publish/deploy, Site/production change, commit, or push. Task 5CW later
completed the skill 1 Type 1 producer and its score projection; continue the
remaining attack-reachable affect audit before broadening the NPC effect path.

## MOB-AI-001 Task 5CU - Enfraquecer Type 10 producer and NPC expiry (23/10/2026)

TMProject 7.69 SkillData row 51 (`Servidor/Server/TMSrv/run/skilldata.csv:52`)
defines skill 51 `Enfraquecer` as an active aggressive effect-only skill with
`AffectType=10`, `AffectValue=10`, `AffectTime=5`, `TargetType=3`, and
`AffectResist=0`; C# SkillData parsing converts AffectTime to server ticks by
dividing by four. TMProject `_MSG_Attack.cpp:1667-1734` and W2PP
`_MSG_Attack.cpp:1205-1234` both reach generic `SetAffect` after the same
leader/guild, resistance, RSV_BLOCK, and Clan 6 checks. Their `SetAffect`
guards reject `conn > MAX_USER` (TMProject `Server.cpp:10361`; W2PP
`Server.cpp:9211`), so this producer can mutate only the first NPC index 1000.
Merchant rejection is in the `SetAffect` helper itself.

Type 10 lowers `MOB.CurrentScore.Damage` by `Level / 5 + Value` in both sources
(TMProject `Basedef.cpp:3683-3688`; W2PP `Basedef.cpp:4018-4022`). The common
`LessAffect` tail decrements finite time and clears Type/Value/Level/Time on
expiry (TMProject `Server.cpp:6899-6920`; W2PP `Server.cpp:5831-5851`), after
which the source calls `GetCurrentScore` and `SendScore`. Type 10 has no periodic
HP or attack action in this timer path.

C# now handles only this proven producer at `ConnectionId == MAX_USER`, applies
the source gates, and projects the delta between the old/new Type 10 damage
stages into the authoritative NPC score. The generated and non-generated NPC
timer paths decrement Type 10 and restore damage from the affect-stage delta on
expiry; existing Peace and fourth Combat opportunity cadence is reused. The
recast path also honors `SetAffect`'s existing-slot Type 1/3/10 special case by
resetting a refreshed Type 10 duration to four timer ticks. Host uses
`LegacyNpcAffectScoreRelay` for NPC skill affect outcomes and sends
the 7.69 UpdateScore only to the NPC's captured view. It sends no UpdateAffect
to an NPC index. No higher NPC index, passive producer, or general effect-only
skill range was opened.

TDD RED reproduced the missing Type 10 at NPC 1000 (`495/496` Engine.Tests).
GREEN Engine.Tests passed `496/496`, including NPC 1001 exclusion, same-leader,
same-guild, merchant, Clan 6, and RSV_BLOCK gates, CurrentScore damage reduction,
initial two-tick duration and four-tick recast, and timer expiry/restoration for
generated and non-generated NPCs. The E2E
project builds with 0 warnings/errors. The synthetic three-peer TCP
`--mob-ai-smoke` passes `ENFRAQUECER_NPC_TYPE10_SMOKE_PASS
damage-view-scoped=true`: both in-view peers receive valid Type 10 UpdateScore
frames containing reduced damage, while the outside peer receives no frame.
Full `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental` succeeded
with 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` identified
the SDK and exited 1 because the runner could not open Service Control Manager.
This does not exercise authenticated Host or visual client behavior. No
package/publish/deploy, Site/production change, commit, or push. Tasks 5CV and
5CW later completed the Type 3 and Type 1 producers; continue auditing the other
attack-reachable affect producers before broadening the NPC effect skill path.

## MOB-AI-001 Task 5CT - Cancelamento Type 32 producer (23/10/2026)

TMProject 7.69 and W2PP `_MSG_Attack.cpp` keep the skill 47 Type 19 scan
before the generic buff tail. If no Type 19 is removed, SkillData row 47
(`skilldata.csv:48`) supplies `AffectType=32`, `Aggressive=1`, and
`AffectResist=0`; the handler continues to `SetAffect` (TMProject
`_MSG_Attack.cpp:1727`, W2PP `_MSG_Attack.cpp:1228`). TMProject
`Basedef.h:70` and W2PP `Basedef.h:116` set `MAX_USER=1000` as the first NPC
index. Both `SetAffect` functions reject only
`conn > MAX_USER` (TMProject `Server.cpp:10359`, W2PP `Server.cpp:9209`), so
NPC 1000 is accepted and higher NPC indices are refused by this producer.

The common aggressive-affect gate also requires different leader and guild,
the target not to be a merchant, no `RSV_BLOCK`, and—when the caster is a
player—a target outside Clan 6. Row 47 has no random resistance roll because
`AffectResist=0`. The C# fallback is deliberately limited to skill 47 and NPC
connection 1000 after the Type 19 removal branch. It reads the legacy
`STRUCT_MOB.Merchant` byte, normalizes two zero guilds to the legacy `-1/0`
comparison, checks `LeaderId`/party leader and Clan 6, and reuses the existing
`RSV_BLOCK` guard plus `TryApplyLegacyAffects`. Type 19 removal at other NPC
indices and the existing player fallback are unchanged. The Host routes this
skill 47 NPC outcome through `LegacyNpcAffectScoreRelay`, which builds the
7.69 `UpdateScore` carrying the affect projection and broadcasts it to the
NPC's current 33x33 view. This matches C++ `SendScore`/`GridMulticast`
(TMProject `SendFunc.cpp:1233`, W2PP `SendFunc.cpp:1244`); no separate
`UpdateAffect` is sent because both C++ `SendAffect` functions reject NPC
indices (`SendFunc.cpp:1883`/`1901`). The wire format is unchanged.

TDD RED reproduced the missing Type 32 at NPC 1000 (`494/495` Engine.Tests).
GREEN Engine.Tests passed `495/495`, including NPC 1001 exclusion and
same-leader, same-guild, merchant, Clan 6, and `RSV_BLOCK` gates. Full
`dotnet build WydCdk.Engine.slnx --no-incremental` succeeded with 0 errors and
the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`. `dotnet --info`
identified SDK 10.0.401 and exited 1 because the runner could not open Service
Control Manager. The synthetic `--mob-ai-smoke` loopback passes
`CANCELAMENTO_NPC_TYPE32_SMOKE_PASS score-view-scoped=true`: the actor and a
peer exactly on the inclusive 33x33 NPC-view boundary receive valid UpdateScore
frames containing Type 32, while a peer outside the view receives no frame.
This does not exercise an authenticated Host or visual client. No
package/publish/deploy, Site/production change, commit, or push.

## MOB-AI-001 Task 5CW - Toque Sagrado Type 1 producer and NPC expiry (25/10/2026)

TMProject 7.69 SkillData row 1 (`Servidor/Server/TMSrv/run/skilldata.csv:2`)
defines skill 1 `Toque_Sagrado` with `AffectType=1`, `AffectValue=2`,
`AffectTime=3`, `Aggressive=1`, and `AffectResist=2`; the C# loader converts
the source duration to zero server ticks before the affect delay is applied.
TMProject `_MSG_Attack.cpp:1670-1727` and W2PP
`_MSG_Attack.cpp:1174-1228` both use the generic aggressive-affect tail:
leader and guild must both differ, the resistance roll must be at most
`RegenMP + AffectResist + levelDifference`, `RSV_BLOCK` must be clear, and a
player caster cannot affect Clan 6. Their `SetAffect` functions reject a
merchant and only reject `conn > MAX_USER` (TMProject `Server.cpp:10359-10390`,
W2PP `Server.cpp:9209-9239`); with `MAX_USER=1000`, this producer admits NPC
1000 and rejects NPC 1001 and higher. Refreshing an existing Type 1 record
resets its duration to four timer ticks in both implementations.

The Type 1 score branch agrees in TMProject `Basedef.cpp:3446-3458` and W2PP
`Basedef.cpp:3938-3948`: it subtracts `Value` from Run and 30 from Attack, and
subtracts 40 Intelligence when `Equip[0].sIndex > 50`. Final score composition
clamps Run to 0..6 and Attack to 0..150 before packing Attack in the high nibble
as tens and Run in the low nibble (TMProject `Basedef.cpp:4318-4326`; W2PP
`Basedef.cpp:4701-4709`). The common `LessAffect` paths decrement finite time,
clear all eight bytes on expiry, recalculate current score, and call `SendScore`
(TMProject `Server.cpp:6899-6950`; W2PP `Server.cpp:5831-5879`).

C# now applies skill 1's producer only at NPC connection 1000 after those
source gates. `WorldHub` projects the existing
`LegacyCurrentScoreMath.CalculateHolyTouchAffectStage` from a saved packed
AttackRun/Intelligence baseline. Recasts reproject from that baseline instead
of subtracting repeatedly; both generated and non-generated NPC timer paths
restore the exact saved fields at expiry, including when AttackRun saturated at
zero. The timer expiry remains score-only, and the existing
`LegacyNpcAffectScoreRelay` sends the NPC `UpdateScore` only to its captured
view. No NPC 1001+ fallback or generic effect-skill range was added.

TDD RED reproduced the missing producer (`497/498` Engine.Tests); GREEN passed
`498/498`, covering the source gates and roll threshold, head index boundary,
recast, NPC index boundary, and generated/non-generated expiry with saturation.
The full `dotnet build WydCdk.Engine.slnx --no-incremental` succeeded with 0
errors and the pre-existing CS8604 at `AttackDivineBlessingTests.cs:256`; the
standalone E2E build succeeded with 0 warnings/errors. SDK 10.0.401 was
identified by `dotnet --info`, which exited 1 because the runner could not open
Service Control Manager. The synthetic three-peer TCP `--mob-ai-smoke` passed
`TOQUE_SAGRADO_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true`: actor and
observer received valid Type 1 UpdateScore frames with AttackRun `0x64` and
Intelligence 60, while the outside peer received no frame. This is loopback
evidence, not authenticated Host or visual-client E2E. No package/publish/deploy,
Site/production change, commit, or push.

## MOB-AI-001 Task 5CX - Lanca de Gelo Type 1 on damaging NPC skill (25/10/2026)

TMProject 7.69 SkillData row 34 (`Servidor/Server/Common/skilldata.csv:35`)
defines skill 34 `Lanca_de_Gelo` with `InstanceType=3`, `AffectType=1`,
`AffectValue=2`, `AffectTime=1`, `Aggressive=1`, and `AffectResist=2`.
TMProject `_MSG_Attack.cpp:1670-1727` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1172-1228` calculate the
InstanceType damage and then reach the common aggressive `Buffs Time` affect
tail. Both `SetAffect` implementations reject merchants and only reject
`conn > MAX_USER` (TMProject `Server.cpp:10359-10390`, W2PP `Server.cpp:9209-9239`),
so this producer accepts NPC 1000 and rejects NPC 1001 and higher. W2PP has no
SkillData CSV; the skill row contract comes from the selected 7.69 source.

The source gates require different leader and guild, a successful resistance
roll (`roll <= RegenMP + AffectResist + levelDifference`), no `RSV_BLOCK`, and
no Clan 6 target when the caster is a player. Existing Type 1 records reset to
four timer ticks. The Type 1 score projection already compared in Task 5CW
subtracts `Value` from Run, 30 from Attack, and 40 Intelligence when the head
item index is above 50. For the task fixture, `Special2=0` gives Delay=100 and
the initial duration `(AffectTime + 1) * Delay / 100 = 2` ticks.

C# now applies this affect only for skill 34 in the damaging NPC path at
connection 1000. It preserves the source gate order, passes the affect roll to
the finish stage, reuses the saved AttackRun/Intelligence baseline and Task
5CW score projection, and includes the changed affect snapshot in the outcome.
Generated and non-generated NPC timer paths restore the saturated score at
expiry; existing `LegacyNpcAffectScoreRelay` keeps `UpdateScore` limited to the
captured NPC view. No generic Type 1 fallback or other skill was added.

TDD RED reproduced the missing skill 34 affect (`498 passed / 1 failed`).
GREEN Engine.Tests passed `499/499`, covering damage-path application, packed
AttackRun, head-gated Intelligence, two-tick initial lifetime, four-tick
recast, resistance, NPC 1001 exclusion, and generated/non-generated expiry
restoring a saturated score. The full no-incremental solution build succeeded
with 0 errors and the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`; the standalone E2E build succeeded with 0
warnings/errors. SDK 10.0.401 was identified by `dotnet --info`, which exited 1
because the runner could not open Service Control Manager. The synthetic
three-peer TCP `--mob-ai-smoke` passed
`LANCA_DE_GELO_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true`: actor and
observer received valid Type 1 `UpdateScore` frames with AttackRun `0x64` and
Intelligence 60, while the outside peer received no frame. This is loopback
evidence, not authenticated Host or visual-client E2E. No package/publish/deploy,
Site/production change, commit, or push.

## MOB-AI-001 Task 5CY - Lamina Congelante Type 7 in damaging NPC skill path (25/10/2026)

TMProject 7.69 SkillData row 19
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv:20`)
defines skill 19 `Lamina_Congelante` with `InstanceType=3`, `AffectType=7`,
`AffectValue=1`, CSV `AffectTime=2` (the loader stores zero after integer
division by four), `Aggressive=1`, and `AffectResist=2`. TMProject
`_MSG_Attack.cpp:1667-1737` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1170-1238` run the common
aggressive affect tail after skill damage and recalculate/send score when
`SetAffect` changes the record. Both `SetAffect` functions reject merchants and
reject only `conn > MAX_USER`, so the source admits NPC 1000 and rejects 1001+.

The common gates compare leader and guild, use the `AffectResist=2` random
threshold, reject `RSV_BLOCK`, and reject Clan 6 when the caster is a player.
The Type 7 score branch agrees in TMProject `Basedef.cpp:3609-3621` and W2PP
`Basedef.cpp:3986-3997`: `tmp = Level / 10 + 10`, subtract `tmp` from raw
AttackSpeed, and subtract `tmp + 10` from Intelligence only when head item
index exceeds 50. Final attack/run composition clamps and packs the values later
in the same CurrentScore calculation. Unlike Types 1/3/10, Type 7 does not use
the common four-tick recast reset.

C# now applies Type 7 only for skill 19 at NPC index 1000 in the NPC skill
finish path. Login and NPC score projectors apply the Type 7 deltas before final
AttackRun packing. When ItemList is configured, generated and non-generated NPC
timer paths decrement Type 7, clear all eight bytes at expiry, recompute
AttackRun and Intelligence from the remaining active affects, and relay
UpdateScore only to the captured NPC view. The saved score baseline supports
expiry restoration when the NPC score projector is unavailable.

TDD RED reproduced the missing skill 19 affect (`499 passed / 1 failed`);
GREEN Engine.Tests passed `500/500`, covering the skill path, source gates
(including same guild/leader and Clan 6), resistance, NPC 1001 exclusion,
AttackRun raw-speed composition, the head index 50/51 boundary, and expiry for
generated/non-generated NPCs. `dotnet build WydCdk.Engine.slnx --no-incremental`
succeeded with 0 errors and the existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exited 0.
The E2E binary built as part of the solution. Synthetic three-peer TCP
`--mob-ai-smoke` passed `LAMINA_CONGELANTE_NPC_TYPE7_SMOKE_PASS
attackrun-view-scoped=true`: actor and observer received valid Type 7 UpdateScore
frames with AttackRun `0x96` and Intelligence 75; the outside peer received no
frame. This is loopback evidence, not authenticated Host or visual-client E2E.
No package/publish/deploy, Site/production change, commit, or push.

Task 5CZ follows with the Type 20 producer for damage skills; continue auditing
the remaining attack-reachable affect producers.

## MOB-AI-001 Task 5CZ - Type 20 tick from damaging NPC skills (25/10/2026)

TMProject 7.69 SkillData rows 20, 21, and 40
(`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv:21-22,41`)
define active aggressive damage skills with `TickType=20`, values 2, 10, and
10, and CSV `AffectTime` 1, 3, and 1. The SkillData loader divides
`AffectTime` by four, so all three active rows load as zero. W2PP has no
SkillData CSV in its source tree, but its `_MSG_Attack` handler runs the same
generic aggressive tail and consumes the same `g_pSpell` fields.

TMProject `_MSG_Attack.cpp:1670-1731` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1174-1232` calculate the shared
leader/guild, level resistance, `RSV_BLOCK`, and player-caster-vs-Clan-6 gates,
then call both `SetAffect` and `SetTick`. For these rows `AffectType=0`, so the
producer is `SetTick`. TMProject `Server.cpp:10419-10458` and W2PP
`Server/W2PP/Source/Code/TMSrv/Server.cpp:9256-9295` agree that `SetTick` has no
NPC-index bound; it rejects Merchant only when `Merchant==1 && mob>MAX_USER`,
stores Type, TickValue, Level and `(Delay*(AffectTime+1))/100`, then refreshes
CurrentScore. The same gates and tick fields are preserved in C#.

The current Type 20 NPC timer already decrements and expires this affect. Its
existing `UpdateScore` relay serializes the wire word as `(Type << 8) | time`;
TMProject `GetFunc.cpp:1469-1485`, W2PP `GetFunc.cpp:1300-1316`, and C#
`UpdateScoreConfirmation.ToPayload` agree. The smoke therefore expects `0x1401`
for Type 20 with one tick remaining; the internal snapshot separately carries
the skill TickValue.

C# now applies only `TickType=20` from the damaging NPC skill finish path in
`WorldHub.TryApplyNpcPoisonTickType20Locked`. It preserves source target gates,
applies the exact `SetTick` Merchant boundary, and returns the changed affect in
the outcome for the existing view-scoped relay. Skills 20, 21, and 40 apply to
NPC 1001; a Merchant at 1001 is rejected, while `SetTick` accepts the first NPC
index 1000. No other TickType producer or timer behavior changed.

TDD RED reproduced missing Type 20 on NPC 1001 (`500 passed / 1 failed`). GREEN
Engine.Tests passed `501/501`, covering tick value/level/time, all three skills,
same leader/guild, resistance, `RSV_BLOCK`, Clan 6, and the Merchant index
boundary. `dotnet build WydCdk.Engine.slnx --no-restore --no-incremental`
succeeded with 0 errors and only the pre-existing CS8604 at
`AttackDivineBlessingTests.cs:256`. SDK 10.0.401; `dotnet --info` exited 1
because the runner cannot open Service Control Manager. The E2E project builds
with no warnings/errors. Three-peer TCP `--mob-ai-smoke` passed
`POISON_TICK_NPC_TYPE20_SMOKE_PASS view-scoped=true`: both peers in the NPC view
received UpdateScore `0x1401`, and the outside peer received no frame. This is
synthetic loopback evidence, not authenticated Host or visual-client E2E. No
package/publish/deploy, Site/production change, commit, or push.

## COMBAT-ATTACK-SKILL-13-TYPE24-001 - projeção online de Samaritano (26/10/2026)

The SkillData row 13 (`Samaritano`) is active, self-targeted, non-aggressive,
non-ticking, and produces AffectType 24 with Value 0. TMProject 7.69
`TMSrv/_MSG_Attack.cpp:1727-1734` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1228-1235` send generic
`SetAffect`/`SetTick` changes through the score-update tail; the row uses the
Type 24 `SetAffect` branch. The matching source data is retained in
`Server/artifacts/skilldata-reference/skilldata.csv` and the W2PP sandbox data.

The score formulas agree: TMProject 7.69 `Basedef.cpp:3588-3592` and W2PP
`Server/W2PP/Source/Code/Basedef.cpp:4225-4229` apply each active Type 24 in
slot order as `AC += AC / 4 + Value`, using the updated AC for the next record.
`LegacyCurrentScoreMath.CalculateSamaritanArmorClassAffectStage` already matched
that formula. The online player-skill adapter was missing skill 13 from its
score-rebuild classifier, so it stored the affect without projecting its AC.

`WorldHub` now recognizes only the audited row shape: skill 13, Passive 0,
TargetType 0, InstanceType 0, Aggressive 0, TickType 0, AffectType 24. On cast
and recast, the existing projector applies the Type 24 stage and preserves
current HP/MP; the affect setter replaces the existing Type 24 instead of
stacking it. The existing player-affect timer clears the record on expiry and
rebuilds the baseline score. No wire fields or opcodes changed.

TDD RED reproduced the missing online projection: the fixture's baseline AC was
100, while one Type 24 required 125. GREEN Engine.Tests passed `534/534`, covering
source row fields, score projection, current HP/MP, single-record recast, and
expiry restoration. `dotnet build WydCdk.Engine.slnx --no-restore
--no-incremental -m:1 -v:minimal` succeeded (exit 0). SDK 10.0.401;
`dotnet --info` identified the SDK but exited 1 because the runner cannot open
Service Control Manager.

The three-peer TCP `--mob-ai-smoke` passed `player-type24-expiry=score-only-relayed`
and `MOB_AI_SMOKE_PASS`: both peers in the captured view received UpdateScore;
the outside peer received no frame. This synthetic loopback case validates
expiry relay only; cast/recast behavior is covered by Engine.Tests. No
authenticated Host or visual-client E2E, package/publish/deploy, Site/production
change, commit, or push.

## ITEM-009-APPLY-BONUS-BASE-001 - MSG_ApplyBonus ScoreBonus (31/10/2026)

TMProject 7.69 `Servidor/Source/Code/TMSrv/_MSG_ApplyBonus.cpp:4-66` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_ApplyBonus.cpp:21-83` agree on the bounded
`BonusType=0` branch. The 20-byte request is `_MSG_ApplyBonus=0x0119` plus
`short BonusType`, `short Detail`, `ushort TargetID`, and two ABI tail bytes.
The handler acts on the caller connection; `TargetID` does not select another
character. It requires HP > 0 and `USER_PLAY`, then accepts Details 0..3 as
Strength, Intelligence, Dexterity, and Constitution. A positive `ScoreBonus`
spends 100 points when at least 300 remain and otherwise spends 1. Intelligence
and Constitution also add two points per allocation to BaseScore/CurrentScore
MaxMP/MaxHP. A successful allocation calls `GetCurrentScore`, sends `SendEtc`,
then `SendScore`; no points sends only `SendScore`, while an invalid Detail with
points remaining produces no mutation or response. The 7.69 and W2PP behavior
matches for this branch.

C# now parses this request in `ApplyBonusRequest` and dispatches `BonusType=0`
through `WorldHub.TryApplyBaseScoreBonus`. It applies the same point threshold,
attribute mapping, resource maximum increments and bonus debit to the
authoritative MOB. The existing `ProjectPlayerScoreLocked` projector rebuilds
the supported CurrentScore subset while preserving current HP/MP; this does not
close full `BASE_GetCurrentScore` parity. The dispatcher relays the adapted
7.69 `UpdateEtc` (`0x0337`) before `UpdateScore` (`0x0336`); no-points returns
only `UpdateScore`. Dead or non-playing requests use `SetHpMode` and the
existing 10/type20 crack-error path. `BonusType=1` (Special) and `BonusType=2`
(Skill) are deliberately not implemented in this slice. The updated MOB stays
in the existing character snapshot; no new persistence format or schema was
introduced, and save/reload was not exercised here.

TDD coverage checks the frame contract/checksum, all four attribute mappings,
the 300-point threshold and below-threshold single point, +2 MaxHP/MaxMP,
current HP/MP preservation, zero-balance relay selection, invalid Detail, and
dead-character immutability. `dotnet build WydCdk.Engine.slnx --no-restore
--no-incremental --nologo -v:minimal -m:1 -p:UseSharedCompilation=false`
passed with 0 warnings and 0 errors. Engine.Tests reported `512/555`; both new
ApplyBonus tests passed, while 43 suite failures included fixture/persistence
`UnauthorizedAccessException`, loopback `SocketException 10013`, and existing
fixture-state failures such as `SlotOccupied`. `dotnet --info` identified SDK
10.0.401 but exited 1 because access to Service Control Manager was denied.
No TCP Host smoke, authenticated/visual E2E, package/deploy, Site change,
commit, or push was performed.

## ITEM-009-APPLY-BONUS-SKILL-001 - MSG_ApplyBonus base skill tree (31/10/2026)

TMProject 7.69 `_MSG_ApplyBonus.cpp:115-254` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_ApplyBonus.cpp:129-249` implement the
`BonusType=2` branch. The client sets `Detail = m_dwArg >> 16` and
`TargetID = m_dwArg` in `TMFieldScene.cpp:16588-16595`; the latter is range
checked as a skill-master MOB ID (`MAX_USER <= TargetID < MAX_MOB`) and does not
select the character being mutated. Details 5000..5095 map to class/position;
`SkillPoint` comes from `g_pSpell[position + 24 * class]`. The handler checks
target range, class match, available skill points (1500 for CELESTIAL,
CELESTIALCS, SCELESTIAL), one-final-skill and seven prior branch bits for
positions 7/15/23, duplicate state, then ItemList level/mastery requirements.
Only MORTAL/ARCH use `ReqLvl`; all classes use `ReqInt`, `ReqDex`, `ReqCon`.
Success deducts `SkillBonus`, sets the base learned bit, charges Gold for a
final skill, recalculates score, then sends UpdateScore before UpdateEtc.

The final-skill fees deliberately follow TMProject 7.69: 10M for MORTAL, 50M
for ARCH, and 100M for other ClassMaster values. W2PP instead uses 50M for all
three cases; its fee is recorded as a source divergence and was not selected.
Language.txt responses for class, skill points, duplicate, level, mastery,
one-final-skill, missing prerequisites, and insufficient Gold are mapped to
MessagePanel. Invalid Detail/Target remain silent apart from server diagnostic
logging. The existing C# dead-character gate keeps SetHpMode and crack-error
10/type20 handling.

C# implements the rule in `LegacySkillLearningRules` and the synchronized
`WorldHub.TryApplySkillBonus`; `Program.cs` dispatches the request and emits the
7.69 success order. Score projection preserves live HP/MP. In the active 7,945
byte W2PP blob, the first base learned mask is MOB +780, `Magic` is +784, and
signed `SkillBonus` is +792. This slice changes only that first mask,
`SkillBonus`, final-skill Gold, and the projected CurrentScore; it leaves
`MOBEXTRA.SecLearnedSkill` intact and does not treat +784 as `LearnedSkill[1]`.
It adds no schema or persistence field. The client second word still lacks a proven canonical producer,
crosswalk, and persistence contract.

Engine.Tests now covers detail/class/target bounds, SkillData cost, signed
points and celestial override, ItemList requirements, duplicate/final skill
rules and fees, Portuguese responses, score projection and HP/MP preservation,
and Extra isolation. RED first showed the missing `WorldHub` API. The full
`WydCdk.Engine.slnx` build exited 0 with 0 warnings/errors. Engine.Tests
reported `514/557` (43 failed); this is one more pass and the same 43-failure
count as the prior `513/556` run. The observed failures include local file/ACL
access failures, fixture-state failures, and loopback `SocketException 10013`.
SDK 10.0.401 was selected by `global.json`; `dotnet --info` identified it but
exited 1 because access to Service Control Manager was denied. No TCP Host
smoke, visual-client E2E, persistence round trip, package/deploy, Site change,
commit, or push was performed.

## ITEM-009-APPLY-BONUS-SPECIAL-001 - MSG_ApplyBonus SpecialBonus (31/10/2026)

TMProject 7.69 `_MSG_ApplyBonus.cpp:68-106` and W2PP
`_MSG_ApplyBonus.cpp:85-121` agree on the ordinary Special allocation flow.
When `SpecialBonus > 0`, Details 0..3 select `BaseScore.Special[0..3]`; a valid
allocation consumes one point and recalculates `GetCurrentScore`. The level cap
is `(3 * (BaseScore.Level + 1)) >> 1`, with `3 * 400` added before shifting for
CELESTIAL, CELESTIALCS, and SCELESTIAL. The hard cap is 200; base
`MOB.LearnedSkill` bits 7/15/23 raise the cap to 255 for Details 1/2/3.
The order of checks is observable: the level-derived cap is checked before the
hard cap. A level-cap failure sends Language.txt message 105 (`Não pode colocar
mais pontos.`); a hard-cap failure sends message 106 (`Não pode passar do nível
200.`). Success sends `SendScore` before `SendEtc`; zero SpecialBonus sends only
`SendEtc`, and an invalid Detail with points remaining sends no response.

W2PP contains an extra check absent from TMProject 7.69: any
`Extra.SecLearnedSkill` bits 0/4/8 raise `max_special` to 320. This fork-only
extension is not active in the C# 7.69 port. The three skill words remain
independent; no `SecLearnedSkill` to base `LearnedSkill` bridge was added.

ABI follow-up: TMProject 7.69 declares `STRUCT_MOB.ScoreBonus` and
`SpecialBonus` as signed `short`; W2PP declares both as `unsigned short`. C# now
follows the primary 7.69 interpretation for both BonusType 0/1: only values
greater than zero enter allocation; zero and negative signed values take their
respective no-points relay path. RED reproduced the wrong behavior for negative
values before the signed read/write correction.

C# implements `WorldHub.TryApplySpecialScoreBonus` under the WorldHub lock,
decrements `SpecialBonus`, increments only the selected base Special, and uses
the existing score projector with current HP/MP preserved. The Server dispatches
the 7.69 success order (`UpdateScore`, then `UpdateEtc`), sends only `UpdateEtc`
when there are no points, and sends the two cap strings through `MessagePanel`.
The common dead/non-playing gate remains `SetHpMode` plus crack error 10/type20.
The `BonusType=0` branch remains separately ordered as `UpdateEtc` then
`UpdateScore`; `BonusType=2` (Skill) remains deferred.

TDD first confirmed the missing WorldHub API at compile time. Coverage now
includes one-point debit and score projection, Special1 mapping, base learned
skill caps at 255 for Special2/3/4, celestial level-cap extension, level-cap
precedence, W2PP `SecLearnedSkill=0x111` isolation, zero points, invalid Detail,
dead-character immutability, and both exact cap messages. `dotnet build WydCdk.Engine.slnx --no-restore
--no-incremental --nologo -v:minimal -m:1 -p:UseSharedCompilation=false` passed
with 0 warnings and 0 errors. The final Engine.Tests run was `513/556`; both
`apply bonus score allocation` and `apply bonus special allocation` passed,
while 43 suite failures included fixture ACL `Access denied`, `SlotOccupied`,
and loopback socket permission errors. `dotnet --info` identified SDK 10.0.401 and exited 1 because
access to Service Control Manager was denied. No TCP Host smoke,
authenticated/visual E2E, package/deploy, Site change, commit, or push was
performed.

## COMBAT-ATTACK-SKILL-PVP-REFLECTION-001 - reflection in skill attacks (31/10/2026)

TMProject 7.69 `_MSG_Attack.cpp:1975-1989` and W2PP
`Server/W2PP/Source/Code/TMSrv/_MSG_Attack.cpp:1496-1509` apply the defender's
flat `ReflectDamage` to positive damage against a player, floor the result at
1, then subtract `damage / 100 * ReflectPvP` and floor at 1 again. The 7.69
handler applies the attacker's Type 8 bit 6 Parry penalty between those two
steps; the corresponding W2PP block omits that penalty. The C# physical path
already used its existing `GetLegacyReflectDamage` and `GetLegacyReflectPvp`
helpers, but the skill-to-player path skipped both reductions and applied its
Parry penalty before flat reflection.

The C# skill path now reuses those helpers for player targets and preserves the
7.69 order: flat reflection, minimum-one floor, Type 8 bit 6 Parry adjustment,
then integer `damage / 100 * ReflectPvP` and the second floor. NPC and summon
targets stay outside this player-only branch. No score fields, persistence,
schema, or wire layouts changed.

`AttackPvpJewelAbsorptionTests` adds a WorldHub headless player-skill case with
Beastmaster ReflectDamage (learned bit 17 and Special4=59), a grade-8 item, and
PvpDefense 50. The source-ordered result is 10,000 -> 9,475 because integer
division occurs before multiplying by 5. The full solution build completed
with 0 warnings and 0 errors. Engine.Tests reported 513/557 (44 failures); the
reflection test group passed. The observed suite failures include file/ACL,
fixture-state, and loopback socket-permission errors. SDK 10.0.401 was selected;
`dotnet --info` identified it but exited 1 on denied Service Control Manager
access. No dedicated TCP smoke, authenticated Host, or visual-client E2E ran;
no persistence, deploy, Site change, commit, or push was performed.

## SHOP-005 - 7.69 MSG_Buy wire contract (31/10/2026)

TMProject client `Projects/TMProject/Basedef.h:1158-1166` declares
`MSG_Buy_Opcode=0x0379` and fields `TargetID`, signed `TargetCarryPos`, signed
`MyCarryPos`, and `Coin`. `Projects/TMProject/SGrid.cpp:1300-1324` zero-initializes
this packet, fills the two selected positions, and sends 24 bytes. The effective
7.69 TMSrv header uses `_MSG_Buy = 121 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME`
with corresponding `TargetInvenPos`, `MyInvenPos`, and `Coin`; the handler
replaces `Coin` with the authoritative balance before echoing the same message.
W2PP `Basedef.h` declares the same field sequence and opcode flags. The W2PP
handler lacks some 7.69 branches (including the recycle-bin self-target and the
account-blocked check), recorded here as behavior divergence rather than
silently copied into this DTO.

C# `ShopBuyMessage` models the common 24-byte frame: the 12-byte header and
12-byte payload; payload offsets are TargetID 0, TargetCarryPos 2, MyCarryPos 4,
ABI alignment 6-7, and Coin 8. The type preserves the alignment bytes and
signed slot values. `Coin` is round-tripped for the shared request/confirmation
shape but is explicitly untrusted on ingress. The test checks the literal
payload, frame type/size/header ID, successful parse, invalid checksum, wrong
opcode, and truncation. TDD RED failed at the missing DTO; GREEN passed
`MSG_Buy 7.69 wire`. Full solution build
`dotnet build WydCdk.Engine.slnx --no-restore --no-incremental --nologo -v:minimal
-m:1 -p:UseSharedCompilation=false` completed with 0 warnings/errors. Engine.Tests
reported 515/558 (43 failures); the new wire test passed. Observed failures
include sandbox-denied atomic file moves, stale synthetic character slots,
equipment/fixture state, and loopback `SocketException 10013`; log:
`Server/Backup/Build/shop-buy-wire-tests.log`. This slice does not dispatch
`MSG_Buy`, implement retail pricing/tax/discount/stock, mutate Gold/carry, relay
success, handle `_MSG_Sell`, or enable the fork's Donate purchase. No TCP Host or
visual E2E ran; no schema, persistence, deploy, Site change, commit, or push.
SDK 10.0.401 selected; `dotnet --info` identified it and exited 1 because the
environment denied access to Service Control Manager.

## SHOP-005 - 7.69 MSG_Sell wire contract (31/10/2026)

TMProject client `Projects/TMProject/Basedef.h:1168-1175` declares
`MSG_Sell_Opcode=0x037A` with `TargetID`, signed `MyType`, and signed `MyPos`.
`Projects/TMProject/SGrid.cpp:1817-1823` zero-initializes the packet, assigns
the selected merchant and item source, then sends `sizeof(MSG_Sell)`. The 7.69
TMSrv header uses `_MSG_Sell = 122 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME` and
the same field order; its successful sell path echoes the full structure.
W2PP `Basedef.h` has the same opcode expression and fields, and its successful
handler also echoes the structure. The 7.69 handler checks `pUser[conn].IsBlocked`
before processing; W2PP's handler does not. This is a behavior divergence and
does not change the shared wire DTO.

C# `ShopSellMessage` models the 20-byte frame: 12-byte header plus 8-byte
payload. The payload offsets are `TargetID` 0, `MyType` 2, `MyPos` 4, and ABI
tail padding 6-7. Both item source fields remain signed 16-bit values, while
the parser/serializer preserve the padding for the request echo. The test pins
literal bytes, frame type/size/header ID, round-trip, invalid checksum, wrong
opcode, and truncated input. TDD RED failed on the absent DTO; GREEN passed
`MSG_Sell 7.69 wire`. The full solution build passed with 0 warnings/errors.
Engine.Tests reported 516/559 (43 failures), unchanged in failure count from
the previous 515/558 run; the new wire test passed. Log:
`Server/Backup/Build/shop-sell-wire-tests.log`. This does not dispatch
`MSG_Sell`, validate a merchant or item source, price the item, mutate Gold or
inventory, or relay the echo. `MSG_Buy` business behavior is also pending. No
TCP Host smoke, visual E2E, persistence/schema, deploy, Site change, commit, or
push was performed. SDK 10.0.401 was selected; `dotnet --info` exited 1 because
the environment denied access to Service Control Manager.

## SHOP-005 - NPC sell price math (31/10/2026)

The 7.69 TMSrv `_MSG_Sell.cpp:132-193` and W2PP `_MSG_Sell.cpp:142-203` use the
same price formula. Start with ItemList `Price / 4`; if the result is at most
10,000, values above 5,000 become `2 * price / 3`, while values at or below
5,000 stay unchanged. Results above 10,000 are halved. For a price strictly
above 1,000, class 3 with `LearnedSkill & 0x200`, and item IDs outside
693-695, the seller modifier uses `Special[2] / 10 + 6`, caps the rate at 26,
halves it, then multiplies the price by `(rate + 50) / 50`, with integer
truncation at each division. No skill name or UI crosswalk is inferred from
this mask.

The NPC village is taxed only for IDs 0..5; invalid/out-of-range IDs jump past
tax. For a valid village, only CityTax 0..30 enters the payment path. The
post-tax percentage uses the pre-tax price on both sides of the 100,000 branch,
but the source preserves separate multiplication expressions and integer
division. W2PP agrees on the arithmetic. Its handler omits the 7.69
`IsBlocked` gate and 7.69's `GetRecycleBin` check before mutation; neither is
part of this pure calculation. The later 2,000,000,000 player-Coin cap, guild
tax revenue, item clearing, and `MSG_Sell` echo also remain in the handler.

`LegacyNpcSellPriceMath.Calculate` returns `Success` with the net Gold amount,
`NegativePrice`, `InvalidCityTax`, or `Int32OverflowUnverified`. It checks
whether the source's signed 32-bit multiplication would overflow before
returning a value; it does not choose a wraparound result for C++ signed
overflow without runtime evidence. The reference server-format
`Servidor/Server/Common/ItemList.bin` is 910,004 bytes. Its item 3443,
`Selo_das_Almas`, has Price 300,000,000 and `EF_VOLATILE=206`; the NPC handler's
potion gate rejects only value 1. The base-price steps produce 37,500,000, so
the tax product and eligible class-3 seller modifier can exceed `int32`. The
math test pins this unresolved boundary rather than guessing the result.

TDD RED failed because the result/helper API was absent; GREEN `NPC sell price
math` passed price bands (including 5,000/5,001 and 10,000/10,001 after the
quarter step), seller class/bit/special/exclusions, tax and village bounds,
negative price, and overflow status. Full solution build passed with 0
warnings/errors. Engine.Tests reported 517/560 (43 failures), the same failure
count as the preceding 559-test run; the new group passed. Log:
`Server/Backup/Build/shop-sell-price-math-tests.log`. No packet dispatch,
merchant/item validation, stock, Gold/item mutation, guild-tax revenue,
recycle-bin behavior, Coin cap, persistence, Host smoke, or visual E2E was
added. No schema, deploy, Site change, commit, or push. SDK 10.0.401 was
selected; `dotnet --info` exited 1 because access to Service Control Manager
was denied.

## SHOP-005 - MSG_Sell active trade branch (03/11/2026)

TMProject 7.69 `_MSG_Sell.cpp:15-19` and W2PP `_MSG_Sell.cpp:32-36` use the
same truthy check: when `Trade.OpponentID` is nonzero, call
`RemoveTrade(opponent)` first, then `RemoveTrade(requester)`, and return before
the sale gates. This is a nonzero test, not a positive-ID test. In both
sources, `RemoveTrade` clears `Trade` and `AutoTrade`; for a `USER_PLAY`
connection it sends signal 900, and if `TradeMode` was set it broadcasts the
mob refresh and clears that flag. The opponent cleanup happens before the
requester cleanup.

`LegacyNpcSellTradeRules.Resolve` models only this branch decision and ordered
connection IDs: zero continues to the next gate; any nonzero opponent requests
peer removal followed by requester removal and aborts the sale. It does not
mutate either participant or send signals/`CreateMob`. C# already has bilateral
trade-state cancellation and an auto-trade close coordinator with durable
listing cleanup, but wiring those paths into `_MSG_Sell` remains pending; the
integration must preserve the legacy no-PK behavior and the existing durable
listing semantics.

TDD RED failed because the rule API was absent; GREEN `NPC sell trade rules`
passed the zero, positive, and negative `OpponentID` cases and exact removal
order. The full solution build passed with 0 warnings/errors. Engine.Tests
reported 522/565; the same 43 test names failed as in the preceding run. Log:
`Server/Backup/Build/shop-sell-trade-rules-tests.log`. No handler dispatch,
Host smoke, visual E2E, persistence/schema change, deploy, Site change, commit,
or push was performed. SDK 10.0.401 was selected; `dotnet --info` exited 1
because access to Service Control Manager was denied.

## SHOP-005 - MSG_Sell account block gate (03/11/2026)

After the active-trade branch and `TradeMode` cargo restriction,
TMProject 7.69 `_MSG_Sell.cpp:28-32` checks `pUser[conn].IsBlocked`; if set,
it sends the hard-coded blocked-account notice and returns before source,
target, item, or price processing. W2PP `_MSG_Sell.cpp:32-45` has the same
trade-cancellation and `TradeMode` cargo branch but proceeds directly to the
sell fields without an `IsBlocked` check. The 7.69 rule is retained because it
is the canonical release contract; the W2PP omission is a documented fork
difference, not a reason to remove the account gate.

`LegacyAccountSnapshot` already carries `IsBlocked`. The new
`LegacyNpcSellAccountRules.Resolve` accepts the authoritative boolean and
returns either `ContinueSelling` or `Blocked`, with a flag indicating that the
legacy notice is required. It does not obtain account state, send the notice,
or dispatch `_MSG_Sell`; the account lookup and message relay remain for handler
integration. The adjacent `TradeMode`-plus-cargo decision is already modeled by
`LegacyNpcSellSourceRules`; it also does not send the source's client message.

TDD RED failed because the account rule API was absent; GREEN `NPC sell account
rules` passed both allowed and blocked decisions. The full Engine.Tests run
reported 523/566 with the same 43 failing test names as the previous run. Log:
`Server/Backup/Build/shop-sell-account-rules-tests.log`. The full solution build
passed with 0 warnings/errors. No handler dispatch, Host smoke, visual E2E,
schema, persistence change, deploy, Site change, commit, or push was performed.
SDK 10.0.401 was selected; `dotnet --info` exited 1 because access to Service
Control Manager was denied.

## SHOP-005 - MSG_Sell HP and USER_PLAY precondition (03/11/2026)

The first gate in TMProject 7.69 `_MSG_Sell.cpp:8-12` and W2PP
`_MSG_Sell.cpp:25-29` is identical: when `CurrentScore.Hp == 0` or
`pUser.Mode != USER_PLAY`, the server sends `SendHpMode`, calls
`AddCrackError(conn, 10, 23)`, and returns before trade cancellation or the
remaining sale checks. The comparison is exactly zero; negative HP is not
rejected by this condition. C# `LoginSessionState.Playing` is the existing
`USER_PLAY` mapping.

`LegacyNpcSellHpModeRules.Resolve` mirrors the combined predicate using
authoritative current HP and `LoginSessionState`. Its rejection result marks
both the HP/mode synchronization and crack-error response, with points 10 and
type 23. It is a pure rule: it does not send `SetHpModeConfirmation`, record a
crack error, mutate session state, or enter `_MSG_Sell` dispatch. The response
integration and the following active-trade cancellation remain pending.

TDD RED failed on the missing result API; GREEN `NPC sell HP mode rules`
passed zero/nonzero HP, all session states, the exact `Hp == 0` negative-HP
edge, and the response arguments. Full solution build passed with 0 warnings
and 0 errors. Engine.Tests reported 521/564 (43 failures); comparison with the
preceding run found the same 43 failing test names, and this group's tests
passed. Log: `Server/Backup/Build/shop-sell-hp-mode-tests.log`. No Host smoke,
visual E2E, schema, persistence, deploy, Site change, commit or push. SDK
10.0.401; `dotnet --info` exited 1 because access to Service Control Manager
was denied.

## SHOP-005 - MSG_Sell EF_VOLATILE sale gate (03/11/2026)

TMProject 7.69 `_MSG_Sell.cpp:124-129` and W2PP `_MSG_Sell.cpp:134-139`
perform the same gate after item-pointer/index validation and before sale
pricing: `BASE_GetItemAbility(Item, EF_VOLATILE) == 1` sends the localized
cannot-sell-potion notice and returns. The comparison is equality with 1;
other values, including nonzero values, pass this gate. Both sources define
`EF_VOLATILE` as effect 38. C# already exposes the same effect index as
`LegacyItemEffect.Volatile` and excludes it from sanctuary scaling when reading
item abilities.

`LegacyNpcSellVolatileRules.Resolve` accepts the resolved integer and returns
either `ContinueSelling` or `PotionSaleBlocked`, including an intent flag for
the notice. It does not look up the item, send the message, or dispatch
`_MSG_Sell`. The following `GetRecycleBin` decision happens only after price
calculation; a tested server stage now applies it to session state, but the
`0x037A` request still is not dispatched through that stage.

TDD RED failed because the rule API was absent; GREEN `NPC sell volatile
rules` passed value 1 and representative negative, zero, non-1 positive, 32,
206, and maximum-int values, proving only exact value 1 is blocked. The full
Engine.Tests run reported 524/567; the same 43 test names failed as in the
previous run. Log: `Server/Backup/Build/shop-sell-volatile-rules-tests.log`.
The full solution build passed with 0 warnings/errors. No handler dispatch,
Host smoke, visual E2E, schema, persistence change, deploy, Site change, commit,
or push was performed. SDK 10.0.401 was selected; `dotnet --info` exited 1
because access to Service Control Manager was denied.

## SHOP-005 - MSG_Sell source and item gates (01/11/2026)

TMProject 7.69 `_MSG_Sell.cpp:22-112` and W2PP `_MSG_Sell.cpp:39-122` agree
on source validation, item-pointer lookup and the early item gates. Sell source
type 0 accepts equipment slots 1..15, type 1 accepts carry slots 0..59, and
type 2 accepts cargo slots 0..127; `TradeMode` rejects cargo before validating
its position. Equipment slots below 8 request the later `SendEquip` refresh.
The sources define `MAX_EQUIP=16`, `MAX_CARRY=64`, `MAX_CARGO=128` and
`MAX_ITEMLIST=6500`.

`GetItemPointer` maps the three source types to equipment, carry or cargo and
returns null for invalid storage positions or an item index below 0 or at least
6500. It accepts index 0 as a pointer to an empty slot. The handler next rejects
item IDs 3193, 3194 and 747 before merchant-target validation. Its later
`sIndex <= 0 || sIndex >= MAX_ITEMLIST` branch is therefore reached only for
index 0 after a successful pointer lookup; it resends the item only for carry
sources whose position is within the full carry array, and logs the other
sources. Since the sell-specific carry gate already narrowed positions to
0..59, an empty selected carry slot requests this resync.

C# `LegacyNpcSellSourceRules` resolves the sell-specific type/position and
`LegacyNpcSellItemRules` models the pointer-index and item gates as pure rules.
The latter returns `ItemPointerUnavailable` for negative/out-of-table indices,
distinguishes an empty item from a missing pointer, identifies exactly the three
protected IDs, and signals carry resync for an empty carry slot. Neither helper
reads inventory or emits packets. The item-gate rules were subsequently
connected to `_MSG_Sell` dispatch in the 04/11 slice below. The special class-3
skill bypass, merchant ID/type/view checks, account and HP gates, potion
restriction, inventory/Gold mutation and echo remained outside this original
rule-only slice.

TDD RED failed on the missing item-resolution API; GREEN `NPC sell item
eligibility` passed index boundaries, empty carry resync, protected IDs and
invalid-source handling. The full solution build passed with 0 warnings and 0
errors. Engine.Tests reported 519/562 (43 failures), the same failure count as
the preceding run; both new SHOP-005 rule groups passed. Log:
`Server/Backup/Build/shop-sell-item-eligibility-tests.log`. No Host smoke or
visual E2E, persistence/schema, deploy, Site change, commit or push was done.
SDK 10.0.401; `dotnet --info` identified the SDK but exited 1 because the
environment denied access to Service Control Manager.

## SHOP-005 - MSG_Sell NPC target gates (03/11/2026)

TMProject 7.69 `_MSG_Sell.cpp:87-100` and W2PP `_MSG_Sell.cpp:97-110` use the
same merchant-target flow. `TargetID` is an unsigned 16-bit field. Before the
ordinary target checks, class 3 with `LearnedSkill & 0x1000` jumps past the
target range, `Merchant`, and `GetInView` gates; no skill name or crosswalk is
inferred from that mask. Without the bypass, valid target IDs are
`MAX_USER..MAX_MOB-1` (`1000..24999`), `MOB.Merchant` must equal exactly 1, and
the target must pass `GetInView`.

`GetInView` is identical in TMProject 7.69 `TMSrv/GetFunc.cpp:805-812` and
W2PP `TMSrv/GetFunc.cpp:764-771`: the seller's and target's `TargetX/TargetY`
must differ by at most `VIEWGRIDX/Y=33` on each axis, inclusive. This is a
rectangular coordinate check, not a query of the client view list. The target
failure order is ID range, exact Merchant byte, then view; out-of-view closes
the shop with the existing signal.

`LegacyNpcSellTargetRules` models these gates from supplied seller/target
snapshots and uses `LegacyMovementRules`' 33-cell constants. It retains the
class-3/mask bypass, including bypassing the ordinary NPC lower bound, merchant
type and view checks. The source jumps around its `MAX_MOB` upper-bound check
before later indexing `pMob[TargetID]`; to keep a malformed unsigned ID from
indexing outside the C# mob table, this helper first rejects IDs at or above
25000 even when the bypass mask is set. This deliberate safety boundary only
changes the legacy out-of-table case. The helper performs no target lookup and
is not connected to `_MSG_Sell` dispatch; authoritative world-state lookup,
error/signal relay, and transaction remain pending.

TDD RED failed on the missing target-resolution API; GREEN `NPC sell target
rules` passed ID boundaries, Merchant equality, gate precedence, inclusive
view edges, exact class/bit bypass, and the out-of-table safety case. Full
solution build passed with 0 warnings and 0 errors. Engine.Tests reported
520/563 (43 failures); comparison with the preceding run found the same 43
failing test names, and the new target-rule group passed. Log:
`Server/Backup/Build/shop-sell-target-rules-tests.log`. No Host smoke, visual
E2E, schema, persistence, deploy, Site change, commit or push was done. SDK
10.0.401; `dotnet --info` exited 1 because access to Service Control Manager
was denied.

## SHOP-005 - GetRecycleBin sale insertion (03/11/2026)

TMProject 7.69 `_MSG_Sell.cpp:187` calls `GetRecycleBin` after the item and
price gates and before guild-tax/Coin/item mutation. Its
`TMSrv/GetFunc.cpp:15-56` implementation and W2PP `GetFunc.cpp:32-72` agree:
connection IDs outside `1..999` and any mode other than `USER_PLAY` return 0;
otherwise a fixed 10-entry `CMob.Recycle` array receives the sold item, net
price, and `rand() % 100000` ticks. The first entry whose item index is `<=0`
or `>=6500` is reused. If none is empty, entries 1..9 shift to 0..8 and the
new sale replaces slot 9. The function returns 1 after insertion. W2PP
contains this helper but its `_MSG_Sell` handler omits the call, so the 7.69
call site remains canonical for this release.

The array is separate from `STRUCT_MOB`. Both `CMob::Init` implementations
call `memset` with `sizeof(STRUCT_RECYCLE)`, which clears only the first array
entry; the account-login path explicitly loops over and clears all 10 entries.
The active login flow therefore starts with an empty bin, and this remains
session state rather than a new account-file/schema field. The references also
define `MSG_Recycle` type `0x03E8`; at this stage of the port, C# had neither
live recycle state nor a corresponding wire DTO. `LegacyRecycleBinRules.InsertSale`
models the fixed-capacity transition as a pure function. It accepts a
caller-supplied unsigned random value and applies the legacy modulo; this does
not reproduce the C runtime's random sequence. The rule returns new entries
without mutation; at that implementation stage it was not connected to dispatch,
`WorldHub` state, persistence, buyback, or client relay.

TDD RED failed on the missing recycle entry type; GREEN `recycle bin insertion
rules` passed first-vacancy selection (including out-of-range item indices),
full-bin oldest-entry shift, connection/mode guards, modulo ticks, and
nonmutation. Engine.Tests reported 525/568; the same 43 test names failed as
in the preceding run. Log: `Server/Backup/Build/shop-sell-recycle-bin-tests.log`.
The full solution build passed with 0 warnings/errors. `dotnet --info`
identified SDK 10.0.401 and exited 1 due to denied Service Control Manager
access. At the end of this stage, no handler dispatch, Host smoke, visual E2E,
schema, deploy, Site change, commit, or push was performed; later sections
record the subsequent sale and recycle-list integrations.

## SHOP-005 - client repurchase message wire `0x03E8` (03/11/2026)

The compiled TMProject client defines `MSG_RepurchaseItems` with a 12-byte
`MSG_STANDARD` header, an `int target`, and ten
`STRUCT_REPURCHASEITEM { int Order; STRUCT_ITEM stItem; int Price; }` entries.
With the 8-byte `STRUCT_ITEM`, the client frame is 176 bytes: 12-byte header
plus 164-byte payload. `SGrid.cpp` sends this same structure for the request;
`TMFieldScene.cpp` dispatches the response opcode `0x03E8` to
`OnPacketUndoSellItem`, which reads the ten entries. The target and entry
offsets are therefore explicit client ABI, not inferred from the server's
internal recycle record.

C# `RepurchaseItemsMessage` and `RepurchaseItemEntry` encode and parse that
client-facing 176-byte layout. The wire test checks `target`, `Order`, the eight
item bytes, `Price`, header metadata, round-trip, fixed ten-entry shape,
wrong-size rejection, and checksum rejection. `LegacyRecycleBinClientProjection`
now builds that message from exactly ten `LegacyRecycleBinEntry` values,
preserving each item at its slot and echoing the request target. It writes zero
to client `Order` and `Price`: `OnPacketUndoSellItem` does not read the response
target, `Order`, or `Price`; its UI only uses the returned `stItem`. The client
tooltip gets its amount from `g_pItemList`, and server buyback also recalculates
the charge from `g_pItemList`; neither consumes `STRUCT_RECYCLE.Price` or
`Ticks` from this response. This is a client projection, not a direct wire copy
of the server's internal record.

The raw server layout remains incompatible with the client ABI. TMProject
TMSrv and W2PP define server `MSG_Recycle` as a 12-byte header followed by ten
internal `STRUCT_RECYCLE { int Price; STRUCT_ITEM Item; int Ticks; }` records
and three trailing ints (`Not`, `Pot`, `Dot`), totaling 184 bytes. Its entry
array starts at payload offset 0, while the client has `target` at offset 0 and
starts its entry array at payload offset 4. TMProject's `PacketHandler.cpp`
case `0x3E8` checks `ID == connection` and `USER_PLAY`, overwrites the inbound
message with server records, and relays the original client-supplied size; this
does not translate the layouts. W2PP retains the same server struct and
`SendRecycle` writer, but no `0x3E8` receive case was found in its source tree.
The C# `WorldHub.Participant` now owns ten transient entries, starts empty on
world entry, returns defensive snapshots under its lock, and loses the entries
on `Leave`; this matches the observed login reset without changing account
persistence. `Program.cs` dispatches parsed `0x03E8` requests through
`LegacyRecycleBinRequestHandler`: a mismatched header ID ends that connection as
in TMProject, while non-`USER_PLAY` or missing world state receives no reply.
For valid requests it ignores the ten client-supplied entries, projects the
server session entries, and reuses request ID, ClientTick and KeywordIndex in a
new checksum-valid 176-byte frame. At this stage of the port, `0x037A` was still
undispatched and buyback was unimplemented; the later authenticated-dispatch
section below records the sale integration.

TDD RED first identified the missing projection, then the session/request tests
RED on the absent WorldHub state APIs and handler. GREEN passed `client
repurchase items wire`, `recycle-bin client projection`, and `recycle-bin
session state and request`. The full `WydCdk.Engine.slnx` build passed with 0
warnings/errors using SDK 10.0.401; log:
`Server/Backup/Build/shop-recycle-session-build.log`.

The default sandbox temp path is outside the writable checkout. The earlier
default-temp Engine.Tests run had 35 direct `UnauthorizedAccessException`
failures at atomic `File.Move`, seven wrapped file/socket-dependent failures,
and one direct loopback `SocketException (10013)`. With process-local
`TEMP`/`TMP` redirected to `Server/Backup/Build/EngineTestsTemp`, file tests
passed; final Engine.Tests was 569/571, with only `authenticated startup
requires ItemList before bind` and `serialized network stream concurrent
writes` failing because loopback socket access is denied. Log:
`Server/Backup/Build/shop-recycle-session-tests.log`. `dotnet --info`
identified 10.0.401 and exited 1 due to denied Service Control Manager access.
No `_MSG_Sell` wire dispatch, authenticated Host smoke, visual E2E, schema,
deploy, Site change, commit, or push was performed.

## SHOP-005 - `_MSG_Sell` GetRecycleBin stage (03/11/2026)

TMProject 7.69 `_MSG_Sell.cpp:168-206` resolves the vendor village after price
calculation. Its `BASE_GetVillage` searches five city rectangles and returns
5 outside them; `MAX_GUILDZONE` is also 5. The handler checks only
`sVillage < 0 || sVillage > 5`, so a normal out-of-city result of 5 passes and
indexes `g_pGuildZone[5]`. That is an out-of-bounds read with undefined
behavior, not a verified `LABEL_S2` route. The subsequent `CityTax` range gate
and sale/recycle-bin path therefore have no safe, source-defined result for
that coordinate case. For a valid configured city index 0..4, `CityTax` must
be 0..30; it is applied to `sPrice`, and only a nonnegative result reaches
`GetRecycleBin(conn, Item, sPrice)` at line 187. That call precedes guild-tax
revenue and all later sale mutation/relay. The `<0` and `>5` branch still
exists for an integer passed to the handler, but it does not describe the
five-rectangle `BASE_GetVillage` result outside cities.
`GetFunc.cpp:15-57` rejects connection IDs outside 1..999 and sessions other
than `USER_PLAY`, chooses the first empty/out-of-range item slot, shifts the
full ten-entry array when needed, and writes the passed item, price, and
`rand() % 100000` ticks. W2PP has the same helper in `GetFunc.cpp:32-57`, but
no `GetRecycleBin` call in its `_MSG_Sell.cpp`; the port follows 7.69.

`LegacyNpcSellPriceResult` carries the `NpcVillage` passed to
`LegacyNpcSellPriceMath.Calculate`. `LegacyNpcSellRecycleBinHandler.TryRecordSale`
is a stateful helper for an already prepared price result: an integer village
outside 0..5 takes its modeled `ContinueWithoutRecycleBin` branch, but this
helper-level case is not evidence that live out-of-city coordinates safely
reach `LABEL_S2`. The live coordinator must resolve a tax before creating a
sale plan. The random value is injected and reduced by the existing pure rule;
it does not reproduce global CRT `rand()` state. The stage is not dispatched
from `Program.cs` because the sale still needs one revalidated completion path.

TDD coverage verifies valid-village insertion with the post-tax amount,
invalid-village bypass, rejected price, missing world session, and helper-level
connection rejection. `NPC sell recycle-bin stage` passed. Engine.Tests ended
at 570/572; the only failures match the previous sandbox limits:
`authenticated startup requires ItemList before bind` cannot bind its loopback
listener, and `serialized network stream concurrent writes` receives
`SocketException (10013)`. Full `WydCdk.Engine.slnx` build passed directly with
0 warnings and 0 errors. `dotnet --info` identified SDK 10.0.401 and exited 1
because Service Control Manager access was denied; log:
`Server/Backup/Build/shop-sell-recycle-stage-dotnet-info.log`. Engine.Tests log:
`Server/Backup/Build/shop-sell-recycle-stage-tests.log`. No authenticated Host
smoke, visual E2E, schema change, deploy, Site change, commit, or push was
performed. Next: compose the read-only authoritative lookup and existing gates
into `_MSG_Sell` dispatch through this stage before porting later sale mutations
and buyback.

## SHOP-005 - authoritative seller/item/target lookup (03/11/2026)

TMProject 7.69 and W2PP `GetItemPointer` map `ITEM_PLACE_EQUIP`,
`ITEM_PLACE_CARRY`, and `ITEM_PLACE_CARGO` to the MOB/account cargo with the
same position bounds, then reject `sIndex` outside `MAX_ITEMLIST`. The C# source
resolver preserves source bounds and the TradeMode cargo restriction; the
existing `LegacyNpcSellItemEligibility` remains the separate gate for item
index and blocked-item behavior.

`LegacyNpcSellAuthoritativeLookup.CaptureAsync` reads seller MOB/position from
`WorldHub`, account cargo and `IsBlocked` from `IAccountSnapshotStore`, resolves
the selected equipment/carry/cargo item from its owning snapshot, and captures
the requested target through `WorldHub.TryGetMobSnapshot`. The target adapter
returns defensive snapshots for player, NPC, or summon tables. The result
contains only the blocked flag and selected item, not the block password or
complete cargo array. Reads are non-mutating copies, not one transaction/lock
across WorldHub and the account store, so the eventual mutation stage must
revalidate. A missing target preserves the captured source/item for gate order.

Tests cover carry with NPC target, equipment and cargo ownership, active player
target, TradeMode cargo rejection, a non-Playing session, and a selected item
with a missing target. `WorldHub.TryGetMobSnapshot` includes summons, but no
targeted summon test exists. `NPC sell authoritative lookup` passed. Engine.Tests
ended 571/573; the two remaining failures are `authenticated startup requires
ItemList before bind` (loopback listener bind) and
`serialized network stream concurrent writes` (`SocketException 10013`), both
blocked by sandbox network permissions. Log:
`Server/Backup/Build/shop-sell-authoritative-lookup-tests.log`. Full
`WydCdk.Engine.slnx` build passed directly with 0 warnings and 0 errors using
SDK 10.0.401; `dotnet --info` identified the SDK and exited 1 because access to
Service Control Manager was denied. The lookup is not dispatched from
`Program.cs` or a complete `0x037A` handler. Next: assemble the authoritative
snapshots into the ordered evaluator and implement a revalidated sale completion
through `GetRecycleBin` before dispatch, mutation and relay. No Host smoke,
visual E2E, schema change, deploy, Site change, commit, or push was performed.

## SHOP-005 - ordered `_MSG_Sell` gate composition (03/11/2026)

`LegacyNpcSellFlowRules.Resolve` composes the prepared gates in the TMProject
7.69 sequence: HP/`USER_PLAY`; active-trade cancellation plan; cargo rejection
during TradeMode; blocked account; source; selected-item pointer availability;
target ID/merchant/view; item eligibility; `EF_VOLATILE == 1`; and resale price
with class-3 modifier and city tax. At `ReadyForSaleMutation`, it returns a
`LegacyNpcSellPreparedSale` with the exact item, post-tax net price, village and
whether `GetRecycleBin` insertion is required. The context is explicit and the
evaluator is read-only; it neither captures live state nor sends packets.

The evaluator does not invoke `LegacyNpcSellRecycleBinHandler`, because that
helper mutates the session recycle-bin before the later sale body. Until one
revalidated completion path updates the selected inventory, Gold and recycle
bin together, dispatching only through the helper could publish a buyback item
without completing its sale. The stateful helper and the complete `_MSG_Sell`
handler therefore remain unconnected.

Tests assert gate precedence (HP before trade; trade and TradeMode before the
blocked-account check; account before source; target before empty-item handling;
volatile before price) and the valid post-tax result. `NPC sell ordered flow
rules` passed. Engine.Tests finished at 572/574; the same two sandbox loopback
failures remain: `authenticated startup requires ItemList before bind` and
`serialized network stream concurrent writes` (`SocketException 10013`). Log:
`Server/Backup/Build/shop-sell-flow-rules-tests.log`. Full solution build passed
with 0 warnings and 0 errors. SDK 10.0.401; `dotnet --info` identified the SDK
but exited 1 on Service Control Manager access denial. No dispatch, Host smoke,
visual E2E, persistence/schema change, deploy, Site change, commit or push.
Next: design the revalidated sale commit including the 7.69 recycle-bin
insertion.

## SHOP-005 - live `_MSG_Sell` preparation coordinator (03/11/2026)

`LegacyNpcSellPreparationCoordinator.PrepareAsync` now assembles the playing
session, live seller MOB/position, TradeMode (`LegacyAutoTradeBook` listing),
trade opponent, account `IsBlocked`/cargo snapshot, selected item, target,
item-table price/volatile ability, city and tax into the ordered pure flow.
It short-circuits the early HP/trade/cargo gates before account-store access,
rechecks TradeMode after the separate lookup, resolves the target's live
kind/position/merchant flag before applying city tax, and returns only a
read-only `LegacyNpcSellPreparedSale`. It does not mutate inventory, Gold or
recycle-bin state. WorldHub and account-store reads are not one transaction;
the eventual commit must revalidate them together.

The reference comparison exposed an unsafe edge in the previous trace: both
TMProject 7.69 `_MSG_Sell.cpp:168-173` and W2PP `:178-183` call
`BASE_GetVillage`, accept `sVillage == 5` because the guard is `> 5`, then read
`g_pGuildZone[5].CityTax`, while `MAX_GUILDZONE == 5` and the five city
rectangles produce IDs 0..4. The out-of-city path is undefined behavior; the
earlier blanket statement that it reaches `LABEL_S2` was inaccurate. C# now
returns `VillageFiveCityTaxUnverified` with no flow/plan for this case. It
uses the configured city tax or existing default for zones 0..4 and does not
invent a tax for zone 5.

Tests cover an in-city post-tax plan with no inventory/recycle-bin mutation, a
non-Playing short circuit before storage, and the village-5 no-plan boundary.
`NPC sell live preparation coordinator` passed. Engine.Tests finished at
573/575; the two remaining failures are the existing loopback bind case
(`authenticated startup requires ItemList before bind`) and
`serialized network stream concurrent writes` (`SocketException 10013`). Log:
`Server/Backup/Build/shop-sell-live-preparation-tests.log`. The full
`WydCdk.Engine.slnx` build passed with 0 warnings and 0 errors. SDK 10.0.401;
`dotnet --info` exited 1 because access to Service Control Manager was denied;
log: `Server/Backup/Build/shop-sell-live-preparation-dotnet-info.log`. No
`0x037A` dispatch, Host smoke, visual E2E, schema, deploy, Site change, commit
or push. Next: define a revalidated atomic item/Gold/recycle-bin commit and
resolve the out-of-city tax policy before dispatching `_MSG_Sell`.

## SHOP-005 - revalidated MOB sale commit (03/11/2026)

`LegacyNpcSellCompletionRules.TryBuild` accepts only a ready live preparation
from equipment/carry and copies the expected/updated MOB plus all ten recycle-bin
slots into a plan. It uses the post-tax price already produced by the prior
flow. City IDs must be 0..4; village 5 produces no commit plan. Coin uses a
checked `long` sum: values above signed int32 are `CoinOverflowUnverified`,
while a representable sum follows the strict 7.69 rule (`old + price < 2B`
adds; otherwise Coin is set to 2B). Cargo is still rejected because the account
snapshot store has no versioned compare-and-mutate contract.

`WorldHub.TryCommitNpcSell` takes `gate` and revalidates the full seller MOB,
position, separate Coin, recycle-bin state, current target kind/position/
merchant marker, and current city/tax before applying MOB, Coin, and recycle-bin
state together. For equipment it also checks and clears the independent
`ClientEquipment[position]` copy and resets `RuntimeParryAdjustment`, matching
the existing equipment-write behavior. The conditional rollback checks the
committed snapshots and restores both equipment representations, Coin, all
recycle-bin slots, and the previous runtime parry adjustment. A new defensive
`TryGetClientEquipmentSnapshot` reads the independent 18-slot runtime state
without normalizing it from MOB, allowing tests/callers to observe divergence.

The sidecar is not a second live mutation API: `LegacyFileAccountStore` and
`MariaDbWorldCharacterStore` save it through `IClientEquipmentStateStore` on the
existing character-state save path. `WorldHub.TryGetCharacterSnapshot`
synchronizes MOB slots 0..15 into the runtime equipment array before departure
persistence, so a committed slot clear reaches the sidecar on that path. That
save remains separate from the MOB write; this recut does not claim a durable
multi-store transaction or persist the transient recycle bin. It also does not
revalidate the external account gate under one lock or update guild tax/EXP.

Reference order: TMProject 7.69 `_MSG_Sell.cpp:187` calls `GetRecycleBin`
before seller mutation, then credits/clamps Coin, clears the item, adds the
`MSG_Sell` response, sends `SendEquip` for equipment, and finally `SendEtc`.
W2PP `_MSG_Sell.cpp` has no `GetRecycleBin` call. The C# slice intentionally
stops at the in-memory WorldHub transition: no `0x037A` dispatch, buyback,
log/notice, external guild tax/EXP credit, persistent account write, or relay.

Tests cover carry and equipment commit/rollback, stale seller/equipment/target/
recycle-bin rejection, capped Coin, cargo rejection, and unverified int32
overflow. `NPC sell revalidated MOB commit` passed. With TEMP/TMP isolated
inside the workspace, Engine.Tests finished 574/576; the same two loopback
failures remain: `authenticated startup requires ItemList before bind` and
`serialized network stream concurrent writes` (`SocketException 10013`). Log:
`Server/Backup/Build/shop-sell-mob-commit-tests.log`. Full solution build passed
with 0 warnings/errors. SDK 10.0.401; `dotnet --info` exited 1 due to Service
Control Manager access denial; log:
`Server/Backup/Build/shop-sell-mob-commit-dotnet-info.log`. No Host smoke,
visual E2E, schema, deploy, Site change, commit, or push. Next: define the
durable sale transaction and response/relay ordering before connecting `0x037A`.

## SHOP-005 - NPC sale city/global tax EXP (03/11/2026)

`LegacyNpcSellPriceMath.Calculate` now retains `GrossAmount`: the price after
the resale formula/class-3 bonus and before `CityTax`; `Amount` remains the net
amount used for Coin and the recycle bin. `LegacyNpcSellFlowRules` carries both
values in `LegacyNpcSellPreparedSale`. `LegacyNpcSellTaxExperienceMath.TryCalculate`
ports the source's two integer truncations, `(gross-net)/2` and then `/2`, and
rejects negative or inverted gross/net inputs.

TMProject 7.69 `_MSG_Sell.cpp:189-203` and W2PP
`Source/Code/TMSrv/_MSG_Sell.cpp:197-211` calculate the same `tcoin`. After the
7.69 `GetRecycleBin`, both check city `GuildImpostoID[sVillage]` first and global
`GuildImpostoID[4]` second. Each recipient requires ID `>= MAX_USER` and
`< MAX_MOB`, `Mode != MOB_EMPTY`, positive `tcoin`, and EXP strictly below its
own threshold (200,000,000,000 city; 20,000,000,000,000 global). Each gate adds
the full `tcoin`, so overshoot is allowed. The sale source does not check the
recipient's Guild against `ChargeGuild`; that Tiny Combine gate is not reused.
In village 4, both ordered gates can credit the same NPC twice. W2PP matches the
tax formula, order and EXP gates but omits the 7.69 `GetRecycleBin` stage.

`WorldHub.TryCommitNpcSell` revalidates the sale and applies eligible recipient
MOB EXP updates under the same `gate` as seller item, Gold, equipment mirror and
recycle-bin state. A missing, inactive or out-of-range NPC does not reject the
sale. The outcome carries one initial/final `LegacyGuildTreasuryNpcUpdate` per
changed NPC; repeated village-4/global credit updates one final snapshot while
preserving the original MOB for rollback. Rollback validates every current
recipient snapshot and registration before changing any state, so one stale
recipient cannot cause a partial rollback. With `--npc-root` configured, the
host persists these registered treasury MOB snapshots in the next 12-second
checkpoint; this does not coordinate the NPC file with the seller account save.
The sale still has no dispatch, response or relay.

The independent review found a shared registration boundary mismatch:
`RegisterGuildTreasuryNpcLocked` previously required `ConnectionId > 1000`,
while the reference defines `MAX_USER` as the first NPC ID and the sale/Tiny
consumers accept `ID >= MAX_USER`. The helper now accepts
`[LegacyMobAiPlanner.MaximumPlayerId, MaximumMobId)`, i.e. `[1000,25000)`, and
does not register `MAX_MOB`. New sale and Tiny regression cases exercise ID
1000 and passed after the correction.

RED confirmed the gross price field, pure `tcoin` helper and treasury mutation
were absent. After implementation, the full Engine.Tests runner finished
575/577 with workspace-local TEMP/TMP. The groups `NPC sell price math`, `NPC
sell tax experience math`, `NPC sell revalidated MOB commit`, and
`Tiny combine credits the registered guild treasury on success` passed. The
only remaining failures were the two sandbox loopback cases:
`authenticated startup requires ItemList before bind` and
`serialized network stream concurrent writes` (`SocketException 10013`). Full
`WydCdk.Engine.slnx --no-restore` build passed with 0 warnings/errors. SDK
10.0.401; `dotnet --info` exited 1 on Service Control Manager access denial;
log: `Server/Backup/Build/shop-sell-tax-dotnet-info.log`. No durable sale
transaction, Host smoke, visual E2E, schema, deploy, Site change, commit or push.
Next: define durable account/sidecar/recycle-bin/treasury commit and recovery,
then response ordering before dispatching `0x037A`.

## SHOP-005 - NPC sale response sequence plan (03/11/2026)

TMProject 7.69
`Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Sell.cpp:224-229`
echoes the accepted `MSG_Sell`; when the source is equipment (`MyPos < 8`),
`SendEquip(conn, conn)` multicasts the equipment projection to the inclusive
33x33 view excluding the seller; then `SendEtc` updates the seller.
`SendFunc.cpp:1181-1227,1289-1326` builds those equipment and `UpdateEtc`
frames. W2PP `Server/W2PP/Source/Code/TMSrv/_MSG_Sell.cpp:229-234` preserves
the success echo/equipment/`SendEtc` order, but omits the 7.69 `GetRecycleBin`
stage.

`LegacyNpcSellResponsePlanner.TryBuild` validates the decoded request and its
target/source/slot against `LegacyNpcSellCompletionOutcome`, preserves the
request header on the `0x037A` echo, and returns the ordered response steps.
Carry sale produces seller echo then seller `0x0337`. Equipment slots 1..7 add
`0x036B` to the captured view recipients with the seller removed; equipment
slots 8..15 omit this frame, matching the source condition. Invalid checksum,
parse failure, or request/outcome mismatch returns no plan. The planner does
not send frames, register a handler, or dispatch `0x037A`.

Persistence remains a separate source contract: `ProcessSecMinTimer.cpp:180`
calls periodic `SaveUser`, and its guild-tax NPC save path writes MOB files;
`Recycle` belongs to the live session and is not part of the account MOB
checkpoint. The C# player character currently saves on departure. The existing
12-second guild-treasury loop iterates registered IDs, including sale tax MOBs
updated by `TryCommitNpcSell`; it does not coordinate that NPC save with the
seller account checkpoint. Next: compare and port the 7.69 player save
cadence/contract, then wire commit and this response plan into the `0x037A`
dispatch and relay.

RED confirmed the missing planner/types at compile time. GREEN: the new
`NPC sell response sequence` group passed. The complete Engine.Tests run ended
576/578; the two remaining failures are unchanged sandbox loopback cases:
`authenticated startup requires ItemList before bind` and
`serialized network stream concurrent writes` (`SocketException 10013`). The
full `WydCdk.Engine.slnx --no-restore -m:1` build passed with 0 warnings/errors.
SDK 10.0.401 was identified; `dotnet --info` exited 1 because access to Service
Control Manager was denied. No Host smoke, visual E2E, schema, deploy, Site
change, commit, or push was performed. Runner output is recorded at
`Server/Backup/Build/shop-sell-response-tests.log`. Independent review found no
further issue after the focused invalid-request matrix was added.

## SHOP-005 - independent seller and guild-treasury checkpoints (04/11/2026)

TMProject 7.69 configures `TIMER_SEC=500ms` and `TIMER_MIN=12000ms` in
`Server.cpp:4977-4978`; `ProcessMinTimer` writes live `GuildImpostoID` MOBs to
their NPC files (`ProcessSecMinTimer.cpp:2627-2653`). W2PP matches the timer
values (`Server.cpp:4086-4087`) and tax-MOB save path
(`ProcessSecMinTimer.cpp:2585-2611`). The corresponding player `SaveUser` scan
in both sources advances every eight 500 ms ticks and saves at most one
eligible connection (`ProcessSecMinTimer.cpp:114,160-188` in TMProject;
`123,169-197` in W2PP). The client receives the sale response after the
in-memory `_MSG_Sell` mutation; neither source creates an account+treasury
transaction.

In C#, `WorldHub.TryCommitNpcSell` mutates seller MOB/Coin, the transient
recycle bin, and eligible treasury MOB snapshots under the world lock. The
authenticated Host then sends the sale success response; it does not invoke an
account or treasury writer there. The seller reaches storage through the
serial character checkpoint (one eligible slot per nominal 4 s pass, returning
to each active character roughly every `N × 4 s`) or one best-effort
logout/disconnect attempt. The character checkpoint keeps its account fields
and 18-slot sidecar together: the file adapter uses journal v2 and the MariaDB
adapter uses one `Serializable` transaction. MariaDB integration has not been
validated locally (latest provider 1042 occurred before DDL).

Treasury durability is a separate file path. `--npc-root` enables the 12-second
`RunGuildTreasurySaveLoopAsync`; `SaveRegisteredAsync` visits registered IDs
sequentially and writes each raw MOB through a same-directory temporary file
and `File.Move(..., overwrite: true)`. A treasury file is loaded by its exact
generator filename. There is no journal joining treasury files to the seller
blob/sidecar, nor a transaction across multiple treasury MOBs. A write error
is logged and retried at the next interval. During shutdown, the accept-loop stops first; the Host drains connection tasks so departure saves finish, awaits this periodic loop, then runs one final treasury sweep when the NPC store is configured. A final sweep error is collected while offline status is still published. The recycle bin is not persisted or restored.

The independent durable outcomes after a process interruption are: neither
side checkpointed; seller only; treasury only; or both sides. Account-journal
recovery restores/replays the account blob and equipment sidecar together. The
treasury writer replaces one raw MOB path at a time, leaving the previous file
until replacement. It does not replay a committed sale event or repair a
seller/treasury mismatch after restart.

The new `NPC sell and treasury independent checkpoints` test exercises the real
WorldHub and file store: a committed sale changes city/global treasury EXP in
memory from `1000/2000` to `1125/2125`, leaves both raw files unchanged, then
`SaveRegisteredAsync` writes the new EXP values. The authenticated headless
`--npc-sell-smoke` now stops the Host with the seller connected, verifies
seller Coin/carry after shutdown and requires the final treasury checkpoint log.
It does not prove nonzero treasury bytes or a joint transaction. Fresh
validation: solution build passed with 0 warnings/errors; Engine.Tests passed
`591/591` with local filesystem and loopback access. The restricted run ended
`545/590`, with 45 access-denied failures (`UnauthorizedAccessException`/
`SocketException 10013`); the authorized rerun passed. SDK 10.0.401 was
identified; `dotnet --info` exited 1 because Service Control Manager access was
denied. Empty-carry resync and out-of-view close-shop smokes also passed. Evidence is under
`Server/Backup/Build/npc-sell-empty-carry-resync-20261104/` and
`Server/Backup/Build/npc-sell-close-shop-20261104/`. No visual E2E,
schema, Site, deploy, commit or push was performed.

## SHOP-005 - periodic `SaveUser` payload and C# writer audit (04/11/2026)

TMProject `Source/Code/Basedef.h:1450-1468` defines `MSG_DBSaveMob`; its
`TMSrv/Server.cpp:8910-8945` producer sends `_MSG.ID`, `Slot`, `MOB`, `Cargo`,
account `Coin`, `ShortSkill`, `AccountName`, `Export`, `Extra`, affects,
`Donate`, `VemProEternal`, `PartyPassword`, and `QuestDiaria`.
`DBSrv/CFileDB.cpp:1236+` persists those payload fields into the account file;
nonzero `Export` also calls `DBExportAccount`. W2PP `Basedef.h:1300-1316` and
`TMSrv/Server.cpp:7845-7877` define/send the corresponding message without
`VemProEternal` and `PartyPassword`; its DB receiver is
`DBSrv/CFileDB.cpp:1134+`. The 7.69 contract is canonical; this internal
data-server message is not a client wire frame.

Both periodic `SaveUser` functions contain the same assignment defect:
`CountMob1`, `CountMob2`, then `CountMob3` are each written to
`QuestDiaria.MobCount1` (TMProject `Server.cpp:8932-8936`; W2PP
`Server.cpp:7864-7868`). Because the message is zeroed first, the DB receivers
persist zero for `MobCount2/3`. The alternate `MSG_SavingQuit` builders copy
all three fields correctly (TMProject `Server.cpp:8726-8729`; W2PP
`Server.cpp:7697-7700`); the defect is in the `SaveUser` helper that sends
`MSG_DBSaveMob` and is used by the timer and `CharLogOut`.

The C# checkpoint contract is deliberately narrower than the internal message.
`LegacyCharacterCheckpointRuntime` captures the live `WorldHub` MOB, position,
MOBEXTRA, 32 affects, and all 18 client equipment positions. The store writes
the MOB bytes with the persisted `SkillBar` overlaid, saves SPX/SPY from the live
position, preserves account `Coin` at `AccountCoinOffset`, and commits Extra,
affects and the versioned equipment sidecar as one checkpoint. The 05/11
follow-up names the account projection `BankCoin` and the MOB field
`MobCarriedGoldOffset`; checkpoint, character-state, guild, and atomic-state
writers no longer copy carried Gold into the bank field. File-store tests pass
for preservation; MariaDB write/rollback integration remains pending. This
treats the live MOB as
the Host's character snapshot while retaining `SkillBar`, whose `0x0378`
`SetShortSkill` writer is independent.

The fields outside that snapshot remain owned by their existing storage paths:
`ShortSkill` is updated by `TrySaveCharacterShortSkillsAsync`; Cargo and
`QuestDiaria` have no mutable live-session writer for the online NPC-sale path;
the file adapter's Donate field is an account-wide writer at `DonateOffset`;
and the optional MariaDB Donate Shop stores `accounts.donate` separately from the
world character blob. A specialized AutoTrade purchase commit does mutate the
seller Cargo/bank Coin in its backend transaction, together with buyer MOB/carried Gold and
the listing, but it does not create a live Cargo owner for NPC sale. The checkpoint
leaves those blob ranges untouched. `VemProEternal` and
`PartyPassword` are TMProject-only additions absent from the active W2PP save
ABI and have no C# session writers. Nonzero `Export` invokes
`DBExportAccount` in the reference; the Host does not reproduce that side
effect. The 18-slot sidecar is a C# client extension and is not a
`MSG_DBSaveMob` field.

The file adapter now uses `LegacyAccountFileStateGate`, a process-local
`SemaphoreSlim` keyed by normalized `accountRoot`. `LegacyFileAccountStore` and
`LegacyAutoTradeFileStore` share it when configured with the same root; it
serializes their account readers/writers and journal recovery. This closes a
same-process lost-update window where the checkpoint could replace the account
image after an AutoTrade purchase changed seller Cargo. It is not a cross-process
or distributed lock. The checkpoint writes account image and equipment sidecar
through journal v2 while holding that gate: prepared entries roll back,
committed entries replay, and the older two-account journal v1 still recovers.
The MariaDB checkpoint locks the current account blob with `FOR UPDATE`, then
upserts the blob and equipment row in one `Serializable` transaction. The
AutoTrade purchase commit also locks its account rows in deterministic order
and writes Cargo and MOB state in a `Serializable` transaction; other blob
mutations use the same row-lock/transaction pattern. Donate Shop writes to
`accounts.donate` are outside that checkpoint transaction; the checkpoint does
not touch that table.

This contract does not port the defective daily-counter assignments or fields
without a live C# source. It also does not combine the seller account checkpoint
with the 12-second guild-treasury MOB save or transient recycle bin. The runtime
round-robin is implemented over IDs `1..999`, nominally one eligible character
every four seconds; its focused tests cover empty-slot scans, wrap, stale
sessions, non-overlap, and logout coordination. The new root-gate test passes;
with `TEMP/TMP` under the workspace, Engine.Tests ended `590/592`, with only the
two loopback tests blocked by `SocketException 10013`. The full solution build
passed with 0 warnings/errors. Logs: `Server/Backup/Build/shop-005-account-file-writer-coordination-20261104/`.
MariaDB remains unvalidated because provider 1042 occurred before DDL. The
authenticated `0x037A` dispatch is implemented in the following section;
Cargo NPC sale, buyback, `MSG_Buy` business behavior, stock and visual E2E remain
pending.

## SHOP-005 - Cargo ownership and writer coordination (04/11/2026)

TMProject 7.69 and W2PP `_MSG_Sell.cpp` resolve the selected Cargo item from
`pUser[conn].Cargo` and call `BASE_ClearItem(Item)` after crediting seller Coin,
before echoing `MSG_Sell` and sending `SendEtc`. Periodic `SaveUser` copies that
same session array into `MSG_DBSaveMob`. The C# sale preparation instead reads
Cargo from `LegacyAccountSnapshot`; `WorldHub` has no mutable account Cargo and
rejects cargo item moves as `CargoUnavailable`. The `MSG_Sell` completion commit
therefore continues to support only live carry/equipment. Re-reading the stored
snapshot for each request is not a substitute for live ownership: it would leave
the current session able to resell a backend-cleared item.

`LegacyAutoTradeFileStore` and `MariaDbWorldCharacterStore` contain a specialized
AutoTrade purchase writer that compares the seller Cargo item before clearing
it and updates seller Coin, buyer MOB/Coin and listing. That proves one
operation-specific backend mutation, not a reusable live Cargo owner. For the
file backend, `LegacyAccountFileStateGate` now shares one process-local gate
between account checkpoints and AutoTrade file/journal writes configured with
the same root. The `account and auto-trade writers share account-root gate` test
passed. The gate does not coordinate multiple processes. MariaDB uses
`FOR UPDATE` and `Serializable` transactions for the same account row, but live
database integration remains unvalidated.

Next Cargo work requires a versioned mutable session owner plus a backend
compare-and-swap that persists cleared Cargo and seller MOB/Coin together. The
private per-slot checkpoint/logout lease cannot be assumed to cover sale; use
the focused online writer map below to design a shared reservation before
expanding the round-robin snapshot or adding the Cargo mutation.

### Focused model provenance audit for the Cargo recut (05/11/2026)

| C# model/class | Provenance and current responsibility | Decision for this recut |
|---|---|---|
| `LegacyAccountSnapshot` | Parses the active `W2ppAccountFileV1` account blob, including `Cargo[128]` at offset 3480 and account-level `BankCoin` at offset 4504. `BankCoin` is bank balance `pUser.Coin`; each `LegacyCharacterSlot.Coin` comes from a character MOB. It is a persistence projection, not an authoritative mutable account aggregate; the shallow `IReadOnlyList` surface does not make nested arrays immutable. `ToConfirmation()` also projects login wire output from this persistence record. | Keep it at the legacy storage boundary during migration. Do not make it the runtime owner; split blob decoding from login/wire projection when that surface is next refactored. Preserve the explicit `BankCoin`/carried-Gold distinction. |
| `LegacyItem` | Exact eight-byte `STRUCT_ITEM` value codec in `WydCdk.Protocol`; it is reused by account storage, MOB equipment/carry, and wire projections. Neither 7.69 nor W2PP has this C# type; both define the native `STRUCT_ITEM` ABI. | Treat it as a legacy codec/transport shape, not the new Cargo domain model. The account owner and Cargo rules must not depend on offsets or `LegacyItem` serialization; map at the adapter boundary. Avoid a broad rename/refactor of all existing item paths. |
| `LegacyCharacterCheckpointSaveRequest` / `ICharacterCheckpointStore` | C# persistence port carrying raw MOB/Affect/MOBEXTRA buffers and Protocol `LegacyItem` equipment, with no separate account-bank Coin value. 7.69/W2PP serialize an internal `MSG_DBSaveMob` containing both `MOB` and `pUser.Coin`; neither source defines this C# request. The 05/11 continuation removed the old `MOB.Coin` to `AccountCoinOffset` copy from file and MariaDB checkpoint, character-state, guild, and atomic-trade writers. | Keep the checkpoint operation separate from account Cargo ownership. Preserve the account bank Coin when saving a character MOB; do not derive it from `MOB.Coin`. The new Cargo commit must be narrow, not another raw field added to this checkpoint DTO. |
| `pUser.Coin` / `LegacyAccountSnapshot.BankCoin` / `MOB.Coin` | Separate source balances: `STRUCT_ACCOUNTFILE.Coin` is account bank balance; `_MSG_Deposit.cpp`/`_MSG_Withdraw.cpp` transfer between it and `pMob[].MOB.Coin`, and `SendCargoCoin` sends `pUser[].Coin`. AutoTrade `_MSG_ReqBuy.cpp` credits seller `pUser.Coin`; NPC `_MSG_Sell.cpp` credits seller `pMob[].MOB.Coin`. C# snapshot exposes `BankCoin` separately from carried character Gold; `WorldHub.Participant.Coin` is carried Gold. | Preserve distinct `BankCoin` and `CarriedGold` concepts. NPC Cargo sale updates `Cargo` + seller `MOB.Coin` only and preserves bank Coin. AutoTrade credits only the seller's bank; buyer purchase cost changes buyer MOB.Coin and leaves the buyer bank balance intact. File tests cover byte preservation; the MariaDB lifecycle/checkpoint/Cargo/guild integration passed five times against a disposable VPS loopback fixture on 06/11. |
| `WorldHub.Participant` / `LoginSessionRegistry` | Runtime state is keyed by connection ID. `LoginSessionRegistry` is a `ConcurrentDictionary<int, LoginSession>`; `Open` reserves only the connection ID, and successful authentication stores the canonical account name without searching for another session for that account. `WorldHub` likewise stores participants by connection. Neither owner currently carries mutable Cargo. | Keep character/connection state here. Introduce one account-scoped Cargo owner shared by all active sessions for the same backend account identity and world; after a durable change, refresh all those sessions' Cargo projection. Do not reject duplicate-account login as a side effect of this slice. |
| `LegacyNpcSellAuthoritativeLookup` / `LegacyNpcSellPreparationCoordinator` | Read-only lookup and application orchestration for the C# `_MSG_Sell` path. The 7.69/W2PP handlers read `pUser[conn].Cargo` and `pMob[conn].MOB`; C# currently gets Cargo from `LegacyAccountSnapshot` and does not mutate it. | Preserve the read-only preparation boundary. Resolve Cargo through the runtime account owner and capture its revision; do not let lookup mutate storage or turn the coordinator into a wire/parser class. |
| `LegacyNpcSellCompletionRules` | Pure completion validation/projection for sale outcome, grounded in `_MSG_Sell` and `GetRecycleBin` behavior. It operates on the seller's carried `MOB.Coin`; it is a C# rule object, not a source-named legacy class. | Keep calculations and revalidation pure. Feed Cargo + carried-Gold changes to a narrow application commit; leave account bank Coin untouched and do not embed provider calls or blob offsets in these rules. |
| `WorldHub.TryCommitNpcSell` | Revalidates and mutates the in-memory player, recycle bin, and treasury under the WorldHub lock; it is not a persistence transaction. | Keep as the runtime facade. Cargo sale commits its account-store CAS before publishing the runtime mutation; treasury/recycle-bin durability remains separate. |
| `LegacyNpcSellResponsePlanner` | Builds the response sequence for `_MSG_Sell`; TMProject/W2PP send the corresponding panel, request echo, visibility update and final state sync from the handler. | Keep wire ordering in this planner. The Cargo storage commit returns an outcome; the host sends this plan only after durable commit and runtime publication. |
| `LegacyCharacterCheckpointRuntime` / `Program._MSG_Sell` | Checkpoint/logout acquires the account reservation before its per-connection `slotGates[connectionId]`, revalidates, captures, and awaits the save. `_MSG_Sell` holds the seller account reservation through Cargo CAS/runtime commit (also around Carry/Equipment runtime commits), then releases it before response frames. | The account reservation is shared by the focused live account/Coin writers and checkpoint/departure. No `WorldHub` monitor is held across I/O. |
| `LegacyFileAccountStore` / `MariaDbWorldCharacterStore` | Storage adapters currently live in `WydCdk.World`. Both implement multiple store ports; the MariaDB adapter also owns AutoTrade commit logic. File uses a root-shared process-local gate and journal; MariaDB uses `Serializable` transactions and `FOR UPDATE` on the account row. AutoTrade compares the seller's bank Coin and selected Cargo, writes the buyer's changed MOB, credits only the seller's bank Coin, and updates the listing; the buyer's account-bank Coin is preserved. Character checkpoint/state writers preserve `AccountCoinOffset` and keep carried Gold inside MOB. Cargo sale CAS now rereads under serialization, compares complete expected Cargo and expected carried Gold, patches only those fields, and preserves other blob bytes. | Bank separation is applied locally. The 05/11 continuation also removed an AutoTrade buyer write that copied carried Gold into `AccountCoinOffset`; file contract tests assert preservation. MariaDB adapters compile and implement the CAS/pair update, but live database rollback/concurrency integration remains pending because no local service was listening. Keep provider I/O outside domain rules; later assembly separation remains incremental BL-000 work. |

#### Focused online writer map (05/11/2026)

This map covers the focused active C# writers of character Gold and account
Cargo plus checkpoint/departure. It is not the repository-wide BL-000 class
inventory. These paths acquire `LegacyAccountOperationGate` reservations using
canonical account keys; bilateral operations deduplicate and sort the keys.
`WorldHub` serializes each in-memory mutation under its synchronous monitor,
but no monitor is held across awaited storage I/O.

| Active path | State it changes | Current persistence/coordination boundary |
|---|---|---|
| `Program._MSG_Sell` / `WorldHub.TryCommitNpcSell` | Carry/equipment and Cargo sales update the seller MOB and carried Gold; all sale paths may affect the transient recycle bin and treasury. Cargo sale also updates the account Cargo owner. | Seller account reservation spans revalidation, durable Cargo CAS (Cargo source only), and conditional runtime publication. Conflict/ambiguous outcomes are authoritatively reconciled or mark the account recovery-required. Release occurs before all socket writes. Carry/equipment retain their existing checkpoint/logout durability; treasury/recycle-bin saves remain independent. |
| `WorldHub.TryApplyTinyCombine` / `LegacyTinyCombineOutcomeBuilder` | Carry and carried Gold; success may also credit an in-memory treasury. | Account reservation spans the runtime mutation/rollback. Character checkpoint/logout and treasury saves remain independent. |
| `WorldHub.TryApplyCoinConsumable`, final-skill fee in `TryApplySkillBonus`, and `TryConsumeGoldenShieldCoin` | Carried Gold in MOB. | Each Host path reserves the account around the runtime mutation; participant Coin stays synchronized with MOB.Coin. Checkpoint/logout uses the same account reservation. |
| Castle quest reward and party combat reward paths | Recipient MOB carried Gold/experience; recipients may differ from the attacker's connection. Party membership changes can change reward recipients. | The Host reserves the affected canonical account set around the attack batch and relevant party membership transitions; it releases before relays/socket writes. |
| `WorldHub.TryCompleteTrade` / Host trade completion | Both participants' MOB inventories and carried Gold. | Distinct account keys are deduplicated and acquired in stable order around snapshots, pair-atomic store update, runtime publication, and conditional rollback. Same-account/two-slot trades use one reservation. |
| Guild invite (`Program` + `WorldHub.ApplyGuildInvite`) | Source carried Gold and both participants' guild fields. | The Host holds sorted distinct account reservations and calls `TrySaveGuildInvitePairAsync`, an expected-value, pair-atomic update that supports same-account/two-slot and different-account pairs in both file and MariaDB adapters. Runtime state is published only after durable success. |
| AutoTrade purchase coordinator and account stores | Buyer MOB/carry/carried Gold; seller account Cargo and bank Coin; listing. | Sorted AutoTrade book connection-ID lease is outermost, followed by sorted account reservations and backend CAS. The account Cargo owner is reconciled after commit; buyer `AccountCoinOffset` remains preserved. File coordination is process/root-local; MariaDB row transactions serialize durable storage. |
| `LegacyCharacterCheckpointRuntime` / disconnect save | Captures the full current MOB (including carried Gold), Extra, affects, equipment, and position; Cargo and account bank Coin are preserved. | Account reservation precedes the per-connection slot gate across revalidation, capture, and I/O. Departure reuses the already-held account lease after the AutoTrade book lease. |

A direct source scan of `WorldHub*.cs` for the MOB Coin offset and participant
Coin assignments agrees with the rows above: active mutations are final-skill
fee, Vol 185 consumable, Golden Shield, castle quest reward, AutoTrade buyer
purchase, guild invite, bilateral trade, NPC sale, and Tiny combine. The
`SetCharacterState` assignment copies the loaded balance into a newly
registered runtime participant and is initialization, not a live transaction.
The focused runtime paths now share account reservations with checkpoint and
departure, and guild invite has pair-atomic persistence. This closes the
focused Cargo/carried-Gold reservation slice, not the repository-wide BL-000
class inventory.

A separate first-pass scan found additional `WorldHub` paths that mutate fields
persisted by the whole-MOB checkpoint: ground pickup, carry split/move and item
use/refinement, resurrection-item consumption, score/resource updates, combat,
player-affect timers, and quest/NPC rewards. These remain candidate groups, not
a complete call graph; helper writes and Host dispatch still need classification
for repository-wide BL-000. The Cargo sale CAS itself patches only its owned
Cargo and seller `MOB.Coin` fields from the serialized account image; it does not
expand the periodic checkpoint to Cargo. The remaining whole-MOB writer audit is
still required before claiming global snapshot/write serialization.

#### Crosswalk for active C# Gold writers (05/11/2026)

This crosswalk identifies the direct state-writing handlers that the account
reservation audit must cover. It does not replace the per-module behavior
comparison or decide unported features.

| Active C# path | TMProject 7.69 writer | W2PP writer |
|---|---|---|
| `WorldHub.TryCommitNpcSell` | `TMSrv/_MSG_Sell.cpp` | `TMSrv/_MSG_Sell.cpp` |
| `LegacyTinyCombineOutcomeBuilder` / `WorldHub.TryApplyTinyCombine` | `TMSrv/_MSG_CombineItemTiny.cpp` | `TMSrv/_MSG_CombineItemTiny.cpp` |
| `WorldHub.TryApplyCoinConsumable` (Vol 185) | `TMSrv/_MSG_Use_Item.cpp` | `TMSrv/_MSG_UseItem.cpp` |
| `WorldHub.TryApplySkillBonus` final-skill fee | `TMSrv/_MSG_ApplyBonus.cpp` | `TMSrv/_MSG_ApplyBonus.cpp` |
| `WorldHub.TryConsumeGoldenShieldCoin` | `TMSrv/_MSG_Attack.cpp` | `TMSrv/_MSG_Attack.cpp` |
| Castle quest reward in `WorldHub` | `TMSrv/CCastleZakum.cpp` | `TMSrv/CCastleZakum.cpp` |
| `WorldHub.TryCompleteTrade` | `TMSrv/_MSG_Trade.cpp` | `TMSrv/_MSG_Trade.cpp` |
| `WorldHub.ApplyGuildInvite` | `TMSrv/_MSG_InviteGuild.cpp` | `TMSrv/_MSG_InviteGuild.cpp` |
| AutoTrade purchase coordinator/store | `TMSrv/_MSG_ReqBuy.cpp` | `TMSrv/_MSG_ReqBuy.cpp` |
| `LegacyCharacterCheckpointRuntime` and account stores | `TMSrv/Server.cpp::SaveUser` | `TMSrv/Server.cpp::SaveUser` |

#### Direct reference-writer scan (05/11/2026)

A read-only textual scan for direct `MOB.Coin` assignments (`+=`, `-=`, and
`=`) under both TMSrv source trees found 42 matching sites in TMProject 7.69
and 35 in W2PP; these counts can include commented/inactive text and are only
search triage, not counts of proven runtime writers. The live C# Cargo/carried-Gold paths found by the corresponding
offset/property scan are the rows above; in the account blob, AutoTrade is the
only current Cargo writer and its seller credit is the only current
`AccountCoinOffset` writer. Character checkpoint/state/guild/trade writers
preserve that bank field. The source trees also contain additional producer
families to classify against C# classes and dispatch: `MobKilled`, NPC/task or
quest handlers (`NTask_*` in 7.69; `_MSG_Quest` in W2PP), `_MSG_Buy`,
`_MSG_GetItem`, `_MSG_MessageWhisper`, teleport, `Deposit`/`Withdraw`, challenge,
and item quest paths; TMProject also has direct `_MSG_Quiz` and
`_MSG_ProcessCommand` writes, while W2PP's command setter is in `imple.cpp`.
`Deposit`/`Withdraw` are not dispatched by the current C# Host. This scan is
limited to direct field-assignment text and does not establish equivalence for
whole-struct copies, indirect helpers, or every class in BL-000.

TMProject/W2PP Deposit and Withdraw transfer between `pUser.Coin` and
`MOB.Coin`; those handlers are not currently dispatched by the C# Host. The
references also contain additional MOB.Coin producers in quest, NPC, item,
combat, and event code, so this focused map does not close BL-000.

#### BL-000 — scope of the AutoTrade operation lease (05/11/2026)

| C# type/path | Observed behavior | Boundary for the Cargo reservation |
|---|---|---|
| `LegacyAutoTradeOperationGate` / `LegacyAutoTradeOperationLease` | Each `LegacyAutoTradeBook` owns one gate keyed by connection ID. `AcquireAsync` deduplicates and sorts IDs, rejects IDs outside `1..65535`, awaits the per-ID semaphores with cancellation, and releases partial acquisitions in reverse order on failure. The lease verifies that it belongs to that book, covers the requested ID, and has not been disposed; disposal is idempotent and releases in reverse order. Semaphore instances stay in the host-lifetime registry. | Retain it as the outer AutoTrade operation lease. It serializes operations that use the same book and connection IDs only; it is process-local and does not reserve account identity across distinct sessions or coordinate writers that do not acquire this lease. |
| `LegacyAutoTradeOwner` / account identity | A listing owner is `(AccountName, CharacterSlot)`. The book validates slots `0..3` and trims/uppercases names. Authenticated sessions are already canonical uppercase ASCII names of length `1..15`: both `LegacyFileAccountStore` and `MariaDbAccountAuthenticator` use `LegacyAccountNameRules`; the MariaDB world adapter enforces the same alphabet/length. `wyd_world_account_state` uses `ascii_bin` and primary key `(account_name, world_key)`. | Use the normalized account as the identity component, then include the actual backend/store namespace: MariaDB world key; file account root. `LegacyAutoTradeOwner` alone omits that namespace and includes a character slot, so it is not the Cargo owner key. The file adapter has no `world_key` input, so verify root sharing across UP/PVP before relying on per-world in-process owners; `LegacyAccountFileStateGate` cannot coordinate a second process sharing that root. The Site-owned `accounts` table DDL is outside this checkout; its collation/uniqueness cannot be inferred here. |
| `LegacyAutoTradeCloseCoordinator` | Close captures the connection pair, acquires the book lease, then revalidates session identities and the pair before reserving/closing trade state. Pending-close recovery reacquires the saved connection IDs. Existing tests cover ID order/deduplication, cancellation releasing a partial lease, foreign/disposed lease rejection, and close cancellation before/after store commit. | Keep the book lease outside any future account reservation. Close recovery is listing/trade recovery, not an account Cargo recovery protocol. |

The focused writer/gate paths and guild invite pair transaction are implemented
and covered by Engine.Tests. This remains a source audit for global BL-000: the
candidate declaration count and textual scans do not prove a complete runtime
call graph, and additional whole-MOB writer groups still need classification.

#### BL-000 — Tiny combine reservation boundary (06/11/2026)

`LegacyTinyCombineDispatchCoordinator.ExecuteAsync` now owns the account
reservation across `TryPrepareTinyCombine`, `TryApplyTinyCombine`, UpdateEtc and
relay-frame planning, and conditional `TryRollbackTinyCombine`. A forced null
response plan after a real accepted apply rolls the player MOB/Gold and guild
treasury back before the reservation is released. The concurrent checkpoint
test then persists the exact pre-combine MOB. A successful response plan is
returned only after release; the success test reacquires the account reservation
and checkpoints the committed MOB. Production response builders still produce
aligned frames for accepted outcomes; the forced null planner is test injection,
so this demonstrates safe rollback ordering without asserting a current
production trigger for response-plan failure.

The extraction also fixes a reachable lock-order inversion in the invalid
inventory-position path. Previously the Host kept the account reservation while
awaiting `CloseForTinyCombineInvalidPositionAndCaptureAsync`, which takes the
AutoTrade book lease; AutoTrade start takes book lease before account
reservation. The coordinator now returns the rejection and releases the account
lease before the Host starts trade cleanup. The test holds the book lease while
dispatch rejects the invalid position, then confirms caller cleanup can wait on
the book without retaining the account reservation.

TMProject 7.69 `TMSrv/_MSG_CombineItemTiny.cpp` and W2PP's same-named handler
both charge `MOB.Coin`, mutate the carry on success/failure, and then send their
message/signal frames (`SendClientMsg`/`SendClientMessage` and
`SendClientSignalParm`). Treasury-file I/O and socket writes remain in the Host
after the coordinator returns. The focused self-contained Engine.Tests runner
passed on the VPS (`PASS_TINY_COMBINE_DISPATCH_CHECKPOINT`, exit 0) using a
disposable WorldHub and in-memory checkpoint store. No authenticated network
smoke, MariaDB mutation, Site change, or game-server listener was part of this
test.

Next: continue the repository-wide BL-000 writer and lock-order audit, including
combat and timer writers that cross character checkpoint. This Tiny slice does
not close global BL-000; the outstanding declaration counts remain heuristic.
Client visual E2E remains a separate client-side validation.

#### BL-000 — combat and timer MOB writers versus checkpoint (06/11/2026)

| C# path | Runtime writes and reservation boundary | 7.69/W2PP crosswalk | Audit disposition |
|---|---|---|---|
| `Program` attack dispatcher / `WorldHub.TryConsumeSkillMana` | Skill MP is written to the attacker's `MOB` before the `attackRewardLease` is acquired. The class-one Type 6 cleanup, attacker hide/Samaritan cleanup, and per-target hide cleanup also run before that lease. | TMProject and W2PP `_MSG_Attack.cpp` debit `MOB.CurrentScore.Mp` and perform these affect transitions in the attack handler. | These are persisted MOB/Affect fields; the attack request path is not covered end-to-end by the account reservation. |
| `AcquirePartyAccountLeaseAsync` / `WorldHub.TryApplyAttackActionBatch` | The Host acquires the party accounts only around the synchronous action-batch call and passes no additional target connection IDs. The batch mutates attacker and player-target MOB/Affect under the WorldHub monitor; physical and skill branches can write a target participant outside the attacker's party. | Both `_MSG_Attack.cpp` handlers update target HP/MP and skill effects. | The party lease protects party reward/account paths, but does not cover every player state changed by the batch. The WorldHub monitor protects each in-memory operation, not the later checkpoint I/O. |
| `LegacyPlayerAffectRuntime.TickShardCoreAsync` / `WorldHub.TryProcessPlayerTimerAffects` | A private `tickGate` serializes timer shards; there is no `LegacyAccountOperationGate` reservation. The WorldHub method changes player MOB/Affect and can run area attacks against other participants before the relay awaits socket I/O. | Both `ProcessSecMinTimer.cpp` files call `ProcessAffect`; the same timer source also advances the one-user `SaveUser` scan. | Timer mutations share persisted fields with checkpoint but are not coordinated with its account/slot lease. The source comparison establishes field ownership and call paths, not equivalent concurrency semantics. |

`LegacyCharacterCheckpointRuntime` holds the account reservation and slot gate
through snapshot capture and asynchronous store I/O. Combat and player-affect
timer mutations do not take that account reservation (and do not take the
checkpoint slot gate). Therefore an overlapping mutation can occur after the
checkpoint captured its MOB and before the store finishes writing it. That can
leave a stale persisted snapshot until a later checkpoint; this is an inferred
race from the current boundaries, not a reproduced loss or a claim that runtime
state was observed to diverge.

This was a source and call-path audit only: no code or tests changed. The next
implementation slice must establish the complete set of account identities
that an attack or area timer may mutate, acquire distinct accounts in canonical
order before any of those mutations, revalidate the captured participants,
and release reservations before network relays. The target set must include
non-party player targets; simply extending the current party-only lease around
`TryApplyAttackActionBatch` would leave the preceding MP/affect writes exposed.
Global BL-000 remains open and the 643 declaration candidates remain
heuristics.

#### Observed gate order and reservation design (05/11/2026)

Observed acquisition order in the current implementation:

1. AutoTrade purchase holds the sorted book connection-ID lease, then acquires
   distinct account reservations in canonical ordinal order. The account lease
   spans state revalidation, backend commit, runtime publication/rollback, and
   Cargo-owner reconciliation.
2. Periodic checkpoint acquires the account reservation before the per-connection
   slot gate, revalidates the session, captures the WorldHub snapshot, and holds
   both through store I/O. Departure keeps an existing AutoTrade book lease
   outermost, then the stable party-account reservation, then the character slot
   gate/save. No observed path acquires an account reservation while already
   holding a checkpoint slot gate.
3. Bilateral trade and guild invite acquire sorted, deduplicated account keys
   before revalidation/snapshot and hold them through pair-atomic store outcome,
   runtime publication or conditional rollback.
4. NPC sale holds the seller account reservation through expected-state
   revalidation, Cargo CAS, and runtime publication. Conflict/ambiguous outcomes
   reload the authoritative snapshot; an unreadable or irreconcilable outcome
   marks the account recovery-required. The reservation is released before all
   socket responses and resync frames.
5. Other gated Gold writers use short synchronous WorldHub operations while the
   account reservation is held, then release before relay/socket writes. Store
   adapters do not acquire application reservations or call back into WorldHub;
   the WorldHub monitor is never held during awaited I/O.
6. Tiny combine holds the account reservation through apply, response-frame
   planning, and conditional rollback; the reservation is released before
   invalid-position AutoTrade cleanup, guild-treasury storage, and socket writes.

This gives checkpoint `account -> slot -> snapshot/save`; AutoTrade
`book(connection IDs) -> account keys -> snapshot/runtime/store/rollback`;
bilateral trade and guild invite `account keys -> runtime/pair store/rollback`;
and Cargo sale `account key -> expected-state capture -> backend CAS -> runtime
publication`. The file gate remains process-local to one account root, and
MariaDB row transactions serialize durable storage but do not replace the live
reservation. No cross-process runtime coordination is claimed.

The concrete recut is limited to NPC sale from Cargo. Runtime identity is the
backend's existing canonical account identity within its world/root, not a
connection ID or character slot. One account owner holds the full 128-slot Cargo
and a monotonically increasing in-process revision; all online characters for
that account reference it. A sell plan carries the expected owner revision and
the seller's expected carried Gold (`MOB.Coin`). The store CAS checks the
complete expected Cargo and the current `MOB.Coin` at the seller's character
slot, then writes resulting Cargo and only that `MOB.Coin` field in one file
journal commit or one MariaDB transaction. MariaDB uses a conditional
byte-range `UPDATE` at `ReadCommitted`, comparing Cargo, seller `MOB.Coin`, and
the slot's character name before splicing only those owned fields into the
current blob. It leaves the account bank Coin at
`AccountCoinOffset` untouched and patches only its owned fields, preserving all
other blob bytes. It returns explicit `Conflict` without writes when expected
values are stale. No runtime mutation or success response is published before
persistence commits under the required live mutation lease; on success, publish
the new owner revision and resync the changed Cargo slot to each active session.

The gate order above is implemented and covered for the focused Cargo/carried-
Gold writers, checkpoint/departure, bilateral trade, AutoTrade, and guild invite.
It is not a claim that repository-wide BL-000 or every whole-MOB writer has been
closed. The file gate is process-local, so this contract supports one server
process per account root; sharing that root across processes remains
unsupported. MariaDB's row transaction provides backend serialization but does
not replace expected-value CAS or a live-session reservation. The recut adds no
schema column and does not widen periodic checkpoints to Cargo.
Character checkpoint writers preserve the account bank Coin field; AutoTrade
credits only the seller's bank field and preserves the buyer's bank field, while
the buyer's purchase cost and NPC sale proceeds are carried Gold in MOB. The
focused models and current writer paths are documented here, but BL-000's
repository-wide class inventory remains open. Acceptance covers stale
conflict/no write, untouched account-byte preservation, file journal recovery,
runtime reconciliation, and the authenticated headless Cargo sale. The first
VPS MariaDB run reproduced error 1020 in the concurrent locking read; after the
conditional range-update change, five consecutive runs passed on disposable
MariaDB 11.8.5 bound to `127.0.0.1:13306`. Authenticated Cargo sale and
character checkpoint/relogin smokes also passed on the VPS. The active
`WYDCDKMariaDB` service and its database were not used. Generic
Cargo moves, buyback, `MSG_Buy`, and stock remain out of scope.
This CAS makes only the seller account blob (`Cargo` plus seller MOB/Coin)
atomic. Guild treasury, recycle bin, and their persistence remain independent;
the existing partial-durability boundary for those side effects is unchanged.

## SHOP-005 - authenticated `MSG_Sell` dispatch (04/11/2026)

TMProject 7.69 `TMSrv/_MSG_Sell.cpp` gates HP/`USER_PLAY`, closes an active
trade and returns, rejects auto-trade cargo and blocked accounts, resolves the
selected item/NPC/price, calls `GetRecycleBin`, then applies the sale. On
success it calls `SendMsgExp` with `Adquiriu %d de gold` in yellow before the
`MSG_Sell` echo, optional equipment view update for `MyPos < 8`, and final
`SendEtc`. W2PP has the same success notice/order, but its handler omits the
7.69 `IsBlocked` and `GetRecycleBin` stages; the port retains 7.69 behavior.

`MSG_Exp_Msg_Panel_` is type `0x5000`, a zero-filled `char[96]` plus `Color32`
at offset 96. `SendMsgExp` writes the seller connection ID and
`TNColor::Yellow = 0xFFFFFF00`. `ExperienceMessagePanel` encodes this fixed
layout and rejects text that cannot fit in the 96-byte field.

`Program.cs` now handles authenticated `ShopSellMessage`: HP/mode uses the
existing sync response plus `RecordCrackErrorAsync(10,23)`; active trade goes
through `LegacyAutoTradeCloseCoordinator`, sends the captured close frames and
stops the sale; blocked accounts receive the existing message panel. The Host
also sends the source-backed localized `MSG_MessagePanel` for cargo during
auto-trade (Language 170), non-merchant target (116), and `EF_VOLATILE == 1`
(167). TMProject uses `SendClientMsg`; W2PP uses `SendClientMessage`; both
zero-initialize the message with ID 0. The text follows TMProject 7.69
`Language.txt`, including the source spelling of entry 170. Invalid source,
out-of-range target, missing item, and other unlocalized gates remain silent.
For a target outside the seller's view, TMProject and W2PP both call
`SendClientSignal(ESCENE_FIELD, _MSG_CloseShop)`. Their helper zeroes
`MSG_STANDARD` then sets Type/ID only; the encoded frame is `0x0196`, ID
`30000` (`ESCENE_FIELD`) and empty payload. The C# Host now sends that frame for
the `TargetNotInView` rejection and keeps the item untouched.
The live preparation coordinator remains read-only. Accepted plans use the current
recycle-bin snapshot and `WorldHub.TryCommitNpcSell`; rejected/stale outcomes
do not emit success. If `LegacyNpcSellResponsePlanner.TryBuild` fails after
commit, the Host attempts conditional rollback before sending anything.
Successful frame order is yellow `0x5000`, exact `0x037A` echo, optional
`0x036B` to view recipients for equipment positions 1..7 with seller excluded,
then `0x0337` UpdateEtc to the seller. Carry and equipment positions 8..15 do
not add the view frame.

Carry/equipment sale retains the ordinary player checkpoint/logout durability.
Cargo sale now commits Cargo plus the seller's carried `MOB.Coin` synchronously
through one account-blob CAS before runtime publication; bank Coin and unrelated
blob bytes are preserved. The recycle bin remains transient, and eligible
treasury MOBs use the separate 12-second checkpoint. Shutdown drains accepted
connection tasks so departure saves finish, awaits the periodic treasury loop,
then performs one final `SaveRegisteredAsync` when `--npc-root` is configured.
A final-save failure is collected and logged while offline status is still
published. The seller account CAS does not make treasury/recycle-bin persistence
part of the same transaction or add cross-store replay/reconciliation.

The isolated authenticated loopback sale smoke uses temporary account, review and
NPC roots, `Client/7670/ItemList.bin`, and a single local TMProject 7.69
generator fixture (`Perzen_Normal`). The synthetic class-3 seller with learned
bit `0x1000` bypasses the normal merchant view gate for this focused fixture.
The fixture overrides `StartWait=600` so the live 200 ms NPC-AI runtime cannot
advance the synthetic merchant route during preparation/commit; the production
Host behavior is unchanged. The runner checks valid checksums and
`0x5000 -> 0x037A -> 0x0337`, then stops the Host with the seller still
connected, requires a clean exit and final treasury checkpoint log, and reads
the persisted Coin and cleared carry after process exit. Two consecutive
latest result: `NPC_SELL_LOOPBACK_SMOKE_PASS item=62 netGold=8787
response=5000-037A-0337 departure-checkpoint-confirmed`.
No visual client was launched. The authenticated headless smoke passed twice
after local loopback authorization; this validates seller checkpointing during
Host shutdown and the final treasury scan for the configured NPC root. It does
not validate MariaDB or transaction recovery across the independent stores.
Engine.Tests passed `591/591`; the full build passed with 0 warnings/errors.
MariaDB remains unverified: local `127.0.0.1:3306` is closed and the last
provider 1042 occurred before DDL. No schema, Site, deploy, commit or push.

The separate `--npc-sell-out-of-view-smoke` uses a temporary `Perzen_Normal`
fixture with `Merchant=1`, a class-0 seller, and a target outside the view. It
confirms the exact `0x0196`/ID `30000` empty frame and that the carry item remains
present. Marker: `NPC_SELL_OUT_OF_VIEW_LOOPBACK_SMOKE_PASS`. Log:
`Backup/Build/npc-sell-close-shop-20261104/out-of-view-smoke.log`.

The `--npc-sell-empty-carry-resync-smoke` uses the same disposable authenticated
Host with carry slot 0 empty. It confirms `MSG_SendItem` (`0x0182`, ID equal to
the seller connection, inventory type 1, slot 0, zero-index item), then verifies
the empty slot and Coin remain unchanged after shutdown. Marker:
`NPC_SELL_EMPTY_CARRY_RESYNC_LOOPBACK_SMOKE_PASS`. Engine.Tests passed
`591/591`; build passed with 0 warnings/errors. Logs finais:
`Backup/Build/shop-005-carry-resync-20261104/`; a primeira rodada do smoke está
em `Backup/Build/npc-sell-empty-carry-resync-20261104/`.

The authenticated `--npc-sell-cargo-smoke` covers the Cargo source through the
Host and file adapter: it logs in, sells item 62, receives the normal sale
response plus Cargo-slot resync, then verifies persisted seller Gold 8787,
cleared Cargo slot, and unchanged bank Coin 246810 after shutdown. Marker:
`NPC_SELL_CARGO_LOOPBACK_SMOKE_PASS item=62 netGold=8787 cargo-resync=sent
bank-coin=preserved`. In this run the full solution build passed with 0
warnings/errors and Engine.Tests passed `602/602` after loopback authorization.
No visual client was launched.

Remaining gates: execute the expanded `WydCdk.Character.Integration` MariaDB
fixture for Cargo CAS success/stale conflict/concurrency/rollback and guild
invite pairs (same-account and two-account). The harness compiled with 0
warnings/errors, but this run found no listener on `127.0.0.1:13306` or `:3306`,
and Docker Desktop did not make its local daemon available, so it was not run and no schema/data changed. The wider BL-000 class/call-path
inventory remains open; 643 declaration candidates are a heuristic, not
confirmed gaps. Buyback, `MSG_Buy`, stock, cross-store treasury recovery, and
visual E2E remain separate work. Negative and out-of-table item indices remain
silent: both `GetItemPointer` implementations return `NULL` before the later
resync branch.
