using System;
using System.Collections.Generic;
using Avalonia;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>La vue et la mise en avant (v0.15) : zoom pour tout voir / sur la sélection,
    /// tout sélectionner, filtre par étiquettes et « focus » sur la sélection — les boîtes
    /// qui ne correspondent pas s'estompent à 15 %, leurs liens aussi.</summary>
    public partial class ToileVue
    {
        private const double MargeZoomAjuste = 48;

        private HashSet<string> _filtreEtiquettes;   // null = pas de filtre ; sinon les noms actifs
        private bool _filtreSansEtiquette;           // le filtre laisse-t-il passer les boîtes sans étiquette ?
        private HashSet<string> _focusIds;           // null = pas de mise en avant

        public bool FocusActif { get { return _focusIds != null; } }
        public bool FiltreActif { get { return _filtreEtiquettes != null; } }

        /// <summary>Le filtre ou la mise en avant a changé (pour les menus cochés).</summary>
        public event Action EstompageChange;

        // ------------------------------------------------------------------ zoom

        /// <summary>Cadre la zone donnée dans la vue, avec une marge, zoom borné.</summary>
        public void ZoomSurRectangle(Rect zone)
        {
            if (EstVide(zone) || LargeurVue <= 0 || HauteurVue <= 0) return;
            double largeur = Math.Max(zone.Width, 40) + MargeZoomAjuste * 2;
            double hauteur = Math.Max(zone.Height, 40) + MargeZoomAjuste * 2;
            double echelle = Math.Min(LargeurVue / largeur, HauteurVue / hauteur);
            echelle = Math.Max(ZoomMin, Math.Min(ZoomMax, echelle));
            _echelle.ScaleX = echelle;
            _echelle.ScaleY = echelle;
            Point centre = Centre(zone);
            _translation.X = LargeurVue / 2 - centre.X * echelle;
            _translation.Y = HauteurVue / 2 - centre.Y * echelle;
            SignalerZoom();
            SignalerVue();
        }

        /// <summary>Toute la carte dans la vue (Ctrl+Maj+0).</summary>
        public void ZoomToutVoir()
        {
            Rect bornes = BornesContenu();
            if (EstVide(bornes)) { ZoomReinitialiser(); Recentrer(); return; }
            ZoomSurRectangle(bornes);
        }

        /// <summary>La sélection (boîtes, groupe ou lien) dans la vue ; sans sélection, tout voir.</summary>
        public void ZoomSurSelection()
        {
            Rect zone = RectangleSelectionCourante();
            if (EstVide(zone)) ZoomToutVoir();
            else ZoomSurRectangle(zone);
        }

        private Rect RectangleSelectionCourante()
        {
            bool premier = true;
            Rect zone = default(Rect);
            foreach (NoeudVisuel v in _selectionnes)
            {
                Rect r = RectangleNoeud(v.Modele);
                zone = premier ? r : zone.Union(r);
                premier = false;
            }
            if (_groupeActif != null)
            {
                Rect cadre = RectangleDuGroupe(_groupeActif);
                if (!EstVide(cadre))
                {
                    zone = premier ? cadre : zone.Union(cadre);
                    premier = false;
                }
            }
            if (_selectionLien != null)
            {
                Point a, b;
                if (PointsLien(_selectionLien.Modele, out a, out b))
                {
                    Rect r = RectEntre(a, b);
                    zone = premier ? r : zone.Union(r);
                    premier = false;
                }
            }
            return premier ? default(Rect) : zone;
        }

        // ------------------------------------------------------------------ sélection

        /// <summary>Toutes les boîtes visibles (Ctrl+A) — celles des groupes pliés restent hors jeu.</summary>
        public void SelectionnerTout()
        {
            if (_mode == "lecture") return;
            List<NoeudVisuel> toutes = new List<NoeudVisuel>();
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
                if (kv.Value.IsVisible) toutes.Add(kv.Value);
            SelectionnerEnsemble(toutes, toutes.Count > 0 ? toutes[0] : null);
        }

        // ------------------------------------------------------------------ filtre et mise en avant

        /// <summary>Applique un filtre : seules les boîtes portant l'une des étiquettes actives
        /// (ou aucune, si « sansEtiquette ») restent nettes. null = plus de filtre.</summary>
        public void DefinirFiltreEtiquettes(HashSet<string> actives, bool sansEtiquette)
        {
            _filtreEtiquettes = actives;
            _filtreSansEtiquette = sansEtiquette;
            AppliquerEstompage();
        }

        public HashSet<string> FiltreEtiquettes { get { return _filtreEtiquettes; } }
        public bool FiltreSansEtiquette { get { return _filtreSansEtiquette; } }

        /// <summary>Met en avant la sélection (boîtes, ou groupe actif) : tout le reste s'estompe.
        /// Un second appel, ou Échap, rétablit.</summary>
        public void BasculerFocusSelection()
        {
            if (_focusIds != null)
            {
                _focusIds = null;
                AppliquerEstompage();
                return;
            }
            HashSet<string> ids = new HashSet<string>();
            foreach (NoeudVisuel v in _selectionnes) ids.Add(v.Modele.Id);
            if (_groupeActif != null)
                foreach (string id in _groupeActif.NoeudIds) ids.Add(id);
            if (_selectionLien != null)
            {
                ids.Add(_selectionLien.Modele.SourceId);
                ids.Add(_selectionLien.Modele.CibleId);
            }
            if (ids.Count == 0) return;
            _focusIds = ids;
            AppliquerEstompage();
        }

        public void AnnulerFocus()
        {
            if (_focusIds == null) return;
            _focusIds = null;
            AppliquerEstompage();
        }

        private bool NoeudMisEnAvant(Noeud noeud)
        {
            if (_focusIds != null && !_focusIds.Contains(noeud.Id)) return false;
            if (_filtreEtiquettes != null)
            {
                if (noeud.Etiquettes.Count == 0) return _filtreSansEtiquette;
                foreach (string nom in noeud.Etiquettes)
                    if (_filtreEtiquettes.Contains(nom)) return true;
                return false;
            }
            return true;
        }

        private bool ExtremiteMiseEnAvant(string id)
        {
            Noeud noeud = Document.TrouverNoeud(id);
            if (noeud != null) return NoeudMisEnAvant(noeud);
            // Un cadre de groupe : net si l'un de ses membres l'est.
            foreach (Groupe groupe in Document.Groupes)
            {
                if (groupe.Id != id) continue;
                foreach (string membre in groupe.NoeudIds)
                {
                    Noeud n = Document.TrouverNoeud(membre);
                    if (n != null && NoeudMisEnAvant(n)) return true;
                }
                return false;
            }
            return true;
        }

        private void AppliquerEstompage()
        {
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
                kv.Value.DefinirEstompe(!NoeudMisEnAvant(kv.Key));
            foreach (KeyValuePair<Lien, LienVisuel> kv in _visuelsLiens)
                AppliquerEstompageLien(kv.Key, kv.Value);
            Action h = EstompageChange;
            if (h != null) h();
        }

        private void AppliquerEstompageLien(Lien lien, LienVisuel visuel)
        {
            bool net = ExtremiteMiseEnAvant(lien.SourceId) && ExtremiteMiseEnAvant(lien.CibleId);
            visuel.Opacity = net ? 1.0 : 0.15;
        }

        /// <summary>Remet l'estompage d'une boîte (et de ses liens) après un changement local.</summary>
        private void RafraichirEstompageDe(Noeud noeud)
        {
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.DefinirEstompe(!NoeudMisEnAvant(noeud));
            foreach (KeyValuePair<Lien, LienVisuel> kv in _visuelsLiens)
                if (kv.Key.Concerne(noeud.Id)) AppliquerEstompageLien(kv.Key, kv.Value);
        }

        private void ReinitialiserEstompage()
        {
            _filtreEtiquettes = null;
            _filtreSansEtiquette = false;
            _focusIds = null;
            Action h = EstompageChange;
            if (h != null) h();
        }
    }
}
