using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Gestion des étiquettes du fichier (v0.15) : créer, renommer, recolorer,
    /// supprimer. Chaque action passe par la toile, donc s'annule (Ctrl+Z après fermeture).</summary>
    public static class DialogueEtiquettes
    {
        public static async void Ouvrir(Window proprietaire, ToileVue toile)
        {
            Dialogue dialogue = new Dialogue("Étiquettes du fichier");
            dialogue.SizeToContent = SizeToContent.Manual;
            dialogue.Width = 440;
            dialogue.Height = 400;

            Grid grille = new Grid { Margin = new Thickness(14) };
            grille.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grille.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grille.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grille.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            grille.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            TextBlock invite = Ui.TexteDoux("Les étiquettes classent les boîtes sans hiérarchie ; le filtre (menu Fenêtres) estompe ce qui ne correspond pas.");
            invite.Margin = new Thickness(0, 0, 0, 8);
            Grid.SetColumnSpan(invite, 2);
            grille.Children.Add(invite);

            ListBox liste = new ListBox { Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetRow(liste, 1);
            grille.Children.Add(liste);

            StackPanel boutons = new StackPanel { Width = 120, Spacing = 6 };
            Grid.SetRow(boutons, 1);
            Grid.SetColumn(boutons, 1);
            grille.Children.Add(boutons);

            Action remplir = delegate
            {
                string selection = liste.SelectedItem is ListBoxItem ? (string)((ListBoxItem)liste.SelectedItem).Tag : null;
                liste.Items.Clear();
                foreach (Etiquette e in toile.Document.Etiquettes)
                {
                    StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    Control pastille = Ui.PastilleRonde(e.Couleur, 14);
                    pastille.VerticalAlignment = VerticalAlignment.Center;
                    ligne.Children.Add(pastille);
                    ligne.Children.Add(new TextBlock { Text = e.Nom, Foreground = Chrome.Encre, VerticalAlignment = VerticalAlignment.Center });
                    int porteurs = toile.CompterPorteurs(e.Nom);
                    ligne.Children.Add(new TextBlock
                    {
                        Text = porteurs == 0 ? "(aucune boîte)" : porteurs == 1 ? "(1 boîte)" : "(" + porteurs + " boîtes)",
                        Foreground = Chrome.TexteDoux, VerticalAlignment = VerticalAlignment.Center
                    });
                    ListBoxItem element = new ListBoxItem { Content = ligne, Tag = e.Nom };
                    liste.Items.Add(element);
                    if (e.Nom == selection) liste.SelectedItem = element;
                }
                if (liste.SelectedItem == null && liste.Items.Count > 0) liste.SelectedIndex = 0;
            };

            Func<string> nomSelectionne = delegate
            {
                ListBoxItem element = liste.SelectedItem as ListBoxItem;
                return element == null ? null : (string)element.Tag;
            };

            boutons.Children.Add(Bouton("Nouvelle…", async delegate
            {
                string nom = await DialogueSaisie.DemanderTexte(dialogue, "Nouvelle étiquette", "Nom de l'étiquette :", "");
                if (string.IsNullOrWhiteSpace(nom)) return;
                toile.CreerEtiquette(nom.Trim(), null);
                remplir();
            }));
            boutons.Children.Add(Bouton("Renommer…", async delegate
            {
                string ancien = nomSelectionne();
                if (ancien == null) return;
                string nouveau = await DialogueSaisie.DemanderTexte(dialogue, "Renommer l'étiquette", "Nouveau nom :", ancien);
                if (string.IsNullOrWhiteSpace(nouveau)) return;
                toile.RenommerEtiquette(ancien, nouveau.Trim());
                remplir();
            }));
            boutons.Children.Add(Bouton("Couleur…", async delegate
            {
                string nom = nomSelectionne();
                if (nom == null) return;
                Etiquette e = toile.Document.TrouverEtiquette(nom);
                string hexa = await SelecteurCouleur.Choisir(dialogue, e == null ? "#0078D7" : e.Couleur);
                if (hexa == null) return;
                toile.ColorerEtiquette(nom, hexa);
                remplir();
            }));
            boutons.Children.Add(Bouton("Supprimer", async delegate
            {
                string nom = nomSelectionne();
                if (nom == null) return;
                int porteurs = toile.CompterPorteurs(nom);
                if (porteurs > 0)
                {
                    ResultatMessage reponse = await DialogueMessage.Afficher(dialogue,
                        string.Format("Supprimer l'étiquette « {0} » ? Elle sera retirée de {1} boîte{2} (annulable).",
                            nom, porteurs, porteurs > 1 ? "s" : ""),
                        "Étiquettes", BoutonsMessage.OuiNon, IconeMessage.Question);
                    if (reponse != ResultatMessage.Oui) return;
                }
                toile.SupprimerEtiquette(nom);
                remplir();
            }));

            StackPanel bas = dialogue.RangeeBoutons(null, "Fermer", false);
            Grid.SetRow(bas, 2);
            Grid.SetColumnSpan(bas, 2);
            grille.Children.Add(bas);

            remplir();
            dialogue.Content = grille;
            await dialogue.Afficher(proprietaire);
        }

        private static Button Bouton(string libelle, Action clic)
        {
            Button bouton = new Button { Content = libelle, Height = 30, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center };
            bouton.Click += delegate { clic(); };
            return bouton;
        }
    }

    /// <summary>Fichier > Versions antérieures… (v0.16) : les états précédents conservés dans
    /// le .tea, à restaurer d'un clic. La version restaurée remplace la carte à l'écran (le
    /// fichier n'est réécrit qu'à l'enregistrement suivant).</summary>
    public static class DialogueVersions
    {
        /// <summary>Rend la version choisie, ou null. « lister » et « charger » viennent du
        /// support (fichier .tea ou octets en mémoire chez Marabook).</summary>
        public static async System.Threading.Tasks.Task<DocumentCarte> Ouvrir(Window proprietaire,
            Func<List<string>> lister, Func<string, DocumentCarte> charger)
        {
            List<string> versions;
            try { versions = lister(); }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(proprietaire, "Impossible de lire les versions :\n" + ex.Message,
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
                return null;
            }

            Dialogue dialogue = new Dialogue("Versions antérieures");
            dialogue.SizeToContent = SizeToContent.Manual;
            dialogue.Width = 480;
            dialogue.Height = 420;

            DockPanel racine = new DockPanel { Margin = new Thickness(14) };
            TextBlock invite = Ui.TexteDoux(versions.Count == 0
                ? "Aucune version antérieure dans ce fichier : elles s'accumulent à chaque enregistrement (au plus "
                  + Persistance.Fichier.VersionsMax + ", espacées d'au moins dix minutes)."
                : "Chaque enregistrement conserve l'état précédent dans le fichier (au plus " + Persistance.Fichier.VersionsMax
                  + "). Restaurer remplace la carte à l'écran ; enregistrez ensuite pour confirmer, ou fermez sans enregistrer pour revenir.");
            invite.Margin = new Thickness(0, 0, 0, 10);
            DockPanel.SetDock(invite, Dock.Top);
            racine.Children.Add(invite);

            DocumentCarte resultat = null;
            ListBox liste = new ListBox();
            StackPanel boutons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Spacing = 6 };
            Button restaurer = new Button { Content = "Restaurer cette version", MinWidth = 160, IsDefault = true, IsEnabled = false };
            Button fermer = new Button { Content = "Fermer", MinWidth = 90 };
            fermer.Click += delegate { dialogue.Annuler(); };
            boutons.Children.Add(restaurer);
            boutons.Children.Add(fermer);
            DockPanel.SetDock(boutons, Dock.Bottom);
            racine.Children.Add(boutons);

            foreach (string nom in versions)
            {
                DateTime date;
                string libelle = Persistance.Fichier.DateDeVersion(nom, out date)
                    ? date.ToString("dddd d MMMM yyyy 'à' HH:mm", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))
                    : nom;
                liste.Items.Add(new ListBoxItem { Content = libelle, Tag = nom });
            }
            liste.SelectionChanged += delegate { restaurer.IsEnabled = liste.SelectedItem != null; };
            if (liste.Items.Count > 0) liste.SelectedIndex = 0;
            racine.Children.Add(liste);

            restaurer.Click += async delegate
            {
                ListBoxItem element = liste.SelectedItem as ListBoxItem;
                if (element == null) return;
                try
                {
                    resultat = charger((string)element.Tag);
                    dialogue.Valider();
                }
                catch (Exception ex)
                {
                    await DialogueMessage.Afficher(dialogue, "Impossible de restaurer cette version :\n" + ex.Message,
                        "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
                }
            };

            dialogue.Content = racine;
            if (!await dialogue.Afficher(proprietaire)) return null;
            return resultat;
        }
    }
}
