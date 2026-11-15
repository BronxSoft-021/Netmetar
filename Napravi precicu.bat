@echo off
rem Precica "BronxSoft Netmetar" na desktopu -> dist\BronxSoft Netmetar.exe
cd /d "%~dp0"
if not exist "dist\BronxSoft Netmetar.exe" (
  echo Prvo pokreni "Napravi exe.bat".
  pause
  exit /b 1
)
powershell -NoProfile -Command "$d=[Environment]::GetFolderPath('Desktop'); $e=(Resolve-Path 'dist\BronxSoft Netmetar.exe').Path; $o=Join-Path $d 'UnderGRAND Netmetar.lnk'; if (Test-Path $o) { Remove-Item $o }; $s=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $d 'BronxSoft Netmetar.lnk')); $s.TargetPath=$e; $s.WorkingDirectory=(Split-Path $e); $s.IconLocation=$e + ',0'; $s.Description='BronxSoft Netmetar'; $s.Save(); 'Precica napravljena: ' + (Join-Path $d 'BronxSoft Netmetar.lnk')"
