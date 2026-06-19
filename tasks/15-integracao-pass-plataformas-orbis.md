# Task 15 — Integração pass plataformas (repo Orbisfit)

## Objetivo

TotalPass e Wellhub (Gympass) na validação de catraca — implementado no `alliance-bertioga-dash`. O agente Toletus **não** contém regras de pass.

## Entregáveis (Orbisfit)

- [x] Migration `tenant_turnstile_pass_config` + `pass_checkins`
- [x] UI flags em Integrações → Catracas
- [x] Webhooks `/api/webhooks/totalpass` e `/wellhub`
- [x] Validação on-demand em `access-validation.service.ts`
- [x] Docs `docs/integracoes/totalpass-catraca.md`, `wellhub-catraca.md`

## Contrato para o agente

Resposta v2 pode incluir `access_source` (`crm` | `totalpass` | `wellhub`). O agente não precisa interpretar.

## Critérios de aceite

- Tenant com flags desligadas → comportamento anterior (só CRM)
- Tenant com TotalPass ligado + check-in webhook → CPF libera na catraca
- Aluno adimplente Orbisfit → libera sem consultar pass

## Referência

- [specs.md](../specs.md) §5.3
- [homologacao-pass-plataformas.md](../../alliance-bertioga-dash/docs/integracoes/homologacao-pass-plataformas.md) (repo dash)
