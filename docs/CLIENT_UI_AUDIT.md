# Auditoria inicial da UI do cliente

**Data:** 23/09/2026  
**Escopo:** inventário estático de `Client\7600` contra o perfil ativo `Client\7670`.  
**Estado:** nenhuma cópia ou substituição de asset foi feita.

## Regra de compatibilidade

A source TMProject 7.69 continua sendo a autoridade do cliente. Seus caminhos
confirmados em `Projects/TMProject/TMPaths.h` incluem:

- `UI\UITextureListN.bin`;
- `UI\UITextureSetList.txt`;
- `UI\SelServerScene2.txt`;
- `UI\EffectString.txt`, `UI\EffectSubString.txt` e `UI\GuildString.txt`.

Por isso, um arquivo com o mesmo nome ou extensão no 7600 não é automaticamente
compatível. O `SelServerScene.bin` exclusivo do 7600, por exemplo, não substitui
o recurso `SelServerScene2` usado pela source 7.69.

Nuance confirmada no código: `TMScene::LoadRC()` recebe o nome lógico
`SelServerScene2.txt` e troca a extensão para `.bin` antes de chamar
`ReadRCBin()`. Portanto, `Client\7670\UI\SelServerScene2.bin` é o arquivo
esperado pelo cliente compilado. O perfil também mantém variantes
`SelServerScene2fix.bin`, `SelServerScene2 (2).bin`, `SelServerScene3.bin` e
`SelServerSceneN.bin`; a escolha entre elas ainda precisa de validação visual e
de conteúdo, mas não autoriza copiar o `SelServerScene.bin` do 7600.

## Matriz de inventário

| Área | 7600 | 7670 | Em comum | Idênticos por SHA-256 | Diferentes por SHA-256 | Exclusivos 7600 | Exclusivos 7670 |
|---|---:|---:|---:|---:|---:|---:|---:|
| `UI` | 334 | 423 | 316 | 266 | 50 | 18 | 107 |
| `NUI` | 8 | 12 | 8 | 4 | 4 | 0 | 4 |

O `UITextureListN.bin` também não é intercambiável por tamanho: o arquivo do
7600 mede 135.168 bytes e o do 7670 mede 270.336 bytes. Ambos começam com a
entrada `UI\\cursor.wyt`, mas isso não prova equivalência do catálogo completo.

## Auditoria do catalogo e do formato

A source 7.69 define `stTextureListInfo` com 528 bytes e le ate 512 registros.
`TextureList_FileFormat` e `.wys`; nessa branch o carregamento trata `.wys` como
payload comprimido/DDS, enquanto `.wyt` segue o caminho de textura TGA/raw. A
source tambem le `itemicon.bin` para os indices de item. Portanto, os
`itemicon*.wyt` dos perfis 7559/7600/7662 nao substituem automaticamente os
`itemicon*.wys` do catalogo 7670.
O `Default.guimat` da arvore de referencia ainda cita alguns nomes `.wyt`, mas
ele nao substitui o contrato efetivamente consumido por `TextureManager`: para
o carregamento indexado, a autoridade e `UITextureListN.bin` junto de
`TextureList_FileFormat`.

Comparando os nomes do catalogo, normalizados por separador, e os hashes dos
arquivos realmente presentes:

| Perfil | Comuns | Iguais a base | Diferentes | Arquivo ausente | So no perfil | So na base |
|---|---:|---:|---:|---:|---:|---:|
| 7559 | 96 | 81 | 14 | 1 | 42 | 136 |
| 7600 | 99 | 80 | 16 | 3 | 17 | 133 |
| 7662 | 99 | 96 | 3 | 0 | 17 | 133 |

O 7670 possui 512 registros e 234 entradas nao vazias; 7559 possui 256/139,
enquanto 7600 e 7662 possuem 256/116. Os numeros de catalogo, extensao e hash
impedem tratar uma copia integral da UI antiga como adaptacao.

## Primeiro slice do launcher

O launcher próprio está em `Client\Launcher`. Ele lê `Interface=0..3`, valida
`ClientVersion=7670` no manifest e prepara `Client\7670\Runtime\InterfaceN`.
Cada runtime copia a base completa do 7670 e sobrepõe apenas os arquivos
existentes em `UI`/`NUI` do perfil selecionado; isso preserva fallback para
recursos que não existem nas árvores antigas. O processo sempre inicia o mesmo
`Client\7670\WYD.exe` e não copia executáveis históricos, SN ou serverlist.

Build e validação passaram sem warnings: os quatro IDs foram validados, os quatro
runtimes foram preparados e o smoke de processo abriu o mesmo EXE responsivo nos
perfis 7559, 7600, 7662 e 7670, sem o erro de render target. O manifesto agora
fixa os SHA-256 do `WYD.exe`, `sn.bin` e `serverlist.bin`, confere esses arquivos
no runtime e lista as dependências obrigatórias. Também bloqueia, por overlay,
catálogos e recursos estruturais ainda não auditados (`UITextureListN.bin`,
`UITextureSetList.txt`, `SelServerScene*.bin`, strings de contrato e backups).
Isso ainda não é aceite visual nem prova de equivalência de cada asset antigo.

Para 7559/7600/7662, a politica atual do launcher e `exact-base-or-approved`:
divergencias sao rejeitadas e apenas arquivos byte a byte iguais a base ou
aprovados explicitamente com SHA-256 no manifesto podem passar. No ultimo
prepare, foram rejeitados 129 overlays do 7559, 66 do 7600 e
55 do 7662; o perfil 7670 rejeitou zero. Cada runtime terminou com 435 arquivos
UI/NUI e zero diferencas de hash contra a base 7670.

A matriz detalhada esta em `CLIENT_UI_CANDIDATES.json` e pode ser regenerada por
`Server\tools\PortAudit\Compare-ClientInterfaceAssets.ps1`. O ultimo inventario
registrou 139 candidatos do 7559 (102 pendentes de aceite visual e 37 bloqueados,
sendo 25 por magic WYT legado), 72 do 7600 (65/7) e 59 do 7662 (52/7). Nenhum
esta aprovado no manifesto atual.
Uma aprovacao futura precisa registrar caminho exato, SHA-256 e justificativa;
o launcher rejeita wildcards, caminhos fora de UI/NUI e conflitos com exclusoes.

## Primeiro exame visual do 7662

As copias temporarias dos WYT foram reconstruidas com o loader 7.69: magic
`WT10`, cabecalho TGA tipo 2 e footer `TRUEVISION-XFILE`. Os tres candidatos
mantem a mesma geometria da base, mas o conteudo diverge:

| Asset | Geometria | Observacao | Decisao |
|---|---|---|---|
| `UI/logo2.wyt` | 256x256, 32 bpp | 7662 mostra Episode II; 7670 mostra Episode VI | pending_visual |
| `UI/efftest.wyt` | 525x105, 32 bpp | 7662 mostra uma grade colorida de teste; a base esta quase vazia | pending_visual |
| `UI/NewAmulOn.wyt` | 512x512, 24 bpp | folhas de icones diferentes entre 7662 e 7670 | pending_visual |

Nenhum desses tres assets foi aprovado automaticamente: geometria valida o
formato, mas nao decide branding, debug ou semantica visual do produto.

## Segundo exame visual do 7662

Este lote e mais proximo da base 7670 do que os primeiros candidatos do 7662:
os dois assets mantem 32 bpp, a mesma geometria e uma composicao quase igual.
Ainda assim, o catalogo 7662 nao lista esses caminhos, portanto o consumo real
precisa ser confirmado antes de qualquer overlay.

| Asset | Geometria | Observacao | Decisao |
|---|---|---|---|
| `NUI/main.wyt` | 616x300, 32 bpp | HUD praticamente coincidente com a base; 7662 nao exibe a mesma camada de labels de hotkey na parte inferior | pending_visual |
| `UI/Inventory2.wyt` | 229x512, 32 bpp | grade de equipamento/bau e controles coincidem; as diferencas ficam principalmente nos icones inferiores | pending_visual |

A proximidade visual torna `main` e `Inventory2` candidatos de baixo risco
relativo, mas nao substitui o smoke com o catalogo e o layout efetivamente
carregados pelo cliente 7670.

## Terceiro exame visual do 7662

`UI/NewAmul.wyt` e um WYT valido (`WT10`) de `512x512`, 8 bpp e footer
compativel com o loader 7.69. A copia 7662, porem, e uma atlas monocromatica
densa de efeitos e icones, com numeracao visivel nas linhas inferiores; a base
7670 contem apenas quatro icones claros no canto superior esquerdo e deixa o
restante da folha vazio. O catalogo 7662 tambem nao lista esse caminho, enquanto
o catalogo 7670 lista. Isso impede tratar a diferenca como simples melhoria
visual: o asset pode ser legado, nao utilizado pelo perfil 7662, ou depender de
coordenadas/semantica que ainda nao foram confirmadas.

| Asset | Geometria | Catalogo 7662 / 7670 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/NewAmul.wyt` | 512x512, 8 bpp | ausente / presente | atlas 7662 densamente preenchida contra a folha 7670 quase vazia; sem contrato de uso no 7662 | pending_visual |

O arquivo permanece somente como referencia visual. Nenhuma aprovacao foi
adicionada ao manifesto.

## Primeiro exame visual do 7559

O 7559 apresenta uma familia visual e uma escala diferentes em partes do
gameplay. `Inventory2.wyt` e `Skill2.wyt` usam atlas 2x, com dimensoes
`458x1024` e `512x1024`, e nao sao apenas versoes maiores das texturas 7670.

| Asset | Geometria 7559 | Catalogo 7559 | Observacao | Decisao |
|---|---|---|---|---|
| `NUI/main.wyt` | 616x300, 32 bpp | ausente | HUD muito proximo do 7662/7670 e sem a mesma camada de labels de hotkey; consumo pelo catalogo 7559 nao foi confirmado | pending_visual |
| `UI/CommonBox.wyt` | 512x512, 32 bpp | presente | caixa minimalista com borda azul/dourada e sem a composicao escura/cobre da base, que tambem possui scrollbar | pending_visual |
| `UI/Inventory2.wyt` | 458x1024, 32 bpp | ausente | atlas 2x com equipamento, slots e icones em composicao diferente; nao e um simples redimensionamento do 229x512 do 7670 | pending_visual |
| `UI/Skill2.wyt` | 512x1024, 32 bpp | presente | atlas 2x azul/dourada com distribuicao de blocos e slots diferente da tela 256x512 do 7670 | pending_visual |

O 7559 fica, neste recorte, como referencia historica de layout e nao como
fonte segura de overlay para a release unica. Nenhum dos quatro assets foi
adicionado ao `overlayApprovedFiles`.

## Segundo exame visual do 7559

O segundo lote confirma a mesma ruptura de escala e de familia visual. As
texturas 7559 sao 32 bpp e preservam grades reconheciveis, mas usam atlas 2x,
bordas azul/douradas e controles proprios; a equivalencia de quantidade de
slots nao basta para reutilizar os arquivos no 7670.

| Asset | Geometria 7559 | Catalogo 7559 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/NewCharacter2.wyt` | 512x1024, 32 bpp | ausente | tela de personagem 2x com paineis azul/dourados e divisao diferente da tela 256x512 do 7670 | pending_visual |
| `UI/NewItemMix.wyt` | 710x830, 32 bpp | ausente | grades de mix preservadas em escala maior, mas com moldura e controles antigos; nao e overlay direto do 355x415 do 7670 | pending_visual |
| `UI/Storage2.wyt` | 468x898, 32 bpp | presente | grade vertical 2x e controles inferiores com familia azul/dourada, diferente da moldura escura/cobre do 7670 | pending_visual |
| `UI/Store2.wyt` | 470x936, 32 bpp | ausente | mesma grade de loja do 7559, sem os campos e botoes visiveis da composicao 7670 | pending_visual |

Esses quatro assets exigiriam uma adaptacao de escala, aliases e layout, nao
uma simples selecao de arquivo. Nenhum foi adicionado ao
`overlayApprovedFiles`.

## Terceiro exame visual do 7559

Este lote mostra que proximidade de pixels tambem pode esconder divergencia de
contrato. `mainchat.wyt` e visualmente muito proximo, mas esta em `UI/` no 7559
e em `NUI/` no 7670; `Help02.wyt` exibe teclas diferentes; e `SkillMaster2`
continua sendo uma atlas 2x.

| Asset | Geometria 7559 | Catalogo 7559 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/SkillMaster2.wyt` | 512x1024, 32 bpp | presente | blocos de skill em escala 2x, com moldura azul/dourada e sem a mesma area de descricao da base 256x512 | pending_visual |
| `UI/mainchat.wyt` | 510x58, 24 bpp | presente | arte quase igual a `NUI/mainchat.wyt` do 7670, mas o caminho e a area de catalogo mudam; exige mapeamento explicito | pending_visual |
| `UI/Help02.wyt` | 512x256, 32 bpp | presente | atlas de teclas troca comandos visiveis, como A/R/T no 7559 contra Y e outra distribuicao no 7670 | pending_visual |
| `UI/LoginLock.wyt` | 314x228, 32 bpp | presente | mesma caixa de dez slots e tres botoes, mas 7559 usa moldura azul/dourada e 7670 usa escura/cobre | pending_visual |

Nenhum dos quatro foi adicionado ao `overlayApprovedFiles`; mesmo o
`mainchat` precisa de adapter de caminho e validacao do layout consumidor.

## Quarto exame visual/formato do 7559

`GameGrade` e `jackpotA` repetem o conteudo historico visto no 7600, com
arranjos e icones diferentes dos equivalentes 7670. Ja `NewAmul.wyt` e
`NewAmulOn.wyt` nem sequer usam o magic WYT esperado pela source 7.69: os
arquivos 7559 iniciam com `00 4B 6D 4B`, enquanto a base inicia com `WT10`.

| Asset | Evidencia 7559 | Observacao | Decisao |
|---|---|---|---|
| `UI/GameGrade.wyt` | 206x120, 32 bpp, `WT10` | duas placas lado a lado no 7559; o 7670 rearranja as placas e troca o conteudo visual | pending_visual |
| `UI/jackpotA.wyt` | 256x256, 24 bpp, `WT10` | atlas de premios com icones diferentes; nao e uma simples troca de paleta | pending_visual |
| `UI/NewAmul.wyt` | 512x512, extensao `.wyt`, magic `00 4B 6D 4B` | formato legado nao aceito diretamente pelo leitor WYT 7.69; conversao visual foi deliberadamente interrompida | blocked_non_runtime |
| `UI/NewAmulOn.wyt` | 512x512, extensao `.wyt`, magic `00 4B 6D 4B` | mesma ruptura de formato; requer adapter antes de qualquer comparacao visual confiavel | blocked_non_runtime |

Os dois ultimos permanecem material de referencia ate existir um parser/adapter
versionado. Nenhum dos quatro foi adicionado ao `overlayApprovedFiles`.

## Quinto exame visual do 7559: ajuda restante e efeito de teste

Os dois atlas de ajuda restantes repetem os comandos antigos ja vistos no
7559/7600, enquanto `efftest.wyt` aparece em escala 2x e continua sendo uma
grade de teste, nao uma textura de produto.

| Asset | Geometria 7559 | Catalogo 7559 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/Help01.wyt` | 512x256, 32 bpp | ausente | mostra F1-F10 e a distribuicao de teclas do perfil antigo, diferente do atlas 7670 | pending_visual |
| `UI/Help03.wyt` | 512x256, 32 bpp | ausente | inclui F1-F4 e outras teclas em posicoes que nao correspondem ao conjunto 7670 | pending_visual |
| `UI/efftest.wyt` | 1050x210, 32 bpp | presente | grade colorida em escala 2x, com padrao de teste diferente do `efftest` 525x105 do 7670 | pending_visual |

`Help01` e `Help03` dependem do mapa de comandos e `efftest` permanece
debug-only ate prova de uso. Nenhum dos tres foi adicionado ao
`overlayApprovedFiles`.

## Sexto exame visual do 7559: gameplay e selecao

O proximo grupo mantem a ruptura azul/dourada do 7559. Quatro atlas usam
dimensoes 2x ou composicoes proprias; `m0104.wyt` e menor e visualmente proximo,
mas troca 32 bpp no 7559 por 24 bpp na base e ainda nao tem consumidor
confirmado na source.

| Asset | Geometria 7559 | Catalogo 7559 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/m0104.wyt` | 128x128, 32 bpp | ausente | simbolo/atlas pequeno visualmente proximo ao 7670, mas com diferencas internas e base em 24 bpp | pending_visual |
| `UI/AutoTrade.wyt` | 220x199, 32 bpp | ausente | grade de autotrade reconhecivel, mas moldura azul/dourada e campos superiores diferem do painel escuro/cobre | pending_visual |
| `UI/character2.wyt` | 512x1024, 32 bpp | ausente | atlas 2x com paineis de personagem em composicao diferente da tela 256x512 do 7670 | pending_visual |
| `UI/Combine104.wyt` | 228x313, 32 bpp | ausente | fluxo geral de combinacao permanece, mas 7559 usa painel azul/dourado e a base usa moldura ornamentada/marrom | pending_visual |
| `UI/SelCharBG2.wyt` | 512x1024, 32 bpp | presente | tela de selecao 2x com divisao e controles diferentes do atlas 256x512 do 7670 | pending_visual |

Nenhum dos cinco foi adicionado ao `overlayApprovedFiles`; `m0104` ainda
precisa de mapeamento de consumidor antes de qualquer smoke.

## Setimo exame visual do 7559: lista de servidores

O 7559 possui `UI/ServerList.wyt`, enquanto o cliente 7670 usa o caminho
`NUI/ServerList.wyt`. A comparacao abaixo e, portanto, uma correspondencia
visual/logica, nao uma autorizacao para renomear ou copiar o arquivo. Os dois
WYT tem `287x280`, 32 bpp e formato `WT10`, mas pertencem a familias visuais
distintas: o 7559 usa linhas simples em painel azul/preto, e o 7670 usa dois
paineis marrom/cobre ornamentados, com mais linhas e botoes inferiores.

| Asset 7559 | Equivalente logico 7670 | Catalogo 7559 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/ServerList.wyt` | `NUI/ServerList.wyt` | presente | mesma geometria e bpp, mas caminho diferente e composicao visual incompatível com uma troca direta | pending_visual |

O launcher nao cria esse remapeamento entre `UI` e `NUI`; nenhum dos dois
arquivos foi adicionado ao `overlayApprovedFiles`.

## Oitavo exame visual do 7559: partes do HUD

`UI/mainparts.wyt` e um caso visualmente proximo: a composicao 7559 preserva
os mesmos quadros, icones, barras, esferas e botoes da copia homonima existente
no 7670. A diferenca principal e estrutural: o 7559 usa uma atlas 2x de
`1024x512`, 24 bpp, enquanto o arquivo 7670 mede `512x256`, 32 bpp. Apesar da
proximidade apos reducao, nenhum dos dois aparece no catalogo
`UITextureListN.bin` ativo do 7670, e a source nao confirmou seu consumidor
direto.

| Asset 7559 | Contraparte por caminho | Geometria/formato | Observacao | Decisao |
|---|---|---|---|---|
| `UI/mainparts.wyt` | `UI/mainparts.wyt` | 1024x512, 24 bpp -> 512x256, 32 bpp | composicao praticamente equivalente em escala 2x, mas catalogo e formato impedem troca direta | pending_visual |

O arquivo permanece como evidencia de compatibilidade visual historica; nao foi
adicionado ao `overlayApprovedFiles` nem ao runtime 7670.

## Nono exame visual do 7559: login legado

`UI/NewLogin.wyt` e um WYT valido de `2000x2000`, 32 bpp, listado no catalogo
7559, mas ausente na arvore e no catalogo ativos do 7670. A conversao visual
mostra somente tres paineis pequenos espalhados no canto superior esquerdo de
um canvas quase vazio; nao ha uma tela de login completa nem correspondencia
direta com os recursos de login do cliente alvo. A busca textual na source 7.69
nao encontrou consumidor para `NewLogin`.

| Asset | Catalogo 7559 / 7670 | Geometria | Observacao | Decisao |
|---|---|---|---|---|
| `UI/NewLogin.wyt` | presente / ausente | 2000x2000, 32 bpp | folha legada esparsa, sem contraparte ou consumidor confirmado no alvo | pending_visual |

Este arquivo fica somente como evidencia historica. Nao foi adicionado ao
`overlayApprovedFiles` nem deve ser usado para compor o login 7670.

## Decimo exame visual do 7559: painel de servidor e donate legado

`UI/ServerInfo.wyt` e um WYT valido de `2048x2048`, 32 bpp, presente no
catalogo 7559, mas ausente no 7670. A folha combina dois usos: paineis vazios
de informacao/listagem na parte superior e, na parte inferior, cards de itens,
promocao `DONATE`, precos em diamantes e publicidade de evento. A source 7.69
nao possui referencia textual para `ServerInfo`, e nao ha contraparte no
catalogo ativo do cliente alvo.

| Asset | Catalogo 7559 / 7670 | Geometria | Observacao | Decisao |
|---|---|---|---|---|
| `UI/ServerInfo.wyt` | presente / ausente | 2048x2048, 32 bpp | mistura painel legado com loja/promocao donate e nao tem consumidor confirmado no alvo | pending_visual |

O conteudo donate permanece fora do cliente 7670 conforme a decisao comercial
do projeto: compra e saldo pertencem ao Site, e nao a um painel/NPC in-game.
Nenhum overlay foi aprovado.

## Primeiro exame visual do 7600

Os quatro candidatos abaixo foram convertidos temporariamente para PNG usando o
mesmo caminho de leitura WYT da source 7.69. Todos preservam a geometria e o
tamanho de arquivo esperados, mas a diferenca visual e estrutural impede uma
aprovacao por hash ou por dimensao:

| Asset | Geometria | Observacao | Decisao |
|---|---|---|---|
| `NUI/ServerList.wyt` | 287x280, 32 bpp | 7600 usa moldura bronze/madeira e acabamento dourado; 7670 usa dois paineis escuros com acabamento cobre e botoes diferentes | pending_visual |
| `UI/CommonBox.wyt` | 512x512, 32 bpp | 7600 e uma caixa ornamentada clara, com fundo texturizado e emblema central; 7670 e uma caixa escura com cabecalho cobre e area interna diferente | pending_visual |
| `UI/cursor.wyt` | 256x256, 32 bpp | 7600 contem uma folha extensa de cursores, icones e estados; 7670 contem uma folha bem mais esparsa, com outro conjunto de estados | pending_visual |
| `UI/SelCharBG2.wyt` | 256x512, 32 bpp | 7600 traz o painel de selecao completo em marrom/dourado; 7670 traz paineis escuros/cobre com composicao distinta | pending_visual |

Esses arquivos parecem pertencer a familias visuais diferentes e alguns podem
ser consumidos em conjunto com layouts ou coordenadas especificas. Nenhum foi
adicionado a `overlayApprovedFiles`; o runtime oficial continua usando apenas
os assets-base do 7670.

## Segundo exame visual do 7600: inventario, comercio e trade

O segundo lote foi escolhido por representar paineis de gameplay que a source
7.69 lista no `Default.guimat`. A geometria das grades permanece compativel,
mas ha uma diferenca de formato que precisa ser tratada como dependencia: os
quatro primeiros assets do 7600 sao 24 bpp, enquanto os equivalentes da base
7670 sao 32 bpp. `trade.wyt` e 32 bpp nos dois perfis.

| Asset | Geometria 7600 | Catalogo 7600 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/AutoTrade.wyt` | 220x199, 24 bpp | ausente | 7600 tem moldura clara/dourada, oito slots texturizados e o botao de moeda; 7670 tem o mesmo desenho geral em painel escuro/cobre | pending_visual |
| `UI/Inventory2.wyt` | 229x512, 24 bpp | ausente | slots, equipamento, bau e indicadores ocupam as mesmas regioes; a skin 7600 e clara/marrom e a base e escura/cobre | pending_visual |
| `UI/Storage2.wyt` | 234x449, 24 bpp | presente | grade vertical e controles inferiores coincidem visualmente, mas o 7600 usa textura dourada e o 7670 usa fundo escuro | pending_visual |
| `UI/Store2.wyt` | 235x468, 24 bpp | ausente | grade de loja e botoes inferiores preservam a composicao, com troca completa de paleta e moldura | pending_visual |
| `UI/trade.wyt` | 225x377, 32 bpp | presente | duas grades de oferta, campos de moeda e botoes de confirmacao mantem a mesma geometria; somente a identidade visual diverge fortemente | pending_visual |

A coincidencia de coordenadas e promissora para `trade.wyt`, mas nao substitui
o smoke no cliente: a textura pode depender de aliases, transparencia e outros
assets de moldura. Os quatro arquivos 24 bpp tambem exigem confirmar o alpha e
o caminho de upload do renderer antes de qualquer aprovacao. Nenhum foi incluido
em `overlayApprovedFiles`.

## Terceiro exame visual do 7600: NUI e personagem

Este lote cobre o HUD, o chat e a atlas usada por paineis de personagem. O
`mainchat.wyt` preserva 24 bpp nos dois perfis; os demais preservam 32 bpp e a
geometria geral, mas continuam dependentes de aliases e coordenadas do layout.

| Asset | Geometria 7600 | Catalogo 7600 | Observacao | Decisao |
|---|---|---|---|---|
| `NUI/main.wyt` | 616x300, 32 bpp | ausente | 7600 traz HUD dourado com molduras ornamentadas; 7670 usa moldura escura/cobre, hotkeys explicitos e paineis internos diferentes, embora a atlas geral coincida | pending_visual |
| `NUI/mainchat.wyt` | 510x58, 24 bpp | presente | ambos mantem a barra horizontal de chat; 7600 tem acabamento mais carregado e 7670 separadores mais simples | pending_visual |
| `NUI/popup.wyt` | 512x512, 32 bpp | ausente | as mesmas familias de caixas, botoes e listas aparecem nas mesmas regioes; 7600 e dourado/marrom e 7670 e escuro/cobre | pending_visual |
| `UI/character2.wyt` | 256x512, 32 bpp | ausente | os paineis de personagem ocupam regioes equivalentes, mas a textura 7600 usa madeira e metal claro e a base usa paineis escuros/cobre | pending_visual |

O fato de `main`, `popup` e `character2` nao estarem listados no catalogo 7600
nao impede que existam como candidatos, mas reduz a evidencia de que eram
consumidos por aquela release. Nenhum desses quatro assets foi adicionado ao
manifesto; a escolha depende do smoke no cliente 7670 com os layouts reais.

## Quarto exame visual do 7600: personagem, mix e skills

O lote de progressao separa dois casos de formato. `Skill2.wyt` e
`SkillMaster2.wyt` sao 32 bpp e aparecem nos dois catalogos, enquanto
`NewCharacter2.wyt` e `NewItemMix.wyt` sao 24 bpp no 7600 e 32 bpp na base 7670.

| Asset | Geometria 7600 | Catalogo 7600 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/NewCharacter2.wyt` | 256x512, 24 bpp | ausente | a tela 7600 usa madeira, barras ornamentadas e uma composicao diferente; a base 7670 usa dois paineis escuros empilhados | pending_visual |
| `UI/NewItemMix.wyt` | 355x415, 24 bpp | ausente | grades de materiais e campos de resultado ocupam regioes equivalentes, mas 7600 e dourado/marrom e 7670 e escuro/cobre | pending_visual |
| `UI/Skill2.wyt` | 256x512, 32 bpp | presente | quatro blocos de habilidades com a mesma distribuicao de slots; muda a moldura, a textura e a paleta | pending_visual |
| `UI/SkillMaster2.wyt` | 256x512, 32 bpp | presente | tres blocos de habilidades e a area de descricao preservam a composicao; 7600 usa acabamento ornamentado e 7670 acabamento escuro/cobre | pending_visual |

`Skill2` e `SkillMaster2` sao os candidatos tecnicamente mais limpos deste
lote, mas ainda dependem de aliases, icones e textos que nao foram validados
no cliente compilado. Nenhum dos quatro foi adicionado a
`overlayApprovedFiles`.

## Quinto exame visual do 7600: portal, graduacao e amuletos

Este lote trouxe um candidato com compatibilidade de formato mais clara:
`NewAmulOn.wyt` tem 512x512, 24 bpp e aparece nos dois catalogos. A textura
`NewAmul.wyt` e a variante monocromatica de 8 bpp e deve ser tratada como
possivel complemento, nao como substituta isolada.

| Asset | Geometria 7600 | Catalogo 7600 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/PotalUI.wyt` | 512x256, 24 bpp | ausente | a moldura e as duas acoes ocupam regioes equivalentes, mas o 7600 e dourado/marrom e o 7670 e escuro/cobre; a base usa 32 bpp | pending_visual |
| `UI/GameGrade.wyt` | 206x120, 32 bpp | presente | ambos exibem placas de classificacao, mas o 7600 organiza dois avisos lado a lado e o 7670 rearranja os avisos em outra composicao | pending_visual |
| `UI/NewAmul.wyt` | 512x512, 8 bpp | ausente | atlas monocromatica de efeitos e icones, com a mesma familia de numeracao mas diferente evidencia de catalogo; pode ser mascara/variante da folha colorida | pending_visual |
| `UI/NewAmulOn.wyt` | 512x512, 24 bpp | presente | a grade e o conjunto geral de icones coincidem em grande parte; ha diferencas pontuais de arte e paleta entre 7600 e 7670 | pending_visual |

`NewAmulOn` e o melhor candidato deste lote para um smoke isolado, mas a
dependencia de `NewAmul` e de coordenadas dos icones ainda precisa ser provada.
Nenhum dos quatro foi adicionado ao `overlayApprovedFiles`.

## Sexto exame visual do 7600: ajuda de teclas e bloqueio

Os atlas de ajuda carregam mais do que decoracao: eles exibem combinacoes de
teclas e, portanto, precisam acompanhar os comandos realmente aceitos pelo
cliente 7670. Nos tres arquivos de ajuda, o 7600 usa 32 bpp e a base usa 24
bpp; `LoginLock` apresenta a diferenca inversa, com 24 bpp no 7600 e 32 bpp na
base.

| Asset | Geometria 7600 | Catalogo 7600 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/Help01.wyt` | 512x256, 32 bpp | ausente | atlas de teclas diverge nos comandos visiveis, incluindo F1-F10 no 7600 e outra distribuicao no 7670 | pending_visual |
| `UI/Help02.wyt` | 512x256, 32 bpp | presente | ambos mostram teclas de movimento/atalho, mas os conjuntos e a distribuicao de letras nao sao iguais | pending_visual |
| `UI/Help03.wyt` | 512x256, 32 bpp | ausente | 7600 e 7670 divergem na presenca e posicao de F1-F4 e em outras teclas de navegacao | pending_visual |
| `UI/LoginLock.wyt` | 314x228, 24 bpp | presente | mesma caixa de dez slots e tres botoes, mas 7600 usa moldura dourada/marrom e 7670 usa moldura escura/cobre | pending_visual |

Os arquivos de ajuda nao devem ser aprovados apenas por geometria: a arte deve
ser coerente com o mapa de comandos do cliente compilado. `LoginLock` tambem
precisa de smoke para confirmar o alpha apos a troca de bpp. Nenhum dos quatro
foi adicionado ao `overlayApprovedFiles`.

## Setimo exame visual do 7600: feedback, jackpot, branding e teste

Este lote nao e uma troca puramente cosmetica. `logo2.wyt` carrega branding e
episodio, `jackpotA.wyt` carrega icones de premios, e `efftest.wyt` aparenta ser
uma grade de teste de efeitos. `charmsg.wyt` existe no 7600, mas nao aparece no
catalogo desse perfil.

| Asset | Geometria 7600 | Catalogo 7600 | Observacao | Decisao |
|---|---|---|---|---|
| `UI/charmsg.wyt` | 290x18, 32 bpp | ausente | 7600 mostra uma barra ornamentada; a base aparece praticamente vazia/escura e usa a mesma geometria, mas o consumidor ainda nao foi confirmado | pending_visual |
| `UI/jackpotA.wyt` | 256x256, 24 bpp | presente | mesma atlas em quatro colunas, mas os icones de premio sao semanticamente diferentes entre 7600 e 7670 | pending_visual |
| `UI/logo2.wyt` | 256x256, 32 bpp | presente | 7600 identifica Episode II/Fervor of Championship; 7670 identifica Episode VI/Winds of Change | pending_visual |
| `UI/efftest.wyt` | 525x105, 32 bpp | presente | grade de quadrados coloridos, sem conteudo de produto; forte indicio de asset de teste/debug nos dois perfis | pending_visual |

`logo2` exige uma decisao de branding do produto, `jackpotA` exige revisar a
origem dos premios e `efftest` nao deve entrar na release sem provar uso real.
Nenhum dos quatro foi adicionado ao `overlayApprovedFiles`.

## Candidatos exclusivos do 7600

Estes arquivos são candidatos para inspeção individual, não itens aprovados para
serem copiados:

- `UI\BuyConfirm.wyt` — 485.120 bytes;
- `UI\donatestore.wyt` — 420.312 bytes;
- `UI\GamePainel.wyt` — 4.000.048 bytes;
- `UI\Shop.wyt` — 3.027.632 bytes;
- `UI\itemicon.wyt` e `UI\itemicon01.wyt` até `UI\itemicon10.wyt`;
- `UI\SelServerScene.bin` — 1.648 bytes;
- `UI\UITextureListN.bin.donate-original.bak` e `UI\vssver.scc`.

Os arquivos `*.bak` e `*.scc` ficam fora de qualquer seleção de release. Os
arquivos de loja, donate e ícones exigem primeiro uma referência de uso na source
7.69 e uma validação visual no cliente 7670; o inventário não autoriza importar
Donate Shop para dentro do cliente.

## Candidatos 7600 sem contrato ativo na source 7.69

A busca na source e no `Default.guimat` não encontrou consumidor para
`BuyConfirm.wyt`, `donatestore.wyt`, `GamePainel.wyt` ou `Shop.wyt`. Eles ficam
registrados como material histórico do 7600, mas não como overlays visuais do
cliente 7670. `donatestore.wyt` também não pode ser promovido por aparência: a
decisão comercial vigente mantém Cash/Donate no Site e deixa o Donate Shop
in-game fora da release.

Os `itemicon*.wyt` também não são equivalentes diretos ao contrato do alvo. A
source 7.69 lê `itemicon.bin`, transforma o índice em `UI/ItemIcon%02d.wys` e
seleciona um recorte de 100x100; o `Default.guimat` só cita um
`UI/itemicon10.wyt` legado. Portanto, os WYT 7600 não devem substituir os
catálogos WYS nem o mapeamento `itemicon.bin`. Qualquer migração futura teria
que ser um adapter de formato e índice, com fixture e smoke próprios.

O mesmo gate de magic agora classifica como `blocked_non_runtime` os 25 WYT
7559 que começam com `00 4B 6D 4B`, incluindo `filtrodrop`, `itemicon01..08`,
`itemicon11..12`, `logo021`, `loja`, `MenuDaily`, `menudrop`, `NewAmul`,
`NewAmulOn`, `NewBossTauron`, `newmain`, `newQuetsDay`, `newshop`, `panelgrupo`,
`panelrank`, `QuestIcon` e `storecash`. Eles não são pendências visuais do
leitor WYT 7.69 até existir um parser/adapter versionado.

## Decisão atual

Não copiar a pasta `UI` ou `NUI` do 7600. O perfil 7670 mantém seus catálogos,
strings e layouts próprios. A próxima seleção deve registrar, para cada asset,
formato, hash, caminho consumidor na source, dependências e resultado do smoke
visual. Até essa validação, os 7600 permanecem somente em `Client\7600` como
referência local.
