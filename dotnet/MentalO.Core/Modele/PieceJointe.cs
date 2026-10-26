using System;
using System.IO;

namespace Mentalo.Modele
{
    /// <summary>Un fichier attaché à une boîte (v0.14) : « tout ce qui n'est pas une image ».
    /// Il s'affiche comme un lien à la ligne, sous le texte. Deux états : lien externe
    /// (Chemin pointe vers le fichier d'origine, Donnees est null) ou intégré au .tea
    /// (Donnees porte les octets, écrits dans fichiers/ de l'archive — la carte reste
    /// complète même si l'on n'emporte que le .tea). Données pures, sérialisables.</summary>
    public class PieceJointe
    {
        public string Id { get; set; }

        /// <summary>Le libellé du lien (par défaut le nom du fichier, renommable).</summary>
        public string Nom { get; set; }

        /// <summary>Le chemin d'origine, absolu. Conservé même après intégration (pour
        /// « Ouvrir l'emplacement » ou revenir au lien) ; peut être null pour un fichier
        /// intégré dont l'origine a été perdue.</summary>
        public string Chemin { get; set; }

        /// <summary>Le nom de fichier d'origine avec son extension — sert à l'extraction
        /// et à l'ouverture d'un fichier intégré.</summary>
        public string NomFichier { get; set; }

        /// <summary>Les octets, quand le fichier est intégré au .tea ; null pour un lien externe.</summary>
        public byte[] Donnees { get; set; }

        public bool EstIntegre { get { return Donnees != null; } }

        /// <summary>Un hyperlien web (v0.15) : Chemin porte l'adresse, rien n'est intégrable.</summary>
        public bool EstUrl { get { return EstAdresseWeb(Chemin); } }

        public static bool EstAdresseWeb(string texte)
        {
            if (string.IsNullOrEmpty(texte)) return false;
            string bas = texte.Trim().ToLowerInvariant();
            return bas.StartsWith("http://") || bas.StartsWith("https://")
                || bas.StartsWith("mailto:") || bas.StartsWith("ftp://") || bas.StartsWith("www.");
        }

        /// <summary>Fabrique un hyperlien ; « www. » sans schéma reçoit https://.</summary>
        public static PieceJointe DepuisUrl(string url, string nom)
        {
            PieceJointe piece = new PieceJointe();
            string adresse = (url ?? "").Trim();
            if (adresse.ToLowerInvariant().StartsWith("www.")) adresse = "https://" + adresse;
            piece.Chemin = adresse;
            piece.NomFichier = "";
            piece.Nom = string.IsNullOrWhiteSpace(nom) ? LibelleDepuisUrl(adresse) : nom.Trim();
            return piece;
        }

        /// <summary>Un libellé lisible tiré de l'adresse : l'hôte sans « www. », plus le chemin s'il est court.</summary>
        public static string LibelleDepuisUrl(string adresse)
        {
            try
            {
                Uri uri = new Uri(adresse);
                string hote = uri.Host.StartsWith("www.") ? uri.Host.Substring(4) : uri.Host;
                string chemin = uri.AbsolutePath == "/" ? "" : uri.AbsolutePath.TrimEnd('/');
                if (uri.Scheme == "mailto") return uri.AbsoluteUri.Substring(7);
                return chemin.Length > 0 && chemin.Length <= 40 ? hote + chemin : hote;
            }
            catch { return adresse; }
        }

        public string Extension
        {
            get
            {
                if (EstUrl) return "";
                string source = !string.IsNullOrEmpty(NomFichier) ? NomFichier : Chemin;
                if (string.IsNullOrEmpty(source)) return "";
                try { return Path.GetExtension(source).ToLowerInvariant(); }
                catch { return ""; }
            }
        }

        /// <summary>Vrai si le lien externe pointe encore vers un fichier présent.</summary>
        public bool OrigineDisponible
        {
            get
            {
                if (string.IsNullOrEmpty(Chemin)) return false;
                if (EstUrl) return true; // on ne sonde pas le réseau : l'adresse vaut disponibilité
                try { return File.Exists(Chemin); }
                catch { return false; }
            }
        }

        public PieceJointe()
        {
            Id = Guid.NewGuid().ToString("N");
            Nom = "";
        }

        /// <summary>Fabrique un lien externe vers un fichier existant.</summary>
        public static PieceJointe DepuisChemin(string chemin)
        {
            PieceJointe piece = new PieceJointe();
            piece.Chemin = Path.GetFullPath(chemin);
            piece.NomFichier = Path.GetFileName(chemin);
            piece.Nom = piece.NomFichier;
            return piece;
        }

        /// <summary>Copie à l'identique (les octets, immuables, sont partagés).</summary>
        public PieceJointe Cloner()
        {
            PieceJointe copie = new PieceJointe();
            copie.Id = Id;
            copie.Nom = Nom;
            copie.Chemin = Chemin;
            copie.NomFichier = NomFichier;
            copie.Donnees = Donnees;
            return copie;
        }

        /// <summary>Copie avec un identifiant neuf (collage d'une boîte : deux liens ne
        /// doivent pas partager la même entrée dans l'archive).</summary>
        public PieceJointe ClonerNouvelId()
        {
            PieceJointe copie = Cloner();
            copie.Id = Guid.NewGuid().ToString("N");
            return copie;
        }

        /// <summary>Taille lisible (« 1,2 Mo », « 340 Ko ») des octets intégrés, ou du fichier
        /// d'origine s'il est accessible ; chaîne vide sinon.</summary>
        public string TailleLisible()
        {
            long octets = -1;
            if (EstUrl) return "";
            if (Donnees != null) octets = Donnees.LongLength;
            else if (OrigineDisponible)
            {
                try { octets = new FileInfo(Chemin).Length; }
                catch { octets = -1; }
            }
            if (octets < 0) return "";
            if (octets < 1024) return octets + " o";
            if (octets < 1048576) return string.Format("{0:0.#} Ko", octets / 1024.0);
            return string.Format("{0:0.#} Mo", octets / 1048576.0);
        }
    }
}
