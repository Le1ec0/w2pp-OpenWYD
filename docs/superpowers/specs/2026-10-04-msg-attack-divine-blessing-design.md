# COMBAT-ATTACK-DIVINE-BLESSING-001 — Spec de design

Status: aprovada pelo usuario. Esta spec descreve a Bencao Divina
como uma acao sintetica interna ao `MSG_Attack`; nao altera a persistencia de
`MOB.Exp` nem promove o W2PP a autoridade de comportamento para este ramo.

## Objetivo e limites

Implementar no servidor C# o comportamento server-side 7.69 de `SkillIndex=201`
quando o bit autoritativo `MOBEXTRA.SecLearnedSkill & 0x2` estiver ativo. A
habilidade cria uma cura sintetica de skill 29 sobre o proprio conjurador.
O resultado usa a mesma acao/batch ordenada do WorldHub, sem reentrar no
dispatcher de rede, e produz o relay da cura antes da confirmacao da acao 201.

O recorte cobre o adapter do bit, o gate especial 201, custo de mana da cura
sintetica, HP/montaria/mapa/Clan4, estado temporario, ordem dos relays, abort
posterior do batch e FakeExp. A habilidade continua dependendo de dados de
conta autoritativos. Aprendizado de skills secundarias, comandos de
administracao, outros bits de `SecLearnedSkill`, Site, producao e E2E visual
ficam fora do escopo.

## Evidencia e comparacao das fontes

### TMProject 7.69

- `Servidor/Source/Code/TMSrv/_MSG_Attack.cpp:715-751` verifica
  `Extra.SecLearnedSkill & 0x2`, define `DivineBlessing` e `RateRegen` com
  `MaxHp - Hp`, e chama `ProcessClientMessage(..., TRUE)` com um `MSG_AttackOne`
  sintetico: `SkillIndex=29`, alvo proprio, slot `Damage=-1`,
  `CurrentMp=-2`, tick `0x0E0A1ACA` e `Motion=255`.
- `Basedef.h:186` define `SKIPCHECKTICK=235543242`; o tick sintetico e esse
  sentinel. O caminho server-side pula verificacoes dependentes do tick
  (delay/classe/aprendizado e limites de alvo), mas continua pelo consumo de
  mana normal do skill 29. `CurrentMp=-2` e campo do pedido sintetico, nao
  autorizacao recebida do cliente.
- `Basedef.h:154` define `MAX_SKILLINDEX=250`, portanto 201 cabe na tabela.
  `Basedef.cpp:6291` zera `g_pSpell` antes de carregar as linhas; o
  `Servidor/Server/Common/SkillData.csv:30` tem a linha 29, mas nao a 201,
  portanto a definicao generica 201 permanece zerada. O handler reconhece 201
  explicitamente. Nao criar uma linha CSV ficticia para habilita-lo.
- A linha 29 existe em `SkillData.csv` como cura `InstanceType=6`, custo base
  de mana 30. O pedido sintetico usa `Special=0`; `BASE_GetManaSpent` aplica
  a formula existente e o `SaveMana`, e pode falhar por mana insuficiente.
  Nessa falha o handler filho envia a correcao `MSG_SetHpMp` e retorna; o
  handler externo 201 continua e envia sua confirmacao depois.
- `_MSG_Attack.cpp:752-845` executa a cura sob `DivineBlessing`: Clan 4 bloqueia
  o alvo; a expressao de atributo do mapa e preservada literalmente. No
  self-target sintetico, as duas leituras usam as mesmas coordenadas do ator,
  entao essa condicao nao rejeita o alvo. `RateRegen` e convertido em cura, com
  minimo 6 para deficit positivo menor que 6; montarias de indice 786, 1936
  ou 1937 dividem a cura pela sancao com piso 2 (1936 multiplica o divisor
  por 10, 1937 por 1000); HP e limitado a MaxHp; depois chama SetReqHp e
  limpa os dois campos temporarios no caminho normal.
- Depois do ramo Type 6, `_MSG_Attack.cpp:1761` copia o `dam` assinado ao slot
  wire. A divisao da montaria muda o delta de HP, mas o slot permanece
  `-RateRegen`; validacao anterior de alvo morto ou bloqueio de mapa pode
  zerar/substituir o slot antes desse ponto.
- A validacao anterior dos slots ignora alvos com HP <= 0 para skills diferentes
  de 31/99. Assim, HP exatamente 0 ja e rejeitado pelo gate externo; HP negativo
  passa por esse gate, mas a subacao 29 consome mana e nao cura o proprio ator.
- A EXP de cura usa delta real de HP dividido por 8, teto 120 e exclusao
  `attacker == target`. Como a acao sintetica cura o proprio conjurador, ela
  nao gera contribuicao FakeExp. O MOB.Exp persistido nao e modificado por
  este recorte.
- A chamada filha ocorre dentro do loop da acao externa e envia seu
  `MSG_AttackOne` antes de o handler externo terminar e relayar sua
  confirmacao. Se um slot posterior abortar a acao externa, as mutacoes e o
  relay filho ja ocorreram; a confirmacao agregada do pai e suprimida.

### W2PP e contrato de armazenamento

- O `_MSG_Attack.cpp` W2PP nao tem uso ativo de `DivineBlessing`, `RateRegen`
  ou bit 0x2 de `SecLearnedSkill`. `CMob.h` declara `DivineBlessing` e
  `RateRegen`; `Basedef.h` declara `STRUCT_MOBEXTRA.SecLearnedSkill`. Isso
  confirma os campos de estado, nao o comportamento da Bencao Divina.
- O arquivo de conta em uso no C# tem 7945 bytes; `MOBEXTRA` inicia em 5600
  e cada slot tem 552 bytes. `WorldHub` preserva esses bytes brutos, mas
  `LegacyMobCombatState` expoe somente o `LearnedSkill` primario e o port
  ainda nao tem um adapter tipado para `SecLearnedSkill`.
- O comentario W2PP do campo (`Basedef.h` proximo da linha 626) nao e prova
  suficiente do offset ABI: existe padding natural depois de `Citizen`. A
  implementacao deve gerar/usar um probe x86 para confirmar offset e largura
  em ambos os headers antes de definir o adapter. O offset nao sera inferido do
  comentario nem usado para alterar outros campos do blob.

## Decisoes de design

1. O listener reconhece 201 como uma excecao especial a ausencia de linha na
   tabela. O pai conserva os gates comuns anteriores ao bloco de skill, aplica
   o delay comum de 700 ms, nao cobra mana (entrada `g_pSpell[201]` zerada) e
   nao requer bit primario nem classe. O bit `SecLearnedSkill & 0x2` decide
   apenas se a subacao sintetica sera criada. Sem o bit, o pai continua sem
   cura e sem erro de aprendizado, como a source.
2. A subacao 29 e criada pelo servidor sob o mesmo lock do `WorldHub`, com
   alvo igual ao conjurador, marcador de skill, contexto `SKIPCHECKTICK` e
   definicao 29 real. O consumo de mana e feito pelo algoritmo existente de
   skill 29 com `Special=0`, atraves de uma operacao interna do batch, sem
   reentrar na API publica de mana nem fazer I/O sob lock. Nao se aceita
   `CurrentMp=-2` de pacote cliente.
3. `DivineBlessing` e `RateRegen` sao contexto efemero local da subacao, nao
   campos persistidos ou mutaveis entre pacotes. O contexto e limpo em todos
   os retornos, inclusive mana insuficiente, Clan 4, atributo de mapa e abort,
   para impedir que um pedido futuro reutilize uma taxa antiga. Essa limpeza
   adapta caminhos de erro que podem deixar os campos armados na source.
4. Cada slot 201 elegivel cria no maximo uma subacao filha na ordem de slots;
   alvos extras obedecem `MaxTarget=0` da entrada 201 zerada e o gate comum
   `10/type28`. A subacao filha e imediata: o Host envia suas confirmacoes
   capturadas antes da confirmacao agregada do pai. Se um slot posterior
   abortar, emite as filhas ja capturadas, mantem HP/MP ja aplicados e omite
   apenas a confirmacao final do pai e efeitos diferidos.
5. A subacao usa as regras Type 6 para Clan 4, a expressao exata de atributo de
   mapa, montaria, teto MaxHp, SetReqHp e FakeExp. Para self-target, o teste de
   mapa compara o mesmo atributo nos dois lados e nao bloqueia. A validacao
   anterior de alvo morto permanece: HP negativo consome a mana da filha, mas
   nao cura. Como e self-heal, FakeExp e zero; `MOB.Exp` continua inalterado.
   A confirmacao filha relaya HP/MP autoritativos e slot proprio; a confirmacao
   externa preserva seu layout e a ordem das mutacoes ja existentes.
6. I/O de sockets permanece fora do lock. A implementacao devolve snapshots
   e frames/destinatarios ordenados no resultado do batch; o Host apenas
   serializa e envia essa captura.

## Requisitos de validacao

- ABI: fixture de `MOBEXTRA` 552 bytes e conta 7945 bytes, com bit 0x2 ligado,
  desligado e outros bits adjacentes; offset/width conferidos por probe x86
  compilado contra TMProject 7.69 e W2PP.
- Gates: skill 201 sem definicao CSV e sem bit segue sem filha; com bit dispara
  filha. Bit vindo do wire, `CurrentMp=-2` do cliente ou primary LearnedSkill
  nao pode habilitar a cura.
- Custos/ordem: custo pai zero; filha skill 29 paga o custo normal; mana
  insuficiente nao cura, limpa o estado efemero e envia a correcao de HP/MP
  antes da confirmacao externa; a acao 201 permanece processavel.
- HP: HP cheio, deficit pequeno (piso 6), cura integral, teto MaxHp, Clan 4,
  HP 0 e negativo, paridade da expressao de mapa para atributo 0 e 0x40, e
  montarias 786/1936/1937 com valores de sancao abaixo/acima do piso.
- Relay: dano no slot AttackOne igual a `-RateRegen` mesmo quando a montaria
  reduz o HP aplicado; HP negativo preserva o consumo de mana, mas relaya o
  slot invalidado (`TargetID=0`, `Damage=0`) sem reviver o personagem.
- Multi-slot/abort: slots 1, 2 e 13, quantidade e ordem de filhas, MaxTarget
  inclusivo e abort fisico posterior; efeitos filhos anteriores sobrevivem e
  a confirmacao final do pai e suprimida.
- Relay: filho `MSG_AttackOne` antes do pai, campo CurrentHp/Mp autoritativo,
  destinatarios da janela visivel iguais ao relay normal do ataque; HP e slot
  conferidos em loopback TCP headless.
- Persistencia: `MOB.Exp` igual antes/depois; fixtures originais e blobs
  restaurados no finally do harness.
- Completar com Engine.Tests, build da solucao, self-test do harness e smoke
  TCP em loopback Debug e self-contained. Build deve ser headless.

## Fora de escopo

Aprender/resetar a nona skill, espelho magico e outros consumidores de
`SecLearnedSkill`; demais usos de `DivineBlessing`/`RateRegen`; redistribuicao
de pontos de skill; paridade com uma implementacao W2PP inexistente; E2E visual
com `WYD.exe`; Site/producao; commit/push. Publicacao runtime-only no sandbox
segue apenas os gates locais e a autorizacao sandbox ja registrada.
