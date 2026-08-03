using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Mentalo.Vue
{
    /// <summary>Petits dialogues de saisie utilisés par les menus de formatage.</summary>
    public static class DialogueSaisie
    {
        public static double? DemanderTaille(Window proprietaire, double valeurActuelle)
        {
            Window dialogue = new Window();
            dialogue.Title = "Taille du texte";
            dialogue.Owner = proprietaire;
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialogue.SizeToContent = SizeToContent.WidthAndHeight;
            dialogue.ResizeMode = ResizeMode.NoResize;
            dialogue.WindowStyle = WindowStyle.ToolWindow;
            dialogue.ShowInTaskbar = false;

            StackPanel panneau = new StackPanel { Margin = new Thickness(14) };
            panneau.Children.Add(new TextBlock
            {
                Text = "Taille du texte (6 à 120) :",
                Margin = new Thickness(0, 0, 0, 6)
            });
            TextBox saisie = new TextBox { Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
            saisie.Text = valeurActuelle.ToString(CultureInfo.CurrentCulture);
            panneau.Children.Add(saisie);

            StackPanel boutons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            Button valider = new Button { Content = "Valider", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            Button annuler = new Button { Content = "Annuler", Width = 80, IsCancel = true };
            valider.Click += delegate { dialogue.DialogResult = true; };
            boutons.Children.Add(valider);
            boutons.Children.Add(annuler);
            panneau.Children.Add(boutons);

            dialogue.Content = panneau;
            saisie.Loaded += delegate { saisie.Focus(); saisie.SelectAll(); };

            if (dialogue.ShowDialog() != true) return null;
            return InterpreterTaille(saisie.Text);
        }

        /// <summary>Demande une ligne de texte libre ; renvoie null si le dialogue est annulé.</summary>
        public static string DemanderTexte(Window proprietaire, string titre, string invite, string valeurActuelle)
        {
            Window dialogue = new Window();
            dialogue.Title = titre;
            dialogue.Owner = proprietaire;
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialogue.SizeToContent = SizeToContent.WidthAndHeight;
            dialogue.ResizeMode = ResizeMode.NoResize;
            dialogue.WindowStyle = WindowStyle.ToolWindow;
            dialogue.ShowInTaskbar = false;

            StackPanel panneau = new StackPanel { Margin = new Thickness(14) };
            panneau.Children.Add(new TextBlock { Text = invite, Margin = new Thickness(0, 0, 0, 6) });
            TextBox saisie = new TextBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
            saisie.Text = valeurActuelle ?? "";
            panneau.Children.Add(saisie);

            StackPanel boutons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            Button valider = new Button { Content = "Valider", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            Button annuler = new Button { Content = "Annuler", Width = 80, IsCancel = true };
            valider.Click += delegate { dialogue.DialogResult = true; };
            boutons.Children.Add(valider);
            boutons.Children.Add(annuler);
            panneau.Children.Add(boutons);

            dialogue.Content = panneau;
            saisie.Loaded += delegate { saisie.Focus(); saisie.SelectAll(); };

            if (dialogue.ShowDialog() != true) return null;
            return saisie.Text;
        }

        /// <summary>Demande un texte multiligne (import de texte à puces) ;
        /// renvoie null si le dialogue est annulé. Ctrl+Entrée valide.</summary>
        public static string DemanderTexteMultiligne(Window proprietaire, string titre, string invite)
        {
            Window dialogue = new Window();
            dialogue.Title = titre;
            dialogue.Owner = proprietaire;
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialogue.SizeToContent = SizeToContent.WidthAndHeight;
            dialogue.ResizeMode = ResizeMode.NoResize;
            dialogue.WindowStyle = WindowStyle.ToolWindow;
            dialogue.ShowInTaskbar = false;

            StackPanel panneau = new StackPanel { Margin = new Thickness(14) };
            panneau.Children.Add(new TextBlock { Text = invite, Margin = new Thickness(0, 0, 0, 8) });
            TextBox saisie = new TextBox
            {
                Width = 460,
                Height = 260,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            panneau.Children.Add(saisie);

            StackPanel boutons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            Button valider = new Button { Content = "Importer", Width = 90, Margin = new Thickness(0, 0, 6, 0) };
            Button annuler = new Button { Content = "Annuler", Width = 80, IsCancel = true };
            valider.Click += delegate { dialogue.DialogResult = true; };
            boutons.Children.Add(valider);
            boutons.Children.Add(annuler);
            panneau.Children.Add(boutons);

            saisie.PreviewKeyDown += delegate(object expediteur, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.Enter
                    && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    dialogue.DialogResult = true;
                    e.Handled = true;
                }
            };

            dialogue.Content = panneau;
            saisie.Loaded += delegate { saisie.Focus(); };

            if (dialogue.ShowDialog() != true) return null;
            return saisie.Text;
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
