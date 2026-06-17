# Task 10 — Instalador e deploy

## Objetivo

Permitir instalação na academia sem Visual Studio — script ou MSI.

## Entregáveis

- [ ] `installer/install-service.ps1`:
  - publicar `dotnet publish -c Release -r win-x64 --self-contained` (ou framework-dependent)
  - criar diretório `C:\Program Files\Orbis\ToletusAgent`
  - copiar binários + `appsettings.example.json`
  - registrar serviço Windows (`New-Service` ou `sc create`)
  - instruções para editar config e reiniciar
- [ ] `installer/uninstall-service.ps1`
- [ ] `installer/README.md` com passo a passo para recepção
- [ ] Checklist pós-instalação (ping IP catraca, teste curl Orbisfit)

## Configuração pós-instalação

1. Copiar `appsettings.example.json` → `appsettings.json`
2. Preencher `Orbis.ApiKey`, `Orbis.DeviceCode`, `Toletus.Ip`
3. `Restart-Service OrbisToletusAgent`

## Critérios de aceite

- Instalação em Windows 10/11 limpo sem .NET SDK instalado (se self-contained)
- Desinstalação remove serviço e opcionalmente arquivos
- Script exige execução como Administrador

## Dependências

- Task 09

## Referência

- [specs.md](../specs.md) §11 Setup operacional
