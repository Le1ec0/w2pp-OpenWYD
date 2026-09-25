# Baseline de arquitetura e controle do port C#

**Data:** 19/09/2026  
**Escopo:** reinicio arquitetural do emulador Server C#/.NET, cliente unico 7.670 compilado da referencia comunitaria 7.69, quatro pacotes de layout selecionaveis pelo launcher, Site 7.54 integrado e inventario do port existente.  
**Natureza:** diagnostico e plano de reorganizacao. Nao e commit, release nem autorizacao de deploy.

**Documentos operacionais:** o checklist está em [PROGRESSO.md](../../PROGRESSO.md), o backlog priorizado em [BACKLOG.md](../../BACKLOG.md) e a matriz de rastreabilidade em [PORT_TRACEABILITY.md](PORT_TRACEABILITY.md).

**Gate obrigatorio de port:** a referencia 7.69 define o contrato-alvo, mas todo recorte precisa primeiro comparar o simbolo, wire e efeito equivalente em `Server/W2PP`. Se houver diferenca, a decisao (manter, adaptar ou substituir) entra na ficha e no teste antes de qualquer implementacao C#; nao ha copia direta presumida da 7.69.

## Decisao arquitetural vigente — 19/09/2026

Este documento foi produzido como fotografia do port em 18/09/2026. Toda
referencia 7.59/7559, 7.60/7600, estrategia de extracao incremental sem reinicio
ou sincronizacao com VPS Dev abaixo e historica, nao normativa.

O produto passa a ser um emulador WYD novo em C#/.NET, reconstruido por
comportamento para falar protocolos com o cliente 3D e com harnesses headless.
Nao houve source oficial vazada. A referencia primaria e a engenharia reversa
comunitaria da linha 7.69 (`TMProject2GlobalClient`); C++ comunitario, assembly,
DLLs/hooks e emuladores sao evidencias para validar, nao autoridades oficiais. A
release planejada combina WYD.exe compilado da source TMProject e a release do
servidor C#/.NET. O repositorio inclui `WYDLauncher.exe` precompilado, mas nao foi
localizado codigo/projeto-fonte proprio do launcher; proveniencia e seguranca
permanecem pendentes. Nao incluir no primeiro smoke sem revisao; preferir iniciar
o WYD.exe diretamente ou fornecer launcher proprio. O usuario quer preservar das
referencias arquivadas apenas assets visuais selecionados: importar cada um apos
conferir formatos, dependencias e smoke visual; nao usar executaveis, SN,
serverlist, DLLs, hooks ou demais binarios de referencias antigas.

O checkout local TMProject esta no commit `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`.
O executavel rastreado `v769ClientRelease\WYD.exe` tem 4,282,368 bytes e SHA-256
`C248788036749D1728B2F2ABC5F7CE6934977350E24738021AFEC35AE6DBBE3F`, mas nao ha
prova de como foi compilado. O `.vcxproj` grava nessa pasta por padrao; a build
propria deve ocorrer em copia isolada e com output/intermediarios externos, sem
sobrescrever a referencia. O README declara codigo GPLv3, origem por
descompilacao, direitos do jogo reservados a Hanbitsoft e bugs conhecidos; isso
nao certifica seguranca nem direitos de redistribuicao de executaveis/assets.

Contrato de conectividade confirmado estaticamente na source TMProject 7.69:
`sn.bin` e lido cru em 143 bytes (`char[11][9]` + 11 inteiros little-endian), e
`sn2.bin` e opcional em 900 bytes (`char[10][10][9]`). O leitor de
`serverlist.bin` consome 7040 bytes como matriz `10x11x64`, subtraindo a chave
`szList[63-i]`; slot 0 da linha e URL HTTP de status, slots 1..10 sao hosts IPv4.
O binario empacotado tem 7045 bytes e a decodificacao de seus primeiros 7040 bytes
produz texto invalido; nao o usar como configuracao. A fonte fixa `8281` como porta
de jogo, sem porta por canal. A tabela `pKeyWord[512]` coincide exatamente com a
tabela C#, e um handshake/frame sintetico conforme a source foi aceito pelo
listener diagnostico em `127.0.0.1:8281` (`0x020D`, 116 bytes, versao 1758). O
perfil ativo `Client\7670` agora tem `sn.bin`/`sn2.bin`/`serverlist.bin` gerados
para um grupo Local, canal UP, host `127.0.0.1`, e `ClientVersion=7670`; a porta
do executavel continua fixa em 8281. O WYD.exe foi compilado em copia isolada da
source, com SHA-256
`18364BBD894D367B745FAF4F41D3AB0773C4F68624D4BAC189453678B7F30EAD`; a cópia
isolada corrigiu as seis texturas de render target para `D3DPOOL_DEFAULT`. A build
de smoke desativa chamadas diretas `ShellExecute(...)` para nao disparar o
`Change.exe` do sistema. O slot de status aponta para `127.0.0.1:8280`, ainda sem
responder local. Em 23/09/2026 o listener iniciou e o cliente abriu janela
responsiva sem o erro gráfico, mas a janela nativa não foi exposta ao controle
CUA; isso nao valida autenticacao, resposta, personagem ou entrada no mapa, e o
E2E visual em loopback continua pendente.

Arquitetura alvo aprovada em 19/09/2026: `Protocol` (frames, opcodes e layouts),
`Transport` (sockets, ciclo de conexao e serializacao), `Application` (handlers,
use cases e politicas de sessao), `Domain` (estado e regras autoritativas),
`Infrastructure` (persistencia, catalogos e provedores de dados), `Host` (CLI e
composicao), `ControlPanel` (WinForms operacional) e ferramentas isoladas de
analise/compatibilidade do cliente. Camadas mais internas nao dependem de TCP,
WinForms, banco ou binarios externos; fronteiras wire e dominio possuem testes
proprios. A source comunitaria 7.69 e autoridade de compatibilidade para o alvo,
nao modelo arquitetural nem garantia de qualidade/otimizacao. `Server\W2PP` e a
origem historica do port C# e deve ser comparado modulo a modulo com os contratos
e comportamentos 7.69. Existe um unico cliente ativo, `Client\7670`; o launcher
le `Interface=0..3` e seleciona um overlay de assets (`0=7559`, `1=7600`,
`2=7662`, `3=7670`), sem trocar `WYD.exe`, SN, serverlist, DLLs ou protocolo.
As pastas 7559/7600/7662/7669 sao referencias e
backup, nao perfis de distribuicao. Esta decisao aprova a arquitetura-alvo, nao
declara que os projetos ja foram fisicamente separados.

Versao da release: `7.670` corresponde ao inteiro obrigatorio `7670` em
`MSG_AccountLogin.ClientVersion`. A build do cliente precisa gravar esse valor
antes de enviar o pacote (constante/AssemblyInfo da source do cliente); metadados
do assembly do servidor nao mudam o frame ja recebido. O listener rejeitara
valores diferentes com mensagem de versao. Os valores observados nas fontes
anteriores (1001/1059/7640) servem apenas para mapear migracoes, nao para admitir
clientes na release 7.670.

Compatibilidade multi-cliente: clientes de familias anteriores continuam sendo
referencias e podem receber adaptadores de opcode/layout comprovados, mas precisam
ser recompilados ou ajustados para enviar `ClientVersion=7670`. Quando dois
layouts tiverem o mesmo tamanho, tamanho nao e criterio suficiente para escolher
o parser.

Primeiro recorte de compatibilidade executado em 19/09/2026: `MSG_SwapItem`
(`0x0376`) agora usa o contrato observado na source 7.69 (frame de 20 bytes: 12
de header, quatro campos de placement, `TargetID` de 16 bits e dois bytes de
padding ABI; origem antes do destino). O layout W2PP tambem tem 20 bytes, mas
usa outra ordem e `WarpID` de 32 bits, portanto nao pode ser distinguido por
tamanho. `LegacyItemDataTable` foi alinhada a
`STRUCT_ITEMLIST` cliente 7.69: 6500 registros de 164 bytes e XOR `0x5A`; aceita
o corpo de 1.066.000 bytes e o arquivo distribuido com trailer opaco de 4 bytes.
O contrato server-side 7.69/W2PP e outro: 6500 registros de 140 bytes, `nPos`
short em 134 e `Extra`/`Grade` em 136/138. `LegacyServerItemDataTable` le esse
formato separadamente, sem habilita-lo nas regras autoritativas. Build
sem erros, 207 testes e carregamento do `Client\\7669\\ItemList.bin` real pelo
listener em loopback passaram. Isso ainda nao e E2E de cliente nem extracao fisica
de camadas; o teste concorrente do `SerializedNetworkStream` tambem confirmou
blocos completos sem intercalacao em loopback; ver `PORT_TRACEABILITY.md` para
evidencia e limites. O DTO `ActionRequest` tambem preserva os tres opcodes no
round-trip e valida `VIEWGRIDX/Y=33`. O `Motion` rejeita HP exatamente zero e
emite `MSG_SetHpMode` de 20 bytes com `Mode=22`, sem relay. Os gates de celula
`0x80`/`0x20` e `BASE_GetGuild` agora estao testados no `WorldHub`; o recall
autoritativo muta a posicao, envia os indices 46/47 confirmados de `Language.txt`
por `MSG_MessagePanel` e retransmite o `MSG_Action` de teleporte no listener.
O restante do recorte continua fora do listener. O `WorldHub` agora tambem
mantem o estado bilateral de `MSG_Trade` em memoria e valida carry, saldo,
slots duplicados, itens nao negociaveis, itens de guilda, PK, checks e
cancelamento. `TryCompleteTrade` monta os carries candidatos e aplica
atomicamente carry/Gold em memoria; o dispatcher de `MSG_Trade`/`MSG_QuitTrade`
e o relay de `MSG_CNFCheck`/`MSG_UpdateCarry` agora estao ligados ao host.
`LegacyTradePersistence` monta o plano dos dois personagens, e o host chama
`IAtomicCharacterStateStore` quando o backend suporta a capacidade. O
`MariaDbWorldCharacterStore` bloqueia os dois blobs em ordem deterministica e
faz o commit conjunto em uma transacao serializavel; o `LegacyFileAccountStore`
faz o commit dos dois blobs por journal/recovery. Mesma conta nos dois lados e
falha/ausencia da capacidade ficam no rollback CAS do runtime, sem relay. A
transacao MariaDB ainda nao foi exercitada contra banco
vivo; o dispatcher de consulta/abertura e relay de autotrade foi ligado ao book
em memoria, e `LegacyAutoTradeVisualRelay` ja projeta o `MSG_CreateMobTrade`
7.69 e o envia ao dono, a vizinhanca e ao solicitante de lista.
`LegacyAutoTradeRemovalRelay` ja monta o `MSG_RemoveMob` de 16 bytes e o host
o envia a jogadores na vizinhanca 33x33 durante logout/desconexao, antes do
`WorldHub.Leave`. `LegacyAutoTradeFileStore` cobre a persistencia local opt-in
do owner/listing, localizacao e taxa, com documento versionado; o
`MariaDbAutoTradeStateStore` usa a tabela `wyd_autotrade_state` no modo MariaDB.
`LegacyAutoTradeRehydrator` le o estado e o MOB/affect do personagem no boot,
registra o NPC offline e entrega `MSG_CreateMobTrade` aos novos logins, sem
criar participante TCP. `LegacyAutoTradeReconnectCoordinator` remove o estado
persistido e fecha NPC/book do mesmo account/slot antes da confirmacao de login,
capturando a vizinhanca para o `MSG_RemoveMob`; isso segue o reset de
`TradeMode`/`AutoTrade` observado no carregamento W2PP, sem afirmar que o W2PP
possui uma politica de venda offline persistente. O smoke local exercitou uma
venda MariaDB com commit, verificacao pos-commit e replay CAS; banco remoto,
interrupcao durante venda e E2E ainda nao foram exercitados. O DTO
`AutoTradePurchaseRequest` agora fecha o `MSG_ReqBuy` 7.69 (`0x0398`,
24-byte payload/36-byte frame, padding ABI explicito) e
`LegacyAutoTradePurchaseRules` cobre os gates puros de compra.
`LegacyAutoTradePurchasePlanBuilder` prepara copias de carry/cargo, saldos e
listing pos-venda sem mutar as fontes. `LegacyAutoTradePurchaseCommitRequest`
define o compare-and-swap e `MariaDbWorldCharacterStore` grava os dois blobs e
o listing JSON na mesma transacao serializavel. O `LegacyAutoTradeBook` protege
o slot contra compra duplicada, o `WorldHub` aplica/rollbacka a metade do
comprador com checagem de coin/destino/snapshot, e
`LegacyAutoTradePurchaseExecutor` coordena o commit antes dos relays. O
dispatcher esta ligado para backends com `ILegacyAutoTradePurchaseCommitStore`;
MariaDB usa transacao serializavel e o file store local usa journal/recovery
entre arquivos. Banco ao vivo e E2E continuam pendentes.
O DTO `UpdateCarryConfirmation` de 528 bytes foi fechado separadamente e agora
e usado pelo relay completo de carry/Gold dos dois participantes.

Estrutura-alvo aprovada da solution (monolito modular; sem microservicos nesta fase):

```text
Server/
  src/WydCdk.Protocol/                 # frame codec, opcodes, layouts, DTOs
  src/WydCdk.Domain/                   # estado/invariantes por contexto de jogo
  src/WydCdk.Application/              # use cases, ports, policies, orchestration
  src/WydCdk.Transport.Tcp/            # sockets, framing, lifecycle, backpressure
  src/WydCdk.Infrastructure/           # MariaDB, arquivos, catalogos, mapas
  src/WydCdk.Server.Host/              # composition root, CLI, process host
  src/WydCdk.Server.WinForms/          # operacao/observabilidade; sem regra de jogo
  tools/WydCdk.ClientTool/             # PE/disassembly/patch por assinatura
  tests/WydCdk.Domain.Tests/
  tests/WydCdk.Protocol.ContractTests/
  tests/WydCdk.Infrastructure.IntegrationTests/
  tests/WydCdk.Server.NetworkTests/
  tests/WydCdk.Client.E2E/              # harness contra release propria 7.69
Client/                                 # perfis/releases, nao source de ferramentas
Site/                                   # checkout Frontend-WYD-CdK
Backup/                                 # local-only; historico, laboratorio e Tools/refs
  Tools/ReferenceSources/TMProject2GlobalClient/ # source 7.69, read-only
```

Dentro de Domain, manter contextos coesos (Account/Character, World, Movement,
Inventory, Combat, Social, Commerce e Events) sem transformar cada contexto em
um servico separado. Protocol define apenas contrato wire e traduz formatos da
release-alvo para DTOs; Domain nao referencia Protocol, TCP ou persistencia.
Application orquestra casos de uso e portas; Infrastructure implementa as portas
e carrega dados versionados; Host compoe as dependencias. O modelo C# canonico
nao deve ser struct C++/blob persistido por conveniencia: formatos legados ficam
em adaptadores de leitura/migracao. WinForms consulta casos de uso e nao vira
autoridade de estado do jogo. Capturas golden so entram sem segredos/dados
pessoais e com release/hash e proveniencia registrados.

## DTOs, adapters e fixtures golden — fatia personagem/equipamento (21/09/2026)

O primeiro mapa ABI confirma que “18 equipamentos” e uma capacidade do contrato
wire/layout cliente 7.69: `STRUCT_SELCHAR` expoe `Equip[4][18]`, e o cliente
associa os indices 16/17 aos grids `NewSlot1/2`. Isso nao prova, por si so, que
as regras do TMSrv 7.69 aceitam esses slots: o header/servidor TMSrv ainda usa
`MAX_EQUIP=16`, assim como W2PP. O modelo canonico deve conseguir preservar 18
posicoes sem habilitar automaticamente regras de uso para 16/17; validar item,
mutacao, save e relay e um trabalho separado.

Achado adicional de fechamento de build: `TMSrv.vcxproj` inclui `..\Basedef.cpp`
para compilacao; esse arquivo inclui o `Basedef.h` comum (18 equipamentos,
`STRUCT_MOB` de 1040 bytes), enquanto as unidades locais como `CMob.cpp` e
`_MSG_Move_Item.cpp` incluem `TMSrv/Basedef.h` (16 equipamentos, MOB de 816
bytes). Existe uma copia `TMSrv/Basedef.cpp` coerente com 16, mas ela nao consta
como unidade compilada no `.vcxproj`. Assim, o source tree configura helpers
comuns com uma ABI diferente da estrutura passada pelos callers TMSrv. Por
exemplo, `BASE_GetMobAbility` percorre `MAX_EQUIP` do header que o compilou;
aplicado a um MOB TMSrv, offsets posteriores tambem divergem. `BASE_ClearMob`
com esse header teria `sizeof(STRUCT_MOB)=1040`, nao 816, caso recebesse um
ponteiro TMSrv. Isso e uma incoerencia estatica da source/build, nao prova de que
um binario publicado executou esses caminhos. Os DTOs wire do cliente devem usar
o header do proprio cliente como oracle; nenhuma helper do TMSrv deve virar
golden comportamental antes de fixar e verificar a unidade/ABI realmente usada.

Fronteiras de arquitetura e estagio de implementacao:

| Camada | Tipo/adapter proposto | Responsabilidade e limite |
|---|---|---|
| Domain | `Character`, `CharacterAttributes`, `CharacterEquipment` | Estado sem offsets, alinhamento, `MAX_*` de um protocolo ou dependencias C++; representar as 18 posicoes e deixar a politica de elegibilidade sob regra testada. |
| Protocol 7.69 | `ClientScoreV769`, `CharacterSelectionV769`, `CharacterMobV769`, `CharacterLoginV769`, `CreateMobConfirmationV769`, `EquipmentAppearanceV769`, `UpdateAffectConfirmationV769`, `UpdateScoreConfirmationV769`, `UpdateEtcConfirmationV769` | DTOs projetam o modelo para layouts medidos da release-alvo. `ClientScoreV769`, `CharacterSelectionV769`, os envelopes New/Delete e os adapters W2PP de account/selection/character MOB existem com goldens x86 testados. CharacterLogin, CreateMob, UpdateEtc e UpdateScore estao integrados ao listener; UpdateAffect esta ligado no login e nas mutacoes com snapshot autoritativo quando os campos cabem no wire; UpdateEquip esta ligado nas trocas de equipamento e atualizacoes de montaria. O word 1 de LearnedSkill e zerado para personagens W2PP porque skills 200..246 nao existem na origem. O contrato cliente de UpdateScore e 152 bytes; o header DBSrv comum 7.69 mede 147 e permanece uma divergencia documentada. UpdateEtc mede 48 bytes em cliente/W2PP, mas troca LearnedSkill[2] por Magic entre os layouts. O leitor `LegacyServerItemDataTable` permanece separado do `LegacyItemDataTable` cliente/converter: 140 bytes server-side contra 164 bytes cliente, confirmados pelo probe Win32. EXT2, semantica completa dos indices 16/17, demais recalculos e E2E seguem pendentes. | 
| Protocol W2PP | DTOs `W2ppScoreV1`, `W2ppSelectionV1`, `W2ppMobV1` (816 bytes) e wires antigos somente onde ainda forem necessarios | Preservar compatibilidade/fixture historica sem permitir que esses formatos antigos sejam o modelo de dominio ou o encoder default do cliente 7.69. |
| Infrastructure | `W2ppAccountSnapshotReaderV1` | Leitura explicita do blob atualmente esperado pelo port. O `LegacyAccountSnapshot` C# le campos ate 7945 bytes; o `sizeof` Win32 W2PP e 7952. Isso e um reader/projection parcial, nao um round-trip comprovado do arquivo nativo. |
| Infrastructure, opcional | `V769AccountFileReader` | So criar se houver necessidade concreta de importar arquivos brutos 7.69. O header comum mede 8792 bytes, com offsets diferentes; nao substituir silenciosamente o storage C# atual nem adotar o struct C++ como schema canonico. |
| Application | `CharacterSelectionProjector` e `CharacterLoginProjector` | Converter snapshot/aggregate para DTO da release ativa; coordenar regras de spawn/estado sem escrever offsets nem compartilhar arrays wire com Domain. |

`LegacyItem` pode ser reaproveitado como codec de oito bytes quando o wire exigir,
mas `LegacyScore` nao deve permanecer simultaneamente modelo de atributos e
serializer universal: hoje ele grava o layout W2PP (Level int e bytes
Merchant/AttackRun/Direction/ChaosRate), enquanto o cliente 7.69 interpreta um
Level short e usa bytes +12/+13 como Reserved/AttackRun, com +14/+15 de
alinhamento. O mesmo tamanho 48 nao autoriza compartilhar o DTO.

Estado das fixtures e serializers:

- [x] Gerar oracle Win32 de `STRUCT_SELCHAR` cliente 7.69, com sentinelas para
  quatro personagens, scores e 18 equipamentos. A fixture sintetica de 904 bytes
  e seu manifesto estao em `tests/WydCdk.Engine.Tests/Fixtures/V769/`; o gerador
  fica em `tools/PortAudit/Generate-V769CharacterSelectionGolden.ps1`.
- [x] Comparar a selecao (904) byte a byte com o oracle e verificar o envelope
  New/Delete: quatro bytes de alinhamento, `SelChar` +16 e frame de 920 bytes.
- [x] Gerar oracle Win32 de `MSG_CNFAccountLogin` do header cliente 7.69:
  payload de 1916 bytes/frame de 1928, campos e padding comparados byte a byte;
  adapter W2PP versionado preserva os 120 slots representaveis e rejeita perda
  silenciosa de item nos slots 120..127.
- [ ] Gerar oracles Win32 restantes para headers 7.69 e W2PP, com sentinelas de
  personagem, score, item, posicao, skill e indices 0/15/16/17; salvar fixtures
  sinteticas com manifesto de origem, commit/hash, x86, compilador e `sizeof/offsetof`.
- [x] Compor e integrar o character login usando o contrato cliente 7.69 de
   1728 bytes ja comparado com header e handler; a cadeia inicial de
   AccountLogin/New/Delete/CharacterLogin/CreateMob/UpdateEtc/UpdateScore esta
   ligada ao listener. UpdateAffect e UpdateEquip tambem estao ligados nos
   caminhos com snapshot autoritativo cobertos; validar captura/runtime/E2E ainda
   falta. O oracle de CreateMob (236) e UpdateEquip (68) permanece versionado, e
   bytes sem semantica mapeada ficam opacos/reservados.
- Manter oracles W2PP separados para os contratos de 16 slots e a leitura de
  storage; testar leitor do blob sintetico de 7945 bytes e tambem registrar que
  o struct nativo mede 7952. Um golden de storage 7.69 so e necessario se o
  importador opcional for aprovado.
- Para regras de dominio, usar fixtures de comportamento: criar/deletar/selecionar
  personagem, preservar os itens em todas as 18 posicoes, atualizar appearance,
  login e falhas/concorrencia. Byte golden nao substitui esses testes; vice-versa,
  fixture de comportamento nao prova ABI.

A sonda `Server/tools/PortAudit/Measure-StructLayouts.ps1` mede os quatro headers;
os geradores Win32 de selecao, account login, character login e `STRUCT_MOB`
recompilam os oracles contra a source cliente 7.69. Os DTOs/adapters seguem
isolados e nao substituem os encoders ativos W2PP. O adapter do MOB cobre os
campos com correspondencia provada, as conversoes estreitas checked e o sufixo
de appearance derivado do KILL_MARK; o LearnedSkill word 1 e zerado por ser uma
extensao 7.69 ausente no W2PP, sem reutilizar o `SecLearnedSkill` distinto. Permanecem pendentes goldens dos demais wires, envio de
Affect separado, crosswalk EXT2, ABI inconsistente do TMSrv privado, integracao e
E2E visual. A extensao segura mapeia `MOBEXTRA.Hold` -> `EXT1.Data[0]` FakeExp e
zera bytes sem equivalencia provada.

Antes de mover codigo, mapear cada modulo do port iniciado em W2PP para manter,
adaptar, substituir ou estacionar frente ao comportamento/contratos 7.69. Preservar
evidencias e alteracoes do usuario. Separar testes unitarios, wire-contract,
integracao, smoke headless/rede e E2E na release propria compilada da TMProject,
com servidor C#. A UI selecionada do 7600 entra apenas como asset comparativo/
visual. O checkout Dev/source e credenciais Git na VPS foram removidos: VPS
somente recebe releases/runtime autorizados.

## Inventario estrutural inicial — 19/09/2026

A reorganizacao fisica da raiz foi executada sem mover codigo do servidor. O
checkout `Site` recebeu os 75 arquivos da base escolhida; a arvore anterior esta
preservada em `Backup/Site-live-before-7.54`. O checkout do servidor permaneceu
em `master`, com alteracoes preexistentes preservadas e sem staging/commit.

A fonte primaria local esta em `Backup/Tools/ReferenceSources/TMProject2GlobalClient`.
O clone `xiaoseqq/TMProject2GlobalClient` estava limpo em `main`, no commit
`3ccc4d3377a1226648d58d59f87d7a020cabc8a1`, com o subtree `v769ClientRelease`.
Esse e um pin inicial de proveniencia; cada comportamento portado ainda deve
apontar ao arquivo/simbolo e ao wire especificos da release.

Leitura estrutural dos projetos .NET. As contagens abaixo incluem somente arquivos
`.cs` mantidos no projeto; `bin/` e `obj/` foram excluidos. A classificacao por
responsabilidade foi confirmada por leitura das classes e dos pontos de composicao.

| Projeto atual | Arquivos `.cs` mantidos | Dependencias observadas | Destino arquitetural inicial |
|---|---:|---|---|
| `WydCdk.Protocol` | 51 | sem referencia a outros projetos | manter como wire; extrair estado de cliente e separar modelos binarios de dominio gradualmente |
| `WydCdk.World` | 42 | `Protocol`, MySqlConnector 2.6.2, BCrypt.Net-Next 4.0.3 | decompor por classe entre Domain, Application e Infrastructure |
| `WydCdk.Server` | 4 | `Protocol`, `World`; assembly `WydCdk.Server.Core` | separar Transport, roteamento/use cases e composition root Host |
| `WydCdk.Server.ControlPanel` | 2 | WinForms, Windows-only; assembly `WydCdk.Server` | manter como UI/operacao, dependente de interfaces/casos de uso |
| `WydCdk.ClientPatcher` | 1 | Iced 1.21.0 | dividir CLI, analise somente-leitura e mutacao de binario protegida por identidade/assinatura |
| `WydCdk.Engine.Tests` | 1 | referencia Protocol, World e Server; runner console | separar unit, wire-contract, integration-store, network smoke e client E2E |

A solution `WydCdk.Engine.slnx` aponta para esses cinco projetos de produto e um
projeto de teste. O primeiro corte deve mapear classes e chamadas antes de
renomear/mover projetos: `World` mistura persistencia e regra; `Server` combina
host/transporte/dispatch; e o runner de teste e um executavel unico. O C++ em
`W2PP` e as ferramentas/fontes sob `Backup/Tools` continuam referencias externas
ao runtime C#.

### Inventario por classe e decisao de destino — 19/09/2026

Este mapa e um plano de migracao, nao uma ordem para mover arquivos em lote. A
regra e preservar o comportamento atual com testes de caracterizacao e de wire
antes de alterar cada fronteira.

| Modulo observado e evidencias | Diagnostico concreto | Destino inicial | Decisao |
|---|---|---|---|
| `Protocol`: `LegacyFrameCodec`, `LegacyFrameStream`, `PacketHeader`, mensagens `*Request`/`*Confirmation` | Codec e DTOs de frame convivem com projeções de layout legado (`LegacyItem`, `LegacyScore`, `LegacyCharacterSelection`); `World` tambem depende deste projeto | `WydCdk.Protocol` com contratos wire e codecs; modelos canonicos de jogo em `Domain`, com adaptadores de wire | **Manter/adaptar** por contrato e por mensagem; golden tests antes de separar layouts compartilhados |
| `Protocol/DonateShopClientState` e `World/LegacyDonateShopMessages` | Estado de painel Donate e compra Cash in-game estao no assembly wire/mundo | Sem destino no fluxo comercial-alvo; a loja deve ser web iniciada pelo Premium Neil | **Estacionar** o painel/opcodes customizados; nao implementar UI native Donate no cliente 7.670 |
| `World/AccountLoginCoordinator`, `AccountSecureCoordinator`, `CharacterLoginCoordinator`, `CreateCharacterCoordinator`, `DeleteCharacterCoordinator`, `LegacyHpMpMath`; adapters `W2ppCharacterMobV1Adapter`/`W2ppCharacterLoginV1Adapter` | Orquestram sessao, validacao, armazenamento e transicoes; adapters traduzem MOB W2PP e compoem o confirmation 7.69 isoladamente; `LegacyHpMpMath` cobre somente `BASE_GetHpMp` | `WydCdk.Application` com portas de conta/personagem e adapters na fronteira Infrastructure/Protocol; calculos puros no Domain | **Manter comportamento e adaptar**; caracterizar fluxos e falhas primeiro; o calculo HP/MP tem vetores isolados, nao fecha os demais recalculos nem o pipeline de login |
| `World/LoginSessionRegistry` | Maquina de estados de conexao/login, politica de transicao e limites de movimento/ataque | `Application` para ciclo da sessao; politicas puras para `Domain` quando separadas | **Manter e decompor** sem alterar transicoes observadas |
| `World/WorldHub` (`WorldHub` ~5,400 linhas) | Um objeto concentra participantes, NPCs, inventario, combate, party/guild, eventos, extensao custom Donate, timers e chamadas de envio | `Domain` por agregado/contexto (`World`, `Combat`, `Inventory`, `Social`, `Events`) + saidas como efeitos/planos; Cash web fora do fluxo NPC | **Manter como fachada temporaria**; estacionar `TryPurchaseDonateItem`; extrair contextos com testes |
| `World/Legacy*Math`, gates e estados | Mistura funcoes deterministicas com modelos/decisoes legadas e catalogos | Regras deterministicas/invariantes em `Domain`; carregamento/parsing em `Infrastructure` | **Manter/adaptar seletivamente**; nao copiar de referencias sem equivalencia comportamental |
| `World/LegacyFileAccountStore`, `MariaDbAccountAuthenticator`, `MariaDbWorldCharacterStore`, `MariaDbConnectionFactory`, `DonateShopStores` | SQL, BCrypt, IO de arquivo e blobs legados ficam no mesmo assembly das regras; `World` possui pacotes externos | `Infrastructure` implementando portas definidas em `Application`; Cash commerce integra Site e bau sem compra NPC | **Manter adapters de conta/personagem**; estacionar stores/catalogos do painel Donate ate definir o checkout web |
| `World/Legacy*Catalog`, `LegacyCharacterTemplateStore`, mapas/dados de skill/item/NPC | Tipos e algoritmos de leitura de CSV/binario coexistem com modelos usados em runtime | `Infrastructure` para loaders; `Domain` para definicoes imutaveis consumidas pela regra | **Separar parsing de semantica** com fixtures dos arquivos reais e validacao de release/hash |
| `Server/Program.cs` (`ServerOptions`, listener e roteamento sequencial por `TryParse`) | Composition root, configuracao, accept/connection loops, despacho de mensagens, mutacoes e respostas de negocio no mesmo top-level file; handlers instanciam coordenadores inline | `Server.Host` para CLI/composicao; `Transport.Tcp` para socket/framing/lifecycle; `Application` para handlers | **Extrair em fatias por familia de mensagem**, sem trocar o protocolo nem reescrever tudo de uma vez |
| `Server/SerializedNetworkStream`, `ServerWireLog`, `ServerStatusFilePublisher` | Serializacao de writes, log wire e publicacao do heartbeat estao dentro do projeto de host atual | `Transport.Tcp`, observabilidade do Host e adapter de status/health em `Infrastructure` | **Manter** os comportamentos operacionais; separar contratos e responsabilidades |
| `Server.ControlPanel/MainForm` | UI WinForms inicia/encerra o processo Core e exibe stdout; secoes de ferramentas aparecem como placeholders | `Server.WinForms` dependente de contratos de operacao | **Manter separado**; nao colocar regra autoritativa de mundo na UI |
| `ClientPatcher/Program.cs` | CLI inclui inspect/disasm/find e tambem apply/restore/embed; tabelas de patch possuem hash e bytes esperados | Ferramenta de analise somente-leitura e ferramenta de patch separadas, com manifest de binario/assinatura | **Isolar**; nenhum endereco de uma build vira regra global, e alteracao exige hash + bytes de contexto |
| `Engine.Tests/Program.cs` | Um runner manual concentra parsers, wire, dominio, adapters e fluxos; fixtures de release resolviam `Tools/Reference759` na arvore antiga | Projetos distintos de Domain, Protocol contract, Infrastructure integration, network smoke e client E2E | **Preservar os testes existentes** e migrar sem perder cobertura; corrigir fixtures para `Backup/Tools` |
| `World/WorldLoop.cs` | Implementa um canal single-reader e estado proprio, mas busca no host/testes nao encontrou consumidor | Sem destino ate comprovar necessidade e semantica de propriedade do estado | **Estacionar para decisao**; nao presumir que hoje serializa o `WorldHub` |

#### Chamadas, sincronizacao e concorrencia observadas

O Host aceita conexoes TCP em loops concorrentes; cada conexao faz decode e percorre
uma cadeia de `TryParse` em `Program.cs`. Mutacoes centrais do `WorldHub` usam um
`gate` comum, enquanto cada participante guarda delegate de envio; o projeto
`WorldLoop` nao e referenciado pelo Host nem pela suite atual. Isso comprova que a
serializacao do estado atual e baseada no lock do `WorldHub`, nao no actor loop.
Ainda nao e prova suficiente de ausencia de corridas envolvendo persistencia:
essa analise deve cobrir por operacao a ordem `validar -> mutar -> persistir ->
confirmar/enviar`, cancelamento, timeout, rollback e sessoes simultaneas antes de
extrair stores ou mudar o modelo de concorrencia.

#### Sequencia de migracao recomendada

1. **Congelar comportamento observavel:** separar e executar os testes existentes;
   catalogar opcodes, tamanhos, checksum, efeitos de estado e persistencia por handler.
2. **Separar Host/Transport/Application sem mudar regra:** tirar parsing de CLI,
   conexao TCP e roteamento do `Program.cs`; preservar o mesmo contrato de entrada
   e saida por golden/network tests.
3. **Isolar Infrastructure:** criar portas de conta/personagem e mover file/MariaDB
   adapters e loaders mantendo os blobs legados enquanto a equivalencia e validada.
4. **Decompor WorldHub por contexto:** iniciar por uma area com fronteira/testes
   claros; cada extracao deve conservar o facade e ser comportamento-neutra.
5. **Separar cliente/tooling e expandir validacao:** tirar estado visual de `Protocol`,
   dividir o tooling e adicionar testes de rede/headless e E2E na release propria
   compilada da TMProject, com hash/configuracao registrados; importar somente UI
   selecionada do 7600 depois de validar compatibilidade.

Checkpoint inicial anterior ao primeiro recorte 7.69: a primeira execucao da
suite depois da reorganizacao fisica identificou caminho de fixture obsoleto para
Reference759. O resolver foi atualizado para procurar a referencia arquivada em
`Backup/Tools/Reference759`; naquele checkpoint, a build passou e os 126 testes
passaram. Esse resultado historico foi supersedido pela validacao atual de 134
testes descrita acima; nenhum dos dois checkpoints e E2E com cliente real.

O restante deste arquivo e um snapshot de 18/09/2026. Nao atribuir vigencia a
seus status, fronteiras de projeto ou plano de migracao sem nova verificacao.

## 1. Parecer executivo

O port deixou de ser apenas um prototipo de protocolo: ja existe um nucleo funcional com autenticacao, selecao/criacao/exclusao de personagem, sessao de jogo, movimentacao, combate parcial, itens, comunicacao social, party, guilda e persistencia em arquivo legado/MariaDB. O codigo tambem contem uma extensao custom de compra Donate in-game, mas ela esta fora do comportamento-alvo e estacionada. O modelo-alvo Cash e Site/Premium Neil -> debito -> bau; a reconstituicao historica ainda precisa de evidencia. O ultimo baseline registrado em `PROGRESSO.md` foi build sem warnings/erros e 135 testes.

O problema principal agora nao e ausencia de codigo; e ausencia de uma estrutura de controle capaz de responder, para cada comportamento legado:

1. qual simbolo C++ foi analisado;
2. qual pacote de entrada e saida existe;
3. qual funcao C# executa o caso;
4. qual estado de mundo e alterado;
5. quando e como a persistencia ocorre;
6. quais testes provam equivalencia;
7. se o cliente real foi validado;
8. se o comportamento foi somente construido, validado localmente, publicado ou confirmado em E2E.

**Classificacao atual do produto:** migracao funcional controlada, ainda pre-producao.  
**Classificacao atual da arquitetura:** funcional, mas com divida estrutural alta.  
**Classificacao atual do controle do projeto:** insuficiente sem uma matriz de rastreabilidade e uma fonte unica de status.

A recomendacao e congelar este baseline, criar a rastreabilidade por mensagem/funcao e iniciar a extracao de fronteiras antes de aumentar o tamanho do `WorldHub` e do dispatcher. O port deve continuar fiel primeiro; a reorganizacao deve preservar comportamento e ser protegida por testes.

## 2. Evidencia observada no checkout

### 2.1 Estado de versionamento

- Repositorio analisado: `D:\Jogos\WYD CDK\Server`.
- Branch: `master`.
- Base observada: `d85eec6f` (`Corrige snapshot de personagens na selecao`), acompanhando `origin/master`.
- A working tree contem alteracoes nao commitadas do port e arquivos novos de protocolo/mundo.
- O commit continua deliberadamente adiado; a arvore local e a arvore `C:\WYDCDK\Dev\Server` devem permanecer espelhadas por hash antes do proximo bloco.
- `C:\WYDCDK\Server` e a arvore publicada e fica fora deste fluxo de desenvolvimento.

### 2.2 Build e testes

- `dotnet build .\WydCdk.Engine.slnx --nologo -v:q`: validacao local anterior com 0 warnings e 0 erros.
- `WydCdk.Engine.Tests`: 124 casos passando na validacao atual; a suite foi ampliada durante esta reanalise com wire e dominio de `_MSG_SplitItem`, wire de `_MSG_UpdateItem`, catalogo/mutacao de altura de itens de mapa, mascara oficial, fluxo autoritativo de chave/portao, sinal `_MSG_StartTime`, timers de expiracao da quest Zakum, fechamento automatico de portoes estaticos, decay de drops dinamicos e status HTTP com heartbeat/expiracao.
- O runner de testes ainda e um executavel manual, nao um framework de testes com categorias, fixtures, fixtures de ambiente e relatorios de cobertura.
- A contagem antiga espalhada em `AGENTS.md`, `PROGRESSO.md` e notas historicas nao deve ser tratada como status atual; a contagem atual precisa ser extraida da execucao.

### 2.3 Tamanho e concentracao de responsabilidade

| Area | Evidencia medida | Diagnostico |
|---|---:|---|
| `src/WydCdk.Server/Program.cs` | 1.952 linhas | transporte, roteamento, casos de uso, respostas, loops e configuracao no mesmo arquivo |
| `src/WydCdk.World/WorldHub.cs` | 4.853 linhas | agregado central com estado, regras de combate, itens, party, eventos, NPCs e operacoes de comunicacao |
| `src/WydCdk.ClientPatcher/Program.cs` | 1.079 linhas | inspeccao PE, disassembly, decode, patch e CLI no mesmo modulo |
| `tests/WydCdk.Engine.Tests/Program.cs` | 2.987 linhas | todos os casos, helpers, fixtures e runner manual em um arquivo |
| `src/WydCdk.Protocol` | 85 arquivos / 19.165 linhas | wire e modelos de mensagem relativamente bem isolados, mas sem registro central de compatibilidade |
| `src/WydCdk.World` | 87 arquivos / 83.872 linhas | maior concentracao de regra de negocio e dados legados |

Tambem foram observados `artifacts`, `publish-wirelog`, logs, `.obj`, executaveis nativos, dumps de laboratorio e saidas de publicacao dentro da arvore do servidor. Mesmo quando estao ignorados pelo Git, isso mistura fonte, evidencia, build e laboratorio e dificulta reproduzir o que e release.

## 3. Modelo C4/arc42 da situacao atual

### 3.1 Contexto do sistema

```text
Cliente WYD 7.60 / 7.55.9 / 7.66.2
        |
        | TCP legacy frames: login, mundo, itens, combate, social
        v
WydCdk.Server.Core  <---->  MariaDB / arquivos legados de conta
        |
        +----> Site/IIS: cadastro, saldo/catalogo e status
        |
        +----> logs de wire e diagnostico operacional

Fontes de equivalencia (somente leitura/referencia):
  Server/W2PP + Backup/Tools/Reference759 + binario corrigido 7600
```

### 3.2 Containers atuais

| Container | Papel real hoje | Fronteira esperada |
|---|---|---|
| `WydCdk.Protocol` | headers, codec, parsers e confirmations | somente wire, tamanhos, opcodes e DTOs imutaveis |
| `WydCdk.Server` | listener, autenticacao, roteamento, respostas, loops, configuracao | adapter TCP + composicao; nao conter regra de dominio |
| `WydCdk.World` | sessoes, estado de mundo, regras e stores | dominio/application separados por contexto |
| `WydCdk.Server.ControlPanel` | painel WinForms separado do core | observabilidade/operacao, sem regra de jogo |
| `WydCdk.ClientPatcher` | CLI de PE/disassembly/patch | ferramenta de compatibilidade, com comandos e artefatos separados |
| `WydCdk.Engine.Tests` | testes manuais de wire, dominio e fluxos | suites unitarias, contract, integration e E2E identificadas |
| `W2PP`/`Backup/Tools/Reference759` | referencia historica de comportamento | nunca dependencia de runtime do port |

### 3.3 Pipeline de uma chamada hoje

```text
Socket.Accept
  -> LegacyFrameStream/decoder
  -> ServerWireLog (RX)
  -> ClientTickPolicy
  -> cadeia de TryParse em Program.cs
  -> verificacao de LoginSessionRegistry
  -> Coordinator ou WorldHub
  -> LegacyFileAccountStore/MariaDbWorldCharacterStore quando aplicavel
  -> Protocol.*Confirmation.ToFrame
  -> stream.WriteAsync ou WorldHub.SendAsync
  -> ServerWireLog (TX)
```

O pipeline funciona, mas o roteamento e uma cadeia ordenada de `TryParse`. Isso torna a ordem parte implicita do contrato, permite colisao entre parsers e concentra o conhecimento de sessao, resposta e persistencia no listener.

## 4. Inventario de responsabilidades e chamadas

Esta e a primeira camada do inventario. O inventario literal de cada metodo deve ser gerado a partir do codigo e ligado a uma linha de rastreabilidade; nao sera mantido manualmente como narrativa.

### 4.1 Entrada principal

`src/WydCdk.Server/Program.cs` atualmente combina:

- leitura de argumentos e `ServerOptions`;
- escolha de arquivo/MariaDB e catalogos;
- criacao de `LoginSessionRegistry` e `WorldHub`;
- aceitacao de sockets e ciclo de leitura;
- log de RX/TX e mascaramento de dados sensiveis;
- verificacao de estado de sessao;
- despacho de todas as familias de mensagem;
- montagem e envio de confirmations;
- loops de batalha, pista, castelo, NPC e persistencia;
- fechamento, flush e tratamento de excecoes.

### 4.2 Rotas ja presentes no dispatcher

| Dominio | Parser/entrada observada | Chamada principal | Saida ou efeito |
|---|---|---|---|
| Conta | `AccountLoginRequest` | `AccountLoginCoordinator.HandleAsync` | selecao de personagem ou `MSG_MessagePanel` de falha |
| Conta | `AccountSecureRequest` | `AccountSecureCoordinator.HandleAsync` | sinal de sucesso/falha |
| Personagem | `CreateCharacterRequest` | `CreateCharacterCoordinator.HandleAsync` | `MSG_CNFNewCharacter` ou falha |
| Personagem | `DeleteCharacterRequest` | `DeleteCharacterCoordinator.HandleAsync` | selecao atualizada ou falha |
| Mundo | `CharacterLoginRequest` | `CharacterLoginCoordinator.HandleAsync` | `MSG_CNFCharacterLogin`, spawn e estado inicial |
| Sessao | logout/desconexao | `LoginSessionRegistry` + store de posicao | retorno a selecao e persistencia de SPX/SPY |
| Loja retail | `RetailNpcShopRequest` | loja NPC normal | compra retail por Gold, sob auditoria |
| Loja Cash | Premium Neil / web shop | checkout no Site | debito do Cash da conta + entrega no bau; nao implementado |
| Extensao antiga | `DonateShopOpenRequest` / `DonateShopCatalogRequest` / `DonatePurchaseRequest` | painel custom e `TryPurchaseDonateItem` | estacionado: debita e insere no MOB, conflito com o fluxo web-alvo |
| Itens | `UseItemRequest` | varios `WorldHub.TryApply*` | `SendItem`, `SetHpMp`, `UpdateEtc` ou rollback |
| Itens | `TradingItemRequest` | `WorldHub.TryTradeCarryItems` | troca de carry; cargo/equipamento pendentes |
| Itens | `DropItemRequest` | `WorldHub.TryDropItem` | confirmacao, criacao e broadcast do ground item |
| Itens | `GetItemRequest` | `WorldHub.TryGetGroundItem` | confirmacao, decay e insercao no carry |
| Social | `MessageChatRequest` | visao inclusiva/exclusao do remetente | relay para participantes |
| Social | `MessageWhisperRequest` | busca por nome | envio privado |
| Social | `QuestRequest` | Perzen/Pista | troca de item, chat ou registro |
| Social | party invite/accept/remove | estado de party | confirmations e refresh de MOB |
| Social | `InviteGuildRequest` | regra de guilda e persistencia | panel/update/create mob |
| Movimento | `ActionRequest`, `MotionRequest` | estado/relay | broadcast de acao/movimento |
| Skill | `SetShortSkillRequest` | barra/estado de skill | estado de personagem |
| Combate | `AttackRequest` | mana, dano, NPC/player, morte | frames de ataque, HP, XP/drop e estado |

### 4.3 Pontos de maior acoplamento

1. O listener conhece detalhes de cada caso e de cada frame de resposta.
2. `WorldHub` conhece simultaneamente estado, regra, wire auxiliar, NPCs, party, drops, combate e politicas comerciais.
3. Persistencia e regra de negocio alternam dentro do mesmo fluxo sem uma politica uniforme de transacao/rollback.
4. A prioridade do parser depende da ordem do arquivo, sem uma tabela de opcode/tamanho/estado esperado.
5. O client patcher mistura analise somente-leitura com mutacao de binario e diagnostico temporario.

## 5. Matriz de cobertura atual

### Estados de status

- **C0 — nao inventariado:** ainda nao existe linha de rastreabilidade confiavel.
- **C1 — referencia localizada:** simbolo/estrutura legado identificado, sem port provado.
- **C2 — wire/modelo:** parser ou confirmation implementado, sem dominio completo.
- **C3 — dominio/teste:** regra implementada e coberta por teste deterministico.
- **C4 — fluxo local:** build, testes e fluxo de rede/local executados.
- **C5 — cliente real:** cliente real consumiu/produziu o comportamento esperado.
- **C6 — remoto publicado:** build self-contained publicada e servico validado.
- **C7 — aceite:** fluxo funcional aceito com evidencia e documentacao atualizada.

Um caso nao pode ser chamado de “concluido” apenas por estar em C3. O status da funcionalidade e o menor nivel exigido pelo seu risco; login de personagem, por exemplo, precisa chegar a C5 antes de ser considerado validado para o cliente.

### 5.1 Baseline por epico

| ID | Epico | Situacao atual | Proximo fechamento de evidencia |
|---|---|---|---|
| AUTH | autenticacao e conta | C3/C4; MariaDB e BCrypt existem; falhas com panel | E2E repetido com conta real e logs mascarados |
| CHAR | selecao, criar, excluir, login | C3/C4; wire e estados foram corrigidos | C5 no cliente apos criar/excluir/logar personagem |
| SESSION | estados `USER_*`, movimento, logout | C3/C4 | matriz de transicoes negativa e E2E de reconexao |
| WORLD | NPC, spawn, visao, mapa | parcial; varias tabelas/caches ainda dependem de referencia | inventario de dados e teste por mapa/collision |
| COMBAT | ataque, mana, dano, XP, morte | parcial; varios blocos legados continuam pendentes | equivalencia por funcao e E2E de combate controlado |
| ITEM | use/drop/get/trading/split | drop/get, uso de varias familias, trading carry e split em C3 | merge, cargo, equipamento, combine, delete e E2E de inventario |
| SHOP | Cash de conta e compras pelo Site | painel Donate C# existente e estacionado; Premium Neil/web -> debito -> bau ainda nao implementado | localizar contrato/rotas do Site, desenhar transacao/idempotencia/entrega no bau; manter retail Gold separado |
| SOCIAL | chat, whisper, party, guild | C3/C4 em escopo implementado | rejeicoes, limites, desconexao e multicliente |
| EVENTS | Perzen, Pista, Castelo | parcial, com blocos portados e pendencias | cenarios completos e dados de producao sem segredo |
| CLIENT | cliente unico 7.670 compilado da TMProject | layouts antigos apenas como evidencia/asset selecionado; sem `ClientPatch`, DLLs, hooks ou painel Donate nativo na release | concluir auditoria e compor source/layouts; Premium Neil abre a loja web conforme contrato a localizar |
| DATA | arquivo legado/MariaDB | adapters existem; transacao/conflito ainda exigem matriz | concorrencia, lock, migracao e recuperacao |
| OPS | logs, status, painel, deploy | observabilidade de wire existe; processo manual | release reproducivel e status `servetest.htm` ligado ao core |

## 6. Backlog legado ainda nao fechado

O detalhamento executável foi extraído para [`BACKLOG.md`](../../BACKLOG.md). Esta
baseline mantém apenas o critério arquitetural: cada item deve passar por referência,
wire/assinatura, regra autoritativa, persistência definida, testes, fluxo local,
E2E no 7559 e compatibilidade nos perfis 7600/7662. A lista não autoriza copiar
código do legado ou do upstream.

## 7. Arquitetura alvo recomendada

```text
Transport
  TcpListener, connection lifecycle, frame stream, wire log
        |
Application
  PacketRouter, session guards, use cases, response plans
        |
Domain
  WorldState, Combat, Inventory, Social, Characters, Events, Commerce
        |
Infrastructure
  FileAccountStore, MariaDbStore, catalogs, map/data providers
        |
Protocol
  frame codec, message contracts, legacy layouts, confirmations

Compatibility
  7559 behavior reference | 7600 binary/signature track | 7662 profile verification
```

### 7.1 Extracao em fases, sem reescrever tudo

**Fase A — controle e inventario**

- congelar este baseline;
- criar registro central de opcodes, tamanho, estado permitido e handler;
- gerar inventario de metodos e chamadas a cada build;
- separar status de codigo, teste, cliente e deploy;
- corrigir `PROGRESSO.md` para checklist atual, sem historico narrativo.

**Fase B — transporte e aplicacao**

- extrair `PacketRouter` da cadeia de `TryParse`;
- criar `ConnectionContext` com sessao, tick, resposta e cancelamento;
- mover cada familia para um handler: Auth, Character, Item, Social, Combat, Shop;
- manter `Program.cs` somente como composicao/host.

**Fase C — dominio e persistencia**

- transformar `WorldHub` em facade temporaria;
- extrair `InventoryService`, `CombatService`, `SocialService`, `NpcEventService` e `CommerceService`;
- definir comandos/resultado e fronteiras de lock;
- formalizar transacao, rollback e conflito entre arquivo/MariaDB.

**Fase D — testes e entrega**

- dividir o runner em suites reais ou, no minimo, arquivos por contexto;
- separar unit, contract-wire, integration-store, network smoke e client E2E;
- gerar relatorio de cobertura de mensagens e publicar artefato de evidencia;
- automatizar build limpo e sincronizacao local/VPS Dev por hash.

**Fase E — cliente**

- manter 7559 como referencia de comportamento e 7600 como trilha de compatibilidade;
- levar os patches validados para o EXE por assinatura de bytes;
- retirar DLLs/probes temporarios da estrutura final;
- validar os tres perfis somente apos a matriz de equivalencia estar preenchida.

## 8. Riscos prioritarios

| ID | Risco | Impacto | Probabilidade | Mitigacao |
|---|---|---|---|---|
| R1 | `WorldHub` God object e lock global | regressao, deadlock e dificuldade de teste | alta | extracao incremental por contexto e comandos imutaveis |
| R2 | dispatcher monolitico e ordem implicita dos parsers | pacote roteado errado ou handler impossivel de isolar | alta | registry de opcode/tamanho/estado + `PacketRouter` |
| R3 | documentacao com contagens/status antigos | decisoes baseadas em evidencia falsa | alta | baseline gerado e `PROGRESSO.md` apenas como checklist |
| R4 | testes em um unico executavel manual | cobertura opaca e fixtures contaminadas | media/alta | suites nomeadas e testes negativos por caso |
| R5 | divergencia entre C++/7559 e C#/7600 | incompatibilidade silenciosa | alta | ficha por simbolo, wire, estado e teste de equivalencia |
| R6 | salvamento concorrente em arquivo/MariaDB | perda de personagem, saldo ou item | alta | lock por conta/mundo, versionamento e testes de conflito |
| R7 | patch de cliente dependente de endereco/probe | crash ou DLL obrigatoria no pacote final | alta | assinatura de bytes, copia isolada e patch no EXE |
| R8 | sync manual local/VPS Dev | working tree divergente e perda de alteracao | media/alta | hashes obrigatorios apos cada validacao |
| R9 | artefatos misturados com fonte | release nao reproduzivel | media | separar `src`, `tests`, `Tools`, `artifacts` e publicar somente pacote |
| R10 | segredo/configuracao operacional fora de contrato | deploy ou teste apontando para destino errado | alta | config por ambiente, segredo fora do repo e gate de deploy explicito |

## 9. Definition of Ready e Definition of Done

### 9.1 Definition of Ready para uma nova funcao

Uma funcao so entra em implementacao quando possuir:

- simbolo e arquivo de referencia identificados;
- opcode, tamanho, offsets, endianess e checksum documentados;
- estados de sessao permitidos e rejeicoes conhecidas;
- estado de mundo alterado e invariantes;
- persistencia necessaria e politica de rollback;
- resposta esperada e destinatarios;
- teste deterministico planejado;
- criterio de cliente real, quando a tela/wire exigir;
- risco e dependencia registrados.

### 9.2 Definition of Done para um bloco portado

- codigo compilando sem warnings;
- teste positivo e testes de rejeicao passando;
- contract-wire comparado com a referencia;
- persistencia verificada sem segredo no log/repositorio;
- fluxo local executado quando aplicavel;
- evidencia de cliente real ou pendencia explicitamente registrada;
- matriz atualizada com simbolo, chamada, estado e proximo passo;
- docs compartilhados sincronizados entre PC e VPS Dev por hash;
- nenhum deploy remoto/publico sem autorizacao explicita;
- commit somente quando o usuario decidir.

## 10. Ficha de rastreabilidade por funcao

Este e o formato que deve ser aplicado a cada handler e chamada relevante:

```text
ID:
Legacy symbol/file:
Reference release: 7.59 / 7.60 / both
Incoming opcode/type/size:
Parser C#:
Dispatcher/handler:
Domain method:
State read:
State mutated:
Persistence adapter/transaction:
Outgoing frames and recipients:
Positive tests:
Negative/concurrency tests:
Client evidence:
Build/test evidence:
Status C0-C7:
Known deviations:
Next action:
```

O registro deve ser gerado ou revisado junto com o codigo. Uma funcao sem ficha nao pode ser contada como “portada”; no maximo esta “localizada” ou “em investigacao”.

## 11. Plano imediato de controle

1. Manter este arquivo como baseline arquitetural e nao duplicar contexto em `CONTEXTO_*.md`.
2. Criar `docs/PORT_TRACEABILITY.csv` ou equivalente tipado para mensagens e simbolos legados.
3. Criar `docs/PORT_STATUS.md` somente para status atual; `PROGRESSO.md` permanece checklist de alto nivel.
4. Extrair automaticamente contagem de testes, opcodes e handlers no build de validacao.
5. Introduzir o registry de mensagens antes de adicionar mais branches em `Program.cs`.
6. Extrair primeiro handlers de baixo acoplamento: chat/whisper, drop/get e split item.
7. Deixar o `WorldHub` como facade durante a extracao e mover uma capacidade por vez, sempre com teste de equivalencia.
8. Fechar a matriz de persistencia concorrente antes de trade completo, cargo/equipamento e loja concorrente.
9. Separar artefatos de laboratorio do pacote de fonte/release e documentar a limpeza sem apagar evidencia necessaria.
10. Com o split e o caminho normal/Zakum de `_MSG_UpdateItem` fechados em C3, seguir para E2E; os timers estáticos/dinâmicos já possuem teste e a leitura do legado confirmou que `pItem[]` é volátil. Modelagem de trade e integração final dos patches no EXE ficam depois das fronteiras de domínio.

## 12. Conclusao operacional

O projeto tem base tecnica suficiente para continuar, mas nao deve continuar crescendo apenas por adicao de branches em `Program.cs` e metodos em `WorldHub`. O proximo marco nao e “mais uma feature”: e tornar cada feature auditavel.

O criterio de progresso passa a ser:

```text
referencia identificada
  -> wire provado
  -> regra autoritativa
  -> persistencia segura
  -> testes positivos/negativos
  -> fluxo local
  -> cliente real
  -> remoto publicado somente com autorizacao
```

Esse fluxo e registro historico da trilha antiga 7559/7600 e nao tem autoridade sobre a direcao 7.69 aprovada em 19/09/2026. Tambem nao autoriza sincronizar ou publicar source na VPS.
