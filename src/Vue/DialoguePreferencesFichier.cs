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
            dialogue.Width = 520;
            dialogue.Height = 460;
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
            return true;
        }

        // ------------------------------------------------------------------ aides

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
