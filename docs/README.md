# Engenharia do servidor

Esta pasta e a documentacao viva do port C++ -> C#. Ela complementa o `AGENTS.md`: registra decisoes tecnicas que precisam sobreviver entre sessoes e liga cada comportamento ao codigo legado, implementacao C# e teste local.

## Como usar

- Nao registrar suposicoes como fatos. Cada item deve distinguir **confirmado no C++**, **portado/testado**, **pendente** e **decisao consciente**.
- Ao migrar uma mensagem ou sistema, atualizar o documento tematico com o wire, validacoes, estado alterado, custo de processamento e teste que protege o comportamento.
- Manter a compatibilidade antes de otimizar. Melhorias so entram depois de medir impacto e sem substituir a autoridade do servidor.

## Indice

- [Tempo, input e combate](migration\tempo-input-combate.md) — `ClientTick`, protecoes anti-fraude, movimento, motion e preparacao do PvP.
- [Seguranca de protocolo](migration\seguranca-protocolo.md) — limites que realmente estao ativos no legado, checksums, tamanho e politica de rejeicao.

- [Dados de skills](migration\dados-skills.md) — `SkillData.csv`, IDs 9/10, colunas `Act` e separacao entre fonte do servidor e `SkillData.bin` do cliente.
- [Ambiente local](ambiente-local.md) — separação entre Site PHP, servidor C# loopback, arquivos legados e processos externos.

## Estado de validacao atual

- Solucao: `dotnet build WydCdk.Engine.slnx --nologo -v:q`.
- Suite: `dotnet run --no-build --project .\tests\WydCdk.Engine.Tests\WydCdk.Engine.Tests.csproj`.
- Ultima referencia: 55 testes locais passando após a resolução física/elemental com parry (`GetParryRate`, `EF_PARRY`, códigos `-3`/`-4` sem perda de HP), da primeira rota de cura, da desintoxicação, da aplicação genérica de affect, do gate de mapa seguro/estado de guerra e dos gates agressivos (`Rsv`/`AffectResist`) entre jogadores, com slots múltiplos limitados por `MaxTarget`, fórmulas de dano-base de `BASE_GetSkillDamage`, composição de `WeaponDamage`, leitor de `ItemList.bin`, integração `--item-data` no listener, mapa/atributos de colisão (`heightmap.dat` + `AttributeMap.dat`), estado de zonas/`ChargeGuild` via `--guild-data`, relay de mana/`MSG_SetHpMp`/`MSG_UpdateScore`, loader/gate de `SkillData.csv`, leitura de estado de combate, `_MSG_SetShortSkill`, parser de ataque e gates de `ClientTick`.
