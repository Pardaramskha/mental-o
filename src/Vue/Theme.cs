using System;
using System.Windows;
using System.Windows.Markup;

namespace Mentalo.Vue
{
    /// <summary>Le thème de l'interface : des gabarits modernes (coins arrondis, palette
    /// crème/encre, accent bleu) appliqués à toute l'application. Purement visuel —
    /// aucun contrôle ni comportement n'est modifié.</summary>
    public static class Theme
    {
        private static ResourceDictionary _actuel;

        public static void Appliquer(Application application)
        {
            Basculer(application, Mentalo.Parametres.ParametresGlobaux.ThemeSombre);
        }

        /// <summary>(Re)construit le dictionnaire de styles pour le thème demandé et
        /// remplace l'actuel — les contrôles existants se recolorent d'eux-mêmes.</summary>
        public static void Basculer(Application application, bool sombre)
        {
            try
            {
                ResourceDictionary nouveau = (ResourceDictionary)XamlReader.Parse(
                    Entete + (sombre ? PaletteSombre : PaletteClaire) + Gabarits);
                if (_actuel != null) application.Resources.MergedDictionaries.Remove(_actuel);
                application.Resources.MergedDictionaries.Add(nouveau);
                _actuel = nouveau;
            }
            catch
            {
                // En cas de pépin de gabarit, l'application reste utilisable en style classique.
            }
        }

        private const string Entete = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
";

        private const string PaletteClaire = @"
  <SolidColorBrush x:Key=""Encre"" Color=""#3A3833""/>
  <SolidColorBrush x:Key=""EncreDouce"" Color=""#6A665E""/>
  <SolidColorBrush x:Key=""Papier"" Color=""#FFFFFF""/>
  <SolidColorBrush x:Key=""Voile"" Color=""#F7F4EE""/>
  <SolidColorBrush x:Key=""Bordure"" Color=""#CBC5BB""/>
  <SolidColorBrush x:Key=""Survol"" Color=""#ECE7DE""/>
  <SolidColorBrush x:Key=""Appui"" Color=""#E2DCD1""/>
  <SolidColorBrush x:Key=""Accent"" Color=""#0078D7""/>
  <SolidColorBrush x:Key=""AccentDoux"" Color=""#D8E9F9""/>
  <SolidColorBrush x:Key=""AccentSurvol"" Color=""#E9F2FB""/>
  <SolidColorBrush x:Key=""Trait"" Color=""#E4E0D8""/>
";

        private const string PaletteSombre = @"
  <SolidColorBrush x:Key=""Encre"" Color=""#E8EAED""/>
  <SolidColorBrush x:Key=""EncreDouce"" Color=""#9BA3AF""/>
  <SolidColorBrush x:Key=""Papier"" Color=""#2A2F3A""/>
  <SolidColorBrush x:Key=""Voile"" Color=""#262B34""/>
  <SolidColorBrush x:Key=""Bordure"" Color=""#3F4552""/>
  <SolidColorBrush x:Key=""Survol"" Color=""#333A47""/>
  <SolidColorBrush x:Key=""Appui"" Color=""#3C4454""/>
  <SolidColorBrush x:Key=""Accent"" Color=""#4C9AE8""/>
  <SolidColorBrush x:Key=""AccentDoux"" Color=""#1E3A5A""/>
  <SolidColorBrush x:Key=""AccentSurvol"" Color=""#253243""/>
  <SolidColorBrush x:Key=""Trait"" Color=""#3B414C""/>
";

        private const string Gabarits = @"
  <!-- Les fenêtres (dialogues) suivent le thème ; la fenêtre principale est gérée à part. -->
  <Style TargetType=""Window"">
    <Setter Property=""Background"" Value=""{StaticResource Voile}""/>
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
  </Style>

  <!-- ============================== Boutons ============================== -->
  <Style TargetType=""Button"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Padding"" Value=""7,3""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""Fond"" CornerRadius=""5"" Background=""{StaticResource Papier}""
                  BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1""
                  Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource Survol}""/>
              <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource EncreDouce}""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource Appui}""/>
            </Trigger>
            <Trigger Property=""IsDefault"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
              <Setter TargetName=""Fond"" Property=""BorderThickness"" Value=""1.5""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.45""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ToggleButton"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Padding"" Value=""7,3""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ToggleButton"">
          <Border x:Name=""Fond"" CornerRadius=""5"" Background=""Transparent""
                  BorderBrush=""Transparent"" BorderThickness=""1""
                  Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource Survol}""/>
            </Trigger>
            <Trigger Property=""IsChecked"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource AccentDoux}""/>
              <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.45""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ============================== Zones de texte ============================== -->
  <Style x:Key=""TexteNu"" TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""CaretBrush"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TextBox"">
          <ScrollViewer x:Name=""PART_ContentHost"" VerticalAlignment=""Center""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""CaretBrush"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Padding"" Value=""5,2""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TextBox"">
          <Border x:Name=""Fond"" CornerRadius=""5"" Background=""{StaticResource Papier}""
                  BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1""
                  Padding=""{TemplateBinding Padding}"">
            <ScrollViewer x:Name=""PART_ContentHost"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource EncreDouce}""/>
            </Trigger>
            <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
              <Setter TargetName=""Fond"" Property=""BorderThickness"" Value=""1.5""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.45""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ============================== Cases à cocher ============================== -->
  <Style TargetType=""CheckBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""CheckBox"">
          <StackPanel Orientation=""Horizontal"" Background=""Transparent"">
            <Border x:Name=""Case"" Width=""16"" Height=""16"" CornerRadius=""4""
                    Background=""{StaticResource Papier}"" BorderBrush=""{StaticResource Bordure}""
                    BorderThickness=""1"" VerticalAlignment=""Center"">
              <Path x:Name=""Coche"" Data=""M3,8 L7,12 13,4"" Stroke=""White"" StrokeThickness=""2""
                    Visibility=""Collapsed"" Stretch=""Uniform"" Margin=""3""/>
            </Border>
            <ContentPresenter Margin=""7,0,0,0"" VerticalAlignment=""Center""/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsChecked"" Value=""True"">
              <Setter TargetName=""Case"" Property=""Background"" Value=""{StaticResource Accent}""/>
              <Setter TargetName=""Case"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
              <Setter TargetName=""Coche"" Property=""Visibility"" Value=""Visible""/>
            </Trigger>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Case"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ============================== Listes déroulantes ============================== -->
  <Style TargetType=""ComboBoxItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Padding"" Value=""7,3""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ComboBoxItem"">
          <Border x:Name=""Fond"" CornerRadius=""4"" Margin=""3,1"" Padding=""{TemplateBinding Padding}""
                  Background=""Transparent"">
            <ContentPresenter VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsHighlighted"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource AccentSurvol}""/>
            </Trigger>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource AccentDoux}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ComboBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ComboBox"">
          <Grid>
            <ToggleButton Focusable=""False"" ClickMode=""Press""
                          IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
              <ToggleButton.Template>
                <ControlTemplate TargetType=""ToggleButton"">
                  <Border x:Name=""Fond"" CornerRadius=""5"" Background=""{StaticResource Papier}""
                          BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1"">
                    <Path Data=""M0,0 L4,4 8,0"" Stroke=""{StaticResource EncreDouce}"" StrokeThickness=""1.6""
                          HorizontalAlignment=""Right"" VerticalAlignment=""Center"" Margin=""0,1,8,0""/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property=""IsMouseOver"" Value=""True"">
                      <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource EncreDouce}""/>
                    </Trigger>
                    <Trigger Property=""IsChecked"" Value=""True"">
                      <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter x:Name=""Choix"" Margin=""8,2,22,2"" VerticalAlignment=""Center""
                              Content=""{TemplateBinding SelectionBoxItem}""
                              ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}""
                              IsHitTestVisible=""False""/>
            <TextBox x:Name=""PART_EditableTextBox"" Style=""{StaticResource TexteNu}""
                     Margin=""8,1,22,1"" Visibility=""Collapsed""
                     IsReadOnly=""{TemplateBinding IsReadOnly}""/>
            <Popup IsOpen=""{TemplateBinding IsDropDownOpen}"" Placement=""Bottom""
                   AllowsTransparency=""True"" PopupAnimation=""Fade"">
              <Border CornerRadius=""6"" Background=""{StaticResource Papier}""
                      BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1""
                      Margin=""6,2,6,6"" MinWidth=""{TemplateBinding ActualWidth}""
                      MaxHeight=""{TemplateBinding MaxDropDownHeight}"">
                <Border.Effect>
                  <DropShadowEffect Color=""Black"" Opacity=""0.22"" BlurRadius=""8"" ShadowDepth=""2""/>
                </Border.Effect>
                <ScrollViewer Padding=""0,3"">
                  <ItemsPresenter/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsEditable"" Value=""True"">
              <Setter TargetName=""Choix"" Property=""Visibility"" Value=""Collapsed""/>
              <Setter TargetName=""PART_EditableTextBox"" Property=""Visibility"" Value=""Visible""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.45""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ============================== Menus ============================== -->
  <Style TargetType=""Menu"">
    <Setter Property=""Background"" Value=""{StaticResource Voile}""/>
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Padding"" Value=""3,2""/>
  </Style>

  <Style TargetType=""ContextMenu"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ContextMenu"">
          <Border CornerRadius=""7"" Background=""{StaticResource Papier}""
                  BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1""
                  Margin=""6"" Padding=""0,4"">
            <Border.Effect>
              <DropShadowEffect Color=""Black"" Opacity=""0.25"" BlurRadius=""10"" ShadowDepth=""2""/>
            </Border.Effect>
            <ItemsPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <ControlTemplate x:Key=""MenuHaut"" TargetType=""MenuItem"">
    <Border x:Name=""Fond"" CornerRadius=""5"" Padding=""9,4"" Background=""Transparent"">
      <ContentPresenter ContentSource=""Header"" RecognizesAccessKey=""True"" VerticalAlignment=""Center""/>
    </Border>
    <ControlTemplate.Triggers>
      <Trigger Property=""IsHighlighted"" Value=""True"">
        <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource Survol}""/>
      </Trigger>
    </ControlTemplate.Triggers>
  </ControlTemplate>

  <ControlTemplate x:Key=""MenuHautParent"" TargetType=""MenuItem"">
    <Grid>
      <Border x:Name=""Fond"" CornerRadius=""5"" Padding=""9,4"" Background=""Transparent"">
        <ContentPresenter ContentSource=""Header"" RecognizesAccessKey=""True"" VerticalAlignment=""Center""/>
      </Border>
      <Popup IsOpen=""{TemplateBinding IsSubmenuOpen}"" Placement=""Bottom""
             AllowsTransparency=""True"" PopupAnimation=""Fade"" Focusable=""False"">
        <Border CornerRadius=""7"" Background=""{StaticResource Papier}""
                BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1""
                Margin=""6"" Padding=""0,4"" MinWidth=""170"">
          <Border.Effect>
            <DropShadowEffect Color=""Black"" Opacity=""0.25"" BlurRadius=""10"" ShadowDepth=""2""/>
          </Border.Effect>
          <ScrollViewer>
            <ItemsPresenter/>
          </ScrollViewer>
        </Border>
      </Popup>
    </Grid>
    <ControlTemplate.Triggers>
      <Trigger Property=""IsHighlighted"" Value=""True"">
        <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource Survol}""/>
      </Trigger>
      <Trigger Property=""IsSubmenuOpen"" Value=""True"">
        <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource Appui}""/>
      </Trigger>
    </ControlTemplate.Triggers>
  </ControlTemplate>

  <ControlTemplate x:Key=""MenuElement"" TargetType=""MenuItem"">
    <Border x:Name=""Fond"" CornerRadius=""4"" Margin=""4,1"" Padding=""6,4"" Background=""Transparent"">
      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width=""20""/>
          <ColumnDefinition Width=""*""/>
          <ColumnDefinition Width=""Auto""/>
        </Grid.ColumnDefinitions>
        <ContentPresenter Grid.Column=""0"" ContentSource=""Icon"" VerticalAlignment=""Center""/>
        <Path x:Name=""Coche"" Grid.Column=""0"" Data=""M1,5 L4,8 9,1"" Stroke=""{StaticResource Accent}""
              StrokeThickness=""2"" Visibility=""Collapsed"" VerticalAlignment=""Center"" HorizontalAlignment=""Center""/>
        <ContentPresenter Grid.Column=""1"" ContentSource=""Header"" RecognizesAccessKey=""True""
                          VerticalAlignment=""Center"" Margin=""4,0,0,0""/>
        <TextBlock Grid.Column=""2"" Text=""{TemplateBinding InputGestureText}""
                   Foreground=""{StaticResource EncreDouce}"" Margin=""20,0,6,0"" VerticalAlignment=""Center""/>
      </Grid>
    </Border>
    <ControlTemplate.Triggers>
      <Trigger Property=""IsChecked"" Value=""True"">
        <Setter TargetName=""Coche"" Property=""Visibility"" Value=""Visible""/>
      </Trigger>
      <Trigger Property=""IsHighlighted"" Value=""True"">
        <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource AccentSurvol}""/>
      </Trigger>
      <Trigger Property=""IsEnabled"" Value=""False"">
        <Setter Property=""Opacity"" Value=""0.45""/>
      </Trigger>
    </ControlTemplate.Triggers>
  </ControlTemplate>

  <ControlTemplate x:Key=""MenuElementParent"" TargetType=""MenuItem"">
    <Grid>
      <Border x:Name=""Fond"" CornerRadius=""4"" Margin=""4,1"" Padding=""6,4"" Background=""Transparent"">
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width=""20""/>
            <ColumnDefinition Width=""*""/>
            <ColumnDefinition Width=""Auto""/>
          </Grid.ColumnDefinitions>
          <ContentPresenter Grid.Column=""0"" ContentSource=""Icon"" VerticalAlignment=""Center""/>
          <ContentPresenter Grid.Column=""1"" ContentSource=""Header"" RecognizesAccessKey=""True""
                            VerticalAlignment=""Center"" Margin=""4,0,0,0""/>
          <Path Grid.Column=""2"" Data=""M0,0 L4,4 0,8"" Stroke=""{StaticResource EncreDouce}""
                StrokeThickness=""1.6"" Margin=""14,0,4,0"" VerticalAlignment=""Center""/>
        </Grid>
      </Border>
      <Popup IsOpen=""{TemplateBinding IsSubmenuOpen}"" Placement=""Right""
             AllowsTransparency=""True"" PopupAnimation=""Fade"" Focusable=""False""
             HorizontalOffset=""-4"" VerticalOffset=""-6"">
        <Border CornerRadius=""7"" Background=""{StaticResource Papier}""
                BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1""
                Margin=""6"" Padding=""0,4"" MinWidth=""150"">
          <Border.Effect>
            <DropShadowEffect Color=""Black"" Opacity=""0.25"" BlurRadius=""10"" ShadowDepth=""2""/>
          </Border.Effect>
          <ScrollViewer>
            <ItemsPresenter/>
          </ScrollViewer>
        </Border>
      </Popup>
    </Grid>
    <ControlTemplate.Triggers>
      <Trigger Property=""IsHighlighted"" Value=""True"">
        <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource AccentSurvol}""/>
      </Trigger>
      <Trigger Property=""IsEnabled"" Value=""False"">
        <Setter Property=""Opacity"" Value=""0.45""/>
      </Trigger>
    </ControlTemplate.Triggers>
  </ControlTemplate>

  <Style TargetType=""MenuItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Style.Triggers>
      <Trigger Property=""Role"" Value=""TopLevelHeader"">
        <Setter Property=""Template"" Value=""{StaticResource MenuHautParent}""/>
      </Trigger>
      <Trigger Property=""Role"" Value=""TopLevelItem"">
        <Setter Property=""Template"" Value=""{StaticResource MenuHaut}""/>
      </Trigger>
      <Trigger Property=""Role"" Value=""SubmenuHeader"">
        <Setter Property=""Template"" Value=""{StaticResource MenuElementParent}""/>
      </Trigger>
      <Trigger Property=""Role"" Value=""SubmenuItem"">
        <Setter Property=""Template"" Value=""{StaticResource MenuElement}""/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style TargetType=""Separator"">
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Separator"">
          <Border Height=""1"" Background=""{StaticResource Trait}"" Margin=""8,4""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ============================== Onglets et listes ============================== -->
  <Style TargetType=""TabControl"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Padding"" Value=""4""/>
  </Style>

  <Style TargetType=""TabItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource EncreDouce}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TabItem"">
          <Border x:Name=""Fond"" Background=""Transparent"" Padding=""12,6"" Margin=""2,2,0,0""
                  BorderThickness=""0,0,0,2"" BorderBrush=""Transparent"">
            <ContentPresenter ContentSource=""Header"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource Survol}""/>
            </Trigger>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
              <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
              <Setter Property=""FontWeight"" Value=""SemiBold""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ListBox"">
    <Setter Property=""Background"" Value=""{StaticResource Papier}""/>
    <Setter Property=""BorderBrush"" Value=""{StaticResource Bordure}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Padding"" Value=""2""/>
  </Style>

  <Style TargetType=""ListBoxItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ListBoxItem"">
          <Border x:Name=""Fond"" CornerRadius=""4"" Margin=""1"" Padding=""6,3"" Background=""Transparent"">
            <ContentPresenter VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource AccentSurvol}""/>
            </Trigger>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter TargetName=""Fond"" Property=""Background"" Value=""{StaticResource AccentDoux}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ============================== Barres de défilement ============================== -->
  <Style TargetType=""ScrollBar"">
    <Setter Property=""Width"" Value=""10""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ScrollBar"">
          <Grid Background=""Transparent"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType=""Thumb"">
                      <Border CornerRadius=""4"" Background=""#C9C3B8"" Margin=""2""/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property=""Orientation"" Value=""Horizontal"">
        <Setter Property=""Width"" Value=""Auto""/>
        <Setter Property=""Height"" Value=""10""/>
        <Setter Property=""Template"">
          <Setter.Value>
            <ControlTemplate TargetType=""ScrollBar"">
              <Grid Background=""Transparent"">
                <Track x:Name=""PART_Track"">
                  <Track.Thumb>
                    <Thumb>
                      <Thumb.Template>
                        <ControlTemplate TargetType=""Thumb"">
                          <Border CornerRadius=""4"" Background=""#C9C3B8"" Margin=""2""/>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                </Track>
              </Grid>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Trigger>
    </Style.Triggers>
  </Style>

  <!-- ============================== Infobulles ============================== -->
  <Style TargetType=""ToolTip"">
    <Setter Property=""Foreground"" Value=""{StaticResource Encre}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ToolTip"">
          <Border CornerRadius=""6"" Background=""{StaticResource Papier}""
                  BorderBrush=""{StaticResource Bordure}"" BorderThickness=""1""
                  Padding=""8,4"" Margin=""4"">
            <Border.Effect>
              <DropShadowEffect Color=""Black"" Opacity=""0.2"" BlurRadius=""6"" ShadowDepth=""1""/>
            </Border.Effect>
            <ContentPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>";
    }
}
