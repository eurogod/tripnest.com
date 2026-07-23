# =====================================================================
#  Backend supervisor (PowerShell - batch proved too fragile here).
#  Waits for Postgres, runs the API, appends ALL output to logs\backend.log,
#  and restarts automatically when it exits. Close this window to stop.
# =====================================================================
$ErrorActionPreference = 'Continue'
$Host.UI.RawUI.WindowTitle = 'TripNest Backend (5091)'

$root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj    = Join-Path $root 'TripNest.Core'
$logDir  = Join-Path $root 'logs'
$log     = Join-Path $logDir 'backend.log'
$pgReady = 'C:\Program Files\PostgreSQL\18\bin\pg_isready.exe'

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$fails = 0

while ($true) {
    # --- Wait for Postgres: the API runs Migrate() at startup and hard-crashes
    #     if the DB is down, which would otherwise be a fast crash-loop.
    $waited = 0
    while (Test-Path $pgReady) {
        & $pgReady -q -h localhost -p 5432 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        if ($waited -eq 0) {
            Write-Host "[$(Get-Date -f HH:mm:ss)] Postgres is down - waiting..." -ForegroundColor Yellow
            "[$(Get-Date -f 'yyyy-MM-dd HH:mm:ss')] waiting for Postgres :5432 ..." | Out-File -Append -Encoding utf8 $log
        }
        $waited++
        Start-Sleep -Seconds 3
    }

    Write-Host "[$(Get-Date -f HH:mm:ss)] Starting backend on :5091  (output -> logs\backend.log)" -ForegroundColor Cyan
    "`n===================================================================" | Out-File -Append -Encoding utf8 $log
    "[$(Get-Date -f 'yyyy-MM-dd HH:mm:ss')] backend starting"              | Out-File -Append -Encoding utf8 $log
    "==================================================================="   | Out-File -Append -Encoding utf8 $log

    Push-Location $proj
    # Pipe through Out-File (not *>>): PS 5.1 redirection writes UTF-16, which
    # would make the log unreadable/mixed-encoding alongside the header lines.
    & dotnet run --launch-profile http *>&1 | Out-File -Append -Encoding utf8 $log
    $code = $LASTEXITCODE
    Pop-Location

    "[$(Get-Date -f 'yyyy-MM-dd HH:mm:ss')] backend EXITED with code $code" | Out-File -Append -Encoding utf8 $log
    Write-Host "[!] Backend exited (code $code). Last lines of the log:" -ForegroundColor Red
    Get-Content $log -Tail 8 -ErrorAction SilentlyContinue | ForEach-Object { "       $_" }

    $fails++
    if ($fails -ge 5) {
        Write-Host "[!] 5 failures in a row - pausing 30s. Read logs\backend.log." -ForegroundColor Red
        Start-Sleep -Seconds 30
        $fails = 0
    } else {
        Write-Host "    Restarting in 5s...  (close this window to stop)"
        Start-Sleep -Seconds 5
    }
}
