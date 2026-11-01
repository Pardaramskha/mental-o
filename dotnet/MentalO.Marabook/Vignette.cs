using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Marabook.App;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;
using Chrome = global::Marabook.App.Chrome;

namespace MentalO.Marabook
{
    /// <summary>LA VIGNETTE D'UNE TUILE (26/10/2026) : la carte en schéma — chaque
    /// boîte un rectangle à sa place et à sa couleur, chaque lien un trait —
    /// ramenée aux dimensions de la tuile. Lue depuis les octets .tea, sans
    /// toile ni rendu : cent tuiles ne coûtent rien. Mémorisée par tableau
    /// d'octets (ils changent à l'enregistrement de la carte). Elle suit le
    /// thème de Marabook comme la toile (27/10) : une carte aux couleurs
    /// d'usine se dessine en sombre sous le thème sombre, boîtes comprises ;
    /// une carte à son propre fond (thème de carte) garde ses couleurs.</summary>
    public static class Vignette
    {
        private class Boite { public double X, Y, W, H; public string Fond; }

        private class Sketch
        {
            public readonly List<Boite> Boites = new List<Boite>();
            public readonly List<KeyValuePair<int, int>> Liens = new List<KeyValuePair<int, int>>();
            public string Fond;
        }

        private static readonly Dictionary<byte[], Sketch> _cache = new Dictionary<byte[], Sketch>();

        public static Control Dessiner(byte[] tea, double width, double height)
        {
            Sketch sketch = Lire(tea);
            if (sketch == null || sketch.Boites.Count == 0) return null;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (Boite boite in sketch.Boites)
            {
                minX = Math.Min(minX, boite.X); minY = Math.Min(minY, boite.Y);
                maxX = Math.Max(maxX, boite.X + boite.W); maxY = Math.Max(maxY, boite.Y + boite.H);
            }
            double pad = 6.0;
            double scale = Math.Min((width - 2 * pad) / Math.Max(1, maxX - minX), (height - 2 * pad) / Math.Max(1, maxY - minY));
            if (scale > 0.6) scale = 0.6;
            double offsetX = pad + ((width - 2 * pad) - (maxX - minX) * scale) / 2;
            double offsetY = pad + ((height - 2 * pad) - (maxY - minY) * scale) / 2;
            // Le fond d'usine (crème) devient bleu nuit sous le thème sombre, comme
            // sur la toile (ToileVue.CouleurFondEffective) ; les boîtes blanches suivent.
            bool sombre = ParametresGlobaux.ThemeSombre
                && (string.IsNullOrEmpty(sketch.Fond) || string.Equals(sketch.Fond, "#F7F5F0", StringComparison.OrdinalIgnoreCase));
            Color fond = sombre ? Color.FromRgb(0x16, 0x20, 0x2E) : Palette.CouleurDepuis(sketch.Fond, Color.FromRgb(0xF7, 0xF5, 0xF0));
            Canvas canvas = new Canvas
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(fond),
                ClipToBounds = true
            };
            SolidColorBrush link = new SolidColorBrush(sombre ? Color.FromArgb(0xA0, 0x9A, 0xA8, 0xC0) : Color.FromArgb(0x90, 0x60, 0x66, 0x78));
            foreach (KeyValuePair<int, int> lien in sketch.Liens)
            {
                Boite a = sketch.Boites[lien.Key];
                Boite b = sketch.Boites[lien.Value];
                canvas.Children.Add(new Line
                {
                    StartPoint = new Point(offsetX + (a.X - minX + a.W / 2) * scale, offsetY + (a.Y - minY + a.H / 2) * scale),
                    EndPoint = new Point(offsetX + (b.X - minX + b.W / 2) * scale, offsetY + (b.Y - minY + b.H / 2) * scale),
                    Stroke = link, StrokeThickness = 1
                });
            }
            SolidColorBrush border = new SolidColorBrush(sombre ? Color.FromArgb(0xC0, 0x6A, 0x78, 0x90) : Color.FromArgb(0xA0, 0x40, 0x44, 0x50));
            foreach (Boite boite in sketch.Boites)
            {
                bool blanche = string.IsNullOrEmpty(boite.Fond) || string.Equals(boite.Fond, "#FFFFFF", StringComparison.OrdinalIgnoreCase);
                Color face = sombre && blanche ? Color.FromRgb(0x22, 0x30, 0x3F) : Palette.CouleurDepuis(boite.Fond, Colors.White);
                Rectangle rect = new Rectangle
                {
                    Width = Math.Max(3, boite.W * scale),
                    Height = Math.Max(3, boite.H * scale),
                    RadiusX = 2, RadiusY = 2,
                    Fill = new SolidColorBrush(face),
                    Stroke = border,
                    StrokeThickness = 0.8
                };
                Canvas.SetLeft(rect, offsetX + (boite.X - minX) * scale);
                Canvas.SetTop(rect, offsetY + (boite.Y - minY) * scale);
                canvas.Children.Add(rect);
            }
            return new Border
            {
                BorderBrush = Chrome.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Width = width,
                Height = height,
                Child = canvas,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private static Sketch Lire(byte[] tea)
        {
            if (tea == null || tea.Length == 0) return null;
            Sketch known;
            if (_cache.TryGetValue(tea, out known)) return known;
            Sketch sketch = null;
            try
            {
                DocumentCarte document = Fichier.Charger(tea);
                sketch = new Sketch { Fond = document.FondCouleur };
                Dictionary<string, int> index = new Dictionary<string, int>();
                foreach (Noeud noeud in document.Noeuds)
                {
                    string text = noeud.TextePlat.Replace("\r", "");
                    double size = document.TailleParDefaut > 0 ? document.TailleParDefaut : 14;
                    string[] lines = text.Split('\n');
                    int longest = 0;
                    foreach (string line in lines) longest = Math.Max(longest, line.Length);
                    double width = noeud.LargeurFixe > 0 ? noeud.LargeurFixe : Math.Max(60, Math.Min(240, longest * size * 0.55 + 24));
                    double height = noeud.EstImage ? Math.Max(40, noeud.ImageHauteur) : Math.Max(28, lines.Length * size * 1.4 + 16);
                    index[noeud.Id] = sketch.Boites.Count;
                    sketch.Boites.Add(new Boite { X = noeud.X, Y = noeud.Y, W = width, H = height, Fond = noeud.FondCouleur ?? (document.BoiteParDefaut == null ? null : document.BoiteParDefaut.Fond) });
                }
                foreach (Lien lien in document.Liens)
                {
                    int a, b;
                    if (index.TryGetValue(lien.SourceId, out a) && index.TryGetValue(lien.CibleId, out b))
                        sketch.Liens.Add(new KeyValuePair<int, int>(a, b));
                }
            }
            catch { sketch = null; }
            if (_cache.Count > 400) _cache.Clear();
            _cache[tea] = sketch;
            return sketch;
        }
    }

    /// <summary>L'onglet « Mental-O (dlc) » des Préférences de Marabook (refait le
    /// 27/10/2026 aux formes de Marabook : intertitres demi-gras sous un filet,
    /// notes en texte doux, boutons de Marabook — plus d'onglets dans l'onglet) :
    /// Édition, Apparence, puis les raccourcis re-liables avec leurs favoris.
    /// Les réglages s'écrivent en quittant l'onglet.</summary>
    public class PreferencesTab
    {
        private readonly global::Marabook.App.IModuleHost _host;
        private readonly List<KeyValuePair<string, TextBox>> _captures = new List<KeyValuePair<string, TextBox>>();
        private readonly List<KeyValuePair<string, ToggleButton>> _favoris = new List<KeyValuePair<string, ToggleButton>>();
        private TextBlock _conflits;

        public PreferencesTab(global::Marabook.App.IModuleHost host)
        {
            _host = host;
        }

        public Control Build()
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(16, 12, 20, 16) };
            panel.Children.Add(Note("Mental-O " + typeof(Module).Assembly.GetName().Version.ToString(3)
                + " — les réglages de la toile des cartes mentales. Ils sont propres à Marabook ; le thème est celui de Marabook, "
                + "les panneaux (mini-carte, Tust, inspecteur, filtre) se règlent dans le menu Fenêtres de la carte.", 0, 14));

            // --- Édition -----------------------------------------------------------------
            panel.Children.Add(Caption("Édition", 0));
            panel.Children.Add(Case("Créer automatiquement une liste en tapant « * » ou « - » puis espace en début de ligne",
                ParametresGlobaux.ListesAutomatiques, delegate(bool v) { ParametresGlobaux.ListesAutomatiques = v; }));
            panel.Children.Add(Case("Magnétisme et guides d'alignement pendant le déplacement (Alt pour ignorer)",
                ParametresGlobaux.Magnetisme, delegate(bool v) { ParametresGlobaux.Magnetisme = v; }));

            // --- Apparence ---------------------------------------------------------------
            panel.Children.Add(Caption("Apparence", 18));
            panel.Children.Add(Ligne("Taille des poignées", Choix(new string[,] { { "Petites", "petite" }, { "Normales", "normale" }, { "Grandes", "grande" } },
                ParametresGlobaux.TaillePoignees, delegate(string v) { ParametresGlobaux.TaillePoignees = v; })));
            panel.Children.Add(Ligne("Vitesse du zoom (Ctrl+molette)", Choix(new string[,] { { "Lente", "lente" }, { "Normale", "normale" }, { "Rapide", "rapide" } },
                ParametresGlobaux.VitesseZoom, delegate(string v) { ParametresGlobaux.VitesseZoom = v; })));
            panel.Children.Add(Case("Ombre en vignette sur les bords du tableau",
                ParametresGlobaux.VignetteTableau, delegate(bool v) { ParametresGlobaux.VignetteTableau = v; }));

            // --- Raccourcis --------------------------------------------------------------
            panel.Children.Add(Caption("Raccourcis", 18));
            panel.Children.Add(Note("Cliquez dans une case puis tapez le raccourci souhaité. La flèche rétablit le défaut, l'étoile ajoute l'action au Tust des raccourcis de la carte. Les actions de fichier et de fenêtre restent celles de Marabook.", 6, 6));
            _conflits = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Chrome.Danger, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
            panel.Children.Add(_conflits);
            StackPanel profils = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6), Spacing = 6 };
            Button exporter = Buttons.Text("Exporter le profil…", "Les raccourcis dans un fichier .json, à importer sur un autre poste", Buttons.Compact, Buttons.Look.Outline);
            exporter.Click += async delegate
            {
                string chemin = await Mentalo.Vue.Ui.EnregistrerSous(_host.MainWindow, "Exporter le profil de raccourcis", "raccourcis-mental-o", "json",
                    Mentalo.Vue.Ui.Type("Profil de raccourcis", "*.json"));
                if (chemin != null) ParametresGlobaux.ExporterProfil(chemin);
            };
            Button importer = Buttons.Text("Importer…", "Un profil de raccourcis .json exporté d'ici ou de Mental-O seul", Buttons.Compact, Buttons.Look.Outline);
            importer.Click += async delegate
            {
                string chemin = await Mentalo.Vue.Ui.ChoisirFichier(_host.MainWindow, "Importer un profil de raccourcis", Mentalo.Vue.Ui.Type("Profil de raccourcis", "*.json"));
                if (chemin == null) return;
                if (ParametresGlobaux.ImporterProfil(chemin))
                {
                    Rafraichir();
                    await _host.Message("Profil importé et appliqué.", "Mental-O", MessageButtons.OK, MessageIcon.Information);
                }
                else
                    await _host.Message("Ce fichier n'est pas un profil de raccourcis valide.", "Mental-O", MessageButtons.OK, MessageIcon.Warning);
            };
            profils.Children.Add(exporter);
            profils.Children.Add(importer);
            panel.Children.Add(profils);

            string categorieCourante = null;
            foreach (DefinitionAction action in ParametresGlobaux.Actions)
            {
                if (ParametresGlobaux.EstMasquee(action.Id)) continue; // écartée par Marabook (Module.ActionsEcartees)
                if (action.Categorie != categorieCourante)
                {
                    categorieCourante = action.Categorie;
                    panel.Children.Add(new TextBlock
                    {
                        Text = categorieCourante,
                        Foreground = Chrome.SoftText,
                        FontSize = 12,
                        FontWeight = FontWeight.SemiBold,
                        Margin = new Thickness(0, 10, 0, 4)
                    });
                }
                panel.Children.Add(LigneAction(action));
            }
            ActualiserConflits();

            ScrollViewer defilement = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
            // Les réglages s'écrivent en quittant l'onglet (ou le dialogue).
            defilement.DetachedFromVisualTree += delegate
            {
                ParametresGlobaux.Enregistrer();
                if (MindMapEditor.Courant != null) MindMapEditor.Courant.RafraichirParametres();
            };
            return defilement;
        }

        // ---- les formes de Marabook (PreferencesDialog.Caption / Note, reprises ici)

        private static Control Caption(string text, double topMargin)
        {
            TextBlock label = new TextBlock { Text = text, Foreground = Chrome.Ink, FontWeight = FontWeight.SemiBold };
            if (topMargin <= 0) return label;
            StackPanel block = new StackPanel { Margin = new Thickness(0, topMargin, 0, 0) };
            block.Children.Add(new Border { Height = 1, Background = Chrome.Ink, Opacity = 0.3, Margin = new Thickness(0, 0, 0, 10), Tag = "caption-divider" });
            block.Children.Add(label);
            return block;
        }

        private static TextBlock Note(string text, double top, double bottom)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Chrome.SoftText,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, top, 0, bottom)
            };
        }

        private static CheckBox Case(string texte, bool coche, Action<bool> applique)
        {
            CheckBox case_ = new CheckBox { Content = texte, IsChecked = coche, Margin = new Thickness(0, 6, 0, 0) };
            case_.Click += delegate { applique(case_.IsChecked == true); };
            return case_;
        }

        private static Control Ligne(string libelle, Control champ)
        {
            StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            ligne.Children.Add(new TextBlock { Text = libelle + " :", VerticalAlignment = VerticalAlignment.Center, Width = 230, Foreground = Chrome.Ink });
            ligne.Children.Add(champ);
            return ligne;
        }

        private static ComboBox Choix(string[,] options, string valeur, Action<string> applique)
        {
            ComboBox combo = new ComboBox { Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 0; i < options.GetLength(0); i++)
            {
                ComboBoxItem element = new ComboBoxItem { Content = options[i, 0], Tag = options[i, 1] };
                combo.Items.Add(element);
                if (options[i, 1] == valeur) combo.SelectedItem = element;
            }
            if (combo.SelectedIndex < 0 && combo.Items.Count > 1) combo.SelectedIndex = 1;
            combo.SelectionChanged += delegate
            {
                ComboBoxItem choix = combo.SelectedItem as ComboBoxItem;
                if (choix != null) applique((string)choix.Tag);
            };
            return combo;
        }

        private Grid LigneAction(DefinitionAction action)
        {
            string id = action.Id;
            Grid ligne = new Grid { Margin = new Thickness(8, 2, 0, 2) };
            ligne.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(222)));
            ligne.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(150)));
            ligne.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ligne.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock nom = new TextBlock { Text = action.Nom, VerticalAlignment = VerticalAlignment.Center, Foreground = Chrome.Ink, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(nom, 0);
            ligne.Children.Add(nom);

            TextBox capture = DialogueParametres.ChampCapture(id, ActualiserConflits);
            capture.Height = Buttons.Compact;
            capture.MinHeight = Buttons.Compact;
            _captures.Add(new KeyValuePair<string, TextBox>(id, capture));
            Grid.SetColumn(capture, 1);
            ligne.Children.Add(capture);

            Button reinitialiser = Buttons.Icon("arrow-counter-clockwise", "Rétablir le raccourci par défaut (" + ParametresGlobaux.AfficherGeste(action.GesteParDefaut) + ")", Buttons.Compact, Buttons.Look.Calm);
            reinitialiser.Margin = new Thickness(6, 0, 0, 0);
            reinitialiser.Click += delegate
            {
                ParametresGlobaux.Raccourcis.Remove(id);
                capture.Text = ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(id));
                ActualiserConflits();
            };
            Grid.SetColumn(reinitialiser, 2);
            ligne.Children.Add(reinitialiser);

            ToggleButton favori = Buttons.IconToggle("star", "Favori — affiché dans le Tust des raccourcis de la carte", Buttons.Compact, Buttons.Look.Calm);
            favori.Margin = new Thickness(4, 0, 0, 0);
            favori.IsChecked = ParametresGlobaux.Favoris.Contains(id);
            favori.Click += delegate
            {
                if (favori.IsChecked == true) ParametresGlobaux.Favoris.Add(id);
                else ParametresGlobaux.Favoris.Remove(id);
            };
            _favoris.Add(new KeyValuePair<string, ToggleButton>(id, favori));
            Grid.SetColumn(favori, 3);
            ligne.Children.Add(favori);
            return ligne;
        }

        private void ActualiserConflits()
        {
            List<string> lignes = ParametresGlobaux.DecrireConflits();
            _conflits.Text = lignes.Count == 0 ? "" : "Conflits : " + string.Join(" — ", lignes.ToArray());
            _conflits.IsVisible = lignes.Count > 0;
        }

        /// <summary>Après l'import d'un profil : chaque case et chaque étoile relisent les réglages.</summary>
        private void Rafraichir()
        {
            foreach (KeyValuePair<string, TextBox> kv in _captures)
                kv.Value.Text = ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(kv.Key));
            foreach (KeyValuePair<string, ToggleButton> kv in _favoris)
                kv.Value.IsChecked = ParametresGlobaux.Favoris.Contains(kv.Key);
            ActualiserConflits();
        }
    }
}
