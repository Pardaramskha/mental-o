using System.Collections.Generic;
using Avalonia;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Thèmes de carte et import de canevas (v0.17).</summary>
    public partial class ToileVue
    {
        /// <summary>Applique un thème aux défauts du fichier (fond, texte, boîtes, liens, groupes)
        /// et, si demandé, aux éléments existants — ces derniers en une seule annulation.</summary>
        public void AppliquerThemeCarte(ThemeCarte theme, bool auxExistants)
        {
            string ancienneCouleurTexte = Document.CouleurParDefaut;
            Document.FondCouleur = theme.FondCouleur;
            Document.FondMotif = theme.FondMotif;
            Document.CouleurParDefaut = theme.CouleurTexte;
            if (!string.IsNullOrEmpty(theme.Police)) Document.PoliceParDefaut = theme.Police;
            Document.BoiteParDefaut = theme.Boite.Cloner();
            Document.LienCouleurDefaut = theme.LienCouleur;
            Document.LienEpaisseurDefaut = theme.LienEpaisseur;
            Document.LienTraitDefaut = theme.LienTrait;
            Document.LienTraceDefaut = theme.LienTrace;
            Document.GroupeCouleurDefaut = theme.GroupeCouleur;
            Document.GroupeTraitDefaut = theme.GroupeTrait;
            Document.GroupeEpaisseurDefaut = theme.GroupeEpaisseur;
            AppliquerPreferencesDocument(true);
            if (!auxExistants) return;

            List<ICommande> commandes = new List<ICommande>();
            foreach (Noeud n in Document.Noeuds)
            {
                if (n.EstImage && string.IsNullOrWhiteSpace(n.TextePlat)) continue; // les images gardent leur cadre
                if (!theme.Boite.EgalA(n)) commandes.Add(new CommandeModifierStyleNoeud(this, n, theme.Boite.Cloner()));
                // Le texte qui portait l'ancienne couleur par défaut suit la nouvelle.
                if (ancienneCouleurTexte != theme.CouleurTexte && PorteCouleur(n, ancienneCouleurTexte))
                    commandes.Add(new CommandeModifierTexteRiche(this, n,
                        Noeud.CopieDe(n.Segments), Noeud.CopieDe(n.SegmentsApres), n.Alignement,
                        Recolorer(n.Segments, ancienneCouleurTexte, theme.CouleurTexte),
                        Recolorer(n.SegmentsApres, ancienneCouleurTexte, theme.CouleurTexte), n.Alignement));
            }
            foreach (Lien l in Document.Liens)
            {
                if (l.Couleur != theme.LienCouleur || l.Epaisseur != theme.LienEpaisseur || l.Trait != theme.LienTrait)
                    commandes.Add(new CommandeModifierStyleLien(this, l, theme.LienCouleur, theme.LienEpaisseur, theme.LienTrait, l.Fleche));
                if (l.Trace != theme.LienTrace)
                    commandes.Add(new CommandeModifierTraceLien(this, l, theme.LienTrace,
                        theme.LienTrace == "courbe" ? (l.Courbure != 0 ? l.Courbure : 46) : 0));
            }
            foreach (Groupe g in Document.Groupes)
                if (g.Couleur != theme.GroupeCouleur || g.BordureTrait != theme.GroupeTrait || g.BordureEpaisseur != theme.GroupeEpaisseur)
                    commandes.Add(new CommandeModifierStyleGroupe(this, g, theme.GroupeCouleur, theme.GroupeTrait, theme.GroupeEpaisseur));
            if (commandes.Count == 0) return;
            Historique.ExecuterEtPousser(commandes.Count == 1 ? commandes[0] : new CommandeComposite(commandes));
        }

        private static bool PorteCouleur(Noeud noeud, string couleur)
        {
            foreach (Segment s in noeud.Segments) if (string.Equals(s.Couleur, couleur, System.StringComparison.OrdinalIgnoreCase)) return true;
            foreach (Segment s in noeud.SegmentsApres) if (string.Equals(s.Couleur, couleur, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static List<Segment> Recolorer(List<Segment> segments, string de, string vers)
        {
            List<Segment> copie = Noeud.CopieDe(segments);
            foreach (Segment s in copie)
                if (string.Equals(s.Couleur, de, System.StringComparison.OrdinalIgnoreCase)) s.Couleur = vers;
            return copie;
        }

        /// <summary>Import d'un canevas (JSON Canvas) : boîtes, liens et groupes, recentrés sur la vue.</summary>
        public void ImporterCanevas(List<Noeud> noeuds, List<Lien> liens, List<Groupe> groupes)
        {
            if (_mode == "lecture" || noeuds.Count == 0) return;
            // Recentrage : l'ensemble arrive au centre de la vue, positions relatives conservées.
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (Noeud n in noeuds)
            {
                minX = System.Math.Min(minX, n.X); minY = System.Math.Min(minY, n.Y);
                maxX = System.Math.Max(maxX, n.X + (n.LargeurFixe > 0 ? n.LargeurFixe : 160));
                maxY = System.Math.Max(maxY, n.Y + 40);
            }
            Point centre = CentreCanvas();
            double dx = centre.X - (minX + maxX) / 2, dy = centre.Y - (minY + maxY) / 2;
            List<ICommande> commandes = new List<ICommande>();
            foreach (Noeud n in noeuds)
            {
                string fond = n.FondCouleur;
                if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(n);
                if (!string.Equals(fond, "#FFFFFF", System.StringComparison.OrdinalIgnoreCase)) n.FondCouleur = fond;
                n.X += dx; n.Y += dy;
                commandes.Add(new CommandeCreerNoeud(this, n));
            }
            foreach (Lien l in liens)
            {
                Lien gabarit = Document.NouveauLien();
                if (l.Couleur == "#6A665E") l.Couleur = gabarit.Couleur; // couleur d'usine → défaut du fichier
                l.Epaisseur = gabarit.Epaisseur;
                l.Trait = gabarit.Trait;
                l.Trace = gabarit.Trace;
                l.Courbure = gabarit.Courbure;
                commandes.Add(new CommandeCreerLien(this, l));
            }
            foreach (Groupe g in groupes)
            {
                Groupe gabarit = Document.NouveauGroupe();
                if (g.Couleur == "#8A94A8") g.Couleur = gabarit.Couleur;
                g.BordureTrait = gabarit.BordureTrait;
                g.BordureEpaisseur = gabarit.BordureEpaisseur;
                commandes.Add(new CommandeCreerGroupe(this, g, new Dictionary<Groupe, List<string>>()));
            }
            Historique.ExecuterEtPousser(commandes.Count == 1 ? commandes[0] : new CommandeComposite(commandes));
            List<NoeudVisuel> visuels = new List<NoeudVisuel>();
            foreach (Noeud n in noeuds)
            {
                NoeudVisuel visuel;
                if (_visuels.TryGetValue(n, out visuel)) visuels.Add(visuel);
            }
            SelectionnerEnsemble(visuels, visuels.Count > 0 ? visuels[0] : null);
        }
    }
}
