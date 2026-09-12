# release.ps1 — fabrique ce qui part en release GitHub, dans dist\ :
#
#   mental-o-windows-portable.zip   l'archive « portable » Windows
#        (Stargazer la télécharge et la déballe dans apps\ ; à la main :
#        déballer n'importe où et lancer Mental-o.exe ; c'est aussi elle
#        que l'application télécharge via Aide > Vérifier les mises à jour)
#   Mental-o-Setup-Windows.exe      l'installeur autonome Windows
#        (auto-extracteur qui embarque la même archive, crée les
#        raccourcis, associe les .tea et s'inscrit dans « Applications
#        installées » — voir setup-stub.cs)
#
#   powershell -ExecutionPolicy Bypass -File outils\release.ps1
#
# Aucun SDK requis : tout se compile avec le csc.exe livré avec Windows.
#
# Les noms ne portent PAS la version (elle est dans le tag de la release
# et dans VERSION) : ainsi les boutons du README pointent sur
# releases/latest/download/<nom> et téléchargent toujours la dernière.
# Le hub, lui, choisit l'archive .zip de la release dont le nom contient
# l'id de l'app et « windows » (voir Depot.cs).

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$version = ([IO.File]::ReadAllText('VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION doit être de la forme majeure.mineure.correctif (lu : « $version »)."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force 'dist' | Out-Null
$dist = (Resolve-Path 'dist').Path

# Zip entrée par entrée, noms en « / » (CreateFromDirectory sous
# PowerShell 5 écrit des « \ », illisibles hors Windows).
function Zipper($dossier, $zip) {
    if (Test-Path $zip) { Remove-Item -Force $zip }
    $racine = (Resolve-Path $dossier).Path.TrimEnd('\') + '\'
    $archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
    try {
        foreach ($fichier in Get-ChildItem $dossier -Recurse -File) {
            $nom = $fichier.FullName.Substring($racine.Length).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $fichier.FullName, $nom, 'Optimal') | Out-Null
        }
    } finally { $archive.Dispose() }
}

function Copier($source, $stage) {
    $cible = Join-Path $stage $source
    New-Item -ItemType Directory -Force (Split-Path -Parent $cible) | Out-Null
    Copy-Item $source $cible
}

# 1. compiler l'application (l'exe doit être fermé : csc ne peut pas
#    l'écraser tant qu'une instance tourne)
if (Get-Process -Name 'Mental-o' -ErrorAction SilentlyContinue) {
    throw 'Mental-o est ouvert : fermez-le avant de fabriquer la release.'
}
cmd /c .\build.bat
if ($LASTEXITCODE -ne 0 -or -not (Test-Path 'Mental-o.exe')) { throw 'Compilation ratée : Mental-o.exe manque.' }

# 2. rassembler ce qui part dans l'archive : l'exe, le fichier de portage,
#    la version, le logo du catalogue et les icônes utilisées à l'exécution
#    (fichier-tea.ico pour l'association) — pas les outils, pas les sources
$stage = Join-Path 'dist' 'stage-win'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
$fichiers = @('Mental-o.exe', 'mental-o.stargazer.json', 'VERSION',
              'ressources\logo.png', 'ressources\mentalo.ico', 'ressources\fichier-tea.ico')
foreach ($f in $fichiers) { Copier $f $stage }

$zipWin = Join-Path $dist 'mental-o-windows-portable.zip'
Zipper $stage $zipWin
Remove-Item -Recurse -Force $stage

# 3. l'installeur : le talon compilé avec l'archive embarquée en ressource
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$setup = Join-Path $dist 'Mental-o-Setup-Windows.exe'
& $csc /nologo /target:winexe "/out:$setup" /optimize+ /codepage:65001 `
    /win32icon:ressources\mentalo.ico `
    "/resource:$zipWin,app.zip" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll `
    outils\setup-stub.cs
if ($LASTEXITCODE -ne 0) { throw 'Compilation de l''installeur ratée.' }

Write-Host ''
Write-Host "Archive Windows  : $zipWin"
Write-Host "Installeur       : $setup"
Write-Host "Publication      : gh release create v$version `"$zipWin`" `"$setup`" --title `"Mental-o $version`" --notes-file <notes.md>"
