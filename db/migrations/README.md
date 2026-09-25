# Migrações MariaDB do servidor

Estas migrações criam somente as tabelas de estado pertencentes ao servidor
C#/.NET. A tabela `accounts` e, quando usada, `donate_shop_catalog` pertencem
ao Site/conta e precisam existir no banco configurado antes de iniciar o host.

Aplicar os arquivos em ordem, dentro do database informado no JSON de conexão:

1. `001_world_state.sql`
2. `002_account_security.sql`
3. `003_autotrade_state.sql`
4. `004_client_equipment_769.sql`

Com o runner local do checkout:

```powershell
dotnet run --project .\Server\tools\MariaDbMigrations\MariaDbMigrations.csproj --no-restore -- .\Backup\secrets\site-db.json
```

Quando a senha estiver separada do JSON, passe o arquivo como terceiro
argumento. O runner lê o conteúdo apenas em memória:

```powershell
dotnet run --project .\Server\tools\MariaDbMigrations\MariaDbMigrations.csproj --no-restore -- .\Backup\secrets\site-db.json .\Server\db\migrations .\Backup\secrets\mariadb-site.txt
```

Para uma conta administrativa local diferente da conta do JSON:

```powershell
dotnet run --project .\Server\tools\MariaDbMigrations\MariaDbMigrations.csproj --no-restore -- .\Backup\secrets\site-db.json .\Server\db\migrations root .\Backup\secrets\mariadb-root.txt
```

O comando não mostra a senha nem grava credenciais; valide o caminho e o banco
do JSON antes de executá-lo.

Para um reset explícito do sandbox local, o runner aceita
`--reset-server-schema`; ele derruba somente as seis tabelas `wyd_*` do
servidor e reaplica as migrações. Não use esse modo contra Site ou VPS sem uma
autorização específica.

Os arquivos são idempotentes (`CREATE TABLE IF NOT EXISTS`) e registram a
versão em `wyd_schema_migrations`. O runner também possui o smoke opt-in
`--purchase-smoke`, que usa duas contas temporárias e remove seus dados ao
final; ele validou localmente o commit de compra e o replay CAS. Isso não
autoriza conectar no banco da VPS ou no banco de produção.
