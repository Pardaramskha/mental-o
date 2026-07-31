using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le canevas principal : rendu des nœuds et des liens, navigation (pan/zoom)
    /// et interactions souris, dont la poignée de création de liens.</summary>
    public class ToileVue : Border
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

        // Mode d'interaction : "automatique" | "deplacement" | "liens"
        private string _mode = "automatique";

        private bool _panEnCours;
        private Point _panDernier;                 // coordonnées écran
        private bool _espaceEnfonce;
        private int _zCompteur;

        private Noeud _noeudFraichementCree;
        private StyleBoite _stylePresse;    // « copier le style » d'une boîte
        private StyleBoite _styleParDefaut; // style appliqué aux nouvelles boîtes

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

        public void CentrerSur(Point pointCanvas)
        {
            _translation.X = ActualWidth / 2 - pointCanvas.X * _echelle.ScaleX;
            _translation.Y = ActualHeight / 2 - pointCanvas.Y * _echelle.ScaleY;
            SignalerVue();
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
            SignalerFormatCible();
            Action h = ModeChange;
            if (h != null) h();
        }

        public ToileVue()
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF5, 0xF0));
            ClipToBounds = true;
            Focusable = true;
            FocusVisualStyle = null;

            _canvas = new Canvas();
            // L'arrondi de mise en page (hérité de la fenêtre) déforme les bordures sous zoom.
            _canvas.UseLayoutRounding = false;
            _echelle = new ScaleTransform(1, 1);
            _translation = new TranslateTransform(0, 0);
            TransformGroup groupe = new TransformGroup();
            groupe.Children.Add(_echelle);
            groupe.Children.Add(_translation);
            _canvas.RenderTransform = groupe;
            Child = _canvas;

            for (int i = 0; i < _poignees.Length; i++)
            {
                Ellipse poignee = new Ellipse();
                poignee.Width = TaillePoignee;
                poignee.Height = TaillePoignee;
                poignee.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
                poignee.Stroke = Brushes.White;
                poignee.StrokeThickness = 2;
                poignee.Cursor = Cursors.Cross;
                poignee.Visibility = Visibility.Collapsed;
                _poignees[i] = poignee;
            }

            _lienTemporaire = new Line();
            _lienTemporaire.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
            _lienTemporaire.StrokeThickness = 2;
            _lienTemporaire.StrokeDashArray = new DoubleCollection(new double[] { 3, 3 });
            _lienTemporaire.IsHitTestVisible = false;
            _lienTemporaire.Visibility = Visibility.Collapsed;

            _grip = new Rectangle();
            _grip.Width = 11;
            _grip.Height = 11;
            _grip.RadiusX = 2;
            _grip.RadiusY = 2;
            _grip.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
            _grip.Stroke = Brushes.White;
            _grip.StrokeThickness = 1.5;
            _grip.Cursor = Cursors.SizeNWSE;
            _grip.Visibility = Visibility.Collapsed;

            _rectangleSelection = new Rectangle();
            _rectangleSelection.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
            _rectangleSelection.StrokeThickness = 1;
            _rectangleSelection.StrokeDashArray = new DoubleCollection(new double[] { 4, 3 });
            _rectangleSelection.Fill = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x78, 0xD7));
            _rectangleSelection.IsHitTestVisible = false;
            _rectangleSelection.Visibility = Visibility.Collapsed;

            _guideVertical = NouveauGuide();
            _guideHorizontal = NouveauGuide();

            AjouterCalquesOutils();

            AllowDrop = true;
            Drop += DeposerFichiers;

            Document = new DocumentCarte();
            Historique = new GestionnaireHistorique();

            MouseLeftButtonDown += SourisGaucheEnfoncee;
            MouseLeftButtonUp += SourisGaucheRelachee;
            MouseRightButtonDown += SourisDroiteEnfoncee;
            MouseMove += SourisDeplacee;
            MouseDown += SourisEnfoncee;
            MouseUp += SourisRelachee;
            MouseWheel += MoletteTournee;
        }

        public double Zoom { get { return _echelle.ScaleX; } }

        private void AjouterCalquesOutils()
        {
            foreach (Ellipse poignee in _poignees)
            {
                Canvas.SetZIndex(poignee, ZPremierPlan);
                _canvas.Children.Add(poignee);
            }
            Canvas.SetZIndex(_lienTemporaire, ZPremierPlan);
            _canvas.Children.Add(_lienTemporaire);
            Canvas.SetZIndex(_grip, ZPremierPlan);
            _canvas.Children.Add(_grip);
            Canvas.SetZIndex(_rectangleSelection, ZPremierPlan);
            _canvas.Children.Add(_rectangleSelection);
            Canvas.SetZIndex(_guideVertical, ZPremierPlan);
            _canvas.Children.Add(_guideVertical);
            Canvas.SetZIndex(_guideHorizontal, ZPremierPlan);
            _canvas.Children.Add(_guideHorizontal);
        }

        private static Line NouveauGuide()
        {
            Line guide = new Line();
            guide.Stroke = new SolidColorBrush(Color.FromRgb(0xE0, 0x63, 0x8C));
            guide.StrokeThickness = 1;
            guide.StrokeDashArray = new DoubleCollection(new double[] { 4, 3 });
            guide.IsHitTestVisible = false;
            guide.Visibility = Visibility.Collapsed;
            return guide;
        }

        private void CacherGuides()
        {
            _guideVertical.Visibility = Visibility.Collapsed;
            _guideHorizontal.Visibility = Visibility.Collapsed;
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

            Rect vue = new Rect(-_translation.X / _echelle.ScaleX, -_translation.Y / _echelle.ScaleY,
                ActualWidth / _echelle.ScaleX, ActualHeight / _echelle.ScaleY);
            if (Math.Abs(correctionX) <= seuil)
            {
                dx += correctionX;
                _guideVertical.X1 = cibleX; _guideVertical.X2 = cibleX;
                _guideVertical.Y1 = vue.Top; _guideVertical.Y2 = vue.Bottom;
                _guideVertical.Visibility = Visibility.Visible;
            }
            else _guideVertical.Visibility = Visibility.Collapsed;
            if (Math.Abs(correctionY) <= seuil)
            {
                dy += correctionY;
                _guideHorizontal.Y1 = cibleY; _guideHorizontal.Y2 = cibleY;
                _guideHorizontal.X1 = vue.Left; _guideHorizontal.X2 = vue.Right;
                _guideHorizontal.Visibility = Visibility.Visible;
            }
            else _guideHorizontal.Visibility = Visibility.Collapsed;
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
            _rectangleSelection.Visibility = Visibility.Collapsed;
            CacherGuides();
            _selectionnes.Clear();
            _groupeActif = null;
            _groupeRedimensionne = null;
            foreach (KeyValuePair<Groupe, Rectangle> kv in _visuelsGroupes)
                _canvas.Children.Remove(kv.Value);
            _visuelsGroupes.Clear();
            foreach (KeyValuePair<Groupe, TextBlock> kv in _titresGroupes)
                _canvas.Children.Remove(kv.Value);
            _titresGroupes.Clear();
            _noeudFraichementCree = null;
            _idSousPoignee = null;
            _lienEnCreation = false;
            _lienSourceId = null;
            _cibleSurvolee = null;
            _cibleGroupeSurvole = null;
            foreach (Ellipse poignee in _poignees)
            {
                poignee.Visibility = Visibility.Collapsed;
                poignee.IsHitTestVisible = true;
            }
            _lienTemporaire.Visibility = Visibility.Collapsed;
            _grip.Visibility = Visibility.Collapsed;
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

            Historique.Vider();
            _echelle.ScaleX = 1;
            _echelle.ScaleY = 1;
            _translation.X = 0;
            _translation.Y = 0;
            SignalerZoom();
            SignalerFormatCible();
            // Le recentrage a besoin des tailles mesurées : on attend la passe de rendu.
            Dispatcher.BeginInvoke(new Action(Recentrer), DispatcherPriority.Loaded);
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
            Canvas.SetLeft(visuel, noeud.X);
            Canvas.SetTop(visuel, noeud.Y);
            Canvas.SetZIndex(visuel, ++_zCompteur);
            _canvas.Children.Add(visuel);
            _visuels[noeud] = visuel;
            return visuel;
        }

        private void CreerVisuelLien(Lien lien)
        {
            LienVisuel visuel = new LienVisuel(lien);
            Canvas.SetZIndex(visuel, 0); // toujours sous les boîtes (elles commencent à 1)
            _canvas.Children.Add(visuel);
            _visuelsLiens[lien] = visuel;
            MettreAJourGeometrieLien(lien);
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
                rectangle = RectangleNoeud(noeud);
                return true;
            }
            foreach (Groupe groupe in Document.Groupes)
            {
                if (groupe.Id != id) continue;
                Rectangle cadre;
                if (_visuelsGroupes.TryGetValue(groupe, out cadre) && cadre.Visibility == Visibility.Visible)
                {
                    rectangle = new Rect(Canvas.GetLeft(cadre), Canvas.GetTop(cadre), cadre.Width, cadre.Height);
                    return true;
                }
                rectangle = Rect.Empty;
                return false;
            }
            rectangle = Rect.Empty;
            return false;
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
            Rect rectSource, rectCible;
            if (!RectangleExtremite(lien.SourceId, out rectSource)
                || !RectangleExtremite(lien.CibleId, out rectCible)) return;
            Point centreSource = Centre(rectSource);
            Point centreCible = Centre(rectCible);
            visuel.MettreAJourGeometrie(
                PointAncrage(rectSource, centreCible),
                PointAncrage(rectCible, centreSource));
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
            if (_visuels.TryGetValue(noeud, out visuel) && visuel.ActualWidth > 0)
            {
                largeur = visuel.ActualWidth;
                hauteur = visuel.ActualHeight;
            }
            return new Rect(noeud.X, noeud.Y, largeur, hauteur);
        }

        private static Point Centre(Rect rect)
        {
            return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        }

        /// <summary>Point où la ligne vers « vers » perce le bord du rectangle (avec 2 px de marge).</summary>
        private static Point PointAncrage(Rect rect, Point vers)
        {
            Point centre = Centre(rect);
            double dx = vers.X - centre.X, dy = vers.Y - centre.Y;
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001) return centre;
            double t = double.MaxValue;
            if (Math.Abs(dx) > 0.001) t = Math.Min(t, (rect.Width / 2 + 2) / Math.Abs(dx));
            if (Math.Abs(dy) > 0.001) t = Math.Min(t, (rect.Height / 2 + 2) / Math.Abs(dy));
            if (t > 1) t = 1; // boîtes qui se chevauchent : on relie les centres
            return new Point(centre.X + dx * t, centre.Y + dy * t);
        }

        // ------------------------------------------------------------------ interactions

        private void SourisGaucheEnfoncee(object expediteur, MouseButtonEventArgs e)
        {
            if (EstDansEditeur(e.OriginalSource)) return;

            if (ReferenceEquals(e.OriginalSource, _grip))
            {
                Focus();
                CommencerRedimension();
                e.Handled = true;
                return;
            }

            int indicePoignee = IndicePoignee(e.OriginalSource);
            if (indicePoignee >= 0)
            {
                Focus();
                CommencerCreationLien(indicePoignee);
                e.Handled = true;
                return;
            }

            Focus(); // retire le focus d'un éventuel éditeur → valide son texte

            if (_espaceEnfonce)
            {
                CommencerPan(e.GetPosition(this));
                e.Handled = true;
                return;
            }

            NoeudVisuel noeud = TrouverNoeud(e.OriginalSource);
            LienVisuel lien = noeud == null ? TrouverLien(e.OriginalSource) : null;

            if (e.ClickCount == 2)
            {
                if (_mode == "lecture")
                {
                    if (noeud != null) Selectionner(noeud);
                    e.Handled = true;
                    return;
                }
                if (noeud != null)
                {
                    Selectionner(noeud);
                    noeud.CommencerEdition();
                    SignalerFormatCible();
                }
                else if (lien != null)
                {
                    SelectionnerLien(lien);
                }
                else
                {
                    CreerNoeudA(e.GetPosition(_canvas));
                }
                e.Handled = true;
                return;
            }

            if (noeud != null)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    BasculerSelection(noeud);
                    return;
                }
                if (!_selectionnes.Contains(noeud) || _selectionnes.Count <= 1)
                {
                    Selectionner(noeud);
                }
                else
                {
                    _selection = noeud; // la boîte cliquée devient la principale
                    MettreAJourGrip();
                    SignalerFormatCible();
                }
                Canvas.SetZIndex(noeud, ++_zCompteur);
                if (_mode != "liens" && _mode != "lecture")
                    CommencerDeplacement(e.GetPosition(_canvas), ModelesSelectionnes());
            }
            else if (lien != null)
            {
                SelectionnerLien(lien);
            }
            else
            {
                Groupe groupe = TrouverGroupe(e.OriginalSource);
                if (groupe != null)
                {
                    // Le cadre se sélectionne seul (personnalisation directe) ;
                    // le glisser déplace quand même tout le bloc.
                    SelectionnerGroupe(groupe);
                    if (_mode != "liens" && _mode != "lecture")
                        CommencerDeplacement(e.GetPosition(_canvas), NoeudsDuGroupe(groupe));
                }
                else
                {
                    // Rectangle de sélection (un simple clic sans glisser désélectionnera).
                    _rectangleActif = true;
                    _rectangleDepart = e.GetPosition(_canvas);
                    CaptureMouse();
                }
            }
        }

        public void SelectionnerGroupe(Groupe groupe)
        {
            Selectionner(null); // vide boîtes et liens, remet _groupeActif à null
            _groupeActif = groupe;
            MettreAJourSurbrillanceGroupes();
            MettreAJourGrip();
            SignalerFormatCible();
        }

        private void MettreAJourSurbrillanceGroupes()
        {
            foreach (KeyValuePair<Groupe, Rectangle> kv in _visuelsGroupes)
                AppliquerStyleVisuelGroupe(kv.Key);
        }

        private List<Noeud> ModelesSelectionnes()
        {
            List<Noeud> noeuds = new List<Noeud>();
            foreach (NoeudVisuel v in _selectionnes)
                noeuds.Add(v.Modele);
            return noeuds;
        }

        private List<Noeud> NoeudsDuGroupe(Groupe groupe)
        {
            List<Noeud> noeuds = new List<Noeud>();
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud != null) noeuds.Add(noeud);
            }
            return noeuds;
        }

        private void CommencerDeplacement(Point origine, List<Noeud> noeuds)
        {
            _dragActif = true;
            _deplacementEffectif = false;
            _dragOrigine = origine;
            _dragNoeuds.Clear();
            _dragDeparts.Clear();
            foreach (Noeud n in noeuds)
            {
                _dragNoeuds.Add(n);
                _dragDeparts[n.Id] = new Point(n.X, n.Y);
            }
            CacherPoignees();
            CaptureMouse();
        }

        private void SourisDroiteEnfoncee(object expediteur, MouseButtonEventArgs e)
        {
            if (EstDansEditeur(e.OriginalSource)) return;
            if (_mode == "lecture")
            {
                NoeudVisuel visee = TrouverNoeud(e.OriginalSource);
                if (visee != null) Selectionner(visee);
                e.Handled = true;
                return;
            }
            LienVisuel lien = TrouverLien(e.OriginalSource);
            if (lien != null)
            {
                SelectionnerLien(lien);
                OuvrirMenuLien(lien.Modele);
                e.Handled = true;
                return;
            }
            NoeudVisuel noeud = TrouverNoeud(e.OriginalSource);
            if (noeud != null)
            {
                if (!_selectionnes.Contains(noeud)) Selectionner(noeud);
                else { _selection = noeud; SignalerFormatCible(); }
                ContextMenu menu = ConstruireMenuFormatage(false);
                menu.PlacementTarget = this;
                menu.IsOpen = true;
                e.Handled = true;
                return;
            }
            Groupe groupe = TrouverGroupe(e.OriginalSource);
            if (groupe != null)
            {
                SelectionnerGroupe(groupe);
                OuvrirMenuGroupe(groupe);
                e.Handled = true;
            }
        }

        private void SourisDeplacee(object expediteur, MouseEventArgs e)
        {
            if (_panEnCours)
            {
                Point p = e.GetPosition(this);
                _translation.X += p.X - _panDernier.X;
                _translation.Y += p.Y - _panDernier.Y;
                _panDernier = p;
                SignalerVue();
                return;
            }

            if (_redimensionEnCours)
            {
                Point p = e.GetPosition(_canvas);
                if (_groupeRedimensionne != null)
                {
                    // Le cadre peut excéder son contenu, jamais être plus petit.
                    Size minimum = TailleMinimaleGroupe(_groupeRedimensionne);
                    DefinirTailleGroupe(_groupeRedimensionne,
                        Math.Max(minimum.Width, p.X - _origineCadreX),
                        Math.Max(minimum.Height, p.Y - _origineCadreY));
                    return;
                }
                if (_noeudRedimensionne.EstImage)
                {
                    double cibleL = p.X - _noeudRedimensionne.X - _chromeL;
                    double cibleH = p.Y - _noeudRedimensionne.Y - _chromeH;
                    // Proportions conservées par défaut ; Maj pour redimensionner librement.
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                    {
                        double facteur = Math.Max(cibleL / _departImgL, cibleH / _departImgH);
                        cibleL = _departImgL * facteur;
                        cibleH = _departImgH * facteur;
                    }
                    DefinirTailleImage(_noeudRedimensionne, Math.Max(20, cibleL), Math.Max(20, cibleH));
                }
                else
                {
                    // Boîte texte : la poignée règle la largeur, la hauteur suit le texte.
                    double cible = Math.Max(60, Math.Min(1200, p.X - _noeudRedimensionne.X));
                    DefinirLargeurNoeud(_noeudRedimensionne, cible);
                }
                return;
            }

            if (_lienEnCreation)
            {
                Point p = e.GetPosition(_canvas);
                _lienTemporaire.X1 = _departLien.X;
                _lienTemporaire.Y1 = _departLien.Y;
                _lienTemporaire.X2 = p.X;
                _lienTemporaire.Y2 = p.Y;

                NoeudVisuel cible = NoeudSousPoint(p);
                Groupe cibleGroupe = null;
                if (cible != null && cible.Modele.Id == _lienSourceId) cible = null;
                if (cible == null)
                {
                    cibleGroupe = GroupeSousPoint(p);
                    if (cibleGroupe != null && cibleGroupe.Id == _lienSourceId) cibleGroupe = null;
                }
                if (!ReferenceEquals(cible, _cibleSurvolee))
                {
                    if (_cibleSurvolee != null) _cibleSurvolee.DefinirCibleDeLien(false);
                    _cibleSurvolee = cible;
                    if (_cibleSurvolee != null) _cibleSurvolee.DefinirCibleDeLien(true);
                }
                if (!ReferenceEquals(cibleGroupe, _cibleGroupeSurvole))
                {
                    if (_cibleGroupeSurvole != null) AppliquerStyleVisuelGroupe(_cibleGroupeSurvole);
                    _cibleGroupeSurvole = cibleGroupe;
                    if (_cibleGroupeSurvole != null)
                    {
                        Rectangle cadre;
                        if (_visuelsGroupes.TryGetValue(_cibleGroupeSurvole, out cadre))
                            cadre.Stroke = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
                    }
                }
                return;
            }

            if (_dragActif && e.LeftButton == MouseButtonState.Pressed)
            {
                Point p = e.GetPosition(_canvas);
                Vector delta = p - _dragOrigine;
                if (!_deplacementEffectif)
                {
                    if (Math.Abs(delta.X) < SeuilDeplacement && Math.Abs(delta.Y) < SeuilDeplacement)
                        return;
                    _deplacementEffectif = true;
                }
                double dx = delta.X, dy = delta.Y;
                if (Mentalo.Parametres.ParametresGlobaux.Magnetisme
                    && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
                    AppliquerMagnetisme(ref dx, ref dy);
                else
                    CacherGuides();
                foreach (Noeud n in _dragNoeuds)
                {
                    Point depart = _dragDeparts[n.Id];
                    PositionnerNoeud(n, depart.X + dx, depart.Y + dy);
                }
                return;
            }

            if (_rectangleActif && e.LeftButton == MouseButtonState.Pressed)
            {
                Point p = e.GetPosition(_canvas);
                Rect zone = new Rect(_rectangleDepart, p);
                Canvas.SetLeft(_rectangleSelection, zone.X);
                Canvas.SetTop(_rectangleSelection, zone.Y);
                _rectangleSelection.Width = zone.Width;
                _rectangleSelection.Height = zone.Height;
                _rectangleSelection.Visibility = (zone.Width > 3 || zone.Height > 3)
                    ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            MettreAJourPoignees(e);
        }

        private void SourisGaucheRelachee(object expediteur, MouseButtonEventArgs e)
        {
            if (_panEnCours)
            {
                TerminerPan();
                return;
            }

            if (_redimensionEnCours)
            {
                ReleaseMouseCapture();
                _redimensionEnCours = false;
                if (_groupeRedimensionne != null)
                {
                    if (_groupeRedimensionne.LargeurFixe != _departGrpL || _groupeRedimensionne.HauteurFixe != _departGrpH)
                        Historique.Pousser(new CommandeRedimensionnerGroupe(this, _groupeRedimensionne,
                            _departGrpL, _departGrpH,
                            _groupeRedimensionne.LargeurFixe, _groupeRedimensionne.HauteurFixe));
                    _groupeRedimensionne = null;
                    return;
                }
                if (_noeudRedimensionne.EstImage)
                {
                    if (_noeudRedimensionne.ImageLargeur != _departImgL || _noeudRedimensionne.ImageHauteur != _departImgH)
                        Historique.Pousser(new CommandeRedimensionnerImage(this, _noeudRedimensionne,
                            _departImgL, _departImgH,
                            _noeudRedimensionne.ImageLargeur, _noeudRedimensionne.ImageHauteur));
                }
                else if (_noeudRedimensionne.LargeurFixe != _departLargeurFixe)
                {
                    Historique.Pousser(new CommandeRedimensionnerNoeud(this, _noeudRedimensionne,
                        _departLargeurFixe, _noeudRedimensionne.LargeurFixe));
                }
                _noeudRedimensionne = null;
                return;
            }

            if (_lienEnCreation)
            {
                TerminerCreationLien();
                return;
            }

            if (_dragActif)
            {
                ReleaseMouseCapture();
                _dragActif = false;
                CacherGuides();
                if (_deplacementEffectif)
                {
                    List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
                    foreach (Noeud n in _dragNoeuds)
                    {
                        Point depart = _dragDeparts[n.Id];
                        if (depart.X == n.X && depart.Y == n.Y) continue;
                        CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                        d.Noeud = n;
                        d.AncienX = depart.X;
                        d.AncienY = depart.Y;
                        d.NouveauX = n.X;
                        d.NouveauY = n.Y;
                        deplacements.Add(d);
                    }
                    if (deplacements.Count > 0)
                        Historique.Pousser(new CommandeDeplacerNoeuds(this, deplacements));
                }
                _dragNoeuds.Clear();
                _dragDeparts.Clear();
                return;
            }

            if (_rectangleActif)
            {
                ReleaseMouseCapture();
                _rectangleActif = false;
                _rectangleSelection.Visibility = Visibility.Collapsed;
                Point p = e.GetPosition(_canvas);
                Rect zone = new Rect(_rectangleDepart, p);
                bool ajout = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (zone.Width < 4 && zone.Height < 4)
                {
                    if (!ajout) Selectionner(null);
                    return;
                }
                List<NoeudVisuel> touches = new List<NoeudVisuel>();
                if (ajout) touches.AddRange(_selectionnes);
                foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
                {
                    if (zone.IntersectsWith(RectangleNoeud(kv.Key)) && !touches.Contains(kv.Value))
                        touches.Add(kv.Value);
                }
                SelectionnerEnsemble(touches, touches.Count > 0 ? touches[0] : null);
            }
        }

        private void SourisEnfoncee(object expediteur, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                CacherPoignees();
                CommencerPan(e.GetPosition(this));
                e.Handled = true;
            }
        }

        private void SourisRelachee(object expediteur, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && _panEnCours)
                TerminerPan();
        }

        private void MoletteTournee(object expediteur, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                double vitesse = Mentalo.Parametres.ParametresGlobaux.FacteurZoom();
                double facteur = e.Delta > 0 ? vitesse : 1.0 / vitesse;
                ZoomVers(_echelle.ScaleX * facteur, e.GetPosition(this));
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                _translation.X += e.Delta * 0.7;
            }
            else
            {
                _translation.Y += e.Delta * 0.7;
            }
            SignalerVue();
            e.Handled = true;
        }

        /// <summary>Activé quand la touche Espace est maintenue (géré par la fenêtre).</summary>
        public void ModePanClavier(bool actif)
        {
            _espaceEnfonce = actif;
            if (!_panEnCours)
                Cursor = actif ? Cursors.Hand : Cursors.Arrow;
        }

        private void CommencerPan(Point positionEcran)
        {
            _panEnCours = true;
            _panDernier = positionEcran;
            Cursor = Cursors.Hand;
            CaptureMouse();
        }

        private void TerminerPan()
        {
            _panEnCours = false;
            Cursor = _espaceEnfonce ? Cursors.Hand : Cursors.Arrow;
            ReleaseMouseCapture();
        }

        // ------------------------------------------------------------------ poignées et création de liens

        private void MettreAJourPoignees(MouseEventArgs e)
        {
            if (_mode == "deplacement" || _mode == "lecture")
            {
                CacherPoignees();
                return;
            }
            Point souris = e.GetPosition(_canvas);
            NoeudVisuel noeud = TrouverNoeud(e.OriginalSource);
            Groupe groupeSurvole = noeud == null ? TrouverGroupe(e.OriginalSource) : null;

            if (noeud != null && !noeud.EnEdition)
            {
                _idSousPoignee = noeud.Modele.Id;
            }
            else if (groupeSurvole != null)
            {
                _idSousPoignee = groupeSurvole.Id; // les cadres de groupes se lient aussi
            }
            else if (_idSousPoignee != null)
            {
                // Zone de grâce : les poignées restent tant que la souris est proche,
                // pour qu'on puisse aller les agripper sans les voir disparaître.
                Rect zone;
                if (!RectangleExtremite(_idSousPoignee, out zone))
                {
                    _idSousPoignee = null;
                }
                else
                {
                    zone.Inflate(MargeGracePoignees, MargeGracePoignees);
                    if (!zone.Contains(souris))
                        _idSousPoignee = null;
                }
            }

            if (_idSousPoignee == null)
            {
                CacherPoignees();
                return;
            }

            bool porteurSurvole = (noeud != null && noeud.Modele.Id == _idSousPoignee)
                || (groupeSurvole != null && groupeSurvole.Id == _idSousPoignee);
            PositionnerPoignees(porteurSurvole, souris);
        }

        private void PositionnerPoignees(bool boiteSurvolee, Point souris)
        {
            Rect rect;
            if (!RectangleExtremite(_idSousPoignee, out rect))
            {
                CacherPoignees();
                return;
            }
            Point[] centres = CentresPoignees(rect);
            for (int i = 0; i < _poignees.Length; i++)
            {
                // Surbrillance : épaissies quand la boîte est survolée, encore plus sous le curseur.
                double facteurPoignees = Mentalo.Parametres.ParametresGlobaux.FacteurPoignees();
                double distance = (souris - centres[i]).Length;
                double taille;
                if (distance <= TaillePoigneeSaisie * facteurPoignees) taille = TaillePoigneeSaisie;
                else if (boiteSurvolee) taille = TaillePoigneeAccent;
                else taille = TaillePoignee;
                taille *= facteurPoignees;

                Ellipse poignee = _poignees[i];
                poignee.Width = taille;
                poignee.Height = taille;
                poignee.StrokeThickness = taille >= TaillePoigneeAccent * facteurPoignees ? 3 : 2;
                Canvas.SetLeft(poignee, centres[i].X - taille / 2);
                Canvas.SetTop(poignee, centres[i].Y - taille / 2);
                poignee.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Les centres des quatre bordures : haut, droite, bas, gauche.</summary>
        private static Point[] CentresPoignees(Rect rect)
        {
            double cx = rect.X + rect.Width / 2;
            double cy = rect.Y + rect.Height / 2;
            return new Point[]
            {
                new Point(cx, rect.Y),
                new Point(rect.Right, cy),
                new Point(cx, rect.Bottom),
                new Point(rect.X, cy)
            };
        }

        private void CacherPoignees()
        {
            foreach (Ellipse poignee in _poignees)
                poignee.Visibility = Visibility.Collapsed;
        }

        private int IndicePoignee(object origine)
        {
            for (int i = 0; i < _poignees.Length; i++)
                if (ReferenceEquals(origine, _poignees[i])) return i;
            return -1;
        }

        private void CommencerCreationLien(int indicePoignee)
        {
            if (_idSousPoignee == null || _mode == "lecture") return;
            Rect rect;
            if (!RectangleExtremite(_idSousPoignee, out rect)) return;
            _lienEnCreation = true;
            _lienSourceId = _idSousPoignee;
            _departLien = CentresPoignees(rect)[indicePoignee];
            foreach (Ellipse poignee in _poignees)
                poignee.IsHitTestVisible = false;
            CacherPoignees();
            _lienTemporaire.X1 = _departLien.X;
            _lienTemporaire.Y1 = _departLien.Y;
            _lienTemporaire.X2 = _departLien.X;
            _lienTemporaire.Y2 = _departLien.Y;
            _lienTemporaire.Visibility = Visibility.Visible;
            Cursor = Cursors.Cross;
            CaptureMouse();
        }

        private void TerminerCreationLien()
        {
            ReleaseMouseCapture();
            Cursor = _espaceEnfonce ? Cursors.Hand : Cursors.Arrow;
            _lienTemporaire.Visibility = Visibility.Collapsed;
            foreach (Ellipse poignee in _poignees)
                poignee.IsHitTestVisible = true;
            CacherPoignees();
            _lienEnCreation = false;

            NoeudVisuel cible = _cibleSurvolee;
            _cibleSurvolee = null;
            if (cible != null) cible.DefinirCibleDeLien(false);
            Groupe cibleGroupe = _cibleGroupeSurvole;
            _cibleGroupeSurvole = null;
            if (cibleGroupe != null) AppliquerStyleVisuelGroupe(cibleGroupe);

            string cibleId = cible != null ? cible.Modele.Id
                : (cibleGroupe != null ? cibleGroupe.Id : null);
            if (cibleId != null && _lienSourceId != null && !LienExiste(_lienSourceId, cibleId))
            {
                Lien lien = new Lien();
                lien.SourceId = _lienSourceId;
                lien.CibleId = cibleId;
                Historique.ExecuterEtPousser(new CommandeCreerLien(this, lien));
                SelectionnerLien(_visuelsLiens[lien]);
            }
            _lienSourceId = null;
        }

        private Groupe GroupeSousPoint(Point pointCanvas)
        {
            foreach (KeyValuePair<Groupe, Rectangle> kv in _visuelsGroupes)
            {
                if (kv.Value.Visibility != Visibility.Visible) continue;
                Rect rect = new Rect(Canvas.GetLeft(kv.Value), Canvas.GetTop(kv.Value),
                    kv.Value.Width, kv.Value.Height);
                if (rect.Contains(pointCanvas)) return kv.Key;
            }
            return null;
        }

        private NoeudVisuel NoeudSousPoint(Point pointCanvas)
        {
            // La surcharge simple de HitTest ignore IsHitTestVisible : le trait provisoire du
            // lien se faisait toucher à la place de la boîte (surbrillance qui scintille).
            // Version filtrée : on saute l'invisible et on continue derrière tout ce qui
            // n'appartient pas à une boîte.
            NoeudVisuel trouve = null;
            VisualTreeHelper.HitTest(_canvas,
                delegate(DependencyObject candidat)
                {
                    UIElement element = candidat as UIElement;
                    if (element != null && (!element.IsHitTestVisible || element.Visibility != Visibility.Visible))
                        return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
                    return HitTestFilterBehavior.Continue;
                },
                delegate(HitTestResult resultat)
                {
                    NoeudVisuel noeud = TrouverNoeud(resultat.VisualHit);
                    if (noeud != null)
                    {
                        trouve = noeud;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(pointCanvas));
            return trouve;
        }

        // ------------------------------------------------------------------ menu contextuel des liens

        private void OuvrirMenuLien(Lien lien)
        {
            ContextMenu menu = new ContextMenu();

            MenuItem couleur = new MenuItem { Header = "Couleur" };
            AjouterChoixCouleur(couleur, lien, "Gris", "#6A665E");
            AjouterChoixCouleur(couleur, lien, "Noir", "#2B2B2B");
            AjouterChoixCouleur(couleur, lien, "Bleu", "#0078D7");
            AjouterChoixCouleur(couleur, lien, "Vert", "#2E8B57");
            AjouterChoixCouleur(couleur, lien, "Rouge", "#C0392B");
            AjouterChoixCouleur(couleur, lien, "Orange", "#E67E22");
            AjouterChoixCouleur(couleur, lien, "Violet", "#8E44AD");
            AjouterChoixCouleur(couleur, lien, "Rose", "#D63384");
            menu.Items.Add(couleur);

            MenuItem epaisseur = new MenuItem { Header = "Épaisseur" };
            AjouterChoixEpaisseur(epaisseur, lien, "Fin", 1);
            AjouterChoixEpaisseur(epaisseur, lien, "Normal", 2);
            AjouterChoixEpaisseur(epaisseur, lien, "Épais", 3);
            AjouterChoixEpaisseur(epaisseur, lien, "Très épais", 5);
            menu.Items.Add(epaisseur);

            MenuItem trait = new MenuItem { Header = "Trait" };
            AjouterChoixTrait(trait, lien, "Plein", "plein");
            AjouterChoixTrait(trait, lien, "Tirets", "tirets");
            AjouterChoixTrait(trait, lien, "Pointillé", "points");
            menu.Items.Add(trait);

            MenuItem fleche = new MenuItem { Header = "Flèche" };
            AjouterChoixFleche(fleche, lien, "Aucune", "aucune");
            AjouterChoixFleche(fleche, lien, "Vers la cible", "simple");
            AjouterChoixFleche(fleche, lien, "Dans les deux sens", "double");
            menu.Items.Add(fleche);

            menu.Items.Add(new Separator());
            MenuItem supprimer = new MenuItem { Header = "Supprimer le lien", InputGestureText = "Suppr" };
            supprimer.Click += delegate
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerLien(this, lien));
            };
            menu.Items.Add(supprimer);

            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        private void AjouterChoixCouleur(MenuItem parent, Lien lien, string nom, string hexa)
        {
            MenuItem element = new MenuItem { Header = nom, IsChecked = lien.Couleur == hexa };
            Rectangle pastille = new Rectangle();
            pastille.Width = 14;
            pastille.Height = 14;
            pastille.RadiusX = 3;
            pastille.RadiusY = 3;
            pastille.Fill = new SolidColorBrush(LienVisuel.ConvertirCouleur(hexa));
            element.Icon = pastille;
            element.Click += delegate { ChangerStyleLien(lien, hexa, lien.Epaisseur, lien.Trait, lien.Fleche); };
            parent.Items.Add(element);
        }

        private void AjouterChoixEpaisseur(MenuItem parent, Lien lien, string nom, double valeur)
        {
            MenuItem element = new MenuItem { Header = nom, IsChecked = lien.Epaisseur == valeur };
            element.Click += delegate { ChangerStyleLien(lien, lien.Couleur, valeur, lien.Trait, lien.Fleche); };
            parent.Items.Add(element);
        }

        private void AjouterChoixTrait(MenuItem parent, Lien lien, string nom, string valeur)
        {
            MenuItem element = new MenuItem { Header = nom, IsChecked = lien.Trait == valeur };
            element.Click += delegate { ChangerStyleLien(lien, lien.Couleur, lien.Epaisseur, valeur, lien.Fleche); };
            parent.Items.Add(element);
        }

        private void AjouterChoixFleche(MenuItem parent, Lien lien, string nom, string valeur)
        {
            MenuItem element = new MenuItem { Header = nom, IsChecked = lien.Fleche == valeur };
            element.Click += delegate { ChangerStyleLien(lien, lien.Couleur, lien.Epaisseur, lien.Trait, valeur); };
            parent.Items.Add(element);
        }

        private void ChangerStyleLien(Lien lien, string couleur, double epaisseur, string trait, string fleche)
        {
            if (lien.Couleur == couleur && lien.Epaisseur == epaisseur && lien.Trait == trait && lien.Fleche == fleche)
                return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleLien(this, lien, couleur, epaisseur, trait, fleche));
        }

        // ------------------------------------------------------------------ zoom

        public void ZoomAvant() { ZoomVers(_echelle.ScaleX * 1.2, CentreVue()); }
        public void ZoomArriere() { ZoomVers(_echelle.ScaleX / 1.2, CentreVue()); }
        public void ZoomReinitialiser() { ZoomVers(1.0, CentreVue()); }

        private Point CentreVue()
        {
            return new Point(ActualWidth / 2, ActualHeight / 2);
        }

        private void ZoomVers(double cible, Point pivotEcran)
        {
            double ancienne = _echelle.ScaleX;
            double nouvelle = Math.Max(ZoomMin, Math.Min(ZoomMax, cible));
            if (nouvelle == ancienne) return;
            double facteur = nouvelle / ancienne;
            _translation.X = pivotEcran.X - facteur * (pivotEcran.X - _translation.X);
            _translation.Y = pivotEcran.Y - facteur * (pivotEcran.Y - _translation.Y);
            _echelle.ScaleX = nouvelle;
            _echelle.ScaleY = nouvelle;
            SignalerZoom();
            SignalerVue();
        }

        public void Recentrer()
        {
            if (Document.Noeuds.Count == 0)
            {
                _translation.X = 0;
                _translation.Y = 0;
                return;
            }
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (Noeud n in Document.Noeuds)
            {
                Rect rect = RectangleNoeud(n);
                minX = Math.Min(minX, rect.X);
                minY = Math.Min(minY, rect.Y);
                maxX = Math.Max(maxX, rect.Right);
                maxY = Math.Max(maxY, rect.Bottom);
            }
            _translation.X = ActualWidth / 2 - (minX + maxX) / 2 * _echelle.ScaleX;
            _translation.Y = ActualHeight / 2 - (minY + maxY) / 2 * _echelle.ScaleY;
            SignalerVue();
        }

        // ------------------------------------------------------------------ création / édition de nœuds

        private void CreerNoeudA(Point positionCanvas)
        {
            Noeud noeud = new Noeud();
            if (_styleParDefaut != null) _styleParDefaut.AppliquerA(noeud);
            noeud.X = positionCanvas.X;
            noeud.Y = positionCanvas.Y - 14;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            _noeudFraichementCree = noeud;

            NoeudVisuel visuel = _visuels[noeud];
            Selectionner(visuel);
            visuel.CommencerEdition();
            SignalerFormatCible();
        }

        private void NoeudEditionTerminee(NoeudVisuel visuel, ResultatEdition resultat)
        {
            Focus();
            bool fraichementCree = (_noeudFraichementCree == visuel.Modele);
            _noeudFraichementCree = null;

            if (string.IsNullOrWhiteSpace(visuel.Modele.TextePlat) && !visuel.Modele.EstImage)
            {
                // Une boîte vide n'a pas de raison d'exister (sauf si elle porte une image).
                if (fraichementCree)
                    Historique.AnnulerEtOublier();
                else
                    Historique.ExecuterEtPousser(new CommandeSupprimerNoeud(this, visuel.Modele, LiensAttaches(visuel.Modele)));
                SignalerFormatCible();
                return;
            }

            // Pour une boîte fraîche, la commande de création porte déjà le texte final :
            // une seule étape d'annulation pour « créer + taper ».
            bool change = !Noeud.SegmentsEgaux(resultat.AnciensSegments, resultat.NouveauxSegments)
                || !Noeud.SegmentsEgaux(resultat.AnciensSegmentsApres, resultat.NouveauxSegmentsApres)
                || resultat.AncienAlignement != resultat.NouvelAlignement;
            if (!fraichementCree && change)
                Historique.Pousser(new CommandeModifierTexteRiche(this, visuel.Modele,
                    resultat.AnciensSegments, resultat.AnciensSegmentsApres, resultat.AncienAlignement,
                    resultat.NouveauxSegments, resultat.NouveauxSegmentsApres, resultat.NouvelAlignement));
            else if (fraichementCree)
                SignalerModification();
            SignalerFormatCible();
        }

        private void Selectionner(NoeudVisuel visuel)
        {
            List<NoeudVisuel> ensemble = new List<NoeudVisuel>();
            if (visuel != null) ensemble.Add(visuel);
            SelectionnerEnsemble(ensemble, visuel);
        }

        private void SelectionnerEnsemble(List<NoeudVisuel> ensemble, NoeudVisuel principal)
        {
            if (_groupeActif != null)
            {
                _groupeActif = null;
                MettreAJourSurbrillanceGroupes();
            }
            if (_selectionLien != null)
            {
                _selectionLien.DefinirSelection(false);
                _selectionLien = null;
            }
            foreach (NoeudVisuel v in _selectionnes)
                if (!ensemble.Contains(v)) v.DefinirSelection(false);
            _selectionnes.Clear();
            foreach (NoeudVisuel v in ensemble)
            {
                if (_selectionnes.Contains(v)) continue;
                _selectionnes.Add(v);
                v.DefinirSelection(true);
            }
            _selection = principal != null && _selectionnes.Contains(principal) ? principal
                : (_selectionnes.Count > 0 ? _selectionnes[0] : null);
            MettreAJourGrip();
            SignalerFormatCible();
        }

        private void BasculerSelection(NoeudVisuel visuel)
        {
            List<NoeudVisuel> ensemble = new List<NoeudVisuel>(_selectionnes);
            if (ensemble.Contains(visuel)) ensemble.Remove(visuel);
            else ensemble.Add(visuel);
            SelectionnerEnsemble(ensemble, visuel);
        }

        private void SelectionnerLien(LienVisuel visuel)
        {
            if (_groupeActif != null)
            {
                _groupeActif = null;
                MettreAJourSurbrillanceGroupes();
            }
            if (_selection != null)
            {
                _selection.DefinirSelection(false);
                _selection = null;
            }
            if (_selectionLien == visuel) return;
            if (_selectionLien != null) _selectionLien.DefinirSelection(false);
            _selectionLien = visuel;
            if (_selectionLien != null) _selectionLien.DefinirSelection(true);
            SignalerFormatCible();
        }

        // ------------------------------------------------------------------ copier / coller / dupliquer

        // Presse-papier interne (partagé par toutes les cartes de la session) ; un marqueur
        // est posé dans le presse-papier système pour savoir s'il est toujours d'actualité.
        private const string FormatPressePapier = "Mentalo.Boites";
        private static readonly List<Noeud> _pressePapierNoeuds = new List<Noeud>();
        private static readonly List<Lien> _pressePapierLiens = new List<Lien>();
        private static int _collages;

        private static Noeud ClonerNoeud(Noeud source)
        {
            Noeud copie = new Noeud();
            copie.X = source.X;
            copie.Y = source.Y;
            copie.Segments = Noeud.CopieDe(source.Segments);
            copie.SegmentsApres = Noeud.CopieDe(source.SegmentsApres);
            copie.Alignement = source.Alignement;
            copie.LargeurFixe = source.LargeurFixe;
            copie.ImageDonnees = source.ImageDonnees; // octets immuables, partagés sans risque
            copie.ImageExtension = source.ImageExtension;
            copie.ImageLargeur = source.ImageLargeur;
            copie.ImageHauteur = source.ImageHauteur;
            StyleBoite.Depuis(source).AppliquerA(copie);
            return copie;
        }

        private static Lien ClonerLien(Lien source)
        {
            Lien copie = new Lien();
            copie.SourceId = source.SourceId;
            copie.CibleId = source.CibleId;
            copie.Couleur = source.Couleur;
            copie.Epaisseur = source.Epaisseur;
            copie.Trait = source.Trait;
            copie.Fleche = source.Fleche;
            return copie;
        }

        public void CopierSelection()
        {
            if (_selectionnes.Count == 0) return;
            _pressePapierNoeuds.Clear();
            _pressePapierLiens.Clear();
            _collages = 0;
            HashSet<string> ids = new HashSet<string>();
            foreach (NoeudVisuel v in _selectionnes)
                ids.Add(v.Modele.Id);
            foreach (NoeudVisuel v in _selectionnes)
            {
                Noeud copie = ClonerNoeud(v.Modele);
                copie.Id = v.Modele.Id; // conservé pour recoller les liens ; regénéré au collage
                _pressePapierNoeuds.Add(copie);
            }
            foreach (Lien lien in Document.Liens)
                if (ids.Contains(lien.SourceId) && ids.Contains(lien.CibleId))
                {
                    Lien copie = ClonerLien(lien);
                    _pressePapierLiens.Add(copie);
                }
            try
            {
                DataObject donnees = new DataObject();
                donnees.SetData(FormatPressePapier, true);
                string textePlat = "";
                foreach (Noeud n in _pressePapierNoeuds)
                    textePlat += (textePlat.Length > 0 ? "\n" : "") + n.TextePlat;
                donnees.SetText(textePlat);
                Clipboard.SetDataObject(donnees);
            }
            catch { }
        }

        /// <summary>Vrai si notre presse-papier interne est toujours le contenu « actuel »
        /// (rien n'a été copié ailleurs depuis).</summary>
        public bool PeutCollerBoites()
        {
            if (_pressePapierNoeuds.Count == 0) return false;
            try { return Clipboard.ContainsData(FormatPressePapier); }
            catch { return true; }
        }

        public void CollerBoites()
        {
            if (_mode == "lecture" || _pressePapierNoeuds.Count == 0) return;
            _collages++;
            CollerElements(_pressePapierNoeuds, _pressePapierLiens, 24 * _collages);
        }

        public void DupliquerSelection()
        {
            if (_mode == "lecture" || _selectionnes.Count == 0) return;
            List<Noeud> noeuds = new List<Noeud>();
            HashSet<string> ids = new HashSet<string>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                noeuds.Add(v.Modele);
                ids.Add(v.Modele.Id);
            }
            List<Lien> liens = new List<Lien>();
            foreach (Lien lien in Document.Liens)
                if (ids.Contains(lien.SourceId) && ids.Contains(lien.CibleId))
                    liens.Add(lien);
            CollerElements(noeuds, liens, 24);
        }

        private void CollerElements(List<Noeud> sources, List<Lien> liens, double decalage)
        {
            Dictionary<string, string> correspondance = new Dictionary<string, string>();
            List<Noeud> nouveaux = new List<Noeud>();
            List<ICommande> commandes = new List<ICommande>();
            foreach (Noeud source in sources)
            {
                Noeud copie = ClonerNoeud(source);
                copie.X = source.X + decalage;
                copie.Y = source.Y + decalage;
                correspondance[source.Id] = copie.Id;
                nouveaux.Add(copie);
                commandes.Add(new CommandeCreerNoeud(this, copie));
            }
            foreach (Lien lien in liens)
            {
                Lien copie = ClonerLien(lien);
                copie.SourceId = correspondance[lien.SourceId];
                copie.CibleId = correspondance[lien.CibleId];
                commandes.Add(new CommandeCreerLien(this, copie));
            }
            if (commandes.Count == 0) return;
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));

            List<NoeudVisuel> visuels = new List<NoeudVisuel>();
            foreach (Noeud n in nouveaux)
            {
                NoeudVisuel visuel;
                if (_visuels.TryGetValue(n, out visuel)) visuels.Add(visuel);
            }
            SelectionnerEnsemble(visuels, visuels.Count > 0 ? visuels[0] : null);
        }

        // ------------------------------------------------------------------ aligner / distribuer

        public void AlignerSelection(string type)
        {
            if (_mode == "lecture" || _selectionnes.Count < 2) return;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (NoeudVisuel v in _selectionnes)
            {
                Rect r = RectangleNoeud(v.Modele);
                minX = Math.Min(minX, r.X);
                minY = Math.Min(minY, r.Y);
                maxX = Math.Max(maxX, r.Right);
                maxY = Math.Max(maxY, r.Bottom);
            }
            List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                Rect r = RectangleNoeud(v.Modele);
                double nx = r.X, ny = r.Y;
                if (type == "gauche") nx = minX;
                else if (type == "centre-h") nx = (minX + maxX) / 2 - r.Width / 2;
                else if (type == "droite") nx = maxX - r.Width;
                else if (type == "haut") ny = minY;
                else if (type == "centre-v") ny = (minY + maxY) / 2 - r.Height / 2;
                else if (type == "bas") ny = maxY - r.Height;
                if (nx == r.X && ny == r.Y) continue;
                CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                d.Noeud = v.Modele;
                d.AncienX = r.X; d.AncienY = r.Y;
                d.NouveauX = nx; d.NouveauY = ny;
                deplacements.Add(d);
            }
            if (deplacements.Count == 0) return;
            CommandeDeplacerNoeuds commande = new CommandeDeplacerNoeuds(this, deplacements);
            commande.Executer();
            Historique.Pousser(commande);
        }

        public void DistribuerSelection(bool horizontal)
        {
            if (_mode == "lecture" || _selectionnes.Count < 3) return;
            List<NoeudVisuel> tries = new List<NoeudVisuel>(_selectionnes);
            tries.Sort(delegate(NoeudVisuel a, NoeudVisuel b)
            {
                Rect ra = RectangleNoeud(a.Modele), rb = RectangleNoeud(b.Modele);
                return horizontal ? ra.X.CompareTo(rb.X) : ra.Y.CompareTo(rb.Y);
            });
            double debut, fin, total = 0;
            Rect premier = RectangleNoeud(tries[0].Modele);
            Rect dernier = RectangleNoeud(tries[tries.Count - 1].Modele);
            debut = horizontal ? premier.X : premier.Y;
            fin = horizontal ? dernier.Right : dernier.Bottom;
            foreach (NoeudVisuel v in tries)
            {
                Rect r = RectangleNoeud(v.Modele);
                total += horizontal ? r.Width : r.Height;
            }
            double espace = (fin - debut - total) / (tries.Count - 1);
            List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
            double position = debut;
            foreach (NoeudVisuel v in tries)
            {
                Rect r = RectangleNoeud(v.Modele);
                double nx = horizontal ? position : r.X;
                double ny = horizontal ? r.Y : position;
                if (nx != r.X || ny != r.Y)
                {
                    CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                    d.Noeud = v.Modele;
                    d.AncienX = r.X; d.AncienY = r.Y;
                    d.NouveauX = nx; d.NouveauY = ny;
                    deplacements.Add(d);
                }
                position += (horizontal ? r.Width : r.Height) + espace;
            }
            if (deplacements.Count == 0) return;
            CommandeDeplacerNoeuds commande = new CommandeDeplacerNoeuds(this, deplacements);
            commande.Executer();
            Historique.Pousser(commande);
        }

        // ------------------------------------------------------------------ recherche & navigation

        public List<Noeud> Rechercher(string texte)
        {
            List<Noeud> resultats = new List<Noeud>();
            if (string.IsNullOrWhiteSpace(texte)) return resultats;
            string bas = texte.Trim().ToLower();
            foreach (Noeud n in Document.Noeuds)
                if (n.TextePlat.ToLower().Contains(bas)) resultats.Add(n);
            return resultats;
        }

        public void AllerAuNoeud(Noeud noeud)
        {
            NoeudVisuel visuel;
            if (!_visuels.TryGetValue(noeud, out visuel)) return;
            Selectionner(visuel);
            CentrerSur(Centre(RectangleNoeud(noeud)));
        }

        /// <summary>Sélectionne la boîte la plus proche dans la direction donnée (flèches).</summary>
        public void NaviguerSelection(int dx, int dy)
        {
            if (Document.Noeuds.Count == 0) return;
            if (_selection == null)
            {
                // Rien de sélectionné : la boîte la plus proche du centre de la vue.
                Point centreVue = new Point(
                    (ActualWidth / 2 - _translation.X) / _echelle.ScaleX,
                    (ActualHeight / 2 - _translation.Y) / _echelle.ScaleY);
                Noeud plusProche = null;
                double meilleure = double.MaxValue;
                foreach (Noeud n in Document.Noeuds)
                {
                    double distance = (Centre(RectangleNoeud(n)) - centreVue).Length;
                    if (distance < meilleure) { meilleure = distance; plusProche = n; }
                }
                if (plusProche != null) SelectionnerEtMontrer(plusProche);
                return;
            }
            Point origine = Centre(RectangleNoeud(_selection.Modele));
            Noeud meilleur = null;
            double meilleurScore = double.MaxValue;
            foreach (Noeud n in Document.Noeuds)
            {
                if (n == _selection.Modele) continue;
                Vector v = Centre(RectangleNoeud(n)) - origine;
                double avant = v.X * dx + v.Y * dy;
                if (avant <= 1) continue; // pas dans la direction demandée
                double travers = Math.Abs(v.X * dy) + Math.Abs(v.Y * dx);
                double score = avant + travers * 2;
                if (score < meilleurScore) { meilleurScore = score; meilleur = n; }
            }
            if (meilleur != null) SelectionnerEtMontrer(meilleur);
        }

        private void SelectionnerEtMontrer(Noeud noeud)
        {
            NoeudVisuel visuel;
            if (!_visuels.TryGetValue(noeud, out visuel)) return;
            Selectionner(visuel);
            // Recentrer seulement si la boîte sort de la vue.
            Rect vue = new Rect(-_translation.X / _echelle.ScaleX, -_translation.Y / _echelle.ScaleY,
                ActualWidth / _echelle.ScaleX, ActualHeight / _echelle.ScaleY);
            if (!vue.Contains(Centre(RectangleNoeud(noeud))))
                CentrerSur(Centre(RectangleNoeud(noeud)));
        }

        public void EditerSelection()
        {
            if (_selection == null || _mode == "lecture") return;
            _selection.CommencerEdition();
            SignalerFormatCible();
        }

        // ------------------------------------------------------------------ styles favoris

        public void EnregistrerStyleFavori()
        {
            if (_selection == null) return;
            string forme = StyleBoite.Depuis(_selection.Modele).Serialiser();
            if (Mentalo.Parametres.ParametresGlobaux.StylesFavoris.Contains(forme)) return;
            Mentalo.Parametres.ParametresGlobaux.StylesFavoris.Add(forme);
            Mentalo.Parametres.ParametresGlobaux.Enregistrer();
        }

        /// <summary>Applique un style favori à toutes les boîtes sélectionnées (une seule annulation).</summary>
        public void AppliquerStyleFavori(StyleBoite style)
        {
            if (_mode == "lecture" || _selectionnes.Count == 0) return;
            List<ICommande> commandes = new List<ICommande>();
            foreach (NoeudVisuel v in _selectionnes)
                if (!style.EgalA(v.Modele))
                    commandes.Add(new CommandeModifierStyleNoeud(this, v.Modele, style.Cloner()));
            if (commandes.Count == 0) return;
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));
        }

        // ------------------------------------------------------------------ groupes

        public Groupe GroupeDuNoeud(Noeud noeud)
        {
            foreach (Groupe groupe in Document.Groupes)
                if (groupe.Contient(noeud.Id)) return groupe;
            return null;
        }

        private List<NoeudVisuel> VisuelsDuGroupe(Groupe groupe)
        {
            List<NoeudVisuel> visuels = new List<NoeudVisuel>();
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                NoeudVisuel visuel;
                if (noeud != null && _visuels.TryGetValue(noeud, out visuel))
                    visuels.Add(visuel);
            }
            return visuels;
        }

        private Groupe TrouverGroupe(object origine)
        {
            foreach (KeyValuePair<Groupe, Rectangle> kv in _visuelsGroupes)
                if (ReferenceEquals(kv.Value, origine)) return kv.Key;
            return null;
        }

        private void CreerVisuelGroupe(Groupe groupe)
        {
            Rectangle cadre = new Rectangle();
            cadre.RadiusX = 10;
            cadre.RadiusY = 10;
            cadre.StrokeThickness = 1.5;
            cadre.StrokeDashArray = new DoubleCollection(new double[] { 5, 3 });
            Canvas.SetZIndex(cadre, -1); // sous les liens (0) et les boîtes (≥ 1)
            _canvas.Children.Add(cadre);
            _visuelsGroupes[groupe] = cadre;

            TextBlock titre = new TextBlock();
            titre.FontWeight = FontWeights.SemiBold;
            titre.FontSize = 12.5;
            titre.Padding = new Thickness(6, 0, 6, 0);
            // Le fond du canevas en arrière-plan : le titre « coupe » visuellement le trait.
            titre.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF5, 0xF0));
            titre.Visibility = Visibility.Collapsed;
            Canvas.SetZIndex(titre, -1);
            _canvas.Children.Add(titre);
            _titresGroupes[groupe] = titre;

            AppliquerStyleVisuelGroupe(groupe);
            MettreAJourCadreGroupe(groupe);
        }

        private void AppliquerStyleVisuelGroupe(Groupe groupe)
        {
            Color couleur = TexteRiche.CouleurDepuis(groupe.Couleur, Color.FromRgb(0x8A, 0x94, 0xA8));
            Rectangle cadre;
            if (_visuelsGroupes.TryGetValue(groupe, out cadre))
            {
                cadre.Stroke = new SolidColorBrush(couleur);
                cadre.Fill = new SolidColorBrush(Color.FromArgb(0x14, couleur.R, couleur.G, couleur.B));
                cadre.StrokeDashArray = groupe.BordureTrait == "plein" ? null
                    : groupe.BordureTrait == "points" ? new DoubleCollection(new double[] { 0.1, 2.5 })
                    : new DoubleCollection(new double[] { 5, 3 });
                cadre.StrokeDashCap = PenLineCap.Round;
                double epaisseur = groupe.BordureEpaisseur > 0 ? groupe.BordureEpaisseur : 1.5;
                cadre.StrokeThickness = groupe == _groupeActif ? epaisseur + 1 : epaisseur;
            }
            TextBlock titre;
            if (_titresGroupes.TryGetValue(groupe, out titre))
            {
                Color couleurTitre = string.IsNullOrEmpty(groupe.TitreCouleur)
                    ? couleur : TexteRiche.CouleurDepuis(groupe.TitreCouleur, couleur);
                titre.Foreground = new SolidColorBrush(couleurTitre);
                Color fondCanevas = CouleurFondEffective();
                titre.Background = new SolidColorBrush(string.IsNullOrEmpty(groupe.TitreFond)
                    ? fondCanevas : TexteRiche.CouleurDepuis(groupe.TitreFond, fondCanevas));
                titre.FontSize = groupe.TitreTaille > 0 ? groupe.TitreTaille : 12.5;
            }
        }

        private void MettreAJourCadreGroupe(Groupe groupe)
        {
            Rectangle cadre;
            if (!_visuelsGroupes.TryGetValue(groupe, out cadre)) return;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int membres = 0;
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud == null) continue;
                membres++;
                Rect rect = RectangleNoeud(noeud);
                minX = Math.Min(minX, rect.X);
                minY = Math.Min(minY, rect.Y);
                maxX = Math.Max(maxX, rect.Right);
                maxY = Math.Max(maxY, rect.Bottom);
            }
            if (membres == 0)
            {
                cadre.Visibility = Visibility.Collapsed;
                return;
            }
            cadre.Visibility = Visibility.Visible;
            Canvas.SetLeft(cadre, minX - MargeGroupe);
            Canvas.SetTop(cadre, minY - MargeGroupe);
            cadre.Width = Math.Max(maxX - minX + MargeGroupe * 2, groupe.LargeurFixe);
            cadre.Height = Math.Max(maxY - minY + MargeGroupe * 2, groupe.HauteurFixe);

            TextBlock titre;
            if (_titresGroupes.TryGetValue(groupe, out titre))
            {
                if (string.IsNullOrWhiteSpace(groupe.Titre))
                {
                    titre.Visibility = Visibility.Collapsed;
                }
                else
                {
                    titre.Text = groupe.Titre;
                    titre.Visibility = Visibility.Visible;
                    titre.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    bool enBas = groupe.TitrePosition != null && groupe.TitrePosition.StartsWith("bas");
                    bool aGauche = groupe.TitrePosition != null && groupe.TitrePosition.EndsWith("gauche");
                    double x = aGauche
                        ? Canvas.GetLeft(cadre) + 10
                        : Canvas.GetLeft(cadre) + cadre.Width - titre.DesiredSize.Width - 10;
                    double y = (enBas ? Canvas.GetTop(cadre) + cadre.Height : Canvas.GetTop(cadre))
                        - titre.DesiredSize.Height / 2;
                    Canvas.SetLeft(titre, x);
                    Canvas.SetTop(titre, y);
                }
            }
            if (_groupeActif == groupe) MettreAJourGrip();
            MettreAJourLiensDeId(groupe.Id); // le cadre peut être une extrémité de lien
        }

        private Size TailleMinimaleGroupe(Groupe groupe)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int membres = 0;
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud == null) continue;
                membres++;
                Rect rect = RectangleNoeud(noeud);
                minX = Math.Min(minX, rect.X);
                minY = Math.Min(minY, rect.Y);
                maxX = Math.Max(maxX, rect.Right);
                maxY = Math.Max(maxY, rect.Bottom);
            }
            if (membres == 0) return new Size(0, 0);
            return new Size(maxX - minX + MargeGroupe * 2, maxY - minY + MargeGroupe * 2);
        }

        public void DefinirTailleGroupe(Groupe groupe, double largeur, double hauteur)
        {
            groupe.LargeurFixe = largeur;
            groupe.HauteurFixe = hauteur;
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
        }

        private void MettreAJourGroupesDe(Noeud noeud)
        {
            foreach (Groupe groupe in Document.Groupes)
                if (groupe.Contient(noeud.Id)) MettreAJourCadreGroupe(groupe);
        }

        public void AjouterGroupe(Groupe groupe)
        {
            Document.Groupes.Add(groupe);
            CreerVisuelGroupe(groupe);
            SignalerModification();
        }

        public void RetirerGroupe(Groupe groupe)
        {
            Rectangle cadre;
            if (_visuelsGroupes.TryGetValue(groupe, out cadre))
            {
                _canvas.Children.Remove(cadre);
                _visuelsGroupes.Remove(groupe);
            }
            TextBlock titre;
            if (_titresGroupes.TryGetValue(groupe, out titre))
            {
                _canvas.Children.Remove(titre);
                _titresGroupes.Remove(groupe);
            }
            if (_groupeActif == groupe)
            {
                _groupeActif = null;
                MettreAJourGrip();
            }
            Document.Groupes.Remove(groupe);
            SignalerModification();
        }

        public void RetirerIdsDuGroupe(Groupe groupe, List<string> ids)
        {
            foreach (string id in ids)
                groupe.NoeudIds.Remove(id);
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
        }

        public void AjouterIdsAuGroupe(Groupe groupe, List<string> ids)
        {
            foreach (string id in ids)
                if (!groupe.NoeudIds.Contains(id)) groupe.NoeudIds.Add(id);
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
        }

        public void AppliquerStyleGroupe(Groupe groupe, string couleur, string trait, double epaisseur)
        {
            groupe.Couleur = couleur;
            groupe.BordureTrait = trait;
            groupe.BordureEpaisseur = epaisseur;
            AppliquerStyleVisuelGroupe(groupe);
            SignalerModification();
            SignalerFormatCible();
        }

        public void AppliquerStyleTitreGroupe(Groupe groupe, string couleurTitre, string fondTitre, double taille)
        {
            groupe.TitreCouleur = couleurTitre;
            groupe.TitreFond = fondTitre;
            groupe.TitreTaille = taille;
            AppliquerStyleVisuelGroupe(groupe);
            MettreAJourCadreGroupe(groupe); // la taille du titre change sa position
            SignalerModification();
            SignalerFormatCible();
        }

        public void AppliquerTitreGroupe(Groupe groupe, string titre, string position)
        {
            groupe.Titre = titre ?? "";
            groupe.TitrePosition = string.IsNullOrEmpty(position) ? "haut-droite" : position;
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
            SignalerFormatCible();
        }

        // --- API pour la barre des groupes -------------------------------------------------

        public Groupe GroupeSelectionne { get { return _groupeActif; } }

        public void ChangerCouleurGroupeSelection(string hexa)
        {
            if (_groupeActif == null || _mode == "lecture" || _groupeActif.Couleur == hexa) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, _groupeActif,
                hexa, _groupeActif.BordureTrait, _groupeActif.BordureEpaisseur));
        }

        public void ChangerTraitGroupeSelection(string trait)
        {
            if (_groupeActif == null || _mode == "lecture" || _groupeActif.BordureTrait == trait) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, _groupeActif,
                _groupeActif.Couleur, trait, _groupeActif.BordureEpaisseur));
        }

        public void ChangerEpaisseurGroupeSelection(double epaisseur)
        {
            if (_groupeActif == null || _mode == "lecture" || _groupeActif.BordureEpaisseur == epaisseur) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, _groupeActif,
                _groupeActif.Couleur, _groupeActif.BordureTrait, epaisseur));
        }

        /// <summary>Style du titre : null = valeur inchangée ; "" = « hériter » (cadre / fond du tableau).</summary>
        public void ChangerStyleTitreGroupeSelection(string couleurTitre, string fondTitre, double? taille)
        {
            if (_groupeActif == null || _mode == "lecture") return;
            string couleur = couleurTitre ?? _groupeActif.TitreCouleur;
            string fond = fondTitre ?? _groupeActif.TitreFond;
            double nouvelleTaille = taille.HasValue ? taille.Value : _groupeActif.TitreTaille;
            if (couleur == _groupeActif.TitreCouleur && fond == _groupeActif.TitreFond
                && nouvelleTaille == _groupeActif.TitreTaille) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleTitreGroupe(this, _groupeActif,
                couleur, fond, nouvelleTaille));
        }

        public void ChoisirCouleurTitreGroupePersonnalisee(bool pourLeFond)
        {
            if (_groupeActif == null || _mode == "lecture") return;
            string hexa = OuvrirSelecteurCouleur();
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            if (pourLeFond) ChangerStyleTitreGroupeSelection(null, hexa, null);
            else ChangerStyleTitreGroupeSelection(hexa, null, null);
            SignalerFormatCible();
        }

        public void ChoisirCouleurGroupePersonnalisee()
        {
            if (_groupeActif == null) return;
            string hexa = OuvrirSelecteurCouleur();
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            ChangerCouleurGroupeSelection(hexa);
            SignalerFormatCible();
        }

        public void ChangerTitreGroupeSelection(string titre, string position)
        {
            if (_groupeActif == null) return;
            if ((titre ?? "") == (_groupeActif.Titre ?? "") && position == _groupeActif.TitrePosition) return;
            Historique.ExecuterEtPousser(new CommandeModifierTitreGroupe(this, _groupeActif, titre ?? "", position));
        }

        public void ReajusterGroupeSelection()
        {
            if (_groupeActif == null) return;
            if (_groupeActif.LargeurFixe > 0 || _groupeActif.HauteurFixe > 0)
                Historique.ExecuterEtPousser(new CommandeRedimensionnerGroupe(this, _groupeActif,
                    _groupeActif.LargeurFixe, _groupeActif.HauteurFixe, 0, 0));
        }

        // --- API pour l'inspecteur ---------------------------------------------------------

        public Noeud NoeudSelectionne { get { return _selection == null ? null : _selection.Modele; } }
        public int NbSelectionnes { get { return _selectionnes.Count; } }
        public Rect RectangleDuNoeud(Noeud noeud) { return RectangleNoeud(noeud); }

        // --- API pour la barre des liens ---------------------------------------------------

        public Lien LienSelectionne
        {
            get { return _selectionLien == null ? null : _selectionLien.Modele; }
        }

        /// <summary>Modifie le style du lien sélectionné ; null = valeur inchangée.</summary>
        public void AppliquerStyleLienSelection(string couleur, double? epaisseur, string trait, string fleche)
        {
            if (_selectionLien == null || _mode == "lecture") return;
            Lien lien = _selectionLien.Modele;
            ChangerStyleLien(lien,
                couleur ?? lien.Couleur,
                epaisseur.HasValue ? epaisseur.Value : lien.Epaisseur,
                trait ?? lien.Trait,
                fleche ?? lien.Fleche);
        }

        public void ChoisirCouleurLienPersonnalisee()
        {
            if (_selectionLien == null) return;
            string hexa = OuvrirSelecteurCouleur();
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            AppliquerStyleLienSelection(hexa, null, null, null);
            SignalerFormatCible();
        }

        // --- Fond du tableau ---------------------------------------------------------------

        /// <summary>La couleur de fond réellement affichée : si l'utilisateur n'a pas changé
        /// la couleur par défaut, le thème sombre lui substitue un bleu très foncé
        /// (le motif n'entre pas dans ce critère).</summary>
        public Color CouleurFondEffective()
        {
            bool couleurParDefaut = string.Equals(Document.FondCouleur, "#F7F5F0",
                StringComparison.OrdinalIgnoreCase);
            if (couleurParDefaut && Mentalo.Parametres.ParametresGlobaux.ThemeSombre)
                return Color.FromRgb(0x16, 0x20, 0x2E);
            return TexteRiche.CouleurDepuis(Document.FondCouleur, Color.FromRgb(0xF7, 0xF5, 0xF0));
        }

        public void AppliquerFondDocument()
        {
            Background = ConstruireFond(CouleurFondEffective(), Document.FondMotif);
            foreach (KeyValuePair<Groupe, TextBlock> kv in _titresGroupes)
                AppliquerStyleVisuelGroupe(kv.Key);
        }

        private static Brush ConstruireFond(Color fond, string motif)
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
            dessin.Children.Add(new GeometryDrawing(new SolidColorBrush(fond), null,
                new RectangleGeometry(new Rect(0, 0, 24, 24))));
            if (motif == "points")
            {
                dessin.Children.Add(new GeometryDrawing(new SolidColorBrush(trait), null,
                    new EllipseGeometry(new Point(12, 12), 1.2, 1.2)));
            }
            else
            {
                GeometryGroup lignes = new GeometryGroup();
                lignes.Children.Add(new RectangleGeometry(new Rect(0, 0, 24, 1)));
                lignes.Children.Add(new RectangleGeometry(new Rect(0, 0, 1, 24)));
                dessin.Children.Add(new GeometryDrawing(new SolidColorBrush(trait), null, lignes));
            }
            DrawingBrush brosse = new DrawingBrush(dessin);
            brosse.TileMode = TileMode.Tile;
            brosse.Viewport = new Rect(0, 0, 24, 24);
            brosse.ViewportUnits = BrushMappingMode.Absolute;
            brosse.Freeze();
            return brosse;
        }

        // --- Listes à puces ----------------------------------------------------------------

        /// <summary>Bascule les puces ("puce" | "tiret" | "numero") sur toute la boîte
        /// sélectionnée. Une édition en cours est d'abord validée.</summary>
        public void BasculerPuces(string type)
        {
            if (_selection == null || _mode == "lecture") return;
            if (_selection.EnEdition)
            {
                // En édition : seulement les lignes sélectionnées.
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
            AppliquerFondDocument();
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
            {
                kv.Value.MettreAJourStyle();
                kv.Value.MettreAJour();
            }
            if (signaler) SignalerModification();
        }

        public void GrouperSelection()
        {
            if (_selectionnes.Count < 2) return;
            Groupe groupe = new Groupe();
            Dictionary<Groupe, List<string>> retraits = new Dictionary<Groupe, List<string>>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                groupe.NoeudIds.Add(v.Modele.Id);
                Groupe ancien = GroupeDuNoeud(v.Modele);
                if (ancien != null)
                {
                    if (!retraits.ContainsKey(ancien)) retraits[ancien] = new List<string>();
                    retraits[ancien].Add(v.Modele.Id);
                }
            }
            Historique.ExecuterEtPousser(new CommandeCreerGroupe(this, groupe, retraits));
        }

        public void DegrouperSelection()
        {
            List<Groupe> groupes = new List<Groupe>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                Groupe groupe = GroupeDuNoeud(v.Modele);
                if (groupe != null && !groupes.Contains(groupe)) groupes.Add(groupe);
            }
            if (groupes.Count == 0) return;
            if (groupes.Count == 1)
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerGroupe(this, groupes[0]));
                return;
            }
            List<ICommande> commandes = new List<ICommande>();
            foreach (Groupe groupe in groupes)
                commandes.Add(new CommandeSupprimerGroupe(this, groupe));
            Historique.ExecuterEtPousser(new CommandeComposite(commandes));
        }

        private void OuvrirMenuGroupe(Groupe groupe)
        {
            ContextMenu menu = new ContextMenu();
            MenuItem couleur = new MenuItem { Header = "Couleur du cadre" };
            for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                AjouterChoixCouleurGroupe(couleur, groupe, TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1]);
            if (Document.CouleursPersonnalisees.Count > 0)
            {
                couleur.Items.Add(new Separator());
                foreach (string hexa in Document.CouleursPersonnalisees)
                    AjouterChoixCouleurGroupe(couleur, groupe, hexa, hexa);
            }
            couleur.Items.Add(new Separator());
            MenuItem personnalisee = new MenuItem { Header = "Personnalisée…" };
            personnalisee.Click += delegate
            {
                string hexa = OuvrirSelecteurCouleur();
                if (hexa == null) return;
                if (Document.AjouterCouleurPersonnalisee(hexa))
                    SignalerModification();
                Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, groupe,
                    hexa, groupe.BordureTrait, groupe.BordureEpaisseur));
            };
            couleur.Items.Add(personnalisee);
            menu.Items.Add(couleur);

            MenuItem reajuster = new MenuItem
            {
                Header = "Réajuster au contenu",
                IsEnabled = groupe.LargeurFixe > 0 || groupe.HauteurFixe > 0
            };
            reajuster.Click += delegate
            {
                if (groupe.LargeurFixe > 0 || groupe.HauteurFixe > 0)
                    Historique.ExecuterEtPousser(new CommandeRedimensionnerGroupe(this, groupe,
                        groupe.LargeurFixe, groupe.HauteurFixe, 0, 0));
            };
            menu.Items.Add(reajuster);

            menu.Items.Add(new Separator());
            MenuItem degrouper = new MenuItem { Header = "Dégrouper", InputGestureText = "Ctrl+Maj+G" };
            degrouper.Click += delegate
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerGroupe(this, groupe));
            };
            menu.Items.Add(degrouper);
            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        private void AjouterChoixCouleurGroupe(MenuItem parent, Groupe groupe, string nom, string hexa)
        {
            MenuItem element = new MenuItem { Header = nom, IsChecked = groupe.Couleur == hexa };
            Rectangle pastille = new Rectangle
            {
                Width = 14, Height = 14, RadiusX = 3, RadiusY = 3,
                Fill = new SolidColorBrush(TexteRiche.CouleurDepuis(hexa, Colors.Gray))
            };
            element.Icon = pastille;
            string capture = hexa;
            element.Click += delegate
            {
                Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, groupe,
                    capture, groupe.BordureTrait, groupe.BordureEpaisseur));
            };
            parent.Items.Add(element);
        }

        // ------------------------------------------------------------------ images

        private static readonly string[] ExtensionsImage = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

        public static bool ExtensionImageValide(string chemin)
        {
            string extension = System.IO.Path.GetExtension(chemin).ToLowerInvariant();
            foreach (string valide in ExtensionsImage)
                if (extension == valide) return true;
            return false;
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
                    MessageBox.Show(Window.GetWindow(this),
                        string.Format("« {0} » dépasse la limite de 5 Mo ({1:0.0} Mo).",
                            informations.Name, informations.Length / 1048576.0),
                        "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                InsererImageDonnees(File.ReadAllBytes(chemin),
                    System.IO.Path.GetExtension(chemin).ToLowerInvariant(), centre);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this),
                    string.Format("Impossible d'importer l'image :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void InsererImageDonnees(byte[] donnees, string extension, Point centre)
        {
            if (_mode == "lecture") return;
            if (donnees.LongLength > TailleImageMax)
            {
                MessageBox.Show(Window.GetWindow(this), "L'image dépasse la limite de 5 Mo.",
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            BitmapImage image;
            try
            {
                image = NoeudVisuel.DecoderImage(donnees);
            }
            catch
            {
                MessageBox.Show(Window.GetWindow(this), "Format d'image illisible.",
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Noeud noeud = new Noeud();
            if (_styleParDefaut != null) _styleParDefaut.AppliquerA(noeud);
            noeud.ImageDonnees = donnees;
            noeud.ImageExtension = string.IsNullOrEmpty(extension) ? ".png" : extension;
            double facteur = Math.Min(1.0, 320.0 / Math.Max(image.PixelWidth, (double)image.PixelHeight));
            noeud.ImageLargeur = Math.Max(20, image.PixelWidth * facteur);
            noeud.ImageHauteur = Math.Max(20, image.PixelHeight * facteur);
            noeud.X = centre.X - noeud.ImageLargeur / 2;
            noeud.Y = centre.Y - noeud.ImageHauteur / 2;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            Selectionner(_visuels[noeud]);
        }

        public void CollerImage()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    System.Collections.Specialized.StringCollection fichiers = Clipboard.GetFileDropList();
                    Point centre = CentreCanvas();
                    double decalage = 0;
                    foreach (string fichier in fichiers)
                    {
                        if (!ExtensionImageValide(fichier)) continue;
                        InsererImage(fichier, new Point(centre.X + decalage, centre.Y + decalage));
                        decalage += 24;
                    }
                    return;
                }
                if (Clipboard.ContainsImage())
                {
                    BitmapSource source = Clipboard.GetImage();
                    if (source == null) return;
                    PngBitmapEncoder encodeur = new PngBitmapEncoder();
                    encodeur.Frames.Add(BitmapFrame.Create(source));
                    using (MemoryStream flux = new MemoryStream())
                    {
                        encodeur.Save(flux);
                        InsererImageDonnees(flux.ToArray(), ".png", CentreCanvas());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this),
                    string.Format("Impossible de coller l'image :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public Point CentreCanvas()
        {
            return new Point((ActualWidth / 2 - _translation.X) / _echelle.ScaleX,
                             (ActualHeight / 2 - _translation.Y) / _echelle.ScaleY);
        }

        private void DeposerFichiers(object expediteur, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] fichiers = (string[])e.Data.GetData(DataFormats.FileDrop);
            Point position = e.GetPosition(_canvas);
            // Déposée sur une boîte, la première image s'y attache ; le reste devient des boîtes.
            NoeudVisuel noeudCible = NoeudSousPoint(position);
            double decalage = 0;
            foreach (string fichier in fichiers)
            {
                if (!ExtensionImageValide(fichier)) continue;
                if (noeudCible != null)
                {
                    AttacherImage(noeudCible.Modele, fichier);
                    noeudCible = null;
                    continue;
                }
                InsererImage(fichier, new Point(position.X + decalage, position.Y + decalage));
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

        private void CommencerRedimension()
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
                CaptureMouse();
                return;
            }
            if (_selection == null) return;
            _redimensionEnCours = true;
            _noeudRedimensionne = _selection.Modele;
            if (_noeudRedimensionne.EstImage)
            {
                _departImgL = _noeudRedimensionne.ImageLargeur;
                _departImgH = _noeudRedimensionne.ImageHauteur;
                _chromeL = _selection.ActualWidth - _departImgL;
                _chromeH = _selection.ActualHeight - _departImgH;
            }
            else
            {
                _departLargeurFixe = _noeudRedimensionne.LargeurFixe;
            }
            CaptureMouse();
        }

        private void MettreAJourGrip()
        {
            if (_mode == "lecture")
            {
                _grip.Visibility = Visibility.Collapsed;
                return;
            }
            // Groupe entièrement sélectionné : la poignée redimensionne son cadre.
            if (_groupeActif != null)
            {
                Rectangle cadre;
                if (_visuelsGroupes.TryGetValue(_groupeActif, out cadre) && cadre.Visibility == Visibility.Visible)
                {
                    _grip.Cursor = Cursors.SizeNWSE;
                    Canvas.SetLeft(_grip, Canvas.GetLeft(cadre) + cadre.Width - 6);
                    Canvas.SetTop(_grip, Canvas.GetTop(cadre) + cadre.Height - 6);
                    _grip.Visibility = Visibility.Visible;
                    return;
                }
            }
            if (_selection != null && _selectionnes.Count == 1 && _selection.ActualWidth > 0 && !_selection.EnEdition)
            {
                _grip.Cursor = _selection.Modele.EstImage ? Cursors.SizeNWSE : Cursors.SizeWE;
                Canvas.SetLeft(_grip, _selection.Modele.X + _selection.ActualWidth - 6);
                Canvas.SetTop(_grip, _selection.Modele.Y + _selection.ActualHeight - 6);
                _grip.Visibility = Visibility.Visible;
            }
            else
            {
                _grip.Visibility = Visibility.Collapsed;
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
        public void InsererImageDansBoite()
        {
            if (_selection == null) return;
            NoeudVisuel cible = _selection;
            string chemin = null;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                Microsoft.Win32.OpenFileDialog dialogue = new Microsoft.Win32.OpenFileDialog();
                dialogue.Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Tous les fichiers (*.*)|*.*";
                if (dialogue.ShowDialog(Window.GetWindow(this)) == true)
                    chemin = dialogue.FileName;
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
                    MessageBox.Show(Window.GetWindow(this),
                        string.Format("« {0} » dépasse la limite de 5 Mo ({1:0.0} Mo).",
                            informations.Name, informations.Length / 1048576.0),
                        "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                byte[] donnees = File.ReadAllBytes(chemin);
                BitmapImage image = NoeudVisuel.DecoderImage(donnees);
                double facteur = Math.Min(1.0, 280.0 / Math.Max(image.PixelWidth, (double)image.PixelHeight));
                Historique.ExecuterEtPousser(new CommandeDefinirImage(this, noeud,
                    donnees, System.IO.Path.GetExtension(chemin).ToLowerInvariant(),
                    Math.Max(20, image.PixelWidth * facteur), Math.Max(20, image.PixelHeight * facteur)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this),
                    string.Format("Impossible d'importer l'image :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    BitmapImage image = NoeudVisuel.DecoderImage(noeud.ImageDonnees);
                    double facteur = Math.Min(1.0, 320.0 / Math.Max(image.PixelWidth, (double)image.PixelHeight));
                    double largeur = Math.Max(20, image.PixelWidth * facteur);
                    double hauteur = Math.Max(20, image.PixelHeight * facteur);
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
            // Une boîte-image sans texte redeviendrait vide : on la supprime plutôt.
            if (string.IsNullOrWhiteSpace(noeud.TextePlat))
            {
                SupprimerSelection();
                return;
            }
            Historique.ExecuterEtPousser(new CommandeDefinirImage(this, noeud, null, null, 0, 0));
        }

        // ------------------------------------------------------------------ style des boîtes

        /// <summary>Route une propriété vers le texte ou vers le style de la boîte.</summary>
        private void AppliquerPropriete(string propriete, object valeur)
        {
            if (propriete == "fond" || propriete == "bordure-couleur" || propriete == "bordure-epaisseur"
                || propriete == "bordure-trait" || propriete == "rayon")
                AppliquerStyleBoite(propriete, valeur);
            else
                AppliquerFormatTexte(propriete, valeur);
        }

        public void AppliquerStyleBoite(string propriete, object valeur)
        {
            if (_selection == null || _mode == "lecture") return;
            Noeud noeud = _selection.Modele;
            StyleBoite style = StyleBoite.Depuis(noeud);
            switch (propriete)
            {
                case "fond": style.Fond = (string)valeur; break;
                case "bordure-couleur": style.BordureCouleur = (string)valeur; break;
                case "bordure-epaisseur": style.BordureEpaisseur = (double)valeur; break;
                case "bordure-trait": style.BordureTrait = (string)valeur; break;
                case "rayon": style.Rayon = (double)valeur; break;
            }
            if (style.EgalA(noeud)) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleNoeud(this, noeud, style));
        }

        /// <summary>Appelé par les commandes d'annulation : applique un style complet.</summary>
        public void AppliquerStyleNoeud(Noeud noeud, StyleBoite style)
        {
            style.AppliquerA(noeud);
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.MettreAJourStyle();
            SignalerModification();
            SignalerFormatCible();
        }

        // ------------------------------------------------------------------ formatage du texte

        /// <summary>Applique un changement de mise en forme : à la sélection du RichTextBox si une
        /// édition est en cours, sinon à toute la boîte sélectionnée (avec commande d'annulation).
        /// Propriétés : police, taille, couleur, surlignage, gras, italique, souligne, barre,
        /// alignement. Pour les bascules, valeur null = inverser.</summary>
        public void AppliquerFormatTexte(string propriete, object valeur)
        {
            if (_selection == null || _mode == "lecture") return;

            if (_selection.EnEdition)
            {
                _selection.AppliquerFormatSelection(propriete, valeur);
                SignalerFormatCible();
                return;
            }

            Noeud noeud = _selection.Modele;
            if (noeud.Segments.Count == 0 && noeud.SegmentsApres.Count == 0 && propriete != "alignement") return;

            List<Segment> anciens = Noeud.CopieDe(noeud.Segments);
            List<Segment> anciensApres = Noeud.CopieDe(noeud.SegmentsApres);
            List<Segment> nouveaux = Noeud.CopieDe(noeud.Segments);
            List<Segment> nouveauxApres = Noeud.CopieDe(noeud.SegmentsApres);
            List<Segment> tous = new List<Segment>(nouveaux);
            tous.AddRange(nouveauxApres);
            string nouvelAlignement = noeud.Alignement;

            switch (propriete)
            {
                case "alignement":
                    // Hors édition : toute la boîte, lignes comprises (et l'image via le nœud).
                    nouvelAlignement = (string)valeur;
                    foreach (Segment s in tous) s.AlignementLigne = nouvelAlignement;
                    break;
                case "police":
                    foreach (Segment s in tous) s.Police = (string)valeur;
                    break;
                case "taille":
                    foreach (Segment s in tous) s.Taille = (double)valeur;
                    break;
                case "couleur":
                    foreach (Segment s in tous) s.Couleur = (string)valeur;
                    break;
                case "surlignage":
                    foreach (Segment s in tous) s.Surlignage = (string)valeur;
                    break;
                case "gras":
                    {
                        bool cible = !TousLesSegments(tous, "gras");
                        foreach (Segment s in tous) s.Gras = cible;
                    }
                    break;
                case "italique":
                    {
                        bool cible = !TousLesSegments(tous, "italique");
                        foreach (Segment s in tous) s.Italique = cible;
                    }
                    break;
                case "souligne":
                    {
                        bool cible = !TousLesSegments(tous, "souligne");
                        foreach (Segment s in tous) s.Souligne = cible;
                    }
                    break;
                case "barre":
                    {
                        bool cible = !TousLesSegments(tous, "barre");
                        foreach (Segment s in tous) s.Barre = cible;
                    }
                    break;
            }

            if (Noeud.SegmentsEgaux(anciens, nouveaux) && Noeud.SegmentsEgaux(anciensApres, nouveauxApres)
                && nouvelAlignement == noeud.Alignement)
                return;
            Historique.ExecuterEtPousser(new CommandeModifierTexteRiche(
                this, noeud, anciens, anciensApres, noeud.Alignement,
                nouveaux, nouveauxApres, nouvelAlignement));
        }

        private static bool TousLesSegments(List<Segment> segments, string propriete)
        {
            if (segments.Count == 0) return false;
            foreach (Segment s in segments)
            {
                bool valeur = propriete == "gras" ? s.Gras
                    : propriete == "italique" ? s.Italique
                    : propriete == "souligne" ? s.Souligne
                    : s.Barre;
                if (!valeur) return false;
            }
            return true;
        }

        public FormatCourant LireFormatCourant()
        {
            if (_selection == null)
            {
                FormatCourant inactif = new FormatCourant();
                inactif.Actif = false;
                return inactif;
            }
            if (_selection.EnEdition)
                return _selection.LireFormatSelection();

            Noeud noeud = _selection.Modele;
            FormatCourant etat = new FormatCourant();
            etat.Actif = true;
            etat.Alignement = noeud.Alignement;
            Segment premier = noeud.Segments.Count > 0 ? noeud.Segments[0]
                : (noeud.SegmentsApres.Count > 0 ? noeud.SegmentsApres[0] : new Segment());
            etat.Police = premier.Police;
            etat.Taille = premier.Taille;
            etat.Couleur = premier.Couleur;
            etat.Surlignage = premier.Surlignage;
            etat.Gras = TousLesSegments(noeud.Segments, "gras");
            etat.Italique = TousLesSegments(noeud.Segments, "italique");
            etat.Souligne = TousLesSegments(noeud.Segments, "souligne");
            etat.Barre = TousLesSegments(noeud.Segments, "barre");
            _selection.RemplirStyleBoite(etat);
            return etat;
        }

        /// <summary>Le menu contextuel de formatage — utilisé au clic droit sur une boîte et
        /// comme menu contextuel du RichTextBox pendant l'édition.</summary>
        private ContextMenu ConstruireMenuFormatage(bool pourEditeur)
        {
            FormatCourant etat = LireFormatCourant();
            ContextMenu menu = new ContextMenu();
            // Une boîte-image sans texte : on masque la mise en forme du texte,
            // mais l'alignement reste (il positionne aussi l'image).
            bool imageSeule = !pourEditeur && _selection != null && _selection.Modele.EstImage
                && string.IsNullOrWhiteSpace(_selection.Modele.TextePlat);

            if (pourEditeur && _selection != null)
            {
                IInputElement cible = _selection.CibleCommandes;
                menu.Items.Add(new MenuItem { Header = "Couper", Command = ApplicationCommands.Cut, CommandTarget = cible });
                menu.Items.Add(new MenuItem { Header = "Copier", Command = ApplicationCommands.Copy, CommandTarget = cible });
                menu.Items.Add(new MenuItem { Header = "Coller", Command = ApplicationCommands.Paste, CommandTarget = cible });
                menu.Items.Add(new Separator());
            }

            if (!imageSeule)
            {
            MenuItem police = new MenuItem { Header = "Police" };
            // Les polices système sont nombreuses : le sous-menu se remplit à l'ouverture.
            police.Items.Add(new MenuItem { Header = "…" });
            bool policesConstruites = false;
            police.SubmenuOpened += delegate
            {
                if (policesConstruites) return;
                policesConstruites = true;
                police.Items.Clear();
                foreach (string nom in TexteRiche.PolicesSysteme())
                {
                    MenuItem element = new MenuItem { Header = nom, IsChecked = etat.Police == nom };
                    element.FontFamily = new FontFamily(nom);
                    string capture = nom;
                    element.Click += delegate { AppliquerFormatTexte("police", capture); };
                    police.Items.Add(element);
                }
            };
            menu.Items.Add(police);

            MenuItem taille = new MenuItem { Header = "Taille" };
            foreach (double valeur in TexteRiche.Tailles)
            {
                MenuItem element = new MenuItem { Header = valeur.ToString(), IsChecked = etat.Taille == valeur };
                double capture = valeur;
                element.Click += delegate { AppliquerFormatTexte("taille", capture); };
                taille.Items.Add(element);
            }
            taille.Items.Add(new Separator());
            MenuItem tailleLibre = new MenuItem { Header = "Autre…" };
            tailleLibre.Click += delegate { DemanderTaillePersonnalisee(); };
            taille.Items.Add(tailleLibre);
            menu.Items.Add(taille);

            menu.Items.Add(new Separator());
            AjouterBascule(menu, "Gras", "gras", etat.Gras, "Ctrl+B");
            AjouterBascule(menu, "Italique", "italique", etat.Italique, "Ctrl+I");
            AjouterBascule(menu, "Souligné", "souligne", etat.Souligne, "Ctrl+U");
            AjouterBascule(menu, "Barré", "barre", etat.Barre, null);
            menu.Items.Add(new Separator());

            MenuItem couleur = new MenuItem { Header = "Couleur du texte" };
            for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                AjouterChoixCouleurTexte(couleur, "couleur", TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1], etat.Couleur);
            AjouterCouleursPersonnalisees(couleur, "couleur", etat.Couleur);
            menu.Items.Add(couleur);

            MenuItem surlignage = new MenuItem { Header = "Surlignage" };
            MenuItem aucun = new MenuItem { Header = "Aucun", IsChecked = etat.Surlignage == null };
            aucun.Click += delegate { AppliquerFormatTexte("surlignage", null); };
            surlignage.Items.Add(aucun);
            for (int i = 0; i < TexteRiche.Surlignages.GetLength(0); i++)
                AjouterChoixCouleurTexte(surlignage, "surlignage", TexteRiche.Surlignages[i, 0], TexteRiche.Surlignages[i, 1], etat.Surlignage);
            AjouterCouleursPersonnalisees(surlignage, "surlignage", etat.Surlignage);
            menu.Items.Add(surlignage);
            }

            MenuItem alignement = new MenuItem { Header = "Alignement" };
            AjouterChoixAlignement(alignement, "Gauche", "gauche", etat.Alignement);
            AjouterChoixAlignement(alignement, "Centre", "centre", etat.Alignement);
            AjouterChoixAlignement(alignement, "Droite", "droite", etat.Alignement);
            menu.Items.Add(alignement);

            if (!pourEditeur)
            {
                menu.Items.Add(new Separator());

                MenuItem fond = new MenuItem { Header = "Fond de la boîte" };
                AjouterChoixCouleurTexte(fond, "fond", "Blanc", "#FFFFFF", etat.FondBoite);
                AjouterChoixCouleurTexte(fond, "fond", "Transparent", "Transparent", etat.FondBoite);
                for (int i = 0; i < TexteRiche.Surlignages.GetLength(0); i++)
                    AjouterChoixCouleurTexte(fond, "fond", TexteRiche.Surlignages[i, 0], TexteRiche.Surlignages[i, 1], etat.FondBoite);
                for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                    AjouterChoixCouleurTexte(fond, "fond", TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1], etat.FondBoite);
                AjouterCouleursPersonnalisees(fond, "fond", etat.FondBoite);
                menu.Items.Add(fond);

                MenuItem bordure = new MenuItem { Header = "Bordure" };
                MenuItem bordureCouleur = new MenuItem { Header = "Couleur" };
                AjouterChoixCouleurTexte(bordureCouleur, "bordure-couleur", "Gris sable (défaut)", "#B8B2A7", etat.BordureCouleur);
                for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                    AjouterChoixCouleurTexte(bordureCouleur, "bordure-couleur", TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1], etat.BordureCouleur);
                AjouterCouleursPersonnalisees(bordureCouleur, "bordure-couleur", etat.BordureCouleur);
                bordure.Items.Add(bordureCouleur);

                MenuItem bordureEpaisseur = new MenuItem { Header = "Épaisseur" };
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Fine (1)", 1, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Normale (1,5)", 1.5, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Moyenne (2)", 2, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Épaisse (3)", 3, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Très épaisse (5)", 5, etat.BordureEpaisseur);
                bordure.Items.Add(bordureEpaisseur);

                MenuItem bordureTrait = new MenuItem { Header = "Trait" };
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Plein", "plein", etat.BordureTrait);
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Tirets", "tirets", etat.BordureTrait);
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Pointillé", "points", etat.BordureTrait);
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Aucune", "aucune", etat.BordureTrait);
                bordure.Items.Add(bordureTrait);
                menu.Items.Add(bordure);

                MenuItem coins = new MenuItem { Header = "Coins" };
                AjouterChoixNombre(coins, "rayon", "Carrés", 0, etat.Rayon);
                AjouterChoixNombre(coins, "rayon", "Arrondis", 5, etat.Rayon);
                AjouterChoixNombre(coins, "rayon", "Très arrondis", 12, etat.Rayon);
                menu.Items.Add(coins);

                menu.Items.Add(new Separator());
                MenuItem insererImageBoite = new MenuItem { Header = "Insérer une image dans la boîte…" };
                insererImageBoite.Click += delegate { InsererImageDansBoite(); };
                menu.Items.Add(insererImageBoite);
                if (_selection != null && _selection.Modele.EstImage)
                {
                    MenuItem retirerImage = new MenuItem { Header = "Retirer l'image" };
                    retirerImage.Click += delegate { RetirerImageDeBoite(); };
                    menu.Items.Add(retirerImage);
                }
                if (_selection != null)
                {
                    MenuItem reajuster = new MenuItem
                    {
                        Header = "Réajuster au contenu",
                        IsEnabled = _selection.Modele.LargeurFixe > 0 || _selection.Modele.EstImage
                    };
                    reajuster.Click += delegate { ReajusterBoite(); };
                    menu.Items.Add(reajuster);
                }

                MenuItem favoris = new MenuItem { Header = "Styles favoris" };
                List<string> formes = Mentalo.Parametres.ParametresGlobaux.StylesFavoris;
                for (int i = 0; i < formes.Count; i++)
                {
                    StyleBoite style = StyleBoite.Deserialiser(formes[i]);
                    if (style == null) continue;
                    MenuItem element = new MenuItem { Header = "Favori " + (i + 1) };
                    element.Icon = new Border
                    {
                        Width = 14, Height = 14,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(TexteRiche.CouleurDepuis(style.Fond, Colors.White)),
                        BorderBrush = new SolidColorBrush(TexteRiche.CouleurDepuis(style.BordureCouleur, Colors.Gray)),
                        BorderThickness = new Thickness(2)
                    };
                    StyleBoite capture = style;
                    element.Click += delegate { AppliquerStyleFavori(capture); };
                    favoris.Items.Add(element);
                }
                if (formes.Count > 0) favoris.Items.Add(new Separator());
                MenuItem enregistrerFavori = new MenuItem { Header = "Enregistrer le style de cette boîte" };
                enregistrerFavori.Click += delegate { EnregistrerStyleFavori(); };
                favoris.Items.Add(enregistrerFavori);
                if (formes.Count > 0)
                {
                    MenuItem retirer = new MenuItem { Header = "Retirer un favori" };
                    for (int i = 0; i < formes.Count; i++)
                    {
                        int indice = i;
                        MenuItem element = new MenuItem { Header = "Favori " + (i + 1) };
                        element.Click += delegate
                        {
                            if (indice < Mentalo.Parametres.ParametresGlobaux.StylesFavoris.Count)
                            {
                                Mentalo.Parametres.ParametresGlobaux.StylesFavoris.RemoveAt(indice);
                                Mentalo.Parametres.ParametresGlobaux.Enregistrer();
                            }
                        };
                        retirer.Items.Add(element);
                    }
                    favoris.Items.Add(retirer);
                }
                menu.Items.Add(favoris);

                if (_selectionnes.Count >= 2)
                {
                    MenuItem aligner = new MenuItem { Header = "Aligner et distribuer" };
                    AjouterChoixAlignerMenu(aligner, "Aligner à gauche", "gauche");
                    AjouterChoixAlignerMenu(aligner, "Centrer horizontalement", "centre-h");
                    AjouterChoixAlignerMenu(aligner, "Aligner à droite", "droite");
                    aligner.Items.Add(new Separator());
                    AjouterChoixAlignerMenu(aligner, "Aligner en haut", "haut");
                    AjouterChoixAlignerMenu(aligner, "Centrer verticalement", "centre-v");
                    AjouterChoixAlignerMenu(aligner, "Aligner en bas", "bas");
                    if (_selectionnes.Count >= 3)
                    {
                        aligner.Items.Add(new Separator());
                        MenuItem distribuerH = new MenuItem { Header = "Distribuer horizontalement" };
                        distribuerH.Click += delegate { DistribuerSelection(true); };
                        aligner.Items.Add(distribuerH);
                        MenuItem distribuerV = new MenuItem { Header = "Distribuer verticalement" };
                        distribuerV.Click += delegate { DistribuerSelection(false); };
                        aligner.Items.Add(distribuerV);
                    }
                    menu.Items.Add(aligner);
                }

                menu.Items.Add(new Separator());
                MenuItem grouper = new MenuItem
                {
                    Header = "Grouper les boîtes sélectionnées",
                    InputGestureText = "Ctrl+G",
                    IsEnabled = _selectionnes.Count >= 2
                };
                grouper.Click += delegate { GrouperSelection(); };
                menu.Items.Add(grouper);
                bool dansGroupe = _selection != null && GroupeDuNoeud(_selection.Modele) != null;
                MenuItem degrouper = new MenuItem
                {
                    Header = "Dégrouper",
                    InputGestureText = "Ctrl+Maj+G",
                    IsEnabled = dansGroupe
                };
                degrouper.Click += delegate { DegrouperSelection(); };
                menu.Items.Add(degrouper);

                menu.Items.Add(new Separator());
                MenuItem copierStyle = new MenuItem { Header = "Copier le style de la boîte" };
                copierStyle.Click += delegate
                {
                    if (_selection != null) _stylePresse = StyleBoite.Depuis(_selection.Modele);
                };
                menu.Items.Add(copierStyle);
                MenuItem collerStyle = new MenuItem { Header = "Coller le style", IsEnabled = _stylePresse != null };
                collerStyle.Click += delegate
                {
                    if (_selection == null || _stylePresse == null) return;
                    if (_stylePresse.EgalA(_selection.Modele)) return;
                    Historique.ExecuterEtPousser(
                        new CommandeModifierStyleNoeud(this, _selection.Modele, _stylePresse.Cloner()));
                };
                menu.Items.Add(collerStyle);
                MenuItem styleDefaut = new MenuItem { Header = "Utiliser ce style pour les nouvelles boîtes" };
                styleDefaut.Click += delegate
                {
                    if (_selection != null) _styleParDefaut = StyleBoite.Depuis(_selection.Modele);
                };
                menu.Items.Add(styleDefaut);

                menu.Items.Add(new Separator());
                MenuItem supprimer = new MenuItem { Header = "Supprimer la boîte", InputGestureText = "Suppr" };
                supprimer.Click += delegate { SupprimerSelection(); };
                menu.Items.Add(supprimer);
            }
            return menu;
        }

        private void AjouterChoixAlignerMenu(MenuItem parent, string entete, string type)
        {
            MenuItem element = new MenuItem { Header = entete };
            string capture = type;
            element.Click += delegate { AlignerSelection(capture); };
            parent.Items.Add(element);
        }

        private void AjouterChoixValeur(MenuItem parent, string propriete, string entete, string valeur, string actuel)
        {
            MenuItem element = new MenuItem { Header = entete, IsChecked = actuel == valeur };
            element.Click += delegate { AppliquerPropriete(propriete, valeur); };
            parent.Items.Add(element);
        }

        private void AjouterChoixNombre(MenuItem parent, string propriete, string entete, double valeur, double actuel)
        {
            MenuItem element = new MenuItem { Header = entete, IsChecked = Math.Abs(actuel - valeur) < 0.01 };
            element.Click += delegate { AppliquerPropriete(propriete, valeur); };
            parent.Items.Add(element);
        }

        private void AjouterBascule(ContextMenu menu, string entete, string propriete, bool coche, string geste)
        {
            MenuItem element = new MenuItem { Header = entete, IsChecked = coche };
            if (geste != null) element.InputGestureText = geste;
            element.Click += delegate { AppliquerFormatTexte(propriete, null); };
            menu.Items.Add(element);
        }

        private void AjouterChoixCouleurTexte(MenuItem parent, string propriete, string nom, string hexa, string actuel)
        {
            MenuItem element = new MenuItem { Header = nom, IsChecked = actuel == hexa };
            Rectangle pastille = new Rectangle();
            pastille.Width = 14;
            pastille.Height = 14;
            pastille.RadiusX = 3;
            pastille.RadiusY = 3;
            pastille.Fill = new SolidColorBrush(TexteRiche.CouleurDepuis(hexa, Colors.Gray));
            element.Icon = pastille;
            element.Click += delegate { AppliquerPropriete(propriete, hexa); };
            parent.Items.Add(element);
        }

        private void AjouterChoixAlignement(MenuItem parent, string entete, string valeur, string actuel)
        {
            MenuItem element = new MenuItem { Header = entete, IsChecked = actuel == valeur };
            element.Click += delegate { AppliquerFormatTexte("alignement", valeur); };
            parent.Items.Add(element);
        }

        private void AjouterCouleursPersonnalisees(MenuItem parent, string propriete, string actuel)
        {
            if (Document.CouleursPersonnalisees.Count > 0)
            {
                parent.Items.Add(new Separator());
                foreach (string hexa in Document.CouleursPersonnalisees)
                    AjouterChoixCouleurTexte(parent, propriete, hexa, hexa, actuel);
            }
            parent.Items.Add(new Separator());
            MenuItem personnalisee = new MenuItem { Header = "Personnalisée…" };
            string capture = propriete;
            personnalisee.Click += delegate { ChoisirCouleurPersonnalisee(capture); };
            parent.Items.Add(personnalisee);
        }

        /// <summary>Ouvre le sélecteur de couleur maison ; renvoie l'hexa choisi ou null.</summary>
        private string OuvrirSelecteurCouleur()
        {
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                return SelecteurCouleur.Choisir(Window.GetWindow(this), null);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
        }

        /// <summary>Sélecteur de couleur pour la cible courante ; la couleur choisie rejoint
        /// le répertoire du fichier.</summary>
        public void ChoisirCouleurPersonnalisee(string propriete)
        {
            if (_selection == null) return;
            NoeudVisuel cible = _selection;
            string hexa = OuvrirSelecteurCouleur();
            cible.RedonnerFocus();
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            AppliquerPropriete(propriete, hexa);
            SignalerFormatCible();
        }

        public void DemanderTaillePersonnalisee()
        {
            if (_selection == null) return;
            NoeudVisuel cible = _selection;
            double? taille;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                taille = DialogueSaisie.DemanderTaille(Window.GetWindow(this), LireFormatCourant().Taille);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
            cible.RedonnerFocus();
            if (taille.HasValue) AppliquerFormatTexte("taille", taille.Value);
        }

        private void SignalerFormatCible()
        {
            Action h = FormatCibleChange;
            if (h != null) h();
        }

        // ------------------------------------------------------------------ utilitaires

        private NoeudVisuel TrouverNoeud(object origine)
        {
            DependencyObject d = origine as DependencyObject;
            while (d != null && d != this)
            {
                NoeudVisuel n = d as NoeudVisuel;
                if (n != null) return n;
                d = ParentDe(d);
            }
            return null;
        }

        private LienVisuel TrouverLien(object origine)
        {
            DependencyObject d = origine as DependencyObject;
            while (d != null && d != this)
            {
                LienVisuel l = d as LienVisuel;
                if (l != null) return l;
                d = ParentDe(d);
            }
            return null;
        }

        private bool EstDansEditeur(object origine)
        {
            DependencyObject d = origine as DependencyObject;
            while (d != null && d != this)
            {
                if (d is System.Windows.Controls.Primitives.TextBoxBase) return true;
                d = ParentDe(d);
            }
            return false;
        }

        private static DependencyObject ParentDe(DependencyObject d)
        {
            if (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
                return VisualTreeHelper.GetParent(d);
            return LogicalTreeHelper.GetParent(d);
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
    }
}
