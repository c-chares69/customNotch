@echo off
rem Installe customNotch pour l'utilisateur courant (double-cliquez ce fichier).
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Source "%~dp0customNotch"
if errorlevel 1 (
  echo.
  echo L'installation a echoue. Consultez le message ci-dessus.
  pause
)
