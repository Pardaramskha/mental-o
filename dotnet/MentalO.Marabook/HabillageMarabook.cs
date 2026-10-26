using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Vue;
using Chrome = global::Marabook.App.Chrome;

namespace MentalO.Marabook
{
    /// <summary>L'habillage des barres et des panneaux de la toile aux couleurs
    /// et aux boutons de Marabook (26/10/2026) : les pinceaux de son Chrome
    /// (ils suivent le thème clair / sombre), des boutons de 26 px portant les
    /// classes de son thème (arrondis, survol, état actif).</summary>
    public class HabillageMarabook : IHabillage
    {
        public IBrush FondBarre { get { return Chrome.BarBg; } }
        public IBrush FondBarreLegere { get { return Chrome.BarBgLight; } }
        public IBrush Bordure { get { return Chrome.Border; } }
        public IBrush Encre { get { return Chrome.Ink; } }
        public IBrush TexteDoux { get { return Chrome.SoftText; } }
        public IBrush FondPanneau { get { return Chrome.RaisedBg; } }
        public IBrush FondMiniCarte { get { return Chrome.CardBg; } }
        public IBrush Accent { get { return Chrome.Accent; } }

        public Control Separateur()
        {
            return new Border { Width = 1, Height = 22, Margin = new Thickness(6, 0, 8, 0), Background = Chrome.Border, VerticalAlignment = VerticalAlignment.Center };
        }

        private static void Habiller(TemplatedControl bouton, string infobulle)
        {
            bouton.Height = global::Marabook.App.Buttons.Compact;
            bouton.MinHeight = global::Marabook.App.Buttons.Compact;
            bouton.FontSize = 13;
            bouton.Focusable = false;
            bouton.VerticalAlignment = VerticalAlignment.Center;
            bouton.Margin = new Thickness(0, 0, 3, 0);
            if (!bouton.Classes.Contains(global::Marabook.App.Theme.Owned)) bouton.Classes.Add(global::Marabook.App.Theme.Owned);
            bouton.Classes.Add(global::Marabook.App.Theme.Calm);
            if (!string.IsNullOrEmpty(infobulle)) ToolTip.SetTip(bouton, infobulle);
        }

        public ToggleButton Bascule(Control contenu, string infobulle)
        {
            contenu.HorizontalAlignment = HorizontalAlignment.Center;
            contenu.VerticalAlignment = VerticalAlignment.Center;
            ToggleButton bouton = new ToggleButton { Content = contenu, Padding = new Thickness(0), Width = global::Marabook.App.Buttons.Compact };
            Habiller(bouton, infobulle);
            return bouton;
        }

        public ToggleButton BasculeTexte(Control icone, string texte, string infobulle)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            if (icone != null)
            {
                icone.Margin = new Thickness(0, 0, 6, 0);
                icone.VerticalAlignment = VerticalAlignment.Center;
                ligne.Children.Add(icone);
            }
            ligne.Children.Add(new TextBlock { Text = texte, VerticalAlignment = VerticalAlignment.Center });
            ToggleButton bouton = new ToggleButton { Content = ligne, Padding = new Thickness(8, 0, 10, 0) };
            Habiller(bouton, infobulle);
            return bouton;
        }

        public Button Outil(Control contenu, string infobulle)
        {
            contenu.HorizontalAlignment = HorizontalAlignment.Center;
            contenu.VerticalAlignment = VerticalAlignment.Center;
            Button bouton = new Button { Content = contenu, Padding = new Thickness(0), Width = global::Marabook.App.Buttons.Compact };
            Habiller(bouton, infobulle);
            return bouton;
        }

        public Button BoutonTexte(string texte, string infobulle)
        {
            Button bouton = global::Marabook.App.Buttons.Text(texte, infobulle, global::Marabook.App.Buttons.Compact, global::Marabook.App.Buttons.Look.Outline);
            bouton.VerticalAlignment = VerticalAlignment.Center;
            bouton.Margin = new Thickness(3, 0, 3, 0);
            return bouton;
        }

        public TextBlock Glyphe(string symbole, double taille)
        {
            return new TextBlock
            {
                Text = symbole,
                FontSize = taille,
                Foreground = Chrome.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }
    }
}
