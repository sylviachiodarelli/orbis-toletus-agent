# Task 03 — Integração SDK LiteNet2 (Toletus)

## Objetivo

Conectar na catraca Toletus via SDK oficial e expor eventos de tentativa de acesso.

## Entregáveis

- [ ] Interface `IToletusDeviceService`
- [ ] Implementação `ToletusDeviceService` usando `litenet2-integrationpackage`
- [ ] Conexão por IP (`ToletusOptions.Ip`)
- [ ] Reconexão automática com backoff em queda de link
- [ ] Eventos normalizados: `ToletusAccessEvent` (tipo, valor, timestamp, raw)
- [ ] Métodos: `ConnectAsync`, `DisconnectAsync`, `ReleaseTurnstileAsync`, `DenyTurnstileAsync`
- [ ] Log de firmware/série na conexão bem-sucedida

## Tipos de evento (MVP)

| Evento SDK | Mapear para |
|------------|-------------|
| ID numérico / teclado | `ENROLLID` |
| Digital reconhecida | `FINGERPRINT` |
| Cartão (se suportado) | `RFID` |

## Critérios de aceite

- Conecta na catraca em `192.168.0.220` (ambiente piloto)
- Evento de leitura dispara callback sem travar o host
- Reconecta após reinício da catraca ou queda de rede
- Toda lógica SDK isolada em `Toletus/` — nenhum `using` Toletus fora do adapter

## Riscos

- SDK pode exigir .NET Framework em versões antigas — validar compatibilidade .NET 8
- Documentação em [litenet2-manuaisdeintegracao](https://github.com/toletus/litenet2-manuaisdeintegracao)

## Dependências

- Task 01, 02

## Referência

- [specs.md](../specs.md) §2 Arquitetura, §5 Fluxo
