@echo off
rem Compilation de Mental-o. Ne demande que le .NET Framework 4.8 (inclus dans Windows 10/11).
setlocal
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319

"%FW%\csc.exe" /nologo /target:winexe /out:Mental-o.exe /optimize+ /codepage:65001 ^
  /win32icon:ressources\mentalo.ico ^
  /lib:"%FW%\WPF" ^
  /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll ^
  /r:System.IO.Compression.dll /r:System.Web.Extensions.dll /r:System.Xml.dll ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /recurse:src\*.cs

if errorlevel 1 (
  echo.
  echo *** Echec de la compilation ***
  exit /b 1
)
echo Compilation reussie : Mental-o.exe
