# AGENTS.md — Orbis Toletus Agent

Guia para agentes de código e desenvolvedores: como entregar features neste repositório sem poluir o projeto.

## Contexto do produto

- **orbis-toletus-agent**: serviço Windows on-prem que conecta catracas **Toletus LiteNet2** à API do **Orbisfit**.
- **Não é** o dashboard Orbisfit (`alliance-bertioga-dash`). Regras de negócio (adimplência, tenant, credenciais) ficam na API na nuvem.
- **Especificação:** [specs.md](specs.md)
- **Tarefas:** [tasks/](tasks/)

## Princípios

1. **Agente burro, Orbisfit inteligente** — o agente traduz protocolo LiteNet2 ↔ HTTP; não replica regras de financeiro ou tenant.
2. **Fail safe** — na dúvida, bloquear (`fail_closed`). `fail_open` só quando o tenant configurar explicitamente.
3. **Sem segredos no repo** — API keys e senhas só em config local ou variáveis de ambiente.
4. **Sem biométrica no agente** — não persistir templates, imagens ou vetores; apenas IDs externos (`enrollid`, `fingerprint_id`).
5. **Uma responsabilidade por classe** — conexão SDK, cliente HTTP e orquestração de acesso em camadas separadas.

## Mapa do repositório (alvo)

| Caminho | Uso |
|---------|-----|
| `src/Orbis.ToletusAgent/` | Projeto principal (Windows Service + UI local) |
| `src/Orbis.ToletusAgent/Setup/` | UI web, login local, persistência de config |
| `src/Orbis.ToletusAgent/Status/` | Store de tentativas recentes (dashboard) |
| `src/Orbis.ToletusAgent.Core/` | Domínio, interfaces, DTOs (opcional se o projeto crescer) |
| `src/Orbis.ToletusAgent.Toletus/` | Adapter SDK LiteNet2 |
| `src/Orbis.ToletusAgent.Orbis/` | Cliente HTTP da API Orbisfit |
| `tests/` | Testes unitários e de integração |
| `installer/` | Scripts MSI / PowerShell de instalação |
| `docs/setup-ui.md` | Guia da UI de configuração local |
| `tasks/` | Especificação de entregas (não é código) |
| `specs.md` | Especificação do produto |

## Stack

| Item | Padrão |
|------|--------|
| Runtime | .NET 8 |
| Hospedagem | `Microsoft.Extensions.Hosting.WindowsServices` |
| HTTP | `IHttpClientFactory` + `HttpClient` |
| Config | `IOptions<T>` + `appsettings.json` + env vars |
| Logs | `ILogger<T>`; Serilog para arquivo rotativo se necessário |
| SDK hardware | NuGet `litenet2-integrationpackage` (Toletus) |

## Padrão de código

### Nomenclatura

- **Namespaces:** `Orbis.ToletusAgent.{Area}` (PascalCase)
- **Classes/interfaces:** PascalCase (`IOrbisApiClient`, `AccessOrchestrator`)
- **Métodos/propriedades:** PascalCase
- **Campos privados:** `_camelCase`
- **Arquivos:** um tipo público principal por arquivo

### Estrutura de serviços

```text
Program.cs                    → bootstrap DI + host + UI web
AccessOrchestrator            → orquestra: evento → API → comando catraca
ToletusDeviceService          → SDK LiteNet2 (conectar, eventos, liberar)
OrbisApiClient                → HTTP para /api/turnstile/access-attempt
OfflinePolicyCache            → cache de turnstile_offline_mode
AgentHealthService            → heartbeat + health.json
AgentConfigurationService     → validação e gravação do setup
SetupUiEndpoints              → login local + wizard + dashboard (:5080)
```

### Injeção de dependência

- Registrar serviços em `Program.cs` ou `ServiceCollectionExtensions.cs`.
- Preferir interfaces para `IOrbisApiClient`, `IToletusDeviceService` — facilita testes.
- `HttpClient` sempre via `IHttpClientFactory`; nunca `new HttpClient()` solto em serviço singleton.

### Configuração

- Classes tipadas: `OrbisOptions`, `ToletusOptions`, `AgentOptions`.
- Validar na inicialização (`IValidateOptions<T>`) **somente quando** `Agent:SetupComplete` é `true`.
- Instalação nova: campos em branco; configurar via UI em `http://127.0.0.1:5080`.
- Variáveis de ambiente sobrescrevem `appsettings.json`.

### Tratamento de erros

- Exceções de rede: log + aplicar política offline; não derrubar o serviço.
- Exceções do SDK Toletus: log + tentar reconectar com backoff.
- Nunca engolir exceção sem log.
- Não usar `catch (Exception)` vazio.

### Async

- Métodos I/O: `async`/`await`; sufixo `Async` em métodos públicos assíncronos.
- Não bloquear com `.Result` ou `.Wait()` em código de produção.

## O que fazer

| Faça | Motivo |
|------|--------|
| Centralizar mapeamento credencial → DTO em um único lugar | Evita divergência ENROLLID/FINGERPRINT/RFID |
| Debounce de leituras duplicadas | Hardware dispara eventos repetidos |
| `transaction_id` único por tentativa | Auditoria e deduplicação futura |
| Log estruturado (campos, não string concatenada) | Facilita suporte em produção |
| Testes unitários para mapeamento e política offline | Lógica crítica sem hardware |
| Mascarar CPF e API key nos logs | LGPD e segurança |
| Documentar breaking changes em `specs.md` | Contrato com Orbisfit |

## O que evitar

| Não faça | Motivo |
|----------|--------|
| Duplicar regras de adimplência / tenant no agente | Fonte de verdade é a API Orbisfit |
| Acessar Supabase direto | Quebra isolamento; use só API HTTP |
| Commitar API keys, senhas ou `appsettings.Production.json` com secrets | Segurança |
| Copiar/colar lógica HTTP em vários handlers | Um `OrbisApiClient` |
| Lógica LiteNet2 espalhada fora de `Toletus/` | Adapter único por fabricante |
| `Thread.Sleep` para retry | Usar `Task.Delay` com cancellation token |
| Dependência de UI para o serviço rodar | Service deve funcionar headless |
| Armazenar templates biométricos | Fora de escopo e risco LGPD |
| Código duplicado entre v1 e v2 da API | Um método com fallback interno |

## Contrato com API Orbisfit

### Endpoints

| Versão | Path | Uso |
|--------|------|-----|
| v2 (preferido) | `POST /api/turnstile/access-attempt` | Payload canônico |
| v1 (fallback) | `POST /api/verificar-acesso-catraca` | Compatibilidade |

### Autenticação

```
x-api-key: {uuid}
Content-Type: application/json
```

### Interpretar resposta

Considerar autorizado se **qualquer** for verdadeiro:

- `authorized === true`
- `autorizado === true`
- `permitido === true`
- `resultado === 1`

Implementar em **um único método** (`AccessDecisionParser`).

## Testes

| Tipo | O quê |
|------|-------|
| Unitário | Mapeamento credencial, parser de resposta, política offline, debounce |
| Integração HTTP | Mock de `HttpMessageHandler` para Orbisfit |
| Manual / E2E | Com catraca física na LAN (não automatizar no CI) |

Comando alvo:

```bash
dotnet test
```

## Nova feature — checklist

1. Ler [specs.md](specs.md) e a task em `tasks/`.
2. Implementar na camada correta (Toletus / Orbis / Core).
3. Adicionar testes para lógica sem hardware.
4. Atualizar `specs.md` se mudar contrato ou config.
5. Não commitar secrets.

## Bugfix — checklist

1. Reproduzir (log local + resposta HTTP).
2. Corrigir na camada certa (rede/SDK vs API vs orquestração).
3. Adicionar teste de regressão se aplicável.
4. Verificar se o bug não deve ser corrigido no Orbisfit em vez do agente.

## Segurança

- API key só em config local ou env var.
- PC da academia deve ter firewall restritivo; agente só precisa saída HTTPS.
- Logs: nunca gravar `x-api-key` completa.
- Atualizações do agente: assinar binário / distribuir por canal confiável (fase posterior).

## Relação com alliance-bertioga-dash

| Este repo | Repo Orbisfit |
|-----------|---------------|
| Consome API de validação | Expõe API de validação |
| Instalado na academia | Hospedado na nuvem |
| SDK Toletus | Sem SDK Toletus |
| Pode ser versionado independente | Releases separados |

Coordenar mudanças de contrato API via `specs.md` e issues vinculadas nos dois repos.

## Comandos úteis (alvo)

```bash
dotnet build
dotnet test
dotnet run --project src/Orbis.ToletusAgent
# Instalar como serviço (após implementar installer):
# .\installer\install-service.ps1
```
