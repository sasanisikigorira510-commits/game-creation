@echo off
setlocal
PowerShell -ExecutionPolicy Bypass -File "%~dp0unity_smoke_check.ps1" %*
exit /b %errorlevel%
