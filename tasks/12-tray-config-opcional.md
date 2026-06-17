# Task 12 — UI de configuração

## Objetivo

Interface amigável para configurar API key, device code e IP da catraca sem editar JSON manualmente.

## Status

| Entrega | Status |
|---------|--------|
| UI web local (`http://127.0.0.1:5080`) | Concluída |
| Login com senha de administrador local | Concluída |
| Wizard com campos em branco na instalação nova | Concluída |
| Testar Orbisfit / testar catraca | Concluída |
| Dashboard de status e tentativas recentes | Concluída |
| Tray app WPF/WinForms (bandeja Windows) | Pendente v0.2 |
| Login com conta Orbisfit | Pendente v0.2 |

Documentação: [docs/setup-ui.md](../docs/setup-ui.md)

## Entregáveis (UI web — feito)

- [x] Painel em `src/Orbis.ToletusAgent/Setup/` (`SetupUiEndpoints`, `AgentConfigurationService`)
- [x] Campos: ApiBaseUrl, ApiKey, DeviceCode, Toletus IP, Serial (opcional)
- [x] Botão "Testar Orbisfit" e "Testar catraca"
- [x] Salvar em `appsettings.json` + `Agent:SetupComplete`
- [x] Senha admin local em `%ProgramData%\Orbis\ToletusAgent\setup-auth.json`

## Entregáveis (tray — opcional v0.2)

- [ ] Projeto WPF ou WinForms `Orbis.ToletusAgent.Tray` (separado do serviço)
- [ ] Ícone na system tray: verde (OK), amarelo (reconectando), vermelho (erro)

## Critérios de aceite

- UI não é obrigatória para o serviço funcionar (config manual ainda suportada)
- Alteração de config pela UI recarrega `appsettings.json`; reiniciar serviço se conexão não atualizar
- Bind apenas em localhost por padrão

## Dependências

- Task 09, 10

## Referência

- [specs.md](../specs.md) §4 e §8
- [docs/setup-ui.md](../docs/setup-ui.md)
