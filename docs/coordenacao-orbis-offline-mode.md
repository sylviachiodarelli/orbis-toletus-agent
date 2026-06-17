# Coordenação — offline_mode no Orbisfit (Task 13)

Esta entrega é no repositório **alliance-bertioga-dash**, não neste agente.

## Contrato esperado

Resposta de `POST /api/turnstile/access-attempt`:

```json
{
  "authorized": true,
  "offline_mode": "fail_closed"
}
```

## Comportamento do agente

| Situação | Agente |
|----------|--------|
| API retorna `offline_mode` | Cacheia por `Agent:PolicyCacheMinutes` |
| API sem `offline_mode` | Usa `Agent:DefaultOfflineMode` (`fail_closed`) |
| API indisponível + cache `fail_open` | Libera giro |
| API indisponível + cache `fail_closed` ou sem cache | Nega giro |

O agente **funciona sem** a migration no Orbisfit; a política por tenant só melhora o comportamento offline.

## Checklist Orbisfit (referência)

- [ ] Migration `tenant_financial_policy.turnstile_offline_mode`
- [ ] `access-validation.service.ts` inclui `offline_mode`
- [ ] `api/turnstile/access-attempt.ts` propaga campo
- [ ] UI opcional em configurações financeiras

Ver [tasks/13-dependencia-orbis-offline-mode.md](../tasks/13-dependencia-orbis-offline-mode.md).
