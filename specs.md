# specs.md — Orbis Toletus Agent

Especificação do agente on-prem que integra catracas **Toletus / Actuar (LiteNet2)** ao **Orbisfit** sem depender de softwares de terceiros.

## 1. Contexto

### Problema

Catracas Toletus comunicam na rede local via protocolo **LiteNet2**. O Orbisfit expõe API HTTP na nuvem (`/api/verificar-acesso-catraca`, `/api/turnstile/access-attempt`), mas não alcança IPs locais como `192.168.0.220`.

### Solução

Aplicação **separada** do dashboard Orbisfit, instalada como **serviço Windows** no PC fixo da recepção da academia. O agente:

1. Conecta na catraca via SDK Toletus (`litenet2-integrationpackage`).
2. Recebe tentativas de acesso (digital, teclado/ID numérico, RFID se disponível).
3. Valida no Orbisfit via HTTPS + `x-api-key`.
4. Libera ou nega o giro conforme resposta da API.
5. Registra logs locais e envia heartbeat.

### Hardware de referência (piloto)

| Campo | Valor observado |
|-------|-----------------|
| Marca | Toletus (Actuar Group) |
| Firmware | V2.2.2 R0 |
| IP | 192.168.0.220 |
| Série | 7218 |
| Interface | Teclado numérico + display LCD |
| Leitor | Biometria (faixa vertical no painel) |

Compatível com linha **LiteNet2 com teclado**.

---

## 2. Arquitetura

```text
┌─────────────────────────┐         HTTPS          ┌─────────────────────────┐
│  PC recepção (LAN)      │ ──────────────────────▶│  Orbisfit (nuvem)       │
│  orbis-toletus-agent    │  POST /api/turnstile/  │  API Node + Supabase    │
│  Windows Service (.NET) │       access-attempt   │  Regras + logs          │
└───────────┬─────────────┘◀──────────────────────└─────────────────────────┘
            │ LiteNet2 (TCP/IP)
            ▼
┌─────────────────────────┐
│  Catraca Toletus          │
│  192.168.0.220            │
└─────────────────────────┘
```

### Responsabilidades

| Componente | Responsabilidade |
|------------|------------------|
| **Orbisfit** | Fonte de verdade: aluno, plano, financeiro, credenciais, política offline, logs em `catraca_logs_acesso` / `catraca_events` |
| **orbis-toletus-agent** | Ponte LAN: eventos do equipamento, chamada HTTP, comando de liberação, cache de política offline, logs locais |

### O que este repositório **não** é

- Não é o dashboard Orbisfit (`alliance-bertioga-dash`).
- Não contém regras de negócio de adimplência (isso fica na API Orbisfit).
- Não armazena templates biométricos nem imagens.

---

## 3. Stack técnica

| Item | Decisão |
|------|---------|
| Linguagem | C# / .NET 8 |
| Tipo de app | Windows Service (`Microsoft.Extensions.Hosting.WindowsServices`) |
| SDK hardware | [toletus/litenet2-integrationpackage](https://github.com/toletus/litenet2-integrationpackage) |
| HTTP client | `HttpClient` + `IHttpClientFactory` |
| Config | `appsettings.json` + variáveis de ambiente + UI local |
| UI | Painel web `http://127.0.0.1:5080` (setup + dashboard) |
| Logs | Serilog → arquivo rotativo em `%ProgramData%` |
| Instalação | Instalador `.exe` de duplo clique (`OrbisToletusAgent-Setup`) ou script PowerShell legado |

---

## 4. Configuração

### 4.1 Primeira instalação (UI local — recomendado)

O agente expõe um assistente em **http://127.0.0.1:5080** (somente localhost).

| Etapa | Ação |
|-------|------|
| 1 | Criar senha de administrador local (protege a UI neste PC) |
| 2 | Informar API key e device code (copiar do Orbisfit) |
| 3 | Informar IP da catraca na LAN |
| 4 | Testar Orbisfit e catraca |
| 5 | Salvar → grava `appsettings.json` e define `Agent:SetupComplete: true` |

Enquanto não configurado, o serviço sobe em **modo setup** e não conecta na catraca.

Detalhes operacionais: [docs/setup-ui.md](docs/setup-ui.md).

### 4.2 Arquivo `appsettings.json`

Valores iniciais em instalação nova (campos em branco):

```json
{
  "Orbis": {
    "ApiBaseUrl": "https://orbisfit.com",
    "ApiKey": "",
    "DeviceCode": "",
    "AccessAttemptPath": "/api/turnstile/access-attempt",
    "LegacyValidationPath": "/api/verificar-acesso-catraca",
    "UseV2Endpoint": true,
    "TimeoutMs": 5000,
    "MaxRetries": 2,
    "RetryBackoffMs": [500, 1500]
  },
  "Toletus": {
    "Ip": "",
    "Port": 0,
    "SerialNumber": ""
  },
  "Agent": {
    "DebounceMs": 3000,
    "PolicyCacheMinutes": 10,
    "HeartbeatIntervalSeconds": 60,
    "DefaultOfflineMode": "fail_closed",
    "SetupComplete": false,
    "StatusUiEnabled": true,
    "StatusUiPort": 5080,
    "StatusUiBindAddress": "127.0.0.1"
  }
}
```

Validação estrita (`ApiKey`, `DeviceCode`, `Toletus:Ip`) só ocorre quando `Agent:SetupComplete` é `true`.

### 4.3 Variáveis de ambiente (sobrescrevem appsettings)

| Variável | Descrição |
|----------|-----------|
| `ORBIS_API_BASE_URL` | URL base da API |
| `ORBIS_API_KEY` | UUID da catraca (header `x-api-key`) |
| `ORBIS_DEVICE_CODE` | Código do dispositivo no CRM |
| `TOLETUS_IP` | IP da catraca na LAN |
| `TOLETUS_SERIAL` | Número de série (opcional, para logs) |

**Nunca** commitar API keys no repositório.

---

## 5. Fluxo de acesso

```text
1. Usuário apresenta credencial (digital / ID no teclado / RFID)
2. SDK LiteNet2 dispara evento no agente
3. Agente aplica debounce (ignora leitura duplicada < 3s)
4. Agente monta payload e chama Orbisfit
5. Orbisfit valida API key → tenant → credencial → financeiro → política
6. Orbisfit responde authorized / autorizado
7. Agente executa comando na catraca (liberar ou negar)
8. Agente registra resultado localmente
```

### 5.1 Mapeamento credencial

| Origem na catraca | `credential.type` | Campo no Orbisfit |
|-------------------|-------------------|-------------------|
| Display "Id 50" / teclado | `ENROLLID` | `student_access_credentials.external_id` ou `crm.enrollid` |
| Digital biométrica | `FINGERPRINT` | credencial tipo `FINGERPRINT` |
| Cartão RFID | `RFID` | credencial tipo `RFID` |
| CPF digitado | `CPF` | `crm.cpf` |

**MVP:** priorizar `ENROLLID` (ID numérico exibido no display) e `FINGERPRINT`.

### 5.2 Payload v2 (canônico)

```http
POST {ApiBaseUrl}/api/turnstile/access-attempt
Content-Type: application/json
x-api-key: {ApiKey}
```

```json
{
  "transaction_id": "toletus-7218-1739123456789",
  "direction": "IN",
  "device": {
    "device_code": "CATRACA-01",
    "modelo": "toletus",
    "ip": "192.168.0.220"
  },
  "credential": {
    "type": "ENROLLID",
    "value": "50"
  },
  "event": {
    "kind": "ACCESS_ATTEMPT",
    "timestamp": "2026-06-16T12:00:00.000Z"
  },
  "raw_payload": {
    "vendor": "toletus",
    "firmware": "V2.2.2 R0",
    "serial": "7218",
    "agent_version": "0.1.0"
  }
}
```

### 5.3 Resposta esperada

```json
{
  "ok": true,
  "authorized": true,
  "message": "Acesso liberado",
  "status_pagamento": "em_dia",
  "student_name": "João Silva",
  "enrollid": "50",
  "device_id": "CATRACA-01",
  "transaction_id": "toletus-7218-1739123456789",
  "offline_mode": "fail_closed",
  "access_source": "crm"
}
```

Campo opcional `access_source`: `crm` | `totalpass` | `wellhub`. O agente **ignora** este campo; a validação de planos parceiros (TotalPass / Wellhub) é feita exclusivamente na API Orbisfit. Alunos pass podem digitar **CPF** no teclado Toletus ou usar digital vinculada no CRM.

Campos de compatibilidade v1 (`autorizado`, `permitido`, `resultado`) devem ser aceitos pelo agente como fallback.

---

## 6. Política offline (por tenant)

Comportamento quando a API Orbisfit está inacessível:

| Modo | Comportamento |
|------|---------------|
| `fail_closed` | Bloqueia acesso (padrão, mais seguro) |
| `fail_open` | Libera giro (risco operacional; só se tenant configurar) |

### Resolução da política

1. Orbisfit devolve `offline_mode` na resposta de validação (dependência no repo `alliance-bertioga-dash`).
2. Agente cacheia por `PolicyCacheMinutes` (default 10 min).
3. Se API indisponível e sem cache: usar `DefaultOfflineMode` (`fail_closed`).

---

## 7. Retries e idempotência

| Situação | Ação |
|----------|------|
| Timeout / 5xx | Retry até `MaxRetries` com backoff |
| 401 / 403 | Sem retry; log de erro + alerta local |
| Leitura duplicada (&lt; DebounceMs) | Ignorar |
| `transaction_id` | UUID ou `{serial}-{timestamp}` por tentativa |

---

## 8. Logging e observabilidade

### Log local (agente)

- Tentativa de acesso (credential type/value mascarado se CPF)
- Request/response HTTP (status, latência; sem API key)
- Comando enviado à catraca (liberar/negar)
- Erros de rede e SDK
- Heartbeat periódico (agente vivo, IP catraca, última validação OK)

### UI local (setup + dashboard)

| Recurso | Descrição |
|---------|-----------|
| URL | `http://127.0.0.1:{StatusUiPort}` (padrão 5080) |
| Autenticação | Senha de administrador local (PBKDF2 em `setup-auth.json`) |
| Setup | Formulário Orbisfit + catraca, testes de conectividade |
| Dashboard | Status SDK, tentativas recentes, link para reconfigurar |

A UI não substitui o serviço Windows — roda no mesmo processo, bind apenas em localhost.

### Log remoto (Orbisfit)

Orbisfit já persiste em `catraca_logs_acesso` e `catraca_events` via API — o agente não escreve direto no Supabase.

---

## 9. Escopo por versão

### v0.1 — MVP

- [x] Projeto .NET 8 Windows Service
- [x] Conexão LiteNet2 com 1 catraca
- [x] Eventos ENROLLID + FINGERPRINT
- [x] Cliente HTTP Orbisfit (v2 + fallback v1)
- [x] Liberar / negar giro
- [x] Política offline com cache
- [x] Logs locais + heartbeat
- [x] Instalador básico Windows
- [x] UI local de setup e dashboard (`http://127.0.0.1:5080`)

### v0.2

- [ ] Múltiplas catracas por agente
- [ ] Login Orbisfit na UI (herdar device_code / api_key da nuvem)
- [ ] Endpoint `GET /api/turnstile/agent-config` no Orbisfit
- [ ] Tray app Windows (opcional; complementar à UI web)
- [ ] Mensagem customizada no display da catraca

### v0.3

- [ ] Sync de usuários CRM → catraca (cadastro remoto)
- [ ] Modo offline com lista de autorizados em cache
- [ ] Painel de status no Orbisfit (último heartbeat do agente)

---

## 10. Dependências no Orbisfit

Alterações mínimas no repo `alliance-bertioga-dash` (fora deste projeto):

| Item | Descrição |
|------|-----------|
| Migration | `tenant_financial_policy.turnstile_offline_mode` (`fail_closed` \| `fail_open`) |
| API | Incluir `offline_mode` na resposta de `/api/turnstile/access-attempt` |
| UI | Opção em configurações financeiras / catraca (opcional v0.1) |
| Docs | Link para instalação do agente |

O agente **deve funcionar** mesmo antes dessas mudanças, usando `DefaultOfflineMode` local.

---

## 11. Setup operacional (academia)

1. Cadastrar catraca em **Orbisfit → Integrações → Catracas** (fabricante: Genérica; modo: Validação online).
2. Copiar **API key** e **device code**.
3. Cadastrar alunos com credencial (`enrollid` = ID na catraca, ex.: `50`).
4. Instalar agente no PC da recepção (mesma rede que a catraca).
5. Iniciar o serviço Windows (`Start-Service OrbisToletusAgent`).
6. Abrir **http://127.0.0.1:5080** no PC da recepção:
   - Criar senha de administrador local
   - Preencher API key, device code e IP da catraca
   - Testar e salvar
7. Confirmar no dashboard: `SDK: Conectado` e heartbeat em `health.json`.
8. Testar com aluno adimplente e inadimplente.

Alternativa sem UI: editar `appsettings.json` manualmente (ver §4.2).

---

## 12. Referências

- Orbisfit API catraca: `alliance-bertioga-dash/docs/integracoes/catraca-especificacao.md`
- Plano integração: `alliance-bertioga-dash/docs/PLANO_INTEGRACAO_CATRACAS_CRM.md`
- SDK Toletus: [github.com/toletus/litenet2-integrationpackage](https://github.com/toletus/litenet2-integrationpackage)
- Manuais LiteNet2: [github.com/toletus/litenet2-manuaisdeintegracao](https://github.com/toletus/litenet2-manuaisdeintegracao)
- Contato Toletus: integracao@toletus.com

---

## 13. Tarefas de implementação

Ver pasta [`tasks/`](tasks/) — um arquivo por entrega.
