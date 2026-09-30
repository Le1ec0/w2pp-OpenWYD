# Ambiente do WYD CDK

## Plataforma .NET do ambiente local

O listener, os projetos de dominio/protocolo, testes, ferramentas e o launcher
usam `net10.0`; o painel WinForms usa `net10.0-windows`. Nesta atualizacao o SDK
observado foi `10.0.401`. A linha recomendada e .NET 10 LTS no ultimo patch
estavel, sem preview/STS automatico. Ate existir `global.json` pelo `BL-025`,
comece cada evidencia de build com:

```powershell
dotnet --info
dotnet build .\Server\WydCdk.Engine.slnx --nologo -v:q
```

Use `async`/`await` e cancelamento para I/O; nao use `Task.Run` para mascarar
operacoes de rede, arquivo ou banco. Preserve `Nullable`/`ImplicitUsings`, injecao
de dependencia, configuracao tipada, logs sem credenciais e o bind local por
padrao. Qualquer teste publico, credencial ou endpoint externo continua fora
desse ambiente e exige autorizacao separada.

## Estado confirmado nesta sessão

- `Site\` é o frontend PHP legado de cadastro, ranking, download e guildmark. Os arquivos `php-local.err.log` e `php-e2e.err.log` registram execuções anteriores do PHP embutido em `127.0.0.1:8000` e `127.0.0.1:8080`.
- Não há processo PHP ativo neste momento e não há MySQL/MariaDB escutando nas portas verificadas.
- `Site\index.php` grava contas diretamente em `Server\Release\DBSRV\run\account`, um caminho legado local; isso não constitui integração com um servidor online.
- `Server\src\WydCdk.Server` é o listener C# de teste e está restrito a `127.0.0.1:8281`.
- As portas 80/443 observadas pertencem ao `MockApi.exe` de `D:\Visual\Overmind\Tasks\Soluções\local-env-shared`, não ao Site nem ao WYD CDK.
- O domínio `wydcdkpvp.servegame.com` aparece apenas em links antigos do frontend. Sua disponibilidade e seu destino não foram usados como evidência nem verificados nesta etapa.

## Separação de ambientes

| Componente | Papel | Estado seguro atual |
|---|---|---|
| `Site\` | frontend PHP | código local; sem PHP ativo |
| `Server\` | servidor C# e referência C++ | C# em loopback; C++ é referência |
| `Release\DBSRV\run\account` | arquivos de conta legados | caminho esperado pelo PHP antigo; não usar como produção |
| portas 80/443 | outro processo | fora do escopo do WYD |

Qualquer teste de ambiente público deve ser uma etapa separada e autorizada. Até lá, cadastro, contas e combate continuam somente no ambiente local isolado.
