using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Modele;
using Mentalo.Persistance;
using Mentalo.Vue;

namespace MentalO.Marabook
{
    /// <summary>LA VIGNETTE D'UNE TUILE (26/10/2026) : la carte en schéma — chaque
    /// boîte un rectangle à sa place et à sa couleur, chaque lien un trait —
    /// ramenée aux dimensions de la tuile. Lue depuis les octets .tea, sans
    /// toile ni rendu : cent tuiles ne coûtent rien. Mémorisée par tableau
    /// d'octets (ils changent à l'enregistrement de la carte).</summary>
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
            Canvas canvas = new Canvas
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Palette.CouleurDepuis(sketch.Fond, Color.FromRgb(0xF7, 0xF5, 0xF0))),
                ClipToBounds = true
            };
            SolidColorBrush link = new SolidColorBrush(Color.FromArgb(0x90, 0x60, 0x66, 0x78));
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
            SolidColorBrush border = new SolidColorBrush(Color.FromArgb(0xA0, 0x40, 0x44, 0x50));
            foreach (Boite boite in sketch.Boites)
            {
                Rectangle rect = new Rectangle
                {
                    Width = Math.Max(3, boite.W * scale),
                    Height = Math.Max(3, boite.H * scale),
                    RadiusX = 2, RadiusY = 2,
                    Fill = new SolidColorBrush(Palette.CouleurDepuis(boite.Fond, Colors.White)),
                    Stroke = border,
                    StrokeThickness = 0.8
                };
                Canvas.SetLeft(rect, offsetX + (boite.X - minX) * scale);
                Canvas.SetTop(rect, offsetY + (boite.Y - minY) * scale);
                canvas.Children.Add(rect);
            }
            return new Border
            {
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Width = width,
                Height = height,
                Child = canvas,
                HorizontalAlignment = HorizontalAlignment.Left
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

    /// <summary>L'onglet « Mental-O (dlc) » des Préférences de Marabook : les
    /// réglages de Mental-O (raccourcis, édition, apparence), enregistrés dans
    /// le dossier de Marabook. Le thème est celui de Marabook.</summary>
    public class PreferencesTab
    {
        private readonly global::Marabook.App.IModuleHost _host;

        public PreferencesTab(global::Marabook.App.IModuleHost host)
        {
            _host = host;
        }

        public Control Build()
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            panel.Children.Add(new TextBlock
            {
                Text = "Mental-O " + typeof(Module).Assembly.GetName().Version.ToString(3) + " — les réglages de la toile des cartes mentales : raccourcis re-liables (ceux de la toile, dans Marabook ; les actions de fichier restent à Marabook), listes automatiques, magnétisme, poignées, zoom, panneaux. Ils sont propres à Marabook (" + Mentalo.Parametres.ParametresGlobaux.Dossier() + ").",
                Foreground = global::Marabook.App.Chrome.SoftText,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            TabControl onglets = DialogueParametres.ConstruireOnglets(_host.MainWindow, false);
            onglets.MinHeight = 420;
            panel.Children.Add(onglets);
            // Les réglages s'écrivent en quittant l'onglet (ou le dialogue).
            panel.DetachedFromVisualTree += delegate { Mentalo.Parametres.ParametresGlobaux.Enregistrer(); };
            return panel;
        }
    }
}
