#Requires -Version 5.1

param(
    [string]$AgentOutputDir = "",
    [string]$SetupOutputDir = "",
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectPath = Join-Path $RepoRoot "src\Orbis.ToletusAgent\Orbis.ToletusAgent.csproj"
$SetupProjectPath = Join-Path $RepoRoot "src\Orbis.ToletusAgent.Setup\Orbis.ToletusAgent.Setup.csproj"
$Version = "0.1.3"

if ([string]::IsNullOrWhiteSpace($AgentOutputDir)) {
    $AgentOutputDir = Join-Path $RepoRoot "dist\agent-payload"
}

if ([string]::IsNullOrWhiteSpace($SetupOutputDir)) {
    $SetupOutputDir = Join-Path $RepoRoot "dist"
}

$PayloadZip = Join-Path $RepoRoot "src\Orbis.ToletusAgent.Setup\Resources\agent-payload.zip"
$SetupExeName = "OrbisToletusAgent-Setup-v$Version.exe"
$SetupExePath = Join-Path $SetupOutputDir $SetupExeName

$Dotnet = "${env:ProgramFiles}\dotnet\dotnet.exe"
if (-not (Test-Path $Dotnet)) {
    throw "dotnet SDK not found at $Dotnet. Install .NET 8 SDK or add dotnet to PATH."
}

Write-Host "Publishing agent (self-contained win-x64)..."
& $Dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true -o $AgentOutputDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for Orbis.ToletusAgent."
}

Copy-Item -Path (Join-Path $RepoRoot "src\Orbis.ToletusAgent\appsettings.example.json") `
    -Destination (Join-Path $AgentOutputDir "appsettings.example.json") -Force

Write-Host "Creating embedded payload ZIP..."
$ResourcesDir = Split-Path $PayloadZip -Parent
New-Item -ItemType Directory -Force -Path $ResourcesDir | Out-Null
if (Test-Path $PayloadZip) {
    Remove-Item $PayloadZip -Force
}
Compress-Archive -Path (Join-Path $AgentOutputDir "*") -DestinationPath $PayloadZip -Force

Write-Host "Building double-click installer..."
New-Item -ItemType Directory -Force -Path $SetupOutputDir | Out-Null
& $Dotnet publish $SetupProjectPath -c Release -r win-x64 --self-contained true -o $SetupOutputDir /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for Orbis.ToletusAgent.Setup."
}

$PublishedSetup = Join-Path $SetupOutputDir "OrbisToletusAgent-Setup.exe"
if (-not (Test-Path $PublishedSetup)) {
    throw "Setup executable not found at $PublishedSetup"
}

if (Test-Path $SetupExePath) {
    Remove-Item $SetupExePath -Force
}
Move-Item -Path $PublishedSetup -Destination $SetupExePath -Force

Write-Host "Adding legacy ZIP package (optional)..."
$LegacyDir = Join-Path $RepoRoot "dist\OrbisToletusAgent-win-x64-v$Version"
if (Test-Path $LegacyDir) {
    Remove-Item $LegacyDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $LegacyDir | Out-Null
Copy-Item -Path (Join-Path $AgentOutputDir "*") -Destination $LegacyDir -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot "install-from-package.ps1") -Destination (Join-Path $LegacyDir "install.ps1") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "uninstall-service.ps1") -Destination (Join-Path $LegacyDir "uninstall.ps1") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "preflight-check.ps1") -Destination (Join-Path $LegacyDir "preflight-check.ps1") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "LEIA-ME-CLIENTE.txt") -Destination (Join-Path $LegacyDir "LEIA-ME.txt") -Force
Copy-Item -Path $SetupExePath -Destination (Join-Path $LegacyDir $SetupExeName) -Force

if (-not $SkipZip) {
    $LegacyZipPath = "$LegacyDir.zip"
    if (Test-Path $LegacyZipPath) {
        Remove-Item $LegacyZipPath -Force
    }
    Compress-Archive -Path $LegacyDir -DestinationPath $LegacyZipPath -Force
}

Write-Host ""
Write-Host "Release ready:" -ForegroundColor Green
Write-Host "  Installer (double-click): $SetupExePath"
if (-not $SkipZip) {
    Write-Host "  Legacy ZIP:               $LegacyZipPath"
}
Write-Host ""
Write-Host "Host the Setup.exe and set VITE_TOLETUS_AGENT_DOWNLOAD_URL in Orbisfit to the public URL."
