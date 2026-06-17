# Task 11 — Testes unitários e integração

## Objetivo

Cobrir lógica crítica sem hardware e permitir CI básico.

## Entregáveis

- [ ] Projeto xUnit `Orbis.ToletusAgent.Tests`
- [ ] `CredentialMapperTests` — todos os tipos de credencial
- [ ] `AccessDecisionParserTests` — v1 e v2 responses
- [ ] `OfflinePolicyCacheTests` — TTL, fail_closed, fail_open
- [ ] `DebounceTests` — leituras duplicadas
- [ ] `OrbisApiClientTests` — `HttpMessageHandler` mock (200, 401, 503, timeout)
- [ ] `AccessOrchestratorTests` — fluxo completo com mocks
- [ ] GitHub Actions ou script `dotnet test` no README

## Fora de escopo CI

- Testes com catraca física (manual / ambiente piloto)
- Testes contra produção Orbisfit (usar mock ou staging)

## Critérios de aceite

- `dotnet test` verde sem hardware
- Cobertura mínima nas classes de mapeamento, parser e offline (não exigir % fixo no MVP)

## Dependências

- Tasks 04, 05, 06, 07

## Referência

- [AGENTS.md](../AGENTS.md) § Testes
