using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Vue;

namespace Mentalo
{
    /// <summary>Les barres de la fenêtre : modes, personnalisation (Texte / Boîte / Groupe /
    /// Lien), recherche, état, vignette du tableau et bouton de thème.</summary>
    public partial class FenetrePrincipale
    {
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
                Content = Glyphe("⚲", 13),
                Width = 28,
                MinWidth = 28,
                Padding = new Thickness(0, 2),
                Margin = new Thickness(2, 0, 0, 0),
                Focusable = false
            };
            Ui.Infobulle(_epingleOnglet, "Épingler l'onglet actuel : la sélection ne le change plus automatiquement");
            panneau.Children.Add(_epingleOnglet);

            _texteMode = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Foreground = Chrome.TexteDoux,
                FontStyle = FontStyle.Italic
            };
            panneau.Children.Add(_texteMode);

            // Le bouton de thème vit à l'opposé de la barre, ancré tout à droite.
            _boutonTheme = new Button
            {
                Width = 32,
                MinWidth = 32,
                Height = 26,
                Padding = new Thickness(0),
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
            Ui.Infobulle(bouton, infobulle);
            bouton.Width = 34;
            bouton.MinWidth = 34;
            bouton.Padding = new Thickness(0, 3);
            bouton.Margin = new Thickness(2, 0, 0, 0);
            bouton.Focusable = false;
            bouton.Click += delegate { _toile.DefinirMode(mode); MettreAJourBarreModes(); };
            return bouton;
        }

        private ToggleButton BoutonOnglet(string entete, string icone)
        {
            StackPanel contenu = new StackPanel { Orientation = Orientation.Horizontal };
            Control image = Icones.Creer(icone, 13);
            image.Margin = new Thickness(0, 0, 5, 0);
            contenu.Children.Add(image);
            contenu.Children.Add(new TextBlock { Text = entete, VerticalAlignment = VerticalAlignment.Center });

            ToggleButton bouton = new ToggleButton();
            bouton.Content = contenu;
            Ui.Infobulle(bouton, "Contenu de la barre de personnalisation : " + entete.ToLower());
            bouton.Padding = new Thickness(8, 2);
            bouton.Margin = new Thickness(2, 0, 0, 0);
            bouton.Focusable = false;
            string nom = entete == "Boîte" ? "boite" : entete.ToLower();
            bouton.Click += delegate { SelectionOnglet(nom); };
            return bouton;
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

        // ------------------------------------------------------------------ recherche

        private Border ConstruireBarreRecherche()
        {
            StackPanel contenu = new StackPanel { Orientation = Orientation.Horizontal };
            _champRecherche = new TextBox
            {
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center
            };
            Ui.Infobulle(_champRecherche, "Entrée : occurrence suivante · Maj+Entrée : précédente · Échap : fermer");
            _champRecherche.PropertyChanged += delegate(object expediteur, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == TextBox.TextProperty) ActualiserRecherche(true);
            };
            _champRecherche.AddHandler(KeyDownEvent, delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    NaviguerRecherche((e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    FermerRecherche();
                    e.Handled = true;
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
            Button precedent = new Button { Content = "‹", Width = 26, MinWidth = 26, Padding = new Thickness(0, 2), Focusable = false };
            Ui.Infobulle(precedent, "Occurrence précédente (Maj+Entrée)");
            precedent.Click += delegate { NaviguerRecherche(-1); };
            contenu.Children.Add(precedent);
            Button suivant = new Button { Content = "›", Width = 26, MinWidth = 26, Padding = new Thickness(0, 2), Margin = new Thickness(3, 0, 0, 0), Focusable = false };
            Ui.Infobulle(suivant, "Occurrence suivante (Entrée)");
            suivant.Click += delegate { NaviguerRecherche(1); };
            contenu.Children.Add(suivant);
            Button fermer = new Button { Content = "✕", Width = 26, MinWidth = 26, Padding = new Thickness(0, 2), Margin = new Thickness(8, 0, 0, 0), Focusable = false };
            Ui.Infobulle(fermer, "Fermer (Échap)");
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
                IsVisible = false,
                Child = contenu
            };
        }

        private void OuvrirRecherche()
        {
            _barreRecherche.IsVisible = true;
            _champRecherche.Focus();
            _champRecherche.SelectAll();
            ActualiserRecherche(false);
        }

        private void FermerRecherche()
        {
            _barreRecherche.IsVisible = false;
            _toile.Focus();
        }

        private void ActualiserRecherche(bool aller)
        {
            string texte = _champRecherche.Text ?? "";
            _resultatsRecherche = _toile.Rechercher(texte);
            _indiceRecherche = 0;
            _compteurRecherche.Text = _resultatsRecherche.Count == 0
                ? (texte.Trim().Length == 0 ? "" : "0")
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

        private void MettreAJourVignette()
        {
            if (_vignette != null)
                _vignette.IsVisible = ParametresGlobaux.VignetteTableau;
        }

        private void BasculerTheme()
        {
            ParametresGlobaux.ThemeSombre = !ParametresGlobaux.ThemeSombre;
            ParametresGlobaux.Enregistrer();
            Mentalo.Theme.Appliquer(ParametresGlobaux.ThemeSombre);
            _toile.AppliquerFondDocument();
            MettreAJourIconeTheme();
            MettreAJourMiniCarte();
        }

        private void MettreAJourIconeTheme()
        {
            _boutonTheme.Content = Glyphe(ParametresGlobaux.ThemeSombre ? "☀" : "☾", 14);
            Ui.Infobulle(_boutonTheme, ParametresGlobaux.ThemeSombre
                ? "Repasser au thème clair"
                : "Passer au thème sombre");
        }

        // ------------------------------------------------------------------ panneau des boîtes

        private ComboBox Combo(double largeur, string infobulle)
        {
            ComboBox combo = new ComboBox { Width = largeur, VerticalAlignment = VerticalAlignment.Center };
            Ui.Infobulle(combo, infobulle);
            return combo;
        }

        private StackPanel ConstruirePanneauBoites()
        {
            StackPanel panneau = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };

            _comboFond = Combo(124, "Fond de la boîte");
            _comboFond.SelectionChanged += delegate { CouleurBoiteChoisie(_comboFond, "fond"); };
            panneau.Children.Add(AvecEtiquette("Fond", _comboFond));

            _comboBordureCouleur = Combo(124, "Couleur de la bordure");
            _comboBordureCouleur.SelectionChanged += delegate { CouleurBoiteChoisie(_comboBordureCouleur, "bordure-couleur"); };
            panneau.Children.Add(AvecEtiquette("Bordure", _comboBordureCouleur));

            _comboBordureEpaisseur = Combo(64, "Épaisseur de la bordure");
            foreach (double valeur in new double[] { 1, 1.5, 2, 3, 5 })
                _comboBordureEpaisseur.Items.Add(new ComboBoxItem { Content = valeur.ToString(), Tag = valeur });
            _comboBordureEpaisseur.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboBordureEpaisseur.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleBoite("bordure-epaisseur", (double)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboBordureEpaisseur));

            _comboBordureTrait = Combo(96, "Trait de la bordure");
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

            _comboCoins = Combo(116, "Coins de la boîte");
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

            _comboOmbre = Combo(94, "Ombre portée de la boîte");
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

            _comboOpacite = Combo(78, "Opacité de la boîte");
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

            Button reajuster = BoutonOutil("⤢", "Réajuster la boîte à son contenu");
            reajuster.Click += delegate { _toile.ReajusterBoite(); };
            panneau.Children.Add(reajuster);

            RemplirCombosCouleurs();
            return panneau;
        }

        private Button BoutonOutil(string symbole, string infobulle)
        {
            Button bouton = new Button
            {
                Content = Glyphe(symbole, 14),
                Width = 30,
                MinWidth = 30,
                Height = 26,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Focusable = false
            };
            Ui.Infobulle(bouton, infobulle);
            return bouton;
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

        // ------------------------------------------------------------------ panneau des groupes

        private StackPanel ConstruirePanneauGroupes()
        {
            StackPanel panneau = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };

            _comboCouleurGroupe = Combo(118, "Couleur du cadre");
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

            _comboTraitGroupe = Combo(92, "Trait du cadre");
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

            _comboEpaisseurGroupe = Combo(64, "Épaisseur du cadre");
            foreach (double valeur in new double[] { 1, 1.5, 2.5, 4 })
                _comboEpaisseurGroupe.Items.Add(new ComboBoxItem { Content = valeur.ToString(), Tag = valeur });
            _comboEpaisseurGroupe.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboEpaisseurGroupe.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.ChangerEpaisseurGroupeSelection((double)choix.Tag);
            };
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

            _comboCouleurTitre = Combo(118, "Couleur du texte du titre");
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

            _comboFondTitre = Combo(118, "Fond du titre");
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

            _comboTailleTitre = Combo(64, "Taille du titre");
            foreach (double valeur in new double[] { 10, 12.5, 14, 16, 20 })
                _comboTailleTitre.Items.Add(new ComboBoxItem { Content = valeur.ToString(), Tag = valeur });
            _comboTailleTitre.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboTailleTitre.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.ChangerStyleTitreGroupeSelection(null, null, (double)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Taille", _comboTailleTitre));

            Button reajuster = BoutonOutil("⤢", "Réajuster le cadre à son contenu");
            reajuster.Click += delegate { _toile.ReajusterGroupeSelection(); };
            panneau.Children.Add(reajuster);

            Button degrouper = new Button
            {
                Content = "Dégrouper",
                Padding = new Thickness(8, 2),
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Focusable = false
            };
            Ui.Infobulle(degrouper, "Dissoudre le groupe (" + ParametresGlobaux.GesteAffiche("degrouper") + ")");
            degrouper.Click += delegate { _toile.DegrouperSelection(); };
            panneau.Children.Add(degrouper);

            RemplirCombosCouleurs();
            return panneau;
        }

        // ------------------------------------------------------------------ panneau des liens

        private StackPanel ConstruirePanneauLiens()
        {
            StackPanel panneau = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };

            _comboCouleurLien = Combo(124, "Couleur du lien");
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

            _comboEpaisseurLien = Combo(64, "Épaisseur du lien");
            foreach (double valeur in new double[] { 1, 2, 3, 5 })
                _comboEpaisseurLien.Items.Add(new ComboBoxItem { Content = valeur.ToString(), Tag = valeur });
            _comboEpaisseurLien.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboEpaisseurLien.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerStyleLienSelection(null, (double)choix.Tag, null, null);
            };
            panneau.Children.Add(AvecEtiquette("Épaisseur", _comboEpaisseurLien));

            _comboTraitLien = Combo(96, "Trait du lien");
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

            _comboFlecheLien = Combo(136, "Flèches du lien");
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

            _comboTraceLien = Combo(100, "Tracé du lien — la courbe se règle à la poignée rose");
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
            return panneau;
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

        // ------------------------------------------------------------------ panneau texte

        private StackPanel ConstruirePanneauTexte()
        {
            StackPanel panneau = new StackPanel();
            panneau.Orientation = Orientation.Horizontal;
            panneau.Margin = new Thickness(6, 4, 6, 4);

            _comboPolice = Combo(180, "Police — toutes les polices installées");
            foreach (string police in TexteRiche.PolicesSysteme())
                _comboPolice.Items.Add(new ComboBoxItem { Content = police, Tag = police, FontFamily = TexteRiche.Police(police) });
            _comboPolice.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboPolice.SelectedItem as ComboBoxItem;
                if (choix != null) _toile.AppliquerFormatTexte("police", (string)choix.Tag);
            };
            panneau.Children.Add(AvecEtiquette("Police", _comboPolice));

            _comboTaille = Combo(66, "Taille du texte (« Autre… » pour une valeur libre, 6 à 120)");
            foreach (double taille in Palette.Tailles)
                _comboTaille.Items.Add(new ComboBoxItem { Content = taille.ToString(), Tag = taille });
            _comboTaille.Items.Add(new ComboBoxItem { Content = "Autre…", Tag = "autre" });
            _comboTaille.SelectionChanged += delegate
            {
                if (_synchroEnCours) return;
                ComboBoxItem choix = _comboTaille.SelectedItem as ComboBoxItem;
                if (choix == null) return;
                if (choix.Tag is double) _toile.AppliquerFormatTexte("taille", (double)choix.Tag);
                else _toile.DemanderTaillePersonnalisee();
            };
            panneau.Children.Add(AvecEtiquette("Taille", _comboTaille));

            panneau.Children.Add(Separateur());

            _btnGras = BoutonBascule(Icones.Creer("text-b-bold", 14), "Gras (" + ParametresGlobaux.GesteAffiche("gras") + ")", "gras");
            _btnItalique = BoutonBascule(Icones.Creer("text-italic", 14), "Italique (" + ParametresGlobaux.GesteAffiche("italique") + ")", "italique");
            _btnSouligne = BoutonBascule(Icones.Creer("text-underline", 14), "Souligné (" + ParametresGlobaux.GesteAffiche("souligne") + ")", "souligne");
            _btnBarre = BoutonBascule(Icones.Creer("text-strikethrough", 14), "Barré", "barre");
            panneau.Children.Add(_btnGras);
            panneau.Children.Add(_btnItalique);
            panneau.Children.Add(_btnSouligne);
            panneau.Children.Add(_btnBarre);

            panneau.Children.Add(Separateur());

            _comboCouleur = Combo(126, "Couleur du texte");
            _comboCouleur.SelectionChanged += delegate { CouleurChoisie(_comboCouleur, "couleur"); };
            panneau.Children.Add(AvecEtiquette("Couleur", _comboCouleur));

            _comboSurlignage = Combo(126, "Surlignage");
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
                MinWidth = 30,
                Height = 26,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                Focusable = false
            };
            Ui.Infobulle(bouton, infobulle + " — boîte entière, ou lignes sélectionnées en édition ; re-cliquer pour retirer");
            bouton.Click += delegate { _toile.BasculerPuces(type); };
            return bouton;
        }

        private ToggleButton BoutonBascule(Control contenu, string infobulle, string propriete)
        {
            ToggleButton bouton = new ToggleButton();
            bouton.Content = contenu;
            Ui.Infobulle(bouton, infobulle);
            bouton.Width = 30;
            bouton.MinWidth = 30;
            bouton.Height = 26;
            bouton.Padding = new Thickness(0);
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
            Ui.Infobulle(bouton, infobulle);
            bouton.Width = 30;
            bouton.MinWidth = 30;
            bouton.Height = 26;
            bouton.Padding = new Thickness(0);
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
            if (_comboCouleur != null) RemplirComboCouleurs(_comboCouleur, Palette.Couleurs, false);
            if (_comboSurlignage != null) RemplirComboCouleurs(_comboSurlignage, Palette.Surlignages, true);
            if (_comboFond != null) RemplirComboFond();
            if (_comboBordureCouleur != null) RemplirComboBordure();
            if (_comboCouleurGroupe != null) RemplirComboCouleurs(_comboCouleurGroupe, Palette.Couleurs, false);
            if (_comboCouleurLien != null) RemplirComboCouleurs(_comboCouleurLien, Palette.Couleurs, false);
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
                for (int i = 0; i < Palette.Surlignages.GetLength(0); i++)
                    _comboFond.Items.Add(ElementCouleur(Palette.Surlignages[i, 0], Palette.Surlignages[i, 1]));
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                    _comboFond.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
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
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                    _comboBordureCouleur.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
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
                    combo.Items.Add(new ComboBoxItem { Content = "Aucun", Tag = "" });
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
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                    combo.Items.Add(ElementCouleur(Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]));
                for (int i = 0; i < Palette.Surlignages.GetLength(0); i++)
                    combo.Items.Add(ElementCouleur(Palette.Surlignages[i, 0], Palette.Surlignages[i, 1]));
                foreach (string hexa in _toile.Document.CouleursPersonnalisees)
                    combo.Items.Add(ElementCouleur(hexa, hexa));
                combo.Items.Add(new ComboBoxItem { Content = "Personnalisée…", Tag = TagPersonnalisee });
            }
            finally { _synchroEnCours = synchroPrecedente; }
        }

        private static ComboBoxItem ElementCouleur(string nom, string hexa)
        {
            return DialoguePreferencesFichier.ElementCouleur(nom, hexa);
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
            // Tag "" = « Aucun » (surlignage uniquement).
            _toile.AppliquerFormatTexte(propriete, string.IsNullOrEmpty(tag) ? null : tag);
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

                SelectionnerParTag(_comboPolice, etat.Police);
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
            // À gauche, la mise à jour disponible (vide sinon) — cliquable
            _texteMaj.Margin = new Thickness(8, 3, 8, 3);
            _texteMaj.Foreground = Chrome.Or;
            _texteMaj.Cursor = Ui.CurseurMain;
            _texteMaj.IsVisible = false;
            _texteMaj.PointerReleased += delegate { VerifierMisesAJour(); };
            DockPanel.SetDock(_texteMaj, Dock.Left);
            contenu.Children.Add(_texteMaj);

            Border bordure = new Border();
            bordure.BorderThickness = new Thickness(0, 1, 0, 0);
            bordure.BorderBrush = Chrome.Bordure;
            bordure.Background = Chrome.FondBarre;
            bordure.Child = contenu;
            return bordure;
        }
    }
}
