using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;

namespace Mentalo
{
    public class FenetrePrincipale : Window
    {
        private readonly ToileVue _toile;
        private readonly TextBlock _texteZoom;
        private string _cheminFichier;
        private bool _modifie;

        // Barre de formatage
        private Border _barreFormat;
        private ComboBox _comboPolice, _comboTaille, _comboCouleur, _comboSurlignage;
        private ToggleButton _btnGras, _btnItalique, _btnSouligne, _btnBarre;
        private ToggleButton _btnAlignGauche, _btnAlignCentre, _btnAlignDroite;
        private bool _synchroEnCours;

        private static readonly RoutedCommand CmdZoomAvant = new RoutedCommand();
        private static readonly RoutedCommand CmdZoomArriere = new RoutedCommand();
        private static readonly RoutedCommand CmdZoomCent = new RoutedCommand();
        private static readonly RoutedCommand CmdRecentrer = new RoutedCommand();
        private static readonly RoutedCommand CmdGras = new RoutedCommand();
        private static readonly RoutedCommand CmdItalique = new RoutedCommand();
        private static readonly RoutedCommand CmdSouligne = new RoutedCommand();
        private static readonly RoutedCommand CmdInsererImage = new RoutedCommand();
        private static readonly RoutedCommand CmdInsererFichier = new RoutedCommand();
        private static readonly RoutedCommand CmdInsererLienWeb = new RoutedCommand();
        private static readonly RoutedCommand CmdToutSelectionner = new RoutedCommand();
        private static readonly RoutedCommand CmdNote = new RoutedCommand();
        private static readonly RoutedCommand CmdPlier = new RoutedCommand();
        private static readonly RoutedCommand CmdZoomTout = new RoutedCommand();
        private static readonly RoutedCommand CmdZoomSelection = new RoutedCommand();
        private static readonly RoutedCommand CmdFocus = new RoutedCommand();
        private MenuItem _menuFocus, _menuFiltre;
        private Border _panneauFiltre;
        private StackPanel _filtreContenu;
        private static readonly RoutedCommand CmdGrouper = new RoutedCommand();
        private static readonly RoutedCommand CmdDegrouper = new RoutedCommand();
        private static readonly RoutedCommand CmdModeAuto = new RoutedCommand();
        private static readonly RoutedCommand CmdModeDeplacement = new RoutedCommand();
        private static readonly RoutedCommand CmdModeLiens = new RoutedCommand();
        private static readonly RoutedCommand CmdModeLecture = new RoutedCommand();
        private static readonly RoutedCommand CmdDupliquer = new RoutedCommand();
        private static readonly RoutedCommand CmdRechercher = new RoutedCommand();
        private static readonly RoutedCommand CmdEditer = new RoutedCommand();
        private static readonly RoutedCommand CmdImprimer = new RoutedCommand();
        private static readonly RoutedCommand CmdEmpiler = new RoutedCommand();
        private static readonly RoutedCommand CmdPleinEcran = new RoutedCommand();
        private static readonly RoutedCommand CmdPresentation = new RoutedCommand();

        // Plein écran et mode présentation
        private bool _pleinEcran, _presentation;
        private WindowState _etatAvantPleinEcran = WindowState.Normal;
        private string _modeAvantPresentation;
        private Menu _menuPrincipal;
        private Border _barreModes, _barreEtat;
        private MenuItem _menuPleinEcran, _menuPresentation;

        // Barre de recherche
        private Border _barreRecherche;
        private TextBox _champRecherche;
        private TextBlock _compteurRecherche;
        private List<Noeud> _resultatsRecherche = new List<Noeud>();
        private int _indiceRecherche;

        private static readonly RoutedCommand CmdSupprimer = new RoutedCommand();

        // Barre de modes
        private ToggleButton _btnModeAuto, _btnModeDeplacement, _btnModeLiens, _btnModeLecture;
        private ToggleButton _epingleOnglet;
        private Button _boutonTheme;
        private TextBlock _texteMode;

        // Barre de personnalisation unique (onglets Texte / Boîte / Groupe / Lien)
        private StackPanel _panneauTexte, _panneauBoites, _panneauGroupes, _panneauLiens;
        private ToggleButton _ongletTexte, _ongletBoite, _ongletGroupe, _ongletLien;
        private string _ongletActif = "texte";
        private ComboBox _comboFond, _comboBordureCouleur, _comboBordureEpaisseur, _comboBordureTrait, _comboCoins;
        private ComboBox _comboOmbre, _comboOpacite;
        private ComboBox _comboCouleurGroupe, _comboPositionTitre;
        private ComboBox _comboTraitGroupe, _comboEpaisseurGroupe, _comboCouleurTitre, _comboFondTitre, _comboTailleTitre;
        private TextBox _champTitreGroupe;
        private ComboBox _comboCouleurLien, _comboEpaisseurLien, _comboTraitLien, _comboFlecheLien, _comboTraceLien;
        private TextBox _champEtiquetteLien;

        // Panneaux flottants
        private Border _tust;
        private StackPanel _tustContenu;
        private MenuItem _menuTust, _menuMiniCarte, _menuInspecteur, _menuHistorique;
        private MenuItem _menuRecents;
        private Border _miniCarte, _inspecteur, _panneauHistorique;
        private Grid _vignette;
        private Canvas _miniCanvas;
        private TextBlock _inspecteurTexte, _historiqueTexte;
        private double _miniEchelle = 1;
        private Point _miniOrigine;
        private Dictionary<string, ICommand> _commandesParAction;

        public FenetrePrincipale()
        {
            Width = 1200;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            Background = Chrome.FondFenetre;
            Foreground = Chrome.Encre;

            _toile = new ToileVue();
            _toile.DocumentModifie += delegate
            {
                if (!_modifie) { _modifie = true; MettreAJourTitre(); }
            };
            _texteZoom = new TextBlock();
            _texteZoom.Margin = new Thickness(8, 3, 8, 3);
            _toile.ZoomChange += delegate(double zoom)
            {
                _texteZoom.Text = string.Format("Zoom : {0:0} %", zoom * 100);
            };
            _toile.Historique.Change += CommandManager.InvalidateRequerySuggested;

            ParametresGlobaux.Charger();

            DockPanel racine = new DockPanel();
            _menuPrincipal = ConstruireMenu();
            DockPanel.SetDock(_menuPrincipal, Dock.Top);
            racine.Children.Add(_menuPrincipal);
            _barreModes = ConstruireBarreModes();
            DockPanel.SetDock(_barreModes, Dock.Top);
            racine.Children.Add(_barreModes);
            // Barre de personnalisation unique : son contenu suit les onglets de la barre des modes.
            _panneauTexte = ConstruirePanneauTexte();
            _panneauBoites = ConstruirePanneauBoites();
            _panneauGroupes = ConstruirePanneauGroupes();
            _panneauLiens = ConstruirePanneauLiens();
            _barreFormat = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = Chrome.Bordure,
                Background = Chrome.FondBarre,
                MinHeight = 48,
                Child = _panneauTexte
            };
            DockPanel.SetDock(_barreFormat, Dock.Top);
            racine.Children.Add(_barreFormat);
            _barreEtat = ConstruireBarreEtat();
            DockPanel.SetDock(_barreEtat, Dock.Bottom);
            racine.Children.Add(_barreEtat);
            Grid zoneCentrale = new Grid();
            zoneCentrale.Children.Add(_toile);
            _vignette = ConstruireVignette();
            zoneCentrale.Children.Add(_vignette);
            _barreRecherche = ConstruireBarreRecherche();
            zoneCentrale.Children.Add(_barreRecherche);
            _tust = ConstruireTust();
            zoneCentrale.Children.Add(_tust);
            _miniCarte = ConstruireMiniCarte();
            zoneCentrale.Children.Add(_miniCarte);
            _inspecteur = ConstruireInspecteur();
            zoneCentrale.Children.Add(_inspecteur);
            _panneauHistorique = ConstruireHistorique();
            zoneCentrale.Children.Add(_panneauHistorique);
            _panneauFiltre = ConstruirePanneauFiltre();
            zoneCentrale.Children.Add(_panneauFiltre);
            racine.Children.Add(zoneCentrale);
            Content = racine;
            SelectionOnglet("texte");

            NoeudVisuel.ZoneFormatage = _barreFormat;
            _toile.FormatCibleChange += MettreAJourBarreFormat;

            BrancherCommandes();
            KeyDown += ToucheEnfoncee;
            KeyUp += ToucheRelachee;

            _toile.VueChangee += MettreAJourMiniCarte;
            _toile.DocumentModifie += delegate
            {
                MettreAJourMiniCarte();
                MettreAJourInspecteur();
            };
            _toile.Historique.Change += MettreAJourHistorique;
            _toile.EtiquettesChange += MettreAJourPanneauFiltre;
            _toile.EstompageChange += delegate
            {
                if (_menuFocus != null) _menuFocus.IsChecked = _toile.FocusActif;
            };

            // Sauvegarde automatique (option du fichier) : toutes les 3 minutes.
            DispatcherTimer minuterie = new DispatcherTimer { Interval = TimeSpan.FromSeconds(180) };
            minuterie.Tick += delegate
            {
                if (_modifie && _cheminFichier != null && _toile.Document.SauvegardeAuto)
                    EcrireFichier(_cheminFichier);
            };
            minuterie.Start();

            _texteZoom.Text = "Zoom : 100 %";
            MettreAJourBarreFormat();
            MettreAJourTust();
            MettreAJourPanneaux();
            MettreAJourTitre();
        }

        private void SelectionOnglet(string nom)
        {
            _ongletActif = nom;
            if (_ongletTexte == null) return;
            _ongletTexte.IsChecked = nom == "texte";
            _ongletBoite.IsChecked = nom == "boite";
            _ongletGroupe.IsChecked = nom == "groupe";
            _ongletLien.IsChecked = nom == "lien";
            if (_barreFormat != null)
                _barreFormat.Child = nom == "boite" ? (UIElement)_panneauBoites
                    : nom == "groupe" ? _panneauGroupes
                    : nom == "lien" ? _panneauLiens
                    : _panneauTexte;
        }

        /// <summary>Une icône Segoe MDL2 Assets (police intégrée à Windows 10/11).</summary>
        private static TextBlock Glyphe(string codeHexa, double taille)
        {
            return new TextBlock
            {
                Text = ((char)Convert.ToInt32(codeHexa, 16)).ToString(),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = taille,
                Foreground = Chrome.Encre,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        /// <summary>Empile une petite étiquette au-dessus d'un contrôle de barre d'outils.</summary>
        private static StackPanel AvecEtiquette(string etiquette, FrameworkElement controle)
        {
            StackPanel pile = new StackPanel
            {
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            pile.Children.Add(new TextBlock
            {
                Text = etiquette,
                FontSize = 10,
                Foreground = Chrome.TexteDoux,
                Margin = new Thickness(2, 0, 0, 1)
            });
            controle.Margin = new Thickness(0);
            pile.Children.Add(controle);
            return pile;
        }

        // ------------------------------------------------------------------ construction de l'interface

        private Menu ConstruireMenu()
        {
            Menu menu = new Menu();

            MenuItem fichier = new MenuItem { Header = "_Fichier" };
            fichier.Items.Add(Element("_Nouveau", ApplicationCommands.New, GesteDe("nouveau")));
            fichier.Items.Add(Element("_Ouvrir…", ApplicationCommands.Open, GesteDe("ouvrir")));
            _menuRecents = new MenuItem { Header = "Ouvrir les fichiers _récents" };
            RemplirMenuRecents();
            fichier.Items.Add(_menuRecents);
            fichier.Items.Add(new Separator());
            fichier.Items.Add(Element("_Enregistrer", ApplicationCommands.Save, GesteDe("enregistrer")));
            fichier.Items.Add(Element("Enregistrer _sous…", ApplicationCommands.SaveAs, GesteDe("enregistrer-sous")));
            fichier.Items.Add(new Separator());
            MenuItem exporter = new MenuItem { Header = "E_xporter" };
            AjouterActionMenu(exporter, "Image PNG…", delegate { ExporterImage(); });
            AjouterActionMenu(exporter, "Document PDF…", delegate { ExporterPdf(); });
            AjouterActionMenu(exporter, "Dessin SVG…", delegate { ExporterSvg(); });
            AjouterActionMenu(exporter, "Texte Markdown…", delegate { ExporterMarkdown(); });
            AjouterActionMenu(exporter, "Plan OPML…", delegate { ExporterOpml(); });
            fichier.Items.Add(exporter);
            MenuItem importerPuces = new MenuItem
            {
                Header = "I_mporter un texte à puces…",
                ToolTip = "Colle une liste à puces indentée : chaque ligne devient une boîte, l'indentation crée les liens"
            };
            importerPuces.Click += delegate { ImporterTexteAPuces(); };
            fichier.Items.Add(importerPuces);
            MenuItem importerOpml = new MenuItem
            {
                Header = "Importer un plan OPM_L…",
                ToolTip = "Chaque élément du plan devient une boîte, la hiérarchie devient des liens fléchés"
            };
            importerOpml.Click += delegate { ImporterOpml(); };
            fichier.Items.Add(importerOpml);
            fichier.Items.Add(Element("Im_primer…", CmdImprimer, GesteDe("imprimer")));
            fichier.Items.Add(new Separator());
            MenuItem preferences = new MenuItem { Header = "Préférences de _fichier…" };
            preferences.Click += delegate { OuvrirPreferencesFichier(); };
            fichier.Items.Add(preferences);
            MenuItem parametres = new MenuItem { Header = "Paramè_tres…" };
            parametres.Click += delegate { OuvrirParametres(); };
            fichier.Items.Add(parametres);
            fichier.Items.Add(new Separator());
            MenuItem associer = new MenuItem
            {
                Header = "_Associer les fichiers .tea à Mental-o",
                ToolTip = "Le double-clic sur un fichier .tea (ou .zeubi) dans l'Explorateur ouvrira Mental-o"
            };
            associer.Click += delegate { AssocierExtensions(); };
            fichier.Items.Add(associer);
            fichier.Items.Add(new Separator());
            MenuItem quitter = new MenuItem { Header = "_Quitter", InputGestureText = "Alt+F4" };
            quitter.Click += delegate { Close(); };
            fichier.Items.Add(quitter);
            menu.Items.Add(fichier);

            MenuItem edition = new MenuItem { Header = "É_dition" };
            edition.Items.Add(Element("_Annuler", ApplicationCommands.Undo, null));
            edition.Items.Add(Element("_Rétablir", ApplicationCommands.Redo, null));
            edition.Items.Add(new Separator());
            edition.Items.Add(Element("_Copier les boîtes", ApplicationCommands.Copy, GesteDe("copier")));
            edition.Items.Add(Element("C_oller", ApplicationCommands.Paste, GesteDe("coller")));
            edition.Items.Add(Element("_Dupliquer", CmdDupliquer, GesteDe("dupliquer")));
            edition.Items.Add(new Separator());
            edition.Items.Add(Element("Rec_hercher…", CmdRechercher, GesteDe("rechercher")));
            edition.Items.Add(Element("_Tout sélectionner", CmdToutSelectionner, GesteDe("tout-selectionner")));
            edition.Items.Add(new Separator());
            MenuItem noteBoite = Element("_Note de la boîte…", CmdNote, GesteDe("note"));
            noteBoite.ToolTip = "Un détail hors de la carte : icône dans le coin de la boîte, texte en infobulle";
            edition.Items.Add(noteBoite);
            MenuItem etiquettes = new MenuItem { Header = "Éti_quettes du fichier…", ToolTip = "Créer, renommer, colorer, supprimer les étiquettes ; poser : clic droit > Étiquettes" };
            etiquettes.Click += delegate { DialogueEtiquettes.Ouvrir(this, _toile); };
            edition.Items.Add(etiquettes);
            edition.Items.Add(new Separator());
            MenuItem supprimer = new MenuItem { Header = "_Supprimer la sélection", InputGestureText = "Suppr" };
            supprimer.Click += delegate { _toile.SupprimerSelection(); };
            edition.Items.Add(supprimer);
            edition.Items.Add(new Separator());
            edition.Items.Add(Element("_Grouper", CmdGrouper, "Ctrl+G"));
            edition.Items.Add(Element("Dégrou_per", CmdDegrouper, "Ctrl+Maj+G"));
            MenuItem plier = Element("P_lier / déplier le groupe", CmdPlier, GesteDe("plier"));
            plier.ToolTip = "Réduit le groupe sélectionné (ou celui de la boîte sélectionnée) à une pastille-titre ; double-clic sur la pastille pour déplier";
            edition.Items.Add(plier);
            edition.Items.Add(new Separator());
            MenuItem aligner = new MenuItem { Header = "_Aligner et distribuer" };
            AjouterActionMenu(aligner, "Aligner à gauche", delegate { _toile.AlignerSelection("gauche"); });
            AjouterActionMenu(aligner, "Centrer horizontalement", delegate { _toile.AlignerSelection("centre-h"); });
            AjouterActionMenu(aligner, "Aligner à droite", delegate { _toile.AlignerSelection("droite"); });
            aligner.Items.Add(new Separator());
            AjouterActionMenu(aligner, "Aligner en haut", delegate { _toile.AlignerSelection("haut"); });
            AjouterActionMenu(aligner, "Centrer verticalement", delegate { _toile.AlignerSelection("centre-v"); });
            AjouterActionMenu(aligner, "Aligner en bas", delegate { _toile.AlignerSelection("bas"); });
            aligner.Items.Add(new Separator());
            AjouterActionMenu(aligner, "Distribuer horizontalement", delegate { _toile.DistribuerSelection(true); });
            AjouterActionMenu(aligner, "Distribuer verticalement", delegate { _toile.DistribuerSelection(false); });
            edition.Items.Add(aligner);
            MenuItem empiler = new MenuItem
            {
                Header = "E_mpiler la sélection",
                Command = CmdEmpiler,
                ToolTip = "Range les boîtes sélectionnées en colonne, centres alignés"
            };
            edition.Items.Add(empiler);
            menu.Items.Add(edition);

            MenuItem insertion = new MenuItem { Header = "_Insertion" };
            insertion.Items.Add(Element("_Image…", CmdInsererImage, "Ctrl+Maj+I"));
            MenuItem insererFichier = Element("_Fichier…", CmdInsererFichier, GesteDe("inserer-fichier"));
            insererFichier.ToolTip = "Lie un fichier (tout sauf une image) à la boîte sélectionnée, ou dans une boîte neuve";
            insertion.Items.Add(insererFichier);
            MenuItem insererLienWeb = Element("Lien _web…", CmdInsererLienWeb, GesteDe("inserer-lien-web"));
            insererLienWeb.ToolTip = "Une adresse web cliquable dans la boîte sélectionnée, ou dans une boîte neuve (on peut aussi glisser une adresse depuis le navigateur)";
            insertion.Items.Add(insererLienWeb);
            menu.Items.Add(insertion);

            MenuItem affichage = new MenuItem { Header = "_Affichage" };
            affichage.Items.Add(Element("Zoom a_vant", CmdZoomAvant, "Ctrl++"));
            affichage.Items.Add(Element("Zoom arri_ère", CmdZoomArriere, "Ctrl+-"));
            affichage.Items.Add(Element("Zoom _100 %", CmdZoomCent, "Ctrl+0"));
            affichage.Items.Add(new Separator());
            affichage.Items.Add(Element("_Recentrer la carte", CmdRecentrer, "Ctrl+R"));
            affichage.Items.Add(Element("_Tout voir", CmdZoomTout, GesteDe("zoom-tout")));
            affichage.Items.Add(Element("Zoom sur la _sélection", CmdZoomSelection, GesteDe("zoom-selection")));
            affichage.Items.Add(new Separator());
            _menuFocus = Element("_Mettre en avant la sélection", CmdFocus, GesteDe("focus"));
            _menuFocus.IsCheckable = true;
            _menuFocus.ToolTip = "Estompe tout sauf la sélection ; Échap ou un second appel rétablit";
            affichage.Items.Add(_menuFocus);
            affichage.Items.Add(new Separator());
            _menuPleinEcran = Element("_Plein écran", CmdPleinEcran, GesteDe("plein-ecran"));
            affichage.Items.Add(_menuPleinEcran);
            _menuPresentation = Element("Mode pré_sentation", CmdPresentation, GesteDe("presentation"));
            _menuPresentation.ToolTip = "Plein écran, barres masquées, lecture seule — Échap pour sortir";
            affichage.Items.Add(_menuPresentation);
            menu.Items.Add(affichage);

            MenuItem fenetres = new MenuItem { Header = "Fenê_tres" };
            _menuTust = new MenuItem
            {
                Header = "_Tust des raccourcis",
                IsCheckable = true,
                IsChecked = ParametresGlobaux.TustVisible
            };
            _menuTust.Click += delegate
            {
                ParametresGlobaux.TustVisible = _menuTust.IsChecked == true;
                ParametresGlobaux.Enregistrer();
                MettreAJourTust();
            };
            fenetres.Items.Add(_menuTust);
            _menuMiniCarte = ElementPanneau("_Mini-carte", delegate(bool visible) { ParametresGlobaux.MiniCarteVisible = visible; }, ParametresGlobaux.MiniCarteVisible);
            fenetres.Items.Add(_menuMiniCarte);
            _menuInspecteur = ElementPanneau("_Inspecteur de sélection", delegate(bool visible) { ParametresGlobaux.InspecteurVisible = visible; }, ParametresGlobaux.InspecteurVisible);
            fenetres.Items.Add(_menuInspecteur);
            _menuHistorique = ElementPanneau("_Historique", delegate(bool visible) { ParametresGlobaux.HistoriqueVisible = visible; }, ParametresGlobaux.HistoriqueVisible);
            fenetres.Items.Add(_menuHistorique);
            _menuFiltre = ElementPanneau("_Filtre par étiquettes", delegate(bool visible) { ParametresGlobaux.FiltreVisible = visible; }, ParametresGlobaux.FiltreVisible);
            _menuFiltre.ToolTip = "Coche des étiquettes : les boîtes qui n'en portent aucune s'estompent";
            fenetres.Items.Add(_menuFiltre);
            menu.Items.Add(fenetres);

            MenuItem aide = new MenuItem { Header = "_?" };
            MenuItem aPropos = new MenuItem { Header = "À _propos de Mental-o…" };
            aPropos.Click += delegate
            {
                DialogueMessage.Afficher(this,
                    "Mental-o — v0.15\n\nLogiciel de carte mentale, simple et léger.\n\n" +
                    "Grandes cartes : Tout voir (Ctrl+Maj+0), zoom sur la sélection (Ctrl+Maj+1), Ctrl+A, Alt+rectangle = désélectionner\n" +
                    "Plier / déplier un groupe (Ctrl+Maj+P, clic droit sur le cadre, double-clic sur la pastille)\n" +
                    "Notes cachées (Ctrl+Maj+N, icône dans le coin) · Étiquettes (clic droit > Étiquettes, Fenêtres > Filtre) · Mettre en avant (Ctrl+Maj+M)\n" +
                    "Cases à cocher (bouton ☐ de l'onglet Texte, clic pour cocher, avancement dans le titre du groupe) · Liens web (Ctrl+Maj+L, glisser une adresse)\n" +
                    "Crédits à Gruffystudio de FlatIcon.com pour l'icône de l'app.",
                    "À propos de Mental-o", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            aide.Items.Add(aPropos);
            menu.Items.Add(aide);

            return menu;
        }

        private static void AjouterActionMenu(MenuItem parent, string entete, RoutedEventHandler action)
        {
            MenuItem element = new MenuItem { Header = entete };
            element.Click += action;
            parent.Items.Add(element);
        }

        private static string GesteDe(string idAction)
        {
            return ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(idAction));
        }

        private static MenuItem Element(string entete, ICommand commande, string gesteAffiche)
        {
            MenuItem element = new MenuItem();
            element.Header = entete;
            element.Command = commande;
            if (gesteAffiche != null) element.InputGestureText = gesteAffiche;
            return element;
        }

        // ------------------------------------------------------------------ barre de modes

        private Border ConstruireBarreModes()
        {
            StackPanel panneau = new StackPanel();
            panneau.Orientation = Orientation.Horizontal;
            panneau.Margin = new Thickness(6, 3, 6, 3);

            panneau.Children.Add(new TextBlock
            {
                Text = "Mode :",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = Chrome.TexteDoux
            });

            _btnModeAuto = BoutonMode("all-mode", "automatique",
                "Mode automatique — tout est actif : déplacement, liens, édition (Ctrl+1)");
            _btnModeDeplacement = BoutonMode("move-mode", "deplacement",
                "Mode déplacement — déplacer les boîtes uniquement, pas de création de liens (Ctrl+2)");
            _btnModeLiens = BoutonMode("link-mode", "liens",
                "Mode liens — créer des liens uniquement, les boîtes ne bougent pas (Ctrl+3)");
            _btnModeLecture = BoutonMode("read-mode", "lecture",
                "Mode lecture — naviguer sans rien modifier (Ctrl+4)");
            panneau.Children.Add(_btnModeAuto);
            panneau.Children.Add(_btnModeDeplacement);
            panneau.Children.Add(_btnModeLiens);
            panneau.Children.Add(_btnModeLecture);

            panneau.Children.Add(Separateur());
            panneau.Children.Add(new TextBlock
            {
                Text = "Barre d'outils :",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = Chrome.TexteDoux
            });
            _ongletTexte = BoutonOnglet("Texte", "text-custom-bar");
            _ongletBoite = BoutonOnglet("Boîte", "box-custom-bar");
            _ongletGroupe = BoutonOnglet("Groupe", "group-custom-bar");
            _ongletLien = BoutonOnglet("Lien", "links-custom-bar");
            panneau.Children.Add(_ongletTexte);
            panneau.Children.Add(_ongletBoite);
            panneau.Children.Add(_ongletGroupe);
            panneau.Children.Add(_ongletLien);
            _epingleOnglet = new ToggleButton
            {
                Content = Glyphe("E718", 13),
                Width = 26,
                Margin = new Thickness(2, 0, 0, 0),
                Focusable = false,
                ToolTip = "Épingler l'onglet actuel : la sélection ne le change plus automatiquement"
            };
            panneau.Children.Add(_epingleOnglet);

            _texteMode = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Foreground = Chrome.TexteDoux,
                FontStyle = FontStyles.Italic
            };
            panneau.Children.Add(_texteMode);

            // Le bouton de thème vit à l'opposé de la barre, ancré tout à droite.
            _boutonTheme = new Button
            {
                Width = 30,
                Height = 24,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0)
            };
            _boutonTheme.Click += delegate { BasculerTheme(); };
            MettreAJourIconeTheme();

            DockPanel conteneur = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_boutonTheme, Dock.Right);
            conteneur.Children.Add(_boutonTheme);
            conteneur.Children.Add(panneau);

            Border bordure = new Border();
            bordure.BorderThickness = new Thickness(0, 0, 0, 1);
            bordure.BorderBrush = Chrome.Bordure;
            bordure.Background = Chrome.FondBarreLegere;
            bordure.Child = conteneur;

            _toile.ModeChange += MettreAJourBarreModes;
            MettreAJourBarreModes();
            return bordure;
        }

        private ToggleButton BoutonMode(string icone, string mode, string infobulle)
        {
            ToggleButton bouton = new ToggleButton();
            bouton.Content = Icones.Creer(icone, 16);
            bouton.ToolTip = infobulle;
            bouton.Width = 34;
            bouton.Padding = new Thickness(0, 2, 0, 2);
            bouton.Margin = new Thickness(2, 0, 0, 0);
            bouton.Focusable = false;
            bouton.Click += delegate { _toile.DefinirMode(mode); MettreAJourBarreModes(); };
            return bouton;
        }

        private ToggleButton BoutonOnglet(string entete, string icone)
        {
            StackPanel contenu = new StackPanel { Orientation = Orientation.Horizontal };
            FrameworkElement image = Icones.Creer(icone, 13);
            image.Margin = new Thickness(0, 0, 5, 0);
            contenu.Children.Add(image);
            contenu.Children.Add(new TextBlock { Text = entete, VerticalAlignment = VerticalAlignment.Center });

            ToggleButton bouton = new ToggleButton();
            bouton.Content = contenu;
            bouton.ToolTip = "Contenu de la barre de personnalisation : " + entete.ToLower();
            bouton.Padding = new Thickness(8, 1, 8, 1);
            bouton.Margin = new Thickness(2, 0, 0, 0);
            bouton.Focusable = false;
            string nom = entete == "Boîte" ? "boite" : entete.ToLower();
            bouton.Click += delegate { SelectionOnglet(nom); };
            return bouton;
        }

        // ------------------------------------------------------------------ recherche

        private Border ConstruireBarreRecherche()
        {
            StackPanel contenu = new StackPanel { Orientation = Orientation.Horizontal };
            _champRecherche = new TextBox
            {
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Entrée : occurrence suivante · Maj+Entrée : précédente · Échap : fermer"
            };
            _champRecherche.TextChanged += delegate { ActualiserRecherche(true); };
            _champRecherche.PreviewKeyDown += delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    NaviguerRecherche((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    FermerRecherche();
                    e.Handled = true;
                }
            };
            contenu.Children.Add(_champRecherche);
            _compteurRecherche = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                Foreground = Chrome.TexteDoux,
                MinWidth = 32,
                TextAlignment = TextAlignment.Center
            };
            contenu.Children.Add(_compteurRecherche);
            Button precedent = new Button { Content = "‹", Width = 24, Focusable = false, ToolTip = "Occurrence précédente (Maj+Entrée)" };
            precedent.Click += delegate { NaviguerRecherche(-1); };
            contenu.Children.Add(precedent);
            Button suivant = new Button { Content = "›", Width = 24, Margin = new Thickness(3, 0, 0, 0), Focusable = false, ToolTip = "Occurrence suivante (Entrée)" };
            suivant.Click += delegate { NaviguerRecherche(1); };
            contenu.Children.Add(suivant);
            Button fermer = new Button { Content = "✕", Width = 24, Margin = new Thickness(8, 0, 0, 0), Focusable = false, ToolTip = "Fermer (Échap)" };
            fermer.Click += delegate { FermerRecherche(); };
            contenu.Children.Add(fermer);

            return new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 0, 0),
                Background = Chrome.FondPanneau,
                BorderBrush = Chrome.Bordure,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Visibility = Visibility.Collapsed,
                Child = contenu
            };
        }

        private void OuvrirRecherche()
        {
            _barreRecherche.Visibility = Visibility.Visible;
            _champRecherche.Focus();
            _champRecherche.SelectAll();
            ActualiserRecherche(false);
        }

        private void FermerRecherche()
        {
            _barreRecherche.Visibility = Visibility.Collapsed;
            _toile.Focus();
        }

        private void ActualiserRecherche(bool aller)
        {
            _resultatsRecherche = _toile.Rechercher(_champRecherche.Text);
            _indiceRecherche = 0;
            _compteurRecherche.Text = _resultatsRecherche.Count == 0
                ? (_champRecherche.Text.Trim().Length == 0 ? "" : "0")
                : "1/" + _resultatsRecherche.Count;
            if (aller && _resultatsRecherche.Count > 0)
                _toile.AllerAuNoeud(_resultatsRecherche[0]);
        }

        private void NaviguerRecherche(int sens)
        {
            if (_resultatsRecherche.Count == 0) return;
            _indiceRecherche = (_indiceRecherche + sens + _resultatsRecherche.Count) % _resultatsRecherche.Count;
            _compteurRecherche.Text = (_indiceRecherche + 1) + "/" + _resultatsRecherche.Count;
            _toile.AllerAuNoeud(_resultatsRecherche[_indiceRecherche]);
        }

        /// <summary>Très légère ombre en vignette sur les quatre bords du tableau.</summary>
        private Grid ConstruireVignette()
        {
            Grid calque = new Grid { IsHitTestVisible = false };
            Color ombre = Color.FromArgb(0x22, 0x00, 0x00, 0x00);
            Color rien = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
            calque.Children.Add(new Rectangle
            {
                Height = 26,
                VerticalAlignment = VerticalAlignment.Top,
                Fill = new LinearGradientBrush(ombre, rien, 90)
            });
            calque.Children.Add(new Rectangle
            {
                Height = 26,
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = new LinearGradientBrush(rien, ombre, 90)
            });
            calque.Children.Add(new Rectangle
            {
                Width = 26,
                HorizontalAlignment = HorizontalAlignment.Left,
                Fill = new LinearGradientBrush(ombre, rien, 0)
            });
            calque.Children.Add(new Rectangle
            {
                Width = 26,
                HorizontalAlignment = HorizontalAlignment.Right,
                Fill = new LinearGradientBrush(rien, ombre, 0)
            });
            calque.Visibility = ParametresGlobaux.VignetteTableau ? Visibility.Visible : Visibility.Collapsed;
            return calque;
        }

        private void MettreAJourVignette()
        {
            if (_vignette != null)
                _vignette.Visibility = ParametresGlobaux.VignetteTableau
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BasculerTheme()
        {
            ParametresGlobaux.ThemeSombre = !ParametresGlobaux.ThemeSombre;
            ParametresGlobaux.Enregistrer();
            Theme.Basculer(Application.Current, ParametresGlobaux.ThemeSombre);
            Chrome.Basculer(ParametresGlobaux.ThemeSombre);
            _toile.AppliquerFondDocument();
            MettreAJourIconeTheme();
            MettreAJourMiniCarte();
        }

        private void MettreAJourIconeTheme()
        {
            _boutonTheme.Content = Glyphe(ParametresGlobaux.ThemeSombre ? "E706" : "E708", 14);
            _boutonTheme.ToolTip = ParametresGlobaux.ThemeSombre
                ? "Repasser au thème clair"
                : "Passer au thème sombre";
        }

        private void MettreAJourBarreModes()
        {
            string mode = _toile.Mode;
            _btnModeAuto.IsChecked = mode == "automatique";
            _btnModeDeplacement.IsChecked = mode == "deplacement";
            _btnModeLiens.IsChecked = mode == "liens";
            _btnModeLecture.IsChecked = mode == "lecture";
            _texteMode.Text = mode == "deplacement" ? "Les poignées de lien sont désactivées."
                : mode == "liens" ? "Les boîtes et groupes ne se déplacent pas."
                : mode == "lecture" ? "Lecture seule : navigation et consultation uniquement."
                : "";
        }

        // ------------------------------------------------------------------ panneau des boîtes

        private StackPanel ConstruirePanneauBoites()
        {
            StackPanel panneau = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };

            _comboFond = new ComboBox { Width = 116, ToolTip = "Fond de la boîte", VerticalAlignment = VerticalAlignment.Center };
            _comboFond.SelectionChanged += delegate { CouleurBoiteChoisie(_comboFond, "fond"); };
            panneau.Children.Add(AvecEtiquette("Fond", _comboFond));

            _comboBordureCouleur = new ComboBox { Width = 116, ToolTip = "Couleur de la bordure", VerticalAlignment = VerticalAlignment.Center };
            _comboBordureCouleur.SelectionChanged += delegate { CouleurBoiteChoisie(_comboBordureCouleur, "bordure-couleur"); };
            panneau.Children.Add(AvecEtiquette("Bordure", _comboBordureCouleur));

            _comboBordureEpaisseur = new ComboBox { Width = 52, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Épaisseur de la bordure", VerticalAlignment = VerticalAlignment.Center };
            foreach (double valeur in new double[] { 1, 1.5, 2, 3, 5 })
                _comboBordureEpaisseur.Items.Add(valeur);
            _comboBordureEpaisseur.SelectionChanged += delegate
            {
                if (_synchroEnCours || _comboBordureEpaisseur.SelectedItem == null) return;
                _toile.AppliquerStyleBoite("bordure-epaisseur", (double)_comboBordureEpaisseur.SelectedItem);
            };
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboBordureEpaisseur));

            _comboBordureTrait = new ComboBox { Width = 86, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Trait de la bordure", VerticalAlignment = VerticalAlignment.Center };
            _comboBordureTrait.Items.Add(new ComboBoxItem { Content = "Plein", Tag = "plein" });
            _comboBordureTrait.Items.Add(new ComboBoxItem { Content = "Tirets", Tag = "tirets" });
            _comboBordureTrait.Items.Add(new ComboBoxItem { Content = "Pointillé", Tag = "points" });
            _comboBordureTrait.Items.Add(new ComboBoxItem { Content = "Aucune", Tag = "aucune" });
            _comboBordureTrait.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboBordureTrait.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleBoite("bordure-trait", (string)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Trait", _comboBordureTrait));

            _comboCoins = new ComboBox { Width = 100, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Coins de la boîte", VerticalAlignment = VerticalAlignment.Center };
            _comboCoins.Items.Add(new ComboBoxItem { Content = "Carrés", Tag = 0.0 });
            _comboCoins.Items.Add(new ComboBoxItem { Content = "Arrondis", Tag = 5.0 });
            _comboCoins.Items.Add(new ComboBoxItem { Content = "Très arrondis", Tag = 12.0 });
            _comboCoins.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboCoins.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleBoite("rayon", (double)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Coins", _comboCoins));

            _comboOmbre = new ComboBox { Width = 84, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Ombre portée de la boîte", VerticalAlignment = VerticalAlignment.Center };
            _comboOmbre.Items.Add(new ComboBoxItem { Content = "Aucune", Tag = "aucune" });
            _comboOmbre.Items.Add(new ComboBoxItem { Content = "Légère", Tag = "legere" });
            _comboOmbre.Items.Add(new ComboBoxItem { Content = "Marquée", Tag = "marquee" });
            _comboOmbre.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboOmbre.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleBoite("ombre", (string)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Ombre", _comboOmbre));

            _comboOpacite = new ComboBox { Width = 66, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Opacité de la boîte", VerticalAlignment = VerticalAlignment.Center };
            _comboOpacite.Items.Add(new ComboBoxItem { Content = "100 %", Tag = 1.0 });
            _comboOpacite.Items.Add(new ComboBoxItem { Content = "85 %", Tag = 0.85 });
            _comboOpacite.Items.Add(new ComboBoxItem { Content = "70 %", Tag = 0.7 });
            _comboOpacite.Items.Add(new ComboBoxItem { Content = "50 %", Tag = 0.5 });
            _comboOpacite.Items.Add(new ComboBoxItem { Content = "30 %", Tag = 0.3 });
            _comboOpacite.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboOpacite.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleBoite("opacite", (double)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Opacité", _comboOpacite));

            Button reajuster = new Button
            {
                Content = Glyphe("E9A6", 14),
                Width = 30,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Focusable = false,
                ToolTip = "Réajuster la boîte à son contenu"
            };
            reajuster.Click += delegate { _toile.ReajusterBoite(); };
            panneau.Children.Add(reajuster);

            RemplirCombosCouleurs();
            return panneau;
        }

        private void CouleurBoiteChoisie(ComboBox combo, string propriete)
        {
            if (_synchroEnCours) return;
            ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
            if (choix == null) return;
            string tag = choix.Tag as string;
            if (tag == TagPersonnalisee)
            {
                _toile.ChoisirCouleurPersonnalisee(propriete);
                return;
            }
            if (tag != null) _toile.AppliquerStyleBoite(propriete, tag);
        }

        private void MettreAJourBarreBoites(FormatCourant etat)
        {
            if (_panneauBoites == null) return;
            _panneauBoites.IsEnabled = etat.Actif && _toile.Mode != "lecture";
            if (!etat.Actif) return;
            SelectionnerParTag(_comboFond, etat.FondBoite);
            SelectionnerParTag(_comboBordureCouleur, etat.BordureCouleur);
            _comboBordureEpaisseur.SelectedItem = null;
            foreach (object objet in _comboBordureEpaisseur.Items)
                if (objet is double && Math.Abs((double)objet - etat.BordureEpaisseur) < 0.01)
                {
                    _comboBordureEpaisseur.SelectedItem = objet;
                    break;
                }
            SelectionnerParTag(_comboBordureTrait, etat.BordureTrait);
            SelectionnerParTag(_comboOmbre, etat.Ombre);
            _comboOpacite.SelectedIndex = -1;
            foreach (object objet in _comboOpacite.Items)
            {
                ComboBoxItem element = (ComboBoxItem)objet;
                if (element.Tag is double && Math.Abs((double)element.Tag - etat.Opacite) < 0.01)
                {
                    _comboOpacite.SelectedItem = element;
                    break;
                }
            }
            _comboCoins.SelectedIndex = -1;
            foreach (object objet in _comboCoins.Items)
            {
                ComboBoxItem element = (ComboBoxItem)objet;
                if (element.Tag is double && Math.Abs((double)element.Tag - etat.Rayon) < 0.01)
                {
                    _comboCoins.SelectedItem = element;
                    break;
                }
            }
        }

        private static void SelectionnerParTag(ComboBox combo, string valeur)
        {
            combo.SelectedIndex = -1;
            foreach (object objet in combo.Items)
            {
                ComboBoxItem element = objet as ComboBoxItem;
                if (element != null && (string)element.Tag == valeur)
                {
                    combo.SelectedItem = element;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ panneau des groupes

        private StackPanel ConstruirePanneauGroupes()
        {
            StackPanel panneau = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };

            _comboCouleurGroupe = new ComboBox { Width = 110, ToolTip = "Couleur du cadre", VerticalAlignment = VerticalAlignment.Center };
            _comboCouleurGroupe.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboCouleurGroupe.SelectedItem as ComboBoxItem;
                if (choix == null) return;
                string tag = choix.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurGroupePersonnalisee();
                else if (tag != null) _toile.ChangerCouleurGroupeSelection(tag);
            };
            panneau.Children.Add(AvecEtiquette("Cadre", _comboCouleurGroupe));

            _comboTraitGroupe = new ComboBox { Width = 82, ToolTip = "Trait du cadre", VerticalAlignment = VerticalAlignment.Center };
            _comboTraitGroupe.Items.Add(new ComboBoxItem { Content = "Tirets", Tag = "tirets" });
            _comboTraitGroupe.Items.Add(new ComboBoxItem { Content = "Plein", Tag = "plein" });
            _comboTraitGroupe.Items.Add(new ComboBoxItem { Content = "Pointillé", Tag = "points" });
            _comboTraitGroupe.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboTraitGroupe.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.ChangerTraitGroupeSelection((string)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Trait", _comboTraitGroupe));

            _comboEpaisseurGroupe = new ComboBox { Width = 50, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Épaisseur du cadre", VerticalAlignment = VerticalAlignment.Center };
            foreach (double valeur in new double[] { 1, 1.5, 2.5, 4 })
                _comboEpaisseurGroupe.Items.Add(valeur);
            _comboEpaisseurGroupe.SelectionChanged += delegate
            {
                if (_synchroEnCours || _comboEpaisseurGroupe.SelectedItem == null) return;
                _toile.ChangerEpaisseurGroupeSelection((double)_comboEpaisseurGroupe.SelectedItem);
            };
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboEpaisseurGroupe));

            _champTitreGroupe = new TextBox
            {
                Width = 110,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Titre du groupe, posé sur le cadre (Entrée pour appliquer)"
            };
            _champTitreGroupe.KeyDown += delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                AppliquerTitreGroupeSaisi();
                e.Handled = true;
            };
            _champTitreGroupe.LostFocus += delegate { AppliquerTitreGroupeSaisi(); };
            panneau.Children.Add(AvecEtiquette("Titre", _champTitreGroupe));

            _comboPositionTitre = new ComboBox { Width = 106, ToolTip = "Position du titre sur le cadre", VerticalAlignment = VerticalAlignment.Center };
            _comboPositionTitre.Items.Add(new ComboBoxItem { Content = "Haut gauche", Tag = "haut-gauche" });
            _comboPositionTitre.Items.Add(new ComboBoxItem { Content = "Haut droite", Tag = "haut-droite" });
            _comboPositionTitre.Items.Add(new ComboBoxItem { Content = "Bas gauche", Tag = "bas-gauche" });
            _comboPositionTitre.Items.Add(new ComboBoxItem { Content = "Bas droite", Tag = "bas-droite" });
            _comboPositionTitre.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                AppliquerTitreGroupeSaisi();
            };
            panneau.Children.Add(AvecEtiquette("Position", _comboPositionTitre));

            _comboCouleurTitre = new ComboBox { Width = 104, ToolTip = "Couleur du texte du titre", VerticalAlignment = VerticalAlignment.Center };
            _comboCouleurTitre.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboCouleurTitre.SelectedItem as ComboBoxItem;
                if (choix == null) return;
                string tag = choix.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurTitreGroupePersonnalisee(false);
                else if (tag != null) _toile.ChangerStyleTitreGroupeSelection(tag, null, null);
            };
            panneau.Children.Add(AvecEtiquette("Texte du titre", _comboCouleurTitre));

            _comboFondTitre = new ComboBox { Width = 104, ToolTip = "Fond du titre", VerticalAlignment = VerticalAlignment.Center };
            _comboFondTitre.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboFondTitre.SelectedItem as ComboBoxItem;
                if (choix == null) return;
                string tag = choix.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurTitreGroupePersonnalisee(true);
                else if (tag != null) _toile.ChangerStyleTitreGroupeSelection(null, tag, null);
            };
            panneau.Children.Add(AvecEtiquette("Fond du titre", _comboFondTitre));

            _comboTailleTitre = new ComboBox { Width = 48, IsEditable = true, ToolTip = "Taille du titre (Entrée pour valider une valeur libre)", VerticalAlignment = VerticalAlignment.Center };
            foreach (double valeur in new double[] { 10, 12.5, 14, 16, 20 })
                _comboTailleTitre.Items.Add(valeur);
            _comboTailleTitre.SelectionChanged += delegate
            {
                if (_synchroEnCours || _comboTailleTitre.SelectedItem == null) return;
                _toile.ChangerStyleTitreGroupeSelection(null, null, (double)_comboTailleTitre.SelectedItem);
            };
            _comboTailleTitre.KeyDown += delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key != Key.Enter || _synchroEnCours) return;
                double? taille = DialogueSaisie.InterpreterTaille(_comboTailleTitre.Text);
                if (taille.HasValue) _toile.ChangerStyleTitreGroupeSelection(null, null, taille.Value);
                e.Handled = true;
            };
            panneau.Children.Add(AvecEtiquette("Taille", _comboTailleTitre));

            Button reajuster = new Button
            {
                Content = Glyphe("E9A6", 14),
                Width = 30,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Focusable = false,
                ToolTip = "Réajuster le cadre à son contenu"
            };
            reajuster.Click += delegate { _toile.ReajusterGroupeSelection(); };
            panneau.Children.Add(reajuster);

            Button degrouper = new Button
            {
                Content = "Dégrouper",
                Padding = new Thickness(8, 1, 8, 1),
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Focusable = false,
                ToolTip = "Dissoudre le groupe (Ctrl+Maj+G)"
            };
            degrouper.Click += delegate { _toile.DegrouperSelection(); };
            panneau.Children.Add(degrouper);

            RemplirCombosCouleurs();
            return panneau;
        }

        // ------------------------------------------------------------------ panneau des liens

        private StackPanel ConstruirePanneauLiens()
        {
            StackPanel panneau = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };

            _comboCouleurLien = new ComboBox { Width = 116, ToolTip = "Couleur du lien", VerticalAlignment = VerticalAlignment.Center };
            _comboCouleurLien.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboCouleurLien.SelectedItem as ComboBoxItem;
                if (choix == null) return;
                string tag = choix.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurLienPersonnalisee();
                else if (tag != null) _toile.AppliquerStyleLienSelection(tag, null, null, null);
            };
            panneau.Children.Add(AvecEtiquette("Couleur", _comboCouleurLien));

            _comboEpaisseurLien = new ComboBox { Width = 52, ToolTip = "Épaisseur du lien", VerticalAlignment = VerticalAlignment.Center };
            foreach (double valeur in new double[] { 1, 2, 3, 5 })
                _comboEpaisseurLien.Items.Add(valeur);
            _comboEpaisseurLien.SelectionChanged += delegate
            {
                if (_synchroEnCours || _comboEpaisseurLien.SelectedItem == null) return;
                _toile.AppliquerStyleLienSelection(null, (double)_comboEpaisseurLien.SelectedItem, null, null);
            };
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboEpaisseurLien));

            _comboTraitLien = new ComboBox { Width = 86, ToolTip = "Trait du lien", VerticalAlignment = VerticalAlignment.Center };
            _comboTraitLien.Items.Add(new ComboBoxItem { Content = "Plein", Tag = "plein" });
            _comboTraitLien.Items.Add(new ComboBoxItem { Content = "Tirets", Tag = "tirets" });
            _comboTraitLien.Items.Add(new ComboBoxItem { Content = "Pointillé", Tag = "points" });
            _comboTraitLien.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboTraitLien.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleLienSelection(null, null, (string)choix.Tag, null);
            };
            panneau.Children.Add(AvecEtiquette("Trait", _comboTraitLien));

            _comboFlecheLien = new ComboBox { Width = 128, ToolTip = "Flèches du lien", VerticalAlignment = VerticalAlignment.Center };
            _comboFlecheLien.Items.Add(new ComboBoxItem { Content = "Sans flèche", Tag = "aucune" });
            _comboFlecheLien.Items.Add(new ComboBoxItem { Content = "Vers la cible", Tag = "simple" });
            _comboFlecheLien.Items.Add(new ComboBoxItem { Content = "Les deux sens", Tag = "double" });
            _comboFlecheLien.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboFlecheLien.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleLienSelection(null, null, null, (string)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Flèches", _comboFlecheLien));

            _comboTraceLien = new ComboBox { Width = 92, ToolTip = "Tracé du lien — la courbe se règle à la poignée rose", VerticalAlignment = VerticalAlignment.Center };
            _comboTraceLien.Items.Add(new ComboBoxItem { Content = "Droit", Tag = "droit" });
            _comboTraceLien.Items.Add(new ComboBoxItem { Content = "Courbe", Tag = "courbe" });
            _comboTraceLien.Items.Add(new ComboBoxItem { Content = "Équerre", Tag = "equerre" });
            _comboTraceLien.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboTraceLien.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerTraceLienSelection((string)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Tracé", _comboTraceLien));

            _champEtiquetteLien = new TextBox
            {
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Étiquette posée au milieu du trait (Entrée pour appliquer, vide pour retirer)"
            };
            _champEtiquetteLien.KeyDown += delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                if (!_synchroEnCours) _toile.AppliquerEtiquetteLienSelection(_champEtiquetteLien.Text);
                e.Handled = true;
            };
            _champEtiquetteLien.LostFocus += delegate
            {
                if (!_synchroEnCours) _toile.AppliquerEtiquetteLienSelection(_champEtiquetteLien.Text);
            };
            panneau.Children.Add(AvecEtiquette("Étiquette", _champEtiquetteLien));

            RemplirCombosCouleurs();
            return panneau;
        }

        private void MettreAJourBarreLiens()
        {
            if (_panneauLiens == null) return;
            Lien lien = _toile.LienSelectionne;
            _panneauLiens.IsEnabled = lien != null;
            if (lien == null) return;
            SelectionnerParTag(_comboCouleurLien, lien.Couleur);
            _comboEpaisseurLien.SelectedItem = null;
            foreach (object objet in _comboEpaisseurLien.Items)
                if (objet is double && Math.Abs((double)objet - lien.Epaisseur) < 0.01)
                {
                    _comboEpaisseurLien.SelectedItem = objet;
                    break;
                }
            SelectionnerParTag(_comboTraitLien, lien.Trait);
            SelectionnerParTag(_comboFlecheLien, lien.Fleche);
            SelectionnerParTag(_comboTraceLien, lien.Trace);
            _champEtiquetteLien.Text = lien.Etiquette ?? "";
        }

        private void AppliquerTitreGroupeSaisi()
        {
            if (_synchroEnCours || _toile.GroupeSelectionne == null) return;
            ComboBoxItem choix = _comboPositionTitre.SelectedItem as ComboBoxItem;
            string position = choix != null ? (string)choix.Tag : _toile.GroupeSelectionne.TitrePosition;
            _toile.ChangerTitreGroupeSelection(_champTitreGroupe.Text, position);
        }

        private void MettreAJourBarreGroupes()
        {
            if (_panneauGroupes == null) return;
            Groupe groupe = _toile.GroupeSelectionne;
            _panneauGroupes.IsEnabled = groupe != null && _toile.Mode != "lecture";
            if (groupe == null) return;
            SelectionnerParTag(_comboCouleurGroupe, groupe.Couleur);
            SelectionnerParTag(_comboTraitGroupe, groupe.BordureTrait);
            _comboEpaisseurGroupe.SelectedItem = null;
            foreach (object objet in _comboEpaisseurGroupe.Items)
                if (objet is double && Math.Abs((double)objet - groupe.BordureEpaisseur) < 0.01)
                {
                    _comboEpaisseurGroupe.SelectedItem = objet;
                    break;
                }
            _champTitreGroupe.Text = groupe.Titre ?? "";
            SelectionnerParTag(_comboPositionTitre, groupe.TitrePosition);
            SelectionnerParTag(_comboCouleurTitre, groupe.TitreCouleur ?? "");
            SelectionnerParTag(_comboFondTitre, groupe.TitreFond ?? "");
            _comboTailleTitre.Text = groupe.TitreTaille.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }

        // ------------------------------------------------------------------ panneau texte

        private StackPanel ConstruirePanneauTexte()
        {
            StackPanel panneau = new StackPanel();
            panneau.Orientation = Orientation.Horizontal;
            panneau.Margin = new Thickness(6, 4, 6, 4);

            _comboPolice = new ComboBox
            {
                Width = 165,
                IsEditable = true,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Police — toutes les polices installées (tapez un nom puis Entrée)"
            };
            foreach (string police in TexteRiche.PolicesSysteme())
                _comboPolice.Items.Add(police);
            _comboPolice.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                string choix = _comboPolice.SelectedItem as string;
                if (choix != null) _toile.AppliquerFormatTexte("police", choix);
            };
            _comboPolice.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                AppliquerPoliceSaisie();
                e.Handled = true;
            };
            panneau.Children.Add(AvecEtiquette("Police", _comboPolice));

            _comboTaille = new ComboBox
            {
                Width = 60,
                Margin = new Thickness(4, 0, 0, 0),
                IsEditable = true,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Taille — tapez une valeur libre (6 à 120) puis Entrée"
            };
            foreach (double taille in TexteRiche.Tailles)
                _comboTaille.Items.Add(taille);
            _comboTaille.SelectionChanged += delegate
            {
                if (_synchroEnCours || _comboTaille.SelectedItem == null) return;
                _toile.AppliquerFormatTexte("taille", (double)_comboTaille.SelectedItem);
            };
            _comboTaille.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                AppliquerTailleSaisie();
                e.Handled = true;
            };
            panneau.Children.Add(AvecEtiquette("Taille", _comboTaille));

            panneau.Children.Add(Separateur());

            _btnGras = BoutonBascule(Icones.Creer("text-b-bold", 14), "Gras (Ctrl+B)", "gras");
            _btnItalique = BoutonBascule(Icones.Creer("text-italic", 14), "Italique (Ctrl+I)", "italique");
            _btnSouligne = BoutonBascule(Icones.Creer("text-underline", 14), "Souligné (Ctrl+U)", "souligne");
            _btnBarre = BoutonBascule(Icones.Creer("text-strikethrough", 14), "Barré", "barre");
            panneau.Children.Add(_btnGras);
            panneau.Children.Add(_btnItalique);
            panneau.Children.Add(_btnSouligne);
            panneau.Children.Add(_btnBarre);

            panneau.Children.Add(Separateur());

            _comboCouleur = new ComboBox { Width = 118, ToolTip = "Couleur du texte", VerticalAlignment = VerticalAlignment.Center };
            _comboCouleur.SelectionChanged += delegate { CouleurChoisie(_comboCouleur, "couleur"); };
            panneau.Children.Add(AvecEtiquette("Couleur", _comboCouleur));

            _comboSurlignage = new ComboBox { Width = 118, ToolTip = "Surlignage", VerticalAlignment = VerticalAlignment.Center };
            _comboSurlignage.SelectionChanged += delegate { CouleurChoisie(_comboSurlignage, "surlignage"); };
            panneau.Children.Add(AvecEtiquette("Surlignage", _comboSurlignage));

            RemplirCombosCouleurs();

            panneau.Children.Add(Separateur());

            _btnAlignGauche = BoutonAlignement("text-align-left", "Aligner à gauche", "gauche");
            _btnAlignCentre = BoutonAlignement("text-align-center", "Centrer", "centre");
            _btnAlignDroite = BoutonAlignement("text-align-right", "Aligner à droite", "droite");
            panneau.Children.Add(_btnAlignGauche);
            panneau.Children.Add(_btnAlignCentre);
            panneau.Children.Add(_btnAlignDroite);

            panneau.Children.Add(Separateur());
            panneau.Children.Add(BoutonPuce("•", "Liste à puces", "puce"));
            panneau.Children.Add(BoutonPuce("–", "Liste à tirets", "tiret"));
            panneau.Children.Add(BoutonPuce("1.", "Liste numérotée", "numero"));
            panneau.Children.Add(BoutonPuce("☐", "Cases à cocher (cliquer une case dans la boîte pour la cocher)", "case"));

            return panneau;
        }

        private Button BoutonPuce(string etiquette, string infobulle, string type)
        {
            Button bouton = new Button
            {
                Content = etiquette,
                Width = 30,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                Focusable = false,
                ToolTip = infobulle + " — boîte entière, ou lignes sélectionnées en édition ; re-cliquer pour retirer"
            };
            bouton.Click += delegate { _toile.BasculerPuces(type); };
            return bouton;
        }

        private ToggleButton BoutonBascule(FrameworkElement contenu, string infobulle, string propriete)
        {
            ToggleButton bouton = new ToggleButton();
            bouton.Content = contenu;
            bouton.ToolTip = infobulle;
            bouton.Width = 30;
            bouton.Height = 26;
            bouton.VerticalAlignment = VerticalAlignment.Center;
            bouton.Margin = new Thickness(2, 0, 0, 0);
            bouton.Focusable = false; // ne vole pas le focus de l'éditeur
            bouton.Click += delegate
            {
                if (_synchroEnCours) return;
                _toile.AppliquerFormatTexte(propriete, null);
            };
            return bouton;
        }

        private ToggleButton BoutonAlignement(string icone, string infobulle, string valeur)
        {
            ToggleButton bouton = new ToggleButton();
            bouton.Content = Icones.Creer(icone, 14);
            bouton.ToolTip = infobulle;
            bouton.Width = 30;
            bouton.Height = 26;
            bouton.VerticalAlignment = VerticalAlignment.Center;
            bouton.Margin = new Thickness(2, 0, 0, 0);
            bouton.Focusable = false;
            bouton.Click += delegate
            {
                if (_synchroEnCours) return;
                _toile.AppliquerFormatTexte("alignement", valeur);
            };
            return bouton;
        }

        private const string TagPersonnalisee = "__perso__";
        private DocumentCarte _documentCouleurs;
        private int _nbCouleursPerso;

        private void RemplirCombosCouleurs()
        {
            _documentCouleurs = _toile.Document;
            _nbCouleursPerso = _toile.Document.CouleursPersonnalisees.Count;
            if (_comboCouleur != null) RemplirComboCouleurs(_comboCouleur, TexteRiche.Couleurs, false);
            if (_comboSurlignage != null) RemplirComboCouleurs(_comboSurlignage, TexteRiche.Surlignages, true);
            if (_comboFond != null) RemplirComboFond();
            if (_comboBordureCouleur != null) RemplirComboBordure();
            if (_comboCouleurGroupe != null) RemplirComboCouleurs(_comboCouleurGroupe, TexteRiche.Couleurs, false);
            if (_comboCouleurLien != null) RemplirComboCouleurs(_comboCouleurLien, TexteRiche.Couleurs, false);
            if (_comboCouleurTitre != null) RemplirComboTitre(_comboCouleurTitre, "Couleur du cadre");
            if (_comboFondTitre != null) RemplirComboTitre(_comboFondTitre, "Fond du tableau");
        }

        private void RemplirComboFond()
        {
            bool synchroPrecedente = _synchroEnCours;
            _synchroEnCours = true;
            try
            {
                _comboFond.Items.Clear();
                _comboFond.Items.Add(ElementCouleur("Blanc", "#FFFFFF"));
                _comboFond.Items.Add(ElementCouleur("Transparent", "Transparent"));
                for (int i = 0; i < TexteRiche.Surlignages.GetLength(0); i++)
                    _comboFond.Items.Add(ElementCouleur(TexteRiche.Surlignages[i, 0], TexteRiche.Surlignages[i, 1]));
                for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                    _comboFond.Items.Add(ElementCouleur(TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1]));
                foreach (string hexa in _toile.Document.CouleursPersonnalisees)
                    _comboFond.Items.Add(ElementCouleur(hexa, hexa));
                _comboFond.Items.Add(new ComboBoxItem { Content = "Personnalisée…", Tag = TagPersonnalisee });
            }
            finally { _synchroEnCours = synchroPrecedente; }
        }

        private void RemplirComboBordure()
        {
            bool synchroPrecedente = _synchroEnCours;
            _synchroEnCours = true;
            try
            {
                _comboBordureCouleur.Items.Clear();
                _comboBordureCouleur.Items.Add(ElementCouleur("Gris sable", "#B8B2A7"));
                for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                    _comboBordureCouleur.Items.Add(ElementCouleur(TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1]));
                foreach (string hexa in _toile.Document.CouleursPersonnalisees)
                    _comboBordureCouleur.Items.Add(ElementCouleur(hexa, hexa));
                _comboBordureCouleur.Items.Add(new ComboBoxItem { Content = "Personnalisée…", Tag = TagPersonnalisee });
            }
            finally { _synchroEnCours = synchroPrecedente; }
        }

        private void RemplirComboCouleurs(ComboBox combo, string[,] palette, bool avecAucun)
        {
            bool synchroPrecedente = _synchroEnCours;
            _synchroEnCours = true;
            try
            {
                combo.Items.Clear();
                if (avecAucun)
                    combo.Items.Add(new ComboBoxItem { Content = "Aucun", Tag = null });
                for (int i = 0; i < palette.GetLength(0); i++)
                    combo.Items.Add(ElementCouleur(palette[i, 0], palette[i, 1]));
                foreach (string hexa in _toile.Document.CouleursPersonnalisees)
                    combo.Items.Add(ElementCouleur(hexa, hexa));
                combo.Items.Add(new ComboBoxItem { Content = "Personnalisée…", Tag = TagPersonnalisee });
            }
            finally
            {
                _synchroEnCours = synchroPrecedente;
            }
        }

        /// <summary>Combo de couleur de titre de groupe : « hériter » ("") + palette + perso.</summary>
        private void RemplirComboTitre(ComboBox combo, string nomHeritage)
        {
            bool synchroPrecedente = _synchroEnCours;
            _synchroEnCours = true;
            try
            {
                combo.Items.Clear();
                combo.Items.Add(new ComboBoxItem { Content = nomHeritage, Tag = "" });
                for (int i = 0; i < TexteRiche.Couleurs.GetLength(0); i++)
                    combo.Items.Add(ElementCouleur(TexteRiche.Couleurs[i, 0], TexteRiche.Couleurs[i, 1]));
                for (int i = 0; i < TexteRiche.Surlignages.GetLength(0); i++)
                    combo.Items.Add(ElementCouleur(TexteRiche.Surlignages[i, 0], TexteRiche.Surlignages[i, 1]));
                foreach (string hexa in _toile.Document.CouleursPersonnalisees)
                    combo.Items.Add(ElementCouleur(hexa, hexa));
                combo.Items.Add(new ComboBoxItem { Content = "Personnalisée…", Tag = TagPersonnalisee });
            }
            finally { _synchroEnCours = synchroPrecedente; }
        }

        private static ComboBoxItem ElementCouleur(string nom, string hexa)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
            ligne.Children.Add(new Rectangle
            {
                Width = 12,
                Height = 12,
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 5, 0),
                Fill = new SolidColorBrush(TexteRiche.CouleurDepuis(hexa, Colors.Gray))
            });
            ligne.Children.Add(new TextBlock { Text = nom });
            return new ComboBoxItem { Content = ligne, Tag = hexa };
        }

        private void CouleurChoisie(ComboBox combo, string propriete)
        {
            if (_synchroEnCours) return;
            ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
            if (choix == null) return;
            string tag = choix.Tag as string;
            if (tag == TagPersonnalisee)
            {
                _toile.ChoisirCouleurPersonnalisee(propriete);
                return;
            }
            // Tag null = « Aucun » (surlignage uniquement).
            _toile.AppliquerFormatTexte(propriete, tag);
        }

        private void AppliquerPoliceSaisie()
        {
            if (_synchroEnCours) return;
            string texte = _comboPolice.Text;
            if (string.IsNullOrWhiteSpace(texte)) return;
            foreach (string police in TexteRiche.PolicesSysteme())
            {
                if (string.Equals(police, texte.Trim(), StringComparison.CurrentCultureIgnoreCase))
                {
                    _toile.AppliquerFormatTexte("police", police);
                    return;
                }
            }
        }

        private void AppliquerTailleSaisie()
        {
            if (_synchroEnCours) return;
            double? taille = DialogueSaisie.InterpreterTaille(_comboTaille.Text);
            if (taille.HasValue) _toile.AppliquerFormatTexte("taille", taille.Value);
        }

        private static Border Separateur()
        {
            return new Border
            {
                Width = 1,
                Margin = new Thickness(7, 2, 5, 2),
                Background = Chrome.Bordure
            };
        }

        private void MettreAJourBarreFormat()
        {
            if (_toile.Document != _documentCouleurs
                || _toile.Document.CouleursPersonnalisees.Count != _nbCouleursPerso)
                RemplirCombosCouleurs();

            FormatCourant etat = _toile.LireFormatCourant();
            _synchroEnCours = true;
            try
            {
                // Onglet automatique selon la sélection (sauf épinglé) ; grisé sans cible.
                bool epingle = _epingleOnglet != null && _epingleOnglet.IsChecked == true;
                if (!epingle)
                {
                    if (_toile.LienSelectionne != null) SelectionOnglet("lien");
                    else if (_toile.GroupeSelectionne != null) SelectionOnglet("groupe");
                    else if (etat.Actif && (_ongletActif == "groupe" || _ongletActif == "lien"))
                        SelectionOnglet("texte");
                }
                bool lecture = _toile.Mode == "lecture";
                _panneauTexte.IsEnabled = etat.Actif && !lecture;
                MettreAJourBarreBoites(etat);
                MettreAJourBarreGroupes();
                MettreAJourBarreLiens();
                MettreAJourInspecteur();

                _comboPolice.Text = etat.Police;
                _comboTaille.Text = etat.Taille.ToString(System.Globalization.CultureInfo.CurrentCulture);

                _btnGras.IsChecked = etat.Gras;
                _btnItalique.IsChecked = etat.Italique;
                _btnSouligne.IsChecked = etat.Souligne;
                _btnBarre.IsChecked = etat.Barre;

                _comboCouleur.SelectedIndex = -1;
                foreach (object objet in _comboCouleur.Items)
                {
                    ComboBoxItem element = (ComboBoxItem)objet;
                    if ((string)element.Tag == etat.Couleur) { _comboCouleur.SelectedItem = element; break; }
                }
                _comboSurlignage.SelectedIndex = -1;
                foreach (object objet in _comboSurlignage.Items)
                {
                    ComboBoxItem element = (ComboBoxItem)objet;
                    if ((string)element.Tag == etat.Surlignage) { _comboSurlignage.SelectedItem = element; break; }
                }

                _btnAlignGauche.IsChecked = etat.Alignement == "gauche";
                _btnAlignCentre.IsChecked = etat.Alignement == "centre";
                _btnAlignDroite.IsChecked = etat.Alignement == "droite";
            }
            finally
            {
                _synchroEnCours = false;
            }
        }

        private Border ConstruireBarreEtat()
        {
            // Les raccourcis vivent désormais dans le Tust (menu Fenêtres) : le pied reste sobre.
            DockPanel contenu = new DockPanel { LastChildFill = false };
            DockPanel.SetDock(_texteZoom, Dock.Right);
            contenu.Children.Add(_texteZoom);

            Border bordure = new Border();
            bordure.BorderThickness = new Thickness(0, 1, 0, 0);
            bordure.BorderBrush = Chrome.Bordure;
            bordure.Background = Chrome.FondBarre;
            bordure.Child = contenu;
            return bordure;
        }

        private void BrancherCommandes()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.New,
                delegate { Nouveau(); }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open,
                delegate { Ouvrir(); }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save,
                delegate { Enregistrer(); }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs,
                delegate { EnregistrerSous(); }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
                delegate { _toile.Historique.Annuler(); },
                delegate(object s, CanExecuteRoutedEventArgs e) { e.CanExecute = _toile.Historique.PeutAnnuler; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
                delegate { _toile.Historique.Refaire(); },
                delegate(object s, CanExecuteRoutedEventArgs e) { e.CanExecute = _toile.Historique.PeutRefaire; }));
            CommandBindings.Add(new CommandBinding(CmdZoomAvant, delegate { _toile.ZoomAvant(); }));
            CommandBindings.Add(new CommandBinding(CmdZoomArriere, delegate { _toile.ZoomArriere(); }));
            CommandBindings.Add(new CommandBinding(CmdZoomCent, delegate { _toile.ZoomReinitialiser(); }));
            CommandBindings.Add(new CommandBinding(CmdRecentrer, delegate { _toile.Recentrer(); }));
            CommandBindings.Add(new CommandBinding(CmdGras, delegate { _toile.AppliquerFormatTexte("gras", null); }));
            CommandBindings.Add(new CommandBinding(CmdItalique, delegate { _toile.AppliquerFormatTexte("italique", null); }));
            CommandBindings.Add(new CommandBinding(CmdSouligne, delegate { _toile.AppliquerFormatTexte("souligne", null); }));
            CommandBindings.Add(new CommandBinding(CmdInsererImage, delegate { ChoisirImage(); }));
            CommandBindings.Add(new CommandBinding(CmdInsererFichier, delegate { ChoisirFichier(); }));
            CommandBindings.Add(new CommandBinding(CmdInsererLienWeb, delegate { _toile.InsererLienWeb(); }));
            CommandBindings.Add(new CommandBinding(CmdToutSelectionner, delegate { _toile.SelectionnerTout(); }));
            CommandBindings.Add(new CommandBinding(CmdNote, delegate { _toile.EditerNoteSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdPlier, delegate { _toile.BasculerPliageCible(); }));
            CommandBindings.Add(new CommandBinding(CmdZoomTout, delegate { _toile.ZoomToutVoir(); }));
            CommandBindings.Add(new CommandBinding(CmdZoomSelection, delegate { _toile.ZoomSurSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdFocus, delegate { _toile.BasculerFocusSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdGrouper, delegate { _toile.GrouperSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdDegrouper, delegate { _toile.DegrouperSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdModeAuto, delegate { _toile.DefinirMode("automatique"); MettreAJourBarreModes(); }));
            CommandBindings.Add(new CommandBinding(CmdModeDeplacement, delegate { _toile.DefinirMode("deplacement"); MettreAJourBarreModes(); }));
            CommandBindings.Add(new CommandBinding(CmdModeLiens, delegate { _toile.DefinirMode("liens"); MettreAJourBarreModes(); }));
            CommandBindings.Add(new CommandBinding(CmdModeLecture, delegate { _toile.DefinirMode("lecture"); MettreAJourBarreModes(); }));
            // Quand l'éditeur de texte a le focus, il gère lui-même copier/coller.
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, delegate { _toile.CopierSelection(); }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, delegate
            {
                if (_toile.PeutCollerBoites()) _toile.CollerBoites();
                else _toile.CollerImage();
            }));
            CommandBindings.Add(new CommandBinding(CmdDupliquer, delegate { _toile.DupliquerSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdRechercher, delegate { OuvrirRecherche(); }));
            CommandBindings.Add(new CommandBinding(CmdEditer, delegate { _toile.EditerSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdImprimer, delegate { Imprimer(); }));
            CommandBindings.Add(new CommandBinding(CmdEmpiler, delegate { _toile.EmpilerSelection(); }));
            CommandBindings.Add(new CommandBinding(CmdPleinEcran, delegate { BasculerPleinEcran(); }));
            CommandBindings.Add(new CommandBinding(CmdPresentation, delegate { BasculerPresentation(); }));

            CommandBindings.Add(new CommandBinding(CmdSupprimer, delegate { _toile.SupprimerSelection(); }));

            // Les raccourcis sont pilotés par Fichier > Paramètres (re-liables, favoris).
            _commandesParAction = new Dictionary<string, ICommand>
            {
                { "nouveau", ApplicationCommands.New },
                { "ouvrir", ApplicationCommands.Open },
                { "enregistrer", ApplicationCommands.Save },
                { "enregistrer-sous", ApplicationCommands.SaveAs },
                { "annuler", ApplicationCommands.Undo },
                { "retablir", ApplicationCommands.Redo },
                { "copier", ApplicationCommands.Copy },
                { "coller", ApplicationCommands.Paste },
                { "dupliquer", CmdDupliquer },
                { "rechercher", CmdRechercher },
                { "editer", CmdEditer },
                { "imprimer", CmdImprimer },
                { "empiler", CmdEmpiler },
                { "plein-ecran", CmdPleinEcran },
                { "presentation", CmdPresentation },
                { "supprimer", CmdSupprimer },
                { "grouper", CmdGrouper },
                { "degrouper", CmdDegrouper },
                { "inserer-image", CmdInsererImage },
                { "inserer-fichier", CmdInsererFichier },
                { "inserer-lien-web", CmdInsererLienWeb },
                { "tout-selectionner", CmdToutSelectionner },
                { "note", CmdNote },
                { "plier", CmdPlier },
                { "zoom-tout", CmdZoomTout },
                { "zoom-selection", CmdZoomSelection },
                { "focus", CmdFocus },
                { "zoom-avant", CmdZoomAvant },
                { "zoom-arriere", CmdZoomArriere },
                { "zoom-100", CmdZoomCent },
                { "recentrer", CmdRecentrer },
                { "mode-automatique", CmdModeAuto },
                { "mode-deplacement", CmdModeDeplacement },
                { "mode-liens", CmdModeLiens },
                { "mode-lecture", CmdModeLecture },
                { "gras", CmdGras },
                { "italique", CmdItalique },
                { "souligne", CmdSouligne },
            };
            AppliquerRaccourcis();
        }

        private void AppliquerRaccourcis()
        {
            // Neutralise les gestes système des commandes Fichier : seul le raccourci configuré
            // reste actif. (Annuler/Rétablir/Coller gardent les leurs — les éditeurs de texte
            // internes en dépendent.)
            ApplicationCommands.New.InputGestures.Clear();
            ApplicationCommands.Open.InputGestures.Clear();
            ApplicationCommands.Save.InputGestures.Clear();
            ApplicationCommands.SaveAs.InputGestures.Clear();

            InputBindings.Clear();
            foreach (DefinitionAction action in ParametresGlobaux.Actions)
            {
                ICommand commande;
                if (!_commandesParAction.TryGetValue(action.Id, out commande)) continue;
                Key touche;
                ModifierKeys modificateurs;
                if (!ParametresGlobaux.InterpreterGeste(ParametresGlobaux.Geste(action.Id), out touche, out modificateurs))
                    continue;
                // Affectation directe : contrairement au constructeur, elle accepte
                // les touches sans modificateur (Suppr, F2…).
                KeyBinding liaison = new KeyBinding { Command = commande };
                liaison.Key = touche;
                liaison.Modifiers = modificateurs;
                InputBindings.Add(liaison);
            }
            // Variantes de confort, hors personnalisation.
            InputBindings.Add(new KeyBinding(CmdZoomAvant, Key.OemPlus, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(CmdZoomArriere, Key.OemMinus, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(CmdZoomCent, Key.NumPad0, ModifierKeys.Control));
        }

        // ------------------------------------------------------------------ clavier

        private void ToucheEnfoncee(object expediteur, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBoxBase) return; // édition en cours : le clavier appartient à l'éditeur
            // Suppr passe désormais par les raccourcis re-liables (Fichier > Paramètres).
            if (e.Key == Key.Space)
            {
                _toile.ModePanClavier(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_presentation) { QuitterPresentation(); e.Handled = true; }
                else if (_pleinEcran) { DefinirPleinEcran(false); e.Handled = true; }
                else if (_toile.FocusActif) { _toile.AnnulerFocus(); e.Handled = true; }
            }
            else if (e.Key == Key.Left) { _toile.NaviguerSelection(-1, 0); e.Handled = true; }
            else if (e.Key == Key.Right) { _toile.NaviguerSelection(1, 0); e.Handled = true; }
            else if (e.Key == Key.Up) { _toile.NaviguerSelection(0, -1); e.Handled = true; }
            else if (e.Key == Key.Down) { _toile.NaviguerSelection(0, 1); e.Handled = true; }
        }

        private void ToucheRelachee(object expediteur, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                _toile.ModePanClavier(false);
        }

        // ------------------------------------------------------------------ fichier

        private MenuItem ElementPanneau(string entete, Action<bool> definir, bool coche)
        {
            MenuItem element = new MenuItem { Header = entete, IsCheckable = true, IsChecked = coche };
            element.Click += delegate
            {
                definir(element.IsChecked == true);
                ParametresGlobaux.Enregistrer();
                MettreAJourPanneaux();
            };
            return element;
        }

        // ------------------------------------------------------------------ panneaux flottants

        private Border PanneauFlottant(HorizontalAlignment horizontal, VerticalAlignment vertical, UIElement contenu)
        {
            return new Border
            {
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = new Thickness(12, 10, 12, 10),
                Background = Chrome.FondPanneau,
                BorderBrush = Chrome.Bordure,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 7, 10, 8),
                Visibility = Visibility.Collapsed,
                Child = contenu
            };
        }

        private Border ConstruireMiniCarte()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(new TextBlock { Text = "Mini-carte", FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            _miniCanvas = new Canvas
            {
                Width = 166,
                Height = 106,
                Background = Chrome.FondMiniCarte,
                ClipToBounds = true
            };
            _miniCanvas.MouseLeftButtonDown += delegate(object expediteur, MouseButtonEventArgs e)
            {
                Point p = e.GetPosition(_miniCanvas);
                _toile.CentrerSur(new Point(_miniOrigine.X + p.X / _miniEchelle, _miniOrigine.Y + p.Y / _miniEchelle));
            };
            contenu.Children.Add(_miniCanvas);
            return PanneauFlottant(HorizontalAlignment.Left, VerticalAlignment.Bottom, contenu);
        }

        private void MettreAJourMiniCarte()
        {
            if (_miniCanvas == null || _miniCarte.Visibility != Visibility.Visible) return;
            _miniCanvas.Children.Clear();
            double zoom = _toile.Zoom;
            if (zoom <= 0 || _toile.ActualWidth <= 0) return;
            Point origine = _toile.OrigineVue;
            Rect vue = new Rect(-origine.X / zoom, -origine.Y / zoom,
                _toile.ActualWidth / zoom, _toile.ActualHeight / zoom);
            Rect monde = vue;
            List<Rect> rectangles = _toile.RectanglesNoeuds();
            foreach (Rect r in rectangles) monde.Union(r);
            if (monde.Width < 1 || monde.Height < 1) return;
            _miniEchelle = Math.Min(_miniCanvas.Width / monde.Width, _miniCanvas.Height / monde.Height);
            _miniOrigine = monde.TopLeft;
            SolidColorBrush gris = new SolidColorBrush(Color.FromRgb(0xA9, 0xA2, 0x97));
            foreach (Rect r in rectangles)
            {
                Rectangle forme = new Rectangle
                {
                    Width = Math.Max(2, r.Width * _miniEchelle),
                    Height = Math.Max(2, r.Height * _miniEchelle),
                    Fill = gris,
                    RadiusX = 1,
                    RadiusY = 1
                };
                Canvas.SetLeft(forme, (r.X - monde.X) * _miniEchelle);
                Canvas.SetTop(forme, (r.Y - monde.Y) * _miniEchelle);
                _miniCanvas.Children.Add(forme);
            }
            Rectangle cadreVue = new Rectangle
            {
                Width = Math.Max(4, vue.Width * _miniEchelle),
                Height = Math.Max(4, vue.Height * _miniEchelle),
                Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x78, 0xD7))
            };
            Canvas.SetLeft(cadreVue, (vue.X - monde.X) * _miniEchelle);
            Canvas.SetTop(cadreVue, (vue.Y - monde.Y) * _miniEchelle);
            _miniCanvas.Children.Add(cadreVue);
        }

        private Border ConstruireInspecteur()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(new TextBlock { Text = "Inspecteur", FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            _inspecteurTexte = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 220 };
            contenu.Children.Add(_inspecteurTexte);
            return PanneauFlottant(HorizontalAlignment.Right, VerticalAlignment.Bottom, contenu);
        }

        private void MettreAJourInspecteur()
        {
            if (_inspecteurTexte == null || _inspecteur.Visibility != Visibility.Visible) return;
            Lien lien = _toile.LienSelectionne;
            Groupe groupe = _toile.GroupeSelectionne;
            Noeud noeud = _toile.NoeudSelectionne;
            if (lien != null)
            {
                _inspecteurTexte.Text = string.Format(
                    "Lien\nCouleur : {0}\nÉpaisseur : {1} · Trait : {2}\nFlèche : {3} · Tracé : {4}{5}",
                    lien.Couleur, lien.Epaisseur, lien.Trait, lien.Fleche, lien.Trace,
                    string.IsNullOrEmpty(lien.Etiquette) ? "" : "\nÉtiquette : « " + lien.Etiquette + " »");
            }
            else if (groupe != null)
            {
                _inspecteurTexte.Text = string.Format(
                    "Groupe{0}\n{1} boîte(s) · Couleur : {2}",
                    string.IsNullOrEmpty(groupe.Titre) ? "" : " « " + groupe.Titre + " »",
                    groupe.NoeudIds.Count, groupe.Couleur);
            }
            else if (noeud != null)
            {
                Rect rect = _toile.RectangleDuNoeud(noeud);
                string type = noeud.EstImage ? "Boîte-image" : "Boîte";
                if (noeud.Fichiers.Count > 0)
                    type += string.Format(" · {0} lien{1}", noeud.Fichiers.Count, noeud.Fichiers.Count > 1 ? "s" : "");
                string extra = _toile.NbSelectionnes > 1
                    ? string.Format("\n({0} boîtes sélectionnées)", _toile.NbSelectionnes) : "";
                if (noeud.Etiquettes.Count > 0) extra += "\nÉtiquettes : " + string.Join(", ", noeud.Etiquettes.ToArray());
                string cases = ToileVue.ProgressionBoite(noeud);
                if (cases.Length > 0) extra += "\nCases cochées : " + cases;
                if (noeud.ANote)
                {
                    string note = noeud.Note.Replace("\r", "").Replace("\n", " ");
                    extra += "\nNote : " + (note.Length > 60 ? note.Substring(0, 60) + "…" : note);
                }
                _inspecteurTexte.Text = string.Format(
                    "{0}\nX : {1:0} · Y : {2:0}\nLargeur : {3:0} · Hauteur : {4:0}{5}",
                    type, rect.X, rect.Y, rect.Width, rect.Height, extra);
            }
            else
            {
                _inspecteurTexte.Text = "Aucune sélection";
            }
        }

        private Border ConstruireHistorique()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(new TextBlock { Text = "Historique", FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            _historiqueTexte = new TextBlock { FontSize = 12 };
            contenu.Children.Add(_historiqueTexte);
            StackPanel boutons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            Button annuler = new Button { Content = "↶ Annuler", Padding = new Thickness(6, 0, 6, 0), Focusable = false };
            annuler.Click += delegate { _toile.Historique.Annuler(); };
            Button retablir = new Button { Content = "↷ Rétablir", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(4, 0, 0, 0), Focusable = false };
            retablir.Click += delegate { _toile.Historique.Refaire(); };
            boutons.Children.Add(annuler);
            boutons.Children.Add(retablir);
            contenu.Children.Add(boutons);
            return PanneauFlottant(HorizontalAlignment.Left, VerticalAlignment.Top, contenu);
        }

        private void MettreAJourHistorique()
        {
            if (_historiqueTexte == null || _panneauHistorique.Visibility != Visibility.Visible) return;
            _historiqueTexte.Text = string.Format("{0} action(s) annulable(s)\n{1} action(s) rétablissable(s)",
                _toile.Historique.NbAnnulables, _toile.Historique.NbRefaisables);
        }

        private void MettreAJourPanneaux()
        {
            if (_miniCarte == null) return;
            _miniCarte.Visibility = ParametresGlobaux.MiniCarteVisible ? Visibility.Visible : Visibility.Collapsed;
            _inspecteur.Visibility = ParametresGlobaux.InspecteurVisible ? Visibility.Visible : Visibility.Collapsed;
            _panneauHistorique.Visibility = ParametresGlobaux.HistoriqueVisible ? Visibility.Visible : Visibility.Collapsed;
            if (_panneauFiltre != null)
            {
                _panneauFiltre.Visibility = ParametresGlobaux.FiltreVisible ? Visibility.Visible : Visibility.Collapsed;
                if (!ParametresGlobaux.FiltreVisible && _toile.FiltreActif) _toile.DefinirFiltreEtiquettes(null, false);
                if (_menuFiltre != null) _menuFiltre.IsChecked = ParametresGlobaux.FiltreVisible;
                MettreAJourPanneauFiltre();
            }
            if (_menuMiniCarte != null) _menuMiniCarte.IsChecked = ParametresGlobaux.MiniCarteVisible;
            if (_menuInspecteur != null) _menuInspecteur.IsChecked = ParametresGlobaux.InspecteurVisible;
            if (_menuHistorique != null) _menuHistorique.IsChecked = ParametresGlobaux.HistoriqueVisible;
            MettreAJourMiniCarte();
            MettreAJourInspecteur();
            MettreAJourHistorique();
        }

        // ------------------------------------------------------------------ filtre par étiquettes (v0.15)

        /// <summary>Panneau flottant en haut à gauche : une case par étiquette du fichier, plus
        /// « sans étiquette ». Dès qu'une case est cochée, les boîtes qui ne correspondent pas
        /// s'estompent. Fermer le panneau lève le filtre.</summary>
        private Border ConstruirePanneauFiltre()
        {
            _filtreContenu = new StackPanel { MinWidth = 170 };
            Border panneau = PanneauFlottant(HorizontalAlignment.Left, VerticalAlignment.Top, _filtreContenu);
            panneau.Visibility = Visibility.Collapsed;
            return panneau;
        }

        private void MettreAJourPanneauFiltre()
        {
            if (_filtreContenu == null) return;
            _filtreContenu.Children.Clear();
            TextBlock titre = new TextBlock
            {
                Text = "Filtre par étiquettes",
                FontWeight = FontWeights.SemiBold,
                Foreground = Chrome.Encre,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _filtreContenu.Children.Add(titre);
            HashSet<string> actives = _toile.FiltreEtiquettes;
            List<Etiquette> definitions = _toile.Document.Etiquettes;
            if (definitions.Count == 0)
            {
                _filtreContenu.Children.Add(new TextBlock
                {
                    Text = "Aucune étiquette dans ce fichier.\nClic droit sur une boîte > Étiquettes.",
                    Foreground = Chrome.TexteDoux,
                    TextWrapping = TextWrapping.Wrap
                });
            }
            foreach (Etiquette e in definitions)
            {
                string nom = e.Nom;
                StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
                ligne.Children.Add(new Border
                {
                    Width = 11, Height = 11, CornerRadius = new CornerRadius(5.5),
                    Background = new SolidColorBrush(TexteRiche.CouleurDepuis(e.Couleur, Colors.SteelBlue)),
                    Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
                });
                ligne.Children.Add(new TextBlock { Text = nom, VerticalAlignment = VerticalAlignment.Center });
                ligne.Children.Add(new TextBlock
                {
                    Text = " (" + _toile.CompterPorteurs(nom) + ")",
                    Foreground = Chrome.TexteDoux, VerticalAlignment = VerticalAlignment.Center
                });
                CheckBox case_ = new CheckBox
                {
                    Content = ligne,
                    IsChecked = actives != null && actives.Contains(nom),
                    Margin = new Thickness(0, 2, 0, 2),
                    Focusable = false
                };
                case_.Click += delegate { AppliquerFiltreDepuisPanneau(); };
                _filtreContenu.Children.Add(case_);
            }
            if (definitions.Count > 0)
            {
                CheckBox sans = new CheckBox
                {
                    Content = new TextBlock { Text = "Sans étiquette", Foreground = Chrome.TexteDoux },
                    IsChecked = actives != null && _toile.FiltreSansEtiquette,
                    Margin = new Thickness(0, 6, 0, 2),
                    Focusable = false,
                    Tag = "sans"
                };
                sans.Click += delegate { AppliquerFiltreDepuisPanneau(); };
                _filtreContenu.Children.Add(sans);
            }
            StackPanel boutons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            Button tout = new Button { Content = "Tout afficher", Focusable = false, Padding = new Thickness(8, 2, 8, 2), IsEnabled = actives != null };
            tout.Click += delegate { _toile.DefinirFiltreEtiquettes(null, false); MettreAJourPanneauFiltre(); };
            boutons.Children.Add(tout);
            Button gerer = new Button { Content = "Gérer…", Focusable = false, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
            gerer.Click += delegate { DialogueEtiquettes.Ouvrir(this, _toile); MettreAJourPanneauFiltre(); };
            boutons.Children.Add(gerer);
            _filtreContenu.Children.Add(boutons);
        }

        private void AppliquerFiltreDepuisPanneau()
        {
            HashSet<string> actives = new HashSet<string>();
            bool sans = false;
            foreach (UIElement enfant in _filtreContenu.Children)
            {
                CheckBox case_ = enfant as CheckBox;
                if (case_ == null || case_.IsChecked != true) continue;
                if ((case_.Tag as string) == "sans") { sans = true; continue; }
                StackPanel ligne = case_.Content as StackPanel;
                if (ligne != null && ligne.Children.Count > 1)
                    actives.Add(((TextBlock)ligne.Children[1]).Text);
            }
            if (actives.Count == 0 && !sans) _toile.DefinirFiltreEtiquettes(null, false);
            else _toile.DefinirFiltreEtiquettes(actives, sans);
            MettreAJourPanneauFiltre();
        }

        // ------------------------------------------------------------------ Tust des raccourcis

        private Border ConstruireTust()
        {
            _tustContenu = new StackPanel();
            return new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 12, 0),
                Background = Chrome.FondPanneau,
                BorderBrush = Chrome.Bordure,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 10),
                MaxWidth = 290,
                Visibility = Visibility.Collapsed,
                Child = _tustContenu
            };
        }

        private void MettreAJourTust()
        {
            if (_tust == null) return;
            if (_menuTust != null) _menuTust.IsChecked = ParametresGlobaux.TustVisible;
            _tust.Visibility = ParametresGlobaux.TustVisible ? Visibility.Visible : Visibility.Collapsed;
            _tustContenu.Children.Clear();
            _tustContenu.Children.Add(new TextBlock
            {
                Text = "Tust des raccourcis",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            string[] bases =
            {
                "Double-clic : créer une boîte",
                "Poignées bleues : créer un lien",
                "Ctrl+clic / rectangle : sélection multiple",
                "Ctrl+molette : zoom",
                "Espace+glisser : naviguer"
            };
            foreach (string ligne in bases)
                _tustContenu.Children.Add(new TextBlock { Text = ligne, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (ParametresGlobaux.Favoris.Count > 0)
            {
                _tustContenu.Children.Add(new Border
                {
                    Height = 1,
                    Background = Chrome.Bordure,
                    Margin = new Thickness(0, 6, 0, 6)
                });
                foreach (DefinitionAction action in ParametresGlobaux.Actions)
                {
                    if (!ParametresGlobaux.Favoris.Contains(action.Id)) continue;
                    _tustContenu.Children.Add(new TextBlock
                    {
                        Text = action.Nom + " : " + ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(action.Id)),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
        }

        private void OuvrirPreferencesFichier()
        {
            if (DialoguePreferencesFichier.Afficher(this, _toile.Document, _toile.RectanglesNoeuds()))
            {
                _toile.AppliquerPreferencesDocument(true);
                RemplirCombosCouleurs();
                MettreAJourBarreFormat();
                MettreAJourTitre();
            }
        }

        private void OuvrirParametres()
        {
            DialogueParametres.Afficher(this);
            AppliquerRaccourcis();
            MettreAJourTust();
            MettreAJourVignette();
        }

        // ------------------------------------------------------------------ plein écran & présentation

        private void BasculerPleinEcran()
        {
            if (_presentation) { QuitterPresentation(); return; }
            DefinirPleinEcran(!_pleinEcran);
        }

        private void DefinirPleinEcran(bool actif)
        {
            if (_pleinEcran == actif) return;
            _pleinEcran = actif;
            if (actif)
            {
                _etatAvantPleinEcran = WindowState;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                // Repasser par Normal force le recalcul : Maximized couvre alors l'écran entier.
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                WindowState = _etatAvantPleinEcran;
            }
            MettreAJourMenusAffichage();
        }

        private void BasculerPresentation()
        {
            if (_presentation) QuitterPresentation();
            else EntrerPresentation();
        }

        /// <summary>Mode présentation : plein écran, chrome masqué, carte en lecture seule.
        /// Échap restaure tout.</summary>
        private void EntrerPresentation()
        {
            if (_presentation) return;
            _presentation = true;
            _modeAvantPresentation = _toile.Mode;
            _toile.DefinirMode("lecture");
            _menuPrincipal.Visibility = Visibility.Collapsed;
            _barreModes.Visibility = Visibility.Collapsed;
            _barreFormat.Visibility = Visibility.Collapsed;
            _barreEtat.Visibility = Visibility.Collapsed;
            DefinirPleinEcran(true);
            MettreAJourMenusAffichage();
            _toile.Focus();
        }

        private void QuitterPresentation()
        {
            if (!_presentation) return;
            _presentation = false;
            _menuPrincipal.Visibility = Visibility.Visible;
            _barreModes.Visibility = Visibility.Visible;
            _barreFormat.Visibility = Visibility.Visible;
            _barreEtat.Visibility = Visibility.Visible;
            DefinirPleinEcran(false);
            _toile.DefinirMode(string.IsNullOrEmpty(_modeAvantPresentation) ? "automatique" : _modeAvantPresentation);
            MettreAJourBarreModes();
            MettreAJourMenusAffichage();
        }

        private void MettreAJourMenusAffichage()
        {
            if (_menuPleinEcran != null) _menuPleinEcran.IsChecked = _pleinEcran && !_presentation;
            if (_menuPresentation != null) _menuPresentation.IsChecked = _presentation;
        }

        // ------------------------------------------------------------------ impression & PDF

        private void Imprimer()
        {
            string erreur;
            BitmapSource carte = _toile.RendreCarte(out erreur);
            if (carte == null)
            {
                DialogueMessage.Afficher(this, string.Format("Impression impossible :\n{0}", erreur),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PrintDialog dialogue = new PrintDialog();
            if (dialogue.ShowDialog() != true) return;
            try
            {
                double largeurZone = dialogue.PrintableAreaWidth;
                double hauteurZone = dialogue.PrintableAreaHeight;
                double echelle = Math.Min(largeurZone / carte.Width, hauteurZone / carte.Height);
                double largeur = carte.Width * echelle, hauteur = carte.Height * echelle;
                DrawingVisual visuel = new DrawingVisual();
                using (DrawingContext contexte = visuel.RenderOpen())
                    contexte.DrawImage(carte, new Rect(
                        (largeurZone - largeur) / 2, (hauteurZone - hauteur) / 2, largeur, hauteur));
                dialogue.PrintVisual(visuel, "Mental-o — " + NomExport());
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this, string.Format("Impression impossible :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>Export PDF sans dépendance : la carte rendue en image passe par
        /// l'imprimante « Microsoft Print to PDF » de Windows, fichier cible imposé.</summary>
        private void ExporterPdf()
        {
            string chemin = DemanderCheminExport("Document PDF (*.pdf)|*.pdf", ".pdf");
            if (chemin == null) return;
            string erreur;
            BitmapSource carte = _toile.RendreCarte(out erreur);
            if (carte == null)
            {
                DialogueMessage.Afficher(this, string.Format("Export impossible :\n{0}", erreur),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                System.Drawing.Printing.PrintDocument document = new System.Drawing.Printing.PrintDocument();
                document.PrinterSettings.PrinterName = "Microsoft Print to PDF";
                if (!document.PrinterSettings.IsValid)
                {
                    DialogueMessage.Afficher(this,
                        "L'imprimante « Microsoft Print to PDF » est introuvable sur cette machine.\n" +
                        "Passez par Fichier > Imprimer et choisissez une imprimante PDF.",
                        "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                document.PrinterSettings.PrintToFile = true;
                document.PrinterSettings.PrintFileName = chemin;
                document.DefaultPageSettings.Landscape = carte.Width > carte.Height;
                document.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(30, 30, 30, 30);
                document.PrintController = new System.Drawing.Printing.StandardPrintController(); // pas de fenêtre de progression
                System.Drawing.Bitmap image = ConvertirEnBitmap(carte);
                document.PrintPage += delegate(object expediteur, System.Drawing.Printing.PrintPageEventArgs e)
                {
                    System.Drawing.Rectangle zone = e.MarginBounds;
                    double echelle = Math.Min((double)zone.Width / image.Width, (double)zone.Height / image.Height);
                    int largeur = Math.Max(1, (int)(image.Width * echelle));
                    int hauteur = Math.Max(1, (int)(image.Height * echelle));
                    e.Graphics.DrawImage(image, zone.X + (zone.Width - largeur) / 2,
                        zone.Y + (zone.Height - hauteur) / 2, largeur, hauteur);
                    e.HasMorePages = false;
                };
                document.Print();
                image.Dispose();
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this, string.Format("Export impossible :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static System.Drawing.Bitmap ConvertirEnBitmap(BitmapSource source)
        {
            PngBitmapEncoder encodeur = new PngBitmapEncoder();
            encodeur.Frames.Add(BitmapFrame.Create(source));
            using (MemoryStream flux = new MemoryStream())
            {
                encodeur.Save(flux);
                flux.Position = 0;
                using (System.Drawing.Bitmap liee = new System.Drawing.Bitmap(flux))
                    return new System.Drawing.Bitmap(liee); // copie détachée du flux
            }
        }

        // ------------------------------------------------------------------ import / export

        /// <summary>Le nom proposé pour un fichier exporté, calqué sur la carte ouverte.</summary>
        private string NomExport()
        {
            if (!string.IsNullOrWhiteSpace(_toile.Document.NomAffichage)) return _toile.Document.NomAffichage;
            if (_cheminFichier != null) return System.IO.Path.GetFileNameWithoutExtension(_cheminFichier);
            return "ma carte";
        }

        private string DemanderCheminExport(string filtre, string extension)
        {
            SaveFileDialog dialogue = new SaveFileDialog();
            dialogue.Filter = filtre;
            dialogue.DefaultExt = extension;
            dialogue.FileName = NomExport();
            return dialogue.ShowDialog(this) == true ? dialogue.FileName : null;
        }

        private void ExporterImage()
        {
            string chemin = DemanderCheminExport("Image PNG (*.png)|*.png", ".png");
            if (chemin == null) return;
            string erreur;
            if (!_toile.ExporterPng(chemin, out erreur))
                DialogueMessage.Afficher(this, string.Format("Export impossible :\n{0}", erreur),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ExporterSvg()
        {
            string chemin = DemanderCheminExport("Dessin SVG (*.svg)|*.svg", ".svg");
            if (chemin == null) return;
            try
            {
                Export.ExporterSvg(_toile, chemin);
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this, string.Format("Export impossible :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExporterMarkdown()
        {
            string chemin = DemanderCheminExport("Texte Markdown (*.md)|*.md", ".md");
            if (chemin == null) return;
            try
            {
                Export.ExporterMarkdown(_toile.Document, NomExport(), chemin);
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this, string.Format("Export impossible :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExporterOpml()
        {
            string chemin = DemanderCheminExport("Plan OPML (*.opml)|*.opml", ".opml");
            if (chemin == null) return;
            try
            {
                Export.ExporterOpml(_toile.Document, NomExport(), chemin);
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this, string.Format("Export impossible :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ImporterOpml()
        {
            OpenFileDialog dialogue = new OpenFileDialog();
            dialogue.Filter = "Plan OPML (*.opml;*.xml)|*.opml;*.xml|Tous les fichiers (*.*)|*.*";
            dialogue.DefaultExt = ".opml";
            if (dialogue.ShowDialog(this) != true) return;
            try
            {
                List<Noeud> noeuds;
                List<Lien> liens;
                Export.ImporterOpml(dialogue.FileName, _toile.CentreCanvas(), out noeuds, out liens);
                if (noeuds.Count == 0)
                {
                    DialogueMessage.Afficher(this, "Aucun élément exploitable dans ce plan.",
                        "Mental-o", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                _toile.ImporterArbre(noeuds, liens);
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this, string.Format("Import impossible :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ImporterTexteAPuces()
        {
            string texte = DialogueSaisie.DemanderTexteMultiligne(this, "Importer un texte à puces",
                "Chaque ligne devient une boîte ; l'indentation (espaces ou tabulations)\n" +
                "fait de la ligne l'enfant de la précédente, reliée par une flèche.");
            if (texte == null) return;
            List<Noeud> noeuds;
            List<Lien> liens;
            Export.ImporterTexteAPuces(texte, _toile.CentreCanvas(), out noeuds, out liens);
            if (noeuds.Count == 0)
            {
                DialogueMessage.Afficher(this, "Aucune ligne exploitable dans ce texte.",
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _toile.ImporterArbre(noeuds, liens);
        }

        private void ChoisirImage()
        {
            OpenFileDialog dialogue = new OpenFileDialog();
            dialogue.Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Tous les fichiers (*.*)|*.*";
            if (dialogue.ShowDialog(this) == true)
                _toile.InsererImageDepuisFichier(dialogue.FileName);
        }

        /// <summary>Insertion > Fichier : un ou plusieurs fichiers, liés à la boîte sélectionnée
        /// ou placés dans des boîtes neuves. Une image choisie ici suit la voie des images.</summary>
        private void ChoisirFichier()
        {
            OpenFileDialog dialogue = new OpenFileDialog();
            dialogue.Title = "Insérer un ou plusieurs fichiers";
            dialogue.Filter = "Tous les fichiers (*.*)|*.*";
            dialogue.Multiselect = true;
            if (dialogue.ShowDialog(this) != true) return;
            foreach (string chemin in dialogue.FileNames)
                _toile.InsererFichierDepuisChemin(chemin);
        }

        private void Nouveau()
        {
            if (!ConfirmerAbandon()) return;
            _toile.ChargerDocument(new DocumentCarte());
            _cheminFichier = null;
            _modifie = false;
            MettreAJourTitre();
        }

        private void Ouvrir()
        {
            if (!ConfirmerAbandon()) return;
            OpenFileDialog dialogue = new OpenFileDialog();
            dialogue.Filter = Fichier.FiltreOuverture;
            dialogue.DefaultExt = Fichier.Extension;
            if (dialogue.ShowDialog(this) == true)
                OuvrirFichier(dialogue.FileName);
        }

        public void OuvrirFichier(string chemin)
        {
            try
            {
                DocumentCarte document = Fichier.Charger(chemin);
                _toile.ChargerDocument(document);
                _cheminFichier = chemin;
                _modifie = false;
                MettreAJourTitre();
                RetenirFichierRecent(chemin);
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this,
                    string.Format("Impossible d'ouvrir le fichier :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RetenirFichierRecent(string chemin)
        {
            ParametresGlobaux.AjouterFichierRecent(chemin);
            ParametresGlobaux.Enregistrer();
            RemplirMenuRecents();
        }

        private void RemplirMenuRecents()
        {
            if (_menuRecents == null) return;
            _menuRecents.Items.Clear();
            List<string> recents = ParametresGlobaux.FichiersRecents;
            if (recents.Count == 0)
            {
                _menuRecents.Items.Add(new MenuItem { Header = "(aucun fichier récent)", IsEnabled = false });
                return;
            }
            int numero = 1;
            foreach (string chemin in recents)
            {
                MenuItem element = new MenuItem
                {
                    Header = string.Format("_{0}  {1}", numero++, System.IO.Path.GetFileName(chemin)),
                    ToolTip = chemin
                };
                string capture = chemin;
                element.Click += delegate { OuvrirFichierRecent(capture); };
                _menuRecents.Items.Add(element);
            }
            _menuRecents.Items.Add(new Separator());
            MenuItem vider = new MenuItem { Header = "_Vider la liste" };
            vider.Click += delegate
            {
                ParametresGlobaux.FichiersRecents.Clear();
                ParametresGlobaux.Enregistrer();
                RemplirMenuRecents();
            };
            _menuRecents.Items.Add(vider);
        }

        private void OuvrirFichierRecent(string chemin)
        {
            if (!File.Exists(chemin))
            {
                DialogueMessage.Afficher(this,
                    string.Format("« {0} » n'existe plus : il est retiré de la liste.", chemin),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Warning);
                ParametresGlobaux.FichiersRecents.RemoveAll(delegate(string existant)
                {
                    return string.Equals(existant, chemin, StringComparison.OrdinalIgnoreCase);
                });
                ParametresGlobaux.Enregistrer();
                RemplirMenuRecents();
                return;
            }
            if (!ConfirmerAbandon()) return;
            OuvrirFichier(chemin);
        }

        /// <summary>Enregistre l'association .tea/.zeubi → Mental-o pour l'utilisateur courant
        /// (HKCU, aucun droit administrateur) — l'équivalent d'outils\associer-tea.bat.</summary>
        private void AssocierExtensions()
        {
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string icone = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "ressources", "fichier-tea.ico");
                using (RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                {
                    using (RegistryKey cle = classes.CreateSubKey(".tea"))
                        cle.SetValue(null, "Mentalo.Carte");
                    using (RegistryKey cle = classes.CreateSubKey(".zeubi"))
                        cle.SetValue(null, "Mentalo.Carte");
                    using (RegistryKey cle = classes.CreateSubKey("Mentalo.Carte"))
                        cle.SetValue(null, "Carte Mental-o");
                    if (File.Exists(icone))
                        using (RegistryKey cle = classes.CreateSubKey(@"Mentalo.Carte\DefaultIcon"))
                            cle.SetValue(null, icone);
                    using (RegistryKey cle = classes.CreateSubKey(@"Mentalo.Carte\shell\open\command"))
                        cle.SetValue(null, "\"" + exe + "\" \"%1\"");
                }
                DialogueMessage.Afficher(this,
                    "C'est fait : le double-clic sur un fichier .tea (ou .zeubi) ouvrira Mental-o.\n" +
                    "Si l'Explorateur affiche encore l'ancienne icône, elle se rafraîchira à la prochaine session.",
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this,
                    string.Format("Impossible d'enregistrer l'association :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool Enregistrer()
        {
            if (_cheminFichier == null) return EnregistrerSous();
            return EcrireFichier(_cheminFichier);
        }

        private bool EnregistrerSous()
        {
            SaveFileDialog dialogue = new SaveFileDialog();
            dialogue.Filter = Fichier.FiltreEnregistrement;
            dialogue.DefaultExt = Fichier.Extension;
            dialogue.FileName = _cheminFichier == null ? "ma carte" : System.IO.Path.GetFileNameWithoutExtension(_cheminFichier);
            if (dialogue.ShowDialog(this) != true) return false;
            return EcrireFichier(dialogue.FileName);
        }

        private bool EcrireFichier(string chemin)
        {
            try
            {
                Fichier.Enregistrer(_toile.Document, chemin);
                _cheminFichier = chemin;
                _modifie = false;
                MettreAJourTitre();
                RetenirFichierRecent(chemin);
                return true;
            }
            catch (Exception ex)
            {
                DialogueMessage.Afficher(this,
                    string.Format("Impossible d'enregistrer le fichier :\n{0}", ex.Message),
                    "Mental-o", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool ConfirmerAbandon()
        {
            if (!_modifie) return true;
            string nom = _cheminFichier == null ? "sans titre" : System.IO.Path.GetFileName(_cheminFichier);
            MessageBoxResult reponse = DialogueMessage.Afficher(this,
                string.Format("Voulez-vous enregistrer les modifications de « {0} » ?", nom),
                "Mental-o", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (reponse == MessageBoxResult.Cancel) return false;
            if (reponse == MessageBoxResult.Yes) return Enregistrer();
            return true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!ConfirmerAbandon()) e.Cancel = true;
            base.OnClosing(e);
        }

        private void MettreAJourTitre()
        {
            string nom = !string.IsNullOrWhiteSpace(_toile.Document.NomAffichage)
                ? _toile.Document.NomAffichage
                : (_cheminFichier == null ? "sans titre" : System.IO.Path.GetFileName(_cheminFichier));
            Title = string.Format("Mental-o — {0}{1}", nom, _modifie ? " *" : "");
        }
    }
}
