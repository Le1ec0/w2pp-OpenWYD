# Engenharia do servidor

> **Direcao vigente (19/09/2026):** o projeto e um emulador WYD novo em C#/.NET.
> A referencia primaria passa a ser a engenharia reversa comunitaria 7.69 em
> `Backup\Tools\ReferenceSources\TMProject2GlobalClient`; nao houve source oficial
> vazada. O WYD.exe da release-alvo sera compilado dessa source, com servidor
> compilado do port C#/.NET. O launcher presente no repositorio e precompilado,
> sem projeto-fonte identificado, e deve ser revisto antes do uso. Do 7600 sera
> preservada somente a UI escolhida, por importacao
> seletiva apos validacao; nao entram seu EXE, launcher, SN/serverlist, hooks ou
> DLLs. A source 7.69 le `sn.bin` cru em 143 bytes e `sn2.bin` opcional em 900;
> serverlist tem 7040 bytes, mas o exemplo de 7045 bytes incluido nao decodifica
> corretamente. O source fixa a porta em 8281. O protocolo inicial ja passou
> smoke sintetico em loopback. `Client\7670` ja tem build proprio com
> `ClientVersion=7670` e SN/serverlist regenerados para
> `Local -> UP -> 127.0.0.1:8281`; a build de smoke desativa chamadas diretas
> `ShellExecute(...)` para nao abrir o `Change.exe` do sistema.
> O contador HTTP em `127.0.0.1:8280` ainda nao tem responder.
> No smoke de rede de 24/09/2026, o servidor publicou `0`/`-1` no arquivo de
> status, criou e limpou o heartbeat e aceitou handshake mais `MSG_AccountLogin`
> de 116 bytes com `ClientVersion=7670`; uma conta inexistente recebeu frame
> `0x0101` de 140 bytes com checksum valido. O watcher de stdin foi deslocado
> para background porque o PTY Windows bloqueava a inicializacao do accept loop.
> Em 24/09/2026 o cliente abriu janela responsiva e foi encerrado ao fim do smoke.
> A cópia isolada recebeu a correção das seis texturas de render target para
> `D3DPOOL_DEFAULT`; no smoke de 23/09/2026 o cliente abriu uma janela responsiva
> sem o erro gráfico. A janela nativa não foi exposta ao controle CUA, portanto o
> teste de login e o E2E seguem pendentes.
> O restante deste documento e indice/evidencia historica e deve ser revisto por
> modulo durante o reinicio arquitetural ja aprovado.

Esta pasta documenta a engenharia do emulador WYD em C#/.NET. Complementa o
`AGENTS.md` com arquitetura, rastreabilidade de wire e evidencia por modulo.
As fontes comunitarias e a engenharia reversa servem como referencias de estudo;
nao existe source oficial vazada. O README da TMProject declara GPLv3 para o
codigo, origem por descompilacao, direitos do jogo reservados a Hanbitsoft e
existencia de bugs; nao inferir permissao para redistribuir binarios/assets do
jogo. Registros anteriores do port sao historicos e devem ser revalidados contra
a referencia primaria 7.69.

O backlog priorizado fica na raiz em [BACKLOG.md](../../BACKLOG.md), e o checklist resumido fica em [PROGRESSO.md](../../PROGRESSO.md). Este diretório não deve duplicar esses dois documentos.

## Como usar

- Nao registrar suposicoes como fatos. Cada item deve distinguir **confirmado no C++**, **portado/testado**, **pendente** e **decisao consciente**.
- Ao migrar uma mensagem ou sistema, atualizar o documento tematico com o wire, validacoes, estado alterado, custo de processamento e teste que protege o comportamento.
- Manter a compatibilidade antes de otimizar. Melhorias so entram depois de medir impacto e sem substituir a autoridade do servidor.

## Indice

- [Baseline de arquitetura e DTOs](PORT_ARCHITECTURE_BASELINE.md) — decisoes arquiteturais, contratos x86 e estagio de migracao.
- [Rastreabilidade por modulo](PORT_TRACEABILITY.md) — comparacao 7.69/W2PP/C# e estado das evidencias.
- [Auditoria inicial da UI do cliente](CLIENT_UI_AUDIT.md) — inventario estatico 7600/7670, candidatos e limites de compatibilidade.
- [Matriz de candidatos de UI](CLIENT_UI_CANDIDATES.json) — hashes, formato, catalogo consumidor e decisao atual por asset; regeneravel por [Compare-ClientInterfaceAssets.ps1](../tools/PortAudit/Compare-ClientInterfaceAssets.ps1).
- [Golden de selecao 7.69](../tests/WydCdk.Engine.Tests/Fixtures/V769/manifest.md) — fixture sintetica Win32, manifesto de origem e hash.
- [Gerador do golden Win32](../tools/PortAudit/Generate-V769CharacterSelectionGolden.ps1) — recompila o probe contra o header local.
- Golden do `MSG_CNFAccountLogin` 7.69: [manifesto](../tests/WydCdk.Engine.Tests/Fixtures/V769/account-login.manifest.md) e [gerador Win32](../tools/PortAudit/Generate-V769AccountLoginGolden.ps1); contrato separado do encoder W2PP ativo.
- Golden do `MSG_CNFCharacterLogin` 7.69: [manifesto](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login.manifest.md); contrato cliente selecionado de 1728 bytes, isolado. Os frames W2PP 2648/1832 e o TMSrv privado 7.69 de 2640 nao correspondem a esse header cliente. A extensao mapeia W2PP `MOBEXTRA.Hold` para `EXT1.Data[0]` FakeExp.
- [Vetores de HP/MP no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-hpmp.manifest.md): `BASE_GetHpMp`/incrementos coincidem com W2PP, mas a tabela `BaseSIDCHM` de Foema/Hunter e diferente; os valores seguem o `Basedef.cpp` comum selecionado no `.vcxproj` (HP 400000/MP 100000). A source TMSrv tambem tem uma copia local nao compilada e ABI divergente. `LegacyHpMpMath` permanece isolado do adapter/listener.
- [Vetores de Soul no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-soul.manifest.md): 31 casos source-derived cobrem os multiplicadores 7.69 e registram a divergencia para W2PP; `LegacyCurrentScoreMath` permanece isolado do adapter/listener.
- [Soul Kibita no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-kibita-soul.manifest.md): sete vetores cobrem o gate Mortal/nível/affect, a mutacao tardia e a divergencia de MaxMP em relacao ao W2PP.
- [Seed de abilities no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-ability-stage.manifest.md): dois vetores cobrem o bloco `Cur HP/MP`; formulas iguais no W2PP, com ressalva explicita para o limite de 16/18 equipamentos.
- [Clamps finais no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-clamps.manifest.md): tres vetores cobrem HP/MP, regen, Magic, Critical e as quatro resistencias; destaca o cap Magic 7.69 (190M) versus W2PP (1B).
- [Modificadores de resistencia no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-resistance-affects.manifest.md): 14 vetores cobrem os affects 3/8/16/25, a ordem transformacao/Perseguicao e as divergencias do W2PP.
- [Velocidade de transformacao no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-speed.manifest.md): 13 vetores cobrem Attack/Run de `Affect.Type=16`, gates, sobrescrita por ordem e divergencias com W2PP.
- [Dano da transformacao no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-damage.manifest.md): 16 vetores cobrem o dano aditivo, delta do multiplicador, aritmetica por nivel e divergencias Wolf/Eden com W2PP.
- [AC da transformacao no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-ac.manifest.md): 19 vetores cobrem AC sequencial, ordem, truncamento e divergencias Wolf/Titan/Eden com W2PP.
- [MaxHp da transformacao no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-maxhp.manifest.md): 20 vetores cobrem o multiplicador sequencial e as divergencias de HpAdd em Wolf/Bear com skill; `RegAdd` ja e coberto no recorte de resistencias.
- [Critical e equipamento da transformacao no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-critical-equipment.manifest.md): 20 vetores cobrem o Critical e a mutacao de `Equip[0]`/`EF_SANC`; Critical diverge no W2PP para Astaroth com skill e Eden.
- [Composicao dos efeitos da transformacao no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-composition.manifest.md): oito vetores reexecutam os estagios Type=16 sobre o mesmo snapshot, com comparacao W2PP e resistencia geral sem duplicacao.
- [Escudo Magico no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-magic-shield.manifest.md): seis vetores cobrem `Affect.Type=11`; o alvo usa `Level / 5 + Value` e o W2PP `Level / 3 + Value`.
- [Arma Magica no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-magic-weapon.manifest.md): sete vetores cobrem Type=9; Damage/multiplicador coincidem e o W2PP acrescenta Magic +5 por affect.
- [Toque de Athena no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-athena-touch.manifest.md): sete vetores cobrem Type=15; o alvo limita todos os Special em 255 e o W2PP limita Special1 em 200.
- [DEX no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-dexterity.manifest.md): sete vetores cobrem Type=6; alvo e W2PP usam o mesmo fator float, narrowing para short e ordem sequencial.
- [Fanatismo no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-fanatism.manifest.md): sete vetores cobrem Type=5; alvo e W2PP usam o mesmo fator float, narrowing para short e ordem sequencial.
- [Toque Sagrado no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-holy-touch.manifest.md): sete vetores cobrem Type=1; Run/Att/Int coincidem entre alvo e W2PP, com o gate de head index > 50; o AttackRun final permanece separado.
- [Possuido no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-samaritan.manifest.md): sete vetores cobrem Type=14; o alvo e o W2PP tem formulas diferentes para MaxHp/Con, preservadas no comparador.
- [Assalto no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-assault.manifest.md): sete vetores cobrem Type=13; o alvo altera somente o multiplicador de dano, enquanto o W2PP tambem muta Damage e MaxHp.
- [Samaritano no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-possessed.manifest.md): sete vetores cobrem Type=24; alvo e W2PP usam a mesma mutacao sequencial de ArmorClass.
- [Coordenador dos blocos de score no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.manifest.md): quatro vetores validam a ordem base/equipamento, Special, Type=16, Type=11, Type=9, Type=15, Type=5, Type=6, Type=12, Haste, Soul e Kibita, sem commit no MOB.
- [Dano final no login](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-damage.manifest.md): oito vetores cobrem o trecho terminal face/stat/level/multiplicador; nao incluem os blocos anteriores por classe existentes apenas no W2PP.
- Golden do `STRUCT_MOB` 7.69: [manifesto](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-mob.manifest.md) e [gerador Win32](../tools/PortAudit/Generate-V769CharacterMobGolden.ps1); adapter W2PP versionado cobre o layout de 1040 bytes, incluindo a aparencia do nome derivada de KILL_MARK. LearnedSkill word 0 e copiado; o word 1 das skills 200..246 e zerado para MOBs W2PP, pois a origem nao o armazena.

- [Tempo, input e combate](migration\tempo-input-combate.md) — `ClientTick`, protecoes anti-fraude, movimento, motion e preparacao do PvP.
- [Seguranca de protocolo](migration\seguranca-protocolo.md) — limites que realmente estao ativos no legado, checksums, tamanho e politica de rejeicao.

- [Dados de skills](migration\dados-skills.md) — `SkillData.csv`, IDs 9/10, colunas `Act` e separacao entre fonte do servidor e `SkillData.bin` do cliente.
- [Ambiente local](ambiente-local.md) — separação entre Site PHP, servidor C# loopback, arquivos legados e processos externos.
- [Tabela de drops 7.54](drops-7.54.md) — referência de conteúdo fornecida pelo usuário; ainda não é catálogo autoritativo nem foi validada contra os arquivos da release.

## Migrações MariaDB

As tabelas de estado pertencentes ao servidor estão descritas em
[`db/migrations/README.md`](../db/migrations/README.md). `accounts` e o catálogo
Donate continuam sendo pré-requisitos externos do Site.

## Estado de validacao atual (23/09/2026)

- Suite local: `dotnet run --project ./tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore` — 207 testes passaram, incluindo os contratos byte a byte isolados de CreateMob, CreateMobTrade, UpdateEquip, RemoveMob, UpdateAffect, UpdateScore e UpdateEtc 7.69, os adapters W2PP de CreateMobTrade e os relays visual/de remoção de autotrade, o DTO do request de abertura, o `LegacyAutoTradeBook`, o `LegacyAutoTradeFileStore` versionado, o `LegacyAutoTradeRehydrator` e o fechamento de reconexão do mesmo account/slot, as regras de vila/taxa, o gate explícito de alvo offline e o relay de `MSG_AutoTrade`, os DTOs/fixtures de `MSG_ReqBuy` e `MSG_ItemSold`, os gates puros de compra e o settlement de imposto, mais o contrato de commit compare-and-swap, a mutação/rollback do comprador no `WorldHub` e a proteção do slot no book, as fixtures de UpdateCarry e `MSG_SendAutoTrade`, o leitor server-side de ItemList, a integridade concorrente de `SerializedNetworkStream` em loopback, o round-trip dos tres opcodes de `ActionRequest` com a janela `VIEWGRIDX/Y=33`, o wire de `MSG_SetHpMode` usado pelo gate de HP de `Motion`, os gates de celula `0x80`/`0x20`, o recall autoritativo de movimento no `WorldHub`, os DTOs wire de trade, o estado bilateral em memoria de ofertas, o cancelamento bilateral no disconnect sem mutar carry/Gold, os gates isolados de listagem de autotrade, o plano dos dois personagens para persistencia, os planos de relay de `MSG_CNFCheck` e `MSG_UpdateCarry` e a conclusao atomica de carry/Gold em memoria. O dispatcher do host tambem passou em build separado; as migracoes MariaDB foram aplicadas no banco local `wyd_cdk` e o `MariaDbAutoTradeStateStore` passou round-trip; banco externo e compra atomica seguem pendentes.
- Suite atualizada em 24/09/2026: 207 testes passaram; a build da solução passou com 0 avisos/0 erros. O runner de migrações confirmou cinco tabelas do servidor, o round-trip de autotrade e o smoke de compra atômica/replay CAS no MariaDB local; VPS, banco externo e E2E continuam pendentes.
- O MariaDB local está em execução. Com autorização do usuário, as cinco tabelas do servidor foram derrubadas e recriadas do zero em `wyd_cdk`; `accounts` e os objetos do Site foram preservados. `wyd_site` existe, mas a configuração/segredo disponível não autenticou esse usuário; nenhuma senha ou grant foi alterada.
- O gate de movimento agora emite os avisos 46/47 confirmados de `Language.txt` por `MSG_MessagePanel` antes do recall autoritativo e do `MSG_Action` de teleporte.
- O probe MSVC Win32 recompilou o header 7.69 e produziu os mesmos 904 bytes da fixture versionada.
- Os probes MSVC Win32 tambem produziram os payloads de CreateMob (224/frame 236), CreateMobTrade (248/frame 260) e UpdateEquip (56/frame 68). CreateMob e UpdateEquip ja usam o contrato alvo nos caminhos integrados; o adapter W2PP de CreateMobTrade recebe payload 240 e projeta os 16 slots do legado para os 18 slots do contrato cliente.
- O probe de RemoveMob confirmou a equivalencia cliente/W2PP de 4 bytes de payload/frame de 16 bytes.
- O probe de UpdateAffect confirmou 32 entradas, payload/frame de 256/268 bytes e a adaptacao da ordem dos campos W2PP.
- O probe de UpdateScore confirmou payload/frame cliente de 140/152 bytes; o W2PP tambem mede 152 bytes, mas o DBSrv comum 7.69 empacota 147 bytes e exige adapter explicito.
- O probe de UpdateEtc confirmou payload/frame de 36/48 bytes; o adapter divide `Learn` W2PP nos dois words de `LearnedSkill` do cliente e nao transporta `Magic`, que pertence ao UpdateScore 7.69.
- O probe de layouts confirmou `STRUCT_ITEMLIST` de 164 bytes no cliente e 140 bytes nos headers server-side 7.69/W2PP; `LegacyServerItemDataTable` le o contrato de 140 bytes isoladamente, sem trocar o catalogo ativo nem declarar equivalencia dos consumidores de habilidade.
- Os DTOs 7.69 de selecao e respostas New/Delete geram frames de 920 bytes e passam no teste; o sucesso de New/Delete ja esta integrado no listener, com E2E visual pendente.
- `AccountLoginConfirmationV769` e `W2ppAccountLoginV1Adapter` passaram no golden sintetico de payload 1916 bytes; o adapter recusa truncar cargo ocupado e o listener emite a confirmacao alvo de 1928 bytes.
- `CharacterLoginConfirmationV769` passou no golden Win32 de payload 1716/frame 1728. Esse e o contrato escolhido pela definicao e pelo handler do cliente; o header TMSrv privado 7.69 conflita e nao deve ser copiado. O DTO MOB e o adapter W2PP passaram contra o golden Win32 1040; `W2ppCharacterLoginV1Adapter` compoe e testa o frame a partir dos dados W2PP, e a cadeia de login/spawn ja esta integrada no listener. Conversoes estreitas rejeitam overflow e LearnedSkill word 1 e zerado como extensao 7.69 ausente no W2PP. `LegacyHpMpMath` tem 12 vetores e `LegacyCurrentScoreMath` cobre reset/equipamento, Special, Soul normal (31 vetores), Kibita (7), seed de abilities `Cur HP/MP` (2), modificadores de resistencia (14), Haste `Type=2` (7), dano final (8), AttackRun final (8) e clamps finais (3); os recortes continuam isolados. `LegacyMountRunRules` cobre os 45 pisos de montaria aceitos pelo source gate. O alvo segue 7.69 nos seis primeiros mounts comuns (pisos 4/5), divergindo do W2PP (todos 6); o indice 2390 e rejeitado por ser uma leitura fora dos limites da tabela nas duas sources. Haste soma `Value` e retorna `RSV_HASTE=0x20`, igual ao W2PP. Os demais deltas source-derived incluem Soul/Kibita MaxMP, cap final de Magic e Holy em `Affect.Type=25`. Os fechamentos finais de dano e AttackRun coincidem para as mesmas entradas acumuladas, mas W2PP possui blocos anteriores por classe ausentes no alvo. Outros buffs, efeitos de transformacao nao cobertos pelos recortes atuais, EXT2, demais derivados e E2E continuam pendentes.
- O recorte de dano `Affect.Type=16` retorna separadamente o bonus aditivo de Damage e a soma dos deltas `multi - 100`, sem aplicar o multiplicador final nem integrar ao login; target/W2PP divergem no DamAdd de Wolf habilitado e Eden.
- O recorte AC `Affect.Type=16` escala sequencialmente o AC atual, com truncamento inteiro por affect e +5 por Wolf; Target/W2PP divergem no AcAdd de Wolf com skill, Titan e Eden. Continua isolado do login.
- O recorte MaxHp `Affect.Type=16` tambem multiplica sequencialmente o MaxHp atual; Target/W2PP divergem no HpAdd de Wolf e Bear com skill. `RegAdd` ja esta no recorte de resistencias; nenhum dos dois estagios foi integrado ao login.
- O recorte Critical/equipamento `Affect.Type=16` acumula Critical e retorna a mutacao do item de cabeca com `EF_SANC`; Critical diverge para Astaroth com skill e Eden, enquanto o item/sancao coincide. Usa o nivel do personagem, nao `Affect.Level`, e permanece fora do login.
- A composicao `Affect.Type=16` reune os estagios auditados para um snapshot imutavel, preserva a ordem e separa a resistencia geral dos efeitos de transformacao; oito vetores target/W2PP passaram. Continua fora do login e do listener.
- O recorte `Affect.Type=11` retorna o delta de AC sem clamp ou mutacao: `Level / 5 + Value` no alvo contra `Level / 3 + Value` no W2PP; seis vetores passaram.
- O recorte `Affect.Type=9` retorna Damage e multiplicador; o alvo nao altera Magic, enquanto o W2PP soma `+5` por affect. Sete vetores passaram.
- O recorte `Affect.Type=15` retorna os quatro Special; o alvo usa cap 255 em todos, contra cap 200 somente em Special1 no W2PP. Sete vetores passaram.
- O recorte `Affect.Type=6` aplica o fator float `(Value + 100) / 100.0f`, converte para `short` e preserva a ordem sequencial; sete vetores target/W2PP passaram.
- O recorte `Affect.Type=5` aplica o fator float `(100 - Value) / 100.0f`, converte para `short` e preserva a ordem sequencial; sete vetores target/W2PP passaram.
- O recorte `Affect.Type=1` retorna os deltas `Run -= Value`, `Att -= 30` e `Int -= 40` quando o head index é maior que 50; sete vetores target/W2PP passaram. O empacotamento final de `AttackRun` não foi integrado.
- O recorte `Affect.Type=12` aplica `(100 - Value) / 100.0f`, trunca para `int` e preserva a ordem sequencial da reducao de AC; sete vetores target/W2PP passaram.
- O recorte `Affect.Type=14` (Possuido) porta a regra do alvo (`MaxHp += value * 2`, `Con += value`) e registra a divergencia W2PP (`MaxHp += value * 22`, `Con += (Con + value) * 125 / 100`) em sete vetores; continua isolado do coordenador.
- O recorte `Affect.Type=13` porta somente `Level / 10 + Value` para o multiplicador de dano do alvo; o comparador W2PP registra tambem o aumento de Damage em 15%, o cap `MAX_DAMAGE=1000000000` e a reducao de MaxHp. Sete vetores passaram e o estagio continua isolado.
- O recorte `Affect.Type=24` (Samaritano) aplica `add = Ac / 4 + Value` e `Ac += add` sequencialmente, com truncamento inteiro; a formula coincide entre alvo e W2PP em sete vetores e permanece isolada do coordenador. RegenHP e dano fisico nao aparecem nesse branch.
- O coordenador de score ordena os blocos auditados e projeta AC/MaxHp/Damage antes de Soul/Kibita, incluindo Type=11, Type=9, Type=15, Type=5, Type=6 e Type=12 entre Type=16 e Haste; Critical, Attack/Run, equipamento, multiplier e buffs pendentes continuam separados. Quatro vetores passaram e o coordenador permanece fora do listener.
- Builds/testes locais nao validam o cliente real nem autorizam deploy, commit ou push.
