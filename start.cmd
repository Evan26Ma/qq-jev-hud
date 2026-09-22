@echo off
setlocal
cd /d "%~dp0"
if exist "src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe" (
  start "QQ Jev HUD" "src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe"
  exit /b 0
)
if not exist ".venv\Scripts\python.exe" (
  echo First-time setup is required.
  where pwsh >nul 2>nul && (pwsh -ExecutionPolicy Bypass -File "%~dp0setup.ps1") || (powershell -ExecutionPolicy Bypass -File "%~dp0setup.ps1")
  if errorlevel 1 pause & exit /b 1
)
dotnet run --project "%~dp0src\QQJevHud\QQJevHud.csproj"
if errorlevel 1 pause
