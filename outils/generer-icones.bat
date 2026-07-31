@echo off
rem Genere les icones dans ressources\ (a lancer d'ou l'on veut, il se replace tout seul).
setlocal
for %%I in ("%~dp0..") do set RACINE=%%~fI
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319

"%FW%\csc.exe" /nologo /target:exe /out:"%TEMP%\GenerateurIcones.exe" /codepage:65001 ^
  /r:System.Drawing.dll "%~dp0GenerateurIcones.cs"
if errorlevel 1 (
  echo *** Echec de la compilation du generateur ***
  exit /b 1
)

cd /d "%RACINE%"
"%TEMP%\GenerateurIcones.exe"
