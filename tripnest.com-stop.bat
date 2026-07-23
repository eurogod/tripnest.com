@echo off
setlocal enableextensions enabledelayedexpansion
title TripNest stop

REM =====================================================================
REM  TripNest shutdown
REM  Stops the backend API (:5091) and frontend dev server (:5173).
REM
REM  ORDER MATTERS: the servers run under supervisor windows that restart
REM  them on exit, so the supervisors must be killed FIRST - otherwise
REM  freeing the port just makes the supervisor start a new server.
REM
REM  Leaves the PostgreSQL service running (shared infrastructure).
REM  Pass "db" to stop PostgreSQL too:  tripnest.com-stop.bat db
REM =====================================================================

echo(
echo ==================== TripNest shutdown ====================
echo(

REM ---- 1. Kill the supervisor windows (and their child processes) -----
echo [stop] Closing supervisor windows...
REM  The Postgres watchdog goes first: it restarts the service on sight, so
REM  stopping the DB below would be undone while it is still running.
taskkill /FI "WINDOWTITLE eq TripNest Postgres*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq TripNest Backend*"  /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq TripNest Frontend*" /T /F >nul 2>&1

REM Give the trees a moment to die before checking the ports.
REM (ping, not timeout: timeout fails when this script's stdin is redirected.)
ping -n 3 127.0.0.1 >nul 2>&1

REM ---- 2. Free anything still listening on the dev ports --------------
for %%P in (5091 5173) do (
    set "found="
    for /f "tokens=5" %%I in ('netstat -ano ^| findstr "LISTENING" ^| findstr ":%%P "') do (
        set "found=1"
        echo [stop] port %%P still held by PID %%I - killing
        taskkill /PID %%I /T /F >nul 2>&1
    )
    if not defined found echo [stop] port %%P clear
)

REM ---- 3. Optional: stop PostgreSQL when called with "db" -------------
if /i "%~1"=="db" (
    echo [db]   Stopping PostgreSQL service ^(UAC prompt^)...
    powershell -NoProfile -Command "Start-Process net -ArgumentList 'stop','postgresql-x64-18' -Verb RunAs -Wait"
) else (
    echo [db]   PostgreSQL left running ^(run "tripnest.com-stop.bat db" to stop it too^).
)

echo(
echo  Servers stopped. Logs kept in logs\backend.log / logs\frontend.log
echo ==========================================================
echo(
pause
