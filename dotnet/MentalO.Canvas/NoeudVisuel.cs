using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le bilan d'une session d'édition, pour la commande d'annulation.</summary>
    public class ResultatEdition
    {
        public List<Segment> AnciensSegments;
        public List<Segment> AnciensSegmentsApres;
        public string AncienAlignement;
        public List<Segment> NouveauxSegments;
        public List<Segment> NouveauxSegmentsApres;
        public string NouvelAlignement;
    }

    /// <summary>La représentation à l'écran d'un nœud : fond (Border à ombre portée) et cadre
    /// en Rectangle (le cadre gère les pointillés), avec bascule affichage (un TextBlock à
    /// inlines stylés par ligne) / édition (TextBox + EditeurSegments) au double-clic.</summary>
    public class NoeudVisuel : Grid
    {
        private static readonly IBrush PinceauSelection = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
        private static readonly IBrush PinceauCible = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
        private static readonly IBrush PinceauEncre = new SolidColorBrush(Color.FromRgb(0x1C, 0x1B, 0x19));
        // Liens de fichiers : bleu des hyperliens, rouge sourd quand la cible a disparu.
        private static readonly IBrush PinceauLien = new SolidColorBrush(Color.FromRgb(0x0F, 0x6C, 0xC4));
        private static readonly IBrush PinceauLienManquant = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
        private static readonly IBrush PinceauLienSurvol = new SolidColorBrush(Color.FromArgb(0x22, 0x0F, 0x6C, 0xC4));

        public Noeud Modele { get; private set; }

        private readonly Border _fond;
        private readonly Rectangle _cadre;
        private readonly Grid _contenu;
        // Un TextBlock par ligne (alignement ligne à ligne) empilés dans un StackPanel.
        private readonly StackPanel _affichage;
        private readonly StackPanel _affichageApres;
        // Les fichiers attachés : un lien par ligne, sous tout le reste (visible même en édition).
        private readonly StackPanel _fichiers;
        // Étiquettes (v0.15) : rangée de puces colorées au-dessus du texte.
        private readonly WrapPanel _etiquettes;
        // Note cachée (v0.15) : petite icône en haut à droite, infobulle = la note.
        private readonly Control _iconeNote;
        private bool _estompe;
        private readonly Image _image;

        /// <summary>Fournit la couleur d'une étiquette par son nom (posé par la toile : les
        /// définitions vivent dans le document).</summary>
        public static Func<string, string> ResolveurCouleurEtiquette;
        private byte[] _imageDecodee; // dernières données décodées, pour ne pas re-décoder à chaque rafraîchissement
        private Bitmap _bitmap;

        // Édition : un TextBox pour le texte avant l'image, un autre pour le texte après.
        private readonly TextBox _editeur;
        private readonly TextBox _editeurApres;
        private EditeurSegments _segmentsEdition;
        private EditeurSegments _segmentsEditionApres;
        private bool _synchro;
        private bool _enEdition;
        private bool _selectionne;
        private bool _cibleDeLien;
        private List<Segment> _segmentsAvantEdition;
        private List<Segment> _segmentsApresAvantEdition;
        private string _alignementAvantEdition;

        /// <summary>Fabrique du menu contextuel de formatage, fournie par la toile.</summary>
        public Func<ContextMenu> FabriqueMenuContextuel { get; set; }

        /// <summary>La barre de formatage : y déplacer le focus ne clôt pas l'édition.</summary>
        public static Visual ZoneFormatage { get; set; }

        /// <summary>Vrai pendant un dialogue ou un menu de formatage : la perte de focus
        /// de l'éditeur ne clôt pas l'édition.</summary>
        public static bool ProtectionFocus { get; set; }

        /// <summary>Marge intérieure horizontale des boîtes (préférence du fichier).</summary>
        public static double PaddingTexte = 9;

        public event Action<NoeudVisuel, ResultatEdition> EditionTerminee;
        public event Action SelectionEditeurChangee;

        /// <summary>Clic (gauche ou droit) sur un lien de fichier : la toile ouvre le menu
        /// des actions (ouvrir, renommer, intégrer…) ancré sur l'élément fourni.</summary>
        public event Action<NoeudVisuel, PieceJointe, Control> FichierClique;

        /// <summary>Clic sur une case à cocher (v0.15) : (visuel, dans le texte après l'image ?, index de ligne).</summary>
        public event Action<NoeudVisuel, bool, int> CaseCliquee;

        /// <summary>Clic sur l'icône de note (v0.15) : la toile ouvre l'éditeur de note.</summary>
        public event Action<NoeudVisuel> NoteCliquee;

        public bool EnEdition { get { return _enEdition; } }

        /// <summary>L'éditeur qui a le focus (Couper/Copier/Coller du menu contextuel).</summary>
        public TextBox EditeurActif
        {
            get { return _editeurApres.IsVisible && _editeurApres.IsFocused ? _editeurApres : _editeur; }
        }

        public NoeudVisuel(Noeud modele)
        {
            Modele = modele;

            MinWidth = 30;
            MaxWidth = 340;
            Cursor = Ui.CurseurDeplacer;
            UseLayoutRounding = false;

            _fond = new Border();
            Children.Add(_fond);

            _contenu = new Grid();
            Children.Add(_contenu);

            _cadre = new Rectangle();
            _cadre.IsHitTestVisible = false;
            _cadre.StrokeLineCap = PenLineCap.Round;
            Children.Add(_cadre);

            _iconeNote = Icones.Creer("note", 12, new SolidColorBrush(Color.FromRgb(0x8C, 0x87, 0x7E)));
            _iconeNote.HorizontalAlignment = HorizontalAlignment.Right;
            _iconeNote.VerticalAlignment = VerticalAlignment.Top;
            _iconeNote.Margin = new Thickness(0, 4, 5, 0);
            _iconeNote.Cursor = Ui.CurseurMain;
            _iconeNote.IsVisible = false;
            ToolTip.SetShowDelay(_iconeNote, 300);
            _iconeNote.PointerPressed += delegate(object expediteur, PointerPressedEventArgs e)
            {
                if (!e.GetCurrentPoint(_iconeNote).Properties.IsLeftButtonPressed) return;
                e.Handled = true;
                Action<NoeudVisuel> h = NoteCliquee;
                if (h != null) h(this);
            };
            Children.Add(_iconeNote);

            _affichage = new StackPanel();
            _affichageApres = new StackPanel();
            _affichageApres.IsVisible = false;
            _fichiers = new StackPanel();
            _fichiers.IsVisible = false;
            _etiquettes = new WrapPanel();
            _etiquettes.IsVisible = false;

            _editeur = NouvelEditeur();
            _editeurApres = NouvelEditeur();

            _image = new Image();
            _image.Stretch = Stretch.Fill;
            _image.IsVisible = false;
            RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.HighQuality);

            // Cinq rangées : étiquettes, texte avant, image, texte après, fichiers attachés.
            // Les éditeurs recouvrent les rangées de texte ; étiquettes, image et liens
            // restent visibles en édition.
            for (int i = 0; i < 5; i++)
                _contenu.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(_etiquettes, 0);
            Grid.SetRow(_affichage, 1);
            Grid.SetRow(_image, 2);
            Grid.SetRow(_affichageApres, 3);
            Grid.SetRow(_fichiers, 4);
            Grid.SetRow(_editeur, 1);
            Grid.SetRow(_editeurApres, 3);
            _contenu.Children.Add(_etiquettes);
            _contenu.Children.Add(_affichage);
            _contenu.Children.Add(_image);
            _contenu.Children.Add(_affichageApres);
            _contenu.Children.Add(_fichiers);
            _contenu.Children.Add(_editeur);
            _contenu.Children.Add(_editeurApres);

            MettreAJour();
            MettreAJourStyle();
        }

        private TextBox NouvelEditeur()
        {
            TextBox editeur = new TextBox();
            editeur.FontFamily = TexteRiche.Police(Segment.PoliceDefaut);
            editeur.FontSize = 14;
            editeur.Foreground = PinceauEncre;
            editeur.MinWidth = 80;
            editeur.BorderThickness = new Thickness(0);
            editeur.Background = Brushes.Transparent;
            editeur.Padding = new Thickness(0);
            editeur.AcceptsReturn = true;
            editeur.AcceptsTab = false;
            editeur.TextWrapping = TextWrapping.Wrap;
            editeur.IsVisible = false;
            editeur.Cursor = Ui.CurseurTexte;
            editeur.AddHandler(KeyDownEvent, EditeurToucheEnfoncee, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            editeur.LostFocus += EditeurFocusPerdu;
            editeur.PropertyChanged += delegate(object expediteur, AvaloniaPropertyChangedEventArgs e)
            {
                if (!_enEdition || _synchro) return;
                if (e.Property == TextBox.TextProperty)
                {
                    EditeurSegments cible = ReferenceEquals(expediteur, _editeurApres) ? _segmentsEditionApres : _segmentsEdition;
                    if (cible != null) cible.Synchroniser(editeur.Text, editeur.CaretIndex);
                }
                if (e.Property == TextBox.TextProperty || e.Property == TextBox.SelectionStartProperty
                    || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.CaretIndexProperty)
                {
                    Action h = SelectionEditeurChangee;
                    if (h != null) h();
                }
            };
            return editeur;
        }

        public static Bitmap DecoderImage(byte[] donnees)
        {
            using (MemoryStream flux = new MemoryStream(donnees))
                return new Bitmap(flux);
        }

        /// <summary>Applique le style de boîte du modèle : fond, bordure, coins, ombre, opacité.</summary>
        public void MettreAJourStyle()
        {
            if (Modele.Ombre == "aucune")
                _fond.BoxShadow = default(BoxShadows);
            else
            {
                BoxShadow ombre = new BoxShadow();
                ombre.Color = Color.FromArgb((byte)(Modele.Ombre == "marquee" ? 0x61 : 0x2E), 0, 0, 0);
                ombre.Blur = Modele.Ombre == "marquee" ? 14 : 6;
                ombre.OffsetY = Modele.Ombre == "marquee" ? 3.5 : 1.5;
                _fond.BoxShadow = new BoxShadows(ombre);
            }
            // Opacité bornée : une boîte totalement invisible serait introuvable.
            // Estompée (filtre d'étiquettes ou mise en avant), elle s'efface à 15 %.
            double opacite = Math.Max(0.15, Math.Min(1.0, Modele.Opacite > 0 ? Modele.Opacite : 1));
            Opacity = _estompe ? opacite * 0.15 : opacite;

            _fond.Background = new SolidColorBrush(Palette.CouleurDepuis(Modele.FondCouleur, Colors.White));
            _fond.CornerRadius = new CornerRadius(Modele.Rayon);
            _cadre.RadiusX = Modele.Rayon;
            _cadre.RadiusY = Modele.Rayon;
            _cadre.StrokeDashArray = LienVisuel.TableauTirets(Modele.BordureTrait);
            // Une bordure épaisse ne doit pas mordre sur le texte.
            double marge = EpaisseurEffective() / 2;
            double ph = PaddingTexte;
            double pv = Math.Max(2, PaddingTexte * 2 / 3);
            _contenu.Margin = new Thickness(ph + marge, pv + marge, ph + marge, pv + marge);
            RafraichirBordure();
        }

        private double EpaisseurEffective()
        {
            return Modele.BordureTrait == "aucune" ? 0 : Modele.BordureEpaisseur;
        }

        /// <summary>Estompe (ou rétablit) la boîte : filtre par étiquettes, mise en avant.</summary>
        public void DefinirEstompe(bool estompe)
        {
            if (_estompe == estompe) return;
            _estompe = estompe;
            MettreAJourStyle();
        }

        public bool Estompe { get { return _estompe; } }

        public void MettreAJour()
        {
            bool image = Modele.EstImage;
            bool aTexte = Modele.Segments.Count > 0;

            // Largeur : fixée par l'utilisateur, sinon automatique (retour à la ligne à 340).
            if (Modele.LargeurFixe > 0)
            {
                Width = Modele.LargeurFixe;
                MaxWidth = double.PositiveInfinity;
                _affichage.MaxWidth = double.PositiveInfinity;
            }
            else
            {
                Width = double.NaN;
                MaxWidth = image ? double.PositiveInfinity : 340;
                _affichage.MaxWidth = 322;
            }

            string alignement = Modele.Alignement;

            if (image)
            {
                if (!ReferenceEquals(_imageDecodee, Modele.ImageDonnees))
                {
                    try
                    {
                        _bitmap = DecoderImage(Modele.ImageDonnees);
                        _image.Source = _bitmap;
                    }
                    catch { _image.Source = null; }
                    _imageDecodee = Modele.ImageDonnees;
                }
                _image.Width = Modele.ImageLargeur;
                _image.Height = Modele.ImageHauteur;
                _image.HorizontalAlignment = TexteRiche.AlignementHorizontal(alignement);
                _image.Margin = aTexte ? new Thickness(0, 6, 0, 0) : new Thickness(0);
                _image.IsVisible = true;
            }
            else
            {
                _image.IsVisible = false;
                _image.Source = null;
                _imageDecodee = null;
                _bitmap = null;
            }

            if (!_enEdition)
            {
                _affichage.IsVisible = aTexte || !image;
                _affichageApres.IsVisible = image && Modele.SegmentsApres.Count > 0;
            }
            _affichage.Children.Clear();
            foreach (TextBlock bloc in TexteRiche.ConstruireBlocsLignes(Modele.Segments, Modele.Alignement,
                delegate(int ligne) { SignalerCase(false, ligne); }))
                _affichage.Children.Add(bloc);
            _affichageApres.MaxWidth = _affichage.MaxWidth;
            _affichageApres.Margin = new Thickness(0, 4, 0, 0);
            _affichageApres.Children.Clear();
            foreach (TextBlock bloc in TexteRiche.ConstruireBlocsLignes(Modele.SegmentsApres, Modele.Alignement,
                delegate(int ligne) { SignalerCase(true, ligne); }))
                _affichageApres.Children.Add(bloc);

            // Étiquettes : puces colorées, alignées comme le texte.
            _etiquettes.Children.Clear();
            _etiquettes.MaxWidth = _affichage.MaxWidth;
            if (Modele.Etiquettes.Count > 0)
            {
                _etiquettes.HorizontalAlignment = TexteRiche.AlignementHorizontal(alignement);
                _etiquettes.Margin = new Thickness(0, 0, 0, aTexte || image || Modele.Fichiers.Count > 0 ? 3 : 0);
                foreach (string nom in Modele.Etiquettes)
                    _etiquettes.Children.Add(ConstruirePuceEtiquette(nom));
                _etiquettes.IsVisible = true;
            }
            else
                _etiquettes.IsVisible = false;

            // Note cachée : icône discrète, la note entière en infobulle.
            if (Modele.ANote)
            {
                Ui.Infobulle(_iconeNote, Modele.Note.Length > 600 ? Modele.Note.Substring(0, 600) + "…" : Modele.Note);
                _iconeNote.IsVisible = true;
            }
            else
                _iconeNote.IsVisible = false;

            // Fichiers attachés : un lien par ligne, aligné comme le texte.
            _fichiers.Children.Clear();
            _fichiers.MaxWidth = _affichage.MaxWidth;
            if (Modele.Fichiers.Count > 0)
            {
                bool contenuAvant = aTexte || image || Modele.SegmentsApres.Count > 0;
                _fichiers.Margin = new Thickness(0, contenuAvant ? 5 : 0, 0, 0);
                foreach (PieceJointe piece in Modele.Fichiers)
                    _fichiers.Children.Add(ConstruireLigneFichier(piece, alignement));
                _fichiers.IsVisible = true;
            }
            else
                _fichiers.IsVisible = false;
        }

        private void SignalerCase(bool apres, int ligne)
        {
            Action<NoeudVisuel, bool, int> h = CaseCliquee;
            if (h != null) h(this, apres, ligne);
        }

        /// <summary>Une puce d'étiquette : fond de la couleur de l'étiquette, nom en petit.</summary>
        private static Control ConstruirePuceEtiquette(string nom)
        {
            string hexa = ResolveurCouleurEtiquette != null ? ResolveurCouleurEtiquette(nom) : "#0078D7";
            Color couleur = Palette.CouleurDepuis(hexa, Color.FromRgb(0x00, 0x78, 0xD7));
            return new Border
            {
                Background = new SolidColorBrush(couleur),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 0, 6, 1),
                Margin = new Thickness(0, 0, 4, 2),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = nom,
                    FontSize = 9.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Palette.EstSombre(couleur) ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x2A, 0x28, 0x24))
                }
            };
        }

        /// <summary>Une ligne « icône lien + libellé souligné » pour un fichier attaché.
        /// Le clic, gauche comme droit, est absorbé ici (pas de déplacement de la boîte)
        /// et remonté à la toile par FichierClique.</summary>
        private Control ConstruireLigneFichier(PieceJointe piece, string alignement)
        {
            bool manquant = !piece.EstIntegre && !piece.OrigineDisponible;
            IBrush encre = manquant ? PinceauLienManquant : PinceauLien;
            double taille = Segment.TailleDefaut;

            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            Control icone = Icones.Creer("link", Math.Max(10, taille * 0.95), encre);
            icone.Margin = new Thickness(0, 1, 5, 0);
            icone.VerticalAlignment = VerticalAlignment.Center;
            ligne.Children.Add(icone);

            TextBlock libelle = new TextBlock
            {
                Text = string.IsNullOrEmpty(piece.Nom) ? (piece.NomFichier ?? "Fichier") : piece.Nom,
                Foreground = encre,
                FontFamily = TexteRiche.Police(Segment.PoliceDefaut),
                FontSize = taille,
                TextDecorations = TexteRiche.Decorations(true, false),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (manquant) libelle.FontStyle = FontStyle.Italic;
            ligne.Children.Add(libelle);

            Border cadre = new Border
            {
                Background = Brushes.Transparent, // un fond, même transparent, pour recevoir les clics
                Padding = new Thickness(3, 1, 5, 1),
                CornerRadius = new CornerRadius(3),
                Cursor = Ui.CurseurMain,
                Child = ligne,
                HorizontalAlignment = TexteRiche.AlignementHorizontal(alignement)
            };
            Ui.Infobulle(cadre, InfobulleFichier(piece, manquant));
            cadre.PointerEntered += delegate { cadre.Background = PinceauLienSurvol; };
            cadre.PointerExited += delegate { cadre.Background = Brushes.Transparent; };
            cadre.PointerPressed += delegate(object expediteur, PointerPressedEventArgs e)
            {
                e.Handled = true;
                Action<NoeudVisuel, PieceJointe, Control> h = FichierClique;
                if (h != null) h(this, piece, cadre);
            };
            return cadre;
        }

        private static string InfobulleFichier(PieceJointe piece, bool manquant)
        {
            string taille = piece.TailleLisible();
            if (piece.EstIntegre)
            {
                string texte = "Intégré au .tea" + (taille.Length > 0 ? " · " + taille : "");
                if (!string.IsNullOrEmpty(piece.Chemin)) texte += "\nOrigine : " + piece.Chemin;
                return texte;
            }
            if (manquant) return "Fichier introuvable :\n" + (piece.Chemin ?? piece.NomFichier ?? "");
            return piece.Chemin + (taille.Length > 0 ? "\n" + taille : "");
        }

        public void DefinirSelection(bool selectionne)
        {
            _selectionne = selectionne;
            RafraichirBordure();
        }

        /// <summary>Surbrillance verte quand un lien en cours de création survole cette boîte.</summary>
        public void DefinirCibleDeLien(bool cible)
        {
            _cibleDeLien = cible;
            RafraichirBordure();
        }

        private void RafraichirBordure()
        {
            double epaisseur = EpaisseurEffective();
            if (_cibleDeLien)
            {
                _cadre.Stroke = PinceauCible;
                _cadre.StrokeThickness = Math.Max(epaisseur, 2);
            }
            else if (_selectionne)
            {
                _cadre.Stroke = PinceauSelection;
                _cadre.StrokeThickness = Math.Max(epaisseur, 1.5);
            }
            else if (epaisseur > 0)
            {
                _cadre.Stroke = new SolidColorBrush(
                    Palette.CouleurDepuis(Modele.BordureCouleur, Color.FromRgb(0xB8, 0xB2, 0xA7)));
                _cadre.StrokeThickness = epaisseur;
            }
            else
            {
                _cadre.Stroke = null;
                _cadre.StrokeThickness = 0;
            }
        }

        // ------------------------------------------------------------------ édition

        public void CommencerEdition()
        {
            if (_enEdition) return;
            _enEdition = true;
            _segmentsAvantEdition = Noeud.CopieDe(Modele.Segments);
            _segmentsApresAvantEdition = Noeud.CopieDe(Modele.SegmentsApres);
            _alignementAvantEdition = Modele.Alignement;
            _segmentsEdition = new EditeurSegments(Modele.Segments);
            _segmentsEditionApres = new EditeurSegments(Modele.SegmentsApres);

            PreparerEditeur(_editeur, _segmentsEdition, Modele.Segments);
            // Sans largeur imposée, le TextBox s'étire au maximum : une boîte neuve
            // serait immense. On cale le retour à la ligne vers ~30 caractères, sauf si la
            // boîte a déjà une largeur (fixée ou dictée par une image).
            double largeur = _contenu.Bounds.Width;
            if (largeur < 20) largeur = 220;
            if (!Modele.EstImage && Modele.LargeurFixe <= 0)
                largeur = Math.Min(largeur + 6, 320);
            _editeur.Width = largeur;
            _editeurApres.Width = largeur;
            if (FabriqueMenuContextuel != null)
            {
                _editeur.ContextMenu = FabriqueMenuContextuel();
                _editeurApres.ContextMenu = _editeur.ContextMenu;
            }
            _affichage.IsVisible = false;
            _affichageApres.IsVisible = false;
            _editeur.IsVisible = true;
            if (Modele.EstImage)
            {
                PreparerEditeur(_editeurApres, _segmentsEditionApres, Modele.SegmentsApres);
                _editeurApres.Margin = new Thickness(0, 4, 0, 0);
                _editeurApres.IsVisible = true;
            }
            Dispatcher.UIThread.Post(delegate
            {
                if (!_enEdition) return;
                _editeur.Focus();
                if (!Modele.EstImage) _editeur.SelectAll();
                else _editeur.CaretIndex = (_editeur.Text ?? "").Length;
            }, DispatcherPriority.Input);
        }

        private void PreparerEditeur(TextBox editeur, EditeurSegments segments, List<Segment> source)
        {
            // L'éditeur est plat : il prend la police et la couleur du premier segment
            // (ou les défauts du fichier), la mise en forme revient à la validation.
            Segment premier = source.Count > 0 ? source[0] : null;
            editeur.FontFamily = TexteRiche.Police(premier != null ? premier.Police : Segment.PoliceDefaut);
            editeur.FontSize = premier != null && premier.Taille > 0 ? premier.Taille : Segment.TailleDefaut;
            editeur.Foreground = new SolidColorBrush(Palette.CouleurDepuis(premier != null ? premier.Couleur : Segment.CouleurDefaut, Colors.Black));
            editeur.TextAlignment = TexteRiche.ConvertirAlignement(Modele.Alignement);
            _synchro = true;
            editeur.Text = segments.Texte;
            _synchro = false;
        }

        public void TerminerEdition(bool valider)
        {
            if (!_enEdition) return;
            _enEdition = false;

            List<Segment> nouveaux, nouveauxApres;
            if (valider)
            {
                nouveaux = TexteRiche.Normaliser(_segmentsEdition.Segments);
                nouveauxApres = Modele.EstImage ? TexteRiche.Normaliser(_segmentsEditionApres.Segments) : new List<Segment>();
                // Une zone qui ne contient que des blancs (la ligne née du double-clic) est purgée.
                if (Modele.EstImage && TexteRiche.SegmentsBlancs(nouveaux)) nouveaux = new List<Segment>();
                if (TexteRiche.SegmentsBlancs(nouveauxApres)) nouveauxApres = new List<Segment>();
            }
            else
            {
                nouveaux = _segmentsAvantEdition;
                nouveauxApres = _segmentsApresAvantEdition;
            }
            string alignement = valider ? Modele.Alignement : _alignementAvantEdition;
            Modele.Segments = nouveaux;
            Modele.SegmentsApres = nouveauxApres;
            Modele.Alignement = alignement;
            _editeur.IsVisible = false;
            _editeurApres.IsVisible = false;
            _editeur.ContextMenu = null;
            _editeurApres.ContextMenu = null;
            MettreAJour();

            ResultatEdition resultat = new ResultatEdition();
            resultat.AnciensSegments = _segmentsAvantEdition;
            resultat.AnciensSegmentsApres = _segmentsApresAvantEdition;
            resultat.AncienAlignement = _alignementAvantEdition;
            resultat.NouveauxSegments = nouveaux;
            resultat.NouveauxSegmentsApres = nouveauxApres;
            resultat.NouvelAlignement = alignement;
            Action<NoeudVisuel, ResultatEdition> h = EditionTerminee;
            if (h != null) h(this, resultat);
        }

        /// <summary>Les suggestions pour le mot sous le curseur ; sans correcteur porté,
        /// « mot » reste null (le correcteur de Windows n'existe pas sous Avalonia).</summary>
        public List<string> SuggestionsOrthographe(out string mot)
        {
            mot = null;
            return new List<string>();
        }

        private void EditeurToucheEnfoncee(object expediteur, KeyEventArgs e)
        {
            TextBox editeur = expediteur as TextBox;
            // Entrée insère un retour à la ligne, Maj+Entrée valide, Échap abandonne.
            if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Shift) != 0)
            {
                TerminerEdition(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TerminerEdition(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Space && Mentalo.Parametres.ParametresGlobaux.ListesAutomatiques && editeur != null)
            {
                // Option (désactivée par défaut) : « * » ou « - » en début de ligne + espace → puce.
                EditeurSegments cible = ReferenceEquals(editeur, _editeurApres) ? _segmentsEditionApres : _segmentsEdition;
                int debut, fin;
                cible.BornesLigne(editeur.CaretIndex, out debut, out fin);
                string texte = editeur.Text ?? "";
                string ligne = texte.Substring(debut, Math.Max(0, Math.Min(fin, texte.Length) - debut));
                if (ligne != "*" && ligne != "-") return;
                string marqueur = ligne == "*" ? PucesTexte.Puce : PucesTexte.Tiret;
                cible.Remplacer(debut, ligne.Length, marqueur);
                RemettreTexte(editeur, cible, debut + marqueur.Length);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Tab appartient à la toile (boîte enfant) : on valide d'abord.
                TerminerEdition(true);
            }
        }

        private void RemettreTexte(TextBox editeur, EditeurSegments segments, int caret)
        {
            _synchro = true;
            editeur.Text = segments.Texte;
            _synchro = false;
            editeur.CaretIndex = Math.Max(0, Math.Min((editeur.Text ?? "").Length, caret));
        }

        private void EditeurFocusPerdu(object expediteur, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!_enEdition) return;
            if (ProtectionFocus) return; // un dialogue ou un menu de formatage est ouvert
            // Le focus passe d'un éditeur à l'autre (texte avant / après l'image) : on continue.
            Dispatcher.UIThread.Post(delegate
            {
                if (!_enEdition || ProtectionFocus) return;
                if (_editeur.IsFocused || _editeurApres.IsFocused) return;
                if (FocusProtege()) return;
                TerminerEdition(true);
            }, DispatcherPriority.Input);
        }

        public void RedonnerFocus()
        {
            if (_enEdition) _editeur.Focus();
        }

        private bool FocusProtege()
        {
            TopLevel sommet = TopLevel.GetTopLevel(this);
            if (sommet == null) return false;
            IInputElement focus = sommet.FocusManager == null ? null : sommet.FocusManager.GetFocusedElement();
            Visual visuel = focus as Visual;
            while (visuel != null)
            {
                if (ReferenceEquals(visuel, ZoneFormatage)) return true;
                if (visuel is MenuItem || visuel is ContextMenu || visuel is Avalonia.Controls.Primitives.Popup) return true;
                visuel = visuel.GetVisualParent();
            }
            return false;
        }

        // ------------------------------------------------------------------ formatage de la sélection (pendant l'édition)

        public void AppliquerFormatSelection(string propriete, object valeur)
        {
            if (!_enEdition) return;
            TextBox editeur = EditeurActif;
            EditeurSegments cible = ReferenceEquals(editeur, _editeurApres) ? _segmentsEditionApres : _segmentsEdition;
            int debut = Math.Min(editeur.SelectionStart, editeur.SelectionEnd);
            int fin = Math.Max(editeur.SelectionStart, editeur.SelectionEnd);
            if (propriete == "alignement" && debut == fin)
            {
                // Sans sélection : la ligne du curseur.
                cible.AppliquerFormat(propriete, valeur, editeur.CaretIndex, editeur.CaretIndex);
            }
            else if (debut == fin)
            {
                // Sans sélection, une bascule s'applique au mot sous le curseur.
                string texte = editeur.Text ?? "";
                int d = editeur.CaretIndex, f = editeur.CaretIndex;
                while (d > 0 && !char.IsWhiteSpace(texte[d - 1])) d--;
                while (f < texte.Length && !char.IsWhiteSpace(texte[f])) f++;
                cible.AppliquerFormat(propriete, valeur, d, f);
            }
            else
                cible.AppliquerFormat(propriete, valeur, debut, fin);
            if (propriete == "police") editeur.FontFamily = TexteRiche.Police((string)valeur);
            else if (propriete == "taille") editeur.FontSize = (double)valeur;
            else if (propriete == "couleur") editeur.Foreground = new SolidColorBrush(Palette.CouleurDepuis((string)valeur, Colors.Black));
            editeur.Focus();
            Action h = SelectionEditeurChangee;
            if (h != null) h();
        }

        /// <summary>Bascule les puces sur les seules lignes sélectionnées de l'éditeur.</summary>
        public void BasculerPucesSelection(string type)
        {
            if (!_enEdition) return;
            TextBox editeur = EditeurActif;
            EditeurSegments cible = ReferenceEquals(editeur, _editeurApres) ? _segmentsEditionApres : _segmentsEdition;
            int debut = Math.Min(editeur.SelectionStart, editeur.SelectionEnd);
            int fin = Math.Max(editeur.SelectionStart, editeur.SelectionEnd);
            int caret = editeur.CaretIndex;
            cible.BasculerPuces(type, debut, fin);
            RemettreTexte(editeur, cible, caret);
            editeur.Focus();
        }

        public FormatCourant LireFormatSelection()
        {
            TextBox editeur = EditeurActif;
            EditeurSegments cible = ReferenceEquals(editeur, _editeurApres) ? _segmentsEditionApres : _segmentsEdition;
            FormatCourant etat = cible.Lire(editeur.CaretIndex, editeur.SelectionStart, editeur.SelectionEnd, Modele.Alignement);
            RemplirStyleBoite(etat);
            return etat;
        }

        public void RemplirStyleBoite(FormatCourant etat)
        {
            etat.FondBoite = Modele.FondCouleur;
            etat.BordureCouleur = Modele.BordureCouleur;
            etat.BordureEpaisseur = Modele.BordureEpaisseur;
            etat.BordureTrait = Modele.BordureTrait;
            etat.Rayon = Modele.Rayon;
            etat.Ombre = Modele.Ombre;
            etat.Opacite = Modele.Opacite;
        }
    }
}
