@echo off
setlocal
PowerShell -ExecutionPolicy Bypass -File "%~dp0unity_report_summary.ps1" %*
exit /b %errorlevel%
