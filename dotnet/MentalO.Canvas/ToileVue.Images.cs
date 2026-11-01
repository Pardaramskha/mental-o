using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Images, dépôt de fichiers, redimensionnement (grip), rendu en image,
    /// fond du tableau, puces et préférences du document.</summary>
    public partial class ToileVue
    {
        /// <summary>Rend la carte en image (échelle 2×, fond du tableau compris) — le socle
        /// commun de l'export PNG et des vignettes. Renvoie null si la carte est vide ou si
        /// le rendu échoue. Le canevas est rendu dans un conteneur hors écran le temps du
        /// rendu, puis remis en place.</summary>
        public RenderTargetBitmap RendreCarte(out string erreur)
        {
            return RendreCarte(2.0, 8000.0, out erreur);
        }

        public RenderTargetBitmap RendreCarte(double echelleMax, double coteMax, out string erreur)
        {
            erreur = null;
            Rect bornes = BornesContenu();
            if (EstVide(bornes))
            {
                erreur = "La carte est vide : rien à exporter.";
                return null;
            }
            bornes = bornes.Inflate(30);

            // Les artefacts d'interface ne font pas partie de la carte.
            foreach (NoeudVisuel v in _selectionnes) v.DefinirSelection(false);
            if (_selectionLien != null) _selectionLien.DefinirSelection(false);
            bool gripAvant = _grip.IsVisible;
            bool courbureAvant = _poigneeCourbure.IsVisible;
            _grip.IsVisible = false;
            _poigneeCourbure.IsVisible = false;
            CacherPoignees();
            double echelleAvant = _echelle.ScaleX, txAvant = _translation.X, tyAvant = _translation.Y;

            try
            {
                double echelle = Math.Min(echelleMax, coteMax / Math.Max(bornes.Width, bornes.Height));
                int largeur = Math.Max(1, (int)Math.Ceiling(bornes.Width * echelle));
                int hauteur = Math.Max(1, (int)Math.Ceiling(bornes.Height * echelle));

                // Le rendu se fait dans le repère du canevas : pan et zoom sont remplacés
                // par le cadrage de l'emprise.
                _echelle.ScaleX = echelle; _echelle.ScaleY = echelle;
                _translation.X = -bornes.X * echelle;
                _translation.Y = -bornes.Y * echelle;

                Child = null;
                Border fond = new Border();
                fond.Width = largeur;
                fond.Height = hauteur;
                fond.Background = Background;
                Panel conteneur = new Panel();
                conteneur.Width = largeur;
                conteneur.Height = hauteur;
                conteneur.ClipToBounds = true;
                conteneur.Children.Add(fond);
                conteneur.Children.Add(_canvas);
                try
                {
                    conteneur.Measure(new Size(largeur, hauteur));
                    conteneur.Arrange(new Rect(0, 0, largeur, hauteur));
                    RenderTargetBitmap rendu = new RenderTargetBitmap(new PixelSize(largeur, hauteur), new Vector(96, 96));
                    rendu.Render(conteneur);
                    return rendu;
                }
                finally
                {
                    conteneur.Children.Remove(_canvas);
                    Child = _canvas;
                }
            }
            catch (Exception ex)
            {
                erreur = ex.Message;
                return null;
            }
            finally
            {
                _echelle.ScaleX = echelleAvant; _echelle.ScaleY = echelleAvant;
                _translation.X = txAvant; _translation.Y = tyAvant;
                _grip.IsVisible = gripAvant;
                _poigneeCourbure.IsVisible = courbureAvant;
                foreach (NoeudVisuel v in _selectionnes) v.DefinirSelection(true);
                if (_selectionLien != null) _selectionLien.DefinirSelection(true);
                InvalidateMeasure();
                SignalerVue();
            }
        }

        /// <summary>Rend la carte en PNG. Renvoie faux avec un message en cas d'échec.</summary>
        public bool ExporterPng(string chemin, out string erreur)
        {
            RenderTargetBitmap rendu = RendreCarte(out erreur);
            if (rendu == null) return false;
            try
            {
                using (rendu)
                    rendu.Save(chemin);
                return true;
            }
            catch (Exception ex)
            {
                erreur = ex.Message;
                return false;
            }
        }

        /// <summary>Les octets PNG de la carte (vignettes, presse-papiers), ou null.</summary>
        public byte[] RendrePng(double echelleMax, double coteMax, out string erreur)
        {
            RenderTargetBitmap rendu = RendreCarte(echelleMax, coteMax, out erreur);
            if (rendu == null) return null;
            try
            {
                using (rendu)
                using (MemoryStream flux = new MemoryStream())
                {
                    rendu.Save(flux);
                    return flux.ToArray();
                }
            }
            catch (Exception ex)
            {
                erreur = ex.Message;
                return null;
            }
        }

        /// <summary>Ajoute d'un bloc (une seule annulation) les boîtes et liens d'un import,
        /// puis les sélectionne.</summary>
        public void ImporterArbre(List<Noeud> noeuds, List<Lien> liens)
        {
            if (_mode == "lecture" || noeuds.Count == 0) return;
            List<ICommande> commandes = new List<ICommande>();
            foreach (Noeud n in noeuds)
            {
                if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(n);
                commandes.Add(new CommandeCreerNoeud(this, n));
            }
            foreach (Lien l in liens)
            {
                // Les liens importés suivent les défauts du fichier, sauf la flèche
                // (l'import oriente déjà parent → enfant).
                Lien gabarit = Document.NouveauLien();
                l.Couleur = gabarit.Couleur;
                l.Epaisseur = gabarit.Epaisseur;
                l.Trait = gabarit.Trait;
                l.Trace = gabarit.Trace;
                l.Courbure = gabarit.Courbure;
                commandes.Add(new CommandeCreerLien(this, l));
            }
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));
            List<NoeudVisuel> visuels = new List<NoeudVisuel>();
            foreach (Noeud n in noeuds)
            {
                NoeudVisuel visuel;
                if (_visuels.TryGetValue(n, out visuel)) visuels.Add(visuel);
            }
            SelectionnerEnsemble(visuels, visuels.Count > 0 ? visuels[0] : null);
        }

        // --- Fond du tableau ---------------------------------------------------------------

        /// <summary>La couleur de fond réellement affichée : si l'utilisateur n'a pas changé
        /// la couleur par défaut, le thème sombre lui substitue un bleu très foncé
        /// (le motif n'entre pas dans ce critère).</summary>
        public Color CouleurFondEffective()
        {
            if (FondSombreSubstitue()) return Color.FromRgb(0x16, 0x20, 0x2E);
            return Palette.CouleurDepuis(Document.FondCouleur, Color.FromRgb(0xF7, 0xF5, 0xF0));
        }

        /// <summary>Vrai quand le thème sombre remplace le fond d'usine : les boîtes
        /// d'usine (blanches, texte noir) suivent alors (Palette.SubstitutionSombre).</summary>
        private bool FondSombreSubstitue()
        {
            bool couleurParDefaut = string.Equals(Document.FondCouleur, "#F7F5F0", StringComparison.OrdinalIgnoreCase);
            return couleurParDefaut && Mentalo.Parametres.ParametresGlobaux.ThemeSombre;
        }

        /// <summary>Le fond du tableau — et, le thème sombre ayant pu changer, les
        /// boîtes, les titres de groupes et les étiquettes de liens.</summary>
        public void AppliquerFondDocument()
        {
            Palette.SubstitutionSombre = FondSombreSubstitue();
            Background = ConstruireFond(CouleurFondEffective(), Document.FondMotif);
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
            {
                kv.Value.MettreAJourStyle();
                kv.Value.MettreAJour();
            }
            foreach (KeyValuePair<Groupe, TextBlock> kv in _titresGroupes)
                AppliquerStyleVisuelGroupe(kv.Key);
            // Les étiquettes de liens portent le fond du tableau pour « couper » le trait.
            LienVisuel.FondEtiquette = CouleurFondEffective();
            foreach (KeyValuePair<Lien, LienVisuel> kv in _visuelsLiens)
                kv.Value.AppliquerStyle();
        }

        public static IBrush ConstruireFond(Color fond, string motif)
        {
            if (motif != "points" && motif != "grille")
                return new SolidColorBrush(fond);

            // Fond clair → motif plus foncé ; fond sombre → motif plus clair.
            bool fondSombre = fond.R * 0.299 + fond.G * 0.587 + fond.B * 0.114 < 100;
            Color trait = fondSombre
                ? Color.FromRgb(
                    (byte)Math.Min(255, fond.R + 28),
                    (byte)Math.Min(255, fond.G + 28),
                    (byte)Math.Min(255, fond.B + 30))
                : Color.FromRgb(
                    (byte)Math.Max(0, fond.R - 22),
                    (byte)Math.Max(0, fond.G - 22),
                    (byte)Math.Max(0, fond.B - 20));
            DrawingGroup dessin = new DrawingGroup();
            dessin.Children.Add(new GeometryDrawing
            {
                Brush = new SolidColorBrush(fond),
                Geometry = new RectangleGeometry(new Rect(0, 0, 24, 24))
            });
            if (motif == "points")
            {
                dessin.Children.Add(new GeometryDrawing
                {
                    Brush = new SolidColorBrush(trait),
                    Geometry = new EllipseGeometry(new Rect(10.8, 10.8, 2.4, 2.4))
                });
            }
            else
            {
                GeometryGroup lignes = new GeometryGroup();
                lignes.Children.Add(new RectangleGeometry(new Rect(0, 0, 24, 1)));
                lignes.Children.Add(new RectangleGeometry(new Rect(0, 0, 1, 24)));
                dessin.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(trait), Geometry = lignes });
            }
            DrawingBrush brosse = new DrawingBrush(dessin);
            brosse.TileMode = TileMode.Tile;
            brosse.DestinationRect = new RelativeRect(0, 0, 24, 24, RelativeUnit.Absolute);
            brosse.SourceRect = new RelativeRect(0, 0, 24, 24, RelativeUnit.Absolute);
            brosse.Stretch = Stretch.None;
            return brosse;
        }

        // --- Listes à puces ----------------------------------------------------------------

        /// <summary>Bascule les puces ("puce" | "tiret" | "numero" | "case") sur toute la boîte
        /// sélectionnée. En édition, seulement les lignes sélectionnées.</summary>
        public void BasculerPuces(string type)
        {
            if (_selection == null || _mode == "lecture") return;
            if (_selection.EnEdition)
            {
                _selection.BasculerPucesSelection(type);
                return;
            }
            Noeud noeud = _selection.Modele;
            List<Segment> anciens = Noeud.CopieDe(noeud.Segments);
            List<Segment> anciensApres = Noeud.CopieDe(noeud.SegmentsApres);
            List<Segment> nouveaux = PucesTexte.Basculer(noeud.Segments, type);
            List<Segment> nouveauxApres = PucesTexte.Basculer(noeud.SegmentsApres, type);
            if (Noeud.SegmentsEgaux(anciens, nouveaux) && Noeud.SegmentsEgaux(anciensApres, nouveauxApres))
                return;
            Historique.ExecuterEtPousser(new CommandeModifierTexteRiche(this, noeud,
                anciens, anciensApres, noeud.Alignement,
                nouveaux, nouveauxApres, noeud.Alignement));
        }

        /// <summary>Répercute les préférences du fichier (padding, texte par défaut) sur le rendu.</summary>
        public void AppliquerPreferencesDocument(bool signaler)
        {
            Segment.PoliceDefaut = Document.PoliceParDefaut;
            Segment.TailleDefaut = Document.TailleParDefaut;
            Segment.CouleurDefaut = Document.CouleurParDefaut;
            NoeudVisuel.PaddingTexte = Document.PaddingBoites;
            AppliquerFondDocument(); // redessine aussi les boîtes
            if (signaler) SignalerModification();
        }

        // ------------------------------------------------------------------ images

        public static bool ExtensionImageValide(string chemin)
        {
            return Images.ExtensionValide(chemin);
        }

        public void InsererImageDepuisFichier(string chemin)
        {
            InsererImage(chemin, CentreCanvas());
        }

        public void InsererImage(string chemin, Point centre)
        {
            try
            {
                FileInfo informations = new FileInfo(chemin);
                if (informations.Length > TailleImageMax)
                {
                    Hote.Message(FenetreProprietaire(),
                        string.Format("« {0} » dépasse la limite de 5 Mo ({1:0.0} Mo).",
                            informations.Name, informations.Length / 1048576.0),
                        "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
                    return;
                }
                InsererImageDonnees(File.ReadAllBytes(chemin),
                    System.IO.Path.GetExtension(chemin).ToLowerInvariant(), centre);
            }
            catch (Exception ex)
            {
                Hote.Message(FenetreProprietaire(),
                    string.Format("Impossible d'importer l'image :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
        }

        public void InsererImageDonnees(byte[] donnees, string extension, Point centre)
        {
            if (_mode == "lecture") return;
            if (donnees.LongLength > TailleImageMax)
            {
                Hote.Message(FenetreProprietaire(), "L'image dépasse la limite de 5 Mo.",
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
                return;
            }
            Bitmap image;
            try
            {
                image = NoeudVisuel.DecoderImage(donnees);
            }
            catch
            {
                Hote.Message(FenetreProprietaire(), "Format d'image illisible.",
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
                return;
            }

            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.ImageDonnees = donnees;
            noeud.ImageExtension = string.IsNullOrEmpty(extension) ? ".png" : extension;
            double facteur = Math.Min(1.0, 320.0 / Math.Max(image.PixelSize.Width, (double)image.PixelSize.Height));
            noeud.ImageLargeur = Math.Max(20, image.PixelSize.Width * facteur);
            noeud.ImageHauteur = Math.Max(20, image.PixelSize.Height * facteur);
            noeud.X = centre.X - noeud.ImageLargeur / 2;
            noeud.Y = centre.Y - noeud.ImageHauteur / 2;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            Selectionner(_visuels[noeud]);
        }

        /// <summary>Colle les images (fichiers) du presse-papiers ; le texte est collé en
        /// boîte si le presse-papiers interne n'est pas d'actualité.</summary>
        public async void CollerImage()
        {
            try
            {
                IClipboard presse = Ui.PressePapier(this);
                if (presse == null) return;
                object fichiersBruts = null;
                try { fichiersBruts = await presse.GetDataAsync(DataFormats.Files); }
                catch { }
                IEnumerable<IStorageItem> fichiers = fichiersBruts as IEnumerable<IStorageItem>;
                if (fichiers != null)
                {
                    Point centre = CentreCanvas();
                    double decalage = 0;
                    foreach (IStorageItem element in fichiers)
                    {
                        string fichier = Ui.CheminLocal(element);
                        if (fichier == null || !ExtensionImageValide(fichier)) continue;
                        InsererImage(fichier, new Point(centre.X + decalage, centre.Y + decalage));
                        decalage += 24;
                    }
                    if (decalage > 0) return;
                }
                // Une image brute : Avalonia ne la lit pas de façon portable (PNG en « image/png » sous Linux/mac).
                object png = null;
                try { png = await presse.GetDataAsync("image/png"); }
                catch { }
                byte[] octets = png as byte[];
                if (octets != null && octets.Length > 0)
                {
                    InsererImageDonnees(octets, ".png", CentreCanvas());
                    return;
                }
                string texte = await presse.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(texte) && texte != _marqueurPressePapier && _mode != "lecture")
                    DeposerTexte(texte, null, CentreCanvas());
            }
            catch (Exception ex)
            {
                Hote.Message(FenetreProprietaire(),
                    string.Format("Impossible de coller :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
        }

        public Point CentreCanvas()
        {
            return new Point((LargeurVue / 2 - _translation.X) / _echelle.ScaleX,
                             (HauteurVue / 2 - _translation.Y) / _echelle.ScaleY);
        }

        private void DeposerFichiers(object expediteur, DragEventArgs e)
        {
            Point position = e.GetPosition(_canvas);
            IEnumerable<IStorageItem> elements = null;
            try { elements = e.Data.GetFiles(); }
            catch { }
            List<string> fichiers = new List<string>();
            if (elements != null)
                foreach (IStorageItem element in elements)
                {
                    string chemin = Ui.CheminLocal(element);
                    if (chemin != null) fichiers.Add(chemin);
                }
            if (fichiers.Count == 0)
            {
                // Texte ou adresse glissés depuis un navigateur ou un éditeur.
                if (_mode == "lecture") return;
                string texte = null;
                try { texte = e.Data.GetText(); }
                catch { }
                if (texte != null) DeposerTexte(texte, NoeudSousPoint(position), position);
                return;
            }
            // Déposés sur une boîte : la première image s'y attache (une seule par boîte),
            // les autres fichiers s'y lient tous ; le reste devient des boîtes.
            NoeudVisuel noeudCible = NoeudSousPoint(position);
            bool imageAttachee = false;
            double decalage = 0;
            bool lierTexte = (e.KeyModifiers & KeyModifiers.Alt) != 0; // Alt : lier un .txt/.md au lieu de l'importer
            foreach (string fichier in fichiers)
            {
                bool image = ExtensionImageValide(fichier);
                if (!image && !EstFichierAttachable(fichier)) continue; // dossiers, cibles disparues
                if (!image && !lierTexte && EstFichierTexte(fichier) && _mode != "lecture")
                {
                    // Un .txt / .md déposé devient des boîtes (plan → arbre) ; Alt pour le lier.
                    string texte;
                    try { texte = File.ReadAllText(fichier); }
                    catch { texte = null; }
                    if (texte != null)
                    {
                        DeposerTexte(texte, null, new Point(position.X + decalage, position.Y + decalage));
                        decalage += 24;
                        continue;
                    }
                }
                if (noeudCible != null)
                {
                    if (image)
                    {
                        if (imageAttachee) { InsererImage(fichier, new Point(position.X + decalage, position.Y + decalage)); decalage += 24; }
                        else { AttacherImage(noeudCible.Modele, fichier); imageAttachee = true; }
                    }
                    else
                        AttacherFichier(noeudCible.Modele, fichier);
                    continue;
                }
                if (image) InsererImage(fichier, new Point(position.X + decalage, position.Y + decalage));
                else InsererFichierNouvelleBoite(fichier, new Point(position.X + decalage, position.Y + decalage));
                decalage += 24;
            }
        }

        public void DefinirTailleImage(Noeud noeud, double largeur, double hauteur)
        {
            noeud.ImageLargeur = largeur;
            noeud.ImageHauteur = hauteur;
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.MettreAJour();
            MettreAJourLiensDe(noeud);
            SignalerModification();
        }

        private void CommencerRedimension(IPointer pointeur)
        {
            if (_mode == "lecture") return;
            if (_groupeActif != null)
            {
                Rectangle cadre;
                if (!_visuelsGroupes.TryGetValue(_groupeActif, out cadre)) return;
                _redimensionEnCours = true;
                _groupeRedimensionne = _groupeActif;
                _departGrpL = _groupeRedimensionne.LargeurFixe;
                _departGrpH = _groupeRedimensionne.HauteurFixe;
                _origineCadreX = Canvas.GetLeft(cadre);
                _origineCadreY = Canvas.GetTop(cadre);
                Capturer(pointeur);
                return;
            }
            if (_selection == null) return;
            _redimensionEnCours = true;
            _noeudRedimensionne = _selection.Modele;
            if (_noeudRedimensionne.EstImage)
            {
                _departImgL = _noeudRedimensionne.ImageLargeur;
                _departImgH = _noeudRedimensionne.ImageHauteur;
                _chromeL = _selection.Bounds.Width - _departImgL;
                _chromeH = _selection.Bounds.Height - _departImgH;
            }
            else
            {
                _departLargeurFixe = _noeudRedimensionne.LargeurFixe;
            }
            Capturer(pointeur);
        }

        private void MettreAJourGrip()
        {
            if (_mode == "lecture")
            {
                _grip.IsVisible = false;
                return;
            }
            // Groupe entièrement sélectionné : la poignée redimensionne son cadre (pas une pastille).
            if (_groupeActif != null)
            {
                if (_groupeActif.Plie)
                {
                    _grip.IsVisible = false;
                    return;
                }
                Rectangle cadre;
                if (_visuelsGroupes.TryGetValue(_groupeActif, out cadre) && cadre.IsVisible)
                {
                    _grip.Cursor = Ui.CurseurDiagonale;
                    Canvas.SetLeft(_grip, Canvas.GetLeft(cadre) + cadre.Width - 6);
                    Canvas.SetTop(_grip, Canvas.GetTop(cadre) + cadre.Height - 6);
                    _grip.IsVisible = true;
                    return;
                }
            }
            if (_selection != null && _selectionnes.Count == 1 && _selection.Bounds.Width > 0 && !_selection.EnEdition)
            {
                _grip.Cursor = _selection.Modele.EstImage ? Ui.CurseurDiagonale : Ui.CurseurLargeur;
                Canvas.SetLeft(_grip, _selection.Modele.X + _selection.Bounds.Width - 6);
                Canvas.SetTop(_grip, _selection.Modele.Y + _selection.Bounds.Height - 6);
                _grip.IsVisible = true;
            }
            else
            {
                _grip.IsVisible = false;
            }
        }

        public void DefinirLargeurNoeud(Noeud noeud, double largeur)
        {
            noeud.LargeurFixe = largeur;
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.MettreAJour();
            MettreAJourLiensDe(noeud);
            if (_selection != null && _selection.Modele == noeud) MettreAJourGrip();
            SignalerModification();
        }

        public void DefinirImageNoeud(Noeud noeud, byte[] octets, string extension, double largeur, double hauteur)
        {
            noeud.ImageDonnees = octets;
            noeud.ImageExtension = extension;
            noeud.ImageLargeur = largeur;
            noeud.ImageHauteur = hauteur;
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.MettreAJour();
            MettreAJourLiensDe(noeud);
            if (_selection != null && _selection.Modele == noeud) MettreAJourGrip();
            SignalerModification();
            SignalerFormatCible();
        }

        /// <summary>Choisit une image et l'attache à la boîte sélectionnée (texte + image).</summary>
        public async void InsererImageDansBoite()
        {
            if (_selection == null) return;
            NoeudVisuel cible = _selection;
            string chemin;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                chemin = await Ui.ChoisirFichier(this, "Insérer une image dans la boîte", Ui.TypeImages, Ui.TypeTous);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
            cible.RedonnerFocus();
            if (chemin != null) AttacherImage(cible.Modele, chemin);
        }

        private void AttacherImage(Noeud noeud, string chemin)
        {
            try
            {
                FileInfo informations = new FileInfo(chemin);
                if (informations.Length > TailleImageMax)
                {
                    Hote.Message(FenetreProprietaire(),
                        string.Format("« {0} » dépasse la limite de 5 Mo ({1:0.0} Mo).",
                            informations.Name, informations.Length / 1048576.0),
                        "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
                    return;
                }
                byte[] donnees = File.ReadAllBytes(chemin);
                Bitmap image = NoeudVisuel.DecoderImage(donnees);
                double facteur = Math.Min(1.0, 280.0 / Math.Max(image.PixelSize.Width, (double)image.PixelSize.Height));
                Historique.ExecuterEtPousser(new CommandeDefinirImage(this, noeud,
                    donnees, System.IO.Path.GetExtension(chemin).ToLowerInvariant(),
                    Math.Max(20, image.PixelSize.Width * facteur), Math.Max(20, image.PixelSize.Height * facteur)));
            }
            catch (Exception ex)
            {
                Hote.Message(FenetreProprietaire(),
                    string.Format("Impossible d'importer l'image :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
        }

        /// <summary>Ramène la boîte sélectionnée à sa taille naturelle : largeur automatique,
        /// et image remise à son échelle d'import (grand côté ≤ 320 px).</summary>
        public void ReajusterBoite()
        {
            if (_selection == null) return;
            Noeud noeud = _selection.Modele;
            List<ICommande> commandes = new List<ICommande>();
            if (noeud.LargeurFixe > 0)
                commandes.Add(new CommandeRedimensionnerNoeud(this, noeud, noeud.LargeurFixe, 0));
            if (noeud.EstImage)
            {
                try
                {
                    Bitmap image = NoeudVisuel.DecoderImage(noeud.ImageDonnees);
                    double facteur = Math.Min(1.0, 320.0 / Math.Max(image.PixelSize.Width, (double)image.PixelSize.Height));
                    double largeur = Math.Max(20, image.PixelSize.Width * facteur);
                    double hauteur = Math.Max(20, image.PixelSize.Height * facteur);
                    if (largeur != noeud.ImageLargeur || hauteur != noeud.ImageHauteur)
                        commandes.Add(new CommandeRedimensionnerImage(this, noeud,
                            noeud.ImageLargeur, noeud.ImageHauteur, largeur, hauteur));
                }
                catch { }
            }
            if (commandes.Count == 0) return;
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));
        }

        public void RetirerImageDeBoite()
        {
            if (_selection == null || !_selection.Modele.EstImage) return;
            Noeud noeud = _selection.Modele;
            // Une boîte-image sans texte ni fichier redeviendrait vide : on la supprime plutôt.
            if (string.IsNullOrWhiteSpace(noeud.TextePlat) && noeud.Fichiers.Count == 0)
            {
                SupprimerSelection();
                return;
            }
            Historique.ExecuterEtPousser(new CommandeDefinirImage(this, noeud, null, null, 0, 0));
        }
    }
}
