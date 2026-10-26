using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Conversions entre les segments du modèle et les objets texte d'Avalonia
    /// (TextBlock à inlines), plus la liste des polices du système.</summary>
    public static class TexteRiche
    {
        private static List<string> _policesSysteme;

        /// <summary>Toutes les polices installées sur le système, triées.</summary>
        public static List<string> PolicesSysteme()
        {
            if (_policesSysteme == null)
            {
                _policesSysteme = new List<string>();
                try
                {
                    foreach (FontFamily famille in FontManager.Current.SystemFonts)
                        if (!string.IsNullOrEmpty(famille.Name) && !_policesSysteme.Contains(famille.Name))
                            _policesSysteme.Add(famille.Name);
                }
                catch { }
                if (_policesSysteme.Count == 0) _policesSysteme.Add(Segment.PoliceDefaut);
                _policesSysteme.Sort(StringComparer.CurrentCultureIgnoreCase);
            }
            return _policesSysteme;
        }

        /// <summary>Une police par son nom ; une police inconnue retombe sur la police par défaut du système.</summary>
        public static FontFamily Police(string nom)
        {
            if (string.IsNullOrWhiteSpace(nom)) return FontFamily.Default;
            try { return new FontFamily(nom); }
            catch { return FontFamily.Default; }
        }

        public static TextAlignment ConvertirAlignement(string alignement)
        {
            if (alignement == "centre") return TextAlignment.Center;
            if (alignement == "droite") return TextAlignment.Right;
            return TextAlignment.Left;
        }

        public static string ConvertirAlignementInverse(TextAlignment alignement)
        {
            if (alignement == TextAlignment.Center) return "centre";
            if (alignement == TextAlignment.Right) return "droite";
            return "gauche";
        }

        public static Avalonia.Layout.HorizontalAlignment AlignementHorizontal(string alignement)
        {
            if (alignement == "centre") return Avalonia.Layout.HorizontalAlignment.Center;
            if (alignement == "droite") return Avalonia.Layout.HorizontalAlignment.Right;
            return Avalonia.Layout.HorizontalAlignment.Left;
        }

        public static string AlignementDeLigne(List<Segment> ligne, string defaut)
        {
            foreach (Segment s in ligne)
                if (!string.IsNullOrEmpty(s.AlignementLigne)) return s.AlignementLigne;
            return defaut;
        }

        /// <summary>Un TextBlock par ligne : c'est ce qui permet l'alignement ligne à ligne.
        /// Quand une ligne commence par ☐/☑ et que « surCase » est fourni, un clic sur le
        /// marqueur (la largeur d'un cadratin et demi) appelle surCase(indexLigne).</summary>
        public static List<TextBlock> ConstruireBlocsLignes(List<Segment> segments, string alignementDefaut, Action<int> surCase)
        {
            List<TextBlock> blocs = new List<TextBlock>();
            int index = 0;
            foreach (List<Segment> ligne in PucesTexte.DecouperLignes(segments))
            {
                TextBlock bloc = new TextBlock();
                bloc.TextWrapping = TextWrapping.Wrap;
                string alignement = AlignementDeLigne(ligne, alignementDefaut);
                bloc.TextAlignment = ConvertirAlignement(alignement);
                if (ligne.Count == 0)
                {
                    Run vide = new Run(" ");
                    vide.FontSize = Segment.TailleDefaut;
                    vide.FontFamily = Police(Segment.PoliceDefaut);
                    bloc.Inlines.Add(vide);
                }
                else
                {
                    foreach (Segment s in ligne)
                    {
                        Run run = new Run(s.Texte);
                        AppliquerAuRun(run, s);
                        bloc.Inlines.Add(run);
                    }
                    if (surCase != null && PucesTexte.EstCase(ligne[0].Texte))
                    {
                        int indexCapture = index;
                        double taille = ligne[0].Taille;
                        bool cochee = ligne[0].Texte.StartsWith(PucesTexte.CaseCochee);
                        bloc.Cursor = Ui.CurseurMain;
                        Ui.Infobulle(bloc, cochee ? "Décocher" : "Cocher");
                        bloc.PointerPressed += delegate(object expediteur, PointerPressedEventArgs e)
                        {
                            if (!e.GetCurrentPoint(bloc).Properties.IsLeftButtonPressed) return;
                            Point p = e.GetPosition(bloc);
                            double zone = taille * 1.5;
                            double debut = alignement == "centre" ? Math.Max(0, (bloc.Bounds.Width - LargeurApprox(ligne)) / 2)
                                : alignement == "droite" ? Math.Max(0, bloc.Bounds.Width - LargeurApprox(ligne))
                                : 0;
                            if (p.X < debut || p.X > debut + zone) return;
                            e.Handled = true;
                            surCase(indexCapture);
                        };
                    }
                }
                blocs.Add(bloc);
                index++;
            }
            return blocs;
        }

        private static double LargeurApprox(List<Segment> ligne)
        {
            double largeur = 0;
            foreach (Segment s in ligne) largeur += s.Texte.Length * s.Taille * 0.55;
            return largeur;
        }

        public static List<Inline> ConstruireInlines(List<Segment> segments)
        {
            List<Inline> inlines = new List<Inline>();
            foreach (Segment s in segments)
            {
                Run run = new Run(s.Texte);
                AppliquerAuRun(run, s);
                inlines.Add(run);
            }
            return inlines;
        }

        public static void AppliquerAuRun(Run run, Segment s)
        {
            run.FontFamily = Police(s.Police);
            run.FontSize = s.Taille > 0 ? s.Taille : Segment.TailleDefaut;
            run.Foreground = new SolidColorBrush(Palette.CouleurDepuis(s.Couleur, Color.FromRgb(0x1C, 0x1B, 0x19)));
            run.FontWeight = s.Gras ? FontWeight.Bold : FontWeight.Normal;
            run.FontStyle = s.Italique ? FontStyle.Italic : FontStyle.Normal;
            run.TextDecorations = Decorations(s.Souligne, s.Barre);
            if (!string.IsNullOrEmpty(s.Surlignage))
                run.Background = new SolidColorBrush(Palette.CouleurDepuis(s.Surlignage, Colors.Transparent));
        }

        public static TextDecorationCollection Decorations(bool souligne, bool barre)
        {
            if (!souligne && !barre) return null;
            TextDecorationCollection decorations = new TextDecorationCollection();
            if (souligne) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
            if (barre) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
            return decorations;
        }

        public static bool SegmentsBlancs(List<Segment> segments)
        {
            foreach (Segment s in segments)
                if (!string.IsNullOrWhiteSpace(s.Texte)) return false;
            return true;
        }

        /// <summary>Le texte plat d'une suite de segments.</summary>
        public static string TextePlat(List<Segment> segments)
        {
            System.Text.StringBuilder texte = new System.Text.StringBuilder();
            foreach (Segment s in segments) texte.Append(s.Texte);
            return texte.ToString();
        }

        /// <summary>Fusionne les segments voisins de même style et retire les vides.</summary>
        public static List<Segment> Normaliser(List<Segment> segments)
        {
            List<Segment> resultat = new List<Segment>();
            foreach (Segment s in segments)
            {
                if (string.IsNullOrEmpty(s.Texte)) continue;
                if (resultat.Count > 0 && resultat[resultat.Count - 1].MemeStyleQue(s))
                    resultat[resultat.Count - 1].Texte += s.Texte;
                else
                    resultat.Add(s.Cloner());
            }
            return resultat;
        }
    }
}
