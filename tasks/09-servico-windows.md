# Task 09 — Windows Service

## Objetivo

Executar o agente como serviço Windows estável, com start automático e recuperação de falhas.

## Entregáveis

- [ ] `UseWindowsService()` configurado com nome de serviço: `OrbisToletusAgent`
- [ ] Display name: `Orbis Toletus Agent`
- [ ] Descrição: `Ponte de acesso Toletus LiteNet2 para Orbisfit`
- [ ] Start type: Automatic (Delayed Start aceitável)
- [ ] Graceful shutdown: desconectar SDK ao parar serviço
- [ ] `CancellationToken` propagado em loops e HTTP
- [ ] Documentar execução em console para debug (`dotnet run` vs serviço)

## Critérios de aceite

- Serviço inicia após reboot do Windows (quando instalado)
- `sc stop OrbisToletusAgent` encerra sem processo zumbi
- Logs registram start/stop do host

## Dependências

- Task 01, 03, 06

## Referência

- [AGENTS.md](../AGENTS.md) — serviço headless
