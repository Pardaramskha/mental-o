using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Mentalo.Modele;
using Mentalo.Persistance;

namespace Mentalo.Vue
{
    /// <summary>Fichier > Versions antérieures… (v0.16) : les états précédents conservés dans
    /// le .tea, à restaurer d'un clic. La version restaurée remplace la carte à l'écran (le
    /// fichier n'est réécrit qu'à l'enregistrement suivant).</summary>
    public static class DialogueVersions
    {
        /// <summary>Rend la version choisie, ou null.</summary>
        public static DocumentCarte Ouvrir(Window proprietaire, string cheminFichier)
        {
            List<string> versions;
            try { versions = Fichier.ListerVersions(cheminFichier); }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(proprietaire, "Impossible de lire les versions :\n" + ex.Message,
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            Window dialogue = new Window();
            dialogue.Title = "Versions antérieures";
            dialogue.Owner = proprietaire;
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialogue.Width = 460;
            dialogue.Height = 400;
            dialogue.ResizeMode = ResizeMode.NoResize;
            dialogue.WindowStyle = WindowStyle.ToolWindow;
            dialogue.ShowInTaskbar = false;

            DockPanel racine = new DockPanel { Margin = new Thickness(14) };
            TextBlock invite = new TextBlock
            {
                Text = versions.Count == 0
                    ? "Aucune version antérieure dans ce fichier : elles s'accumulent à chaque enregistrement (au plus "
                      + Fichier.VersionsMax + ", espacées d'au moins dix minutes)."
                    : "Chaque enregistrement conserve l'état précédent dans le fichier (au plus " + Fichier.VersionsMax
                      + "). Restaurer remplace la carte à l'écran ; enregistrez ensuite pour confirmer, ou fermez sans enregistrer pour revenir.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Chrome.TexteDoux,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(invite, Dock.Top);
            racine.Children.Add(invite);

            StackPanel boutons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Button restaurer = new Button { Content = "Restaurer cette version", MinWidth = 160, IsDefault = true, IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
            Button fermer = new Button { Content = "Fermer", Width = 90, IsCancel = true };
            boutons.Children.Add(restaurer);
            boutons.Children.Add(fermer);
            DockPanel.SetDock(boutons, Dock.Bottom);
            racine.Children.Add(boutons);

            ListBox liste = new ListBox();
            foreach (string nom in versions)
            {
                DateTime date;
                string libelle = Fichier.DateDeVersion(nom, out date)
                    ? date.ToString("dddd d MMMM yyyy 'à' HH:mm", CultureInfo.GetCultureInfo("fr-FR"))
                    : nom;
                liste.Items.Add(new ListBoxItem { Content = libelle, Tag = nom });
            }
            liste.SelectionChanged += delegate { restaurer.IsEnabled = liste.SelectedItem != null; };
            if (liste.Items.Count > 0) liste.SelectedIndex = 0;
            racine.Children.Add(liste);

            DocumentCarte resultat = null;
            restaurer.Click += delegate
            {
                ListBoxItem element = liste.SelectedItem as ListBoxItem;
                if (element == null) return;
                try
                {
                    resultat = Fichier.ChargerVersion(cheminFichier, (string)element.Tag);
                    dialogue.DialogResult = true;
                }
                catch (Exception ex)
                {
                    DialogueMessage.Afficher(dialogue, "Impossible de restaurer cette version :\n" + ex.Message,
                        "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            dialogue.Content = racine;
            if (dialogue.ShowDialog() != true) return null;
            return resultat;
        }
    }
}
