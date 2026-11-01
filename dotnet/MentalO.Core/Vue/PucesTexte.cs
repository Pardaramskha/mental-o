using System.Collections.Generic;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Listes à puces (un niveau) : les marqueurs sont des préfixes de ligne
    /// portés par le texte lui-même — portables, stylables et sérialisés tels quels.</summary>
    public static class PucesTexte
    {
        public const string Puce = "• ";
        public const string Tiret = "– ";
        // Cases à cocher (v0.15) : deux marqueurs, la case se coche au clic dans la boîte affichée.
        public const string CaseVide = "☐ ";
        public const string CaseCochee = "☑ ";

        public static bool EstCase(string texte)
        {
            return texte.StartsWith(CaseVide) || texte.StartsWith(CaseCochee);
        }

        /// <summary>Inverse la case de la ligne demandée (index parmi les lignes découpées) ;
        /// renvoie la liste d'origine si la ligne ne porte pas de case.</summary>
        public static List<Segment> BasculerCase(List<Segment> source, int indexLigne)
        {
            List<List<Segment>> lignes = DecouperLignes(source);
            if (indexLigne < 0 || indexLigne >= lignes.Count) return source;
            List<Segment> ligne = lignes[indexLigne];
            string texte = TexteDeLigne(ligne);
            if (!EstCase(texte)) return source;
            bool cochee = texte.StartsWith(CaseCochee);
            RetirerMarqueur(ligne);
            Segment prefixe = ligne.Count > 0 ? ligne[0].Cloner() : new Segment();
            prefixe.Texte = cochee ? CaseVide : CaseCochee;
            ligne.Insert(0, prefixe);
            return RecomposerLignes(lignes);
        }

        /// <summary>Compte les cases (cochées, total) d'une suite de segments.</summary>
        public static void CompterCases(List<Segment> source, ref int cochees, ref int total)
        {
            foreach (List<Segment> ligne in DecouperLignes(source))
            {
                string texte = TexteDeLigne(ligne);
                if (texte.StartsWith(CaseCochee)) { cochees++; total++; }
                else if (texte.StartsWith(CaseVide)) total++;
            }
        }

        /// <summary>Bascule le type demandé ("puce" | "tiret" | "numero") sur toutes les lignes.
        /// Si toutes les lignes non vides portent déjà ce marqueur, il est retiré.</summary>
        public static List<Segment> Basculer(List<Segment> source, string type)
        {
            if (source.Count == 0) return new List<Segment>();
            List<List<Segment>> lignes = DecouperLignes(source);

            bool toutesMarquees = true;
            foreach (List<Segment> ligne in lignes)
            {
                string texte = TexteDeLigne(ligne);
                if (texte.Trim().Length == 0) continue;
                if (!PorteMarqueur(texte, type)) { toutesMarquees = false; break; }
            }

            int numero = 1;
            foreach (List<Segment> ligne in lignes)
            {
                RetirerMarqueur(ligne);
                if (toutesMarquees) continue;
                if (TexteDeLigne(ligne).Trim().Length == 0) continue;
                string marqueur = type == "tiret" ? Tiret
                    : type == "numero" ? numero++ + ". "
                    : type == "case" ? CaseVide
                    : Puce;
                Segment prefixe = ligne.Count > 0 ? ligne[0].Cloner() : new Segment();
                prefixe.Texte = marqueur;
                ligne.Insert(0, prefixe);
            }
            return RecomposerLignes(lignes);
        }

        private static bool PorteMarqueur(string texte, string type)
        {
            if (type == "puce") return texte.StartsWith(Puce);
            if (type == "tiret") return texte.StartsWith(Tiret);
            if (type == "case") return EstCase(texte);
            int position = 0;
            while (position < texte.Length && char.IsDigit(texte[position])) position++;
            return position > 0 && position + 1 < texte.Length
                && texte[position] == '.' && texte[position + 1] == ' ';
        }

        /// <summary>Longueur du marqueur connu (puce, tiret, numéro) au début du texte, 0 sinon.</summary>
        public static int LongueurMarqueur(string texte)
        {
            if (texte.StartsWith(Puce) || texte.StartsWith(Tiret) || EstCase(texte)) return 2;
            int position = 0;
            while (position < texte.Length && char.IsDigit(texte[position])) position++;
            if (position > 0 && position + 1 < texte.Length
                && texte[position] == '.' && texte[position + 1] == ' ')
                return position + 2;
            return 0;
        }

        public static bool PorteMarqueurPublic(string texte, string type)
        {
            return PorteMarqueur(texte, type);
        }

        /// <summary>Retire tout marqueur connu (puce, tiret, numéro) du début de la ligne,
        /// même s'il chevauche plusieurs segments.</summary>
        private static void RetirerMarqueur(List<Segment> ligne)
        {
            string texte = TexteDeLigne(ligne);
            int longueur = LongueurMarqueur(texte);
            while (longueur > 0 && ligne.Count > 0)
            {
                Segment premier = ligne[0];
                if (premier.Texte.Length <= longueur)
                {
                    longueur -= premier.Texte.Length;
                    ligne.RemoveAt(0);
                }
                else
                {
                    premier.Texte = premier.Texte.Substring(longueur);
                    longueur = 0;
                }
            }
        }

        private static string TexteDeLigne(List<Segment> ligne)
        {
            string texte = "";
            foreach (Segment s in ligne) texte += s.Texte;
            return texte;
        }

        public static List<List<Segment>> DecouperLignes(List<Segment> source)
        {
            List<List<Segment>> lignes = new List<List<Segment>>();
            List<Segment> courante = new List<Segment>();
            lignes.Add(courante);
            foreach (Segment s in source)
            {
                // Un « \r » résiduel (texte venu d'ailleurs) ne fait pas une ligne de plus (26/10/2026).
                string[] morceaux = s.Texte.Replace("\r", "").Split('\n');
                for (int i = 0; i < morceaux.Length; i++)
                {
                    if (i > 0)
                    {
                        courante = new List<Segment>();
                        lignes.Add(courante);
                    }
                    if (morceaux[i].Length == 0) continue;
                    Segment copie = s.Cloner();
                    copie.Texte = morceaux[i];
                    courante.Add(copie);
                }
            }
            return lignes;
        }

        private static List<Segment> RecomposerLignes(List<List<Segment>> lignes)
        {
            List<Segment> resultat = new List<Segment>();
            for (int i = 0; i < lignes.Count; i++)
            {
                foreach (Segment s in lignes[i])
                {
                    if (resultat.Count > 0 && resultat[resultat.Count - 1].MemeStyleQue(s))
                        resultat[resultat.Count - 1].Texte += s.Texte;
                    else
                        resultat.Add(s);
                }
                if (i < lignes.Count - 1)
                {
                    if (resultat.Count > 0) resultat[resultat.Count - 1].Texte += "\n";
                    else
                    {
                        Segment saut = new Segment();
                        saut.Texte = "\n";
                        resultat.Add(saut);
                    }
                }
            }
            return resultat;
        }
    }
}
