# Task 14 — Teste piloto na academia

## Objetivo

Validar ponta a ponta com hardware real (Toletus IP 192.168.0.220, série 7218).

## Pré-requisitos

- [ ] Orbisfit: catraca cadastrada, API key ativa
- [ ] Configuração salva na UI (http://127.0.0.1:5080) ou em `appsettings.json`
- [ ] Aluno teste com `enrollid` = ID na catraca (ex.: `50`)
- [ ] Aluno inadimplente de teste (para validar bloqueio)
- [ ] PC recepção na mesma rede /24 que a catraca
- [ ] Agente instalado (task 10)

## Roteiro de teste

| # | Cenário | Esperado |
|---|---------|----------|
| 1 | Aluno adimplente, ID correto | Giro liberado + log Orbisfit `autorizado: true` |
| 2 | Aluno inadimplente | Giro negado + mensagem na API |
| 3 | ID não cadastrado | Negado |
| 4 | Desconectar internet do PC | Comportamento conforme `fail_closed` / `fail_open` |
| 5 | Reconectar internet | Volta a validar online |
| 6 | Duas leituras em 1s | Apenas uma chamada HTTP (debounce) |
| 7 | Reiniciar catraca | Agente reconecta automaticamente |

## Evidências

- [ ] Log local do agente (arquivo)
- [ ] Dashboard UI (http://127.0.0.1:5080) com tentativas recentes
- [ ] Registro em `catraca_logs_acesso` no Supabase
- [ ] Screenshot ou vídeo curto do giro (opcional)

## Critérios de aceite

- 7 cenários executados e documentados
- Issues abertas para bugs encontrados

## Dependências

- Tasks 01–11 concluídas
- Task 13 opcional (offline por tenant)

## Runbook

Siga [docs/piloto-academia-runbook.md](../docs/piloto-academia-runbook.md), configure via [docs/setup-ui.md](../docs/setup-ui.md) e execute `installer/preflight-check.ps1` antes do piloto.

## Referência

- [specs.md](../specs.md) §11 Setup operacional
