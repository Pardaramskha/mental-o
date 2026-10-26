using System.IO;
using SkiaSharp;

namespace Mentalo.Vue
{
    /// <summary>Export PDF sans imprimante (26/10/2026) : la carte rendue en PNG est posée
    /// sur une page PDF à sa taille, via le moteur PDF de Skia (déjà embarqué par Avalonia).
    /// Remplace l'imprimante « Microsoft Print to PDF » de l'ancien Mental-o, propre à Windows.</summary>
    public static class ExportPdf
    {
        public static void Ecrire(byte[] png, string chemin)
        {
            using (SKBitmap image = SKBitmap.Decode(png))
            {
                if (image == null) throw new InvalidDataException("Rendu de la carte illisible.");
                // Points PDF (72 par pouce) ; la carte est rendue à 2× : on la pose à 50 % pour
                // retrouver la taille écran, avec une marge de 30 points.
                const double marge = 30;
                double largeur = image.Width / 2.0 + marge * 2;
                double hauteur = image.Height / 2.0 + marge * 2;
                using (FileStream flux = new FileStream(chemin, FileMode.Create))
                using (SKManagedWStream sortie = new SKManagedWStream(flux))
                using (SKDocument document = SKDocument.CreatePdf(sortie))
                {
                    SKCanvas canevas = document.BeginPage((float)largeur, (float)hauteur);
                    canevas.Clear(SKColors.White);
                    SKRect cible = new SKRect((float)marge, (float)marge, (float)(marge + image.Width / 2.0), (float)(marge + image.Height / 2.0));
                    using (SKPaint pinceau = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
                        canevas.DrawBitmap(image, cible, pinceau);
                    document.EndPage();
                    document.Close();
                }
            }
        }
    }
}
