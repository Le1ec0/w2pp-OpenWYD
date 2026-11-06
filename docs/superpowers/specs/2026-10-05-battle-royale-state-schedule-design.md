# COMBAT-ATTACK-BR-SCHEDULE-001 — Especificação do estado agendado

Status: aprovada pelo usuário; implementação validada localmente em 05/10/2026
conforme o plano associado. Esta especificação define somente a configuração e
as transições de `BrState` usadas pelo gate de `bParty`. Os efeitos completos do
Battle Royale permanecem fora deste recorte. Evidencias de build/testes/smoke em
`Server/Backup/Build/AttackBattleRoyaleStateSchedule-20261005`.

## Objetivo e limites

Fornecer ao Host C# uma configuração opcional para alimentar o estado agendado
do Battle Royale no `LegacyCombatWorldState`. Com configuração válida, o Host
avalia o horário local e a elegibilidade do servidor e publica as transições
0–3 já consumidas pelo override de Guild Battle Royale em `bParty`.

Este recorte cobre leitura e validação da configuração, cálculo de elegibilidade
por grupo/servidor, relógio e transições, atualização segura do estado do mundo,
cancelamento cooperativo do loop e testes de unidade/integrados locais.

Ficam fora: avisos no mapa, limpeza por nível, portas do Coliseu, atualização de
grid dos personagens, remoção de party, criação do item de recompensa,
pontuação/vencedor, demais comandos do evento, persistência do estado, hot
reload, cliente visual, Site e produção. O estado é volátil e começa em zero a
cada processo.

## Evidência e comparação das fontes

### TMProject 7.69

- `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/Server.cpp:7928-8028`
  seleciona o servidor diário por `(tm_mday - 1) % NumServerInGroup`, exige
  `BRItem > 0` e permite bypass quando `TESTSERVER` ou `LOCALSERVER` estão
  ativos. O relógio é local (`localtime`).
- A rotina avalia `tm_min % 20`: estado 0 passa a 1 no minuto de fase 0; estado
  1 passa a 2 na fase 5; estado 2 passa a 3 na fase 13; estado 3 volta a 0 a
  partir da fase 14. As condições dependem do estado anterior, e a mudança de
  estado só ocorre quando a rotina observa a fase correspondente.
- `Server.cpp:1578` e `1601` carregam `BRItem` e `BRHour` da configuração do
  jogo. `Server.cpp:4887` atribui `ServerIndex = j - 1`; `Server.cpp:4918-4928`
  conta servidores ativos do grupo e limita `NumServerInGroup` a 1..10.
- `TMSrv/_MSG_Attack.cpp:427-428` lê `BrState` e `BRItem` no caminho de
  `bParty`. O gate C# já consome os campos correspondentes do estado do mundo.

### W2PP

- `Server/W2PP/Source/Code/TMSrv/Server.cpp:6840-6940` mantém a mesma
  elegibilidade, estados 0–3, fases 0/5/13/14 e comportamento dependente do
  estado anterior.
- `Server/W2PP/Source/Code/TMSrv/Server.cpp:1226` e `1461` leem `BRItem` e
  `BRHour`; `Server.cpp:3987-4037` configura `ServerIndex` e conta os servidores
  ativos, usando o mesmo intervalo 1..10 para o total.
- `TMSrv/_MSG_Attack.cpp:386-387` mantém a mesma dependência do estado no gate
  `bParty`. Não há divergência comportamental identificada para este recorte.

### Host C# atual

- `WorldHub.SetCombatWorldState` substitui o registro completo sob o lock do
  mundo. `SetDoubleModeMultiplier` demonstra a atualização pontual de um campo
  sem substituir os demais.
- `Program.cs` já carrega configurações opcionais antes de `TcpListener.Start`
  e executa loops agendados com `PeriodicTimer`, cancelamento e `DateTime.Now`.
- `LegacyCombatWorldState` contém `BattleRoyaleState` e `BattleRoyaleItem`,
  ambos com padrão zero. Nenhum produtor Host atualiza esses campos.

## Decisões de design

1. **Configuração explícita e opcional.** Adicionar `--battle-royale-config
   <arquivo.json>`. Sem essa opção, o recurso permanece desativado, o estado
   fica em zero e nenhum loop é iniciado. A configuração é carregada e
   validada antes de abrir o listener; arquivo ausente, JSON inválido ou campo
   inválido encerra o início do Host com erro claro.

2. **Formato tipado.** O documento exige os seis campos abaixo, em PascalCase,
   para evitar que campos ausentes assumam silenciosamente valores de servidor
   de produção:

   ```json
   {
     "BRItem": 700,
     "BRHour": 19,
     "ServerIndex": 0,
     "NumServerInGroup": 3,
     "TestServer": false,
     "LocalServer": false
   }
   ```

   `BRItem` é inteiro não negativo; zero desativa o agendamento. `BRHour` deve
   estar entre 0 e 23. `ServerIndex` deve estar entre 0 e 9 e é ordinal
   zero-based, conforme `j - 1` nas fontes. `NumServerInGroup` deve estar entre
   1 e 10. Os booleanos devem estar presentes. `ServerIndex` e
   `NumServerInGroup` são validados nos intervalos da fonte, sem exigir
   `ServerIndex < NumServerInGroup`, pois a referência compara esses valores
   diretamente mesmo se a lista de servidores tiver lacunas. O valor de item
   não é validado contra `MAX_ITEMLIST` neste recorte: os efeitos que consumiriam
   esse item não serão executados aqui.

3. **Elegibilidade por tick.** O evento é elegível quando `BRItem > 0` e
   (`TestServer` ou `LocalServer` ou
   `ServerIndex == (diaDoMes - 1) % NumServerInGroup`). Os dois flags são
   explícitos; o Host não os infere do endereço bind, ambiente ou modo UP/PVP.
   Uma configuração sem bypass em servidor fora da rotação não produz
   transições.

4. **Transições dirigidas por estado.** A classe de domínio recebe um instante
   local e preserva seu estado atual entre chamadas. Para a hora configurada,
   calcula `fase = minuto % 20` e aplica no máximo uma transição por chamada:

   | Estado atual | Condição observada | Próximo estado |
   | --- | --- | --- |
   | 0 | `fase == 0` | 1 — preparação |
   | 1 | `fase == 5` | 2 — batalha |
   | 2 | `fase == 13` | 3 — recompensa |
   | 3 | `fase >= 14` | 0 — encerrado |

   Fora da hora configurada, sem elegibilidade ou sem item habilitador, não há
   transição. A rotina não deriva o estado atual diretamente do horário, não
   recupera fases perdidas e não persiste estado. Portanto, um processo iniciado
   no meio de um ciclo permanece em 0 até observar a próxima fase 0 elegível.
   Saltos de relógio que pularem as fases exatas 5 ou 13 mantêm o estado
   anterior, como na fonte; a fase >=14 encerra o estado 3 quando observada.

5. **Host e relógio.** O Host inicia um loop opcional com `PeriodicTimer` de
   dois segundos, usando `DateTime.Now` para corresponder ao `localtime` da
   referência. O primeiro tick ocorre depois da inicialização do listener. O
   loop encerra cooperativamente pelo token de shutdown. A frequência garante
   várias observações durante cada minuto de fase em operação normal e segue o
   padrão do loop AutoDouble existente.

6. **Atualização atômica do estado do mundo.** Quando a classe de domínio
   retorna uma transição, o Host chama um método específico do `WorldHub` para
   atualizar `BattleRoyaleState` e `BattleRoyaleItem` sob um único lock,
   copiando o registro existente com `with`. Isso preserva todos os demais
   campos e evita perder uma mudança concorrente, como `DoubleModeMultiplier`,
   entre uma leitura e uma substituição separadas. O gate lê o par estado/item
   do mesmo snapshot.

7. **Apenas estado.** Nenhuma transição envia frames, notices ou mensagens,
   altera personagens/parties/mapa/portas, cria itens ou pontua participantes.
   Em particular, a fase 3 apenas atualiza o estado consumido pelo gate; não
   entrega recompensa. Os efeitos completos requerem outro recorte aprovado.

## Critérios de aceite da implementação

- Sem `--battle-royale-config`, o Host mantém os valores padrão 0 e não inicia
  o loop.
- Configuração completa e válida é aceita; campo faltante, tipo incorreto,
  arquivo ausente ou limites inválidos falham antes de `listener.Start()`.
- Testes determinísticos cobrem transições 0→1→2→3→0, repetição de ticks na
  mesma fase, inicialização no meio do ciclo e salto por cima das fases 5/13.
- Testes de elegibilidade cobrem cada lado da rotação por dia do mês, servidor
  selecionado, servidor não selecionado, ambos os bypasses, `BRItem=0`, total de
  grupo mínimo/máximo e limites inválidos.
- Teste de integração do Host comprova que cada transição publica o estado e o
  item no mundo sem alterar os demais campos do `LegacyCombatWorldState`; o
  shutdown cancela o loop.
- Regressão do gate `bParty` comprova que apenas estados 1, 2 e 3 com
  `BRItem > 0` ativam o override dentro da região aprovada, mantendo controles
  fora da região e com estado/item desativados.
- Build da solução, testes de domínio/wire aplicáveis e smoke local/headless do
  Host passam antes de registrar este recorte como validado. A execução será
  feita na etapa de implementação, após aprovação desta especificação.

## Riscos e limites conhecidos

- Estado volátil implica reinício em zero; iniciar durante preparação, batalha
  ou recompensa não restaura a fase que estaria ocorrendo no servidor legado.
- Rotação e horário dependem do relógio local e da precisão do sistema. O teste
  determinístico injeta instantes para validar as regras sem aguardar o relógio
  real.
- O estado 3 não comprova que a recompensa foi criada ou recebida. A fase de
  recompensa é apenas um valor consumido pelo gate até o recorte de efeitos.
- O modo de config explícita evita ativação acidental baseada em loopback, mas
  um operador ainda pode configurar deliberadamente `TestServer` ou
  `LocalServer`.
