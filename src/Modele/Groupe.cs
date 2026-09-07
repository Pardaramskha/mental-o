using System;
using System.Collections.Generic;

namespace Mentalo.Modele
{
    /// <summary>Un groupe de boîtes : elles se déplacent d'un bloc, dans un cadre stylable.</summary>
    public class Groupe
    {
        public string Id { get; set; }
        public List<string> NoeudIds { get; private set; }
        public string Couleur { get; set; } // couleur du cadre

        /// <summary>Taille imposée du cadre (poignée) ; 0 = ajustée au contenu.
        /// Le cadre ne descend jamais sous la taille de son contenu.</summary>
        public double LargeurFixe { get; set; }
        public double HauteurFixe { get; set; }

        /// <summary>Titre affiché sur le cadre ("" = aucun) et sa position :
        /// "haut-gauche" | "haut-droite" | "bas-gauche" | "bas-droite".</summary>
        public string Titre { get; set; }
        public string TitrePosition { get; set; }

        // Style du cadre et du titre
        public string BordureTrait { get; set; }     // "tirets" | "plein" | "points"
        public double BordureEpaisseur { get; set; }
        public string TitreCouleur { get; set; }     // "" = couleur du cadre
        public string TitreFond { get; set; }        // "" = fond du tableau
        public double TitreTaille { get; set; }

        /// <summary>Groupe plié (v0.15) : ses boîtes sont masquées, le cadre se réduit à une
        /// pastille-titre qui reçoit les liens venant de l'extérieur.</summary>
        public bool Plie { get; set; }

        public Groupe()
        {
            Id = Guid.NewGuid().ToString("N");
            NoeudIds = new List<string>();
            Couleur = "#8A94A8";
            Titre = "";
            TitrePosition = "haut-droite";
            BordureTrait = "tirets";
            BordureEpaisseur = 1.5;
            TitreCouleur = "";
            TitreFond = "";
            TitreTaille = 12.5;
        }

        public bool Contient(string idNoeud)
        {
            return NoeudIds.Contains(idNoeud);
        }
    }
}
