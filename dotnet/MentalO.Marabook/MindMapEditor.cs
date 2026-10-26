using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Marabook.App;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;
using Chrome = global::Marabook.App.Chrome; // le chrome de Marabook, pas celui de Mental-O
using Ui = Mentalo.Vue.Ui;

namespace MentalO.Marabook
{
    /// <summary>L'ÉDITEUR DE CARTE À LA SAUCE MARABOOK (26/10/2026, refondu deux
    /// fois le même jour) : en haut la barre des menus — Édition, Insertion,
    /// Disposition, Affichage, Fenêtres | Thème, Carte, Exporter | Rechercher —,
    /// dessous la barre des boîtes — Nouvelle boîte, Dupliquer, enfant, sœur, et
    /// les modes —, puis la toile et ses panneaux flottants (mini-carte, Tust,
    /// inspecteur de sélection, filtre, recherche). Les réglages de l'élément
    /// sélectionné (texte en édition, boîte, lien, groupe) vivent dans le Général
    /// du rail de Marabook (Inspector) ; le zoom dans sa barre d'état. Un clic
    /// sélectionne une boîte, un double-clic l'édite. Ctrl+K lie une fiche,
    /// Ctrl+L un écrit, et l'inspecteur (Texte) insère la tuile de n'importe quel
    /// élément du projet. Les raccourcis sont ceux de Préférences > Mental-O
    /// (dlc), moins ceux que Marabook écarte (Module.ActionsEcartees). La carte
    /// arrive en octets .tea et repart en octets .tea.</summary>
    public partial class MindMapEditor : IMindMapEditor
    {
        /// <summary>L'éditeur affiché (un seul par session Marabook).</summary>
        public static MindMapEditor Courant;

        private readonly IModuleHost _host;
        private readonly HabillageMarabook _habillage = new HabillageMarabook();
        private readonly Grid _root;
        private readonly Grid _centre;
        private readonly ToileVue _toile;
        private readonly BarresToile _barres;
        private readonly PanneauxFlottants _panneaux;
        private readonly Control _inspecteur;
        private Button _dupliquer, _enfant, _soeur;
        private string _itemId;
        private bool _dirty, _loading;
        private byte[] _derniersOctets;

        public event Action Changed;
        public event Action<double> ZoomChanged;

        public MindMapEditor(IModuleHost host)
        {
            _host = host;
            Courant = this;
            _toile = new ToileVue();
            _barres = new BarresToile(_toile, _habillage, true);
            _panneaux = new PanneauxFlottants(_toile, delegate { return _host.MainWindow; }, _habillage, false);
            _panneaux.HistoriqueAutorise = false; // Ctrl+Z suffit : pas de panneau Historique dans Marabook

            _root = new Grid { Background = Chrome.WindowBg };
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            Border menus = Barre(ConstruireBarreMenus(), Chrome.BarBg);
            Grid.SetRow(menus, 0);
            _root.Children.Add(menus);

            Border boites = Barre(ConstruireBarreBoites(), Chrome.BarBgLight);
            Grid.SetRow(boites, 1);
            _root.Children.Add(boites);

            _centre = new Grid();
            Grid.SetRow(_centre, 2);
            _root.Children.Add(_centre);
            _centre.Children.Add(_toile);
            _panneaux.AjouterA(_centre);

            // L'inspecteur du Général (rail de Marabook) : y cliquer ne termine pas
            // l'édition d'une boîte. Surtout pas le conteneur de la toile : la toile
            // en fait partie, et un clic sur son fond ne clôturait plus l'édition
            // (le TextBox restait visible, le texte jamais validé — 26/10 soir).
            _inspecteur = _barres.ConstruireInspecteur(ConstruireExtrasTexte());
            NoeudVisuel.ZonesFormatage.Clear();
            NoeudVisuel.ZonesFormatage.Add(_inspecteur);

            // Les liens vers les fiches, les écrits et les tuiles : rendus et ouverts par Marabook.
            NoeudVisuel.RenduHote = RendreHote;
            ToileVue.OuvrirHote = delegate(PieceJointe piece)
            {
                string id = piece.CibleHote;
                if (!string.IsNullOrEmpty(id)) _host.OpenItem(id);
            };
            _host.ProjectChanged += delegate { _toile.RafraichirLiensHote(); };

            _toile.DocumentModifie += delegate
            {
                if (_loading) return;
                _dirty = true;
                Action handler = Changed;
                if (handler != null) handler();
            };
            _toile.ZoomChange += delegate(double zoom)
            {
                Action<double> handler = ZoomChanged;
                if (handler != null) handler(zoom * 100);
            };
            _toile.FormatCibleChange += MettreAJourBoutons;
            _toile.ModeChange += MettreAJourBoutons;
            _root.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            _root.AddHandler(InputElement.KeyUpEvent, delegate(object s, KeyEventArgs e) { if (e.Key == Key.Space) _toile.ModePanClavier(false); }, RoutingStrategies.Tunnel);
            MettreAJourBoutons();
        }

        private static Border Barre(Control contenu, IBrush fond)
        {
            return new Border
            {
                Background = fond,
                BorderBrush = Chrome.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = contenu
            };
        }

        public Control View { get { return _root; } }

        /// <summary>Modifiée — ou en cours d'édition : le texte tapé n'est pas encore
        /// dans le modèle, Marabook doit demander Save (qui le valide).</summary>
        public bool IsDirty { get { return _dirty || _toile.EditionEnCours; } }
        public Control Inspector { get { return _inspecteur; } }

        public double Zoom { get { return _toile.Zoom * 100; } }

        public void SetZoom(double percent)
        {
            _toile.DefinirZoom(Math.Max(0.2, Math.Min(4.0, percent / 100.0)));
        }

        public void Load(string itemId, string title, byte[] tea)
        {
            _toile.ValiderEditionEnCours(); // la carte quittée ne garde pas un TextBox ouvert
            _loading = true;
            try
            {
                _itemId = itemId;
                _derniersOctets = tea;
                DocumentCarte document = null;
                if (tea != null && tea.Length > 0)
                {
                    try { document = Fichier.Charger(tea); }
                    catch (Exception error)
                    {
                        _host.Message("La carte n'a pas pu être lue (" + error.Message + ") : une carte vierge s'ouvre à la place.", "Cartes mentales", MessageButtons.OK, MessageIcon.Warning);
                    }
                }
                if (document == null) document = new DocumentCarte { NomAffichage = title ?? "" };
                if (string.IsNullOrEmpty(document.NomAffichage)) document.NomAffichage = title ?? "";
                _toile.ChargerDocument(document);
                _toile.Historique.Vider();
                _dirty = false;
                _barres.RemplirCombosCouleurs();
                _barres.MettreAJourFormat();
                _barres.MettreAJourModes();
                _panneaux.MettreAJourTout();
                MettreAJourBoutons();
            }
            finally { _loading = false; }
            _toile.Focus();
            LancerSondeSiDemandee();
        }

        public byte[] Save(byte[] previous)
        {
            _toile.ValiderEditionEnCours(); // le texte encore dans le TextBox rejoint le modèle avant la sérialisation
            byte[] bytes = Fichier.EnregistrerEnMemoire(_toile.Document, previous);
            _derniersOctets = bytes;
            _dirty = false;
            return bytes;
        }

        /// <summary>Le thème de Marabook a changé : le fond de la toile et les
        /// boîtes aux couleurs d'usine suivent (clair / sombre).</summary>
        public void ThemeChange()
        {
            _toile.AppliquerFondDocument();
            _panneaux.MettreAJourMiniCarte();
        }

        /// <summary>Les Paramètres (onglet Mental-O des Préférences) ont changé :
        /// le Tust, les panneaux, la vignette et les infobulles des gestes suivent.</summary>
        public void RafraichirParametres()
        {
            _panneaux.MettreAJourTout();
        }

        // ============================================================ les barres

        private Control ConstruireBarreMenus()
        {
            WrapPanel bar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 5, 10, 3) };
            bar.Children.Add(BoutonMenu("pencil-simple-line", "Édition", "Annuler, copier, dupliquer, rechercher, note, étiquettes, supprimer, grouper, plier", MenuEdition));
            bar.Children.Add(BoutonMenu("image-square-bold", "Insertion", "Image, fichier, lien web, lien vers une fiche ou un écrit, tuile, texte à puces", MenuInsertion));
            bar.Children.Add(BoutonMenu("align-justify", "Disposition", "Aligner et distribuer, empiler, ranger en arbre", MenuDisposition));
            bar.Children.Add(BoutonMenu("magnifying-glass-bold", "Affichage", "Zoom, recentrer, tout voir, mettre en avant", MenuAffichage));
            bar.Children.Add(BoutonMenu("grid", "Fenêtres", "Les panneaux flottants : Tust, mini-carte, inspecteur, filtre", MenuFenetres));
            bar.Children.Add(_habillage.Separateur());

            bar.Children.Add(BoutonMenu("palette", "Thème", "Thème de carte : fond, boîtes et liens d'un coup", MenuThemes));
            bar.Children.Add(BoutonMenu("gear-six-bold", "Carte", "Préférences de la carte, étiquettes, versions antérieures", MenuCarte));
            bar.Children.Add(BoutonMenu("file-arrow-down-bold", "Exporter", "La carte en image, PDF, dessin, texte, plan ou canevas", MenuExporter));
            bar.Children.Add(_habillage.Separateur());

            bar.Children.Add(Bouton("file-magnifying-glass", null, "Rechercher dans la carte (" + Geste("rechercher") + ")", Buttons.Look.Calm, delegate { _panneaux.OuvrirRecherche(); }));
            return bar;
        }

        private Control ConstruireBarreBoites()
        {
            StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 2) };
            bar.Children.Add(Bouton("plus-bold", "Nouvelle boîte", "Une boîte au centre de la vue (ou double-clic sur le fond)", Buttons.Look.Primary, delegate { _toile.CreerBoiteAuCentre(); }));
            _dupliquer = Bouton("copy-simple-bold", "Dupliquer", "Une copie des boîtes sélectionnées, posée à côté", Buttons.Look.Outline, delegate { _toile.DupliquerSelection(); });
            bar.Children.Add(_dupliquer);
            _enfant = Bouton("arrow-right-bold", null, "Une boîte enfant, reliée par une flèche (Tab)", Buttons.Look.Calm, delegate { _toile.CreerBoiteEnfant(); });
            bar.Children.Add(_enfant);
            _soeur = Bouton("arrow-up-right-bold", null, "Une boîte sœur sous la sélection (Entrée)", Buttons.Look.Calm, delegate { _toile.CreerBoiteSoeur(); });
            bar.Children.Add(_soeur);
            bar.Children.Add(_habillage.Separateur());
            bar.Children.Add(_barres.ConstruirePanneauModes(false));
            return bar;
        }

        /// <summary>Dupliquer, enfant et sœur veulent une boîte sélectionnée (et pas le mode lecture).</summary>
        private void MettreAJourBoutons()
        {
            if (_dupliquer == null) return;
            bool boite = _toile.NoeudSelectionne != null && _toile.Mode != "lecture";
            _dupliquer.IsEnabled = boite;
            _enfant.IsEnabled = boite;
            _soeur.IsEnabled = boite;
        }

        /// <summary>En tête de la section Texte de l'inspecteur : lien vers une
        /// fiche, vers un écrit, tuile d'un élément — comme dans l'éditeur de fiches.</summary>
        private Control ConstruireExtrasTexte()
        {
            WrapPanel ligne = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            ligne.Children.Add(Bouton("fiche-individual", null, "Lien vers une fiche (" + global::Marabook.App.Ui.Keys("Ctrl+K") + ") : la fiche choisie se pose en lien dans la boîte sélectionnée", Buttons.Look.Calm, delegate { LierFiche(); }));
            ligne.Children.Add(Bouton("document", null, "Lien vers un écrit (" + global::Marabook.App.Ui.Keys("Ctrl+L") + ") : l'écrit choisi se pose en lien dans la boîte sélectionnée", Buttons.Look.Calm, delegate { LierTexte(); }));
            ligne.Children.Add(Bouton("grid", null, "Insérer une tuile : n'importe quel élément du projet, montré comme sur son tableau (clic droit sur la tuile : normal ou condensé)", Buttons.Look.Calm, delegate { InsererTuile(); }));
            return ligne;
        }

        private static string Geste(string id)
        {
            string geste = ParametresGlobaux.EstMasquee(id) ? "" : ParametresGlobaux.GesteAffiche(id);
            return geste.Length == 0 ? "sans raccourci" : geste;
        }

        private Button Bouton(string icon, string label, string tooltip, Buttons.Look look, Action onClick)
        {
            Button button = label == null
                ? Buttons.Icon(icon, tooltip, Buttons.Compact, look)
                : Buttons.IconText(icon, label, tooltip, Buttons.Compact, look);
            button.Margin = new Thickness(0, 0, 4, 2);
            button.Click += delegate { onClick(); if (!_toile.EditionEnCours) _toile.Focus(); };
            return button;
        }

        /// <summary>Un bouton qui déroule un menu reconstruit à chaque ouverture (les coches suivent l'état).</summary>
        private Button BoutonMenu(string icon, string label, string tooltip, Func<List<Control>> contenu)
        {
            Button button = Buttons.IconText(icon, label + " ▾", tooltip, Buttons.Compact, Buttons.Look.Calm);
            button.Margin = new Thickness(0, 0, 4, 2);
            button.Click += delegate
            {
                ContextMenu menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedLeft };
                foreach (Control element in contenu()) menu.Items.Add(element);
                menu.Opened += delegate { NoeudVisuel.ProtectionFocus = true; };
                menu.Closed += delegate { NoeudVisuel.ProtectionFocus = false; if (!_toile.EditionEnCours) _toile.Focus(); };
                menu.Open(button);
            };
            return button;
        }

        private MenuItem Entree(string entete, string idAction, Action clic)
        {
            string geste = idAction == null || ParametresGlobaux.EstMasquee(idAction) ? null : ParametresGlobaux.GesteAffiche(idAction);
            return Ui.Element(entete, clic, false, string.IsNullOrEmpty(geste) ? null : geste);
        }

        private MenuItem Entree(string entete, Action clic)
        {
            return Ui.Element(entete, clic);
        }

        private MenuItem Coche(string entete, bool coche, Action clic)
        {
            return Ui.Element(entete, clic, coche);
        }

        private List<Control> MenuEdition()
        {
            List<Control> items = new List<Control>();
            MenuItem annuler = Entree("Annuler", "annuler", delegate { _toile.Historique.Annuler(); });
            annuler.IsEnabled = _toile.Historique.PeutAnnuler;
            items.Add(annuler);
            MenuItem retablir = Entree("Rétablir", "retablir", delegate { _toile.Historique.Refaire(); });
            retablir.IsEnabled = _toile.Historique.PeutRefaire;
            items.Add(retablir);
            items.Add(Ui.Separateur());
            items.Add(Entree("Copier les boîtes", "copier", delegate { _toile.CopierSelection(); }));
            items.Add(Entree("Coller", "coller", delegate { Coller(); }));
            items.Add(Entree("Dupliquer", delegate { _toile.DupliquerSelection(); }));
            items.Add(Ui.Separateur());
            items.Add(Entree("Rechercher…", "rechercher", delegate { _panneaux.OuvrirRecherche(); }));
            items.Add(Entree("Tout sélectionner", "tout-selectionner", delegate { _toile.SelectionnerTout(); }));
            items.Add(Ui.Separateur());
            items.Add(Entree("Modifier la boîte sélectionnée (double-clic)", delegate { _toile.EditerSelection(); }));
            items.Add(Entree("Note de la boîte…", "note", delegate { _toile.EditerNoteSelection(); }));
            items.Add(Entree("Étiquettes de la carte…", delegate { DialogueEtiquettes.Ouvrir(_host.MainWindow, _toile); }));
            items.Add(Ui.Separateur());
            items.Add(Entree("Supprimer la sélection", "supprimer", delegate { _toile.SupprimerSelection(); }));
            items.Add(Ui.Separateur());
            items.Add(Entree("Grouper", "grouper", delegate { _toile.GrouperSelection(); }));
            items.Add(Entree("Dégrouper", "degrouper", delegate { _toile.DegrouperSelection(); }));
            items.Add(Entree("Plier / déplier le groupe", "plier", delegate { _toile.BasculerPliageCible(); }));
            return items;
        }

        private List<Control> MenuInsertion()
        {
            List<Control> items = new List<Control>();
            items.Add(Entree("Image…", "inserer-image", delegate { _toile.InsererImageDansBoite(); }));
            items.Add(Entree("Fichier…", "inserer-fichier", delegate { _toile.InsererFichierDansBoite(); }));
            items.Add(Entree("Lien web…", "inserer-lien-web", delegate { _toile.InsererLienWeb(); }));
            items.Add(Ui.Separateur());
            items.Add(Ui.Element("Lien vers une fiche…", delegate { LierFiche(); }, false, global::Marabook.App.Ui.Keys("Ctrl+K")));
            items.Add(Ui.Element("Lien vers un écrit…", delegate { LierTexte(); }, false, global::Marabook.App.Ui.Keys("Ctrl+L")));
            items.Add(Entree("Tuile d'un élément…", delegate { InsererTuile(); }));
            items.Add(Ui.Separateur());
            items.Add(Entree("Importer un texte à puces…", delegate { ImporterTexteAPuces(); }));
            return items;
        }

        private List<Control> MenuDisposition()
        {
            List<Control> items = new List<Control>();
            MenuItem aligner = Ui.SousMenu("Aligner et distribuer");
            aligner.Items.Add(Entree("Aligner à gauche", delegate { _toile.AlignerSelection("gauche"); }));
            aligner.Items.Add(Entree("Centrer horizontalement", delegate { _toile.AlignerSelection("centre-h"); }));
            aligner.Items.Add(Entree("Aligner à droite", delegate { _toile.AlignerSelection("droite"); }));
            aligner.Items.Add(Ui.Separateur());
            aligner.Items.Add(Entree("Aligner en haut", delegate { _toile.AlignerSelection("haut"); }));
            aligner.Items.Add(Entree("Centrer verticalement", delegate { _toile.AlignerSelection("centre-v"); }));
            aligner.Items.Add(Entree("Aligner en bas", delegate { _toile.AlignerSelection("bas"); }));
            aligner.Items.Add(Ui.Separateur());
            aligner.Items.Add(Entree("Distribuer horizontalement", delegate { _toile.DistribuerSelection(true); }));
            aligner.Items.Add(Entree("Distribuer verticalement", delegate { _toile.DistribuerSelection(false); }));
            items.Add(aligner);
            items.Add(Entree("Empiler la sélection", "empiler", delegate { _toile.EmpilerSelection(); }));
            MenuItem ranger = Ui.SousMenu("Ranger en arbre");
            ranger.Items.Add(Entree("Horizontal (racine à gauche)", delegate { _toile.RangerEnArbre("horizontal"); }));
            ranger.Items.Add(Entree("Vertical (racine en haut)", delegate { _toile.RangerEnArbre("vertical"); }));
            ranger.Items.Add(Entree("Radial (racine au centre)", delegate { _toile.RangerEnArbre("radial"); }));
            items.Add(ranger);
            return items;
        }

        private List<Control> MenuAffichage()
        {
            List<Control> items = new List<Control>();
            items.Add(Entree("Zoom avant", "zoom-avant", delegate { _toile.ZoomAvant(); }));
            items.Add(Entree("Zoom arrière", "zoom-arriere", delegate { _toile.ZoomArriere(); }));
            items.Add(Entree("Zoom 100 %", "zoom-100", delegate { _toile.ZoomReinitialiser(); }));
            items.Add(Ui.Separateur());
            items.Add(Entree("Recentrer la carte", "recentrer", delegate { _toile.Recentrer(); }));
            items.Add(Entree("Tout voir", "zoom-tout", delegate { _toile.ZoomToutVoir(); }));
            items.Add(Entree("Zoom sur la sélection", "zoom-selection", delegate { _toile.ZoomSurSelection(); }));
            items.Add(Ui.Separateur());
            items.Add(Coche("Mettre en avant la sélection", _toile.FocusActif, delegate { _toile.BasculerFocusSelection(); }));
            return items;
        }

        private List<Control> MenuFenetres()
        {
            List<Control> items = new List<Control>();
            items.Add(Coche("Tust des raccourcis", ParametresGlobaux.TustVisible, delegate { ParametresGlobaux.TustVisible = !ParametresGlobaux.TustVisible; PanneauxChanges(); }));
            items.Add(Coche("Mini-carte", ParametresGlobaux.MiniCarteVisible, delegate { ParametresGlobaux.MiniCarteVisible = !ParametresGlobaux.MiniCarteVisible; PanneauxChanges(); }));
            items.Add(Coche("Inspecteur de sélection", ParametresGlobaux.InspecteurVisible, delegate { ParametresGlobaux.InspecteurVisible = !ParametresGlobaux.InspecteurVisible; PanneauxChanges(); }));
            items.Add(Coche("Filtre par étiquettes", ParametresGlobaux.FiltreVisible, delegate { ParametresGlobaux.FiltreVisible = !ParametresGlobaux.FiltreVisible; PanneauxChanges(); }));
            items.Add(Ui.Separateur());
            items.Add(Coche("Ombre sur les bords du tableau", ParametresGlobaux.VignetteTableau, delegate { ParametresGlobaux.VignetteTableau = !ParametresGlobaux.VignetteTableau; PanneauxChanges(); }));
            return items;
        }

        private void PanneauxChanges()
        {
            ParametresGlobaux.Enregistrer();
            _panneaux.MettreAJourTout();
        }

        private List<Control> MenuThemes()
        {
            List<Control> items = new List<Control>();
            foreach (ThemeCarte theme in ThemeCarte.Liste())
            {
                ThemeCarte choix = theme;
                MenuItem element = Entree(theme.Nom, delegate { AppliquerTheme(choix); });
                Ui.Infobulle(element, theme.Description);
                element.Icon = new Border
                {
                    Width = 14, Height = 14, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Palette.CouleurDepuis(theme.FondCouleur, Colors.White)),
                    BorderBrush = new SolidColorBrush(Palette.CouleurDepuis(theme.Boite.BordureCouleur, Colors.Gray)),
                    BorderThickness = new Thickness(1.5)
                };
                items.Add(element);
            }
            return items;
        }

        private List<Control> MenuCarte()
        {
            List<Control> items = new List<Control>();
            items.Add(Entree("Préférences de la carte…", delegate { OuvrirPreferencesFichier(); }));
            items.Add(Entree("Étiquettes de la carte…", delegate { DialogueEtiquettes.Ouvrir(_host.MainWindow, _toile); }));
            items.Add(Entree("Versions antérieures…", delegate { OuvrirVersions(); }));
            return items;
        }

        private List<Control> MenuExporter()
        {
            List<Control> items = new List<Control>();
            items.Add(Entree("Image PNG…", delegate { Exporter("png", "Image PNG"); }));
            items.Add(Entree("Document PDF…", delegate { Exporter("pdf", "Document PDF"); }));
            items.Add(Entree("Dessin SVG…", delegate { Exporter("svg", "Dessin SVG"); }));
            items.Add(Entree("Texte Markdown…", delegate { Exporter("md", "Texte Markdown"); }));
            items.Add(Entree("Plan OPML…", delegate { Exporter("opml", "Plan OPML"); }));
            items.Add(Entree("JSON Canvas (Obsidian)…", delegate { Exporter("canvas", "Canevas JSON Canvas"); }));
            items.Add(Ui.Separateur());
            items.Add(Entree("Carte Mental-O (.tea)…", delegate { Exporter("tea", "Carte Mental-O"); }));
            return items;
        }

        // ============================================================ liens vers Marabook : fiches, écrits, tuiles

        private async void LierFiche()
        {
            await LierElement("Lien vers une fiche", new string[] { "fiche" }, null);
        }

        private async void LierTexte()
        {
            await LierElement("Lien vers un écrit", new string[] { "texte" }, null);
        }

        private async void InsererTuile()
        {
            await LierElement("Insérer une tuile", null, "normal");
        }

        /// <summary>Le dialogue de choix de Marabook, puis le lien (ou la tuile) dans
        /// la boîte sélectionnée — ou dans une boîte neuve. L'édition en cours survit
        /// au dialogue (ProtectionFocus) et reprend le clavier ensuite.</summary>
        private async System.Threading.Tasks.Task LierElement(string titre, string[] genres, string affichage)
        {
            if (_toile.Mode == "lecture") return;
            HostItem choix;
            NoeudVisuel.ProtectionFocus = true;
            try { choix = await _host.PickItem(titre, genres); }
            finally { NoeudVisuel.ProtectionFocus = false; }
            if (choix == null)
            {
                if (!_toile.EditionEnCours) _toile.Focus();
                return;
            }
            _toile.AttacherLienHote(choix.Id, choix.Title, affichage);
        }

        /// <summary>Le rendu d'un lien vers Marabook dans une boîte : une ligne de
        /// lien (icône du genre, titre courant de l'élément), ou sa tuile — normale
        /// ou condensée. Un élément disparu : la ligne en grisé, « introuvable ».</summary>
        private Control RendreHote(PieceJointe piece)
        {
            HostItem item = _host.FindItem(piece.CibleHote);
            if (item == null) return LigneHote(null, (piece.Nom ?? "") + " (introuvable)", true);
            if (piece.Affichage != null)
            {
                Control tuile = _host.Tile(item.Id, 200, piece.Affichage == "condense");
                if (tuile != null) return tuile;
            }
            return LigneHote(item, item.Title, false);
        }

        private static string IconeDuGenre(string genre)
        {
            switch (genre)
            {
                case "fiche": return "fiche-individual";
                case "media": return "image-square-bold";
                case "livre": return "book-bold";
                case "plan": return "plan";
                case "dossier": return "folder-open";
                case "carte": return "diagram";
                case "gabarit": return "article-bold";
                default: return "document";
            }
        }

        private static Control LigneHote(HostItem item, string libelle, bool manquant)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            IBrush encre = manquant ? Chrome.FaintText : Chrome.Accent;
            Control icone = Icons.Make(item == null ? "document" : IconeDuGenre(item.Kind), 13, encre);
            if (icone != null)
            {
                icone.Margin = new Thickness(0, 1, 5, 0);
                icone.VerticalAlignment = VerticalAlignment.Center;
                ligne.Children.Add(icone);
            }
            ligne.Children.Add(new TextBlock
            {
                Text = libelle,
                Foreground = encre,
                FontFamily = TexteRiche.Police(Segment.PoliceDefaut),
                FontSize = Segment.TailleDefaut,
                TextDecorations = manquant ? null : TextDecorations.Underline,
                FontStyle = manquant ? FontStyle.Italic : FontStyle.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            return ligne;
        }

        // ============================================================ thèmes, dialogues, exports

        private async void AppliquerTheme(ThemeCarte theme)
        {
            bool auxExistants = false;
            if (_toile.Document.Noeuds.Count > 0)
            {
                MessageResult reponse = await _host.Message(
                    string.Format("Appliquer le thème « {0} » ?\n{1}\n\nOui : aussi aux boîtes, liens et groupes existants (annulable).\nNon : seulement au fond et aux nouveaux éléments.", theme.Nom, theme.Description),
                    "Thème de la carte", MessageButtons.YesNoCancel, MessageIcon.Question);
                if (reponse == MessageResult.Cancel) return;
                auxExistants = reponse == MessageResult.Yes;
            }
            _toile.AppliquerThemeCarte(theme, auxExistants);
        }

        private async void OuvrirPreferencesFichier()
        {
            if (await DialoguePreferencesFichier.Afficher(_host.MainWindow, _toile.Document, _toile.RectanglesNoeuds()))
            {
                _toile.AppliquerPreferencesDocument(true);
                _barres.RemplirCombosCouleurs();
                _barres.MettreAJourFormat();
            }
        }

        private async void OuvrirVersions()
        {
            byte[] octets = _derniersOctets;
            if (octets == null || octets.Length == 0)
            {
                await _host.Message("Cette carte n'a pas encore été enregistrée : aucune version antérieure.", "Versions antérieures", MessageButtons.OK, MessageIcon.Information);
                return;
            }
            DocumentCarte version = await DialogueVersions.Ouvrir(_host.MainWindow,
                delegate { return Fichier.ListerVersions(octets); },
                delegate(string nom) { return Fichier.ChargerVersion(octets, nom); });
            if (version == null) return;
            _toile.ChargerDocument(version);
            _dirty = true;
            Action handler = Changed;
            if (handler != null) handler();
        }

        private async void ImporterTexteAPuces()
        {
            string texte = await DialogueSaisie.DemanderTexteMultiligne(_host.MainWindow, "Importer un texte à puces",
                "Chaque ligne devient une boîte ; l'indentation (espaces ou tabulations)\nfait de la ligne l'enfant de la précédente, reliée par une flèche.");
            if (texte == null) return;
            List<Noeud> noeuds; List<Lien> liens;
            Export.ImporterTexteAPuces(texte, _toile.CentreCanvas(), out noeuds, out liens);
            if (noeuds.Count == 0) return;
            _toile.ImporterArbre(noeuds, liens);
        }

        private async void Exporter(string extension, string nomType)
        {
            string nom = SafeName(_toile.Document.NomAffichage);
            string chemin = await Ui.EnregistrerSous(_toile, "Exporter la carte", nom, extension, Ui.Type(nomType, "*." + extension), Ui.TypeTous);
            if (chemin == null) return;
            try
            {
                string erreur;
                switch (extension)
                {
                    case "png":
                        if (!_toile.ExporterPng(chemin, out erreur)) throw new Exception(erreur);
                        break;
                    case "pdf":
                        byte[] png = _toile.RendrePng(2.0, 8000.0, out erreur);
                        if (png == null) throw new Exception(erreur);
                        ExportPdf.Ecrire(png, chemin);
                        break;
                    case "svg": Export.ExporterSvg(_toile, chemin); break;
                    case "md": Export.ExporterMarkdown(_toile.Document, nom, chemin); break;
                    case "opml": Export.ExporterOpml(_toile.Document, nom, chemin); break;
                    case "canvas": JsonCanvas.Exporter(_toile, chemin); break;
                    case "tea": System.IO.File.WriteAllBytes(chemin, Fichier.EnregistrerEnMemoire(_toile.Document, null)); break;
                }
            }
            catch (Exception error)
            {
                await _host.Message("Export impossible : " + error.Message, "Cartes mentales", MessageButtons.OK, MessageIcon.Warning);
            }
        }

        private static string SafeName(string name)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in name ?? "")
                sb.Append(Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.Length == 0 ? "carte" : sb.ToString();
        }

        // ============================================================ clavier

        /// <summary>Les actions que l'éditeur de texte garde pour lui quand il a le focus.</summary>
        private static readonly HashSet<string> ActionsDeLEditeur = new HashSet<string>
        {
            "annuler", "retablir", "copier", "coller", "tout-selectionner", "supprimer"
        };

        /// <summary>Les raccourcis de la toile (ceux de Préférences > Mental-O (dlc)),
        /// quand la toile ou un de ses panneaux a le clavier — plus Ctrl+K (lien vers
        /// une fiche) et Ctrl+L (lien vers un écrit), les mêmes que dans l'éditeur de
        /// Marabook. Les actions de fichier et de fenêtre restent à Marabook.</summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!_root.IsKeyboardFocusWithin) return;
            bool editing = Ui.Ancetre<TextBox>(e.Source, _root) != null;
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                if (e.Key == Key.K) { LierFiche(); e.Handled = true; return; }
                if (e.Key == Key.L) { LierTexte(); e.Handled = true; return; }
            }
            if (!editing && e.KeyModifiers == KeyModifiers.None && _toile.IsKeyboardFocusWithin)
            {
                if (e.Key == Key.Tab) { if (_toile.CreerBoiteEnfant()) { e.Handled = true; return; } }
                else if (e.Key == Key.Enter) { if (_toile.CreerBoiteSoeur()) { e.Handled = true; return; } }
            }
            string id = ParametresGlobaux.ActionPour(e.Key, e.KeyModifiers);
            if (id == null && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                if (e.Key == Key.OemPlus || e.Key == Key.Add) id = "zoom-avant";
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) id = "zoom-arriere";
                else if (e.Key == Key.NumPad0) id = "zoom-100";
            }
            if (id != null && !(editing && ActionsDeLEditeur.Contains(id)) && Executer(id, editing))
            {
                e.Handled = true;
                return;
            }
            if (editing) return;
            if (e.Key == Key.Space) { _toile.ModePanClavier(true); e.Handled = true; return; }
            if (e.Key == Key.Escape)
            {
                if (_toile.FocusActif) _toile.AnnulerFocus();
                else if (_panneaux.RechercheVisible) _panneaux.FermerRecherche();
                else _toile.Deselectionner();
                e.Handled = true;
                return;
            }
            if (e.KeyModifiers == KeyModifiers.None && _toile.IsKeyboardFocusWithin)
            {
                if (e.Key == Key.Left) { _toile.NaviguerSelection(-1, 0); e.Handled = true; }
                else if (e.Key == Key.Right) { _toile.NaviguerSelection(1, 0); e.Handled = true; }
                else if (e.Key == Key.Up) { _toile.NaviguerSelection(0, -1); e.Handled = true; }
                else if (e.Key == Key.Down) { _toile.NaviguerSelection(0, 1); e.Handled = true; }
            }
        }

        private bool Executer(string id, bool editing)
        {
            switch (id)
            {
                case "annuler": _toile.Historique.Annuler(); return true;
                case "retablir": _toile.Historique.Refaire(); return true;
                case "copier": _toile.CopierSelection(); return true;
                case "coller": Coller(); return true;
                case "rechercher": _panneaux.OuvrirRecherche(); return true;
                case "supprimer": _toile.SupprimerSelection(); return true;
                case "grouper": _toile.GrouperSelection(); return true;
                case "degrouper": _toile.DegrouperSelection(); return true;
                case "empiler": _toile.EmpilerSelection(); return true;
                case "inserer-image": _toile.InsererImageDansBoite(); return true;
                case "inserer-fichier": _toile.InsererFichierDansBoite(); return true;
                case "inserer-lien-web": _toile.InsererLienWeb(); return true;
                case "tout-selectionner": _toile.SelectionnerTout(); return true;
                case "note": _toile.EditerNoteSelection(); return true;
                case "plier": _toile.BasculerPliageCible(); return true;
                case "zoom-tout": _toile.ZoomToutVoir(); return true;
                case "zoom-selection": _toile.ZoomSurSelection(); return true;
                case "focus": _toile.BasculerFocusSelection(); return true;
                case "zoom-avant": _toile.ZoomAvant(); return true;
                case "zoom-arriere": _toile.ZoomArriere(); return true;
                case "zoom-100": _toile.ZoomReinitialiser(); return true;
                case "recentrer": _toile.Recentrer(); return true;
                case "gras": if (editing && !_toile.EditionEnCours) return false; _toile.AppliquerFormatTexte("gras", null); return true;
                case "italique": if (editing && !_toile.EditionEnCours) return false; _toile.AppliquerFormatTexte("italique", null); return true;
                case "souligne": if (editing && !_toile.EditionEnCours) return false; _toile.AppliquerFormatTexte("souligne", null); return true;
                default: return false; // nouveau, ouvrir, enregistrer, imprimer, plein écran, modes, présentation : écartés (Module.ActionsEcartees)
            }
        }

        private async void Coller()
        {
            if (await _toile.PeutCollerBoites()) _toile.CollerBoites();
            else _toile.CollerImage();
        }
    }
}
