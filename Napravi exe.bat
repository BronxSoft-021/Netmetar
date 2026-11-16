@echo off
rem Pravi dist\BronxSoft Netmetar.exe i dist\BronxSoft Netmetar Merac.exe
rem (pre toga zatvori Netmetar: ikonica -> Izlaz)
cd /d "%~dp0"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet publish -c Release -nologo -v q -o dist
if errorlevel 1 (
  echo.
  echo Pakovanje glavnog programa nije uspelo.
  pause
  exit /b 1
)
dotnet publish Merac -c Release -nologo -v q -o dist
if errorlevel 1 (
  echo.
  echo Pakovanje meraca nije uspelo.
  pause
  exit /b 1
)
echo.
echo Gotovo: dist\BronxSoft Netmetar.exe i dist\BronxSoft Netmetar Merac.exe
