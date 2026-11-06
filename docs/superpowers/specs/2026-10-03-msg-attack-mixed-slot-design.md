# Design: `MSG_Attack` com slots fisicos e de skill

**Status:** spec para revisao do usuario; nenhuma implementacao deste recorte foi iniciada.
**Contrato:** TMProject2GlobalClient TMSrv 7.69.
**Comparacao:** `Server/W2PP` e produtor do cliente TMProject 7.69.
**Escopo:** processar, em uma acao ordenada, os marcadores `Damage == -2` (fisico) e `Damage == -1` (skill) coexistindo no mesmo `MSG_Attack`.

## Problema confirmado

O TMSrv 7.69 percorre cada slot em ordem. Em `_MSG_Attack.cpp`, o ramo fisico
testa `dam == -2` (linha 462) e o ramo de skill testa `dam == -1` com
`skillnum` valido (linha 574); o W2PP faz o mesmo nas linhas 422 e 488. Portanto,
o tipo da acao nao pode ser inferido somente de `SkillIndex`.

O produtor do cliente tambem contem uma emissao mista: em
`Projects/TMProject/TMFieldScene.cpp:8439`, o fluxo `TargetType == 5` pode, quando
`Range == -1`, escrever `Damage=-2` no slot 0 e `Damage=-1` no slot seguinte
(`:9037-9046`). Na tabela `Servidor/Server/Common/skilldata.csv` deste checkout,
as entradas `TargetType=5` (skills 47 e 86) usam `Range=5` e `Range=6`; assim, a
condicao `Range==-1` nao e ativada pela tabela base atual. A capacidade continua
no codigo do produtor e o servidor legado trata ambos os marcadores por slot.

No host C#, `Program.cs` coleta `Damage==-2` apenas se `skillGate.IsPhysical`
(`SkillIndex==-1`, perto da linha 2578). Para uma skill com `SkillIndex>=0`, o
dispatcher processa os slots `-1` e ignora os `-2`. A chamada de skill e a
chamada do batch fisico tambem adquirem o lock do `WorldHub` separadamente, o
que impede preservar uma unica ordem atomica para uma acao mista.

## Objetivo

Processar todos os slots reconhecidos do frame em ordem crescente, sob um unico
lock do `WorldHub`, com uma unica resposta `AttackConfirmation`. Manter o
calculo e os efeitos ja implementados para cada ramo; este recorte nao adiciona
novos efeitos de skills. Todos os frames de rede continuam sendo enviados pelo
Host depois que o `WorldHub` libera o lock.

## Comportamento

1. A classificacao e por slot: `Damage==-2` escolhe o ramo fisico;
   `Damage==-1` escolhe o ramo de skill somente quando `SkillIndex` e valido.
   A ordem/indexacao wire e preservada nos formatos de 1, 2 e 13 slots.
2. Os gates e calculos atuais permanecem no ramo correspondente. Em skill,
   MaxTarget/type28 antecede party/type27. Slots bloqueados mantem
   `TargetID/-1`; skills ainda sem efeito C# portado continuam sem mutacao.
3. `DoubleCritical`, `Progress`, o passivo de slot 1 e ManaControl continuam com
   a semantica compartilhada por acao ja aprovada. O ramo fisico so resolve
   `DoubleCritical/Progress` se o indice original 0 chegar ao ramo fisico; um
   slot 0 de skill nao autoriza recalcular esses campos no primeiro fisico
   posterior. Se o passivo fisico no slot 0 reservar wire slot 1, essa
   sobrescrita substitui qualquer marcador original do slot 1 e o alvo original
   desse indice nao e processado.
4. O Host consome custos/gates de acao que hoje precedem o loop uma vez. Dentro
   do `WorldHub`, um batch tipado percorre os slots reconhecidos e chama helpers
   lock-held dos ramos existentes; nenhum helper abre socket ou escreve frame.
5. O batch agrega resultados tipados por slot, snapshots finais, overrides de
   wire (incluindo o slot passivo reservado), eventos `CrackError` e efeitos
   diferidos. O Host monta e envia uma `AttackConfirmation` unica com os indices
   originais no sucesso e depois envia os demais frames capturados, sempre fora
   do lock.
6. Um `OutOfRange` do ramo fisico aborta a acao no slot corrente: preserva
   mutacoes/efeitos imediatos anteriores, nao faz rollback, ignora slots
   posteriores, suprime a confirmacao final e nao finaliza mortes diferidas.
   Outros resultados locais, inclusive rejeicao/gate de skill, seguem sem abortar
   salvo se a regra 7.69/W2PP daquele ramo ja abortar o handler.
7. Mortes de slots fisicos e de skill sao finalizadas uma vez somente quando o
   batch termina normalmente, ainda sob o lock para preservar o estado atomico.
   O Host envia primeiro a resposta agregada e depois os frames de morte/XP/drop
   capturados, na ordem wire. Em abort, nao se remove o alvo, nao se concede
   XP/drop e nao se consome item de ressurreicao como consequencia de morte
   diferida. Mutacoes anteriores nao sao revertidas.
8. As APIs publicas unitarias atuais permanecem como wrappers de um slot, para
   preservar chamadores e regressoes.

## Desenho proposto

- Adicionar request de acao com slots discriminados (`Physical`/`Skill`), indice
  original, ID do alvo e dados de rolagem. O request leva uma unica definicao de
  skill/estado de acao, quando aplicavel, e nao aceita dano autoritativo do wire.
- Adicionar `WorldHub.TryApplyAttackActionBatch`: valida indices e ordem antes
  do lock, adquire `gate` uma vez, compartilha o estado do batch fisico, executa
  helpers lock-held e captura resultados/efeitos sem I/O de rede. Se o batch
  reservar wire slot 1 pelo passivo, ignora a entrada original daquele slot.
- Extrair a implementacao atual de `TryApplySkillAttack` para um helper lock-held
  sem mudar a API publica nem o resultado dos ataques de um slot. A API publica
  de skill vira wrapper de um batch de um slot. Adaptar a finalizacao de morte
  de skill para ser postergada pelo batch, preservando o comportamento normal de
  um batch concluido.
- Manter `TryApplyPhysicalAttackBatch` como wrapper/adaptador da nova operacao
  para requests exclusivamente fisicos; preservar `TryApplyPhysicalAttack` como
  wrapper unitario de compatibilidade.
- Mover a montagem final de `AttackConfirmation` para depois do resultado do
  batch e emitir os efeitos coletados fora do lock, sem relay duplicado.
- Nao reorganizar o restante do monolito modular nem ativar novas familias de
  skill. Mudancas ficam limitadas ao fluxo `MSG_Attack`, WorldHub e testes/docs
  diretamente ligados a esse contrato.

## Criterios de aceite

- Testes de dominio para 1/2/13 slots, tipos misturados, indices originais,
  ordem e repeticao de alvo com mutacao visivel ao slot seguinte.
- Cobrir slot 0 skill seguido de fisico: nenhum `DoubleCritical/Progress` novo
  fora do indice 0; cobrir slot 0 fisico e slots mistos posteriores para provar
  que o estado de acao continua unico.
- Forcar o passivo no slot 0 fisico antes de uma skill no slot 1; provar que o
  override wire reservado substitui a skill e o alvo original nao e mutado.
- Cobrir MaxTarget/type28 antes de party/type27 no slot skill misto; cobrir
  `CrackError` por slot, slots skill bloqueados e skill sem efeito portado sem
  mutacao.
- Cobrir morte em qualquer ramo, finalizacao uma vez no sucesso normal e
  preservacao de estado parcial/ausencia de relay/sem finalizacao no abort fisico
  por alcance.
- Smoke TCP loopback com ao menos um pacote realmente contendo `-2` e `-1`, uma
  confirmacao agregada por participante, slots alinhados, recursos finais e
  frames auxiliares depois do relay. Usar copias descartaveis das fixtures; nao
  iniciar `WYD.exe`.
- Engine.Tests e solution build sem avisos/erros; smoke Debug e self-contained;
  hashes das fixtures originais intactos e porta `18283` liberada.
- Atualizar rastreabilidade, smoke, PROGRESSO, BACKLOG e contexto existente em
  AGENTS. So publicar runtime sandbox depois de todos esses gates; sem Site,
  producao, commit ou push.

## Fora de escopo e limites

- Efeitos/skills sem implementacao C# atual, novas formulas, novos gates e
  alteracoes no produtor/arquivo de dados do cliente.
- `Action2`, `Action3`, cliente visual, inicializacao do jogo e E2E visual.
- Reorganizar `WorldHub` por modulos ou portar outros ramos de combate.
- Mudar sem evidencia os gates de alcance de skill ja existentes. O abort
  estendido por este desenho e especificamente o `OutOfRange` fisico do batch.

## Revisao da spec

- A fonte 7.69, W2PP e o produtor de cliente foram comparados; o caso Range=-1
  do produtor esta documentado como ramo nao ativado pelos dados-base atuais.
- O contrato reaproveita o batch fisico aprovado e explicita a unificacao por
  slot; nao inventa um efeito novo nem assume que um frame e exclusivamente de
  skill ou exclusivamente fisico.
- A reserva fisica do slot 1 sobrescreve e impede processar a entrada que o
  cliente havia colocado nesse indice, como no buffer mutavel C++.
- As fronteiras de ordem, lock, relay, efeitos, morte e abort estao separadas;
  os criterios de teste correspondem a cada uma.
- Sem mudanca de codigo, interface ou runtime ate a aprovacao desta spec escrita.
