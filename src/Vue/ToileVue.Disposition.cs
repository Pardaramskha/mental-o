using System;
using System.Collections.Generic;
using System.Windows;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>« Ranger en arbre » (v0.17) : dispose la sélection (ou toute la carte) en
    /// arbre suivant les liens — horizontal (racine à gauche), vertical (racine en haut) ou
    /// radial (racine au centre). La racine garde sa place ; tout se déplace en une seule
    /// annulation. Le placement libre reprend ensuite ses droits.</summary>
    public partial class ToileVue
    {
        private const double EcartArbreH = 70;
        private const double EcartArbreV = 22;
        private const double RayonRadial = 190;

        private class Arbre
        {
            public Noeud Noeud;
            public List<Arbre> Enfants = new List<Arbre>();
            public double Etendue;   // hauteur (horizontal) ou largeur (vertical) du sous-arbre
            public int Feuilles;
        }

        public void RangerEnArbre(string disposition)
        {
            if (_mode == "lecture") return;
            List<Noeud> ensemble = ModelesSelectionnes();
            if (_groupeActif != null) ensemble = NoeudsDuGroupe(_groupeActif);
            if (ensemble.Count < 2)
            {
                ensemble = new List<Noeud>();
                foreach (Noeud n in Document.Noeuds)
                    if (!NoeudMasque(n)) ensemble.Add(n);
            }
            if (ensemble.Count < 2) return;

            List<Arbre> foret = ConstruireForetLocale(ensemble);
            Dictionary<Noeud, Point> positions = new Dictionary<Noeud, Point>();
            if (disposition == "radial") DisposerRadial(foret, positions);
            else if (disposition == "vertical") DisposerVertical(foret, positions);
            else DisposerHorizontal(foret, positions);

            List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
            foreach (KeyValuePair<Noeud, Point> kv in positions)
            {
                if (Math.Abs(kv.Key.X - kv.Value.X) < 0.5 && Math.Abs(kv.Key.Y - kv.Value.Y) < 0.5) continue;
                CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                d.Noeud = kv.Key;
                d.AncienX = kv.Key.X; d.AncienY = kv.Key.Y;
                d.NouveauX = kv.Value.X; d.NouveauY = kv.Value.Y;
                deplacements.Add(d);
            }
            if (deplacements.Count == 0) return;
            Historique.ExecuterEtPousser(new CommandeDeplacerNoeuds(this, deplacements));
        }

        /// <summary>La forêt de l'ensemble : racines = boîtes sans flèche entrante venant de
        /// l'ensemble ; descente par les flèches, puis par les liens non orientés ; les boîtes
        /// restées à l'écart (cycles) deviennent des racines.</summary>
        private List<Arbre> ConstruireForetLocale(List<Noeud> ensemble)
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (Noeud n in ensemble) ids.Add(n.Id);
            Dictionary<string, Noeud> parId = new Dictionary<string, Noeud>();
            foreach (Noeud n in ensemble) parId[n.Id] = n;
            HashSet<string> aParent = new HashSet<string>();
            foreach (Lien l in Document.Liens)
                if (l.Fleche == "simple" && ids.Contains(l.SourceId) && ids.Contains(l.CibleId) && l.SourceId != l.CibleId)
                    aParent.Add(l.CibleId);

            List<Noeud> ordonnes = new List<Noeud>(ensemble);
            ordonnes.Sort(delegate(Noeud a, Noeud b)
            {
                int c = a.Y.CompareTo(b.Y);
                return c != 0 ? c : a.X.CompareTo(b.X);
            });

            List<Arbre> foret = new List<Arbre>();
            HashSet<string> visites = new HashSet<string>();
            foreach (Noeud racine in ordonnes)
                if (!aParent.Contains(racine.Id) && !visites.Contains(racine.Id))
                    foret.Add(ConstruireArbre(racine, ids, parId, visites));
            foreach (Noeud reste in ordonnes)
                if (!visites.Contains(reste.Id))
                    foret.Add(ConstruireArbre(reste, ids, parId, visites));
            return foret;
        }

        private Arbre ConstruireArbre(Noeud noeud, HashSet<string> ids, Dictionary<string, Noeud> parId, HashSet<string> visites)
        {
            visites.Add(noeud.Id);
            Arbre arbre = new Arbre { Noeud = noeud };
            List<Noeud> enfants = new List<Noeud>();
            // D'abord les flèches qui partent d'ici, puis les liens sans sens.
            foreach (Lien l in Document.Liens)
            {
                if (l.Fleche == "simple" && l.SourceId == noeud.Id && ids.Contains(l.CibleId) && !visites.Contains(l.CibleId))
                    enfants.Add(parId[l.CibleId]);
            }
            foreach (Lien l in Document.Liens)
            {
                if (l.Fleche == "simple") continue;
                string autre = l.SourceId == noeud.Id ? l.CibleId : l.CibleId == noeud.Id ? l.SourceId : null;
                if (autre == null || autre == noeud.Id || !ids.Contains(autre) || visites.Contains(autre)) continue;
                if (!enfants.Contains(parId[autre])) enfants.Add(parId[autre]);
            }
            enfants.Sort(delegate(Noeud a, Noeud b)
            {
                int c = a.Y.CompareTo(b.Y);
                return c != 0 ? c : a.X.CompareTo(b.X);
            });
            foreach (Noeud enfant in enfants)
            {
                if (visites.Contains(enfant.Id)) continue; // pris par un frère entre-temps
                arbre.Enfants.Add(ConstruireArbre(enfant, ids, parId, visites));
            }
            return arbre;
        }

        // ------------------------------------------------------------------ horizontal

        private double MesurerEtenduesH(Arbre arbre)
        {
            double hauteur = RectangleNoeud(arbre.Noeud).Height;
            double enfants = 0;
            foreach (Arbre e in arbre.Enfants) enfants += MesurerEtenduesH(e);
            if (arbre.Enfants.Count > 1) enfants += EcartArbreV * (arbre.Enfants.Count - 1);
            arbre.Etendue = Math.Max(hauteur, enfants);
            return arbre.Etendue;
        }

        private void PlacerH(Arbre arbre, double x, double yHaut, Dictionary<Noeud, Point> positions)
        {
            Rect rect = RectangleNoeud(arbre.Noeud);
            positions[arbre.Noeud] = new Point(x, yHaut + (arbre.Etendue - rect.Height) / 2);
            double xEnfants = x + rect.Width + EcartArbreH;
            double y = yHaut;
            foreach (Arbre e in arbre.Enfants)
            {
                PlacerH(e, xEnfants, y, positions);
                y += e.Etendue + EcartArbreV;
            }
        }

        private void DisposerHorizontal(List<Arbre> foret, Dictionary<Noeud, Point> positions)
        {
            double x = foret[0].Noeud.X;
            double y = double.NaN;
            foreach (Arbre arbre in foret)
            {
                MesurerEtenduesH(arbre);
                if (double.IsNaN(y))
                    y = arbre.Noeud.Y - (arbre.Etendue - RectangleNoeud(arbre.Noeud).Height) / 2; // la racine ne bouge pas
                PlacerH(arbre, x, y, positions);
                y += arbre.Etendue + EcartArbreV * 2;
            }
        }

        // ------------------------------------------------------------------ vertical

        private double MesurerEtenduesV(Arbre arbre)
        {
            double largeur = RectangleNoeud(arbre.Noeud).Width;
            double enfants = 0;
            foreach (Arbre e in arbre.Enfants) enfants += MesurerEtenduesV(e);
            if (arbre.Enfants.Count > 1) enfants += EcartArbreV * 1.5 * (arbre.Enfants.Count - 1);
            arbre.Etendue = Math.Max(largeur, enfants);
            return arbre.Etendue;
        }

        private void PlacerV(Arbre arbre, double xGauche, double y, Dictionary<Noeud, Point> positions)
        {
            Rect rect = RectangleNoeud(arbre.Noeud);
            positions[arbre.Noeud] = new Point(xGauche + (arbre.Etendue - rect.Width) / 2, y);
            double yEnfants = y + rect.Height + EcartArbreH * 0.8;
            double x = xGauche;
            foreach (Arbre e in arbre.Enfants)
            {
                PlacerV(e, x, yEnfants, positions);
                x += e.Etendue + EcartArbreV * 1.5;
            }
        }

        private void DisposerVertical(List<Arbre> foret, Dictionary<Noeud, Point> positions)
        {
            double y = foret[0].Noeud.Y;
            double x = double.NaN;
            foreach (Arbre arbre in foret)
            {
                MesurerEtenduesV(arbre);
                if (double.IsNaN(x))
                    x = arbre.Noeud.X - (arbre.Etendue - RectangleNoeud(arbre.Noeud).Width) / 2;
                PlacerV(arbre, x, y, positions);
                x += arbre.Etendue + EcartArbreV * 3;
            }
        }

        // ------------------------------------------------------------------ radial

        private int CompterFeuilles(Arbre arbre)
        {
            if (arbre.Enfants.Count == 0) { arbre.Feuilles = 1; return 1; }
            int total = 0;
            foreach (Arbre e in arbre.Enfants) total += CompterFeuilles(e);
            arbre.Feuilles = total;
            return total;
        }

        private void PlacerRadial(Arbre arbre, Point centreRacine, int profondeur, double angleDebut, double angleFin,
            Dictionary<Noeud, Point> positions)
        {
            double angle = (angleDebut + angleFin) / 2;
            Rect rect = RectangleNoeud(arbre.Noeud);
            if (profondeur > 0)
            {
                double rayon = RayonRadial * profondeur * (1 + 0.15 * Math.Max(0, arbre.Feuilles - 3) / 10.0);
                Point centre = new Point(centreRacine.X + rayon * Math.Cos(angle), centreRacine.Y + rayon * Math.Sin(angle));
                positions[arbre.Noeud] = new Point(centre.X - rect.Width / 2, centre.Y - rect.Height / 2);
            }
            if (arbre.Enfants.Count == 0) return;
            double portee = angleFin - angleDebut;
            double curseur = angleDebut;
            foreach (Arbre e in arbre.Enfants)
            {
                double part = portee * e.Feuilles / Math.Max(1, arbre.Feuilles);
                PlacerRadial(e, centreRacine, profondeur + 1, curseur, curseur + part, positions);
                curseur += part;
            }
        }

        private void DisposerRadial(List<Arbre> foret, Dictionary<Noeud, Point> positions)
        {
            Point centre = Centre(RectangleNoeud(foret[0].Noeud));
            double angleDebut = -Math.PI / 2;
            int total = 0;
            foreach (Arbre a in foret) total += CompterFeuilles(a);
            // Plusieurs racines : chacune reçoit un secteur du cercle complet, sa propre boîte
            // au centre de son secteur (la première reste en place).
            if (foret.Count == 1)
            {
                positions[foret[0].Noeud] = new Point(foret[0].Noeud.X, foret[0].Noeud.Y);
                PlacerRadial(foret[0], centre, 0, angleDebut, angleDebut + Math.PI * 2, positions);
                return;
            }
            double curseur = angleDebut;
            for (int i = 0; i < foret.Count; i++)
            {
                double part = Math.PI * 2 * foret[i].Feuilles / Math.Max(1, total);
                if (i == 0) positions[foret[0].Noeud] = new Point(foret[0].Noeud.X, foret[0].Noeud.Y);
                else
                {
                    Rect r = RectangleNoeud(foret[i].Noeud);
                    double angle = curseur + part / 2;
                    positions[foret[i].Noeud] = new Point(
                        centre.X + RayonRadial * 1.2 * Math.Cos(angle) - r.Width / 2,
                        centre.Y + RayonRadial * 1.2 * Math.Sin(angle) - r.Height / 2);
                }
                PlacerRadial(foret[i], centre, i == 0 ? 0 : 1, curseur, curseur + part, positions);
                curseur += part;
            }
        }
    }
}
