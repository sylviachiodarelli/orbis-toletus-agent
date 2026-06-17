#Requires -RunAsAdministrator

param(
    [string]$InstallDir = "C:\Program Files\Orbis\ToletusAgent",
    [switch]$RemoveFiles
)

$ErrorActionPreference = "Stop"
$ServiceName = "OrbisToletusAgent"

$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService) {
    Write-Host "Stopping service $ServiceName..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Write-Host "Removing service $ServiceName..."
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
} else {
    Write-Host "Service $ServiceName is not installed."
}

if ($RemoveFiles -and (Test-Path $InstallDir)) {
    Write-Host "Removing $InstallDir..."
    Remove-Item -Path $InstallDir -Recurse -Force
}

Write-Host "Uninstall complete."
