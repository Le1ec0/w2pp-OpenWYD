# WydCdk.Engine

> Direcao vigente (19/09/2026): este checkout documenta a implementacao existente,
> mas o projeto passa a ser tratado como um emulador WYD novo em C#/.NET. A
> arquitetura atual sera inventariada e reconstruida em 20/09; seus modulos sao
> material reutilizavel, nao fronteiras arquiteturais definitivas. A referencia
> primaria e a engenharia reversa comunitaria 7.69; o cliente 7662 e o alvo
> preferido de E2E. Nao existe source oficial vazada.

Port C#/.NET do servidor WYD CDK, mantido em paralelo ao C++ legado em
`W2PP`. O comportamento do legado so entra no port depois de existir
equivalencia observavel e teste deterministico.

## Arquitetura atual

- `WydCdk.Protocol`: frames CPSock, parsers e confirmacoes do wire legado.
- `WydCdk.World`: sessao, personagem, persistencia, mundo e regras UP/PVP.
- `WydCdk.Server.Core`: listener TCP e processamento do protocolo.
- `WydCdk.Server`: painel WinForms que hospeda o nucleo e exibe stdout/stderr.
- `WydCdk.ClientPatcher`: ferramentas de analise e ponte nativa do cliente; nao
  substitui enderecos de outra release sem assinatura de bytes.
- MariaDB: autenticacao em `accounts` e estado de personagem por
  `account_name + world_key` em `wyd_world_account_state`; o modo MariaDB nao
  usa `Server\accounts`.
- As tabelas de estado do servidor estao em `db\migrations\001_world_state.sql`,
  `002_account_security.sql` e `003_autotrade_state.sql`. Elas nao criam nem
  alteram as tabelas pertencentes ao Site (`accounts` e catalogo Donate).

Cada mundo roda com o mesmo executavel, mas possui porta e `WorldHub` proprios:

```text
UP  -> 8281 -> --world UP  -> regras normais
PVP -> 8282 -> --world PVP -> precos de Donate/Gold zerados pela politica
```

## Build e testes

Na raiz do repositorio:

```powershell
dotnet build .\Server\WydCdk.Engine.slnx --nologo -v:q
dotnet run --no-build --project .\Server\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj
```

O estado documentado em 18/09/2026 e build limpo com 124 testes passando.

### Status HTTP do serverlist

O core pode publicar a contagem de jogadores no slot correspondente do
`Site\servtest.htm` sem misturar essa rota com `serv00.htm` ou com o binario
`serverlist.bin`:

```powershell
WydCdk.Server.Core.exe --bind 0.0.0.0 --port 8281 --world UP `
  --status-file C:\WYDCDK\Site\servtest.htm --status-slot 1
WydCdk.Server.Core.exe --bind 0.0.0.0 --port 8282 --world PVP `
  --status-file C:\WYDCDK\Site\servtest.htm --status-slot 2
```

O valor do slot e atualizado no inicio e no heartbeat de 60 segundos; zero
jogadores permanece como `0`. Um heartbeat grava um marcador em `.wyd-status`. O watchdog
`Server\tools\Publish-StatusExpiry.ps1`, agendado a cada minuto, escreve `-1`
quando o heartbeat esta ausente por 120 segundos. O desligamento normal tambem
escreve `-1` imediatamente. O painel envia `stop` pela entrada padrao e so usa
encerramento forcado como fallback apos cinco segundos.

### Estado local de autotrade

O adapter opt-in `--autotrade-state <arquivo.json>` grava owner, listing,
localizacao e taxa em um documento versionado com escrita atomica. O host salva
apos aceitar a abertura e remove o registro no logout/desconexao; ele ainda nao
rehidrata mobs offline quando nao existe store de personagem. Com um store de
personagem ativo, `LegacyAutoTradeRehydrator` restaura os MOBs no boot sem criar
sessoes TCP. No login do mesmo account/slot, `LegacyAutoTradeReconnectCoordinator`
fecha o NPC/book e remove o registro antes da confirmacao, enviando a remocao
visual aos jogadores capturados na vizinhanca; isso segue o reset de
`TradeMode`/`AutoTrade` observado no carregamento W2PP. Sem
`--autotrade-state`, o modo MariaDB de contas usa a tabela
`wyd_autotrade_state`; as migracoes estao versionadas em `db\migrations` e
foram aplicadas no MariaDB local `wyd_cdk`, com round-trip do listing aprovado.
O reset administrativo do schema do servidor foi exercitado no sandbox local;
VPS/banco externo e E2E continuam fora deste gate.
O usuário configurado `wyd_site` foi encontrado no MariaDB local, mas as
credenciais disponíveis não autenticaram; nenhuma senha/grant foi alterada.
O request `MSG_ReqBuy` ja possui DTO/fixture do contrato 7.69 e gates puros
para preco, taxa, item/cargo, Gold, espaco, vila, alcance e limite de 2G.
`ItemSoldConfirmation`, o calculo puro do imposto e o
`LegacyAutoTradePurchasePlanBuilder` tambem estao cobertos. O contrato
`LegacyAutoTradePurchaseCommitRequest` e o adapter `MariaDbWorldCharacterStore`
fecham o compare-and-swap dos dois blobs e do listing JSON na mesma transacao;
o `LegacyAutoTradeBook` protege o slot contra compra duplicada e o `WorldHub`
ja aplica/rollbacka a metade do comprador. `LegacyAutoTradePurchaseExecutor`
coordena o CAS do book, do NPC offline e do comprador, chama o commit duravel e
so depois envia `MSG_UpdateCarry`, `MSG_ItemSold` e o visual atualizado. MariaDB
usa transacao serializavel; o file store usa journal/recovery entre os dois
blobs e o listing JSON. O CAS
separa o MOB runtime do comprador do MOB persistido usado pelo commit, evitando
conflito falso em alteracoes ainda nao persistidas no logout. O dispatcher rejeita
a compra quando o backend nao oferece commit atomico; o smoke contra MariaDB
local exercitou o commit, a validacao pos-commit e o replay CAS. Banco remoto,
E2E e interrupcao de processo durante uma venda real continuam pendentes.

## Fluxo de login instrumentado

O listener registra a sequencia real por conexao. O caminho esperado e:

```text
RX 0x020D  MSG_AccountLogin
TX 0x010A  MSG_CNFAccountLogin       -> payload 1916 / frame 1928
RX 0x020F  MSG_CreateCharacter
TX 0x0110  MSG_CNFNewCharacter        -> payload 904 / frame 920 bytes
RX 0x0213  MSG_CharacterLogin
TX 0x0114  MSG_CNFCharacterLogin      -> payload 1716 / frame 1728 bytes
TX 0x0364  CreateMob                  -> payload 224 / frame 236 bytes
TX 0x0337  UpdateEtc
TX 0x0336  UpdateScore                -> payload 140 / frame 152 bytes
TX 0x03B9  UpdateAffect               -> payload 256 / frame 268 bytes, quando compativel
```

`MSG_CNFNewCharacter` usa tipo `0x0110`, `ID=30001` e o snapshot completo de
quatro slots no sucesso; o listener projeta o envelope 7.69 de 920 bytes. O teste de wire confirma tamanho, checksum, cabecalho e nome no
slot criado. O envio critico faz `FlushAsync`; portanto, se o cliente nao
atualizar a selecao, o log permite separar resposta ausente, resposta
incorreta ou resposta correta ignorada pelo `WYD.exe`.

## Logs de wire

`ServerWireLog` cria, ao lado do executavel, `server-UP.log` ou
`server-PVP.log` em modo append. Cada linha inclui timestamp ISO-8601,
conexao, direcao (`RX`/`TX`), tipo, ID, tamanho, tick, checksum e payload
hexadecimal quando o frame nao e de autenticacao.

Exemplo sintetico:

```text
2026-09-18T00:00:00.0000000-03:00 RX connection=7 type=0x020F id=30000 size=... checksum=True payload=<redacted-auth-payload>
2026-09-18T00:00:00.0100000-03:00 TX connection=7 type=0x0110 id=30001 size=852 checksum=True payload=... new-character-selection slot=0 name=1234
2026-09-18T00:00:01.0000000-03:00 RX EOF connection=7
```

Senha, PIN, chaves, strings de conexao e payloads de autenticacao ficam
mascarados por politica. Essa regra vale tambem para uma build de sandbox e e
obrigatoria para uma distribuicao open source. Os demais frames sao
hexadecimais para permitir reproducao do wire sem depender de mensagens de
console. O log nao deve ser versionado nem publicado junto com dumps reais.

Para diagnosticar uma desconexao:

1. localizar a mesma `connection=N` no log;
2. verificar o ultimo `RX` aceito e o primeiro `TX` depois dele;
3. conferir `size`, `type`, `id`, checksum e `payload` do frame;
4. comparar `CONNECTION ERROR`, `RX EOF` e `CONNECTION FINALLY`;
5. reproduzir novamente antes de alterar o parser.

## Publicacao remota de sandbox

A instancia UP usa `--bind 0.0.0.0 --port 8281 --world UP` e a PVP usa
`--bind 0.0.0.0 --port 8282 --world PVP`. O caminho do arquivo de configuracao
MariaDB deve ser passado por segredo local/gerenciador de tarefa; nunca gravar
credenciais neste README, no codigo, em `AGENTS.md` ou em `PROGRESSO.md`.

Na VPS, as tarefas agendadas executam o nucleo com o nome de compatibilidade
`WydCdk.Server.exe`; o painel WinForms deve ser publicado separado para nao
substituir esse nucleo. A publicacao local pode manter os dois artefatos:
`WydCdk.Server.Core.exe` para o listener e `WydCdk.Server.exe` para a UI.

O E2E pendente desta etapa e: autenticar `TESTE`, criar uma Foema, confirmar o
`0x0110` na selecao, selecionar o slot, confirmar o `0x0114` e capturar o
primeiro frame ou excecao que precede uma eventual desconexao.
