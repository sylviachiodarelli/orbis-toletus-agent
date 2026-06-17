#Requires -Version 5.1

param(
    [string]$ConfigPath = "",
    [string]$InstallDir = "C:\Program Files\Orbis\ToletusAgent"
)

$ErrorActionPreference = "Continue"

function Resolve-ConfigPath {
    if ($ConfigPath -and (Test-Path $ConfigPath)) {
        return $ConfigPath
    }

    $candidates = @(
        (Join-Path $InstallDir "appsettings.json"),
        (Join-Path $PSScriptRoot "..\src\Orbis.ToletusAgent\appsettings.json")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function Read-JsonConfig {
    param([string]$Path)
    return Get-Content -Raw -Path $Path | ConvertFrom-Json
}

Write-Host "=== Orbis Toletus Agent — Preflight ===" -ForegroundColor Cyan

$configFile = Resolve-ConfigPath
if (-not $configFile) {
    Write-Host "[FAIL] appsettings.json not found." -ForegroundColor Red
    exit 1
}

Write-Host "Config: $configFile"
$config = Read-JsonConfig $configFile

$apiBaseUrl = [string]$config.Orbis.ApiBaseUrl
$apiKey = [string]$config.Orbis.ApiKey
$deviceCode = [string]$config.Orbis.DeviceCode
$turnstileIp = [string]$config.Toletus.Ip
$useV2 = [bool]$config.Orbis.UseV2Endpoint

$failures = 0

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "[FAIL] Orbis:ApiKey is empty." -ForegroundColor Red
    $failures++
} else {
    Write-Host "[OK] Orbis:ApiKey is set." -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($deviceCode)) {
    Write-Host "[FAIL] Orbis:DeviceCode is empty." -ForegroundColor Red
    $failures++
} else {
    Write-Host "[OK] Orbis:DeviceCode = $deviceCode" -ForegroundColor Green
}

Write-Host ""
Write-Host "Turnstile reachability $turnstileIp (LiteNet2 TCP port 7878) ..."
$turnstilePort = 7878
if ($config.Toletus.Port -and [int]$config.Toletus.Port -gt 0) {
    $turnstilePort = [int]$config.Toletus.Port
}

$tcpOk = $false
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $connect = $tcp.ConnectAsync($turnstileIp, $turnstilePort)
    if ($connect.Wait(3000) -and $tcp.Connected) {
        $tcpOk = $true
    }
    $tcp.Close()
} catch {
    $tcpOk = $false
}

if ($tcpOk) {
    Write-Host "[OK] Turnstile accepts TCP on port $turnstilePort." -ForegroundColor Green
} else {
    Write-Host "[FAIL] Cannot connect to turnstile on ${turnstileIp}:${turnstilePort}." -ForegroundColor Red
    Write-Host "       PC must be on the same LAN as the turnstile (e.g. 192.168.0.x)." -ForegroundColor Yellow
    $failures++
}

$ping = Test-Connection -ComputerName $turnstileIp -Count 1 -Quiet -ErrorAction SilentlyContinue
if ($ping) {
    Write-Host "[OK] Turnstile responds to ICMP ping." -ForegroundColor Green
} elseif ($tcpOk) {
    Write-Host "[INFO] ICMP ping blocked or disabled; TCP works (OK for LiteNet2)." -ForegroundColor Yellow
} else {
    Write-Host "[INFO] ICMP ping also failed." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Orbisfit API probe..."
$endpoint = if ($useV2) { "/api/turnstile/access-attempt" } else { "/api/verificar-acesso-catraca" }
$uri = ($apiBaseUrl.TrimEnd("/")) + $endpoint

$body = if ($useV2) {
    @{
        transaction_id = "preflight-$(Get-Date -Format 'yyyyMMddHHmmss')"
        direction = "IN"
        device = @{ device_code = $deviceCode; modelo = "toletus"; ip = $turnstileIp }
        credential = @{ type = "ENROLLID"; value = "0" }
        event = @{ kind = "ACCESS_ATTEMPT"; timestamp = (Get-Date).ToUniversalTime().ToString("o") }
    } | ConvertTo-Json -Depth 5
} else {
    @{
        enrollid = "0"
        device_id = $deviceCode
        credential_type = "ENROLLID"
        credential_value = "0"
    } | ConvertTo-Json
}

try {
    $response = Invoke-WebRequest -Uri $uri -Method POST `
        -Headers @{ "x-api-key" = $apiKey; "Content-Type" = "application/json" } `
        -Body $body -TimeoutSec 15 -UseBasicParsing

    Write-Host "[OK] Orbisfit HTTP $($response.StatusCode) on $endpoint" -ForegroundColor Green
} catch {
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode.value__ -eq 401) {
        Write-Host "[FAIL] Orbisfit returned 401 — invalid API key." -ForegroundColor Red
        $failures++
    } elseif ($_.Exception.Response) {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "[OK] Orbisfit reachable (HTTP $code). Auth/config likely valid for connectivity." -ForegroundColor Yellow
    } else {
        Write-Host "[FAIL] Orbisfit unreachable: $($_.Exception.Message)" -ForegroundColor Red
        $failures++
    }
}

Write-Host ""
$service = Get-Service -Name "OrbisToletusAgent" -ErrorAction SilentlyContinue
if ($service) {
    $color = if ($service.Status -eq "Running") { "Green" } else { "Yellow" }
    Write-Host "[$($service.Status)] Windows service OrbisToletusAgent" -ForegroundColor $color
} else {
    Write-Host "[INFO] Service OrbisToletusAgent is not installed." -ForegroundColor Yellow
    Write-Host "       Dev:  dotnet run --project src/Orbis.ToletusAgent" -ForegroundColor Yellow
    Write-Host "       Prod: run install-service.ps1 as Administrator, then Start-Service OrbisToletusAgent" -ForegroundColor Yellow
}

$healthPath = Join-Path $env:ProgramData "Orbis\ToletusAgent\health.json"
if (Test-Path $healthPath) {
    Write-Host ""
    Write-Host "health.json:"
    Get-Content $healthPath
} else {
    Write-Host ""
    Write-Host "[INFO] health.json not found yet. Start the agent and wait for heartbeat." -ForegroundColor Yellow
}

Write-Host ""
if ($failures -gt 0) {
    Write-Host "Preflight finished with $failures failure(s)." -ForegroundColor Red
    exit 1
}

Write-Host "Preflight passed. Ready for pilot scenarios (see docs/piloto-academia-runbook.md)." -ForegroundColor Green
exit 0
