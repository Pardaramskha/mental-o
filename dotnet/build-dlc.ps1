# build-dlc.ps1 — fabrique le paquet mental-o.mdlc (Marabook DownLoadable
# Content, 26/10/2026) : un zip qui porte module.json à sa racine et, sous
# bin/, la DLL du module (MentalO.Marabook.dll) avec le cœur et la toile
# (MentalO.Core.dll, MentalO.Canvas.dll) — Avalonia et Skia viennent de
# Marabook. Le nom de l'asset est STABLE : la release GitHub de Mental-O
# publie mental-o.mdlc, Marabook le cherche sous ce nom.
#
#   powershell -ExecutionPolicy Bypass -File dotnet\build-dlc.ps1
#       -Out <dossier>   (défaut : le dossier Téléchargements)
#       -Install         (copie aussi le paquet déballé dans %APPDATA%\Marabook\dlc\mental-o,
#                         Marabook FERMÉ : la DLL est verrouillée tant qu'il tourne)
#       -SkipBuild
#
# Le module est compilé CONTRE Marabook.dll (MARABOOK_DLL, ou le dépôt frère
# ..\marabook bâti en Release) : rebâtir Marabook avant si son contrat a changé.
param(
    [string]$Out = "",
    [switch]$Install,
    [switch]$SkipBuild,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projet = Join-Path $root 'MentalO.Marabook'
if ($Out -eq "") { $Out = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads' }

if (-not $SkipBuild) {
    & dotnet build (Join-Path $projet 'MentalO.Marabook.csproj') -c $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "La compilation du module a échoué." }
}
$bin = Join-Path $projet "bin\$Configuration\net8.0"
$dlls = @('MentalO.Marabook.dll', 'MentalO.Canvas.dll', 'MentalO.Core.dll')
foreach ($dll in $dlls) {
    if (-not (Test-Path (Join-Path $bin $dll))) { throw "$dll introuvable dans $bin" }
}
$manifest = Join-Path $projet 'module.json'
$json = Get-Content -Raw -Encoding UTF8 $manifest | ConvertFrom-Json
# La version du manifeste suit VERSION (la release de Mental-O).
$version = ([IO.File]::ReadAllText((Join-Path $root '..\VERSION'))).Trim()
if ($json.version -ne $version) {
    $texte = [IO.File]::ReadAllText($manifest, [Text.Encoding]::UTF8)
    $texte = $texte -replace '"version":\s*"[^"]*"', ('"version": "' + $version + '"')
    [IO.File]::WriteAllText($manifest, $texte, (New-Object Text.UTF8Encoding($false)))
    Write-Host "module.json : version $version"
}

$mdlc = Join-Path $Out 'mental-o.mdlc'
if (Test-Path $mdlc) { Remove-Item $mdlc -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($mdlc, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $null = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $manifest, 'module.json')
    foreach ($dll in $dlls) {
        $null = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $bin $dll), "bin/$dll")
    }
    $extras = Join-Path $projet 'achievements'
    if (Test-Path $extras) {
        Get-ChildItem -Path $extras -File -Recurse | ForEach-Object {
            $relative = $_.FullName.Substring($projet.Length).TrimStart('\', '/').Replace('\', '/')
            $null = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative)
        }
    }
} finally {
    $zip.Dispose()
}
Write-Host "Paquet : $mdlc"

if ($Install) {
    $cible = Join-Path $env:APPDATA 'Marabook\dlc\mental-o'
    if (Get-Process -Name Marabook -ErrorAction SilentlyContinue) {
        Write-Warning "Marabook tourne : la DLL en place est verrouillée. Fermez-le puis relancez avec -Install."
    } else {
        if (Test-Path $cible) { Remove-Item $cible -Recurse -Force }
        New-Item -ItemType Directory -Force $cible | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory($mdlc, $cible)
        Write-Host "Installé dans $cible"
    }
}
