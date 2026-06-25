# Instalação — Orbis Toletus Agent

Instalação do agente na recepção da academia (Windows 10/11).

## Pré-requisitos

- PowerShell **como Administrador**
- Para build framework-dependent: [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Para build self-contained (padrão): nenhum runtime extra
- PC na mesma rede que a catraca Toletus
- API key e device code cadastrados no Orbisfit

## Gerar pacote para o cliente (recomendado)

Na máquina de desenvolvimento (com .NET 8 SDK):

```powershell
cd installer
.\build-release.ps1
```

Artefatos em `dist\`:

| Arquivo | Uso |
|---------|-----|
| `OrbisToletusAgent-Setup-v0.1.0.exe` | **Instalador de duplo clique** (recomendado para academia) |
| `OrbisToletusAgent-win-x64-v0.1.0.zip` | Pacote legado com `install.ps1` |

**Não precisa instalar .NET** no PC do cliente (pacote self-contained).

Publique o `.exe` (GitHub Releases ou CDN) e configure no Orbisfit:

```env
VITE_TOLETUS_AGENT_DOWNLOAD_URL=https://.../OrbisToletusAgent-Setup-v0.1.0.exe
```

O link aparece em **Orbisfit → Integrações → Catracas**.

## Instalar (na academia — duplo clique)

1. Baixe `OrbisToletusAgent-Setup-v0.1.0.exe` (pelo Orbisfit ou envio manual).
2. Execute com **duplo clique** e aceite o UAC (Administrador).
3. Clique em **Instalar** no assistente.
4. O navegador abrirá **http://127.0.0.1:5080** para configurar API key e IP da catraca.

## Instalar (na academia — a partir do ZIP legado)

PowerShell **como Administrador**, na pasta extraída:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\install.ps1
```

Depois: http://127.0.0.1:5080

## Instalar (desenvolvimento — a partir do código)

```powershell
cd installer
.\install-service.ps1
Start-Service OrbisToletusAgent
```

Opções:

| Parâmetro | Descrição |
|-----------|-----------|
| `-FrameworkDependent` | Publica sem runtime embutido (menor, exige .NET 8 no PC) |
| `-InstallDir` | Pasta de instalação (padrão: `C:\Program Files\Orbis\ToletusAgent`) |

## Configurar (UI local — recomendado)

1. No PC da recepção, abra o navegador: **http://127.0.0.1:5080**
2. Crie a **senha de administrador local** (primeira visita).
3. Preencha:
   - URL Orbisfit (`https://orbisfit.com`)
   - **API key** e **código do dispositivo** (Orbisfit → Integrações → Catracas)
   - **IP da catraca** na rede local (ex.: `192.168.0.220`)
4. Clique em **Testar Orbisfit** e **Testar catraca**.
5. **Salvar configuração**.

Guia completo: [docs/setup-ui.md](../docs/setup-ui.md).

### Alternativa: editar JSON manualmente

```powershell
notepad "C:\Program Files\Orbis\ToletusAgent\appsettings.json"
Restart-Service OrbisToletusAgent
```

Preencha `Orbis:ApiKey`, `Orbis:DeviceCode`, `Toletus:Ip` e defina `Agent:SetupComplete: true`.

## Verificar

```powershell
.\preflight-check.ps1
Get-Service OrbisToletusAgent
Get-Content "$env:ProgramData\Orbis\ToletusAgent\health.json"
```

Também confira o dashboard em http://127.0.0.1:5080 (SDK conectado, tentativas recentes).

`preflight-check.ps1` valida TCP na catraca (porta 7878), conectividade Orbisfit e exibe `health.json`.

Checklist pós-instalação:

- [ ] UI em http://127.0.0.1:5080 acessível e configuração salva
- [ ] `health.json` mostra `sdkConnected: true` após alguns segundos
- [ ] Log em `%ProgramData%\Orbis\ToletusAgent\logs\` contém `Heartbeat`
- [ ] Teste com aluno adimplente na catraca

Teste HTTP Orbisfit (substitua a API key):

```powershell
curl.exe -X POST "https://orbisfit.com/api/turnstile/access-attempt" `
  -H "Content-Type: application/json" `
  -H "x-api-key: SUA-API-KEY" `
  -d "{\"credential\":{\"type\":\"ENROLLID\",\"value\":\"50\"},\"device\":{\"device_code\":\"CATRACA-01\"}}"
```

## Desinstalar

```powershell
.\uninstall-service.ps1
.\uninstall-service.ps1 -RemoveFiles
```

## Debug em console (sem serviço)

```powershell
cd ..
dotnet run --project src/Orbis.ToletusAgent
```

Abra http://127.0.0.1:5080 para configurar.

## Logs e diagnóstico

| Item | Caminho |
|------|---------|
| Logs rotativos (JSON) | `%ProgramData%\Orbis\ToletusAgent\logs\` |
| Status rápido | `%ProgramData%\Orbis\ToletusAgent\health.json` |
| Senha da UI | `%ProgramData%\Orbis\ToletusAgent\setup-auth.json` |
| Painel web | http://127.0.0.1:5080 |

Reiniciar após mudar config manualmente (ou use **Reiniciar aplicativo** no painel):

```powershell
Restart-Service OrbisToletusAgent
```

Alterações feitas pela UI são salvas em `appsettings.json` e recarregadas automaticamente.

## Recuperação automática (sem PowerShell)

O agente tenta se recuperar sozinho quando a catraca ou o aplicativo travam:

| Camada | Comportamento |
|--------|----------------|
| **SDK desconectado** | A cada ~30 s verifica; após **90 s** força reconexão com a catraca |
| **Falhas repetidas** | Após **8** tentativas ou **15 min** desconectado, reinicia o processo |
| **Serviço Windows** | Se o processo cair, o Windows reinicia o serviço em até **1 min** (configurado na instalação) |

No painel http://127.0.0.1:5080 (dashboard), botões para usuário da academia:

- **Reconectar catraca** — reconexão manual do SDK
- **Reiniciar aplicativo** — reinicia o agente sem abrir PowerShell (aguarde ~1 min)

Instalações antigas: reinstale o `.exe` por cima **ou** rode uma vez como Administrador:

```powershell
sc.exe failure OrbisToletusAgent reset= 86400 actions= restart/60000/restart/60000/restart/60000
```
