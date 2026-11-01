using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Mentalo.Vue
{
    /// <summary>Petits dialogues de saisie utilisés par les menus de formatage.</summary>
    public static class DialogueSaisie
    {
        public static async Task<double?> DemanderTaille(Window proprietaire, double valeurActuelle)
        {
            Dialogue dialogue = new Dialogue("Taille du texte");
            StackPanel panneau = new StackPanel { Margin = new Thickness(14) };
            panneau.Children.Add(new TextBlock
            {
                Text = "Taille du texte (6 à 120) :",
                Foreground = Chrome.Encre,
                Margin = new Thickness(0, 0, 0, 6)
            });
            TextBox saisie = new TextBox { Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
            saisie.Text = valeurActuelle.ToString(CultureInfo.CurrentCulture);
            saisie.KeyDown += delegate(object expediteur, KeyEventArgs e) { if (e.Key == Key.Enter) { dialogue.Valider(); e.Handled = true; } };
            panneau.Children.Add(saisie);
            panneau.Children.Add(dialogue.RangeeBoutons("Valider", "Annuler", true));
            dialogue.Content = panneau;
            dialogue.Opened += delegate { Dispatcher.UIThread.Post(delegate { saisie.Focus(); saisie.SelectAll(); }); };
            if (!await dialogue.Afficher(proprietaire)) return null;
            return InterpreterTaille(saisie.Text);
        }

        /// <summary>Demande une ligne de texte libre ; renvoie null si le dialogue est annulé.</summary>
        public static async Task<string> DemanderTexte(Window proprietaire, string titre, string invite, string valeurActuelle)
        {
            Dialogue dialogue = new Dialogue(titre);
            StackPanel panneau = new StackPanel { Margin = new Thickness(14) };
            panneau.Children.Add(new TextBlock { Text = invite, Foreground = Chrome.Encre, Margin = new Thickness(0, 0, 0, 6) });
            TextBox saisie = new TextBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
            saisie.Text = valeurActuelle ?? "";
            saisie.KeyDown += delegate(object expediteur, KeyEventArgs e) { if (e.Key == Key.Enter) { dialogue.Valider(); e.Handled = true; } };
            panneau.Children.Add(saisie);
            panneau.Children.Add(dialogue.RangeeBoutons("Valider", "Annuler", true));
            dialogue.Content = panneau;
            dialogue.Opened += delegate { Dispatcher.UIThread.Post(delegate { saisie.Focus(); saisie.SelectAll(); }); };
            if (!await dialogue.Afficher(proprietaire)) return null;
            return saisie.Text ?? "";
        }

        /// <summary>Demande un texte multiligne (import de texte à puces) ;
        /// renvoie null si le dialogue est annulé. Ctrl+Entrée valide.</summary>
        public static Task<string> DemanderTexteMultiligne(Window proprietaire, string titre, string invite)
        {
            return DemanderTexteMultiligne(proprietaire, titre, invite, "", "Importer");
        }

        /// <summary>Variante avec texte initial et libellé du bouton (note d'une boîte, v0.15).</summary>
        public static async Task<string> DemanderTexteMultiligne(Window proprietaire, string titre, string invite,
            string valeurInitiale, string libelleBouton)
        {
            Dialogue dialogue = new Dialogue(titre);
            StackPanel panneau = new StackPanel { Margin = new Thickness(14) };
            panneau.Children.Add(new TextBlock { Text = invite, Foreground = Chrome.Encre, Margin = new Thickness(0, 0, 0, 8) });
            TextBox saisie = new TextBox
            {
                Width = 460,
                Height = 260,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = libelleBouton == "Importer" ? TextWrapping.NoWrap : TextWrapping.Wrap,
                FontFamily = libelleBouton == "Importer" ? new FontFamily("Consolas, Menlo, monospace") : FontFamily.Default,
                Text = valeurInitiale ?? ""
            };
            panneau.Children.Add(saisie);
            panneau.Children.Add(dialogue.RangeeBoutons(libelleBouton, "Annuler", false));
            saisie.AddHandler(InputElement.KeyDownEvent, delegate(object expediteur, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
                {
                    dialogue.Valider();
                    e.Handled = true;
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            dialogue.Content = panneau;
            dialogue.Opened += delegate { Dispatcher.UIThread.Post(delegate { saisie.Focus(); }); };
            if (!await dialogue.Afficher(proprietaire)) return null;
            return saisie.Text ?? "";
        }

        /// <summary>Lit une taille tapée librement (virgule ou point) et la borne entre 6 et 120.</summary>
        public static double? InterpreterTaille(string texte)
        {
            if (string.IsNullOrWhiteSpace(texte)) return null;
            double valeur;
            if (!double.TryParse(texte.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valeur))
                return null;
            return Math.Max(6, Math.Min(120, valeur));
        }
    }
}
