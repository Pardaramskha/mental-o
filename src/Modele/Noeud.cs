using System;
using System.Collections.Generic;
using System.Text;

namespace Mentalo.Modele
{
    /// <summary>Une boîte de la carte : position, texte riche et alignement. Données pures, sérialisables.</summary>
    public class Noeud
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<Segment> Segments { get; set; }        // texte avant l'image
        public List<Segment> SegmentsApres { get; set; }   // texte après l'image (vide sans image)
        public string Alignement { get; set; }  // "gauche" | "centre" | "droite"

        /// <summary>Largeur imposée par l'utilisateur (poignée de redimensionnement) ; 0 = automatique.</summary>
        public double LargeurFixe { get; set; }

        // Image (boîte-image : les octets vivent dans le zip .zeubi, pas dans le JSON)
        public byte[] ImageDonnees { get; set; }
        public string ImageExtension { get; set; }
        public double ImageLargeur { get; set; }
        public double ImageHauteur { get; set; }

        public bool EstImage { get { return ImageDonnees != null; } }

        /// <summary>Les fichiers attachés (v0.14), affichés en liens sous le texte.</summary>
        public List<PieceJointe> Fichiers { get; set; }

        /// <summary>Note cachée (v0.15) : détail hors carte, montré en infobulle ("" = aucune).</summary>
        public string Note { get; set; }

        /// <summary>Noms des étiquettes portées (v0.15) ; les couleurs vivent dans DocumentCarte.Etiquettes.</summary>
        public List<string> Etiquettes { get; set; }

        public bool ANote { get { return !string.IsNullOrWhiteSpace(Note); } }

        /// <summary>Une boîte sans texte, sans image et sans fichier n'a pas de raison d'exister.</summary>
        public bool EstVide
        {
            get { return string.IsNullOrWhiteSpace(TextePlat) && !EstImage && Fichiers.Count == 0; }
        }

        // Style de la boîte
        public string FondCouleur { get; set; }
        public string BordureCouleur { get; set; }
        public double BordureEpaisseur { get; set; }
        public string BordureTrait { get; set; } // "plein" | "tirets" | "points" | "aucune"
        public double Rayon { get; set; }        // coins arrondis
        public string Ombre { get; set; }        // "aucune" | "legere" | "marquee"
        public double Opacite { get; set; }      // 0,3 à 1 (1 = opaque)

        public Noeud()
        {
            Id = Guid.NewGuid().ToString("N");
            Segments = new List<Segment>();
            SegmentsApres = new List<Segment>();
            Fichiers = new List<PieceJointe>();
            Note = "";
            Etiquettes = new List<string>();
            Alignement = "gauche";
            FondCouleur = "#FFFFFF";
            BordureCouleur = "#B8B2A7";
            BordureEpaisseur = 1.5;
            BordureTrait = "plein";
            Rayon = 5;
            Ombre = "legere";
            Opacite = 1;
        }

        /// <summary>Le texte sans mise en forme (recherche, test de vacuité, compatibilité).</summary>
        public string TextePlat
        {
            get
            {
                StringBuilder texte = new StringBuilder();
                foreach (Segment s in Segments)
                    texte.Append(s.Texte);
                foreach (Segment s in SegmentsApres)
                    texte.Append(s.Texte);
                return texte.ToString();
            }
        }

        public void DefinirTexteSimple(string texte)
        {
            Segments = new List<Segment>();
            if (!string.IsNullOrEmpty(texte))
            {
                Segment s = new Segment();
                s.Texte = texte;
                Segments.Add(s);
            }
        }

        public static List<Segment> CopieDe(List<Segment> segments)
        {
            List<Segment> copie = new List<Segment>();
            foreach (Segment s in segments)
                copie.Add(s.Cloner());
            return copie;
        }

        public static bool SegmentsEgaux(List<Segment> a, List<Segment> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Texte != b[i].Texte || !a[i].MemeStyleQue(b[i])) return false;
            return true;
        }
    }
}
