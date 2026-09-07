using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>L'état de mise en forme au point d'insertion ou de la boîte sélectionnée,
    /// pour synchroniser la barre de formatage et les coches des menus.</summary>
    public class FormatCourant
    {
        public bool Actif;
        public string Police = "Segoe UI";
        public double Taille = 14;
        public bool Gras, Italique, Souligne, Barre;
        public string Couleur = "#1C1B19";
        public string Surlignage;
        public string Alignement = "gauche";

        // Style de la boîte sélectionnée
        public string FondBoite = "#FFFFFF";
        public string BordureCouleur = "#B8B2A7";
        public double BordureEpaisseur = 1.5;
        public string BordureTrait = "plein";
        public double Rayon = 5;
        public string Ombre = "legere";
        public double Opacite = 1;
    }

    /// <summary>Conversions entre les segments du modèle et les objets texte de WPF,
    /// plus les palettes proposées par l'interface.</summary>
    public static class TexteRiche
    {
        private static List<string> _policesSysteme;

        /// <summary>Toutes les polices installées sur le système, triées.</summary>
        public static List<string> PolicesSysteme()
        {
            if (_policesSysteme == null)
            {
                _policesSysteme = new List<string>();
                foreach (FontFamily famille in Fonts.SystemFontFamilies)
                    _policesSysteme.Add(famille.Source);
                _policesSysteme.Sort(StringComparer.CurrentCultureIgnoreCase);
            }
            return _policesSysteme;
        }

        public static readonly double[] Tailles = { 10, 12, 14, 16, 18, 20, 24, 28, 32, 36 };

        // { nom, hexa } — couleurs de texte
        public static readonly string[,] Couleurs =
        {
            { "Noir", "#1C1B19" }, { "Gris", "#6A665E" }, { "Bleu", "#0078D7" },
            { "Vert", "#2E8B57" }, { "Rouge", "#C0392B" }, { "Orange", "#E67E22" },
            { "Violet", "#8E44AD" }, { "Rose", "#D63384" }
        };

        // { nom, hexa } — couleurs de surlignage (pastel pour rester lisible)
        public static readonly string[,] Surlignages =
        {
            { "Jaune", "#FFF3A0" }, { "Vert clair", "#C8F0C8" }, { "Bleu clair", "#C5E4F7" },
            { "Rose clair", "#F7CBDC" }, { "Orange clair", "#FBE0C0" }, { "Gris clair", "#E4E0D8" }
        };

        public static Color CouleurDepuis(string hexa, Color defaut)
        {
            try { return (Color)ColorConverter.ConvertFromString(hexa); }
            catch { return defaut; }
        }

        public static string HexaDepuisCouleur(Color couleur)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", couleur.R, couleur.G, couleur.B);
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

        private static string AlignementDeLigne(List<Segment> ligne, string defaut)
        {
            foreach (Segment s in ligne)
                if (!string.IsNullOrEmpty(s.AlignementLigne)) return s.AlignementLigne;
            return defaut;
        }

        /// <summary>Un TextBlock par ligne : c'est ce qui permet l'alignement ligne à ligne.</summary>
        public static List<System.Windows.Controls.TextBlock> ConstruireBlocsLignes(
            List<Segment> segments, string alignementDefaut)
        {
            return ConstruireBlocsLignes(segments, alignementDefaut, null);
        }

        /// <summary>Variante avec cases à cocher cliquables (v0.15) : quand une ligne commence
        /// par ☐/☑, le marqueur devient un Run à part, curseur main, qui appelle
        /// « surCase(indexLigne) » au clic gauche (le clic n'atteint pas la toile).</summary>
        public static List<System.Windows.Controls.TextBlock> ConstruireBlocsLignes(
            List<Segment> segments, string alignementDefaut, Action<int> surCase)
        {
            List<System.Windows.Controls.TextBlock> blocs = new List<System.Windows.Controls.TextBlock>();
            int index = 0;
            foreach (List<Segment> ligne in PucesTexte.DecouperLignes(segments))
            {
                System.Windows.Controls.TextBlock bloc = new System.Windows.Controls.TextBlock();
                bloc.TextWrapping = TextWrapping.Wrap;
                bloc.TextAlignment = ConvertirAlignement(AlignementDeLigne(ligne, alignementDefaut));
                if (ligne.Count == 0)
                {
                    Run vide = new Run(" ");
                    vide.FontSize = Segment.TailleDefaut;
                    bloc.Inlines.Add(vide);
                }
                else
                {
                    bool premier = true;
                    foreach (Segment s in ligne)
                    {
                        if (premier && surCase != null && PucesTexte.EstCase(s.Texte))
                        {
                            int indexCapture = index;
                            Run marqueur = new Run(s.Texte.Substring(0, 2));
                            AppliquerAuRun(marqueur, s);
                            marqueur.Cursor = System.Windows.Input.Cursors.Hand;
                            marqueur.ToolTip = s.Texte.StartsWith(PucesTexte.CaseCochee) ? "Décocher" : "Cocher";
                            marqueur.MouseLeftButtonDown += delegate(object expediteur, System.Windows.Input.MouseButtonEventArgs e)
                            {
                                e.Handled = true;
                                surCase(indexCapture);
                            };
                            bloc.Inlines.Add(marqueur);
                            if (s.Texte.Length > 2)
                            {
                                Run reste = new Run(s.Texte.Substring(2));
                                AppliquerAuRun(reste, s);
                                bloc.Inlines.Add(reste);
                            }
                            premier = false;
                            continue;
                        }
                        premier = false;
                        Run run = new Run(s.Texte);
                        AppliquerAuRun(run, s);
                        bloc.Inlines.Add(run);
                    }
                }
                blocs.Add(bloc);
                index++;
            }
            return blocs;
        }

        // ------------------------------------------------------------------ affichage (TextBlock)

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

        // ------------------------------------------------------------------ édition (FlowDocument)

        /// <summary>Le document d'édition complet : texte avant, image (bloc traversable par le
        /// curseur), texte après. Un paragraphe vide encadre toujours l'image pour qu'on puisse
        /// écrire avant comme après.</summary>
        public static FlowDocument ConstruireDocumentComplet(Noeud noeud, System.Windows.Media.ImageSource image)
        {
            FlowDocument document = new FlowDocument();
            document.PagePadding = new Thickness(0);
            TextAlignment alignementTexte = ConvertirAlignement(noeud.Alignement);

            AjouterParagraphes(document, noeud.Segments, alignementTexte);

            if (noeud.EstImage && image != null)
            {
                System.Windows.Controls.Image element = new System.Windows.Controls.Image();
                element.Source = image;
                element.Width = noeud.ImageLargeur;
                element.Height = noeud.ImageHauteur;
                element.Stretch = System.Windows.Media.Stretch.Fill;
                element.HorizontalAlignment = alignementTexte == TextAlignment.Center ? HorizontalAlignment.Center
                    : alignementTexte == TextAlignment.Right ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
                BlockUIContainer bloc = new BlockUIContainer(element);
                bloc.Margin = new Thickness(0, 4, 0, 4);
                document.Blocks.Add(bloc);

                AjouterParagraphes(document, noeud.SegmentsApres, alignementTexte);
            }
            return document;
        }

        private static void AjouterParagraphes(FlowDocument document, List<Segment> segments, TextAlignment alignement)
        {
            string defaut = ConvertirAlignementInverse(alignement);
            List<List<Segment>> lignes = PucesTexte.DecouperLignes(segments);
            foreach (List<Segment> ligne in lignes)
            {
                Paragraph paragraphe = NouveauParagraphe(
                    ConvertirAlignement(AlignementDeLigne(ligne, defaut)));
                document.Blocks.Add(paragraphe);
                foreach (Segment s in ligne)
                {
                    Run run = new Run(s.Texte);
                    AppliquerAuRun(run, s);
                    paragraphe.Inlines.Add(run);
                }
            }
        }

        /// <summary>Relit le document d'édition : les paragraphes avant le bloc image vont dans
        /// « avant », ceux qui suivent dans « après ».</summary>
        public static void ExtraireSegmentsComplet(FlowDocument document,
            out List<Segment> avant, out List<Segment> apres)
        {
            avant = new List<Segment>();
            apres = new List<Segment>();
            List<Segment> courant = avant;
            bool premierAvant = true, premierApres = true;
            foreach (Block bloc in document.Blocks)
            {
                if (bloc is BlockUIContainer)
                {
                    courant = apres;
                    continue;
                }
                Paragraph paragraphe = bloc as Paragraph;
                if (paragraphe == null) continue;
                bool premier = ReferenceEquals(courant, avant) ? premierAvant : premierApres;
                if (!premier) AjouterRetour(courant);
                if (ReferenceEquals(courant, avant)) premierAvant = false;
                else premierApres = false;
                string alignementLigne = ConvertirAlignementInverse(paragraphe.TextAlignment);
                foreach (Inline inline in paragraphe.Inlines)
                    ExtraireInline(inline, courant, alignementLigne);
            }
        }

        public static bool SegmentsBlancs(List<Segment> segments)
        {
            foreach (Segment s in segments)
                if (!string.IsNullOrWhiteSpace(s.Texte)) return false;
            return true;
        }

        // ------------------------------------------------------------------ interne

        private static Paragraph NouveauParagraphe(TextAlignment alignement)
        {
            Paragraph paragraphe = new Paragraph();
            paragraphe.Margin = new Thickness(0);
            paragraphe.TextAlignment = alignement;
            return paragraphe;
        }

        private static void AppliquerAuRun(Run run, Segment s)
        {
            run.FontFamily = new FontFamily(s.Police);
            run.FontSize = s.Taille;
            run.Foreground = new SolidColorBrush(CouleurDepuis(s.Couleur, Color.FromRgb(0x1C, 0x1B, 0x19)));
            run.FontWeight = s.Gras ? FontWeights.Bold : FontWeights.Normal;
            run.FontStyle = s.Italique ? FontStyles.Italic : FontStyles.Normal;
            TextDecorationCollection decorations = new TextDecorationCollection();
            if (s.Souligne) decorations.Add(TextDecorations.Underline[0]);
            if (s.Barre) decorations.Add(TextDecorations.Strikethrough[0]);
            run.TextDecorations = decorations;
            if (!string.IsNullOrEmpty(s.Surlignage))
                run.Background = new SolidColorBrush(CouleurDepuis(s.Surlignage, Colors.Transparent));
        }

        private static void ExtraireInline(Inline inline, List<Segment> segments, string alignementLigne)
        {
            Run run = inline as Run;
            if (run != null)
            {
                if (run.Text.Length == 0) return;
                AjouterSegment(segments, SegmentDepuisRun(run, alignementLigne));
                return;
            }
            if (inline is LineBreak)
            {
                AjouterRetour(segments);
                return;
            }
            Span span = inline as Span;
            if (span != null)
                foreach (Inline enfant in span.Inlines)
                    ExtraireInline(enfant, segments, alignementLigne);
        }

        private static Segment SegmentDepuisRun(Run run, string alignementLigne)
        {
            Segment s = new Segment();
            s.Texte = run.Text;
            s.AlignementLigne = alignementLigne;
            s.Police = run.FontFamily.Source;
            s.Taille = run.FontSize;
            SolidColorBrush encre = run.Foreground as SolidColorBrush;
            if (encre != null) s.Couleur = HexaDepuisCouleur(encre.Color);
            s.Gras = run.FontWeight.ToOpenTypeWeight() >= 600;
            s.Italique = run.FontStyle == FontStyles.Italic || run.FontStyle == FontStyles.Oblique;
            if (run.TextDecorations != null)
            {
                foreach (TextDecoration decoration in run.TextDecorations)
                {
                    if (decoration.Location == TextDecorationLocation.Underline) s.Souligne = true;
                    if (decoration.Location == TextDecorationLocation.Strikethrough) s.Barre = true;
                }
            }
            SolidColorBrush fond = run.Background as SolidColorBrush;
            if (fond != null && fond.Color.A > 0) s.Surlignage = HexaDepuisCouleur(fond.Color);
            return s;
        }

        private static void AjouterSegment(List<Segment> segments, Segment s)
        {
            if (segments.Count > 0 && segments[segments.Count - 1].MemeStyleQue(s))
                segments[segments.Count - 1].Texte += s.Texte;
            else
                segments.Add(s);
        }

        private static void AjouterRetour(List<Segment> segments)
        {
            if (segments.Count > 0)
            {
                segments[segments.Count - 1].Texte += "\n";
            }
            else
            {
                Segment s = new Segment();
                s.Texte = "\n";
                segments.Add(s);
            }
        }
    }
}
