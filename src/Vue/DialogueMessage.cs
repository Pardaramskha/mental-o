using System.Windows;
using System.Windows.Controls;

namespace Mentalo.Vue
{
    /// <summary>Le remplaçant maison de MessageBox, calqué sur celui de Marabook : la boîte
    /// native Win32 ignorait le thème (boutons carrés, icône système, couleurs Windows).
    /// Celle-ci est une Window ordinaire, habillée par le style implicite comme tous les
    /// dialogues de l'application — fond et encre du chrome, boutons arrondis, aucune icône.
    /// Même signature que MessageBox.Show(owner, texte, titre, boutons, icône) → MessageBoxResult ;
    /// l'icône n'est acceptée que pour la compatibilité des appels et n'est jamais dessinée.
    /// Fermer par la croix ou Échap rend le refus le plus sûr (Non ou Annuler selon les
    /// boutons, OK quand il n'y a que lui).</summary>
    public class DialogueMessage : Window
    {
        private MessageBoxResult _resultat;

        private DialogueMessage(Window proprietaire, string texte, string titre, MessageBoxButton boutons)
        {
            Title = titre ?? "Mental-o";
            if (proprietaire != null)
            {
                Owner = proprietaire;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = Chrome.FondFenetre;
            Foreground = Chrome.Encre;
            _resultat = ResultatParDefaut(boutons);

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
                Margin = new Thickness(0, 16, 0, 0)
            };
            if (boutons == MessageBoxButton.OK || boutons == MessageBoxButton.OKCancel)
                rangee.Children.Add(Choix("OK", MessageBoxResult.OK, true, boutons == MessageBoxButton.OK));
            if (boutons == MessageBoxButton.YesNo || boutons == MessageBoxButton.YesNoCancel)
            {
                rangee.Children.Add(Choix("Oui", MessageBoxResult.Yes, true, false));
                rangee.Children.Add(Choix("Non", MessageBoxResult.No, false, boutons == MessageBoxButton.YesNo));
            }
            if (boutons == MessageBoxButton.OKCancel || boutons == MessageBoxButton.YesNoCancel)
                rangee.Children.Add(Choix("Annuler", MessageBoxResult.Cancel, false, true));
            panneau.Children.Add(rangee);
            Content = panneau;
        }

        /// <summary>Le résultat quand on ferme sans choisir (croix, Échap).</summary>
        private static MessageBoxResult ResultatParDefaut(MessageBoxButton boutons)
        {
            switch (boutons)
            {
                case MessageBoxButton.OK: return MessageBoxResult.OK;
                case MessageBoxButton.YesNo: return MessageBoxResult.No;
                default: return MessageBoxResult.Cancel;
            }
        }

        private Button Choix(string libelle, MessageBoxResult resultat, bool parDefaut, bool annulation)
        {
            Button bouton = new Button
            {
                Content = libelle,
                MinWidth = 84,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = parDefaut,
                IsCancel = annulation
            };
            if (parDefaut) bouton.FontWeight = FontWeights.SemiBold;
            bouton.Click += delegate { _resultat = resultat; Close(); };
            return bouton;
        }

        /// <summary>Affiche le message et rend le bouton choisi. Signature identique à
        /// MessageBox.Show pour que les appels existants se remplacent mot pour mot.</summary>
        public static MessageBoxResult Afficher(Window proprietaire, string texte, string titre,
            MessageBoxButton boutons, MessageBoxImage icone)
        {
            DialogueMessage dialogue = new DialogueMessage(proprietaire, texte, titre, boutons);
            dialogue.ShowDialog();
            return dialogue._resultat;
        }

        public static MessageBoxResult Afficher(Window proprietaire, string texte, string titre, MessageBoxButton boutons)
        {
            return Afficher(proprietaire, texte, titre, boutons, MessageBoxImage.None);
        }
    }
}
