# Seguranca de protocolo

## Principio do port

O cliente declara intencao; o servidor valida wire, estado e regras do mundo antes de alterar estado ou retransmitir resultado. Compatibilidade nao significa copiar protecoes inertes do legado sem medir se elas realmente fazem efeito.

## Confirmado no C++

### `AddCrackError`

- A funcao soma pontos em `pUser[conn].NumError` e registra varios tipos de infracao.
- So chama `CharLogOut` quando o acumulado chega a `2.000.000.000`.
- Consequencia: nessa fork, o mecanismo funciona principalmente como log/telemetria. Ele nao e uma defesa imediata contra um pacote invalido isolado ou spam moderado.

### Validacao global de tamanho

- `BASE_CheckPacket` contem uma grande tabela de `Type`/`sizeof`, incluindo ataque, movimento, inventario e login.
- Todo o corpo da funcao esta comentado em `Basedef.cpp`; ela nao esta ativa nesta base.
- Nao assumir que uma mensagem esta protegida apenas porque aparece nessa tabela comentada.

### Validacoes locais que importam

- `ProcessClientMessage` descarta `SKIPCHECKTICK` quando a origem e cliente.
- Handlers verificam `USER_PLAY`, HP, cooldown, timestamp, habilidade aprendida, classe, velocidade e outros limites antes de mutar estado. A cobertura varia por mensagem.
- `_MSG_Action` limita velocidade ao `AttackRun` do MOB e rejeita passos/estados invalidos; o port ja possui limites de mapa e passo, mas ainda nao tem `AttackRun` autoritativo.

## Portado e ativo no C#

- `LegacyFrameCodec` valida checksum; cada parser migrado exige tipo e tamanho exatos.
- O listener descarta o timestamp reservado antes do dispatch.
- Estados de sessao, PIN, slot, nome, posicao e guilda tem validacao local e testes.
- O gate de ataque ja rejeita timestamp reservado, spam de menos de 800 ms e timestamps fora da janela do servidor antes de qualquer calculo futuro.

## Prioridades de baixo custo

1. Para cada mensagem nova, parser estrito e estado de sessao antes de alocar, consultar arquivo ou calcular.
2. Para combate, validar timestamp, skill aprendida/classe, custo de MP e alcance antes de iterar alvos.
3. Registrar infracoes por conexao para diagnostico, mas usar limites objetivos para rejeitar agora — nao depender de um contador de dois bilhoes.
4. So aplicar rate limit de rede por tipo depois de capturar trafego real; nao bloquear input normal por uma suposicao.
