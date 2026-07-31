# Genere src\Vue\Icones.cs a partir des SVG de ressources\icons.
# Chaque fichier devient une entree du dictionnaire (nom sans extension -> traces).
$racine = Split-Path -Parent $PSScriptRoot
$dossierIcones = Join-Path $racine "ressources\icons"
$sortie = Join-Path $racine "src\Vue\Icones.cs"

$lignes = New-Object System.Collections.Generic.List[string]
$lignes.Add("using System.Collections.Generic;")
$lignes.Add("using System.Windows;")
$lignes.Add("using System.Windows.Media;")
$lignes.Add("using System.Windows.Shapes;")
$lignes.Add("")
$lignes.Add("namespace Mentalo.Vue")
$lignes.Add("{")
$lignes.Add("    /// <summary>Icones vectorielles des menus, generees depuis ressources\icons")
$lignes.Add("    /// par outils\generer-icones-menus.ps1 - ne pas editer a la main.</summary>")
$lignes.Add("    public static class Icones")
$lignes.Add("    {")
$lignes.Add("        // Pinceau partage : suit le theme clair/sombre.")
$lignes.Add("        private static Brush Encre { get { return Chrome.EncreIcone; } }")
$lignes.Add("")
$lignes.Add("        private static readonly Dictionary<string, string> Traces = new Dictionary<string, string>")
$lignes.Add("        {")

foreach ($fichier in Get-ChildItem $dossierIcones -Filter *.svg | Sort-Object Name) {
    $contenu = Get-Content $fichier.FullName -Raw
    $traces = [regex]::Matches($contenu, ' d="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    if ($traces.Count -eq 0) {
        Write-Warning "$($fichier.Name) : aucun trace <path d=...> trouve, ignore."
        continue
    }
    $donnees = ($traces -join " ")
    $nom = [System.IO.Path]::GetFileNameWithoutExtension($fichier.Name)
    $lignes.Add("            { `"$nom`", `"$donnees`" },")
}

$lignes.Add("        };")
$lignes.Add("")
$lignes.Add("        /// <summary>Construit l'icone demandee (remplissage encre, nette a tous les DPI).</summary>")
$lignes.Add("        public static FrameworkElement Creer(string nom, double taille)")
$lignes.Add("        {")
$lignes.Add("            string donnees;")
$lignes.Add("            if (!Traces.TryGetValue(nom, out donnees))")
$lignes.Add("                return new System.Windows.Controls.TextBlock { Text = `"?`" };")
$lignes.Add("            Path chemin = new Path();")
$lignes.Add("            chemin.Data = Geometry.Parse(donnees);")
$lignes.Add("            chemin.Fill = Encre;")
$lignes.Add("            chemin.Width = taille;")
$lignes.Add("            chemin.Height = taille;")
$lignes.Add("            chemin.Stretch = Stretch.Uniform;")
$lignes.Add("            chemin.VerticalAlignment = VerticalAlignment.Center;")
$lignes.Add("            chemin.HorizontalAlignment = HorizontalAlignment.Center;")
$lignes.Add("            return chemin;")
$lignes.Add("        }")
$lignes.Add("    }")
$lignes.Add("}")

[System.IO.File]::WriteAllLines($sortie, $lignes, (New-Object System.Text.UTF8Encoding $false))
Write-Output "Genere : $sortie ($((Get-ChildItem $dossierIcones -Filter *.svg).Count) icones)"
