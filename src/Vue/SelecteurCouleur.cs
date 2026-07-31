using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Mentalo.Vue
{
    /// <summary>Sélecteur de couleur maison : carré saturation/luminosité, barre de teinte,
    /// champ hexadécimal et aperçu. Remplace le dialogue système, non stylable.</summary>
    public static class SelecteurCouleur
    {
        private static double _teinte;      // 0-360
        private static double _saturation;  // 0-1
        private static double _valeur;      // 0-1

        /// <summary>Ouvre le sélecteur ; renvoie "#RRGGBB" ou null si annulé.</summary>
        public static string Choisir(Window proprietaire, string couleurInitiale)
        {
            Color initiale = TexteRiche.CouleurDepuis(couleurInitiale ?? "#0078D7", Color.FromRgb(0x00, 0x78, 0xD7));
            VersHsv(initiale, out _teinte, out _saturation, out _valeur);

            Window dialogue = new Window
            {
                Title = "Choisir une couleur",
                Owner = proprietaire,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };

            StackPanel racine = new StackPanel { Margin = new Thickness(14) };
            StackPanel zonePrincipale = new StackPanel { Orientation = Orientation.Horizontal };

            // --- Carré saturation (x) / luminosité (y) sur la teinte courante -----------
            Grid carre = new Grid { Width = 216, Height = 160, Cursor = Cursors.Cross };
            Rectangle fondTeinte = new Rectangle();
            LinearGradientBrush blanc = new LinearGradientBrush(
                Colors.White, Color.FromArgb(0, 255, 255, 255), 0);
            LinearGradientBrush noir = new LinearGradientBrush(
                Color.FromArgb(0, 0, 0, 0), Colors.Black, 90);
            carre.Children.Add(fondTeinte);
            carre.Children.Add(new Rectangle { Fill = blanc });
            carre.Children.Add(new Rectangle { Fill = noir });
            Ellipse curseur = new Ellipse
            {
                Width = 12, Height = 12,
                Stroke = Brushes.White, StrokeThickness = 2,
                Fill = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Opacity = 0.6, BlurRadius = 3, ShadowDepth = 0
                }
            };
            carre.Children.Add(curseur);
            zonePrincipale.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xC5, 0xBB)),
                BorderThickness = new Thickness(1),
                Child = carre
            });

            // --- Barre de teinte ---------------------------------------------------------
            LinearGradientBrush arcEnCiel = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            for (int i = 0; i <= 6; i++)
                arcEnCiel.GradientStops.Add(new GradientStop(DepuisHsv(i * 60, 1, 1), i / 6.0));
            Grid barre = new Grid { Width = 22, Height = 160, Cursor = Cursors.SizeNS };
            barre.Children.Add(new Rectangle { Fill = arcEnCiel });
            Border reperes = new Border
            {
                Height = 5,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Opacity = 0.6, BlurRadius = 3, ShadowDepth = 0
                }
            };
            barre.Children.Add(reperes);
            zonePrincipale.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xC5, 0xBB)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 0, 0, 0),
                Child = barre
            });
            racine.Children.Add(zonePrincipale);

            // --- Aperçu + hexadécimal ----------------------------------------------------
            StackPanel ligneBas = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            Border apercu = new Border
            {
                Width = 44, Height = 26,
                CornerRadius = new CornerRadius(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xC5, 0xBB)),
                BorderThickness = new Thickness(1)
            };
            ligneBas.Children.Add(apercu);
            TextBox hexa = new TextBox { Width = 90, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            ligneBas.Children.Add(hexa);
            racine.Children.Add(ligneBas);

            StackPanel boutons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            Button valider = new Button { Content = "Choisir", Width = 84, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            Button annuler = new Button { Content = "Annuler", Width = 84, IsCancel = true };
            valider.Click += delegate { dialogue.DialogResult = true; };
            boutons.Children.Add(valider);
            boutons.Children.Add(annuler);
            racine.Children.Add(boutons);
            dialogue.Content = racine;

            // --- Synchronisation ---------------------------------------------------------
            bool synchro = false;
            Action rafraichir = delegate
            {
                synchro = true;
                Color couleur = DepuisHsv(_teinte, _saturation, _valeur);
                fondTeinte.Fill = new SolidColorBrush(DepuisHsv(_teinte, 1, 1));
                apercu.Background = new SolidColorBrush(couleur);
                hexa.Text = TexteRiche.HexaDepuisCouleur(couleur);
                curseur.Margin = new Thickness(
                    _saturation * carre.Width - 6, (1 - _valeur) * carre.Height - 6, 0, 0);
                reperes.Margin = new Thickness(0, _teinte / 360.0 * barre.Height - 2.5, 0, 0);
                synchro = false;
            };

            MouseEventHandler surCarre = delegate(object expediteur, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                Point p = e.GetPosition(carre);
                _saturation = Math.Max(0, Math.Min(1, p.X / carre.Width));
                _valeur = Math.Max(0, Math.Min(1, 1 - p.Y / carre.Height));
                rafraichir();
            };
            carre.MouseLeftButtonDown += delegate(object expediteur, MouseButtonEventArgs e)
            {
                carre.CaptureMouse();
                surCarre(expediteur, e);
            };
            carre.MouseMove += surCarre;
            carre.MouseLeftButtonUp += delegate { carre.ReleaseMouseCapture(); };

            MouseEventHandler surBarre = delegate(object expediteur, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                Point p = e.GetPosition(barre);
                _teinte = Math.Max(0, Math.Min(359.9, p.Y / barre.Height * 360));
                rafraichir();
            };
            barre.MouseLeftButtonDown += delegate(object expediteur, MouseButtonEventArgs e)
            {
                barre.CaptureMouse();
                surBarre(expediteur, e);
            };
            barre.MouseMove += surBarre;
            barre.MouseLeftButtonUp += delegate { barre.ReleaseMouseCapture(); };

            hexa.TextChanged += delegate
            {
                if (synchro) return;
                string texte = hexa.Text.Trim();
                if (!texte.StartsWith("#")) texte = "#" + texte;
                if (texte.Length != 7) return;
                try
                {
                    Color couleur = (Color)ColorConverter.ConvertFromString(texte);
                    VersHsv(couleur, out _teinte, out _saturation, out _valeur);
                    rafraichir();
                }
                catch { }
            };

            dialogue.Loaded += delegate { rafraichir(); };

            if (dialogue.ShowDialog() != true) return null;
            return TexteRiche.HexaDepuisCouleur(DepuisHsv(_teinte, _saturation, _valeur));
        }

        // ------------------------------------------------------------------ conversions

        private static Color DepuisHsv(double teinte, double saturation, double valeur)
        {
            double c = valeur * saturation;
            double x = c * (1 - Math.Abs(teinte / 60 % 2 - 1));
            double m = valeur - c;
            double r = 0, v = 0, b = 0;
            if (teinte < 60) { r = c; v = x; }
            else if (teinte < 120) { r = x; v = c; }
            else if (teinte < 180) { v = c; b = x; }
            else if (teinte < 240) { v = x; b = c; }
            else if (teinte < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((v + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        private static void VersHsv(Color couleur, out double teinte, out double saturation, out double valeur)
        {
            double r = couleur.R / 255.0, v = couleur.G / 255.0, b = couleur.B / 255.0;
            double max = Math.Max(r, Math.Max(v, b));
            double min = Math.Min(r, Math.Min(v, b));
            double delta = max - min;
            valeur = max;
            saturation = max <= 0 ? 0 : delta / max;
            if (delta <= 0) teinte = 0;
            else if (max == r) teinte = 60 * ((v - b) / delta % 6);
            else if (max == v) teinte = 60 * ((b - r) / delta + 2);
            else teinte = 60 * ((r - v) / delta + 4);
            if (teinte < 0) teinte += 360;
        }
    }
}
