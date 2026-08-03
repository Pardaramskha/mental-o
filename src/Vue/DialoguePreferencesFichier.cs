using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Fichier > Préférences de fichier : réglages propres à la carte ouverte.</summary>
    public static class DialoguePreferencesFichier
    {
        /// <summary>Affiche le dialogue ; renvoie vrai si des préférences ont été appliquées.
        /// Les rectangles servent à la miniature de l'onglet Général.</summary>
        public static bool Afficher(Window proprietaire, DocumentCarte document,
            System.Collections.Generic.List<Rect> rectangles)
        {
            Window dialogue = new Window();
            dialogue.Title = "Préférences de fichier";
            dialogue.Owner = proprietaire;
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialogue.Width = 560;
            dialogue.Height = 480;
            dialogue.ResizeMode = ResizeMode.NoResize;
            dialogue.ShowInTaskbar = false;

            TabControl onglets = new TabControl();

            // --- Général -------------------------------------------------------------------
            Grid general = NouvelleGrille(6);
            TextBox nom = new TextBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Text = document.NomAffichage ?? "" };
            AjouterLigne(general, 0, "Nom de la carte (titre de la fenêtre) :", nom);
            TextBox auteur = new TextBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Text = document.Auteur ?? "" };
            AjouterLigne(general, 1, "Auteur ou autrice :", auteur);
            CheckBox sauvegardeAuto = new CheckBox
            {
                Content = "Enregistrer automatiquement toutes les 3 minutes",
                IsChecked = document.SauvegardeAuto,
                VerticalAlignment = VerticalAlignment.Center
            };
            AjouterLigne(general, 2, "", sauvegardeAuto);
            int nbImages = 0;
            foreach (Noeud n in document.Noeuds)
                if (n.EstImage) nbImages++;
            AjouterLigne(general, 3, "Contenu :", new TextBlock
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
            AjouterLigne(general, 4, "Dates :", new TextBlock
            {
                Text = dates,
                Foreground = Chrome.TexteDoux,
                VerticalAlignment = VerticalAlignment.Center
            });
            AjouterLigne(general, 5, "Aperçu :", ConstruireMiniature(rectangles, document));
            onglets.Items.Add(new TabItem { Header = "Général", Content = general });

            // --- Personnalisation ----------------------------------------------------------
            Grid personnalisation = NouvelleGrille(4);
            ComboBox fond = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
            fond.Items.Add(ElementCouleur("Crème (défaut)", "#F7F5F0"));
            fond.Items.Add(ElementCouleur("Blanc", "#FFFFFF"));
            for (int i = 0; i < TexteRiche.Surlignages.GetLength(0); i++)
                fond.Items.Add(ElementCouleur(TexteRiche.Surlignages[i, 0], TexteRiche.Surlignages[i, 1]));
            foreach (string hexa in document.CouleursPersonnalisees)
                fond.Items.Add(ElementCouleur(hexa, hexa));
            SelectionnerCouleur(fond, document.FondCouleur);
            StackPanel ligneFond = new StackPanel { Orientation = Orientation.Horizontal };
            ligneFond.Children.Add(fond);
            Button autreFond = new Button { Content = "Autre…", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0) };
            autreFond.Click += delegate
            {
                string hexa = ChoisirCouleur();
                if (hexa == null) return;
                document.AjouterCouleurPersonnalisee(hexa);
                fond.Items.Add(ElementCouleur(hexa, hexa));
                fond.SelectedIndex = fond.Items.Count - 1;
            };
            ligneFond.Children.Add(autreFond);
            AjouterLigne(personnalisation, 0, "Fond du tableau :", ligneFond);

            ComboBox motif = new ComboBox { Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            motif.Items.Add(new ComboBoxItem { Content = "Uni", Tag = "uni" });
            motif.Items.Add(new ComboBoxItem { Content = "Pointillé", Tag = "points" });
            motif.Items.Add(new ComboBoxItem { Content = "Quadrillé", Tag = "grille" });
            SelectionnerCouleur(motif, document.FondMotif);
            if (motif.SelectedIndex < 0) motif.SelectedIndex = 0;
            AjouterLigne(personnalisation, 1, "Motif du fond :", motif);

            ListBox listeCouleurs = new ListBox { Width = 200, Height = 110, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (string hexa in document.CouleursPersonnalisees)
                listeCouleurs.Items.Add(ElementCouleurListe(hexa));
            StackPanel ligneCouleurs = new StackPanel { Orientation = Orientation.Horizontal };
            ligneCouleurs.Children.Add(listeCouleurs);
            StackPanel boutonsCouleurs = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            Button ajouterCouleur = new Button { Content = "Ajouter…", Padding = new Thickness(8, 1, 8, 1) };
            ajouterCouleur.Click += delegate
            {
                string hexa = ChoisirCouleur();
                if (hexa == null || !document.AjouterCouleurPersonnalisee(hexa)) return;
                listeCouleurs.Items.Add(ElementCouleurListe(hexa));
                fond.Items.Add(ElementCouleur(hexa, hexa));
            };
            Button supprimerCouleur = new Button { Content = "Supprimer", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 4, 0, 0) };
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
            AjouterLigne(personnalisation, 2, "Couleurs personnalisées du fichier :", ligneCouleurs);
            onglets.Items.Add(new TabItem { Header = "Personnalisation", Content = personnalisation });

            // --- Texte ---------------------------------------------------------------------
            Grid texte = NouvelleGrille(4);
            TextBox padding = new TextBox
            {
                Width = 70,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = document.PaddingBoites.ToString(CultureInfo.CurrentCulture)
            };
            AjouterLigne(texte, 0, "Marge intérieure des boîtes (px) :", padding);
            ComboBox police = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Left, IsEditable = true };
            foreach (string nomPolice in TexteRiche.PolicesSysteme())
                police.Items.Add(nomPolice);
            police.Text = document.PoliceParDefaut;
            AjouterLigne(texte, 1, "Police par défaut :", police);
            ComboBox taille = new ComboBox { Width = 70, HorizontalAlignment = HorizontalAlignment.Left, IsEditable = true };
            foreach (double valeur in TexteRiche.Tailles)
                taille.Items.Add(valeur);
            taille.Text = document.TailleParDefaut.ToString(CultureInfo.CurrentCulture);
            AjouterLigne(texte, 2, "Taille par défaut :", taille);
            ComboBox couleur = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                couleur.Items.Add(ElementCouleur(TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1]));
            foreach (string hexa in document.CouleursPersonnalisees)
                couleur.Items.Add(ElementCouleur(hexa, hexa));
            SelectionnerCouleur(couleur, document.CouleurParDefaut);
            AjouterLigne(texte, 3, "Couleur par défaut :", couleur);
            onglets.Items.Add(new TabItem { Header = "Texte", Content = texte });

            // --- Boîtes (style des nouvelles boîtes) -----------------------------------------
            StyleBoite styleBoite = document.BoiteParDefaut != null
                ? document.BoiteParDefaut : StyleBoite.Depuis(new Noeud());
            Grid boites = NouvelleGrille(7);
            ComboBox fondBoite = ComboPalette(document, styleBoite.Fond, "Blanc", "#FFFFFF", true);
            AjouterLigne(boites, 0, "Fond :", AvecAutre(document, fondBoite));
            ComboBox bordureBoite = ComboPalette(document, styleBoite.BordureCouleur, "Gris sable (défaut)", "#B8B2A7", false);
            AjouterLigne(boites, 1, "Couleur de bordure :", AvecAutre(document, bordureBoite));
            ComboBox epaisseurBoite = ComboNombres(
                new double[] { 1, 1.5, 2, 3, 5 },
                new string[] { "Fine (1)", "Normale (1,5)", "Moyenne (2)", "Épaisse (3)", "Très épaisse (5)" },
                styleBoite.BordureEpaisseur);
            AjouterLigne(boites, 2, "Épaisseur de bordure :", epaisseurBoite);
            ComboBox traitBoite = ComboChoix(new string[,]
                { { "Plein", "plein" }, { "Tirets", "tirets" }, { "Pointillé", "points" }, { "Aucune", "aucune" } },
                styleBoite.BordureTrait);
            AjouterLigne(boites, 3, "Trait de bordure :", traitBoite);
            ComboBox coinsBoite = ComboNombres(
                new double[] { 0, 5, 12 },
                new string[] { "Carrés", "Arrondis", "Très arrondis" },
                styleBoite.Rayon);
            AjouterLigne(boites, 4, "Coins :", coinsBoite);
            ComboBox ombreBoite = ComboChoix(new string[,]
                { { "Aucune", "aucune" }, { "Légère", "legere" }, { "Marquée", "marquee" } },
                string.IsNullOrEmpty(styleBoite.Ombre) ? "legere" : styleBoite.Ombre);
            AjouterLigne(boites, 5, "Ombre :", ombreBoite);
            ComboBox opaciteBoite = ComboNombres(
                new double[] { 1, 0.85, 0.7, 0.5, 0.3 },
                new string[] { "100 %", "85 %", "70 %", "50 %", "30 %" },
                styleBoite.Opacite > 0 ? styleBoite.Opacite : 1);
            AjouterLigne(boites, 6, "Opacité :", opaciteBoite);
            onglets.Items.Add(new TabItem { Header = "Boîtes", Content = boites });

            // --- Liens (style des nouveaux liens) --------------------------------------------
            Grid liensGrille = NouvelleGrille(5);
            ComboBox couleurLien = ComboPalette(document, document.LienCouleurDefaut, "Gris (défaut)", "#6A665E", false);
            AjouterLigne(liensGrille, 0, "Couleur :", AvecAutre(document, couleurLien));
            ComboBox epaisseurLien = ComboNombres(
                new double[] { 1, 2, 3, 5 },
                new string[] { "Fin (1)", "Normal (2)", "Épais (3)", "Très épais (5)" },
                document.LienEpaisseurDefaut);
            AjouterLigne(liensGrille, 1, "Épaisseur :", epaisseurLien);
            ComboBox traitLien = ComboChoix(new string[,]
                { { "Plein", "plein" }, { "Tirets", "tirets" }, { "Pointillé", "points" } },
                document.LienTraitDefaut);
            AjouterLigne(liensGrille, 2, "Trait :", traitLien);
            ComboBox flecheLien = ComboChoix(new string[,]
                { { "Sans flèche", "aucune" }, { "Vers la cible", "simple" }, { "Les deux sens", "double" } },
                document.LienFlecheDefaut);
            AjouterLigne(liensGrille, 3, "Flèches :", flecheLien);
            ComboBox traceLien = ComboChoix(new string[,]
                { { "Droit", "droit" }, { "Courbe", "courbe" }, { "Équerre", "equerre" } },
                document.LienTraceDefaut);
            AjouterLigne(liensGrille, 4, "Tracé :", traceLien);
            onglets.Items.Add(new TabItem { Header = "Liens", Content = liensGrille });

            // --- Groupes (style des nouveaux groupes) ----------------------------------------
            Grid groupes = NouvelleGrille(3);
            ComboBox couleurGroupe = ComboPalette(document, document.GroupeCouleurDefaut, "Gris bleuté (défaut)", "#8A94A8", false);
            AjouterLigne(groupes, 0, "Couleur du cadre :", AvecAutre(document, couleurGroupe));
            ComboBox traitGroupe = ComboChoix(new string[,]
                { { "Tirets", "tirets" }, { "Plein", "plein" }, { "Pointillé", "points" } },
                document.GroupeTraitDefaut);
            AjouterLigne(groupes, 1, "Trait du cadre :", traitGroupe);
            ComboBox epaisseurGroupe = ComboNombres(
                new double[] { 1, 1.5, 2.5, 4 },
                new string[] { "Fine (1)", "Normale (1,5)", "Moyenne (2,5)", "Épaisse (4)" },
                document.GroupeEpaisseurDefaut);
            AjouterLigne(groupes, 2, "Épaisseur du cadre :", epaisseurGroupe);
            onglets.Items.Add(new TabItem { Header = "Groupes", Content = groupes });

            // --- Boutons -------------------------------------------------------------------
            StackPanel boutons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 12, 10)
            };
            Button valider = new Button { Content = "Appliquer", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            Button annuler = new Button { Content = "Annuler", Width = 90, IsCancel = true };
            valider.Click += delegate { dialogue.DialogResult = true; };
            boutons.Children.Add(valider);
            boutons.Children.Add(annuler);

            DockPanel racine = new DockPanel();
            DockPanel.SetDock(boutons, Dock.Bottom);
            racine.Children.Add(boutons);
            racine.Children.Add(onglets);
            dialogue.Content = racine;

            if (dialogue.ShowDialog() != true) return false;

            document.NomAffichage = nom.Text.Trim();
            document.Auteur = auteur.Text.Trim();
            document.SauvegardeAuto = sauvegardeAuto.IsChecked == true;
            ComboBoxItem choixFond = fond.SelectedItem as ComboBoxItem;
            if (choixFond != null) document.FondCouleur = (string)choixFond.Tag;
            ComboBoxItem choixMotif = motif.SelectedItem as ComboBoxItem;
            if (choixMotif != null) document.FondMotif = (string)choixMotif.Tag;
            double? nouveauPadding = DialogueSaisie.InterpreterTaille(padding.Text);
            if (nouveauPadding.HasValue)
                document.PaddingBoites = Math.Max(2, Math.Min(40, nouveauPadding.Value));
            string nouvellePolice = (police.SelectedItem as string) ?? police.Text;
            if (!string.IsNullOrWhiteSpace(nouvellePolice))
            {
                foreach (string nomPolice in TexteRiche.PolicesSysteme())
                    if (string.Equals(nomPolice, nouvellePolice.Trim(), StringComparison.CurrentCultureIgnoreCase))
                    {
                        document.PoliceParDefaut = nomPolice;
                        break;
                    }
            }
            double? nouvelleTaille = DialogueSaisie.InterpreterTaille(taille.Text);
            if (nouvelleTaille.HasValue) document.TailleParDefaut = nouvelleTaille.Value;
            ComboBoxItem choixCouleur = couleur.SelectedItem as ComboBoxItem;
            if (choixCouleur != null && choixCouleur.Tag is string)
                document.CouleurParDefaut = (string)choixCouleur.Tag;

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
            ComboBox combo = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
            combo.Items.Add(ElementCouleur(premierNom, premierHexa));
            if (avecTransparent) combo.Items.Add(ElementCouleur("Transparent", "Transparent"));
            for (int i = 0; i < TexteRiche.Surlignages.GetLength(0); i++)
                combo.Items.Add(ElementCouleur(TexteRiche.Surlignages[i, 0], TexteRiche.Surlignages[i, 1]));
            for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                combo.Items.Add(ElementCouleur(TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1]));
            foreach (string hexa in document.CouleursPersonnalisees)
                combo.Items.Add(ElementCouleur(hexa, hexa));
            SelectionnerCouleur(combo, actuel);
            if (combo.SelectedIndex < 0 && !string.IsNullOrEmpty(actuel))
            {
                combo.Items.Add(ElementCouleur(actuel, actuel));
                combo.SelectedIndex = combo.Items.Count - 1;
            }
            if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
            return combo;
        }

        /// <summary>Ajoute un bouton « Autre… » (sélecteur de couleur maison) à côté d'un combo.</summary>
        private static StackPanel AvecAutre(DocumentCarte document, ComboBox combo)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            ligne.Children.Add(combo);
            Button autre = new Button { Content = "Autre…", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0) };
            autre.Click += delegate
            {
                string hexa = ChoisirCouleur();
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
            ComboBox combo = new ComboBox { Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 0; i < paires.GetLength(0); i++)
                combo.Items.Add(new ComboBoxItem { Content = paires[i, 0], Tag = paires[i, 1] });
            SelectionnerCouleur(combo, actuel);
            if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
            return combo;
        }

        /// <summary>Combo de valeurs numériques nommées ; la valeur actuelle est ajoutée
        /// telle quelle si elle ne figure pas dans la liste.</summary>
        private static ComboBox ComboNombres(double[] valeurs, string[] noms, double actuel)
        {
            ComboBox combo = new ComboBox { Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
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

        private static FrameworkElement ConstruireMiniature(
            System.Collections.Generic.List<Rect> rectangles, DocumentCarte document)
        {
            Canvas apercu = new Canvas
            {
                Width = 220,
                Height = 120,
                Background = new SolidColorBrush(TexteRiche.CouleurDepuis(
                    document.FondCouleur, Color.FromRgb(0xF7, 0xF5, 0xF0))),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            if (rectangles != null && rectangles.Count > 0)
            {
                Rect monde = rectangles[0];
                foreach (Rect r in rectangles) monde.Union(r);
                monde.Inflate(30, 30);
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
            Border cadre = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD5, 0xD0, 0xC8)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = apercu
            };
            return cadre;
        }

        private static string ChoisirCouleur()
        {
            return SelecteurCouleur.Choisir(Application.Current.MainWindow, null);
        }

        private static Grid NouvelleGrille(int rangs)
        {
            Grid grille = new Grid { Margin = new Thickness(16) };
            grille.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grille.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < rangs; i++)
                grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            return grille;
        }

        private static void AjouterLigne(Grid grille, int rang, string etiquette, FrameworkElement champ)
        {
            TextBlock texte = new TextBlock
            {
                Text = etiquette,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 10)
            };
            Grid.SetRow(texte, rang);
            Grid.SetColumn(texte, 0);
            grille.Children.Add(texte);
            champ.Margin = new Thickness(0, 0, 0, 10);
            Grid.SetRow(champ, rang);
            Grid.SetColumn(champ, 1);
            grille.Children.Add(champ);
        }

        private static ComboBoxItem ElementCouleur(string nomAffiche, string hexa)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            ligne.Children.Add(new Rectangle
            {
                Width = 12, Height = 12, RadiusX = 2, RadiusY = 2,
                Margin = new Thickness(0, 0, 5, 0),
                Fill = new SolidColorBrush(TexteRiche.CouleurDepuis(hexa, Colors.Gray))
            });
            ligne.Children.Add(new TextBlock { Text = nomAffiche });
            return new ComboBoxItem { Content = ligne, Tag = hexa };
        }

        private static ListBoxItem ElementCouleurListe(string hexa)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            ligne.Children.Add(new Rectangle
            {
                Width = 12, Height = 12, RadiusX = 2, RadiusY = 2,
                Margin = new Thickness(0, 0, 5, 0),
                Fill = new SolidColorBrush(TexteRiche.CouleurDepuis(hexa, Colors.Gray))
            });
            ligne.Children.Add(new TextBlock { Text = hexa });
            return new ListBoxItem { Content = ligne, Tag = hexa };
        }

        private static void SelectionnerCouleur(ComboBox combo, string valeur)
        {
            foreach (object objet in combo.Items)
            {
                ComboBoxItem element = objet as ComboBoxItem;
                if (element != null && (string)element.Tag == valeur)
                {
                    combo.SelectedItem = element;
                    return;
                }
            }
        }
    }
}
