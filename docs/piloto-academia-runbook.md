# Piloto na academia — Runbook (Task 14)

Validação ponta a ponta com hardware Toletus **192.168.0.220** (série **7218**).

## Antes de começar

Execute o preflight no PC da recepção:

```powershell
cd installer
.\preflight-check.ps1
```

### Pré-requisitos Orbisfit

- [ ] Catraca cadastrada em **Integrações → Catracas** (modo validação online)
- [ ] API key e device code configurados (via UI em http://127.0.0.1:5080 ou `appsettings.json`)
- [ ] Aluno **adimplente** com `enrollid` = ID na catraca (ex.: `50`)
- [ ] Aluno **inadimplente** de teste
- [ ] PC recepção na mesma rede `/24` que a catraca
- [ ] Agente instalado (`.\install-service.ps1`) ou `dotnet run` em dev

### Instalação rápida

```powershell
cd installer
.\install-service.ps1
Start-Service OrbisToletusAgent
```

No navegador do PC da recepção: **http://127.0.0.1:5080** → criar senha admin → configurar → testar → salvar.

```powershell
.\preflight-check.ps1
```

---

## Roteiro de teste

Preencha a coluna **Resultado** após cada cenário.

| # | Cenário | Passos | Esperado | Resultado | Data/Hora | Observações |
|---|---------|--------|----------|-----------|-----------|-------------|
| 1 | Aluno adimplente | Digitar ID do aluno adimplente no teclado | Giro liberado; log `authorized: true` | | | |
| 2 | Aluno inadimplente | ID do aluno inadimplente | Giro negado; mensagem na API | | | |
| 3 | ID não cadastrado | ID inexistente no Orbisfit | Negado | | | |
| 4 | Sem internet | Desconectar cabo/Wi‑Fi do PC; tentar acesso | `fail_closed` → negado (padrão) | | | |
| 5 | Internet volta | Reconectar rede; tentar acesso | Validação online normal | | | |
| 6 | Debounce | Duas leituras do mesmo ID em &lt; 3s | Uma chamada HTTP nos logs | | | |
| 7 | Reinício catraca | Reiniciar catraca; aguardar 1–2 min | Agente reconecta (`sdkConnected: true`) | | | |

---

## Onde coletar evidências

| Evidência | Caminho / como obter |
|-----------|----------------------|
| Log local | `%ProgramData%\Orbis\ToletusAgent\logs\agent-*.json` |
| Status agente | `%ProgramData%\Orbis\ToletusAgent\health.json` |
| Dashboard UI | http://127.0.0.1:5080 (tentativas recentes, SDK) |
| Log Orbisfit | Supabase → `catraca_logs_acesso` (via dashboard) |
| Giro físico | Screenshot ou vídeo curto (opcional) |

### Comandos úteis

```powershell
Get-Service OrbisToletusAgent
Get-Content "$env:ProgramData\Orbis\ToletusAgent\health.json"
Get-ChildItem "$env:ProgramData\Orbis\ToletusAgent\logs" | Sort-Object LastWriteTime -Descending | Select-Object -First 3
```

**Preferir o painel** http://127.0.0.1:5080 → **Reiniciar aplicativo** ou **Reconectar catraca**.

Suporte técnico (se necessário): `Restart-Service OrbisToletusAgent`

Filtrar log por transação (PowerShell 7+):

```powershell
Get-Content "$env:ProgramData\Orbis\ToletusAgent\logs\agent-*.json" |
  Select-String "Access processed"
```

---

## Critérios de aceite

- [ ] 7 cenários executados e tabela acima preenchida
- [ ] Bugs encontrados registrados como issues no repositório
- [ ] `preflight-check.ps1` passa antes e depois do piloto

---

## Problemas comuns

| Sintoma | Verificar |
|---------|-----------|
| UI não abre em :5080 | Aguarde ~1 min (recuperação automática) ou reinstale o agente; serviço `OrbisToletusAgent` |
| `sdkConnected: false` | Botão **Reconectar catraca** no painel; IP correto; ping `192.168.0.220`; outro software Toletus fechado |
| HTTP 401 | API key errada ou catraca inativa no Orbisfit |
| Sempre negado offline | `Agent:DefaultOfflineMode` = `fail_closed` (esperado sem internet) |
| Duas chamadas HTTP em 1s | `Agent:DebounceMs` (padrão 3000) |

---

## Referências

- [specs.md](../specs.md) §11 Setup operacional
- [docs/setup-ui.md](../docs/setup-ui.md)
- [installer/README.md](../installer/README.md)
- [tasks/14-teste-piloto-academia.md](../tasks/14-teste-piloto-academia.md)
