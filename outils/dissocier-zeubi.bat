@echo off
rem Supprime l'association .zeubi creee par associer-zeubi.bat.
reg delete "HKCU\Software\Classes\.zeubi" /f >nul 2>nul
reg delete "HKCU\Software\Classes\Mentalo.Carte" /f >nul 2>nul
ie4uinit.exe -show 2>nul
echo Association .zeubi supprimee.
