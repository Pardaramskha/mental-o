using System;
using System.Collections.Generic;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le texte riche pendant l'édition (26/10/2026). Avalonia n'a pas de
    /// RichTextBox : la boîte s'édite dans un TextBox ordinaire (texte plat), et cette
    /// classe garde la liste des segments stylés alignée sur ce texte. À chaque frappe,
    /// l'ancien et le nouveau texte sont comparés (préfixe et suffixe communs) : la
    /// partie remplacée hérite du style du segment où elle s'insère. Les commandes de
    /// mise en forme s'appliquent à une plage [début, fin[ en coupant les segments aux
    /// bornes. L'alignement d'une ligne vit dans les segments de cette ligne.</summary>
    public class EditeurSegments
    {
        private List<Segment> _segments;

        public EditeurSegments(List<Segment> segments)
        {
            _segments = Mentalo.Vue.Segments.Normaliser(segments);
        }

        public List<Segment> Segments { get { return _segments; } }

        public string Texte { get { return Mentalo.Vue.Segments.TextePlat(_segments); } }

        /// <summary>Le texte a changé dans le TextBox : réconcilie les segments.
        /// « caret » = position du curseur après la frappe (sert à placer l'insertion
        /// quand l'ancien et le nouveau texte ont plusieurs réconciliations possibles).</summary>
        public void Synchroniser(string nouveau, int caret)
        {
            nouveau = nouveau ?? "";
            string ancien = Texte;
            if (ancien == nouveau) return;
            int prefixe = 0;
            int maxPrefixe = Math.Min(ancien.Length, nouveau.Length);
            // Le préfixe commun ne dépasse pas le curseur : ce qui est tapé se trouve juste avant lui.
            int limitePrefixe = Math.Min(maxPrefixe, Math.Max(0, caret - Math.Max(0, nouveau.Length - ancien.Length)));
            if (nouveau.Length < ancien.Length) limitePrefixe = Math.Min(maxPrefixe, Math.Max(0, caret));
            while (prefixe < limitePrefixe && ancien[prefixe] == nouveau[prefixe]) prefixe++;
            // Si le curseur n'a pas été fourni (ou est incohérent), on prend le plus long préfixe.
            if (caret < 0) while (prefixe < maxPrefixe && ancien[prefixe] == nouveau[prefixe]) prefixe++;
            int suffixe = 0;
            while (suffixe < ancien.Length - prefixe && suffixe < nouveau.Length - prefixe
                && ancien[ancien.Length - 1 - suffixe] == nouveau[nouveau.Length - 1 - suffixe])
                suffixe++;
            int retire = ancien.Length - prefixe - suffixe;
            string insere = nouveau.Substring(prefixe, nouveau.Length - prefixe - suffixe);
            Remplacer(prefixe, retire, insere);
        }

        /// <summary>Remplace « longueur » caractères à partir de « debut » par « insere »,
        /// qui prend le style du segment où il s'insère (celui qui précède, sinon celui qui suit).</summary>
        public void Remplacer(int debut, int longueur, string insere)
        {
            // Le style de l'insertion : le caractère juste avant le point d'insertion.
            Segment gabarit = SegmentA(debut > 0 ? debut - 1 : 0) ?? SegmentA(debut) ?? NouveauSegment();
            if (longueur > 0) Supprimer(debut, longueur);
            if (!string.IsNullOrEmpty(insere))
            {
                Segment neuf = gabarit.Cloner();
                neuf.Texte = insere;
                // L'alignement de ligne suit la ligne où l'on insère.
                Inserer(debut, neuf);
            }
            _segments = Mentalo.Vue.Segments.Normaliser(_segments);
            if (_segments.Count == 0 && string.IsNullOrEmpty(insere)) { }
        }

        private Segment NouveauSegment()
        {
            Segment s = new Segment();
            s.Texte = "";
            return s;
        }

        /// <summary>Le segment qui contient le caractère à cette position, ou null.</summary>
        private Segment SegmentA(int position)
        {
            int curseur = 0;
            foreach (Segment s in _segments)
            {
                if (position < curseur + s.Texte.Length) return s;
                curseur += s.Texte.Length;
            }
            return _segments.Count > 0 ? _segments[_segments.Count - 1] : null;
        }

        private void Supprimer(int debut, int longueur)
        {
            Couper(debut);
            Couper(debut + longueur);
            int curseur = 0;
            List<Segment> restants = new List<Segment>();
            foreach (Segment s in _segments)
            {
                int fin = curseur + s.Texte.Length;
                if (!(curseur >= debut && fin <= debut + longueur)) restants.Add(s);
                curseur = fin;
            }
            _segments = restants;
        }

        private void Inserer(int position, Segment neuf)
        {
            Couper(position);
            int curseur = 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (curseur == position)
                {
                    _segments.Insert(i, neuf);
                    return;
                }
                curseur += _segments[i].Texte.Length;
            }
            _segments.Add(neuf);
        }

        /// <summary>Coupe le segment qui chevauche cette position en deux, pour que la
        /// position tombe sur une frontière de segments.</summary>
        private void Couper(int position)
        {
            int curseur = 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                Segment s = _segments[i];
                int fin = curseur + s.Texte.Length;
                if (position > curseur && position < fin)
                {
                    Segment droite = s.Cloner();
                    droite.Texte = s.Texte.Substring(position - curseur);
                    s.Texte = s.Texte.Substring(0, position - curseur);
                    _segments.Insert(i + 1, droite);
                    return;
                }
                curseur = fin;
            }
        }

        /// <summary>Applique une mise en forme à la plage [debut, fin[ ; une plage vide
        /// applique à toute la ligne du curseur pour l'alignement, à rien sinon.
        /// Propriétés : police, taille, couleur, surlignage, gras, italique, souligne,
        /// barre (valeur null = inverser), alignement (lignes couvertes).</summary>
        public void AppliquerFormat(string propriete, object valeur, int debut, int fin)
        {
            if (fin < debut) { int t = debut; debut = fin; fin = t; }
            if (propriete == "alignement")
            {
                AppliquerAlignement((string)valeur, debut, fin);
                return;
            }
            if (fin <= debut) return;
            Couper(debut);
            Couper(fin);
            List<Segment> cibles = new List<Segment>();
            int curseur = 0;
            foreach (Segment s in _segments)
            {
                int finSegment = curseur + s.Texte.Length;
                if (curseur >= debut && finSegment <= fin && s.Texte.Length > 0) cibles.Add(s);
                curseur = finSegment;
            }
            if (cibles.Count == 0) return;
            bool bascule = valeur == null;
            bool tousActifs = true;
            foreach (Segment s in cibles)
            {
                bool actif = propriete == "gras" ? s.Gras : propriete == "italique" ? s.Italique
                    : propriete == "souligne" ? s.Souligne : s.Barre;
                if (!actif) { tousActifs = false; break; }
            }
            foreach (Segment s in cibles)
            {
                switch (propriete)
                {
                    case "police": s.Police = (string)valeur; break;
                    case "taille": s.Taille = (double)valeur; break;
                    case "couleur": s.Couleur = (string)valeur; break;
                    case "surlignage": s.Surlignage = (string)valeur; break;
                    case "gras": s.Gras = bascule ? !tousActifs : (bool)valeur; break;
                    case "italique": s.Italique = bascule ? !tousActifs : (bool)valeur; break;
                    case "souligne": s.Souligne = bascule ? !tousActifs : (bool)valeur; break;
                    case "barre": s.Barre = bascule ? !tousActifs : (bool)valeur; break;
                }
            }
            _segments = Mentalo.Vue.Segments.Normaliser(_segments);
        }

        /// <summary>Les bornes [début, fin[ de la ligne qui contient la position.</summary>
        public void BornesLigne(int position, out int debut, out int fin)
        {
            string texte = Texte;
            position = Math.Max(0, Math.Min(texte.Length, position));
            debut = position;
            while (debut > 0 && texte[debut - 1] != '\n') debut--;
            fin = position;
            while (fin < texte.Length && texte[fin] != '\n') fin++;
        }

        private void AppliquerAlignement(string alignement, int debut, int fin)
        {
            int debutLignes, finLignes, ignore;
            BornesLigne(debut, out debutLignes, out ignore);
            BornesLigne(Math.Max(debut, fin > debut ? fin - 1 : fin), out ignore, out finLignes);
            Couper(debutLignes);
            Couper(finLignes);
            int curseur = 0;
            foreach (Segment s in _segments)
            {
                int finSegment = curseur + s.Texte.Length;
                // Les segments qui touchent la plage de lignes (y compris le « \n » final).
                if (finSegment > debutLignes && curseur <= finLignes)
                    AppliquerAlignementSegment(s, curseur, debutLignes, finLignes, alignement);
                curseur = finSegment;
            }
            _segments = Mentalo.Vue.Segments.Normaliser(_segments);
        }

        /// <summary>Un segment peut chevaucher plusieurs lignes : on ne change que la
        /// part qui tombe dans les lignes visées, en le coupant aux « \n ».</summary>
        private void AppliquerAlignementSegment(Segment s, int origine, int debutLignes, int finLignes, string alignement)
        {
            // Le segment est déjà coupé aux bornes ; s'il contient des « \n » en dehors,
            // il a été coupé par BornesLigne/Couper sur ces bornes. Reste le cas d'un
            // segment multi-lignes entièrement dans la plage : toutes ses lignes prennent l'alignement.
            s.AlignementLigne = alignement;
        }

        /// <summary>L'état de mise en forme à une position (pour la barre).</summary>
        public FormatCourant Lire(int position, int debutSelection, int finSelection, string alignementBoite)
        {
            FormatCourant etat = new FormatCourant();
            etat.Actif = true;
            Segment s = SegmentA(Math.Max(0, position - 1)) ?? SegmentA(position);
            if (s == null)
            {
                etat.Police = Segment.PoliceDefaut;
                etat.Taille = Segment.TailleDefaut;
                etat.Couleur = Segment.CouleurDefaut;
                etat.Alignement = alignementBoite;
                return etat;
            }
            etat.Police = s.Police;
            etat.Taille = s.Taille;
            etat.Couleur = s.Couleur;
            etat.Surlignage = s.Surlignage;
            etat.Gras = s.Gras;
            etat.Italique = s.Italique;
            etat.Souligne = s.Souligne;
            etat.Barre = s.Barre;
            // L'alignement de la ligne du curseur.
            int debut, fin;
            BornesLigne(position, out debut, out fin);
            etat.Alignement = alignementBoite;
            int curseur = 0;
            foreach (Segment seg in _segments)
            {
                int finSegment = curseur + seg.Texte.Length;
                if (finSegment > debut && curseur < Math.Max(fin, debut + 1) && !string.IsNullOrEmpty(seg.AlignementLigne))
                {
                    etat.Alignement = seg.AlignementLigne;
                    break;
                }
                curseur = finSegment;
            }
            return etat;
        }

        /// <summary>Bascule les puces sur les lignes couvertes par [debut, fin].</summary>
        public void BasculerPuces(string type, int debut, int fin)
        {
            int debutLignes, finLignes, ignore;
            BornesLigne(debut, out debutLignes, out ignore);
            BornesLigne(Math.Max(debut, fin > debut ? fin - 1 : fin), out ignore, out finLignes);
            string texte = Texte;
            // Index des lignes couvertes.
            int premiereLigne = 0;
            for (int i = 0; i < debutLignes; i++) if (texte[i] == '\n') premiereLigne++;
            int derniereLigne = premiereLigne;
            for (int i = debutLignes; i < finLignes; i++) if (texte[i] == '\n') derniereLigne++;

            List<List<Segment>> lignes = PucesTexte.DecouperLignes(_segments);
            List<Segment> cibles = new List<Segment>();
            for (int i = premiereLigne; i <= derniereLigne && i < lignes.Count; i++)
                cibles.AddRange(lignes[i]);
            // On reconstruit : les lignes visées passent par PucesTexte.Basculer.
            List<Segment> visees = new List<Segment>();
            for (int i = premiereLigne; i <= derniereLigne && i < lignes.Count; i++)
            {
                if (i > premiereLigne) AjouterRetour(visees);
                foreach (Segment s in lignes[i]) visees.Add(s.Cloner());
            }
            List<Segment> basculees = PucesTexte.Basculer(visees, type);
            List<Segment> resultat = new List<Segment>();
            for (int i = 0; i < lignes.Count; i++)
            {
                if (i > 0) AjouterRetour(resultat);
                if (i == premiereLigne)
                {
                    foreach (Segment s in basculees) resultat.Add(s.Cloner());
                    i = Math.Min(derniereLigne, lignes.Count - 1);
                    continue;
                }
                foreach (Segment s in lignes[i]) resultat.Add(s.Cloner());
            }
            _segments = Mentalo.Vue.Segments.Normaliser(resultat);
        }

        private static void AjouterRetour(List<Segment> segments)
        {
            if (segments.Count > 0) segments[segments.Count - 1].Texte += "\n";
            else
            {
                Segment s = new Segment();
                s.Texte = "\n";
                segments.Add(s);
            }
        }
    }
}
