using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Marabook.App;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;
using Chrome = Marabook.App.Chrome; // le chrome de Marabook, pas celui de Mental-O
using Ui = Mentalo.Vue.Ui;

namespace MentalO.Marabook
{
    /// <summary>L'ÉDITEUR DE CARTE À LA SAUCE MARABOOK (26/10/2026) : la toile de
    /// Mental-O (le même canevas que l'application seule) sous une barre d'outils
    /// faite avec les boutons et les icônes de Marabook — boîtes, contenu, texte,
    /// groupes, disposition, modes, vue, thème, export, annulation — et les
    /// panneaux (mini-carte, inspecteur, historique, filtre, Tust) en
    /// flottants, comme dans Mental-O seul. Les raccourcis sont ceux de
    /// Fichier > Paramètres de Mental-O (onglet « Mental-O (dlc) » des
    /// Préférences) : dans Mental-O seul, c'est la fenêtre qui les porte. La
    /// carte arrive en octets .tea et repart en octets .tea
    /// (Fichier.EnregistrerEnMemoire garde l'historique des versions).</summary>
    public class MindMapEditor : IMindMapEditor
    {
        /// <summary>L'éditeur affiché (un seul par session Marabook).</summary>
        public static MindMapEditor Courant;

        private readonly IModuleHost _host;
        private readonly Grid _root;
        private readonly Grid _centre;
        private readonly ToileVue _toile;
        private readonly WrapPanel _bar;
        private readonly Border _barHost;
        private readonly List<ToggleButton> _modes = new List<ToggleButton>();
        private readonly TextBlock _zoomLabel;
        private Button _undo, _redo;
        private Border _miniCarte, _inspecteur, _historique, _filtre, _tust, _compteur, _recherche;
        private Canvas _miniCanvas;
        private TextBlock _inspecteurTexte, _historiqueTexte, _compteurTexte, _compteurRecherche;
        private StackPanel _filtreContenu, _tustContenu;
        private TextBox _champRecherche;
        private List<Noeud> _resultats = new List<Noeud>();
        private int _indiceResultat;
        private double _miniEchelle = 1;
        private Point _miniOrigine;
        private string _itemId;
        private bool _dirty, _loading, _presenting;
        private string _modeAvantPresentation;
        private byte[] _derniersOctets;

        public event Action Changed;

        public MindMapEditor(IModuleHost host)
        {
            _host = host;
            Courant = this;
            _root = new Grid { Background = Chrome.WindowBg };
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            _bar = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 6, 10, 6)
            };
            _barHost = new Border
            {
                Background = Chrome.BarBg,
                BorderBrush = Chrome.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = _bar
            };
            Grid.SetRow(_barHost, 0);
            _root.Children.Add(_barHost);

            _centre = new Grid();
            Grid.SetRow(_centre, 1);
            _root.Children.Add(_centre);

            _toile = new ToileVue();
            _centre.Children.Add(_toile);
            _toile.DocumentModifie += delegate
            {
                if (_loading) return;
                _dirty = true;
                Action handler = Changed;
                if (handler != null) handler();
                MettreAJourMiniCarte();
                MettreAJourInspecteur();
            };
            _toile.ZoomChange += delegate(double zoom) { _zoomLabel.Text = Math.Round(zoom * 100) + " %"; };
            _toile.ModeChange += SyncModes;
            _toile.Historique.Change += delegate { SyncHistory(); MettreAJourHistorique(); };
            _toile.VueChangee += MettreAJourMiniCarte;
            _toile.FormatCibleChange += MettreAJourInspecteur;
            _toile.EtiquettesChange += MettreAJourFiltre;
            _toile.DiapositiveChangee += delegate(int courante, int total)
            {
                if (_compteurTexte == null) return;
                _compteurTexte.Text = courante == 0
                    ? (total == 0 ? "Vue d'ensemble" : "Vue d'ensemble · " + total + " diapositive" + (total > 1 ? "s" : "") + " — → pour commencer")
                    : courante + " / " + total;
            };

            _zoomLabel = new TextBlock { Text = "100 %", Foreground = Chrome.SoftText, VerticalAlignment = VerticalAlignment.Center, MinWidth = 44, TextAlignment = TextAlignment.Center };
            BuildBar();
            BuildPanels();
            NoeudVisuel.ZoneFormatage = _barHost;
            _root.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            _root.AddHandler(InputElement.KeyUpEvent, delegate(object s, KeyEventArgs e) { if (e.Key == Key.Space) _toile.ModePanClavier(false); }, RoutingStrategies.Tunnel);
        }

        public Control View { get { return _root; } }
        public bool IsDirty { get { return _dirty; } }

        public void Load(string itemId, string title, byte[] tea)
        {
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
                SyncModes();
                SyncHistory();
                MettreAJourPanneaux();
            }
            finally { _loading = false; }
            _toile.Focus();
        }

        public byte[] Save(byte[] previous)
        {
            byte[] bytes = Fichier.EnregistrerEnMemoire(_toile.Document, previous);
            _derniersOctets = bytes;
            _dirty = false;
            return bytes;
        }

        /// <summary>Le thème de Marabook a changé : le fond de la toile suit.</summary>
        public void ThemeChange()
        {
            _toile.AppliquerFondDocument();
            MettreAJourMiniCarte();
        }

        // ============================================================ la barre

        private void BuildBar()
        {
            // — Boîtes.
            Button newBox = Buttons.IconText("plus-bold", "Nouvelle boîte", "Une boîte au centre de la vue (ou double-clic sur le fond)", Buttons.Compact, Buttons.Look.Primary);
            newBox.Click += delegate { _toile.CreerBoiteAuCentre(); };
            Add(newBox);
            Add(Action("arrow-right-bold", null, "Une boîte enfant, reliée par une flèche (Tab)", delegate { if (!_toile.CreerBoiteEnfant()) Hint(); }));
            Add(Action("arrow-up-right-bold", null, "Une boîte sœur sous la sélection (Entrée)", delegate { if (!_toile.CreerBoiteSoeur()) Hint(); }));
            Add(Action("pencil-simple-line", null, "Éditer le texte de la boîte (" + Geste("editer") + ")", delegate { _toile.EditerSelection(); }));
            Add(Action("file-text-bold", null, "Note de la boîte (" + Geste("note") + ")", delegate { _toile.EditerNoteSelection(); }));
            Add(Action("trash", null, "Supprimer la sélection (" + Geste("supprimer") + ")", delegate { _toile.SupprimerSelection(); }));
            Rule();

            // — Contenu.
            Add(Action("image-square-bold", null, "Insérer une image dans la boîte… (" + Geste("inserer-image") + ")", delegate { _toile.InsererImageDansBoite(); }));
            Add(Action("files-bold", null, "Lier un fichier à la boîte… (" + Geste("inserer-fichier") + ")", delegate { _toile.InsererFichierDansBoite(); }));
            Add(Action("connection", null, "Insérer un lien web… (" + Geste("inserer-lien-web") + ")", delegate { _toile.InsererLienWeb(); }));
            Rule();

            // — Texte.
            Add(Action("text-b-bold", null, "Gras (" + Geste("gras") + ")", delegate { _toile.AppliquerFormatTexte("gras", null); }));
            Add(Action("text-italic-bold", null, "Italique (" + Geste("italique") + ")", delegate { _toile.AppliquerFormatTexte("italique", null); }));
            Add(Action("text-underline-bold", null, "Souligné (" + Geste("souligne") + ")", delegate { _toile.AppliquerFormatTexte("souligne", null); }));
            Add(Action("text-strikethrough-bold", null, "Barré", delegate { _toile.AppliquerFormatTexte("barre", null); }));
            Add(Menu("text-align-left-bold", null, "Alignement du texte", new MenuItem[]
            {
                Entry("Gauche", delegate { _toile.AppliquerFormatTexte("alignement", "gauche"); }),
                Entry("Centré", delegate { _toile.AppliquerFormatTexte("alignement", "centre"); }),
                Entry("Droite", delegate { _toile.AppliquerFormatTexte("alignement", "droite"); })
            }));
            Add(Menu("list-bullets", null, "Listes : puces, tirets, numéros, cases à cocher", new MenuItem[]
            {
                Entry("Puces", delegate { _toile.BasculerPuces("puce"); }),
                Entry("Tirets", delegate { _toile.BasculerPuces("tiret"); }),
                Entry("Numéros", delegate { _toile.BasculerPuces("numero"); }),
                Entry("Cases à cocher", delegate { _toile.BasculerPuces("case"); })
            }));
            Add(Menu("palette", null, "Couleur et taille du texte", BuildTextMenu()));
            Rule();

            // — Groupes et disposition.
            Add(Action("folder-bold", null, "Grouper les boîtes sélectionnées (" + Geste("grouper") + ")", delegate { _toile.GrouperSelection(); }));
            Add(Action("folder-open", null, "Dégrouper (" + Geste("degrouper") + ")", delegate { _toile.DegrouperSelection(); }));
            Add(Action("caret-down-bold", null, "Plier / déplier le groupe (" + Geste("plier") + ")", delegate { _toile.BasculerPliageCible(); }));
            Add(Menu("align-justify", "Aligner", "Aligner et distribuer la sélection", new MenuItem[]
            {
                Entry("Aligner à gauche", delegate { _toile.AlignerSelection("gauche"); }),
                Entry("Centrer horizontalement", delegate { _toile.AlignerSelection("centre-h"); }),
                Entry("Aligner à droite", delegate { _toile.AlignerSelection("droite"); }),
                Entry("Aligner en haut", delegate { _toile.AlignerSelection("haut"); }),
                Entry("Centrer verticalement", delegate { _toile.AlignerSelection("centre-v"); }),
                Entry("Aligner en bas", delegate { _toile.AlignerSelection("bas"); }),
                Entry("Distribuer horizontalement", delegate { _toile.DistribuerSelection(true); }),
                Entry("Distribuer verticalement", delegate { _toile.DistribuerSelection(false); }),
                Entry("Empiler", delegate { _toile.EmpilerSelection(); })
            }));
            Add(Menu("git-branch", "Ranger", "Ranger la carte en arbre", new MenuItem[]
            {
                Entry("Horizontal (racine à gauche)", delegate { _toile.RangerEnArbre("horizontal"); }),
                Entry("Vertical (racine en haut)", delegate { _toile.RangerEnArbre("vertical"); }),
                Entry("Radial (racine au centre)", delegate { _toile.RangerEnArbre("radial"); })
            }));
            Rule();

            // — Modes.
            Mode("automatique", "Auto", "Sélection et déplacement, liens par les poignées (" + Geste("mode-automatique") + ")");
            Mode("deplacement", "Déplacer", "Ne fait que déplacer les boîtes (" + Geste("mode-deplacement") + ")");
            Mode("liens", "Liens", "Tirer des liens d'une boîte à l'autre (" + Geste("mode-liens") + ")");
            Mode("lecture", "Lecture", "Rien ne bouge : lire et naviguer (" + Geste("mode-lecture") + ")");
            Rule();

            // — Vue.
            Add(Action("minus", null, "Zoom arrière (" + Geste("zoom-arriere") + ")", delegate { _toile.ZoomArriere(); }));
            _bar.Children.Add(_zoomLabel);
            Add(Action("plus-bold", null, "Zoom avant (" + Geste("zoom-avant") + ")", delegate { _toile.ZoomAvant(); }));
            Add(Action("magnifying-glass-bold", null, "Tout voir (" + Geste("zoom-tout") + ")", delegate { _toile.ZoomToutVoir(); }));
            Add(Action("file-magnifying-glass", null, "Rechercher dans la carte (" + Geste("rechercher") + ")", delegate { OuvrirRecherche(); }));
            Add(Action("play-fill", "Présentation", "Diaporama de la carte, groupe par groupe (" + Geste("presentation") + ", Échap pour sortir)", delegate { BasculerPresentation(); }));
            Add(Menu("grid", "Fenêtres", "Les panneaux flottants : mini-carte, inspecteur, historique, filtre, Tust", new MenuItem[]
            {
                Entry("Mini-carte", delegate { ParametresGlobaux.MiniCarteVisible = !ParametresGlobaux.MiniCarteVisible; ParametresGlobaux.Enregistrer(); MettreAJourPanneaux(); }),
                Entry("Inspecteur de sélection", delegate { ParametresGlobaux.InspecteurVisible = !ParametresGlobaux.InspecteurVisible; ParametresGlobaux.Enregistrer(); MettreAJourPanneaux(); }),
                Entry("Historique", delegate { ParametresGlobaux.HistoriqueVisible = !ParametresGlobaux.HistoriqueVisible; ParametresGlobaux.Enregistrer(); MettreAJourPanneaux(); }),
                Entry("Filtre par étiquettes", delegate { ParametresGlobaux.FiltreVisible = !ParametresGlobaux.FiltreVisible; ParametresGlobaux.Enregistrer(); MettreAJourPanneaux(); }),
                Entry("Tust des raccourcis", delegate { ParametresGlobaux.TustVisible = !ParametresGlobaux.TustVisible; ParametresGlobaux.Enregistrer(); MettreAJourPanneaux(); })
            }));
            Rule();

            // — Thème, préférences de fichier, étiquettes, export, annulation.
            List<MenuItem> themes = new List<MenuItem>();
            foreach (ThemeCarte theme in ThemeCarte.Liste())
            {
                ThemeCarte themeRef = theme;
                themes.Add(Entry(theme.Nom + (string.IsNullOrEmpty(theme.Description) ? "" : " — " + theme.Description), delegate { AppliquerTheme(themeRef); }));
            }
            Add(Menu("palette", "Thème", "Thème de carte : fond, boîtes et liens d'un coup", themes.ToArray()));
            Add(Menu("gear-six-bold", null, "Préférences de la carte, étiquettes, versions antérieures", new MenuItem[]
            {
                Entry("Préférences de la carte…", delegate { OuvrirPreferencesFichier(); }),
                Entry("Étiquettes de la carte…", delegate { DialogueEtiquettes.Ouvrir(_host.MainWindow, _toile); }),
                Entry("Versions antérieures…", delegate { OuvrirVersions(); }),
                Entry("Importer un texte à puces…", delegate { ImporterTexteAPuces(); })
            }));
            Add(Menu("file-arrow-down-bold", "Exporter", "La carte en image, dessin, texte ou canevas", new MenuItem[]
            {
                Entry("Image PNG…", delegate { Exporter("png", "Image PNG"); }),
                Entry("Document PDF…", delegate { Exporter("pdf", "Document PDF"); }),
                Entry("Dessin SVG…", delegate { Exporter("svg", "Dessin SVG"); }),
                Entry("Texte Markdown…", delegate { Exporter("md", "Texte Markdown"); }),
                Entry("Plan OPML…", delegate { Exporter("opml", "Plan OPML"); }),
                Entry("JSON Canvas (Obsidian)…", delegate { Exporter("canvas", "Canevas JSON Canvas"); }),
                Entry("Carte Mental-O (.tea)…", delegate { Exporter("tea", "Carte Mental-O"); })
            }));
            _undo = Action("arrow-counter-clockwise", null, "Annuler (" + Geste("annuler") + ")", delegate { _toile.Historique.Annuler(); });
            Add(_undo);
            _redo = Action("arrow-counter-clockwise", null, "Rétablir (" + Geste("retablir") + ")", delegate { _toile.Historique.Refaire(); });
            Control redoIcon = _redo.Content as Control;
            if (redoIcon != null) redoIcon.RenderTransform = new ScaleTransform(-1, 1);
            Add(_redo);
        }

        private MenuItem[] BuildTextMenu()
        {
            List<MenuItem> items = new List<MenuItem>();
            MenuItem couleur = Ui.SousMenu("Couleur du texte");
            for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
            {
                string hexa = Palette.Couleurs[i, 1];
                MenuItem e = Entry(Palette.Couleurs[i, 0], delegate { _toile.AppliquerFormatTexte("couleur", hexa); });
                e.Icon = Ui.Pastille(hexa, 14);
                couleur.Items.Add(e);
            }
            couleur.Items.Add(Ui.Separateur());
            couleur.Items.Add(Entry("Personnalisée…", delegate { _toile.ChoisirCouleurPersonnalisee("couleur"); }));
            items.Add(couleur);
            MenuItem surlignage = Ui.SousMenu("Surlignage");
            surlignage.Items.Add(Entry("Aucun", delegate { _toile.AppliquerFormatTexte("surlignage", null); }));
            for (int i = 0; i < Palette.Surlignages.GetLength(0); i++)
            {
                string hexa = Palette.Surlignages[i, 1];
                MenuItem e = Entry(Palette.Surlignages[i, 0], delegate { _toile.AppliquerFormatTexte("surlignage", hexa); });
                e.Icon = Ui.Pastille(hexa, 14);
                surlignage.Items.Add(e);
            }
            items.Add(surlignage);
            MenuItem taille = Ui.SousMenu("Taille");
            foreach (double valeur in Palette.Tailles)
            {
                double capture = valeur;
                taille.Items.Add(Entry(valeur.ToString(), delegate { _toile.AppliquerFormatTexte("taille", capture); }));
            }
            taille.Items.Add(Ui.Separateur());
            taille.Items.Add(Entry("Autre…", delegate { _toile.DemanderTaillePersonnalisee(); }));
            items.Add(taille);
            MenuItem police = Ui.SousMenu("Police");
            bool construit = false;
            police.Items.Add(Entry("…", null));
            police.SubmenuOpened += delegate
            {
                if (construit) return;
                construit = true;
                police.Items.Clear();
                foreach (string nom in TexteRiche.PolicesSysteme())
                {
                    string capture = nom;
                    MenuItem e = Entry(nom, delegate { _toile.AppliquerFormatTexte("police", capture); });
                    e.FontFamily = TexteRiche.Police(nom);
                    police.Items.Add(e);
                }
            };
            items.Add(police);
            return items.ToArray();
        }

        private static string Geste(string id)
        {
            string geste = ParametresGlobaux.GesteAffiche(id);
            return geste.Length == 0 ? "sans raccourci" : geste;
        }

        private void Add(Control element)
        {
            element.Margin = new Thickness(0, 0, 4, 2);
            _bar.Children.Add(element);
        }

        private void Rule()
        {
            _bar.Children.Add(new Border { Width = 1, Height = 22, Background = Chrome.Border, Margin = new Thickness(4, 0, 8, 2), VerticalAlignment = VerticalAlignment.Center });
        }

        private Button Action(string icon, string label, string tooltip, Action onClick)
        {
            Button button = label == null
                ? Buttons.Icon(icon, tooltip, Buttons.Compact, Buttons.Look.Calm)
                : Buttons.IconText(icon, label, tooltip, Buttons.Compact, Buttons.Look.Outline);
            button.Focusable = false;
            button.Click += delegate { onClick(); _toile.Focus(); };
            return button;
        }

        private Button Menu(string icon, string label, string tooltip, MenuItem[] entries)
        {
            Button button = label == null
                ? Buttons.Icon(icon, tooltip, Buttons.Compact, Buttons.Look.Calm)
                : Buttons.IconText(icon, label, tooltip, Buttons.Compact, Buttons.Look.Outline);
            button.Focusable = false;
            ContextMenu menu = new ContextMenu { Placement = PlacementMode.Bottom };
            foreach (MenuItem entry in entries) menu.Items.Add(entry);
            menu.Opened += delegate { NoeudVisuel.ProtectionFocus = true; };
            menu.Closed += delegate { NoeudVisuel.ProtectionFocus = false; };
            button.Click += delegate { menu.Open(button); };
            return button;
        }

        private MenuItem Entry(string header, Action onClick)
        {
            MenuItem item = new MenuItem { Header = header };
            if (onClick != null) item.Click += delegate { onClick(); _toile.Focus(); };
            return item;
        }

        private void Mode(string mode, string label, string tooltip)
        {
            ToggleButton toggle = Buttons.TextToggle(label, tooltip, Buttons.Compact);
            toggle.Tag = mode;
            toggle.Focusable = false;
            toggle.Click += delegate { _toile.DefinirMode(mode); SyncModes(); _toile.Focus(); };
            _modes.Add(toggle);
            Add(toggle);
        }

        private void SyncModes()
        {
            foreach (ToggleButton toggle in _modes) toggle.IsChecked = (string)toggle.Tag == _toile.Mode;
        }

        private void SyncHistory()
        {
            if (_undo != null) _undo.IsEnabled = _toile.Historique.PeutAnnuler;
            if (_redo != null) _redo.IsEnabled = _toile.Historique.PeutRefaire;
        }

        private void Hint()
        {
            _host.Message("Sélectionnez d'abord une boîte : la nouvelle se place par rapport à elle.", "Cartes mentales", MessageButtons.OK, MessageIcon.Information);
        }

        // ============================================================ panneaux flottants

        private Border Panneau(HorizontalAlignment h, VerticalAlignment v, Control contenu)
        {
            return new Border
            {
                HorizontalAlignment = h,
                VerticalAlignment = v,
                Margin = new Thickness(12, 10, 12, 10),
                Background = Chrome.RaisedBg,
                BorderBrush = Chrome.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 7, 10, 8),
                IsVisible = false,
                Child = contenu
            };
        }

        private static TextBlock Titre(string texte)
        {
            return new TextBlock { Text = texte, FontWeight = FontWeight.Bold, FontSize = 12, Foreground = Chrome.Ink, Margin = new Thickness(0, 0, 0, 4) };
        }

        private void BuildPanels()
        {
            StackPanel mini = new StackPanel();
            mini.Children.Add(Titre("Mini-carte"));
            _miniCanvas = new Canvas { Width = 166, Height = 106, Background = Chrome.CardBg, ClipToBounds = true };
            _miniCanvas.PointerPressed += delegate(object s, PointerPressedEventArgs e)
            {
                Point p = e.GetPosition(_miniCanvas);
                _toile.CentrerSur(new Point(_miniOrigine.X + p.X / _miniEchelle, _miniOrigine.Y + p.Y / _miniEchelle));
            };
            mini.Children.Add(_miniCanvas);
            _miniCarte = Panneau(HorizontalAlignment.Left, VerticalAlignment.Bottom, mini);
            _centre.Children.Add(_miniCarte);

            StackPanel insp = new StackPanel();
            insp.Children.Add(Titre("Inspecteur"));
            _inspecteurTexte = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 220, Foreground = Chrome.SoftText };
            insp.Children.Add(_inspecteurTexte);
            _inspecteur = Panneau(HorizontalAlignment.Right, VerticalAlignment.Bottom, insp);
            _centre.Children.Add(_inspecteur);

            StackPanel histo = new StackPanel();
            histo.Children.Add(Titre("Historique"));
            _historiqueTexte = new TextBlock { FontSize = 12, Foreground = Chrome.SoftText };
            histo.Children.Add(_historiqueTexte);
            _historique = Panneau(HorizontalAlignment.Left, VerticalAlignment.Top, histo);
            _centre.Children.Add(_historique);

            _filtreContenu = new StackPanel { MinWidth = 170 };
            _filtre = Panneau(HorizontalAlignment.Left, VerticalAlignment.Top, _filtreContenu);
            _centre.Children.Add(_filtre);

            _tustContenu = new StackPanel();
            _tust = Panneau(HorizontalAlignment.Right, VerticalAlignment.Top, _tustContenu);
            _tust.MaxWidth = 290;
            _centre.Children.Add(_tust);

            _compteurTexte = new TextBlock { Foreground = Chrome.SoftText, FontSize = 12 };
            _compteur = Panneau(HorizontalAlignment.Right, VerticalAlignment.Bottom, _compteurTexte);
            _compteur.Padding = new Thickness(10, 4, 10, 4);
            ToolTip.SetTip(_compteur, "→ / Espace : diapositive suivante · ← : précédente · Début : vue d'ensemble · Échap : quitter");
            _centre.Children.Add(_compteur);

            // La recherche, en haut au centre.
            StackPanel rech = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            _champRecherche = new TextBox { Width = 200, VerticalAlignment = VerticalAlignment.Center };
            _champRecherche.PropertyChanged += delegate(object s, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == TextBox.TextProperty) ActualiserRecherche(true);
            };
            _champRecherche.AddHandler(InputElement.KeyDownEvent, delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { NaviguerRecherche((e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1); e.Handled = true; }
                else if (e.Key == Key.Escape) { FermerRecherche(); e.Handled = true; }
            }, RoutingStrategies.Tunnel);
            rech.Children.Add(_champRecherche);
            _compteurRecherche = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 32, TextAlignment = TextAlignment.Center, Foreground = Chrome.SoftText };
            rech.Children.Add(_compteurRecherche);
            Button prec = Buttons.Icon("caret-left-bold", "Occurrence précédente (Maj+Entrée)", Buttons.Compact, Buttons.Look.Calm);
            prec.Click += delegate { NaviguerRecherche(-1); };
            rech.Children.Add(prec);
            Button suiv = Buttons.Icon("caret-right-bold", "Occurrence suivante (Entrée)", Buttons.Compact, Buttons.Look.Calm);
            suiv.Click += delegate { NaviguerRecherche(1); };
            rech.Children.Add(suiv);
            Button fermer = Buttons.Text("✕", "Fermer (Échap)", Buttons.Compact, Buttons.Look.Calm);
            fermer.Click += delegate { FermerRecherche(); };
            rech.Children.Add(fermer);
            _recherche = Panneau(HorizontalAlignment.Center, VerticalAlignment.Top, rech);
            _centre.Children.Add(_recherche);
        }

        private void MettreAJourPanneaux()
        {
            _miniCarte.IsVisible = ParametresGlobaux.MiniCarteVisible;
            _inspecteur.IsVisible = ParametresGlobaux.InspecteurVisible;
            _historique.IsVisible = ParametresGlobaux.HistoriqueVisible;
            _filtre.IsVisible = ParametresGlobaux.FiltreVisible;
            _tust.IsVisible = ParametresGlobaux.TustVisible;
            if (!ParametresGlobaux.FiltreVisible && _toile.FiltreActif) _toile.DefinirFiltreEtiquettes(null, false);
            MettreAJourMiniCarte();
            MettreAJourInspecteur();
            MettreAJourHistorique();
            MettreAJourFiltre();
            MettreAJourTust();
        }

        private void MettreAJourMiniCarte()
        {
            if (_miniCanvas == null || !_miniCarte.IsVisible) return;
            _miniCanvas.Children.Clear();
            double zoom = _toile.Zoom;
            if (zoom <= 0 || _toile.Bounds.Width <= 0) return;
            Point origine = _toile.OrigineVue;
            Rect vue = new Rect(-origine.X / zoom, -origine.Y / zoom, _toile.Bounds.Width / zoom, _toile.Bounds.Height / zoom);
            Rect monde = vue;
            List<Rect> rectangles = _toile.RectanglesNoeuds();
            foreach (Rect r in rectangles) monde = monde.Union(r);
            if (monde.Width < 1 || monde.Height < 1) return;
            _miniEchelle = Math.Min(_miniCanvas.Width / monde.Width, _miniCanvas.Height / monde.Height);
            _miniOrigine = monde.TopLeft;
            foreach (Rect r in rectangles)
            {
                Avalonia.Controls.Shapes.Rectangle forme = new Avalonia.Controls.Shapes.Rectangle
                {
                    Width = Math.Max(2, r.Width * _miniEchelle),
                    Height = Math.Max(2, r.Height * _miniEchelle),
                    Fill = Chrome.FaintText,
                    RadiusX = 1,
                    RadiusY = 1
                };
                Canvas.SetLeft(forme, (r.X - monde.X) * _miniEchelle);
                Canvas.SetTop(forme, (r.Y - monde.Y) * _miniEchelle);
                _miniCanvas.Children.Add(forme);
            }
            Avalonia.Controls.Shapes.Rectangle cadre = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = Math.Max(4, vue.Width * _miniEchelle),
                Height = Math.Max(4, vue.Height * _miniEchelle),
                Stroke = Chrome.Accent,
                StrokeThickness = 1,
                Fill = Chrome.AccentTint,
                Opacity = 0.8
            };
            Canvas.SetLeft(cadre, (vue.X - monde.X) * _miniEchelle);
            Canvas.SetTop(cadre, (vue.Y - monde.Y) * _miniEchelle);
            _miniCanvas.Children.Add(cadre);
        }

        private void MettreAJourInspecteur()
        {
            if (_inspecteurTexte == null || !_inspecteur.IsVisible) return;
            _inspecteurTexte.Text = PanneauxToile.TexteInspecteur(_toile);
        }

        private void MettreAJourHistorique()
        {
            if (_historiqueTexte == null || !_historique.IsVisible) return;
            _historiqueTexte.Text = string.Format("{0} action(s) annulable(s)\n{1} action(s) rétablissable(s)",
                _toile.Historique.NbAnnulables, _toile.Historique.NbRefaisables);
        }

        private void MettreAJourFiltre()
        {
            if (_filtreContenu == null || !_filtre.IsVisible) return;
            PanneauxToile.RemplirPanneauFiltre(_filtreContenu, _toile, _host.MainWindow);
        }

        private void MettreAJourTust()
        {
            if (_tustContenu == null || !_tust.IsVisible) return;
            PanneauxToile.RemplirTust(_tustContenu);
        }

        // ============================================================ recherche

        private void OuvrirRecherche()
        {
            _recherche.IsVisible = true;
            _champRecherche.Focus();
            _champRecherche.SelectAll();
            ActualiserRecherche(false);
        }

        private void FermerRecherche()
        {
            _recherche.IsVisible = false;
            _toile.Focus();
        }

        private void ActualiserRecherche(bool aller)
        {
            string texte = _champRecherche.Text ?? "";
            _resultats = _toile.Rechercher(texte);
            _indiceResultat = 0;
            _compteurRecherche.Text = _resultats.Count == 0 ? (texte.Trim().Length == 0 ? "" : "0") : "1/" + _resultats.Count;
            if (aller && _resultats.Count > 0) _toile.AllerAuNoeud(_resultats[0]);
        }

        private void NaviguerRecherche(int sens)
        {
            if (_resultats.Count == 0) return;
            _indiceResultat = (_indiceResultat + sens + _resultats.Count) % _resultats.Count;
            _compteurRecherche.Text = (_indiceResultat + 1) + "/" + _resultats.Count;
            _toile.AllerAuNoeud(_resultats[_indiceResultat]);
        }

        // ============================================================ présentation, thèmes, dialogues, exports

        private void BasculerPresentation()
        {
            if (_presenting) { QuitterPresentation(); return; }
            _presenting = true;
            _modeAvantPresentation = _toile.Mode;
            _toile.DefinirMode("lecture");
            _barHost.IsVisible = false;
            _compteur.IsVisible = true;
            SyncModes();
            _toile.Focus();
            Dispatcher.UIThread.Post(delegate { _toile.PresentationDebut(); }, DispatcherPriority.Loaded);
        }

        private void QuitterPresentation()
        {
            if (!_presenting) return;
            _presenting = false;
            _barHost.IsVisible = true;
            _compteur.IsVisible = false;
            _toile.DefinirMode(string.IsNullOrEmpty(_modeAvantPresentation) ? "automatique" : _modeAvantPresentation);
            SyncModes();
        }

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
                _toile.AppliquerPreferencesDocument(true);
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
            "annuler", "retablir", "copier", "coller", "tout-selectionner", "supprimer", "editer", "dupliquer"
        };

        /// <summary>Les raccourcis de la toile (ceux de Fichier > Paramètres de
        /// Mental-O), quand la toile a le clavier et qu'aucune boîte n'est en cours
        /// d'édition. Les actions de fichier (nouveau, ouvrir, enregistrer) et de
        /// fenêtre (plein écran) restent à Marabook.</summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!_toile.IsKeyboardFocusWithin && !_champRecherche.IsFocused) return;
            bool editing = Ui.Ancetre<TextBox>(e.Source, _root) != null;
            if (_presenting)
            {
                if (e.Key == Key.Escape) { QuitterPresentation(); e.Handled = true; return; }
                if (e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Space || e.Key == Key.Enter) { _toile.PresentationSuivante(); e.Handled = true; return; }
                if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Back) { _toile.PresentationPrecedente(); e.Handled = true; return; }
                if (e.Key == Key.Home) { _toile.PresentationDebut(); e.Handled = true; return; }
            }
            if (!editing && e.KeyModifiers == KeyModifiers.None)
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
                else if (_recherche.IsVisible) FermerRecherche();
                else _toile.Deselectionner();
                e.Handled = true;
                return;
            }
            if (e.KeyModifiers == KeyModifiers.None)
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
                case "dupliquer": _toile.DupliquerSelection(); return true;
                case "rechercher": OuvrirRecherche(); return true;
                case "editer": _toile.EditerSelection(); return true;
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
                case "presentation": BasculerPresentation(); return true;
                case "mode-automatique": _toile.DefinirMode("automatique"); SyncModes(); return true;
                case "mode-deplacement": _toile.DefinirMode("deplacement"); SyncModes(); return true;
                case "mode-liens": _toile.DefinirMode("liens"); SyncModes(); return true;
                case "mode-lecture": _toile.DefinirMode("lecture"); SyncModes(); return true;
                case "gras": if (editing && !_toile.EditionEnCours) return false; _toile.AppliquerFormatTexte("gras", null); return true;
                case "italique": if (editing && !_toile.EditionEnCours) return false; _toile.AppliquerFormatTexte("italique", null); return true;
                case "souligne": if (editing && !_toile.EditionEnCours) return false; _toile.AppliquerFormatTexte("souligne", null); return true;
                default: return false; // nouveau, ouvrir, enregistrer, imprimer, plein écran : à Marabook
            }
        }

        private async void Coller()
        {
            if (await _toile.PeutCollerBoites()) _toile.CollerBoites();
            else _toile.CollerImage();
        }
    }
}
