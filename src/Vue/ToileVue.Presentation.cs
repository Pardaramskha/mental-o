using System;
using System.Collections.Generic;
using System.Windows;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Présentation par diapositives (v0.17) : en mode présentation (F5), chaque
    /// groupe est une diapositive — ordre réglé au clic droit, sinon ordre de lecture (haut →
    /// bas, gauche → droite). Sans groupe, chaque boîte est une diapositive. Flèches / Espace /
    /// Page suivante avancent, Début revient à la vue d'ensemble.</summary>
    public partial class ToileVue
    {
        private int _diapositive = -1; // -1 = vue d'ensemble

        /// <summary>(numéro courant à partir de 1, total) — 0 = vue d'ensemble.</summary>
        public event Action<int, int> DiapositiveChangee;

        public void DefinirOrdrePresentation(Groupe groupe, int ordre)
        {
            groupe.OrdrePresentation = Math.Max(0, ordre);
            SignalerModification();
        }

        /// <summary>Clic droit sur un cadre > Ordre dans la présentation…</summary>
        public void DemanderOrdrePresentation(Groupe groupe)
        {
            string saisie = DialogueSaisie.DemanderTexte(Window.GetWindow(this), "Ordre dans la présentation",
                "Rang de ce groupe (1 = première diapositive ; 0 ou vide = ordre de lecture automatique) :",
                groupe.OrdrePresentation > 0 ? groupe.OrdrePresentation.ToString() : "");
            if (saisie == null) return;
            int ordre;
            if (!int.TryParse(saisie.Trim(), out ordre)) ordre = 0;
            if (ordre == groupe.OrdrePresentation) return;
            Historique.ExecuterEtPousser(new CommandeOrdrePresentationGroupe(this, groupe, ordre));
        }

        private List<Rect> Diapositives()
        {
            List<KeyValuePair<Groupe, Rect>> groupes = new List<KeyValuePair<Groupe, Rect>>();
            foreach (Groupe g in Document.Groupes)
            {
                if (g.Plie) continue;
                Rect rect = RectangleDuGroupe(g);
                if (rect != Rect.Empty) groupes.Add(new KeyValuePair<Groupe, Rect>(g, rect));
            }
            List<Rect> diapositives = new List<Rect>();
            if (groupes.Count > 0)
            {
                groupes.Sort(delegate(KeyValuePair<Groupe, Rect> a, KeyValuePair<Groupe, Rect> b)
                {
                    int oa = a.Key.OrdrePresentation > 0 ? a.Key.OrdrePresentation : int.MaxValue;
                    int ob = b.Key.OrdrePresentation > 0 ? b.Key.OrdrePresentation : int.MaxValue;
                    if (oa != ob) return oa.CompareTo(ob);
                    // Ordre de lecture : par rangées de 120 px, puis de gauche à droite.
                    int ra = (int)Math.Floor(a.Value.Y / 120), rb = (int)Math.Floor(b.Value.Y / 120);
                    if (ra != rb) return ra.CompareTo(rb);
                    return a.Value.X.CompareTo(b.Value.X);
                });
                foreach (KeyValuePair<Groupe, Rect> kv in groupes) diapositives.Add(kv.Value);
                return diapositives;
            }
            List<Noeud> noeuds = new List<Noeud>();
            foreach (Noeud n in Document.Noeuds) if (!NoeudMasque(n)) noeuds.Add(n);
            noeuds.Sort(delegate(Noeud a, Noeud b)
            {
                int ra = (int)Math.Floor(a.Y / 120), rb = (int)Math.Floor(b.Y / 120);
                if (ra != rb) return ra.CompareTo(rb);
                return a.X.CompareTo(b.X);
            });
            foreach (Noeud n in noeuds) diapositives.Add(RectangleNoeud(n));
            return diapositives;
        }

        public void PresentationDebut()
        {
            _diapositive = -1;
            ZoomToutVoir();
            SignalerDiapositive(0);
        }

        public void PresentationSuivante()
        {
            List<Rect> diapositives = Diapositives();
            if (diapositives.Count == 0) return;
            if (_diapositive >= diapositives.Count - 1) return; // dernière : on reste
            _diapositive++;
            ZoomSurRectangle(diapositives[_diapositive]);
            SignalerDiapositive(diapositives.Count);
        }

        public void PresentationPrecedente()
        {
            List<Rect> diapositives = Diapositives();
            if (_diapositive <= 0)
            {
                PresentationDebut();
                return;
            }
            _diapositive = Math.Min(_diapositive - 1, diapositives.Count - 1);
            if (_diapositive < 0) { PresentationDebut(); return; }
            ZoomSurRectangle(diapositives[_diapositive]);
            SignalerDiapositive(diapositives.Count);
        }

        private void SignalerDiapositive(int total)
        {
            Action<int, int> h = DiapositiveChangee;
            if (h != null) h(_diapositive + 1, total == 0 ? Diapositives().Count : total);
        }
    }
}
