# Tasks — Orbis Toletus Agent

Ordem sugerida de implementação:

| # | Task | Prioridade | Status |
|---|------|------------|--------|
| 01 | [Scaffold .NET](01-scaffold-projeto-dotnet.md) | P0 | Concluída |
| 02 | [Configuração](02-configuracao-appsettings.md) | P0 | Concluída |
| 03 | [SDK LiteNet2](03-integracao-sdk-litenet2.md) | P0 | Concluída |
| 04 | [Cliente API Orbisfit](04-cliente-api-orbis.md) | P0 | Concluída |
| 05 | [Mapeamento credenciais](05-mapeamento-credenciais.md) | P0 | Concluída |
| 06 | [Orquestrador de acesso](06-orquestrador-acesso.md) | P0 | Concluída |
| 07 | [Política offline](07-politica-offline.md) | P0 | Concluída |
| 08 | [Logging e heartbeat](08-logging-heartbeat.md) | P1 | Concluída |
| 09 | [Windows Service](09-servico-windows.md) | P0 | Concluída |
| 10 | [Instalador](10-instalador-deploy.md) | P1 | Concluída |
| 11 | [Testes](11-testes-unitarios-integracao.md) | P1 | Concluída |
| 12 | [UI de configuração](12-tray-config-opcional.md) | P1 | UI web concluída; tray opcional v0.2 |
| 13 | [Orbisfit offline_mode](13-dependencia-orbis-offline-mode.md) | P1 (repo externo) | Doc em [docs/coordenacao-orbis-offline-mode.md](../docs/coordenacao-orbis-offline-mode.md) |
| 14 | [Piloto academia](14-teste-piloto-academia.md) | P0 (validação final) | Runbook em [docs/piloto-academia-runbook.md](../docs/piloto-academia-runbook.md) |

Especificação completa: [specs.md](../specs.md)

## Validação final (task 14)

1. `installer\preflight-check.ps1`
2. Seguir [docs/piloto-academia-runbook.md](../docs/piloto-academia-runbook.md)
3. Preencher tabela de cenários e abrir issues para bugs
