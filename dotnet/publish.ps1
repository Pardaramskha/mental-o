# publish.ps1 — fabrique l'archive portable de Mental-O (Avalonia, .NET 8)
# pour UN système, dans dist\ (26/10/2026, sur le modèle de Marabook) :
#
#   -Rid win-x64     → dist\mental-o-windows-portable.zip   (nom INCHANGÉ : l'Updater
#                      de l'ancien Mental-o 0.18 et le README pointent releases/latest/download)
#   -Rid linux-x64   → dist\mental-o-linux-portable.zip
#   -Rid osx-x64     → dist\mental-o-macos-portable.zip
#   -Rid osx-arm64   → dist\mental-o-macos-arm64-portable.zip
#
#   powershell -ExecutionPolicy Bypass -File dotnet\publish.ps1 -Rid win-x64
#   pwsh dotnet/publish.ps1 -Rid linux-x64          (Linux, macOS : PowerShell 7)
#
# Ce qui part : la publication autonome (self-contained, un seul dossier, le
# runtime .NET compris), avec Mental-O(.exe) À LA RACINE, plus VERSION,
# mental-o.stargazer.json et ressources\fichier-tea.ico. Même disposition que
# la 0.18 : son script de mise à jour recopie l'archive par-dessus et relance
# Mental-o.exe (le nom ne diffère que par la casse : Windows s'en moque).
# Les noms ne portent PAS la version (elle est dans le tag et dans VERSION).
param(
    [Parameter(Mandatory = $true)][string]$Rid,
    [string]$Configuration = 'Release',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$version = ([IO.File]::ReadAllText('VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') {
    throw "VERSION doit être de la forme majeure.mineure.correctif[-suffixe] (lu : « $version »)."
}
$assetNames = @{
    'win-x64'   = 'mental-o-windows-portable.zip'
    'linux-x64' = 'mental-o-linux-portable.zip'
    'osx-x64'   = 'mental-o-macos-portable.zip'
    'osx-arm64' = 'mental-o-macos-arm64-portable.zip'
}
if (-not $assetNames.ContainsKey($Rid)) { throw "RID inconnu : $Rid (win-x64, linux-x64, osx-x64, osx-arm64)." }
$asset = $assetNames[$Rid]
$ridWindows = $Rid.StartsWith('win')

Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force 'dist' | Out-Null
$dist = (Resolve-Path 'dist').Path

# Zip entrée par entrée, noms en « / » (CreateFromDirectory sous
# PowerShell 5 écrit des « \ », illisibles hors Windows).
function Zipper($dossier, $zip) {
    if (Test-Path $zip) { Remove-Item -Force $zip }
    $racine = (Resolve-Path $dossier).Path.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
    try {
        foreach ($fichier in Get-ChildItem $dossier -Recurse -File) {
            $nom = $fichier.FullName.Substring($racine.Length).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $fichier.FullName, $nom, 'Optimal') | Out-Null
        }
    } finally { $archive.Dispose() }
}

$stage = Join-Path $dist ("stage-" + $Rid)
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
if (-not $SkipBuild) {
    & dotnet publish (Join-Path 'dotnet' 'MentalO.App') -c $Configuration -r $Rid --self-contained -o $stage -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ($Rid) a échoué." }
}
$exe = if ($ridWindows) { 'Mental-O.exe' } else { 'Mental-O' }
if (-not (Test-Path (Join-Path $stage $exe))) { throw "La publication ne contient pas $exe." }
Get-ChildItem $stage -Filter '*.pdb' | Remove-Item -Force
Copy-Item 'VERSION' $stage -Force
Copy-Item 'mental-o.stargazer.json' $stage -Force
New-Item -ItemType Directory -Force (Join-Path $stage 'ressources') | Out-Null
Copy-Item (Join-Path 'ressources' 'fichier-tea.ico') (Join-Path $stage 'ressources') -Force
Copy-Item (Join-Path 'ressources' 'mentalo.ico') (Join-Path $stage 'ressources') -Force

$out = Join-Path $dist $asset
if ($ridWindows) {
    Zipper $stage $out
} else {
    # Hors Windows, le bit d'exécution : chmod puis zip (ditto/unzip le gardent sur macOS ;
    # sous Linux, l'utilisateur lance « chmod +x Mental-O » si besoin — la même règle que Marabook).
    if (Get-Command chmod -ErrorAction SilentlyContinue) { & chmod +x (Join-Path $stage $exe) }
    Zipper $stage $out
}
Write-Host "Archive : $out (version $version)"
