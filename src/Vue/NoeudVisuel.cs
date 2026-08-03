using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
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

    /// <summary>La représentation à l'écran d'un nœud : fond et cadre en Rectangle (le cadre
    /// gère les pointillés, ce que Border ne sait pas faire), avec bascule affichage
    /// (TextBlock à inlines stylés) / édition (RichTextBox) au double-clic.</summary>
    public class NoeudVisuel : Grid
    {
        private static readonly Brush PinceauSelection = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
        private static readonly Brush PinceauCible = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
        private static readonly Brush PinceauEncre = new SolidColorBrush(Color.FromRgb(0x1C, 0x1B, 0x19));

        public Noeud Modele { get; private set; }

        private readonly Rectangle _fond;
        private readonly Rectangle _cadre;
        private readonly Grid _contenu;
        // Un TextBlock par ligne (alignement ligne à ligne) empilés dans un StackPanel.
        private readonly StackPanel _affichage;
        private readonly StackPanel _affichageApres;
        private readonly Image _image;
        private byte[] _imageDecodee; // dernières données décodées, pour ne pas re-décoder à chaque rafraîchissement
        private readonly RichTextBox _editeur;
        private bool _enEdition;
        private bool _selectionne;
        private bool _cibleDeLien;
        private List<Segment> _segmentsAvantEdition;
        private List<Segment> _segmentsApresAvantEdition;
        private string _alignementAvantEdition;

        /// <summary>Fabrique du menu contextuel de formatage, fournie par la toile.</summary>
        public Func<ContextMenu> FabriqueMenuContextuel { get; set; }

        /// <summary>La barre de formatage : y déplacer le focus ne clôt pas l'édition.</summary>
        public static FrameworkElement ZoneFormatage { get; set; }

        /// <summary>Vrai pendant un dialogue de formatage (couleur, taille…) :
        /// la perte de focus de l'éditeur ne clôt pas l'édition.</summary>
        public static bool ProtectionFocus { get; set; }

        /// <summary>Marge intérieure horizontale des boîtes (préférence du fichier).</summary>
        public static double PaddingTexte = 9;

        public event Action<NoeudVisuel, ResultatEdition> EditionTerminee;
        public event Action SelectionEditeurChangee;

        public bool EnEdition { get { return _enEdition; } }

        /// <summary>Cible des commandes Couper/Copier/Coller du menu contextuel d'édition.</summary>
        public IInputElement CibleCommandes { get { return _editeur; } }

        public NoeudVisuel(Noeud modele)
        {
            Modele = modele;

            MinWidth = 30;
            MaxWidth = 340;
            Cursor = Cursors.SizeAll;
            // Pas d'accrochage aux pixels : sous zoom, il rendait les bordures irrégulières.
            SnapsToDevicePixels = false;
            UseLayoutRounding = false;

            _fond = new Rectangle();
            Children.Add(_fond);

            _contenu = new Grid();
            Children.Add(_contenu);

            _cadre = new Rectangle();
            _cadre.IsHitTestVisible = false;
            _cadre.StrokeDashCap = PenLineCap.Round;
            Children.Add(_cadre);

            _affichage = new StackPanel();
            _affichageApres = new StackPanel();
            _affichageApres.Visibility = Visibility.Collapsed;

            _editeur = new RichTextBox();
            _editeur.FontFamily = new FontFamily("Segoe UI");
            _editeur.FontSize = 14;
            _editeur.Foreground = PinceauEncre;
            _editeur.MinWidth = 80;
            _editeur.BorderThickness = new Thickness(0);
            _editeur.Background = Brushes.Transparent;
            _editeur.Visibility = Visibility.Collapsed;
            _editeur.PreviewKeyDown += EditeurToucheEnfoncee;
            _editeur.LostFocus += EditeurFocusPerdu;
            _editeur.SelectionChanged += delegate
            {
                if (!_enEdition) return;
                Action h = SelectionEditeurChangee;
                if (h != null) h();
            };

            _image = new Image();
            _image.Stretch = Stretch.Fill;
            _image.Visibility = Visibility.Collapsed;
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

            // Trois rangées : texte avant, image, texte après. L'éditeur unique recouvre tout.
            _contenu.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _contenu.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _contenu.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_affichage, 0);
            Grid.SetRow(_image, 1);
            Grid.SetRow(_affichageApres, 2);
            Grid.SetRow(_editeur, 0);
            Grid.SetRowSpan(_editeur, 3);
            _contenu.Children.Add(_affichage);
            _contenu.Children.Add(_image);
            _contenu.Children.Add(_affichageApres);
            _contenu.Children.Add(_editeur);

            MettreAJour();
            MettreAJourStyle();
        }

        public static BitmapImage DecoderImage(byte[] donnees)
        {
            BitmapImage image = new BitmapImage();
            using (MemoryStream flux = new MemoryStream(donnees))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = flux;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        /// <summary>Applique le style de boîte du modèle : fond, bordure, coins, ombre, opacité.</summary>
        public void MettreAJourStyle()
        {
            if (Modele.Ombre == "aucune")
                Effect = null;
            else
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = Modele.Ombre == "marquee" ? 0.38 : 0.18,
                    BlurRadius = Modele.Ombre == "marquee" ? 14 : 6,
                    ShadowDepth = Modele.Ombre == "marquee" ? 3.5 : 1.5,
                    Direction = 270
                };
            // Opacité bornée : une boîte totalement invisible serait introuvable.
            Opacity = Math.Max(0.15, Math.Min(1.0, Modele.Opacite > 0 ? Modele.Opacite : 1));

            _fond.Fill = new SolidColorBrush(TexteRiche.CouleurDepuis(Modele.FondCouleur, Colors.White));
            _fond.RadiusX = Modele.Rayon;
            _fond.RadiusY = Modele.Rayon;
            _cadre.RadiusX = Modele.Rayon;
            _cadre.RadiusY = Modele.Rayon;
            _cadre.StrokeDashArray = TiretsBordure(Modele.BordureTrait);
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

        private static DoubleCollection TiretsBordure(string trait)
        {
            // Les valeurs sont en multiples de l'épaisseur du trait.
            if (trait == "tirets") return new DoubleCollection(new double[] { 4, 2.5 });
            if (trait == "points") return new DoubleCollection(new double[] { 0.1, 2.5 });
            return null;
        }

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

            TextAlignment alignement = TexteRiche.ConvertirAlignement(Modele.Alignement);

            if (image)
            {
                if (!ReferenceEquals(_imageDecodee, Modele.ImageDonnees))
                {
                    _image.Source = DecoderImage(Modele.ImageDonnees);
                    _imageDecodee = Modele.ImageDonnees;
                }
                _image.Width = Modele.ImageLargeur;
                _image.Height = Modele.ImageHauteur;
                _image.HorizontalAlignment = alignement == TextAlignment.Center ? HorizontalAlignment.Center
                    : alignement == TextAlignment.Right ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
                _image.Margin = aTexte ? new Thickness(0, 6, 0, 0) : new Thickness(0);
                _image.Visibility = Visibility.Visible;
            }
            else
            {
                _image.Visibility = Visibility.Collapsed;
                _image.Source = null;
                _imageDecodee = null;
            }

            if (!_enEdition)
            {
                _affichage.Visibility = (aTexte || !image) ? Visibility.Visible : Visibility.Collapsed;
                _affichageApres.Visibility = (image && Modele.SegmentsApres.Count > 0)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            _affichage.Children.Clear();
            foreach (TextBlock bloc in TexteRiche.ConstruireBlocsLignes(Modele.Segments, Modele.Alignement))
                _affichage.Children.Add(bloc);
            _affichageApres.MaxWidth = _affichage.MaxWidth;
            _affichageApres.Margin = new Thickness(0, 4, 0, 0);
            _affichageApres.Children.Clear();
            foreach (TextBlock bloc in TexteRiche.ConstruireBlocsLignes(Modele.SegmentsApres, Modele.Alignement))
                _affichageApres.Children.Add(bloc);
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
                    TexteRiche.CouleurDepuis(Modele.BordureCouleur, Color.FromRgb(0xB8, 0xB2, 0xA7)));
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
            // Le texte tapé dans un endroit vierge hérite des valeurs par défaut du fichier.
            _editeur.FontFamily = new FontFamily(Segment.PoliceDefaut);
            _editeur.FontSize = Segment.TailleDefaut;
            _editeur.Foreground = new SolidColorBrush(TexteRiche.CouleurDepuis(Segment.CouleurDefaut, Colors.Black));
            _segmentsAvantEdition = Noeud.CopieDe(Modele.Segments);
            _segmentsApresAvantEdition = Noeud.CopieDe(Modele.SegmentsApres);
            _alignementAvantEdition = Modele.Alignement;
            _editeur.Document = TexteRiche.ConstruireDocumentComplet(Modele, _image.Source);
            // Sans largeur imposée, le RichTextBox s'étire au maximum : une boîte neuve
            // serait immense. On cale le retour à la ligne vers ~30 caractères, sauf si la
            // boîte a déjà une largeur (fixée ou dictée par une image).
            double largeur = _contenu.ActualWidth;
            if (largeur < 20) largeur = 220;
            if (!Modele.EstImage && Modele.LargeurFixe <= 0)
                largeur = Math.Min(largeur + 6, 320);
            _editeur.Width = largeur;
            if (FabriqueMenuContextuel != null)
                _editeur.ContextMenu = FabriqueMenuContextuel();
            _affichage.Visibility = Visibility.Collapsed;
            _affichageApres.Visibility = Visibility.Collapsed;
            _image.Visibility = Visibility.Collapsed; // l'image vit dans l'éditeur pendant l'édition
            _editeur.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                _editeur.Focus();
                if (!Modele.EstImage) _editeur.SelectAll();
                else _editeur.CaretPosition = _editeur.Document.ContentEnd;
            }), DispatcherPriority.Input);
        }

        public void TerminerEdition(bool valider)
        {
            if (!_enEdition) return;
            _enEdition = false;

            List<Segment> nouveaux, nouveauxApres;
            if (valider)
            {
                TexteRiche.ExtraireSegmentsComplet(_editeur.Document, out nouveaux, out nouveauxApres);
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
            _editeur.Visibility = Visibility.Collapsed;
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

        private void EditeurToucheEnfoncee(object expediteur, KeyEventArgs e)
        {
            // Entrée insère un retour à la ligne, Maj+Entrée valide, Échap abandonne.
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                TerminerEdition(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TerminerEdition(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Space && Mentalo.Parametres.ParametresGlobaux.ListesAutomatiques)
            {
                // Option (désactivée par défaut) : « * » ou « - » en début de ligne + espace → puce.
                Paragraph paragraphe = _editeur.CaretPosition == null ? null : _editeur.CaretPosition.Paragraph;
                if (paragraphe == null) return;
                string texte = new TextRange(paragraphe.ContentStart, paragraphe.ContentEnd).Text;
                if (texte != "*" && texte != "-") return;
                string marqueur = texte == "*" ? PucesTexte.Puce : PucesTexte.Tiret;
                paragraphe.Inlines.Clear();
                paragraphe.Inlines.Add(new Run(marqueur));
                _editeur.CaretPosition = paragraphe.ContentEnd;
                e.Handled = true;
            }
        }

        private void EditeurFocusPerdu(object expediteur, RoutedEventArgs e)
        {
            if (!_enEdition) return;
            if (ProtectionFocus) return; // un dialogue de formatage est ouvert
            // Le focus part vers la barre de formatage ou un menu : l'édition continue.
            if (FocusProtege(Keyboard.FocusedElement as DependencyObject)) return;
            TerminerEdition(true);
        }

        public void RedonnerFocus()
        {
            if (_enEdition) _editeur.Focus();
        }

        private static bool FocusProtege(DependencyObject element)
        {
            while (element != null)
            {
                if (ReferenceEquals(element, ZoneFormatage)) return true;
                if (element is MenuBase) return true;
                DependencyObject parent;
                if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
                    parent = VisualTreeHelper.GetParent(element);
                else
                    parent = LogicalTreeHelper.GetParent(element);
                element = parent;
            }
            return false;
        }

        // ------------------------------------------------------------------ formatage de la sélection (pendant l'édition)

        public void AppliquerFormatSelection(string propriete, object valeur)
        {
            if (!_enEdition) return;
            TextSelection selection = _editeur.Selection;
            switch (propriete)
            {
                case "police":
                    selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily((string)valeur));
                    break;
                case "taille":
                    selection.ApplyPropertyValue(TextElement.FontSizeProperty, (double)valeur);
                    break;
                case "couleur":
                    selection.ApplyPropertyValue(TextElement.ForegroundProperty,
                        new SolidColorBrush(TexteRiche.CouleurDepuis((string)valeur, Colors.Black)));
                    break;
                case "surlignage":
                    Brush fond = valeur == null
                        ? Brushes.Transparent
                        : new SolidColorBrush(TexteRiche.CouleurDepuis((string)valeur, Colors.Transparent));
                    selection.ApplyPropertyValue(TextElement.BackgroundProperty, fond);
                    break;
                case "gras":
                    EditingCommands.ToggleBold.Execute(null, _editeur);
                    break;
                case "italique":
                    EditingCommands.ToggleItalic.Execute(null, _editeur);
                    break;
                case "souligne":
                    EditingCommands.ToggleUnderline.Execute(null, _editeur);
                    break;
                case "barre":
                    BasculerBarreSelection(selection);
                    break;
                case "alignement":
                    // Seulement les paragraphes couverts par la sélection.
                    TextAlignment alignement = TexteRiche.ConvertirAlignement((string)valeur);
                    foreach (Paragraph paragraphe in ParagraphesSelectionnes())
                        paragraphe.TextAlignment = alignement;
                    break;
            }
            _editeur.Focus();
        }

        private static void BasculerBarreSelection(TextSelection selection)
        {
            object valeur = selection.GetPropertyValue(Inline.TextDecorationsProperty);
            TextDecorationCollection actuelles = valeur as TextDecorationCollection;
            bool barre = false, souligne = false;
            if (actuelles != null)
            {
                foreach (TextDecoration decoration in actuelles)
                {
                    if (decoration.Location == TextDecorationLocation.Strikethrough) barre = true;
                    if (decoration.Location == TextDecorationLocation.Underline) souligne = true;
                }
            }
            TextDecorationCollection nouvelles = new TextDecorationCollection();
            if (souligne) nouvelles.Add(TextDecorations.Underline[0]);
            if (!barre) nouvelles.Add(TextDecorations.Strikethrough[0]);
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, nouvelles);
        }

        /// <summary>Les paragraphes du document d'édition couverts par la sélection.</summary>
        private List<Paragraph> ParagraphesSelectionnes()
        {
            List<Paragraph> paragraphes = new List<Paragraph>();
            TextSelection selection = _editeur.Selection;
            foreach (Block bloc in _editeur.Document.Blocks)
            {
                Paragraph paragraphe = bloc as Paragraph;
                if (paragraphe == null) continue;
                if (paragraphe.ContentEnd.CompareTo(selection.Start) >= 0
                    && paragraphe.ContentStart.CompareTo(selection.End) <= 0)
                    paragraphes.Add(paragraphe);
            }
            return paragraphes;
        }

        /// <summary>Bascule les puces sur les seuls paragraphes sélectionnés de l'éditeur.</summary>
        public void BasculerPucesSelection(string type)
        {
            if (!_enEdition) return;
            List<Paragraph> paragraphes = ParagraphesSelectionnes();
            if (paragraphes.Count == 0) return;

            bool tousMarques = true;
            foreach (Paragraph paragraphe in paragraphes)
            {
                string texte = new TextRange(paragraphe.ContentStart, paragraphe.ContentEnd).Text;
                if (texte.Trim().Length == 0) continue;
                if (!PucesTexte.PorteMarqueurPublic(texte, type)) { tousMarques = false; break; }
            }

            int numero = 1;
            foreach (Paragraph paragraphe in paragraphes)
            {
                string texte = new TextRange(paragraphe.ContentStart, paragraphe.ContentEnd).Text;
                int longueur = PucesTexte.LongueurMarqueur(texte);
                if (longueur > 0)
                {
                    TextPointer fin = AvancerCaracteres(paragraphe.ContentStart, longueur);
                    if (fin != null) new TextRange(paragraphe.ContentStart, fin).Text = "";
                }
                if (tousMarques) continue;
                if (new TextRange(paragraphe.ContentStart, paragraphe.ContentEnd).Text.Trim().Length == 0
                    && paragraphes.Count > 1)
                    continue;
                string marqueur = type == "tiret" ? PucesTexte.Tiret
                    : type == "numero" ? numero++ + ". "
                    : PucesTexte.Puce;
                Run runMarqueur = new Run(marqueur);
                Run premier = paragraphe.Inlines.FirstInline as Run;
                if (premier != null) paragraphe.Inlines.InsertBefore(premier, runMarqueur);
                else if (paragraphe.Inlines.FirstInline != null)
                    paragraphe.Inlines.InsertBefore(paragraphe.Inlines.FirstInline, runMarqueur);
                else paragraphe.Inlines.Add(runMarqueur);
            }
            _editeur.Focus();
        }

        private static TextPointer AvancerCaracteres(TextPointer depart, int nombre)
        {
            TextPointer position = depart;
            while (nombre > 0 && position != null)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string run = position.GetTextInRun(LogicalDirection.Forward);
                    int prendre = Math.Min(nombre, run.Length);
                    position = position.GetPositionAtOffset(prendre);
                    nombre -= prendre;
                }
                else
                {
                    TextPointer suivante = position.GetNextContextPosition(LogicalDirection.Forward);
                    if (suivante == null) return position;
                    position = suivante;
                }
            }
            return position;
        }

        public FormatCourant LireFormatSelection()
        {
            FormatCourant etat = new FormatCourant();
            etat.Actif = true;
            TextSelection selection = _editeur.Selection;
            Paragraph paragrapheCaret = selection.Start.Paragraph;
            etat.Alignement = paragrapheCaret != null
                ? TexteRiche.ConvertirAlignementInverse(paragrapheCaret.TextAlignment)
                : Modele.Alignement;

            object valeur = selection.GetPropertyValue(TextElement.FontFamilyProperty);
            if (valeur is FontFamily) etat.Police = ((FontFamily)valeur).Source;
            valeur = selection.GetPropertyValue(TextElement.FontSizeProperty);
            if (valeur is double) etat.Taille = (double)valeur;
            valeur = selection.GetPropertyValue(TextElement.FontWeightProperty);
            if (valeur is FontWeight) etat.Gras = ((FontWeight)valeur).ToOpenTypeWeight() >= 600;
            valeur = selection.GetPropertyValue(TextElement.FontStyleProperty);
            if (valeur is FontStyle)
                etat.Italique = (FontStyle)valeur == FontStyles.Italic || (FontStyle)valeur == FontStyles.Oblique;
            valeur = selection.GetPropertyValue(Inline.TextDecorationsProperty);
            TextDecorationCollection decorations = valeur as TextDecorationCollection;
            if (decorations != null)
            {
                foreach (TextDecoration decoration in decorations)
                {
                    if (decoration.Location == TextDecorationLocation.Underline) etat.Souligne = true;
                    if (decoration.Location == TextDecorationLocation.Strikethrough) etat.Barre = true;
                }
            }
            valeur = selection.GetPropertyValue(TextElement.ForegroundProperty);
            SolidColorBrush encre = valeur as SolidColorBrush;
            if (encre != null) etat.Couleur = TexteRiche.HexaDepuisCouleur(encre.Color);
            valeur = selection.GetPropertyValue(TextElement.BackgroundProperty);
            SolidColorBrush fond = valeur as SolidColorBrush;
            if (fond != null && fond.Color.A > 0) etat.Surlignage = TexteRiche.HexaDepuisCouleur(fond.Color);
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
