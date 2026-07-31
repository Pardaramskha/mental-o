using System.Windows.Media;

namespace Mentalo.Vue
{
    /// <summary>Pinceaux partagés du « chrome » de l'interface (barres, panneaux, libellés).
    /// Ils sont mutables : basculer le thème recolore instantanément toute l'application,
    /// puisque chaque élément référence la même instance.</summary>
    public static class Chrome
    {
        public static readonly SolidColorBrush FondFenetre = Pinceau(0xF7, 0xF4, 0xEE);
        public static readonly SolidColorBrush FondBarre = Pinceau(0xEF, 0xEC, 0xE6);
        public static readonly SolidColorBrush FondBarreLegere = Pinceau(0xF4, 0xF1, 0xEB);
        public static readonly SolidColorBrush Bordure = Pinceau(0xD5, 0xD0, 0xC8);
        public static readonly SolidColorBrush TexteDoux = Pinceau(0x6A, 0x66, 0x5E);
        public static readonly SolidColorBrush Encre = Pinceau(0x3A, 0x38, 0x33);
        public static readonly SolidColorBrush EncreIcone = Pinceau(0x3A, 0x38, 0x33);
        public static readonly SolidColorBrush FondPanneau = PinceauAlpha(0xF2, 0xFF, 0xFF, 0xFF);
        public static readonly SolidColorBrush FondMiniCarte = Pinceau(0xF7, 0xF5, 0xF0);

        public static void Basculer(bool sombre)
        {
            if (sombre)
            {
                FondFenetre.Color = Couleur(0x21, 0x26, 0x2F);
                FondBarre.Color = Couleur(0x26, 0x2B, 0x34);
                FondBarreLegere.Color = Couleur(0x2B, 0x30, 0x39);
                Bordure.Color = Couleur(0x3B, 0x41, 0x4C);
                TexteDoux.Color = Couleur(0x9B, 0xA3, 0xAF);
                Encre.Color = Couleur(0xE8, 0xEA, 0xED);
                EncreIcone.Color = Couleur(0xE8, 0xEA, 0xED);
                FondPanneau.Color = Color.FromArgb(0xF0, 0x26, 0x2B, 0x34);
                FondMiniCarte.Color = Couleur(0x16, 0x20, 0x2E);
            }
            else
            {
                FondFenetre.Color = Couleur(0xF7, 0xF4, 0xEE);
                FondBarre.Color = Couleur(0xEF, 0xEC, 0xE6);
                FondBarreLegere.Color = Couleur(0xF4, 0xF1, 0xEB);
                Bordure.Color = Couleur(0xD5, 0xD0, 0xC8);
                TexteDoux.Color = Couleur(0x6A, 0x66, 0x5E);
                Encre.Color = Couleur(0x3A, 0x38, 0x33);
                EncreIcone.Color = Couleur(0x3A, 0x38, 0x33);
                FondPanneau.Color = Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF);
                FondMiniCarte.Color = Couleur(0xF7, 0xF5, 0xF0);
            }
        }

        private static SolidColorBrush Pinceau(byte r, byte v, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, v, b));
        }

        private static SolidColorBrush PinceauAlpha(byte a, byte r, byte v, byte b)
        {
            return new SolidColorBrush(Color.FromArgb(a, r, v, b));
        }

        private static Color Couleur(byte r, byte v, byte b)
        {
            return Color.FromRgb(r, v, b);
        }
    }
}
