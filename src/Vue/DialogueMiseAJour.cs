using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mentalo.Persistance;

namespace Mentalo.Vue
{
    /// <summary>La proposition de mise à jour (v0.18) : « Mental-o X est disponible », les notes de
    /// la release, et « Installer et redémarrer ». Habillée comme les autres dialogues (fond et
    /// encre du chrome, boutons arrondis du thème). Rend vrai si l'installation est prête : la
    /// fenêtre principale se ferme alors, et le script de mise à jour prend le relais.</summary>
    public class DialogueMiseAJour : Window
    {
        /// <summary>L'or de la famille Stargazer, lisible sur les deux thèmes.</summary>
        public static readonly SolidColorBrush Or = new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27));

        private readonly MiseAJour.Info _info;
        private readonly TextBlock _etat;
        private readonly ProgressBar _barre;
        private readonly Button _installer;
        private readonly Button _plusTard;
        private bool _prete;

        private DialogueMiseAJour(Window proprietaire, MiseAJour.Info info)
        {
            _info = info;
            Title = "Mise à jour de Mental-o";
            Owner = proprietaire;
            WindowStartupLocation = proprietaire != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen;
            Width = 560;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = Chrome.FondFenetre;
            Foreground = Chrome.Encre;

            StackPanel panneau = new StackPanel { Margin = new Thickness(18, 16, 18, 14) };
            panneau.Children.Add(new TextBlock
            {
                Text = "Mental-o " + info.Version + " est disponible",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = Or
            });
            panneau.Children.Add(new TextBlock
            {
                Text = "Vous avez la " + MiseAJour.VersionLocale() + ". Ce qui change :",
                Foreground = Chrome.TexteDoux,
                Margin = new Thickness(0, 4, 0, 10)
            });

            string notes = (info.Notes ?? "").Replace("\r", "").Trim();
            if (notes.Length == 0) notes = "(pas de notes pour cette version)";
            TextBox zoneNotes = new TextBox
            {
                Text = notes,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 190,
                FontSize = 12.5,
                Padding = new Thickness(8, 6, 8, 6)
            };
            panneau.Children.Add(zoneNotes);

            _etat = new TextBlock
            {
                Text = "L'archive est téléchargée, l'application se ferme, les fichiers sont remplacés " +
                       "(vos réglages et vos cartes restent) et Mental-o redémarre.",
                Foreground = Chrome.TexteDoux,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            };
            panneau.Children.Add(_etat);

            _barre = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 6,
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            panneau.Children.Add(_barre);

            StackPanel rangee = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            _plusTard = new Button { Content = "Plus tard", MinWidth = 84, IsCancel = true };
            _plusTard.Click += delegate { Close(); };
            _installer = new Button
            {
                Content = "Installer et redémarrer",
                MinWidth = 160,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = true,
                FontWeight = FontWeights.SemiBold
            };
            _installer.Click += delegate { Lancer(); };
            rangee.Children.Add(_plusTard);
            rangee.Children.Add(_installer);
            panneau.Children.Add(rangee);
            Content = panneau;
        }

        private void Lancer()
        {
            _installer.IsEnabled = false;
            _plusTard.IsEnabled = false;
            _barre.Visibility = Visibility.Visible;
            _etat.Foreground = Chrome.TexteDoux;
            _etat.Text = "Téléchargement de la version " + _info.Version + "…";
            string dossier = MiseAJour.DossierApplication;
            BackgroundWorker travail = new BackgroundWorker();
            travail.DoWork += delegate { MiseAJour.Installer(_info, dossier); };
            travail.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs a)
            {
                if (a.Error != null)
                {
                    _barre.Visibility = Visibility.Collapsed;
                    _installer.IsEnabled = true;
                    _plusTard.IsEnabled = true;
                    _etat.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
                    _etat.Text = "Impossible : " + a.Error.Message;
                    return;
                }
                _etat.Text = "Prête. Mental-o redémarre…";
                _prete = true;
                Close();
            };
            travail.RunWorkerAsync();
        }

        /// <summary>Affiche la proposition ; rend vrai quand la mise à jour est téléchargée et que
        /// l'application doit se fermer pour la laisser s'installer.</summary>
        public static bool Proposer(Window proprietaire, MiseAJour.Info info)
        {
            DialogueMiseAJour dialogue = new DialogueMiseAJour(proprietaire, info);
            dialogue.ShowDialog();
            return dialogue._prete;
        }
    }
}
