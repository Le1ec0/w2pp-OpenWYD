# Tempo, input e combate

## Objetivo

Manter o PvP responsivo com baixo custo: cada input recebe um instante de origem, o servidor permanece autoritativo e nenhuma verificacao depende de espera ocupada, polling agressivo ou dano declarado pelo cliente.

## Relogio do protocolo

### Confirmado no C++

- `MSG_STANDARD.ClientTick` e um contador em milissegundos.
- O `TMSrv` atualiza `CurrentTime` com `timeGetTime()`; nao ha divisao por 8 nem arredondamento por tick de simulacao.
- `ProcessClientMessage` rejeita pacotes de cliente com `ClientTick == SKIPCHECKTICK` (`235543242`). Esse valor especial e reservado a mensagens internas do servidor.
- `Exec_MSG_Action` e `Exec_MSG_Motion` encaminham o pacote recebido por `GridMulticast`; portanto, o timestamp do input nao e refeito pelo servidor.

### Portado e testado

- O listener C# usa I/O assincrono e nao possui loop de espera ocupada.
- `_MSG_Action` preserva `frame.Header.ClientTick` ao ecoar e transmitir um movimento aceito. O teste `action parser` protege esse contrato.
- `_MSG_Motion` (`0x036A`, 20 bytes) foi portado: em `USER_PLAY`, valida checksum/tamanho, retransmite os campos `Motion`, `Parm` e `NotUsed` ao proprio cliente e aos observadores, preservando o `ClientTick`. O teste `motion wire` cobre o wire e o timestamp.
- Movimento aceito atualiza a posicao em memoria do `WorldHub`; esse estado e usado nos refreshes visuais de outros jogadores.
- `ClientTickPolicy` bloqueia `SKIPCHECKTICK` em toda entrada de rede do listener, antes do dispatch. O teste `client tick policy` protege que o marcador interno nunca seja aceito de um cliente.
- `LoginSessionRegistry.TryAcceptAttackTiming` porta os gates de 800 ms e da janela de relogio antes de qualquer calculo de combate. O teste `attack timing gate` cobre primeiro ataque, limite exato, spam, marcador reservado e timestamps fora da janela.

## Ataque e skill

### Confirmado no C++

`ProcessClientMessage` entrega `_MSG_Attack`, `_MSG_AttackOne` e `_MSG_AttackTwo` ao mesmo `Exec_MSG_Attack`. Antes de calcular dano, o handler usa `ClientTick` para protecoes baratas:

1. Rejeita um novo ataque antes de 800 ms desde o anterior.
2. Registra violacao se o tempo voltar mais de 100 ms.
3. Rejeita um timestamp mais de 15 s no futuro ou mais de 120 s atras do relogio do servidor.
4. Valida habilidade passiva, classe, habilidade aprendida, atraso da skill e quantidade maxima de alvos antes do calculo de dano.

O pacote de ataque inclui posicao, alvo, habilidade e uma lista de danos, mas o servidor recalcula o resultado a partir do mundo, da tabela de spells e dos atributos. O port nao deve aceitar `Dam[]`, HP, MP ou EXP recebidos como autoridade.

### Pendente deliberadamente

- Sonda C++ x86 confirmada: `MSG_Attack=168`, `MSG_AttackOne=68`, `MSG_AttackTwo=80`, `STRUCT_DAM=8`; os campos comuns sao `PosX=34`, `TargetX=38`, `AttackerID=42`, `Motion=46`, `SkillIndex=56` e `Dam=60`. A sonda descartavel fica em `artifacts/layout-probe`.
- `AttackRequest` valida e le os tres tamanhos, expondo somente intencao: posicao, alvo, atacante, movimento, parametro e skill. O teste `attack parser` cobre os tres wires.
- O listener agora liga parser, gate temporal, gate de metadados e desconto autoritativo de mana. Em sucesso, `AttackConfirmation` retransmite o mesmo tipo/tamanho com `CurrentHp`, `CurrentExp`, `CurrentMp` e `ReqMp` derivados do MOB; em falta de mana, `SetHpMpConfirmation` sincroniza HP/MP/ReqHp/ReqMp como o `SendSetHpMp` legado. O teste `attack response wire` garante que HP/EXP/MP/ReqMp declarados pelo cliente sao substituidos, e que `Dam[]` e zerado enquanto a resolucao ainda nao existe.
- Para o marcador físico `Dam[0].Damage == -2`, o servidor resolve o `TargetID` contra participantes conectados, usa as posições do `WorldHub`, rejeita distância acima de 23, bloqueia mesma guilda e aplica a aritmética inteira de `BASE_GetDamage` seguida da redução PvP de um quarto. O HP do alvo é mutado sob o mesmo lock e o dano calculado é escrito no relay. `PhysicalCombatFormula` e `world physical attack` cobrem fórmula, HP, guilda e alcance.
- Skills (`Dam[].Damage == -1`) já resolvem a primeira rota elemental de alvo entre jogadores: o listener deriva `Magic`/`WeaponDamage`, percorre os slots permitidos pelo wire até `MaxTarget + 1`, calcula base, defesa/resistência e muta cada HP sob o lock do `WorldHub`. O listener rejeita atacante com HP zero antes do timing/mana; a `skill 99` agora reproduz a rota de ressurreição do legado, com rolagem intermediária, HP/MP finais de 1–50% dos máximos, `RequestedMana`, `MSG_UpdateScore` e `MSG_SetHpMp`; o recall cobre spawn base de cidade/iniciante, `ChargeGuild` carregado por `--guild-data`, posição autoritativa da sessão, `MSG_Action` de efeito e busca de célula livre por ocupação no raio legado, mas ainda não tem gestão/persistência de guerra, `pHeightGrid`/carga de mapa nem toda a sincronização `SendEtc`/`CreateMob` do legado. A rota de cura (`InstanceType == 6`) também foi portada para alvo vivo: reproduz `Special`/`InstanceValue`, limite por `ClassMaster`, mínimo de 6 e os divisores das montarias 786/1936/1937, sem exigir `ItemList.bin`. A rota de desintoxicação (`InstanceType == 8`) limpa os tipos de affect confirmados no C++ e envia `MSG_UpdateScore` ao alvo com o affect compactado no wire legado. A aplicação genérica de `AffectType`/`TickType` agora reproduz slot por tipo, valor, nível, duração e o tick curto dos tipos 1/3/10; para skills agressivas, também aplica o gate de mapa seguro, estados de guerra explícitos, `RSV_BLOCK`, `AffectResist`, diferença de nível, guilda e `Clan==6`. O parry de `GetParryRate` soma `EF_PARRY` dos 16 equipamentos do alvo, aplica os bits de `Rsv`/skill do atacante e produz `-3` ou `-4` no relay sem reduzir HP. As duas fórmulas de `BASE_GetSkillDamage`, a composição de `WeaponDamage` e o leitor de `ItemList.bin` já foram portados e testados. Ainda faltam regras completas de elegibilidade por mapa/PK/war, `Delay`/`Aggressive` fora desse gate, NPCs, morte/XP e as demais regras de resistência.

## Barra de skills

- `_MSG_SetShortSkill` (`0x0378`, 32 bytes) foi portado. O C++ copia quatro bytes para `STRUCT_MOB.SkillBar` e dezesseis para `STRUCT_ACCOUNTFILE.ShortSkill[slot]`; a sonda x86 confirmou `SkillBar` no offset 796 do MOB.
- O listener exige personagem em `USER_PLAY` e persiste os dois blocos no arquivo legado. O teste `short skill wire and persistence` cobre wire e offsets persistidos.
- Depois carregar os valores de item corretos, ligar as fórmulas portadas ao resolver de skill e portar `Delay`/`Aggressive`, resistências, PK/war, NPCs e morte/XP. Somente então ampliar o resolver físico para os demais tipos de alvo e skills.

## Diretriz de desempenho

- Timestamp preciso melhora ordem e responsividade do input; por si so nao reduz CPU.
- Economia real vem de validar cedo, descartar pacotes invalidos antes de alocar/calcular, manter estado por conexao e processar apenas eventos recebidos.
- Uma arquitetura subtick completa como a de engines modernas exigiria simulacao, predicao e reconciliacao do mundo. Nao deve ser introduzida antes de existir combate autoritativo mensuravel; o protocolo WYD ja oferece a base de timestamp em ms necessaria para o port fiel.
