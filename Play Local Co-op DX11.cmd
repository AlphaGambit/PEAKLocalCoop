@echo off
setlocal
set "PeakArgs="
if not "%~1"=="" set PeakArgs=-PeakDir "%~1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage-LocalCoop.ps1" -LaunchDx11 %PeakArgs%
set "PeakExit=%errorlevel%"
if not "%PeakExit%"=="0" pause
exit /b %PeakExit%
