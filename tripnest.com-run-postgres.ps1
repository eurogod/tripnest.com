# =====================================================================
#  PostgreSQL watchdog.
#  The service has no console of its own, so this window is its terminal:
#  it polls pg_isready and restarts the service whenever the DB goes away.
#  postgres.exe v18 has terminated unexpectedly on this machine repeatedly,
#  which takes the API down with it - this brings it straight back.
#
#  Requires admin (the launcher self-elevates, so this inherits it).
#  Close this window to stop watching (the service keeps running).
# =====================================================================
$ErrorActionPreference = 'Continue'
$Host.UI.RawUI.WindowTitle = 'TripNest Postgres (5432)'

$root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir  = Join-Path $root 'logs'
$log     = Join-Path $logDir 'postgres.log'
$pgReady = 'C:\Program Files\PostgreSQL\18\bin\pg_isready.exe'
$svc     = 'postgresql-x64-18'

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-Log($msg) {
    "[$(Get-Date -f 'yyyy-MM-dd HH:mm:ss')] $msg" | Out-File -Append -Encoding utf8 $log
}

Write-Log "watchdog started"
Write-Host "Watching PostgreSQL on :5432  (events -> logs\postgres.log)" -ForegroundColor Cyan

$wasUp = $true
$revives = 0

while ($true) {
    & $pgReady -q -h localhost -p 5432 2>$null | Out-Null
    $up = ($LASTEXITCODE -eq 0)

    if ($up) {
        if (-not $wasUp) {
            $revives++
            Write-Host "[$(Get-Date -f HH:mm:ss)] PostgreSQL is back up (revive #$revives)" -ForegroundColor Green
            Write-Log "postgres is accepting connections again (revive #$revives)"
        }
        $wasUp = $true
    }
    else {
        if ($wasUp) {
            Write-Host "[$(Get-Date -f HH:mm:ss)] PostgreSQL is DOWN - restarting the service..." -ForegroundColor Red
            Write-Log "postgres not answering on :5432 - attempting service start"
        }
        $wasUp = $false
        try {
            Start-Service $svc -ErrorAction Stop
            Write-Log "Start-Service succeeded"
        }
        catch {
            Write-Log "Start-Service failed: $($_.Exception.Message)"
            Write-Host "    start failed: $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    }

    Start-Sleep -Seconds 5
}
