# extraire-textes-ui.ps1 — dresse la liste de TOUS les textes affichés par
# l'interface (menus, infobulles, boutons, dialogues, messages, titres) dans
# TEXTES-UI.md à la racine, fichier par fichier avec le numéro de ligne, pour
# repasser sur chaque formulation à la main.
#
#   powershell -ExecutionPolicy Bypass -File outils\extraire-textes-ui.ps1
#
# Principe : on lit chaque src\*.cs, on prend chaque chaîne littérale qui
# ressemble à du texte lu par l'utilisateur (au moins une lettre, et un espace,
# un accent, un « … » ou une ponctuation finale), on écarte les identifiants,
# chemins, formats, XAML et expressions régulières. Les fragments d'une même
# chaîne coupée sur plusieurs lignes par « + » sont recollés. Le tri final
# reste à l'œil : un texte manquant ou en trop se corrige ici, dans les
# filtres, pas dans le .md (il est régénéré à chaque passage).

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$utf8 = New-Object System.Text.UTF8Encoding($false)

# Fichiers sans texte d'interface : gabarits XAML, tracés d'icônes.
$exclus = @('Theme.cs', 'Icones.cs')

# Une chaîne littérale C# : "…" avec échappements, ou @"…" avec "" doublées.
$motifLitteral = '@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*"'

function Decoder($brut) {
    if ($brut.StartsWith('@')) { return $brut.Substring(2, $brut.Length - 3).Replace('""', '"') }
    $s = $brut.Substring(1, $brut.Length - 2)
    $s = [regex]::Replace($s, '\\u([0-9a-fA-F]{4})', { param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16) })
    return $s.Replace('\n', "`n").Replace('\r', '').Replace('\t', "`t").Replace('\"', '"').Replace('\\', '\')
}

function EstTexteUI($s) {
    if ($s -notmatch '\p{L}') { return $false }                      # aucune lettre
    # (-cmatch : sensible à la casse — « Fermer » est un libellé, « fermer » une clé)
    if ($s -cmatch '^[a-z0-9\-\.:/]+$') { return $false }            # clé d'action, extension (« centre-h », « gras »)
    if ($s -cmatch '^(Ctrl|Alt|Maj)\+') { return $false }             # raccourci affiché (InputGestureText)
    if ($s -cmatch '^[yMdHhmsf\-_ :/''.]+$') { return $false }        # format de date (« yyyy-MM-dd HH:mm »)
    if ($s -cmatch '^"|^[a-z_]+$|^[a-z]+/[a-z\.\+\-]+$') { return $false }   # fragment JSON, clé (tag_name), type MIME
    if ($s -cmatch '^(VERSION|GITHUB_TOKEN|Bearer |Mental-o|app\.zip|UTF-8|fr-FR|[a-z]+-[a-z]+-[a-z]+)$') { return $false }
    if ($s -cmatch '@echo|tasklist|errorlevel|xcopy|rmdir|^cd /d|^start ') { return $false }   # script cmd de mise à jour
    if ($s -cmatch '^&\w+;$|^stroke|^fill|^M \{|^L \{|="|^\s*<|^;|^px;') { return $false }   # fragments SVG / XML / CSS
    if ($s -cmatch '^[A-Z]$|\.exe$|^[A-Za-z]+/[A-Za-z\-]+$') { return $false }   # format Guid, nom d'exe, dépôt GitHub
    if ($s -match '^\{\d+\}$') { return $false }                      # format seul
    if ($s -match '^[A-Za-z]:\\|^\\\\|\.(cs|ico|png|json|lex|tea|zeubi|md|txt|svg|ps1|bat)$') { return $false }
    if ($s -match '^<|^</|xmlns|^\s*<\w+.*>\s*$') { return $false }  # XAML / XML / SVG
    if ($s -match '^\^|\\d|\\s|\\w|\[\^') { return $false }          # expression régulière
    if ($s -match '^#[0-9A-Fa-f]{6}') { return $false }              # couleur
    if ($s -match '^(Segoe|Consolas|Calibri|Arial)') { return $false } # police
    if ($s -match '^[A-Z][a-z]+(\.[A-Z]\w+)+$') { return $false }    # Mentalo.Carte
    if ($s -match '\s|\p{Lu}|[àâäéèêëîïôöùûüçœ…:!?\.]' ) { return $true }
    return $false
}

$sections = New-Object System.Text.StringBuilder
$total = 0
foreach ($fichier in Get-ChildItem 'src' -Recurse -Filter '*.cs' | Sort-Object FullName) {
    if ($exclus -contains $fichier.Name) { continue }
    $lignes = [IO.File]::ReadAllLines($fichier.FullName, $utf8)
    $entrees = @()
    $enCours = $null          # @{ Ligne = n ; Texte = '' }
    $precedentePlus = $false
    for ($i = 0; $i -lt $lignes.Count; $i++) {
        $ligne = $lignes[$i]
        $nette = $ligne.Trim()
        if ($nette -match '^(//|/\*|\*)') { $precedentePlus = $false; continue }   # commentaires
        $sansCommentaire = [regex]::Replace($ligne, '(?<=^(?:[^"]|"(?:[^"\\]|\\.)*")*)//.*$', '')
        $fragments = [regex]::Matches($sansCommentaire, $motifLitteral) | ForEach-Object { Decoder $_.Value }
        $continuation = $precedentePlus -and $nette.StartsWith('"')
        $precedentePlus = $sansCommentaire.TrimEnd() -match '\+\s*$'
        if ($fragments.Count -eq 0) { if (-not $precedentePlus) { $enCours = $null }; continue }
        foreach ($f in $fragments) {
            if ($continuation -and $enCours -ne $null) { $enCours.Texte += $f; $continuation = $false; continue }
            $continuation = $false
            $enCours = @{ Ligne = $i + 1; Texte = $f }
            $entrees += $enCours
        }
        if (-not $precedentePlus) { $enCours = $null }
    }
    $gardees = $entrees | Where-Object { EstTexteUI $_.Texte }
    if ($gardees.Count -eq 0) { continue }
    $chemin = $fichier.FullName.Substring((Get-Location).Path.Length + 1).Replace('\', '/')
    [void]$sections.AppendLine("## $chemin")
    [void]$sections.AppendLine('')
    [void]$sections.AppendLine('| Ligne | Texte |')
    [void]$sections.AppendLine('|---|---|')
    foreach ($e in $gardees) {
        $t = $e.Texte.Replace('|', '\|').Replace("`n", ' ⏎ ').Replace("`t", ' ')
        [void]$sections.AppendLine("| $($e.Ligne) | $t |")
        $total++
    }
    [void]$sections.AppendLine('')
}

$entete = @"
# Mental-o — textes de l'interface

Généré par ``outils\extraire-textes-ui.ps1`` (relancer après modification des sources : le fichier est réécrit).
$total textes, relevés dans les sources avec leur numéro de ligne.

Conventions de lecture :
- ``_`` devant une lettre dans un titre de menu marque la touche d'accès (Alt) : « É_dition » s'affiche « Édition ».
- ``⏎`` marque un retour à la ligne dans le texte.
- ``{0}``, ``{1}`` sont remplacés à l'affichage (nom de fichier, nombre, message d'erreur).
- Une ligne peut porter le début d'un texte assemblé sur plusieurs lignes : le texte est recollé ici, la ligne est celle du premier morceau.

"@
[IO.File]::WriteAllText('TEXTES-UI.md', $entete + $sections.ToString(), $utf8)
Write-Host "TEXTES-UI.md : $total textes."
