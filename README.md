# Orbis Toletus Agent

Serviço Windows on-prem que conecta catracas **Toletus LiteNet2** à API do **Orbisfit**.

## O que faz

1. Conecta na catraca na rede local (LiteNet2).
2. Recebe tentativas de acesso (ID no teclado, digital, RFID).
3. Valida no Orbisfit via HTTPS.
4. Libera ou nega o giro na catraca.
5. Registra logs locais e expõe painel de status.

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (desenvolvimento)
- Windows 10/11 (produção)
- PC na mesma rede local que a catraca Toletus
- Catraca cadastrada no Orbisfit (API key + device code)

## Configuração (recomendado: UI local)

Na primeira instalação os campos começam **em branco**. Use o assistente web no próprio PC:

1. Inicie o agente (console ou serviço Windows).
2. Abra no navegador: **http://127.0.0.1:5080**
3. Crie a **senha de administrador local** (protege a UI neste computador).
4. Preencha:
   - URL da API Orbisfit (padrão: `https://orbisfit.com`)
   - **API key** e **código do dispositivo** — copie em Orbisfit → Integrações → Catracas
   - **IP da catraca** na LAN (ex.: `192.168.0.220`)
   - Número de série (opcional; pode ser detectado após conectar)
5. Use **Testar Orbisfit** e **Testar catraca** antes de salvar.
6. Clique em **Salvar configuração**.

Guia detalhado: [docs/setup-ui.md](docs/setup-ui.md).

### Alternativa: editar `appsettings.json`

```powershell
copy src\Orbis.ToletusAgent\appsettings.example.json src\Orbis.ToletusAgent\appsettings.json
```

Nunca commite `appsettings.json` com API keys (arquivo está no `.gitignore`).

Variáveis de ambiente (sobrescrevem o arquivo):

| Variável | Config |
|----------|--------|
| `ORBIS_API_BASE_URL` | `Orbis:ApiBaseUrl` |
| `ORBIS_API_KEY` | `Orbis:ApiKey` |
| `ORBIS_DEVICE_CODE` | `Orbis:DeviceCode` |
| `TOLETUS_IP` | `Toletus:Ip` |
| `TOLETUS_SERIAL` | `Toletus:SerialNumber` |

## Executar em modo console (dev)

```powershell
dotnet run --project src/Orbis.ToletusAgent
```

- Log `Agent started in setup mode` → abra http://127.0.0.1:5080 para configurar.
- Log `Agent started. Turnstile target ...` → agente configurado e em execução.

## Build e testes

```powershell
dotnet build
dotnet test
```

## Instalação em produção

**Academia (recomendado):** baixe o instalador em Orbisfit → Integrações → Catracas, ou gere com `installer\build-release.ps1` e publique o `.exe`. Duplo clique, aceite o UAC, clique em **Instalar** e configure em http://127.0.0.1:5080.

**Desenvolvimento / script:**

```powershell
cd installer
.\install-service.ps1
Start-Service OrbisToletusAgent
```

Depois configure pela UI em http://127.0.0.1:5080 no PC da recepção.

Detalhes: [installer/README.md](installer/README.md) · Piloto: [docs/piloto-academia-runbook.md](docs/piloto-academia-runbook.md)

## Logs, status e arquivos locais

| Item | Caminho padrão |
|------|----------------|
| Logs (JSON rotativo) | `%ProgramData%\Orbis\ToletusAgent\logs\` |
| Heartbeat / status | `%ProgramData%\Orbis\ToletusAgent\health.json` |
| Senha da UI local | `%ProgramData%\Orbis\ToletusAgent\setup-auth.json` |
| Painel web | http://127.0.0.1:5080 (somente neste PC) |

Heartbeat a cada `Agent:HeartbeatIntervalSeconds` (padrão 60s).

## SDK Toletus (LiteNet2)

Pacote NuGet: [`Toletus.LiteNet2`](https://www.nuget.org/packages/Toletus.LiteNet2) (`.NET 8`).

Com configuração válida e catraca na rede, o agente conecta em `Toletus:Ip` e registra firmware/série quando disponível.

## Documentação

| Documento | Conteúdo |
|-----------|----------|
| [specs.md](specs.md) | Especificação do produto |
| [docs/setup-ui.md](docs/setup-ui.md) | Assistente de configuração e login local |
| [installer/README.md](installer/README.md) | Instalação como serviço Windows |
| [docs/piloto-academia-runbook.md](docs/piloto-academia-runbook.md) | Roteiro de teste com hardware |
| [tasks/](tasks/) | Plano de implementação |
| [AGENTS.md](AGENTS.md) | Guia para agentes de código |
