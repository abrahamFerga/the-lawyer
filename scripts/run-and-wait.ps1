#!/usr/bin/env pwsh
#requires -Version 7.0
<#
.SYNOPSIS
    Boot TheLawyer to a known-good running state and point the .http catalog at it.

.DESCRIPTION
    One command to reach a running stack: starts the Aspire AppHost, waits for the
    dashboard, discovers the (dynamic) API and web URLs, writes http/.env so the
    http/*.http catalog targets the live API, and prints the URLs.

    Uses `dotnet run` (NOT `aspire run`): the installed Aspire CLI (13.x) rejects the
    repo's pinned 9.0.0 Aspire packages. See memory: aspire-version-mismatch.

.PARAMETER DashboardPort
    Stable dashboard port from the AppHost http launch profile (default 15090).

.PARAMETER TimeoutSeconds
    Max time to wait for the dashboard and the API to come up.

.EXAMPLE
    pwsh scripts/run-and-wait.ps1
#>
[CmdletBinding()]
param(
    [int]$DashboardPort = 15090,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$appHostProj = Join-Path $repoRoot 'src/TheLawyer.AppHost/TheLawyer.AppHost.csproj'
$envFile = Join-Path $repoRoot 'http/.env'

function Wait-Until {
    param([scriptblock]$Condition, [int]$Seconds, [string]$What)
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $result = & $Condition
        if ($result) { return $result }
        Start-Sleep -Seconds 2
    }
    throw "Timed out after ${Seconds}s waiting for: $What"
}

function Get-ListenPort {
    param([string]$ProcessName)
    $proc = Get-CimInstance Win32_Process -Filter "Name='$ProcessName'" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $proc) { return $null }
    $conn = Get-NetTCPConnection -State Listen -OwningProcess $proc.ProcessId -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -in @('127.0.0.1', '::1', '0.0.0.0', '::') } |
        Sort-Object LocalPort | Select-Object -First 1
    if ($conn) { return [int]$conn.LocalPort }
    return $null
}

Write-Host "==> Starting TheLawyer AppHost (dotnet run --launch-profile http)..." -ForegroundColor Cyan
$null = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $appHostProj, '--launch-profile', 'http') `
    -WorkingDirectory $repoRoot -PassThru -WindowStyle Hidden

Write-Host "==> Waiting for dashboard on :$DashboardPort..." -ForegroundColor Cyan
Wait-Until -Seconds $TimeoutSeconds -What "dashboard on port $DashboardPort" -Condition {
    Get-NetTCPConnection -State Listen -LocalPort $DashboardPort -ErrorAction SilentlyContinue
} | Out-Null

Write-Host "==> Discovering API endpoint..." -ForegroundColor Cyan
$apiPort = Wait-Until -Seconds $TimeoutSeconds -What "TheLawyer.Api to listen" -Condition {
    Get-ListenPort -ProcessName 'TheLawyer.Api.exe'
}
$apiBase = "http://localhost:$apiPort"

# Confirm the API is actually serving before declaring ready.
Write-Host "==> Waiting for API /health at $apiBase ..." -ForegroundColor Cyan
Wait-Until -Seconds $TimeoutSeconds -What "API /health to return 200" -Condition {
    try {
        (Invoke-WebRequest -Uri "$apiBase/health" -TimeoutSec 5 -SkipHttpErrorCheck).StatusCode -eq 200
    } catch { $false }
} | Out-Null

# Web (Vite) is best-effort — it may still be warming up; don't fail the run on it.
$webPort = Get-ListenPort -ProcessName 'node.exe'
$webBase = if ($webPort) { "http://localhost:$webPort" } else { '(starting — see dashboard)' }

# Write http/.env so the .http catalog targets the live API (preserve other keys).
$lines = @()
if (Test-Path $envFile) {
    $lines = Get-Content $envFile | Where-Object { $_ -notmatch '^\s*API_BASE\s*=' }
}
$lines = @("API_BASE=$apiBase") + $lines
Set-Content -Path $envFile -Value $lines -Encoding utf8

Write-Host ""
Write-Host "TheLawyer is running." -ForegroundColor Green
Write-Host ("  Dashboard : http://localhost:{0}" -f $DashboardPort)
Write-Host ("  API       : {0}" -f $apiBase)
Write-Host ("  Web (SPA) : {0}" -f $webBase)
Write-Host ("  Wrote     : {0} (API_BASE)" -f $envFile)
Write-Host ""
Write-Host "Exercise it:  open http/foundations.http and send requests." -ForegroundColor Yellow
Write-Host "Stop it:      Get-Process -Name TheLawyer.AppHost | Stop-Process -Force" -ForegroundColor Yellow
