# Matriz de rastreabilidade do port

> Snapshot historico de 18/09/2026. A politica de referencias 7.59/7559 e
> 7.60/7600 nao e mais vigente. Durante o reinicio, revalidar cada ficha contra
> a referencia comunitaria 7.69 e registrar os hashes/releases efetivamente
> examinados; nao renomear uma evidencia antiga como 7.69 sem nova verificacao.

**Baseline historico:** 18/09/2026; linhas novas 7.69 verificadas em 19/09/2026  
**Fonte de verdade:** esta matriz registra o estado atual das rotas; o detalhe de cada metodo deve ser mantido no formato da ficha do `PORT_ARCHITECTURE_BASELINE.md`.  
**Regra:** `implementado` nao significa automaticamente `validado pelo cliente`.

## Recalculo de personagem no login — recortes isolados (21/09/2026)

`LegacyCurrentScoreMath` agora cobre, em metodos separados e ainda fora do
adapter/listener, o reset `BaseScore -> CurrentScore` com abilities de equipamento,
o estagio `Cur Special`, Soul normal e o override tardio Kibita, o seed de
abilities da secao `Cur HP/MP`, os clamps escalares finais de HP/MP, regen, Magic
e Critical, os modificadores de resistencia por affects, o fechamento de dano e
os clamps finais das quatro resistencias, a composicao final de AttackRun e o
affect `Type=2` Haste (soma de Run e flag `RSV_HASTE=0x20`). A comparacao do fechamento de dano esta na ficha
[`character-login-final-damage.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-damage.manifest.md);
o bloco final coincide com W2PP para as mesmas entradas, mas o W2PP possui adicoes
anteriores por classe ausentes do alvo 7.69. As comparacoes de Soul estao nas fichas
[`character-login-soul.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-soul.manifest.md)
e [`character-login-kibita-soul.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-kibita-soul.manifest.md);
o override Kibita coincide na condicao, mas diverge de W2PP no delta de MaxMP.
A comparacao do seed (incluindo regras faltantes de `BASE_GetItemAbility`) esta na ficha
[`character-login-ability-stage.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-ability-stage.manifest.md).
O alvo 7.69 limita Special1 a 255 (W2PP usa 200); para Soul, 7.69 aplica Int/Con
escalados por `ClassMaster`/Soul a cada `Affect.Type=29`, enquanto W2PP calcula
temporarios mas aplica os deltas dos atributos de equipamento. Os 31 casos golden
testam os multiplicadores-alvo, repeticao de affect, truncamento float, summon e
preservacao de HP/MP atuais.
O seed de abilities calcula SaveMana/Magic/Run/AttackSpeed/RegenHP/RegenMP/Critical
antes dos bonus e caps posteriores; os vetores sao source-derived, nao runtime. As
formulas do bloco coincidem com W2PP. Os clamps finais comuns tambem coincidem,
exceto `MAX_DAMAGE_MG` (7.69: 190.000.000; W2PP: 1.000.000.000); os casos estao na
ficha [`character-login-final-clamps.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-clamps.manifest.md).
Quatorze vetores cobrem as mutacoes de resistencia para `Affect.Type` 3/8/16/25;
7.69 inclui Holy em `Type=25` e usa adicoes de transformacao que divergem do W2PP.
Detalhes e comparacao executavel estao na ficha
[`character-login-resistance-affects.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-resistance-affects.manifest.md).
O fechamento final AttackRun coincide com W2PP para os mesmos valores
acumulados; oito vetores cobrem bonus, divisao por Dexterity, caps, floor de
montaria condicionado por `face` em cache e empacotamento. `LegacyMountRunRules`
resolve separadamente o gate por item/efeito e os pisos de 30 mounts comuns e 15
temporarios. Os seis primeiros mounts comuns usam valores 4/5 no alvo 7.69, mas
6 no W2PP. O indice 2390 e rejeitado: o limite inclusivo nas duas sources tenta
ler uma linha alem das 30 disponiveis. A ficha e
[`character-login-mount-speed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-mount-speed.manifest.md).
O recorte `Affect.Type=16` tambem retorna os bonus finais de Attack/Run na ordem
da tabela: a ultima transformacao valida substitui as anteriores. A tabela-base
coincide com W2PP, mas `AttAdd` diverge para Bear, Titan e Eden. A fonte-alvo e
`Servidor/Source/Code/Basedef.cpp`, incluida pelo projeto TMSrv; a copia local
TMSrv nao compilada diverge no Titan. A ficha
[`character-login-transformation-speed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-speed.manifest.md)
registra os vetores e limites. O recorte nao aplica outros efeitos/mutacoes da
transformacao nem esta integrado ao fluxo. Demais bonus de buffs e efeitos de
montaria seguem fora desses recortes, detalhados em
[`character-login-final-speed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-final-speed.manifest.md).
O recorte de dano `Affect.Type=16` retorna em separado o Damage aditivo (+10 por
Wolf valido) e a soma de `multi - 100` que alimenta `DAMAGEMULTI`; o multiplicador
final nao e aplicado por esse metodo. A aritmetica usa `Affect.Level` ushort sem
clamp. W2PP diverge no `DamAdd` de Wolf com skill e Eden (10 contra 25/20 no
alvo). Ver
[`character-login-transformation-damage.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-damage.manifest.md).
O recorte `Affect.Type=16` de AC escala o valor ja acumulado em cada affect,
trunca na divisao por 100 e adiciona +5 por Wolf valido; portanto, trocar a ordem
das formas muda o resultado. A base Min/Max coincide com W2PP, mas o `AcAdd`
diverge para Wolf com skill, Titan e Eden. Dezenove vetores cobrem as duas fontes;
a ficha [`character-login-transformation-ac.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-ac.manifest.md)
detalha o estagio, que nao esta integrado ao login.
O recorte MaxHp `Affect.Type=16` tambem aplica cada faixa sobre o MaxHp ja
atualizado, sem clamp local. Base HP e aritmetica coincidem com W2PP; `HpAdd`
diverge para Wolf e Bear com skill. O `RegAdd` soma nas quatro resistencias e ja
e coberto pelo recorte de resistencia Type=16, nao sendo duplicado aqui. Vinte
vetores e a ficha
[`character-login-transformation-maxhp.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-maxhp.manifest.md)
registram as duas fontes.
O recorte de Critical/equipamento `Affect.Type=16` acumula o bonus de Critical
por affect valido e retorna uma copia do item de cabeca com indice 22/23/24/25/32
e efeito `EF_SANC`. A mutacao do item e a formula de sancao coincidem com W2PP,
mas o Critical diverge para Astaroth com skill (target 0, W2PP +5) e Eden (target
+10, W2PP +6). Vinte vetores, incluindo repeticao e gates, estao na ficha
[`character-login-transformation-critical-equipment.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-critical-equipment.manifest.md).
O estagio de composicao [`character-login-transformation-composition.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-transformation-composition.manifest.md)
reexecuta os resultados ja auditados para o mesmo snapshot de affects: Attack/Run,
dano, AC, MaxHp, Critical, equipamento e resistencia. Sao oito vetores com
comparacao W2PP; a resistencia continua vindo do estagio geral Type=3/8/16/25,
sem somar `RegAdd` duas vezes. O resultado e um snapshot puro, fora do listener,
do encoder ativo e do E2E do cliente.
O recorte [`character-login-magic-shield.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-magic-shield.manifest.md)
fecha o `Affect.Type=11` em seis vetores: o alvo soma `Level / 5 + Value` ao
AC e o W2PP soma `Level / 3 + Value`. O metodo retorna somente o delta, sem
clamp ou mutacao do MOB, e permanece fora do login/listener.
O recorte [`character-login-magic-weapon.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-magic-weapon.manifest.md)
fecha o `Affect.Type=9` em sete vetores: Damage e o multiplicador usam a mesma
formula no alvo/W2PP, mas somente o W2PP soma `Magic +5` por affect. O C# projeta
o Damage antes de Soul e mantem o delta de Magic como divergencia explicita.
O recorte [`character-login-athena-touch.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-athena-touch.manifest.md)
fecha o `Affect.Type=15` em sete vetores: todos os Special usam `Level / 10 + Value`,
mas o alvo limita Special1/2/3/4 em 255 e o W2PP limita Special1 em 200. O C#
projeta os quatro Special depois de Type=9 e antes de Soul.
O recorte [`character-login-dexterity.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-dexterity.manifest.md)
fecha o `Affect.Type=6` em sete vetores: alvo e W2PP usam o mesmo fator `float`,
narrowing para `short` e mutacao sequencial. O C# projeta DEX depois de Type=15.
O recorte [`character-login-fanatism.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-fanatism.manifest.md)
fecha o `Affect.Type=5` em sete vetores: alvo e W2PP usam o mesmo fator `float`,
narrowing para `short` e mutacao sequencial. O C# projeta Fanatismo antes de Type=6.
O recorte [`character-login-holy-touch.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-holy-touch.manifest.md)
fecha o `Affect.Type=1` em sete vetores: alvo e W2PP subtraem `Value` do Run,
30 do Att e, quando `head index > 50`, 40 da Int por affect. O C# retorna deltas
puros; o empacotamento final de `AttackRun` continua fora deste recorte.
O recorte [`character-login-samaritan.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-samaritan.manifest.md)
fecha o `Affect.Type=14` (Possuido) em sete vetores. O alvo usa `MaxHp += value * 2` e
`Con += value`; W2PP usa `MaxHp += value * 22` e `Con += (Con + value) * 125 / 100`.
O C# porta apenas o alvo e mantém a divergencia comparativa explicita, sem integrar
o estagio ao coordenador.
O recorte [`character-login-assault.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-assault.manifest.md)
fecha o `Affect.Type=13` em sete vetores. O alvo soma somente `Level / 10 + Value`
ao multiplicador de dano; W2PP tambem altera Damage em 15%, aplica
`MAX_DAMAGE=1000000000` e reduz MaxHp para 90%. O C# porta apenas a contribuicao
do alvo e mantem Damage/MaxHp inalterados, sem integrar o estagio ao coordenador.
O recorte [`character-login-possessed.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-possessed.manifest.md)
fecha o `Affect.Type=24` (Samaritano) em sete vetores. Alvo e W2PP usam `add = Ac / 4 + Value`
e `Ac += add` sequencialmente, com truncamento inteiro; a diferenca de comentario
(`Possuido` no alvo e `Samaritano` no W2PP) nao representa divergencia de comportamento.
O `SkillData.csv` 7.69 confirma `Possuido -> Type=14` e `Samaritano -> Type=24`;
RegenHP e dano fisico nao sao alterados nesses branches. O C# porta a formula
comum como etapa pura, sem integrar o coordenador.
O recorte [`character-login-armor-class-reduction.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-armor-class-reduction.manifest.md)
fecha o `Affect.Type=12` em sete vetores: alvo e W2PP usam o mesmo fator `float`,
truncamento para `int` e mutacao sequencial. O C# projeta AC depois de Type=6.
O coordenador [`character-login-score-coordinator.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-score-coordinator.manifest.md)
ordena os blocos auditados em snapshots: base/equipamento, Special, Type=16,
Type=11, Type=9, Type=15, Type=5, Type=6, Type=12, Haste, Soul e Kibita. Ele projeta AC/MaxHp/Damage entre etapas para preservar a
ordem observada na fonte; Critical, Attack/Run, equipamento, multiplier e buffs
nao portados continuam fora do commit. Quatro vetores cobrem Soul normal,
Kibita, resistencia clampada e gate de classe, sem integracao no login.
O bloco Haste coincide com W2PP e esta coberto por sete vetores, mas retorna
somente o delta/mask: nao muta MOB nem foi integrado ao fluxo. Ver
[`character-login-haste-affect.manifest.md`](../tests/WydCdk.Engine.Tests/Fixtures/V769/character-login-haste-affect.manifest.md).
Outros derivados/mutacoes de buffs e montarias continuam pendentes.
O helper comum 7.69 declara 18 slots, mas
TMSrv/W2PP/C# trabalham com 16; os indices 16/17 ficam fora deste recorte.

**Backlog:** prioridades, critérios de aceite e fontes externas candidatas ficam em
[`BACKLOG.md`](../../BACKLOG.md); esta matriz registra somente a rastreabilidade
por símbolo, wire, estado, teste e nível.

## Niveis

|  Nivel |               Significado                 |
| ------ | ----------------------------------------- |
|   C0   | nao inventariado                          |
|   C1   | referencia/simbolo legado localizado      |
|   C2   | parser ou wire parcial                    |
|   C3   | regra implementada e teste deterministico |
|   C4   | fluxo local/rede executado                |
|   C5   | cliente real validado                     |
|   C6   | build publicada e servico remoto validado |
|   C7   | aceite funcional com evidencia completa   |

## Auditoria comparativa classe a classe

O estagio vigente percorre cada classe ativa do C# contra as duas fontes locais:
7.69 como contrato-alvo e `Server/W2PP` como comparador obrigatorio. Cada lote
registra somente classes cujo codigo foi lido nas tres arvores; ausencia de
equivalente e uma conclusao valida, mas nunca implicita.

Para a auditoria, classificar cada tipo C# como contrato wire, modelo/regra de
dominio, caso de uso/estado de sessao, adapter de storage/dados, host/operacao ou
ferramenta. Golden byte-a-byte se aplica a contratos wire e adapters binarios;
regras de dominio recebem fixtures comportamentais, casos de uso recebem cenarios
de transicao/falha, e infraestrutura recebe testes de contrato/integracao isolada.
Um nome agrupado na matriz so conta como coberto depois de cada tipo do grupo ter
responsabilidade, decisao e evidencia identificadas.

### Ficha de auditoria: `MSG_CNFCharacterLogin` 7.69

O wire do cliente/header comum DBSrv e um DTO proprio: 1728 bytes no total,
`STRUCT_MOB` 1040, `ShortSkill` no offset absoluto 1062, `EXT1` 1080 e `EXT2`
1368. `CharacterLoginConfirmationV769` e o golden Win32 cobrem esse envelope.
`W2ppCharacterLoginV1Adapter` agora compoe os campos W2PP ja mapeados no payload
e um teste valida o frame inteiro; ambos continuam fora do listener. Esse e o contrato selecionado para o cliente 7.69:
o handler recebe `MSG_CNFCharacterLogin`, copia os 1040 bytes do MOB e usa os
offsets seguintes conforme esse header. O DBSrv comum tambem declara o mesmo layout.

A source do TMSrv privado 7.69 contradiz esse contrato: seu header local mede MOB
816/16 equipamentos e `MSG_CNFCharacterLogin` 2640 bytes; `DataServerPackageHandler`
envia `sizeof(MSG_CNFCharacterLogin)`. Apesar de declarar uma forma
`MSG_CNFClientCharacterLogin` de 1832 bytes, esse handler nao envia essa forma.
W2PP, por sua vez, realmente relaya `MSG_CNFClientCharacterLogin` de 1832 bytes a
partir do pacote interno de 2648. Nenhum desses tamanhos substitui o contrato 1728
que o cliente 7.69 declara e consome. Nao copiar o wire privado inconsistente do
TMSrv; a implementacao C# alvo usara o DTO cliente 1728 quando a integracao do
fluxo estiver completa.

| Bloco/campos | Comparacao W2PP -> cliente 7.69 | Decisao atual |
|---|---|---|
| `PosX/PosY`, `Slot`, `ClientID`, `Weather` | W2PP inicia a posicao a partir de SPX/SPY, mas escolhe depois um spawn vivo (cidade/guilda/treinamento e grid livre) e envia esse resultado. O client 7.69 copia PosX/PosY para `MOB.HomeTownX/Y`; portanto nao sao simplesmente o SPX/SPY persistido. C# ja tem `ResolveCharacterLoginPosition`; o mapper devera receber essa posicao separada. Slot e ID da sessao sao escalares; W2PP envia `CurrentWeather`, enquanto o host C# ainda nao tem clima. | Manter spawn como dado do caso de uso, slot/ID explicitos e Weather=0 ate portar clima; nao derivar PosX/Y apenas do blob. |
| Prefixo de `MOB` | `MobName`, Clan, Merchant, Guild, Class e Coin mapeiam por semantica. O client tem `Rsv` byte +21; W2PP tem ushort +22, portanto o adapter preserva o byte baixo e rejeita flags no byte alto. W2PP Quest byte +24 alarga para ushort +22. Exp permanece +32; SPX/SPY +40/+42 sao as coordenadas salvas, mas a resposta recebe o spawn vivo final separado. | Implementado em `W2ppCharacterMobV1Adapter`; spawn e parametro obrigatorio e deve ser resolvido pelo caso de uso. |
| `BaseScore` / `CurrentScore` | Ambos comecam em +44/+92 e medem 48 bytes. W2PP usa Level int e Merchant/AttackRun/Direction/ChaosRate; client usa Level short, Reserved/AttackRun e padding. Level e convertido checked, Reserved/padding sao zero; specials preservam os 16 bits. | Implementado no adapter; overflow de Level rejeitado e bytes comparados ao golden 7.69 compilado. |
| `Equip` / `Carry` | Equip comeca em +140 nos dois layouts, mas W2PP tem 16 itens e o client 18; copiar 0..15 e manter 16/17 em extensao versionada. Carry e 64 itens em ambos, deslocado de +268 para +284. | `CharacterMobV769`/`ClientEquipmentStateV769` cobrem os 18 itens; o `WorldHub` mantem a extensao separada, projeta os indices novos em CreateMob/UpdateEquip e muta slots 16/17 sem aumentar o blob W2PP. MariaDB usa `wyd_client_equipment_769` (migration 004); backend legado sem essa tabela continua com 16 + dois vazios. |
| skills e campos finais do MOB | W2PP `STRUCT_MOB` tem um `LearnedSkill` de 32 bits em +780; o client 7.69 tem dois uints em +796. O word 0 e copiado bit a bit. O client `IsValidSkill` consulta word 1 para skills 200..246; o TMSrv 7.69 tambem serializa `MOB.Learned[1]`. Tanto W2PP quanto 7.69 mantem `MOBEXTRA.SecLearnedSkill` separado de `MOB.Learned[1]`, com usos distintos no TMSrv; nao ha conversao entre eles. Como W2PP nao persiste a extensao de skills 7.69, o adapter inicializa word 1 em zero. `SkillBar[4]` mapeia a `MOB.ShortSkill[4]`. Magic uint32->byte, RegenHP/MP ushort->byte e bonus ushort->short sao checked; Critical, SaveMana, GuildLevel e Resist[4] mantem a representacao. Dummy e padding target sao zerados. | Mapeamento W2PP executado e testado: word 0 preservado, word 1 zerado como extensao indisponivel na origem. Nao copiar `SecLearnedSkill`; se as skills 200..246 forem implementadas, exigir estado canonico explicito. |
| nome/contadores/appearance | O client 7.69, ao entrar no Field, le chaos de `MobName[12]`, kills de `[13..15]` e termina o nome em `[12]`. W2PP `GetCreateMob` monta esse sufixo via `GetPKPoint`, `GetGuilty`, `GetCurKill` e `GetTotKill` a partir de `KILL_MARK` em Carry[63]. `CurrentKill/TotalKill` target tambem foram medidos em +1036/+1038. | Implementado para self-login: os 12 bytes do nome sao seguidos do sufixo e os mesmos valores preenchem os contadores explicitos. Chaos zera quando guilty valido, como no sender W2PP. |
| `EXT1.Data[0]` | Client le este int como FakeExp. W2PP `SendEtc` envia `extra.Hold`; TMSrv 7.69 envia o mesmo valor como `FakeExp`. | Mapeamento aprovado: `W2PP STRUCT_MOBEXTRA.Hold` (uint, +476) -> `EXT1.Data[0]` (int), com overflow rejeitado. `CharacterLoginExtensionsV769` e `W2ppCharacterLoginExtensionsV1Adapter` implementam somente esta parte, isolados. |
| `EXT1.Data[1..7]` e `EXT1.Affect[32]` | O handler de login do client so le Data[0], nao consome Affect nesse pacote. O `STRUCT_AFFECT` cliente e Type/Level/Value/Time (char/char/short/int); W2PP e Type/Value/Level/Time (byte/byte/ushort/uint). W2PP grava levels 500 e 2000, que nao cabem no Level char, logo copia crua ou truncamento corromperia semantica. O client declara `MSG_UpdateAffect` (`0x03B9`) como rota separada. | Data[1..7] e Affect ficam zero neste confirmation baseline; efeitos ativos usam adapter tipado e wire separado. |
| `UpdateAffectConfirmationV769` / `W2ppUpdateAffectV1Adapter` | cliente `Basedef.h::MSG_UpdateAffect` (`0x03B9`) | W2PP `MSG_SendAffect` (`0x03B9` com flags), `STRUCT_AFFECT` | Ambos medem frame 268/payload 256 com 32 entradas de 8 bytes, mas o cliente ordena `Type, Level, Value, Time` e W2PP `Type, Value, Level, Time`. O adapter troca os campos e rejeita `Level > 127` ou `Time > int.MaxValue`, em vez de truncar efeitos como 500/2000. | DTO + adapter + golden Win32 + teste byte a byte executados; relay integrado no login e em mutacoes com snapshot autoritativo; efeitos incompatíveis sao omitidos sem truncamento; E2E continua pendente |
| `EXT2` | 360 bytes contem Quest[12], LastConnectTime, SubClass[2], ItemPassWord, ItemPos, SendLevItem, AdminGuildItem e Dummy. O handler client de CharacterLogin nao le Ext2; `STRUCT_MOBEXTRA` W2PP nao e layout equivalente e inclui dados que nao devem ser vazados por copia bruta. | Ext2 zerado ate existir consumidor e mapeamento por campo; sem copiar MOBEXTRA para ele. |

O caminho 7.69 nao fecha um oracle runtime: o handler DBSrv deixa Ext1/Ext2 sem
copia e sem inicializar o struct local; os headers privados do TMSrv ainda usam
MOB816/16 equipamentos e tamanhos diferentes do cliente/DBSrv comum. Essa
divergencia e evidência de defeito/incompletude da source, nao uma regra a
reproduzir. W2PP tambem distingue seu pacote interno de 2648 bytes do relay real
de 1832 bytes; o encoder C# legado envia o interno. O DTO/adapter do MOB e seu
golden Win32 de 1040 bytes agora existem, mas continuam isolados. A extensao
7.69 `LearnedSkill[1]` e explicitamente zerada na projecao W2PP, pois a origem nao
tem esse word e `MOBEXTRA.SecLearnedSkill` e distinto. `W2ppCharacterLoginV1Adapter`
compoe MOB, short skills, spawn, slot/ID/clima e FakeExp no confirmation 7.69; um
teste cobre os blocos, alinhamento e frame codificado. `LegacyHpMpMath` agora
porta isoladamente `BASE_GetHpMp`, com vetores por classe. Para integrar, ainda e
preciso portar/validar os demais recalculos e mutacoes que TMSrv aplica ao MOB,
migrar coordenadamente os outros wires de login/selecao e validar captura/runtime
e E2E. Affect requer wire separado; EXT2 permanece zerado.

| Classe C# | Referencia 7.69 | Comparador W2PP | Decisao | Estado |
|---|---|---|---|---|
| `PacketHeader` | `Servidor/Source/Code/Basedef.h::_MSG` | `Source/Code/Basedef.h::_MSG` | manter o header de 12 bytes (`Size`, `KeyWord`, `CheckSum`, `Type`, `ID`, `ClientTick`). | auditado; teste de header executado |
| `LegacyFrameCodec` | `Servidor/Source/Code/CPSock.cpp` | `Source/Code/CPSock.cpp` | manter `INITCODE 0x1F11F311`, cifra, checksum e limite de 8192 bytes comuns. | auditado; testes de cifra/checksum executados |
| `LegacyFrameStream` / `DecodedFrame` | `Servidor/Source/Code/CPSock.cpp` | `Source/Code/CPSock.h/.cpp` | manter o acumulador de stream; `DecodedFrame` e wrapper .NET do header, payload e checksum, sem objeto C++ equivalente. | auditado; teste de fragmentacao executado |
| `SerializedNetworkStream` | `CPSock::AddMessage/SendMessageA` em `Servidor/Source/Code/CPSock.cpp` | mesmos metodos em `Source/Code/CPSock.cpp` | **Adaptador arquitetural, nao port 1:1.** Nas duas sources, `AddMessage` grava frames cifrados num buffer por socket e atualiza `Size`/`KeyWord`/`CheckSum`/`ClientTick`; `SendMessageA` conserva `nSentPosition` e compacta o restante com `RefreshSendBuffer` apos envio parcial. O C# envolve cada `NetworkStream` uma vez e serializa `Write`/`WriteAsync` via `SemaphoreSlim`; o `Program` usa esse wrapper nas respostas e broadcasts. Nao replica batching, buffer fixo nem `nSentPosition`; os metodos C++ observados tambem nao mostram lock proprio. Manter a adaptacao para o modelo concorrente .NET, sem copiar a fila legada; a integridade de dois writes concorrentes foi provada em loopback: os blocos chegam completos em uma ordem ou na outra, sem intercalacao. | comparacao estatica concluida nas duas sources; chamadas TX encaminhadas pelo wrapper; teste concorrente de frames/ordem executado |
| `LegacyKeywordTable` | `Servidor/Source/Code/CPSock.cpp::pKeyWord[512]` | `Source/Code/CPSock.cpp::pKeyWord[512]` | manter tabela de 512 bytes e transformacao CPSock. | auditado; teste de tabela/cifra executado |
| `ClientTickPolicy` | `Servidor/Source/Code/Basedef.cpp` | `Source/Code/Basedef.cpp`, `TMSrv/ProcessClientMessage.cpp` | manter: `SKIPCHECKTICK` recebido do cliente e invalido. | auditado; teste de tick executado |
| `AccountLoginRequest` | `Basedef.h::MSG_AccountLogin`, `TMSrv/_MSG_AccountLogin.cpp` | `Basedef.h::MSG_AccountLogin`, `TMSrv/_MSG_AccountLogin.cpp` | manter wire de 116 bytes e campos. A release propria exige `ClientVersion == 7670`: a 7.69 deixa a comparacao comentada e W2PP a exige para seu valor legado; a constante 7.670 e politica explicita desta migracao, nao valor copiado de nenhuma das duas. O parser permanece neutro e `ClientReleasePolicy` aplica a admissao antes da autenticacao. | auditado; parser e politica de aceitar 7670/rejeitar demais testados |
| `ClientReleasePolicy` | `APP_VERSION=1059`; comparacao de `ClientVersion` comentada em `TMSrv/_MSG_AccountLogin.cpp` | `APP_VERSION=7640`; comparacao de versao ativa no mesmo handler, exceto no ramo `_PACKET_DEBUG` | classe de politica sem equivalente 1:1; manter o requisito do produto `7670` no host, antes de autenticar, sem copiar os valores das fontes. `AccountLoginRequest` continua aceitando sintaticamente outras versoes para separar parse de admissao. | diferenca comparada nas duas fontes; `Program` aplica antes da autenticacao; teste de aceitar 7670/rejeitar demais existente |
| `AccountLoginConfirmation` | cliente `Projects/TMProject/Basedef.h::MSG_CNFAccountLogin`; servidor comum/DBSrv `Servidor/Source/Code/Basedef.h::MSG_DBCNFAccountLogin`; encaminhamento em `TMSrv/DataServerPackageHandler.cpp` | `Basedef.h::MSG_DBCNFAccountLogin`, `TMSrv/ProcessDBMessage.cpp` | **Divergencia bloqueadora medida:** cliente 7.69 = 1928 bytes (`SecretCode` +12, `SelChar` +32, `Cargo` +936, `Coin` +1896, `AccountName` +1900, `SSN1` +1916). Header comum/DBSrv = 1992; TMSrv local = 1928, mas com selecao antiga de 840 e Cargo +872 (128 itens), logo mesmo tamanho nao significa mesmo contrato. W2PP = 2008; C# legado emitia 2000 e serializava campos W2PP extras. | encoder 7.69 integrado ao listener; autenticacao, sessao e wire de falha preservados; E2E cliente ainda pendente |
| `AccountLoginConfirmationV769` / `W2ppAccountLoginV1Adapter` | `Projects/TMProject/Basedef.h::MSG_CNFAccountLogin`; leitura em `TMSelectServerScene.cpp`; resposta DB em `DBSrv/CFileDB.cpp` | `Basedef.h::MSG_DBCNFAccountLogin`, `TMSrv/ProcessDBMessage.cpp` | DTO cliente separado: payload 1916/frame 1928; offsets relativos ao payload SecretCode +0, padding +16..19, selecao +20, cargo +924, Coin +1884, AccountName +1888, SSN1 +1904, SSN2 +1908, padding final +1912..15. Golden MSVC Win32 usa sentinelas sinteticas. Adapter copia selecao/cargo 0..119/Coin/nome; rejeita carga nao vazia 120..127; nao equipara `HashKeyTable` a `SecretCode` e projeta SecretCode/SSNs como zero, conforme o pacote alvo zero-inicializado e os campos nao atribuidos pelo handler DBSrv. `QuestDiaria`, `Unknown28`, Keys e bloqueio ficam fora do wire cliente. A medicao e do header cliente; DBSrv comum 7.69 e TMSrv privado ainda divergem entre si. | DTO + adapter + golden byte a byte + teste de frame 1928 integrado ao listener; E2E cliente pendente |
| `LegacyDailyQuest` | `STRUCT_QUEST` existe no servidor 7.69, mas nao integra `MSG_CNFAccountLogin` do cliente/servidor raiz | mesmos campos em `Basedef.h::STRUCT_QUEST`, incluidos em `MSG_DBCNFAccountLogin` | manter o DTO de 52 bytes somente para persistencia/fluxos que comprovem esse formato; nao serializar os 52 bytes no login 7.69 sem um wire separado. No header raiz 7.69, a mensagem de login termina em SSN1/SSN2; a copia local de `TMSrv` nao e o contrato compilado pelo projeto. | diferenca estatica confirmada; uso de quest fora do login ainda precisa de rastreabilidade |
| `AccountSecureRequest` | `Basedef.h::MSG_AccountSecure`, `TMSrv/_MSG_AccountSecure.cpp`, `TMSrv/CFileDB.cpp` | mesmos simbolos | manter wire de 32 bytes e PIN de 6 bytes; `Unknown[10]` nao participa da regra nas fontes. | auditado; parser/coordinator testados |
| `AccountSecureSignal` | `TMSrv/DataServerPackageHandler.cpp::_MSG_AccountSecure/_MSG_AccountSecureFail` | `TMSrv/ProcessDBMessage.cpp::_MSG_AccountSecure/_MSG_AccountSecureFail` | manter os sinais sem payload, tipos `0x0FDE`/`0x0FDF` e `ESCENE_FIELD`; C# elimina o hop DB. | auditado; encoder/coordinator testados |
| `AccountLoginCoordinator` / `AccountLoginOutcome` / `AccountLoginFailureNotice` | `TMSrv/_MSG_AccountLogin.cpp` mais resposta DB em `CFileDB.cpp`/`DataServerPackageHandler.cpp` | `TMSrv/_MSG_AccountLogin.cpp` mais `DBSrv/CFileDB.cpp`/`ProcessDBMessage.cpp` | sao coordenacao e resultados C# sem classe legada unica: manter autenticacao antes de `USER_SELCHAR` e fechar em falha; a versao obrigatoria 7670 e aplicada pelo host via `ClientReleasePolicy`, antes da autenticacao. | auditoria estatica concluida; parser/politica testados; E2E 7.670 pendente |
| `AccountSecureCoordinator` / `AccountSecureStatus` / `AccountSecureOutcome` | `TMSrv/_MSG_AccountSecure.cpp`, `TMSrv/CFileDB.cpp::_MSG_AccountSecure` e sinais DB | mesmos handlers em TMSrv, `DBSrv/CFileDB.cpp` e `ProcessDBMessage.cpp` | preservar a ordem: troca sem PIN validado fica silenciosa; PIN ainda nao definido aceita configuracao; verificacao compara 6 bytes; troca exige sessao validada. `SecureVerified` e estado de sessao C#, sem objeto legado equivalente. | auditoria estatica concluida; fluxos e sinais testados |
| `LegacyItem` | `Basedef.h::STRUCT_ITEM` | `Basedef.h::STRUCT_ITEM` | manter os 8 bytes (indice + tres pares efeito/valor). E tipo compartilhado de wire, nao uma regra de inventario. | auditado na estrutura; testes de serializacao existentes |
| `LegacyScore` | cliente `Projects/TMProject/Basedef.h::STRUCT_SCORE`; servidor comum/DBSrv `Servidor/Source/Code/Basedef.h::STRUCT_SCORE` | `Basedef.h::STRUCT_SCORE` | **Mesmos 48 bytes, sem contrato semantico comum.** Cliente: `Level` short em +0, `Ac` +4, `Damage` +8, `Reserved` +12, `AttackRun` +13, alinhamento +14..15, demais valores +16..47. Header comum 7.69 usa unions/bitfields nos bytes +12/+13; TMSrv 7.69/W2PP/C# usam `Level` int e `Merchant/AttackRun/Direction/ChaosRate`. O C# ativo permanece DTO W2PP; `ClientScoreV769` e separado. | offsets medidos nos quatro headers; score cliente coberto no golden `STRUCT_SELCHAR`; golden W2PP standalone pendente |
| `LegacyCharacterSelection` / `LegacyCharacterSlot` | cliente `Projects/TMProject/Basedef.h::STRUCT_SELCHAR`, `TMHuman.cpp` e `TMFieldScene.cpp` (`NewSlot1/2`); servidor comum/DBSrv `Servidor/Source/Code/Basedef.h::STRUCT_SELCHAR` e `DBGetSelChar` | `Basedef.h::STRUCT_SELCHAR` e DB equivalente | **Divergencia bloqueadora medida:** cliente e DBSrv comum 7.69 usam coordenadas unsigned, Score[4], Equip[4][18], tamanho 904 e Equip no +272; TMSrv 7.69/W2PP/C# usam 16 slots e tamanho 840. W2PP `DBGetSelChar` contem typo que grava SPY em SPX; 7.69 projeta SPX/SPY corretamente, como o reader C# atual. O golden 7.69 valida os 904 bytes; regras de gameplay/persistencia/relay de 16/17 continuam abertas. | reader e projecao antigos permanecem W2PP; serializer versionado e golden 7.69 testados em classes separadas |
| `CharacterSelectionV769` / `ClientScoreV769` / confirmations New/Delete / `W2ppCharacterSelectionV1Adapter` | `Projects/TMProject/Basedef.h::STRUCT_SELCHAR`, `STRUCT_SCORE`, `MSG_CNFNewCharacter`, `MSG_CNFDeleteCharacter`; `DBSrv/CFileDB.cpp::DBGetSelChar` | `LegacyCharacterSelection`/`LegacyScore` C# que representam W2PP | DTO wire 7.69: selection 904 bytes com offsets Score +80, Equip +272, Guild +848, Coin +856 e Exp +872; golden Win32 sintetico cobre todos os campos e 18 equipamentos. New/Delete acrescentam 4 bytes x86 apos MSG_STANDARD: SelChar +16, frames 920. Adapter preserva bits das coordenadas, valida Level int->short, mantem AttackRun/atributos comuns, zera Reserved, copia 16 itens e deixa 16/17 vazios; Merchant/Direction/ChaosRate sao descartados por nao terem equivalente 7.69. Nomes so sao projetados se ASCII, sem NUL e <=16 bytes. A confirmacao de AccountLogin e os sucessos de New/Delete estao ligados ao listener; E2E cliente permanece pendente. | MSVC Win32 golden byte a byte; testes de frames New/Delete 920 e projecao passaram |
| `CreateCharacterRequest` | `Basedef.h::MSG_CreateCharacter`, `TMSrv/_MSG_CreateCharacter.cpp` | mesmos simbolos | manter wire de 36 bytes e validacao de nome/estado; C# substitui o hop `USER_WAITDB` por `USER_CHARWAIT` enquanto persiste no proprio coordinator. | auditado; parser/coordinator e fixture executados |
| `NewCharacterConfirmation` | `Projects/TMProject/Basedef.h::MSG_CNFNewCharacter` / `STRUCT_SELCHAR` 7.69; sender DB em `DBSrv/CFileDB.cpp` e relay em `TMSrv/DataServerPackageHandler.cpp` | `Basedef.h::MSG_CNFNewCharacter` / `STRUCT_SELCHAR` W2PP | cliente/DBSrv comum 7.69 medem 920 bytes, `SelChar` no offset 16. O padding x86 torna incorreta a soma simples 12+904=916. TMSrv 7.69/W2PP medem 856 (`SelChar` +16, 16 slots); o encoder C# legado ativo emite 852 com `SelChar` +12. `NewCharacterConfirmationV769` serializa a estrutura medida e o listener usa o frame alvo no sucesso. | probe Win32/golden selection e frame 920 testados; falha/E2E pendentes |
| `NewCharacterFailSignal` | `TMSrv/_MSG_CreateCharacter.cpp`, `TMSrv/DataServerPackageHandler.cpp` | `TMSrv/_MSG_CreateCharacter.cpp`, `TMSrv/ProcessDBMessage.cpp` | manter sinal vazio `0x011A`; C# responde diretamente sem hop DB. | auditado; encoder/coordinator testados |
| `DeleteCharacterRequest` | `Basedef.h::MSG_DeleteCharacter`, `TMSrv/_MSG_DeleteCharacter.cpp` | mesmos simbolos | manter wire de 44 bytes, nome e senha; C# preserva a identidade da sessao e nao confia no nome para selecionar o slot. | auditado; parser/coordinator e fixture executados |
| `DeleteCharacterConfirmation` | `Projects/TMProject/Basedef.h::MSG_CNFDeleteCharacter` / `STRUCT_SELCHAR` 7.69; sender DB e relay TMSrv | `Basedef.h::MSG_CNFDeleteCharacter` / `STRUCT_SELCHAR` W2PP | Mesmo layout medido de NewCharacter: cliente/DBSrv comum 920 bytes com `SelChar` +16; TMSrv/W2PP 856 com 16 slots; C# ativo 852 com selecao +12. `DeleteCharacterConfirmationV769` usa o envelope compartilhado de 4 bytes e o listener usa o frame alvo no sucesso. | probe Win32 static_assert e fixture do corpo; teste de frame passou; falha/E2E pendentes |
| `DeleteCharacterFailSignal` | `Basedef.h::_MSG_DeleteCharacterFail`, handler DB de delete | mesmos simbolos | manter sinal vazio `0x011B`; C# responde diretamente sem hop DB. | auditado; encoder/coordinator testados |
| `CharacterLoginRequest` | cliente `Projects/TMProject/Basedef.h::MSG_CharacterLogin` e `TMSelectCharScene.cpp`; servidor `Basedef.h`/`TMSrv/_MSG_CharacterLogin.cpp` | `Basedef.h::MSG_CharacterLogin`, `TMSrv/_MSG_CharacterLogin.cpp` | o cliente 7.69 declara 36 bytes (`Header + Slot + Force + SecretCode[16]`) e envia uma instancia zerada; os dois servidores declaram 20 bytes e o handler consome `Slot`/`Force`, encaminhando apenas `sizeof(MSG_CharacterLogin)` ao DB, sem usar `Force` nem o tail. C# aceita 20 ou 36 e ignora os 16 bytes finais; logo 36 nao e exclusivo do adaptador 7.60, mas o nome/semantica `SecretCode` do contrato 7.69 ainda nao esta modelado. Nao rejeitar a forma 7.69; se a semantica mudar, tratar o campo explicitamente. | comparacao estatica cliente 7.69/servidor 7.69/W2PP concluida; parser testa formas 20 e 36 com tail zero; captura runtime 7.670 pendente |
| `LegacyHpMpMath` / `BASE_GetHpMp` | `Servidor/Source/Code/Basedef.cpp` e `TMSrv/DataServerPackageHandler.cpp`; `TMSrv.vcxproj` compila `..\\Basedef.cpp`, nao a copia local `TMSrv/Basedef.cpp` | `W2PP/Source/Code/Basedef.cpp::BASE_GetHpMp` | Corpo da rotina e incrementos por classe coincidem; `BaseSIDCHM` diverge para FM e HT: o helper 7.69 selecionado usa FM `{5,10,5,5,60,65}` e HT `{8,9,13,6,70,55}`, enquanto W2PP usa FM `{5,8,5,5,60,65}` e HT `{8,9,13,6,75,60}`. O helper selecionado pelo projeto 7.69 usa os limites do header comum (`MAX_HP=400000`, `MAX_MP=100000`); W2PP usa `1000000000` para ambos. A copia TMSrv local nao compilada coincide com W2PP e diz `100000` para HP e MP. O calculo so atualiza BaseScore/CurrentScore.MaxHp/MaxMp e preserva HP/MP atuais. A incompatibilidade geral de ABI TMSrv/common impede chamar isto de oracle runtime. | `LegacyHpMpMath` isolado; 12 vetores fonte-derivados cobrem 4 classes x Mortal/Arch/Celestial, alem de caps/entrada invalida. Usa explicitamente as tabelas/limites do helper comum selecionado, sem adotar valores W2PP onde divergem. Nao ligado ao adapter/listener; demais mutacoes de login pendentes |
| `CharacterLogoutRequest` | `Basedef.h::_MSG_CharacterLogout`, `TMSrv/_MSG_CharacterLogout.cpp` | mesmos simbolos | manter `MSG_STANDARD` vazio; persistencia e transicao de sessao ficam no coordinator C#. | auditado; wire/transicoes testados |
| `CharacterLogoutConfirmation` | `Basedef.h::_MSG_CNFCharacterLogout`, `TMSrv/_MSG_CharacterLogout.cpp` | mesmos simbolos | manter sinal vazio `0x0116`, com ID da conexao como no legado. | auditado; wire/transicoes testados |
| `CharacterLoginCoordinator` / `CharacterLoginStatus` / `CharacterLoginOutcome` / `LegacyCharacterLoginData` | `TMSrv/_MSG_CharacterLogin.cpp`, `CFileDB.cpp::_MSG_DBCharacterLogin`, `DataServerPackageHandler.cpp` | `TMSrv/_MSG_CharacterLogin.cpp`, `DBSrv/CFileDB.cpp::_MSG_DBCharacterLogin`, `ProcessDBMessage.cpp` | mescla os dois processos legados; manter a validacao do slot antes da transicao para `USER_CHARWAIT` e a leitura do personagem. Em ambas as sources, slot fora da faixa envia a mensagem `_NN_SelectCharacter` antes de retornar; C# restaura `CharacterSelection`, mas nao envia resposta. Separar essa diferenca de mensagem das falhas posteriores do DB, cuja resposta deve ser rastreada por ramo. O C# aplica o marcador inicial de Kill Mark ao MOB, comportamento de inicializacao que o handler legado executa apos carregar o personagem. | comparacao estatica concluida; parser/coordinator testados; diferenca de mensagem para slot invalido registrada; cliente real pendente |
| `CreateCharacterCoordinator` / `CreateCharacterStatus` / `CreateCharacterOutcome` | `TMSrv/_MSG_CreateCharacter.cpp`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter` | mesmos simbolos/ordem em TMSrv e DBSrv | as duas sources validam slot/classe/PIN/nome/ocupacao, reservam o nome global, inicializam MOB/extra/affect/ShortSkill e gravam a conta; reserva anterior a falha de escrita tambem pode deixar artefato no legado. A diferenca e concorrencia: DBSrv despacha os handlers sincronamente no loop central; C# dispara `InspectConnectionAsync` por conexao. A reserva MariaDB e autocommit separado, enquanto o blob e slot sao revalidados em transacao serializavel; uma criacao concorrente que perde o mesmo slot deixa sua reserva sem personagem. O template C# tambem e lido depois da reserva; `TemplateUnavailable` deixa reserva orfa. | sequencia comparada nas duas sources; fixtures sequenciais existentes; corrida de mesmo slot/diferentes nomes e falha de template/escrita precisam de testes deterministas; nao equivaler a atomicidade do legado |
| `DeleteCharacterCoordinator` / `DeleteCharacterStatus` / `DeleteCharacterOutcome` | `TMSrv/_MSG_DeleteCharacter.cpp`, `DBSrv/CFileDB.cpp::_MSG_DBDeleteCharacter` e resposta DB | mesmos simbolos/ordem em TMSrv e DBSrv | ambas as sources comparam a senha, removem a reserva global do nome, limpam ShortSkill, chamam `BASE_ClearMob/BASE_ClearMobExtra`, gravam a conta e retornam selecao; os bloqueios por classe/level estao comentados. A remocao do nome precede `DBWriteAccount` no legado e o retorno da escrita e ignorado. C# File grava/limpa o blob antes de remover o arquivo do nome (ordem oposta); MariaDB atualiza blob e remove registro na mesma transacao, mas tambem limpa Affect, que os handlers C++ nao limpam neste caminho. | wire/refresh e defaults cobertos; divergencias estaticas de ordem/affect registradas; teste de falha entre etapas e politica de estado persistido pendentes |
| `LegacyCharacterName` | `Basedef.cpp::BASE_CheckValidString`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter`, `ConfigIni.cpp::ReadFilterName` | `Basedef.cpp::BASE_CheckValidString`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter` | lista reservada comum, faixa 4..15 e comandos/COMn/LPTn coincidem. A validacao C++ permite pares de bytes altos ao avancar dois bytes; o parser C# decodifica ASCII, converte esses bytes em `?` e os rejeita, portanto nao preserva nomes multibyte. 7.69 acrescenta filtro configuravel `FilterName/block.json` ausente no W2PP; o JSON gerado por default vem com `ATIVO=0`, logo o efeito padrao e inativo, mas C# nao tem o adapter de configuracao. | casos ASCII testados; diferenca multibyte e capacidade de filtro configuravel registradas; politica 7.670/config ativa e testes de byte pendentes |
| `LegacyCharacterTemplateStore` | `DBSrv/Server.cpp::ReadBaseMob`, `ConfigIni.cpp::ReadbaseMob`, `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter`; cliente `TMHuman.cpp` | `DBSrv/Server.cpp` le `BaseMob/TK|FM|BM|HT`; `DBSrv/CFileDB.cpp::_MSG_DBCreateCharacter` | 7.69 carrega `Config/baseMob.json`, mas a configuracao e o TMSrv local ainda declaram 16 equipamentos; o cliente, por outro lado, desenha os grids 16/17 como `NewSlot1/2`. Cliente/DBSrv `STRUCT_MOB` = 1040, Equip +140, Carry +284; TMSrv/W2PP = 816, Carry +268. C# le templates W2PP com stride 816; nao assumir zeros/defaults para as posicoes novas como regra final ate rastrear create, login, save e visualizacao. | divergencia de capacidade confirmada; fixture atual sintetica; mapa de inicializacao e elegibilidade dos slots 16/17 pendentes |
| `LegacyCharacterStorageDefaults` | `Basedef.cpp::BASE_ClearMob/BASE_ClearMobExtra`, `TMSrv/ProcessDBMessage.cpp` inicializacao de Kill Mark | mesmos metodos/etapa de login | manter defaults persistidos do slot vazio em 2112,2112 e `ClassMaster=MORTAL`, alem de Kill Mark inicial ausente; a rotina agrega trechos de pontos diferentes do legado. | auditoria estatica concluida; fixtures de selecao/delete testadas |
| `LegacyAccountSnapshot` | `DBSrv/CFileDB.cpp::DBReadAccount/DBGetSelChar`, `Basedef.h::STRUCT_ACCOUNTFILE` | mesmos simbolos em `DBSrv/CFileDB.cpp` e `Basedef.h` | **Sao formatos de storage diferentes, nao uma extensao simples de slots.** W2PP: `sizeof=7952`, Char +216, Cargo +3480, Coin +4504, `IsBlocked` +7944; o parser C# aceita minimo de 7945 bytes e le MOB816/16 equips/Cargo128. Header comum DBSrv 7.69: `sizeof=8792`, Char +216, Cargo +4376, Coin +5336, Donate +8640, extensao opaca de 8 bytes antes de TempKey +8652, `IsBlocked` +8784; MOB1040/18 equips/Cargo120. Nao adotar o blob cru 7.69 como modelo canonico nem deslocar campos por aritmetica; manter `W2ppAccountFileV1` isolado para compatibilidade/importacao e preferir storage C# versionado. | ABI `sizeof/offsetof` medida; offsets W2PP/7.69 diferem; adapter/importacao e golden sintetico de storage pendentes |
| `IAccountStore` / `ICharacterStore` / `IAccountSnapshotStore` / `LegacyFileAccountStore` / `MariaDbWorldCharacterStore` | `STRUCT_ACCOUNTFILE`, `DBSrv/CFileDB.cpp` e `DBGetSelChar` | `STRUCT_ACCOUNTFILE`, `DBSrv/CFileDB.cpp` e `DBGetSelChar` | contratos/adapters nao correspondem a classes C++ individuais. A persistencia C# atual usa layout W2PP versionado implicitamente (blob MariaDB de 7945 bytes; parser minimo 7945, embora `sizeof` nativo seja 7952), nao o arquivo bruto 7.69 de 8792 bytes. Isolar o formato atual como `W2ppAccountFileV1Adapter`; um importador 7.69 so sera criado se houver requisito de migracao e teste. Persistencia nova deve mapear entidade canonica via adapter, sem expor offsets no dominio. | adapters atuais auditados; versao do contrato precisa ficar explicita; golden de storage e concorrencia deterministica pendentes |
| `AccountAuthenticationStatus` / `AccountAuthenticationResult` / `DeleteCharacterStoreResult` / `CharacterPositionSaveResult` / `CharacterStateSaveResult` / `CharacterGuildSaveResult` / `CharacterShortSkillSaveResult` / `DonateBalanceSaveResult` | retornos booleanos/estados de `DBSrv/CFileDB.cpp` e handlers | mesmos retornos/handlers | tipos de resultado C# para coordenacao e adapters, sem DTO C++ homonimo; preservar a traducao para as confirmacoes/falhas wire, nao os numeros internos do adapter. | contratos locais mapeados; matriz de falhas por operacao pendente |
| `MariaDbConnectionOptions` / `MariaDbConnectionFactory` | sem equivalente: conexao MariaDB configurada pelo novo host | sem equivalente: conexao MariaDB configurada pelo novo host | manter fora do dominio legado; configura provider a partir de JSON, cria conexoes parametrizadas e nao deve expor credenciais em log/docs. | fluxo estatico auditado; provider/config de cada ambiente nao validado nesta auditoria |
| `IDonateBalanceStore` / `IDonatePixStore` / `IDonateCatalogStore` / `MariaDbDonateShopStore` | saldo/cadastro nas rotinas DB 7.69, import de catalogo comentado | saldo/import DB ativo, sem adapter C# equivalente | persistencia de saldo so continua se integrar a conta Cash do Site; catalogo/painel de compra in-game fica estacionado. Definir fonte de verdade, transacao e entrega no bau antes de reativar qualquer adapter comercial. | adapter analisado; schema ativo/atomicidade e contrato Site pendentes |
| `ServerWireLog` / `TeeTextWriter` | sem equivalente: logging de host novo | sem equivalente: logging de host novo | manter como observabilidade, nunca como parte do protocolo. Divergencia de seguranca: o redator atual nao inclui `DeleteCharacterRequest` (`0x0211`, contem `AccountPassword`) nem `AccountLoginConfirmation` (`0x010A`, inclui chaves e `BlockPassword`); ambos podem ter payload gravado em claro. Corrigir a classificacao antes de manter logs em ambiente com contas reais. | auditoria estatica; redacao incompleta confirmada; sem teste de segredo nos logs |
| `ServerStatusFilePublisher` | sem equivalente: health/status do projeto, fora do protocolo do servidor 7.69 | sem equivalente: health/status do projeto, fora do protocolo W2PP | manter o adapter separado: atualiza `servtest.htm` na inicializacao/cadencia de 60 s, preserva `0` jogadores e marca `-1` no shutdown cooperativo; timestamp fica em `.wyd-status`. A expiracao apos 120 s pertence ao watchdog PowerShell, nao ao processo C#. O watchdog agora consulta e grava `values[slot]`, mantendo slots independentes. | implementacao e teste local executados; script/live site ainda nao sao E2E |
| `ServerOptions` | sem equivalente: argumentos/configuracao do host C# | sem equivalente: argumentos/configuracao do host C# | manter como configuracao operacional, sem misturar com politica de wire/dominio. Validacao de paridade dos argumentos com deployment continua em checklist proprio. | parser auditado; cobertura integral de combinacoes pendente |
| `Program` / `MainForm` (`WydCdk.Server.ControlPanel`) | sem equivalente: UI WinForms nova | sem equivalente: UI WinForms nova | manter fora do core e tratar como host local. O core aceita `stop`/`shutdown` pela entrada padrao e executa o `finally`/status offline; o painel solicita essa parada e usa `Process.Kill` somente apos timeout de cinco segundos. | encerramento cooperativo implementado; build/UI e fluxo real do painel pendentes |
| `ClientPatcher` (`PatchDefinition`, `NativeHookPayload`, `X86Emitter`, `RelFixup`, `AbsoluteFixup`, `PeSection`, `PeLayout`) | sem equivalente no servidor 7.69; utilitario nativo historico do checkout C# | sem equivalente no W2PP servidor | estacionar como ferramenta de laboratorio/historico. O projeto esta listado na solution, mas nao e referencia do core; nao distribuir nem executar contra o cliente-alvo 7.670, cuja estrategia aprovada e source compilada sem DLL/hooks. | escopo auditado; exclusao do pacote de release precisa de gate automatizado/documentado |
| `SetShortSkillRequest` | `Basedef.h::MSG_SetShortSkill`, `TMSrv/_MSG_SetShortSkill.cpp` | mesmos simbolos/handler | manter os 20 bytes de payload: `SkillBar[4]`, `ShortSkills[16]`; ambos os legados apenas copiam os vetores ao estado do personagem. O C# persiste de imediato depois de validar `USER_PLAY`, decisao de durabilidade que nao altera o wire. | auditado; parser e persistencia testados |
| `PkInfoConfirmation` | `Basedef.h::_MSG_PKInfo` como `MSG_STANDARDPARM`, `TMSrv/_MSG_PKMode.cpp` | mesmos simbolos/handler | manter frame de 16 bytes com `state` int32 e ID do alvo, multicast na grade. O calculo comum cobre culpa/PK/castelo/torre; a zona RvR adicional esta no W2PP, nao na 7.69, logo nao entra sem regra de mundo propria. | auditado no wire; calculo/multicast de estado PK pendentes |
| `ActionRequest` | `Basedef.h::MSG_Action`, `TMSrv/_MSG_Action.cpp` | mesmos simbolos | manter layout de 52 bytes e opcodes `Action`/`Action2`/`Action3`. O DTO preserva `WireType` ao reemitir e `TryApplyMovement` usa a janela inclusiva `VIEWGRIDX=VIEWGRIDY=33` sobre a posicao autoritativa, ignorando `PosX/PosY` enviados pelo cliente. `LegacyMovementMapRules`/`WorldHub` aplicam `0x80`/`0x20`, `BASE_GetGuild` para as cinco cidades e `DoRecall` com relay de `MSG_Action` de teleporte; os indices 46/47 de `Language.txt` sao enviados por `MSG_MessagePanel` antes do recall. Permanecem abertos trade e correcao/teleporte fora desse gate. | auditado; parser, round-trip dos tres opcodes, origem defasada, limites 33/34, gates de celula, avisos e recall autoritativo testados |
| `MotionRequest` / `SetHpModeConfirmation` | `Basedef.h::MSG_Motion`, `Basedef.h::MSG_SetHpMode`, `TMSrv/_MSG_Emotion.cpp`, `SendFunc.cpp::SendHpMode` | mesmos simbolos | manter os 20 bytes de `Motion` e relay em `USER_PLAY`; com HP exatamente zero, enviar antes o `MSG_SetHpMode` de 20 bytes (`Hp`, `Mode=22`) e nao retransmitir a animacao. | auditado; wires de Motion e SetHpMode testados; gate HP integrado no listener; `AddCrackError` ainda nao possui equivalente |
| `LoginSessionRegistry` | `pUser[conn].Mode`, `pMob[conn].TargetX/TargetY`, `LastActionTick` nos handlers TMSrv | mesmos campos/handlers | adaptador arquitetural C#, sem classe C++ equivalente. Manter transicoes de login; separar futuramente politica de movimento e timing de combate, hoje acopladas indevidamente ao registro de sessao. | auditado; transicoes/movimento/timing testados; extracao bloqueada ate caracterizacao |
| `AttackRequest` / `AttackConfirmation` | `Basedef.h::MSG_Attack/MSG_AttackOne/MSG_AttackTwo`, `TMSrv/_MSG_Attack.cpp` | mesmos structs e handler | manter os tres wires: 168/68/80 bytes, offsets de posicao/alvo/atacante/skill e slots `STRUCT_DAM`. O cliente so declara marcador de dano (`-2` fisico, `-1` skill); HP, EXP, MP, ReqMp, cena e dano sao reescritos pelo servidor. O C# ja preserva `USER_PLAY`, vivo antes de timing, janela de tick e mana; ainda nao possui gate de trade. A 7.69 acrescenta bloqueio RvR sem equivalente no W2PP, portanto fica como decisao separada, nao importacao automatica. | auditado; parser e relay autoritativo testados; gates completos de mapa/PvP/efeitos/anti-cheat ainda nao equivalentes |
| `NpcAttackConfirmation` | `Basedef.h::MSG_AttackOne`, `TMSrv/GetFunc.cpp::GetAttack` | mesmos simbolos/funcao | reutilizar o `MSG_AttackOne` de 68 bytes, `ID=ESCENE_FIELD`, atacante/posicoes/alvo autoritativos e dano no primeiro `STRUCT_DAM`. `GetAttack` define `SkillParm=0`, `SkillIndex=-1` e motion aleatorio 4..6; o C# ainda usa valores simplificados para summon, que precisam ser cotejados antes de afirmar equivalencia visual. | wire auditado; ataque autonomo C# testado; sem equivalencia integral de semantica/cliente |
| `SetHpMpConfirmation` | `Basedef.h::MSG_SetHpMp`, `SendFunc.cpp::SendSetHpMp` | mesmos simbolos | manter frame de 28 bytes com HP/MP e ReqHp/ReqMp autoritativos; nao e resposta exclusiva de mana, tambem sincroniza dano, cura e ressurreicao. | auditado; wire e recursos testados |
| `UpdateScoreConfirmationV769` / `W2ppUpdateScoreV1Adapter` | cliente `Projects/TMProject/Basedef.h::MSG_UpdateScore` (`0x0336`) | W2PP `MSG_UpdateScore` (`0x0336` com flags), `TMSrv/SendFunc.cpp::SendScore` | O cliente 7.69 mede frame/payload 152/140: `STRUCT_SCORE` 48, Critical/SaveMana, 32 affects, Guild/GuildLevel, Resist, padding de alinhamento, ReqHp/ReqMp, ushort Magic/Rsv e LearnedSkill. W2PP tambem mede 152/140, mas usa score com Level int e Merchant/Direction/ChaosRate, RegenHP/MP, CurrHp/CurrMp, Magic int e quatro Special bytes; o adapter mapeia CurrHp/MP para ReqHp/MP, verifica narrowing de Level/Magic e zera campos target-only. O header DBSrv comum 7.69 empacota a mesma lista para 147 bytes, divergencia que impede copiar o sender sem confirmar a unidade ABI efetiva. | DTO + adapter + golden Win32 + teste byte a byte executados; relay target integrado no login e nos caminhos de consumiveis, combate, guild refresh e ressurreicao; o encoder legado de 160 bytes permanece fora do caminho alvo; recomputacao completa e E2E pendentes |
| `UpdateEtcConfirmationV769` / `W2ppUpdateEtcV1Adapter` | cliente `Projects/TMProject/Basedef.h::MSG_UpdateEtc` (`0x0337`) | W2PP/TMSrv `MSG_UpdateEtc` (`0x0337` com flags), `TMSrv/SendFunc.cpp::SendEtc` | Cliente 7.69 mede frame/payload 48/36: FakeExp, Exp, dois words de LearnedSkill, tres bonus signed short e Coin, com padding nativo. W2PP/TMSrv tambem medem 48/36, mas usam Hold unsigned, Exp, Learn long long, tres bonus unsigned short, Magic ushort e Coin. O adapter converte Hold com rejeicao de overflow, divide Learn nos dois words do alvo e preserva os bonus por bits; Magic e descartado porque pertence ao UpdateScore no cliente 7.69. | DTO + adapter + golden Win32 + teste byte a byte executados; relay target integrado com UpdateScore nos caminhos de login, consumiveis, combate e ressurreicao; E2E permanece pendente |
| `MessageChatRequest` / `MessageChatConfirmation` | `Basedef.h::MSG_MessageChat`, `TMSrv/_MSG_MessageChat.cpp` | mesmos simbolos/handler | manter frame fixo de 140 bytes, ID do remetente e multicast local sem eco. C# deliberadamente cobre somente chat local; comandos, mute, canais de guilda/party/reino/cidadao e filtros sao modulos separados, nao efeitos colaterais do parser. | auditado; wire e relay local testados; politicas de chat pendentes |
| `MessageWhisperRequest` / `MessageWhisperConfirmation` | `Basedef.h::MSG_MessageWhisper`, `TMSrv/_MSG_MessageWhisper.cpp` | mesmos simbolos/handler | manter 128 bytes (nome 16 + texto 100), remetente reescrito pelo servidor e entrega apenas a alvo online. `/cp` e o unico comando caracterizado. Reply, disponibilidade de whisper, mute e comandos/eventos da referencia ficam estacionados. | auditado; wire, alvo e `/cp` testados; politicas/comandos pendentes |
| `PartyInviteRequest` / `PartyAcceptRequest` / `PartyRemoveRequest` / `PartyAddConfirmation` / `PartyRemoveConfirmation` | `Basedef.h::MSG_SendReqParty/MSG_CNFAddParty/MSG_AcceptParty/MSG_RemoveParty`, `TMSrv/SendFunc.cpp::SendAddParty/SendRemoveParty` e handlers | mesmos simbolos/handlers | manter convite lider->alvo, aceite vinculado ao convite pendente, formacao/maximo e remocao autoritativos; add confirmation e frame 40 bytes com `Target=52428`, remove confirmation e frame 16 bytes de dois shorts. O C# usa plano de formacao para evitar mutacao parcial. Diferencas de eventos e regras locais (Battle Royale/OnlyTrade) nao migram sem decisao. | auditado; wire/formacao/remocao testados; regras especiais e E2E pendentes |
| `InviteGuildRequest` | `Basedef.h::_MSG_InviteGuild` como `MSG_STANDARDPARM2`, `TMSrv/_MSG_InviteGuild.cpp` | mesmos simbolos/handler | manter 20 bytes, alvo/tipo validados, custo e persistencia antes da atualizacao visual. A 7.69 adiciona penalidade e capacidade de GuildHall ausentes no W2PP; permanecem requisitos distintos ate caracterizacao de dados/persistencia. | auditado; wire e persistencia/visual basicos testados; GuildHall/penalidade e transacao compensatoria pendentes |
| `QuestRequest` | `Basedef.h::_MSG_Quest` como `MSG_STANDARDPARM2`, `TMSrv/_MSG_SingleControl.cpp` | `Basedef.h::_MSG_Quest`, `TMSrv/_MSG_Quest.cpp` | manter 20 bytes, com NPC e confirmacao como int32. O C# cobre somente Perzen e entrada Pista por regras explicitamente configuradas; nao representa o dispatcher integral de quest de nenhuma das fontes. | auditado; wire e rotas Perzen/Pista testados; catalogo/dispatcher de quests pendentes |
| `CreateMobConfirmation` / `CreateMobTradeConfirmationV769` | cliente `Basedef.h::MSG_CreateMob`/`MSG_CreateMobTrade`, `GetFunc.cpp::GetCreateMob`/`GetCreateMobTrade` e `TMFieldScene::OnPacketCreateMob` | mesmos simbolos/funcoes | Probe Win32: cliente/DBSrv `MSG_CreateMob` 236 bytes, `Equip` +34, `Affect` +70, `Score` +140, `Equip2` +190; `MSG_CreateMobTrade` 260 bytes, `Equip2` +190, `Nick` +208, `Desc` +234, `Server` +258. TMSrv/W2PP `MSG_CreateMobTrade` mede 252 bytes totais, com 16 equipamentos/aparencias, `Tab` em +190 e `Desc` em +216. C# mantém contratos client 18-slot separados do sender W2PP 16-slot; sem a extensao os indices 16/17 continuam zero, e com ela a projecao visual usa os 18 itens. | `CreateMobConfirmationV769`/`W2ppCreateMobV1Adapter` possuem fixture Win32 de payload 224/frame 236; `CreateMobTradeConfirmationV769` possui fixture estrutural byte a byte de payload 248/frame 260; `W2ppCreateMobTradeV1Adapter` possui fixture de projeção do payload W2PP 240 para o contrato cliente; os relays de CreateMob normal, UpdateEquip e autotrade aceitam o estado 18-slot quando disponivel; política NPC, calculo de visual por semantica dos slots novos e E2E continuam pendentes |
| `RemoveMobConfirmation` | cliente `Basedef.h::MSG_RemoveMob`, W2PP `Basedef.h::MSG_RemoveMob`, `SendFunc.cpp::SendRemoveMob` | mesmos simbolos/funcoes | Cliente 7.69 e W2PP medem 16 bytes: `MSG_STANDARD` de 12 bytes seguido de `int RemoveType` no payload offset 0; o ID do mob fica no header. Nao ha delta de layout nem campo target-only. | golden Win32 de payload 4/frame 16 e teste byte a byte executados; nenhuma adapter estrutural e necessaria; politica de remocao e relay continuam no fluxo de mundo |
| `NpcChatConfirmation` / `MessagePanelConfirmation` | `Basedef.h::MSG_MessageChat/MSG_MessagePanel`, `SendFunc.cpp` | mesmos simbolos | manter ambos os textos fixos de 140 bytes, mas com remetente/ID distintos: NPC usa o ID do NPC, painel e notificacao privada do servidor. | auditado; wire e emissores principais testados |
| `StartTimeConfirmation` / `MobLeftConfirmation` | `_MSG_StartTime` / `_MSG_MobLeft` como `MSG_STANDARDPARM`, timers e `CCastleZakum.cpp` | mesmos sinais/timers | manter 16 bytes e `ESCENE_FIELD`, distribuindo somente a jogadores na area/party adequada. C# cobre Pista/Castelo configurados; a matriz completa de salas, eventos e timers fica pendente. | auditado; wire e rotas Pista/Castelo testados; catalogo completo de emissores pendente |
| `DonateShopOpenRequest` / `RetailNpcShopRequest` | `Basedef.h::MSG_REQShopList`, `TMSrv/_MSG_REQShopList.cpp` | mesmos simbolos/handler | `0x027B` e request retail de NPC; nao o confundir com compra web usando Cash. Manter o fluxo retail por Gold sob auditoria. O bridge Donate de 20 bytes e custom e fica estacionado. | parsers/gates testados; sem aceite para loja Cash in-game |
| `DonateBalanceConfirmation`, `DonateShopOpenConfirmation`, `DonateStoreCatalogConfirmation`, `DonateStoreEntry`, `DonatePurchaseRequest`, `DonateShopCatalogRequest`, `DonateShopClientState` | 7.69 e W2PP mantem saldo `Donate` e incluem uma variante de fork para `MSG_BUY` com `EF_DONATE`; 7.69 possui ainda opcodes/protocolos auxiliares, enquanto W2PP nao contem os opcodes do painel custom | mesmo saldo/variantes de fork; sem equivalentes para os opcodes C# `0x0406/0411/0418/041C` | regra de produto: Cash e saldo de conta/site; Premium Neil abre a loja web; checkout debita Cash e entrega item no bau. Compra por Cash/Donate via NPC/painel nao sera implementada. O C# atual `TryPurchaseDonateItem` debita e insere no MOB, portanto e extensao fora de escopo para estacionar/remover em limpeza autorizada. Preservar somente o dado/bridge de conta que a integracao Site realmente exigir. | wire custom e compra C# existem, mas estao fora do escopo; integracao web->debito->bau nao localizada/implementada |
| `LegacySkillAttackGate` / `LegacySkillCombatMath` / `LegacyMobCombatState` | gates e formulas de `TMSrv/_MSG_Attack.cpp`, `CMob.cpp`, `GetFunc.cpp` | mesmos fluxos-base, com variacoes de regras locais | preservar calculo inteiro, custo autoritativo de mana, limite de master/parry e estado lido do `STRUCT_MOB`; cada formula so avanca depois de cotejada nas duas referencias. | auditado; mana/dano fisico/skill e varios ramos de NPC testados; score integral, delays, regras de area/war e efeitos restantes pendentes |
| `LegacyMobAbilityMath` / `LegacyCurrentScoreMath` / `LegacyEquipmentRules` / `LegacyExperienceMath` | cliente `Projects/TMProject/Basedef.cpp::BASE_GetMobAbility/BASE_CanEquip`; servidor `Servidor/Source/Code/Basedef.cpp`; TMSrv `CMob.cpp::GetCurrentScore` e `SendFunc.cpp::SendEquip` | `Basedef.cpp` ability/clear loops, `TMSrv/CMob.cpp::GetCurrentScore` | A regra de `BASE_CanEquip` coincide com W2PP nos guards relevantes (mascara `EF_POS`, conflito 6/7 e rejeicao explicita da posicao 15); isso nao prova estado de 18 slots. O handler TMSrv `_MSG_Move_Item.cpp` valida destino de equipamento em 0..14 e origem em 1..14 usando seu `MAX_EQUIP=16`, igual ao W2PP. A release C# 7670 mantém `LegacyEquipmentRules` como gate de semantica e o `WorldHub` usa a lista versionada de 18 para trade, appearance, parry, force, PVP e reflect; os slots 16/17 so passam se o item-data atribuir posicao valida. `LegacyMobAbilityMath` ja percorre 16 ou 18 conforme a entrada; `LegacyCurrentScoreMath`/Experience e outros loops que recebem apenas MOB continuam 16 ate o recalculo autoritativo aceitar a extensao. A incoerencia de build permanece: `TMSrv.vcxproj` compila `..\Basedef.cpp` com header comum 18 enquanto translation units TMSrv usam `TMSrv/Basedef.h` 16; nao tratar esse source como oracle runtime coerente. | comparacao C#↔W2PP concluida; serializer/migration/runtime 18-slot e teste de mutacao/relay executados; score integral, semantica final dos slots novos, ABI TMSrv e E2E cliente continuam pendentes |
| `GetItemRequest` / `GetItemConfirmation` / `DecayItemConfirmation` | `Basedef.h::MSG_GetItem/MSG_CNFGetItem/MSG_DecayItem`, `TMSrv/_MSG_GetItem.cpp` | mesmos simbolos/handler | manter wire e pickup autoritativo; o destino e somente carry e a validade/alcance vem do item de chao, nao do cliente. | auditado; wire/pickup/decay testados |
| `DropItemRequest` / `DropItemConfirmation` / `CreateItemConfirmation` | `Basedef.h::MSG_DropItem/MSG_CNFDropItem/MSG_CreateItem`, `TMSrv/_MSG_DropItem.cpp` | mesmos simbolos/handler | manter wire e escolha autoritativa da celula; a origem e validada no servidor. Cargo permanece fora do recorte atual. | auditado; wire/drop/chao testados |
| `DeleteItemRequest` | `Basedef.h::MSG_DeleteItem`, `TMSrv/_MSG_Clear_Item.cpp` | `Basedef.h::MSG_DeleteItem`, `TMSrv/_MSG_DeleteItem.cpp` | manter wire de 20 bytes e limpeza autoritativa do carry; a confirmacao visual ocorre por `SendItem`, nao por resposta dedicada. | auditado; wire/delete carry testados |
| `SplitItemRequest` | `Basedef.h::MSG_SplitItem`, `TMSrv/_MSG_SplitItem.cpp` | mesmos simbolos/handler | manter wire de 24 bytes, faixa 1..119 e insercao no primeiro carry livre. A elegibilidade vem de catalogo configuravel: 7.69 usa `groupItens`, W2PP traz lista fixa; nao transplantar lista sem evidencia da release 7.670. | auditado; wire/split testados |
| `UseItemRequest` | `Basedef.h::MSG_UseItem`, `TMSrv/_MSG_Use_Item.cpp` | `Basedef.h::MSG_UseItem`, handler equivalente | manter wire de 36 bytes e autoridade de origem/destino/grade no servidor. O voucher Vol 184 que soma `EF_DONATE` e top-up por item nas duas fontes e comportamento distinto de comprar em loja; confirmar se entra no modelo Cash do Site antes de manter. Compra de Cash por NPC/painel esta excluida. | auditado; wire/familias portadas testados; decisao do voucher pendente |
| `SendItemConfirmation` | `Basedef.h::MSG_SendItem`, `TMSrv/SendFunc.cpp::SendItem` | mesmos simbolos | manter 24 bytes e atualizacao de slot por `invType`, `Slot`, `STRUCT_ITEM`; e a resposta comum a mutacoes de inventario, nao confirmacao exclusiva de uma rota. | auditado; wire testado |
| `UpdateCarryConfirmation` | cliente 7.69 `Projects/TMProject/Basedef.h::MSG_Carry`/`TMHuman::OnPacketCarry`, servidor `Servidor/Source/Code/TMSrv/SendFunc.cpp::SendCarry` | `MSG_UpdateCarry` / `MSG_Carry` (`0x0185`) | manter frame de 528 bytes: 64 `STRUCT_ITEM` de 8 bytes no payload e `Coin` no offset 512; e o refresh completo usado pela conclusao de trade, nao uma sequencia de `MSG_SendItem`. | DTO/encoder, fixture de contrato byte a byte e relay para os dois participantes testados; E2E pendente |
| `EquipmentAppearanceV769` / `W2ppEquipmentAppearanceV1Adapter` | cliente `Projects/TMProject/Basedef.h::MSG_UpdateEquip`, opcode `0x036B`, `TMHuman`; `TMSrv/SendFunc.cpp::SendEquip` | W2PP `MSG_UpdateEquip` (`Equip[MAX_EQUIP]`, `AnctCode[MAX_EQUIP]`) e `SendEquip` | Cliente/DBSrv comum 7.69 mede 68 bytes: `sEquip` +12 e `Equip2` +48 (18 slots); o TMSrv privado/W2PP mede 60 bytes: vetores de 16 e `AnctCode` +44. O sender TMSrv percorre os 16 do header local, logo nao emite appearance dos indices 16/17; o C# possui DTO isolado de 18 slots e aceita tanto a projecao legada 16->18 quanto a lista versionada completa. | probe Win32 executado; golden Win32, teste do DTO/adapter, relay visual com slots 16/17 e mutacao de runtime executados; E2E cliente e semantica completa dos slots novos pendentes |
| `UpdateItemRequest` / `UpdateItemConfirmation` | `Basedef.h::MSG_UpdateItem`, `TMSrv/_MSG_UpdateItem.cpp`, timers/portoes | mesmos simbolos, `CCastleZakum` e timers | manter wire de 20 bytes e mutacao autoritativa do estado de item de mapa; mascaras, chave, altura, gate/timer e multicast pertencem ao dominio de mundo. | auditado; wire, mascara, timer e portoes testados |
| `LegacyItemDataTable` / `LegacyServerItemDataTable` / `LegacyItemDefinition` / `LegacyServerItemDefinition` / `LegacyItemStaticEffect` / `LegacyItemEffect` | loader server 7.69 em `Servidor/Source/Code/Basedef.cpp`, tipos/constantes em `Basedef.h`; layout client em `Projects/TMProject` | loaders/tipos equivalentes em `Source/Code/Basedef.cpp/.h` | os leitores C# mantem separados o layout client/converter de 164 bytes e o runtime server de 140 bytes. A habilidade C# continua subconjunto no leitor cliente, nao `BASE_GetItemAbility` completo; o leitor server-side ainda e somente formato. | parser client e server 7.69/W2PP testados; fonte autoritativa do arquivo, consumidores server-side e ramos restantes de habilidade pendentes |
| `LegacySkillDataTable` / `LegacySkillDefinition` | `Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill`, `Basedef.h::STRUCT_SPELL` | mesmos simbolos; `MAX_SKILLINDEX=248` | manter `/4` em `AffectTime` e a semantica da segunda coluna de `Act`; C# rejeita linha malformada em faixa ao inves de aceitar parcial via `sscanf`. | comparacao estatica concluida; fixture/parser testados |
| `LegacySummonCatalog` / `LegacySummonTemplate` / `LegacySummonBonus` | `TMSrv/CNPCGene.cpp::CNPCSummon::Initialize`, `Server.cpp::GenerateSummon`, `Basedef.cpp::pSummonBonus` | mesmos simbolos/funcoes | C# carrega 43/50 slots por padrao, incluindo `Cav._Arcano`, `Arq._Arcano` e `Mag._Arcano` nos slots 40..42. A leitura historica de 7.59 continua disponivel somente com limite explicito de 40 slots; slots 43..49 permanecem indisponiveis. | catalogo 7.69/W2PP carregado e nomes/slots testados; E2E de combate/relay especifico dos slots Arcano pendente |
| `LegacyNpcGenerationCatalog` / `LegacyNpcGenerationDefinition` / `LegacyGeneratedNpc` / `LegacyNpcGenerationBuilder` | `TMSrv/CNPCGene.cpp::CNPCGenerator::ParseString`, `Server.cpp::GenerateMob` | mesmos parser e geracao; W2PP tem apenas variacao de diagnostico | manter como adapter parcial para spawn configurado de lider; segmentos, followers, grupos, contadores, rotas, waits/actions e agenda do legado nao estao cobertos. | subset implementado/testado; geracao integral pendente |

**Proximo lote:** continuar a auditoria dos leitores e consumidores de dados
estaticos, resolvendo primeiro o contrato ItemList server/client e o catalogo
de summons. Depois retomar combate (gates, delay, mapa/PvP e emissores de score)
e os recortes restantes de `WorldHub`. A validacao visual do cliente 7.670 segue
pendente.

### Politica de versao da release

`7.670` e o identificador da release-alvo e corresponde ao inteiro obrigatorio
`7670` em `MSG_AccountLogin.ClientVersion`. A constante deve ser emitida pela
build do cliente; o servidor apenas a valida. As fontes registram valores antigos
distintos (`APP_VERSION` 1001/1059 na arvore 7.69 e 7640 no W2PP), que devem ser
substituidos na migracao do cliente e rejeitados pelo listener 7.670. Adaptadores
de opcode/layout continuam possiveis, mas nao dispensam a versao exigida.

## Dados estaticos e formatos de referencia

| ID | Referencia 7.69 | C# | Contrato e diferenca W2PP | Teste/evidencia | Nivel |
|---|---|---|---|---|---|
| ITEMDATA-001 | `Servidor/Source/Code/Basedef.h::STRUCT_ITEMLIST/BASE_ReadItemList`; `Projects/TMProject/Basedef.h` e `Tools/ItemListConverter/WYDConverter.h` | `LegacyItemDataTable` + `LegacyServerItemDataTable` | o contrato cliente/converter le 6500 registros de 164 bytes (XOR `0x5A`, trailer opcional), com `nPos` int no offset 136. A source do servidor 7.69 e o W2PP definem 6500 registros de 140 bytes, com `nPos` short em 134, `Extra` em 136 e `Grade` em 138; ambos leem trailer de checksum e decodificam o corpo com o mesmo XOR. | os dois leitores C# possuem fixtures e rejeicao de trailer parcial; o probe Win32 confirmou 164 no cliente e 140 no DBSrv/TMSrv 7.69 e W2PP; arquivo autoritativo e consumidores server-side ainda precisam de decisao | C2 |
| ITEMDATA-002 | `STRUCT_ITEMLIST` e efeitos `EF_*` em `Servidor/Source/Code/Basedef.h`; habilidade `BASE_GetItemAbility/Sanc/Gem` em `Basedef.cpp` | `LegacyItemDefinition` / `LegacyServerItemDefinition` / `LegacyItemStaticEffect` / `LegacyItemEffect` / metodos de `LegacyItemDataTable` | os DTOs C# mantem os formatos separados, sem converter 32-bit `nPos` do cliente em `short` por presuncao. `GetItemAbility` continua um subconjunto no leitor cliente: faltam ramos presentes nas duas sources como restricoes por `nUnique`, substituicao `EF_CRITICAL2`/`EF_ACADD2` e bonuses de montaria; o leitor server-side ainda nao foi ligado a habilidade autoritativa. | parser/offsets dos dois contratos possuem fixtures; comparacao do runtime server-file, escolha da fonte e ramos restantes pendentes | C2 |
| SKILLDATA-001 | `Servidor/Source/Code/Basedef.cpp::BASE_InitializeSkill`, `Basedef.h::STRUCT_SPELL` | `LegacySkillDataTable` / `LegacySkillDefinition` | ambas as sources usam `MAX_SKILLINDEX=248`, convertem `AffectTime / 4` e passam o mesmo buffer `skilldata` para as duas colunas de acao; a segunda coluna prevalece no `Act[0..5]`, e `Act[6..7]` fica zero. O C# preserva as duas colunas para auditoria e expoe `Action2` como efetiva; parsing de linha valida e deliberadamente mais estrito que `sscanf`. | parser/tempo/acoes cotejados; teste de fixture existente; dados reais completos nao estao no checkout de referencia |
| SUMMONDATA-001 | `TMSrv/CNPCGene.cpp::CNPCSummon::Initialize`, `Server.cpp::GenerateSummon`, `Basedef.cpp::pSummonBonus` | `LegacySummonCatalog` / `LegacySummonTemplate` / `LegacySummonBonus` | C# carrega 43 templates em slots 0..42 por padrao; 7.69 e W2PP inicializam os mesmos 43, incluindo os tres templates Arcano. Os 50 slots continuam sendo o limite do wire/tabela; bonuses nao zero estao somente nos primeiros oito em ambas as fontes. | discrepancia de tres templates resolvida no leitor/catalogo; E2E de summon e cobertura de bonus especifica ainda pendentes |
| NPCDATA-001 | `TMSrv/CNPCGene.cpp::CNPCGenerator::ParseString/ReadNPCGenerator`, `Server.cpp::GenerateMob` | `LegacyNpcGenerationCatalog` / `LegacyNpcGenerationDefinition` / `LegacyGeneratedNpc` | C# e parser subset, intencionalmente usado para spawn do lider. O legado tambem carrega segmentos, waits/actions, destino, followers, grupos, contador/maximo e agenda; nao tratar o leitor/`TryCreateLeader` como port completo de `GenerateMob`. | subset auditado; parser e spawn configurado testados; followers/rotas/grupos/timers pendentes | C2 |

## Rotas de entrada atualmente ligadas

| ID | Referencia legada | Entrada C# / opcode | Handler ou metodo de dominio | Estado/persistencia | Saida principal | Teste/evidencia | Nivel |
|---|---|---|---|---|---|---|---|
| AUTH-001 | `_MSG_AccountLogin` / `CFileDB` | `AccountLoginRequest` `0x020D` | `AccountLoginCoordinator.HandleAsync` | sessao; arquivo/MariaDB | `AccountLoginConfirmationV769` `0x010A` ou `MessagePanel` | parser, coordinator, adapter/golden 1928, frame projetado no listener, failure notice | C4 |
| AUTH-002 | `_MSG_AccountSecure` | `AccountSecureRequest` `0x0FDE` | `AccountSecureCoordinator.HandleAsync` | PIN/security store | `AccountSecureSignal` | parser, signal e coordinator | C3 |
| CHAR-001 | `_MSG_CreateCharacter` | `CreateCharacterRequest` `0x020F` | `CreateCharacterCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_SELCHAR`; snapshot, nome, template | `NewCharacterConfirmationV769` `0x0110`, layout alvo Win32 920 bytes, `SelChar` +16 | adapter de seleção, contrato/golden 920, template, create fixture e sucesso integrado ao listener; falha/E2E pendentes | C4 |
| CHAR-002 | `_MSG_DeleteCharacter` | `DeleteCharacterRequest` `0x0211` | `DeleteCharacterCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_SELCHAR`; slot limpo/defaults | `DeleteCharacterConfirmationV769` `0x0112`, layout alvo Win32 920 bytes, `SelChar` +16 | adapter de seleção, contrato/golden 920, fixture de refresh e sucesso integrado ao listener; falha/E2E pendentes | C4 |
| CHAR-003 | `_MSG_CharacterLogin` | `CharacterLoginRequest` `0x0213` | `CharacterLoginCoordinator.HandleAsync` | `USER_SELCHAR -> USER_CHARWAIT -> USER_PLAY`; rejeicao retorna a `USER_SELCHAR`, MOB/MOBEXTRA | `CharacterLoginConfirmationV769` `0x0114`, `CreateMobV769`, `UpdateEtcV769`, `UpdateScoreV769` | parser, transicao e composer W2PP -> 7.69 testados; cadeia integrada no listener com frames 1728/236/48/152; relays de CreateMob/UpdateEtc/UpdateScore cobrem consumiveis, combate, summons/NPCs, guild refresh e ressurreicao; UpdateAffect cobre login e mutacoes com snapshot autoritativo quando os campos cabem no wire; UpdateEquip cobre troca de equipamento, catalisador e durabilidade de montaria; `BASE_GetHpMp`, quinze recortes e uma composicao de `LegacyCurrentScoreMath`, mais `LegacyMountRunRules`, portados/testados isoladamente; demais recalculos TMSrv, narrowing incompativel e E2E pendentes | C4 |
| CHAR-004 | `_MSG_CharacterLogout` | `CharacterLogoutRequest` `0x0215` | `LoginSessionRegistry` + save | SPX/SPY e estado | `CharacterLogoutConfirmation` `0x0116` | logout wire e transicoes | C4 |
| MOVE-001 | `_MSG_Action` | `ActionRequest` `0x036C` e variantes | `TryApplyMovement`, summons | sessao/posicao | relay de action/create mob | parser, movement, world broadcast | C3 |
| MOVE-002 | `_MSG_Motion` | `MotionRequest` `0x036A` | relay de movimento; `MSG_SetHpMode` quando HP=0 | sessao/HP | frame de motion ou SetHpMode | motion wire e gate de vida | C3 |
| SKILL-001 | `_MSG_SetShortSkill` | `SetShortSkillRequest` `0x0378` | save de short skills | estado do personagem | sem confirmacao dedicada | wire 32 bytes e persistencia | C3 |
| WORLD-001 | `_MSG_PKInfo` | `PkInfoConfirmation` `0x0366` | politica PK/multicast de visao | flag visual de PK/culpa | `MSG_STANDARDPARM` | wire 16 bytes; politica/multicast pendentes | C2 |
| COMBAT-001 | `_MSG_Attack` | `AttackRequest` `0x0367/039D/039E` | `TryConsumeSkillMana`, `TryApplyPhysicalAttack` | HP/MP, alvo, drops, XP | `AttackConfirmation`, `SetHpMpConfirmation`, `UpdateEtc` | wire 168/68/80, mana e dano fisico autoritativos testados; ordem integral de gates/anti-cheat/mapa/PvP pendente | C3 |
| COMBAT-002 | ataque de skill | `AttackRequest` + skill index | `TryApplySkillAttack` | HP/MP, efeitos, summon | ataque, recursos, morte | gate/formulas e ramos iniciais de NPC testados; delays, todos os efeitos e equivalencia de area pendentes | C3 |
| ITEM-001 | `_MSG_UseItem` | `UseItemRequest` `0x0373` | `TryApplyPotion` | MOB/carry/HP/MP | `SetHpMpConfirmation`, `SendItem` | potion, reset, item families | C3 |
| ITEM-002 | `_MSG_DropItem` | `DropItemRequest` `0x0272` | `TryDropItem` | carry -> ground item | drop/create/broadcast | wire e ground drop | C3 |
| ITEM-003 | `_MSG_GetItem` | `GetItemRequest` `0x0270` | `TryGetGroundItem` | ground -> carry | get/decay/send item | wire e pickup | C3 |
| ITEM-004 | 7.69 `Basedef.h::MSG_SwapItem`, `SGrid.cpp`, cliente `TMHuman.cpp` e `TMSrv/_MSG_Move_Item.cpp` (`0x0376`) | `TradingItemRequest` / `TradingItemConfirmation`, payload 8 bytes / frame 20 bytes | `WorldHub.TryTradeItems` | carry 0..59 e carry↔equipamento; C# permite destino Equip 0..14 e origem Equip 1..14; cargo estacionado | echo + dois `SendItem` | C# valida a mesma faixa de slot que o handler TMSrv/W2PP e preserva a ordem de campos 7.69 no request; wire de mesmo tamanho ainda nao distingue layouts, pois W2PP usa ordem inversa e `WarpID` int. UI 7.69 expoe indices 16/17, mas a rota TMSrv os rejeita pelo header privado de 16 e pelo limite `MAX_EQUIP-1`; a regra client-side de `EF_POS` nao os habilita no servidor. A compilacao TMSrv do `..\Basedef.cpp` com header comum 18 cria conflito de layout com os translation units 16-slot, portanto a equivalencia observada da rota nao resolve os efeitos das helpers ligadas. | C3; comparacao de faixa concluida; indices 15..17 sem suporte nessa rota; fechamento de ABI TMSrv e golden 7.69 pendentes |
| ITEM-005 | `_MSG_SplitItem` | `SplitItemRequest` `0x02E5` | `TrySplitCarryItem` | stack origem -> primeiro carry livre | dois `SendItem`, destino depois origem | wire, quantidade, item permitido, sem espaco | C3 |
| SOCIAL-001 | `_MSG_MessageChat` | `MessageChatRequest` `0x0333` | view relay | nenhum | chat para visao | wire e player view | C3 |
| SOCIAL-002 | `_MSG_MessageWhisper` | `MessageWhisperRequest` `0x0334` | busca por nome + `SendAsync` | nenhum | whisper privado | wire e target | C3 |
| SOCIAL-003 | `_MSG_SendReqParty` | `PartyInviteRequest` `0x037F` | `TryPreparePartyInvite` | pending party | confirmation/panel | party wire/lifecycle | C3 |
| SOCIAL-004 | `_MSG_AcceptParty` | `PartyAcceptRequest` `0x03AB` | `TryAcceptParty` | party/leader | add/remove mob | party lifecycle | C3 |
| SOCIAL-005 | `_MSG_RemoveParty` | `PartyRemoveRequest` `0x037E` | `TryRemovePartyMember` | party | removal/disband | party lifecycle | C3 |
| SOCIAL-006 | `_MSG_InviteGuild` | `InviteGuildRequest` `0x03D5` | `TryPrepareGuildInvite` | guild/coin + store | UpdateEtc/CreateMob | guild invite/update | C3 |
| EVENT-001 | `_MSG_Quest` / Perzen | `QuestRequest` `0x028B` | `TryExchangePerzenItem` | carry/date | chat + replacement item | Perzen exchange | C3 |
| EVENT-002 | `_MSG_Quest` / Pista | `QuestRequest` `0x028B` | `TryRegisterPistaParty` | registration/timers | entry plan | Pista registration/schedule | C3 |
| SHOP-001 | custom Donate panel open | `DonateShopOpenRequest` | old balance/catalog policy | C# session | custom opcodes | none in target client | parked: no in-game Cash store |
| SHOP-002 | retail NPC shop list | `RetailNpcShopRequest` | `0x027B` / `IsDonateShopNpc` legacy bridge | NPC/Gold shop | retail catalog | distinguish Gold retail from Cash | audit normal Gold flow; Donate bridge parked |
| SHOP-003 | custom Donate catalog | `DonateShopCatalogRequest` | custom catalog provider | rate limiter | `MSG_UpdateDonateStore` | no target UI contract | parked/out of product scope |
| SHOP-004 | custom Donate purchase | `DonatePurchaseRequest` | `TryPurchaseDonateItem` | atomic debit + MOB | balance/item/panel | target is web debit + chest delivery | existing C# behavior conflicts; park/remove |
| WEBSTORE-001 | Premium Neil / account Cash web shop | no C# route identified | Site checkout + account balance + chest delivery | account/Cash source of truth | web checkout and warehouse grant | site route, idempotency, transaction/outbox | not implemented/verified |
| OPS-001 | wire diagnostics | all decoded frames | `ServerWireLog.WriteFrame` | append-only log | RX/TX evidence | masking/flush behavior | C4 |

## Rotas localizadas, mas ainda nao fechadas

Estas entradas aparecem na referencia `ProcessClientMessage.cpp` ou nos objetos legados, mas nao possuem ainda uma linha C3/C4 equivalente no dispatcher atual.

| ID | Simbolo legado | Situacao atual | Dependencia para implementar |
|---|---|---|---|
| ITEM-006 | `_MSG_UpdateItem` | wire, agregado de `InitItem`, mascara oficial, mutacao de altura, chave, dispatcher, multicast, inicio/portoes Zakum, `_MSG_StartTime`, timer de expiracao, fechamento automatico estatico por minuto e decay dinamico C3 | validar E2E; `pItem[]` permanece volátil |
| ITEM-007 | `_MSG_DeleteItem` | referencia localizada | validacao de slot e rollback |
| ITEM-008 | `_MSG_CombineItem*` | varias variantes legadas | servico de inventario e tabela de regras |
| ITEM-009 | `_MSG_ApplyBonus` | referencia localizada | efeitos, limites e persistencia |
| TRADE-001 | `_MSG_ReqTradeList` / `_MSG_SendAutoTrade` | `TradeListRequest` isolado; parser/encoder de `MSG_STANDARDPARM` (`0x039A`, 16 bytes) testado; `LegacyAutoTradeListRules` cobre solicitante vivo/em `USER_PLAY`, alvo existente/ativo/em `USER_PLAY` e janela inclusiva `VIEWGRIDX/Y=33`; `LegacyAutoTradeLocationRules` reproduz os cinco limites de vila, a área protegida de Armia e a taxa por `CityTax`; `AutoTradeListConfirmation` e `AutoTradeStartRequest` compartilham e testam o layout client/server de `MSG_SendAutoTrade`/`MSG_AutoTrade` (`0x0397`, 196 bytes) com 12 slots; `LegacyAutoTradeBook` valida uma oferta autoritativa em memoria, incluindo cargo 7.69 de 120 slots, preço, taxa, título, conta bloqueada e `EF_NOTRADE`; o dispatcher abre a loja, consulta o book, reaplica os gates e `LegacyAutoTradeListRelay` entrega a resposta ao solicitante; `LegacyAutoTradeVisualRelay` projeta o MOB/affects autoritatorios e o titulo para `MSG_CreateMobTrade` 7.69 de 260 bytes, enviando ao dono, à vizinhanca e ao solicitante de lista; `LegacyAutoTradeRemovalRelay` monta `MSG_RemoveMob` de 16 bytes no logout/desconexao, enviado somente à vizinhanca 33x33 antes de `WorldHub.Leave`; `LegacyAutoTradeFileStore` persiste owner/listing, localizacao e taxa em documento versionado quando `--autotrade-state` esta ativo, e o host salva na abertura e remove na saida; `MariaDbAutoTradeStateStore` usa `wyd_autotrade_state` no modo MariaDB; `LegacyAutoTradeRehydrator` restaura o MOB offline no boot e projeta `MSG_CreateMobTrade` para novos logins | migração e round-trip local no MariaDB aprovados; banco externo, política de reconexão do dono e E2E pendentes |
| TRADE-005 | `_MSG_ReqBuy` / `MSG_ReqBuy` / `_MSG_ItemSold` | `AutoTradePurchaseRequest` preserva `0x0398`, payload 24/frame 36, `Pos`, `TargetID`, padding ABI, `Price`, `Tax` e `STRUCT_ITEM`; `LegacyAutoTradePurchaseRules` valida alvo/alcance, loja ativa, posição, preço/taxa/item do request contra o listing, cargo do vendedor contra o item, Gold do comprador, espaço 0..59, vila 0..4 e limite de 2G do vendedor, inclusive alvo offline explicitamente reidratado; `ItemSoldConfirmation` preserva `0x039B`, standard-parm2 de 20 bytes e `ESCENE_FIELD`; `LegacyAutoTradeSettlementMath` reproduz o limiar de 100 mil e o truncamento inteiro do imposto; `LegacyAutoTradePurchasePlanBuilder` produz cópias do carry/cargo, saldos e listing pós-venda sem mutar as fontes; `LegacyAutoTradePurchaseCommitRequest` separa o MOB runtime do comprador do MOB persistido e fecha os dois blobs com o listing na mesma transação serializável no MariaDB ou via journal/recovery no file store; `LegacyAutoTradeBook` protege o slot, `WorldHub` aplica/rollbacka o comprador, `LegacyAutoTradePurchaseCoordinator` prepara o request do host e `LegacyAutoTradePurchaseExecutor` coordena book/NPC offline/comprador/commit com rollback reverso; o dispatcher emite `MSG_UpdateCarry`, `MSG_ItemSold` e o visual offline somente depois do commit | 211 testes deterministas, incluindo coordinator, relay pós-commit, CAS distintos, commit/recovery do file store e a fatia 18-slot; smoke local do MariaDB passou com compra, verificação pós-commit e replay CAS; banco remoto, interrupção e E2E continuam pendentes |
| TRADE-002 | `_MSG_Trade` / `_MSG_CNFCheck` | `TradeOfferRequest` e `TradeCheckConfirmation` testados; `WorldHub.TryStageTradeOffer` cobre estado bilateral em memoria, carry autoritativo, saldo, duplicidade, `EF_NOTRADE`, guilda, PK e checks; `LegacyTradeOfferRelay` encaminha a oferta e o dispatcher conclui os dois checks com `TryCompleteTrade` + `LegacyTradeCompletionRelay`/`UpdateCarryConfirmation` para os dois participantes; `LegacyTradePersistence` monta os dois estados e o host chama `IAtomicCharacterStateStore` quando disponivel; `MariaDbWorldCharacterStore` e `LegacyFileAccountStore` fazem o commit dos dois blobs em transacao serializavel ou journal/recovery, respectivamente | transacao MariaDB ainda sem teste contra banco vivo; mesma conta, ciclo completo e E2E |
| TRADE-003 | `_MSG_QuitTrade` | `TradeCloseRequest` isolado; dispatcher implementado no host; `WorldHub.TryCloseTrade` limpa os dois lados em memoria; `WorldHub.Leave` reporta o par cancelado no disconnect sem mutar carry/Gold | sinal visual legado `900`, refresh de PK equivalente, ciclo completo e E2E |
| TRADE-004 | `_MSG_TradingItem` completo (cliente 7.69 `MSG_SwapItem`) | apenas carry 0..59 | cargo, equipamento, merge, validade e regras especiais |
| SHOP-005 | `_MSG_Buy` / `_MSG_Sell` | nao portado neste fluxo | catalogo de NPC, Gold, estoque e transacao |
| SHOP-006 | `_MSG_Deposit` / `_MSG_Withdraw` | nao portado | banco/cargo e lock por conta |
| SOCIAL-007 | autotrade | referencia localizada; `LegacyAutoTradeRehydrator` e `LegacyAutoTradeReconnectCoordinator` ligados | estado persistente, rehidratacao e fechamento no login do owner; vendas/rollback e E2E pendentes |
| WORLD-001 | `_MSG_ReqTeleport` | referencia localizada | mapa, zona e custo |
| WORLD-002 | `_MSG_ChangeCity` | referencia localizada | zonas, spawn e persistencia |
| WORLD-003 | `_MSG_PKMode` | referencia localizada | relacoes PK/war e broadcast |
| EVENT-003 | `_MSG_War` | referencia localizada | estado global e timers |
| EVENT-004 | ranking/challenge | referencia localizada | fonte de dados e wire de retorno |

## Regras para atualizar a matriz

Atualizacao de 23/09/2026: `LegacyAutoTradeReconnectCoordinator` fecha o NPC e
o estado do mesmo account/slot antes da confirmacao de login, captura a
vizinhanca para o `MSG_RemoveMob` e remove o registro persistido. A referencia
W2PP comprova o reset de `TradeMode`/`AutoTrade` no carregamento do personagem,
mas nao comprova uma politica propria de venda offline persistente. Migracao,
vendas/rollback e E2E continuam pendentes.

Atualizacao posterior de 23/09/2026: `LegacyAutoTradePurchaseExecutor` agora
coordena o CAS do book, NPC offline e comprador, chama o commit duravel e faz
rollback em ordem reversa quando o commit e rejeitado. O dispatcher de
`MSG_ReqBuy` foi ligado somente para backends com
`ILegacyAutoTradePurchaseCommitStore`; depois do commit ele envia
`MSG_UpdateCarry`, multicasta `MSG_ItemSold` na vizinhanca do vendedor e
reprojeta o visual offline. O file store tambem possui journal/recovery e teste
de rollback de transacao preparada; o smoke local do MariaDB tambem passou com
compra, verificacao pos-commit e replay CAS. Banco remoto, E2E e interrupcao
durante uma venda real ainda nao foram exercitados.

1. Uma entrada nova começa em C0/C1, nunca em “done”.
2. O simbolo legado e a referencia comportamental devem ser registrados antes do codigo.
3. O wire deve ter teste de tamanho, opcode, checksum, offsets e rejeicoes.
4. O handler deve registrar estado lido, estado mutado e destino de cada resposta.
5. Persistencia precisa indicar adapter, transacao, concorrencia e rollback.
6. A coluna de cliente somente avanca com evidencia do cliente real; build do servidor nao basta.
7. A coluna remota somente avanca com build/publicacao e logs/portas verificados.
8. Qualquer divergencia deve ficar em “known deviations”, nunca escondida em comentario solto.

## Inventario mecanico

Para localizar tipos e metodos no checkout atual, executar na raiz de `Server`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\PortAudit\Generate-PortInventory.ps1
```

O gerador e uma leitura mecanica auxiliar, nao substitui a analise semantica da referencia C++ nem a ficha de equivalencia. Ele deve ser executado antes de uma revisao de arquitetura e depois de grandes extracoes para detectar metodos que ficaram sem classificacao.

## Proximo bloco controlado

O wire e o caminho normal de dominio de `_MSG_UpdateItem` foram fechados em C3. Em paralelo, a validacao dos dois `MSG_SendItem` do split no cliente/rede continua pendente.

## Ficha ITEM-005 — `_MSG_SplitItem`

### Referencia analisada

- `W2PP/Source/Code/Basedef.h:1206`: `_MSG` tem 12 bytes (`Size`, `KeyWord`, `CheckSum`, `Type`, `ID`, `ClientTick`).
- `W2PP/Source/Code/Basedef.h:2381-2390`: `_MSG_SplitItem = 229 | FLAG_CLIENT2GAME`; `Slot`, `sIndex`, `Num` sao tres `int`.
- `W2PP/Source/Code/TMSrv/ProcessClientMessage.cpp:279`: roteamento para `Exec_MSG_SplitItem`.
- `W2PP/Source/Code/TMSrv/_MSG_SplitItem.cpp:21-78`: regra completa da operacao.
- `W2PP/Source/Code/TMSrv/Server.cpp:1059-1081`: `PutItem` escolhe o primeiro carry livre e envia o item inserido.

### Contrato de wire

| Campo | Valor |
|---|---|
| opcode | `229 + 0x0200 = 0x02E5` |
| direcao | cliente -> servidor |
| header | 12 bytes |
| payload | 12 bytes |
| pacote total | 24 bytes |
| payload offset 0 | `int Slot` |
| payload offset 4 | `int sIndex` — presente no wire, mas ignorado pela funcao legada |
| payload offset 8 | `int Num` |
| resposta | nao existe confirmation/echo de split |
| respostas indiretas | `MSG_SendItem` do novo slot, depois `MSG_SendItem` do slot original |

### Ordem comportamental legada

1. Rejeita `Slot < 0` ou `Slot >= MAX_CARRY - 4`; com `MAX_CARRY=64`, o intervalo aceito e 0–59.
2. Rejeita `Num <= 0` ou `Num >= 120`; o intervalo aceito e 1–119.
3. Se o modo nao e `USER_PLAY`, envia `SendHpMode` e encerra.
4. Se existe trade ativo, chama `RemoveTrade` e encerra sem dividir.
5. Aceita somente os indices `413, 412, 419, 420, 416, 414` ou `2390..2419`.
6. Conta slots livres no carry configurado.
7. Rejeita sem espaco, quantidade zero/um ou `amount <= Num`.
8. Atualiza o item original para `amount - Num` usando o efeito de quantidade legado.
9. Cria o novo item com o mesmo `sIndex` e `Num`; os demais efeitos sao zerados pela construcao legada de `STRUCT_ITEM`.
10. `PutItem` grava no primeiro slot livre e envia o novo item.
11. `SendItem` envia novamente o item original atualizado.

### Decisoes de equivalencia para o C#

- Nao validar `request.sIndex` contra o item persistido: a referencia nao o usa.
- Nao acrescentar uma guarda de HP apenas por consistencia com outras operacoes; `_MSG_SplitItem` verifica modo, nao vida do personagem.
- Nao copiar efeitos adicionais do item para o novo slot sem prova da referencia; o comportamento observado reconstrói somente `sIndex` e quantidade.
- O estado bilateral de trade ja existe no `WorldHub` como fatia de dominio em memoria: `TryStageTradeOffer` valida o carry vivo, saldo, slots, itens nao negociaveis, itens de guilda, PK e checks; `TryCompleteTrade` monta os carries candidatos, valida espaco/Gold e aplica a conclusao atomicamente, devolvendo snapshots com metadados de conta, slot, posicao e `MOBEXTRA`; `TryRollbackTradeCompletion` restaura os dois snapshots somente se o estado pos-completion ainda coincide, e `TryCloseTrade` limpa ambos os lados enquanto `Leave` reporta o par cancelado no disconnect. Como a oferta staged ainda nao mutou carry/Gold, o disconnect nao gera rollback persistente. O host ja despacha `MSG_Trade`/`MSG_QuitTrade`, envia `MSG_CNFCheck` e usa `UpdateCarryConfirmation` para o refresh completo dos dois participantes. `LegacyTradePersistence` monta a solicitacao dupla; `MariaDbWorldCharacterStore` faz o commit conjunto com locks ordenados e `LegacyFileAccountStore` faz o commit dos dois blobs por journal/recovery. Quando mesma conta ou falha do backend impedem o commit, o host restaura o runtime por CAS e nao emite relay. A transacao MariaDB ainda nao foi exercitada contra banco vivo. Para autotrade, `LegacyAutoTradeLocationRules` resolve vila/taxa, `LegacyAutoTradeBook` valida a abertura contra conta, cargo e item data, `LegacyAutoTradeListRelay` devolve a listagem, `LegacyAutoTradeVisualRelay` monta o visual 7.69 a partir do MOB/affects autoritatorios e `LegacyAutoTradeRemovalRelay` monta a remocao visual de 16 bytes no logout/desconexao. `LegacyAutoTradeFileStore` cobre a persistencia local opt-in de owner/listing, localizacao e taxa, `MariaDbAutoTradeStateStore` usa a tabela `wyd_autotrade_state` quando o modo MariaDB esta ativo e `LegacyAutoTradeRehydrator` reanexa o MOB offline ao `WorldHub` no boot; migracao/banco ao vivo, politica de reconexao do dono e E2E continuam fora. Essa fatia ainda nao deve ser tratada como equivalencia C7 completa.
- O MOB alterado fica autoritativo no `WorldHub`; o host tenta persistencia imediata somente quando o store oferece `IAtomicCharacterStateStore`. Sem essa capacidade, o save completo ocorre no logout/desconexao. O salvamento completo tambem espelha o Coin do MOB no campo account-wide usado pela selecao.

### Testes obrigatorios antes de ligar o dispatcher

- wire valido: opcode `0x02E5`, tamanho 24, offsets e checksum;
- split de quantidade 10 em 3: origem 7, destino 3, mesmo indice;
- primeiro slot livre escolhido;
- slots `-1`, `60`, `63` e `64` rejeitados;
- `Num` 0, 120 e negativo rejeitados;
- item nao agrupavel rejeitado;
- origem vazia rejeitada;
- `Num >= amount` rejeitado sem mutacao;
- carry cheio rejeitado sem mutacao;
- `sIndex` informado diferente do item real nao altera o resultado;
- dois frames `MSG_SendItem` emitidos na ordem destino, origem;
- estado salvo e restaurado no logout/desconexao.

**Status atual:** C3 — wire, dominio, dispatcher e testes deterministas implementados.  
**Proxima acao:** validar os dois `MSG_SendItem` no cliente/rede e fechar a evidencia de persistencia no logout/desconexao antes de elevar para C4/C5.

## Ficha ITEM-006 — `_MSG_UpdateItem`

### Referencia analisada

- `W2PP/Source/Code/Basedef.h:2209-2222`: estados `NOTHING=0`, `OPEN=1`, `CLOSED=2`, `LOCKED=3` e estrutura `MSG_UpdateItem`.
- `W2PP/Source/Code/TMSrv/ProcessClientMessage.cpp:180-182`: roteamento para `Exec_MSG_UpdateItem`.
- `W2PP/Source/Code/TMSrv/_MSG_UpdateItem.cpp:21-105`: validacoes, consumo de chave e abertura normal do portao.
- `W2PP/Source/Code/TMSrv/CCastleZakum.cpp:72-208`: fluxo especial de portoes do Castelo Zakum.
- `W2PP/Source/Code/TMSrv/CCastleZakum.cpp:314-343`: `ProcessSecTimer`, decremento a cada dois segundos e limpeza da arena ao chegar a zero.
- `W2PP/Source/Code/Basedef.h:2527`: `_MSG_StartTime = 161 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME`.
- `W2PP/Source/Code/TMSrv/SendFunc.cpp:300-309`: `_MSG_StartTime` usa `MSG_STANDARDPARM` com um `int Parm`.
- `Backup/Tools/Reference759/ClienteSource/Projects/TMProject/Basedef.h:2721-3010`: tabela estatica `g_pGroundMask[10][4][6][6]`; o servidor usa os indices 0..5 por `MAX_GROUNDMASK=6`.
- `W2PP/Source/Code/Basedef.cpp:5948-6002`: `BASE_UpdateItem`, que altera a altura/malha do terreno pela mascara do item.

### Contrato de wire

| Campo | Valor |
|---|---|
| opcode | `116 + 0x0200 + 0x0100 = 0x0374` |
| direcao | cliente -> servidor e servidor -> clientes da cena |
| header | 12 bytes |
| payload | 8 bytes |
| pacote total | 20 bytes |
| payload offset 0 | `int ItemID` — no servidor legado e `gateid + 10000` |
| payload offset 4 | `int State` |
| resposta | o proprio `MSG_UpdateItem`, multicast na posicao do item |

### Comportamento legado identificado

1. Rejeita personagem morto ou fora de `USER_PLAY`, envia `SendHpMode` e registra `AddCrackError`.
2. Rejeita `State` fora de 0..5 e `ItemID` fora de `10000..10000+MAX_ITEM-1`.
3. Converte `ItemID` para o indice do agregado global `pItem[]`.
4. Delega primeiro ao `CCastleZakum::OpenCastleGate`; chaves 10..14 exigem nivel/quest e podem iniciar a quest, remover mobs antigos, gerar mobs, enviar `_MSG_StartTime` ao lider/party e iniciar timer.
5. No caminho normal, quando o item esta `LOCKED` ou o cliente pede estado 3, procura no carry um item com `EF_KEYID` igual ao portao, consome a chave e envia `MSG_SendItem` vazio.
6. Chaves especiais podem ser suprimidas para o item de indice 773 (`pItem[gateid].ITEM.sIndex != 773`).
7. `UpdateItem` altera o estado/malha de terreno; somente se houver alteracao, o servidor envia o frame para a visao `GridMulticast`.
8. A operacao registra coordenadas do portao e nao envia confirmacao privada adicional no caminho normal.

### Dependencias ainda ausentes no C#

- o fluxo Castelo Zakum agora inicia por chave, valida `EF_QUEST`, remove mobs da arena, gera os NPCs catalogados, envia `_MSG_StartTime`, abre os portoes internos e expira a quest pelo timer de dois segundos;
- o `SendHpMode`/`AddCrackError` ainda e apenas resultado/log no caminho C#, sem o mesmo aviso legado;
- a tabela oficial de `g_pGroundMask` foi portada para `LegacyGroundMaskTable.CreateOfficial`; a injecao continua disponivel somente para fixtures de teste;
- nao existe persistencia de estado de portao nem timer de `Delay`/`Decay` no storage do mundo.

### Decisao de port

O contrato wire foi implementado em `WydCdk.Protocol.UpdateItemMessages` e `WydCdk.Protocol.StartTimeSignal`. O caminho normal e o ramo Zakum foram ligados ao `WorldHub` e ao dispatcher com carregamento opt-in por `--init-item`; o estado inicial e o update usam a mesma chave `ItemID = pItem slot + 10000`, o multicast usa a janela 33x33 do item, o timer autoritativo reproduz o `SecCounter % 2` do legado e o timer de minuto reproduz o fechamento estatico `Delay 0 -> 1 -> LOCKED`.

### Proxima acao

`LegacyMapItemCatalog`/`LegacyMapItemState`/`LegacyGroundMaskTable` estao integrados ao `WorldHub`; `--init-item` carrega o catalogo, o login envia `MSG_CreateItem` dos itens estaticos e o caminho Zakum valida `EF_QUEST`, gera a quest, envia `_MSG_StartTime`, abre os portoes internos, fecha portoes estaticos no timer de minuto, decai drops dinamicos com `MSG_DecayItem` e limpa a arena ao expirar. A leitura do legado confirmou a volatilidade de `pItem[]`; o proximo passo e validar o fluxo no cliente 7559/7600/7662.
