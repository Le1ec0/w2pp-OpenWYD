# Autotrade Close Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Execucao nativa aprovada pelo usuario em 26/09/2026; ferramenta de revisao independente read-only agora disponivel. A autorizacao vale para o plano todo, sem repetir o gate a cada tarefa.

**Goal:** fechar autotrade pelo QuitTrade sem interromper compra/rollback, remover estado persistido conjuntamente e restaurar CreateMob normal 7.69.

**Architecture:** coordenadores de aplicacao compartilham leases por identidade runtime no mesmo book. WorldHub reserva o fechamento sob lock curto, sem I/O, enquanto os adapters removem listings esperados atomicamente. O host envia os frames apenas depois da conclusao duravel; as fronteiras existentes permanecem, sem reorganizar projetos.

**Tech Stack:** C#/.NET `net10.0`, SDK `10.0.401`, PowerShell, MySqlConnector existente, persistencia local JSON e MariaDB.

**Spec:** [TRADE-CLOSE-001](PORT_ARCHITECTURE_BASELINE.md#trade-close-001-coordenacao-de-autotrade-26092026), desenho escrito aprovado pelo usuario em 26/09/2026.

## Global Constraints

- Cliente unico `ClientVersion=7670`; contratos de wire 7.69, comparacao obrigatoria com W2PP.
- Preservar working tree compartilhada em `Server`/branch `master`; nao resetar, stagear, commitar ou fazer push.
- Usar apply_patch para arquivos; nao mover projetos nem reescrever suites inteiras.
- Em QuitTrade, HP <= 0 no ponto de aceite nao remove loja/par nem publica
  cancelamento/PK. Action tem gate proprio HP==0: HP zero preserva HpMode, mas
  HP negativo pode fechar loja/par quando houver estado ativo.
- Sinal `0x0384`/12, CreateMob normal `236`, CreateMobTrade `260`, PK `0x0366`/16; vizinhanca `33x33` incluindo dono.
- Nao alterar Carry, cargo, Gold ou MOBEXTRA durante fechamento. Venda conserva suas regras/CAS atuais.
- Nao manter lock do WorldHub durante I/O; leases assincronas adquiridas em IDs distintos/crescentes.
- Segredos fora do Git: nunca imprimir connection string, senha, blob de conta, PIN ou chave SSH.
- Apenas fixtures copiadas, rede loopback `127.0.0.1:18283` e banco local de teste isolado.
- Depois dos gates locais, publicar apenas runtime na VPS sandbox autorizada; nunca Site/producao, source ou documentos remotos.
- Nenhum commit por tarefa: a regra local substitui a recomendacao de commits da skill.
- Estado atual: T1..T6 concluidos no ledger; extensao T7 para fechamento por Action/TradeMode concluida localmente. Engine.Tests 276 PASS, build 0 avisos/0 erros e loopback combinado aprovados; este recorte nao foi publicado. E2E visual no cliente3D permanece pendente. Politica ordinaria de remocao nao redesenhada. Ultimas execucoes e contagens no ledger abaixo; contagens anteriores sao historicas.

## Review Focus

1. ID reutilizado/loja reaberta com mesmos itens e precos: geracao/owner antigos nao podem fechar ou comprar a nova loja (T1/T4).
2. Socket cancelado depois do commit: finalizar memoria sem desfazer venda/delete confirmado (T4/T5).
3. DELETE comprometido mas resposta perdida: reconciliar, ou bloquear operacoes afetadas; nao restaurar cegamente (T2/T4).
4. Compras reciprocas e cancelamento na segunda lease: ordem deterministica, nenhuma lease vazada (T1/T3).
5. Morte durante delete e dois lados com lojas: gate inicial define aceite; persistencia conjunta, sem fechar metade (T2/T4).

## Comandos comuns

Executar na raiz `D:\Jogos\WYD CDK`:

```powershell
dotnet run --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore
dotnet build Server/WydCdk.Engine.slnx --no-restore
git -c safe.directory='D:/Jogos/WYD CDK/Server' -c core.safecrlf=false -C Server diff --check
```

A suite atual usa Actions e para na primeira falha. Registrar nome/mensagem do
RED antes de implementar; erros de compilacao sozinhos nao provam regressao.
Executar suite inteira em cada GREEN, registrando contagem efetiva, nao fixa.

### T1: leases do book, identidade e geracao

**Files:** criar `Server/src/WydCdk.World/LegacyAutoTradeOperationGate.cs`;
alterar `LegacyAutoTradeBook.cs`; criar `Server/tests/WydCdk.Engine.Tests/AutoTradeCloseTests.cs`
e `AutoTradeCloseFixtures.cs`; registrar testes no `Program.cs` da suite.

**Interfaces (novas):**

```csharp
public sealed record LegacyAutoTradeOwner(string AccountName, int CharacterSlot);
public sealed record LegacyAutoTradeListingHandle(
    long Generation, LegacyAutoTradeOwner Owner, LegacyAutoTradeSnapshot Snapshot);
public sealed class LegacyAutoTradeOperationLease : IAsyncDisposable
{
    public bool Covers(int connectionId); // propriedade da instancia do book tambem verificada
    public ValueTask DisposeAsync();
}
// LegacyAutoTradeBook:
public ValueTask<LegacyAutoTradeOperationLease> AcquireAsync(
    IEnumerable<int> connectionIds, CancellationToken cancellationToken = default);
public bool TryGet(LegacyAutoTradeOperationLease lease, int connectionId,
    out LegacyAutoTradeListingHandle? handle);
public LegacyAutoTradeStartResult TryStart(LegacyAutoTradeOperationLease lease,
    int connectionId, LegacyAutoTradeOwner owner, AutoTradeStartRequest request,
    LegacyAutoTradeStartContext context, out LegacyAutoTradeListingHandle? handle);
public bool TryRestore(LegacyAutoTradeOperationLease lease, int connectionId,
    LegacyAutoTradeOwner owner, LegacyAutoTradeSnapshot snapshot,
    out LegacyAutoTradeListingHandle? handle);
public bool TryStop(LegacyAutoTradeOperationLease lease,
    LegacyAutoTradeListingHandle expected);
public LegacyAutoTradePurchaseResult TryApplyPurchase(LegacyAutoTradeOperationLease lease,
    LegacyAutoTradeListingHandle expected, LegacyAutoTradePurchasePlan plan,
    out LegacyAutoTradeListingHandle? updated);
public bool TryRollbackPurchase(LegacyAutoTradeOperationLease lease,
    LegacyAutoTradeListingHandle expectedUpdated, LegacyAutoTradeListingHandle original);
public IReadOnlyList<LegacyAutoTradeListingHandle> Snapshot(); // apenas descoberta, nao autorizacao
```

Lease invalida/disposta/de outro book rejeita mutacao sem efeitos. IDs aceitos
`1..65535`; normalizar owner para uppercase/slot `0..3`, sem nome vazio/NUL.
Geracao cresce por abertura/restauro, nao por venda. Registry limitado a esses
IDs e vida do book; nao descartar semaphore enquanto houver waiter/holder.
Migrar chamadas do modulo para a API protegida: hosts, rehydrator, reconnect,
executor e testes de autotrade, sem alterar testes de outros contextos.

- [x] RED: corrida reproduzida antes da mudanca, `Concurrent close broke rollback: RollbackFailed`; fixture offline isolada em AutoTradeCloseFixtures.
- [x] Implementar leases/handles e adaptar chamadas existentes ao novo contrato, sem antecipar handler QuitTrade.
- [x] Acrescentar testes de ordem/cancelamento, ownership/geracao e reabertura; barreiras deterministas e timeout somente como limite de travamento.

```csharp
Assert(!sameShopWaiter.IsCompleted, "Uma segunda operacao entrou na loja reservada.");
Assert(otherShopLease.Covers(13), "A loja 13 foi bloqueada pela loja 12.");
Assert(reopened.Generation != original.Generation, "Reabertura reutilizou geracao.");
Assert(!book.TryStop(newLease, original), "Plano antigo apagou nova loja.");
```

- [x] GREEN: suite completa; cancelamento esperando `[12,13]` libera 12, IDs repetidos nao causam deadlock, lease de book diferente nunca altera estado.

### T2: remocao persistida condicional e conjunta

**Files:** criar `Server/src/WydCdk.World/LegacyAutoTradeCloseStore.cs`;
alterar `LegacyAutoTradeStateStore.cs` e `MariaDbAutoTradeStateStore.cs`;
testar em `AutoTradeCloseTests.cs`; criar projeto local
`Server/tests/WydCdk.AutoTrade.Integration/{WydCdk.AutoTrade.Integration.csproj,Program.cs}`
e incluir somente esse projeto na solution.

**Interfaces:**

```csharp
public sealed class LegacyAutoTradeCloseRequest
{
    public IReadOnlyList<LegacyAutoTradePersistedListing> ExpectedListings { get; }
    public LegacyAutoTradeCloseRequest(IReadOnlyList<LegacyAutoTradePersistedListing> expectedListings);
}
public enum LegacyAutoTradeCloseStoreResult
{ Removed, AlreadyAbsent, Conflict, InvalidRequest, Unavailable, Ambiguous }
public enum LegacyAutoTradeCloseReadState
{ AllAbsent, AllUnchanged, Conflict, Unknown }
public interface ILegacyAutoTradeCloseStore
{
    ValueTask<LegacyAutoTradeCloseStoreResult> RemoveExpectedAsync(
        LegacyAutoTradeCloseRequest request, CancellationToken cancellationToken = default);
    ValueTask<LegacyAutoTradeCloseReadState> ReadCloseStateAsync(
        LegacyAutoTradeCloseRequest request, CancellationToken cancellationToken = default);
}
```

Request aceita `1..2` owners distintos (account/slot normalizados); copiar
arrays para nao receber mutacao externa durante await. InvalidRequest nao faz
I/O. Em snapshot coerente, registros ausentes sao idempotentes; se qualquer
registro presente diverge do esperado, nao remover nenhum. AlreadyAbsent
significa todos ausentes. Releitura: todos presentes/iguais = AllUnchanged;
mistura ausente/presente ou divergencia = Conflict; falha de leitura = Unknown.

- [x] RED file: stub Unknown/Unavailable falhou em `Existing close batch was not classified as unchanged`; teste cobre ambos os owners, item900/cargo4/preco100 e conflito101, mais owner duplicado e request copiado.
- [x] Implementar file store sob seu semaphore existente: recovery do journal anterior, comparar todos e substituir documento uma vez. Nao criar dois deletes ou outro formato de conta.
- [x] Implementar MariaDB com transacao serializavel, locks de owners em ordem ordinal account/slot e `SELECT ... FOR UPDATE` antes dos deletes; ReadCloseStateAsync usa snapshot transacional. Reusar schema atual, sem migracao comercial.
- [x] GREEN file real: nova instancia, blobs sinteticos W2PP completos byte a byte inalterados (cargo/Gold), ambos/um/nenhum ausente, conflito sem delete parcial, cancelamento e dados corrompidos fail-closed.
- [x] GREEN MariaDB real: harness net10.0/arquivo protegido, loopback literal e database exato antes de conectar; batch/conflict/releitura/retry e falha SQL injetada no segundo DELETE com rollback real do primeiro. Evidencia e fixtures no codigo/ledger; somente linhas/triggers exclusivos da execucao sao limpos. Sem DROP DATABASE.

### T3: compra protegida do preflight ate o rollback

**Files:** alterar `LegacyAutoTradePurchaseCoordinator.cs`,
`LegacyAutoTradePurchaseExecutor.cs`, `LegacyAutoTradeBook.cs` e fixtures/testes de T1.

**Interfaces:** preservar ExecuteAsync publico atual do executor como wrapper
que adquire `[BuyerConnectionId, Context.TargetId]`. Acrescentar metodo interno
`ExecuteUnderLeaseAsync(WorldHub world, LegacyAutoTradeBook book,
LegacyAutoTradeOperationLease lease, ILegacyAutoTradePurchaseCommitStore? commitStore,
LegacyAutoTradePurchaseExecutionRequest request, CancellationToken cancellationToken)`.
Adicionar `ExpectedGeneration` long ao request de execucao; coordinator captura
handle/owner depois de adquirir ambas as leases e chama o metodo interno,
sem aquisicao aninhada. Snapshot/contexto de compra continua imutavel.

- [x] Controle negativo de geracao executado depois da implementacao (nao alegar RED anterior): retirando somente o gate, o teste falhou em `Old purchase request entered a reopened identical shop`; gate restaurado e suite repetida.
- [x] Implementar preflight sob lease, verificar owner/geracao e preservar ordem CAS book -> NPC -> comprador -> commit -> rollback reverso. Nenhum await em lock do WorldHub.
- [x] GREEN/regressao: compra rejeitada/confirmada bloqueia fechamento; rejeicao restaura listing/NPC/MOB antes de liberar (Gold150/item vazio); commit deixa Gold50/item900, geracao inalterada. Mutacao externa de arrays tambem teve RED real antes de copiar os snapshots.
- [x] GREEN: ordem reciproca `[13,12]`/`[12,13]` testada no gate compartilhado; duas compras concorrentes no executor nao vendem duas vezes, cancelar waiter nao altera estado. Isso nao substitui E2E de compras reciprocas entre dois clientes.

### T4: reserva do WorldHub, fechamento e reconciliacao

**Files:** criar `WorldHub.TradeClose.cs`, `LegacyAutoTradeCloseCoordinator.cs`
e `LegacyAutoTradeCloseRecovery.cs` em `Server/src/WydCdk.World`;
alterar gates de `WorldHub.Trade.cs` e book; testar em `AutoTradeCloseTests.cs`.

**Interfaces:**

```csharp
public sealed record LegacyTradeCloseReservation(Guid Token, int ConnectionId, int OpponentId);
public enum LegacyTradeClosePrepareResult
{ Prepared, ParticipantNotFound, CombatStateUnavailable, NotAlive, Busy, PairChanged }
// WorldHub: nomes e tipos novos, sem remover o TryCloseTrade sincrono existente.
public LegacyTradeClosePrepareResult TryReserveTradeClose(int connectionId,
    int expectedOpponentId, out LegacyTradeCloseReservation? reservation);
public bool TryFinishTradeClose(LegacyTradeCloseReservation reservation,
    out LegacyTradeCloseOutcome? outcome);
public bool TryAbortTradeClose(LegacyTradeCloseReservation reservation);
public int GetTradeOpponentId(int connectionId);
public enum LegacyAutoTradeCloseResult
{ Closed, NoShop, SessionNotPlaying, NotAlive, Conflict, PersistenceUnavailable, PendingRecovery }
public sealed record LegacyAutoTradeCloseOutcome(LegacyAutoTradeCloseResult Result,
    LegacyTradeCloseOutcome? Trade, IReadOnlyList<LegacyAutoTradeListingHandle> ClosedListings);
public static ValueTask<LegacyAutoTradeCloseOutcome> CloseAsync(int connectionId,
    LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
    ILegacyAutoTradeStateStore? stateStore, CancellationToken cancellationToken = default);
public static ValueTask<LegacyAutoTradeCloseOutcome> RecoverAsync(int connectionId,
    LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
    ILegacyAutoTradeStateStore stateStore, CancellationToken cancellationToken = default);
```

CloseAsync pertence a LegacyAutoTradeCloseCoordinator; RecoverAsync a
LegacyAutoTradeCloseRecovery. O contexto pendente guarda copias dos dados e
bloqueia os dois IDs e owners; nao e anunciado como uma loja ativa nem cria tabela.
Adicionar ao book, sob lease que cubra todos os IDs, as interfaces:

```csharp
public sealed record LegacyAutoTradePendingClose(LegacyTradeCloseReservation Reservation,
    LegacyAutoTradeCloseRequest Request, IReadOnlyList<LegacyAutoTradeListingHandle> Listings)
{
    public IReadOnlyList<LegacyAutoTradeOwner> AffectedOwners { get; init; } = [];
}
public bool TrySetPendingClose(LegacyAutoTradeOperationLease lease,
    LegacyAutoTradePendingClose pending);
public bool TryGetPendingClose(LegacyAutoTradeOperationLease lease, int connectionId,
    out LegacyAutoTradePendingClose? pending);
public bool TryClearPendingClose(LegacyAutoTradeOperationLease lease,
    LegacyAutoTradePendingClose expected);
```

TryClear usa token/geracoes esperados; nao limpa outro fechamento. Snapshot
inclui handles pendentes para descoberta/recovery, mas TryGet para consulta ou
venda os recusa. Recovery usa owner/request salvos mesmo se a sessao original
ja terminou; nao exige recriar um participante TCP.

- [x] RED do coordenador stub: `Close did not finish both shops and the reserved pair`; GREEN cobre HP 0/-1, persistencia antes do runtime, rejeicao, duas lojas e retry, com snapshots completos de MOB/inventario/Gold.
- [x] Captura/revalidacao de identidade e par depois das leases; Conflict sem retry infinito. Reserva valida HP uma vez e bloqueia ofertas/checks/cancelamento/delete de carry. Finish usa token exato sem repetir HP depois do delete.
- [x] Delete conjunto T2; store ausente permite runtime, backend sem capacidade rejeita. NoShop conserva plano de sinais/PK sem I/O. Commit confirmado finaliza sem cancellation do socket.
- [x] GREEN: HP cai durante await; cancelamento antes/depois do commit; readback AllAbsent/AllUnchanged/Conflict/Unknown. Nao alegar RED individual de todos esses vetores.
- [x] Recovery sem SaveAsync cego: ausentes finalizam, inalterados abortam, misto/desconhecido bloqueiam IDs/owners. Reconnect rejeita owner pendente; sessao encerrada nao e recriada nem gera plano para ID reutilizado.
- [x] GREEN: token/geracao antigos rejeitados; NoShop inclui Trade e rejeicao/pending nao incluem sucesso. Falha de envio real fica para T5/T6, pois T4 nao escreve sockets.

### T5: integrar todos os caminhos runtime e relays

**Files:** alterar `Server/src/WydCdk.Server/Program.cs`,
`LegacyAutoTradeReconnectCoordinator.cs`, `LegacyAutoTradeRehydrator.cs`;
criar `LegacyAutoTradeCloseRelay.cs`; testar relays/lifecycle em AutoTradeCloseTests.

**Interfaces:** `LegacyAutoTradeCloseRelay.TryBuild(WorldHub world,
LegacyAutoTradeCloseOutcome outcome, LegacyFrameCodec codec, uint clientTick,
byte keywordIndex, out IReadOnlyList<LegacyAutoTradeCloseFramePlan> plans)`;
plan record `(byte[] Frame, IReadOnlyList<int> RecipientIds)`.
Reusar WorldHub.TryBuildCreateMobFrame e o encoder normal ativo, sem duplicacao.
Identidade owner vem da sessao online ou do NPC offline ja persistido.

- [x] RED/GREEN: `CloseRelayNormalAndRecipients` cobre os dois contratos juntos: frame 236 (nao 260/16), IDs corretos; dono/vizinho16 incluidos e vizinhos17 excluidos. Reenvio nao acrescenta visual de loja ja fechada; rejeicao/pending/sessao encerrada nao gera plano de sucesso.
- [x] QuitTrade ligado ao CloseAndCaptureAsync: sinal do oponente se ID >0, proprio quando aceito e PK do autor original (tambem na recovery). Morto recebe SetHpMode HP atual/22 sem sinais/PK. Regressoes loopback idle/HP-1 passaram; lojas reais na rede continuam em T6.
- [x] Adaptar abertura/save e consulta/list/visual a lease; guardar owner/handle. Logout/finally adquirem lease ANTES do save/Leave, com compra/rollback aguardados. Reconnect revalida handles sob lease e cleanup stale usa expected delete/barreira; rehydrator registra handles sob lease antes de aceitar conexoes. Limites de persistencia ordinaria no ledger.
- [x] Abertura/save e consulta/list/visual: capture protegida de lista/visual/destinatarios antes da liberacao; host libera startLease/listLease antes dos sockets. Save continua sob lease antes da captura.
- [x] RED/GREEN: `DepartureWaitsPurchase`, `ReconnectWaitsPurchase`, `DisconnectPreservesPendingRecovery`; mutadores do book exigem lease e callbacks de saida sao persistence-only. Captura ocorre sob lease; sockets apos retorno.
- [x] GREEN: suite 259 PASS/build completa; planos de saida sem sockets nos testes de aplicacao. Envio falho nao restaura loja; dispatcher TCP real continua T6.

### T6: smoke de rede, banco local e handoff sandbox

**Files:** alterar `Server/tests/WydCdk.Client.E2E/Program.cs`;
crear `Server/tools/PortAudit/Test-AutoTradeClose.ps1`; actualizar `CLIENT_E2E_SMOKE.md`,
`PORT_TRACEABILITY.md`, este plano e AGENTS/PROGRESSO/BACKLOG in-place.

**Interfaces do harness:** flags novos `--autotrade-close-peer-account-file <path>`
e `--autotrade-state-file <path>`; somente modo autotrade, incompativel com flags
trade-delete/quit/replay e adversarial. Manter validadores loopback/copias
isoladas; autenticar lendo fixture, sem credenciais em CLI/output.
O script cria pasta nova em `Backup/Build/AutoTradeCloseSmoke`, copia
TESTE/TESTE2 e semeia somente copias (cargo slot4, item900/preco100, saldos).
Posicoes de teste devem ficar em vila permitida fora do retangulo
protegido Armia; usar limites auditados e verificar antes de iniciar.

- [x] RED: publish-20260925-quittrade-hpgate abriu/consultou loja real, depois falhou por falta do normal236. Nao e teste bilateral substituto.
- [x] Script loopback18283 com accounts-root/autotrade-state/ItemList real, processo oculto e cleanup do PID proprio. Copias/hashes/evidencias preservadas.
- [x] GREEN rede: sinais/PK/normal236 sem RemoveMob, compra posterior rejeitada, dois retries sem visual duplicado; carry/cargo/Gold duraveis antes do logout e relogin intactos. Interleavings continuam T3/T4.
- [x] Script executado na shell existente com ExecutionPolicy somente Process; suite259/build limpa e MariaDB local com config protegido passaram. Self-tests de porta isolada/barreira comprador tiveram RED->GREEN apos revisao.
- [x] Self-contained win-x64 publicado runtime-only no alvo sandbox identificado por tarefas/listeners/exe; backup unico, 197 arquivos com hashes conferidos, somente UP/PVP reiniciados, configs/contas preservados. Sem git pull/source/docs/segredos.
- [x] RED/GREEN/contagens/hashes/backup no ledger; originais intactos/18283 liberada. E2E visual nao executado nem inferido desta evidencia.

### T7: Action fecha loja TradeMode junto do trade bilateral (28/09/2026)

- [x] Comparar `_MSG_Action.cpp`/`RemoveTrade` do TMProject 7.69 e W2PP:
  gate Action `HP==0`, fechamento bilateral primeiro e, sem par, TradeMode;
  nao confundir com o gate `HP<=0` de QuitTrade.
- [x] Ligar o caminho ao coordenador duravel existente: hint de listing seguido
  de revalidacao sob leases, fechamento/recovery condicional e retorno antes de
  speed/range/movimento. Trade bilateral e loja(s) fecham na mesma operacao.
- [x] Relay especifico: `0x0384`/12 por participante e CreateMob normal/236 por
  loja para a visao; nenhum PK, Action, CreateMobTrade/260 ou RemoveMob.
- [x] Engine.Tests: 276 PASS; build da solution: 0 avisos/0 erros.
- [x] E2E TCP loopback `127.0.0.1:18283`: duas lojas + trade bilateral, HP/frames,
  fechamento duravel, retry/compra rejeitada/relogin e hashes originais intactos.
  Evidencia `Backup/Build/AutoTradeCloseSmoke/39e5171332814620b95ebc0a671b0650`;
  porta liberada.
- [x] Nenhum deploy, Site, producao, commit ou push. Cliente WYD.exe/visual,
  evento AFK/ISTradTimer e ban/deteccao 7.69 permanecem fora deste T7.

## Autorrevisao e handoff

- [x] Cobertura do spec: leases/ciclo de vida T1/T3/T5; persistencia conjunta T2;
  reserva/HP/recovery T4; wire/destinatarios T5; rede/sandbox T6.
- [x] Review Focus associado a testes especificos; geracao muda somente na reabertura.
- [x] APIs publicas do executor preservadas por wrapper; sem aquisicao recursiva.
- [x] Tokens/handles/owner definidos; mudancas limitadas ao modulo autotrade/trade close.
- [x] Sem commits automaticos ou gate MariaDB/cliente visual inventado.
- [x] Usuario revisa/confirma este plano e execucao nativa nesta sessao.
- [x] Implementar T1..T6 com TDD/verificacao no recorte do ledger; nao representa port inteiro/release comercial ou E2E visual concluido.

## Execution ledger — 26/09/2026

### Rulings

- Execucao inline no Server/master aprovado, preservando diffs. Ledger neste
  documento compartilhado, sem worktree, commits nem arquivos CONTEXTO adicionais.
- A migracao de API T1 exigiu o wrapper/preflight de T3 na mesma alteracao para
  nenhum caller produtivo manter mutadores sem lease. QuitTrade nao foi ligado.
- CloseRequest e classe com listas readonly copiadas/normalizadas, em vez do
  record posicional sugerido; evita mutacao externa e owner duplicado no await.
- Banco dedicado inexistente (1049): provisionado apenas
  `wyd_cdk_autotrade_close_test` em 127.0.0.1:3306 com credencial local protegida,
  sem copiar/exibir segredo. Harness inicial sem updated_at falhou (1054);
  DDL de teste corrigido para o contrato da migracao003, sem mudar bases comerciais.
- Harness limpa exclusivamente owners/triggers aleatorios criados por ele;
  banco e schema permanecem, sem DROP DATABASE. Fixtures/evidencia ficam no codigo.
- Restore do novo projeto usou cache com NuGetAudit=false; nao e evidencia de
  auditoria atual de vulnerabilidades. Nenhuma dependencia nova foi adicionada.
- Sem subagente disponivel: autorrevisao inline, mais fraca que revisao independente.

### Resultados e limites

- Baseline executada antes de alterar; RED de corrida RollbackFailed confirmado.
- RED file/state e RED de mutacao externa do request confirmados; controle
  negativo de geracao falhou no comportamento esperado, source restaurada.
- Suite ao concluir T1..T3: 228 PASS. Testes novos usam book/executor/world reais;
  a barreira substitui somente a espera de commit, nao as regras de compra.
- MariaDB real passou em mismatch, commit/releitura, retry e falha entre deletes
  com rollback serializavel. Requests de host remoto/outra base devem falhar
  antes de ler credencial ou abrir conexao; gates negativos registrados abaixo.
- Build da solution/diff-check e hashes originais conferidos na verificacao final.
- T4 concluido no recorte abaixo; proxima tarefa T5 relays/host, depois T6 rede.
- Sem novo deploy, commit, push ou alteracao no Site. O port completo deste
  handler e o plano T1..T6 NAO estao concluidos.

### Verificacao final do recorte T1..T3 (historico)

- `dotnet run --project Server/tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore`:
  exit0, 228 PASS; corrida, commit/reject, cancelamento, owner/geracao, snapshots,
  close file, autotrade antigo e demais modulos passaram.
- `dotnet build Server/WydCdk.Engine.slnx --no-restore -v quiet`:
  exit0, 0 avisos/0 erros, incluindo novo projeto de integracao.
- Integracao local repetida com `--config` protegido: PASS batch/mismatch,
  rollback da falha no segundo delete, readback por nova conexao e retry.
- Gates negativos executados com arquivo de credencial inexistente:
  host `198.51.100.1`, alias `localhost` e database `wyd_cdk` retornaram exit1/
  InvalidOperationException antes da leitura da credencial/conexao.
- `git ... diff --check`: exit0. Branch master/HEAD
  `06939c60834c2e98606a3a7ffd01764e83cb596d` preservados, sem stage/commit/push.
- Hashes originais TESTE/TESTE2 iguais aos registrados antes do bloco;
  nenhum listener em18283. Testes de fechamento nao abriram o jogo nem listener.
- Autorrevisao detectou mutabilidade do request: RED real, copias defensivas,
  GREEN. Controle negativo de geracao teve exit -532462766/falha esperada;
  a linha foi restaurada e build/suite finais passaram. Uma build intermediaria
  encontrou DLL presa por processo do RED; apos finalizar essa execucao,
  a build repetida ficou limpa. Nenhuma mudanca no jogo ou em processos externos.

### Recorte T4: reserva e reconciliacao

- Implementados WorldHub.TradeClose, CloseCoordinator e CloseRecovery. Reserva
  revalida identidades/par, aceita HP uma vez e guarda referencias dos participantes.
  Delete confirmado finaliza sem cancelar; desconexao/ID reutilizado nao recebe
  plano antigo, PK nem visual e nao ressuscita sessao TCP.
- PendingClose bloqueia ambos os IDs e owners, inclusive owner sem listing.
  Compra como comprador, consulta, cancelamento e delete de carry respeitam o gate.
  Owners duraveis duplicados sao rejeitados antes da reserva; token/geracao antigos
  nao podem limpar outro contexto. Copias defensivas impedem mutacao externa.
- REDs adicionais reais na autorrevisao: comprador pendente comprava outra loja;
  owner sem listing podia reabrir; owner duplicado lancava excecao apos reserva.
  Os tres foram corrigidos e a suite repetida. Erros intermediarios de compilacao
  nos testes nao sao evidencia RED comportamental.
- 13 testes novos: fechamento/retry, HP0/-1, durabilidade/queda de HP, falhas,
  cancelamento, quatro readbacks, ID reutilizado, par alterado durante espera,
  tokens/NoShop, comprador pendente, owner sem loja, owner duplicado e readback
  duravel misto. Book/world e arquivo temporario reais; barreira somente no I/O.
- Suite completa: 241 PASS. Solution: exit0, 0 avisos/0 erros. MariaDB local
  repetido: batch/mismatch, rollback entre deletes, readback em nova conexao/retry PASS.
- Autorrevisao inline, sem revisor independente. Sem jogo, listener de smoke,
  deploy, commit/push ou Site. Host QuitTrade ainda nao usa o novo coordenador.
- T5 deve descobrir pendencias pelos AffectedOwners (nao somente listing owner),
  recuperar antes da reconexao, usar os IDs do outcome.Trade (que podem diferir
  do solicitante da recovery), capturar planos e liberar leases antes dos sockets.
  T6 rede/relogin e gates de publicacao continuam obrigatorios.

### T5 em andamento: primeira fatia de relay

- LegacyAutoTradeCloseRelay captura sinais 0x0384/12, CreateMob normal
  0x0364/236 para cada loja fechada e PK 0x0366/16 somente do autor.
  Reutiliza encoder/adapter ativo do WorldHub e destinatarios 33x33.
  RemoveTrade 7.69 e W2PP foram comparados: ambos restauram CreateMob normal,
  nao RemoveMob. Sem mudanca nos contratos historicos de logout/NPC.
- RED stub executado: `Close relay did not build a success plan`; GREEN suite
  completa 242 PASS. Teste usa world/book/coordenador reais e decodifica frames,
  verifica identidades/destinatarios/tick/checksum, retry e ausencia de sucesso
  para rejeicao/pending/sessao encerrada. MOB/Gold/inventario continuam intactos.
- Ruling: nao ligar o host ao relay depois de CloseAsync liberar as leases:
  reabertura/ID reutilizado entre fechamento e captura poderia receber visual
  antigo. Integrar captura dentro da operacao protegida antes de sockets e
  incluir recovery/reconnect continua sendo requisito do restante de T5.
  Custo desta fatia isolada: visual ainda nao mudou no jogo.
- Autorrevisao inline sem ferramenta de revisor independente. Host QuitTrade,
  lifecycle e smoke T6 pendentes; nenhum deploy, commit/push ou Site.
- Inspecao de processos: sete dotnet antigos identificados como workers MSBuild,
  nao testes/host WYD. PowerShells interativos/automacao nao foram encerrados.
  A compilacao RED demorou antes de iniciar a suite, mas terminou; nao foi
  necessario matar processos ou presumir deadlock.
- Build da solution nesta fatia: exit0, 0 avisos/0 erros, usando -m:1/-nr:false
  e UseSharedCompilation=false; nenhum novo worker persistente requerido.

### T5: captura protegida e integracao QuitTrade

- CloseAndCaptureAsync devolve outcome e planos de frames. Callback privado
  captura antes do await using liberar as leases; checks Covers recusam captura
  tardia. NoShop, commit normal, readback ambiguo e retry/recovery usam o mesmo
  caminho. O host escreve sockets somente depois do retorno, sem lease.
- RED real: `Close dispatch did not capture frames before returning`.
  GREEN cobre espera do delete/barreira, frames congelados antes da reabertura
  e movimento, lease liberada no envio e falha de transporte sem restore de
  persistencia nem overwrite da geracao reaberta. Recovery pelo ID12 mantem
  autor/PK ID7 e nao envia sucesso enquanto Unknown.
- Ruling: registro compartilhado de sessoes exigido pelo coordinator, pois o
  host criava um registry por TCP e nao conseguia revalidar a sessao do oponente.
  LoginSessionRegistry usa ConcurrentDictionary e Open/Close atomicos; transicoes
  do mesmo ID continuam serializadas pelo dispatcher daquele socket. O host
  remove a sessao em finally, inclusive se o cleanup falhar. Custo se um futuro
  caller escrever a mesma sessao em paralelo: requer CAS/serializacao adicional.
- RED concorrente no registry antigo: `Shared registry lost concurrent transitions`;
  GREEN para 4096 IDs, owners/estados preservados e closes concorrentes. Suite
  completa 245 PASS. Registro privado por TCP removido do host.
- Rede loopback 127.0.0.1:18283, contas copiadas, store JSON vazio sem MariaDB,
  ItemList/mapas/skills/status desabilitados. Antes e depois do registry global:
  idle QuitTrade com dois reenvios e HP-1/22 sem sucesso indevido passaram;
  carry/Gold completos duraveis antes do logout e relogin intactos.
- Evidencia preservada em Backup/Build/E2E7670/t5dispatch-c63c7fc3270144abbae16e9f9dd0493e
  e t5shared-9f5cc99e6ec746379ff486a8b844e466. Originais nao foram semeados.
  Primeiro host sem stdin aberto encerrado por PID validado pelo listener/path;
  segundo host encerrado cooperativamente via stdin, exit0. Nenhum terminal
  alheio foi fechado e nenhum processo foi encerrado por nome generico.
- Autorrevisao inline sem revisor independente. Este recorte NAO conclui T5:
  liberar leases/capturar relays em abertura/consulta/logout/disconnect e
  recuperar owner pendente na reconexao ainda faltam. T6 precisa abrir lojas
  reais pelo wire e validar CreateMob normal/retry/relogin antes de deploy.
  Sem deploy, commit/push, Site ou alteracao de configuracao live.
- Verificacao final: solution exit0, zero avisos/erros; suite 245 PASS.
  Regressoes de rede repetidas apos registry compartilhado, exit0 nos dois modos.

### T5: recovery por owner antes da reconexao

- TryDiscoverPendingClose e somente descoberta copiada por AffectedOwners,
  inclusive owner sem listing. RecoverOwnerAndCaptureAsync adquire os IDs do
  par salvo e revalida reservation/request antes de reconciliar; nao depende
  da sessao TCP original ainda existir. Nenhum novo delete na recovery.
- CloseAndRecoverAsync ligado ao character login do host. AllAbsent termina
  o fechamento e captura os relays do autor original antes de liberar leases;
  original ausente nao gera frames para sessao nova. Unknown/Conflict bloqueiam.
  AllUnchanged aborta a reserva sem apagar lojas e rejeita esta tentativa.
- Ruling conservador: handles online/stale nao-NPC nao podem passar pela
  remocao offline do reconnect. Gate repetido apos adquirir os IDs dos NPCs.
  Custo: stale handle apos AllUnchanged permanece bloqueado ate cleanup seguro
  no restante T5; nao improvisar delete/restore nem anunciar lifecycle completo.
  Politica geral de conta duplicada e serializacao por owner continuam fora
  deste recorte; o fechamento offline existente nao foi redesenhado em massa.
- RED stub real: Owner recovery did not resolve the saved pair after disconnect.
  GREEN suite completa 249 PASS, quatro testes novos com world/book/file reais:
  desconexao, sem loja propria, readbacks incertos/inalterados e captura de PK
  do autor7 quando owner12 reconecta. Leases liberadas antes do envio.
- Autorrevisao inline, sem revisor independente disponivel. Sem novo smoke TCP,
  MariaDB vivo, cliente visual, deploy, commit/push ou Site. Restam abertura,
  consulta/logout/disconnect, cleanup stale e T6 antes dos gates de publicacao.

### T5: abertura e consulta com transporte fora das leases

- LegacyAutoTradeListingRelay captura lista196 e visual260 com DTOs/encoders
  existentes, book/lease validos cobrindo solicitante e alvo. Nao faz I/O nem
  muta o runtime. Consulta cobre NPC offline; visual continua opcional se nao
  houver snapshot completo, como no handler anterior.
- Host captura somente apos os gates existentes e save aceito; DisposeAsync
  idempotente precede todo WriteAsync/SendAsync nos dois ramos de sucesso.
  Rejeicoes seguem cleanup por await using. Falha de transporte nao introduz
  rollback do save nem nova abertura; logout/disconnect seguem sua trilha propria.
- RED stub observado: Listing capture omitted protected opening frames.
  Erro anterior de compilacao na propriedade do DTO foi corrigido para
  Description antes de observar o RED; erro de compilacao nao conta como RED.
- Ruling: preservar o visual adicional da consulta ja existente no C#.
  _MSG_ReqTradeList 7.69/W2PP envia apenas SendAutoTrade; a extensao atual usa
  CreateMobTrade para consulta/NPC offline. Este recorte muda somente captura/
  envio, nao anuncia equivalencia nova nem reabre decisao de wire. Custo:
  a extensao ainda requer validacao pelo smoke T6/cliente.
- Autorrevisao inline sem revisor independente. Gates/HP/alcance/persistencia
  de abertura permanecem no host; testes de captura nao substituem o dispatcher
  real na rede. Sem novo smoke TCP, VPS, Site ou commit/push nesta fatia.
- GREEN suite completa: 251 PASS, dois testes novos com world/book reais:
  lista196/visual260, dono/vizinhos16/17, consulta privada, NPC offline,
  reabertura sem reconstruir o plano e lease disposta/foreign/requester ausente.
- Solution build exit0, 0 avisos/0 erros (50s); diff --check exit0. HEAD/master
  preservados; hashes das contas originais TESTE/TESTE2 conferidos e intactos.
  Compilacao da suite levou cerca de 3 minutos em csc.exe ativo; nao houve
  evidencia de deadlock, encerramento de terminal ou processo persistente novo.

### T5: logout/disconnect e cleanup residual

- DepartureCoordinator adquire a lease antes dos callbacks de persistencia,
  espera compra/rollback, para somente sua loja e captura RemoveMob16/33x33
  antes de Leave. Host logout/finally usam o coordenador; sockets, summons e
  confirmacao ficam depois do retorno. SaveCharacterOnDepartureAsync preserva
  MOB/extra/equipamentos e fallback anteriores. Callbacks nao enviam sockets.
- RED real: Departure saved/left during a pending purchase. GREEN da primeira
  fatia: suite 254 PASS, incluindo commit/rollback, captura antes de Leave,
  transporte falho sem restaurar loja e disconnect com pendencia preservada.
- Cleanup residual: RED real observado em Reconnect did not conditionally clean
  the detached owner after AllUnchanged. GREEN confirmado na suite completa:
  revalidar owner/geracao/ausencia de jogador ou NPC; expected delete de uma loja;
  pendencia RuntimeDetached protege todos os IDs do owner ate confirmacao.
  Recovery detached nunca executa Finish/Abort no WorldHub nem relays antigos.
- Ruling: cleanup posterior a AllUnchanged pode fechar somente o owner ausente,
  nao o outro participante. Unknown/Conflict continuam fail-closed; nenhuma
  restauracao cega ou alteracao de MOB/carry/Gold. Persistencia/erro de remocao
  ordinaria de logout e NPC offline conserva a politica anterior neste recorte;
  nao anunciar um redesenho de toda persistencia de lifecycle.
- RED adicional real: Outside owner generation entered close barrier. Registro
  agora rejeita sob o lock do book outro ID do owner fora do fechamento, antes
  de I/O; protege a janela entre descoberta e pendencia. Nao redesenha auth geral.
- Teste de substituicao inicialmente falhou por fixture MOB com Coin diferente
  do saldo esperado; corrigido somente o dado sintetico, sem alterar regra runtime.
- GREEN final: 259 PASS; solution build exit0, 0 avisos/0 erros; diff --check
  e hashes das contas originais conferidos. Master/HEAD06939c6 preservados.
- Revisao independente read-only concluida: nenhum achado Critical/Important
  em DepartureCoordinator, cleanup detached/recovery e host logout/finally.
  Revisao estatica, sem executar testes; barreira atomica por owner teve RED/GREEN
  local separado. Nao certifica E2E ou a politica ordinaria de remocao preservada.
  Sem TCP smoke novo, deploy, commit/push ou Site.

### T6: smoke de loja real e gates locais

- RED contra runtime self-contained anterior publish-20260925-quittrade-hpgate:
  abertura196/shop260 e consulta confirmadas; QuitTrade falhou por normal236
  ausente. Evidencia AutoTradeCloseSmoke/955ac834ae5641f09fe6b0039bd5bfc4.
- GREEN debug apos correcoes de review: 91a3adfd7502470f9144762981a2bf3a;
  GREEN release self-contained win-x64: 046aef37a0324dae9e9600e4b085a49e.
  Pacotes/checksums reais, sinal12/PK16/normal236 para dono/vizinho, sem RemoveMob
  ou shop260 no fechamento; dois retries sem visual, compra velha rejeitada,
  carry64/cargo128/Gold completos antes do logout e depois do relogin dos dois.
- Ruling: spawn aleatorio e validado pelos limites de Armia, fora do retangulo
  protegido, nao por coordenada literal salva; fixture Level1 mantem vizinhanca.
  Custo: nao demonstra outras cidades ou colisao com mapas carregados.
- Preparacao corrigida: originais8000 com contrato minimo7945, trailer mantido;
  PowerShell exige int16/parenteses em array. Erros do runner nao contam como RED.
  Invocar &script na shell existente evita Path/PATH duplicados do filho -File;
  ExecutionPolicy alterada somente no processo, nao na maquina. Sem nova politica.
- Review independente encontrou dois Important corrigidos: porta18283 literal
  obrigatoria antes de autenticar; barreira BUY-REJECT inicia pelo comprador.
  Self-test real TCP captura resposta UpdateCarry somente do comprador. Ambos
  RED->GREEN; suite259/build0avisos0erros regredidas apos correcoes.
- Final: minor anterior resolvido na fatia seguinte: Id do sinal e estado PK
  agora verificados no smoke com self-tests negativos; ver ledger abaixo.
- Final: minor anterior resolvido na fatia seguinte: client.log/evidence.json
  guardam output completo/exit/hash runtime+harness; ver ledger abaixo.
- Ruling: review e estatico, nao certifica execucao; comandos acima/integ local
  fornecem essa evidencia. Junctions deliberadas nao foram exercitadas; o runner
  usa pastas GUID novas/copias ordinarias. Custo: nao e defesa contra invasor local.
- MariaDB loopback/schema wyd_cdk_autotrade_close_test: PASS mismatch/batch,
  falha no segundo delete/rollback, readback em outra conexao e retry. Credencial
  protegida lida somente pelo harness, sem output/versao/sincronizacao.
- Pacote runtime somente DLL/EXE/deps/runtimeconfig; excluidos PDB/logs/contas/
  configs/source/docs/segredos. Helper operacional de deploy copiado somente ao
  staging, nao checkout/source do servidor. Hash errado recusado antes de parar
  tarefas. Nao houve commit/push nem alteracao do Site/producao.
- Ruling: alvo remoto e o executavel ativo medido nos listeners/tarefas, nao
  C:\WYDCDK\Server\WydCdk.Server.Core.exe da raiz. Ambos usam
  test-releases\20260924-v7670\WydCdk.Server-v7670-20260924. Custo: nao atualizar
  binario inativo da raiz, que nao define o sandbox atual.
- `dotnet publish ... -c Release -r win-x64 --self-contained true --no-restore`
  exit0 em artifacts/publish-20260926-autotrade-close. SDK10.0.401; pinning/BL025
  continua separado, nao declarar build release reproduzivel por esta execucao.
- Zip SHA256 A3DB22B1641BBD8F528050D4F3E4CA18FF02FDEA31765A648F0A9A47C49DAFDF.
  Core AAB4BE2800AF1BF5C25CF0EEE6F7D11EF1EE314EE894D5621BBB5C7869450E71;
  World E3267EF6AA5E824651E360DEEC3A5372E17FC3FEC008CA050FA749947AB069AD;
  Protocol D6CE109145A1302832F763DE7E1917B9124B99B955E8A756A1E7EDE3BB4CC295.
  Todos iguais no destino; 197 arquivos completos conferidos apos instalacao.
- SANDBOX_DEPLOY_PASS: tarefas WYDCDK-Server-UP/PVP Running, 0.0.0.0:8281/8282
  Listen com PIDs5568/5608 e exe no alvo aprovado. Backup preservado em
  C:\WYDCDK\Server\test-releases\backup-autotrade-close-7c34c243ab694647b2dd8e168bc864d7.
  Staging stage-autotrade-close-20260926-01 conserva archive/helper, sem configs.
  Sem teste autenticado contra banco remoto; rede funcional validada localmente.

### T6: reforcos de wire e evidencia do harness

- Escopo: somente harness/scripts/MDs; nenhum codigo de servidor, novo runtime,
  deploy, commit/push ou Site/producao. Working tree/HEAD/master preservados.
- RED real em Close assertion accepted wrong-signal-id, seguido de RED em
  Close assertion accepted wrong-pk-state. AssertClose agora verifica todos os
  sinais (count/size12/Id do autor) e PKs (count/size16/Id/payload state0).
  Fixture exige guilty0; estado1/combate continua nas regressoes proprias.
- RED de evidencia apos smoke real bem-sucedido: Smoke evidence lacks
  runtime/harness hash manifest. Runner agora grava client.log stdout+stderr
  e evidence.json version1: exit nativo, hashes de runtime/World/Protocol,
  DLL harness/Protocol e script executados, hashes das fixtures originais.
  O arquivo contem somente metadados/hashes, nao blobs/senhas/PIN/chaves.
- GREEN cf2b5eebee4d4ee791c8d9d76a1e3055: Test-AutoTradeCloseEvidence passou
  verificando hashes atuais, packet output e resultado; self-tests de wire
  rejeitaram Id/PK errados com checksum valido.
- Falha negativa10cf2ed09cce434f890cbd1d5681e5d9, runtime anterior: normal236
  ausente, native exit=-532462766 preservado e stderr completo no client.log.
  Nao transformar erro nativo em PASS mesmo quando Tee-Object termina bem.
- Sem rede externa/VPS, somente loopback18283/copias; hashes originais intactos
  e porta liberada. Build0avisos0erros e suite259 PASS confirmadas nesta fatia.
- Review independente: um Important corrigido RED->GREEN. Falha no cleanup
  podia impedir manifesto; Complete-AutoTradeCloseEvidence agora grava em
  finally independente, mantendo ExitCode e CleanupCompleted=false se falhar.
  Teste isolado com cleanup que lanca excecao grava JSON e propaga o erro;
  GREEN cleanup-0894bafff5c344238c187922870efe16. Smoke final com helper novo
  passou em 8bd21c4cfd1d4db5a47f28f673839e9b; guard nao aceita cleanup incompleto.
- Final: minor (deferred): validador de manifesto ainda usa quantidade minima,
  nao exige papeis/nome de cada entrada; runner grava todos os sete artefatos.
- Final: minor (deferred): regex de conteudo sensivel cobre formatos literais,
  nao e certificacao geral/sanitizador. Output do harness permanece restrito a
  metadados wire; nao imprimir autenticação para satisfazer um teste.
