# Task 02 — Configuração e secrets

## Objetivo

Tipar e validar toda configuração do agente via `IOptions<T>` e variáveis de ambiente.

## Entregáveis

- [ ] `OrbisOptions` (ApiBaseUrl, ApiKey, DeviceCode, paths, timeout, retries)
- [ ] `ToletusOptions` (Ip, Port, SerialNumber)
- [ ] `AgentOptions` (DebounceMs, PolicyCacheMinutes, HeartbeatIntervalSeconds, DefaultOfflineMode)
- [ ] Binding em `Program.cs` + validação na inicialização
- [ ] Mapeamento env vars: `ORBIS_API_KEY`, `ORBIS_API_BASE_URL`, `TOLETUS_IP`, etc.
- [ ] `appsettings.example.json` commitável (valores placeholder)
- [ ] `appsettings.json` real no `.gitignore` ou uso de User Secrets em dev

## Validações obrigatórias

Aplicadas **somente quando** `Agent:SetupComplete` é `true` (após configurar via UI ou JSON manual).

| Campo | Regra |
|-------|-------|
| `Orbis.ApiBaseUrl` | URL absoluta HTTPS (ou HTTP só em dev) |
| `Orbis.ApiKey` | Não vazio em produção |
| `Orbis.DeviceCode` | Não vazio |
| `Toletus.Ip` | IPv4 válido |
| `Agent.DefaultOfflineMode` | `fail_closed` ou `fail_open` |

Antes do setup: campos podem ficar em branco; o serviço sobe em modo configuração.

## Critérios de aceite

- Serviço falha rápido com mensagem clara se config inválida **após** setup completo
- Env var sobrescreve appsettings
- Nenhum secret no repositório
- Configuração também disponível via UI: [docs/setup-ui.md](../docs/setup-ui.md)

## Dependências

- Task 01

## Referência

- [specs.md](../specs.md) §4 Configuração
