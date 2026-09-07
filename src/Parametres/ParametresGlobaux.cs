using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Input;

namespace Mentalo.Parametres
{
    /// <summary>Une action re-liable à un raccourci dans Fichier > Paramètres.</summary>
    public class DefinitionAction
    {
        public string Id, Categorie, Nom, GesteParDefaut;

        public DefinitionAction(string id, string categorie, string nom, string gesteParDefaut)
        {
            Id = id;
            Categorie = categorie;
            Nom = nom;
            GesteParDefaut = gesteParDefaut;
        }
    }

    public class ParametresDto
    {
        public Dictionary<string, string> raccourcis { get; set; }
        public List<string> favoris { get; set; }
        public bool tustVisible { get; set; }
        public bool miniCarteVisible { get; set; }
        public bool inspecteurVisible { get; set; }
        public bool historiqueVisible { get; set; }
        public bool filtreVisible { get; set; }
        public bool listesAutomatiques { get; set; }
        public string taillePoignees { get; set; }
        public string vitesseZoom { get; set; }
        public bool themeSombre { get; set; }
        public bool? vignette { get; set; }    // nullable : absent = activée
        public bool? magnetisme { get; set; }  // idem
        public List<string> stylesFavoris { get; set; }
        public List<string> fichiersRecents { get; set; }
    }

    /// <summary>Paramètres de l'application, communs à toutes les cartes
    /// (%APPDATA%\Mental-o\parametres.json).</summary>
    public static class ParametresGlobaux
    {
        // Les sélections de contenu (polices, couleurs…) n'ont pas leur place ici.
        public static readonly DefinitionAction[] Actions =
        {
            new DefinitionAction("nouveau", "Fichier", "Nouveau", "Ctrl+N"),
            new DefinitionAction("ouvrir", "Fichier", "Ouvrir", "Ctrl+O"),
            new DefinitionAction("enregistrer", "Fichier", "Enregistrer", "Ctrl+S"),
            new DefinitionAction("enregistrer-sous", "Fichier", "Enregistrer sous", "Ctrl+Shift+S"),
            new DefinitionAction("annuler", "Édition", "Annuler", "Ctrl+Z"),
            new DefinitionAction("retablir", "Édition", "Rétablir", "Ctrl+Y"),
            new DefinitionAction("copier", "Édition", "Copier les boîtes", "Ctrl+C"),
            new DefinitionAction("coller", "Édition", "Coller", "Ctrl+V"),
            new DefinitionAction("dupliquer", "Édition", "Dupliquer", "Ctrl+D"),
            new DefinitionAction("rechercher", "Édition", "Rechercher", "Ctrl+F"),
            new DefinitionAction("editer", "Édition", "Modifier la boîte sélectionnée", "F2"),
            new DefinitionAction("supprimer", "Édition", "Supprimer la sélection", "Delete"),
            new DefinitionAction("grouper", "Édition", "Grouper", "Ctrl+G"),
            new DefinitionAction("degrouper", "Édition", "Dégrouper", "Ctrl+Shift+G"),
            new DefinitionAction("imprimer", "Fichier", "Imprimer", "Ctrl+P"),
            new DefinitionAction("empiler", "Édition", "Empiler la sélection", null),
            new DefinitionAction("inserer-image", "Insertion", "Insérer une image", "Ctrl+Shift+I"),
            new DefinitionAction("inserer-fichier", "Insertion", "Insérer un fichier", "Ctrl+Shift+F"),
            new DefinitionAction("inserer-lien-web", "Insertion", "Insérer un lien web", "Ctrl+Shift+L"),
            new DefinitionAction("tout-selectionner", "Édition", "Tout sélectionner", "Ctrl+A"),
            new DefinitionAction("note", "Édition", "Note de la boîte", "Ctrl+Shift+N"),
            new DefinitionAction("plier", "Édition", "Plier / déplier le groupe", "Ctrl+Shift+P"),
            new DefinitionAction("zoom-tout", "Affichage", "Tout voir", "Ctrl+Shift+D0"),
            new DefinitionAction("zoom-selection", "Affichage", "Zoom sur la sélection", "Ctrl+Shift+D1"),
            new DefinitionAction("focus", "Affichage", "Mettre en avant la sélection", "Ctrl+Shift+M"),
            new DefinitionAction("zoom-avant", "Affichage", "Zoom avant", "Ctrl+Add"),
            new DefinitionAction("zoom-arriere", "Affichage", "Zoom arrière", "Ctrl+Subtract"),
            new DefinitionAction("zoom-100", "Affichage", "Zoom 100 %", "Ctrl+D0"),
            new DefinitionAction("recentrer", "Affichage", "Recentrer la carte", "Ctrl+R"),
            new DefinitionAction("plein-ecran", "Affichage", "Plein écran", "F11"),
            new DefinitionAction("presentation", "Affichage", "Mode présentation", "F5"),
            new DefinitionAction("mode-automatique", "Modes", "Mode automatique", "Ctrl+D1"),
            new DefinitionAction("mode-deplacement", "Modes", "Mode déplacement", "Ctrl+D2"),
            new DefinitionAction("mode-liens", "Modes", "Mode liens", "Ctrl+D3"),
            new DefinitionAction("mode-lecture", "Modes", "Mode lecture", "Ctrl+D4"),
            new DefinitionAction("gras", "Texte", "Gras", "Ctrl+B"),
            new DefinitionAction("italique", "Texte", "Italique", "Ctrl+I"),
            new DefinitionAction("souligne", "Texte", "Souligné", "Ctrl+U"),
        };

        public static Dictionary<string, string> Raccourcis = new Dictionary<string, string>();
        public static HashSet<string> Favoris = new HashSet<string>();
        public static bool TustVisible;
        public static bool MiniCarteVisible;
        public static bool InspecteurVisible;
        public static bool HistoriqueVisible;
        public static bool FiltreVisible; // panneau « Filtre par étiquettes » (v0.15)
        public static bool ListesAutomatiques; // « * »/« - » + espace → puce (désactivé par défaut)
        public static string TaillePoignees = "normale"; // "petite" | "normale" | "grande"
        public static string VitesseZoom = "normale";    // "lente" | "normale" | "rapide"
        public static bool ThemeSombre;
        public static bool VignetteTableau = true; // légère ombre sur les bords du tableau
        public static bool Magnetisme = true;      // aimant + guides pendant le glisser (Alt pour ignorer)
        public static List<string> StylesFavoris = new List<string>(); // StyleBoite.Serialiser()
        public static List<string> FichiersRecents = new List<string>(); // 5 derniers .tea ouverts/enregistrés

        /// <summary>Pose un chemin en tête des fichiers récents (5 au plus, sans doublon).</summary>
        public static void AjouterFichierRecent(string chemin)
        {
            if (string.IsNullOrEmpty(chemin)) return;
            FichiersRecents.RemoveAll(delegate(string existant)
            {
                return string.Equals(existant, chemin, StringComparison.OrdinalIgnoreCase);
            });
            FichiersRecents.Insert(0, chemin);
            while (FichiersRecents.Count > 5)
                FichiersRecents.RemoveAt(FichiersRecents.Count - 1);
        }

        public static double FacteurPoignees()
        {
            return TaillePoignees == "petite" ? 0.8 : TaillePoignees == "grande" ? 1.35 : 1.0;
        }

        public static double FacteurZoom()
        {
            return VitesseZoom == "lente" ? 1.05 : VitesseZoom == "rapide" ? 1.2 : 1.1;
        }

        private static string Chemin()
        {
            string dossier = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mental-o");
            Directory.CreateDirectory(dossier);
            return Path.Combine(dossier, "parametres.json");
        }

        public static DefinitionAction Definition(string id)
        {
            foreach (DefinitionAction action in Actions)
                if (action.Id == id) return action;
            return null;
        }

        /// <summary>Le geste effectif d'une action : personnalisé, sinon celui par défaut.</summary>
        public static string Geste(string id)
        {
            string geste;
            if (Raccourcis.TryGetValue(id, out geste)) return geste;
            DefinitionAction definition = Definition(id);
            return definition == null ? null : definition.GesteParDefaut;
        }

        public static void Charger()
        {
            try
            {
                string chemin = Chemin();
                if (!File.Exists(chemin)) return;
                ParametresDto dto = new JavaScriptSerializer()
                    .Deserialize<ParametresDto>(File.ReadAllText(chemin));
                if (dto == null) return;
                if (dto.raccourcis != null) Raccourcis = dto.raccourcis;
                if (dto.favoris != null) Favoris = new HashSet<string>(dto.favoris);
                TustVisible = dto.tustVisible;
                MiniCarteVisible = dto.miniCarteVisible;
                InspecteurVisible = dto.inspecteurVisible;
                HistoriqueVisible = dto.historiqueVisible;
                FiltreVisible = dto.filtreVisible;
                ListesAutomatiques = dto.listesAutomatiques;
                if (!string.IsNullOrEmpty(dto.taillePoignees)) TaillePoignees = dto.taillePoignees;
                if (!string.IsNullOrEmpty(dto.vitesseZoom)) VitesseZoom = dto.vitesseZoom;
                ThemeSombre = dto.themeSombre;
                if (dto.vignette.HasValue) VignetteTableau = dto.vignette.Value;
                if (dto.magnetisme.HasValue) Magnetisme = dto.magnetisme.Value;
                if (dto.stylesFavoris != null) StylesFavoris = dto.stylesFavoris;
                if (dto.fichiersRecents != null) FichiersRecents = dto.fichiersRecents;
            }
            catch { }
        }

        public static void Enregistrer()
        {
            try
            {
                ParametresDto dto = new ParametresDto();
                dto.raccourcis = Raccourcis;
                dto.favoris = new List<string>(Favoris);
                dto.tustVisible = TustVisible;
                dto.miniCarteVisible = MiniCarteVisible;
                dto.inspecteurVisible = InspecteurVisible;
                dto.historiqueVisible = HistoriqueVisible;
                dto.filtreVisible = FiltreVisible;
                dto.listesAutomatiques = ListesAutomatiques;
                dto.taillePoignees = TaillePoignees;
                dto.vitesseZoom = VitesseZoom;
                dto.themeSombre = ThemeSombre;
                dto.vignette = VignetteTableau;
                dto.magnetisme = Magnetisme;
                dto.stylesFavoris = StylesFavoris;
                dto.fichiersRecents = FichiersRecents;
                File.WriteAllText(Chemin(), new JavaScriptSerializer().Serialize(dto));
            }
            catch { }
        }

        /// <summary>Exporte les raccourcis et favoris vers un profil json.</summary>
        public static void ExporterProfil(string chemin)
        {
            ParametresDto dto = new ParametresDto();
            dto.raccourcis = Raccourcis;
            dto.favoris = new List<string>(Favoris);
            File.WriteAllText(chemin, new JavaScriptSerializer().Serialize(dto));
        }

        /// <summary>Importe un profil de raccourcis ; renvoie faux si illisible.</summary>
        public static bool ImporterProfil(string chemin)
        {
            try
            {
                ParametresDto dto = new JavaScriptSerializer()
                    .Deserialize<ParametresDto>(File.ReadAllText(chemin));
                if (dto == null) return false;
                if (dto.raccourcis != null) Raccourcis = dto.raccourcis;
                if (dto.favoris != null) Favoris = new HashSet<string>(dto.favoris);
                Enregistrer();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Les gestes utilisés par plusieurs actions, pour l'avertissement de conflit.</summary>
        public static List<string> DecrireConflits()
        {
            Dictionary<string, List<string>> parGeste = new Dictionary<string, List<string>>();
            foreach (DefinitionAction action in Actions)
            {
                string geste = Geste(action.Id);
                if (string.IsNullOrEmpty(geste)) continue;
                if (!parGeste.ContainsKey(geste)) parGeste[geste] = new List<string>();
                parGeste[geste].Add(action.Nom);
            }
            List<string> conflits = new List<string>();
            foreach (KeyValuePair<string, List<string>> kv in parGeste)
                if (kv.Value.Count > 1)
                    conflits.Add(AfficherGeste(kv.Key) + " : " + string.Join(", ", kv.Value.ToArray()));
            return conflits;
        }

        // ------------------------------------------------------------------ gestes

        /// <summary>Stockage invariant ("Ctrl+Shift+G") → affichage français ("Ctrl+Maj+G").</summary>
        public static string AfficherGeste(string geste)
        {
            if (string.IsNullOrEmpty(geste)) return "";
            string[] parties = geste.Split('+');
            for (int i = 0; i < parties.Length; i++)
            {
                string p = parties[i];
                if (p == "Shift") parties[i] = "Maj";
                else if (p == "Delete") parties[i] = "Suppr";
                else if (p == "Add") parties[i] = "+ (pavé)";
                else if (p == "Subtract") parties[i] = "- (pavé)";
                else if (p == "OemPlus") parties[i] = "=";
                else if (p == "OemMinus") parties[i] = "-";
                else if (p.Length == 2 && p[0] == 'D' && char.IsDigit(p[1])) parties[i] = p[1].ToString();
            }
            return string.Join("+", parties);
        }

        public static bool InterpreterGeste(string geste, out Key touche, out ModifierKeys modificateurs)
        {
            touche = Key.None;
            modificateurs = ModifierKeys.None;
            if (string.IsNullOrEmpty(geste)) return false;
            string[] parties = geste.Split('+');
            foreach (string brute in parties)
            {
                string partie = brute.Trim();
                if (partie == "Ctrl") modificateurs |= ModifierKeys.Control;
                else if (partie == "Shift" || partie == "Maj") modificateurs |= ModifierKeys.Shift;
                else if (partie == "Alt") modificateurs |= ModifierKeys.Alt;
                else
                {
                    try { touche = (Key)Enum.Parse(typeof(Key), partie, true); }
                    catch { return false; }
                }
            }
            return touche != Key.None;
        }

        public static string ComposerGeste(Key touche, ModifierKeys modificateurs)
        {
            string geste = "";
            if ((modificateurs & ModifierKeys.Control) != 0) geste += "Ctrl+";
            if ((modificateurs & ModifierKeys.Alt) != 0) geste += "Alt+";
            if ((modificateurs & ModifierKeys.Shift) != 0) geste += "Shift+";
            return geste + touche;
        }
    }
}
