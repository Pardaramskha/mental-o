using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le canevas principal : rendu des nœuds et des liens, navigation (pan/zoom)
    /// et interactions pointeur, dont la poignée de création de liens. Portage Avalonia
    /// (26/10/2026) de la toile WPF ; même code pour Mental-O seul et pour le DLC Marabook.</summary>
    public partial class ToileVue : Border, IToile
    {
        private const double ZoomMin = 0.2;
        private const double ZoomMax = 4.0;
        private const double SeuilDeplacement = 3.0; // pixels canevas avant qu'un clic devienne un glisser
        private const double TaillePoignee = 11.0;        // au repos (zone de grâce)
        private const double TaillePoigneeAccent = 15.0;  // boîte survolée : poignées épaissies
        private const double TaillePoigneeSaisie = 18.0;  // curseur sur la poignée elle-même
        private const double MargeGracePoignees = 28.0;   // marge autour de la boîte où les poignées subsistent
        private const int ZPremierPlan = 1000000;    // poignées et lien temporaire, toujours au-dessus

        private readonly Canvas _canvas;
        private readonly ScaleTransform _echelle;
        private readonly TranslateTransform _translation;
        private readonly Dictionary<Noeud, NoeudVisuel> _visuels = new Dictionary<Noeud, NoeudVisuel>();
        private readonly Dictionary<Lien, LienVisuel> _visuelsLiens = new Dictionary<Lien, LienVisuel>();

        public DocumentCarte Document { get; private set; }
        public GestionnaireHistorique Historique { get; private set; }

        private NoeudVisuel _selection;            // boîte « principale » (cible du format et du grip)
        private readonly List<NoeudVisuel> _selectionnes = new List<NoeudVisuel>(); // inclut _selection
        private LienVisuel _selectionLien;

        // Déplacement d'une ou plusieurs boîtes
        private bool _dragActif;
        private bool _deplacementEffectif;
        private Point _dragOrigine;                // souris (canevas) au départ du geste
        private readonly List<Noeud> _dragNoeuds = new List<Noeud>();
        private readonly Dictionary<string, Point> _dragDeparts = new Dictionary<string, Point>();

        // Rectangle de sélection
        private bool _rectangleActif;
        private Point _rectangleDepart;
        private readonly Rectangle _rectangleSelection;

        // Guides de magnétisme
        private readonly Line _guideVertical;
        private readonly Line _guideHorizontal;

        // Groupes
        private const double MargeGroupe = 14;
        private readonly Dictionary<Groupe, Rectangle> _visuelsGroupes = new Dictionary<Groupe, Rectangle>();
        private readonly Dictionary<Groupe, TextBlock> _titresGroupes = new Dictionary<Groupe, TextBlock>();
        private Groupe _groupeActif;          // groupe entièrement sélectionné (grip sur son cadre)
        private Groupe _groupeRedimensionne;
        private double _departGrpL, _departGrpH, _origineCadreX, _origineCadreY;

        // Mode d'interaction : "automatique" | "deplacement" | "liens" | "lecture"
        private string _mode = "automatique";

        private bool _panEnCours;
        private Point _panDernier;                 // coordonnées écran
        private bool _espaceEnfonce;
        private int _zCompteur;
        private IPointer _pointeurCapture;

        private Noeud _noeudFraichementCree;
        private StyleBoite _stylePresse;    // « copier le style » d'une boîte
        // Le style des nouvelles boîtes vit dans Document.BoiteParDefaut (préférence du fichier).

        // Poignée de courbure des liens courbes
        private readonly Ellipse _poigneeCourbure;
        private bool _courbureEnCours;
        private Lien _lienCourbure;
        private double _departCourbure;

        // Redimensionnement des images
        private const long TailleImageMax = 5 * 1024 * 1024;
        private readonly Rectangle _grip;
        private bool _redimensionEnCours;
        private Noeud _noeudRedimensionne;
        private double _departImgL, _departImgH, _chromeL, _chromeH;
        private double _departLargeurFixe; // boîtes texte : largeur avant le geste (0 = auto)

        // Poignées de création de liens (0 haut, 1 droite, 2 bas, 3 gauche)
        private readonly Ellipse[] _poignees = new Ellipse[4];
        private readonly Line _lienTemporaire;
        private string _idSousPoignee;      // boîte OU cadre de groupe porteur des poignées
        private bool _lienEnCreation;
        private string _lienSourceId;
        private Point _departLien;
        private NoeudVisuel _cibleSurvolee;
        private Groupe _cibleGroupeSurvole;

        public event Action DocumentModifie;
        public event Action<double> ZoomChange;
        public event Action FormatCibleChange;
        public event Action ModeChange;
        public event Action VueChangee; // pan, zoom, recentrage — pour la mini-carte

        private void SignalerVue()
        {
            Action h = VueChangee;
            if (h != null) h();
        }

        public Point OrigineVue { get { return new Point(_translation.X, _translation.Y); } }

        private double LargeurVue { get { return Bounds.Width; } }
        private double HauteurVue { get { return Bounds.Height; } }

        public void CentrerSur(Point pointCanvas)
        {
            _translation.X = LargeurVue / 2 - pointCanvas.X * _echelle.ScaleX;
            _translation.Y = HauteurVue / 2 - pointCanvas.Y * _echelle.ScaleY;
            SignalerVue();
        }

        /// <summary>Le rectangle entre deux points, QUELS QUE SOIENT leurs ordres : le
        /// constructeur Rect(Point, Point) d'Avalonia ne normalise pas et donne une
        /// largeur négative quand on glisse vers la gauche (plantage du 26/10 :
        /// « -1 is not a valid value for Width »).</summary>
        public static Rect RectEntre(Point a, Point b)
        {
            return new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        public List<Rect> RectanglesNoeuds()
        {
            List<Rect> rectangles = new List<Rect>();
            foreach (Noeud n in Document.Noeuds)
                rectangles.Add(RectangleNoeud(n));
            return rectangles;
        }

        public string Mode { get { return _mode; } }

        public void DefinirMode(string mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            if (_mode == "deplacement" || _mode == "lecture") CacherPoignees();
            MettreAJourGrip();
            MettreAJourPoigneeCourbure();
            SignalerFormatCible();
            Action h = ModeChange;
            if (h != null) h();
        }

        public ToileVue()
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF5, 0xF0));
            ClipToBounds = true;
            Focusable = true;

            _canvas = new Canvas();
            _canvas.UseLayoutRounding = false;
            _canvas.ClipToBounds = false;
            _canvas.Background = Brushes.Transparent; // reçoit les clics dans le vide
            _echelle = new ScaleTransform(1, 1);
            _translation = new TranslateTransform(0, 0);
            TransformGroup groupe = new TransformGroup();
            groupe.Children.Add(_echelle);
            groupe.Children.Add(_translation);
            _canvas.RenderTransform = groupe;
            // Le pivot par défaut d'Avalonia est le centre : on le ramène au coin.
            _canvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            Child = _canvas;

            for (int i = 0; i < _poignees.Length; i++)
            {
                Ellipse poignee = new Ellipse();
                poignee.Width = TaillePoignee;
                poignee.Height = TaillePoignee;
                poignee.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
                poignee.Stroke = Brushes.White;
                poignee.StrokeThickness = 2;
                poignee.Cursor = Ui.CurseurCroix;
                poignee.IsVisible = false;
                _poignees[i] = poignee;
            }

            _lienTemporaire = new Line();
            _lienTemporaire.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
            _lienTemporaire.StrokeThickness = 2;
            _lienTemporaire.StrokeDashArray = new AvaloniaList<double>(new double[] { 3, 3 });
            _lienTemporaire.IsHitTestVisible = false;
            _lienTemporaire.IsVisible = false;

            _grip = new Rectangle();
            _grip.Width = 11;
            _grip.Height = 11;
            _grip.RadiusX = 2;
            _grip.RadiusY = 2;
            _grip.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
            _grip.Stroke = Brushes.White;
            _grip.StrokeThickness = 1.5;
            _grip.Cursor = Ui.CurseurDiagonale;
            _grip.IsVisible = false;

            _poigneeCourbure = new Ellipse();
            _poigneeCourbure.Width = 12;
            _poigneeCourbure.Height = 12;
            _poigneeCourbure.Fill = new SolidColorBrush(Color.FromRgb(0xE0, 0x63, 0x8C));
            _poigneeCourbure.Stroke = Brushes.White;
            _poigneeCourbure.StrokeThickness = 2;
            _poigneeCourbure.Cursor = Ui.CurseurDeplacer;
            Ui.Infobulle(_poigneeCourbure, "Courbure du lien : tirer pour bomber le trait");
            _poigneeCourbure.IsVisible = false;

            _rectangleSelection = new Rectangle();
            _rectangleSelection.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
            _rectangleSelection.StrokeThickness = 1;
            _rectangleSelection.StrokeDashArray = new AvaloniaList<double>(new double[] { 4, 3 });
            _rectangleSelection.Fill = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x78, 0xD7));
            _rectangleSelection.IsHitTestVisible = false;
            _rectangleSelection.IsVisible = false;

            _guideVertical = NouveauGuide();
            _guideHorizontal = NouveauGuide();

            AjouterCalquesOutils();

            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, DeposerFichiers);
            AddHandler(DragDrop.DragOverEvent, delegate(object expediteur, DragEventArgs e)
            {
                e.DragEffects = _mode == "lecture" ? DragDropEffects.None : DragDropEffects.Copy;
            });

            Document = new DocumentCarte();
            Historique = new GestionnaireHistorique();

            PointerPressed += PointeurEnfonce;
            PointerReleased += PointeurRelache;
            PointerMoved += PointeurDeplace;
            PointerWheelChanged += MoletteTournee;
            PointerCaptureLost += delegate { _pointeurCapture = null; };
        }

        public double Zoom { get { return _echelle.ScaleX; } }

        private void AjouterCalquesOutils()
        {
            foreach (Ellipse poignee in _poignees)
            {
                poignee.ZIndex = ZPremierPlan;
                _canvas.Children.Add(poignee);
            }
            _lienTemporaire.ZIndex = ZPremierPlan;
            _canvas.Children.Add(_lienTemporaire);
            _grip.ZIndex = ZPremierPlan;
            _canvas.Children.Add(_grip);
            _poigneeCourbure.ZIndex = ZPremierPlan;
            _canvas.Children.Add(_poigneeCourbure);
            _rectangleSelection.ZIndex = ZPremierPlan;
            _canvas.Children.Add(_rectangleSelection);
            _guideVertical.ZIndex = ZPremierPlan;
            _canvas.Children.Add(_guideVertical);
            _guideHorizontal.ZIndex = ZPremierPlan;
            _canvas.Children.Add(_guideHorizontal);
        }

        private static Line NouveauGuide()
        {
            Line guide = new Line();
            guide.Stroke = new SolidColorBrush(Color.FromRgb(0xE0, 0x63, 0x8C));
            guide.StrokeThickness = 1;
            guide.StrokeDashArray = new AvaloniaList<double>(new double[] { 4, 3 });
            guide.IsHitTestVisible = false;
            guide.IsVisible = false;
            return guide;
        }

        private void CacherGuides()
        {
            _guideVertical.IsVisible = false;
            _guideHorizontal.IsVisible = false;
        }

        /// <summary>Le rectangle visible du tableau, en coordonnées canevas.</summary>
        private Rect RectangleVue()
        {
            return new Rect(-_translation.X / _echelle.ScaleX, -_translation.Y / _echelle.ScaleY,
                LargeurVue / _echelle.ScaleX, HauteurVue / _echelle.ScaleY);
        }

        /// <summary>Aimante le déplacement sur les bords et centres des boîtes voisines
        /// et affiche les guides correspondants.</summary>
        private void AppliquerMagnetisme(ref double dx, ref double dy)
        {
            const double seuil = 7;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            HashSet<string> enDeplacement = new HashSet<string>();
            foreach (Noeud n in _dragNoeuds)
            {
                enDeplacement.Add(n.Id);
                Rect taille = RectangleNoeud(n);
                Point depart = _dragDeparts[n.Id];
                minX = Math.Min(minX, depart.X + dx);
                minY = Math.Min(minY, depart.Y + dy);
                maxX = Math.Max(maxX, depart.X + dx + taille.Width);
                maxY = Math.Max(maxY, depart.Y + dy + taille.Height);
            }
            double[] mesX = { minX, (minX + maxX) / 2, maxX };
            double[] mesY = { minY, (minY + maxY) / 2, maxY };

            double correctionX = double.MaxValue, cibleX = 0;
            double correctionY = double.MaxValue, cibleY = 0;
            foreach (Noeud n in Document.Noeuds)
            {
                if (enDeplacement.Contains(n.Id)) continue;
                Rect r = RectangleNoeud(n);
                double[] sesX = { r.X, r.X + r.Width / 2, r.Right };
                double[] sesY = { r.Y, r.Y + r.Height / 2, r.Bottom };
                foreach (double sx in sesX)
                    foreach (double mx in mesX)
                    {
                        double ecart = sx - mx;
                        if (Math.Abs(ecart) < Math.Abs(correctionX)) { correctionX = ecart; cibleX = sx; }
                    }
                foreach (double sy in sesY)
                    foreach (double my in mesY)
                    {
                        double ecart = sy - my;
                        if (Math.Abs(ecart) < Math.Abs(correctionY)) { correctionY = ecart; cibleY = sy; }
                    }
            }

            Rect vue = RectangleVue();
            if (Math.Abs(correctionX) <= seuil)
            {
                dx += correctionX;
                _guideVertical.StartPoint = new Point(cibleX, vue.Top);
                _guideVertical.EndPoint = new Point(cibleX, vue.Bottom);
                _guideVertical.IsVisible = true;
            }
            else _guideVertical.IsVisible = false;
            if (Math.Abs(correctionY) <= seuil)
            {
                dy += correctionY;
                _guideHorizontal.StartPoint = new Point(vue.Left, cibleY);
                _guideHorizontal.EndPoint = new Point(vue.Right, cibleY);
                _guideHorizontal.IsVisible = true;
            }
            else _guideHorizontal.IsVisible = false;
        }

        // ------------------------------------------------------------------ document

        public void ChargerDocument(DocumentCarte document)
        {
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
                kv.Value.EditionTerminee -= NoeudEditionTerminee;
            _canvas.Children.Clear();
            _visuels.Clear();
            _visuelsLiens.Clear();
            _selection = null;
            _selectionLien = null;
            _dragActif = false;
            _dragNoeuds.Clear();
            _dragDeparts.Clear();
            _rectangleActif = false;
            _rectangleSelection.IsVisible = false;
            CacherGuides();
            _selectionnes.Clear();
            _groupeActif = null;
            _groupeRedimensionne = null;
            _visuelsGroupes.Clear();
            _titresGroupes.Clear();
            _noeudFraichementCree = null;
            _idSousPoignee = null;
            _lienEnCreation = false;
            _lienSourceId = null;
            _cibleSurvolee = null;
            _cibleGroupeSurvole = null;
            foreach (Ellipse poignee in _poignees)
            {
                poignee.IsVisible = false;
                poignee.IsHitTestVisible = true;
            }
            _lienTemporaire.IsVisible = false;
            _grip.IsVisible = false;
            _poigneeCourbure.IsVisible = false;
            _courbureEnCours = false;
            _lienCourbure = null;
            _redimensionEnCours = false;
            _noeudRedimensionne = null;
            AjouterCalquesOutils();

            Document = document;
            AppliquerPreferencesDocument(false);
            foreach (Noeud n in document.Noeuds)
                CreerVisuel(n);

            // On écarte les liens dont une extrémité n'existe pas (fichier abîmé).
            List<Lien> invalides = new List<Lien>();
            foreach (Lien lien in document.Liens)
            {
                if (!ExtremiteExiste(lien.SourceId) || !ExtremiteExiste(lien.CibleId))
                    invalides.Add(lien);
                else
                    CreerVisuelLien(lien);
            }
            foreach (Lien lien in invalides)
                document.Liens.Remove(lien);

            // Groupes : on écarte les identifiants orphelins puis les groupes vidés.
            List<Groupe> groupesVides = new List<Groupe>();
            foreach (Groupe groupe in document.Groupes)
            {
                groupe.NoeudIds.RemoveAll(delegate(string id) { return document.TrouverNoeud(id) == null; });
                if (groupe.NoeudIds.Count == 0) groupesVides.Add(groupe);
                else CreerVisuelGroupe(groupe);
            }
            foreach (Groupe groupe in groupesVides)
                document.Groupes.Remove(groupe);

            // Étiquettes : les puces lisent leur couleur dans le document ; groupes pliés masqués.
            NoeudVisuel.ResolveurCouleurEtiquette = delegate(string nom) { return Document.CouleurEtiquette(nom); };
            ReinitialiserEstompage();
            foreach (Groupe groupe in document.Groupes)
                if (groupe.Plie) AppliquerVisibilitePliage(groupe);
            Action hEtiquettes = EtiquettesChange;
            if (hEtiquettes != null) hEtiquettes();

            Historique.Vider();
            _echelle.ScaleX = 1;
            _echelle.ScaleY = 1;
            _translation.X = 0;
            _translation.Y = 0;
            SignalerZoom();
            SignalerFormatCible();
            // Le recentrage a besoin des tailles mesurées : on attend la passe de rendu.
            Dispatcher.UIThread.Post(delegate
            {
                if (!ReferenceEquals(Document, document)) return;
                RafraichirGeometries();
                Recentrer();
            }, DispatcherPriority.Loaded);
        }

        /// <summary>Après la première mesure : les liens et cadres se recalent sur les tailles réelles.</summary>
        private void RafraichirGeometries()
        {
            foreach (Lien lien in Document.Liens) MettreAJourGeometrieLien(lien);
            foreach (Groupe groupe in Document.Groupes) MettreAJourCadreGroupe(groupe);
            MettreAJourGrip();
        }

        public void AjouterNoeud(Noeud noeud)
        {
            Document.Noeuds.Add(noeud);
            CreerVisuel(noeud);
            MettreAJourGroupesDe(noeud);
            SignalerModification();
        }

        public void RetirerNoeud(Noeud noeud)
        {
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
            {
                if (_selectionnes.Contains(visuel))
                {
                    _selectionnes.Remove(visuel);
                    visuel.DefinirSelection(false);
                    if (_selection == visuel)
                        _selection = _selectionnes.Count > 0 ? _selectionnes[0] : null;
                    MettreAJourGrip();
                    SignalerFormatCible();
                }
                visuel.EditionTerminee -= NoeudEditionTerminee;
                _canvas.Children.Remove(visuel);
                _visuels.Remove(noeud);
            }
            MettreAJourGroupesDe(noeud);
            if (_idSousPoignee == noeud.Id)
            {
                _idSousPoignee = null;
                CacherPoignees();
            }
            Document.Noeuds.Remove(noeud);
            SignalerModification();
        }

        public void PositionnerNoeud(Noeud noeud, double x, double y)
        {
            noeud.X = x;
            noeud.Y = y;
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
            {
                Canvas.SetLeft(visuel, x);
                Canvas.SetTop(visuel, y);
            }
            MettreAJourLiensDe(noeud);
            MettreAJourGroupesDe(noeud);
            if (_selection != null && _selection.Modele == noeud) MettreAJourGrip();
            SignalerModification();
        }

        public void DefinirTexteRiche(Noeud noeud, List<Segment> segments, List<Segment> segmentsApres, string alignement)
        {
            // Copie : la commande d'annulation garde ses listes intactes.
            noeud.Segments = Noeud.CopieDe(segments);
            noeud.SegmentsApres = Noeud.CopieDe(segmentsApres);
            noeud.Alignement = alignement;
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.MettreAJour();
            SignalerModification();
            SignalerFormatCible();
        }

        public void AjouterLien(Lien lien)
        {
            Document.Liens.Add(lien);
            CreerVisuelLien(lien);
            SignalerModification();
        }

        public void RetirerLien(Lien lien)
        {
            LienVisuel visuel;
            if (_visuelsLiens.TryGetValue(lien, out visuel))
            {
                if (_selectionLien == visuel)
                {
                    _selectionLien.DefinirSelection(false);
                    _selectionLien = null;
                    MettreAJourPoigneeCourbure();
                }
                _canvas.Children.Remove(visuel);
                _visuelsLiens.Remove(lien);
            }
            Document.Liens.Remove(lien);
            SignalerModification();
        }

        public void AppliquerStyleLien(Lien lien, string couleur, double epaisseur, string trait, string fleche)
        {
            lien.Couleur = couleur;
            lien.Epaisseur = epaisseur;
            lien.Trait = trait;
            lien.Fleche = fleche;
            LienVisuel visuel;
            if (_visuelsLiens.TryGetValue(lien, out visuel))
                visuel.AppliquerStyle();
            MettreAJourGeometrieLien(lien);
            SignalerModification();
        }

        public void AppliquerTraceLien(Lien lien, string trace, double courbure)
        {
            lien.Trace = trace;
            lien.Courbure = courbure;
            MettreAJourGeometrieLien(lien);
            MettreAJourPoigneeCourbure();
            SignalerModification();
            SignalerFormatCible();
        }

        public void AppliquerEtiquetteLien(Lien lien, string etiquette)
        {
            lien.Etiquette = etiquette ?? "";
            LienVisuel visuel;
            if (_visuelsLiens.TryGetValue(lien, out visuel))
                visuel.AppliquerStyle();
            MettreAJourGeometrieLien(lien);
            SignalerModification();
            SignalerFormatCible();
        }

        public void SupprimerSelection()
        {
            if (_mode == "lecture") return;
            if (_selectionLien != null)
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerLien(this, _selectionLien.Modele));
                return;
            }
            if (_selectionnes.Count == 0)
            {
                // Cadre de groupe sélectionné : Suppr dissout le groupe, les boîtes restent.
                if (_groupeActif != null)
                    Historique.ExecuterEtPousser(new CommandeSupprimerGroupe(this, _groupeActif));
                return;
            }
            foreach (NoeudVisuel v in _selectionnes)
                if (v.EnEdition) return;

            List<ICommande> commandes = new List<ICommande>();
            HashSet<Lien> liensPris = new HashSet<Lien>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                // Un lien entre deux boîtes supprimées ne doit être capturé qu'une fois,
                // sinon l'annulation le restaurerait en double.
                List<Lien> liens = new List<Lien>();
                foreach (Lien lien in LiensAttaches(v.Modele))
                {
                    if (liensPris.Contains(lien)) continue;
                    liensPris.Add(lien);
                    liens.Add(lien);
                }
                commandes.Add(new CommandeSupprimerNoeud(this, v.Modele, liens));
            }
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));
        }

        private List<Lien> LiensAttaches(Noeud noeud)
        {
            List<Lien> liens = new List<Lien>();
            foreach (Lien lien in Document.Liens)
                if (lien.Concerne(noeud.Id)) liens.Add(lien);
            return liens;
        }

        private bool LienExiste(string idA, string idB)
        {
            foreach (Lien lien in Document.Liens)
            {
                if ((lien.SourceId == idA && lien.CibleId == idB) ||
                    (lien.SourceId == idB && lien.CibleId == idA))
                    return true;
            }
            return false;
        }

        private NoeudVisuel CreerVisuel(Noeud noeud)
        {
            NoeudVisuel visuel = new NoeudVisuel(noeud);
            visuel.EditionTerminee += NoeudEditionTerminee;
            visuel.SizeChanged += delegate
            {
                MettreAJourLiensDe(noeud);
                MettreAJourGroupesDe(noeud);
                if (_selection == visuel) MettreAJourGrip();
            };
            visuel.FabriqueMenuContextuel = delegate { return ConstruireMenuFormatage(true); };
            visuel.SelectionEditeurChangee += SignalerFormatCible;
            visuel.FichierClique += OuvrirMenuFichier;
            visuel.CaseCliquee += NoeudCaseCliquee;
            visuel.NoteCliquee += NoeudNoteCliquee;
            visuel.DefinirEstompe(!NoeudMisEnAvant(noeud));
            Canvas.SetLeft(visuel, noeud.X);
            Canvas.SetTop(visuel, noeud.Y);
            visuel.ZIndex = ++_zCompteur;
            _canvas.Children.Add(visuel);
            _visuels[noeud] = visuel;
            return visuel;
        }

        private void CreerVisuelLien(Lien lien)
        {
            LienVisuel visuel = new LienVisuel(lien);
            visuel.ZIndex = 0; // toujours sous les boîtes (elles commencent à 1)
            _canvas.Children.Add(visuel);
            _visuelsLiens[lien] = visuel;
            MettreAJourGeometrieLien(lien);
            AppliquerEstompageLien(lien, visuel);
        }

        // ------------------------------------------------------------------ géométrie des liens

        private void MettreAJourLiensDe(Noeud noeud)
        {
            foreach (Lien lien in Document.Liens)
                if (lien.Concerne(noeud.Id)) MettreAJourGeometrieLien(lien);
        }

        /// <summary>Rectangle d'une extrémité de lien : une boîte ou un cadre de groupe.</summary>
        private bool RectangleExtremite(string id, out Rect rectangle)
        {
            Noeud noeud = Document.TrouverNoeud(id);
            if (noeud != null)
            {
                // Boîte d'un groupe plié : le lien se rabat sur la pastille du groupe.
                Groupe plie = GroupePlieDe(id);
                if (plie != null)
                {
                    Rect pastille = RectangleDuGroupe(plie);
                    if (!EstVide(pastille))
                    {
                        rectangle = pastille;
                        return true;
                    }
                }
                rectangle = RectangleNoeud(noeud);
                return true;
            }
            foreach (Groupe groupe in Document.Groupes)
            {
                if (groupe.Id != id) continue;
                Rectangle cadre;
                if (_visuelsGroupes.TryGetValue(groupe, out cadre) && cadre.IsVisible)
                {
                    rectangle = new Rect(Canvas.GetLeft(cadre), Canvas.GetTop(cadre), cadre.Width, cadre.Height);
                    return true;
                }
                rectangle = default(Rect);
                return false;
            }
            rectangle = default(Rect);
            return false;
        }

        private static bool EstVide(Rect rect)
        {
            return rect.Width <= 0 && rect.Height <= 0;
        }

        private bool ExtremiteExiste(string id)
        {
            if (Document.TrouverNoeud(id) != null) return true;
            foreach (Groupe groupe in Document.Groupes)
                if (groupe.Id == id) return true;
            return false;
        }

        private void MettreAJourGeometrieLien(Lien lien)
        {
            LienVisuel visuel;
            if (!_visuelsLiens.TryGetValue(lien, out visuel)) return;
            // Un lien intérieur à un groupe plié (deux boîtes du groupe, ou une boîte et
            // son propre cadre) n'a plus rien à relier : il se masque avec les boîtes.
            Groupe plieSource = GroupePlieDe(lien.SourceId), plieCible = GroupePlieDe(lien.CibleId);
            bool interne = (plieSource != null && (plieSource == plieCible || lien.CibleId == plieSource.Id))
                || (plieCible != null && lien.SourceId == plieCible.Id);
            visuel.IsVisible = !interne;
            if (interne) return;
            Rect rectSource, rectCible;
            if (!RectangleExtremite(lien.SourceId, out rectSource)
                || !RectangleExtremite(lien.CibleId, out rectCible)) return;
            Point centreSource = Centre(rectSource);
            Point centreCible = Centre(rectCible);
            visuel.MettreAJourGeometrie(
                PointAncrage(rectSource, centreCible),
                PointAncrage(rectCible, centreSource));
            if (_selectionLien != null && _selectionLien.Modele == lien)
                MettreAJourPoigneeCourbure();
        }

        /// <summary>La poignée de courbure vit au milieu du tracé du lien courbe sélectionné.</summary>
        private void MettreAJourPoigneeCourbure()
        {
            if (_selectionLien == null || _selectionLien.Modele.Trace != "courbe" || _mode == "lecture")
            {
                _poigneeCourbure.IsVisible = false;
                return;
            }
            Point milieu = _selectionLien.PointMilieu();
            Canvas.SetLeft(_poigneeCourbure, milieu.X - _poigneeCourbure.Width / 2);
            Canvas.SetTop(_poigneeCourbure, milieu.Y - _poigneeCourbure.Height / 2);
            _poigneeCourbure.IsVisible = true;
        }

        private void CommencerCourbure(IPointer pointeur)
        {
            if (_selectionLien == null || _mode == "lecture") return;
            _courbureEnCours = true;
            _lienCourbure = _selectionLien.Modele;
            _departCourbure = _lienCourbure.Courbure;
            Capturer(pointeur);
        }

        /// <summary>Rectangles des extrémités du lien, pour convertir la position de la
        /// souris en courbure signée.</summary>
        private void GlisserCourbure(Point souris)
        {
            Rect rectSource, rectCible;
            if (!RectangleExtremite(_lienCourbure.SourceId, out rectSource)
                || !RectangleExtremite(_lienCourbure.CibleId, out rectCible)) return;
            Point a = PointAncrage(rectSource, Centre(rectCible));
            Point b = PointAncrage(rectCible, Centre(rectSource));
            Vector direction = b - a;
            if (direction.Length < 1) return;
            direction = direction.Normalize();
            Vector perpendiculaire = new Vector(-direction.Y, direction.X);
            Point milieu = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            // Le point à mi-parcours de la Bézier s'écarte de courbure/2 : on double donc
            // l'écart de la souris pour que la courbe passe sous le curseur.
            double ecart = Vector.Dot(souris - milieu, perpendiculaire);
            _lienCourbure.Courbure = Math.Max(-400, Math.Min(400, ecart * 2));
            MettreAJourGeometrieLien(_lienCourbure);
        }

        private void TerminerCourbure()
        {
            Liberer();
            _courbureEnCours = false;
            double nouvelle = _lienCourbure.Courbure;
            if (nouvelle != _departCourbure)
            {
                // On repart de l'ancienne valeur pour que la commande capture le bon état.
                _lienCourbure.Courbure = _departCourbure;
                Historique.ExecuterEtPousser(new CommandeModifierTraceLien(
                    this, _lienCourbure, _lienCourbure.Trace, nouvelle));
            }
            _lienCourbure = null;
        }

        private void MettreAJourLiensDeId(string id)
        {
            foreach (Lien lien in Document.Liens)
                if (lien.Concerne(id)) MettreAJourGeometrieLien(lien);
        }

        private Rect RectangleNoeud(Noeud noeud)
        {
            double largeur = 120, hauteur = 32;
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel) && visuel.Bounds.Width > 0)
            {
                largeur = visuel.Bounds.Width;
                hauteur = visuel.Bounds.Height;
            }
            return new Rect(noeud.X, noeud.Y, largeur, hauteur);
        }

        private static Point Centre(Rect rect)
        {
            return GeometrieLien.Centre(rect);
        }

        private static Point PointAncrage(Rect rect, Point vers)
        {
            return GeometrieLien.PointAncrage(rect, vers);
        }

        // ------------------------------------------------------------------ capture du pointeur

        private void Capturer(IPointer pointeur)
        {
            _pointeurCapture = pointeur;
            if (pointeur != null) pointeur.Capture(this);
        }

        private void Liberer()
        {
            if (_pointeurCapture != null)
            {
                try { _pointeurCapture.Capture(null); }
                catch { }
            }
            _pointeurCapture = null;
        }

        private void SignalerModification()
        {
            Action h = DocumentModifie;
            if (h != null) h();
        }

        private void SignalerZoom()
        {
            Action<double> h = ZoomChange;
            if (h != null) h(_echelle.ScaleX);
        }

        private void SignalerFormatCible()
        {
            Action h = FormatCibleChange;
            if (h != null) h();
        }

        // ------------------------------------------------------------------ utilitaires

        private NoeudVisuel TrouverNoeud(object origine)
        {
            return Ui.Ancetre<NoeudVisuel>(origine, this);
        }

        private LienVisuel TrouverLien(object origine)
        {
            return Ui.Ancetre<LienVisuel>(origine, this);
        }

        private bool EstDansEditeur(object origine)
        {
            return Ui.Ancetre<TextBox>(origine, this) != null;
        }

        /// <summary>La fenêtre propriétaire des dialogues ouverts par la toile.</summary>
        private Window FenetreProprietaire()
        {
            return Ui.Fenetre(this);
        }
    }
}
