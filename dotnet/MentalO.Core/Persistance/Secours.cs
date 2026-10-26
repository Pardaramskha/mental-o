using System;
using System.Collections.Generic;
using System.IO;
using Mentalo.Modele;

namespace Mentalo.Persistance
{
    /// <summary>Copies de secours (v0.16) : toutes les minutes, une carte modifiée est
    /// recopiée dans %APPDATA%\Mental-o\secours\ (un .tea + un .info portant le chemin
    /// d'origine et l'heure). Un enregistrement réussi ou une fermeture normale effacent la
    /// copie ; au démarrage suivant, ce qui reste est proposé à la restauration.</summary>
    public static class Secours
    {
        public class Entree
        {
            public string Cle;      // nom de base des deux fichiers
            public string Chemin;   // chemin d'origine de la carte ("" = jamais enregistrée)
            public string Date;     // « aaaa-mm-jj hh:mm » de la copie
            public string Fichier;  // le .tea de secours

            public string NomAffiche
            {
                get { return string.IsNullOrEmpty(Chemin) ? "carte sans titre" : Path.GetFileName(Chemin); }
            }
        }

        private static string Dossier()
        {
            string dossier = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mental-o", "secours");
            Directory.CreateDirectory(dossier);
            return dossier;
        }

        public static string NouvelleCle()
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        /// <summary>Écrit (ou remplace) la copie de secours d'une carte.</summary>
        public static void Ecrire(DocumentCarte document, string cle, string cheminOrigine)
        {
            try
            {
                string dossier = Dossier();
                string tea = Path.Combine(dossier, cle + ".tea");
                string temporaire = tea + ".tmp";
                Fichier.Enregistrer(document, temporaire, false);
                if (File.Exists(tea)) File.Delete(tea);
                File.Move(temporaire, tea);
                File.WriteAllText(Path.Combine(dossier, cle + ".info"),
                    (cheminOrigine ?? "") + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            }
            catch
            {
                // Une copie de secours qui échoue ne doit jamais gêner le travail en cours.
            }
        }

        public static void Effacer(string cle)
        {
            if (string.IsNullOrEmpty(cle)) return;
            try
            {
                string dossier = Dossier();
                foreach (string suffixe in new string[] { ".tea", ".info", ".tea.tmp" })
                {
                    string chemin = Path.Combine(dossier, cle + suffixe);
                    if (File.Exists(chemin)) File.Delete(chemin);
                }
            }
            catch { }
        }

        /// <summary>Les copies laissées par une session interrompue, la plus récente d'abord.</summary>
        public static List<Entree> Lister()
        {
            List<Entree> entrees = new List<Entree>();
            try
            {
                foreach (string info in Directory.GetFiles(Dossier(), "*.info"))
                {
                    string cle = Path.GetFileNameWithoutExtension(info);
                    string tea = Path.Combine(Path.GetDirectoryName(info), cle + ".tea");
                    if (!File.Exists(tea)) { Effacer(cle); continue; }
                    string[] lignes = File.ReadAllText(info).Replace("\r", "").Split('\n');
                    Entree entree = new Entree();
                    entree.Cle = cle;
                    entree.Fichier = tea;
                    entree.Chemin = lignes.Length > 0 ? lignes[0].Trim() : "";
                    entree.Date = lignes.Length > 1 ? lignes[1].Trim() : "";
                    entrees.Add(entree);
                }
            }
            catch { }
            entrees.Sort(delegate(Entree a, Entree b) { return string.CompareOrdinal(b.Date, a.Date); });
            return entrees;
        }

        /// <summary>Range une copie hors du dossier de secours : « Mental-o récupéré … .tea »
        /// dans Documents. Renvoie le chemin écrit, ou null.</summary>
        public static string Exfiltrer(Entree entree)
        {
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string nom = Path.GetFileNameWithoutExtension(entree.NomAffiche);
                string cible = Path.Combine(documents, string.Format("Mental-o récupéré — {0} ({1}).tea",
                    nom, entree.Date.Replace(":", "h")));
                int n = 2;
                while (File.Exists(cible))
                    cible = Path.Combine(documents, string.Format("Mental-o récupéré — {0} ({1}) {2}.tea",
                        nom, entree.Date.Replace(":", "h"), n++));
                File.Copy(entree.Fichier, cible);
                Effacer(entree.Cle);
                return cible;
            }
            catch { return null; }
        }
    }
}
