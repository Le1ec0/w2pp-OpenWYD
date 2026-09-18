# WydCdk.Engine

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

O estado documentado em 18/09/2026 e build limpo com 104 testes passando.

## Fluxo de login instrumentado

O listener registra a sequencia real por conexao. O caminho esperado e:

```text
RX 0x020D  MSG_AccountLogin
TX 0x010A  MSG_CNFAccountLogin       -> selecao, cargo e coin
RX 0x020F  MSG_CreateCharacter
TX 0x0110  MSG_CNFNewCharacter        -> payload 840 / frame 852 bytes
RX 0x0213  MSG_CharacterLogin
TX 0x0114  MSG_CNFCharacterLogin      -> payload 2636 / frame 2648 bytes
TX 0x0364  CreateMob                  -> personagem no mundo
TX 0x0337  UpdateEtc
TX 0x0336  UpdateScore
```

`MSG_CNFNewCharacter` usa tipo `0x0110`, `ID=30001` e o snapshot completo de
quatro slots. O teste de wire confirma tamanho, checksum, cabecalho e nome no
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
