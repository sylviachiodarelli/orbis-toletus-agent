# UI de configuração local

Assistente web embutido no agente para configurar Orbisfit e a catraca sem editar JSON manualmente.

## Acesso

| Item | Valor padrão |
|------|----------------|
| URL | http://127.0.0.1:5080 |
| Bind | Apenas localhost (`Agent:StatusUiBindAddress`) |
| Porta | `Agent:StatusUiPort` (5080) |

A UI **não** fica exposta na rede da academia — só quem usa o PC da recepção acessa.

Desabilitar (avançado): `Agent:StatusUiEnabled: false` em `appsettings.json`.

## Fluxo na primeira instalação

```text
1. Instalar / iniciar o agente
2. Abrir http://127.0.0.1:5080
3. Criar senha de administrador local (mín. 8 caracteres)
4. Preencher formulário de configuração
5. Testar Orbisfit e testar catraca
6. Salvar
7. Verificar dashboard (SDK conectado, heartbeat)
```

Enquanto `Agent:SetupComplete` for `false` e os campos obrigatórios estiverem vazios, o agente **não** tenta conectar na catraca.

## Campos de configuração

| Campo | Obrigatório | Origem |
|-------|-------------|--------|
| URL da API Orbisfit | Sim | Padrão `https://orbisfit.com` |
| API key | Sim | Orbisfit → Integrações → Catracas |
| Código do dispositivo | Sim | Mesma tela no Orbisfit |
| IP da catraca | Sim | Rede local (ex.: `192.168.0.220`) |
| Número de série | Não | Opcional; SDK pode detectar após conectar |

O login da UI é **local** (senha deste PC). Não é o mesmo login do dashboard Orbisfit — a API key continua sendo a credencial do serviço para validar acessos 24/7.

## Botões de teste

| Botão | O que verifica |
|-------|----------------|
| **Testar Orbisfit** | POST em `/api/turnstile/access-attempt` com a API key informada |
| **Testar catraca** | Conexão TCP na porta LiteNet2 (7878 por padrão) |

## Após salvar

- Valores gravados em `appsettings.json` na pasta do agente.
- `Agent:SetupComplete` passa a `true`.
- O serviço recarrega a configuração; a conexão com a catraca inicia em alguns segundos.
- Se a conexão não atualizar, reinicie: `Restart-Service OrbisToletusAgent`.

## Dashboard

Após login, o painel mostra:

- IP da catraca, status SDK, firmware, serial
- Política offline efetiva
- Última validação bem-sucedida
- Tentativas de acesso recentes (liberado, negado, offline, erro)

Use **Configuração** no topo para alterar valores. **Sair** encerra a sessão da UI.

## Arquivos locais

| Arquivo | Conteúdo |
|---------|----------|
| `appsettings.json` | API key, device code, IP da catraca |
| `%ProgramData%\Orbis\ToletusAgent\setup-auth.json` | Hash da senha admin + segredo de sessão |

A senha admin usa PBKDF2-SHA256. Não armazene a API key em outro lugar além do `appsettings.json` do PC.

## Migração de instalações antigas

Se `appsettings.json` já tiver API key, device code e IP preenchidos, o agente marca `SetupComplete` automaticamente na inicialização. Ainda é necessário criar a senha admin na primeira visita à UI.

## Próxima evolução (v0.2+)

- Login com conta Orbisfit (em vez de senha local)
- Herdar `device_code` / `api_key` da nuvem via `GET /api/turnstile/agent-config`
- Tray app Windows (opcional; a UI web cobre o fluxo principal hoje)
