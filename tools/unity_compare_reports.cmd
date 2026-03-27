@echo off
setlocal
PowerShell -ExecutionPolicy Bypass -File "%~dp0unity_compare_reports.ps1" %*
exit /b %errorlevel%
