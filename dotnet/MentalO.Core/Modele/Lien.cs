using System;

namespace Mentalo.Modele
{
    /// <summary>Une connexion entre deux boîtes, avec son style.</summary>
    public class Lien
    {
        public string Id { get; set; }
        public string SourceId { get; set; }
        public string CibleId { get; set; }
        public string Couleur { get; set; }    // "#RRGGBB"
        public double Epaisseur { get; set; }
        public string Trait { get; set; }      // "plein" | "tirets" | "points"
        public string Fleche { get; set; }     // "aucune" | "simple" (vers la cible) | "double"
        public string Trace { get; set; }      // "droit" | "courbe" | "equerre"

        /// <summary>Bombé du tracé courbe : distance signée du point de contrôle à la
        /// médiatrice du segment (0 = droit). Réglée par la poignée de courbure.</summary>
        public double Courbure { get; set; }

        /// <summary>Texte posé au milieu du trait ("" = pas d'étiquette).</summary>
        public string Etiquette { get; set; }

        public Lien()
        {
            Id = Guid.NewGuid().ToString("N");
            Couleur = "#6A665E";
            Epaisseur = 2;
            Trait = "plein";
            Fleche = "aucune";
            Trace = "droit";
            Courbure = 0;
            Etiquette = "";
        }

        public bool Concerne(string idNoeud)
        {
            return SourceId == idNoeud || CibleId == idNoeud;
        }
    }
}
