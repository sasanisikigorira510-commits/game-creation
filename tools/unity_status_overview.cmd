@echo off
setlocal
PowerShell -ExecutionPolicy Bypass -File "%~dp0unity_status_overview.ps1" %*
exit /b %errorlevel%
