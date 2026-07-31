namespace Mentalo.Modele
{
    /// <summary>Un morceau de texte d'un nœud avec son style. Le texte riche d'une boîte
    /// est une suite de segments ; les retours à la ligne vivent dans Texte (« \n »).</summary>
    public class Segment
    {
        public string Texte { get; set; }
        public string Police { get; set; }
        public double Taille { get; set; }
        public string Couleur { get; set; }     // "#RRGGBB"
        public bool Gras { get; set; }
        public bool Italique { get; set; }
        public bool Souligne { get; set; }
        public bool Barre { get; set; }
        public string Surlignage { get; set; }  // "#RRGGBB" ou null (aucun)

        /// <summary>Alignement de la ligne à laquelle appartient ce segment
        /// ("gauche"/"centre"/"droite" ; ""/null = alignement de la boîte). Tous les
        /// segments d'une même ligne portent la même valeur.</summary>
        public string AlignementLigne { get; set; }

        // Valeurs par défaut, pilotées par les préférences du fichier ouvert.
        public static string PoliceDefaut = "Segoe UI";
        public static double TailleDefaut = 14;
        public static string CouleurDefaut = "#1C1B19";

        public Segment()
        {
            Texte = "";
            Police = PoliceDefaut;
            Taille = TailleDefaut;
            Couleur = CouleurDefaut;
        }

        public Segment Cloner()
        {
            Segment copie = new Segment();
            copie.Texte = Texte;
            copie.Police = Police;
            copie.Taille = Taille;
            copie.Couleur = Couleur;
            copie.Gras = Gras;
            copie.Italique = Italique;
            copie.Souligne = Souligne;
            copie.Barre = Barre;
            copie.Surlignage = Surlignage;
            copie.AlignementLigne = AlignementLigne;
            return copie;
        }

        public bool MemeStyleQue(Segment autre)
        {
            return Police == autre.Police
                && Taille == autre.Taille
                && Couleur == autre.Couleur
                && Gras == autre.Gras
                && Italique == autre.Italique
                && Souligne == autre.Souligne
                && Barre == autre.Barre
                && Surlignage == autre.Surlignage
                && (AlignementLigne ?? "") == (autre.AlignementLigne ?? "");
        }
    }
}
