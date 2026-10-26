using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mentalo.Vue
{
    /// <summary>Le socle des dialogues de Mental-O sur Avalonia : une fenêtre habillée
    /// (fond et encre du chrome), centrée sur sa propriétaire, qui se ferme avec un
    /// résultat. Pas de dialogue modal bloquant sous Avalonia : tout est attendu.</summary>
    public class Dialogue : Window
    {
        private readonly TaskCompletionSource<bool> _resultat = new TaskCompletionSource<bool>();
        private bool _valide;

        public Dialogue(string titre)
        {
            Title = titre ?? "Mental-O";
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.WidthAndHeight;
            CanResize = false;
            ShowInTaskbar = false;
            Background = Chrome.FondFenetre;
            Foreground = Chrome.Encre;
            // Échap ferme sans valider.
            AddHandler(KeyDownEvent, delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && !e.Handled) { Annuler(); e.Handled = true; }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Closed += delegate { _resultat.TrySetResult(_valide); };
        }

        public void Valider()
        {
            _valide = true;
            Close();
        }

        public void Annuler()
        {
            _valide = false;
            Close();
        }

        /// <summary>Affiche le dialogue et rend vrai s'il a été validé.</summary>
        public Task<bool> Afficher(Window proprietaire)
        {
            if (proprietaire != null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Show(proprietaire);
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Show();
            }
            // Modal à la main : la propriétaire reste visible mais ne réagit plus.
            if (proprietaire != null)
            {
                proprietaire.IsEnabled = false;
                Closed += delegate { proprietaire.IsEnabled = true; proprietaire.Activate(); };
            }
            return _resultat.Task;
        }

        /// <summary>La rangée de boutons standard (Valider à droite, Annuler à sa droite).</summary>
        public StackPanel RangeeBoutons(string libelleValider, string libelleAnnuler, bool validerParDefaut)
        {
            StackPanel rangee = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
                Spacing = 6
            };
            if (libelleValider != null)
            {
                Button valider = new Button { Content = libelleValider, MinWidth = 84, IsDefault = validerParDefaut };
                if (validerParDefaut) valider.FontWeight = FontWeight.SemiBold;
                valider.Click += delegate { Valider(); };
                rangee.Children.Add(valider);
            }
            if (libelleAnnuler != null)
            {
                Button annuler = new Button { Content = libelleAnnuler, MinWidth = 84 };
                annuler.Click += delegate { Annuler(); };
                rangee.Children.Add(annuler);
            }
            return rangee;
        }

        /// <summary>Une ligne « libellé : champ » d'un formulaire en grille.</summary>
        public static void AjouterLigne(Grid grille, int rang, string etiquette, Control champ)
        {
            TextBlock texte = new TextBlock
            {
                Text = etiquette,
                Foreground = Chrome.Encre,
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

        public static Grid NouvelleGrille(int rangs)
        {
            Grid grille = new Grid { Margin = new Thickness(16) };
            grille.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grille.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (int i = 0; i < rangs; i++)
                grille.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            return grille;
        }
    }

    /// <summary>Le remplaçant maison de MessageBox : une fenêtre ordinaire habillée comme
    /// les autres dialogues — fond et encre du chrome, boutons arrondis, aucune icône
    /// (l'icône n'est acceptée que pour la compatibilité des appels). Fermer par la croix
    /// ou Échap rend le refus le plus sûr (Non ou Annuler selon les boutons, OK quand il
    /// n'y a que lui).</summary>
    public static class DialogueMessage
    {
        public static async Task<ResultatMessage> Afficher(Window proprietaire, string texte, string titre,
            BoutonsMessage boutons, IconeMessage icone)
        {
            Dialogue dialogue = new Dialogue(titre ?? "Mental-O");
            ResultatMessage resultat = ResultatParDefaut(boutons);

            StackPanel panneau = new StackPanel { Margin = new Thickness(18, 16, 18, 14), MaxWidth = 520 };
            panneau.Children.Add(new TextBlock
            {
                Text = texte ?? "",
                Foreground = Chrome.Encre,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });

            StackPanel rangee = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                Spacing = 8
            };
            Action<string, ResultatMessage, bool> choix = delegate(string libelle, ResultatMessage valeur, bool parDefaut)
            {
                Button bouton = new Button { Content = libelle, MinWidth = 84, IsDefault = parDefaut };
                if (parDefaut) bouton.FontWeight = FontWeight.SemiBold;
                bouton.Click += delegate { resultat = valeur; dialogue.Valider(); };
                rangee.Children.Add(bouton);
            };
            if (boutons == BoutonsMessage.OK || boutons == BoutonsMessage.OKAnnuler)
                choix("OK", ResultatMessage.OK, true);
            if (boutons == BoutonsMessage.OuiNon || boutons == BoutonsMessage.OuiNonAnnuler)
            {
                choix("Oui", ResultatMessage.Oui, true);
                choix("Non", ResultatMessage.Non, false);
            }
            if (boutons == BoutonsMessage.OKAnnuler || boutons == BoutonsMessage.OuiNonAnnuler)
                choix("Annuler", ResultatMessage.Annuler, false);
            panneau.Children.Add(rangee);
            dialogue.Content = panneau;
            bool valide = await dialogue.Afficher(proprietaire);
            return valide ? resultat : ResultatParDefaut(boutons);
        }

        public static Task<ResultatMessage> Afficher(Window proprietaire, string texte, string titre, BoutonsMessage boutons)
        {
            return Afficher(proprietaire, texte, titre, boutons, IconeMessage.Aucune);
        }

        /// <summary>Le résultat quand on ferme sans choisir (croix, Échap).</summary>
        private static ResultatMessage ResultatParDefaut(BoutonsMessage boutons)
        {
            switch (boutons)
            {
                case BoutonsMessage.OK: return ResultatMessage.OK;
                case BoutonsMessage.OuiNon: return ResultatMessage.Non;
                default: return ResultatMessage.Annuler;
            }
        }

        /// <summary>Branche les messages de l'hôte standard sur ce dialogue.</summary>
        public static void Brancher()
        {
            HoteStandard.AfficherMessage = delegate(object proprietaire, string texte, string titre, BoutonsMessage boutons, IconeMessage icone)
            {
                return Afficher(proprietaire as Window, texte, titre, boutons, icone);
            };
        }
    }
}
