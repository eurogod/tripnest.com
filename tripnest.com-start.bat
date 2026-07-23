@echo off
setlocal enableextensions
title TripNest launcher

REM =====================================================================
REM  TripNest full-stack launcher
REM
REM  Brings up all three servers, each in its OWN terminal window:
REM     1. "TripNest Postgres"  - watchdog for the PostgreSQL 18 service (:5432)
REM     2. "TripNest Backend"   - the ASP.NET Core API                  (:5091)
REM     3. "TripNest Frontend"  - the Vite dev server                   (:5173)
REM
REM  Each window supervises its server and restarts it if it dies. All
REM  output is logged under .\logs\ so failures can be diagnosed later.
REM
REM  Lives in the Tripnest.com folder; uses %~dp0 so it is path-independent.
REM =====================================================================

REM ---- 0. Self-elevate: ONE UAC prompt for the whole stack ------------
REM  Admin is needed to start/configure the PostgreSQL service. Elevating
REM  here means the watchdog windows inherit the right, so they can revive
REM  Postgres on their own instead of the stack sitting dead until someone
REM  clicks a prompt.
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator rights ^(needed to manage the PostgreSQL service^)...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "ROOT=%~dp0"
REM %~dp0 ends with a backslash; a trailing "\" before a closing quote escapes
REM that quote and corrupts argument parsing, so keep an unslashed copy for /d.
set "ROOTD=%ROOT:~0,-1%"
set "PGBIN=C:\Program Files\PostgreSQL\18\bin"
set "PGSVC=postgresql-x64-18"
set "PS=powershell -NoProfile -ExecutionPolicy Bypass -NoExit -File"
if not exist "%ROOT%logs" mkdir "%ROOT%logs"

echo(
echo ==================== TripNest startup ====================
echo(

REM ---- 1. PostgreSQL: crash recovery + start --------------------------
REM  Already elevated, so sc/net run directly - no second UAC prompt.
REM  reset= 60 (not 86400): the failure counter clears after 60s of healthy
REM  running, so Windows keeps restarting Postgres instead of giving up for
REM  the rest of the day after 3 crashes.
echo [db]  Configuring PostgreSQL crash auto-restart...
sc failure "%PGSVC%" reset= 60 actions= restart/5000/restart/10000/restart/30000 >nul
sc failureflag "%PGSVC%" 1 >nul

sc query "%PGSVC%" | find "RUNNING" >nul
if errorlevel 1 (
    echo [db]  PostgreSQL is stopped - starting the service...
    net start "%PGSVC%" >nul 2>&1
) else (
    echo [db]  PostgreSQL service already running.
)

REM ---- 2. Wait until the DB actually accepts connections --------------
echo [db]  Waiting for PostgreSQL to accept connections on :5432 ...
set /a _pg=0
:waitpg
"%PGBIN%\pg_isready.exe" -q -h localhost -p 5432 >nul 2>&1
if not errorlevel 1 goto pgok
set /a _pg+=1
if %_pg% geq 30 (
    echo [db]  [!] PostgreSQL still not ready - the watchdog window will keep trying.
    goto pgdone
)
REM ping, not timeout: timeout aborts when this script's stdin is redirected.
ping -n 3 127.0.0.1 >nul 2>&1
goto waitpg
:pgok
echo [db]  PostgreSQL is ready.
:pgdone
echo(

REM ---- 3. Postgres watchdog terminal ----------------------------------
echo [db]  Launching Postgres watchdog window ->  localhost:5432
start "TripNest Postgres" /d "%ROOTD%" %PS% "%ROOTD%\tripnest.com-run-postgres.ps1"
echo(

REM ---- 4. Backend terminal --------------------------------------------
REM  Supervisors are PowerShell: batch could not reliably detect the child
REM  exiting and loop. Full paths because this machine has the current
REM  directory removed from the executable search path.
echo [api] Launching backend window   ->  http://localhost:5091
start "TripNest Backend" /d "%ROOTD%" %PS% "%ROOTD%\tripnest.com-run-backend.ps1"

REM ---- 5. Wait for the API before starting the frontend ---------------
REM  (otherwise the Vite proxy throws ECONNREFUSED on the first /api call)
echo [api] Waiting for backend to answer on :5091 (up to ~90s) ...
powershell -NoProfile -Command "for($i=0;$i -lt 90;$i++){try{$c=New-Object Net.Sockets.TcpClient;$c.Connect('localhost',5091);$c.Close();exit 0}catch{Start-Sleep 1}};exit 1"
if errorlevel 1 (
    echo [api] [!] Backend did not answer within 90s.
    echo       Check the "TripNest Backend" window or logs\backend.log.
    echo       Starting the frontend anyway.
) else (
    echo [api] Backend is up.
)
echo(

REM ---- 6. Frontend terminal -------------------------------------------
echo [web] Launching frontend window  ->  http://localhost:5173
start "TripNest Frontend" /d "%ROOTD%" %PS% "%ROOTD%\tripnest.com-run-frontend.ps1"

echo(
echo ==========================================================
echo  Three server windows are now open:
echo(
echo    TripNest Postgres : localhost:5432
echo    TripNest Backend  : http://localhost:5091  (Swagger at /swagger)
echo    TripNest Frontend : http://localhost:5173  ^<-- open this one
echo(
echo  Logs : %ROOT%logs\postgres.log
echo         %ROOT%logs\backend.log
echo         %ROOT%logs\frontend.log
echo(
echo  Each server restarts automatically if it crashes. Close a window
echo  to stop that server, or run tripnest.com-stop.bat to stop all.
echo ==========================================================
echo(
pause
