# Implementation Plan: matriz de estado das duas barras de atalhos

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fechar, em loopback descartável, a matriz de estado que separa os 20 atalhos por personagem, a página selecionada, `LearnedSkill[1]`, `MOBEXTRA.SecLearnedSkill` e posição/cena, antes de mudar aquisição ou persistência de skills.

**Architecture:** Reutilizar a fixture e o proxy E2E existentes para controlar o estado antes do login, observar os pacotes `0x0114` e `0x0337`, e conferir o blob do personagem depois do logout. Manter o domínio, wire de produção e schema sem alterações salvo se a evidência e um plano revisado justificarem isso depois.

**Tech stack:** C# / .NET 10 (`10.0.401`), harness executável `WydCdk.Engine.Tests`, fixture `WydCdk.Client.E2E`, proxy de wire local, cliente isolado TMProject 7.69 e arquivos de persistência descartáveis.

**Approved design:** [`../specs/2026-10-28-second-hotbar-state-matrix-design.md`](../specs/2026-10-28-second-hotbar-state-matrix-design.md)

## Restrições globais

- Preservar o comportamento 7.69: `Z` alterna as grades localmente e o login sempre começa na página 1, com o índice selecionado em 0. Não adicionar estado de página ao wire ou à persistência.
- Tratar os 20 atalhos como `MOB.SkillBar[4] + CNFCharacterLogin.ShortSkill[16]`; não confundir atalhos com `LearnedSkill[0]`, `LearnedSkill[1]` ou `MOBEXTRA.SecLearnedSkill`.
- Fixar personagem, classe, word base, atalhos e coordenada de entrada enquanto variar os estados da matriz. Registrar a coordenada efetiva recebida em `0x0114`.
- Separar o spawn normal da cidade e o spawn de guilda. O usuario lembra `(2100,2100)` como centro normal de Armia depois do nivel 35; a source usa o centro configurado da cidade com `rand()%15`, por isso a fixture desta rodada controla a guilda como dona da zona e registra o GuildSpawn. O usuario confirmou que esse e o caso da area de guilda ao conquistar a cidade.
- Usar somente contas/personagens sintéticos, dados descartáveis e loopback. Não usar Host autenticado, Site, produção ou VPS.
- Não alterar runtime/schema, copiar Extra para o word adicional, inferir crosswalk por nome de livro nem portar aquisição do livro 5504 nesta etapa.
- Uma cena inválida (incluindo `Field0000.trn` ou erro de Critical Data) torna a observação visual daquela tentativa inconclusiva; bytes capturados ainda podem ser registrados separadamente.
- Preservar alterações não relacionadas da working tree. Sem commit, push, publicação ou deploy.
- Rodar verificações em série e em saída isolada quando aplicável. Se o sandbox bloquear loopback (`WSA10013`), registrar a limitação sem transformar falha de permissão em conclusão sobre o protocolo.

## Task 1 — Fortalecer a regressão de atalhos por personagem

**Files:**
- Modify: `Server/tests/WydCdk.Engine.Tests/Program.cs` — `ShortSkillWireAndPersistence`

- [x] **Step 1: Escrever primeiro o caso de regressão.** Na rotina `ShortSkillWireAndPersistence`, preparar dois slots de personagem da mesma conta com vetores distintos e não nulos de 20 atalhos: quatro bytes no `MOB.SkillBar` e 16 bytes em `ShortSkill`. Fazer o vetor de cada slot ser inequivocamente diferente do outro.
- [x] **Step 2: Executar a regressão antes de qualquer ajuste.** Na pasta `Server`, compilar em saída isolada com `dotnet build tests/WydCdk.Engine.Tests/WydCdk.Engine.Tests.csproj --no-restore -m:1 -p:BaseOutputPath=bin\CodexVerify\ -t:Rebuild`, executar a DLL gerada com `TEMP`/`TMP` no scratch e confirmar que uma mutação que grava o slot 1 no offset do slot 0 faz a asserção falhar.
- [x] **Step 3: Exercitar os caminhos de persistência já cobertos pelo teste.** Salvar `0x0378` para cada slot, aplicar em seguida um snapshot MOB antigo/stale e o caminho de save atômico; reler ambos com `ReadCharacterLoginDataAsync` e comparar todos os 20 bytes, slot a slot.
- [x] **Step 4: Corrigir somente se a regressão revelar defeito.** Manter a correção restrita ao caminho de persistência responsável; não mudar o formato do blob nem adicionar estado de página. Repetir o teste e confirmar que nenhum byte de um slot aparece no outro.
- [x] **Step 5: Confirmar o harness completo.** Reexecutar a DLL da Task 1 com loopback permitido e registrar contagem final, incluindo o resultado do caso novo.

## Task 2 — Tornar login e refresh observáveis na fixture descartável

**Files:**
- Modify if necessary: `Server/tests/WydCdk.Client.E2E/SkillSecondBarVisualFixture.cs`
- Modify if necessary: `Server/tests/WydCdk.Client.E2E/SkillSecondBarWireProxy.cs`
- Modify if necessary: `Server/tests/WydCdk.Client.E2E/Program.cs`

- [x] **Step 1: Verificar primeiro os logs da fixture existente.** Os logs confirmam word base, `Extra`, slot/personagem, posição, atalhos e words antigo/enviado de login e refresh.
- [x] **Step 2: Acrescentar somente observabilidade que estiver faltando.** O proxy registra `0x0114`, `0x0337`, slot/personagem e posição; os bytes alterados se limitam a `LearnedSkill[1]` e checksum.
- [x] **Step 3: Validar a mudança do harness.** Build serial isolada da solução e build E2E concluídos; o Engine harness passou com loopback permitido.
- [x] **Step 4: Conferir a captura de referência.** Baseline correlacionado. Os atalhos persistidos da fixture permaneceram sem prova de save visual: nenhum `0x0378` ocorreu no smoke headless.

## Task 3 — Executar a matriz independente de `LearnedSkill[1]` e Extra

**Files/artifacts:**
- Use: `Server/tests/WydCdk.Client.E2E/SkillSecondBarVisualFixture.cs`
- Use: `Server/tests/WydCdk.Client.E2E/SkillSecondBarWireProxy.cs`
- Add evidence: `Backup/Build/SkillSecondBarMatrix-20261028/` (somente fixture local descartável)

- [x] **Step 1: Fixar o baseline e validar entrada.** Baseline headless com `LearnedSkill[0]=0x80`, classe, personagem/slot, atalhos e GuildSpawn fixos; confirmou `0x0114`/`0x0337`. O harness concluiu login/personagem/logout; a cena 3D não foi aberta.
- [x] **Step 2: Variar apenas o word adicional.** Testados `LearnedSkill[1]=1`, `2`, `0x10` e `0x100`, mantendo `Extra=0x10`; logs por variante preservados.
- [x] **Step 3: Variar apenas Extra.** Testados `Extra=0` e `0x100` com `LearnedSkill[1]=0x10`, sete combinacoes no total.
- [x] **Step 4: Não generalizar uma falha de cena.** Como as variantes foram headless, a cena/UI por variante foi registrada como não observada; nenhum erro visual foi generalizado.
- [x] **Step 5: Resumir o que a matriz prova.** Evidência separa login, refresh, Extra, atalhos, logout e limites; não prova save/reload, UI por variante ou crosswalk.

## Task 4 — Verificar atalhos, relogin e página inicial

**Files/artifacts:**
- Use: fixture descartável da Task 3 (um personagem para a sessão visual)
- Use: regressão multi-personagem da Task 1 para isolamento entre slots
- Add evidence: `Backup/Build/SkillSecondBarMatrix-20261028/`

- [ ] **Step 1: Preparar um personagem visual com 20 atalhos distintos.** Pendente: a fixture visual usou o personagem existente; os quatro drops manuais salvos usam o mesmo código `07`, então ainda não há captura visual com 20 valores distinguíveis em `0x0114`. A prova de isolamento entre personagens fica no teste de armazenamento da Task 1, sem depender de a fixture visual suportar seleção de múltiplos personagens.
- [x] **Step 2: Exercitar as duas páginas.** A observação visual confirmou página 1, `Z` para página 2 e retorno à página 1. O usuário confirmou que arrastou uma skill do painel `S` mirando os espaços 1–4; os ícones ficaram em 1, 2, 3 e 5. Depois, esclareceu que vários componentes da interface também estão fora do lugar; a investigação deve considerar o layout global, não somente a grade de atalhos. Nenhum ID/crosswalk foi inferido.
- [x] **Step 3: Salvar e reler os atalhos.** O servidor registrou quatro `Short skills saved` para TESTE/slot 0; a releitura do blob encontrou SkillBar `FFFFFFFF` e ShortSkill `FFFFFFFFFFFF070707FF07FFFFFFFFFF`, com bytes `07` nas posições globais 10, 11, 12 e 14. A tentativa do usuário no espaço 4 foi salva no espaço 5. A regressão Engine cobre todos os 20 bytes, stale MOB e save atômico. O proxy preservou o fluxo cliente→servidor cru, então o payload bruto de `0x0378` não foi capturado.
- [ ] **Step 4: Conferir estado visual depois do relogin.** Pendente: a source inicializa `m_bSkillBeltSwitch=0` e o índice selecionado 0, mas a sessão ainda não relogou depois de usar `Z`; confirmar visualmente quando houver o personagem com atalhos distinguíveis. Não adicionar campo persistente se o reset ocorrer como na referência.
- [x] **Step 5: Capturar as incertezas restantes.** A autoria dos quatro arrastes foi confirmada pelo usuário. A divergência 4→5 e o relato de vários componentes fora de posição mantêm aberta uma hipótese de escala/resolução no layout global; isso ainda não é causa comprovada. Antes de repetir drops, medir resolução configurada, DPI/área cliente e posições dos controles, e depois comparar com uma segunda resolução na fixture isolada. A source usa largura de célula inteira no hit-test, passos flutuantes na renderização e ratios calculados na inicialização; dimensões reais dos controles e arte ainda não foram medidas. Também permanecem pendentes payload bruto, logout/relogin e vetor com 20 valores distintos. As tentativas automatizadas do assistente na página 2 e na página 1 não produziram save nem mudança de blob; não contam como prova de slot.

## Task 5 — Atualizar rastreabilidade e encerrar a decisão desta etapa

**Files:**
- Modify: `Server/docs/PORT_TRACEABILITY.md` — seção `SKILL-SECOND-BAR-ACQUISITION-001`
- Modify: `BACKLOG.md` — aceite e pendências da aquisição da segunda barra
- Modify: `PROGRESSO.md` — checklist resumido
- Add evidence: `Backup/Build/SkillSecondBarMatrix-20261028/capture-2026-10-28.md`

- [x] **Step 1: Redigir a captura por variante.** A captura registra os sete estados de wire, posição, atalhos zero no smoke e limitações da cena/save.
- [x] **Step 2: Separar conclusão de pendência.** Persistência canônica, relação com Extra, mapeamento visual e aquisição seguem explicitamente abertos.
- [x] **Step 3: Atualizar os índices in-place.** `PORT_TRACEABILITY.md`, `BACKLOG.md` e `PROGRESSO.md` apontam para a evidência; aquisição não foi marcada como portada.
- [x] **Step 4: Fazer revisão final do diff.** Revistos os arquivos desta etapa: regressão Engine, fixture/proxy E2E e documentação; não há mudança desta tarefa em `Server/src`, schema, cliente de referência ou configuração live. A working tree já tinha amplo estado dirty, preservado sem commit/push. `git diff --check` no `PORT_TRACEABILITY.md` sinalizou CRLF em todo o arquivo grande já alterado; não fiz normalização em massa para preservar o restante.

## Verificação final

Na pasta `Server`, rodar em série:

```powershell
dotnet build WydCdk.Engine.slnx -m:1 --no-restore -p:BaseOutputPath=bin\CodexVerifyTask10\ -t:Rebuild
$env:TEMP = Join-Path (Get-Location) '.superpowers/sdd/2026-10-28-second-hotbar-state-matrix/tmp-final'
$env:TMP = $env:TEMP
dotnet tests/WydCdk.Engine.Tests/bin/CodexVerifyTask10/Debug/net10.0/WydCdk.Engine.Tests.dll
```

Execução final em 28/10/2026: rebuild integral da solução saiu com 0 avisos e
0 erros; o harness Engine concluiu `537/537` com loopback permitido. SDK
selecionado: 10.0.401. `dotnet --info` identificou a versão, mas retornou exit
1 porque o processo não pôde abrir o Service Control Manager. A captura de
sete variantes e suas limitações headless está em
`Backup/Build/SkillSecondBarMatrix-20261028/capture-2026-10-28.md`.

Executar a fixture visual somente em loopback e registrar a versão/hash do cliente de referência já usado no laboratório. A saída da matriz é evidência de wire/estado local; não equivale a Host autenticado nem E2E de release 7670 compilada.

## Foco da revisão

- Contaminação entre os dois slots de personagem ou perda dos 20 atalhos após snapshot MOB completo/atômico.
- Confusão entre `LearnedSkill[1]` de `0x0114`/`0x0337` e `MOBEXTRA.SecLearnedSkill` no blob.
- Coordenada ou cena mudando entre variantes e invalidando a comparação visual.
- Página 2 persistida por acidente, ou página/índice inicial diferentes do reset 7.69.
- Conclusão de crosswalk/aquisição baseada apenas em bit coincidente, nome de livro ou cena que falhou.
