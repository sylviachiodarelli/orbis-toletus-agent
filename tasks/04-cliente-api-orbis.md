# Task 04 — Cliente HTTP API Orbisfit

## Objetivo

Implementar cliente para validar acesso na API Orbisfit com retry, timeout e parser unificado de resposta.

## Entregáveis

- [ ] Interface `IOrbisApiClient`
- [ ] `OrbisApiClient` com `IHttpClientFactory`
- [ ] `ValidateAccessAsync(AccessAttemptRequest)` → `AccessDecision`
- [ ] Suporte v2: `POST /api/turnstile/access-attempt`
- [ ] Fallback v1: `POST /api/verificar-acesso-catraca` (flag `UseV2Endpoint`)
- [ ] Header `x-api-key` em todas as requisições
- [ ] Retry em timeout/5xx conforme `OrbisOptions`
- [ ] `AccessDecisionParser` — único lugar que interpreta `authorized` / `autorizado` / `resultado`

## Modelos

```csharp
record AccessAttemptRequest(
  string TransactionId,
  string Direction,
  string DeviceCode,
  string CredentialType,
  string CredentialValue,
  object? RawPayload);

record AccessDecision(
  bool Authorized,
  string Message,
  string? StudentName,
  string? StatusPagamento,
  string? OfflineMode);
```

## Critérios de aceite

- Chamada bem-sucedida contra `https://orbisfit.com/api/turnstile/access-attempt` (ou mock)
- Retry funciona em 503 simulado
- 401 não faz retry e retorna erro claro
- API key nunca aparece em logs

## Dependências

- Task 01, 02

## Referência

- [specs.md](../specs.md) §5.2 Payload, §7 Retries
