@echo off
rem Associe l'extension .zeubi a Mental-o pour l'utilisateur courant (aucun droit admin requis).
rem Reversible avec dissocier-zeubi.bat.
setlocal
for %%I in ("%~dp0..") do set RACINE=%%~fI

reg add "HKCU\Software\Classes\.zeubi" /ve /d "Mentalo.Carte" /f >nul
reg add "HKCU\Software\Classes\Mentalo.Carte" /ve /d "Carte Mental-o" /f >nul
reg add "HKCU\Software\Classes\Mentalo.Carte\DefaultIcon" /ve /d "%RACINE%\ressources\fichier-zeubi.ico" /f >nul
reg add "HKCU\Software\Classes\Mentalo.Carte\shell\open\command" /ve /d "\"%RACINE%\Mental-o.exe\" \"%%1\"" /f >nul

rem Rafraichit le cache d'icones de l'Explorateur.
ie4uinit.exe -show 2>nul

echo Association .zeubi enregistree (double-clic sur un .zeubi ouvrira Mental-o).
