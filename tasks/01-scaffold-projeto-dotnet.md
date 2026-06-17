# Task 01 — Scaffold do projeto .NET 8

## Objetivo

Criar a estrutura inicial do repositório com Windows Service host, DI, configuração e testes.

## Entregáveis

- [ ] Solution `Orbis.ToletusAgent.sln`
- [ ] Projeto `src/Orbis.ToletusAgent` (.NET 8, `OutputType=Exe`)
- [ ] Projeto `tests/Orbis.ToletusAgent.Tests` (xUnit)
- [ ] `Program.cs` com `Host.CreateDefaultBuilder` + `UseWindowsService()`
- [ ] `appsettings.json` e `appsettings.Development.json` (sem secrets)
- [ ] `.gitignore` para .NET (bin, obj, user secrets)
- [ ] `README.md` mínimo com pré-requisitos e `dotnet run`

## Estrutura de pastas

```text
src/Orbis.ToletusAgent/
  Program.cs
  ServiceCollectionExtensions.cs
  appsettings.json
tests/Orbis.ToletusAgent.Tests/
```

## Pacotes NuGet

- `Microsoft.Extensions.Hosting.WindowsServices`
- `Microsoft.Extensions.Http`
- `Microsoft.Extensions.Options.ConfigurationExtensions`

## Critérios de aceite

- `dotnet build` sem erros
- `dotnet test` executa (mesmo sem testes ainda)
- Serviço inicia em modo console com `dotnet run` (log "Agent started")

## Dependências

Nenhuma.

## Referência

- [specs.md](../specs.md) §3 Stack técnica
