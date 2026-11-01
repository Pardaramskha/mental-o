using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Mentalo.Parametres;
using Mentalo.Vue;

namespace Mentalo
{
    /// <summary>Le thème de l'interface de Mental-O seul : le thème Fluent d'Avalonia en
    /// variante claire ou sombre, recoloré avec la palette crème/encre du chrome et
    /// arrondi (boutons, champs, menus). Les pinceaux du chrome sont mutables : basculer
    /// recolore tout d'un coup.</summary>
    public static class Theme
    {
        private static Avalonia.Application _application;

        public static void Installer(Avalonia.Application application)
        {
            _application = application;
            Styles styles = new Styles();

            // Fenêtres et dialogues : fond crème, encre sombre.
            styles.Add(Style(x => x.OfType<Window>(), Window.BackgroundProperty, Chrome.FondFenetre));
            styles.Add(Style(x => x.OfType<Window>(), TemplatedControl.ForegroundProperty, Chrome.Encre));
            styles.Add(Style(x => x.OfType<TextBlock>(), TextBlock.ForegroundProperty, Chrome.Encre));

            // Boutons arrondis.
            styles.Add(Style(x => x.OfType<Button>(), Button.CornerRadiusProperty, new CornerRadius(5)));
            styles.Add(Style(x => x.OfType<Button>(), Button.PaddingProperty, new Thickness(8, 4)));
            styles.Add(Style(x => x.OfType<Button>(), Button.BackgroundProperty, Chrome.Papier));
            styles.Add(Style(x => x.OfType<Button>(), Button.BorderBrushProperty, Chrome.Bordure));
            styles.Add(Style(x => x.OfType<Button>(), Button.BorderThicknessProperty, new Thickness(1)));
            styles.Add(Style(x => x.OfType<Button>(), Button.ForegroundProperty, Chrome.Encre));
            styles.Add(Style(x => x.OfType<Button>().Class(":pointerover").Template().OfType<ContentPresenter>(),
                ContentPresenter.BackgroundProperty, Chrome.Survol));
            styles.Add(Style(x => x.OfType<Button>().Class(":pointerover").Template().OfType<ContentPresenter>(),
                ContentPresenter.BorderBrushProperty, Chrome.TexteDoux));
            styles.Add(Style(x => x.OfType<Button>().Class(":pressed").Template().OfType<ContentPresenter>(),
                ContentPresenter.BackgroundProperty, Chrome.Appui));

            styles.Add(Style(x => x.OfType<ToggleButton>(), ToggleButton.CornerRadiusProperty, new CornerRadius(5)));
            styles.Add(Style(x => x.OfType<ToggleButton>(), ToggleButton.BackgroundProperty, Brushes.Transparent));
            styles.Add(Style(x => x.OfType<ToggleButton>(), ToggleButton.BorderBrushProperty, Brushes.Transparent));
            styles.Add(Style(x => x.OfType<ToggleButton>(), ToggleButton.ForegroundProperty, Chrome.Encre));
            styles.Add(Style(x => x.OfType<ToggleButton>().Class(":pointerover").Template().OfType<ContentPresenter>(),
                ContentPresenter.BackgroundProperty, Chrome.Survol));
            styles.Add(Style(x => x.OfType<ToggleButton>().Class(":checked").Template().OfType<ContentPresenter>(),
                ContentPresenter.BackgroundProperty, Chrome.AccentDoux));
            styles.Add(Style(x => x.OfType<ToggleButton>().Class(":checked").Template().OfType<ContentPresenter>(),
                ContentPresenter.BorderBrushProperty, Chrome.Accent));
            styles.Add(Style(x => x.OfType<ToggleButton>().Class(":checked").Template().OfType<ContentPresenter>(),
                ContentPresenter.ForegroundProperty, Chrome.Encre));

            // Champs.
            styles.Add(Style(x => x.OfType<TextBox>(), TextBox.CornerRadiusProperty, new CornerRadius(5)));
            styles.Add(Style(x => x.OfType<TextBox>(), TextBox.MinHeightProperty, 26.0));
            styles.Add(Style(x => x.OfType<TextBox>(), TextBox.PaddingProperty, new Thickness(6, 3)));
            styles.Add(Style(x => x.OfType<ComboBox>(), ComboBox.CornerRadiusProperty, new CornerRadius(5)));
            styles.Add(Style(x => x.OfType<ComboBox>(), ComboBox.MinHeightProperty, 26.0));
            styles.Add(Style(x => x.OfType<ComboBox>(), ComboBox.PaddingProperty, new Thickness(8, 2, 4, 2)));

            // Menus et infobulles.
            styles.Add(Style(x => x.OfType<Menu>(), Menu.BackgroundProperty, Chrome.FondFenetre));
            styles.Add(Style(x => x.OfType<MenuItem>(), MenuItem.ForegroundProperty, Chrome.Encre));
            styles.Add(Style(x => x.OfType<ContextMenu>(), ContextMenu.CornerRadiusProperty, new CornerRadius(7)));
            styles.Add(Style(x => x.OfType<ToolTip>(), ToolTip.CornerRadiusProperty, new CornerRadius(6)));
            styles.Add(Style(x => x.OfType<ToolTip>(), ToolTip.BackgroundProperty, Chrome.Papier));
            styles.Add(Style(x => x.OfType<ToolTip>(), ToolTip.ForegroundProperty, Chrome.Encre));
            styles.Add(Style(x => x.OfType<ToolTip>(), ToolTip.BorderBrushProperty, Chrome.Bordure));

            // Onglets, listes.
            styles.Add(Style(x => x.OfType<ListBox>(), ListBox.BackgroundProperty, Chrome.Papier));
            styles.Add(Style(x => x.OfType<ListBox>(), ListBox.BorderBrushProperty, Chrome.Bordure));
            styles.Add(Style(x => x.OfType<ListBox>(), ListBox.BorderThicknessProperty, new Thickness(1)));
            styles.Add(Style(x => x.OfType<ListBox>(), ListBox.CornerRadiusProperty, new CornerRadius(5)));
            styles.Add(Style(x => x.OfType<TabItem>(), TabItem.FontSizeProperty, 13.0));
            styles.Add(Style(x => x.OfType<TabItem>(), TabItem.MinHeightProperty, 32.0));
            styles.Add(Style(x => x.OfType<TabItem>(), TabItem.PaddingProperty, new Thickness(12, 4)));
            styles.Add(Style(x => x.OfType<CheckBox>(), CheckBox.ForegroundProperty, Chrome.Encre));

            application.Styles.Add(styles);
            Appliquer(ParametresGlobaux.ThemeSombre);
        }

        private static Style Style(System.Func<Selector, Selector> selecteur, AvaloniaProperty propriete, object valeur)
        {
            Style style = new Style(selecteur);
            style.Setters.Add(new Setter(propriete, valeur));
            return style;
        }

        /// <summary>Bascule clair/sombre : variante Fluent + pinceaux du chrome.</summary>
        public static void Appliquer(bool sombre)
        {
            Chrome.Basculer(sombre);
            if (_application != null)
                _application.RequestedThemeVariant = sombre ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
