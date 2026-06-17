# Task 06 — Orquestrador de acesso

## Objetivo

Coordenar o fluxo completo: evento Toletus → validação Orbisfit → comando na catraca.

## Entregáveis

- [ ] `AccessOrchestrator` (hosted service ou handler de eventos)
- [ ] Debounce: ignorar mesma credencial em &lt; `DebounceMs`
- [ ] Fluxo: receber evento → mapear → chamar API → liberar/negar
- [ ] Em negação: chamar `DenyTurnstileAsync` (ou equivalente SDK)
- [ ] Em autorização: chamar `ReleaseTurnstileAsync`
- [ ] Tratamento de exceção sem derrubar o serviço
- [ ] Integração com `OfflinePolicyCache` quando API falhar

## Pseudofluxo

```text
OnToletusEvent(event)
  if debounced → return
  decision = await orbis.ValidateAccessAsync(...)
  if decision.Authorized → release
  else → deny
  log result
```

## Critérios de aceite

- Leitura duplicada em 2s não gera segunda chamada HTTP
- Falha HTTP aplica política offline (task 07)
- Serviço continua rodando após erro isolado
- Testes unitários com mocks de Toletus e Orbis

## Dependências

- Task 03, 04, 05, 07

## Referência

- [specs.md](../specs.md) §5 Fluxo de acesso
