# Task 05 — Mapeamento de credenciais

## Objetivo

Centralizar a tradução de eventos LiteNet2 para o formato esperado pela API Orbisfit.

## Entregáveis

- [ ] `CredentialMapper` (classe estática ou serviço singleton)
- [ ] `Map(ToletusAccessEvent)` → `(credentialType, credentialValue)`
- [ ] Geração de `transaction_id`: `toletus-{serial}-{unixMs}`
- [ ] `direction` default `IN` (configurável futuro)
- [ ] Montagem de `raw_payload` com firmware, serial, agent_version
- [ ] Testes unitários para cada tipo: ENROLLID, FINGERPRINT, RFID, CPF

## Regras de mapeamento

| Entrada Toletus | `credential.type` | `credential.value` |
|-----------------|-------------------|---------------------|
| Display "Id 50" | `ENROLLID` | `"50"` |
| Template digital ID 12 | `FINGERPRINT` | `"12"` |
| Cartão Wiegand | `RFID` | valor normalizado |
| 11 dígitos CPF | `CPF` | apenas dígitos |

## Critérios de aceite

- Um único ponto de mapeamento — sem duplicação em orchestrator ou API client
- Testes cobrem edge cases (string vazia, zeros à esquerda)
- CPF mascarado em logs (`***.***.***-**`)

## Dependências

- Task 03, 04

## Referência

- [specs.md](../specs.md) §5.1 Mapeamento credencial
