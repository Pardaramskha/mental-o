using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
    /// <summary>Les panneaux flottants (mini-carte, inspecteur, historique, filtre, Tust),
    /// le plein écran et la présentation, les modèles, les thèmes et les exports.</summary>
    public partial class FenetrePrincipale
    {
        // ------------------------------------------------------------------ panneaux flottants

        private Border PanneauFlottant(HorizontalAlignment horizontal, VerticalAlignment vertical, Control contenu)
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
                IsVisible = false,
                Child = contenu
            };
        }

        private Border ConstruireMiniCarte()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(new TextBlock { Text = "Mini-carte", FontWeight = FontWeight.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            _miniCanvas = new Canvas
            {
                Width = 166,
                Height = 106,
                Background = Chrome.FondMiniCarte,
                ClipToBounds = true
            };
            _miniCanvas.PointerPressed += delegate(object expediteur, PointerPressedEventArgs e)
            {
                Point p = e.GetPosition(_miniCanvas);
                _toile.CentrerSur(new Point(_miniOrigine.X + p.X / _miniEchelle, _miniOrigine.Y + p.Y / _miniEchelle));
            };
            contenu.Children.Add(_miniCanvas);
            return PanneauFlottant(HorizontalAlignment.Left, VerticalAlignment.Bottom, contenu);
        }

        private void MettreAJourMiniCarte()
        {
            if (_miniCanvas == null || !_miniCarte.IsVisible) return;
            _miniCanvas.Children.Clear();
            double zoom = _toile.Zoom;
            if (zoom <= 0 || _toile.Bounds.Width <= 0) return;
            Point origine = _toile.OrigineVue;
            Rect vue = new Rect(-origine.X / zoom, -origine.Y / zoom,
                _toile.Bounds.Width / zoom, _toile.Bounds.Height / zoom);
            Rect monde = vue;
            List<Rect> rectangles = _toile.RectanglesNoeuds();
            foreach (Rect r in rectangles) monde = monde.Union(r);
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
            contenu.Children.Add(new TextBlock { Text = "Inspecteur", FontWeight = FontWeight.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            _inspecteurTexte = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 220 };
            contenu.Children.Add(_inspecteurTexte);
            return PanneauFlottant(HorizontalAlignment.Right, VerticalAlignment.Bottom, contenu);
        }

        private void MettreAJourInspecteur()
        {
            if (_inspecteurTexte == null || !_inspecteur.IsVisible) return;
            _inspecteurTexte.Text = TexteInspecteur(_toile);
        }

        public static string TexteInspecteur(ToileVue toile)
        {
            return PanneauxToile.TexteInspecteur(toile);
        }

        private Border ConstruireHistorique()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(new TextBlock { Text = "Historique", FontWeight = FontWeight.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            _historiqueTexte = new TextBlock { FontSize = 12 };
            contenu.Children.Add(_historiqueTexte);
            StackPanel boutons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0), Spacing = 4 };
            Button annuler = new Button { Content = "↶ Annuler", Padding = new Thickness(6, 2), Focusable = false };
            annuler.Click += delegate { _toile.Historique.Annuler(); };
            Button retablir = new Button { Content = "↷ Rétablir", Padding = new Thickness(6, 2), Focusable = false };
            retablir.Click += delegate { _toile.Historique.Refaire(); };
            boutons.Children.Add(annuler);
            boutons.Children.Add(retablir);
            contenu.Children.Add(boutons);
            return PanneauFlottant(HorizontalAlignment.Left, VerticalAlignment.Top, contenu);
        }

        private void MettreAJourHistorique()
        {
            if (_historiqueTexte == null || !_panneauHistorique.IsVisible) return;
            _historiqueTexte.Text = string.Format("{0} action(s) annulable(s)\n{1} action(s) rétablissable(s)",
                _toile.Historique.NbAnnulables, _toile.Historique.NbRefaisables);
        }

        private void MettreAJourPanneaux()
        {
            if (_miniCarte == null) return;
            _miniCarte.IsVisible = ParametresGlobaux.MiniCarteVisible;
            _inspecteur.IsVisible = ParametresGlobaux.InspecteurVisible;
            _panneauHistorique.IsVisible = ParametresGlobaux.HistoriqueVisible;
            if (_panneauFiltre != null)
            {
                _panneauFiltre.IsVisible = ParametresGlobaux.FiltreVisible;
                if (!ParametresGlobaux.FiltreVisible && _toile.FiltreActif) _toile.DefinirFiltreEtiquettes(null, false);
                if (_menuFiltre != null) Ui.Cocher(_menuFiltre, ParametresGlobaux.FiltreVisible);
                MettreAJourPanneauFiltre();
            }
            if (_menuMiniCarte != null) Ui.Cocher(_menuMiniCarte, ParametresGlobaux.MiniCarteVisible);
            if (_menuInspecteur != null) Ui.Cocher(_menuInspecteur, ParametresGlobaux.InspecteurVisible);
            if (_menuHistorique != null) Ui.Cocher(_menuHistorique, ParametresGlobaux.HistoriqueVisible);
            MettreAJourMiniCarte();
            MettreAJourInspecteur();
            MettreAJourHistorique();
        }

        // ------------------------------------------------------------------ filtre par étiquettes (v0.15)

        private Border ConstruirePanneauFiltre()
        {
            _filtreContenu = new StackPanel { MinWidth = 170 };
            Border panneau = PanneauFlottant(HorizontalAlignment.Left, VerticalAlignment.Top, _filtreContenu);
            panneau.IsVisible = false;
            return panneau;
        }

        private void MettreAJourPanneauFiltre()
        {
            if (_filtreContenu == null) return;
            RemplirPanneauFiltre(_filtreContenu, _toile, this);
        }

        public static void RemplirPanneauFiltre(StackPanel contenu, ToileVue toile, Window proprietaire)
        {
            PanneauxToile.RemplirPanneauFiltre(contenu, toile, proprietaire);
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
                IsVisible = false,
                Child = _tustContenu
            };
        }

        private void MettreAJourTust()
        {
            if (_tust == null) return;
            if (_menuTust != null) Ui.Cocher(_menuTust, ParametresGlobaux.TustVisible);
            _tust.IsVisible = ParametresGlobaux.TustVisible;
            RemplirTust(_tustContenu);
        }

        public static void RemplirTust(StackPanel contenu)
        {
            PanneauxToile.RemplirTust(contenu);
        }

        private async void OuvrirPreferencesFichier()
        {
            if (await DialoguePreferencesFichier.Afficher(this, _toile.Document, _toile.RectanglesNoeuds()))
            {
                _toile.AppliquerPreferencesDocument(true);
                RemplirCombosCouleurs();
                MettreAJourBarreFormat();
                MettreAJourTitre();
            }
        }

        private void OuvrirParametres()
        {
            DialogueParametres.AppliquerTheme = delegate(bool sombre)
            {
                Mentalo.Theme.Appliquer(sombre);
                _toile.AppliquerFondDocument();
                MettreAJourIconeTheme();
            };
            DialogueParametres.Afficher(this, delegate
            {
                RafraichirGestesMenus(_menuPrincipal);
                MettreAJourTust();
                MettreAJourVignette();
            });
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
                WindowState = WindowState.FullScreen;
            }
            else
            {
                WindowState = _etatAvantPleinEcran == WindowState.FullScreen ? WindowState.Normal : _etatAvantPleinEcran;
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
            _menuPrincipal.IsVisible = false;
            _barreModes.IsVisible = false;
            _barreFormat.IsVisible = false;
            _barreEtat.IsVisible = false;
            DefinirPleinEcran(true);
            MettreAJourMenusAffichage();
            _toile.Focus();
            _compteurPresentation.IsVisible = true;
            // La vue d'ensemble attend que le plein écran ait redimensionné la toile.
            Dispatcher.UIThread.Post(delegate { _toile.PresentationDebut(); }, DispatcherPriority.Loaded);
        }

        private void QuitterPresentation()
        {
            if (!_presentation) return;
            _presentation = false;
            _menuPrincipal.IsVisible = true;
            _barreModes.IsVisible = true;
            _barreFormat.IsVisible = true;
            _barreEtat.IsVisible = true;
            _compteurPresentation.IsVisible = false;
            if (_laserActif) BasculerLaser();
            DefinirPleinEcran(false);
            _toile.DefinirMode(string.IsNullOrEmpty(_modeAvantPresentation) ? "automatique" : _modeAvantPresentation);
            MettreAJourBarreModes();
            MettreAJourMenusAffichage();
        }

        private void MettreAJourMenusAffichage()
        {
            if (_menuPleinEcran != null) Ui.Cocher(_menuPleinEcran, _pleinEcran && !_presentation);
            if (_menuPresentation != null) Ui.Cocher(_menuPresentation, _presentation);
        }

        // ------------------------------------------------------------------ présentation : laser et compteur (v0.17)

        private Border ConstruireCompteurPresentation()
        {
            _compteurPresentationTexte = new TextBlock { Foreground = Chrome.TexteDoux, FontSize = 12 };
            Border cadre = PanneauFlottant(HorizontalAlignment.Right, VerticalAlignment.Bottom, _compteurPresentationTexte);
            cadre.Padding = new Thickness(10, 4, 10, 4);
            cadre.IsVisible = false;
            Ui.Infobulle(cadre, "→ / Espace : diapositive suivante · ← : précédente · Début : vue d'ensemble · L : pointeur laser · Échap : quitter");
            return cadre;
        }

        /// <summary>Un point rouge lumineux qui suit la souris (touche L en présentation).</summary>
        private Canvas ConstruireLaser()
        {
            Canvas calque = new Canvas { IsHitTestVisible = false, IsVisible = false };
            BoxShadow halo = new BoxShadow();
            halo.Color = Color.FromArgb(0xE6, 0xFF, 0x2D, 0x2D);
            halo.Blur = 18;
            _laser = new Border
            {
                Width = 16, Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0x2D, 0x2D)),
                BoxShadow = new BoxShadows(halo)
            };
            calque.Children.Add(_laser);
            _zoneCentrale.AddHandler(PointerMovedEvent, delegate(object expediteur, PointerEventArgs e)
            {
                _dernierPointeur = e.GetPosition(_zoneCentrale);
                if (!_laserActif) return;
                Canvas.SetLeft(_laser, _dernierPointeur.X - _laser.Width / 2);
                Canvas.SetTop(_laser, _dernierPointeur.Y - _laser.Height / 2);
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            return calque;
        }

        private void BasculerLaser()
        {
            _laserActif = !_laserActif;
            _calqueLaser.IsVisible = _laserActif;
            _toile.Cursor = _laserActif ? new Cursor(StandardCursorType.None) : Ui.CurseurFleche;
            if (_laserActif)
            {
                Canvas.SetLeft(_laser, _dernierPointeur.X - _laser.Width / 2);
                Canvas.SetTop(_laser, _dernierPointeur.Y - _laser.Height / 2);
            }
        }

        // ------------------------------------------------------------------ modèles de cartes (v0.17)

        private void RemplirMenuModeles()
        {
            if (_menuModeles == null) return;
            _menuModeles.Items.Clear();
            List<string> modeles = Modeles.Lister();
            if (modeles.Count == 0)
            {
                MenuItem aucun = Ui.Element("(aucun modèle — enregistrez une carte comme modèle)", null);
                aucun.IsEnabled = false;
                _menuModeles.Items.Add(aucun);
            }
            foreach (string chemin in modeles)
            {
                string cible = chemin;
                MenuItem element = Ui.Element(Modeles.NomDe(chemin), delegate { NouveauDepuisModele(cible); });
                Ui.Infobulle(element, chemin);
                _menuModeles.Items.Add(element);
            }
            _menuModeles.Items.Add(Ui.Separateur());
            MenuItem enregistrer = Ui.Element("Enregistrer la carte actuelle comme modèle…", delegate { EnregistrerCommeModele(); });
            Ui.Infobulle(enregistrer, "Défauts du fichier, thème, boîtes de départ : tout ce que contient la carte devient le point de départ des nouvelles");
            _menuModeles.Items.Add(enregistrer);
            _menuModeles.Items.Add(Ui.Element("Ouvrir le dossier des modèles", delegate
            {
                try { ToileVue.OuvrirAvecLeSysteme(Modeles.Dossier()); }
                catch { }
            }));
        }

        private async void NouveauDepuisModele(string chemin)
        {
            if (!await ConfirmerAbandon()) return;
            try
            {
                DocumentCarte document = Fichier.Charger(chemin);
                document.DateCreation = "";
                document.DateModification = "";
                NouvelleSessionSecours();
                _toile.ChargerDocument(document);
                _cheminFichier = null;
                _modifie = false;
                MettreAJourTitre();
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this, string.Format("Impossible d'ouvrir le modèle :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
                RemplirMenuModeles();
            }
        }

        private async void EnregistrerCommeModele()
        {
            string suggestion = !string.IsNullOrWhiteSpace(_toile.Document.NomAffichage) ? _toile.Document.NomAffichage
                : _cheminFichier != null ? System.IO.Path.GetFileNameWithoutExtension(_cheminFichier) : "Mon modèle";
            string nom = await DialogueSaisie.DemanderTexte(this, "Enregistrer comme modèle", "Nom du modèle :", suggestion);
            if (string.IsNullOrWhiteSpace(nom)) return;
            if (Modeles.Existe(nom.Trim()))
            {
                ResultatMessage reponse = await DialogueMessage.Afficher(this,
                    string.Format("Un modèle « {0} » existe déjà. Le remplacer ?", nom.Trim()),
                    "Modèles", BoutonsMessage.OuiNon, IconeMessage.Question);
                if (reponse != ResultatMessage.Oui) return;
            }
            try
            {
                Modeles.Enregistrer(_toile.Document, nom);
                RemplirMenuModeles();
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this, string.Format("Impossible d'enregistrer le modèle :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
        }

        // ------------------------------------------------------------------ thèmes de carte (v0.17)

        private async void AppliquerTheme(ThemeCarte theme)
        {
            bool carteVide = _toile.Document.Noeuds.Count == 0;
            bool auxExistants = false;
            if (!carteVide)
            {
                ResultatMessage reponse = await DialogueMessage.Afficher(this,
                    string.Format("Appliquer le thème « {0} » ?\n{1}\n\nOui : aussi aux boîtes, liens et groupes existants (annulable).\nNon : seulement au fond et aux nouveaux éléments.",
                        theme.Nom, theme.Description),
                    "Thème de la carte", BoutonsMessage.OuiNonAnnuler, IconeMessage.Question);
                if (reponse == ResultatMessage.Annuler) return;
                auxExistants = reponse == ResultatMessage.Oui;
            }
            _toile.AppliquerThemeCarte(theme, auxExistants);
            MettreAJourBarreFormat();
        }

        // ------------------------------------------------------------------ import / export

        /// <summary>Le nom proposé pour un fichier exporté, calqué sur la carte ouverte.</summary>
        private string NomExport()
        {
            if (!string.IsNullOrWhiteSpace(_toile.Document.NomAffichage)) return _toile.Document.NomAffichage;
            if (_cheminFichier != null) return System.IO.Path.GetFileNameWithoutExtension(_cheminFichier);
            return "ma carte";
        }

        private System.Threading.Tasks.Task<string> DemanderCheminExport(string titre, string nomType, string extension)
        {
            return Ui.EnregistrerSous(this, titre, NomExport(), extension, Ui.Type(nomType, "*." + extension), Ui.TypeTous);
        }

        private async void SignalerExportImpossible(string erreur)
        {
            await DialogueMessage.Afficher(this, string.Format("Export impossible :\n{0}", erreur),
                "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
        }

        private async void ExporterCanvas()
        {
            string chemin = await DemanderCheminExport("Exporter en JSON Canvas", "Canevas JSON Canvas", JsonCanvas.Extension);
            if (chemin == null) return;
            try { JsonCanvas.Exporter(_toile, chemin); }
            catch (Exception ex) { SignalerExportImpossible(ex.Message); }
        }

        private async void ImporterCanvas()
        {
            string chemin = await Ui.ChoisirFichier(this, "Importer un canevas JSON Canvas", Ui.Type("Canevas JSON Canvas", "*.canvas"), Ui.TypeTous);
            if (chemin == null) return;
            try
            {
                List<Noeud> noeuds;
                List<Lien> liens;
                List<Groupe> groupes;
                JsonCanvas.Importer(chemin, out noeuds, out liens, out groupes);
                if (noeuds.Count == 0)
                {
                    await DialogueMessage.Afficher(this, "Aucune carte exploitable dans ce canevas.",
                        "Mental-O", BoutonsMessage.OK, IconeMessage.Information);
                    return;
                }
                _toile.ImporterCanevas(noeuds, liens, groupes);
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this, string.Format("Import impossible :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
            }
        }

        /// <summary>Imprimer : Avalonia n'a pas de dialogue d'impression. La carte est rendue
        /// en PDF temporaire, ouvert avec l'application associée (qui sait imprimer).</summary>
        private async void Imprimer()
        {
            string erreur;
            byte[] png = _toile.RendrePng(2.0, 8000.0, out erreur);
            if (png == null)
            {
                await DialogueMessage.Afficher(this, string.Format("Impression impossible :\n{0}", erreur),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
                return;
            }
            try
            {
                string dossier = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Mental-o", "impression");
                Directory.CreateDirectory(dossier);
                string chemin = System.IO.Path.Combine(dossier, NomPropre(NomExport()) + ".pdf");
                ExportPdf.Ecrire(png, chemin);
                ToileVue.OuvrirAvecLeSysteme(chemin);
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this, string.Format("Impression impossible :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
            }
        }

        private static string NomPropre(string nom)
        {
            foreach (char interdit in System.IO.Path.GetInvalidFileNameChars()) nom = nom.Replace(interdit, '-');
            return nom.Length == 0 ? "carte" : nom;
        }

        private async void ExporterPdf()
        {
            string chemin = await DemanderCheminExport("Exporter en PDF", "Document PDF", "pdf");
            if (chemin == null) return;
            string erreur;
            byte[] png = _toile.RendrePng(2.0, 8000.0, out erreur);
            if (png == null) { SignalerExportImpossible(erreur); return; }
            try { ExportPdf.Ecrire(png, chemin); }
            catch (Exception ex) { SignalerExportImpossible(ex.Message); }
        }

        private async void ExporterImage()
        {
            string chemin = await DemanderCheminExport("Exporter en PNG", "Image PNG", "png");
            if (chemin == null) return;
            string erreur;
            if (!_toile.ExporterPng(chemin, out erreur)) SignalerExportImpossible(erreur);
        }

        private async void ExporterSvg()
        {
            string chemin = await DemanderCheminExport("Exporter en SVG", "Dessin SVG", "svg");
            if (chemin == null) return;
            try { Export.ExporterSvg(_toile, chemin); }
            catch (Exception ex) { SignalerExportImpossible(ex.Message); }
        }

        private async void ExporterMarkdown()
        {
            string chemin = await DemanderCheminExport("Exporter en Markdown", "Texte Markdown", "md");
            if (chemin == null) return;
            try { Export.ExporterMarkdown(_toile.Document, NomExport(), chemin); }
            catch (Exception ex) { SignalerExportImpossible(ex.Message); }
        }

        private async void ExporterOpml()
        {
            string chemin = await DemanderCheminExport("Exporter en OPML", "Plan OPML", "opml");
            if (chemin == null) return;
            try { Export.ExporterOpml(_toile.Document, NomExport(), chemin); }
            catch (Exception ex) { SignalerExportImpossible(ex.Message); }
        }

        private async void ImporterOpml()
        {
            string chemin = await Ui.ChoisirFichier(this, "Importer un plan OPML", Ui.Type("Plan OPML", "*.opml", "*.xml"), Ui.TypeTous);
            if (chemin == null) return;
            try
            {
                List<Noeud> noeuds;
                List<Lien> liens;
                Export.ImporterOpml(chemin, _toile.CentreCanvas(), out noeuds, out liens);
                if (noeuds.Count == 0)
                {
                    await DialogueMessage.Afficher(this, "Aucun élément exploitable dans ce plan.",
                        "Mental-O", BoutonsMessage.OK, IconeMessage.Information);
                    return;
                }
                _toile.ImporterArbre(noeuds, liens);
            }
            catch (Exception ex)
            {
                await DialogueMessage.Afficher(this, string.Format("Import impossible :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
            }
        }

        private async void ImporterTexteAPuces()
        {
            string texte = await DialogueSaisie.DemanderTexteMultiligne(this, "Importer un texte à puces",
                "Chaque ligne devient une boîte ; l'indentation (espaces ou tabulations)\n" +
                "fait de la ligne l'enfant de la précédente, reliée par une flèche.");
            if (texte == null) return;
            List<Noeud> noeuds;
            List<Lien> liens;
            Export.ImporterTexteAPuces(texte, _toile.CentreCanvas(), out noeuds, out liens);
            if (noeuds.Count == 0)
            {
                await DialogueMessage.Afficher(this, "Aucune ligne exploitable dans ce texte.",
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Information);
                return;
            }
            _toile.ImporterArbre(noeuds, liens);
        }

        private async void ChoisirImage()
        {
            string chemin = await Ui.ChoisirFichier(this, "Insérer une image", Ui.TypeImages, Ui.TypeTous);
            if (chemin != null) _toile.InsererImageDepuisFichier(chemin);
        }

        /// <summary>Insertion > Fichier : un ou plusieurs fichiers, liés à la boîte sélectionnée
        /// ou placés dans des boîtes neuves. Une image choisie ici suit la voie des images.</summary>
        private async void ChoisirFichier()
        {
            List<string> chemins = await Ui.ChoisirFichiers(this, "Insérer un ou plusieurs fichiers", true, Ui.TypeTous);
            foreach (string chemin in chemins)
                _toile.InsererFichierDepuisChemin(chemin);
        }
    }
}
