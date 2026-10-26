using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Parametres;
using Mentalo.Vue;

namespace Mentalo
{
    /// <summary>Les barres propres à la fenêtre : la barre des modes (contenu
    /// partagé BarresToile + bouton de thème), la vignette du tableau, la barre
    /// d'état. La barre de personnalisation est entièrement BarresToile.</summary>
    public partial class FenetrePrincipale
    {
        // ------------------------------------------------------------------ barre de modes

        private Border ConstruireBarreModes()
        {
            StackPanel panneau = _barres.ConstruirePanneauModes();

            // Le bouton de thème vit à l'opposé de la barre, ancré tout à droite.
            _boutonTheme = new Button
            {
                Width = 32,
                MinWidth = 32,
                Height = 26,
                Padding = new Thickness(0),
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0)
            };
            _boutonTheme.Click += delegate { BasculerTheme(); };
            MettreAJourIconeTheme();

            DockPanel conteneur = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_boutonTheme, Dock.Right);
            conteneur.Children.Add(_boutonTheme);
            conteneur.Children.Add(panneau);

            Border bordure = new Border();
            bordure.BorderThickness = new Thickness(0, 0, 0, 1);
            bordure.BorderBrush = Chrome.Bordure;
            bordure.Background = Chrome.FondBarreLegere;
            bordure.Child = conteneur;
            return bordure;
        }

        private void MettreAJourBarreModes() { _barres.MettreAJourModes(); }
        private void MettreAJourBarreFormat() { _barres.MettreAJourFormat(); }
        private void RemplirCombosCouleurs() { _barres.RemplirCombosCouleurs(); }

        // ------------------------------------------------------------------ recherche

        private void OuvrirRecherche() { _panneaux.OuvrirRecherche(); }
        private void FermerRecherche() { _panneaux.FermerRecherche(); }

        /// <summary>Très légère ombre en vignette sur les quatre bords du tableau.</summary>
        private Grid ConstruireVignette()
        {
            Grid calque = new Grid { IsHitTestVisible = false };
            Color ombre = Color.FromArgb(0x22, 0x00, 0x00, 0x00);
            Color rien = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
            calque.Children.Add(new Rectangle { Height = 26, VerticalAlignment = VerticalAlignment.Top, Fill = Degrade(ombre, rien, false) });
            calque.Children.Add(new Rectangle { Height = 26, VerticalAlignment = VerticalAlignment.Bottom, Fill = Degrade(rien, ombre, false) });
            calque.Children.Add(new Rectangle { Width = 26, HorizontalAlignment = HorizontalAlignment.Left, Fill = Degrade(ombre, rien, true) });
            calque.Children.Add(new Rectangle { Width = 26, HorizontalAlignment = HorizontalAlignment.Right, Fill = Degrade(rien, ombre, true) });
            calque.IsVisible = ParametresGlobaux.VignetteTableau;
            return calque;
        }

        private static LinearGradientBrush Degrade(Color de, Color vers, bool horizontal)
        {
            LinearGradientBrush brosse = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = horizontal ? new RelativePoint(1, 0, RelativeUnit.Relative) : new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            brosse.GradientStops.Add(new GradientStop(de, 0));
            brosse.GradientStops.Add(new GradientStop(vers, 1));
            return brosse;
        }

        private void MettreAJourVignette()
        {
            if (_vignette != null)
                _vignette.IsVisible = ParametresGlobaux.VignetteTableau;
        }

        private void BasculerTheme()
        {
            ParametresGlobaux.ThemeSombre = !ParametresGlobaux.ThemeSombre;
            ParametresGlobaux.Enregistrer();
            Mentalo.Theme.Appliquer(ParametresGlobaux.ThemeSombre);
            _toile.AppliquerFondDocument();
            MettreAJourIconeTheme();
            _panneaux.MettreAJourMiniCarte();
        }

        private void MettreAJourIconeTheme()
        {
            _boutonTheme.Content = new TextBlock
            {
                Text = ParametresGlobaux.ThemeSombre ? "☀" : "☾",
                FontSize = 14,
                Foreground = Chrome.Encre,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Ui.Infobulle(_boutonTheme, ParametresGlobaux.ThemeSombre
                ? "Repasser au thème clair"
                : "Passer au thème sombre");
        }

        // ------------------------------------------------------------------ barre d'état

        private Border ConstruireBarreEtat()
        {
            // Les raccourcis vivent désormais dans le Tust (menu Fenêtres) : le pied reste sobre.
            DockPanel contenu = new DockPanel { LastChildFill = false };
            DockPanel.SetDock(_texteZoom, Dock.Right);
            contenu.Children.Add(_texteZoom);
            // À gauche, la mise à jour disponible (vide sinon) — cliquable
            _texteMaj.Margin = new Thickness(8, 3, 8, 3);
            _texteMaj.Foreground = Chrome.Or;
            _texteMaj.Cursor = Ui.CurseurMain;
            _texteMaj.IsVisible = false;
            _texteMaj.PointerReleased += delegate { VerifierMisesAJour(); };
            DockPanel.SetDock(_texteMaj, Dock.Left);
            contenu.Children.Add(_texteMaj);

            Border bordure = new Border();
            bordure.BorderThickness = new Thickness(0, 1, 0, 0);
            bordure.BorderBrush = Chrome.Bordure;
            bordure.Background = Chrome.FondBarre;
            bordure.Child = contenu;
            return bordure;
        }
    }
}
