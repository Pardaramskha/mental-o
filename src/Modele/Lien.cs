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

        public Lien()
        {
            Id = Guid.NewGuid().ToString("N");
            Couleur = "#6A665E";
            Epaisseur = 2;
            Trait = "plein";
            Fleche = "aucune";
        }

        public bool Concerne(string idNoeud)
        {
            return SourceId == idNoeud || CibleId == idNoeud;
        }
    }
}
