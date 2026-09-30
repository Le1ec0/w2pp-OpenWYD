# Auditoria de segurança do ServerWireLog

## Achado

`ServerWireLog.IsSensitiveFrame` não mascarava dois frames críticos:

- **`DeleteCharacterRequest` (`0x0211`)**: payload contém a senha da conta (`BlockPassword`).
- **`AccountLoginConfirmation` (`0x010A`)**: payload contém chaves de sessão (`KeyTable`) e `BlockPassword`.

O logger gravava esses payloads em hexadecimal ao lado do executável, expondo credenciais em disco.

## Correção aplicada

Os dois frames foram adicionados à lista de sensíveis em `ServerWireLog.cs`.  
O payload passa a ser substituído por `<redacted-auth-payload>` nos dois casos.

## Frames sensíveis vigentes

| Opcode | Tipo | Razão |
|--------|------|-------|
| `0x0208` | `AccountLoginRequest` | Senha da conta |
| `0x010A` | `AccountLoginConfirmation` | KeyTable + BlockPassword |
| `0x0FDE` | `AccountSecureRequest` | PIN |
| `0x020B` | `CreateCharacterRequest` | Senha |
| `0x0211` | `DeleteCharacterRequest` | Senha da conta |

## Teste

Não existe teste unitário direto de `IsSensitiveFrame` porque a classe é `internal`.
A cobertura indireta vem do smoke E2E (`WydCdk.Client.E2E`) que exercita o login e o logout passando por esses opcodes — a ausência de payload hexadecimal no log para `0x010A` confirma a supressão.

**Pendente:** adicionar teste de redação via `WriteFrame` expondo a classe ao projeto de teste via `InternalsVisibleTo` ou mover para um wrapper testável.

- Data da correção: 25/09/2026.
