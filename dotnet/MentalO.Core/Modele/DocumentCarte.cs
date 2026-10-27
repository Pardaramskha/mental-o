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

        /// <summary>Les étiquettes définies dans CE fichier (v0.15) : nom + couleur.</summary>
        public List<Etiquette> Etiquettes { get; set; }

        public Etiquette TrouverEtiquette(string nom)
        {
            foreach (Etiquette e in Etiquettes)
                if (string.Equals(e.Nom, nom, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        /// <summary>La couleur d'une étiquette par son nom (accent bleu si elle n'est plus définie).</summary>
        public string CouleurEtiquette(string nom)
        {
            Etiquette e = TrouverEtiquette(nom);
            return e == null ? "#0078D7" : e.Couleur;
        }

        /// <summary>La prochaine couleur de la palette, en boucle sur celles déjà prises.</summary>
        public string ProchaineCouleurEtiquette()
        {
            return Etiquette.Palette[Etiquettes.Count % Etiquette.Palette.Length];
        }

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

        // Styles par défaut des nouveaux éléments (Fichier > Préférences de fichier),
        // enregistrés dans le .tea. « Utiliser ce style pour les nouvelles boîtes »
        // (clic droit) écrit dans BoiteParDefaut.
        public StyleBoite BoiteParDefaut { get; set; }
        public string LienCouleurDefaut { get; set; }
        public double LienEpaisseurDefaut { get; set; }
        public string LienTraitDefaut { get; set; }   // "plein" | "tirets" | "points"
        public string LienFlecheDefaut { get; set; }  // "aucune" | "simple" | "inverse" | "double"
        public string LienTraceDefaut { get; set; }   // "droit" | "courbe" | "equerre"
        public string GroupeCouleurDefaut { get; set; }
        public string GroupeTraitDefaut { get; set; } // "tirets" | "plein" | "points"
        public double GroupeEpaisseurDefaut { get; set; }

        /// <summary>Fabrique un lien neuf portant les défauts du fichier.</summary>
        public Lien NouveauLien()
        {
            Lien lien = new Lien();
            if (!string.IsNullOrEmpty(LienCouleurDefaut)) lien.Couleur = LienCouleurDefaut;
            if (LienEpaisseurDefaut > 0) lien.Epaisseur = LienEpaisseurDefaut;
            if (!string.IsNullOrEmpty(LienTraitDefaut)) lien.Trait = LienTraitDefaut;
            if (!string.IsNullOrEmpty(LienFlecheDefaut)) lien.Fleche = LienFlecheDefaut;
            if (!string.IsNullOrEmpty(LienTraceDefaut)) lien.Trace = LienTraceDefaut;
            if (lien.Trace == "courbe") lien.Courbure = 46;
            return lien;
        }

        /// <summary>Fabrique un groupe neuf portant les défauts du fichier.</summary>
        public Groupe NouveauGroupe()
        {
            Groupe groupe = new Groupe();
            if (!string.IsNullOrEmpty(GroupeCouleurDefaut)) groupe.Couleur = GroupeCouleurDefaut;
            if (!string.IsNullOrEmpty(GroupeTraitDefaut)) groupe.BordureTrait = GroupeTraitDefaut;
            if (GroupeEpaisseurDefaut > 0) groupe.BordureEpaisseur = GroupeEpaisseurDefaut;
            return groupe;
        }

        public DocumentCarte()
        {
            Noeuds = new List<Noeud>();
            Liens = new List<Lien>();
            Groupes = new List<Groupe>();
            CouleursPersonnalisees = new List<string>();
            Etiquettes = new List<Etiquette>();
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
            BoiteParDefaut = StyleBoite.Depuis(new Noeud()); // les défauts « d'usine »
            LienCouleurDefaut = "#6A665E";
            LienEpaisseurDefaut = 2;
            LienTraitDefaut = "plein";
            LienFlecheDefaut = "aucune";
            LienTraceDefaut = "droit";
            GroupeCouleurDefaut = "#8A94A8";
            GroupeTraitDefaut = "tirets";
            GroupeEpaisseurDefaut = 1.5;
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
