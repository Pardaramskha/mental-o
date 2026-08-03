using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le tracé d'un lien : ligne stylée (droite, courbe de Bézier ou équerre),
    /// flèches optionnelles, étiquette au milieu du trait, et une zone de clic élargie
    /// qui sert aussi de halo de sélection.</summary>
    public class LienVisuel : Canvas
    {
        private static readonly Brush PinceauHalo = new SolidColorBrush(Color.FromArgb(0x48, 0x00, 0x78, 0xD7));

        /// <summary>Fond posé derrière les étiquettes pour qu'elles « coupent » le trait ;
        /// mis à jour par la toile quand le fond du tableau change.</summary>
        public static Color FondEtiquette = Color.FromRgb(0xF7, 0xF5, 0xF0);

        public Lien Modele { get; private set; }

        private readonly Path _zoneClic;
        private readonly Path _ligne;
        private readonly Polygon _flecheSource;
        private readonly Polygon _flecheCible;
        private readonly TextBlock _etiquette;
        private Point _a, _b;

        public LienVisuel(Lien modele)
        {
            Modele = modele;

            _zoneClic = new Path();
            _zoneClic.Stroke = Brushes.Transparent;
            _zoneClic.StrokeStartLineCap = PenLineCap.Round;
            _zoneClic.StrokeEndLineCap = PenLineCap.Round;
            _zoneClic.StrokeLineJoin = PenLineJoin.Round;
            Children.Add(_zoneClic);

            _ligne = new Path();
            _ligne.StrokeStartLineCap = PenLineCap.Round;
            _ligne.StrokeEndLineCap = PenLineCap.Round;
            _ligne.StrokeDashCap = PenLineCap.Round;
            _ligne.StrokeLineJoin = PenLineJoin.Round;
            _ligne.IsHitTestVisible = false;
            Children.Add(_ligne);

            _flecheSource = new Polygon();
            _flecheSource.IsHitTestVisible = false;
            Children.Add(_flecheSource);

            _flecheCible = new Polygon();
            _flecheCible.IsHitTestVisible = false;
            Children.Add(_flecheCible);

            _etiquette = new TextBlock();
            _etiquette.FontSize = 12;
            _etiquette.FontWeight = FontWeights.SemiBold;
            _etiquette.Padding = new Thickness(5, 1, 5, 1);
            _etiquette.TextAlignment = TextAlignment.Center;
            _etiquette.Visibility = Visibility.Collapsed;
            Children.Add(_etiquette);

            AppliquerStyle();
        }

        public void DefinirSelection(bool selectionne)
        {
            _zoneClic.Stroke = selectionne ? PinceauHalo : Brushes.Transparent;
        }

        public void AppliquerStyle()
        {
            Brush pinceau = new SolidColorBrush(ConvertirCouleur(Modele.Couleur));
            _ligne.Stroke = pinceau;
            _ligne.StrokeThickness = Modele.Epaisseur;
            _ligne.StrokeDashArray = TableauTirets(Modele.Trait);
            _flecheSource.Fill = pinceau;
            _flecheCible.Fill = pinceau;
            _zoneClic.StrokeThickness = Math.Max(14, Modele.Epaisseur + 10);
            _etiquette.Foreground = pinceau;
            _etiquette.Background = new SolidColorBrush(FondEtiquette);
            Redessiner();
        }

        public void MettreAJourGeometrie(Point a, Point b)
        {
            _a = a;
            _b = b;
            Redessiner();
        }

        /// <summary>Le point de contrôle du tracé courbe : perpendiculaire au segment,
        /// à « courbure » pixels de son milieu.</summary>
        public static Point ControleCourbe(Point a, Point b, double courbure)
        {
            Vector direction = b - a;
            if (direction.Length < 0.001) return a;
            direction.Normalize();
            Vector perpendiculaire = new Vector(-direction.Y, direction.X);
            Point milieu = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            return milieu + perpendiculaire * courbure;
        }

        /// <summary>Le point du tracé à mi-parcours — position de l'étiquette et de la
        /// poignée de courbure.</summary>
        public Point PointMilieu()
        {
            return PointMilieu(_a, _b, Modele);
        }

        public static Point PointMilieu(Point a, Point b, Lien lien)
        {
            Point milieu = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            if (lien.Trace == "courbe")
            {
                // Point de la Bézier quadratique à t = 0,5 : ¼a + ½c + ¼b.
                Point controle = ControleCourbe(a, b, lien.Courbure);
                return new Point(
                    0.25 * a.X + 0.5 * controle.X + 0.25 * b.X,
                    0.25 * a.Y + 0.5 * controle.Y + 0.25 * b.Y);
            }
            return milieu; // droit, et équerre (les deux coudes encadrent le milieu)
        }

        /// <summary>Les coudes du tracé en équerre : le lien part dans l'axe dominant.</summary>
        public static Point[] CoudesEquerre(Point a, Point b)
        {
            if (Math.Abs(b.X - a.X) >= Math.Abs(b.Y - a.Y))
            {
                double mx = (a.X + b.X) / 2;
                return new Point[] { new Point(mx, a.Y), new Point(mx, b.Y) };
            }
            double my = (a.Y + b.Y) / 2;
            return new Point[] { new Point(a.X, my), new Point(b.X, my) };
        }

        private void Redessiner()
        {
            Vector direction = _b - _a;
            double longueur = direction.Length;
            if (longueur < 1)
            {
                _ligne.Data = null;
                _zoneClic.Data = null;
                _flecheSource.Visibility = Visibility.Collapsed;
                _flecheCible.Visibility = Visibility.Collapsed;
                _etiquette.Visibility = Visibility.Collapsed;
                return;
            }

            double taille = 6 + Modele.Epaisseur * 2.2;
            bool versCible = Modele.Fleche == "simple" || Modele.Fleche == "double";
            bool versSource = Modele.Fleche == "double";

            // Directions du tracé à chaque extrémité (pour orienter les flèches et
            // arrêter la ligne à leur base, sans dépasser de la pointe).
            Vector dirDepart, dirArrivee; // toutes deux orientées de la source vers la cible
            Point controle = default(Point);
            Point[] coudes = null;
            if (Modele.Trace == "courbe" && Math.Abs(Modele.Courbure) > 0.5)
            {
                controle = ControleCourbe(_a, _b, Modele.Courbure);
                dirDepart = controle - _a;
                dirArrivee = _b - controle;
            }
            else if (Modele.Trace == "equerre")
            {
                coudes = CoudesEquerre(_a, _b);
                dirDepart = coudes[0] - _a;
                dirArrivee = _b - coudes[1];
            }
            else
            {
                dirDepart = direction;
                dirArrivee = direction;
            }
            if (dirDepart.Length < 0.001) dirDepart = direction;
            if (dirArrivee.Length < 0.001) dirArrivee = direction;
            dirDepart.Normalize();
            dirArrivee.Normalize();

            Point debut = versSource ? _a + dirDepart * (taille * 0.8) : _a;
            Point fin = versCible ? _b - dirArrivee * (taille * 0.8) : _b;

            PathFigure figure = new PathFigure { StartPoint = debut };
            if (coudes != null)
            {
                figure.Segments.Add(new LineSegment(coudes[0], true));
                figure.Segments.Add(new LineSegment(coudes[1], true));
                figure.Segments.Add(new LineSegment(fin, true));
            }
            else if (Modele.Trace == "courbe" && Math.Abs(Modele.Courbure) > 0.5)
            {
                figure.Segments.Add(new QuadraticBezierSegment(controle, fin, true));
            }
            else
            {
                figure.Segments.Add(new LineSegment(fin, true));
            }
            PathGeometry geometrie = new PathGeometry();
            geometrie.Figures.Add(figure);
            _ligne.Data = geometrie;
            _zoneClic.Data = geometrie;

            _flecheCible.Visibility = versCible ? Visibility.Visible : Visibility.Collapsed;
            if (versCible) _flecheCible.Points = PointsFleche(_b, dirArrivee, taille);

            _flecheSource.Visibility = versSource ? Visibility.Visible : Visibility.Collapsed;
            if (versSource) _flecheSource.Points = PointsFleche(_a, -dirDepart, taille);

            if (string.IsNullOrEmpty(Modele.Etiquette))
            {
                _etiquette.Visibility = Visibility.Collapsed;
            }
            else
            {
                _etiquette.Text = Modele.Etiquette;
                _etiquette.Visibility = Visibility.Visible;
                _etiquette.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Point milieu = PointMilieu();
                Canvas.SetLeft(_etiquette, milieu.X - _etiquette.DesiredSize.Width / 2);
                Canvas.SetTop(_etiquette, milieu.Y - _etiquette.DesiredSize.Height / 2);
            }
        }

        private static PointCollection PointsFleche(Point pointe, Vector direction, double taille)
        {
            Vector perpendiculaire = new Vector(-direction.Y, direction.X);
            Point basePointe = pointe - direction * taille;
            PointCollection points = new PointCollection();
            points.Add(pointe);
            points.Add(basePointe + perpendiculaire * (taille * 0.45));
            points.Add(basePointe - perpendiculaire * (taille * 0.45));
            return points;
        }

        private static DoubleCollection TableauTirets(string trait)
        {
            // Les valeurs sont en multiples de l'épaisseur du trait.
            if (trait == "tirets") return new DoubleCollection(new double[] { 4, 2.5 });
            if (trait == "points") return new DoubleCollection(new double[] { 0.1, 2.5 });
            return null;
        }

        public static Color ConvertirCouleur(string hexa)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hexa);
            }
            catch
            {
                return Color.FromRgb(0x6A, 0x66, 0x5E);
            }
        }
    }
}
