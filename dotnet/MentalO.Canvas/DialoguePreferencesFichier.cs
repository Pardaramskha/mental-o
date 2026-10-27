using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Fichier > Préférences de fichier : réglages propres à la carte ouverte.</summary>
    public static class DialoguePreferencesFichier
    {
        /// <summary>Faux chez un hôte qui enregistre lui-même (Marabook et son .plot,
        /// 27/10/2026) : la case « Enregistrer automatiquement » n'y a pas de sens.</summary>
        public static bool AvecSauvegardeAuto = true;

        /// <summary>Affiche le dialogue ; renvoie vrai si des préférences ont été appliquées.
        /// Les rectangles servent à la miniature de l'onglet Général.</summary>
        public static async Task<bool> Afficher(Window proprietaire, DocumentCarte document, List<Rect> rectangles)
        {
            Dialogue dialogue = new Dialogue("Préférences de fichier");
            dialogue.SizeToContent = SizeToContent.Manual;
            dialogue.Width = 600;
            dialogue.Height = 520;

            TabControl onglets = new TabControl();

            // --- Général -------------------------------------------------------------------
            Grid general = Dialogue.NouvelleGrille(6);
            TextBox nom = new TextBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left, Text = document.NomAffichage ?? "" };
            Dialogue.AjouterLigne(general, 0, "Nom de la carte (titre de la fenêtre) :", nom);
            TextBox auteur = new TextBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left, Text = document.Auteur ?? "" };
            Dialogue.AjouterLigne(general, 1, "Auteur ou autrice :", auteur);
            CheckBox sauvegardeAuto = new CheckBox
            {
                Content = "Enregistrer automatiquement toutes les 3 minutes",
                IsChecked = document.SauvegardeAuto,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (AvecSauvegardeAuto) Dialogue.AjouterLigne(general, 2, "", sauvegardeAuto);
            int nbImages = 0;
            foreach (Noeud n in document.Noeuds)
                if (n.EstImage) nbImages++;
            Dialogue.AjouterLigne(general, 3, "Contenu :", new TextBlock
            {
                Text = string.Format("{0} boîte(s), dont {1} image(s) · {2} lien(s) · {3} groupe(s)",
                    document.Noeuds.Count, nbImages, document.Liens.Count, document.Groupes.Count),
                Foreground = Chrome.TexteDoux,
                VerticalAlignment = VerticalAlignment.Center
            });
            string dates = string.IsNullOrEmpty(document.DateCreation)
                ? "Jamais enregistrée"
                : string.Format("Créée le {0} · Modifiée le {1}", document.DateCreation,
                    string.IsNullOrEmpty(document.DateModification) ? "—" : document.DateModification);
            Dialogue.AjouterLigne(general, 4, "Dates :", new TextBlock
            {
                Text = dates,
                Foreground = Chrome.TexteDoux,
                VerticalAlignment = VerticalAlignment.Center
            });
            Dialogue.AjouterLigne(general, 5, "Aperçu :", ConstruireMiniature(rectangles, document));
            onglets.Items.Add(new TabItem { Header = "Général", Content = general });

            // --- Personnalisation ----------------------------------------------------------
            Grid personnalisation = Dialogue.NouvelleGrille(4);
            ComboBox fond = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            fond.Items.Add(ElementCouleur("Crème (défaut)", "#F7F5F0"));
            fond.Items.Add(ElementCouleur("Blanc", "#FFFFFF"));
            for (int i = 0; i < Palette.Surlignages.GetLength(0); i++)
                fond.Items.Add(ElementCouleur(Palette.Surlignages[i, 0], Palette.Surlignages[i, 1]));
            foreach (string hexa in document.CouleursPersonnalisees)
                fond.Items.Add(ElementCouleur(hexa, hexa));
            SelectionnerTag(fond, document.FondCouleur);
            StackPanel ligneFond = new StackPanel { Orientation = Orientation.Horizontal };
            ligneFond.Children.Add(fond);
            Button autreFond = new Button { Content = "Autre…", Margin = new Thickness(6, 0, 0, 0) };
            autreFond.Click += async delegate
            {
                string hexa = await SelecteurCouleur.Choisir(dialogue, null);
                if (hexa == null) return;
                document.AjouterCouleurPersonnalisee(hexa);
                fond.Items.Add(ElementCouleur(hexa, hexa));
                fond.SelectedIndex = fond.Items.Count - 1;
            };
            ligneFond.Children.Add(autreFond);
            Dialogue.AjouterLigne(personnalisation, 0, "Fond du tableau :", ligneFond);

            ComboBox motif = new ComboBox { Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            motif.Items.Add(new ComboBoxItem { Content = "Uni", Tag = "uni" });
            motif.Items.Add(new ComboBoxItem { Content = "Pointillé", Tag = "points" });
            motif.Items.Add(new ComboBoxItem { Content = "Quadrillé", Tag = "grille" });
            SelectionnerTag(motif, document.FondMotif);
            if (motif.SelectedIndex < 0) motif.SelectedIndex = 0;
            Dialogue.AjouterLigne(personnalisation, 1, "Motif du fond :", motif);

            ListBox listeCouleurs = new ListBox { Width = 200, Height = 110, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (string hexa in document.CouleursPersonnalisees)
                listeCouleurs.Items.Add(ElementCouleurListe(hexa));
            StackPanel ligneCouleurs = new StackPanel { Orientation = Orientation.Horizontal };
            ligneCouleurs.Children.Add(listeCouleurs);
            StackPanel boutonsCouleurs = new StackPanel { Margin = new Thickness(6, 0, 0, 0), Spacing = 4 };
            Button ajouterCouleur = new Button { Content = "Ajouter…" };
            ajouterCouleur.Click += async delegate
            {
                string hexa = await SelecteurCouleur.Choisir(dialogue, null);
                if (hexa == null || !document.AjouterCouleurPersonnalisee(hexa)) return;
                listeCouleurs.Items.Add(ElementCouleurListe(hexa));
                fond.Items.Add(ElementCouleur(hexa, hexa));
            };
            Button supprimerCouleur = new Button { Content = "Supprimer" };
            supprimerCouleur.Click += delegate
            {
                ListBoxItem choix = listeCouleurs.SelectedItem as ListBoxItem;
                if (choix == null) return;
                document.CouleursPersonnalisees.Remove((string)choix.Tag);
                listeCouleurs.Items.Remove(choix);
            };
            boutonsCouleurs.Children.Add(ajouterCouleur);
            boutonsCouleurs.Children.Add(supprimerCouleur);
            ligneCouleurs.Children.Add(boutonsCouleurs);
            Dialogue.AjouterLigne(personnalisation, 2, "Couleurs personnalisées du fichier :", ligneCouleurs);
            onglets.Items.Add(new TabItem { Header = "Personnalisation", Content = personnalisation });

            // --- Texte ---------------------------------------------------------------------
            Grid texte = Dialogue.NouvelleGrille(4);
            TextBox padding = new TextBox
            {
                Width = 70,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = document.PaddingBoites.ToString(CultureInfo.CurrentCulture)
            };
            Dialogue.AjouterLigne(texte, 0, "Marge intérieure des boîtes (px) :", padding);
            ComboBox police = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (string nomPolice in TexteRiche.PolicesSysteme())
                police.Items.Add(nomPolice);
            police.SelectedItem = document.PoliceParDefaut;
            Dialogue.AjouterLigne(texte, 1, "Police par défaut :", police);
            ComboBox taille = new ComboBox { Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (double valeur in Palette.Tailles)
                taille.Items.Add(valeur.ToString(CultureInfo.CurrentCulture));
            string tailleActuelle = document.TailleParDefaut.ToString(CultureInfo.CurrentCulture);
            if (!taille.Items.Contains(tailleActuelle)) taille.Items.Add(tailleActuelle);
            taille.SelectedItem = tailleActuelle;
            Dialogue.AjouterLigne(texte, 2, "Taille par défaut :", taille);
            ComboBox couleur = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                couleur.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
            foreach (string hexa in document.CouleursPersonnalisees)
                couleur.Items.Add(ElementCouleur(hexa, hexa));
            SelectionnerTag(couleur, document.CouleurParDefaut);
            if (couleur.SelectedIndex < 0)
            {
                couleur.Items.Add(ElementCouleur(document.CouleurParDefaut, document.CouleurParDefaut));
                couleur.SelectedIndex = couleur.Items.Count - 1;
            }
            Dialogue.AjouterLigne(texte, 3, "Couleur par défaut :", couleur);
            onglets.Items.Add(new TabItem { Header = "Texte", Content = texte });

            // --- Boîtes (style des nouvelles boîtes) -----------------------------------------
            StyleBoite styleBoite = document.BoiteParDefaut != null
                ? document.BoiteParDefaut : StyleBoite.Depuis(new Noeud());
            Grid boites = Dialogue.NouvelleGrille(7);
            ComboBox fondBoite = ComboPalette(document, styleBoite.Fond, "Blanc", "#FFFFFF", true);
            Dialogue.AjouterLigne(boites, 0, "Fond :", AvecAutre(dialogue, document, fondBoite));
            ComboBox bordureBoite = ComboPalette(document, styleBoite.BordureCouleur, "Gris sable (défaut)", "#B8B2A7", false);
            Dialogue.AjouterLigne(boites, 1, "Couleur de bordure :", AvecAutre(dialogue, document, bordureBoite));
            ComboBox epaisseurBoite = ComboNombres(
                new double[] { 1, 1.5, 2, 3, 5 },
                new string[] { "Fine (1)", "Normale (1,5)", "Moyenne (2)", "Épaisse (3)", "Très épaisse (5)" },
                styleBoite.BordureEpaisseur);
            Dialogue.AjouterLigne(boites, 2, "Épaisseur de bordure :", epaisseurBoite);
            ComboBox traitBoite = ComboChoix(new string[,]
                { { "Plein", "plein" }, { "Tirets", "tirets" }, { "Pointillé", "points" }, { "Aucune", "aucune" } },
                styleBoite.BordureTrait);
            Dialogue.AjouterLigne(boites, 3, "Trait de bordure :", traitBoite);
            ComboBox coinsBoite = ComboNombres(
                new double[] { 0, 5, 12 },
                new string[] { "Carrés", "Arrondis", "Très arrondis" },
                styleBoite.Rayon);
            Dialogue.AjouterLigne(boites, 4, "Coins :", coinsBoite);
            ComboBox ombreBoite = ComboChoix(new string[,]
                { { "Aucune", "aucune" }, { "Légère", "legere" }, { "Marquée", "marquee" } },
                string.IsNullOrEmpty(styleBoite.Ombre) ? "legere" : styleBoite.Ombre);
            Dialogue.AjouterLigne(boites, 5, "Ombre :", ombreBoite);
            ComboBox opaciteBoite = ComboNombres(
                new double[] { 1, 0.85, 0.7, 0.5, 0.3 },
                new string[] { "100 %", "85 %", "70 %", "50 %", "30 %" },
                styleBoite.Opacite > 0 ? styleBoite.Opacite : 1);
            Dialogue.AjouterLigne(boites, 6, "Opacité :", opaciteBoite);
            onglets.Items.Add(new TabItem { Header = "Boîtes", Content = boites });

            // --- Liens (style des nouveaux liens) --------------------------------------------
            Grid liensGrille = Dialogue.NouvelleGrille(5);
            ComboBox couleurLien = ComboPalette(document, document.LienCouleurDefaut, "Gris (défaut)", "#6A665E", false);
            Dialogue.AjouterLigne(liensGrille, 0, "Couleur :", AvecAutre(dialogue, document, couleurLien));
            ComboBox epaisseurLien = ComboNombres(
                new double[] { 1, 2, 3, 5 },
                new string[] { "Fin (1)", "Normal (2)", "Épais (3)", "Très épais (5)" },
                document.LienEpaisseurDefaut);
            Dialogue.AjouterLigne(liensGrille, 1, "Épaisseur :", epaisseurLien);
            ComboBox traitLien = ComboChoix(new string[,]
                { { "Plein", "plein" }, { "Tirets", "tirets" }, { "Pointillé", "points" } },
                document.LienTraitDefaut);
            Dialogue.AjouterLigne(liensGrille, 2, "Trait :", traitLien);
            ComboBox flecheLien = ComboChoix(new string[,]
                { { "Sans flèche", "aucune" }, { "A vers B", "simple" }, { "B vers A", "inverse" }, { "Les deux sens", "double" } },
                document.LienFlecheDefaut);
            Dialogue.AjouterLigne(liensGrille, 3, "Flèches :", flecheLien);
            ComboBox traceLien = ComboChoix(new string[,]
                { { "Droit", "droit" }, { "Courbe", "courbe" }, { "Équerre", "equerre" } },
                document.LienTraceDefaut);
            Dialogue.AjouterLigne(liensGrille, 4, "Tracé :", traceLien);
            onglets.Items.Add(new TabItem { Header = "Liens", Content = liensGrille });

            // --- Groupes (style des nouveaux groupes) ----------------------------------------
            Grid groupes = Dialogue.NouvelleGrille(3);
            ComboBox couleurGroupe = ComboPalette(document, document.GroupeCouleurDefaut, "Gris bleuté (défaut)", "#8A94A8", false);
            Dialogue.AjouterLigne(groupes, 0, "Couleur du cadre :", AvecAutre(dialogue, document, couleurGroupe));
            ComboBox traitGroupe = ComboChoix(new string[,]
                { { "Tirets", "tirets" }, { "Plein", "plein" }, { "Pointillé", "points" } },
                document.GroupeTraitDefaut);
            Dialogue.AjouterLigne(groupes, 1, "Trait du cadre :", traitGroupe);
            ComboBox epaisseurGroupe = ComboNombres(
                new double[] { 1, 1.5, 2.5, 4 },
                new string[] { "Fine (1)", "Normale (1,5)", "Moyenne (2,5)", "Épaisse (4)" },
                document.GroupeEpaisseurDefaut);
            Dialogue.AjouterLigne(groupes, 2, "Épaisseur du cadre :", epaisseurGroupe);
            onglets.Items.Add(new TabItem { Header = "Groupes", Content = groupes });

            // --- Boutons -------------------------------------------------------------------
            StackPanel boutons = dialogue.RangeeBoutons("Appliquer", "Annuler", true);
            boutons.Margin = new Thickness(0, 8, 12, 10);

            DockPanel racine = new DockPanel();
            DockPanel.SetDock(boutons, Dock.Bottom);
            racine.Children.Add(boutons);
            racine.Children.Add(onglets);
            dialogue.Content = racine;

            if (!await dialogue.Afficher(proprietaire)) return false;

            document.NomAffichage = (nom.Text ?? "").Trim();
            document.Auteur = (auteur.Text ?? "").Trim();
            if (AvecSauvegardeAuto) document.SauvegardeAuto = sauvegardeAuto.IsChecked == true;
            string choixFond = TagChaine(fond);
            if (choixFond != null) document.FondCouleur = choixFond;
            string choixMotif = TagChaine(motif);
            if (choixMotif != null) document.FondMotif = choixMotif;
            double? nouveauPadding = DialogueSaisie.InterpreterTaille(padding.Text);
            if (nouveauPadding.HasValue)
                document.PaddingBoites = Math.Max(2, Math.Min(40, nouveauPadding.Value));
            string nouvellePolice = police.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(nouvellePolice)) document.PoliceParDefaut = nouvellePolice;
            double? nouvelleTaille = DialogueSaisie.InterpreterTaille(taille.SelectedItem as string);
            if (nouvelleTaille.HasValue) document.TailleParDefaut = nouvelleTaille.Value;
            string choixCouleur = TagChaine(couleur);
            if (choixCouleur != null) document.CouleurParDefaut = choixCouleur;

            StyleBoite nouveauStyle = new StyleBoite();
            nouveauStyle.Fond = TagChaine(fondBoite) ?? styleBoite.Fond;
            nouveauStyle.BordureCouleur = TagChaine(bordureBoite) ?? styleBoite.BordureCouleur;
            nouveauStyle.BordureEpaisseur = TagNombre(epaisseurBoite, styleBoite.BordureEpaisseur);
            nouveauStyle.BordureTrait = TagChaine(traitBoite) ?? styleBoite.BordureTrait;
            nouveauStyle.Rayon = TagNombre(coinsBoite, styleBoite.Rayon);
            nouveauStyle.Ombre = TagChaine(ombreBoite) ?? styleBoite.Ombre;
            nouveauStyle.Opacite = TagNombre(opaciteBoite, styleBoite.Opacite);
            document.BoiteParDefaut = nouveauStyle;

            document.LienCouleurDefaut = TagChaine(couleurLien) ?? document.LienCouleurDefaut;
            document.LienEpaisseurDefaut = TagNombre(epaisseurLien, document.LienEpaisseurDefaut);
            document.LienTraitDefaut = TagChaine(traitLien) ?? document.LienTraitDefaut;
            document.LienFlecheDefaut = TagChaine(flecheLien) ?? document.LienFlecheDefaut;
            document.LienTraceDefaut = TagChaine(traceLien) ?? document.LienTraceDefaut;

            document.GroupeCouleurDefaut = TagChaine(couleurGroupe) ?? document.GroupeCouleurDefaut;
            document.GroupeTraitDefaut = TagChaine(traitGroupe) ?? document.GroupeTraitDefaut;
            document.GroupeEpaisseurDefaut = TagNombre(epaisseurGroupe, document.GroupeEpaisseurDefaut);
            return true;
        }

        // ------------------------------------------------------------------ aides

        /// <summary>Palette complète (pastels + vives + couleurs du fichier), avec une
        /// entrée « défaut » en tête ; la valeur actuelle est ajoutée si elle en sort.</summary>
        private static ComboBox ComboPalette(DocumentCarte document, string actuel,
            string premierNom, string premierHexa, bool avecTransparent)
        {
            ComboBox combo = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            combo.Items.Add(ElementCouleur(premierNom, premierHexa));
            if (avecTransparent) combo.Items.Add(ElementCouleur("Transparent", "Transparent"));
            for (int i = 0; i < Palette.Surlignages.GetLength(0); i++)
                combo.Items.Add(ElementCouleur(Palette.Surlignages[i, 0], Palette.Surlignages[i, 1]));
            for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                combo.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
            foreach (string hexa in document.CouleursPersonnalisees)
                combo.Items.Add(ElementCouleur(hexa, hexa));
            SelectionnerTag(combo, actuel);
            if (combo.SelectedIndex < 0 && !string.IsNullOrEmpty(actuel))
            {
                combo.Items.Add(ElementCouleur(actuel, actuel));
                combo.SelectedIndex = combo.Items.Count - 1;
            }
            if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
            return combo;
        }

        /// <summary>Ajoute un bouton « Autre… » (sélecteur de couleur maison) à côté d'un combo.</summary>
        private static StackPanel AvecAutre(Window dialogue, DocumentCarte document, ComboBox combo)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            ligne.Children.Add(combo);
            Button autre = new Button { Content = "Autre…", Margin = new Thickness(6, 0, 0, 0) };
            autre.Click += async delegate
            {
                string hexa = await SelecteurCouleur.Choisir(dialogue, null);
                if (hexa == null) return;
                document.AjouterCouleurPersonnalisee(hexa);
                combo.Items.Add(ElementCouleur(hexa, hexa));
                combo.SelectedIndex = combo.Items.Count - 1;
            };
            ligne.Children.Add(autre);
            return ligne;
        }

        /// <summary>Combo à choix nommés (paires libellé / valeur de stockage).</summary>
        private static ComboBox ComboChoix(string[,] paires, string actuel)
        {
            ComboBox combo = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 0; i < paires.GetLength(0); i++)
                combo.Items.Add(new ComboBoxItem { Content = paires[i, 0], Tag = paires[i, 1] });
            SelectionnerTag(combo, actuel);
            if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
            return combo;
        }

        /// <summary>Combo de valeurs numériques nommées ; la valeur actuelle est ajoutée
        /// telle quelle si elle ne figure pas dans la liste.</summary>
        private static ComboBox ComboNombres(double[] valeurs, string[] noms, double actuel)
        {
            ComboBox combo = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 0; i < valeurs.Length; i++)
            {
                ComboBoxItem element = new ComboBoxItem { Content = noms[i], Tag = valeurs[i] };
                combo.Items.Add(element);
                if (Math.Abs(valeurs[i] - actuel) < 0.01) combo.SelectedItem = element;
            }
            if (combo.SelectedIndex < 0)
            {
                ComboBoxItem element = new ComboBoxItem
                {
                    Content = actuel.ToString(CultureInfo.CurrentCulture),
                    Tag = actuel
                };
                combo.Items.Add(element);
                combo.SelectedItem = element;
            }
            return combo;
        }

        private static string TagChaine(ComboBox combo)
        {
            ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
            return choix == null ? null : choix.Tag as string;
        }

        private static double TagNombre(ComboBox combo, double repli)
        {
            ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
            return choix != null && choix.Tag is double ? (double)choix.Tag : repli;
        }

        private static Control ConstruireMiniature(List<Rect> rectangles, DocumentCarte document)
        {
            Canvas apercu = new Canvas
            {
                Width = 220,
                Height = 120,
                Background = new SolidColorBrush(Palette.CouleurDepuis(
                    document.FondCouleur, Color.FromRgb(0xF7, 0xF5, 0xF0))),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            if (rectangles != null && rectangles.Count > 0)
            {
                Rect monde = rectangles[0];
                foreach (Rect r in rectangles) monde = monde.Union(r);
                monde = monde.Inflate(30);
                double echelle = Math.Min(apercu.Width / monde.Width, apercu.Height / monde.Height);
                SolidColorBrush gris = new SolidColorBrush(Color.FromRgb(0xA9, 0xA2, 0x97));
                foreach (Rect r in rectangles)
                {
                    Rectangle forme = new Rectangle
                    {
                        Width = Math.Max(2, r.Width * echelle),
                        Height = Math.Max(2, r.Height * echelle),
                        Fill = gris,
                        RadiusX = 1,
                        RadiusY = 1
                    };
                    Canvas.SetLeft(forme, (r.X - monde.X) * echelle);
                    Canvas.SetTop(forme, (r.Y - monde.Y) * echelle);
                    apercu.Children.Add(forme);
                }
            }
            return new Border
            {
                BorderBrush = Chrome.Bordure,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = apercu
            };
        }

        public static ComboBoxItem ElementCouleur(string nomAffiche, string hexa)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            Control pastille = Ui.Pastille(hexa, 12);
            pastille.VerticalAlignment = VerticalAlignment.Center;
            ligne.Children.Add(pastille);
            ligne.Children.Add(new TextBlock { Text = nomAffiche, VerticalAlignment = VerticalAlignment.Center });
            return new ComboBoxItem { Content = ligne, Tag = hexa };
        }

        private static ListBoxItem ElementCouleurListe(string hexa)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            Control pastille = Ui.Pastille(hexa, 12);
            pastille.VerticalAlignment = VerticalAlignment.Center;
            ligne.Children.Add(pastille);
            ligne.Children.Add(new TextBlock { Text = hexa, VerticalAlignment = VerticalAlignment.Center });
            return new ListBoxItem { Content = ligne, Tag = hexa };
        }

        public static void SelectionnerTag(ComboBox combo, string valeur)
        {
            foreach (object objet in combo.Items)
            {
                ComboBoxItem element = objet as ComboBoxItem;
                if (element != null && string.Equals((string)element.Tag, valeur, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = element;
                    return;
                }
            }
        }
    }
}
