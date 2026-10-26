using System.Collections.Generic;
using System.Text;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Les opérations pures sur les suites de segments (texte riche),
    /// partagées par l'éditeur de segments (cœur) et la toile (TexteRiche).</summary>
    public static class Segments
    {
        /// <summary>Le texte plat d'une suite de segments.</summary>
        public static string TextePlat(List<Segment> segments)
        {
            StringBuilder texte = new StringBuilder();
            foreach (Segment s in segments) texte.Append(s.Texte);
            return texte.ToString();
        }

        /// <summary>Fusionne les segments voisins de même style et retire les vides.</summary>
        public static List<Segment> Normaliser(List<Segment> segments)
        {
            List<Segment> resultat = new List<Segment>();
            foreach (Segment s in segments)
            {
                if (string.IsNullOrEmpty(s.Texte)) continue;
                if (resultat.Count > 0 && resultat[resultat.Count - 1].MemeStyleQue(s))
                    resultat[resultat.Count - 1].Texte += s.Texte;
                else
                    resultat.Add(s.Cloner());
            }
            return resultat;
        }
    }
}
