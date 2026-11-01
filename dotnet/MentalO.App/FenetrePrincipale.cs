using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;

namespace Mentalo
{
    /// <summary>La fenêtre de Mental-O seul : menus, barre des modes, barre de
    /// personnalisation (Texte / Boîte / Groupe / Lien), toile, panneaux flottants,
    /// barre d'état. Portage Avalonia (26/10/2026) de la fenêtre WPF. Les barres
    /// et les panneaux sont ceux de la toile (BarresToile, PanneauxFlottants),
    /// partagés avec le DLC Marabook ; la fenêtre n'ajoute que les menus, le
    /// thème, le plein écran, le laser, les fichiers et les mises à jour.</summary>
    public partial class FenetrePrincipale : Window
    {
        private readonly ToileVue _toile;
        private readonly BarresToile _barres;
        private readonly PanneauxFlottants _panneaux;
        private readonly TextBlock _texteZoom;
        private string _cheminFichier;
        private bool _modifie;
        private bool _fermetureConfirmee;

        private MenuItem _menuFocus, _menuFiltre, _menuAnnuler, _menuRetablir;

        // Copies de secours (v0.16) : une clé par carte ouverte, une copie par minute si modifiée.
        private string _cleSecours = Secours.NouvelleCle();
        private bool _modifieDepuisSecours;

        // Présentation (v0.17) : pointeur laser ; modèles de cartes.
        private Grid _zoneCentrale;
        private Canvas _calqueLaser;
        private Border _laser;
        private bool _laserActif;
        private Point _dernierPointeur;
        private MenuItem _menuModeles;

        // Plein écran et mode présentation
        private bool _pleinEcran, _presentation;
        private WindowState _etatAvantPleinEcran = WindowState.Normal;
        private string _modeAvantPresentation;
        private Menu _menuPrincipal;
        private Border _barreModes, _barreFormat, _barreEtat;
        private MenuItem _menuPleinEcran, _menuPresentation;

        // Mises à jour (menu Aide) : la version trouvée au lancement, s'il y en a une plus récente
        private MenuItem _menuAide, _menuVerifierMaj;
        private MiseAJour.Info _maj;
        private readonly TextBlock _texteMaj = new TextBlock();

        // Barre de modes : le bouton de thème, à l'opposé des modes
        private Button _boutonTheme;

        // Panneaux flottants : les coches du menu Fenêtres
        private MenuItem _menuTust, _menuMiniCarte, _menuInspecteur, _menuHistorique;
        private MenuItem _menuRecents;

        private Dictionary<string, Action> _actions;

        public FenetrePrincipale()
        {
            Width = 1200;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Chrome.FondFenetre;
            Foreground = Chrome.Encre;
            try { Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://Mental-O/Assets/mentalo.ico"))); }
            catch { }

            _toile = new ToileVue();
            _toile.DocumentModifie += delegate
            {
                if (!_modifie) { _modifie = true; MettreAJourTitre(); }
                _modifieDepuisSecours = true;
            };
            _texteZoom = new TextBlock();
            _texteZoom.Margin = new Thickness(8, 3, 8, 3);
            _texteZoom.Foreground = Chrome.TexteDoux;
            _toile.ZoomChange += delegate(double zoom)
            {
                _texteZoom.Text = string.Format("Zoom : {0:0} %", zoom * 100);
            };
            IHabillage habillage = new HabillageStandard();
            _barres = new BarresToile(_toile, habillage);
            _panneaux = new PanneauxFlottants(_toile, delegate { return this; }, habillage, true);

            DockPanel racine = new DockPanel();
            _menuPrincipal = ConstruireMenu();
            DockPanel.SetDock(_menuPrincipal, Dock.Top);
            racine.Children.Add(_menuPrincipal);
            _barreModes = ConstruireBarreModes();
            DockPanel.SetDock(_barreModes, Dock.Top);
            racine.Children.Add(_barreModes);
            // Barre de personnalisation unique : son contenu suit les onglets de la barre des modes.
            _barreFormat = _barres.ConstruireBarreFormat();
            DockPanel.SetDock(_barreFormat, Dock.Top);
            racine.Children.Add(_barreFormat);
            _barreEtat = ConstruireBarreEtat();
            DockPanel.SetDock(_barreEtat, Dock.Bottom);
            racine.Children.Add(_barreEtat);
            Grid zoneCentrale = new Grid();
            _zoneCentrale = zoneCentrale;
            zoneCentrale.Children.Add(_toile);
            _panneaux.AjouterA(zoneCentrale); // la vignette du tableau et les panneaux flottants
            _calqueLaser = ConstruireLaser();
            zoneCentrale.Children.Add(_calqueLaser);
            racine.Children.Add(zoneCentrale);
            Content = racine;

            NoeudVisuel.ZonesFormatage.Add(_barreFormat);

            BrancherActions();
            AddHandler(KeyDownEvent, ToucheEnfoncee, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            AddHandler(KeyUpEvent, ToucheRelachee, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            _toile.Historique.Change += MettreAJourMenusEdition;
            _toile.EstompageChange += delegate
            {
                if (_menuFocus != null) Ui.Cocher(_menuFocus, _toile.FocusActif);
            };

            // Sauvegarde automatique (option du fichier) : toutes les 3 minutes.
            DispatcherTimer minuterie = new DispatcherTimer { Interval = TimeSpan.FromSeconds(180) };
            minuterie.Tick += delegate
            {
                if (_modifie && _cheminFichier != null && _toile.Document.SauvegardeAuto)
                    EcrireFichier(_cheminFichier);
            };
            minuterie.Start();

            // Copie de secours (v0.16) : chaque minute, si quelque chose a changé depuis la dernière.
            DispatcherTimer secours = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            secours.Tick += delegate
            {
                if (_modifie && _modifieDepuisSecours)
                {
                    Secours.Ecrire(_toile.Document, _cleSecours, _cheminFichier);
                    _modifieDepuisSecours = false;
                }
            };
            secours.Start();
            Opened += delegate
            {
                _toile.Focus();
                ProposerRecuperation();
                VerifierMisesAJourEnFond();
            };
            Closing += delegate(object expediteur, WindowClosingEventArgs e)
            {
                if (_fermetureConfirmee) { Secours.Effacer(_cleSecours); return; }
                e.Cancel = true;
                FermerApresConfirmation();
            };

            _texteZoom.Text = "Zoom : 100 %";
            _toile.AppliquerFondDocument(); // le fond suit le thème dès la carte vierge
            _barres.MettreAJourFormat();
            MettreAJourPanneaux();
            MettreAJourTitre();
            MettreAJourMenusEdition();
        }

        /// <summary>« --capture &lt;png&gt; » : la fenêtre rendue en PNG une fois posée,
        /// puis l'application quitte (sondes et vérifications visuelles).</summary>
        public void CapturerPuisQuitter(string chemin)
        {
            Opened += async delegate
            {
                await System.Threading.Tasks.Task.Delay(900);
                await Dispatcher.UIThread.InvokeAsync(delegate { }, DispatcherPriority.Render);
                try
                {
                    Control contenu = (Control)Content;
                    var taille = new PixelSize(Math.Max(1, (int)contenu.Bounds.Width), Math.Max(1, (int)contenu.Bounds.Height));
                    using (var image = new Avalonia.Media.Imaging.RenderTargetBitmap(taille, new Vector(96, 96)))
                    {
                        image.Render(contenu);
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(chemin)));
                        image.Save(chemin);
                    }
                    Console.WriteLine("capture : " + chemin + " (" + taille.Width + "×" + taille.Height + ")");
                }
                catch (Exception erreur)
                {
                    Console.Error.WriteLine("capture impossible : " + erreur);
                }
                _fermetureConfirmee = true;
                Close();
            };
        }

        /// <summary>Le fichier passé en argument : ouvert une fois la fenêtre prête.</summary>
        public void OuvrirFichierAuDemarrage(string chemin)
        {
            Opened += delegate { OuvrirFichier(chemin); };
        }

        private async void FermerApresConfirmation()
        {
            if (!await ConfirmerAbandon()) return;
            _fermetureConfirmee = true;
            Close();
        }

        // ------------------------------------------------------------------ construction du menu

        private Menu ConstruireMenu()
        {
            Menu menu = new Menu();

            MenuItem fichier = new MenuItem { Header = "_Fichier" };
            fichier.Items.Add(Element("_Nouveau", "nouveau"));
            _menuModeles = new MenuItem { Header = "Nouveau depuis un _modèle" };
            RemplirMenuModeles();
            fichier.Items.Add(_menuModeles);
            fichier.Items.Add(Element("_Ouvrir…", "ouvrir"));
            _menuRecents = new MenuItem { Header = "Ouvrir les fichiers _récents" };
            RemplirMenuRecents();
            fichier.Items.Add(_menuRecents);
            fichier.Items.Add(Ui.Separateur());
            fichier.Items.Add(Element("_Enregistrer", "enregistrer"));
            fichier.Items.Add(Element("Enregistrer _sous…", "enregistrer-sous"));
            MenuItem versions = Ui.Element("_Versions antérieures…", delegate { OuvrirVersions(); });
            Ui.Infobulle(versions, "Les états précédents conservés dans le fichier à chaque enregistrement (au plus " + Fichier.VersionsMax + ")");
            fichier.Items.Add(versions);
            fichier.Items.Add(Ui.Separateur());
            MenuItem exporter = Ui.SousMenu("E_xporter");
            exporter.Items.Add(Ui.Element("Image PNG…", delegate { ExporterImage(); }));
            exporter.Items.Add(Ui.Element("Document PDF…", delegate { ExporterPdf(); }));
            exporter.Items.Add(Ui.Element("Dessin SVG…", delegate { ExporterSvg(); }));
            exporter.Items.Add(Ui.Element("Texte Markdown…", delegate { ExporterMarkdown(); }));
            exporter.Items.Add(Ui.Element("Plan OPML…", delegate { ExporterOpml(); }));
            exporter.Items.Add(Ui.Element("Canevas JSON Canvas (Obsidian…)…", delegate { ExporterCanvas(); }));
            fichier.Items.Add(exporter);
            MenuItem importerPuces = Ui.Element("I_mporter un texte à puces…", delegate { ImporterTexteAPuces(); });
            Ui.Infobulle(importerPuces, "Colle une liste à puces indentée : chaque ligne devient une boîte, l'indentation crée les liens");
            fichier.Items.Add(importerPuces);
            MenuItem importerOpml = Ui.Element("Importer un plan OPM_L…", delegate { ImporterOpml(); });
            Ui.Infobulle(importerOpml, "Chaque élément du plan devient une boîte, la hiérarchie devient des liens fléchés");
            fichier.Items.Add(importerOpml);
            MenuItem importerCanvas = Ui.Element("Importer un canevas _JSON Canvas…", delegate { ImporterCanvas(); });
            Ui.Infobulle(importerCanvas, "Le format ouvert d'Obsidian Canvas (.canvas) : cartes, fichiers, liens web, groupes et flèches");
            fichier.Items.Add(importerCanvas);
            fichier.Items.Add(Element("Im_primer…", "imprimer"));
            fichier.Items.Add(Ui.Separateur());
            fichier.Items.Add(Ui.Element("Préférences de _fichier…", delegate { OuvrirPreferencesFichier(); }));
            fichier.Items.Add(Ui.Element("Paramè_tres…", delegate { OuvrirParametres(); }));
            fichier.Items.Add(Ui.Separateur());
            MenuItem themes = Ui.SousMenu("_Thème de la carte");
            Ui.Infobulle(themes, "Fond, texte et styles par défaut assortis ; au choix, appliqués aussi aux éléments existants");
            foreach (ThemeCarte theme in ThemeCarte.Liste())
            {
                ThemeCarte choix = theme;
                MenuItem element = Ui.Element(theme.Nom, delegate { AppliquerTheme(choix); });
                Ui.Infobulle(element, theme.Description);
                element.Icon = new Border
                {
                    Width = 14, Height = 14, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Palette.CouleurDepuis(theme.FondCouleur, Colors.White)),
                    BorderBrush = new SolidColorBrush(Palette.CouleurDepuis(theme.Boite.BordureCouleur, Colors.Gray)),
                    BorderThickness = new Thickness(1.5)
                };
                themes.Items.Add(element);
            }
            fichier.Items.Add(themes);
            if (OperatingSystem.IsWindows())
            {
                MenuItem associer = Ui.Element("_Associer les fichiers .tea à Mental-O", delegate { AssocierExtensions(); });
                Ui.Infobulle(associer, "Le double-clic sur un fichier .tea (ou .zeubi) dans l'Explorateur ouvrira Mental-O");
                fichier.Items.Add(associer);
            }
            fichier.Items.Add(Ui.Separateur());
            fichier.Items.Add(Ui.Element("_Quitter", delegate { Close(); }, false, "Alt+F4"));
            menu.Items.Add(fichier);

            MenuItem edition = new MenuItem { Header = "É_dition" };
            _menuAnnuler = Element("_Annuler", "annuler");
            edition.Items.Add(_menuAnnuler);
            _menuRetablir = Element("_Rétablir", "retablir");
            edition.Items.Add(_menuRetablir);
            edition.Items.Add(Ui.Separateur());
            edition.Items.Add(Element("_Copier les boîtes", "copier"));
            edition.Items.Add(Element("C_oller", "coller"));
            edition.Items.Add(Element("_Dupliquer", "dupliquer"));
            edition.Items.Add(Ui.Separateur());
            edition.Items.Add(Element("Rec_hercher…", "rechercher"));
            edition.Items.Add(Element("_Tout sélectionner", "tout-selectionner"));
            edition.Items.Add(Ui.Separateur());
            MenuItem noteBoite = Element("_Note de la boîte…", "note");
            Ui.Infobulle(noteBoite, "Un détail hors de la carte : icône dans le coin de la boîte, texte en infobulle");
            edition.Items.Add(noteBoite);
            MenuItem etiquettes = Ui.Element("Éti_quettes du fichier…", delegate { DialogueEtiquettes.Ouvrir(this, _toile); });
            Ui.Infobulle(etiquettes, "Créer, renommer, colorer, supprimer les étiquettes ; poser : clic droit > Étiquettes");
            edition.Items.Add(etiquettes);
            edition.Items.Add(Ui.Separateur());
            edition.Items.Add(Element("_Supprimer la sélection", "supprimer"));
            edition.Items.Add(Ui.Separateur());
            edition.Items.Add(Element("_Grouper", "grouper"));
            edition.Items.Add(Element("Dégrou_per", "degrouper"));
            MenuItem plier = Element("P_lier / déplier le groupe", "plier");
            Ui.Infobulle(plier, "Réduit le groupe sélectionné (ou celui de la boîte sélectionnée) à une pastille-titre ; double-clic sur la pastille pour déplier");
            edition.Items.Add(plier);
            edition.Items.Add(Ui.Separateur());
            MenuItem aligner = Ui.SousMenu("_Aligner et distribuer");
            aligner.Items.Add(Ui.Element("Aligner à gauche", delegate { _toile.AlignerSelection("gauche"); }));
            aligner.Items.Add(Ui.Element("Centrer horizontalement", delegate { _toile.AlignerSelection("centre-h"); }));
            aligner.Items.Add(Ui.Element("Aligner à droite", delegate { _toile.AlignerSelection("droite"); }));
            aligner.Items.Add(Ui.Separateur());
            aligner.Items.Add(Ui.Element("Aligner en haut", delegate { _toile.AlignerSelection("haut"); }));
            aligner.Items.Add(Ui.Element("Centrer verticalement", delegate { _toile.AlignerSelection("centre-v"); }));
            aligner.Items.Add(Ui.Element("Aligner en bas", delegate { _toile.AlignerSelection("bas"); }));
            aligner.Items.Add(Ui.Separateur());
            aligner.Items.Add(Ui.Element("Distribuer horizontalement", delegate { _toile.DistribuerSelection(true); }));
            aligner.Items.Add(Ui.Element("Distribuer verticalement", delegate { _toile.DistribuerSelection(false); }));
            edition.Items.Add(aligner);
            MenuItem empiler = Element("E_mpiler la sélection", "empiler");
            Ui.Infobulle(empiler, "Range les boîtes sélectionnées en colonne, centres alignés");
            edition.Items.Add(empiler);
            MenuItem ranger = Ui.SousMenu("_Ranger en arbre");
            Ui.Infobulle(ranger, "Dispose la sélection (ou toute la carte) en arbre suivant les liens ; la racine garde sa place");
            ranger.Items.Add(Ui.Element("Horizontal (racine à gauche)", delegate { _toile.RangerEnArbre("horizontal"); }));
            ranger.Items.Add(Ui.Element("Vertical (racine en haut)", delegate { _toile.RangerEnArbre("vertical"); }));
            ranger.Items.Add(Ui.Element("Radial (racine au centre)", delegate { _toile.RangerEnArbre("radial"); }));
            edition.Items.Add(ranger);
            menu.Items.Add(edition);

            MenuItem insertion = new MenuItem { Header = "_Insertion" };
            insertion.Items.Add(Element("_Image…", "inserer-image"));
            MenuItem insererFichier = Element("_Fichier…", "inserer-fichier");
            Ui.Infobulle(insererFichier, "Lie un fichier (tout sauf une image) à la boîte sélectionnée, ou dans une boîte neuve");
            insertion.Items.Add(insererFichier);
            MenuItem insererLienWeb = Element("Lien _web…", "inserer-lien-web");
            Ui.Infobulle(insererLienWeb, "Une adresse web cliquable dans la boîte sélectionnée, ou dans une boîte neuve (on peut aussi glisser une adresse depuis le navigateur)");
            insertion.Items.Add(insererLienWeb);
            menu.Items.Add(insertion);

            MenuItem affichage = new MenuItem { Header = "_Affichage" };
            affichage.Items.Add(Element("Zoom a_vant", "zoom-avant"));
            affichage.Items.Add(Element("Zoom arri_ère", "zoom-arriere"));
            affichage.Items.Add(Element("Zoom _100 %", "zoom-100"));
            affichage.Items.Add(Ui.Separateur());
            affichage.Items.Add(Element("_Recentrer la carte", "recentrer"));
            affichage.Items.Add(Element("_Tout voir", "zoom-tout"));
            affichage.Items.Add(Element("Zoom sur la _sélection", "zoom-selection"));
            affichage.Items.Add(Ui.Separateur());
            _menuFocus = Element("_Mettre en avant la sélection", "focus");
            Ui.Infobulle(_menuFocus, "Estompe tout sauf la sélection ; Échap ou un second appel rétablit");
            affichage.Items.Add(_menuFocus);
            affichage.Items.Add(Ui.Separateur());
            _menuPleinEcran = Element("_Plein écran", "plein-ecran");
            affichage.Items.Add(_menuPleinEcran);
            _menuPresentation = Element("Mode pré_sentation", "presentation");
            Ui.Infobulle(_menuPresentation, "Plein écran, barres masquées, lecture seule — Échap pour sortir");
            affichage.Items.Add(_menuPresentation);
            menu.Items.Add(affichage);

            MenuItem fenetres = new MenuItem { Header = "Fenê_tres" };
            _menuTust = Ui.Element("_Tust des raccourcis", null, ParametresGlobaux.TustVisible);
            _menuTust.Click += delegate
            {
                ParametresGlobaux.TustVisible = !ParametresGlobaux.TustVisible;
                ParametresGlobaux.Enregistrer();
                MettreAJourTust();
            };
            fenetres.Items.Add(_menuTust);
            _menuMiniCarte = ElementPanneau("_Mini-carte", delegate { ParametresGlobaux.MiniCarteVisible = !ParametresGlobaux.MiniCarteVisible; }, ParametresGlobaux.MiniCarteVisible);
            fenetres.Items.Add(_menuMiniCarte);
            _menuInspecteur = ElementPanneau("_Inspecteur de sélection", delegate { ParametresGlobaux.InspecteurVisible = !ParametresGlobaux.InspecteurVisible; }, ParametresGlobaux.InspecteurVisible);
            fenetres.Items.Add(_menuInspecteur);
            _menuHistorique = ElementPanneau("_Historique", delegate { ParametresGlobaux.HistoriqueVisible = !ParametresGlobaux.HistoriqueVisible; }, ParametresGlobaux.HistoriqueVisible);
            fenetres.Items.Add(_menuHistorique);
            _menuFiltre = ElementPanneau("_Filtre par étiquettes", delegate { ParametresGlobaux.FiltreVisible = !ParametresGlobaux.FiltreVisible; }, ParametresGlobaux.FiltreVisible);
            Ui.Infobulle(_menuFiltre, "Coche des étiquettes : les boîtes qui n'en portent aucune s'estompent");
            fenetres.Items.Add(_menuFiltre);
            menu.Items.Add(fenetres);

            _menuAide = new MenuItem { Header = "_Aide" };
            _menuVerifierMaj = Ui.Element("_Vérifier les mises à jour…", delegate { VerifierMisesAJour(); });
            Ui.Infobulle(_menuVerifierMaj, "Va chercher la dernière version publiée sur GitHub et l'installe sur place");
            _menuAide.Items.Add(_menuVerifierMaj);
            _menuAide.Items.Add(Ui.Separateur());
            _menuAide.Items.Add(Ui.Element("À _propos de Mental-O…", delegate
            {
                DialogueMessage.Afficher(this,
                    "Mental-O " + MiseAJour.VersionLocale() + "\n\nLogiciel de carte mentale, simple et léger.\n" +
                    "Une application de la famille Stargazer, par Rémi Escamilla — github.com/" + MiseAJour.Depot + "\n\n" +
                    "Clavier : Entrée = boîte sœur, Tab = boîte enfant reliée · Édition > Ranger en arbre (horizontal, vertical, radial)\n" +
                    "Présentation (F5) : chaque groupe est une diapositive (→ / ← / Début, L = pointeur laser, ordre au clic droit sur le cadre)\n" +
                    "Fichier > Nouveau depuis un modèle, Thème de la carte (6 thèmes assortis), Exporter/Importer JSON Canvas (Obsidian) · dépôt de .txt/.md\n" +
                    "Confiance : copie de secours chaque minute (récupération proposée au démarrage suivant), Fichier > Versions antérieures\n" +
                    "Grandes cartes : Tout voir (Ctrl+Maj+0), zoom sur la sélection (Ctrl+Maj+1), Ctrl+A, Alt+rectangle = désélectionner\n" +
                    "Plier / déplier un groupe (Ctrl+Maj+P, clic droit sur le cadre, double-clic sur la pastille)\n" +
                    "Notes cachées (Ctrl+Maj+N, icône dans le coin) · Étiquettes (clic droit > Étiquettes, Fenêtres > Filtre) · Mettre en avant (Ctrl+Maj+M)\n" +
                    "Cases à cocher (bouton ☐ de l'onglet Texte, clic pour cocher, avancement dans le titre du groupe) · Liens web (Ctrl+Maj+L, glisser une adresse)\n" +
                    "Aussi disponible en DLC de Marabook.\n" +
                    "Crédits à Gruffystudio de FlatIcon.com pour l'icône de l'app.",
                    "À propos de Mental-O", BoutonsMessage.OK, IconeMessage.Information);
            }));
            menu.Items.Add(_menuAide);

            return menu;
        }

        /// <summary>Un élément de menu relié à une action re-liable (le raccourci affiché suit les Paramètres).</summary>
        private MenuItem Element(string entete, string idAction)
        {
            MenuItem element = new MenuItem { Header = entete };
            element.Click += delegate { Executer(idAction); };
            KeyGesture geste = ParametresGlobaux.GestureDe(idAction);
            if (geste != null) element.InputGesture = geste;
            element.Tag = idAction;
            return element;
        }

        private void MettreAJourMenusEdition()
        {
            if (_menuAnnuler != null) _menuAnnuler.IsEnabled = _toile.Historique.PeutAnnuler;
            if (_menuRetablir != null) _menuRetablir.IsEnabled = _toile.Historique.PeutRefaire;
        }

        /// <summary>Après un changement de raccourcis : les menus affichent les nouveaux gestes.</summary>
        private void RafraichirGestesMenus(ItemsControl conteneur)
        {
            foreach (object objet in conteneur.Items)
            {
                MenuItem element = objet as MenuItem;
                if (element == null) continue;
                string id = element.Tag as string;
                if (id != null) element.InputGesture = ParametresGlobaux.GestureDe(id);
                if (element.Items.Count > 0) RafraichirGestesMenus(element);
            }
        }

        // ------------------------------------------------------------------ mises à jour

        /// <summary>Au lancement : vérification silencieuse en arrière-plan ; s'il existe plus
        /// récent, le menu Aide porte un point et la barre d'état le dit. Toute erreur (hors
        /// ligne, dépôt sans release) est tue.</summary>
        private void VerifierMisesAJourEnFond()
        {
            System.Threading.Thread fil = new System.Threading.Thread(delegate()
            {
                MiseAJour.Info info;
                try { info = MiseAJour.Verifier(); }
                catch { return; }
                if (!MiseAJour.PlusRecente(info.Version, MiseAJour.VersionLocale())) return;
                try
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        _maj = info;
                        _menuAide.Header = "_Aide  ●";
                        _menuAide.Foreground = Chrome.Or;
                        _menuVerifierMaj.Header = "_Installer la version " + info.Version + "…";
                        _texteMaj.Text = "Mental-O " + info.Version + " est disponible — menu Aide";
                        _texteMaj.IsVisible = true;
                    });
                }
                catch { }
            });
            fil.IsBackground = true;
            fil.Start();
        }

        /// <summary>Aide > Vérifier les mises à jour : interroge GitHub (sauf si le lancement a
        /// déjà trouvé une version), puis propose de l'installer. Si l'archive est prête,
        /// l'application se ferme (en proposant d'enregistrer) et le script la relance à neuf.</summary>
        private async void VerifierMisesAJour()
        {
            MiseAJour.Info info = _maj;
            if (info == null)
            {
                Cursor = new Cursor(StandardCursorType.Wait);
                try { info = await System.Threading.Tasks.Task.Run(delegate { return MiseAJour.Verifier(); }); }
                catch (Exception ex)
                {
                    Cursor = Ui.CurseurFleche;
                    await DialogueMessage.Afficher(this, "Impossible de vérifier les mises à jour : " + ex.Message,
                        "Mises à jour", BoutonsMessage.OK, IconeMessage.Avertissement);
                    return;
                }
                Cursor = Ui.CurseurFleche;
                if (!MiseAJour.PlusRecente(info.Version, MiseAJour.VersionLocale()))
                {
                    await DialogueMessage.Afficher(this, "Vous avez la dernière version de Mental-O (" +
                        MiseAJour.VersionLocale() + ").", "Mises à jour",
                        BoutonsMessage.OK, IconeMessage.Information);
                    return;
                }
            }
            if (await DialogueMiseAJour.Proposer(this, info))
                Close();
        }

        // ------------------------------------------------------------------ actions et clavier

        private void BrancherActions()
        {
            _actions = new Dictionary<string, Action>
            {
                { "nouveau", delegate { Nouveau(); } },
                { "ouvrir", delegate { Ouvrir(); } },
                { "enregistrer", delegate { Enregistrer(); } },
                { "enregistrer-sous", delegate { EnregistrerSous(); } },
                { "annuler", delegate { _toile.Historique.Annuler(); } },
                { "retablir", delegate { _toile.Historique.Refaire(); } },
                { "copier", delegate { _toile.CopierSelection(); } },
                { "coller", delegate { Coller(); } },
                { "dupliquer", delegate { _toile.DupliquerSelection(); } },
                { "rechercher", delegate { OuvrirRecherche(); } },
                { "editer", delegate { _toile.EditerSelection(); } },
                { "imprimer", delegate { Imprimer(); } },
                { "empiler", delegate { _toile.EmpilerSelection(); } },
                { "plein-ecran", delegate { BasculerPleinEcran(); } },
                { "presentation", delegate { BasculerPresentation(); } },
                { "supprimer", delegate { _toile.SupprimerSelection(); } },
                { "grouper", delegate { _toile.GrouperSelection(); } },
                { "degrouper", delegate { _toile.DegrouperSelection(); } },
                { "inserer-image", delegate { ChoisirImage(); } },
                { "inserer-fichier", delegate { ChoisirFichier(); } },
                { "inserer-lien-web", delegate { _toile.InsererLienWeb(); } },
                { "tout-selectionner", delegate { _toile.SelectionnerTout(); } },
                { "note", delegate { _toile.EditerNoteSelection(); } },
                { "plier", delegate { _toile.BasculerPliageCible(); } },
                { "zoom-tout", delegate { _toile.ZoomToutVoir(); } },
                { "zoom-selection", delegate { _toile.ZoomSurSelection(); } },
                { "focus", delegate { _toile.BasculerFocusSelection(); } },
                { "zoom-avant", delegate { _toile.ZoomAvant(); } },
                { "zoom-arriere", delegate { _toile.ZoomArriere(); } },
                { "zoom-100", delegate { _toile.ZoomReinitialiser(); } },
                { "recentrer", delegate { _toile.Recentrer(); } },
                { "mode-automatique", delegate { _toile.DefinirMode("automatique"); MettreAJourBarreModes(); } },
                { "mode-deplacement", delegate { _toile.DefinirMode("deplacement"); MettreAJourBarreModes(); } },
                { "mode-liens", delegate { _toile.DefinirMode("liens"); MettreAJourBarreModes(); } },
                { "mode-lecture", delegate { _toile.DefinirMode("lecture"); MettreAJourBarreModes(); } },
                { "gras", delegate { _toile.AppliquerFormatTexte("gras", null); } },
                { "italique", delegate { _toile.AppliquerFormatTexte("italique", null); } },
                { "souligne", delegate { _toile.AppliquerFormatTexte("souligne", null); } },
            };
        }

        private void Executer(string idAction)
        {
            Action action;
            if (_actions != null && _actions.TryGetValue(idAction, out action)) action();
        }

        private async void Coller()
        {
            if (await _toile.PeutCollerBoites()) _toile.CollerBoites();
            else _toile.CollerImage();
        }

        /// <summary>Les actions que l'éditeur de texte garde pour lui quand il a le focus.</summary>
        private static readonly HashSet<string> ActionsDeLEditeur = new HashSet<string>
        {
            "annuler", "retablir", "copier", "coller", "tout-selectionner", "supprimer", "editer", "dupliquer"
        };

        private void ToucheEnfoncee(object expediteur, KeyEventArgs e)
        {
            bool dansEditeur = Ui.Ancetre<TextBox>(e.Source, this) != null;
            // Création au clavier (v0.17) : Entrée = boîte sœur, Tab = boîte enfant — avant que
            // Tab ne serve à la navigation entre contrôles.
            if (!dansEditeur && !_presentation && e.KeyModifiers == KeyModifiers.None && _toile.IsKeyboardFocusWithin)
            {
                if (e.Key == Key.Tab) { if (_toile.CreerBoiteEnfant()) { e.Handled = true; return; } }
                else if (e.Key == Key.Enter) { if (_toile.CreerBoiteSoeur()) { e.Handled = true; return; } }
            }

            // Les raccourcis re-liables (Fichier > Paramètres).
            string idAction = ParametresGlobaux.ActionPour(e.Key, e.KeyModifiers);
            if (idAction == null && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                // Variantes de confort, hors personnalisation.
                if (e.Key == Key.OemPlus || e.Key == Key.Add) idAction = "zoom-avant";
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) idAction = "zoom-arriere";
                else if (e.Key == Key.NumPad0) idAction = "zoom-100";
            }
            if (idAction != null && !(dansEditeur && ActionsDeLEditeur.Contains(idAction)))
            {
                if (dansEditeur && (idAction == "gras" || idAction == "italique" || idAction == "souligne")
                    && !_toile.EditionEnCours)
                {
                    // Un champ de la barre a le focus : on le laisse tranquille.
                }
                else
                {
                    Executer(idAction);
                    e.Handled = true;
                    return;
                }
            }

            if (dansEditeur) return; // édition en cours : le clavier appartient à l'éditeur
            if (_presentation)
            {
                // Diapositives (v0.17) : avancer, reculer, vue d'ensemble, pointeur laser.
                if (e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Space || e.Key == Key.Enter)
                { _toile.PresentationSuivante(); e.Handled = true; return; }
                if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Back)
                { _toile.PresentationPrecedente(); e.Handled = true; return; }
                if (e.Key == Key.Home) { _toile.PresentationDebut(); e.Handled = true; return; }
                if (e.Key == Key.L) { BasculerLaser(); e.Handled = true; return; }
            }
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
                else if (_panneaux.RechercheVisible) { FermerRecherche(); e.Handled = true; }
                else { _toile.Deselectionner(); e.Handled = true; }
            }
            else if (e.KeyModifiers == KeyModifiers.None)
            {
                if (e.Key == Key.Left) { _toile.NaviguerSelection(-1, 0); e.Handled = true; }
                else if (e.Key == Key.Right) { _toile.NaviguerSelection(1, 0); e.Handled = true; }
                else if (e.Key == Key.Up) { _toile.NaviguerSelection(0, -1); e.Handled = true; }
                else if (e.Key == Key.Down) { _toile.NaviguerSelection(0, 1); e.Handled = true; }
            }
        }

        private void ToucheRelachee(object expediteur, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                _toile.ModePanClavier(false);
        }

        private MenuItem ElementPanneau(string entete, Action basculer, bool coche)
        {
            MenuItem element = Ui.Element(entete, null, coche);
            element.Click += delegate
            {
                basculer();
                ParametresGlobaux.Enregistrer();
                MettreAJourPanneaux();
            };
            return element;
        }

        // ------------------------------------------------------------------ fichier

        private async void Nouveau()
        {
            if (!await ConfirmerAbandon()) return;
            NouvelleSessionSecours();
            _toile.ChargerDocument(new DocumentCarte());
            _cheminFichier = null;
            _modifie = false;
            MettreAJourTitre();
        }

        /// <summary>La carte précédente quitte l'écran : sa copie de secours n'a plus d'objet.</summary>
        private void NouvelleSessionSecours()
        {
            Secours.Effacer(_cleSecours);
            _cleSecours = Secours.NouvelleCle();
            _modifieDepuisSecours = false;
        }

        /// <summary>Au démarrage : des copies de secours laissées par une session interrompue ?
        /// La plus récente est rouverte, les autres sont rangées dans Documents.</summary>
        private async void ProposerRecuperation()
        {
            List<Secours.Entree> entrees = Secours.Lister();
            if (entrees.Count == 0) return;
            System.Text.StringBuilder texte = new System.Text.StringBuilder();
            texte.Append(entrees.Count == 1
                ? "Mental-O s'est arrêté sans enregistrer une carte :\n"
                : "Mental-O s'est arrêté sans enregistrer " + entrees.Count + " cartes :\n");
            foreach (Secours.Entree entree in entrees)
                texte.Append("  • ").Append(entree.NomAffiche).Append(" (").Append(entree.Date).Append(")\n");
            texte.Append(entrees.Count == 1
                ? "\nLa restaurer ? (Non = abandonner la copie de secours)"
                : "\nLes restaurer ? La plus récente s'ouvre ici, les autres sont copiées dans Documents. (Non = abandonner)");
            ResultatMessage reponse = await DialogueMessage.Afficher(this, texte.ToString(), "Récupération",
                BoutonsMessage.OuiNon, IconeMessage.Question);
            if (reponse != ResultatMessage.Oui)
            {
                foreach (Secours.Entree entree in entrees) Secours.Effacer(entree.Cle);
                return;
            }
            Secours.Entree principale = entrees[0];
            List<string> ranges = new List<string>();
            for (int i = 1; i < entrees.Count; i++)
            {
                string chemin = Secours.Exfiltrer(entrees[i]);
                if (chemin != null) ranges.Add(chemin);
            }
            try
            {
                DocumentCarte document = Fichier.Charger(principale.Fichier);
                NouvelleSessionSecours();
                _toile.ChargerDocument(document);
                _cheminFichier = !string.IsNullOrEmpty(principale.Chemin) ? principale.Chemin : null;
                _modifie = true; // récupéré, mais pas encore réécrit sur le disque
                _modifieDepuisSecours = true;
                MettreAJourTitre();
                Secours.Effacer(principale.Cle);
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this, "Impossible de relire la copie de secours :\n" + ex.Message
                    + "\n\nElle reste dans " + Path.GetDirectoryName(principale.Fichier),
                    "Récupération", BoutonsMessage.OK, IconeMessage.Erreur);
            }
            if (ranges.Count > 0)
                await DialogueMessage.Afficher(this, "Autres cartes récupérées, rangées dans Documents :\n  • "
                    + string.Join("\n  • ", ranges.ToArray()), "Récupération", BoutonsMessage.OK, IconeMessage.Information);
        }

        private async void OuvrirVersions()
        {
            if (_cheminFichier == null)
            {
                await DialogueMessage.Afficher(this, "Cette carte n'a jamais été enregistrée : aucune version antérieure.",
                    "Versions antérieures", BoutonsMessage.OK, IconeMessage.Information);
                return;
            }
            string chemin = _cheminFichier;
            DocumentCarte version = await DialogueVersions.Ouvrir(this,
                delegate { return Fichier.ListerVersions(chemin); },
                delegate(string nom) { return Fichier.ChargerVersion(chemin, nom); });
            if (version == null) return;
            _toile.ChargerDocument(version);
            _modifie = true; // la version restaurée attend un enregistrement pour devenir le fichier
            _modifieDepuisSecours = true;
            MettreAJourTitre();
        }

        private async void Ouvrir()
        {
            if (!await ConfirmerAbandon()) return;
            string chemin = await Ui.ChoisirFichier(this, "Ouvrir une carte", Ui.TypeCartes, Ui.TypeTous);
            if (chemin != null) OuvrirFichier(chemin);
        }

        public async void OuvrirFichier(string chemin)
        {
            try
            {
                DocumentCarte document = Fichier.Charger(chemin);
                NouvelleSessionSecours();
                _toile.ChargerDocument(document);
                _cheminFichier = chemin;
                _modifie = false;
                MettreAJourTitre();
                RetenirFichierRecent(chemin);
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this,
                    string.Format("Impossible d'ouvrir le fichier :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
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
                MenuItem vide = Ui.Element("(aucun fichier récent)", null);
                vide.IsEnabled = false;
                _menuRecents.Items.Add(vide);
                return;
            }
            int numero = 1;
            foreach (string chemin in recents)
            {
                string capture = chemin;
                MenuItem element = Ui.Element(string.Format("_{0}  {1}", numero++, Path.GetFileName(chemin)),
                    delegate { OuvrirFichierRecent(capture); });
                Ui.Infobulle(element, chemin);
                _menuRecents.Items.Add(element);
            }
            _menuRecents.Items.Add(Ui.Separateur());
            _menuRecents.Items.Add(Ui.Element("_Vider la liste", delegate
            {
                ParametresGlobaux.FichiersRecents.Clear();
                ParametresGlobaux.Enregistrer();
                RemplirMenuRecents();
            }));
        }

        private async void OuvrirFichierRecent(string chemin)
        {
            if (!File.Exists(chemin))
            {
                await DialogueMessage.Afficher(this,
                    string.Format("« {0} » n'existe plus : il est retiré de la liste.", chemin),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
                ParametresGlobaux.FichiersRecents.RemoveAll(delegate(string existant)
                {
                    return string.Equals(existant, chemin, StringComparison.OrdinalIgnoreCase);
                });
                ParametresGlobaux.Enregistrer();
                RemplirMenuRecents();
                return;
            }
            if (!await ConfirmerAbandon()) return;
            OuvrirFichier(chemin);
        }

        /// <summary>Enregistre l'association .tea/.zeubi → Mental-O pour l'utilisateur courant
        /// (HKCU, aucun droit administrateur) — Windows seulement.</summary>
        private async void AssocierExtensions()
        {
            try
            {
                string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Mental-O.exe");
                string icone = Path.Combine(AppContext.BaseDirectory, "ressources", "fichier-tea.ico");
                using (Microsoft.Win32.RegistryKey classes = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                {
                    using (Microsoft.Win32.RegistryKey cle = classes.CreateSubKey(".tea"))
                        cle.SetValue(null, "Mentalo.Carte");
                    using (Microsoft.Win32.RegistryKey cle = classes.CreateSubKey(".zeubi"))
                        cle.SetValue(null, "Mentalo.Carte");
                    using (Microsoft.Win32.RegistryKey cle = classes.CreateSubKey("Mentalo.Carte"))
                        cle.SetValue(null, "Carte Mental-O");
                    if (File.Exists(icone))
                        using (Microsoft.Win32.RegistryKey cle = classes.CreateSubKey(@"Mentalo.Carte\DefaultIcon"))
                            cle.SetValue(null, icone);
                    using (Microsoft.Win32.RegistryKey cle = classes.CreateSubKey(@"Mentalo.Carte\shell\open\command"))
                        cle.SetValue(null, "\"" + exe + "\" \"%1\"");
                }
                await DialogueMessage.Afficher(this,
                    "C'est fait : le double-clic sur un fichier .tea (ou .zeubi) ouvrira Mental-O.\n" +
                    "Si l'Explorateur affiche encore l'ancienne icône, elle se rafraîchira à la prochaine session.",
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Information);
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this,
                    string.Format("Impossible d'enregistrer l'association :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
        }

        private async System.Threading.Tasks.Task<bool> Enregistrer()
        {
            if (_cheminFichier == null) return await EnregistrerSous();
            return await EcrireFichier(_cheminFichier);
        }

        private async System.Threading.Tasks.Task<bool> EnregistrerSous()
        {
            string chemin = await Ui.EnregistrerSous(this, "Enregistrer la carte",
                _cheminFichier == null ? "ma carte" : Path.GetFileNameWithoutExtension(_cheminFichier),
                "tea", Ui.TypeCarte, Ui.TypeTous);
            if (chemin == null) return false;
            return await EcrireFichier(chemin);
        }

        private async System.Threading.Tasks.Task<bool> EcrireFichier(string chemin)
        {
            try
            {
                Fichier.Enregistrer(_toile.Document, chemin);
                _cheminFichier = chemin;
                _modifie = false;
                // Le fichier est à jour : la copie de secours n'a plus d'objet.
                Secours.Effacer(_cleSecours);
                _modifieDepuisSecours = false;
                MettreAJourTitre();
                RetenirFichierRecent(chemin);
                return true;
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this,
                    string.Format("Impossible d'enregistrer le fichier :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
                return false;
            }
        }

        private async System.Threading.Tasks.Task<bool> ConfirmerAbandon()
        {
            if (!_modifie) return true;
            string nom = _cheminFichier == null ? "sans titre" : Path.GetFileName(_cheminFichier);
            ResultatMessage reponse = await DialogueMessage.Afficher(this,
                string.Format("Voulez-vous enregistrer les modifications de « {0} » ?", nom),
                "Mental-O", BoutonsMessage.OuiNonAnnuler, IconeMessage.Avertissement);
            if (reponse == ResultatMessage.Annuler) return false;
            if (reponse == ResultatMessage.Oui) return await Enregistrer();
            return true;
        }

        private void MettreAJourTitre()
        {
            string nom = !string.IsNullOrWhiteSpace(_toile.Document.NomAffichage)
                ? _toile.Document.NomAffichage
                : (_cheminFichier == null ? "sans titre" : Path.GetFileName(_cheminFichier));
            Title = string.Format("Mental-O — {0}{1}", nom, _modifie ? " *" : "");
        }
    }
}
