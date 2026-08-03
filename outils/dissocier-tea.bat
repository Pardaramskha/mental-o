@echo off
rem Supprime les associations .tea et .zeubi creees par associer-tea.bat.
reg delete "HKCU\Software\Classes\.tea" /f >nul 2>nul
reg delete "HKCU\Software\Classes\.zeubi" /f >nul 2>nul
reg delete "HKCU\Software\Classes\Mentalo.Carte" /f >nul 2>nul
ie4uinit.exe -show 2>nul
echo Associations .tea et .zeubi supprimees.
