#Requires -RunAsAdministrator

param(
    [string]$InstallDir = "C:\Program Files\Orbis\ToletusAgent"
)

$ErrorActionPreference = "Stop"
$ServiceName = "OrbisToletusAgent"
$PackageDir = $PSScriptRoot

$SourceExe = Join-Path $PackageDir "Orbis.ToletusAgent.exe"
if (-not (Test-Path $SourceExe)) {
    throw "Orbis.ToletusAgent.exe not found in $PackageDir. Extract the full ZIP before running install.ps1."
}

Write-Host "Installing Orbis Toletus Agent from package..."
Write-Host "Source: $PackageDir"
Write-Host "Target: $InstallDir"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $PackageDir "*") -Destination $InstallDir -Recurse -Force

$ConfigPath = Join-Path $InstallDir "appsettings.json"
$ExamplePath = Join-Path $InstallDir "appsettings.example.json"
if (-not (Test-Path $ConfigPath) -and (Test-Path $ExamplePath)) {
    Copy-Item -Path $ExamplePath -Destination $ConfigPath
}

$ExePath = Join-Path $InstallDir "Orbis.ToletusAgent.exe"

$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Registering Windows service..."
New-Service `
    -Name $ServiceName `
    -BinaryPathName "`"$ExePath`"" `
    -DisplayName "Orbis Toletus Agent" `
    -Description "Ponte de acesso Toletus LiteNet2 para Orbisfit" `
    -StartupType Automatic | Out-Null

$scResult = sc.exe config $ServiceName start= delayed-auto 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Could not set delayed-auto startup: $scResult"
}

Write-Host "Starting service..."
Start-Service -Name $ServiceName

Write-Host ""
Write-Host "Installation complete." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Open http://127.0.0.1:5080 in this computer's browser"
Write-Host "  2. Create the local admin password"
Write-Host "  3. Enter Orbisfit API key, device code, and turnstile IP"
Write-Host "  4. Click Save and verify SDK shows Connected on the dashboard"
Write-Host ""
Write-Host "Logs:  $env:ProgramData\Orbis\ToletusAgent\logs\"
Write-Host "Health: $env:ProgramData\Orbis\ToletusAgent\health.json"
