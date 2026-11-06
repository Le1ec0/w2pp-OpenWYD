# Cancelamento Type 32 em NPC — design

## Intenção

Completar o fallback da skill 47 (`Cancelamento`) para o único índice de NPC que o `SetAffect` legado aceita. Se o primeiro Type 19 existir, a skill continua removendo apenas esse registro e encerrando o lote. Sem Type 19, a fonte segue para `SetAffect` e aplica o Type 32 da linha 47.

## Evidência e estado atual

TMProject 7.69 e W2PP deixam a skill 47 cair no caminho genérico `SetAffect` quando Type 19 não foi removido. Ambos rejeitam `conn > MAX_USER`; a definição 7.69 declara `MAX_USER=1000` como o índice inicial de NPCs. Portanto a comparação estrita permite `conn == 1000` e rejeita NPCs de índice maior. A linha 47 tem `AffectType=32`, `Aggressive=1` e `AffectResist=0`. A chamada só ocorre se o ataque genérico mantém `SameLeaderGuild`: líder e guilda diferentes (guilda zero/zero é normalizada para -1/0), alvo sem `RSV_BLOCK`, alvo fora do Clan 6 para caster jogador, e não merchant. O handler C# de NPC hoje trata a remoção de Type 19, mas não aplica `AffectType` no fallback. O timer Type 32 já processa countdown/expiração.

## Abordagens consideradas

- **Aplicar o fallback só no NPC 1000 (escolhida):** replica a guarda estrita das duas fontes; fica limitado, mas fiel ao comportamento observado.
- **Aplicar a todos os NPCs:** mais útil operacionalmente, porém alarga o contrato que o C++ restringe a um índice.
- **Deixar o produtor sem port:** preserva a lacuna atual e torna o timer Type 32 inaplicável pelo caminho normal da skill.

## Escopo

- Em `TryApplyNpcEffectSkillAttackLocked`, depois da tentativa de remover Type 19, aplicar o efeito genérico da skill 47 somente quando o alvo seja o índice `MaximumPlayerId` (1000), merchant seja 0 e os gates agressivos de líder, guilda, `RSV_BLOCK` e Clan 6 permitam.
- Relayar o `UpdateScore` resultante apenas para a view 33x33 do NPC; não enviar um `UpdateAffect` separado para índice NPC.
- Preservar a aplicação em jogadores, a remoção Type 19 em qualquer NPC, o limite legado para índices >1000 e o timer já portado.
- Não ampliar `SetAffect`, não alterar outras skills e não portar o evento BossCamp.

## Aceite

- Sem Type 19, NPC 1000 recebe Type 32 com os valores calculados pelo helper genérico existente.
- Sem Type 19, NPC 1001 não recebe Type 32.
- NPC 1000 com mesmo líder ou guilda do atacante, Clan 6, estado merchant ou `RSV_BLOCK` não recebe Type 32.
- Type 19 continua tendo precedência e o restante da suíte permanece verde.
- O smoke TCP confirma `UpdateScore` com Type 32 nos peers dentro da view do NPC e nenhum frame no peer fora dela.
- Nenhum frame de wire ou API pública muda.
