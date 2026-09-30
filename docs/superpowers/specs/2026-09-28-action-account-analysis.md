# Especificação: detecção de velocidade e ciclo da conta em Análise

**Status:** aprovada pelo usuário em 28/09/2026. Nenhum código de produto foi alterado nesta etapa.

## Objetivo

Completar no servidor C# o efeito de segurança exclusivo da referência comunitária TMProject 7.69 para um `Action` com velocidade incompatível e de alta severidade: persistir a conta como **Análise**, encerrar a sessão que gerou a detecção e negar logins futuros dessa conta. O comportamento deve sobreviver ao reinício do servidor e não depender de dados ou serviços do Site.

O alvo continua sendo o cliente único 7.670, com contratos de wire da source 7.69. A mudança não deve reorganizar os projetos nem alterar o restante do processamento de movimento.

## Evidência e regra de compatibilidade

- Na source TMProject 7.69 (`_MSG_Action.cpp`, commit `3ccc4d3377a1226648d58d59f87d7a020cabc8a1`), a regra está dentro do ramo `m->Speed > (CurrentScore.AttackRun & 0xF)`. O ramo soma `5/type 4`; se `m->Speed >= 20` **ou** o `CurrentScore.AttackRun` autoritativo for `>= 300`, chama `SendBanAccount(..., Banned::Analise)` e `SaveInfoDetect`; em seguida corrige a velocidade para o nibble permitido.
- Na mesma referência, `SendBanAccount` persiste o estado `Analyze` e fecha a conexão. `_MSG_AccountLogin.cpp` consulta o estado da conta antes de encaminhar a autenticação ao DBSrv; uma conta em análise recebe aviso de contato com o suporte e o login não prossegue.
- `SaveInfoDetect` registra conta, personagem, IP e motivo no log de detecção. A evidência de motivo deve ser interna; não se deve expor detalhes de detecção ao cliente.
- O W2PP local (`W2PP/Source/Code/TMSrv/_MSG_Action.cpp`) tem o incremento e o clamp, mas não esse gatilho de Análise. Para esse efeito específico, a 7.69 é a autoridade e a ausência no W2PP é uma divergência documentada, não um veto ao port.
- O `AccountBanned` da 7.69 declara os campos `Permanente`, `Analyze` e `Ativa`; o do W2PP não tem `Ativa`. Como os formatos binários diferem e ainda não há golden de ABI para o arquivo de banimento, o C# **não** deve ler/escrever esses `.bin` por cópia bruta.

### Ressalva de ABI confirmada durante a implementacao

O `STRUCT_SCORE.AttackRun` nos headers servidor 7.69 e W2PP e `BYTE` (8 bits),
e o C# le o mesmo campo como `byte`. Assim, o ramo historico `AttackRun >= 300`
nao e alcancavel pelo score autoritativo atualmente definido nessas ABIs; o
limite `requestedSpeed >= 20` continua alcancavel. O predicado preserva ambos
os limites da source como contrato puro, mas o port nao alarga o campo wire nem
fabrica um score de 300. A cobertura distingue o limiar abstrato de 300 do
maximo runtime de 255.

## Comportamento proposto

### `Action` que dispara a análise

1. Manter os gates atuais de sessão, personagem vivo e disponibilidade do score autoritativo.
2. Só considerar a detecção dentro de uma violação real: `requestedSpeed > (AttackRun & 0xF)`. Um score alto, por si só, não marca a conta se a velocidade enviada estiver permitida.
3. Preservar o incremento compartilhado `5/type 4` para a violação. A severidade que marca a conta é exatamente `requestedSpeed >= 20 || AttackRun >= 300`, usando o score completo do servidor, não o nibble. Capturar a identidade/evidência antes do incremento e não deixar que o helper existente encerre o personagem por atingir seu limite e pule a marcação de Análise para este mesmo evento.
4. Para uma violação de alta severidade, gravar o estado durável **Análise** e a evidência mínima do evento antes de qualquer relay/mutação de movimento. Depois, enviar o aviso de Análise e encerrar a conexão. Se o limite compartilhado de crack-error também disparar, a resposta de Análise prevalece: não enviar dois avisos conflitantes nem continuar o `Action` apenas porque o clamp foi calculado.
5. Uma marcação já existente é idempotente: não remove nem rebaixa o estado, e a conexão que repetir o evento também não pode continuar o movimento. A detecção deve preservar a primeira evidência em vez de sobrescrevê-la silenciosamente.
6. Se houver violação, mas nenhuma das condições de severidade for verdadeira, manter o comportamento já portado: somar `5/type 4`, corrigir somente `Speed` e seguir pelos gates atuais de range/movimento.

O aviso sugerido, sem erro de grafia da source, é: **“Estamos analisando sua conta. Entre em contato com o suporte.”** Enviá-lo no `MSG_MessagePanel` (`0x0101`, payload de 128 bytes), equivalente a `SendClientMsg` já usado pela referência e ao encoder C# existente. O motivo técnico e o endereço IP não são enviados ao cliente.

### Login de conta em Análise

1. Preservar o gate atual `ClientVersion=7670`.
2. Validar e normalizar a identidade da conta antes de formar qualquer caminho: ASCII permitido pelo contrato atual, tamanho de 1 a 15 caracteres e comparação/armazenamento em maiúsculas invariantes.
3. Consultar o estado de Análise antes da autenticação de senha, como na 7.69. Se marcado, enviar o aviso neutro acima e fechar sem chamar o autenticador nem produzir confirmação de seleção de personagem.
4. Sem marcação, seguir o fluxo de autenticação já existente, tanto para conta legada quanto para o autenticador MariaDB.
5. Se a leitura estiver indisponível, corrompida ou usar uma versão desconhecida, negar o login com aviso genérico de indisponibilidade e registrar erro operacional. Nunca interpretar erro/corrupção como “sem Análise”.

O estado não expira automaticamente. Remoção/liberação por staff, painel administrativo, outros tipos de banimento e propagação a outras sessões da mesma conta não fazem parte deste bloco. A referência fecha a conexão que gerou a detecção; o port deve manter esse escopo.

## Persistência e fronteiras

- Criar uma capacidade de aplicação `IAccountReviewStore`, independente de `IAccountStore` e do backend de senha/personagens. O coordenador de login consulta essa capacidade; o handler de Action solicita a marcação. A implementação deve respeitar a organização atual da solution, sem extração em massa de projetos.
- Usar um diretório explícito de runtime (`--account-review-root`) e um documento por conta, com formato C# versionado e escrito atomicamente. O caminho deve ser derivado somente do nome normalizado validado; rejeitar separadores, `..`, nomes inválidos, links/caminhos escapando da raiz e conteúdo com identidade divergente.
- O documento deve conter versão, conta, estado `Analysis`, instante UTC e evidência do gatilho (código estável do motivo, personagem, velocidade pedida/permitida, `AttackRun` completo e IP remoto quando disponível). Nunca incluir senha, PIN, token de reconexão ou MAC.
- Status e evidência ficam no mesmo documento para que a marcação seja uma única gravação atômica. Usar arquivo temporário na mesma raiz, flush apropriado e substituição atômica; serializar atualizações concorrentes da mesma conta no processo. Execução simultânea de múltiplos processos compartilhando a mesma raiz não é suportada neste bloco.
- O armazenamento é obrigatório quando o host habilita autenticação; configuração ausente deve impedir a inicialização do modo autenticado, em vez de aceitar contas sem a proteção. A raiz não deve ficar dentro do checkout versionado nem ser apagada por publicação/limpeza de build.
- Essa persistência reproduz o ciclo semântico da 7.69, não a interoperabilidade byte a byte com `Ban/<conta>.bin`. Não criar tabela ou migration no banco `accounts` do Site, não modificar o Site e não reutilizar os arquivos de banimento do W2PP.

### Falhas operacionais

- Falha ao persistir a detecção: rejeitar o `Action`, não alterar posição e não fazer relay; encerrar a conexão e emitir erro operacional de alta visibilidade. Não afirmar ao jogador que a conta foi marcada se a gravação não confirmou sucesso.
- Falha ao consultar o estado no login: negar o login, sem fallback permissivo. O aviso ao jogador deve pedir nova tentativa/contato com suporte, sem expor caminho ou detalhe interno.
- Falha ao enviar o aviso depois da persistência não desfaz a marcação; o encerramento do transporte ainda deve ocorrer.

## Fora de escopo

- Banimento temporário/permanente, ativação, bloqueio por MAC, painel de staff, API Site, integração comercial, migration MariaDB ou sincronização com `Ban/*.bin` legado.
- Aplicar Análise a outros tipos de `Action`, `Attack` ou detecções não aprovadas neste bloco.
- Mudanças nas regras de clamp, range, teleporte, combate, personagem, inventário ou demais opcodes.
- Teste visual no cliente, publicação no Site/produção, commit ou push. A validação sandbox runtime-only após aprovação dos gates locais segue a autorização separada já vigente.

## Critérios de aceite e validação agrupada ao fim do bloco

1. Teste de regra confirma que Análise só ocorre em violação de velocidade e nos limites inclusivos `speed >= 20` ou `AttackRun >= 300`; inclui casos abaixo de ambos, score alto com velocidade permitida e incremento `5/type 4` que também atinge o limite de logout. A violação não severa continua somando `5/type 4` e clampeando somente a velocidade.
2. Testes do store confirmam gravação/leitura após nova instância (simulação de reinício), estado persistente, idempotência, concorrência no mesmo processo, normalização, rejeição de path traversal e negação segura para registro truncado, inválido ou de versão desconhecida.
3. Testes do coordenador de login confirmam que Análise é consultada antes da senha/autenticador, que nenhum pacote de seleção de personagem é emitido para conta marcada e que uma conta sem marcação continua no fluxo atual.
4. Teste de rede em loopback, com conta/personagem sintéticos isolados, confirma: pacote Action qualificado -> documento durável -> aviso -> desconexão; nenhuma mudança de coordenada nem relay de movimento; novo login, inclusive após recriar o host/store, é negado. Conta-controle continua podendo autenticar. Incluir caso de falha de gravação e caso de store indisponível no login.
5. Rodar juntos ao final do bloco a suite de testes do engine, build da solution e harnesses loopback afetados. Registrar resultados, limites e arquivos de evidência na documentação vigente. Não usar conta real, Site, banco externo, cliente visual ou produção. Depois dos gates locais, publicar somente o artefato runtime na VPS de teste dentro da autorização sandbox já existente, preservar dados/backups e conferir hashes/listeners conforme as regras do projeto; nenhuma source ou segredo será sincronizado.

## Referências

- `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_Action.cpp`
- `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/_MSG_AccountLogin.cpp`
- `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/SendFunc.cpp` (`SendBanAccount` / `SendSaveBanned`)
- `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/TMSrv/IlegalCMD.cpp` (`SaveInfoDetect`)
- `Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Source/Code/Basedef.h` (`AccountBanned`)
- `W2PP/Source/Code/TMSrv/_MSG_Action.cpp` e `W2PP/Source/Code/Basedef.h`
- `src/WydCdk.Server/Program.cs`, `src/WydCdk.World/AccountLoginCoordinator.cs`, `src/WydCdk.World/LoginSessionRegistry.cs`, `src/WydCdk.World/LegacyCrackErrorRules.cs`
- `docs/PORT_TRACEABILITY.md`, recorte `MOVE-ACTION` (estado atual e side effect de Análise pendente)
