#Requires -Version 5.1
<#
.SYNOPSIS
  Publica orbis-toletus-agent no GitHub e gera release v0.1.0 com o instalador.

.PREREQUISITES
  1. Repositório vazio criado no GitHub (ex.: sylviachiodarelli/orbis-toletus-agent)
  2. gh auth login  (ou SSH com acesso de push)
  3. .NET 8 SDK local (para gerar o .exe antes do tag, se dist/ estiver vazio)

.EXAMPLE
  .\installer\publish-github.ps1 -Repo "sylviachiodarelli/orbis-toletus-agent"
#>
param(
    [string]$Repo = "sylviachiodarelli/orbis-toletus-agent",
    [string]$Tag = "v0.1.2",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Set-Location $RepoRoot

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build-release.ps1")
}

$Version = $Tag.TrimStart('v')
$SetupExe = Join-Path $RepoRoot "dist\OrbisToletusAgent-Setup-v$Version.exe"
if (-not (Test-Path $SetupExe)) {
    throw "Instalador não encontrado: $SetupExe. Rode build-release.ps1 primeiro."
}

git remote remove origin 2>$null
git remote add origin "git@github.com:$Repo.git"

Write-Host "Enviando código para github.com/$Repo ..."
git push -u origin main

Write-Host "Criando tag $Tag (dispara GitHub Actions release) ..."
git tag -a $Tag -m "Orbis Toletus Agent $Tag" -f
git push origin $Tag -f

$DownloadUrl = "https://github.com/$Repo/releases/download/$Tag/OrbisToletusAgent-Setup-v$Version.exe"

Write-Host ""
Write-Host "Próximo passo no Dokploy (Orbisfit):" -ForegroundColor Green
Write-Host "  VITE_TOLETUS_AGENT_DOWNLOAD_URL=$DownloadUrl"
Write-Host ""
Write-Host "Redeploy do serviço web (rebuild completo)."
Write-Host ""
Write-Host "Se o workflow ainda estiver rodando, aguarde o release em:"
Write-Host "  https://github.com/$Repo/releases/tag/$Tag"
