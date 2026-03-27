@echo off
setlocal
PowerShell -ExecutionPolicy Bypass -File "%~dp0unity_reports_status.ps1" %*
exit /b %errorlevel%
