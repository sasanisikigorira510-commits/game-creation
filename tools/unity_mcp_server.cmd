@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PYTHON_EXE=%LocalAppData%\Programs\Python\Python311\python.exe"
set "PYTHON_EXE_FALLBACK=%USERPROFILE%\AppData\Local\Programs\Python\Python311\python.exe"
set "PYTHON_EXE_HARDCODED=C:\Users\sasan\AppData\Local\Programs\Python\Python311\python.exe"

if exist "%PYTHON_EXE%" (
  "%PYTHON_EXE%" "%SCRIPT_DIR%unity_mcp_server.py"
  exit /b %errorlevel%
)

if exist "%PYTHON_EXE_FALLBACK%" (
  "%PYTHON_EXE_FALLBACK%" "%SCRIPT_DIR%unity_mcp_server.py"
  exit /b %errorlevel%
)

if exist "%PYTHON_EXE_HARDCODED%" (
  "%PYTHON_EXE_HARDCODED%" "%SCRIPT_DIR%unity_mcp_server.py"
  exit /b %errorlevel%
)

where py >nul 2>nul
if %errorlevel%==0 (
  py -3 "%SCRIPT_DIR%unity_mcp_server.py"
  exit /b %errorlevel%
)

where python >nul 2>nul
if %errorlevel%==0 (
  python "%SCRIPT_DIR%unity_mcp_server.py"
  exit /b %errorlevel%
)

echo Python 3 was not found. Install Python or edit tools\unity_mcp_server.cmd to point at your interpreter. 1>&2
exit /b 1
