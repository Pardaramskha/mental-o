using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Mentalo.Historique;
using Mentalo.Modele;
using Mentalo.Persistance;

namespace Mentalo.Vue
{
    /// <summary>Notes cachées, étiquettes, hyperliens web et cases à cocher (v0.15).
    /// Tout passe par des commandes annulables.</summary>
    public partial class ToileVue
    {
        // ------------------------------------------------------------------ notes cachées

        public void DefinirNoteNoeud(Noeud noeud, string note)
        {
            noeud.Note = note ?? "";
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel)) visuel.MettreAJour();
            SignalerModification();
        }

        /// <summary>Ouvre l'éditeur de note de la boîte (clic droit > Note…, icône de note).</summary>
        public async void EditerNote(Noeud noeud)
        {
            if (noeud == null || _mode == "lecture") return;
            string nouvelle;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                nouvelle = await DialogueSaisie.DemanderTexteMultiligne(FenetreProprietaire(),
                    "Note de la boîte", "Note cachée (visible en infobulle sur l'icône, hors de la carte) :",
                    noeud.Note ?? "", "Enregistrer");
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
            if (nouvelle == null) return;
            nouvelle = nouvelle.Trim();
            if (nouvelle == (noeud.Note ?? "")) return;
            Historique.ExecuterEtPousser(new CommandeModifierNote(this, noeud, nouvelle));
        }

        public void EditerNoteSelection()
        {
            if (_selection != null) EditerNote(_selection.Modele);
        }

        private void NoeudNoteCliquee(NoeudVisuel visuel)
        {
            if (!_selectionnes.Contains(visuel)) Selectionner(visuel);
            EditerNote(visuel.Modele);
        }

        // ------------------------------------------------------------------ étiquettes

        public void DefinirEtiquettesNoeud(Noeud noeud, List<string> etiquettes)
        {
            noeud.Etiquettes = etiquettes == null ? new List<string>() : new List<string>(etiquettes);
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel)) visuel.MettreAJour();
            RafraichirEstompageDe(noeud);
            SignalerModification();
        }

        public void DefinirDefinitionsEtiquettes(List<Etiquette> definitions)
        {
            Document.Etiquettes = new List<Etiquette>();
            foreach (Etiquette e in definitions) Document.Etiquettes.Add(e.Cloner());
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
                if (kv.Key.Etiquettes.Count > 0) kv.Value.MettreAJour();
            AppliquerEstompage();
            SignalerModification();
            Action h = EtiquettesChange;
            if (h != null) h();
        }

        /// <summary>Les définitions d'étiquettes ont changé (menus, panneau de filtre).</summary>
        public event Action EtiquettesChange;

        /// <summary>Pose ou retire une étiquette sur toute la sélection : si toutes les boîtes
        /// la portent déjà, on la retire ; sinon on la pose partout.</summary>
        public void BasculerEtiquetteSelection(string nom)
        {
            if (_mode == "lecture" || string.IsNullOrWhiteSpace(nom)) return;
            List<Noeud> cibles = ModelesSelectionnes();
            if (_groupeActif != null) cibles = NoeudsDuGroupe(_groupeActif);
            if (cibles.Count == 0) return;
            bool toutesPortent = true;
            foreach (Noeud n in cibles)
                if (!n.Etiquettes.Contains(nom)) { toutesPortent = false; break; }
            List<ICommande> commandes = new List<ICommande>();
            foreach (Noeud n in cibles)
            {
                List<string> nouvelles = new List<string>(n.Etiquettes);
                if (toutesPortent) nouvelles.Remove(nom);
                else if (!nouvelles.Contains(nom)) nouvelles.Add(nom);
                else continue;
                commandes.Add(new CommandeModifierEtiquettes(this, n, nouvelles));
            }
            if (commandes.Count == 0) return;
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));
        }

        /// <summary>Crée une étiquette (couleur de la palette si non précisée) et la pose sur la sélection.</summary>
        public void CreerEtiquette(string nom, string couleur)
        {
            nom = (nom ?? "").Trim();
            if (nom.Length == 0) return;
            if (Document.TrouverEtiquette(nom) == null)
            {
                List<Etiquette> definitions = ClonerDefinitions();
                definitions.Add(new Etiquette(nom, string.IsNullOrEmpty(couleur) ? Document.ProchaineCouleurEtiquette() : couleur));
                Historique.ExecuterEtPousser(new CommandeModifierDefinitionsEtiquettes(this, definitions, null));
            }
            bool selectionPorte = true;
            List<Noeud> cibles = _groupeActif != null ? NoeudsDuGroupe(_groupeActif) : ModelesSelectionnes();
            foreach (Noeud n in cibles)
                if (!n.Etiquettes.Contains(nom)) selectionPorte = false;
            if (cibles.Count > 0 && !selectionPorte) BasculerEtiquetteSelection(nom);
        }

        public void RenommerEtiquette(string ancien, string nouveau)
        {
            nouveau = (nouveau ?? "").Trim();
            if (nouveau.Length == 0 || nouveau == ancien || Document.TrouverEtiquette(ancien) == null) return;
            if (Document.TrouverEtiquette(nouveau) != null) return; // déjà pris
            List<Etiquette> definitions = ClonerDefinitions();
            foreach (Etiquette e in definitions)
                if (e.Nom == ancien) e.Nom = nouveau;
            Dictionary<Noeud, List<string>> porteurs = new Dictionary<Noeud, List<string>>();
            foreach (Noeud n in Document.Noeuds)
            {
                if (!n.Etiquettes.Contains(ancien)) continue;
                List<string> nouvelles = new List<string>();
                foreach (string nom in n.Etiquettes) nouvelles.Add(nom == ancien ? nouveau : nom);
                porteurs[n] = nouvelles;
            }
            Historique.ExecuterEtPousser(new CommandeModifierDefinitionsEtiquettes(this, definitions, porteurs));
        }

        public void ColorerEtiquette(string nom, string couleur)
        {
            if (string.IsNullOrEmpty(couleur) || Document.TrouverEtiquette(nom) == null) return;
            List<Etiquette> definitions = ClonerDefinitions();
            foreach (Etiquette e in definitions)
                if (e.Nom == nom) e.Couleur = couleur;
            Historique.ExecuterEtPousser(new CommandeModifierDefinitionsEtiquettes(this, definitions, null));
        }

        public void SupprimerEtiquette(string nom)
        {
            if (Document.TrouverEtiquette(nom) == null) return;
            List<Etiquette> definitions = ClonerDefinitions();
            definitions.RemoveAll(delegate(Etiquette e) { return e.Nom == nom; });
            Dictionary<Noeud, List<string>> porteurs = new Dictionary<Noeud, List<string>>();
            foreach (Noeud n in Document.Noeuds)
            {
                if (!n.Etiquettes.Contains(nom)) continue;
                List<string> nouvelles = new List<string>(n.Etiquettes);
                nouvelles.Remove(nom);
                porteurs[n] = nouvelles;
            }
            Historique.ExecuterEtPousser(new CommandeModifierDefinitionsEtiquettes(this, definitions, porteurs));
        }

        private List<Etiquette> ClonerDefinitions()
        {
            List<Etiquette> copie = new List<Etiquette>();
            foreach (Etiquette e in Document.Etiquettes) copie.Add(e.Cloner());
            return copie;
        }

        /// <summary>Le sous-menu « Étiquettes » des menus contextuels (boîte ou groupe) : une
        /// case par étiquette du fichier, cochée si toute la cible la porte, puis création et gestion.</summary>
        /// <summary>Le sous-menu des étiquettes de la sélection (cocher / décocher,
        /// nouvelle, gérer) ; public pour le bouton « Étiquettes » d'un hôte (27/10/2026).</summary>
        public MenuItem ConstruireMenuEtiquettes()
        {
            MenuItem racine = Ui.SousMenu("Étiquettes");
            List<Noeud> cibles = _groupeActif != null ? NoeudsDuGroupe(_groupeActif) : ModelesSelectionnes();
            foreach (Etiquette e in Document.Etiquettes)
            {
                string nom = e.Nom;
                bool toutes = cibles.Count > 0;
                foreach (Noeud n in cibles)
                    if (!n.Etiquettes.Contains(nom)) { toutes = false; break; }
                MenuItem element = Ui.Element(nom, delegate { BasculerEtiquetteSelection(nom); }, toutes);
                if (!toutes) element.Icon = Ui.PastilleRonde(e.Couleur, 12);
                racine.Items.Add(element);
            }
            if (Document.Etiquettes.Count > 0) racine.Items.Add(Ui.Separateur());
            racine.Items.Add(Ui.Element("Nouvelle étiquette…", async delegate
            {
                string nom;
                NoeudVisuel.ProtectionFocus = true;
                try
                {
                    nom = await DialogueSaisie.DemanderTexte(FenetreProprietaire(), "Nouvelle étiquette", "Nom de l'étiquette :", "");
                }
                finally { NoeudVisuel.ProtectionFocus = false; }
                if (!string.IsNullOrWhiteSpace(nom)) CreerEtiquette(nom, null);
            }));
            racine.Items.Add(Ui.Element("Gérer les étiquettes…", delegate { DialogueEtiquettes.Ouvrir(FenetreProprietaire(), this); }));
            return racine;
        }

        /// <summary>Nombre de boîtes portant chaque étiquette (panneau de filtre, dialogue).</summary>
        public int CompterPorteurs(string nom)
        {
            int total = 0;
            foreach (Noeud n in Document.Noeuds)
                if (n.Etiquettes.Contains(nom)) total++;
            return total;
        }

        // ------------------------------------------------------------------ hyperliens web

        /// <summary>Insertion > Lien web… : demande l'adresse, l'attache à la boîte sélectionnée
        /// ou crée une boîte neuve au centre.</summary>
        public async void InsererLienWeb()
        {
            if (_mode == "lecture") return;
            NoeudVisuel cible = _selection;
            string url;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                url = await DialogueSaisie.DemanderTexte(FenetreProprietaire(), "Insérer un lien web",
                    "Adresse (https://…) :", "https://");
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
            if (cible != null) cible.RedonnerFocus();
            if (url == null) return;
            url = url.Trim();
            if (url.Length == 0 || url == "https://" || url == "http://") return;
            if (!PieceJointe.EstAdresseWeb(url)) url = "https://" + url;
            if (cible != null) AttacherUrl(cible.Modele, url, null);
            else
            {
                Point centre = CentreCanvas();
                InsererUrlNouvelleBoite(url, null, new Point(centre.X - 80, centre.Y - 14));
            }
        }

        private void AttacherUrl(Noeud noeud, string url, string libelle)
        {
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            nouveaux.Add(PieceJointe.DepuisUrl(url, libelle));
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        private void InsererUrlNouvelleBoite(string url, string libelle, Point position)
        {
            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.Fichiers.Add(PieceJointe.DepuisUrl(url, libelle));
            noeud.X = position.X;
            noeud.Y = position.Y;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel)) Selectionner(visuel);
        }

        /// <summary>Un texte déposé sur la toile : adresse web → lien ; autre texte → boîte de texte.</summary>
        private void DeposerTexte(string texte, NoeudVisuel cible, Point position)
        {
            if (string.IsNullOrWhiteSpace(texte)) return;
            string propre = texte.Trim();
            if (PieceJointe.EstAdresseWeb(propre) && !propre.Contains("\n") && !propre.Contains(" "))
            {
                if (cible != null) AttacherUrl(cible.Modele, propre, null);
                else InsererUrlNouvelleBoite(propre, null, position);
                return;
            }
            // Un texte à puces indenté devient un arbre ; un texte simple, une boîte.
            if (propre.Contains("\n") && Export.RessembleAUnPlan(propre))
            {
                List<Noeud> noeuds; List<Lien> liens;
                Export.ImporterTexteAPuces(propre, position, out noeuds, out liens);
                ImporterArbre(noeuds, liens);
                return;
            }
            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.DefinirTexteSimple(propre);
            noeud.X = position.X;
            noeud.Y = position.Y;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel)) Selectionner(visuel);
        }

        /// <summary>Le menu d'un hyperlien web (variante du menu des fichiers).</summary>
        private void OuvrirMenuUrl(NoeudVisuel visuel, PieceJointe piece, Control ancre)
        {
            Noeud noeud = visuel.Modele;
            if (!_selectionnes.Contains(visuel)) Selectionner(visuel);
            ContextMenu menu = new ContextMenu();
            MenuItem entete = Ui.Element(piece.Chemin.Length > 60 ? piece.Chemin.Substring(0, 60) + "…" : piece.Chemin, null);
            entete.IsEnabled = false;
            entete.FontStyle = FontStyle.Italic;
            menu.Items.Add(entete);
            menu.Items.Add(Ui.Separateur());

            MenuItem ouvrir = Ui.Element("Ouvrir dans le navigateur", delegate { OuvrirFichier(piece); });
            ouvrir.FontWeight = FontWeight.SemiBold;
            menu.Items.Add(ouvrir);

            menu.Items.Add(Ui.Element("Copier l'adresse", async delegate
            {
                try
                {
                    IClipboard presse = Ui.PressePapier(this);
                    if (presse != null) await presse.SetTextAsync(piece.Chemin ?? "");
                }
                catch { }
            }));
            menu.Items.Add(Ui.Separateur());
            menu.Items.Add(Ui.Element("Renommer le lien…", delegate { RenommerLien(noeud, piece); }));
            menu.Items.Add(Ui.Element("Modifier l'adresse…", delegate { ModifierAdresse(noeud, piece); }));
            menu.Items.Add(Ui.Separateur());
            menu.Items.Add(Ui.Element("Supprimer le lien", delegate { SupprimerLien(noeud, piece); }));
            Ui.OuvrirMenuSous(menu, ancre);
        }

        private async void ModifierAdresse(Noeud noeud, PieceJointe piece)
        {
            string url;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                url = await DialogueSaisie.DemanderTexte(FenetreProprietaire(), "Modifier l'adresse", "Adresse (https://…) :", piece.Chemin);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
            if (url == null) return;
            url = url.Trim();
            if (url.Length == 0 || url == piece.Chemin) return;
            if (!PieceJointe.EstAdresseWeb(url)) url = "https://" + url;
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            PieceJointe cible = TrouverCopie(nouveaux, piece);
            if (cible == null) return;
            bool libelleParDefaut = cible.Nom == PieceJointe.LibelleDepuisUrl(cible.Chemin);
            cible.Chemin = url;
            if (libelleParDefaut) cible.Nom = PieceJointe.LibelleDepuisUrl(url);
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        // ------------------------------------------------------------------ cases à cocher

        /// <summary>Clic sur une case dans la boîte affichée : inverse la case de cette ligne
        /// (en lecture aussi : c'est de la consultation active).</summary>
        private void NoeudCaseCliquee(NoeudVisuel visuel, bool apres, int ligne)
        {
            Noeud noeud = visuel.Modele;
            List<Segment> source = apres ? noeud.SegmentsApres : noeud.Segments;
            List<Segment> nouveaux = PucesTexte.BasculerCase(source, ligne);
            if (ReferenceEquals(nouveaux, source)) return;
            Historique.ExecuterEtPousser(new CommandeModifierTexteRiche(this, noeud,
                Noeud.CopieDe(noeud.Segments), Noeud.CopieDe(noeud.SegmentsApres), noeud.Alignement,
                apres ? Noeud.CopieDe(noeud.Segments) : nouveaux,
                apres ? nouveaux : Noeud.CopieDe(noeud.SegmentsApres),
                noeud.Alignement));
            // La progression d'un groupe se lit dans son titre.
            Groupe groupe = GroupeDuNoeud(noeud);
            if (groupe != null) MettreAJourCadreGroupe(groupe);
        }

        /// <summary>Cases cochées / total d'une boîte (inspecteur).</summary>
        public static string ProgressionBoite(Noeud noeud)
        {
            int cochees = 0, total = 0;
            PucesTexte.CompterCases(noeud.Segments, ref cochees, ref total);
            PucesTexte.CompterCases(noeud.SegmentsApres, ref cochees, ref total);
            return total == 0 ? "" : cochees + "/" + total;
        }
    }
}
