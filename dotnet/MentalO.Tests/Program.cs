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
    }
}
