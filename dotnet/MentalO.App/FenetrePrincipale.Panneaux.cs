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
        // ------------------------------------------------------------------ panneaux flottants (PanneauxFlottants, partagés)

        private void MettreAJourMiniCarte() { _panneaux.MettreAJourMiniCarte(); }
        private void MettreAJourInspecteur() { _panneaux.MettreAJourInspecteur(); }
        private void MettreAJourHistorique() { _panneaux.MettreAJourHistorique(); }
        private void MettreAJourPanneauFiltre() { _panneaux.MettreAJourFiltre(); }

        /// <summary>La visibilité des panneaux suit les Paramètres ; les coches du menu Fenêtres aussi.</summary>
        private void MettreAJourPanneaux()
        {
            _panneaux.MettreAJourTout();
            if (_menuFiltre != null) Ui.Cocher(_menuFiltre, ParametresGlobaux.FiltreVisible);
            if (_menuMiniCarte != null) Ui.Cocher(_menuMiniCarte, ParametresGlobaux.MiniCarteVisible);
            if (_menuInspecteur != null) Ui.Cocher(_menuInspecteur, ParametresGlobaux.InspecteurVisible);
            if (_menuHistorique != null) Ui.Cocher(_menuHistorique, ParametresGlobaux.HistoriqueVisible);
            if (_menuTust != null) Ui.Cocher(_menuTust, ParametresGlobaux.TustVisible);
        }

        private void MettreAJourTust()
        {
            if (_menuTust != null) Ui.Cocher(_menuTust, ParametresGlobaux.TustVisible);
            _panneaux.Tust.IsVisible = ParametresGlobaux.TustVisible;
            _panneaux.MettreAJourTust();
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
                MettreAJourPanneaux(); // Tust, vignette, panneaux : tout suit les Paramètres
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
            _panneaux.CompteurPresentation.IsVisible = true;
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
            _panneaux.CompteurPresentation.IsVisible = false;
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
