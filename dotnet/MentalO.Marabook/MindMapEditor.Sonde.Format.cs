using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Mentalo.Modele;
using Mentalo.Persistance;
using Mentalo.Vue;

namespace MentalO.Marabook
{
    /// <summary>LA BATTERIE DE MISE EN FORME DE LA SONDE (1/11/2026, Rémi : « gras
    /// non appliqué, changement de police pas uniforme »). Sur une boîte d'essai au
    /// texte connu, chaque propriété du texte (police, taille, couleur, surlignage,
    /// gras, italique, souligné, barré, alignement) est vérifiée dans les trois
    /// situations — (a) boîte sélectionnée sans édition : toute la boîte ; (b)
    /// édition avec une sélection de texte : la sélection seule ; (c) édition sans
    /// sélection : le mot sous le curseur, ou la frappe qui suit quand il n'y a
    /// pas de mot — par l'API de la toile (les raccourcis) et par de VRAIS clics
    /// sur le rail de Marabook (bascule Gras, listes Police / Taille / Couleur /
    /// Surlignage), puis la persistance (validation, aller-retour .tea), la boîte
    /// à image (texte après l'image) et l'annulation.</summary>
    public partial class MindMapEditor
    {
        private const string TexteEssai = "Bonjour monde entier\nSeconde ligne"; // 20 + 1 + 13 = 34 caractères

        private static bool Porte(Segment s, string propriete, object valeur)
        {
            switch (propriete)
            {
                case "police": return s.Police == (string)valeur;
                case "taille": return Math.Abs(s.Taille - (double)valeur) < 0.01;
                case "couleur": return string.Equals(s.Couleur, (string)valeur, StringComparison.OrdinalIgnoreCase);
                case "surlignage": return string.Equals(s.Surlignage ?? "", ((string)valeur) ?? "", StringComparison.OrdinalIgnoreCase);
                case "gras": return s.Gras == (bool)valeur;
                case "italique": return s.Italique == (bool)valeur;
                case "souligne": return s.Souligne == (bool)valeur;
                case "barre": return s.Barre == (bool)valeur;
                case "alignement": return (s.AlignementLigne ?? "") == (((string)valeur) ?? "");
            }
            return false;
        }

        /// <summary>Vrai si TOUS les segments qui recouvrent [debut, fin[ portent la
        /// valeur (attendu = vrai) ou ne la portent pas (attendu = faux) — et qu'il
        /// y en a au moins un.</summary>
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

        private static bool Tous(List<Segment> segments, string propriete, object valeur)
        {
            return Plage(segments, 0, int.MaxValue, propriete, valeur, true);
        }

        private static string Decrire(List<Segment> segments)
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            foreach (Segment s in segments)
            {
                if (b.Length > 0) b.Append(" | ");
                b.Append("« ").Append(Echapper(s.Texte)).Append(" »");
                if (s.Gras) b.Append(" G");
                if (s.Italique) b.Append(" I");
                if (s.Souligne) b.Append(" S");
                if (s.Barre) b.Append(" B");
                b.Append(" ").Append(s.Police).Append(" ").Append(s.Taille).Append(" ").Append(s.Couleur);
                if (!string.IsNullOrEmpty(s.Surlignage)) b.Append(" surl=").Append(s.Surlignage);
                if (!string.IsNullOrEmpty(s.AlignementLigne)) b.Append(" al=").Append(s.AlignementLigne);
            }
            return b.ToString();
        }

        private static int Debut(TextBox editeur) { return Math.Min(editeur.SelectionStart, editeur.SelectionEnd); }
        private static int Fin(TextBox editeur) { return Math.Max(editeur.SelectionStart, editeur.SelectionEnd); }

        private static void Selectionner(TextBox editeur, int debut, int fin)
        {
            editeur.SelectionStart = debut;
            editeur.SelectionEnd = fin;
        }

        /// <summary>Le contrôle du rail dont l'infobulle commence ainsi (le premier visible).</summary>
        private T TrouverDansRail<T>(string debutInfobulle) where T : Control
        {
            foreach (Visual visuel in _inspecteur.GetVisualDescendants())
            {
                T controle = visuel as T;
                if (controle == null || !controle.IsEffectivelyVisible) continue;
                string tip = ToolTip.GetTip(controle) as string;
                if (tip != null && tip.StartsWith(debutInfobulle)) return controle;
            }
            return null;
        }

        /// <summary>Le contrôle est-il atteignable par un vrai clic en son centre
        /// (visible, dans la fenêtre, et le test de collision tombe dessus) ?</summary>
        private bool Cliquable(Control controle)
        {
            if (controle == null || !controle.IsEffectivelyVisible || controle.Bounds.Width <= 0) return false;
            Point? p = controle.TranslatePoint(new Point(controle.Bounds.Width / 2, controle.Bounds.Height / 2), _host.MainWindow);
            if (p == null) return false;
            if (p.Value.X < 0 || p.Value.Y < 0 || p.Value.X > _host.MainWindow.Bounds.Width || p.Value.Y > _host.MainWindow.Bounds.Height) return false;
            Visual touche = _host.MainWindow.InputHitTest(p.Value) as Visual;
            while (touche != null)
            {
                if (ReferenceEquals(touche, controle)) return true;
                touche = touche.GetVisualParent();
            }
            return false;
        }

        /// <summary>Le nom de police d'une ligne de liste : le Tag d'un ComboBoxItem
        /// (liste de la toile), ou le nom d'une entrée du catalogue de Marabook
        /// (autocomplète, 1/11) hors traits ; null sinon.</summary>
        private static string NomPoliceDe(ComboBox combo, int index)
        {
            if (index < 0 || index >= combo.Items.Count) return null;
            object objet = combo.Items[index];
            ComboBoxItem element = objet as ComboBoxItem;
            if (element != null) return element.Tag as string;
            global::Marabook.App.FontCatalog.Entry entree = objet as global::Marabook.App.FontCatalog.Entry;
            return entree != null && !entree.IsSeparator && !string.IsNullOrEmpty(entree.Name) ? entree.Name : null;
        }

        private int IndexParTag(ComboBox combo, Func<object, bool> critere)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                ComboBoxItem element = combo.Items[i] as ComboBoxItem;
                if (element != null && critere(element.Tag)) return i;
            }
            return -1;
        }

        /// <summary>Ouvre une liste du rail d'un vrai clic, puis clique le choix
        /// demandé (dans la liste déroulée) ; à défaut d'un contrôle atteignable,
        /// le fait par programme. Renvoie le chemin suivi, pour le journal.</summary>
        private async Task<string> ChoisirDansCombo(ComboBox combo, int index, Action<bool, string> verifier, string libelle)
        {
            string chemin;
            if (Cliquable(combo)) { ClicReel(combo); chemin = "clic réel sur la liste"; }
            else { combo.IsDropDownOpen = true; chemin = "liste ouverte par programme (hors de portée du clic)"; }
            await Task.Delay(500);
            if (!combo.IsDropDownOpen) { combo.IsDropDownOpen = true; chemin += ", rouverte par programme"; await Task.Delay(300); }
            verifier(combo.IsDropDownOpen, libelle + " : la liste est déroulée (" + chemin + ")");
            Control conteneur = combo.ContainerFromIndex(index);
            if (conteneur != null && conteneur.IsEffectivelyVisible && conteneur.Bounds.Width > 0)
            {
                ClicReel(conteneur);
                chemin += ", clic réel sur le choix";
            }
            else
            {
                combo.SelectedIndex = index;
                combo.IsDropDownOpen = false;
                chemin += ", choix par programme";
            }
            await Task.Delay(500);
            return chemin;
        }

        private static byte[] PetitePng()
        {
            using (Avalonia.Media.Imaging.RenderTargetBitmap image = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(12, 12), new Vector(96, 96)))
            using (MemoryStream flux = new MemoryStream())
            {
                image.Save(flux);
                return flux.ToArray();
            }
        }

        private async Task SonderMiseEnForme(Action<bool, string> verifier)
        {
            string policeDefaut = Segment.PoliceDefaut;
            string autrePolice = null;
            foreach (string nom in TexteRiche.PolicesSysteme())
                if (nom != policeDefaut && (autrePolice == null || nom == "Arial")) autrePolice = nom;
            if (autrePolice == null) autrePolice = "Georgia";

            Noeud boite = new Noeud();
            boite.X = 40; boite.Y = 40;
            boite.DefinirTexteSimple(TexteEssai);
            _toile.AjouterNoeud(boite);
            await Task.Delay(200);
            _toile.AllerAuNoeud(boite);
            await Task.Delay(300);
            verifier(_toile.NoeudSelectionne == boite && !_toile.EditionEnCours, "mise en forme : la boîte d'essai est sélectionnée, sans édition");

            // ---------------------------------------------------------------- (a) hors édition : toute la boîte
            object[,] balayage = new object[,]
            {
                { "police", autrePolice }, { "taille", 24.0 }, { "couleur", "#C0392B" }, { "surlignage", "#FFF3A0" },
                { "gras", true }, { "italique", true }, { "souligne", true }, { "barre", true }
            };
            for (int i = 0; i < balayage.GetLength(0); i++)
            {
                string p = (string)balayage[i, 0];
                object v = balayage[i, 1];
                _toile.AppliquerFormatTexte(p, v);
                verifier(Tous(boite.Segments, p, v), "(a) " + p + " : toute la boîte le porte (" + Decrire(boite.Segments) + ")");
            }
            verifier(boite.Segments.Count == 1, "(a) la boîte reste un seul segment après le balayage (" + boite.Segments.Count + ")");
            _toile.AppliquerFormatTexte("gras", null);
            verifier(Tous(boite.Segments, "gras", false), "(a) gras en bascule (null) : toute la boîte repasse en maigre");
            _toile.AppliquerFormatTexte("gras", null);
            verifier(Tous(boite.Segments, "gras", true), "(a) gras en bascule, de nouveau : toute la boîte repasse en gras");
            _toile.AppliquerFormatTexte("alignement", "centre");
            verifier(boite.Alignement == "centre" && Tous(boite.Segments, "alignement", "centre"), "(a) alignement centre : la boîte et toutes ses lignes");
            _toile.AppliquerFormatTexte("surlignage", null);
            verifier(Tous(boite.Segments, "surlignage", null), "(a) surlignage « Aucun » : retiré partout");
            FormatCourant lu = _toile.LireFormatCourant();
            verifier(lu.Actif && lu.Police == autrePolice && Math.Abs(lu.Taille - 24) < 0.01 && lu.Gras && lu.Italique && lu.Souligne && lu.Barre
                && string.Equals(lu.Couleur, "#C0392B", StringComparison.OrdinalIgnoreCase) && lu.Alignement == "centre",
                "(a) l'état lu pour le rail reflète la boîte (" + lu.Police + " " + lu.Taille + " " + lu.Couleur + " " + lu.Alignement + ")");
            _toile.Historique.Annuler();
            verifier(Tous(boite.Segments, "surlignage", "#FFF3A0"), "(a) Ctrl+Z rend le surlignage");
            _toile.Historique.Refaire();
            verifier(Tous(boite.Segments, "surlignage", null), "(a) Ctrl+Y le retire de nouveau");
            // Remise à plat pour la suite : une boîte neutre.
            _toile.AppliquerFormatTexte("police", policeDefaut);
            _toile.AppliquerFormatTexte("taille", Segment.TailleDefaut);
            _toile.AppliquerFormatTexte("couleur", Segment.CouleurDefaut);
            _toile.AppliquerFormatTexte("gras", false);
            _toile.AppliquerFormatTexte("italique", false);
            _toile.AppliquerFormatTexte("souligne", false);
            _toile.AppliquerFormatTexte("barre", false);
            _toile.AppliquerFormatTexte("alignement", "gauche");
            verifier(boite.Segments.Count == 1 && Tous(boite.Segments, "gras", false) && Tous(boite.Segments, "police", policeDefaut), "(a) la boîte est remise à plat (" + Decrire(boite.Segments) + ")");

            // (a) par le rail, d'un vrai clic : la bascule Gras et la liste Taille.
            ToggleButton btnGras = TrouverDansRail<ToggleButton>("Gras");
            ComboBox comboPolice = TrouverDansRail<ComboBox>("Police");
            ComboBox comboTaille = TrouverDansRail<ComboBox>("Taille");
            ComboBox comboCouleur = TrouverDansRail<ComboBox>("Couleur du texte");
            ComboBox comboSurlignage = TrouverDansRail<ComboBox>("Surlignage");
            verifier(btnGras != null && comboPolice != null && comboTaille != null && comboCouleur != null && comboSurlignage != null, "le rail Texte expose Gras, Police, Taille, Couleur et Surlignage");
            if (btnGras != null)
            {
                verifier(Cliquable(btnGras), "(a) rail : la bascule Gras est atteignable d'un clic");
                ClicReel(btnGras);
                await Task.Delay(400);
                verifier(Tous(boite.Segments, "gras", true), "(a) rail : clic sur Gras → toute la boîte en gras (" + Decrire(boite.Segments) + ")");
                verifier(btnGras.IsChecked == true, "(a) rail : la bascule Gras se montre enfoncée");
                verifier(_toile.NoeudSelectionne == boite && !_toile.EditionEnCours, "(a) rail : la boîte reste sélectionnée, sans édition");
            }
            if (comboTaille != null)
            {
                int index20 = IndexParTag(comboTaille, delegate(object t) { return t is double && Math.Abs((double)t - 20) < 0.01; });
                string chemin = await ChoisirDansCombo(comboTaille, index20, verifier, "(a) rail Taille");
                verifier(Tous(boite.Segments, "taille", 20.0), "(a) rail : Taille 20 → toute la boîte (" + chemin + " ; " + Decrire(boite.Segments) + ")");
                verifier(_toile.NoeudSelectionne == boite && !_toile.EditionEnCours, "(a) rail Taille : la boîte reste sélectionnée, sans édition");
            }

            // Remise à plat après les clics du rail (gras, taille 20) : (b) part d'une boîte neutre.
            _toile.AppliquerFormatTexte("gras", false);
            _toile.AppliquerFormatTexte("taille", Segment.TailleDefaut);
            verifier(boite.Segments.Count == 1 && Tous(boite.Segments, "gras", false) && Tous(boite.Segments, "taille", Segment.TailleDefaut), "(a) la boîte est de nouveau neutre avant (b)");

            // ---------------------------------------------------------------- (b) édition avec sélection : la sélection seule
            _toile.AllerAuNoeud(boite);
            await Task.Delay(200);
            _toile.EditerSelection();
            await Task.Delay(500);
            TextBox editeur = FocusActuel() as TextBox;
            verifier(_toile.EditionEnCours && editeur != null, "(b) l'édition de la boîte d'essai est ouverte, le TextBox a le clavier");
            if (editeur == null) { _toile.ValiderEditionEnCours(); _toile.RetirerNoeud(boite); return; }
            verifier((editeur.Text ?? "") == TexteEssai, "(b) le TextBox porte le texte d'essai (" + Echapper(editeur.Text) + ")");
            Selectionner(editeur, 0, 7); // « Bonjour »
            await Task.Delay(100);
            verifier(Debut(editeur) == 0 && Fin(editeur) == 7, "(b) « Bonjour » est sélectionné (" + editeur.SelectionStart + "-" + editeur.SelectionEnd + ", caret " + editeur.CaretIndex + ")");
            for (int i = 0; i < balayage.GetLength(0); i++)
            {
                string p = (string)balayage[i, 0];
                object v = balayage[i, 1];
                Selectionner(editeur, 0, 7);
                _toile.AppliquerFormatTexte(p, v);
                _toile.SynchroniserEditionEnCours();
                verifier(Plage(boite.Segments, 0, 7, p, v, true) && Plage(boite.Segments, 7, int.MaxValue, p, v, false),
                    "(b) " + p + " : la sélection seule le porte (" + Decrire(boite.Segments) + ")");
                verifier(Debut(editeur) == 0 && Fin(editeur) == 7, "(b) " + p + " : la sélection de texte est conservée");
            }
            Selectionner(editeur, 0, 7);
            _toile.AppliquerFormatTexte("alignement", "centre");
            _toile.SynchroniserEditionEnCours();
            verifier(Plage(boite.Segments, 0, 20, "alignement", "centre", true) && Plage(boite.Segments, 20, int.MaxValue, "alignement", "centre", false),
                "(b) alignement centre : la ligne de la sélection seule (" + Decrire(boite.Segments) + ")");
            verifier(_toile.LireFormatCourant().Gras && _toile.LireFormatCourant().Police == autrePolice, "(b) l'état lu pour le rail est celui de la sélection");
            verifier(btnGras != null && btnGras.IsChecked == true, "(b) la bascule Gras du rail se montre enfoncée");

            // (b) par le rail, d'un vrai clic : Gras (bascule), puis les listes.
            if (btnGras != null)
            {
                Selectionner(editeur, 0, 7);
                ClicReel(btnGras);
                await Task.Delay(400);
                _toile.SynchroniserEditionEnCours();
                verifier(_toile.EditionEnCours && _toile.NoeudSelectionne == boite, "(b) rail Gras : l'édition reste ouverte sur la même boîte");
                verifier(Debut(editeur) == 0 && Fin(editeur) == 7, "(b) rail Gras : la sélection de texte est conservée (" + editeur.SelectionStart + "-" + editeur.SelectionEnd + ")");
                verifier(Plage(boite.Segments, 0, 7, "gras", false, true) && Plage(boite.Segments, 7, int.MaxValue, "gras", true, false),
                    "(b) rail Gras : la sélection repasse en maigre, le reste ne bouge pas (" + Decrire(boite.Segments) + ")");
            }
            if (comboPolice != null)
            {
                Selectionner(editeur, 0, 7);
                // La liste de la toile porte des ComboBoxItem ; l'autocomplète de Marabook (1/11) des FontCatalog.Entry (dont des traits) — une autre police valide que la courante.
                int indexCourant = comboPolice.SelectedIndex;
                string policeCourante = NomPoliceDe(comboPolice, indexCourant);
                int indexCible = -1;
                for (int i = 0; i < comboPolice.Items.Count && indexCible < 0; i++)
                {
                    string nom = NomPoliceDe(comboPolice, i);
                    if (nom != null && i != indexCourant && nom != policeCourante) indexCible = i;
                }
                string policeCible = NomPoliceDe(comboPolice, indexCible);
                string chemin = await ChoisirDansCombo(comboPolice, indexCible, verifier, "(b) rail Police");
                _toile.SynchroniserEditionEnCours();
                verifier(_toile.NoeudSelectionne == boite, "(b) rail Police : la boîte sélectionnée n'a pas changé");
                verifier(_toile.EditionEnCours, "(b) rail Police : l'édition est toujours ouverte");
                verifier(Debut(editeur) == 0 && Fin(editeur) == 7, "(b) rail Police : la sélection de texte est conservée (" + editeur.SelectionStart + "-" + editeur.SelectionEnd + ")");
                verifier(Plage(boite.Segments, 0, 7, "police", policeCible, true) && Plage(boite.Segments, 7, int.MaxValue, "police", policeCible, false),
                    "(b) rail Police : seule la sélection passe en " + policeCible + " (" + chemin + " ; " + Decrire(boite.Segments) + ")");
                verifier(ReferenceEquals(FocusActuel(), editeur), "(b) rail Police : le clavier revient au TextBox");
            }
            if (comboTaille != null)
            {
                Selectionner(editeur, 0, 7);
                int index28 = IndexParTag(comboTaille, delegate(object t) { return t is double && Math.Abs((double)t - 28) < 0.01; });
                string chemin = await ChoisirDansCombo(comboTaille, index28, verifier, "(b) rail Taille");
                _toile.SynchroniserEditionEnCours();
                verifier(_toile.NoeudSelectionne == boite && _toile.EditionEnCours, "(b) rail Taille : même boîte, édition toujours ouverte");
                verifier(Debut(editeur) == 0 && Fin(editeur) == 7, "(b) rail Taille : la sélection de texte est conservée");
                verifier(Plage(boite.Segments, 0, 7, "taille", 28.0, true) && Plage(boite.Segments, 7, int.MaxValue, "taille", 28.0, false),
                    "(b) rail Taille : seule la sélection passe en 28 (" + chemin + " ; " + Decrire(boite.Segments) + ")");
            }
            if (comboCouleur != null)
            {
                Selectionner(editeur, 0, 7);
                string hexa = Palette.Couleurs[1, 1];
                int index = IndexParTag(comboCouleur, delegate(object t) { return string.Equals(t as string, hexa, StringComparison.OrdinalIgnoreCase); });
                string chemin = await ChoisirDansCombo(comboCouleur, index, verifier, "(b) rail Couleur");
                _toile.SynchroniserEditionEnCours();
                verifier(_toile.NoeudSelectionne == boite && _toile.EditionEnCours, "(b) rail Couleur : même boîte, édition toujours ouverte");
                verifier(Debut(editeur) == 0 && Fin(editeur) == 7, "(b) rail Couleur : la sélection de texte est conservée");
                verifier(Plage(boite.Segments, 0, 7, "couleur", hexa, true) && Plage(boite.Segments, 7, int.MaxValue, "couleur", hexa, false),
                    "(b) rail Couleur : seule la sélection passe en " + hexa + " (" + chemin + " ; " + Decrire(boite.Segments) + ")");
            }
            if (comboSurlignage != null)
            {
                Selectionner(editeur, 0, 7);
                string hexa = Palette.Surlignages[1, 1];
                int index = IndexParTag(comboSurlignage, delegate(object t) { return string.Equals(t as string, hexa, StringComparison.OrdinalIgnoreCase); });
                string chemin = await ChoisirDansCombo(comboSurlignage, index, verifier, "(b) rail Surlignage");
                _toile.SynchroniserEditionEnCours();
                verifier(_toile.NoeudSelectionne == boite && _toile.EditionEnCours, "(b) rail Surlignage : même boîte, édition toujours ouverte");
                verifier(Plage(boite.Segments, 0, 7, "surlignage", hexa, true) && Plage(boite.Segments, 7, int.MaxValue, "surlignage", hexa, false),
                    "(b) rail Surlignage : seule la sélection passe en " + hexa + " (" + chemin + " ; " + Decrire(boite.Segments) + ")");
            }

            // ---------------------------------------------------------------- (c) édition sans sélection
            // Le curseur dans « monde » (8-13) : le mot sous le curseur prend la forme.
            editeur.CaretIndex = 10;
            Selectionner(editeur, 10, 10);
            await Task.Delay(100);
            verifier(Debut(editeur) == Fin(editeur) && editeur.CaretIndex == 10, "(c) le curseur est dans « monde », sans sélection");
            _toile.AppliquerFormatTexte("gras", null);
            _toile.SynchroniserEditionEnCours();
            verifier(Plage(boite.Segments, 8, 13, "gras", true, true) && Plage(boite.Segments, 0, 8, "gras", true, false) && Plage(boite.Segments, 13, int.MaxValue, "gras", true, false),
                "(c) gras sans sélection : le mot sous le curseur seul (" + Decrire(boite.Segments) + ")");
            _toile.AppliquerFormatTexte("taille", 32.0);
            _toile.SynchroniserEditionEnCours();
            verifier(Plage(boite.Segments, 8, 13, "taille", 32.0, true) && Plage(boite.Segments, 0, 8, "taille", 32.0, false) && Plage(boite.Segments, 13, int.MaxValue, "taille", 32.0, false),
                "(c) taille sans sélection : le mot sous le curseur seul (" + Decrire(boite.Segments) + ")");
            _toile.AppliquerFormatTexte("couleur", "#1E8449");
            _toile.SynchroniserEditionEnCours();
            verifier(Plage(boite.Segments, 8, 13, "couleur", "#1E8449", true) && Plage(boite.Segments, 13, int.MaxValue, "couleur", "#1E8449", false),
                "(c) couleur sans sélection : le mot sous le curseur seul");
            // Le curseur sur la seconde ligne : l'alignement de cette ligne seule.
            editeur.CaretIndex = 25;
            Selectionner(editeur, 25, 25);
            _toile.AppliquerFormatTexte("alignement", "droite");
            _toile.SynchroniserEditionEnCours();
            verifier(Plage(boite.Segments, 21, int.MaxValue, "alignement", "droite", true) && Plage(boite.Segments, 0, 20, "alignement", "droite", false),
                "(c) alignement sans sélection : la ligne du curseur seule (" + Decrire(boite.Segments) + ")");
            verifier(_toile.LireFormatCourant().Alignement == "droite", "(c) l'état lu pour le rail donne l'alignement de la ligne du curseur");
            // Taper à une frontière de style : la frappe hérite du caractère qui précède, et ce caractère ne change pas.
            // « monde » (8-13) est gras 32 ; l'espace devant (7) et « entier » derrière sont neutres.
            editeur.CaretIndex = 13;
            Selectionner(editeur, 13, 13);
            editeur.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "s", Source = editeur });
            await Task.Delay(150);
            _toile.SynchroniserEditionEnCours();
            verifier((editeur.Text ?? "").Substring(8, 6) == "mondes" && Plage(boite.Segments, 8, 14, "gras", true, true) && Plage(boite.Segments, 14, 21, "gras", true, false),
                "(c) frappe après « monde » : le « s » hérite du gras, « entier » reste maigre (" + Decrire(boite.Segments) + ")");
            editeur.CaretIndex = 8;
            Selectionner(editeur, 8, 8);
            editeur.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "X", Source = editeur });
            await Task.Delay(150);
            _toile.SynchroniserEditionEnCours();
            verifier((editeur.Text ?? "").Substring(7, 8) == " Xmondes" && Plage(boite.Segments, 7, 9, "gras", true, false) && Plage(boite.Segments, 7, 9, "italique", true, false)
                && Plage(boite.Segments, 9, 15, "gras", true, true),
                "(c) frappe devant « monde » : le « X » hérite de l'espace (neutre), pas de « Bonjour » ni de « monde » (" + Decrire(boite.Segments) + ")");
            verifier(editeur.CaretIndex == 9, "(c) le curseur suit la frappe (" + editeur.CaretIndex + ")");

            // Pas de mot sous le curseur (après une espace en fin de texte) : la forme choisie vaut pour la frappe qui suit.
            PlacerCaretFin(editeur);
            editeur.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = " ", Source = editeur });
            await Task.Delay(150);
            int avantFrappe = (editeur.Text ?? "").Length;
            _toile.AppliquerFormatTexte("italique", null);
            _toile.AppliquerFormatTexte("souligne", null);
            verifier(_toile.LireFormatCourant().Italique && _toile.LireFormatCourant().Souligne, "(c) sans mot sous le curseur : le rail montre la forme choisie pour la frappe à venir");
            editeur.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "neuf", Source = editeur });
            await Task.Delay(200);
            _toile.SynchroniserEditionEnCours();
            int apresFrappe = (editeur.Text ?? "").Length;
            verifier(apresFrappe == avantFrappe + 4 && Plage(boite.Segments, avantFrappe, apresFrappe, "italique", true, true) && Plage(boite.Segments, avantFrappe, apresFrappe, "souligne", true, true),
                "(c) sans mot sous le curseur : la frappe qui suit porte italique + souligné (" + Decrire(boite.Segments) + ")");
            verifier(Plage(boite.Segments, 21, avantFrappe, "italique", true, false), "(c) …et le reste de la ligne n'en porte pas");
            editeur.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "s", Source = editeur });
            await Task.Delay(150);
            _toile.SynchroniserEditionEnCours();
            verifier(Plage(boite.Segments, avantFrappe, apresFrappe + 1, "italique", true, true), "(c) la frappe continue dans la même forme");

            // ---------------------------------------------------------------- persistance
            List<Segment> attendus = Noeud.CopieDe(boite.Segments);
            string alignementAttendu = boite.Alignement;
            _toile.ValiderEditionEnCours();
            await Task.Delay(200);
            verifier(!_toile.EditionEnCours && Noeud.SegmentsEgaux(attendus, boite.Segments) && boite.Alignement == alignementAttendu,
                "persistance : la validation garde exactement les segments (" + Decrire(boite.Segments) + ")");
            byte[] tea = Fichier.EnregistrerEnMemoire(_toile.Document, null);
            Noeud relu = Fichier.Charger(tea).TrouverNoeud(boite.Id);
            verifier(relu != null && Noeud.SegmentsEgaux(attendus, relu.Segments) && relu.Alignement == alignementAttendu,
                "persistance : l'aller-retour .tea garde exactement les segments" + (relu == null ? "" : " (" + Decrire(relu.Segments) + ")"));
            _toile.Historique.Annuler();
            verifier(boite.TextePlat == TexteEssai, "persistance : Ctrl+Z après validation rend le texte d'avant l'édition (" + Echapper(boite.TextePlat) + ")");
            _toile.Historique.Refaire();
            verifier(Noeud.SegmentsEgaux(attendus, boite.Segments), "persistance : Ctrl+Y rend l'édition");

            // ---------------------------------------------------------------- la boîte à image : texte avant ET après l'image
            Noeud image = new Noeud();
            image.X = 40; image.Y = 260;
            image.DefinirTexteSimple("Avant image");
            try { image.ImageDonnees = PetitePng(); image.ImageExtension = ".png"; image.ImageLargeur = 12; image.ImageHauteur = 12; }
            catch (Exception error) { verifier(false, "image : impossible de fabriquer une image (" + error.Message + ")"); }
            image.SegmentsApres.Add(new Segment { Texte = "Après image" });
            _toile.AjouterNoeud(image);
            await Task.Delay(200);
            _toile.AllerAuNoeud(image);
            await Task.Delay(300);
            _toile.AppliquerFormatTexte("gras", null);
            _toile.AppliquerFormatTexte("police", autrePolice);
            _toile.AppliquerFormatTexte("alignement", "centre");
            verifier(Tous(image.Segments, "gras", true) && Tous(image.SegmentsApres, "gras", true), "image (a) gras : le texte avant ET après l'image");
            verifier(Tous(image.Segments, "police", autrePolice) && Tous(image.SegmentsApres, "police", autrePolice), "image (a) police : avant ET après");
            verifier(image.Alignement == "centre" && Tous(image.SegmentsApres, "alignement", "centre"), "image (a) alignement : la boîte et le texte après");
            _toile.EditerSelection();
            await Task.Delay(500);
            TextBox editeurAvant = FocusActuel() as TextBox;
            TextBox editeurApres = null;
            foreach (Visual v in _toile.GetVisualDescendants())
            {
                TextBox t = v as TextBox;
                if (t != null && t.IsEffectivelyVisible && !ReferenceEquals(t, editeurAvant)) { editeurApres = t; break; }
            }
            verifier(_toile.EditionEnCours && editeurAvant != null && editeurApres != null, "image (b) : deux TextBox (avant / après l'image) sont ouverts");
            if (editeurApres != null)
            {
                editeurApres.Focus();
                await Task.Delay(150);
                Selectionner(editeurApres, 0, 5); // « Après »
                _toile.AppliquerFormatTexte("italique", null);
                _toile.AppliquerFormatTexte("couleur", "#C0392B");
                _toile.SynchroniserEditionEnCours();
                verifier(_toile.EditionEnCours, "image (b) : l'édition survit au passage d'un TextBox à l'autre");
                verifier(Plage(image.SegmentsApres, 0, 5, "italique", true, true) && Plage(image.SegmentsApres, 5, int.MaxValue, "italique", true, false)
                    && Plage(image.SegmentsApres, 0, 5, "couleur", "#C0392B", true),
                    "image (b) : la sélection du texte après l'image seule (" + Decrire(image.SegmentsApres) + ")");
                verifier(Tous(image.Segments, "italique", false), "image (b) : le texte avant l'image ne bouge pas");
            }
            List<Segment> attendusImage = Noeud.CopieDe(image.SegmentsApres);
            _toile.ValiderEditionEnCours();
            await Task.Delay(200);
            Noeud imageRelue = Fichier.Charger(Fichier.EnregistrerEnMemoire(_toile.Document, null)).TrouverNoeud(image.Id);
            verifier(imageRelue != null && Noeud.SegmentsEgaux(attendusImage, imageRelue.SegmentsApres) && Tous(imageRelue.Segments, "gras", true),
                "image : la validation et le .tea gardent le texte après l'image");

            // Les boîtes d'essai quittent la carte (la suite de la sonde compte 5 boîtes).
            _toile.Deselectionner();
            _toile.RetirerNoeud(image);
            _toile.RetirerNoeud(boite);
            _toile.Historique.Vider();
            await Task.Delay(200);
            verifier(_toile.Document.Noeuds.Count == 5, "mise en forme : les boîtes d'essai sont retirées (" + _toile.Document.Noeuds.Count + " boîtes)");
        }
    }
}
