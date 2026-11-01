namespace Mentalo.Modele
{
    /// <summary>Une étiquette (tag) définie au niveau du document : un nom et une couleur.
    /// Les boîtes ne portent que les noms ; le filtre (Fenêtres > Filtre par étiquettes)
    /// estompe ce qui ne correspond pas.</summary>
    public class Etiquette
    {
        public string Nom { get; set; }
        public string Couleur { get; set; } // "#RRGGBB"

        public Etiquette()
        {
            Nom = "";
            Couleur = "#0078D7";
        }

        public Etiquette(string nom, string couleur)
        {
            Nom = nom ?? "";
            Couleur = string.IsNullOrEmpty(couleur) ? "#0078D7" : couleur;
        }

        public Etiquette Cloner()
        {
            return new Etiquette(Nom, Couleur);
        }

        /// <summary>La palette parcourue en boucle quand on crée une étiquette sans choisir sa couleur.</summary>
        public static readonly string[] Palette =
        {
            "#0078D7", "#2E8B57", "#E67E22", "#8E44AD", "#C0392B", "#D63384", "#17A2B8", "#6A665E"
        };
    }
}
