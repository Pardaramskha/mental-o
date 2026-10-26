using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
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
        // v13+ : styles par défaut des nouveaux éléments (formes compactes « a|b|c »)
        public string boiteDefaut { get; set; }   // StyleBoite.Serialiser()
        public string lienDefaut { get; set; }    // couleur|epaisseur|trait|fleche|trace
        public string groupeDefaut { get; set; }  // couleur|trait|epaisseur
        // v15+ : étiquettes définies dans le fichier
        public List<EtiquetteDto> etiquettes { get; set; }
    }

    public class EtiquetteDto
    {
        public string nom { get; set; }
        public string couleur { get; set; }
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
        // v15+
        public bool? plie { get; set; }
        // v16+
        public int? ordre { get; set; }
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
        // v13+
        public string ombre { get; set; }
        public double? opacite { get; set; }
        // v14+ : fichiers attachés (liens) — les octets d'un fichier intégré sont dans le zip, sous zip
        public List<FichierDto> fichiers { get; set; }
        // v15+ : note cachée et noms d'étiquettes
        public string note { get; set; }
        public List<string> etiquettes { get; set; }
    }

    public class FichierDto
    {
        public string id { get; set; }
        public string nom { get; set; }         // libellé du lien
        public string chemin { get; set; }      // chemin d'origine (absolu), peut manquer
        public string nomFichier { get; set; }  // nom d'origine avec extension
        public string zip { get; set; }         // "fichiers/<id><ext>" si intégré au .tea, sinon null
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
        // v13+
        public string trace { get; set; }
        public double? courbure { get; set; }
        public string etiquette { get; set; }
    }

    /// <summary>Lecture/écriture des fichiers .tea : une archive ZIP contenant document.json
    /// (et, à partir de la v0.5, un dossier images/). L'ancienne extension .zeubi reste
    /// lisible — même format, seul le nom a infusé. Le format est le même que celui de
    /// l'ancien Mental-o WPF (v16) : les cartes voyagent dans les deux sens.</summary>
    public static class Fichier
    {
        public const string Extension = ".tea";
        /// <summary>Les extensions lues : .tea, et l'ancienne .zeubi.</summary>
        public static readonly string[] ExtensionsLues = { "tea", "zeubi" };
        public const int VersionFormat = 16; // … v14 fichiers attachés ; v15 notes, étiquettes, groupes pliés ; v16 ordre de présentation des groupes

        /// <summary>Le nom d'entrée d'un fichier intégré dans l'archive.</summary>
        private static string EntreeFichier(PieceJointe piece)
        {
            return "fichiers/" + piece.Id + piece.Extension;
        }

        /// <summary>Nombre de versions antérieures conservées dans l'archive (v0.16).</summary>
        public const int VersionsMax = 10;
        /// <summary>Deux versions séparées de moins de dix minutes se remplacent : l'autosave ne
        /// chasse pas les enregistrements manuels espacés.</summary>
        private const int MinutesEntreVersions = 10;

        public static void Enregistrer(DocumentCarte document, string chemin)
        {
            Enregistrer(document, chemin, true);
        }

        /// <summary>« avecVersions » faux = copie de secours : ni horodatage ni historique.</summary>
        public static void Enregistrer(DocumentCarte document, string chemin, bool avecVersions)
        {
            // L'ancien document.json et les versions déjà présentes sont relus avant d'écraser.
            List<KeyValuePair<string, byte[]>> versions = avecVersions ? RelireVersions(chemin) : new List<KeyValuePair<string, byte[]>>();
            using (FileStream flux = new FileStream(chemin, FileMode.Create))
                Ecrire(document, flux, avecVersions, versions);
        }

        /// <summary>Le .tea en mémoire (22/09/2026 — une carte vit dans le .plot de
        /// Marabook) : les octets de l'archive, avec l'historique des versions
        /// relu depuis les octets précédents (null = première écriture).</summary>
        public static byte[] EnregistrerEnMemoire(DocumentCarte document, byte[] precedent)
        {
            List<KeyValuePair<string, byte[]>> versions = new List<KeyValuePair<string, byte[]>>();
            if (precedent != null && precedent.Length > 0)
                using (MemoryStream source = new MemoryStream(precedent))
                    versions = RelireVersions(source, DateTime.Now);
            using (MemoryStream sortie = new MemoryStream())
            {
                Ecrire(document, sortie, true, versions);
                return sortie.ToArray();
            }
        }

        private static void Ecrire(DocumentCarte document, Stream sortie, bool avecVersions, List<KeyValuePair<string, byte[]>> versions)
        {
            string maintenant = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (avecVersions)
            {
                if (string.IsNullOrEmpty(document.DateCreation)) document.DateCreation = maintenant;
                document.DateModification = maintenant;
            }

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
            if (document.BoiteParDefaut != null)
                dto.boiteDefaut = document.BoiteParDefaut.Serialiser();
            dto.lienDefaut = document.LienCouleurDefaut + "|"
                + document.LienEpaisseurDefaut.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|"
                + document.LienTraitDefaut + "|" + document.LienFlecheDefaut + "|" + document.LienTraceDefaut;
            dto.groupeDefaut = document.GroupeCouleurDefaut + "|" + document.GroupeTraitDefaut + "|"
                + document.GroupeEpaisseurDefaut.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (document.Etiquettes.Count > 0)
            {
                dto.etiquettes = new List<EtiquetteDto>();
                foreach (Etiquette e in document.Etiquettes)
                    dto.etiquettes.Add(new EtiquetteDto { nom = e.Nom, couleur = e.Couleur });
            }
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
                if (n.Ombre != "legere") nd.ombre = n.Ombre;
                if (n.Opacite > 0 && n.Opacite < 1) nd.opacite = n.Opacite;
                if (n.EstImage)
                {
                    nd.imageFichier = "images/" + n.Id + (n.ImageExtension ?? ".png");
                    nd.imageLargeur = n.ImageLargeur;
                    nd.imageHauteur = n.ImageHauteur;
                }
                if (n.LargeurFixe > 0) nd.largeurFixe = n.LargeurFixe;
                if (n.ANote) nd.note = n.Note;
                if (n.Etiquettes.Count > 0) nd.etiquettes = new List<string>(n.Etiquettes);
                if (n.Fichiers.Count > 0)
                {
                    nd.fichiers = new List<FichierDto>();
                    foreach (PieceJointe piece in n.Fichiers)
                    {
                        FichierDto fd = new FichierDto();
                        fd.id = piece.Id;
                        fd.nom = piece.Nom;
                        fd.chemin = piece.Chemin;
                        fd.nomFichier = piece.NomFichier;
                        if (piece.EstIntegre) fd.zip = EntreeFichier(piece);
                        nd.fichiers.Add(fd);
                    }
                }
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
                if (g.Plie) gd.plie = true;
                if (g.OrdrePresentation > 0) gd.ordre = g.OrdrePresentation;
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
                if (l.Trace != "droit") ld.trace = l.Trace;
                if (l.Courbure != 0) ld.courbure = l.Courbure;
                if (!string.IsNullOrEmpty(l.Etiquette)) ld.etiquette = l.Etiquette;
                dto.liens.Add(ld);
            }

            string json = Json.Serialiser(dto);

            using (ZipArchive archive = new ZipArchive(sortie, ZipArchiveMode.Create, true))
            {
                ZipArchiveEntry entree = archive.CreateEntry("document.json");
                using (StreamWriter ecrivain = new StreamWriter(entree.Open(), new UTF8Encoding(false)))
                    ecrivain.Write(json);

                foreach (KeyValuePair<string, byte[]> version in versions)
                {
                    ZipArchiveEntry entreeVersion = archive.CreateEntry(version.Key);
                    using (Stream fluxVersion = entreeVersion.Open())
                        fluxVersion.Write(version.Value, 0, version.Value.Length);
                }

                foreach (Noeud n in document.Noeuds)
                {
                    if (n.EstImage)
                    {
                        ZipArchiveEntry entreeImage = archive.CreateEntry("images/" + n.Id + (n.ImageExtension ?? ".png"));
                        using (Stream fluxImage = entreeImage.Open())
                            fluxImage.Write(n.ImageDonnees, 0, n.ImageDonnees.Length);
                    }
                    foreach (PieceJointe piece in n.Fichiers)
                    {
                        if (!piece.EstIntegre) continue;
                        ZipArchiveEntry entreePiece = archive.CreateEntry(EntreeFichier(piece));
                        using (Stream fluxPiece = entreePiece.Open())
                            fluxPiece.Write(piece.Donnees, 0, piece.Donnees.Length);
                    }
                }
            }
        }

        /// <summary>Relit, dans l'archive existante, le document.json courant (qui devient une
        /// version) et les versions déjà conservées ; rend la liste à réécrire, plafonnée.</summary>
        private static List<KeyValuePair<string, byte[]>> RelireVersions(string chemin)
        {
            if (string.IsNullOrEmpty(chemin) || !File.Exists(chemin)) return new List<KeyValuePair<string, byte[]>>();
            try
            {
                using (FileStream flux = new FileStream(chemin, FileMode.Open, FileAccess.Read))
                    return RelireVersions(flux, File.GetLastWriteTime(chemin));
            }
            catch
            {
                // Une archive illisible ne doit pas empêcher d'enregistrer par-dessus.
                return new List<KeyValuePair<string, byte[]>>();
            }
        }

        /// <summary>Relit document.json et les versions d'une archive .tea (fichier
        /// ou octets) ; « dateFichier » sert d'horodatage quand le document n'en a pas.</summary>
        private static List<KeyValuePair<string, byte[]>> RelireVersions(Stream flux, DateTime dateFichier)
        {
            List<KeyValuePair<string, byte[]>> versions = new List<KeyValuePair<string, byte[]>>();
            try
            {
                byte[] courant = null;
                string horodatage = null;
                using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read, true))
                {
                    foreach (ZipArchiveEntry entree in archive.Entries)
                    {
                        if (entree.FullName == "document.json")
                        {
                            courant = LireOctets(entree);
                            string json = Encoding.UTF8.GetString(courant);
                            int position = json.IndexOf("\"dateModification\":\"", StringComparison.Ordinal);
                            if (position >= 0)
                            {
                                int debut = position + "\"dateModification\":\"".Length;
                                int fin = json.IndexOf('"', debut);
                                if (fin > debut) horodatage = json.Substring(debut, fin - debut);
                            }
                        }
                        else if (entree.FullName.StartsWith("versions/") && entree.FullName.EndsWith(".json"))
                            versions.Add(new KeyValuePair<string, byte[]>(entree.FullName, LireOctets(entree)));
                    }
                }
                if (courant == null) return versions;
                if (string.IsNullOrEmpty(horodatage) || horodatage.Length < 16)
                    horodatage = dateFichier.ToString("yyyy-MM-dd HH:mm");
                string nom = "versions/" + horodatage.Replace(" ", "_").Replace(":", "-") + ".json";
                // Les noms se trient chronologiquement : la plus récente en dernier.
                versions.Sort(delegate(KeyValuePair<string, byte[]> a, KeyValuePair<string, byte[]> b)
                {
                    return string.CompareOrdinal(a.Key, b.Key);
                });
                // Trop proche de la dernière version ? Elle la remplace.
                if (versions.Count > 0 && Proches(versions[versions.Count - 1].Key, nom))
                    versions.RemoveAt(versions.Count - 1);
                versions.RemoveAll(delegate(KeyValuePair<string, byte[]> v) { return v.Key == nom; });
                versions.Add(new KeyValuePair<string, byte[]>(nom, courant));
                while (versions.Count > VersionsMax) versions.RemoveAt(0);
            }
            catch
            {
                // Une archive illisible ne doit pas empêcher d'enregistrer par-dessus.
            }
            return versions;
        }

        private static bool Proches(string nomA, string nomB)
        {
            DateTime a, b;
            if (!DateDeVersion(nomA, out a) || !DateDeVersion(nomB, out b)) return false;
            return Math.Abs((b - a).TotalMinutes) < MinutesEntreVersions;
        }

        /// <summary>« versions/2026-09-07_14-32.json » → la date ; faux si le nom est étranger.</summary>
        public static bool DateDeVersion(string nomEntree, out DateTime date)
        {
            date = DateTime.MinValue;
            string nom = Path.GetFileNameWithoutExtension(nomEntree);
            return DateTime.TryParseExact(nom, "yyyy-MM-dd_HH-mm", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out date);
        }

        private static byte[] LireOctets(ZipArchiveEntry entree)
        {
            using (Stream flux = entree.Open())
            using (MemoryStream memoire = new MemoryStream())
            {
                flux.CopyTo(memoire);
                return memoire.ToArray();
            }
        }

        /// <summary>Les versions antérieures conservées dans une archive, la plus récente d'abord.</summary>
        public static List<string> ListerVersions(string chemin)
        {
            List<string> noms = new List<string>();
            if (string.IsNullOrEmpty(chemin) || !File.Exists(chemin)) return noms;
            using (FileStream flux = new FileStream(chemin, FileMode.Open, FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entree in archive.Entries)
                    if (entree.FullName.StartsWith("versions/") && entree.FullName.EndsWith(".json"))
                        noms.Add(entree.FullName);
            }
            noms.Sort(delegate(string a, string b) { return string.CompareOrdinal(b, a); });
            return noms;
        }

        /// <summary>Charge une version antérieure ; les images et fichiers intégrés encore
        /// présents dans l'archive sont retrouvés, les autres manquent simplement.</summary>
        public static DocumentCarte ChargerVersion(string chemin, string nomEntree)
        {
            using (FileStream flux = new FileStream(chemin, FileMode.Open, FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
                return ChargerDepuisArchive(archive, nomEntree);
        }

        /// <summary>Une carte depuis les octets d'un .tea (Marabook, 22/09/2026).</summary>
        public static DocumentCarte Charger(byte[] octets)
        {
            using (MemoryStream flux = new MemoryStream(octets))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
                return ChargerDepuisArchive(archive, "document.json");
        }

        public static List<string> ListerVersions(byte[] octets)
        {
            List<string> noms = new List<string>();
            if (octets == null || octets.Length == 0) return noms;
            using (MemoryStream flux = new MemoryStream(octets))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entree in archive.Entries)
                    if (entree.FullName.StartsWith("versions/") && entree.FullName.EndsWith(".json"))
                        noms.Add(entree.FullName);
            }
            noms.Sort(delegate(string a, string b) { return string.CompareOrdinal(b, a); });
            return noms;
        }

        public static DocumentCarte ChargerVersion(byte[] octets, string nomEntree)
        {
            using (MemoryStream flux = new MemoryStream(octets))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
                return ChargerDepuisArchive(archive, nomEntree);
        }

        public static DocumentCarte Charger(string chemin)
        {
            // L'archive reste ouverte pendant toute la lecture : les octets des images y sont lus.
            using (FileStream flux = new FileStream(chemin, FileMode.Open, FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
                return ChargerDepuisArchive(archive, "document.json");
        }

        private static DocumentCarte ChargerDepuisArchive(ZipArchive archive, string nomEntree)
        {
            ZipArchiveEntry entree = archive.GetEntry(nomEntree);
            if (entree == null)
                throw new InvalidDataException("Le fichier ne contient pas de document.json : ce n'est pas une carte Mental-O valide.");
            string json;
            using (StreamReader lecteur = new StreamReader(entree.Open(), Encoding.UTF8))
                json = lecteur.ReadToEnd();

            DocumentDto dto = Json.Deserialiser<DocumentDto>(json);

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
                StyleBoite boiteDefaut = StyleBoite.Deserialiser(dto.boiteDefaut);
                if (boiteDefaut != null) document.BoiteParDefaut = boiteDefaut;
                if (!string.IsNullOrEmpty(dto.lienDefaut))
                {
                    string[] parties = dto.lienDefaut.Split('|');
                    if (parties.Length == 5)
                    {
                        if (!string.IsNullOrEmpty(parties[0])) document.LienCouleurDefaut = parties[0];
                        double epaisseur;
                        if (double.TryParse(parties[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out epaisseur) && epaisseur > 0)
                            document.LienEpaisseurDefaut = epaisseur;
                        if (!string.IsNullOrEmpty(parties[2])) document.LienTraitDefaut = parties[2];
                        if (!string.IsNullOrEmpty(parties[3])) document.LienFlecheDefaut = parties[3];
                        if (!string.IsNullOrEmpty(parties[4])) document.LienTraceDefaut = parties[4];
                    }
                }
                if (!string.IsNullOrEmpty(dto.groupeDefaut))
                {
                    string[] parties = dto.groupeDefaut.Split('|');
                    if (parties.Length == 3)
                    {
                        if (!string.IsNullOrEmpty(parties[0])) document.GroupeCouleurDefaut = parties[0];
                        if (!string.IsNullOrEmpty(parties[1])) document.GroupeTraitDefaut = parties[1];
                        double epaisseur;
                        if (double.TryParse(parties[2], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out epaisseur) && epaisseur > 0)
                            document.GroupeEpaisseurDefaut = epaisseur;
                    }
                }
            }
            if (dto != null && dto.couleursPerso != null)
            {
                foreach (string hexa in dto.couleursPerso)
                    document.AjouterCouleurPersonnalisee(hexa);
            }
            if (dto != null && dto.etiquettes != null)
            {
                foreach (EtiquetteDto ed in dto.etiquettes)
                    if (!string.IsNullOrWhiteSpace(ed.nom) && document.TrouverEtiquette(ed.nom) == null)
                        document.Etiquettes.Add(new Etiquette(ed.nom, ed.couleur));
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
                    if (!string.IsNullOrEmpty(nd.ombre)) n.Ombre = nd.ombre;
                    if (nd.opacite.HasValue && nd.opacite.Value > 0) n.Opacite = nd.opacite.Value;
                    if (nd.largeurFixe.HasValue) n.LargeurFixe = nd.largeurFixe.Value;
                    if (!string.IsNullOrEmpty(nd.note)) n.Note = nd.note;
                    if (nd.etiquettes != null)
                        foreach (string nom in nd.etiquettes)
                            if (!string.IsNullOrWhiteSpace(nom) && !n.Etiquettes.Contains(nom)) n.Etiquettes.Add(nom);
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
                    if (nd.fichiers != null)
                    {
                        foreach (FichierDto fd in nd.fichiers)
                        {
                            PieceJointe piece = new PieceJointe();
                            if (!string.IsNullOrEmpty(fd.id)) piece.Id = fd.id;
                            piece.Chemin = fd.chemin;
                            piece.NomFichier = fd.nomFichier;
                            if (string.IsNullOrEmpty(piece.NomFichier) && !string.IsNullOrEmpty(fd.chemin))
                                piece.NomFichier = Path.GetFileName(fd.chemin);
                            piece.Nom = string.IsNullOrEmpty(fd.nom) ? (piece.NomFichier ?? "Fichier") : fd.nom;
                            if (!string.IsNullOrEmpty(fd.zip))
                            {
                                ZipArchiveEntry entreePiece = archive.GetEntry(fd.zip);
                                if (entreePiece != null)
                                {
                                    using (Stream fluxPiece = entreePiece.Open())
                                    using (MemoryStream memoire = new MemoryStream())
                                    {
                                        fluxPiece.CopyTo(memoire);
                                        piece.Donnees = memoire.ToArray();
                                    }
                                }
                            }
                            // Un fichier ni intégré ni localisable reste un lien (signalé introuvable à l'écran).
                            if (piece.Donnees == null && string.IsNullOrEmpty(piece.Chemin)
                                && string.IsNullOrEmpty(piece.NomFichier))
                                continue;
                            n.Fichiers.Add(piece);
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
                    if (!string.IsNullOrEmpty(ld.trace)) l.Trace = ld.trace;
                    if (ld.courbure.HasValue) l.Courbure = ld.courbure.Value;
                    if (!string.IsNullOrEmpty(ld.etiquette)) l.Etiquette = ld.etiquette;
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
                    g.Plie = gd.plie == true;
                    if (gd.ordre.HasValue && gd.ordre.Value > 0) g.OrdrePresentation = gd.ordre.Value;
                    document.Groupes.Add(g);
                }
            }
            return document;
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
