using System;
using System.Collections.Generic;
using System.IO;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;

namespace Mentalo.Tests
{
    /// <summary>Le harnais de tests console de Mental-O (26/10/2026), contre le
    /// cœur seul : le format .tea (aller-retour complet, versions, lecture
    /// d'une carte écrite par l'ancien Mental-o WPF), l'éditeur de segments
    /// (la frappe réconciliée avec les styles), les puces, les exports et
    /// les gestes de raccourcis. OK/ÉCHEC par test, code de retour = échecs.</summary>
    public static class Program
    {
        private static int _echecs;
        private static int _total;

        public static int Main(string[] args)
        {
            Tester("tea aller-retour complet", TeaAllerRetour);
            Tester("tea versions antérieures", TeaVersions);
            Tester("tea carte WPF existante", TeaCarteExistante);
            Tester("segments : frappe au milieu garde les styles", SegmentsFrappe);
            Tester("segments : suppression et sélection", SegmentsSuppression);
            Tester("segments : format sur une plage", SegmentsFormat);
            Tester("segments : alignement de ligne", SegmentsAlignement);
            Tester("puces : bascule et cases", Puces);
            Tester("export markdown et import puces", Exports);
            Tester("gestes : interprétation et correspondance", Gestes);
            Tester("json canvas aller-retour", CanvasAllerRetour);
            Tester("flèches : A vers B, B vers A, les deux sens", Fleches);
            Tester("mise en forme : sélection uniforme et bornée, toutes propriétés", FormatSelectionUniforme);
            Tester("mise en forme : bascule sur une plage mêlée", FormatBasculeMelee);
            Tester("mise en forme : alignement sans déborder sur la ligne d'après", FormatAlignementBorne);
            Tester("mise en forme : forme en attente au curseur (gabarit)", FormatEnAttente);
            Tester("mise en forme : curseur après la frappe, frontière de style", FormatCaretApresFrappe);
            Tester("mise en forme : normalisation (fusion, vides, alignement)", FormatNormalisation);
            Tester("mise en forme : aller-retour .tea de tous les attributs", FormatPersistance);
            Console.WriteLine();
            Console.WriteLine(_total + " test(s), " + _echecs + " échec(s)");
            return _echecs;
        }

        private static void Tester(string nom, Action test)
        {
            _total++;
            try
            {
                test();
                Console.WriteLine("OK     " + nom);
            }
            catch (Exception ex)
            {
                _echecs++;
                Console.WriteLine("ÉCHEC  " + nom + " : " + ex.Message);
            }
        }

        private static void Verifier(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static DocumentCarte CarteComplete()
        {
            DocumentCarte d = new DocumentCarte();
            d.NomAffichage = "Carte d'essai";
            d.Auteur = "Rémi";
            d.FondCouleur = "#16202E";
            d.FondMotif = "grille";
            d.PaddingBoites = 11;
            d.PoliceParDefaut = "Georgia";
            d.TailleParDefaut = 15;
            d.CouleurParDefaut = "#222222";
            d.AjouterCouleurPersonnalisee("#ABCDEF");
            d.Etiquettes.Add(new Etiquette("Idée", "#C0392B"));
            d.LienTraceDefaut = "courbe";
            Noeud a = new Noeud();
            a.X = 10; a.Y = 20;
            Segment s1 = new Segment { Texte = "Bonjour ", Gras = true };
            Segment s2 = new Segment { Texte = "monde\nligne 2", Italique = true, Couleur = "#C0392B", Surlignage = "#FFF3A0", AlignementLigne = "centre" };
            a.Segments.Add(s1); a.Segments.Add(s2);
            a.Alignement = "droite";
            a.LargeurFixe = 200;
            a.Note = "une note";
            a.Etiquettes.Add("Idée");
            a.Fichiers.Add(PieceJointe.DepuisUrl("https://example.org/page", "Exemple"));
            PieceJointe integre = new PieceJointe { Nom = "doc", NomFichier = "doc.txt", Chemin = @"C:\nulle\part\doc.txt", Donnees = new byte[] { 1, 2, 3 } };
            a.Fichiers.Add(integre);
            a.FondCouleur = "#FFF3A0"; a.BordureTrait = "tirets"; a.Ombre = "marquee"; a.Opacite = 0.7; a.Rayon = 12;
            Noeud b = new Noeud();
            b.X = 300; b.Y = 40;
            b.DefinirTexteSimple("Image");
            b.ImageDonnees = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            b.ImageExtension = ".png";
            b.ImageLargeur = 120; b.ImageHauteur = 80;
            b.SegmentsApres.Add(new Segment { Texte = "après" });
            d.Noeuds.Add(a); d.Noeuds.Add(b);
            Lien l = new Lien { SourceId = a.Id, CibleId = b.Id, Fleche = "simple", Trace = "courbe", Courbure = 33, Etiquette = "Lien" };
            d.Liens.Add(l);
            Groupe g = new Groupe { Titre = "Groupe", TitrePosition = "bas-gauche", Plie = false, OrdrePresentation = 2 };
            g.NoeudIds.Add(a.Id); g.NoeudIds.Add(b.Id);
            d.Groupes.Add(g);
            return d;
        }

        private static void TeaAllerRetour()
        {
            DocumentCarte d = CarteComplete();
            byte[] octets = Fichier.EnregistrerEnMemoire(d, null);
            DocumentCarte r = Fichier.Charger(octets);
            Verifier(r.NomAffichage == "Carte d'essai" && r.Auteur == "Rémi", "nom/auteur");
            Verifier(r.FondCouleur == "#16202E" && r.FondMotif == "grille" && r.PaddingBoites == 11, "préférences");
            Verifier(r.PoliceParDefaut == "Georgia" && r.TailleParDefaut == 15 && r.CouleurParDefaut == "#222222", "texte par défaut");
            Verifier(r.CouleursPersonnalisees.Count == 1 && r.CouleursPersonnalisees[0] == "#ABCDEF", "couleurs perso");
            Verifier(r.Etiquettes.Count == 1 && r.Etiquettes[0].Couleur == "#C0392B", "étiquettes");
            Verifier(r.LienTraceDefaut == "courbe", "défaut lien");
            Verifier(r.Noeuds.Count == 2 && r.Liens.Count == 1 && r.Groupes.Count == 1, "comptes");
            Noeud a = r.Noeuds[0];
            Verifier(a.Segments.Count == 2 && a.Segments[0].Gras && a.Segments[1].Italique && a.Segments[1].Surlignage == "#FFF3A0", "segments");
            Verifier(a.Segments[1].AlignementLigne == "centre" && a.Alignement == "droite", "alignements");
            Verifier(a.LargeurFixe == 200 && a.Note == "une note" && a.Etiquettes[0] == "Idée", "note/étiquettes");
            Verifier(a.Fichiers.Count == 2 && a.Fichiers[0].EstUrl && a.Fichiers[1].EstIntegre && a.Fichiers[1].Donnees.Length == 3, "fichiers");
            Verifier(a.FondCouleur == "#FFF3A0" && a.BordureTrait == "tirets" && a.Ombre == "marquee" && Math.Abs(a.Opacite - 0.7) < 0.001 && a.Rayon == 12, "style");
            Noeud b = r.Noeuds[1];
            Verifier(b.EstImage && b.ImageDonnees.Length == 4 && b.ImageExtension == ".png" && b.ImageLargeur == 120, "image");
            Verifier(b.SegmentsApres.Count == 1 && b.SegmentsApres[0].Texte == "après", "texte après");
            Lien l = r.Liens[0];
            Verifier(l.Fleche == "simple" && l.Trace == "courbe" && l.Courbure == 33 && l.Etiquette == "Lien", "lien");
            Groupe g = r.Groupes[0];
            Verifier(g.Titre == "Groupe" && g.TitrePosition == "bas-gauche" && g.OrdrePresentation == 2 && g.NoeudIds.Count == 2, "groupe");
            Verifier(!string.IsNullOrEmpty(r.DateCreation) && !string.IsNullOrEmpty(r.DateModification), "dates");
        }

        private static void TeaVersions()
        {
            DocumentCarte d = new DocumentCarte();
            d.DateModification = "2026-10-26 10:00";
            byte[] v1 = Fichier.EnregistrerEnMemoire(d, null);
            Verifier(Fichier.ListerVersions(v1).Count == 0, "première écriture sans version");
            Noeud n = new Noeud(); n.DefinirTexteSimple("plus tard"); d.Noeuds.Add(n);
            byte[] v2 = Fichier.EnregistrerEnMemoire(d, v1);
            List<string> versions = Fichier.ListerVersions(v2);
            Verifier(versions.Count == 1, "une version conservée (" + versions.Count + ")");
            DocumentCarte ancienne = Fichier.ChargerVersion(v2, versions[0]);
            Verifier(ancienne.Noeuds.Count == 0, "la version antérieure est l'état précédent");
            Verifier(Fichier.Charger(v2).Noeuds.Count == 1, "l'état courant");
        }

        private static void TeaCarteExistante()
        {
            string chemin = @"C:\Users\remie\Documents\dzqdqz.tea";
            if (!File.Exists(chemin)) { Console.Write("(absente) "); return; }
            DocumentCarte d = Fichier.Charger(chemin);
            Verifier(d.Noeuds.Count > 0, "des boîtes");
            byte[] octets = Fichier.EnregistrerEnMemoire(d, File.ReadAllBytes(chemin));
            DocumentCarte r = Fichier.Charger(octets);
            Verifier(r.Noeuds.Count == d.Noeuds.Count && r.Liens.Count == d.Liens.Count, "réécrite à l'identique");
        }

        private static void SegmentsFrappe()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "abc", Gras = true });
            source.Add(new Segment { Texte = "def", Italique = true });
            EditeurSegments e = new EditeurSegments(source);
            // On tape « X » entre b et c (caret après X = 3).
            e.Synchroniser("abXcdef", 3);
            Verifier(e.Texte == "abXcdef", "texte");
            Verifier(e.Segments.Count == 2 && e.Segments[0].Texte == "abXc" && e.Segments[0].Gras, "l'insertion hérite du gras : " + e.Segments[0].Texte);
            Verifier(e.Segments[1].Texte == "def" && e.Segments[1].Italique, "la suite garde l'italique");
            // On tape à la fin.
            e.Synchroniser("abXcdefg", 8);
            Verifier(e.Segments[1].Texte == "defg" && e.Segments[1].Italique, "fin de texte");
            // Au tout début.
            e.Synchroniser("ZabXcdefg", 1);
            Verifier(e.Segments[0].Texte == "ZabXc" && e.Segments[0].Gras, "début de texte : " + e.Segments[0].Texte);
        }

        private static void SegmentsSuppression()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "abc", Gras = true });
            source.Add(new Segment { Texte = "def", Italique = true });
            EditeurSegments e = new EditeurSegments(source);
            e.Synchroniser("abef", 2); // « cd » effacés (sélection remplacée par rien)
            Verifier(e.Texte == "abef", "texte");
            Verifier(e.Segments.Count == 2 && e.Segments[0].Texte == "ab" && e.Segments[1].Texte == "ef", "segments");
            e.Synchroniser("abXYef", 4); // sélection remplacée par XY
            Verifier(e.Segments[0].Texte == "abXY" && e.Segments[0].Gras, "remplacement hérite du gras");
            e.Synchroniser("", 0);
            Verifier(e.Texte == "" && e.Segments.Count == 0, "tout effacé");
            e.Synchroniser("n", 1);
            Verifier(e.Texte == "n", "retaper après vide");
        }

        private static void SegmentsFormat()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "bonjour monde" });
            EditeurSegments e = new EditeurSegments(source);
            e.AppliquerFormat("gras", null, 0, 7);
            Verifier(e.Segments.Count == 2 && e.Segments[0].Texte == "bonjour" && e.Segments[0].Gras && !e.Segments[1].Gras, "gras sur « bonjour »");
            e.AppliquerFormat("gras", null, 0, 7); // bascule
            Verifier(e.Segments.Count == 1 && !e.Segments[0].Gras, "bascule → fusion");
            e.AppliquerFormat("couleur", "#FF0000", 8, 13);
            Verifier(e.Segments[1].Couleur == "#FF0000" && e.Segments[1].Texte == "monde", "couleur");
            FormatCourant etat = e.Lire(10, 10, 10, "gauche");
            Verifier(etat.Couleur == "#FF0000", "lecture au curseur");
        }

        private static void SegmentsAlignement()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "un\ndeux\ntrois" });
            EditeurSegments e = new EditeurSegments(source);
            e.AppliquerFormat("alignement", "centre", 4, 4); // curseur dans « deux »
            string texte = e.Texte;
            Verifier(texte == "un\ndeux\ntrois", "le texte ne change pas");
            FormatCourant etat = e.Lire(5, 5, 5, "gauche");
            Verifier(etat.Alignement == "centre", "la ligne du milieu est centrée (" + etat.Alignement + ")");
            FormatCourant premiere = e.Lire(0, 0, 0, "gauche");
            Verifier(premiere.Alignement == "gauche", "la première reste à gauche (" + premiere.Alignement + ")");
        }

        private static void Puces()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "a\nb" });
            List<Segment> puces = PucesTexte.Basculer(source, "puce");
            Verifier(TexteRiche_TextePlat(puces) == "• a\n• b", "puces posées");
            List<Segment> retirees = PucesTexte.Basculer(puces, "puce");
            Verifier(TexteRiche_TextePlat(retirees) == "a\nb", "puces retirées");
            List<Segment> cases = PucesTexte.Basculer(source, "case");
            List<Segment> cochee = PucesTexte.BasculerCase(cases, 1);
            int c = 0, t = 0;
            PucesTexte.CompterCases(cochee, ref c, ref t);
            Verifier(c == 1 && t == 2, "une case cochée sur deux");
        }

        private static string TexteRiche_TextePlat(List<Segment> segments)
        {
            string s = "";
            foreach (Segment seg in segments) s += seg.Texte;
            return s;
        }

        private static void Exports()
        {
            List<Noeud> noeuds; List<Lien> liens;
            Export.ImporterTexteAPuces("- Racine\n  - Enfant 1\n  - Enfant 2\n    - Petit-enfant", new Avalonia.Point(0, 0), out noeuds, out liens);
            Verifier(noeuds.Count == 4 && liens.Count == 3, "import à puces : 4 boîtes, 3 liens");
            DocumentCarte d = new DocumentCarte();
            foreach (Noeud n in noeuds) d.Noeuds.Add(n);
            foreach (Lien l in liens) d.Liens.Add(l);
            string md = Export.Markdown(d, "Essai");
            Verifier(md.Contains("# Essai") && md.Contains("- Racine") && md.Contains("    - Petit-enfant"), "markdown : " + md.Replace("\n", "|"));
            Verifier(Export.RessembleAUnPlan("- a\n- b") && !Export.RessembleAUnPlan("bonjour"), "détection de plan");
        }

        private static void Gestes()
        {
            Avalonia.Input.Key touche; Avalonia.Input.KeyModifiers mods;
            Verifier(ParametresGlobaux.InterpreterGeste("Ctrl+Shift+D0", out touche, out mods)
                && touche == Avalonia.Input.Key.D0 && mods == (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift), "Ctrl+Shift+D0");
            Verifier(ParametresGlobaux.AfficherGeste("Ctrl+Shift+D0") == "Ctrl+Maj+0", "affichage français");
            Verifier(ParametresGlobaux.ActionPour(Avalonia.Input.Key.B, Avalonia.Input.KeyModifiers.Control) == "gras", "Ctrl+B = gras");
            Verifier(ParametresGlobaux.ActionPour(Avalonia.Input.Key.OemPlus, Avalonia.Input.KeyModifiers.Control) == "zoom-avant", "Ctrl+= vaut Ctrl++ du pavé");
            Verifier(ParametresGlobaux.ActionPour(Avalonia.Input.Key.Delete, Avalonia.Input.KeyModifiers.None) == "supprimer", "Suppr");
            Verifier(ParametresGlobaux.ComposerGeste(Avalonia.Input.Key.G, Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift) == "Ctrl+Shift+G", "composition");
        }

        private static void CanvasAllerRetour()
        {
            string dossier = Path.Combine(Path.GetTempPath(), "mentalo-tests");
            Directory.CreateDirectory(dossier);
            string chemin = Path.Combine(dossier, "essai.canvas");
            File.WriteAllText(chemin, "{\"nodes\":[{\"id\":\"a\",\"type\":\"text\",\"text\":\"Alpha\",\"x\":0,\"y\":0,\"width\":120,\"height\":40},"
                + "{\"id\":\"b\",\"type\":\"text\",\"text\":\"Beta\",\"x\":200,\"y\":0,\"width\":120,\"height\":40,\"color\":\"1\"},"
                + "{\"id\":\"g\",\"type\":\"group\",\"label\":\"G\",\"x\":-10,\"y\":-10,\"width\":140,\"height\":60}],"
                + "\"edges\":[{\"id\":\"e\",\"fromNode\":\"a\",\"toNode\":\"b\",\"toEnd\":\"arrow\",\"label\":\"vers\"}]}");
            List<Noeud> noeuds; List<Lien> liens; List<Groupe> groupes;
            JsonCanvas.Importer(chemin, out noeuds, out liens, out groupes);
            Verifier(noeuds.Count == 2 && liens.Count == 1 && groupes.Count == 1, "import : comptes");
            Verifier(noeuds[1].FondCouleur == "#C0392B", "couleur préréglée 1");
            Verifier(liens[0].Fleche == "simple" && liens[0].Etiquette == "vers", "arête");
            Verifier(groupes[0].NoeudIds.Count == 1 && groupes[0].NoeudIds[0] == noeuds[0].Id, "appartenance par géométrie");
        }

        // ------------------------------------------------------------------ mise en forme (1/11/2026)

        private static bool Porte(Segment s, string propriete, object valeur)
        {
            switch (propriete)
            {
                case "police": return s.Police == (string)valeur;
                case "taille": return Math.Abs(s.Taille - (double)valeur) < 0.01;
                case "couleur": return s.Couleur == (string)valeur;
                case "surlignage": return (s.Surlignage ?? "") == (((string)valeur) ?? "");
                case "gras": return s.Gras == (bool)valeur;
                case "italique": return s.Italique == (bool)valeur;
                case "souligne": return s.Souligne == (bool)valeur;
                case "barre": return s.Barre == (bool)valeur;
                case "alignement": return (s.AlignementLigne ?? "") == (((string)valeur) ?? "");
            }
            return false;
        }

        /// <summary>Tous les segments qui recouvrent [debut, fin[ portent (ou non) la valeur.</summary>
        private static bool Plage(List<Segment> segments, int debut, int fin, string propriete, object valeur, bool attendu)
        {
            int curseur = 0;
            bool vu = false;
            foreach (Segment s in segments)
            {
                int finSegment = curseur + s.Texte.Length;
                if (finSegment > debut && curseur < fin)
                {
                    vu = true;
                    if (Porte(s, propriete, valeur) != attendu) return false;
                }
                curseur = finSegment;
            }
            return vu;
        }

        private static void FormatSelectionUniforme()
        {
            object[,] balayage = new object[,]
            {
                { "police", "Georgia" }, { "taille", 24.0 }, { "couleur", "#C0392B" }, { "surlignage", "#FFF3A0" },
                { "gras", true }, { "italique", true }, { "souligne", true }, { "barre", true }
            };
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "Bonjour monde entier\nSeconde ligne" });
            EditeurSegments e = new EditeurSegments(source);
            for (int i = 0; i < balayage.GetLength(0); i++)
            {
                string p = (string)balayage[i, 0];
                object v = balayage[i, 1];
                e.AppliquerFormat(p, v, 0, 7);
                Verifier(Plage(e.Segments, 0, 7, p, v, true), p + " : « Bonjour » le porte");
                Verifier(Plage(e.Segments, 7, 34, p, v, false), p + " : le reste ne le porte pas");
                Verifier(e.Texte == "Bonjour monde entier\nSeconde ligne", p + " : le texte ne change pas");
            }
            Verifier(e.Segments.Count == 2, "deux segments, pas de miettes (" + e.Segments.Count + ")");
            // Une plage à cheval sur la frontière : coupe nette, uniforme.
            e.AppliquerFormat("couleur", "#1E8449", 4, 10);
            Verifier(Plage(e.Segments, 4, 10, "couleur", "#1E8449", true) && Plage(e.Segments, 0, 4, "couleur", "#C0392B", true)
                && Plage(e.Segments, 10, 34, "couleur", Segment.CouleurDefaut, true), "couleur à cheval : trois zones nettes");
            // Le surlignage « Aucun » (null) retire sans basculer quoi que ce soit.
            e.AppliquerFormat("surlignage", null, 0, 34);
            Verifier(Plage(e.Segments, 0, 34, "surlignage", null, true), "surlignage null = retiré partout");
            Verifier(Plage(e.Segments, 0, 7, "gras", true, true), "…sans toucher au gras");
            // La lecture pour la barre : à l'intérieur de la sélection.
            FormatCourant etat = e.Lire(7, 0, 7, "gauche");
            Verifier(etat.Gras && etat.Italique && etat.Souligne && etat.Barre && etat.Police == "Georgia" && Math.Abs(etat.Taille - 24) < 0.01, "l'état lu en fin de sélection est celui de la sélection");
        }

        private static void FormatBasculeMelee()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "abc", Gras = true });
            source.Add(new Segment { Texte = "def" });
            EditeurSegments e = new EditeurSegments(source);
            e.AppliquerFormat("gras", null, 0, 6); // mêlée : un seul maigre → tout en gras
            Verifier(e.Segments.Count == 1 && e.Segments[0].Gras, "plage mêlée → tout en gras, un seul segment");
            e.AppliquerFormat("gras", null, 0, 6); // tout en gras → tout en maigre
            Verifier(e.Segments.Count == 1 && !e.Segments[0].Gras, "tout gras → tout maigre");
            e.AppliquerFormat("italique", null, 2, 4);
            Verifier(e.Segments.Count == 3 && e.Segments[1].Texte == "cd" && e.Segments[1].Italique, "italique sur « cd » : coupé aux bornes");
            e.AppliquerFormat("barre", true, 2, 4);
            e.AppliquerFormat("souligne", null, 2, 4);
            Verifier(e.Segments[1].Barre && e.Segments[1].Souligne && !e.Segments[0].Barre && !e.Segments[2].Souligne, "barré et souligné restent bornés");
            // Valeur explicite sur une plage mêlée : elle s'impose telle quelle.
            e.AppliquerFormat("italique", false, 0, 6);
            Verifier(Plage(e.Segments, 0, 6, "italique", false, true), "italique = faux imposé partout");
            // Une plage vide ne fait rien ; des bornes inversées sont remises dans l'ordre.
            string avant = e.Texte;
            e.AppliquerFormat("gras", null, 3, 3);
            Verifier(e.Texte == avant && Plage(e.Segments, 0, 6, "gras", false, true), "plage vide : rien");
            e.AppliquerFormat("gras", null, 6, 0);
            Verifier(Plage(e.Segments, 0, 6, "gras", true, true), "bornes inversées : toute la plage");
        }

        private static void FormatAlignementBorne()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "un\ndeux\ntrois" });
            EditeurSegments e = new EditeurSegments(source);
            e.AppliquerFormat("alignement", "centre", 4, 4); // curseur dans « deux »
            Verifier(e.Texte == "un\ndeux\ntrois", "le texte ne change pas");
            Verifier(e.Lire(5, 5, 5, "gauche").Alignement == "centre", "« deux » est centrée");
            Verifier(e.Lire(0, 0, 0, "gauche").Alignement == "gauche", "« un » reste à gauche");
            Verifier(e.Lire(10, 10, 10, "gauche").Alignement == "gauche", "« trois » reste à gauche (l'alignement ne déborde pas sur la ligne d'après : " + e.Lire(10, 10, 10, "gauche").Alignement + ")");
            Verifier(Plage(e.Segments, 3, 7, "alignement", "centre", true) && Plage(e.Segments, 7, 13, "alignement", "centre", false) && Plage(e.Segments, 0, 3, "alignement", "centre", false),
                "seuls les segments de « deux » portent l'alignement");
            // Une sélection qui couvre deux lignes : les deux, pas la troisième.
            e.AppliquerFormat("alignement", "droite", 1, 5);
            Verifier(e.Lire(0, 0, 0, "gauche").Alignement == "droite" && e.Lire(5, 5, 5, "gauche").Alignement == "droite" && e.Lire(10, 10, 10, "gauche").Alignement == "gauche",
                "sélection sur deux lignes : « un » et « deux » à droite, « trois » à gauche");
            // Les lignes rendues : PucesTexte.DecouperLignes garde l'alignement par ligne.
            List<List<Segment>> lignes = PucesTexte.DecouperLignes(e.Segments);
            Verifier(lignes.Count == 3 && lignes[0][0].AlignementLigne == "droite" && lignes[1][0].AlignementLigne == "droite" && string.IsNullOrEmpty(lignes[2][0].AlignementLigne),
                "les lignes découpées portent chacune leur alignement");
            // La dernière ligne (sans « \n » final) s'aligne aussi.
            e.AppliquerFormat("alignement", "centre", 12, 12);
            Verifier(e.Lire(10, 10, 10, "gauche").Alignement == "centre" && e.Lire(5, 5, 5, "gauche").Alignement == "droite", "la dernière ligne s'aligne seule");
        }

        private static void FormatEnAttente()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "Bonjour " });
            EditeurSegments e = new EditeurSegments(source);
            Segment gabarit = e.StyleA(8);
            Verifier(gabarit.Texte == "" && gabarit.Police == Segment.PoliceDefaut && !gabarit.Gras, "le gabarit au curseur est le style du caractère précédent, sans texte");
            EditeurSegments.AppliquerProprieteA(gabarit, "gras", null);
            EditeurSegments.AppliquerProprieteA(gabarit, "gras", null);
            EditeurSegments.AppliquerProprieteA(gabarit, "gras", null);
            EditeurSegments.AppliquerProprieteA(gabarit, "italique", true);
            EditeurSegments.AppliquerProprieteA(gabarit, "police", "Georgia");
            EditeurSegments.AppliquerProprieteA(gabarit, "taille", 20.0);
            EditeurSegments.AppliquerProprieteA(gabarit, "couleur", "#C0392B");
            EditeurSegments.AppliquerProprieteA(gabarit, "surlignage", "#FFF3A0");
            Verifier(gabarit.Gras && gabarit.Italique && gabarit.Police == "Georgia" && gabarit.Taille == 20 && gabarit.Couleur == "#C0392B" && gabarit.Surlignage == "#FFF3A0", "le gabarit cumule les choix (bascule null = inverser)");
            // La frappe qui suit (caret APRÈS la frappe = 12) : l'insertion est signalée, le gabarit s'y pose.
            e.Synchroniser("Bonjour neuf", 12);
            Verifier(e.DerniereInsertionDebut == 8 && e.DerniereInsertionLongueur == 4, "l'insertion « neuf » est signalée à 8, longueur 4 (" + e.DerniereInsertionDebut + ", " + e.DerniereInsertionLongueur + ")");
            e.AppliquerGabarit(gabarit, e.DerniereInsertionDebut, e.DerniereInsertionDebut + e.DerniereInsertionLongueur);
            Verifier(Plage(e.Segments, 8, 12, "gras", true, true) && Plage(e.Segments, 8, 12, "police", "Georgia", true) && Plage(e.Segments, 8, 12, "surlignage", "#FFF3A0", true), "« neuf » porte tout le gabarit");
            Verifier(Plage(e.Segments, 0, 8, "gras", true, false) && Plage(e.Segments, 0, 8, "police", Segment.PoliceDefaut, true), "« Bonjour » ne bouge pas");
            // La suite de la frappe hérite du caractère précédent, sans gabarit.
            e.Synchroniser("Bonjour neufs", 13);
            Verifier(Plage(e.Segments, 8, 13, "gras", true, true) && e.Segments.Count == 2, "la frappe continue dans la forme (" + e.Segments.Count + " segments)");
            // Sans insertion (effacement), rien n'est signalé.
            e.Synchroniser("Bonjour neuf", 12);
            Verifier(e.DerniereInsertionLongueur == 0, "un effacement n'insère rien");
            // Le gabarit ne touche pas l'alignement de ligne.
            List<Segment> aligne = new List<Segment>();
            aligne.Add(new Segment { Texte = "a\nb", AlignementLigne = "centre" });
            EditeurSegments e2 = new EditeurSegments(aligne);
            e2.AppliquerGabarit(gabarit, 2, 3);
            Verifier(e2.Segments[e2.Segments.Count - 1].AlignementLigne == "centre" && e2.Segments[e2.Segments.Count - 1].Gras, "le gabarit laisse l'alignement de ligne");
        }

        private static void FormatCaretApresFrappe()
        {
            // Insertion au curseur (sans sélection) : curseur + longueur insérée.
            Verifier(EditeurSegments.CaretApresFrappe("abcdef", "abcdxef", 4, 4, 4) == 5, "insertion simple");
            Verifier(EditeurSegments.CaretApresFrappe("abcdef", "abcdxyzef", 4, 4, 4) == 7, "collage de trois caractères");
            // Une sélection remplacée : début de la sélection + longueur insérée.
            Verifier(EditeurSegments.CaretApresFrappe("abcdef", "aXYef", 1, 1, 4) == 3, "sélection [1,4[ remplacée par XY");
            Verifier(EditeurSegments.CaretApresFrappe("abcdef", "aXYef", 4, 4, 1) == 3, "sélection inversée [4,1]");
            Verifier(EditeurSegments.CaretApresFrappe("abcdef", "aef", 4, 1, 4) == 1, "sélection effacée");
            // Un effacement sans sélection (Retour arrière, Suppr) : le curseur fourni est rendu tel quel.
            Verifier(EditeurSegments.CaretApresFrappe("abcdef", "abcef", 4, 4, 4) == 4, "effacement : curseur rendu tel quel");
            // Un changement qui ne s'accorde pas (annulation, texte posé) : le curseur fourni est rendu tel quel.
            Verifier(EditeurSegments.CaretApresFrappe("abcdef", "zzzzzzz", 4, 4, 4) == 4, "texte remplacé en bloc : curseur rendu tel quel");
            Verifier(EditeurSegments.CaretApresFrappe("", "a", 0, 0, 0) == 1 && EditeurSegments.CaretApresFrappe(null, "a", 0, 0, 0) == 1, "premier caractère d'une boîte vide");

            // Au cœur du bogue (1/11) : « abc » gras puis « def » italique, taper « x » juste après « d ».
            // Avec le curseur d'AVANT la frappe (4), l'éditeur croyait l'insertion à 3 et rendait « d » gras.
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "abc", Gras = true });
            source.Add(new Segment { Texte = "def", Italique = true });
            EditeurSegments e = new EditeurSegments(source);
            int caret = EditeurSegments.CaretApresFrappe(e.Texte, "abcdxef", 4, 4, 4);
            e.Synchroniser("abcdxef", caret);
            Verifier(e.Segments.Count == 2 && e.Segments[0].Texte == "abc" && e.Segments[1].Texte == "dxef" && e.Segments[1].Italique && !e.Segments[1].Gras,
                "« x » hérite de « d » (italique), « d » reste italique : " + e.Segments[0].Texte + " | " + e.Segments[1].Texte);
            // Et juste après « c » : « x » gras.
            e.Synchroniser("abcxdxef", EditeurSegments.CaretApresFrappe(e.Texte, "abcxdxef", 3, 3, 3));
            Verifier(e.Segments[0].Texte == "abcx" && e.Segments[0].Gras && e.Segments[1].Texte == "dxef", "« x » après « c » hérite du gras : " + e.Segments[0].Texte);
            // Une sélection à cheval remplacée : l'insertion prend le style du caractère devant la sélection.
            e.Synchroniser("abZZef", EditeurSegments.CaretApresFrappe(e.Texte, "abZZef", 6, 2, 6));
            Verifier(e.Texte == "abZZef" && e.Segments[0].Texte == "abZZ" && e.Segments[0].Gras && e.Segments[1].Texte == "ef" && e.Segments[1].Italique, "remplacement d'une sélection à cheval : " + e.Segments[0].Texte + " | " + e.Segments[1].Texte);
        }

        private static void FormatNormalisation()
        {
            List<Segment> source = new List<Segment>();
            source.Add(new Segment { Texte = "ab", Gras = true });
            source.Add(new Segment { Texte = "", Italique = true });
            source.Add(new Segment { Texte = "cd", Gras = true });
            source.Add(new Segment { Texte = "ef", Gras = true, AlignementLigne = "centre" });
            source.Add(new Segment { Texte = "gh", Gras = true, AlignementLigne = "centre", Surlignage = "#FFF3A0" });
            source.Add(new Segment { Texte = "ij", Gras = true, AlignementLigne = "centre", Surlignage = "#FFF3A0" });
            List<Segment> n = Segments.Normaliser(source);
            Verifier(n.Count == 3 && n[0].Texte == "abcd" && n[1].Texte == "ef" && n[2].Texte == "ghij", "fusion des voisins de même style, vides retirés, alignement et surlignage séparent (" + n.Count + ")");
            Verifier(n[0].Gras && n[1].AlignementLigne == "centre" && n[2].Surlignage == "#FFF3A0", "les attributs sont conservés");
            Verifier(source[0].Texte == "ab", "la source n'est pas modifiée");
            Segment a = new Segment { Texte = "x" };
            Segment b = new Segment { Texte = "y", AlignementLigne = "" };
            Verifier(a.MemeStyleQue(b), "alignement null et vide sont le même style");
            Verifier(Segments.Normaliser(new List<Segment>()).Count == 0, "rien → rien");
        }

        private static void FormatPersistance()
        {
            DocumentCarte d = new DocumentCarte();
            Noeud n = new Noeud();
            n.Segments.Add(new Segment { Texte = "Bonjour", Police = "Georgia", Taille = 24, Couleur = "#C0392B", Surlignage = "#FFF3A0", Gras = true, Italique = true, Souligne = true, Barre = true, AlignementLigne = "centre" });
            n.Segments.Add(new Segment { Texte = " monde\nSeconde", AlignementLigne = "droite" });
            n.Alignement = "droite";
            n.SegmentsApres.Add(new Segment { Texte = "après", Souligne = true, Barre = true, Taille = 10 });
            n.ImageDonnees = new byte[] { 1, 2, 3 }; n.ImageExtension = ".png"; n.ImageLargeur = 10; n.ImageHauteur = 10;
            d.Noeuds.Add(n);
            Noeud r = Fichier.Charger(Fichier.EnregistrerEnMemoire(d, null)).TrouverNoeud(n.Id);
            Verifier(r != null && Noeud.SegmentsEgaux(n.Segments, r.Segments) && Noeud.SegmentsEgaux(n.SegmentsApres, r.SegmentsApres) && r.Alignement == "droite",
                "tous les attributs (police, taille, couleur, surlignage, gras, italique, souligné, barré, alignement de ligne) reviennent à l'identique, avant et après l'image");
        }

        /// <summary>« inverse » (27/10/2026) : la pointe sur la boîte de départ — le
        /// parent est alors la cible. Les aides du modèle, l'import JSON Canvas
        /// (une flèche au départ seul ne retourne plus le lien) et l'arbre des
        /// exports le respectent.</summary>
        private static void Fleches()
        {
            Lien simple = new Lien { SourceId = "a", CibleId = "b", Fleche = "simple" };
            Verifier(simple.FlecheVersCible && !simple.FlecheVersSource && simple.EstOriente && simple.IdAmont == "a" && simple.IdAval == "b", "simple = A vers B");
            Lien inverse = new Lien { SourceId = "a", CibleId = "b", Fleche = "inverse" };
            Verifier(!inverse.FlecheVersCible && inverse.FlecheVersSource && inverse.EstOriente && inverse.IdAmont == "b" && inverse.IdAval == "a", "inverse = B vers A");
            Lien dbl = new Lien { SourceId = "a", CibleId = "b", Fleche = "double" };
            Verifier(dbl.FlecheVersCible && dbl.FlecheVersSource && !dbl.EstOriente && dbl.IdAmont == null, "double : les deux pointes, pas de parent");
            Lien aucune = new Lien { SourceId = "a", CibleId = "b" };
            Verifier(!aucune.FlecheVersCible && !aucune.FlecheVersSource && !aucune.EstOriente, "aucune");

            string dossier = Path.Combine(Path.GetTempPath(), "mentalo-tests");
            Directory.CreateDirectory(dossier);
            string chemin = Path.Combine(dossier, "inverse.canvas");
            File.WriteAllText(chemin, "{\"nodes\":[{\"id\":\"a\",\"type\":\"text\",\"text\":\"Alpha\",\"x\":0,\"y\":0,\"width\":120,\"height\":40},"
                + "{\"id\":\"b\",\"type\":\"text\",\"text\":\"Beta\",\"x\":200,\"y\":0,\"width\":120,\"height\":40}],"
                + "\"edges\":[{\"id\":\"e\",\"fromNode\":\"a\",\"toNode\":\"b\",\"fromEnd\":\"arrow\",\"toEnd\":\"none\"}]}");
            List<Noeud> noeuds; List<Lien> liens; List<Groupe> groupes;
            JsonCanvas.Importer(chemin, out noeuds, out liens, out groupes);
            Verifier(liens.Count == 1 && liens[0].Fleche == "inverse" && liens[0].SourceId == noeuds[0].Id && liens[0].CibleId == noeuds[1].Id,
                "import canvas : une pointe au départ seul = B vers A, lien non retourné");

            // L'arbre des exports descend dans le sens de la pointe : Beta (cible) est la racine, Alpha son enfant.
            DocumentCarte document = new DocumentCarte();
            Noeud alpha = new Noeud(); alpha.DefinirTexteSimple("Alpha"); alpha.X = 0; alpha.Y = 100;
            Noeud beta = new Noeud(); beta.DefinirTexteSimple("Beta"); beta.X = 300; beta.Y = 100;
            document.Noeuds.Add(alpha); document.Noeuds.Add(beta);
            Lien lien = document.NouveauLien();
            lien.SourceId = alpha.Id; lien.CibleId = beta.Id; lien.Fleche = "inverse";
            document.Liens.Add(lien);
            string md = Path.Combine(dossier, "inverse.md");
            Export.ExporterMarkdown(document, "inverse", md);
            string texte = File.ReadAllText(md);
            Verifier(texte.IndexOf("Beta") >= 0 && texte.IndexOf("Alpha") > texte.IndexOf("Beta"), "export markdown : la racine est la boîte pointée (Beta), Alpha dessous");
        }
    }
}
