using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Mentalo.Modele;
using Mentalo.Vue;

namespace Mentalo.Persistance
{
    /// <summary>Le format ouvert JSON Canvas 1.0 (Obsidian Canvas, Kinopio…) — v0.17.
    /// Export : boîtes → nœuds text/file, groupes → group, liens → edges (côtés libres,
    /// flèches, couleur, étiquette) ; les images et fichiers intégrés sont écrits dans un
    /// dossier « &lt;nom&gt;_fichiers » à côté du .canvas. Import : l'inverse, l'appartenance aux
    /// groupes se déduit de la géométrie (une boîte va au plus petit groupe qui la contient).</summary>
    public static class JsonCanvas
    {
        public const string Extension = "canvas";

        // Les six couleurs prédéfinies du format (le format laisse leurs valeurs libres).
        private static readonly string[] Prereglees = { "#C0392B", "#E67E22", "#F1C40F", "#2E8B57", "#17A2B8", "#8E44AD" };

        // ------------------------------------------------------------------ export

        public static void Exporter(IToile toile, string chemin)
        {
            DocumentCarte document = toile.Document;
            string dossier = Path.GetDirectoryName(chemin);
            string base_ = Path.GetFileNameWithoutExtension(chemin) + "_fichiers";
            string dossierFichiers = Path.Combine(dossier, base_);
            bool dossierCree = false;

            List<object> noeuds = new List<object>();
            foreach (Noeud n in document.Noeuds)
            {
                Rect rect = toile.RectangleDuNoeud(n);
                Dictionary<string, object> nd = new Dictionary<string, object>();
                nd["id"] = n.Id;
                bool imageSeule = n.EstImage && string.IsNullOrWhiteSpace(n.TextePlat) && n.Fichiers.Count == 0;
                if (imageSeule)
                {
                    if (!dossierCree) { Directory.CreateDirectory(dossierFichiers); dossierCree = true; }
                    string nomImage = n.Id + (n.ImageExtension ?? ".png");
                    File.WriteAllBytes(Path.Combine(dossierFichiers, nomImage), n.ImageDonnees);
                    nd["type"] = "file";
                    nd["file"] = base_ + "/" + nomImage;
                }
                else
                {
                    StringBuilder texte = new StringBuilder(n.TextePlat);
                    if (n.EstImage)
                    {
                        if (!dossierCree) { Directory.CreateDirectory(dossierFichiers); dossierCree = true; }
                        string nomImage = n.Id + (n.ImageExtension ?? ".png");
                        File.WriteAllBytes(Path.Combine(dossierFichiers, nomImage), n.ImageDonnees);
                        texte.Append("\n\n![](").Append(base_).Append("/").Append(nomImage).Append(")");
                    }
                    foreach (PieceJointe piece in n.Fichiers)
                    {
                        string cible = piece.Chemin ?? "";
                        if (piece.EstIntegre)
                        {
                            if (!dossierCree) { Directory.CreateDirectory(dossierFichiers); dossierCree = true; }
                            string nomPiece = piece.Id + piece.Extension;
                            File.WriteAllBytes(Path.Combine(dossierFichiers, nomPiece), piece.Donnees);
                            cible = base_ + "/" + nomPiece;
                        }
                        texte.Append("\n[").Append(piece.Nom).Append("](").Append(cible.Replace(" ", "%20")).Append(")");
                    }
                    if (n.ANote) texte.Append("\n\n> ").Append(n.Note.Replace("\n", "\n> "));
                    nd["type"] = "text";
                    nd["text"] = texte.ToString();
                }
                nd["x"] = (int)Math.Round(rect.X);
                nd["y"] = (int)Math.Round(rect.Y);
                nd["width"] = Math.Max(1, (int)Math.Round(rect.Width));
                nd["height"] = Math.Max(1, (int)Math.Round(rect.Height));
                if (!string.IsNullOrEmpty(n.FondCouleur) && !string.Equals(n.FondCouleur, "#FFFFFF", StringComparison.OrdinalIgnoreCase))
                    nd["color"] = n.FondCouleur;
                noeuds.Add(nd);
            }
            foreach (Groupe g in document.Groupes)
            {
                Rect rect = toile.RectangleDuGroupe(g);
                if (rect.Width <= 0 || rect.Height <= 0) continue;
                Dictionary<string, object> gd = new Dictionary<string, object>();
                gd["id"] = g.Id;
                gd["type"] = "group";
                gd["x"] = (int)Math.Round(rect.X);
                gd["y"] = (int)Math.Round(rect.Y);
                gd["width"] = Math.Max(1, (int)Math.Round(rect.Width));
                gd["height"] = Math.Max(1, (int)Math.Round(rect.Height));
                if (!string.IsNullOrWhiteSpace(g.Titre)) gd["label"] = g.Titre;
                if (!string.IsNullOrEmpty(g.Couleur)) gd["color"] = g.Couleur;
                noeuds.Add(gd);
            }

            List<object> aretes = new List<object>();
            foreach (Lien l in document.Liens)
            {
                Dictionary<string, object> ed = new Dictionary<string, object>();
                ed["id"] = l.Id;
                ed["fromNode"] = l.SourceId;
                ed["toNode"] = l.CibleId;
                ed["fromEnd"] = l.Fleche == "double" ? "arrow" : "none";
                ed["toEnd"] = l.Fleche == "simple" || l.Fleche == "double" ? "arrow" : "none";
                if (!string.IsNullOrEmpty(l.Couleur)) ed["color"] = l.Couleur;
                if (!string.IsNullOrEmpty(l.Etiquette)) ed["label"] = l.Etiquette;
                aretes.Add(ed);
            }

            Dictionary<string, object> racine = new Dictionary<string, object>();
            racine["nodes"] = noeuds;
            racine["edges"] = aretes;
            File.WriteAllText(chemin, Json.Serialiser(racine), new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------ import

        public static void Importer(string chemin, out List<Noeud> noeuds, out List<Lien> liens, out List<Groupe> groupes)
        {
            noeuds = new List<Noeud>();
            liens = new List<Lien>();
            groupes = new List<Groupe>();
            Dictionary<string, object> racine = Json.LireSouple(File.ReadAllText(chemin)) as Dictionary<string, object>;
            if (racine == null) throw new InvalidDataException("Ce fichier n'est pas un canevas JSON Canvas (objet racine attendu).");
            string dossier = Path.GetDirectoryName(chemin);

            Dictionary<string, string> idParOrigine = new Dictionary<string, string>(); // id du canevas → id Mental-O
            Dictionary<Groupe, Rect> rectsGroupes = new Dictionary<Groupe, Rect>();
            Dictionary<Noeud, Rect> rectsNoeuds = new Dictionary<Noeud, Rect>();

            object[] listeNoeuds = Champ(racine, "nodes") as object[];
            if (listeNoeuds != null)
            {
                foreach (object brut in listeNoeuds)
                {
                    Dictionary<string, object> nd = brut as Dictionary<string, object>;
                    if (nd == null) continue;
                    string type = Chaine(nd, "type") ?? "text";
                    string id = Chaine(nd, "id");
                    Rect rect = new Rect(Nombre(nd, "x"), Nombre(nd, "y"), Math.Max(1, Nombre(nd, "width")), Math.Max(1, Nombre(nd, "height")));
                    string couleur = Couleur(Chaine(nd, "color"));

                    if (type == "group")
                    {
                        Groupe groupe = new Groupe();
                        groupe.Titre = Chaine(nd, "label") ?? "";
                        if (couleur != null) groupe.Couleur = couleur;
                        groupes.Add(groupe);
                        rectsGroupes[groupe] = rect;
                        if (id != null) idParOrigine[id] = groupe.Id;
                        continue;
                    }

                    Noeud noeud = new Noeud();
                    noeud.X = rect.X;
                    noeud.Y = rect.Y;
                    if (couleur != null) noeud.FondCouleur = couleur;
                    if (type == "text")
                    {
                        noeud.DefinirTexteSimple((Chaine(nd, "text") ?? "").Replace("\r", ""));
                        if (rect.Width > 60) noeud.LargeurFixe = rect.Width;
                    }
                    else if (type == "link")
                    {
                        string url = Chaine(nd, "url") ?? "";
                        if (url.Length > 0) noeud.Fichiers.Add(PieceJointe.DepuisUrl(url, null));
                    }
                    else if (type == "file")
                    {
                        string fichier = (Chaine(nd, "file") ?? "").Replace('/', Path.DirectorySeparatorChar);
                        string absolu = Path.IsPathRooted(fichier) ? fichier : Path.Combine(dossier, fichier);
                        if (fichier.Length > 0 && File.Exists(absolu) && Images.ExtensionValide(absolu))
                        {
                            try
                            {
                                noeud.ImageDonnees = File.ReadAllBytes(absolu);
                                noeud.ImageExtension = Path.GetExtension(absolu).ToLowerInvariant();
                                noeud.ImageLargeur = Math.Max(20, rect.Width);
                                noeud.ImageHauteur = Math.Max(20, rect.Height);
                            }
                            catch { noeud.ImageDonnees = null; }
                        }
                        if (!noeud.EstImage && fichier.Length > 0)
                        {
                            PieceJointe piece = new PieceJointe();
                            piece.Chemin = absolu;
                            piece.NomFichier = Path.GetFileName(fichier);
                            piece.Nom = piece.NomFichier;
                            string sousChemin = Chaine(nd, "subpath");
                            if (!string.IsNullOrEmpty(sousChemin)) piece.Nom += " " + sousChemin;
                            noeud.Fichiers.Add(piece);
                        }
                    }
                    else continue;
                    if (noeud.EstVide) noeud.DefinirTexteSimple("(vide)");
                    noeuds.Add(noeud);
                    rectsNoeuds[noeud] = rect;
                    if (id != null) idParOrigine[id] = noeud.Id;
                }
            }

            // Appartenance : chaque boîte rejoint le plus petit groupe qui contient son centre.
            foreach (KeyValuePair<Noeud, Rect> kv in rectsNoeuds)
            {
                Point centre = new Point(kv.Value.X + kv.Value.Width / 2, kv.Value.Y + kv.Value.Height / 2);
                Groupe meilleur = null;
                double aire = double.MaxValue;
                foreach (KeyValuePair<Groupe, Rect> g in rectsGroupes)
                {
                    if (!g.Value.Contains(centre)) continue;
                    double a = g.Value.Width * g.Value.Height;
                    if (a < aire) { aire = a; meilleur = g.Key; }
                }
                if (meilleur != null) meilleur.NoeudIds.Add(kv.Key.Id);
            }
            groupes.RemoveAll(delegate(Groupe g) { return g.NoeudIds.Count == 0; });

            object[] listeAretes = Champ(racine, "edges") as object[];
            if (listeAretes != null)
            {
                foreach (object brut in listeAretes)
                {
                    Dictionary<string, object> ed = brut as Dictionary<string, object>;
                    if (ed == null) continue;
                    string de = Chaine(ed, "fromNode"), vers = Chaine(ed, "toNode");
                    string source, cible;
                    if (de == null || vers == null || !idParOrigine.TryGetValue(de, out source) || !idParOrigine.TryGetValue(vers, out cible)) continue;
                    bool flecheDepart = (Chaine(ed, "fromEnd") ?? "none") == "arrow";
                    bool flecheArrivee = (Chaine(ed, "toEnd") ?? "arrow") == "arrow";
                    Lien lien = new Lien();
                    if (flecheDepart && !flecheArrivee)
                    {
                        // Une flèche qui ne pointe qu'au départ : on retourne le lien.
                        lien.SourceId = cible; lien.CibleId = source; lien.Fleche = "simple";
                    }
                    else
                    {
                        lien.SourceId = source; lien.CibleId = cible;
                        lien.Fleche = flecheDepart && flecheArrivee ? "double" : flecheArrivee ? "simple" : "aucune";
                    }
                    string couleur = Couleur(Chaine(ed, "color"));
                    if (couleur != null) lien.Couleur = couleur;
                    lien.Etiquette = Chaine(ed, "label") ?? "";
                    liens.Add(lien);
                }
            }
        }

        // ------------------------------------------------------------------ lecture souple du JSON

        private static object Champ(Dictionary<string, object> objet, string cle)
        {
            object valeur;
            return objet.TryGetValue(cle, out valeur) ? valeur : null;
        }

        private static string Chaine(Dictionary<string, object> objet, string cle)
        {
            object valeur = Champ(objet, cle);
            return valeur == null ? null : valeur.ToString();
        }

        private static double Nombre(Dictionary<string, object> objet, string cle)
        {
            object valeur = Champ(objet, cle);
            if (valeur == null) return 0;
            try { return Convert.ToDouble(valeur, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        /// <summary>« #RRGGBB » tel quel, « 1 »…« 6 » → palette, sinon null.</summary>
        private static string Couleur(string brute)
        {
            if (string.IsNullOrEmpty(brute)) return null;
            if (brute.StartsWith("#") && (brute.Length == 7 || brute.Length == 9)) return brute.Substring(0, 7).ToUpperInvariant();
            int index;
            if (int.TryParse(brute, out index) && index >= 1 && index <= 6) return Prereglees[index - 1];
            return null;
        }
    }
}
