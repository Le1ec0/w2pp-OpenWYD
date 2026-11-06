# Desenho: matriz de estado das duas barras de atalhos

Data: 2026-10-28

## Intencao

Fechar a evidencia de estado por personagem antes de portar aquisicao da segunda
barra. Separar atribuicoes dos 20 atalhos, pagina visivel, words de skills
aprendidas, MOBEXTRA e posicao/cena de entrada.

## Estado confirmado e limite

- Os 20 atalhos por personagem sao `MOB.SkillBar[4] + CNFCharacterLogin.ShortSkill[16]` e chegam ao cliente no `0x0114`; `0x0378` salva as duas paginas.
- A tecla `Z` alterna localmente as grades. A cena inicia com a pagina 2 oculta e indice selecionado 0; nao ha campo da pagina ativa no login nem no `0x0378`. Preservar o comportamento 7.69: relogar abre a pagina 1.
- `LearnedSkill[1]` do wire nao e `MOBEXTRA.SecLearnedSkill`. O MOB W2PP persistido tem apenas o word base; nao foi provada uma ponte nem uma origem canonica para o word adicional.
- `PosX/PosY` da confirmacao definem `HomeTownX/Y` usado ao carregar o mapa. A combinacao deve ficar fixa durante comparacoes para que falhas de cena nao sejam atribuidas aos bits.
- O usuario lembra o centro normal de Armia como `(2100,2100)` para personagens depois do nivel 35. A source TMProject calcula a posicao de cidade por `CitySpawnX/Y + rand()%15`, entao o centro lembrado e uma referencia, nao uma coordenada fixa ainda confirmada pela configuracao da zona. Para a fixture, a guilda sintetica e dona da zona de Armia e usa o GuildSpawn fixo; o usuario confirmou que esse e o caso de nascer na area da guilda quando a cidade foi conquistada.

## Abordagens

Escolhida: matriz descartavel em loopback com estado controlado por personagem.
Variar `LearnedSkill[1]` e `SecLearnedSkill` independentemente, mantendo word
base, atribuicoes dos slots e posicao de entrada constantes; capturar bytes de
login `0x0114`, refresh `0x0337`, cena aberta e estado depois de logout/relogin.
Usar personagens sinteticos distintos para provar isolamento dos 20 atalhos.

Alternativa considerada: seguir uma skill com contrato conhecido e deixar a
matriz para depois. Isso manteria progresso em outro modulo, mas nao fecharia a
incerteza de state que condiciona a aquisicao pela segunda barra.

## Limites e aceite

- Nenhuma alteracao de runtime ou schema antes da evidencia; nao copiar Extra para `LearnedSkill[1]` nem inferir crosswalk por nome de livro.
- Preservar posicao/estado da cena entre variantes; se `Field0000` ou outra cena invalida aparecer, marcar a tentativa como inconclusiva para observacao visual.
- Registrar, para cada variante, o word enviado em login e refresh, `Extra`, personagem/slot, posicao, pagina inicial e quais dados sobreviveram a save/reload.
- Confirmar que `0x0378`, save completo e reload preservam os 20 atalhos por personagem e nao cruzam dados entre os personagens sinteticos.
- So depois da matriz decidir se existe state adicional persistivel e se ha base suficiente para portar aquisicao do livro 5504.

## Chamadas

- Usuario escolheu a matriz stateful em loopback.
- Usuario confirmou seguir o comportamento 7.69 para pagina ativa: reset para a pagina 1 ao entrar.
- Usuario esclareceu que o spawn de guilda e apropriado para a fixture quando a guilda possui Armia; o spawn normal apos o limite da area de treino e um caso distinto.
- A execucao visual e apenas local/descartavel; sem Host autenticado, deploy, commit ou push.
