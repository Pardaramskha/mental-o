using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Gestion des étiquettes du fichier (v0.15) : créer, renommer, recolorer,
    /// supprimer. Chaque action passe par la toile, donc s'annule (Ctrl+Z après fermeture).</summary>
    public static class DialogueEtiquettes
    {
        public static void Ouvrir(Window proprietaire, ToileVue toile)
        {
            Window dialogue = new Window();
            dialogue.Title = "Étiquettes du fichier";
            dialogue.Owner = proprietaire;
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialogue.Width = 420;
            dialogue.Height = 380;
            dialogue.ResizeMode = ResizeMode.NoResize;
            dialogue.WindowStyle = WindowStyle.ToolWindow;
            dialogue.ShowInTaskbar = false;

            Grid grille = new Grid { Margin = new Thickness(14) };
            grille.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grille.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grille.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock invite = new TextBlock
            {
                Text = "Les étiquettes classent les boîtes sans hiérarchie ; le filtre (menu Fenêtres) estompe ce qui ne correspond pas.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Chrome.TexteDoux,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetColumnSpan(invite, 2);
            grille.Children.Add(invite);

            ListBox liste = new ListBox { Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetRow(liste, 1);
            grille.Children.Add(liste);

            StackPanel boutons = new StackPanel { Width = 120 };
            Grid.SetRow(boutons, 1);
            Grid.SetColumn(boutons, 1);
            grille.Children.Add(boutons);

            System.Action remplir = delegate
            {
                string selection = liste.SelectedItem is ListBoxItem ? (string)((ListBoxItem)liste.SelectedItem).Tag : null;
                liste.Items.Clear();
                foreach (Etiquette e in toile.Document.Etiquettes)
                {
                    StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
                    ligne.Children.Add(new Border
                    {
                        Width = 14, Height = 14, CornerRadius = new CornerRadius(7),
                        Background = new SolidColorBrush(TexteRiche.CouleurDepuis(e.Couleur, Colors.SteelBlue)),
                        Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
                    });
                    ligne.Children.Add(new TextBlock { Text = e.Nom, VerticalAlignment = VerticalAlignment.Center });
                    int porteurs = toile.CompterPorteurs(e.Nom);
                    ligne.Children.Add(new TextBlock
                    {
                        Text = porteurs == 0 ? "  (aucune boîte)" : porteurs == 1 ? "  (1 boîte)" : "  (" + porteurs + " boîtes)",
                        Foreground = Chrome.TexteDoux, VerticalAlignment = VerticalAlignment.Center
                    });
                    ListBoxItem element = new ListBoxItem { Content = ligne, Tag = e.Nom };
                    liste.Items.Add(element);
                    if (e.Nom == selection) liste.SelectedItem = element;
                }
                if (liste.SelectedItem == null && liste.Items.Count > 0) liste.SelectedIndex = 0;
            };

            System.Func<string> nomSelectionne = delegate
            {
                ListBoxItem element = liste.SelectedItem as ListBoxItem;
                return element == null ? null : (string)element.Tag;
            };

            Button nouvelle = Bouton("Nouvelle…");
            nouvelle.Click += delegate
            {
                string nom = DialogueSaisie.DemanderTexte(dialogue, "Nouvelle étiquette", "Nom de l'étiquette :", "");
                if (string.IsNullOrWhiteSpace(nom)) return;
                toile.CreerEtiquette(nom.Trim(), null);
                remplir();
            };
            boutons.Children.Add(nouvelle);

            Button renommer = Bouton("Renommer…");
            renommer.Click += delegate
            {
                string ancien = nomSelectionne();
                if (ancien == null) return;
                string nouveau = DialogueSaisie.DemanderTexte(dialogue, "Renommer l'étiquette", "Nouveau nom :", ancien);
                if (string.IsNullOrWhiteSpace(nouveau)) return;
                toile.RenommerEtiquette(ancien, nouveau.Trim());
                remplir();
            };
            boutons.Children.Add(renommer);

            Button couleur = Bouton("Couleur…");
            couleur.Click += delegate
            {
                string nom = nomSelectionne();
                if (nom == null) return;
                Etiquette e = toile.Document.TrouverEtiquette(nom);
                string hexa = SelecteurCouleur.Choisir(dialogue, e == null ? "#0078D7" : e.Couleur);
                if (hexa == null) return;
                toile.ColorerEtiquette(nom, hexa);
                remplir();
            };
            boutons.Children.Add(couleur);

            Button supprimer = Bouton("Supprimer");
            supprimer.Click += delegate
            {
                string nom = nomSelectionne();
                if (nom == null) return;
                int porteurs = toile.CompterPorteurs(nom);
                if (porteurs > 0)
                {
                    MessageBoxResult reponse = DialogueMessage.Afficher(dialogue,
                        string.Format("Supprimer l'étiquette « {0} » ? Elle sera retirée de {1} boîte{2} (annulable).",
                            nom, porteurs, porteurs > 1 ? "s" : ""),
                        "Étiquettes", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (reponse != MessageBoxResult.Yes) return;
                }
                toile.SupprimerEtiquette(nom);
                remplir();
            };
            boutons.Children.Add(supprimer);

            Button fermer = new Button { Content = "Fermer", Width = 90, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(fermer, 2);
            Grid.SetColumnSpan(fermer, 2);
            grille.Children.Add(fermer);

            remplir();
            dialogue.Content = grille;
            dialogue.ShowDialog();
        }

        private static Button Bouton(string libelle)
        {
            return new Button { Content = libelle, Margin = new Thickness(0, 0, 0, 6), Height = 28 };
        }
    }
}
