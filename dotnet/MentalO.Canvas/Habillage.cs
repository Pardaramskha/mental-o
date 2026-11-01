using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mentalo.Vue
{
    /// <summary>L'HABILLAGE des barres et des panneaux (26/10/2026) : ce qui
    /// change d'un hôte à l'autre — les pinceaux et la façon de fabriquer un
    /// bouton, une bascule, un séparateur. Mental-O seul fournit l'habillage
    /// standard (ci-dessous) ; le DLC Marabook en fournit un avec les boutons,
    /// les tailles et les couleurs de Marabook. Tout le reste (le contenu des
    /// barres, leur synchronisation avec la toile, les panneaux flottants) est
    /// partagé dans BarresToile et PanneauxFlottants.</summary>
    public interface IHabillage
    {
        IBrush FondBarre { get; }
        IBrush FondBarreLegere { get; }
        IBrush Bordure { get; }
        IBrush Encre { get; }
        IBrush TexteDoux { get; }
        IBrush FondPanneau { get; }
        IBrush FondMiniCarte { get; }
        IBrush Accent { get; }

        /// <summary>Un trait vertical entre deux groupes d'une barre.</summary>
        Control Separateur();
        /// <summary>Une bascule carrée à icône (gras, alignement, mode…).</summary>
        ToggleButton Bascule(Control contenu, string infobulle);
        /// <summary>Une bascule à icône et texte (les onglets Texte / Boîte / Groupe / Lien).</summary>
        ToggleButton BasculeTexte(Control icone, string texte, string infobulle);
        /// <summary>Un bouton carré à icône ou glyphe (réajuster, puces…).</summary>
        Button Outil(Control contenu, string infobulle);
        /// <summary>Un bouton à texte, compact (Dégrouper, Annuler…).</summary>
        Button BoutonTexte(string texte, string infobulle);
        /// <summary>Un symbole texte pour un bouton (remplace les glyphes Segoe MDL2).</summary>
        TextBlock Glyphe(string symbole, double taille);
    }

    /// <summary>L'habillage de Mental-O seul : les pinceaux de Chrome, des
    /// boutons de 26 px sans classe particulière (le thème de l'application
    /// les arrondit).</summary>
    public class HabillageStandard : IHabillage
    {
        public IBrush FondBarre { get { return Chrome.FondBarre; } }
        public IBrush FondBarreLegere { get { return Chrome.FondBarreLegere; } }
        public IBrush Bordure { get { return Chrome.Bordure; } }
        public IBrush Encre { get { return Chrome.Encre; } }
        public IBrush TexteDoux { get { return Chrome.TexteDoux; } }
        public IBrush FondPanneau { get { return Chrome.FondPanneau; } }
        public IBrush FondMiniCarte { get { return Chrome.FondMiniCarte; } }
        public IBrush Accent { get { return Chrome.Accent; } }

        public Control Separateur()
        {
            return new Border { Width = 1, Margin = new Thickness(7, 2, 5, 2), Background = Chrome.Bordure };
        }

        public ToggleButton Bascule(Control contenu, string infobulle)
        {
            ToggleButton bouton = new ToggleButton
            {
                Content = contenu,
                Width = 30,
                MinWidth = 30,
                Height = 26,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                Focusable = false // ne vole pas le focus de l'éditeur
            };
            Ui.Infobulle(bouton, infobulle);
            return bouton;
        }

        public ToggleButton BasculeTexte(Control icone, string texte, string infobulle)
        {
            StackPanel contenu = new StackPanel { Orientation = Orientation.Horizontal };
            if (icone != null)
            {
                icone.Margin = new Thickness(0, 0, 5, 0);
                contenu.Children.Add(icone);
            }
            contenu.Children.Add(new TextBlock { Text = texte, VerticalAlignment = VerticalAlignment.Center });
            ToggleButton bouton = new ToggleButton
            {
                Content = contenu,
                Padding = new Thickness(8, 2),
                Margin = new Thickness(2, 0, 0, 0),
                Focusable = false
            };
            Ui.Infobulle(bouton, infobulle);
            return bouton;
        }

        public Button Outil(Control contenu, string infobulle)
        {
            Button bouton = new Button
            {
                Content = contenu,
                Width = 30,
                MinWidth = 30,
                Height = 26,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                Focusable = false
            };
            Ui.Infobulle(bouton, infobulle);
            return bouton;
        }

        public Button BoutonTexte(string texte, string infobulle)
        {
            Button bouton = new Button
            {
                Content = texte,
                Padding = new Thickness(8, 2),
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Focusable = false
            };
            if (!string.IsNullOrEmpty(infobulle)) Ui.Infobulle(bouton, infobulle);
            return bouton;
        }

        public TextBlock Glyphe(string symbole, double taille)
        {
            return new TextBlock
            {
                Text = symbole,
                FontSize = taille,
                Foreground = Chrome.Encre,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }
    }
}
