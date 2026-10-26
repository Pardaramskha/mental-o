using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Modele;
using Mentalo.Parametres;

namespace Mentalo.Vue
{
    /// <summary>LES PANNEAUX FLOTTANTS DE LA TOILE (26/10/2026), partagés par
    /// Mental-O seul et par le DLC Marabook : la mini-carte (en bas à gauche,
    /// cliquable), l'inspecteur de sélection (en bas à droite), l'historique
    /// (en haut à gauche, avec Annuler / Rétablir), le filtre par étiquettes
    /// (en haut à gauche), le Tust des raccourcis (en haut à droite), la barre
    /// de recherche (en haut au centre) et le compteur de diapositives de la
    /// présentation (en bas à droite). Leur visibilité suit ParametresGlobaux ;
    /// l'hôte choisit l'habillage et les pose sur la zone centrale.</summary>
    public class PanneauxFlottants
    {
        private readonly ToileVue _toile;
        private readonly Func<Window> _proprietaire;
        private readonly IHabillage _h;

        private Canvas _miniCanvas;
        private double _miniEchelle = 1;
        private Point _miniOrigine;
        private TextBlock _inspecteurTexte, _historiqueTexte, _compteurTexte;
        private StackPanel _filtreContenu, _tustContenu;
        private TextBox _champRecherche;
        private TextBlock _compteurRecherche;
        private List<Noeud> _resultats = new List<Noeud>();
        private int _indiceResultat;

        public Border MiniCarte { get; private set; }
        public Border Inspecteur { get; private set; }
        public Border Historique { get; private set; }
        public Border Filtre { get; private set; }
        public Border Tust { get; private set; }
        public Border Recherche { get; private set; }
        public Border CompteurPresentation { get; private set; }

        /// <summary>La très légère ombre en vignette sur les quatre bords du tableau
        /// (Paramètres > apparence) — un calque sous les panneaux, posé par AjouterA.</summary>
        public Grid Vignette { get; private set; }

        /// <summary>Faux : l'hôte n'offre pas le panneau Historique (Marabook — Ctrl+Z
        /// y suffit) ; il reste masqué quoi que disent les Paramètres.</summary>
        public bool HistoriqueAutorise = true;

        /// <param name="avecLaser">Le compteur mentionne la touche L (Mental-O seul a le pointeur laser).</param>
        public PanneauxFlottants(ToileVue toile, Func<Window> proprietaire, IHabillage habillage, bool avecLaser)
        {
            _toile = toile;
            _proprietaire = proprietaire;
            _h = habillage;
            Vignette = ConstruireVignette();
            MiniCarte = ConstruireMiniCarte();
            Inspecteur = ConstruireInspecteur();
            Historique = ConstruireHistorique();
            _filtreContenu = new StackPanel { MinWidth = 170 };
            Filtre = Panneau(HorizontalAlignment.Left, VerticalAlignment.Top, _filtreContenu);
            _tustContenu = new StackPanel();
            Tust = Panneau(HorizontalAlignment.Right, VerticalAlignment.Top, _tustContenu);
            Tust.Margin = new Thickness(0, 10, 12, 0);
            Tust.Padding = new Thickness(12, 8, 12, 10);
            Tust.MaxWidth = 290;
            Recherche = ConstruireRecherche();
            _compteurTexte = new TextBlock { Foreground = _h.TexteDoux, FontSize = 12 };
            CompteurPresentation = Panneau(HorizontalAlignment.Right, VerticalAlignment.Bottom, _compteurTexte);
            CompteurPresentation.Padding = new Thickness(10, 4, 10, 4);
            Ui.Infobulle(CompteurPresentation, "→ / Espace : diapositive suivante · ← : précédente · Début : vue d'ensemble"
                + (avecLaser ? " · L : pointeur laser" : "") + " · Échap : quitter");

            _toile.VueChangee += MettreAJourMiniCarte;
            _toile.DocumentModifie += delegate { MettreAJourMiniCarte(); MettreAJourInspecteur(); };
            _toile.FormatCibleChange += MettreAJourInspecteur;
            _toile.Historique.Change += MettreAJourHistorique;
            _toile.EtiquettesChange += MettreAJourFiltre;
            _toile.DiapositiveChangee += delegate(int courante, int total)
            {
                _compteurTexte.Text = courante == 0
                    ? (total == 0 ? "Vue d'ensemble" : "Vue d'ensemble · " + total + " diapositive" + (total > 1 ? "s" : "") + " — → pour commencer")
                    : courante + " / " + total;
            };
        }

        /// <summary>Pose tous les panneaux sur la zone centrale (au-dessus de la toile).</summary>
        public void AjouterA(Panel zone)
        {
            zone.Children.Add(Vignette);
            zone.Children.Add(Recherche);
            zone.Children.Add(Tust);
            zone.Children.Add(MiniCarte);
            zone.Children.Add(Inspecteur);
            zone.Children.Add(Historique);
            zone.Children.Add(Filtre);
            zone.Children.Add(CompteurPresentation);
        }

        private Border Panneau(HorizontalAlignment horizontal, VerticalAlignment vertical, Control contenu)
        {
            return new Border
            {
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = new Thickness(12, 10, 12, 10),
                Background = _h.FondPanneau,
                BorderBrush = _h.Bordure,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 7, 10, 8),
                IsVisible = false,
                Child = contenu
            };
        }

        private TextBlock Titre(string texte)
        {
            return new TextBlock { Text = texte, FontWeight = FontWeight.Bold, FontSize = 12, Foreground = _h.Encre, Margin = new Thickness(0, 0, 0, 4) };
        }

        // ------------------------------------------------------------------ vignette

        private static Grid ConstruireVignette()
        {
            Grid calque = new Grid { IsHitTestVisible = false };
            Color ombre = Color.FromArgb(0x22, 0x00, 0x00, 0x00);
            Color rien = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
            calque.Children.Add(new Rectangle { Height = 26, VerticalAlignment = VerticalAlignment.Top, Fill = Degrade(ombre, rien, false) });
            calque.Children.Add(new Rectangle { Height = 26, VerticalAlignment = VerticalAlignment.Bottom, Fill = Degrade(rien, ombre, false) });
            calque.Children.Add(new Rectangle { Width = 26, HorizontalAlignment = HorizontalAlignment.Left, Fill = Degrade(ombre, rien, true) });
            calque.Children.Add(new Rectangle { Width = 26, HorizontalAlignment = HorizontalAlignment.Right, Fill = Degrade(rien, ombre, true) });
            calque.IsVisible = ParametresGlobaux.VignetteTableau;
            return calque;
        }

        private static LinearGradientBrush Degrade(Color de, Color vers, bool horizontal)
        {
            LinearGradientBrush brosse = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = horizontal ? new RelativePoint(1, 0, RelativeUnit.Relative) : new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            brosse.GradientStops.Add(new GradientStop(de, 0));
            brosse.GradientStops.Add(new GradientStop(vers, 1));
            return brosse;
        }

        // ------------------------------------------------------------------ mini-carte

        private Border ConstruireMiniCarte()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(Titre("Mini-carte"));
            _miniCanvas = new Canvas { Width = 166, Height = 106, Background = _h.FondMiniCarte, ClipToBounds = true };
            _miniCanvas.PointerPressed += delegate(object expediteur, PointerPressedEventArgs e)
            {
                Point p = e.GetPosition(_miniCanvas);
                _toile.CentrerSur(new Point(_miniOrigine.X + p.X / _miniEchelle, _miniOrigine.Y + p.Y / _miniEchelle));
            };
            contenu.Children.Add(_miniCanvas);
            return Panneau(HorizontalAlignment.Left, VerticalAlignment.Bottom, contenu);
        }

        public void MettreAJourMiniCarte()
        {
            if (_miniCanvas == null || !MiniCarte.IsVisible) return;
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
                Rectangle forme = new Rectangle
                {
                    Width = Math.Max(2, r.Width * _miniEchelle),
                    Height = Math.Max(2, r.Height * _miniEchelle),
                    Fill = _h.TexteDoux,
                    Opacity = 0.7,
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
                Stroke = _h.Accent,
                StrokeThickness = 1,
                Fill = _h.Accent,
                Opacity = 0.75
            };
            // Le cadre de la vue : trait plein, remplissage très léger.
            Rectangle voile = new Rectangle { Width = cadreVue.Width, Height = cadreVue.Height, Fill = _h.Accent, Opacity = 0.1 };
            cadreVue.Fill = null;
            Canvas.SetLeft(voile, (vue.X - monde.X) * _miniEchelle);
            Canvas.SetTop(voile, (vue.Y - monde.Y) * _miniEchelle);
            _miniCanvas.Children.Add(voile);
            Canvas.SetLeft(cadreVue, (vue.X - monde.X) * _miniEchelle);
            Canvas.SetTop(cadreVue, (vue.Y - monde.Y) * _miniEchelle);
            _miniCanvas.Children.Add(cadreVue);
        }

        // ------------------------------------------------------------------ inspecteur, historique

        private Border ConstruireInspecteur()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(Titre("Inspecteur"));
            _inspecteurTexte = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 220, Foreground = _h.Encre };
            contenu.Children.Add(_inspecteurTexte);
            return Panneau(HorizontalAlignment.Right, VerticalAlignment.Bottom, contenu);
        }

        public void MettreAJourInspecteur()
        {
            if (_inspecteurTexte == null || !Inspecteur.IsVisible) return;
            _inspecteurTexte.Text = PanneauxToile.TexteInspecteur(_toile);
        }

        private Border ConstruireHistorique()
        {
            StackPanel contenu = new StackPanel();
            contenu.Children.Add(Titre("Historique"));
            _historiqueTexte = new TextBlock { FontSize = 12, Foreground = _h.Encre };
            contenu.Children.Add(_historiqueTexte);
            StackPanel boutons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0), Spacing = 4 };
            Button annuler = _h.BoutonTexte("↶ Annuler", "Annuler (" + ParametresGlobaux.GesteAffiche("annuler") + ")");
            annuler.Margin = new Thickness(0);
            annuler.Click += delegate { _toile.Historique.Annuler(); };
            Button retablir = _h.BoutonTexte("↷ Rétablir", "Rétablir (" + ParametresGlobaux.GesteAffiche("retablir") + ")");
            retablir.Margin = new Thickness(0);
            retablir.Click += delegate { _toile.Historique.Refaire(); };
            boutons.Children.Add(annuler);
            boutons.Children.Add(retablir);
            contenu.Children.Add(boutons);
            return Panneau(HorizontalAlignment.Left, VerticalAlignment.Top, contenu);
        }

        public void MettreAJourHistorique()
        {
            if (_historiqueTexte == null || !Historique.IsVisible) return;
            _historiqueTexte.Text = string.Format("{0} action(s) annulable(s)\n{1} action(s) rétablissable(s)",
                _toile.Historique.NbAnnulables, _toile.Historique.NbRefaisables);
        }

        // ------------------------------------------------------------------ filtre, Tust

        public void MettreAJourFiltre()
        {
            if (_filtreContenu == null || !Filtre.IsVisible) return;
            PanneauxToile.RemplirPanneauFiltre(_filtreContenu, _toile, _proprietaire());
        }

        public void MettreAJourTust()
        {
            if (_tustContenu == null || !Tust.IsVisible) return;
            PanneauxToile.RemplirTust(_tustContenu);
        }

        /// <summary>La visibilité de chaque panneau suit les Paramètres ; les contenus se rafraîchissent.</summary>
        public void MettreAJourTout()
        {
            Vignette.IsVisible = ParametresGlobaux.VignetteTableau;
            MiniCarte.IsVisible = ParametresGlobaux.MiniCarteVisible;
            Inspecteur.IsVisible = ParametresGlobaux.InspecteurVisible;
            Historique.IsVisible = HistoriqueAutorise && ParametresGlobaux.HistoriqueVisible;
            Filtre.IsVisible = ParametresGlobaux.FiltreVisible;
            Tust.IsVisible = ParametresGlobaux.TustVisible;
            if (!ParametresGlobaux.FiltreVisible && _toile.FiltreActif) _toile.DefinirFiltreEtiquettes(null, false);
            MettreAJourMiniCarte();
            MettreAJourInspecteur();
            MettreAJourHistorique();
            MettreAJourFiltre();
            MettreAJourTust();
        }

        // ------------------------------------------------------------------ recherche

        private Border ConstruireRecherche()
        {
            StackPanel contenu = new StackPanel { Orientation = Orientation.Horizontal };
            _champRecherche = new TextBox { Width = 200, VerticalAlignment = VerticalAlignment.Center };
            Ui.Infobulle(_champRecherche, "Entrée : occurrence suivante · Maj+Entrée : précédente · Échap : fermer");
            _champRecherche.PropertyChanged += delegate(object expediteur, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == TextBox.TextProperty) ActualiserRecherche(true);
            };
            _champRecherche.AddHandler(InputElement.KeyDownEvent, delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { NaviguerRecherche((e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1); e.Handled = true; }
                else if (e.Key == Key.Escape) { FermerRecherche(); e.Handled = true; }
            }, RoutingStrategies.Tunnel);
            contenu.Children.Add(_champRecherche);
            _compteurRecherche = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                Foreground = _h.TexteDoux,
                MinWidth = 32,
                TextAlignment = TextAlignment.Center
            };
            contenu.Children.Add(_compteurRecherche);
            Button precedent = _h.Outil(_h.Glyphe("‹", 14), "Occurrence précédente (Maj+Entrée)");
            precedent.Click += delegate { NaviguerRecherche(-1); };
            contenu.Children.Add(precedent);
            Button suivant = _h.Outil(_h.Glyphe("›", 14), "Occurrence suivante (Entrée)");
            suivant.Click += delegate { NaviguerRecherche(1); };
            contenu.Children.Add(suivant);
            Button fermer = _h.Outil(_h.Glyphe("✕", 13), "Fermer (Échap)");
            fermer.Margin = new Thickness(8, 0, 0, 0);
            fermer.Click += delegate { FermerRecherche(); };
            contenu.Children.Add(fermer);
            Border panneau = Panneau(HorizontalAlignment.Center, VerticalAlignment.Top, contenu);
            panneau.Margin = new Thickness(0, 10, 0, 0);
            panneau.Padding = new Thickness(10, 6, 10, 6);
            return panneau;
        }

        public bool RechercheVisible { get { return Recherche.IsVisible; } }

        public void OuvrirRecherche()
        {
            Recherche.IsVisible = true;
            _champRecherche.Focus();
            _champRecherche.SelectAll();
            ActualiserRecherche(false);
        }

        public void FermerRecherche()
        {
            Recherche.IsVisible = false;
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

        public void NaviguerRecherche(int sens)
        {
            if (_resultats.Count == 0) return;
            _indiceResultat = (_indiceResultat + sens + _resultats.Count) % _resultats.Count;
            _compteurRecherche.Text = (_indiceResultat + 1) + "/" + _resultats.Count;
            _toile.AllerAuNoeud(_resultats[_indiceResultat]);
        }
    }
}
