using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le tracé d'un lien : ligne stylée (droite, courbe de Bézier ou équerre),
    /// flèches optionnelles, étiquette au milieu du trait, et une zone de clic élargie
    /// qui sert aussi de halo de sélection.</summary>
    public class LienVisuel : Canvas
    {
        private static readonly IBrush PinceauHalo = new SolidColorBrush(Color.FromArgb(0x48, 0x00, 0x78, 0xD7));

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
            // Un Canvas sans taille : ses enfants se placent en coordonnées absolues du tableau.
            Width = 0;
            Height = 0;
            ClipToBounds = false;

            _zoneClic = new Path();
            _zoneClic.Stroke = Brushes.Transparent;
            _zoneClic.StrokeLineCap = PenLineCap.Round;
            _zoneClic.StrokeJoin = PenLineJoin.Round;
            Children.Add(_zoneClic);

            _ligne = new Path();
            _ligne.StrokeLineCap = PenLineCap.Round;
            _ligne.StrokeJoin = PenLineJoin.Round;
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
            _etiquette.FontWeight = FontWeight.SemiBold;
            _etiquette.Padding = new Thickness(5, 1, 5, 1);
            _etiquette.TextAlignment = TextAlignment.Center;
            _etiquette.IsVisible = false;
            Children.Add(_etiquette);

            AppliquerStyle();
        }

        /// <summary>La zone de clic (le seul élément sensible du lien).</summary>
        public Control ZoneClic { get { return _zoneClic; } }

        public void DefinirSelection(bool selectionne)
        {
            _zoneClic.Stroke = selectionne ? PinceauHalo : Brushes.Transparent;
        }

        public void AppliquerStyle()
        {
            IBrush pinceau = new SolidColorBrush(ConvertirCouleur(Modele.Couleur));
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

        public Point PointMilieu()
        {
            return GeometrieLien.PointMilieu(_a, _b, Modele);
        }

        /// <summary>Les points de passage du tracé (pour la vignette et la zone de clic).</summary>
        public static PathGeometry Trace(Point a, Point b, Lien lien, out Vector dirDepart, out Vector dirArrivee)
        {
            Vector direction = b - a;
            Point controle = default(Point);
            Point[] coudes = null;
            if (lien.Trace == "courbe" && Math.Abs(lien.Courbure) > 0.5)
            {
                controle = GeometrieLien.ControleCourbe(a, b, lien.Courbure);
                dirDepart = controle - a;
                dirArrivee = b - controle;
            }
            else if (lien.Trace == "equerre")
            {
                coudes = GeometrieLien.CoudesEquerre(a, b);
                dirDepart = coudes[0] - a;
                dirArrivee = b - coudes[1];
            }
            else
            {
                dirDepart = direction;
                dirArrivee = direction;
            }
            if (dirDepart.Length < 0.001) dirDepart = direction;
            if (dirArrivee.Length < 0.001) dirArrivee = direction;
            dirDepart = dirDepart.Normalize();
            dirArrivee = dirArrivee.Normalize();

            double taille = 6 + lien.Epaisseur * 2.2;
            bool versCible = lien.FlecheVersCible;
            bool versSource = lien.FlecheVersSource;
            Point debut = versSource ? a + dirDepart * (taille * 0.8) : a;
            Point fin = versCible ? b - dirArrivee * (taille * 0.8) : b;

            PathFigure figure = new PathFigure { StartPoint = debut, IsClosed = false, IsFilled = false };
            if (coudes != null)
            {
                figure.Segments.Add(new LineSegment { Point = coudes[0] });
                figure.Segments.Add(new LineSegment { Point = coudes[1] });
                figure.Segments.Add(new LineSegment { Point = fin });
            }
            else if (lien.Trace == "courbe" && Math.Abs(lien.Courbure) > 0.5)
            {
                figure.Segments.Add(new QuadraticBezierSegment { Point1 = controle, Point2 = fin });
            }
            else
            {
                figure.Segments.Add(new LineSegment { Point = fin });
            }
            PathGeometry geometrie = new PathGeometry();
            geometrie.Figures.Add(figure);
            return geometrie;
        }

        private void Redessiner()
        {
            Vector direction = _b - _a;
            double longueur = direction.Length;
            if (longueur < 1)
            {
                _ligne.Data = null;
                _zoneClic.Data = null;
                _flecheSource.IsVisible = false;
                _flecheCible.IsVisible = false;
                _etiquette.IsVisible = false;
                return;
            }

            double taille = 6 + Modele.Epaisseur * 2.2;
            bool versCible = Modele.FlecheVersCible;
            bool versSource = Modele.FlecheVersSource;
            Vector dirDepart, dirArrivee;
            PathGeometry geometrie = Trace(_a, _b, Modele, out dirDepart, out dirArrivee);
            _ligne.Data = geometrie;
            _zoneClic.Data = geometrie;

            _flecheCible.IsVisible = versCible;
            if (versCible) _flecheCible.Points = new List<Point>(GeometrieLien.PointsFleche(_b, dirArrivee, taille));

            _flecheSource.IsVisible = versSource;
            if (versSource) _flecheSource.Points = new List<Point>(GeometrieLien.PointsFleche(_a, -dirDepart, taille));

            if (string.IsNullOrEmpty(Modele.Etiquette))
            {
                _etiquette.IsVisible = false;
            }
            else
            {
                _etiquette.Text = Modele.Etiquette;
                _etiquette.IsVisible = true;
                _etiquette.Measure(Size.Infinity);
                Point milieu = PointMilieu();
                Canvas.SetLeft(_etiquette, milieu.X - _etiquette.DesiredSize.Width / 2);
                Canvas.SetTop(_etiquette, milieu.Y - _etiquette.DesiredSize.Height / 2);
            }
        }

        public static AvaloniaList<double> TableauTirets(string trait)
        {
            // Les valeurs sont en multiples de l'épaisseur du trait.
            if (trait == "tirets") return new AvaloniaList<double>(new double[] { 4, 2.5 });
            if (trait == "points") return new AvaloniaList<double>(new double[] { 0.1, 2.5 });
            return null;
        }

        public static Color ConvertirCouleur(string hexa)
        {
            return Palette.CouleurDepuis(hexa, Color.FromRgb(0x6A, 0x66, 0x5E));
        }
    }
}
