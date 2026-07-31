using System;
using System.Collections.Generic;

namespace Mentalo.Modele
{
    /// <summary>La carte complète. Les groupes arriveront en v0.6.</summary>
    public class DocumentCarte
    {
        public List<Noeud> Noeuds { get; private set; }
        public List<Lien> Liens { get; private set; }
        public List<Groupe> Groupes { get; private set; }

        /// <summary>Le répertoire des couleurs personnalisées de CE fichier (hexa "#RRGGBB").</summary>
        public List<string> CouleursPersonnalisees { get; private set; }

        // Préférences du fichier (Fichier > Préférences de fichier)
        public double PaddingBoites { get; set; }
        public string PoliceParDefaut { get; set; }
        public double TailleParDefaut { get; set; }
        public string CouleurParDefaut { get; set; }
        public string FondCouleur { get; set; }
        public string FondMotif { get; set; }   // "uni" | "points" | "grille"
        public string NomAffichage { get; set; }
        public string Auteur { get; set; }
        public bool SauvegardeAuto { get; set; }
        public string DateCreation { get; set; }      // "aaaa-mm-jj hh:mm", posée au premier enregistrement
        public string DateModification { get; set; }  // mise à jour à chaque enregistrement

        public DocumentCarte()
        {
            Noeuds = new List<Noeud>();
            Liens = new List<Lien>();
            Groupes = new List<Groupe>();
            CouleursPersonnalisees = new List<string>();
            PaddingBoites = 9;
            PoliceParDefaut = "Segoe UI";
            TailleParDefaut = 14;
            CouleurParDefaut = "#1C1B19";
            FondCouleur = "#F7F5F0";
            FondMotif = "uni";
            NomAffichage = "";
            Auteur = "";
            DateCreation = "";
            DateModification = "";
        }

        /// <summary>Ajoute une couleur au répertoire du fichier. Renvoie faux si déjà présente.</summary>
        public bool AjouterCouleurPersonnalisee(string hexa)
        {
            if (string.IsNullOrEmpty(hexa)) return false;
            foreach (string existante in CouleursPersonnalisees)
                if (string.Equals(existante, hexa, StringComparison.OrdinalIgnoreCase)) return false;
            CouleursPersonnalisees.Add(hexa);
            return true;
        }

        public Noeud TrouverNoeud(string id)
        {
            foreach (Noeud n in Noeuds)
                if (n.Id == id) return n;
            return null;
        }
    }
}
