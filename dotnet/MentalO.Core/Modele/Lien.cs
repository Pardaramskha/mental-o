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
        public string Fleche { get; set; }     // "aucune" | "simple" (A vers B : pointe sur la cible) | "inverse" (B vers A : pointe sur la source, 27/10/2026) | "double"
        public string Trace { get; set; }      // "droit" | "courbe" | "equerre"

        /// <summary>Une pointe sur la boîte d'arrivée (cible).</summary>
        public bool FlecheVersCible { get { return Fleche == "simple" || Fleche == "double"; } }

        /// <summary>Une pointe sur la boîte de départ (source).</summary>
        public bool FlecheVersSource { get { return Fleche == "inverse" || Fleche == "double"; } }

        /// <summary>Un sens unique (une seule pointe) : le lien dit qui est le parent.</summary>
        public bool EstOriente { get { return Fleche == "simple" || Fleche == "inverse"; } }

        /// <summary>D'où part une flèche à sens unique (le parent), ou null.</summary>
        public string IdAmont { get { return Fleche == "simple" ? SourceId : Fleche == "inverse" ? CibleId : null; } }

        /// <summary>Où arrive une flèche à sens unique (l'enfant), ou null.</summary>
        public string IdAval { get { return Fleche == "simple" ? CibleId : Fleche == "inverse" ? SourceId : null; } }

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
