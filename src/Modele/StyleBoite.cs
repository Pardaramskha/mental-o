namespace Mentalo.Modele
{
    /// <summary>Le style visuel d'une boîte, groupé pour copier/coller et annuler.</summary>
    public class StyleBoite
    {
        public string Fond;
        public string BordureCouleur;
        public double BordureEpaisseur;
        public string BordureTrait;   // "plein" | "tirets" | "points" | "aucune"
        public double Rayon;
        public string Ombre;          // "aucune" | "legere" | "marquee"
        public double Opacite;        // 0,3 à 1

        public static StyleBoite Depuis(Noeud noeud)
        {
            StyleBoite style = new StyleBoite();
            style.Fond = noeud.FondCouleur;
            style.BordureCouleur = noeud.BordureCouleur;
            style.BordureEpaisseur = noeud.BordureEpaisseur;
            style.BordureTrait = noeud.BordureTrait;
            style.Rayon = noeud.Rayon;
            style.Ombre = noeud.Ombre;
            style.Opacite = noeud.Opacite;
            return style;
        }

        public void AppliquerA(Noeud noeud)
        {
            noeud.FondCouleur = Fond;
            noeud.BordureCouleur = BordureCouleur;
            noeud.BordureEpaisseur = BordureEpaisseur;
            noeud.BordureTrait = BordureTrait;
            noeud.Rayon = Rayon;
            noeud.Ombre = string.IsNullOrEmpty(Ombre) ? "legere" : Ombre;
            noeud.Opacite = Opacite > 0 ? Opacite : 1;
        }

        public bool EgalA(Noeud noeud)
        {
            return Fond == noeud.FondCouleur
                && BordureCouleur == noeud.BordureCouleur
                && BordureEpaisseur == noeud.BordureEpaisseur
                && BordureTrait == noeud.BordureTrait
                && Rayon == noeud.Rayon
                && (string.IsNullOrEmpty(Ombre) ? "legere" : Ombre) == noeud.Ombre
                && (Opacite > 0 ? Opacite : 1) == noeud.Opacite;
        }

        /// <summary>Forme compacte pour les styles favoris (parametres.json).</summary>
        public string Serialiser()
        {
            return Fond + "|" + BordureCouleur + "|"
                + BordureEpaisseur.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|"
                + BordureTrait + "|"
                + Rayon.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|"
                + (string.IsNullOrEmpty(Ombre) ? "legere" : Ombre) + "|"
                + (Opacite > 0 ? Opacite : 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static StyleBoite Deserialiser(string texte)
        {
            if (string.IsNullOrEmpty(texte)) return null;
            string[] parties = texte.Split('|');
            // 5 parties : favoris enregistrés avant l'arrivée des ombres et de l'opacité.
            if (parties.Length != 5 && parties.Length != 7) return null;
            StyleBoite style = new StyleBoite();
            style.Fond = parties[0];
            style.BordureCouleur = parties[1];
            style.BordureTrait = parties[3];
            double epaisseur, rayon;
            if (!double.TryParse(parties[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out epaisseur)) return null;
            if (!double.TryParse(parties[4], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out rayon)) return null;
            style.BordureEpaisseur = epaisseur;
            style.Rayon = rayon;
            style.Ombre = "legere";
            style.Opacite = 1;
            if (parties.Length == 7)
            {
                style.Ombre = parties[5];
                double opacite;
                if (double.TryParse(parties[6], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out opacite) && opacite > 0)
                    style.Opacite = opacite;
            }
            return style;
        }

        public StyleBoite Cloner()
        {
            StyleBoite copie = new StyleBoite();
            copie.Fond = Fond;
            copie.BordureCouleur = BordureCouleur;
            copie.BordureEpaisseur = BordureEpaisseur;
            copie.BordureTrait = BordureTrait;
            copie.Rayon = Rayon;
            copie.Ombre = Ombre;
            copie.Opacite = Opacite;
            return copie;
        }
    }
}
