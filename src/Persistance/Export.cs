using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using Mentalo.Modele;
using Mentalo.Vue;

namespace Mentalo.Persistance
{
    /// <summary>Exports de la carte vers des formats ouverts : SVG (dessin vectoriel
    /// approché du rendu) et Markdown (arborescence de texte suivant les liens).</summary>
    public static class Export
    {
        private static string Nombre(double valeur)
        {
            return valeur.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string EchapperXml(string texte)
        {
            return (texte ?? "")
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        // ------------------------------------------------------------------ SVG

        public static void ExporterSvg(ToileVue toile, string chemin)
        {
            DocumentCarte document = toile.Document;
            Rect bornes = toile.BornesContenu();
            if (bornes == Rect.Empty)
                throw new InvalidOperationException("La carte est vide : rien à exporter.");
            bornes.Inflate(30, 30);

            StringBuilder svg = new StringBuilder();
            svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            svg.AppendLine(string.Format(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"{2} {3} {0} {1}\">",
                Nombre(bornes.Width), Nombre(bornes.Height), Nombre(bornes.X), Nombre(bornes.Y)));

            string fond = TexteRiche.HexaDepuisCouleur(toile.CouleurFondEffective());
            svg.AppendLine(string.Format(
                "  <rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\" fill=\"{4}\"/>",
                Nombre(bornes.X), Nombre(bornes.Y), Nombre(bornes.Width), Nombre(bornes.Height), fond));

            foreach (Groupe groupe in document.Groupes)
                EcrireGroupeSvg(svg, toile, groupe, fond);
            foreach (Lien lien in document.Liens)
                EcrireLienSvg(svg, toile, lien, fond);
            foreach (Noeud noeud in document.Noeuds)
                EcrireNoeudSvg(svg, toile, noeud);

            svg.AppendLine("</svg>");
            File.WriteAllText(chemin, svg.ToString(), Encoding.UTF8);
        }

        private static string TiretsSvg(string trait, double epaisseur)
        {
            // Mêmes proportions que le rendu WPF (multiples de l'épaisseur).
            if (trait == "tirets") return string.Format("stroke-dasharray=\"{0} {1}\" ",
                Nombre(4 * epaisseur), Nombre(2.5 * epaisseur));
            if (trait == "points") return string.Format("stroke-dasharray=\"{0} {1}\" ",
                Nombre(0.1 * epaisseur), Nombre(2.5 * epaisseur));
            return "";
        }

        private static void EcrireGroupeSvg(StringBuilder svg, ToileVue toile, Groupe groupe, string fondCanevas)
        {
            Rect cadre = toile.RectangleDuGroupe(groupe);
            if (cadre == Rect.Empty) return;
            string couleur = CouleurSure(groupe.Couleur, "#8A94A8");
            double epaisseur = groupe.BordureEpaisseur > 0 ? groupe.BordureEpaisseur : 1.5;
            string trait = string.IsNullOrEmpty(groupe.BordureTrait) ? "tirets" : groupe.BordureTrait;
            svg.AppendLine(string.Format(
                "  <rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\" rx=\"10\" fill=\"{4}\" fill-opacity=\"0.08\" stroke=\"{4}\" stroke-width=\"{5}\" {6}stroke-linecap=\"round\"/>",
                Nombre(cadre.X), Nombre(cadre.Y), Nombre(cadre.Width), Nombre(cadre.Height),
                couleur, Nombre(epaisseur), TiretsSvg(trait, epaisseur)));

            if (string.IsNullOrWhiteSpace(groupe.Titre)) return;
            double taille = groupe.TitreTaille > 0 ? groupe.TitreTaille : 12.5;
            bool enBas = groupe.TitrePosition != null && groupe.TitrePosition.StartsWith("bas");
            bool aGauche = groupe.TitrePosition != null && groupe.TitrePosition.EndsWith("gauche");
            double y = enBas ? cadre.Bottom : cadre.Y;
            double largeurTexte = groupe.Titre.Length * taille * 0.58 + 12;
            double x = aGauche ? cadre.X + 10 : cadre.Right - 10 - largeurTexte;
            string couleurTitre = string.IsNullOrEmpty(groupe.TitreCouleur) ? couleur : CouleurSure(groupe.TitreCouleur, couleur);
            string fondTitre = string.IsNullOrEmpty(groupe.TitreFond) ? fondCanevas : CouleurSure(groupe.TitreFond, fondCanevas);
            svg.AppendLine(string.Format(
                "  <rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\" fill=\"{4}\"/>",
                Nombre(x), Nombre(y - taille * 0.7), Nombre(largeurTexte), Nombre(taille * 1.4), fondTitre));
            svg.AppendLine(string.Format(
                "  <text x=\"{0}\" y=\"{1}\" font-family=\"Segoe UI\" font-size=\"{2}\" font-weight=\"600\" fill=\"{3}\">{4}</text>",
                Nombre(x + 6), Nombre(y + taille * 0.35), Nombre(taille), couleurTitre, EchapperXml(groupe.Titre)));
        }

        private static void EcrireLienSvg(StringBuilder svg, ToileVue toile, Lien lien, string fondCanevas)
        {
            Point a, b;
            if (!toile.PointsLien(lien, out a, out b)) return;
            string couleur = CouleurSure(lien.Couleur, "#6A665E");

            string cheminTrace;
            Vector dirDepart, dirArrivee;
            Vector direction = b - a;
            if (direction.Length < 1) return;
            if (lien.Trace == "courbe" && Math.Abs(lien.Courbure) > 0.5)
            {
                Point controle = LienVisuel.ControleCourbe(a, b, lien.Courbure);
                cheminTrace = string.Format("M {0} {1} Q {2} {3} {4} {5}",
                    Nombre(a.X), Nombre(a.Y), Nombre(controle.X), Nombre(controle.Y), Nombre(b.X), Nombre(b.Y));
                dirDepart = controle - a;
                dirArrivee = b - controle;
            }
            else if (lien.Trace == "equerre")
            {
                Point[] coudes = LienVisuel.CoudesEquerre(a, b);
                cheminTrace = string.Format("M {0} {1} L {2} {3} L {4} {5} L {6} {7}",
                    Nombre(a.X), Nombre(a.Y), Nombre(coudes[0].X), Nombre(coudes[0].Y),
                    Nombre(coudes[1].X), Nombre(coudes[1].Y), Nombre(b.X), Nombre(b.Y));
                dirDepart = coudes[0] - a;
                dirArrivee = b - coudes[1];
            }
            else
            {
                cheminTrace = string.Format("M {0} {1} L {2} {3}",
                    Nombre(a.X), Nombre(a.Y), Nombre(b.X), Nombre(b.Y));
                dirDepart = direction;
                dirArrivee = direction;
            }
            if (dirDepart.Length < 0.001) dirDepart = direction;
            if (dirArrivee.Length < 0.001) dirArrivee = direction;
            dirDepart.Normalize();
            dirArrivee.Normalize();

            svg.AppendLine(string.Format(
                "  <path d=\"{0}\" fill=\"none\" stroke=\"{1}\" stroke-width=\"{2}\" {3}stroke-linecap=\"round\" stroke-linejoin=\"round\"/>",
                cheminTrace, couleur, Nombre(lien.Epaisseur), TiretsSvg(lien.Trait, lien.Epaisseur)));

            double taille = 6 + lien.Epaisseur * 2.2;
            if (lien.Fleche == "simple" || lien.Fleche == "double")
                EcrireFlecheSvg(svg, b, dirArrivee, taille, couleur);
            if (lien.Fleche == "double")
                EcrireFlecheSvg(svg, a, -dirDepart, taille, couleur);

            if (!string.IsNullOrEmpty(lien.Etiquette))
            {
                Point milieu = LienVisuel.PointMilieu(a, b, lien);
                double largeurTexte = lien.Etiquette.Length * 12 * 0.58 + 10;
                svg.AppendLine(string.Format(
                    "  <rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"17\" fill=\"{3}\"/>",
                    Nombre(milieu.X - largeurTexte / 2), Nombre(milieu.Y - 8.5), Nombre(largeurTexte), fondCanevas));
                svg.AppendLine(string.Format(
                    "  <text x=\"{0}\" y=\"{1}\" font-family=\"Segoe UI\" font-size=\"12\" font-weight=\"600\" text-anchor=\"middle\" fill=\"{2}\">{3}</text>",
                    Nombre(milieu.X), Nombre(milieu.Y + 4), couleur, EchapperXml(lien.Etiquette)));
            }
        }

        private static void EcrireFlecheSvg(StringBuilder svg, Point pointe, Vector direction, double taille, string couleur)
        {
            Vector perpendiculaire = new Vector(-direction.Y, direction.X);
            Point basePointe = pointe - direction * taille;
            Point c1 = basePointe + perpendiculaire * (taille * 0.45);
            Point c2 = basePointe - perpendiculaire * (taille * 0.45);
            svg.AppendLine(string.Format(
                "  <polygon points=\"{0},{1} {2},{3} {4},{5}\" fill=\"{6}\"/>",
                Nombre(pointe.X), Nombre(pointe.Y), Nombre(c1.X), Nombre(c1.Y), Nombre(c2.X), Nombre(c2.Y), couleur));
        }

        private static void EcrireNoeudSvg(StringBuilder svg, ToileVue toile, Noeud noeud)
        {
            Rect rect = toile.RectangleDuNoeud(noeud);
            double opacite = noeud.Opacite > 0 ? Math.Max(0.15, Math.Min(1.0, noeud.Opacite)) : 1;
            svg.AppendLine(opacite < 1
                ? string.Format("  <g opacity=\"{0}\">", Nombre(opacite))
                : "  <g>");

            bool transparent = string.Equals(noeud.FondCouleur, "Transparent", StringComparison.OrdinalIgnoreCase);
            string fond = transparent ? "none" : CouleurSure(noeud.FondCouleur, "#FFFFFF");
            double epaisseur = noeud.BordureTrait == "aucune" ? 0 : noeud.BordureEpaisseur;
            string bordure = epaisseur > 0
                ? string.Format("stroke=\"{0}\" stroke-width=\"{1}\" {2}",
                    CouleurSure(noeud.BordureCouleur, "#B8B2A7"), Nombre(epaisseur),
                    TiretsSvg(noeud.BordureTrait, epaisseur))
                : "";
            svg.AppendLine(string.Format(
                "    <rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\" rx=\"{4}\" fill=\"{5}\" {6}/>",
                Nombre(rect.X), Nombre(rect.Y), Nombre(rect.Width), Nombre(rect.Height),
                Nombre(noeud.Rayon), fond, bordure));

            double pad = NoeudVisuel.PaddingTexte + epaisseur / 2;
            double padV = Math.Max(2, NoeudVisuel.PaddingTexte * 2 / 3) + epaisseur / 2;
            double gauche = rect.X + pad;
            double droite = rect.Right - pad;
            double curseurY = rect.Y + padV;

            curseurY = EcrireLignesSvg(svg, noeud.Segments, noeud.Alignement, gauche, droite, curseurY);

            if (noeud.EstImage)
            {
                string mime = noeud.ImageExtension == ".jpg" || noeud.ImageExtension == ".jpeg" ? "image/jpeg"
                    : noeud.ImageExtension == ".gif" ? "image/gif"
                    : noeud.ImageExtension == ".bmp" ? "image/bmp"
                    : "image/png";
                string alignement = noeud.Alignement;
                double xImage = alignement == "centre" ? (gauche + droite - noeud.ImageLargeur) / 2
                    : alignement == "droite" ? droite - noeud.ImageLargeur
                    : gauche;
                if (noeud.Segments.Count > 0) curseurY += 6;
                svg.AppendLine(string.Format(
                    "    <image x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"data:{4};base64,{5}\"/>",
                    Nombre(xImage), Nombre(curseurY), Nombre(noeud.ImageLargeur), Nombre(noeud.ImageHauteur),
                    mime, Convert.ToBase64String(noeud.ImageDonnees)));
                curseurY += noeud.ImageHauteur + 4;
                EcrireLignesSvg(svg, noeud.SegmentsApres, noeud.Alignement, gauche, droite, curseurY);
            }

            svg.AppendLine("  </g>");
        }

        /// <summary>Découpe les segments en lignes (les « \n » vivent dans le texte).</summary>
        public static List<List<Segment>> DecouperEnLignes(List<Segment> segments)
        {
            List<List<Segment>> lignes = new List<List<Segment>>();
            List<Segment> courante = new List<Segment>();
            foreach (Segment segment in segments)
            {
                string[] morceaux = (segment.Texte ?? "").Split('\n');
                for (int i = 0; i < morceaux.Length; i++)
                {
                    if (i > 0)
                    {
                        lignes.Add(courante);
                        courante = new List<Segment>();
                    }
                    if (morceaux[i].Length == 0) continue;
                    Segment copie = segment.Cloner();
                    copie.Texte = morceaux[i];
                    courante.Add(copie);
                }
            }
            lignes.Add(courante);
            // Une dernière ligne vide née d'un « \n » final n'apporte rien.
            while (lignes.Count > 1 && lignes[lignes.Count - 1].Count == 0)
                lignes.RemoveAt(lignes.Count - 1);
            return lignes;
        }

        private static double EcrireLignesSvg(StringBuilder svg, List<Segment> segments,
            string alignementBoite, double gauche, double droite, double curseurY)
        {
            foreach (List<Segment> ligne in DecouperEnLignes(segments))
            {
                double tailleLigne = Segment.TailleDefaut;
                string alignement = alignementBoite;
                foreach (Segment s in ligne)
                {
                    tailleLigne = Math.Max(tailleLigne, s.Taille);
                    if (!string.IsNullOrEmpty(s.AlignementLigne)) alignement = s.AlignementLigne;
                }
                double hauteurLigne = tailleLigne * 1.35;
                if (ligne.Count > 0)
                {
                    string ancre = alignement == "centre" ? "middle" : alignement == "droite" ? "end" : "start";
                    double x = alignement == "centre" ? (gauche + droite) / 2
                        : alignement == "droite" ? droite : gauche;
                    StringBuilder texte = new StringBuilder();
                    foreach (Segment s in ligne)
                    {
                        StringBuilder style = new StringBuilder();
                        style.Append("font-family:").Append(EchapperXml(s.Police)).Append(";font-size:")
                            .Append(Nombre(s.Taille)).Append("px;fill:").Append(CouleurSure(s.Couleur, "#1C1B19"));
                        if (s.Gras) style.Append(";font-weight:bold");
                        if (s.Italique) style.Append(";font-style:italic");
                        if (s.Souligne && s.Barre) style.Append(";text-decoration:underline line-through");
                        else if (s.Souligne) style.Append(";text-decoration:underline");
                        else if (s.Barre) style.Append(";text-decoration:line-through");
                        texte.Append(string.Format("<tspan style=\"{0}\">{1}</tspan>", style, EchapperXml(s.Texte)));
                    }
                    svg.AppendLine(string.Format(
                        "    <text x=\"{0}\" y=\"{1}\" text-anchor=\"{2}\">{3}</text>",
                        Nombre(x), Nombre(curseurY + tailleLigne * 1.02), ancre, texte));
                }
                curseurY += hauteurLigne;
            }
            return curseurY;
        }

        private static string CouleurSure(string hexa, string repli)
        {
            if (string.IsNullOrEmpty(hexa)) return repli;
            return TexteRiche.HexaDepuisCouleur(TexteRiche.CouleurDepuis(hexa,
                TexteRiche.CouleurDepuis(repli, System.Windows.Media.Colors.Gray)));
        }

        // ------------------------------------------------------------------ Markdown

        public static void ExporterMarkdown(DocumentCarte document, string nomParDefaut, string chemin)
        {
            Dictionary<string, Noeud> parId = new Dictionary<string, Noeud>();
            foreach (Noeud n in document.Noeuds) parId[n.Id] = n;

            // Voisins de chaque boîte ; une flèche simple oriente la descente de l'arbre.
            Dictionary<string, List<Lien>> voisins = new Dictionary<string, List<Lien>>();
            Dictionary<string, int> entrants = new Dictionary<string, int>();
            foreach (Noeud n in document.Noeuds)
            {
                voisins[n.Id] = new List<Lien>();
                entrants[n.Id] = 0;
            }
            foreach (Lien lien in document.Liens)
            {
                if (!parId.ContainsKey(lien.SourceId) || !parId.ContainsKey(lien.CibleId)) continue;
                voisins[lien.SourceId].Add(lien);
                voisins[lien.CibleId].Add(lien);
                if (lien.Fleche == "simple") entrants[lien.CibleId]++;
            }

            StringBuilder markdown = new StringBuilder();
            string titre = !string.IsNullOrWhiteSpace(document.NomAffichage) ? document.NomAffichage : nomParDefaut;
            markdown.AppendLine("# " + titre);
            if (!string.IsNullOrWhiteSpace(document.Auteur))
                markdown.AppendLine().AppendLine("*" + document.Auteur + "*");

            HashSet<string> visites = new HashSet<string>();
            foreach (Noeud racine in ChoisirRacines(document, voisins, entrants))
            {
                if (visites.Contains(racine.Id)) continue;
                markdown.AppendLine();
                EcrireBrancheMarkdown(markdown, racine, null, 0, parId, voisins, visites);
            }

            bool premierGroupe = true;
            foreach (Groupe groupe in document.Groupes)
            {
                if (groupe.NoeudIds.Count == 0) continue;
                if (premierGroupe)
                {
                    markdown.AppendLine();
                    markdown.AppendLine("## Groupes");
                    premierGroupe = false;
                }
                markdown.AppendLine();
                markdown.AppendLine("**" + (string.IsNullOrWhiteSpace(groupe.Titre) ? "Groupe sans titre" : groupe.Titre) + "**");
                foreach (string id in groupe.NoeudIds)
                {
                    Noeud membre;
                    if (parId.TryGetValue(id, out membre))
                        markdown.AppendLine("- " + TexteEnLigne(membre));
                }
            }

            File.WriteAllText(chemin, markdown.ToString(), Encoding.UTF8);
        }

        /// <summary>Les points de départ de l'arborescence : d'abord les boîtes sans flèche
        /// entrante, puis (composantes restantes) les plus connectées.</summary>
        private static List<Noeud> ChoisirRacines(DocumentCarte document,
            Dictionary<string, List<Lien>> voisins, Dictionary<string, int> entrants)
        {
            List<Noeud> racines = new List<Noeud>();
            foreach (Noeud n in document.Noeuds)
                if (entrants[n.Id] == 0) racines.Add(n);
            List<Noeud> parDegre = new List<Noeud>(document.Noeuds);
            parDegre.Sort(delegate(Noeud x, Noeud y)
            {
                return voisins[y.Id].Count.CompareTo(voisins[x.Id].Count);
            });
            racines.AddRange(parDegre);
            return racines;
        }

        private static void EcrireBrancheMarkdown(StringBuilder markdown, Noeud noeud, Lien lienArrivee,
            int profondeur, Dictionary<string, Noeud> parId,
            Dictionary<string, List<Lien>> voisins, HashSet<string> visites)
        {
            visites.Add(noeud.Id);
            string etiquette = lienArrivee != null && !string.IsNullOrEmpty(lienArrivee.Etiquette)
                ? " *(" + lienArrivee.Etiquette + ")*" : "";
            markdown.AppendLine(new string(' ', profondeur * 2) + "- " + TexteEnLigne(noeud) + etiquette);
            foreach (Lien lien in voisins[noeud.Id])
            {
                // Une flèche simple ne se descend que dans son sens.
                if (lien.Fleche == "simple" && lien.CibleId == noeud.Id) continue;
                string autreId = lien.SourceId == noeud.Id ? lien.CibleId : lien.SourceId;
                Noeud autre;
                if (!parId.TryGetValue(autreId, out autre) || visites.Contains(autreId)) continue;
                EcrireBrancheMarkdown(markdown, autre, lien, profondeur + 1, parId, voisins, visites);
            }
        }

        private static string TexteEnLigne(Noeud noeud)
        {
            string texte = noeud.TextePlat.Replace("\r", "").Replace("\n", " / ").Trim();
            if (texte.Length == 0) texte = noeud.EstImage ? "(image)" : "(vide)";
            return texte;
        }

        // ------------------------------------------------------------------ import d'un texte à puces

        /// <summary>Transforme un texte à puces indenté en arbre de boîtes liées.
        /// Renvoie les boîtes et les liens à créer (centrés autour de « centre »).</summary>
        public static void ImporterTexteAPuces(string texte, Point centre,
            out List<Noeud> noeuds, out List<Lien> liens)
        {
            noeuds = new List<Noeud>();
            liens = new List<Lien>();
            if (string.IsNullOrWhiteSpace(texte)) return;

            // Les largeurs d'indentation rencontrées, classées, donnent les niveaux.
            List<KeyValuePair<int, string>> lignes = new List<KeyValuePair<int, string>>();
            SortedSet<int> largeurs = new SortedSet<int>();
            foreach (string brute in texte.Replace("\r", "").Split('\n'))
            {
                if (brute.Trim().Length == 0) continue;
                int largeur = 0;
                foreach (char c in brute)
                {
                    if (c == ' ') largeur += 1;
                    else if (c == '\t') largeur += 4;
                    else break;
                }
                string contenu = RetirerMarqueur(brute.Trim());
                if (contenu.Length == 0) continue;
                lignes.Add(new KeyValuePair<int, string>(largeur, contenu));
                largeurs.Add(largeur);
            }
            if (lignes.Count == 0) return;
            Dictionary<int, int> niveauParLargeur = new Dictionary<int, int>();
            int niveau = 0;
            foreach (int largeur in largeurs)
                niveauParLargeur[largeur] = niveau++;

            List<Noeud> pile = new List<Noeud>(); // pile[n] = dernière boîte du niveau n
            int rang = 0;
            foreach (KeyValuePair<int, string> ligne in lignes)
            {
                int profondeur = niveauParLargeur[ligne.Key];
                if (profondeur > pile.Count) profondeur = pile.Count; // saut d'indentation abrupt

                Noeud noeud = new Noeud();
                noeud.DefinirTexteSimple(ligne.Value);
                noeud.X = profondeur * 260;
                noeud.Y = rang * 64;
                rang++;
                noeuds.Add(noeud);

                if (profondeur > 0 && pile.Count >= profondeur)
                {
                    Lien lien = new Lien();
                    lien.SourceId = pile[profondeur - 1].Id;
                    lien.CibleId = noeud.Id;
                    lien.Fleche = "simple";
                    liens.Add(lien);
                }
                if (pile.Count > profondeur) pile.RemoveRange(profondeur, pile.Count - profondeur);
                pile.Add(noeud);
            }

            // L'arbre arrive centré sur le point demandé (approximation avant mesure).
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (Noeud n in noeuds)
            {
                minX = Math.Min(minX, n.X);
                minY = Math.Min(minY, n.Y);
                maxX = Math.Max(maxX, n.X + 180);
                maxY = Math.Max(maxY, n.Y + 40);
            }
            double decalageX = centre.X - (minX + maxX) / 2;
            double decalageY = centre.Y - (minY + maxY) / 2;
            foreach (Noeud n in noeuds)
            {
                n.X += decalageX;
                n.Y += decalageY;
            }
        }

        private static string RetirerMarqueur(string ligne)
        {
            foreach (string marqueur in new string[] { "- ", "* ", "• ", "– ", "+ " })
                if (ligne.StartsWith(marqueur)) return ligne.Substring(marqueur.Length).Trim();
            // Listes numérotées : « 12. » ou « 3) ».
            int position = 0;
            while (position < ligne.Length && char.IsDigit(ligne[position])) position++;
            if (position > 0 && position < ligne.Length && (ligne[position] == '.' || ligne[position] == ')'))
            {
                string reste = ligne.Substring(position + 1);
                if (reste.StartsWith(" ")) return reste.Trim();
            }
            return ligne.Trim();
        }
    }
}
