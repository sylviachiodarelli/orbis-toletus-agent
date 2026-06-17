# Task 07 — Política offline por tenant

## Objetivo

Aplicar `fail_closed` ou `fail_open` quando a API Orbisfit estiver indisponível, com cache da política do tenant.

## Entregáveis

- [ ] `OfflinePolicyCache` com TTL (`PolicyCacheMinutes`)
- [ ] Atualizar cache quando resposta Orbisfit incluir `offline_mode`
- [ ] `ResolveOfflineDecision()` quando HTTP falhar:
  - cache válido → usar modo cacheado
  - sem cache → `AgentOptions.DefaultOfflineMode` (`fail_closed`)
- [ ] Enum `OfflineMode { FailClosed, FailOpen }`
- [ ] Testes: sem cache + fail_closed = negar; com cache fail_open = liberar

## Comportamento

| Situação | Ação |
|----------|------|
| API OK, `authorized: false` | Negar (regra Orbisfit) |
| API OK, `authorized: true` | Liberar |
| API timeout, cache `fail_closed` | Negar |
| API timeout, cache `fail_open` | Liberar |
| API timeout, sem cache | `DefaultOfflineMode` |

## Dependência externa (Orbisfit)

Campo `offline_mode` na resposta da API — ver task 12 em `alliance-bertioga-dash`. Agente deve funcionar sem esse campo (usa default local).

## Dependências

- Task 04

## Referência

- [specs.md](../specs.md) §6 Política offline
