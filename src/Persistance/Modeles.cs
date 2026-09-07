using System;
using System.Collections.Generic;
using System.IO;
using Mentalo.Modele;

namespace Mentalo.Persistance
{
    /// <summary>Modèles de cartes (v0.17) : des .tea rangés dans %APPDATA%\Mental-o\modeles\,
    /// proposés par Fichier > Nouveau depuis un modèle. Un modèle est une carte comme une autre
    /// (défauts de fichier, thème, boîtes de départ…) recopiée au démarrage d'une nouvelle carte.</summary>
    public static class Modeles
    {
        public static string Dossier()
        {
            string dossier = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mental-o", "modeles");
            Directory.CreateDirectory(dossier);
            return dossier;
        }

        /// <summary>Les modèles disponibles, triés par nom.</summary>
        public static List<string> Lister()
        {
            List<string> chemins = new List<string>();
            try { chemins.AddRange(Directory.GetFiles(Dossier(), "*.tea")); }
            catch { }
            chemins.Sort(delegate(string a, string b)
            {
                return string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.CurrentCultureIgnoreCase);
            });
            return chemins;
        }

        public static string NomDe(string chemin)
        {
            return Path.GetFileNameWithoutExtension(chemin);
        }

        /// <summary>Enregistre la carte comme modèle (sans versions ni horodatage). Les caractères
        /// interdits dans un nom de fichier sont remplacés. Renvoie le chemin écrit.</summary>
        public static string Enregistrer(DocumentCarte document, string nom)
        {
            string propre = (nom ?? "").Trim();
            foreach (char interdit in Path.GetInvalidFileNameChars())
                propre = propre.Replace(interdit, '-');
            if (propre.Length == 0) propre = "Modèle";
            string chemin = Path.Combine(Dossier(), propre + ".tea");
            Fichier.Enregistrer(document, chemin, false);
            return chemin;
        }

        public static bool Existe(string nom)
        {
            return File.Exists(Path.Combine(Dossier(), (nom ?? "").Trim() + ".tea"));
        }
    }
}
