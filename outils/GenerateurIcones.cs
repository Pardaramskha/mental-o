using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Mentalo.Outils
{
    /// <summary>Génère ressources\mentalo.ico (application) et ressources\fichier-tea.ico
    /// (type de fichier), en dessinant un motif de carte mentale à plusieurs tailles.
    /// À lancer depuis la racine du projet (voir generer-icones.bat).</summary>
    public static class GenerateurIcones
    {
        private static readonly Color BleuFonce = Color.FromArgb(0x35, 0x54, 0x8F);
        private static readonly Color BleuClair = Color.FromArgb(0x5C, 0x83, 0xC5);
        private static readonly Color BleuMotif = Color.FromArgb(0x3F, 0x63, 0xA8);
        private static readonly Color GrisBord = Color.FromArgb(0xA9, 0xA2, 0x97);

        public static void Main()
        {
            Directory.CreateDirectory("ressources");
            int[] tailles = { 16, 24, 32, 48, 64, 128, 256 };

            // Les PNG « -2 » fournis par l'utilisateur priment sur les dessins générés.
            string sourceApplication = Path.Combine("ressources", "apercu-mentalo-2.png");
            string sourceFichier = Path.Combine("ressources", "apercu-fichier-zeubi-2.png");

            if (File.Exists(sourceApplication))
                EcrireIcoDepuisPng(sourceApplication, Path.Combine("ressources", "mentalo.ico"), tailles);
            else
                EcrireIco(Path.Combine("ressources", "mentalo.ico"), tailles, DessinerIconeApplication);

            if (File.Exists(sourceFichier))
                EcrireIcoDepuisPng(sourceFichier, Path.Combine("ressources", "fichier-tea.ico"), tailles);
            else
                EcrireIco(Path.Combine("ressources", "fichier-tea.ico"), tailles, DessinerIconeFichier);

            Console.WriteLine("Icones generees dans ressources\\");
        }

        /// <summary>Construit un .ico multi-tailles à partir d'un PNG source
        /// (réductions et agrandissements en interpolation haute qualité).</summary>
        private static void EcrireIcoDepuisPng(string source, string chemin, int[] tailles)
        {
            List<byte[]> images = new List<byte[]>();
            using (Bitmap original = new Bitmap(source))
            {
                foreach (int taille in tailles)
                {
                    using (Bitmap redimensionne = new Bitmap(taille, taille, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(redimensionne))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.Transparent);
                            g.DrawImage(original, new Rectangle(0, 0, taille, taille));
                        }
                        using (MemoryStream memoire = new MemoryStream())
                        {
                            redimensionne.Save(memoire, ImageFormat.Png);
                            images.Add(memoire.ToArray());
                        }
                    }
                }
            }
            EcrireFichierIco(chemin, tailles, images);
        }

        // ------------------------------------------------------------------ dessins

        /// <summary>Icône de l'application : trois boîtes reliées sur fond bleu arrondi.</summary>
        private static void DessinerIconeApplication(Graphics g, int taille)
        {
            float f = taille / 256f;
            using (GraphicsPath fond = CheminRectArrondi(8 * f, 8 * f, 240 * f, 240 * f, 52 * f))
            using (LinearGradientBrush pinceau = new LinearGradientBrush(
                new PointF(0, 0), new PointF(0, taille), BleuClair, BleuFonce))
            {
                g.FillPath(pinceau, fond);
            }

            using (Pen stylo = new Pen(Color.White, Math.Max(1f, 10 * f)))
            {
                stylo.StartCap = LineCap.Round;
                stylo.EndCap = LineCap.Round;
                g.DrawLine(stylo, 76 * f, 128 * f, 185 * f, 74 * f);
                g.DrawLine(stylo, 76 * f, 128 * f, 185 * f, 182 * f);
            }
            RemplirBoite(g, Color.White, 40 * f, 104 * f, 72 * f, 48 * f, 12 * f);
            RemplirBoite(g, Color.White, 152 * f, 52 * f, 66 * f, 44 * f, 10 * f);
            RemplirBoite(g, Color.White, 152 * f, 160 * f, 66 * f, 44 * f, 10 * f);
        }

        /// <summary>Icône des fichiers .zeubi : page au coin plié avec le motif en bleu.</summary>
        private static void DessinerIconeFichier(Graphics g, int taille)
        {
            float f = taille / 256f;

            PointF[] page =
            {
                new PointF(52 * f, 12 * f), new PointF(168 * f, 12 * f),
                new PointF(212 * f, 56 * f), new PointF(212 * f, 244 * f),
                new PointF(52 * f, 244 * f)
            };
            using (GraphicsPath chemin = new GraphicsPath())
            {
                chemin.AddPolygon(page);
                g.FillPath(Brushes.White, chemin);
                using (Pen bord = new Pen(GrisBord, Math.Max(1f, 8 * f)))
                {
                    bord.LineJoin = LineJoin.Round;
                    g.DrawPath(bord, chemin);
                }
            }

            PointF[] pli =
            {
                new PointF(168 * f, 12 * f), new PointF(168 * f, 56 * f), new PointF(212 * f, 56 * f)
            };
            using (SolidBrush fondPli = new SolidBrush(Color.FromArgb(0xE4, 0xE0, 0xD8)))
                g.FillPolygon(fondPli, pli);
            using (Pen bordPli = new Pen(GrisBord, Math.Max(1f, 6 * f)))
            {
                bordPli.LineJoin = LineJoin.Round;
                g.DrawPolygon(bordPli, pli);
            }

            using (Pen stylo = new Pen(BleuMotif, Math.Max(1f, 8 * f)))
            {
                stylo.StartCap = LineCap.Round;
                stylo.EndCap = LineCap.Round;
                g.DrawLine(stylo, 104 * f, 151 * f, 166 * f, 110 * f);
                g.DrawLine(stylo, 104 * f, 151 * f, 166 * f, 190 * f);
            }
            RemplirBoite(g, BleuMotif, 76 * f, 134 * f, 56 * f, 34 * f, 8 * f);
            RemplirBoite(g, BleuMotif, 140 * f, 94 * f, 52 * f, 32 * f, 8 * f);
            RemplirBoite(g, BleuMotif, 140 * f, 174 * f, 52 * f, 32 * f, 8 * f);
        }

        private static void RemplirBoite(Graphics g, Color couleur, float x, float y, float largeur, float hauteur, float rayon)
        {
            using (GraphicsPath chemin = CheminRectArrondi(x, y, largeur, hauteur, rayon))
            using (SolidBrush pinceau = new SolidBrush(couleur))
                g.FillPath(pinceau, chemin);
        }

        private static GraphicsPath CheminRectArrondi(float x, float y, float largeur, float hauteur, float rayon)
        {
            GraphicsPath chemin = new GraphicsPath();
            if (rayon < 0.5f)
            {
                chemin.AddRectangle(new RectangleF(x, y, largeur, hauteur));
                return chemin;
            }
            float d = rayon * 2;
            chemin.AddArc(x, y, d, d, 180, 90);
            chemin.AddArc(x + largeur - d, y, d, d, 270, 90);
            chemin.AddArc(x + largeur - d, y + hauteur - d, d, d, 0, 90);
            chemin.AddArc(x, y + hauteur - d, d, d, 90, 90);
            chemin.CloseFigure();
            return chemin;
        }

        // ------------------------------------------------------------------ écriture des fichiers

        private static Bitmap Dessiner(int taille, Action<Graphics, int> dessiner)
        {
            Bitmap bitmap = new Bitmap(taille, taille, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                dessiner(g, taille);
            }
            return bitmap;
        }

        /// <summary>Format ICO à entrées PNG (géré nativement par Windows Vista et suivants).</summary>
        private static void EcrireIco(string chemin, int[] tailles, Action<Graphics, int> dessiner)
        {
            List<byte[]> images = new List<byte[]>();
            foreach (int taille in tailles)
            {
                using (Bitmap bitmap = Dessiner(taille, dessiner))
                using (MemoryStream memoire = new MemoryStream())
                {
                    bitmap.Save(memoire, ImageFormat.Png);
                    images.Add(memoire.ToArray());
                }
            }
            EcrireFichierIco(chemin, tailles, images);
        }

        private static void EcrireFichierIco(string chemin, int[] tailles, List<byte[]> images)
        {
            using (BinaryWriter ecrivain = new BinaryWriter(File.Create(chemin)))
            {
                ecrivain.Write((ushort)0);                 // réservé
                ecrivain.Write((ushort)1);                 // type : icône
                ecrivain.Write((ushort)tailles.Length);
                int decalage = 6 + 16 * tailles.Length;
                for (int i = 0; i < tailles.Length; i++)
                {
                    int t = tailles[i] >= 256 ? 0 : tailles[i]; // 0 = 256 dans le format ICO
                    ecrivain.Write((byte)t);
                    ecrivain.Write((byte)t);
                    ecrivain.Write((byte)0);               // palette
                    ecrivain.Write((byte)0);               // réservé
                    ecrivain.Write((ushort)1);             // plans
                    ecrivain.Write((ushort)32);            // bits par pixel
                    ecrivain.Write(images[i].Length);
                    ecrivain.Write(decalage);
                    decalage += images[i].Length;
                }
                foreach (byte[] png in images)
                    ecrivain.Write(png);
            }
        }

        private static void EcrireApercu(string chemin, Action<Graphics, int> dessiner)
        {
            using (Bitmap bitmap = Dessiner(128, dessiner))
                bitmap.Save(chemin, ImageFormat.Png);
        }
    }
}
