using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Modele;
using Mentalo.Parametres;

namespace Mentalo.Vue
{
    /// <summary>LES BARRES DE LA TOILE (26/10/2026), partagées par Mental-O seul
    /// et par le DLC Marabook : la barre des modes (automatique, déplacement,
    /// liens, lecture) avec les onglets de la barre de personnalisation (Texte /
    /// Boîte / Groupe / Lien, épinglables), et la barre de personnalisation
    /// elle-même — police, taille, gras…, fond, bordure, coins, ombre, cadre
    /// du groupe, titre, couleur du lien, flèches, tracé, étiquette. Elle se
    /// synchronise seule avec la toile (FormatCibleChange, ModeChange) ; l'hôte
    /// choisit l'habillage (IHabillage) et place les deux barres.</summary>
    public class BarresToile
    {
        private readonly ToileVue _toile;
        private readonly IHabillage _h;
        private readonly bool _vertical;
        private bool _synchroEnCours;

        // Inspecteur vertical (le rail de Marabook, 26/10/2026)
        private StackPanel _sectionTexte, _sectionBoite, _sectionGroupe, _sectionLien, _sectionSelection;
        private TextBlock _inviteInspecteur, _texteSelection;
        private Control _filetFin;
        private ExtrasInspecteur _extras;

        // Barre de modes
        private ToggleButton _btnModeAuto, _btnModeDeplacement, _btnModeLiens, _btnModeLecture;
        private ToggleButton _epingleOnglet;
        private TextBlock _texteMode;
        private ToggleButton _ongletTexte, _ongletBoite, _ongletGroupe, _ongletLien;
        private string _ongletActif = "texte";

        // Barre de personnalisation
        private Border _barreFormat;
        private Panel _panneauTexte, _panneauBoites, _panneauGroupes, _panneauLiens;
        private ComboBox _comboPolice, _comboTaille, _comboCouleur, _comboSurlignage;

        /// <summary>Un sélecteur de police venu de l'hôte (1/11, Rémi : dans
        /// Marabook, l'autocomplète de son éditeur) : le contrôle à poser dans
        /// le panneau Texte, la police courante à lui montrer (vide = mêlée),
        /// et son choix. Null = la liste déroulante de la toile.</summary>
        public interface ISelecteurPolice
        {
            Control Controle { get; }
            void Poser(string police);
            event Action<string> Choisie;
        }

        public static Func<ISelecteurPolice> FabriqueSelecteurPolice;
        private ISelecteurPolice _selecteurPolice;
        private ToggleButton _btnGras, _btnItalique, _btnSouligne, _btnBarre;
        private ToggleButton _btnAlignGauche, _btnAlignCentre, _btnAlignDroite;
        private ComboBox _comboFond, _comboBordureCouleur, _comboBordureEpaisseur, _comboBordureTrait, _comboCoins;
        private ComboBox _comboOmbre, _comboOpacite;
        private ComboBox _comboCouleurGroupe, _comboPositionTitre;
        private ComboBox _comboTraitGroupe, _comboEpaisseurGroupe, _comboCouleurTitre, _comboFondTitre, _comboTailleTitre;
        private TextBox _champTitreGroupe;
        private ComboBox _comboCouleurLien, _comboEpaisseurLien, _comboTraitLien, _comboFlecheLien, _comboTraceLien;
        private TextBox _champEtiquetteLien;

        private const string TagPersonnalisee = "__perso__";
        private DocumentCarte _documentCouleurs;
        private int _nbCouleursPerso;

        /// <summary>Après chaque synchronisation avec la sélection (l'hôte met à jour son inspecteur).</summary>
        public event Action Synchronisee;

        public BarresToile(ToileVue toile, IHabillage habillage) : this(toile, habillage, false)
        {
        }

        /// <param name="vertical">Les panneaux se présentent en colonne (ConstruireInspecteur)
        /// plutôt qu'en barre : les contrôles passent à la ligne, pas de séparateurs.</param>
        public BarresToile(ToileVue toile, IHabillage habillage, bool vertical)
        {
            _toile = toile;
            _h = habillage;
            _vertical = vertical;
            _toile.ModeChange += MettreAJourModes;
            _toile.FormatCibleChange += MettreAJourFormat;
        }

        /// <summary>Le conteneur d'un panneau de réglages : une ligne en barre, une
        /// pile de rangées en colonne (Rangee).</summary>
        private Panel NouveauPanneau()
        {
            if (_vertical) return new StackPanel { Orientation = Orientation.Vertical };
            return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };
        }

        /// <summary>Une rangée du panneau (27/10/2026) : en colonne, un WrapPanel
        /// à part — gras/italique/souligné/barré sur leur ligne, les alignements
        /// sur la leur, les listes sur la leur ; en barre, le panneau lui-même.</summary>
        private Panel Rangee(Panel panneau)
        {
            if (!_vertical) return panneau;
            WrapPanel rangee = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            panneau.Children.Add(rangee);
            return rangee;
        }

        private void AjouterSeparateur(Panel panneau)
        {
            if (!_vertical) panneau.Children.Add(_h.Separateur());
        }

        /// <summary>La barre de personnalisation (nulle avant ConstruireBarreFormat).</summary>
        public Border BarreFormat { get { return _barreFormat; } }

        public string OngletActif { get { return _ongletActif; } }

        // ------------------------------------------------------------------ barre de modes

        /// <summary>Le contenu de la barre des modes : « Mode : » et ses quatre
        /// bascules, « Barre d'outils : » et ses quatre onglets, l'épingle, le
        /// commentaire du mode. L'hôte le pose dans sa propre barre.</summary>
        public StackPanel ConstruirePanneauModes()
        {
            return ConstruirePanneauModes(true);
        }

        /// <param name="avecOnglets">Avec « Barre d'outils : » et ses onglets (faux quand
        /// les réglages vivent dans un inspecteur, pas dans une barre).</param>
        public StackPanel ConstruirePanneauModes(bool avecOnglets)
        {
            StackPanel panneau = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 3, 6, 3) };
            panneau.Children.Add(TexteDoux("Mode :"));
            _btnModeAuto = BoutonMode("all-mode", "automatique",
                "Mode automatique — tout est actif : déplacement, liens, édition (" + ParametresGlobaux.GesteAffiche("mode-automatique") + ")");
            _btnModeDeplacement = BoutonMode("move-mode", "deplacement",
                "Mode déplacement — déplacer les boîtes uniquement, pas de création de liens (" + ParametresGlobaux.GesteAffiche("mode-deplacement") + ")");
            _btnModeLiens = BoutonMode("link-mode", "liens",
                "Mode liens — créer des liens uniquement, les boîtes ne bougent pas (" + ParametresGlobaux.GesteAffiche("mode-liens") + ")");
            _btnModeLecture = BoutonMode("read-mode", "lecture",
                "Mode lecture — naviguer sans rien modifier (" + ParametresGlobaux.GesteAffiche("mode-lecture") + ")");
            panneau.Children.Add(_btnModeAuto);
            panneau.Children.Add(_btnModeDeplacement);
            panneau.Children.Add(_btnModeLiens);
            panneau.Children.Add(_btnModeLecture);

            if (avecOnglets)
            {
                AjouterSeparateur(panneau);
                panneau.Children.Add(TexteDoux("Barre d'outils :"));
                _ongletTexte = BoutonOnglet("Texte", "text-custom-bar");
                _ongletBoite = BoutonOnglet("Boîte", "box-custom-bar");
                _ongletGroupe = BoutonOnglet("Groupe", "group-custom-bar");
                _ongletLien = BoutonOnglet("Lien", "links-custom-bar");
                panneau.Children.Add(_ongletTexte);
                panneau.Children.Add(_ongletBoite);
                panneau.Children.Add(_ongletGroupe);
                panneau.Children.Add(_ongletLien);
                _epingleOnglet = _h.Bascule(_h.Glyphe("⚲", 13), "Épingler l'onglet actuel : la sélection ne le change plus automatiquement");
                panneau.Children.Add(_epingleOnglet);
            }

            _texteMode = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Foreground = _h.TexteDoux,
                FontStyle = FontStyle.Italic
            };
            panneau.Children.Add(_texteMode);
            MettreAJourModes();
            SelectionOnglet(_ongletActif);
            return panneau;
        }

        private TextBlock TexteDoux(string texte)
        {
            return new TextBlock
            {
                Text = texte,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = _h.TexteDoux
            };
        }

        private ToggleButton BoutonMode(string icone, string mode, string infobulle)
        {
            ToggleButton bouton = _h.Bascule(Icones.Creer(icone, 16, _h.Encre), infobulle);
            bouton.Width = 34;
            bouton.MinWidth = 34;
            bouton.Click += delegate { _toile.DefinirMode(mode); MettreAJourModes(); };
            return bouton;
        }

        private ToggleButton BoutonOnglet(string entete, string icone)
        {
            ToggleButton bouton = _h.BasculeTexte(Icones.Creer(icone, 13, _h.Encre), entete,
                "Contenu de la barre de personnalisation : " + entete.ToLower());
            string nom = entete == "Boîte" ? "boite" : entete.ToLower();
            bouton.Click += delegate { SelectionOnglet(nom); };
            return bouton;
        }

        public void MettreAJourModes()
        {
            if (_btnModeAuto == null) return;
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

        public void SelectionOnglet(string nom)
        {
            _ongletActif = nom;
            if (_ongletTexte != null)
            {
                _ongletTexte.IsChecked = nom == "texte";
                _ongletBoite.IsChecked = nom == "boite";
                _ongletGroupe.IsChecked = nom == "groupe";
                _ongletLien.IsChecked = nom == "lien";
            }
            ScrollViewer defilement = _barreFormat == null ? null : _barreFormat.Child as ScrollViewer;
            if (defilement != null)
                defilement.Content = nom == "boite" ? (Control)_panneauBoites
                    : nom == "groupe" ? _panneauGroupes
                    : nom == "lien" ? _panneauLiens
                    : _panneauTexte;
        }

        // ------------------------------------------------------------------ barre de personnalisation

        /// <summary>La barre de personnalisation : un seul cadre dont le contenu
        /// suit l'onglet choisi (défilement horizontal si elle déborde).</summary>
        public Border ConstruireBarreFormat()
        {
            _panneauTexte = ConstruirePanneauTexte();
            _panneauBoites = ConstruirePanneauBoites();
            _panneauGroupes = ConstruirePanneauGroupes();
            _panneauLiens = ConstruirePanneauLiens();
            _barreFormat = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = _h.Bordure,
                Background = _h.FondBarre,
                MinHeight = 52,
                Child = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = _panneauTexte
                }
            };
            SelectionOnglet(_ongletActif);
            MettreAJourFormat();
            return _barreFormat;
        }

        /// <summary>L'INSPECTEUR VERTICAL (26/10/2026) : les mêmes réglages que la
        /// barre de personnalisation, empilés pour le Général du rail de Marabook.
        /// Seule la section de l'élément sélectionné se montre : Texte pendant
        /// l'édition d'une boîte, Boîte + Texte pour une boîte sélectionnée, Lien,
        /// Groupe. « extrasTexte » : des boutons de l'hôte en tête de la section
        /// Texte (liens vers les fiches, les écrits, les tuiles).</summary>
        public Control ConstruireInspecteur(Control extrasTexte)
        {
            return ConstruireInspecteur(new ExtrasInspecteur { Texte = extrasTexte });
        }

        /// <summary>L'inspecteur avec les boutons de l'hôte (27/10/2026) : en tête
        /// de Texte, sous les réglages de la boîte (note, étiquettes), sous le
        /// compte d'une sélection multiple (grouper), sous celui d'un groupe
        /// (dégrouper, plier). Chaque section est précédée d'un filet et titrée
        /// en demi-gras ; un filet clôt l'inspecteur quand une section se montre
        /// (il le sépare de ce que l'hôte pose dessous).</summary>
        public Control ConstruireInspecteur(ExtrasInspecteur extras)
        {
            _extras = extras ?? new ExtrasInspecteur();
            _panneauTexte = ConstruirePanneauTexte();
            _panneauBoites = ConstruirePanneauBoites();
            _panneauGroupes = ConstruirePanneauGroupes();
            _panneauLiens = ConstruirePanneauLiens();
            _inviteInspecteur = new TextBlock
            {
                Text = "Sélectionnez une boîte, un lien ou un groupe : ses réglages s'affichent ici. Double-clic sur une boîte pour modifier son texte.",
                FontSize = 12,
                Foreground = _h.TexteDoux,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _texteSelection = new TextBlock { FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = _h.Encre, Margin = new Thickness(0, 0, 0, 6) };
            _sectionSelection = new StackPanel { Margin = new Thickness(0, 0, 0, 10), IsVisible = false };
            _sectionSelection.Children.Add(Filet());
            _sectionSelection.Children.Add(_texteSelection);
            if (_extras.Selection != null) _sectionSelection.Children.Add(_extras.Selection);
            if (_extras.Groupe != null) _sectionSelection.Children.Add(_extras.Groupe);
            _sectionBoite = Section("Boîte", _panneauBoites, null, _extras.Boite);
            _sectionTexte = Section("Texte", _panneauTexte, _extras.Texte, null);
            _sectionGroupe = Section("Groupe", _panneauGroupes, null, null);
            _sectionLien = Section("Lien", _panneauLiens, null, null);
            _filetFin = Filet();
            _filetFin.Margin = new Thickness(0, 0, 0, 4);
            StackPanel pile = new StackPanel();
            pile.Children.Add(_inviteInspecteur);
            pile.Children.Add(_sectionSelection);
            pile.Children.Add(_sectionBoite);
            pile.Children.Add(_sectionTexte);
            pile.Children.Add(_sectionGroupe);
            pile.Children.Add(_sectionLien);
            pile.Children.Add(_filetFin);
            MettreAJourFormat();
            return pile;
        }

        /// <summary>Le filet qui sépare les sections : l'encre de l'hôte, éclaircie.</summary>
        private Control Filet()
        {
            return new Border { Height = 1, Background = _h.Encre, Opacity = 0.3, Margin = new Thickness(0, 0, 0, 8), Tag = "filet-inspecteur" };
        }

        private StackPanel Section(string titre, Control contenu, Control extrasAvant, Control extrasApres)
        {
            StackPanel section = new StackPanel { Margin = new Thickness(0, 0, 0, 10), IsVisible = false };
            section.Children.Add(Filet());
            section.Children.Add(new TextBlock { Text = titre, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = _h.Encre, Margin = new Thickness(0, 0, 0, 6) });
            if (extrasAvant != null) section.Children.Add(extrasAvant);
            section.Children.Add(contenu);
            if (extrasApres != null) section.Children.Add(extrasApres);
            return section;
        }

        /// <summary>Quelle section de l'inspecteur se montre : lien, groupe (avec son
        /// compte et ses boutons), boîte (texte seul pendant l'édition, le compte
        /// et « Grouper » devant pour une sélection multiple), ou l'invite.</summary>
        private void MettreAJourSections(FormatCourant etat)
        {
            if (_sectionTexte == null) return;
            bool lien = _toile.LienSelectionne != null;
            Groupe groupeSelectionne = lien ? null : _toile.GroupeSelectionne;
            bool groupe = groupeSelectionne != null;
            bool boite = !lien && !groupe && etat.Actif;
            bool multiple = boite && !_toile.EditionEnCours && _toile.NbSelectionnes > 1;
            _sectionLien.IsVisible = lien;
            _sectionGroupe.IsVisible = groupe;
            _sectionTexte.IsVisible = boite;
            _sectionBoite.IsVisible = boite && !_toile.EditionEnCours;
            _inviteInspecteur.IsVisible = !lien && !groupe && !boite;
            _sectionSelection.IsVisible = multiple || groupe;
            if (multiple)
                _texteSelection.Text = _toile.NbSelectionnes + " éléments sélectionnés";
            else if (groupe)
            {
                int n = groupeSelectionne.NoeudIds.Count;
                _texteSelection.Text = "Groupe de " + n + (n > 1 ? " éléments" : " élément") + (groupeSelectionne.Plie ? " (plié)" : "");
            }
            if (_extras.Selection != null) _extras.Selection.IsVisible = multiple;
            if (_extras.Groupe != null) _extras.Groupe.IsVisible = groupe;
            _filetFin.IsVisible = lien || groupe || boite;
        }

        private ComboBox Combo(double largeur, string infobulle)
        {
            ComboBox combo = new ComboBox { Width = largeur, VerticalAlignment = VerticalAlignment.Center };
            Ui.Infobulle(combo, infobulle);
            return combo;
        }

        /// <summary>Empile une petite étiquette au-dessus d'un contrôle de barre d'outils.</summary>
        private StackPanel AvecEtiquette(string etiquette, Control controle)
        {
            StackPanel pile = new StackPanel { Margin = _vertical ? new Thickness(0, 0, 6, 2) : new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            pile.Children.Add(new TextBlock
            {
                Text = etiquette,
                FontSize = 10,
                Foreground = _h.TexteDoux,
                Margin = new Thickness(2, 0, 0, 1)
            });
            controle.Margin = new Thickness(0);
            pile.Children.Add(controle);
            return pile;
        }

        private static ComboBoxItem Choix(string contenu, object tag)
        {
            return new ComboBoxItem { Content = contenu, Tag = tag };
        }

        /// <summary>Une combo dont la sélection applique une valeur par un délégué (hors synchronisation).</summary>
        private void SurChoix(ComboBox combo, Action<ComboBoxItem> appliquer)
        {
            combo.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
                if (choix != null) appliquer(choix);
            };
        }

        // ---- boîtes

        private Panel ConstruirePanneauBoites()
        {
            Panel panneau = Rangee(NouveauPanneau());

            _comboFond = Combo(124, "Fond de la boîte");
            _comboFond.SelectionChanged += delegate { CouleurBoiteChoisie(_comboFond, "fond"); };
            panneau.Children.Add(AvecEtiquette("Fond", _comboFond));

            _comboBordureCouleur = Combo(124, "Couleur de la bordure");
            _comboBordureCouleur.SelectionChanged += delegate { CouleurBoiteChoisie(_comboBordureCouleur, "bordure-couleur"); };
            panneau.Children.Add(AvecEtiquette("Bordure", _comboBordureCouleur));

            _comboBordureEpaisseur = Combo(64, "Épaisseur de la bordure");
            foreach (double valeur in new double[] { 1, 1.5, 2, 3, 5 }) _comboBordureEpaisseur.Items.Add(Choix(valeur.ToString(), valeur));
            SurChoix(_comboBordureEpaisseur, delegate(ComboBoxItem c) { _toile.AppliquerStyleBoite("bordure-epaisseur", (double)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboBordureEpaisseur));

            _comboBordureTrait = Combo(96, "Trait de la bordure");
            _comboBordureTrait.Items.Add(Choix("Plein", "plein"));
            _comboBordureTrait.Items.Add(Choix("Tirets", "tirets"));
            _comboBordureTrait.Items.Add(Choix("Pointillé", "points"));
            _comboBordureTrait.Items.Add(Choix("Aucune", "aucune"));
            SurChoix(_comboBordureTrait, delegate(ComboBoxItem c) { _toile.AppliquerStyleBoite("bordure-trait", (string)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Trait", _comboBordureTrait));

            _comboCoins = Combo(116, "Coins de la boîte");
            _comboCoins.Items.Add(Choix("Carrés", 0.0));
            _comboCoins.Items.Add(Choix("Arrondis", 5.0));
            _comboCoins.Items.Add(Choix("Très arrondis", 12.0));
            SurChoix(_comboCoins, delegate(ComboBoxItem c) { _toile.AppliquerStyleBoite("rayon", (double)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Coins", _comboCoins));

            _comboOmbre = Combo(94, "Ombre portée de la boîte");
            _comboOmbre.Items.Add(Choix("Aucune", "aucune"));
            _comboOmbre.Items.Add(Choix("Légère", "legere"));
            _comboOmbre.Items.Add(Choix("Marquée", "marquee"));
            SurChoix(_comboOmbre, delegate(ComboBoxItem c) { _toile.AppliquerStyleBoite("ombre", (string)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Ombre", _comboOmbre));

            _comboOpacite = Combo(78, "Opacité de la boîte");
            _comboOpacite.Items.Add(Choix("100 %", 1.0));
            _comboOpacite.Items.Add(Choix("85 %", 0.85));
            _comboOpacite.Items.Add(Choix("70 %", 0.7));
            _comboOpacite.Items.Add(Choix("50 %", 0.5));
            _comboOpacite.Items.Add(Choix("30 %", 0.3));
            SurChoix(_comboOpacite, delegate(ComboBoxItem c) { _toile.AppliquerStyleBoite("opacite", (double)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Opacité", _comboOpacite));

            Button reajuster = _h.Outil(_h.Glyphe("⤢", 14), "Réajuster la boîte à son contenu");
            reajuster.Margin = new Thickness(6, 0, 0, 0);
            reajuster.Click += delegate { _toile.ReajusterBoite(); };
            panneau.Children.Add(reajuster);

            RemplirCombosCouleurs();
            return Racine(panneau);
        }

        /// <summary>Le panneau à poser dans la section : la pile qui porte les rangées
        /// (en colonne), ou la ligne elle-même (en barre).</summary>
        private Panel Racine(Panel rangee)
        {
            return _vertical && rangee.Parent is Panel ? (Panel)rangee.Parent : rangee;
        }

        private void CouleurBoiteChoisie(ComboBox combo, string propriete)
        {
            if (_synchroEnCours) return;
            ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
            if (choix == null) return;
            string tag = choix.Tag as string;
            if (tag == TagPersonnalisee) { _toile.ChoisirCouleurPersonnalisee(propriete); return; }
            if (tag != null) _toile.AppliquerStyleBoite(propriete, tag);
        }

        private void MettreAJourBarreBoites(FormatCourant etat)
        {
            if (_panneauBoites == null) return;
            _panneauBoites.IsEnabled = etat.Actif && _toile.Mode != "lecture";
            if (!etat.Actif) return;
            SelectionnerParTag(_comboFond, etat.FondBoite);
            SelectionnerParTag(_comboBordureCouleur, etat.BordureCouleur);
            SelectionnerParNombre(_comboBordureEpaisseur, etat.BordureEpaisseur);
            SelectionnerParTag(_comboBordureTrait, etat.BordureTrait);
            SelectionnerParTag(_comboOmbre, etat.Ombre);
            SelectionnerParNombre(_comboOpacite, etat.Opacite);
            SelectionnerParNombre(_comboCoins, etat.Rayon);
        }

        private static void SelectionnerParTag(ComboBox combo, string valeur)
        {
            combo.SelectedIndex = -1;
            foreach (object objet in combo.Items)
            {
                ComboBoxItem element = objet as ComboBoxItem;
                if (element != null && element.Tag is string && string.Equals((string)element.Tag, valeur, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = element;
                    return;
                }
            }
        }

        private static void SelectionnerParNombre(ComboBox combo, double valeur)
        {
            combo.SelectedIndex = -1;
            foreach (object objet in combo.Items)
            {
                ComboBoxItem element = objet as ComboBoxItem;
                if (element != null && element.Tag is double && Math.Abs((double)element.Tag - valeur) < 0.01)
                {
                    combo.SelectedItem = element;
                    return;
                }
            }
        }

        // ---- groupes

        private Panel ConstruirePanneauGroupes()
        {
            Panel panneau = Rangee(NouveauPanneau());

            _comboCouleurGroupe = Combo(118, "Couleur du cadre");
            SurChoix(_comboCouleurGroupe, delegate(ComboBoxItem c)
            {
                string tag = c.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurGroupePersonnalisee();
                else if (tag != null) _toile.ChangerCouleurGroupeSelection(tag);
            });
            panneau.Children.Add(AvecEtiquette("Cadre", _comboCouleurGroupe));

            _comboTraitGroupe = Combo(92, "Trait du cadre");
            _comboTraitGroupe.Items.Add(Choix("Tirets", "tirets"));
            _comboTraitGroupe.Items.Add(Choix("Plein", "plein"));
            _comboTraitGroupe.Items.Add(Choix("Pointillé", "points"));
            SurChoix(_comboTraitGroupe, delegate(ComboBoxItem c) { _toile.ChangerTraitGroupeSelection((string)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Trait", _comboTraitGroupe));

            _comboEpaisseurGroupe = Combo(64, "Épaisseur du cadre");
            foreach (double valeur in new double[] { 1, 1.5, 2.5, 4 }) _comboEpaisseurGroupe.Items.Add(Choix(valeur.ToString(), valeur));
            SurChoix(_comboEpaisseurGroupe, delegate(ComboBoxItem c) { _toile.ChangerEpaisseurGroupeSelection((double)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboEpaisseurGroupe));

            _champTitreGroupe = new TextBox { Width = 120, VerticalAlignment = VerticalAlignment.Center };
            Ui.Infobulle(_champTitreGroupe, "Titre du groupe, posé sur le cadre (Entrée pour appliquer)");
            _champTitreGroupe.KeyDown += delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                AppliquerTitreGroupeSaisi();
                e.Handled = true;
            };
            _champTitreGroupe.LostFocus += delegate { AppliquerTitreGroupeSaisi(); };
            panneau.Children.Add(AvecEtiquette("Titre", _champTitreGroupe));

            _comboPositionTitre = Combo(118, "Position du titre sur le cadre");
            _comboPositionTitre.Items.Add(Choix("Haut gauche", "haut-gauche"));
            _comboPositionTitre.Items.Add(Choix("Haut droite", "haut-droite"));
            _comboPositionTitre.Items.Add(Choix("Bas gauche", "bas-gauche"));
            _comboPositionTitre.Items.Add(Choix("Bas droite", "bas-droite"));
            _comboPositionTitre.SelectionChanged += delegate { if (!_synchroEnCours) AppliquerTitreGroupeSaisi(); };
            panneau.Children.Add(AvecEtiquette("Position", _comboPositionTitre));

            _comboCouleurTitre = Combo(118, "Couleur du texte du titre");
            SurChoix(_comboCouleurTitre, delegate(ComboBoxItem c)
            {
                string tag = c.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurTitreGroupePersonnalisee(false);
                else if (tag != null) _toile.ChangerStyleTitreGroupeSelection(tag, null, null);
            });
            panneau.Children.Add(AvecEtiquette("Texte du titre", _comboCouleurTitre));

            _comboFondTitre = Combo(118, "Fond du titre");
            SurChoix(_comboFondTitre, delegate(ComboBoxItem c)
            {
                string tag = c.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurTitreGroupePersonnalisee(true);
                else if (tag != null) _toile.ChangerStyleTitreGroupeSelection(null, tag, null);
            });
            panneau.Children.Add(AvecEtiquette("Fond du titre", _comboFondTitre));

            _comboTailleTitre = Combo(64, "Taille du titre");
            foreach (double valeur in new double[] { 10, 12.5, 14, 16, 20 }) _comboTailleTitre.Items.Add(Choix(valeur.ToString(), valeur));
            SurChoix(_comboTailleTitre, delegate(ComboBoxItem c) { _toile.ChangerStyleTitreGroupeSelection(null, null, (double)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Taille", _comboTailleTitre));

            Button reajuster = _h.Outil(_h.Glyphe("⤢", 14), "Réajuster le cadre à son contenu");
            reajuster.Margin = new Thickness(6, 0, 0, 0);
            reajuster.Click += delegate { _toile.ReajusterGroupeSelection(); };
            panneau.Children.Add(reajuster);

            if (!_vertical)
            {
                // En colonne, l'hôte pose « Dégrouper » lui-même, en tête (ExtrasInspecteur.Groupe).
                Button degrouper = _h.BoutonTexte("Dégrouper", "Dissoudre le groupe (" + ParametresGlobaux.GesteAffiche("degrouper") + ")");
                degrouper.Click += delegate { _toile.DegrouperSelection(); };
                panneau.Children.Add(degrouper);
            }

            RemplirCombosCouleurs();
            return Racine(panneau);
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
            SelectionnerParNombre(_comboEpaisseurGroupe, groupe.BordureEpaisseur);
            _champTitreGroupe.Text = groupe.Titre ?? "";
            SelectionnerParTag(_comboPositionTitre, groupe.TitrePosition);
            SelectionnerParTag(_comboCouleurTitre, groupe.TitreCouleur ?? "");
            SelectionnerParTag(_comboFondTitre, groupe.TitreFond ?? "");
            SelectionnerParNombre(_comboTailleTitre, groupe.TitreTaille);
        }

        // ---- liens

        private Panel ConstruirePanneauLiens()
        {
            Panel panneau = Rangee(NouveauPanneau());

            _comboCouleurLien = Combo(124, "Couleur du lien");
            SurChoix(_comboCouleurLien, delegate(ComboBoxItem c)
            {
                string tag = c.Tag as string;
                if (tag == TagPersonnalisee) _toile.ChoisirCouleurLienPersonnalisee();
                else if (tag != null) _toile.AppliquerStyleLienSelection(tag, null, null, null);
            });
            panneau.Children.Add(AvecEtiquette("Couleur", _comboCouleurLien));

            _comboEpaisseurLien = Combo(64, "Épaisseur du lien");
            foreach (double valeur in new double[] { 1, 2, 3, 5 }) _comboEpaisseurLien.Items.Add(Choix(valeur.ToString(), valeur));
            SurChoix(_comboEpaisseurLien, delegate(ComboBoxItem c) { _toile.AppliquerStyleLienSelection(null, (double)c.Tag, null, null); });
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboEpaisseurLien));

            _comboTraitLien = Combo(96, "Trait du lien");
            _comboTraitLien.Items.Add(Choix("Plein", "plein"));
            _comboTraitLien.Items.Add(Choix("Tirets", "tirets"));
            _comboTraitLien.Items.Add(Choix("Pointillé", "points"));
            SurChoix(_comboTraitLien, delegate(ComboBoxItem c) { _toile.AppliquerStyleLienSelection(null, null, (string)c.Tag, null); });
            panneau.Children.Add(AvecEtiquette("Trait", _comboTraitLien));

            _comboFlecheLien = Combo(136, "Flèches du lien — A est la boîte de départ du lien, B celle d'arrivée");
            _comboFlecheLien.Items.Add(Choix("Sans flèche", "aucune"));
            _comboFlecheLien.Items.Add(Choix("A vers B", "simple"));
            _comboFlecheLien.Items.Add(Choix("B vers A", "inverse"));
            _comboFlecheLien.Items.Add(Choix("Les deux sens", "double"));
            SurChoix(_comboFlecheLien, delegate(ComboBoxItem c) { _toile.AppliquerStyleLienSelection(null, null, null, (string)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Flèches", _comboFlecheLien));

            _comboTraceLien = Combo(100, "Tracé du lien — la courbe se règle à la poignée rose");
            _comboTraceLien.Items.Add(Choix("Droit", "droit"));
            _comboTraceLien.Items.Add(Choix("Courbe", "courbe"));
            _comboTraceLien.Items.Add(Choix("Équerre", "equerre"));
            SurChoix(_comboTraceLien, delegate(ComboBoxItem c) { _toile.AppliquerTraceLienSelection((string)c.Tag); });
            panneau.Children.Add(AvecEtiquette("Tracé", _comboTraceLien));

            _champEtiquetteLien = new TextBox { Width = 150, VerticalAlignment = VerticalAlignment.Center };
            Ui.Infobulle(_champEtiquetteLien, "Étiquette posée au milieu du trait (Entrée pour appliquer, vide pour retirer)");
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
            return Racine(panneau);
        }

        private void MettreAJourBarreLiens()
        {
            if (_panneauLiens == null) return;
            Lien lien = _toile.LienSelectionne;
            _panneauLiens.IsEnabled = lien != null;
            if (lien == null) return;
            SelectionnerParTag(_comboCouleurLien, lien.Couleur);
            SelectionnerParNombre(_comboEpaisseurLien, lien.Epaisseur);
            SelectionnerParTag(_comboTraitLien, lien.Trait);
            SelectionnerParTag(_comboFlecheLien, lien.Fleche);
            SelectionnerParTag(_comboTraceLien, lien.Trace);
            _champEtiquetteLien.Text = lien.Etiquette ?? "";
        }

        // ---- texte

        private Panel ConstruirePanneauTexte()
        {
            Panel pile = NouveauPanneau();
            Panel panneau = Rangee(pile);

            // Le sélecteur de police : celui de l'hôte s'il en fournit un (1/11, Rémi :
            // l'autocomplète de Marabook, le même que dans son éditeur), sinon la liste.
            if (FabriqueSelecteurPolice != null) _selecteurPolice = FabriqueSelecteurPolice();
            if (_selecteurPolice != null)
            {
                _selecteurPolice.Choisie += delegate(string police)
                {
                    if (_synchroEnCours || string.IsNullOrEmpty(police)) return;
                    _toile.AppliquerFormatTexte("police", police);
                };
                panneau.Children.Add(AvecEtiquette("Police", _selecteurPolice.Controle));
            }
            else
            {
                _comboPolice = Combo(180, "Police — toutes les polices installées");
                foreach (string police in TexteRiche.PolicesSysteme())
                    _comboPolice.Items.Add(new ComboBoxItem { Content = police, Tag = police, FontFamily = TexteRiche.Police(police) });
                SurChoix(_comboPolice, delegate(ComboBoxItem c) { _toile.AppliquerFormatTexte("police", (string)c.Tag); });
                panneau.Children.Add(AvecEtiquette("Police", _comboPolice));
            }

            _comboTaille = Combo(66, "Taille du texte (« Autre… » pour une valeur libre, 6 à 120)");
            foreach (double taille in Palette.Tailles) _comboTaille.Items.Add(Choix(taille.ToString(), taille));
            _comboTaille.Items.Add(Choix("Autre…", "autre"));
            SurChoix(_comboTaille, delegate(ComboBoxItem c)
            {
                if (c.Tag is double) _toile.AppliquerFormatTexte("taille", (double)c.Tag);
                else _toile.DemanderTaillePersonnalisee();
            });
            panneau.Children.Add(AvecEtiquette("Taille", _comboTaille));

            AjouterSeparateur(panneau);

            // Gras, italique, souligné, barré : leur propre ligne en colonne (27/10).
            panneau = Rangee(pile);
            _btnGras = BoutonFormat("text-b-bold", "Gras (" + ParametresGlobaux.GesteAffiche("gras") + ")", "gras", null);
            _btnItalique = BoutonFormat("text-italic", "Italique (" + ParametresGlobaux.GesteAffiche("italique") + ")", "italique", null);
            _btnSouligne = BoutonFormat("text-underline", "Souligné (" + ParametresGlobaux.GesteAffiche("souligne") + ")", "souligne", null);
            _btnBarre = BoutonFormat("text-strikethrough", "Barré", "barre", null);
            panneau.Children.Add(_btnGras);
            panneau.Children.Add(_btnItalique);
            panneau.Children.Add(_btnSouligne);
            panneau.Children.Add(_btnBarre);

            AjouterSeparateur(panneau);

            panneau = Rangee(pile);
            _comboCouleur = Combo(126, "Couleur du texte");
            _comboCouleur.SelectionChanged += delegate { CouleurChoisie(_comboCouleur, "couleur"); };
            panneau.Children.Add(AvecEtiquette("Couleur", _comboCouleur));

            _comboSurlignage = Combo(126, "Surlignage");
            _comboSurlignage.SelectionChanged += delegate { CouleurChoisie(_comboSurlignage, "surlignage"); };
            panneau.Children.Add(AvecEtiquette("Surlignage", _comboSurlignage));

            RemplirCombosCouleurs();

            AjouterSeparateur(panneau);

            // Les alignements sur leur ligne…
            panneau = Rangee(pile);
            _btnAlignGauche = BoutonFormat("text-align-left", "Aligner à gauche", "alignement", "gauche");
            _btnAlignCentre = BoutonFormat("text-align-center", "Centrer", "alignement", "centre");
            _btnAlignDroite = BoutonFormat("text-align-right", "Aligner à droite", "alignement", "droite");
            panneau.Children.Add(_btnAlignGauche);
            panneau.Children.Add(_btnAlignCentre);
            panneau.Children.Add(_btnAlignDroite);

            AjouterSeparateur(panneau);
            // …et les listes sur la leur, avec les icônes de l'éditeur de texte de
            // Marabook (la liste à tirets n'a plus de bouton — « - » puis espace la crée encore).
            panneau = Rangee(pile);
            panneau.Children.Add(BoutonPuce("list-bullets", "Liste à puces", "puce"));
            panneau.Children.Add(BoutonPuce("list-numbers-bold", "Liste numérotée", "numero"));
            panneau.Children.Add(BoutonPuce("list-check", "Cases à cocher (cliquer une case dans la boîte pour la cocher)", "case"));

            return pile;
        }

        private Button BoutonPuce(string icone, string infobulle, string type)
        {
            Button bouton = _h.Outil(Icones.Creer(icone, 14, _h.Encre), infobulle + " — boîte entière, ou lignes sélectionnées en édition ; re-cliquer pour retirer");
            bouton.Click += delegate { _toile.BasculerPuces(type); };
            return bouton;
        }

        private ToggleButton BoutonFormat(string icone, string infobulle, string propriete, object valeur)
        {
            ToggleButton bouton = _h.Bascule(Icones.Creer(icone, 14, _h.Encre), infobulle);
            bouton.Click += delegate
            {
                if (_synchroEnCours) return;
                _toile.AppliquerFormatTexte(propriete, valeur);
            };
            return bouton;
        }

        private void CouleurChoisie(ComboBox combo, string propriete)
        {
            if (_synchroEnCours) return;
            ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
            if (choix == null) return;
            string tag = choix.Tag as string;
            if (tag == TagPersonnalisee) { _toile.ChoisirCouleurPersonnalisee(propriete); return; }
            // Tag "" = « Aucun » (surlignage uniquement).
            _toile.AppliquerFormatTexte(propriete, string.IsNullOrEmpty(tag) ? null : tag);
        }

        // ---- couleurs

        public void RemplirCombosCouleurs()
        {
            _documentCouleurs = _toile.Document;
            _nbCouleursPerso = _toile.Document.CouleursPersonnalisees.Count;
            if (_comboCouleur != null) RemplirComboCouleurs(_comboCouleur, Palette.Couleurs, false);
            if (_comboSurlignage != null) RemplirComboCouleurs(_comboSurlignage, Palette.Surlignages, true);
            if (_comboFond != null) RemplirComboFond();
            if (_comboBordureCouleur != null) RemplirComboBordure();
            if (_comboCouleurGroupe != null) RemplirComboCouleurs(_comboCouleurGroupe, Palette.Couleurs, false);
            if (_comboCouleurLien != null) RemplirComboCouleurs(_comboCouleurLien, Palette.Couleurs, false);
            if (_comboCouleurTitre != null) RemplirComboTitre(_comboCouleurTitre, "Couleur du cadre");
            if (_comboFondTitre != null) RemplirComboTitre(_comboFondTitre, "Fond du tableau");
        }

        private void Remplir(ComboBox combo, Action<ComboBox> garnir)
        {
            bool synchroPrecedente = _synchroEnCours;
            _synchroEnCours = true;
            try
            {
                combo.SelectedIndex = -1; // vider la sélection AVANT les éléments (le modèle de sélection énumère pendant Clear)
                combo.Items.Clear();
                garnir(combo);
                foreach (string hexa in _toile.Document.CouleursPersonnalisees) combo.Items.Add(ElementCouleur(hexa, hexa));
                combo.Items.Add(Choix("Personnalisée…", TagPersonnalisee));
            }
            finally { _synchroEnCours = synchroPrecedente; }
        }

        private void RemplirComboFond()
        {
            Remplir(_comboFond, delegate(ComboBox combo)
            {
                combo.Items.Add(ElementCouleur("Blanc", "#FFFFFF"));
                combo.Items.Add(ElementCouleur("Transparent", "Transparent"));
                for (int i = 0; i < Palette.Surlignages.GetLength(0); i++) combo.Items.Add(ElementCouleur(Palette.Surlignages[i, 0], Palette.Surlignages[i, 1]));
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++) combo.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
            });
        }

        private void RemplirComboBordure()
        {
            Remplir(_comboBordureCouleur, delegate(ComboBox combo)
            {
                combo.Items.Add(ElementCouleur("Gris sable", "#B8B2A7"));
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++) combo.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
            });
        }

        private void RemplirComboCouleurs(ComboBox cible, string[,] palette, bool avecAucun)
        {
            Remplir(cible, delegate(ComboBox combo)
            {
                if (avecAucun) combo.Items.Add(Choix("Aucun", ""));
                for (int i = 0; i < palette.GetLength(0); i++) combo.Items.Add(ElementCouleur(palette[i, 0], palette[i, 1]));
            });
        }

        /// <summary>Combo de couleur de titre de groupe : « hériter » ("") + palette + perso.</summary>
        private void RemplirComboTitre(ComboBox cible, string nomHeritage)
        {
            Remplir(cible, delegate(ComboBox combo)
            {
                combo.Items.Add(Choix(nomHeritage, ""));
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++) combo.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
                for (int i = 0; i < Palette.Surlignages.GetLength(0); i++) combo.Items.Add(ElementCouleur(Palette.Surlignages[i, 0], Palette.Surlignages[i, 1]));
            });
        }

        private static ComboBoxItem ElementCouleur(string nom, string hexa)
        {
            return DialoguePreferencesFichier.ElementCouleur(nom, hexa);
        }

        // ---- synchronisation

        /// <summary>La barre suit la sélection : onglet automatique (sauf épinglé),
        /// combos et bascules posées sur l'état courant, panneaux grisés sans cible.</summary>
        public void MettreAJourFormat()
        {
            if (_panneauTexte == null) return;
            // Pas de rentrée (1/11/2026) : poser l'index d'une combo dont la liste est
            // déroulée déplace le clavier, l'éditeur efface sa sélection, la toile
            // resignale… et un second passage remettait _synchroEnCours à faux sous
            // les pieds du premier — les combos appliquaient alors leur propre
            // synchronisation comme un choix (débordement de pile dans Marabook).
            if (_synchroEnCours) return;
            if (_toile.Document != _documentCouleurs || _toile.Document.CouleursPersonnalisees.Count != _nbCouleursPerso)
                RemplirCombosCouleurs();

            FormatCourant etat = _toile.LireFormatCourant();
            _synchroEnCours = true;
            try
            {
                bool epingle = _epingleOnglet != null && _epingleOnglet.IsChecked == true;
                if (!epingle)
                {
                    if (_toile.LienSelectionne != null) SelectionOnglet("lien");
                    else if (_toile.GroupeSelectionne != null) SelectionOnglet("groupe");
                    else if (etat.Actif && (_ongletActif == "groupe" || _ongletActif == "lien")) SelectionOnglet("texte");
                }
                bool lecture = _toile.Mode == "lecture";
                _panneauTexte.IsEnabled = etat.Actif && !lecture;
                MettreAJourBarreBoites(etat);
                MettreAJourBarreGroupes();
                MettreAJourBarreLiens();
                MettreAJourSections(etat);

                if (_selecteurPolice != null) _selecteurPolice.Poser(etat.Police); else SelectionnerParTag(_comboPolice, etat.Police);
                SelectionnerParNombre(_comboTaille, etat.Taille);
                _btnGras.IsChecked = etat.Gras;
                _btnItalique.IsChecked = etat.Italique;
                _btnSouligne.IsChecked = etat.Souligne;
                _btnBarre.IsChecked = etat.Barre;
                SelectionnerParTag(_comboCouleur, etat.Couleur);
                SelectionnerParTag(_comboSurlignage, etat.Surlignage ?? "");
                _btnAlignGauche.IsChecked = etat.Alignement == "gauche";
                _btnAlignCentre.IsChecked = etat.Alignement == "centre";
                _btnAlignDroite.IsChecked = etat.Alignement == "droite";
            }
            finally { _synchroEnCours = false; }
            Action handler = Synchronisee;
            if (handler != null) handler();
        }
    }

    /// <summary>Les boutons que l'hôte glisse dans l'inspecteur vertical (27/10/2026) ;
    /// chacun est facultatif. Selection et Groupe sont montrés / masqués par
    /// l'inspecteur avec le compte qui les surmonte.</summary>
    public class ExtrasInspecteur
    {
        /// <summary>En tête de la section Texte (liens vers l'hôte, tuiles…).</summary>
        public Control Texte;
        /// <summary>Sous les réglages de la boîte (note, étiquettes…).</summary>
        public Control Boite;
        /// <summary>Sous « n éléments sélectionnés » (grouper).</summary>
        public Control Selection;
        /// <summary>Sous « Groupe de n éléments » (dégrouper, plier / déplier).</summary>
        public Control Groupe;
    }
}
