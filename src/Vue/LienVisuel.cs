using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le tracé d'un lien : ligne stylée, flèches optionnelles, et une zone de clic
    /// élargie qui sert aussi de halo de sélection.</summary>
    public class LienVisuel : Canvas
    {
        private static readonly Brush PinceauHalo = new SolidColorBrush(Color.FromArgb(0x48, 0x00, 0x78, 0xD7));

        public Lien Modele { get; private set; }

        private readonly Line _zoneClic;
        private readonly Line _ligne;
        private readonly Polygon _flecheSource;
        private readonly Polygon _flecheCible;
        private Point _a, _b;

        public LienVisuel(Lien modele)
        {
            Modele = modele;

            _zoneClic = new Line();
            _zoneClic.Stroke = Brushes.Transparent;
            _zoneClic.StrokeStartLineCap = PenLineCap.Round;
            _zoneClic.StrokeEndLineCap = PenLineCap.Round;
            Children.Add(_zoneClic);

            _ligne = new Line();
            _ligne.StrokeStartLineCap = PenLineCap.Round;
            _ligne.StrokeEndLineCap = PenLineCap.Round;
            _ligne.StrokeDashCap = PenLineCap.Round;
            _ligne.IsHitTestVisible = false;
            Children.Add(_ligne);

            _flecheSource = new Polygon();
            _flecheSource.IsHitTestVisible = false;
            Children.Add(_flecheSource);

            _flecheCible = new Polygon();
            _flecheCible.IsHitTestVisible = false;
            Children.Add(_flecheCible);

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
            Redessiner();
        }

        public void MettreAJourGeometrie(Point a, Point b)
        {
            _a = a;
            _b = b;
            Redessiner();
        }

        private void Redessiner()
        {
            _zoneClic.X1 = _a.X; _zoneClic.Y1 = _a.Y;
            _zoneClic.X2 = _b.X; _zoneClic.Y2 = _b.Y;

            Vector direction = _b - _a;
            double longueur = direction.Length;
            if (longueur < 1)
            {
                _ligne.X1 = _a.X; _ligne.Y1 = _a.Y;
                _ligne.X2 = _a.X; _ligne.Y2 = _a.Y;
                _flecheSource.Visibility = Visibility.Collapsed;
                _flecheCible.Visibility = Visibility.Collapsed;
                return;
            }
            direction.Normalize();

            double taille = 6 + Modele.Epaisseur * 2.2;
            bool versCible = Modele.Fleche == "simple" || Modele.Fleche == "double";
            bool versSource = Modele.Fleche == "double";

            // La ligne s'arrête à la base des flèches pour ne pas dépasser de leur pointe.
            Point debut = versSource ? _a + direction * (taille * 0.8) : _a;
            Point fin = versCible ? _b - direction * (taille * 0.8) : _b;
            _ligne.X1 = debut.X; _ligne.Y1 = debut.Y;
            _ligne.X2 = fin.X; _ligne.Y2 = fin.Y;

            _flecheCible.Visibility = versCible ? Visibility.Visible : Visibility.Collapsed;
            if (versCible) _flecheCible.Points = PointsFleche(_b, direction, taille);

            _flecheSource.Visibility = versSource ? Visibility.Visible : Visibility.Collapsed;
            if (versSource) _flecheSource.Points = PointsFleche(_a, -direction, taille);
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
