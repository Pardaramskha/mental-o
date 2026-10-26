using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Input;
using Mentalo.Persistance;

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
        public bool? correcteur { get; set; }  // v0.16 : correcteur orthographique (absent = activé)
        public string langueCorrecteur { get; set; }
        public List<string> stylesFavoris { get; set; }
        public List<string> fichiersRecents { get; set; }
    }

    /// <summary>Paramètres de l'application, communs à toutes les cartes
    /// (%APPDATA%\Mental-o\parametres.json — le même fichier que l'ancien
    /// Mental-o WPF : les réglages de Rémi survivent au portage). En DLC de
    /// Marabook, l'hôte peut poser un autre dossier (DossierReglages).</summary>
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
        public static bool Correcteur = true;      // v0.16 : soulignement des fautes pendant la saisie
        public static string LangueCorrecteur = "fr-FR";

        /// <summary>Le dossier des réglages : %APPDATA%\Mental-o par défaut ; l'hôte
        /// (Marabook) peut en poser un autre avant Charger().</summary>
        public static string DossierReglages;

        /// <summary>Les langues proposées dans Paramètres > Édition.</summary>
        public static readonly string[,] LanguesCorrecteur =
        {
            { "Français (France)", "fr-FR" }, { "Français (Canada)", "fr-CA" }, { "Anglais (États-Unis)", "en-US" },
            { "Anglais (Royaume-Uni)", "en-GB" }, { "Allemand", "de-DE" }, { "Espagnol", "es-ES" }, { "Italien", "it-IT" }
        };

        public static string Dossier()
        {
            string dossier = DossierReglages;
            if (string.IsNullOrEmpty(dossier))
                dossier = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mental-o");
            Directory.CreateDirectory(dossier);
            return dossier;
        }

        /// <summary>Le dictionnaire personnel (mots ajoutés au clic droit), un mot par ligne.</summary>
        public static string CheminDictionnairePerso()
        {
            return Path.Combine(Dossier(), "dictionnaire.lex");
        }

        /// <summary>Ajoute un mot au dictionnaire personnel (fichier .lex, UTF-16 LE comme
        /// l'écrivait l'ancien Mental-o). Renvoie faux si déjà présent ou illisible.</summary>
        public static bool AjouterMotDictionnaire(string mot)
        {
            if (string.IsNullOrWhiteSpace(mot)) return false;
            mot = mot.Trim();
            try
            {
                string chemin = CheminDictionnairePerso();
                List<string> mots = new List<string>();
                if (File.Exists(chemin))
                    mots.AddRange(File.ReadAllLines(chemin, Encoding.Unicode));
                foreach (string existant in mots)
                    if (string.Equals(existant.Trim(), mot, StringComparison.Ordinal)) return false;
                mots.Add(mot);
                File.WriteAllLines(chemin, mots.ToArray(), Encoding.Unicode);
                return true;
            }
            catch { return false; }
        }
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
            return Path.Combine(Dossier(), "parametres.json");
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
                ParametresDto dto = Json.Deserialiser<ParametresDto>(File.ReadAllText(chemin));
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
                if (dto.correcteur.HasValue) Correcteur = dto.correcteur.Value;
                if (!string.IsNullOrEmpty(dto.langueCorrecteur)) LangueCorrecteur = dto.langueCorrecteur;
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
                dto.correcteur = Correcteur;
                dto.langueCorrecteur = LangueCorrecteur;
                dto.stylesFavoris = StylesFavoris;
                dto.fichiersRecents = FichiersRecents;
                File.WriteAllText(Chemin(), Json.Serialiser(dto));
            }
            catch { }
        }

        /// <summary>Exporte les raccourcis et favoris vers un profil json.</summary>
        public static void ExporterProfil(string chemin)
        {
            ParametresDto dto = new ParametresDto();
            dto.raccourcis = Raccourcis;
            dto.favoris = new List<string>(Favoris);
            File.WriteAllText(chemin, Json.Serialiser(dto));
        }

        /// <summary>Importe un profil de raccourcis ; renvoie faux si illisible.</summary>
        public static bool ImporterProfil(string chemin)
        {
            try
            {
                ParametresDto dto = Json.Deserialiser<ParametresDto>(File.ReadAllText(chemin));
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

        /// <summary>Le geste d'une action, prêt pour un menu (« Ctrl+Maj+G », ou "").</summary>
        public static string GesteAffiche(string id)
        {
            return AfficherGeste(Geste(id));
        }

        /// <summary>Un geste stocké → la touche et les modificateurs d'Avalonia. Les noms
        /// sont ceux de l'énumération Key (les mêmes qu'en WPF : D0, Add, OemPlus…).</summary>
        public static bool InterpreterGeste(string geste, out Key touche, out KeyModifiers modificateurs)
        {
            touche = Key.None;
            modificateurs = KeyModifiers.None;
            if (string.IsNullOrEmpty(geste)) return false;
            string[] parties = geste.Split('+');
            foreach (string brute in parties)
            {
                string partie = brute.Trim();
                if (partie == "Ctrl") modificateurs |= KeyModifiers.Control;
                else if (partie == "Shift" || partie == "Maj") modificateurs |= KeyModifiers.Shift;
                else if (partie == "Alt") modificateurs |= KeyModifiers.Alt;
                else
                {
                    Key lue;
                    if (!Enum.TryParse<Key>(partie, true, out lue)) return false;
                    touche = lue;
                }
            }
            return touche != Key.None;
        }

        public static string ComposerGeste(Key touche, KeyModifiers modificateurs)
        {
            string geste = "";
            if ((modificateurs & KeyModifiers.Control) != 0) geste += "Ctrl+";
            if ((modificateurs & KeyModifiers.Alt) != 0) geste += "Alt+";
            if ((modificateurs & KeyModifiers.Shift) != 0) geste += "Shift+";
            return geste + touche;
        }

        /// <summary>Vrai si la frappe correspond au geste de l'action (la rangée du
        /// haut vaut le pavé numérique pour + et -).</summary>
        public static bool Correspond(string id, Key touche, KeyModifiers modificateurs)
        {
            Key attendue;
            KeyModifiers attendus;
            if (!InterpreterGeste(Geste(id), out attendue, out attendus)) return false;
            KeyModifiers masque = KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt;
            if ((modificateurs & masque) != attendus) return false;
            if (touche == attendue) return true;
            if (attendue == Key.Add && touche == Key.OemPlus) return true;
            if (attendue == Key.Subtract && touche == Key.OemMinus) return true;
            return false;
        }

        /// <summary>L'id de l'action liée à cette frappe, ou null.</summary>
        public static string ActionPour(Key touche, KeyModifiers modificateurs)
        {
            foreach (DefinitionAction action in Actions)
                if (Correspond(action.Id, touche, modificateurs)) return action.Id;
            return null;
        }

        /// <summary>Le KeyGesture d'une action pour un MenuItem (null sans geste).</summary>
        public static KeyGesture GestureDe(string id)
        {
            Key touche;
            KeyModifiers modificateurs;
            if (!InterpreterGeste(Geste(id), out touche, out modificateurs)) return null;
            return new KeyGesture(touche, modificateurs);
        }
    }
}
