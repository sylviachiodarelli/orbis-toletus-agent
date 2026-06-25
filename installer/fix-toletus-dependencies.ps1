#Requires -RunAsAdministrator
# Corrige instalação v0.1.0 sem Toletus.Pack.Core / Enums.NET (SDK desconectado).

param(
    [string]$InstallDir = "C:\Program Files\Orbis\ToletusAgent"
)

$ErrorActionPreference = "Stop"

function Install-NuGetDll {
    param(
        [string]$PackageId,
        [string]$Version,
        [string]$DllName
    )

    $work = Join-Path $env:TEMP "orbis-deps\$PackageId.$Version"
    New-Item -ItemType Directory -Force -Path $work | Out-Null

    $zip = Join-Path $work "pack.zip"
    Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/$PackageId/$Version" -OutFile $zip
    Expand-Archive $zip -DestinationPath (Join-Path $work "pack") -Force

    $dll = Get-ChildItem (Join-Path $work "pack") -Recurse -Filter $DllName | Select-Object -First 1
    if (-not $dll) {
        throw "DLL $DllName não encontrada no pacote $PackageId $Version"
    }

    Copy-Item $dll.FullName (Join-Path $InstallDir $DllName) -Force
    Write-Host "[OK] $DllName" -ForegroundColor Green
}

Write-Host "Parando serviço..." -ForegroundColor Cyan
Stop-Service OrbisToletusAgent -Force -ErrorAction SilentlyContinue
Get-Process -Name "Orbis.ToletusAgent" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Copiando dependências Toletus para $InstallDir ..."
Install-NuGetDll -PackageId "Toletus.Pack.Core" -Version "8.15.1" -DllName "Toletus.Pack.Core.dll"
Install-NuGetDll -PackageId "Enums.NET" -Version "5.0.0" -DllName "Enums.NET.dll"

Write-Host ""
Write-Host "DLLs Toletus na pasta de instalação:"
Get-ChildItem $InstallDir -Filter "Toletus*" | ForEach-Object { "  $($_.Name)" }
Get-ChildItem $InstallDir -Filter "Enums.NET.dll" | ForEach-Object { "  $($_.Name)" }

Write-Host ""
Write-Host "Iniciando serviço..." -ForegroundColor Cyan
Start-Service OrbisToletusAgent
Start-Sleep 12

Write-Host ""
Write-Host "health.json:"
Get-Content "$env:ProgramData\Orbis\ToletusAgent\health.json"

Write-Host ""
Write-Host "Se sdkConnected ainda for false, veja o log:" -ForegroundColor Yellow
Write-Host '  Select-String -Path "$env:ProgramData\Orbis\ToletusAgent\logs\agent-*.json" -Pattern "Failed to connect|Exception" | Select -Last 5'
Write-Host ""
Write-Host "Recomendado: reinstalar v0.1.3 completo:" -ForegroundColor Yellow
Write-Host "  https://github.com/sylviachiodarelli/orbis-toletus-agent/releases/download/v0.1.3/OrbisToletusAgent-Setup-v0.1.3.exe"
