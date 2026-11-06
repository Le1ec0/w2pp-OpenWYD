# SHOP-005 — checkpoint periódico de personagem

**Status:** implementada localmente; integração MariaDB pendente  
**Escopo:** snapshot autoritativo, coordenação de persistência e round-robin do checkpoint de personagem.

## Intenção

Persistir periodicamente o estado mutável de personagens em jogo para que vendas e outras mutações já aplicadas pelo Host não dependam somente do logout. O checkpoint deve seguir a cadência observada em TMProject 7.69 e usar os stores existentes, sem reconstruir cegamente o payload interno `MSG_DBSaveMob`.

## Abordagens consideradas

1. **Ring fixo de IDs `1..999` — escolhida pelo usuário.** TMProject define `MAX_USER=1000` e verifica o timer a cada oito ticks de 500 ms. Dentro desse tick, o loop avança pelos IDs vazios até encontrar o primeiro jogador em `USER_PLAY`, salva no máximo um jogador e continua no ID seguinte no próximo tick. Logo, com `N` jogadores ativos, cada personagem volta ao checkpoint aproximadamente a cada `N × 4 s`. O Host aloca hoje IDs monotônicos; será adaptado para reservar e reutilizar IDs `1..999`, mantendo `0` reservado.
2. **Ring somente de sessões `Playing`.** Evitaria alterar a alocação atual e teria cadência semelhante entre jogadores ativos, mas não conservaria a faixa/ordem fixa de IDs da referência; por isso mantemos o ring fixo escolhido.

## Objetivos

- Alocar IDs ativos `1..999` e liberá-los ao encerrar a conexão; recusar conexões novas quando os 999 slots estiverem ocupados. IDs de jogadores continuam abaixo da faixa de NPCs, que começa em `MAX_USER`.
- A cada intervalo nominal de 4 s, procurar a partir do cursor o próximo slot `1..999` cuja sessão esteja em `Playing` e cujo participante ainda exista; avançar pelos slots vazios no mesmo tick, salvar no máximo um jogador e deixar o cursor no slot seguinte. Se nenhum jogador estiver em jogo, percorrer e envolver a faixa sem iniciar uma gravação.
- Coordenar checkpoint e save de saída por slot. Revalidar sessão e capturar o snapshot somente depois de adquirir o lock do slot; manter o lock até a conclusão do writer para evitar que uma conexão reutilizada receba um snapshot antigo.
- Capturar do `WorldHub` MOB, posição, `MOBEXTRA`, affects e equipamento do cliente de 18 slots. Captura usa o lock do mundo só para copiar os bytes; I/O ocorre fora dele.
- Usar uma operação composta nos stores para persistir MOB/posição, `MOBEXTRA`, affects e sidecar de equipamento atomicamente.
- Coordenar writers de conta. O adapter de arquivo usa um gate de estado compartilhado por todos os writers da instância; MariaDB mantém a mutação do blob e do sidecar dentro de uma transação `Serializable`.
- Reutilizar o mesmo contrato de checkpoint no logout/desconexão, eliminando a diferença entre o save periódico e o save de saída. O logout faz uma tentativa; em falha, registra resultado e libera a conexão/ID, como o caminho 7.69 que envia o save sem confirmação síncrona. O ID só volta ao pool depois do save de saída e da remoção do participante.
- Manter no máximo um save de personagem em andamento. Se I/O ultrapassar o intervalo nominal, ticks vencidos são coalescidos; não criar saves paralelos nem reproduzir catch-up. A cadência se alonga sob sobrecarga.

## Contrato de dados

- O MOB e os dados de posição capturados do participante são a fonte do Host. `MOB.Coin` é Gold carregado; `AccountCoinOffset` é saldo bancário e deve ser preservado pelo checkpoint, nunca derivado do MOB.
- O store preserva `SkillBar` existente no blob, porque `0x0378` tem writer próprio e um snapshot do mundo pode anteceder a gravação daquele opcode. `ShortSkill` e Donate permanecem com seus writers próprios.
- `Cargo` e os três contadores diários não têm estado mutável autoritativo no snapshot atual; preservar os bytes já armazenados. Não reproduzir o defeito do `SaveUser` que escreve os três contadores em `MobCount1`.
- Não inventar estado ou colunas para `VemProEternal`/`PartyPassword`, ausentes do ABI W2PP ativo, nem executar o efeito de exportação do campo `Export` da referência.
- O recycle bin permanece transitório. O checkpoint não cria transação entre a venda em memória, saldo de conta e MOB de tesouraria; a cadência existente de 12 s para tesoureiros continua separada.

## Persistência e concorrência

- Os bytes da conta que não pertencem ao snapshot autoritativo devem sobreviver à atualização. O sidecar e o MOB devem representar a mesma captura de 18 slots quando uma operação composta retorna sucesso.
- O backend MariaDB realiza blob e sidecar na mesma transação `Serializable`. O backend de arquivo usa journal de gravação para recuperar o arquivo de conta e o sidecar como uma única operação, além de um gate compartilhado por todos os writers da instância. Leituras que precisam combinar blob e sidecar aguardam o mesmo gate e recuperam journal preparado antes de expor o estado. O modo `--accounts-root` continua destinado a um único processo; os processos UP/PVP de produção usam MariaDB.
- O cursor do scheduler nunca mantém o lock global do `WorldHub` durante I/O. O lock por slot impede concorrência entre o save do timer, o save de saída e a reutilização de ID.
- Falha de persistência não altera o estado em memória nem cancela o loop; registra erro. O checkpoint periódico tenta novamente quando o cursor retornar ao personagem; o save de saída não retém o slot para retries.

## Decisões de implementação estabelecidas na revisão

- A atomicidade do arquivo de conta mais sidecar é requisito de recuperação: um sidecar antigo poderia restaurar equipamento já vendido no próximo login. O adapter de arquivo usará o journal existente/generalizado; MariaDB usa uma única transação.
- O modo de arquivo é de processo único. A configuração de produção inicia UP e PVP com MariaDB; não será adicionado lock distribuído ou cross-process para `--accounts-root` neste recorte.
- Em logout, uma falha é best-effort: registrar e liberar o slot depois da saída, evitando bloquear capacidade ou deixar retry antigo gravar sobre um novo ocupante.
- O scheduler mantém uma gravação em andamento por vez. Ticks durante I/O são coalescidos e não geram fila nem saves paralelos; sob carga, o intervalo efetivo aumenta.

## Fora de escopo

- A implementação do dispatch `0x037A` (concluída em recorte separado), buyback, compra/estoque ou atomicidade/replay de uma venda entre personagem, conta e tesouraria.
- Persistir recycle bin, Cargo ou quest diária sem um produtor mutável autoritativo.
- Reproduzir `MSG_DBSaveMob` como protocolo interno de DBSrv, a atribuição incorreta dos contadores, `Export` ou campos sem representação C# ativa.
- Alterar schema de conta, o blob W2PP de 7.945 bytes, o Site, cliente, deploy, commit ou push.

## Critérios de aceite

- IDs simultâneos ficam limitados a `1..999`, são liberados e reutilizados após encerramento, e conexões excedentes são encerradas sem ultrapassar a faixa de NPCs.
- O scheduler inicia no slot 1, pula slots vazios/não-Playing no mesmo tick, salva no máximo um participante `Playing` por intervalo nominal de 4 s, retoma do próximo slot e faz wrap `999 -> 1`.
- Uma sessão desconectada ou substituída antes de adquirir o lock não é salva como sessão atual; o lock mantém a identidade do slot estável até terminar qualquer save iniciado.
- A persistência conserva SkillBar, ShortSkill, Donate, Cargo, contadores diários, `AccountCoinOffset` bancário e bytes não pertencentes ao snapshot, enquanto grava MOB (incluindo o Gold carregado), posição, Extra, affects e equipamento de 18 slots.
- Escritores concorrentes da instância não perdem atualizações no adapter de arquivo; recuperação do journal mantém blob e sidecar consistentes após interrupção. O adapter MariaDB persiste a operação composta em uma transação. Saves lentos não se sobrepõem nem acumulam fila de ticks; erros são reportados e o scheduler segue adiante.
- Build, testes de domínio/wire, integração local de persistência e smoke de rede/headless cobrem o novo contrato. E2E visual permanece pendente.

## Restrições do projeto

- Manter .NET 10, `Nullable=enable`, `ImplicitUsings=enable`, I/O assíncrono com `CancellationToken`, dependências apontando para dentro e sem microserviços.
- `WorldHub` continua como fachada; não fazer reorganização em massa.
- Registrar diferenças entre TMProject e W2PP em `Server/docs/PORT_TRACEABILITY.md`; atualizar `BACKLOG.md` e `PROGRESSO.md` in-place.
- Sem commit/push/deploy automático.

## Fontes de comportamento e estado atual

- TMProject define `MAX_USER=1000` em `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/Basedef.h:70`; `SaveCount` inicia em 1 em `TMSrv/Server.cpp:623`. O loop completo em `TMSrv/ProcessSecMinTimer.cpp:160-189` pula slots que não estão em jogo dentro do mesmo tick e para após um save. Essa evidência substitui a interpretação anterior do handoff que atribuía um ciclo fixo de 999 × 4 s mesmo com muitos slots vazios.
- O Host aloca e reutiliza IDs `1..999` em `Server/src/WydCdk.Server/Program.cs`; `LoginSessionRegistry` mantém estado por conexão e `WorldHub.TryGetCharacterSnapshot` captura os bytes vivos em `Server/src/WydCdk.World/WorldHub.cs:7888+`.
- O writer de arquivo atual persiste MOB, posição, Extra e affects em `LegacyFileAccountStore.cs:591+`; MariaDB muta o blob em `MariaDbWorldCharacterStore.cs:173+` via `MutateBlobAsync`/transação `Serializable`.

## Shutdown do Host: drain de handlers e checkpoint tributário final (04/11/2026)

TMProject 7.69 (`TMSrv/Server.cpp:5605+`) e W2PP
(`TMSrv/Server.cpp:4571+`) usam `WM_CLOSE` para fechar arquivos e gravar
configurações; o caminho não chama `SaveUser` nem `ProcessMinTimer`. Os saves
de personagem continuam vinculados a `CharLogOut` (`Server.cpp:8995` em
TMProject; `:7912` em W2PP), enquanto a gravação de tesouraria fica no timer
`TIMER_MIN=12000ms` (`ProcessSecMinTimer.cpp:2564` e `:2523`). Não há evidência
de uma varredura tributária de shutdown na referência.

Decisão do Host C#: depois de parar o accept loop e cancelar os runtimes, aguardar
os handlers TCP já aceitos terminarem. O `finally` deles chama
`SaveCharacterOnDepartureAsync` com `CancellationToken.None`; o drain garante
que essa tentativa e o fechamento da sessão terminem antes da saída. O
`LegacyConnectionTaskDrain` deixa de aceitar tasks após iniciar o drain e
preserva falhas para que `Task.WhenAll` as reporte.

Após o drain e a conclusão do timer periódico, executar uma última chamada a
`SaveRegisteredAsync` com `CancellationToken.None` se `--npc-root` estiver
configurado. Ela grava os mesmos snapshots raw e por MOB do writer existente;
continua separada do writer de conta e não cria transação conjunta. TMProject e
W2PP não mostram esse flush em `WM_CLOSE`, então esta é uma regra de durabilidade
do shutdown do Host, não um comportamento alegado como port 7.69. Se o timer for
cancelado durante uma escrita, o writer remove o temporário e conserva o arquivo
anterior até substituí-lo completamente.

Aceite deste recorte: teste determinístico comprova que shutdown espera todos os
handlers ativos, rejeita novos registros depois do drain e reporta falha de
handler. O checkpoint final só usa o store já coberto pela integração local;
não cria transação conjunta entre conta e tesouraria nem altera a cadência
periódica `TIMER_MIN=12000ms`.

## Estado da validação e fronteira de durabilidade (04/11/2026)

O checkpoint de personagem está ligado ao dispatch `MSG_Sell`, mas não grava
síncrono no commit da venda. Ele coordena o blob de conta e sidecar entre os
writers do backend (journal v2 sob gate process-local compartilhado por raiz
entre store de conta e AutoTrade; blob e sidecar na transação `Serializable` do
MariaDB). A tesouraria continua em
checkpoint independente de 12 s, por MOB raw; não existe transação, journal ou
replay que junte os dois lados. Após interrupção, vendedor e tesouraria podem
estar cada um no estado anterior ou posterior. O teste de caracterização com
stores reais confirma que o arquivo tributário só avança depois de
`SaveRegisteredAsync`; o smoke headless confirmou separadamente o save do
vendedor no logout.

Build completa passou com 0 avisos/erros. Engine.Tests passou `590/592` com
`TEMP/TMP` no workspace, incluindo o teste novo da coordenação entre checkpoint
e AutoTrade; as únicas falhas foram os dois loopbacks bloqueados por
`SocketException 10013`. O smoke
autenticado headless confirmou logout do vendedor, Coin/carry persistidos e log
da varredura final de tesouraria no shutdown; não comprova bytes tributários
não-zero ou transação conjunta. MariaDB ainda não foi validado: `127.0.0.1:3306`
está fechado e o último provider 1042 ocorreu antes de DDL. O Host também liga
as mensagens localizadas 170, 116 e 167 para as três rejeições comprovadas;
`_MSG_CloseShop` fora da visão envia `0x0196`/ID `30000`, e slot de carry vazio
recebe `SendItem` `0x0182`. Índices negativos/fora da tabela permanecem
silenciosos porque `GetItemPointer` retorna `NULL`. Evidências em
`Server/Backup/Build/shop-005-carry-resync-20261104/` para a validação final de
build/test/carry vazio, além de `Server/Backup/Build/npc-sell-close-shop-20261104/`
para os smokes de venda e fechamento da loja.
