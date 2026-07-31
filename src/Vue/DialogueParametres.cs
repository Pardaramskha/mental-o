using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Mentalo.Parametres;

namespace Mentalo.Vue
{
    /// <summary>Fichier > Paramètres : réglages de l'application, dont les raccourcis
    /// re-liables et leurs favoris (affichés dans le Tust).</summary>
    public static class DialogueParametres
    {
        private static Action _actualiserConflits;

        public static void Afficher(Window proprietaire)
        {
            Window dialogue = new Window();
            dialogue.Title = "Paramètres";
            dialogue.Owner = proprietaire;
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialogue.Width = 580;
            dialogue.Height = 560;
            dialogue.ShowInTaskbar = false;

            TabControl onglets = new TabControl();
            onglets.Items.Add(new TabItem { Header = "Raccourcis", Content = ConstruireOngletRaccourcis(dialogue) });
            onglets.Items.Add(new TabItem { Header = "Édition", Content = ConstruireOngletEdition() });
            onglets.Items.Add(new TabItem { Header = "Apparence", Content = ConstruireOngletApparence() });
            dialogue.Content = onglets;

            dialogue.ShowDialog();
            _actualiserConflits = null;
            ParametresGlobaux.Enregistrer();
        }

        private static StackPanel ConstruireOngletEdition()
        {
            StackPanel panneau = new StackPanel { Margin = new Thickness(16) };
            CheckBox listesAuto = new CheckBox
            {
                Content = "Créer automatiquement une liste en tapant « * » ou « - » puis espace en début de ligne",
                IsChecked = ParametresGlobaux.ListesAutomatiques
            };
            listesAuto.Click += delegate
            {
                ParametresGlobaux.ListesAutomatiques = listesAuto.IsChecked == true;
            };
            panneau.Children.Add(listesAuto);

            CheckBox magnetisme = new CheckBox
            {
                Content = "Magnétisme et guides d'alignement pendant le déplacement (Alt pour ignorer)",
                IsChecked = ParametresGlobaux.Magnetisme,
                Margin = new Thickness(0, 10, 0, 0)
            };
            magnetisme.Click += delegate
            {
                ParametresGlobaux.Magnetisme = magnetisme.IsChecked == true;
            };
            panneau.Children.Add(magnetisme);
            return panneau;
        }

        private static StackPanel ConstruireOngletApparence()
        {
            StackPanel panneau = new StackPanel { Margin = new Thickness(16) };

            panneau.Children.Add(new TextBlock { Text = "Taille des poignées :", Margin = new Thickness(0, 0, 0, 4) });
            ComboBox poignees = new ComboBox { Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            poignees.Items.Add(new ComboBoxItem { Content = "Petites", Tag = "petite" });
            poignees.Items.Add(new ComboBoxItem { Content = "Normales", Tag = "normale" });
            poignees.Items.Add(new ComboBoxItem { Content = "Grandes", Tag = "grande" });
            SelectionnerTag(poignees, ParametresGlobaux.TaillePoignees);
            poignees.SelectionChanged += delegate
            {
                ComboBoxItem choix = poignees.SelectedItem as ComboBoxItem;
                if (choix != null) ParametresGlobaux.TaillePoignees = (string)choix.Tag;
            };
            panneau.Children.Add(poignees);

            panneau.Children.Add(new TextBlock { Text = "Vitesse du zoom (Ctrl+molette) :", Margin = new Thickness(0, 12, 0, 4) });
            ComboBox zoom = new ComboBox { Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            zoom.Items.Add(new ComboBoxItem { Content = "Lente", Tag = "lente" });
            zoom.Items.Add(new ComboBoxItem { Content = "Normale", Tag = "normale" });
            zoom.Items.Add(new ComboBoxItem { Content = "Rapide", Tag = "rapide" });
            SelectionnerTag(zoom, ParametresGlobaux.VitesseZoom);
            zoom.SelectionChanged += delegate
            {
                ComboBoxItem choix = zoom.SelectedItem as ComboBoxItem;
                if (choix != null) ParametresGlobaux.VitesseZoom = (string)choix.Tag;
            };
            panneau.Children.Add(zoom);

            CheckBox vignette = new CheckBox
            {
                Content = "Ombre en vignette sur les bords du tableau",
                IsChecked = ParametresGlobaux.VignetteTableau,
                Margin = new Thickness(0, 14, 0, 0)
            };
            vignette.Click += delegate
            {
                ParametresGlobaux.VignetteTableau = vignette.IsChecked == true;
            };
            panneau.Children.Add(vignette);
            return panneau;
        }

        private static void SelectionnerTag(ComboBox combo, string valeur)
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
            if (combo.Items.Count > 1) combo.SelectedIndex = 1;
        }

        private static ScrollViewer ConstruireOngletRaccourcis(Window dialogue)
        {
            StackPanel liste = new StackPanel { Margin = new Thickness(14) };
            liste.Children.Add(new TextBlock
            {
                Text = "Cliquez dans une case puis tapez le raccourci souhaité. ↺ rétablit le défaut, ★ ajoute au Tust des raccourcis.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Chrome.TexteDoux,
                Margin = new Thickness(0, 0, 0, 6)
            });

            TextBlock conflits = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _actualiserConflits = delegate
            {
                System.Collections.Generic.List<string> lignes = ParametresGlobaux.DecrireConflits();
                conflits.Text = lignes.Count == 0 ? ""
                    : "⚠ Conflits : " + string.Join(" — ", lignes.ToArray());
                conflits.Visibility = lignes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            };
            _actualiserConflits();
            liste.Children.Add(conflits);

            StackPanel profils = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            Button exporter = new Button { Content = "Exporter le profil…", Padding = new Thickness(8, 1, 8, 1) };
            exporter.Click += delegate
            {
                Microsoft.Win32.SaveFileDialog enregistreur = new Microsoft.Win32.SaveFileDialog();
                enregistreur.Filter = "Profil de raccourcis (*.json)|*.json";
                enregistreur.FileName = "raccourcis-mental-o";
                if (enregistreur.ShowDialog(dialogue) == true)
                    ParametresGlobaux.ExporterProfil(enregistreur.FileName);
            };
            Button importer = new Button { Content = "Importer…", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            importer.Click += delegate
            {
                Microsoft.Win32.OpenFileDialog ouvreur = new Microsoft.Win32.OpenFileDialog();
                ouvreur.Filter = "Profil de raccourcis (*.json)|*.json";
                if (ouvreur.ShowDialog(dialogue) != true) return;
                if (ParametresGlobaux.ImporterProfil(ouvreur.FileName))
                {
                    MessageBox.Show(dialogue, "Profil importé et appliqué.", "Paramètres",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    dialogue.Close();
                }
                else
                {
                    MessageBox.Show(dialogue, "Ce fichier n'est pas un profil de raccourcis valide.",
                        "Paramètres", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            profils.Children.Add(exporter);
            profils.Children.Add(importer);
            liste.Children.Add(profils);

            string categorieCourante = null;
            foreach (DefinitionAction action in ParametresGlobaux.Actions)
            {
                if (action.Categorie != categorieCourante)
                {
                    categorieCourante = action.Categorie;
                    liste.Children.Add(new TextBlock
                    {
                        Text = categorieCourante,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 8, 0, 4)
                    });
                }
                liste.Children.Add(LigneAction(action));
            }

            ScrollViewer defilement = new ScrollViewer();
            defilement.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            defilement.Content = liste;
            return defilement;
        }

        private static Grid LigneAction(DefinitionAction action)
        {
            Grid ligne = new Grid { Margin = new Thickness(8, 2, 0, 2) };
            ligne.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            ligne.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            ligne.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ligne.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock nom = new TextBlock { Text = action.Nom, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nom, 0);
            ligne.Children.Add(nom);

            TextBox capture = new TextBox
            {
                IsReadOnly = true,
                Width = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(action.Id)),
                ToolTip = "Cliquez puis tapez le nouveau raccourci (Échap pour annuler)"
            };
            string id = action.Id;
            capture.PreviewKeyDown += delegate(object expediteur, KeyEventArgs e)
            {
                e.Handled = true;
                Key touche = e.Key == Key.System ? e.SystemKey : e.Key;
                if (touche == Key.Escape) { Keyboard.ClearFocus(); return; }
                if (touche == Key.LeftCtrl || touche == Key.RightCtrl || touche == Key.LeftShift
                    || touche == Key.RightShift || touche == Key.LeftAlt || touche == Key.RightAlt
                    || touche == Key.LWin || touche == Key.RWin || touche == Key.Tab)
                    return;
                string geste = ParametresGlobaux.ComposerGeste(touche, Keyboard.Modifiers);
                ParametresGlobaux.Raccourcis[id] = geste;
                capture.Text = ParametresGlobaux.AfficherGeste(geste);
                if (_actualiserConflits != null) _actualiserConflits();
            };
            Grid.SetColumn(capture, 1);
            ligne.Children.Add(capture);

            Button reinitialiser = new Button
            {
                Content = "↺",
                Width = 26,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = "Rétablir le raccourci par défaut (" + ParametresGlobaux.AfficherGeste(action.GesteParDefaut) + ")"
            };
            reinitialiser.Click += delegate
            {
                ParametresGlobaux.Raccourcis.Remove(id);
                capture.Text = ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(id));
                if (_actualiserConflits != null) _actualiserConflits();
            };
            Grid.SetColumn(reinitialiser, 2);
            ligne.Children.Add(reinitialiser);

            ToggleButton favori = new ToggleButton
            {
                Content = "★",
                Width = 26,
                Margin = new Thickness(6, 0, 0, 0),
                IsChecked = ParametresGlobaux.Favoris.Contains(id),
                ToolTip = "Favori — affiché dans le Tust des raccourcis"
            };
            favori.Click += delegate
            {
                if (favori.IsChecked == true) ParametresGlobaux.Favoris.Add(id);
                else ParametresGlobaux.Favoris.Remove(id);
            };
            Grid.SetColumn(favori, 3);
            ligne.Children.Add(favori);

            return ligne;
        }
    }
}
