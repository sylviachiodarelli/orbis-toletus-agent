# Task 08 — Logging e heartbeat

## Objetivo

Observabilidade local para suporte em produção sem depender do Orbisfit.

## Entregáveis

- [ ] Provider de log em arquivo rotativo (Serilog ou `FileLogger`)
- [ ] Campos estruturados: `TransactionId`, `CredentialType`, `Authorized`, `LatencyMs`, `DeviceIp`
- [ ] Redação: API key, CPF completo
- [ ] `AgentHealthService` (background): heartbeat a cada `HeartbeatIntervalSeconds`
- [ ] Heartbeat loga: versão agente, IP catraca, conexão SDK OK/fail, última validação OK timestamp
- [ ] (Opcional v0.1) arquivo `health.json` local para diagnóstico rápido

## Níveis de log

| Evento | Nível |
|--------|-------|
| Tentativa de acesso | Information |
| API negou | Information |
| Erro rede/SDK | Warning |
| Config inválida / 401 | Error |

## Critérios de aceite

- Logs em `%ProgramData%\Orbis\ToletusAgent\logs\` (ou path configurável)
- Rotação diária ou por tamanho (50 MB)
- Heartbeat visível no log a cada intervalo configurado

## Dependências

- Task 01, 06

## Referência

- [specs.md](../specs.md) §8 Logging
