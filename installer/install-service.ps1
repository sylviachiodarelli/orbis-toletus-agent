#Requires -RunAsAdministrator

param(
    [string]$InstallDir = "C:\Program Files\Orbis\ToletusAgent",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$ServiceName = "OrbisToletusAgent"
$ProjectPath = Join-Path $PSScriptRoot "..\src\Orbis.ToletusAgent\Orbis.ToletusAgent.csproj"
$PublishDir = Join-Path $PSScriptRoot "..\publish"

Write-Host "Publishing Orbis Toletus Agent..."
if ($FrameworkDependent) {
    dotnet publish $ProjectPath -c Release -o $PublishDir
} else {
    dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true -o $PublishDir
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish output not found at $PublishDir"
}

Write-Host "Creating install directory $InstallDir..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

Write-Host "Copying files..."
Copy-Item -Path (Join-Path $PublishDir "*") -Destination $InstallDir -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot "..\src\Orbis.ToletusAgent\appsettings.example.json") -Destination (Join-Path $InstallDir "appsettings.example.json") -Force

$ConfigPath = Join-Path $InstallDir "appsettings.json"
if (-not (Test-Path $ConfigPath)) {
    Copy-Item -Path (Join-Path $InstallDir "appsettings.example.json") -Destination $ConfigPath
    Write-Host "Created appsettings.json from example. Edit API key and turnstile IP before starting."
}

$ExePath = Join-Path $InstallDir "Orbis.ToletusAgent.exe"
if (-not (Test-Path $ExePath)) {
    throw "Executable not found at $ExePath"
}

$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force
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

# AutomaticDelayedStart is not supported by New-Service on Windows PowerShell 5.1.
$scResult = sc.exe config $ServiceName start= delayed-auto 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Could not set delayed-auto startup (service will use Automatic): $scResult"
}

Write-Host "Configuring automatic service recovery..."
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Could not configure service recovery actions."
}

Write-Host ""
Write-Host "Installation complete."
Write-Host "1. Run: Start-Service $ServiceName"
Write-Host "2. Open http://127.0.0.1:5080 to configure (or edit $ConfigPath)"
Write-Host "3. Set Orbis:ApiKey, Orbis:DeviceCode, Toletus:Ip in the setup UI"
Write-Host ""
Write-Host "Logs: $env:ProgramData\Orbis\ToletusAgent\logs\"
Write-Host "Health: $env:ProgramData\Orbis\ToletusAgent\health.json"
