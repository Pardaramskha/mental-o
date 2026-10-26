using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Parametres;

namespace Mentalo.Vue
{
    /// <summary>Fichier > Paramètres : réglages de l'application, dont les raccourcis
    /// re-liables et leurs favoris (affichés dans le Tust). Le même contenu (onglets)
    /// sert à Marabook, dans son onglet « Mental-O (dlc) » des Préférences.</summary>
    public static class DialogueParametres
    {
        private static Action _actualiserConflits;

        /// <summary>Posé par l'application : bascule le thème clair/sombre à la volée.</summary>
        public static Action<bool> AppliquerTheme;

        public static async void Afficher(Window proprietaire, Action apresFermeture)
        {
            Dialogue dialogue = new Dialogue("Paramètres");
            dialogue.SizeToContent = SizeToContent.Manual;
            dialogue.Width = 620;
            dialogue.Height = 600;
            dialogue.CanResize = true;
            dialogue.Content = ConstruireOnglets(dialogue, true);
            await dialogue.Afficher(proprietaire);
            _actualiserConflits = null;
            ParametresGlobaux.Enregistrer();
            if (apresFermeture != null) apresFermeture();
        }

        /// <summary>Les onglets (Raccourcis, Édition, Apparence), réutilisables par un hôte.
        /// « avecTheme » : le choix clair/sombre n'a de sens que pour Mental-O seul.</summary>
        public static TabControl ConstruireOnglets(Visual proprietaire, bool avecTheme)
        {
            TabControl onglets = new TabControl();
            onglets.Items.Add(new TabItem { Header = "Raccourcis", Content = ConstruireOngletRaccourcis(proprietaire) });
            onglets.Items.Add(new TabItem { Header = "Édition", Content = ConstruireOngletEdition() });
            onglets.Items.Add(new TabItem { Header = "Apparence", Content = ConstruireOngletApparence(avecTheme) });
            return onglets;
        }

        private static StackPanel ConstruireOngletEdition()
        {
            StackPanel panneau = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
            CheckBox listesAuto = new CheckBox
            {
                Content = "Créer automatiquement une liste en tapant « * » ou « - » puis espace en début de ligne",
                IsChecked = ParametresGlobaux.ListesAutomatiques
            };
            listesAuto.Click += delegate { ParametresGlobaux.ListesAutomatiques = listesAuto.IsChecked == true; };
            panneau.Children.Add(listesAuto);

            CheckBox magnetisme = new CheckBox
            {
                Content = "Magnétisme et guides d'alignement pendant le déplacement (Alt pour ignorer)",
                IsChecked = ParametresGlobaux.Magnetisme
            };
            magnetisme.Click += delegate { ParametresGlobaux.Magnetisme = magnetisme.IsChecked == true; };
            panneau.Children.Add(magnetisme);

            panneau.Children.Add(Ui.TexteDoux("Le correcteur orthographique de l'ancien Mental-o reposait sur Windows ; "
                + "il n'existe pas dans cette version (le dictionnaire personnel est conservé pour plus tard)."));
            return panneau;
        }

        private static StackPanel ConstruireOngletApparence(bool avecTheme)
        {
            StackPanel panneau = new StackPanel { Margin = new Thickness(16) };

            panneau.Children.Add(new TextBlock { Text = "Taille des poignées :", Margin = new Thickness(0, 0, 0, 4) });
            ComboBox poignees = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
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
            ComboBox zoom = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
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
            vignette.Click += delegate { ParametresGlobaux.VignetteTableau = vignette.IsChecked == true; };
            panneau.Children.Add(vignette);

            if (avecTheme)
            {
                CheckBox sombre = new CheckBox
                {
                    Content = "Thème sombre",
                    IsChecked = ParametresGlobaux.ThemeSombre,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                sombre.Click += delegate
                {
                    ParametresGlobaux.ThemeSombre = sombre.IsChecked == true;
                    if (AppliquerTheme != null) AppliquerTheme(ParametresGlobaux.ThemeSombre);
                };
                panneau.Children.Add(sombre);
            }
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

        private static ScrollViewer ConstruireOngletRaccourcis(Visual proprietaire)
        {
            StackPanel liste = new StackPanel { Margin = new Thickness(14) };
            TextBlock invite = Ui.TexteDoux("Cliquez dans une case puis tapez le raccourci souhaité. ↺ rétablit le défaut, ★ ajoute au Tust des raccourcis.");
            invite.Margin = new Thickness(0, 0, 0, 6);
            liste.Children.Add(invite);

            TextBlock conflits = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Chrome.Danger,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _actualiserConflits = delegate
            {
                List<string> lignes = ParametresGlobaux.DecrireConflits();
                conflits.Text = lignes.Count == 0 ? "" : "⚠ Conflits : " + string.Join(" — ", lignes.ToArray());
                conflits.IsVisible = lignes.Count > 0;
            };
            _actualiserConflits();
            liste.Children.Add(conflits);

            StackPanel profils = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8), Spacing = 6 };
            Button exporter = new Button { Content = "Exporter le profil…" };
            exporter.Click += async delegate
            {
                string chemin = await Ui.EnregistrerSous(proprietaire, "Exporter le profil de raccourcis", "raccourcis-mental-o", "json",
                    Ui.Type("Profil de raccourcis", "*.json"));
                if (chemin != null) ParametresGlobaux.ExporterProfil(chemin);
            };
            Button importer = new Button { Content = "Importer…" };
            importer.Click += async delegate
            {
                string chemin = await Ui.ChoisirFichier(proprietaire, "Importer un profil de raccourcis", Ui.Type("Profil de raccourcis", "*.json"));
                if (chemin == null) return;
                Window fenetre = Ui.Fenetre(proprietaire);
                if (ParametresGlobaux.ImporterProfil(chemin))
                {
                    await DialogueMessage.Afficher(fenetre, "Profil importé et appliqué.", "Paramètres",
                        BoutonsMessage.OK, IconeMessage.Information);
                    Dialogue dialogue = fenetre as Dialogue;
                    if (dialogue != null) dialogue.Annuler();
                }
                else
                {
                    await DialogueMessage.Afficher(fenetre, "Ce fichier n'est pas un profil de raccourcis valide.",
                        "Paramètres", BoutonsMessage.OK, IconeMessage.Avertissement);
                }
            };
            profils.Children.Add(exporter);
            profils.Children.Add(importer);
            liste.Children.Add(profils);

            string categorieCourante = null;
            foreach (DefinitionAction action in ParametresGlobaux.Actions)
            {
                if (ParametresGlobaux.EstMasquee(action.Id)) continue; // écartée par l'hôte (DLC)
                if (action.Categorie != categorieCourante)
                {
                    categorieCourante = action.Categorie;
                    liste.Children.Add(new TextBlock
                    {
                        Text = categorieCourante,
                        FontWeight = FontWeight.Bold,
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
            ligne.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(230)));
            ligne.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(150)));
            ligne.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ligne.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock nom = new TextBlock { Text = action.Nom, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nom, 0);
            ligne.Children.Add(nom);

            TextBox capture = new TextBox
            {
                IsReadOnly = true,
                Width = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(action.Id))
            };
            Ui.Infobulle(capture, "Cliquez puis tapez le nouveau raccourci (Échap pour annuler)");
            string id = action.Id;
            capture.AddHandler(InputElement.KeyDownEvent, delegate(object expediteur, KeyEventArgs e)
            {
                e.Handled = true;
                Key touche = e.Key;
                if (touche == Key.Escape) { return; }
                if (touche == Key.LeftCtrl || touche == Key.RightCtrl || touche == Key.LeftShift
                    || touche == Key.RightShift || touche == Key.LeftAlt || touche == Key.RightAlt
                    || touche == Key.LWin || touche == Key.RWin || touche == Key.Tab)
                    return;
                string geste = ParametresGlobaux.ComposerGeste(touche, e.KeyModifiers);
                ParametresGlobaux.Raccourcis[id] = geste;
                capture.Text = ParametresGlobaux.AfficherGeste(geste);
                if (_actualiserConflits != null) _actualiserConflits();
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Grid.SetColumn(capture, 1);
            ligne.Children.Add(capture);

            Button reinitialiser = new Button
            {
                Content = "↺",
                Width = 30,
                MinWidth = 30,
                Padding = new Thickness(0, 2),
                Margin = new Thickness(6, 0, 0, 0)
            };
            Ui.Infobulle(reinitialiser, "Rétablir le raccourci par défaut (" + ParametresGlobaux.AfficherGeste(action.GesteParDefaut) + ")");
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
                Width = 30,
                MinWidth = 30,
                Padding = new Thickness(0, 2),
                Margin = new Thickness(6, 0, 0, 0),
                IsChecked = ParametresGlobaux.Favoris.Contains(id)
            };
            Ui.Infobulle(favori, "Favori — affiché dans le Tust des raccourcis");
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
