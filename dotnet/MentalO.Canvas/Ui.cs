using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Mentalo.Vue
{
    /// <summary>Petites aides d'interface partagées par la toile, les dialogues et la
    /// fenêtre : menus contextuels, pastilles de couleur, fenêtre propriétaire,
    /// sélecteurs de fichiers (API asynchrone d'Avalonia), curseurs.</summary>
    public static class Ui
    {
        // ------------------------------------------------------------------ fenêtres

        /// <summary>La fenêtre qui porte ce contrôle (null hors d'une fenêtre).</summary>
        public static Window Fenetre(Visual visuel)
        {
            return visuel == null ? null : TopLevel.GetTopLevel(visuel) as Window;
        }

        public static TopLevel Sommet(Visual visuel)
        {
            return visuel == null ? null : TopLevel.GetTopLevel(visuel);
        }

        /// <summary>Le premier ancêtre (ou l'élément lui-même) du type demandé, ou null.</summary>
        public static T Ancetre<T>(object origine, Visual limite) where T : class
        {
            Visual v = origine as Visual;
            while (v != null && !ReferenceEquals(v, limite))
            {
                T t = v as T;
                if (t != null) return t;
                v = v.GetVisualParent();
            }
            return null;
        }

        public static bool Descend(Visual element, Visual ancetre)
        {
            Visual v = element;
            while (v != null)
            {
                if (ReferenceEquals(v, ancetre)) return true;
                v = v.GetVisualParent();
            }
            return false;
        }

        // ------------------------------------------------------------------ curseurs

        public static readonly Cursor CurseurFleche = new Cursor(StandardCursorType.Arrow);
        public static readonly Cursor CurseurMain = new Cursor(StandardCursorType.Hand);
        public static readonly Cursor CurseurCroix = new Cursor(StandardCursorType.Cross);
        public static readonly Cursor CurseurDeplacer = new Cursor(StandardCursorType.SizeAll);
        public static readonly Cursor CurseurDiagonale = new Cursor(StandardCursorType.BottomRightCorner);
        public static readonly Cursor CurseurLargeur = new Cursor(StandardCursorType.SizeWestEast);
        public static readonly Cursor CurseurHauteur = new Cursor(StandardCursorType.SizeNorthSouth);
        public static readonly Cursor CurseurTexte = new Cursor(StandardCursorType.Ibeam);

        // ------------------------------------------------------------------ menus

        public static MenuItem Element(string entete, Action clic)
        {
            MenuItem element = new MenuItem { Header = entete };
            if (clic != null) element.Click += delegate { clic(); };
            return element;
        }

        public static MenuItem Element(string entete, Action clic, bool coche)
        {
            MenuItem element = Element(entete, clic);
            Cocher(element, coche);
            return element;
        }

        public static MenuItem Element(string entete, Action clic, bool coche, string geste)
        {
            MenuItem element = Element(entete, clic, coche);
            if (!string.IsNullOrEmpty(geste)) element.InputGesture = GesteAffichage(geste);
            return element;
        }

        /// <summary>Un sous-menu (entête seul ; les enfants s'ajoutent via Items).</summary>
        public static MenuItem SousMenu(string entete)
        {
            return new MenuItem { Header = entete };
        }

        public static void Cocher(MenuItem element, bool coche)
        {
            element.ToggleType = coche ? MenuItemToggleType.CheckBox : MenuItemToggleType.None;
            element.IsChecked = coche;
        }

        /// <summary>Un KeyGesture « de présentation » : Avalonia n'affiche que ce type
        /// dans la colonne des raccourcis ; le texte français (« Ctrl+Maj+G ») est
        /// relu par ParametresGlobaux.InterpreterGeste quand c'est un geste stocké.</summary>
        public static KeyGesture GesteAffichage(string geste)
        {
            Key touche;
            KeyModifiers modificateurs;
            string normalise = (geste ?? "").Replace("Maj", "Shift").Replace("Suppr", "Delete");
            if (Mentalo.Parametres.ParametresGlobaux.InterpreterGeste(normalise, out touche, out modificateurs))
                return new KeyGesture(touche, modificateurs);
            return null;
        }

        public static Separator Separateur()
        {
            return new Separator();
        }

        /// <summary>Ouvre un menu contextuel sous le pointeur, ancré sur le contrôle.</summary>
        public static void OuvrirMenu(ContextMenu menu, Control ancre)
        {
            if (menu == null || ancre == null) return;
            menu.Placement = PlacementMode.Pointer;
            Ouvrir(menu, ancre);
        }

        /// <summary>Ouvre un menu contextuel sous un élément (lien de fichier…).</summary>
        public static void OuvrirMenuSous(ContextMenu menu, Control ancre)
        {
            if (menu == null || ancre == null) return;
            menu.Placement = PlacementMode.Bottom;
            Ouvrir(menu, ancre);
        }

        private static ContextMenu _menuOuvert;

        /// <summary>Un seul menu de la toile à la fois (1/11, Rémi : des clics
        /// droits enchaînés laissaient des menus ouverts) : le précédent se
        /// ferme avant que le suivant ne s'ouvre.</summary>
        private static void Ouvrir(ContextMenu menu, Control ancre)
        {
            ContextMenu precedent = _menuOuvert;
            if (precedent != null && precedent != menu)
            {
                try { precedent.Close(); } catch (Exception) { }
            }
            _menuOuvert = menu;
            menu.Closed += delegate { if (_menuOuvert == menu) _menuOuvert = null; };
            menu.Open(ancre);
        }

        /// <summary>Ferme le menu de la toile encore ouvert, s'il y en a un.</summary>
        public static void FermerMenuOuvert()
        {
            ContextMenu menu = _menuOuvert;
            _menuOuvert = null;
            if (menu != null) { try { menu.Close(); } catch (Exception) { } }
        }

        /// <summary>Une pastille carrée arrondie de la couleur donnée (icône de menu).</summary>
        public static Control Pastille(string hexa, double taille)
        {
            Rectangle pastille = new Rectangle();
            pastille.Width = taille;
            pastille.Height = taille;
            pastille.RadiusX = 3;
            pastille.RadiusY = 3;
            pastille.Fill = new SolidColorBrush(Palette.CouleurDepuis(hexa, Colors.Gray));
            return pastille;
        }

        public static Control PastilleRonde(string hexa, double taille)
        {
            Border pastille = new Border();
            pastille.Width = taille;
            pastille.Height = taille;
            pastille.CornerRadius = new CornerRadius(taille / 2);
            pastille.Background = new SolidColorBrush(Palette.CouleurDepuis(hexa, Colors.SteelBlue));
            return pastille;
        }

        // ------------------------------------------------------------------ sélecteurs de fichiers

        public static FilePickerFileType Type(string nom, params string[] motifs)
        {
            FilePickerFileType type = new FilePickerFileType(nom);
            type.Patterns = motifs;
            return type;
        }

        public static readonly FilePickerFileType TypeTous = Type("Tous les fichiers", "*.*");
        public static readonly FilePickerFileType TypeCartes = Type("Cartes Mental-O", "*.tea", "*.zeubi");
        public static readonly FilePickerFileType TypeCarte = Type("Carte Mental-O", "*.tea");
        public static readonly FilePickerFileType TypeImages = Type("Images", "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp");

        /// <summary>Ouvre le sélecteur de fichiers ; rend les chemins choisis (vide = annulé).</summary>
        public static async Task<List<string>> ChoisirFichiers(Visual proprietaire, string titre, bool plusieurs, params FilePickerFileType[] types)
        {
            List<string> chemins = new List<string>();
            TopLevel sommet = Sommet(proprietaire);
            if (sommet == null) return chemins;
            FilePickerOpenOptions options = new FilePickerOpenOptions();
            options.Title = titre;
            options.AllowMultiple = plusieurs;
            if (types != null && types.Length > 0) options.FileTypeFilter = types;
            IReadOnlyList<IStorageFile> fichiers = await sommet.StorageProvider.OpenFilePickerAsync(options);
            foreach (IStorageFile fichier in fichiers)
            {
                string chemin = CheminLocal(fichier);
                if (chemin != null) chemins.Add(chemin);
            }
            return chemins;
        }

        public static async Task<string> ChoisirFichier(Visual proprietaire, string titre, params FilePickerFileType[] types)
        {
            List<string> chemins = await ChoisirFichiers(proprietaire, titre, false, types);
            return chemins.Count > 0 ? chemins[0] : null;
        }

        /// <summary>Le dialogue d'enregistrement ; rend le chemin ou null.</summary>
        public static async Task<string> EnregistrerSous(Visual proprietaire, string titre, string nomSuggere, string extensionDefaut, params FilePickerFileType[] types)
        {
            TopLevel sommet = Sommet(proprietaire);
            if (sommet == null) return null;
            FilePickerSaveOptions options = new FilePickerSaveOptions();
            options.Title = titre;
            options.SuggestedFileName = nomSuggere;
            if (!string.IsNullOrEmpty(extensionDefaut)) options.DefaultExtension = extensionDefaut;
            if (types != null && types.Length > 0) options.FileTypeChoices = types;
            options.ShowOverwritePrompt = true;
            IStorageFile fichier = await sommet.StorageProvider.SaveFilePickerAsync(options);
            return fichier == null ? null : CheminLocal(fichier);
        }

        public static string CheminLocal(IStorageItem element)
        {
            if (element == null) return null;
            try { return element.TryGetLocalPath(); }
            catch { return null; }
        }

        // ------------------------------------------------------------------ presse-papiers

        public static IClipboard PressePapier(Visual visuel)
        {
            TopLevel sommet = Sommet(visuel);
            return sommet == null ? null : sommet.Clipboard;
        }

        // ------------------------------------------------------------------ divers

        public static void Infobulle(Control cible, string texte)
        {
            ToolTip.SetTip(cible, string.IsNullOrEmpty(texte) ? null : texte);
        }

        public static TextBlock Texte(string texte)
        {
            return new TextBlock { Text = texte, Foreground = Chrome.Encre };
        }

        public static TextBlock TexteDoux(string texte)
        {
            return new TextBlock { Text = texte, Foreground = Chrome.TexteDoux, TextWrapping = TextWrapping.Wrap };
        }

        /// <summary>Un bouton standard des dialogues.</summary>
        public static Button Bouton(string libelle, Action clic)
        {
            Button bouton = new Button { Content = libelle, MinWidth = 84 };
            if (clic != null) bouton.Click += delegate { clic(); };
            return bouton;
        }
    }
}
