using System.Collections.Generic;
using Avalonia;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Plier / déplier un groupe (v0.15). Plié, ses boîtes sont masquées et le
    /// cadre devient une pastille-titre ; les liens qui arrivaient sur une boîte du groupe
    /// se rabattent sur la pastille, ceux qui restaient à l'intérieur disparaissent.
    /// Les positions des boîtes ne bougent pas : déplier remet tout en place.</summary>
    public partial class ToileVue
    {
        private const double HauteurPastille = 30;

        /// <summary>Le point d'entrée de la commande d'annulation.</summary>
        public void DefinirPliageGroupe(Groupe groupe, bool plie)
        {
            groupe.Plie = plie;
            AppliquerVisibilitePliage(groupe);
            if (_groupeActif == groupe) MettreAJourGrip();
            SignalerModification();
            SignalerFormatCible();
        }

        /// <summary>Le groupe plié qui masque cette boîte, ou null.</summary>
        public Groupe GroupePlieDe(string idNoeud)
        {
            foreach (Groupe groupe in Document.Groupes)
                if (groupe.Plie && groupe.Contient(idNoeud)) return groupe;
            return null;
        }

        public bool NoeudMasque(Noeud noeud)
        {
            return GroupePlieDe(noeud.Id) != null;
        }

        /// <summary>Masque ou montre les boîtes du groupe selon son état, puis recale le cadre
        /// (pastille ou cadre plein) et les liens concernés.</summary>
        private void AppliquerVisibilitePliage(Groupe groupe)
        {
            bool retireDeSelection = false;
            foreach (string id in groupe.NoeudIds)
                retireDeSelection |= AppliquerVisibiliteNoeud(id, groupe.Plie);
            if (retireDeSelection)
            {
                _selection = _selectionnes.Count > 0 ? _selectionnes[0] : null;
                MettreAJourGrip();
            }
            MettreAJourCadreGroupe(groupe);
            foreach (Lien lien in Document.Liens)
                if (groupe.Contient(lien.SourceId) || groupe.Contient(lien.CibleId) || lien.Concerne(groupe.Id))
                    MettreAJourGeometrieLien(lien);
        }

        /// <summary>Montre ou masque une boîte ; renvoie vrai si elle a quitté la sélection.</summary>
        private bool AppliquerVisibiliteNoeud(string id, bool masquer)
        {
            Noeud noeud = Document.TrouverNoeud(id);
            NoeudVisuel visuel;
            if (noeud == null || !_visuels.TryGetValue(noeud, out visuel)) return false;
            if (masquer && visuel.EnEdition) visuel.TerminerEdition(true);
            visuel.IsVisible = !masquer;
            if (masquer && _selectionnes.Contains(visuel))
            {
                _selectionnes.Remove(visuel);
                visuel.DefinirSelection(false);
                return true;
            }
            return false;
        }

        /// <summary>Après un changement d'appartenance : une boîte qui rejoint un groupe plié
        /// se masque, une boîte qui le quitte réapparaît.</summary>
        private void RecalerVisibiliteMembres(Groupe groupe, List<string> ids)
        {
            foreach (string id in ids)
                AppliquerVisibiliteNoeud(id, groupe.Plie && groupe.Contient(id));
            foreach (Lien lien in Document.Liens)
                foreach (string id in ids)
                    if (lien.Concerne(id)) { MettreAJourGeometrieLien(lien); break; }
        }

        public void BasculerPliageGroupe(Groupe groupe)
        {
            if (groupe == null) return;
            Historique.ExecuterEtPousser(new CommandePlierGroupe(this, groupe, !groupe.Plie));
        }

        /// <summary>Le groupe visé par « Plier / déplier » : le cadre actif, sinon celui de la boîte sélectionnée.</summary>
        public Groupe GroupeCible()
        {
            if (_groupeActif != null) return _groupeActif;
            if (_selection != null) return GroupeDuNoeud(_selection.Modele);
            return null;
        }

        /// <summary>Plier ou déplier le groupe ciblé (menu, raccourci).</summary>
        public void BasculerPliageCible()
        {
            if (_mode == "lecture") return;
            BasculerPliageGroupe(GroupeCible());
        }

        /// <summary>Déplie le groupe qui masque cette boîte, s'il y en a un (recherche, navigation).</summary>
        private void DeplierPour(Noeud noeud)
        {
            Groupe groupe = GroupePlieDe(noeud.Id);
            if (groupe != null)
                Historique.ExecuterEtPousser(new CommandePlierGroupe(this, groupe, false));
        }

        /// <summary>La pastille d'un groupe plié : ancrée en haut à gauche de l'emprise de ses boîtes.</summary>
        private Rect RectanglePastille(Groupe groupe, double largeurTitre)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud == null) continue;
                if (noeud.X < minX) minX = noeud.X;
                if (noeud.Y < minY) minY = noeud.Y;
            }
            if (minX == double.MaxValue) return default(Rect);
            double largeur = System.Math.Max(96, largeurTitre + 30);
            return new Rect(minX - MargeGroupe, minY - MargeGroupe, largeur, HauteurPastille);
        }

        /// <summary>Le texte de la pastille : « ▸ Titre · 5 » (ou « ▸ Groupe · 5 »).</summary>
        private static string TextePastille(Groupe groupe)
        {
            string titre = string.IsNullOrWhiteSpace(groupe.Titre) ? "Groupe" : groupe.Titre.Trim();
            return "▸ " + titre + " · " + groupe.NoeudIds.Count;
        }

        /// <summary>Avancement des cases à cocher des boîtes du groupe : « 3/5 » ou "".</summary>
        private string ProgressionGroupe(Groupe groupe)
        {
            int cochees = 0, total = 0;
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud == null) continue;
                PucesTexte.CompterCases(noeud.Segments, ref cochees, ref total);
                PucesTexte.CompterCases(noeud.SegmentsApres, ref cochees, ref total);
            }
            return total == 0 ? "" : cochees + "/" + total;
        }
    }
}
