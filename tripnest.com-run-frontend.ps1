# =====================================================================
#  Frontend supervisor (PowerShell - batch proved too fragile here).
#  Runs the Vite dev server, appends ALL output to logs\frontend.log, and
#  restarts automatically when it exits. Close this window to stop.
#
#  Vite is invoked through node directly rather than "npm run dev": npm is
#  npm.cmd (a batch file), and killing its child can leave an interactive
#  "Terminate batch job (Y/N)?" prompt behind.
# =====================================================================
$ErrorActionPreference = 'Continue'
$Host.UI.RawUI.WindowTitle = 'TripNest Frontend (5173)'

$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$app    = Join-Path $root 'Frontend\Tripnest\Frontend'
$logDir = Join-Path $root 'logs'
$log    = Join-Path $logDir 'frontend.log'
$vite   = Join-Path $app 'node_modules\vite\bin\vite.js'

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$fails = 0

while ($true) {
    Write-Host "[$(Get-Date -f HH:mm:ss)] Starting frontend on :5173  (output -> logs\frontend.log)" -ForegroundColor Cyan
    "`n===================================================================" | Out-File -Append -Encoding utf8 $log
    "[$(Get-Date -f 'yyyy-MM-dd HH:mm:ss')] frontend starting"             | Out-File -Append -Encoding utf8 $log
    "==================================================================="   | Out-File -Append -Encoding utf8 $log

    Push-Location $app
    # Pipe through Out-File (not *>>): PS 5.1 redirection writes UTF-16, which
    # would make the log unreadable/mixed-encoding alongside the header lines.
    & node $vite *>&1 | Out-File -Append -Encoding utf8 $log
    $code = $LASTEXITCODE
    Pop-Location

    "[$(Get-Date -f 'yyyy-MM-dd HH:mm:ss')] frontend EXITED with code $code" | Out-File -Append -Encoding utf8 $log
    Write-Host "[!] Frontend exited (code $code). Last lines of the log:" -ForegroundColor Red
    Get-Content $log -Tail 8 -ErrorAction SilentlyContinue | ForEach-Object { "       $_" }

    $fails++
    if ($fails -ge 5) {
        Write-Host "[!] 5 failures in a row - pausing 30s. Read logs\frontend.log." -ForegroundColor Red
        Start-Sleep -Seconds 30
        $fails = 0
    } else {
        Write-Host "    Restarting in 5s...  (close this window to stop)"
        Start-Sleep -Seconds 5
    }
}
