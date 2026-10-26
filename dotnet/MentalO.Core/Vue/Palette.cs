using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>L'état de mise en forme au point d'insertion ou de la boîte sélectionnée,
    /// pour synchroniser la barre de formatage et les coches des menus.</summary>
    public class FormatCourant
    {
        public bool Actif;
        public string Police = "Segoe UI";
        public double Taille = 14;
        public bool Gras, Italique, Souligne, Barre;
        public string Couleur = "#1C1B19";
        public string Surlignage;
        public string Alignement = "gauche";

        // Style de la boîte sélectionnée
        public string FondBoite = "#FFFFFF";
        public string BordureCouleur = "#B8B2A7";
        public double BordureEpaisseur = 1.5;
        public string BordureTrait = "plein";
        public double Rayon = 5;
        public string Ombre = "legere";
        public double Opacite = 1;
    }

    /// <summary>Les palettes de l'interface et les conversions de couleurs — la part
    /// de l'ancien TexteRiche qui ne dépend d'aucune fenêtre.</summary>
    public static class Palette
    {
        public static readonly double[] Tailles = { 10, 12, 14, 16, 18, 20, 24, 28, 32, 36 };

        // { nom, hexa } — couleurs de texte
        public static readonly string[,] Couleurs =
        {
            { "Noir", "#1C1B19" }, { "Gris", "#6A665E" }, { "Bleu", "#0078D7" },
            { "Vert", "#2E8B57" }, { "Rouge", "#C0392B" }, { "Orange", "#E67E22" },
            { "Violet", "#8E44AD" }, { "Rose", "#D63384" }
        };

        // { nom, hexa } — couleurs de surlignage (pastel pour rester lisible)
        public static readonly string[,] Surlignages =
        {
            { "Jaune", "#FFF3A0" }, { "Vert clair", "#C8F0C8" }, { "Bleu clair", "#C5E4F7" },
            { "Rose clair", "#F7CBDC" }, { "Orange clair", "#FBE0C0" }, { "Gris clair", "#E4E0D8" }
        };

        public static Color CouleurDepuis(string hexa, Color defaut)
        {
            if (string.IsNullOrEmpty(hexa)) return defaut;
            if (string.Equals(hexa, "Transparent", StringComparison.OrdinalIgnoreCase)) return Colors.Transparent;
            Color couleur;
            return Color.TryParse(hexa, out couleur) ? couleur : defaut;
        }

        public static string HexaDepuisCouleur(Color couleur)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", couleur.R, couleur.G, couleur.B);
        }

        /// <summary>« #RRGGBB » normalisé, ou le repli si l'hexa est illisible.</summary>
        public static string CouleurSure(string hexa, string repli)
        {
            if (string.IsNullOrEmpty(hexa)) return repli;
            Color couleur;
            if (!Color.TryParse(hexa, out couleur))
                couleur = CouleurDepuis(repli, Colors.Gray);
            return HexaDepuisCouleur(couleur);
        }

        /// <summary>Texte clair sur une couleur sombre, encre sombre sur une claire.</summary>
        public static bool EstSombre(Color couleur)
        {
            return (0.299 * couleur.R + 0.587 * couleur.G + 0.114 * couleur.B) / 255.0 <= 0.62;
        }
    }

    /// <summary>Le tracé des liens — les calculs que la toile, les exports et les
    /// vignettes partagent (ex-statiques de LienVisuel).</summary>
    public static class GeometrieLien
    {
        /// <summary>Le point de contrôle du tracé courbe : perpendiculaire au segment,
        /// à « courbure » pixels de son milieu.</summary>
        public static Point ControleCourbe(Point a, Point b, double courbure)
        {
            Vector direction = b - a;
            if (direction.Length < 0.001) return a;
            direction = direction.Normalize();
            Vector perpendiculaire = new Vector(-direction.Y, direction.X);
            Point milieu = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            return milieu + perpendiculaire * courbure;
        }

        /// <summary>Le point du tracé à mi-parcours — position de l'étiquette et de la
        /// poignée de courbure.</summary>
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

        public static Point Centre(Rect rect)
        {
            return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        }

        /// <summary>Point où la ligne vers « vers » perce le bord du rectangle (avec 2 px de marge).</summary>
        public static Point PointAncrage(Rect rect, Point vers)
        {
            Point centre = Centre(rect);
            double dx = vers.X - centre.X, dy = vers.Y - centre.Y;
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001) return centre;
            double t = double.MaxValue;
            if (Math.Abs(dx) > 0.001) t = Math.Min(t, (rect.Width / 2 + 2) / Math.Abs(dx));
            if (Math.Abs(dy) > 0.001) t = Math.Min(t, (rect.Height / 2 + 2) / Math.Abs(dy));
            if (t > 1) t = 1; // boîtes qui se chevauchent : on relie les centres
            return new Point(centre.X + dx * t, centre.Y + dy * t);
        }

        /// <summary>Les trois points d'une flèche dont la pointe est « pointe ».</summary>
        public static Point[] PointsFleche(Point pointe, Vector direction, double taille)
        {
            Vector perpendiculaire = new Vector(-direction.Y, direction.X);
            Point basePointe = pointe - direction * taille;
            return new Point[]
            {
                pointe,
                basePointe + perpendiculaire * (taille * 0.45),
                basePointe - perpendiculaire * (taille * 0.45)
            };
        }
    }

    /// <summary>Les images des boîtes : extensions admises et taille plafond.</summary>
    public static class Images
    {
        public const long TailleMax = 5 * 1024 * 1024;
        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

        public static bool ExtensionValide(string chemin)
        {
            string extension = System.IO.Path.GetExtension(chemin ?? "").ToLowerInvariant();
            foreach (string valide in Extensions)
                if (extension == valide) return true;
            return false;
        }
    }
}
