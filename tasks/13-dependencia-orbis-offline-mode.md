# Task 13 — Dependência Orbisfit: política offline (repo externo)

## Objetivo

Alterações no repositório `alliance-bertioga-dash` para suportar `turnstile_offline_mode` por tenant.

> **Nota:** esta task é implementada no Orbisfit, não neste repo. Documentada aqui para coordenação.

## Entregáveis (em alliance-bertioga-dash)

- [ ] Migration: `tenant_financial_policy.turnstile_offline_mode text default 'fail_closed'`
- [ ] Check constraint: `fail_closed` | `fail_open`
- [ ] `access-validation.service.ts`: incluir `offline_mode` na resposta
- [ ] `api/turnstile/access-attempt.ts`: propagar campo
- [ ] UI settings (opcional): toggle em política financeira ou catracas
- [ ] Doc em `docs/integracoes/toletus-agent.md`

## Contrato para o agente

Resposta HTTP deve incluir:

```json
{
  "authorized": true,
  "offline_mode": "fail_closed"
}
```

## Critérios de aceite

- Tenant com `fail_open` → agente cacheia e usa em queda de rede
- Default `fail_closed` para tenants existentes
- Agente funciona mesmo antes desta task (usa default local)

## Dependências

- Nenhuma no orbis-toletus-agent
- Task 07 consome o campo quando disponível

## Referência

- [specs.md](../specs.md) §6, §10
