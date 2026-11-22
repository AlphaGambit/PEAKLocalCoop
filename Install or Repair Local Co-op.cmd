@echo off
setlocal
set "PeakArgs="
if not "%~1"=="" set PeakArgs=-PeakDir "%~1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage-LocalCoop.ps1" -Setup %PeakArgs%
set "PeakExit=%errorlevel%"
pause
exit /b %PeakExit%
