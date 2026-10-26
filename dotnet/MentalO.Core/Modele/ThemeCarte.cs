using System.Collections.Generic;

namespace Mentalo.Modele
{
    /// <summary>Un thème de carte (v0.17) : fond du tableau, texte par défaut et styles par
    /// défaut assortis des boîtes, liens et groupes. S'applique aux nouveaux éléments (défauts
    /// du fichier) et, au choix, aux éléments existants.</summary>
    public class ThemeCarte
    {
        public string Nom;
        public string Description;
        public string FondCouleur;
        public string FondMotif;         // "uni" | "points" | "grille"
        public string CouleurTexte;
        public string Police;
        public StyleBoite Boite;
        public string LienCouleur;
        public double LienEpaisseur;
        public string LienTrait;
        public string LienTrace;
        public string GroupeCouleur;
        public string GroupeTrait;
        public double GroupeEpaisseur;

        private static StyleBoite Style(string fond, string bordure, double epaisseur, string trait, double rayon, string ombre)
        {
            StyleBoite style = new StyleBoite();
            style.Fond = fond;
            style.BordureCouleur = bordure;
            style.BordureEpaisseur = epaisseur;
            style.BordureTrait = trait;
            style.Rayon = rayon;
            style.Ombre = ombre;
            style.Opacite = 1;
            return style;
        }

        private static ThemeCarte Faire(string nom, string description, string fond, string motif, string texte, string police,
            StyleBoite boite, string lienCouleur, double lienEpaisseur, string lienTrait, string lienTrace,
            string groupeCouleur, string groupeTrait, double groupeEpaisseur)
        {
            ThemeCarte t = new ThemeCarte();
            t.Nom = nom; t.Description = description;
            t.FondCouleur = fond; t.FondMotif = motif; t.CouleurTexte = texte; t.Police = police;
            t.Boite = boite;
            t.LienCouleur = lienCouleur; t.LienEpaisseur = lienEpaisseur; t.LienTrait = lienTrait; t.LienTrace = lienTrace;
            t.GroupeCouleur = groupeCouleur; t.GroupeTrait = groupeTrait; t.GroupeEpaisseur = groupeEpaisseur;
            return t;
        }

        /// <summary>Les thèmes livrés. Le premier reprend les défauts d'usine.</summary>
        public static List<ThemeCarte> Liste()
        {
            List<ThemeCarte> themes = new List<ThemeCarte>();
            themes.Add(Faire("Papier", "Les défauts d'usine : fond crème, boîtes blanches, liens gris droits.",
                "#F7F5F0", "uni", "#1C1B19", "Segoe UI",
                Style("#FFFFFF", "#B8B2A7", 1.5, "plein", 5, "legere"),
                "#6A665E", 2, "plein", "droit", "#8A94A8", "tirets", 1.5));
            themes.Add(Faire("Ardoise", "Fond bleu nuit, boîtes sombres au texte clair, liens bleus courbes.",
                "#16202E", "points", "#E8EAED", "Segoe UI",
                Style("#22303F", "#4C9AE8", 1.5, "plein", 6, "marquee"),
                "#4C9AE8", 2, "plein", "courbe", "#4C9AE8", "tirets", 1.5));
            themes.Add(Faire("Encre", "Noir sur blanc, coins carrés, traits nets : pour imprimer.",
                "#FFFFFF", "uni", "#000000", "Georgia",
                Style("#FFFFFF", "#1C1B19", 2, "plein", 0, "aucune"),
                "#1C1B19", 2, "plein", "equerre", "#1C1B19", "plein", 1.5));
            themes.Add(Faire("Menthe", "Fond vert d'eau quadrillé, accents verts, liens courbes.",
                "#EEF6F1", "grille", "#1F3A2E", "Segoe UI",
                Style("#FFFFFF", "#2E8B57", 1.5, "plein", 8, "legere"),
                "#2E8B57", 2, "plein", "courbe", "#2E8B57", "points", 1.5));
            themes.Add(Faire("Pastel", "Post-it jaunes sans bordure sur fond doux, liens orange.",
                "#FBF7EF", "uni", "#3A3833", "Segoe UI",
                Style("#FFF3A0", "#FFF3A0", 1, "aucune", 3, "marquee"),
                "#E67E22", 2.5, "plein", "courbe", "#E67E22", "tirets", 1.5));
            themes.Add(Faire("Craie", "Tableau noir : boîtes transparentes bordées de blanc, traits crayeux.",
                "#2B2F33", "uni", "#F4F1EB", "Segoe UI",
                Style("#2B2F33", "#F4F1EB", 1.5, "tirets", 4, "aucune"),
                "#F4F1EB", 2, "points", "droit", "#F4F1EB", "points", 1.5));
            return themes;
        }
    }
}
