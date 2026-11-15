@echo off
rem Pravi dist\BronxSoft Netmetar.exe (pre toga zatvori Netmetar: ikonica -> Izlaz)
cd /d "%~dp0"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet publish -c Release -nologo -v q -o dist
if errorlevel 1 (
  echo.
  echo Pakovanje nije uspelo.
  pause
  exit /b 1
)
echo.
echo Gotovo: dist\BronxSoft Netmetar.exe
