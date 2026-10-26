using System.Collections.Generic;
using Avalonia;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Création au clavier (v0.17) : Entrée = une boîte sœur sous la boîte
    /// sélectionnée (reliée au même parent s'il y en a un), Tab = une boîte enfant à sa droite,
    /// reliée par une flèche. La boîte neuve s'édite aussitôt ; Échap sans texte l'annule.</summary>
    public partial class ToileVue
    {
        private const double EcartSoeur = 24;
        private const double EcartEnfant = 60;

        /// <summary>Le parent d'une boîte : la source d'une flèche simple qui arrive dessus.</summary>
        private Noeud ParentDe(Noeud noeud)
        {
            foreach (Lien lien in Document.Liens)
                if (lien.Fleche == "simple" && lien.CibleId == noeud.Id)
                    return Document.TrouverNoeud(lien.SourceId);
            return null;
        }

        /// <summary>Les enfants d'une boîte : les cibles des flèches simples qui en partent.</summary>
        private List<Noeud> EnfantsDe(Noeud noeud)
        {
            List<Noeud> enfants = new List<Noeud>();
            foreach (Lien lien in Document.Liens)
            {
                if (lien.Fleche != "simple" || lien.SourceId != noeud.Id) continue;
                Noeud cible = Document.TrouverNoeud(lien.CibleId);
                if (cible != null) enfants.Add(cible);
            }
            return enfants;
        }

        /// <summary>Entrée : une sœur sous la sélection. Renvoie faux si rien n'est sélectionné.</summary>
        public bool CreerBoiteSoeur()
        {
            if (_selection == null || _mode == "lecture" || _selection.EnEdition) return false;
            Noeud reference = _selection.Modele;
            Rect rect = RectangleNoeud(reference);
            // Sous la dernière sœur déjà rangée en colonne, pour ne rien recouvrir.
            double y = rect.Bottom + EcartSoeur;
            Noeud parent = ParentDe(reference);
            if (parent != null)
                foreach (Noeud soeur in EnfantsDe(parent))
                {
                    Rect r = RectangleNoeud(soeur);
                    if (r.X < rect.Right && r.Right > rect.X && r.Bottom + EcartSoeur > y) y = r.Bottom + EcartSoeur;
                }
            CreerBoiteLiee(parent, new Point(rect.X, y));
            return true;
        }

        /// <summary>Tab : un enfant à droite de la sélection, relié par une flèche.</summary>
        public bool CreerBoiteEnfant()
        {
            if (_selection == null || _mode == "lecture" || _selection.EnEdition) return false;
            Noeud reference = _selection.Modele;
            Rect rect = RectangleNoeud(reference);
            double x = rect.Right + EcartEnfant;
            double y = rect.Y;
            List<Noeud> enfants = EnfantsDe(reference);
            foreach (Noeud enfant in enfants)
            {
                Rect r = RectangleNoeud(enfant);
                if (r.Bottom + EcartSoeur > y) y = r.Bottom + EcartSoeur;
                if (enfants.Count > 0 && r.X < x) x = r.X; // même colonne que les enfants existants
            }
            CreerBoiteLiee(reference, new Point(x, y));
            return true;
        }

        /// <summary>Une boîte libre à cette position du canevas (le bouton
        /// « Nouvelle boîte » d'un hôte) — comme le double-clic.</summary>
        public void CreerBoiteLibre(Point position)
        {
            if (_mode == "lecture") return;
            CreerBoiteLiee(null, position);
        }

        /// <summary>Une boîte libre au centre de la vue.</summary>
        public void CreerBoiteAuCentre()
        {
            Point centre = CentreCanvas();
            CreerBoiteLibre(new Point(centre.X - 55, centre.Y - 16));
        }

        private void CreerBoiteLiee(Noeud parent, Point position)
        {
            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.X = position.X;
            noeud.Y = position.Y;
            List<ICommande> commandes = new List<ICommande>();
            commandes.Add(new CommandeCreerNoeud(this, noeud));
            if (parent != null)
            {
                Lien lien = Document.NouveauLien();
                lien.SourceId = parent.Id;
                lien.CibleId = noeud.Id;
                if (lien.Fleche == "aucune") lien.Fleche = "simple";
                commandes.Add(new CommandeCreerLien(this, lien));
            }
            Historique.ExecuterEtPousser(commandes.Count == 1 ? commandes[0] : new CommandeComposite(commandes));
            _noeudFraichementCree = noeud; // vide à la fin de l'édition → toute la création s'annule
            SelectionnerEtMontrer(noeud);
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
            {
                visuel.CommencerEdition();
                SignalerFormatCible();
            }
        }
    }
}
