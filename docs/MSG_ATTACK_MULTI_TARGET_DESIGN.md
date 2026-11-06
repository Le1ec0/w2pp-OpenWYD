# Design: `MSG_Attack` fisico multi-alvo

**Status:** implementacao, revisao de codigo e validacao local concluidas; runtime self-contained corrigido publicado e verificado no sandbox em 01/10/2026. Evidencia operacional em `CLIENT_E2E_SMOKE.md`.
**Escopo:** slots fisicos `Damage == -2` de `MSG_Attack` no port C#.
**Referencia primaria:** TMProject2GlobalClient, TMSrv 7.69.
**Comparacao obrigatoria:** `Server/W2PP`.

## Objetivo

Processar em uma unica operacao autoritativa os slots fisicos de uma acao, na
ordem do pacote, preservando indices e estado compartilhado. `WorldHub` toma o
lock uma vez para o batch; o host monta uma unica `AttackConfirmation` e envia
todos os frames depois de `WorldHub` devolver o resultado.

A API fisica unitaria existente permanece como wrapper de um batch de um slot,
para manter os chamadores e a regressao unitaria.

## Evidencia nas fontes

- TMProject 7.69: `_MSG_Attack.cpp`, loop de alvos iniciado perto da linha 328;
  ramo fisico perto da 462; relay final perto da 2371; varredura de
  `TargetKilled[]`/`MobKilled` depois do relay, perto da 2377.
- W2PP: `_MSG_Attack.cpp`, loop perto da linha 296; ramo fisico perto da 422;
  relay final e varredura de mortes perto das linhas 1749 e 1755.
- Ambas as fontes percorrem os slots por indice, calculam
  `BASE_GetDoubleCritical` somente no ramo fisico do indice 0, compartilham
  `DoubleCritical`/`Progress` durante a acao e abortam o handler com `return` se
  a distancia exceder o alcance. Morte e finalizada somente depois do relay
  normal; um `return` anterior pula relay e varredura de mortes, sem desfazer as
  mutacoes ja realizadas.
- Ambas declaram `MAX_TARGET = 13`. Os tipos aceitos pelo parser C# representam
  13, 1 e 2 slots (`103`, `157` e `158`).
- O passivo de classe 3 roda apenas no indice 0, quando existe slot 1, o bit
  aprendido `0x200000` esta ligado e o teste `rand()%4` tem sucesso (1 em 4).
  Ao ativar, substitui o slot 1 por
  `TargetID=MOB_EMPTY` e dano secundario; o slot 0 aplica dano primario mais
  secundario. Portanto o alvo original do slot 1 nao recebe aquele ataque.
- A divergencia de `DoubleCritical` ja decidida permanece: a source 7.69 usa
  `rand()%60 + 10` para o piso aleatorio do dano secundario; W2PP usa `60` fixo.
  O C# atual segue a regra 7.69.
- `CMob.cpp` atribui `Range = BASE_GetMobAbility(...EF_RANGE)` e logo depois
  sobrescreve com `23` nas duas fontes. O C++ calcula distancia usando as
  coordenadas do pacote (`m->PosX/PosY/TargetX/TargetY`). O C# atual mede entre
  posicoes autoritativas do atacante e do alvo. Este batch conserva a regra C#
  de alcance `> 23`; mudar a origem das coordenadas fica como recorte separado.

## Comportamento proposto

### Entrada e ordem

1. Somente quando `SkillIndex == -1` o host percorre os slots presentes no frame
   e passa ao `WorldHub` cada slot cujo marcador de dano seja `-2`, com indice
   original e ID de alvo. A ordem crescente dos indices e preservada; slots com
   outros marcadores continuam fora deste recorte. Uma skill valida conserva o
   proprio caminho mesmo se um slot posterior trouxer marcador fisico invalido.
2. `WorldHub` resolve todos os slots em uma secao critica, mantendo estado
   compartilhado de `DoubleCritical` e `Progress` para a acao. O cliente nao e
   autoridade para o byte `DoubleCritical`.
3. O calculo de `DoubleCritical`/`Progress` ocorre no maximo uma vez, somente se
   o indice original 0 chegar ao ramo fisico, como nas fontes. Os flags
   resultantes sao reutilizados nos slots fisicos posteriores. O passivo que
   reserva o slot 1 tambem so pode ocorrer no indice 0.
4. Cada resultado conserva o indice wire. O resultado agregado contem os
   resultados por slot, o estado final do atacante, flags/progresso da acao,
   efeitos capturados e o status de conclusao/abort.
5. Slots repetidos para o mesmo alvo observam as mutacoes do slot anterior. Se
   o primeiro slot o deixa com HP zero, o seguinte trata o alvo como morto e nao
   aplica dano novamente.

### Abort por alcance

Se um slot fisico exceder 23, o batch termina naquele indice. Nao ha rollback:
mutacoes, progresso e efeitos imediatos dos slots anteriores permanecem. O host
nao transmite a `AttackConfirmation` final da acao. Mortes acumuladas ficam sem
finalizacao nesse caminho, e slots posteriores nao sao processados. Efeitos
imediatos ja capturados, como a atualizacao de mana do alvo quando ManaControl
se aplica, ainda sao enviados fora do lock.

Falhas de alvo que hoje apenas rejeitam/ignoram aquele alvo permanecem resultados
locais do slot e nao abortam o batch. Gates comuns, inclusive `10/type77`,
continuam em fatias separadas; este trabalho nao os implementa nem os altera.

### Mortes e saida de rede

O `WorldHub` adia as consequencias de morte acumuladas ate o batch terminar
normalmente. No caminho normal, o batch finaliza cada morte uma vez e agrega as
consequencias existentes no C# (remocao, XP, drops, Pista/castelo e snapshots)
ao resultado agregado. A tentativa de consumir pergaminho de ressurreicao de
jogador tambem precisa ocorrer nessa finalizacao, nao durante o loop, para que
um abort nao consuma o item.

O lock termina antes de qualquer envio. O host envia primeiro a
`AttackConfirmation` unica com todos os slots preenchidos e, depois, os frames de
remocao, spawn, XP, drop, notificacoes e demais efeitos agregados. Em abort, o
host nao envia relay final nem efeitos de morte postergados, mas envia os efeitos
imediatos de slots ja processados. Assim nao ha I/O de rede dentro do lock e a
ordem de frames de morte continua depois do relay final. Ha uma diferenca interna
de ordenacao em relacao ao C++: para manter a mutacao atomica sem enviar sockets
sob o lock, o `WorldHub` finaliza as mortes no fim do batch antes de o host
transmitir o relay; a ordem observavel dos frames continua sendo relay e depois
efeitos de morte.

## Limites desta fatia

- Implementar somente o processamento multi-alvo de ataques fisicos `-2` quando
  `SkillIndex == -1`; ManaControl ja portado e estado compartilhado da acao,
  sem introduzir novos gates de mana ou cooldown.
- Nao implementar outros gates de alvo, `Action2`, `Action3`, ticks, mana/cooldown
  novos, Ilusao, E2E visual ou inicializacao do cliente.
- O range segue posicoes autoritativas C# neste recorte; a diferenca para as
  coordenadas wire 7.69/W2PP permanece registrada.
- A habilidade `Maos Sangrentas` aparece no ramo 7.69 de critico e nao no trecho
  equivalente do W2PP; equivalencia/port C# sera uma fatia separada.
- Nao muda a politica de publicacao: somente validacoes locais e, depois de
  passarem, publicacao sandbox previamente autorizada. Sem Site, producao,
  commit ou push.

## Resultado da implementacao

- `WorldHub.TryApplyPhysicalAttackBatch` processa ate 13 slots fisicos
  ascendentes sob um unico lock; a API unitaria existente e wrapper do slot 0.
- O host percorre o tamanho do frame recebido, envia os slots `Damage == -2`
  uma vez ao batch e monta uma unica `AttackConfirmation` do snapshot final do
  atacante, escrevendo dano nos indices originais. O byte `DoubleCritical` do
  cliente e limpo. Relay e efeitos de rede acontecem depois da secao critica; no
  caminho normal o relay agregado precede remocao/spawn e os demais efeitos de
  morte.
- `DoubleCritical`/`Progress` sao compartilhados; sem slot fisico 0 o progresso
  permanece o recebido. O passivo de classe 3/bit aprendido `0x200000` reserva
  slot 1 com alvo zero e dano secundario, sem atingir o alvo que estava naquele
  indice. O Host so coleta slots `Damage == -2` no ramo `SkillIndex == -1`;
  uma skill valida permanece no fluxo de skill e um marcador posterior invalido
  e limpo.
- Finalizacao de NPC, summon e jogador ocorre uma vez depois de conclusao normal.
  OOR mantem mutacoes anteriores sem rollback, para nos slots posteriores, envia
  efeitos imediatos capturados fora do lock e suprime relay final e frames
  postergados de morte.
- Summons classe 4 sincronizam o HP da montaria depois de cada hit nao letal;
  para hit letal, a sincronizacao segue na finalizacao normal, preservando o
  abort por alcance sem finalizar a morte.
- Revisao estatica encontrou tres defeitos Important: propagacao de dano
  secundario/override ManaControl entre slots, sincronizacao de HP de montaria e
  selecao incorreta de rota skill/fisico. Todos foram corrigidos e receberam
  regressao de unidade ou wire.
- Engine.Tests: 321/321; build completa da solution: SDK 10.0.401, 0 avisos e
  0 erros. O smoke TCP de 13 slots falhou antes do Host em
  `Backup/Build/AutoTradeCloseSmoke/e3572fac1d82473189e079d7ba966df0` e passou
  depois em `Backup/Build/AutoTradeCloseSmoke/93da451534ec4f3499b2bb028ff163c0`;
  o smoke final do primeiro publish passou em
  `Backup/Build/AutoTradeCloseSmoke/2e6a52a615684949afb4f34f0403c9f4`.
  Depois das correcoes, multi-alvo self-contained passou em
  `72619534043c4fcb8277baf9c27ae400`, skill mista em
  `49d6e484af894cbc987dca346b9a5e74` e ManaControl em
  `bc93db88392f4ddc8f86ed35d5aa5c74`.
- O runtime corrigido de 197 arquivos foi republicado no sandbox; pacote SHA-256
  `5B3260C1321C1F4D185488273D3F5660E023FBA1FDA7F255ECDAC2A8A87CAFAD`.
  Backup verificado de 199 arquivos; UP/PVP `Running`, listeners 8281/8282 e
  hashes Core/World/Protocol conferidos. Staging removido. Detalhes em
  `CLIENT_E2E_SMOKE.md`.
- A verificacao e headless e usa loopback. Nao valida interface/jogo visual. A
  faixa autoritativa C# `>23` continua diferente da medida por coordenadas wire
  do C++ e fica para um recorte separado.

## Verificacao aplicada

Com TDD e smoke TCP headless em loopback, sem abrir o WYD.exe:

- 1, 2 e 13 slots, preservando indices e exatamente uma confirmacao agregada;
- ordem, alvo repetido e estado compartilhado entre slots;
- `DoubleCritical` e `Progress` calculados uma vez; byte do cliente ignorado;
- passivo de classe 3 que reserva slot 1 e nao atinge o alvo original daquele
  slot;
- dano secundario compartilhado e slot reservado atualizado no override de
  ManaControl;
- sincronizacao de HP da montaria summon em hit nao letal e finalizacao deferida
  em hit letal;
- skill valida que mantem o fluxo de cura quando um slot posterior tem marcador
  fisico invalido;
- morte no primeiro slot finalizada uma vez apos conclusao normal;
- abort por alcance depois de uma mutacao: sem rollback, relay ou finalizacao de
  morte; efeitos imediatos anteriores preservados;
- regressao da API unitaria como wrapper do batch;
- smoke de rede confirmando uma resposta de ataque por acao e o alinhamento dos
  resultados com os indices wire.

## Autorrevisao

- As fontes 7.69/W2PP foram comparadas; a divergencia do piso de dano secundario
  e a diferenca de coordenadas do gate de alcance estao registradas.
- Testes confirmam 1/2/13 slots, ordem, alvo repetido, estado compartilhado,
  slot passivo, morte normal e abortos com estado parcial.
- O smoke TCP confirmou um relay de 13 slots por participante e indices
  autoritativos 1/2. Nao houve inicializacao de `WYD.exe` nem validacao E2E
  visual.
