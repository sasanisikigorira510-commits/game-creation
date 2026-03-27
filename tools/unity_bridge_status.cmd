@echo off
setlocal
PowerShell -ExecutionPolicy Bypass -File "%~dp0unity_bridge_status.ps1" %*
exit /b %errorlevel%
