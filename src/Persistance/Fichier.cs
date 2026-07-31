using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web.Script.Serialization;
using Mentalo.Modele;

namespace Mentalo.Persistance
{
    public class DocumentDto
    {
        public int version { get; set; }
        public List<NoeudDto> noeuds { get; set; }
        public List<LienDto> liens { get; set; }
        public List<GroupeDto> groupes { get; set; }
        public List<string> couleursPerso { get; set; }
        // préférences du fichier (v10+)
        public double? paddingBoites { get; set; }
        public string policeDefaut { get; set; }
        public double? tailleDefaut { get; set; }
        public string couleurDefaut { get; set; }
        // v11+
        public string fondCouleur { get; set; }
        public string fondMotif { get; set; }
        public string nom { get; set; }
        public string auteur { get; set; }
        public bool sauvegardeAuto { get; set; }
        public string dateCreation { get; set; }
        public string dateModification { get; set; }
    }

    public class GroupeDto
    {
        public string id { get; set; }
        public List<string> noeuds { get; set; }
        public string couleur { get; set; }
        public double? largeurFixe { get; set; }
        public double? hauteurFixe { get; set; }
        public string titre { get; set; }
        public string titrePosition { get; set; }
        // v12+
        public string bordTrait { get; set; }
        public double? bordEpaisseur { get; set; }
        public string titreCouleur { get; set; }
        public string titreFond { get; set; }
        public double? titreTaille { get; set; }
    }

    public class NoeudDto
    {
        public string id { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public string texte { get; set; }          // texte plat (compatibilité v1/v2)
        public string alignement { get; set; }
        public List<SegmentDto> riche { get; set; }      // texte riche (v3+)
        public List<SegmentDto> richeApres { get; set; } // texte après l'image (v8+)
        // style de la boîte (v5+) — nullable : absent dans les anciens fichiers
        public string fond { get; set; }
        public string bordCouleur { get; set; }
        public double? bordEpaisseur { get; set; }
        public string bordTrait { get; set; }
        public double? rayon { get; set; }
        // image (v6+) — les octets sont dans le zip, sous le nom imageFichier
        public string imageFichier { get; set; }
        public double? imageLargeur { get; set; }
        public double? imageHauteur { get; set; }
        // largeur imposée par l'utilisateur (v7+)
        public double? largeurFixe { get; set; }
    }

    public class SegmentDto
    {
        public string t { get; set; }
        public string police { get; set; }
        public double taille { get; set; }
        public string couleur { get; set; }
        public bool gras { get; set; }
        public bool italique { get; set; }
        public bool souligne { get; set; }
        public bool barre { get; set; }
        public string surlignage { get; set; }
        public string alignLigne { get; set; } // v12+ : alignement de la ligne
    }

    public class LienDto
    {
        public string id { get; set; }
        public string sourceId { get; set; }
        public string cibleId { get; set; }
        public string couleur { get; set; }
        public double epaisseur { get; set; }
        public string trait { get; set; }
        public string fleche { get; set; }
    }

    /// <summary>Lecture/écriture des fichiers .zeubi : une archive ZIP contenant document.json
    /// (et, à partir de la v0.5, un dossier images/).</summary>
    public static class Fichier
    {
        public const string Extension = ".zeubi";
        public const string Filtre = "Carte Mental-o (*.zeubi)|*.zeubi|Tous les fichiers (*.*)|*.*";
        private const int VersionFormat = 12; // … v10 titres ; v11 fond + identité ; v12 styles groupes + alignements de ligne + dates

        public static void Enregistrer(DocumentCarte document, string chemin)
        {
            string maintenant = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (string.IsNullOrEmpty(document.DateCreation)) document.DateCreation = maintenant;
            document.DateModification = maintenant;

            DocumentDto dto = new DocumentDto();
            dto.version = VersionFormat;
            dto.noeuds = new List<NoeudDto>();
            dto.liens = new List<LienDto>();
            dto.couleursPerso = new List<string>(document.CouleursPersonnalisees);
            dto.paddingBoites = document.PaddingBoites;
            dto.policeDefaut = document.PoliceParDefaut;
            dto.tailleDefaut = document.TailleParDefaut;
            dto.couleurDefaut = document.CouleurParDefaut;
            dto.fondCouleur = document.FondCouleur;
            dto.fondMotif = document.FondMotif;
            dto.nom = document.NomAffichage;
            dto.auteur = document.Auteur;
            dto.sauvegardeAuto = document.SauvegardeAuto;
            dto.dateCreation = document.DateCreation;
            dto.dateModification = document.DateModification;
            foreach (Noeud n in document.Noeuds)
            {
                NoeudDto nd = new NoeudDto();
                nd.id = n.Id;
                nd.x = n.X;
                nd.y = n.Y;
                nd.texte = n.TextePlat;
                nd.alignement = n.Alignement;
                nd.fond = n.FondCouleur;
                nd.bordCouleur = n.BordureCouleur;
                nd.bordEpaisseur = n.BordureEpaisseur;
                nd.bordTrait = n.BordureTrait;
                nd.rayon = n.Rayon;
                if (n.EstImage)
                {
                    nd.imageFichier = "images/" + n.Id + (n.ImageExtension ?? ".png");
                    nd.imageLargeur = n.ImageLargeur;
                    nd.imageHauteur = n.ImageHauteur;
                }
                if (n.LargeurFixe > 0) nd.largeurFixe = n.LargeurFixe;
                nd.riche = VersDtos(n.Segments);
                if (n.SegmentsApres.Count > 0) nd.richeApres = VersDtos(n.SegmentsApres);
                dto.noeuds.Add(nd);
            }
            dto.groupes = new List<GroupeDto>();
            foreach (Groupe g in document.Groupes)
            {
                GroupeDto gd = new GroupeDto();
                gd.id = g.Id;
                gd.noeuds = new List<string>(g.NoeudIds);
                gd.couleur = g.Couleur;
                if (g.LargeurFixe > 0) gd.largeurFixe = g.LargeurFixe;
                if (g.HauteurFixe > 0) gd.hauteurFixe = g.HauteurFixe;
                if (!string.IsNullOrEmpty(g.Titre)) gd.titre = g.Titre;
                gd.titrePosition = g.TitrePosition;
                gd.bordTrait = g.BordureTrait;
                gd.bordEpaisseur = g.BordureEpaisseur;
                if (!string.IsNullOrEmpty(g.TitreCouleur)) gd.titreCouleur = g.TitreCouleur;
                if (!string.IsNullOrEmpty(g.TitreFond)) gd.titreFond = g.TitreFond;
                gd.titreTaille = g.TitreTaille;
                dto.groupes.Add(gd);
            }
            foreach (Lien l in document.Liens)
            {
                LienDto ld = new LienDto();
                ld.id = l.Id;
                ld.sourceId = l.SourceId;
                ld.cibleId = l.CibleId;
                ld.couleur = l.Couleur;
                ld.epaisseur = l.Epaisseur;
                ld.trait = l.Trait;
                ld.fleche = l.Fleche;
                dto.liens.Add(ld);
            }

            JavaScriptSerializer serialiseur = new JavaScriptSerializer();
            serialiseur.MaxJsonLength = int.MaxValue;
            string json = serialiseur.Serialize(dto);

            using (FileStream flux = new FileStream(chemin, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entree = archive.CreateEntry("document.json");
                using (StreamWriter ecrivain = new StreamWriter(entree.Open(), Encoding.UTF8))
                    ecrivain.Write(json);

                foreach (Noeud n in document.Noeuds)
                {
                    if (!n.EstImage) continue;
                    ZipArchiveEntry entreeImage = archive.CreateEntry("images/" + n.Id + (n.ImageExtension ?? ".png"));
                    using (Stream fluxImage = entreeImage.Open())
                        fluxImage.Write(n.ImageDonnees, 0, n.ImageDonnees.Length);
                }
            }
        }

        public static DocumentCarte Charger(string chemin)
        {
            // L'archive reste ouverte pendant toute la lecture : les octets des images y sont lus.
            using (FileStream flux = new FileStream(chemin, FileMode.Open, FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
            {
                ZipArchiveEntry entree = archive.GetEntry("document.json");
                if (entree == null)
                    throw new InvalidDataException("Le fichier ne contient pas de document.json : ce n'est pas une carte Mental-o valide.");
                string json;
                using (StreamReader lecteur = new StreamReader(entree.Open(), Encoding.UTF8))
                    json = lecteur.ReadToEnd();

            JavaScriptSerializer serialiseur = new JavaScriptSerializer();
            serialiseur.MaxJsonLength = int.MaxValue;
            DocumentDto dto = serialiseur.Deserialize<DocumentDto>(json);

            DocumentCarte document = new DocumentCarte();
            if (dto != null)
            {
                if (dto.paddingBoites.HasValue && dto.paddingBoites.Value > 0)
                    document.PaddingBoites = dto.paddingBoites.Value;
                if (!string.IsNullOrEmpty(dto.policeDefaut)) document.PoliceParDefaut = dto.policeDefaut;
                if (dto.tailleDefaut.HasValue && dto.tailleDefaut.Value > 0)
                    document.TailleParDefaut = dto.tailleDefaut.Value;
                if (!string.IsNullOrEmpty(dto.couleurDefaut)) document.CouleurParDefaut = dto.couleurDefaut;
                if (!string.IsNullOrEmpty(dto.fondCouleur)) document.FondCouleur = dto.fondCouleur;
                if (!string.IsNullOrEmpty(dto.fondMotif)) document.FondMotif = dto.fondMotif;
                if (!string.IsNullOrEmpty(dto.nom)) document.NomAffichage = dto.nom;
                if (!string.IsNullOrEmpty(dto.auteur)) document.Auteur = dto.auteur;
                document.SauvegardeAuto = dto.sauvegardeAuto;
                if (!string.IsNullOrEmpty(dto.dateCreation)) document.DateCreation = dto.dateCreation;
                if (!string.IsNullOrEmpty(dto.dateModification)) document.DateModification = dto.dateModification;
            }
            if (dto != null && dto.couleursPerso != null)
            {
                foreach (string hexa in dto.couleursPerso)
                    document.AjouterCouleurPersonnalisee(hexa);
            }
            if (dto != null && dto.noeuds != null)
            {
                foreach (NoeudDto nd in dto.noeuds)
                {
                    Noeud n = new Noeud();
                    if (!string.IsNullOrEmpty(nd.id)) n.Id = nd.id;
                    n.X = nd.x;
                    n.Y = nd.y;
                    if (!string.IsNullOrEmpty(nd.alignement)) n.Alignement = nd.alignement;
                    if (!string.IsNullOrEmpty(nd.fond)) n.FondCouleur = nd.fond;
                    if (!string.IsNullOrEmpty(nd.bordCouleur)) n.BordureCouleur = nd.bordCouleur;
                    if (nd.bordEpaisseur.HasValue) n.BordureEpaisseur = nd.bordEpaisseur.Value;
                    if (!string.IsNullOrEmpty(nd.bordTrait)) n.BordureTrait = nd.bordTrait;
                    if (nd.rayon.HasValue) n.Rayon = nd.rayon.Value;
                    if (nd.largeurFixe.HasValue) n.LargeurFixe = nd.largeurFixe.Value;
                    if (!string.IsNullOrEmpty(nd.imageFichier))
                    {
                        ZipArchiveEntry entreeImage = archive.GetEntry(nd.imageFichier);
                        if (entreeImage != null)
                        {
                            using (Stream fluxImage = entreeImage.Open())
                            using (MemoryStream memoire = new MemoryStream())
                            {
                                fluxImage.CopyTo(memoire);
                                n.ImageDonnees = memoire.ToArray();
                            }
                            n.ImageExtension = Path.GetExtension(nd.imageFichier);
                            n.ImageLargeur = nd.imageLargeur ?? 200;
                            n.ImageHauteur = nd.imageHauteur ?? 200;
                        }
                    }
                    if (nd.riche != null && nd.riche.Count > 0)
                        n.Segments = DepuisDtos(nd.riche);
                    else
                        n.DefinirTexteSimple(nd.texte ?? ""); // cartes v1/v2
                    if (nd.richeApres != null && nd.richeApres.Count > 0)
                        n.SegmentsApres = DepuisDtos(nd.richeApres);
                    document.Noeuds.Add(n);
                }
            }
            if (dto != null && dto.liens != null)
            {
                foreach (LienDto ld in dto.liens)
                {
                    Lien l = new Lien();
                    if (!string.IsNullOrEmpty(ld.id)) l.Id = ld.id;
                    l.SourceId = ld.sourceId;
                    l.CibleId = ld.cibleId;
                    if (!string.IsNullOrEmpty(ld.couleur)) l.Couleur = ld.couleur;
                    if (ld.epaisseur > 0) l.Epaisseur = ld.epaisseur;
                    if (!string.IsNullOrEmpty(ld.trait)) l.Trait = ld.trait;
                    if (!string.IsNullOrEmpty(ld.fleche)) l.Fleche = ld.fleche;
                    document.Liens.Add(l);
                }
            }
            if (dto != null && dto.groupes != null)
            {
                foreach (GroupeDto gd in dto.groupes)
                {
                    Groupe g = new Groupe();
                    if (!string.IsNullOrEmpty(gd.id)) g.Id = gd.id;
                    if (!string.IsNullOrEmpty(gd.couleur)) g.Couleur = gd.couleur;
                    if (gd.noeuds != null) g.NoeudIds.AddRange(gd.noeuds);
                    if (gd.largeurFixe.HasValue) g.LargeurFixe = gd.largeurFixe.Value;
                    if (gd.hauteurFixe.HasValue) g.HauteurFixe = gd.hauteurFixe.Value;
                    if (!string.IsNullOrEmpty(gd.titre)) g.Titre = gd.titre;
                    if (!string.IsNullOrEmpty(gd.titrePosition)) g.TitrePosition = gd.titrePosition;
                    if (!string.IsNullOrEmpty(gd.bordTrait)) g.BordureTrait = gd.bordTrait;
                    if (gd.bordEpaisseur.HasValue) g.BordureEpaisseur = gd.bordEpaisseur.Value;
                    if (!string.IsNullOrEmpty(gd.titreCouleur)) g.TitreCouleur = gd.titreCouleur;
                    if (!string.IsNullOrEmpty(gd.titreFond)) g.TitreFond = gd.titreFond;
                    if (gd.titreTaille.HasValue) g.TitreTaille = gd.titreTaille.Value;
                    document.Groupes.Add(g);
                }
            }
            return document;
            }
        }

        private static List<SegmentDto> VersDtos(List<Segment> segments)
        {
            List<SegmentDto> dtos = new List<SegmentDto>();
            foreach (Segment s in segments)
            {
                SegmentDto sd = new SegmentDto();
                sd.t = s.Texte;
                sd.police = s.Police;
                sd.taille = s.Taille;
                sd.couleur = s.Couleur;
                sd.gras = s.Gras;
                sd.italique = s.Italique;
                sd.souligne = s.Souligne;
                sd.barre = s.Barre;
                sd.surlignage = s.Surlignage;
                if (!string.IsNullOrEmpty(s.AlignementLigne)) sd.alignLigne = s.AlignementLigne;
                dtos.Add(sd);
            }
            return dtos;
        }

        private static List<Segment> DepuisDtos(List<SegmentDto> dtos)
        {
            List<Segment> segments = new List<Segment>();
            foreach (SegmentDto sd in dtos)
            {
                Segment s = new Segment();
                s.Texte = sd.t ?? "";
                if (!string.IsNullOrEmpty(sd.police)) s.Police = sd.police;
                if (sd.taille > 0) s.Taille = sd.taille;
                if (!string.IsNullOrEmpty(sd.couleur)) s.Couleur = sd.couleur;
                s.Gras = sd.gras;
                s.Italique = sd.italique;
                s.Souligne = sd.souligne;
                s.Barre = sd.barre;
                s.Surlignage = sd.surlignage;
                s.AlignementLigne = sd.alignLigne;
                segments.Add(s);
            }
            return segments;
        }
    }
}
