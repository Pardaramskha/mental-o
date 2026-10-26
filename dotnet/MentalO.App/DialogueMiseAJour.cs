using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Persistance;

namespace Mentalo.Vue
{
    /// <summary>La proposition de mise à jour (v0.18) : « Mental-O X est disponible », les notes de
    /// la release, et « Installer et redémarrer ». Rend vrai si l'installation est prête : la
    /// fenêtre principale se ferme alors, et le script de mise à jour prend le relais.</summary>
    public static class DialogueMiseAJour
    {
        public static async Task<bool> Proposer(Window proprietaire, MiseAJour.Info info)
        {
            Dialogue dialogue = new Dialogue("Mise à jour de Mental-O");
            dialogue.Width = 560;
            dialogue.SizeToContent = SizeToContent.Height;
            bool prete = false;

            StackPanel panneau = new StackPanel { Margin = new Thickness(18, 16, 18, 14) };
            panneau.Children.Add(new TextBlock
            {
                Text = "Mental-O " + info.Version + " est disponible",
                FontSize = 17,
                FontWeight = FontWeight.SemiBold,
                Foreground = Chrome.Or
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
                Height = 190,
                FontSize = 12.5
            };
            panneau.Children.Add(zoneNotes);

            TextBlock etat = new TextBlock
            {
                Text = "L'archive est téléchargée, l'application se ferme, les fichiers sont remplacés " +
                       "(vos réglages et vos cartes restent) et Mental-O redémarre.",
                Foreground = Chrome.TexteDoux,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            };
            panneau.Children.Add(etat);

            ProgressBar barre = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 6,
                Margin = new Thickness(0, 10, 0, 0),
                IsVisible = false
            };
            panneau.Children.Add(barre);

            StackPanel rangee = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                Spacing = 8
            };
            Button plusTard = new Button { Content = "Plus tard", MinWidth = 84 };
            plusTard.Click += delegate { dialogue.Annuler(); };
            Button installer = new Button
            {
                Content = "Installer et redémarrer",
                MinWidth = 160,
                IsDefault = true,
                FontWeight = FontWeight.SemiBold
            };
            installer.Click += async delegate
            {
                installer.IsEnabled = false;
                plusTard.IsEnabled = false;
                barre.IsVisible = true;
                etat.Foreground = Chrome.TexteDoux;
                etat.Text = "Téléchargement de la version " + info.Version + "…";
                string dossier = MiseAJour.DossierApplication;
                try
                {
                    await Task.Run(delegate { MiseAJour.Installer(info, dossier); });
                }
                catch (Exception ex)
                {
                    barre.IsVisible = false;
                    installer.IsEnabled = true;
                    plusTard.IsEnabled = true;
                    etat.Foreground = Chrome.Danger;
                    etat.Text = "Impossible : " + ex.Message;
                    return;
                }
                etat.Text = "Prête. Mental-O redémarre…";
                prete = true;
                dialogue.Valider();
            };
            rangee.Children.Add(plusTard);
            rangee.Children.Add(installer);
            panneau.Children.Add(rangee);
            dialogue.Content = panneau;
            await dialogue.Afficher(proprietaire);
            return prete;
        }
    }
}
