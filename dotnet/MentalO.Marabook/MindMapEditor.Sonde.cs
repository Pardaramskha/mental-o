using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Marabook.App;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;

namespace MentalO.Marabook
{
    /// <summary>LA SONDE DU DLC (26/10/2026 soir) : quand la variable
    /// d'environnement MENTALO_SONDE porte un chemin, le module ouvre la carte
    /// de démo du projet d'exemple (--demo), déroule un scénario dessus —
    /// édition d'une boîte avec un retour à la ligne, vrai clic de souris dans
    /// un champ du rail, lien vers une fiche, tuile, aller-retour .tea, thème
    /// de carte, raccourcis écartés, vignette — et écrit ses constats dans ce
    /// fichier (« SONDE DLC OK (n vérifications) » ou les échecs), puis quitte
    /// Marabook (code 0 ou 1). Lancée par les scripts de vérification, jamais
    /// chez l'utilisateur.</summary>
    public partial class MindMapEditor
    {
        private static bool _sondeLancee;

        /// <summary>Sous MENTALO_SONDE, la fenêtre de Marabook ouvre la carte de
        /// démo du projet d'exemple (--demo) par son OpenByTitle, une fois posée.</summary>
        public static void OuvrirCarteDeSondeSiDemandee(IModuleHost host)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MENTALO_SONDE"))) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(async delegate
            {
                for (int essai = 0; essai < 20; essai++)
                {
                    await Task.Delay(500);
                    if (_sondeLancee) return;
                    Window fenetre = host.MainWindow;
                    if (fenetre == null) continue;
                    System.Reflection.MethodInfo ouvrir = fenetre.GetType().GetMethod("OpenByTitle");
                    if (ouvrir != null) ouvrir.Invoke(fenetre, new object[] { "Carte de démo" });
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void LancerSondeSiDemandee()
        {
            if (_sondeLancee) return;
            string chemin = Environment.GetEnvironmentVariable("MENTALO_SONDE");
            if (string.IsNullOrEmpty(chemin)) return;
            _sondeLancee = true;
            DeroulerSonde(chemin);
        }

        private static string Echapper(string texte)
        {
            return (texte ?? "").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private Rect RectDe(Noeud noeud)
        {
            int index = _toile.Document.Noeuds.IndexOf(noeud);
            List<Rect> rects = _toile.RectanglesNoeuds();
            return index >= 0 && index < rects.Count ? rects[index] : new Rect();
        }

        /// <summary>Un champ de texte de Marabook hors de la toile (la recherche de la
        /// Pile, par exemple) : une carte n'a plus ni synopsis ni notes dans le rail.</summary>
        private TextBox TrouverChampExterne()
        {
            foreach (Visual visuel in _host.MainWindow.GetVisualDescendants())
            {
                TextBox champ = visuel as TextBox;
                if (champ == null || !champ.IsEffectivelyVisible || champ.Bounds.Width < 40) continue;
                bool dedans = false;
                Visual parent = champ;
                while (parent != null) { if (ReferenceEquals(parent, _root)) { dedans = true; break; } parent = parent.GetVisualParent(); }
                if (!dedans) return champ;
            }
            return null;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

        /// <summary>Un clic gauche réel (Win32) au centre du contrôle — un Focus()
        /// par programme ne reproduit pas tout ce qu'un clic fait.</summary>
        private static void ClicReel(Control controle)
        {
            ClicReelA(controle, new Point(controle.Bounds.Width / 2, controle.Bounds.Height / 2));
        }

        private static void ClicReelA(Control controle, Point local)
        {
            PixelPoint point = controle.PointToScreen(local);
            SetCursorPos(point.X, point.Y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // LEFTDOWN
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // LEFTUP
        }

        /// <summary>Le caret en fin de texte, sans sélection (CommencerEdition sélectionne tout : taper remplacerait).</summary>
        private static void PlacerCaretFin(TextBox editeur)
        {
            int fin = (editeur.Text ?? "").Length;
            editeur.SelectionStart = fin;
            editeur.SelectionEnd = fin;
            editeur.CaretIndex = fin;
        }

        private IInputElement FocusActuel()
        {
            TopLevel sommet = TopLevel.GetTopLevel(_root);
            return sommet == null || sommet.FocusManager == null ? null : sommet.FocusManager.GetFocusedElement();
        }

        /// <summary>Les descendants LOGIQUES (un contrôle pas encore affiché n'a pas d'arbre visuel) qui répondent au critère.</summary>
        private static int CompterDescendants(Control racine, Func<Control, bool> critere)
        {
            int n = 0;
            foreach (ILogical logique in racine.GetLogicalDescendants())
            {
                Control controle = logique as Control;
                if (controle != null && critere(controle)) n++;
            }
            return n;
        }

        private static int CompterVisibles(Control racine, Func<Control, bool> critere)
        {
            int n = 0;
            foreach (Visual visuel in racine.GetVisualDescendants())
            {
                Control controle = visuel as Control;
                if (controle != null && controle.IsEffectivelyVisible && critere(controle)) n++;
            }
            return n;
        }

        private async void DeroulerSonde(string chemin)
        {
            StringBuilder log = new StringBuilder();
            int verifications = 0, echecs = 0;
            Action<bool, string> verifier = delegate(bool ok, string libelle)
            {
                verifications++;
                if (!ok) echecs++;
                log.AppendLine((ok ? "ok  " : "KO  ") + libelle);
            };
            Action ecrire = delegate { try { File.WriteAllText(chemin, log.ToString()); } catch { } };
            // Chien de garde : quoi qu'il arrive, Marabook quitte.
            Task.Run(async delegate { await Task.Delay(110000); log.AppendLine("KO  chien de garde : la sonde n'a pas fini"); ecrire(); Environment.Exit(3); });
            try
            {
                await Task.Delay(800);
                // Au premier plan, et au-dessus de tout : les vrais clics doivent tomber sur Marabook, pas sur le terminal qui l'a lancé.
                _host.MainWindow.Topmost = true;
                _host.MainWindow.Activate();
                try { IPlatformHandle poignee = _host.MainWindow.TryGetPlatformHandle(); if (poignee != null) SetForegroundWindow(poignee.Handle); } catch { }
                await Task.Delay(300);
                verifier(_toile.Document.Noeuds.Count == 5, "la carte de démo est chargée (" + _toile.Document.Noeuds.Count + " boîtes)");
                verifier(_panneaux.Vignette.IsVisible == ParametresGlobaux.VignetteTableau, "la vignette du tableau suit le réglage (" + ParametresGlobaux.VignetteTableau + ")");
                verifier(ParametresGlobaux.ActionPour(Key.D, KeyModifiers.Control) == null && ParametresGlobaux.ActionPour(Key.F2, KeyModifiers.None) == null
                    && ParametresGlobaux.ActionPour(Key.D1, KeyModifiers.Control) == null, "Ctrl+D, F2 et les modes n'ont plus de raccourci");
                verifier(ParametresGlobaux.ActionPour(Key.Z, KeyModifiers.Control) == "annuler", "Ctrl+Z reste Annuler");
                verifier(_dupliquer != null && !_dupliquer.IsEnabled, "sans sélection, Dupliquer est grisé");

                // La boîte qui a le plus de lignes : édition, un retour à la ligne, du texte.
                Noeud premier = null;
                foreach (Noeud n in _toile.Document.Noeuds)
                    if (premier == null || n.TextePlat.Split('\n').Length > premier.TextePlat.Split('\n').Length) premier = n;
                _toile.AllerAuNoeud(premier);
                await Task.Delay(300);
                verifier(_dupliquer.IsEnabled, "une boîte sélectionnée : Dupliquer s'active");
                verifier(CompterVisibles(_inspecteur, delegate(Control c) { return c is ComboBox; }) >= 7, "l'inspecteur du rail montre les réglages de la boîte et du texte");
                string avant = premier.TextePlat;
                Rect r0 = RectDe(premier);
                _toile.EditerSelection();
                await Task.Delay(500);
                verifier(_toile.EditionEnCours, "l'édition de la boîte est ouverte");
                TextBox editeur = FocusActuel() as TextBox;
                verifier(editeur != null && editeur.NewLine == "\n", "le TextBox d'édition insère « \\n » (pas « \\r\\n »)");
                if (editeur != null)
                {
                    PlacerCaretFin(editeur);
                    editeur.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = editeur });
                    editeur.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "fin", Source = editeur });
                    await Task.Delay(200);
                }
                // Un vrai clic dans un champ du rail (le Synopsis d'un autre élément, ou les notes).
                TextBox externe = TrouverChampExterne();
                verifier(externe != null, "un champ de texte du rail est visible hors de la toile");
                if (externe != null) ClicReel(externe);
                await Task.Delay(900);
                Rect r2 = RectDe(premier);
                verifier(!_toile.EditionEnCours, "le clic dans le champ du rail termine l'édition");
                verifier(premier.TextePlat == avant + "\nfin", "le texte validé porte « \\nfin » sans « \\r » (" + Echapper(premier.TextePlat) + ")");
                verifier(r2.Height - r0.Height > 8 && r2.Height - r0.Height < 30, "la boîte grandit d'une seule ligne (" + r0.Height + " → " + r2.Height + ")");
                verifier(externe != null && externe.IsFocused, "le champ cliqué garde le clavier (la toile ne le reprend pas)");

                // Un vrai clic sur le fond de la toile clôt l'édition (le TextBox ne reste pas ouvert).
                _toile.AllerAuNoeud(premier);
                await Task.Delay(200);
                _toile.EditerSelection();
                await Task.Delay(400);
                TextBox editeur2 = FocusActuel() as TextBox;
                if (editeur2 != null)
                {
                    PlacerCaretFin(editeur2);
                    editeur2.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = " bis", Source = editeur2 });
                    await Task.Delay(150);
                }
                ClicReelA(_toile, new Point(25, 25));
                await Task.Delay(700);
                verifier(!_toile.EditionEnCours, "un clic sur le fond de la toile termine l'édition");
                verifier(premier.TextePlat.EndsWith("fin bis"), "…et garde le texte tapé (" + Echapper(premier.TextePlat) + ")");

                // Quitter la carte en pleine édition : Marabook demande Save, qui valide le texte.
                _toile.AllerAuNoeud(premier);
                await Task.Delay(200);
                _toile.EditerSelection();
                await Task.Delay(400);
                TextBox editeur3 = FocusActuel() as TextBox;
                if (editeur3 != null)
                {
                    PlacerCaretFin(editeur3);
                    editeur3.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = " ter", Source = editeur3 });
                    await Task.Delay(150);
                }
                verifier(IsDirty, "une édition ouverte rend la carte « modifiée »");
                byte[] sauve = Save(null);
                Noeud nSauve = Fichier.Charger(sauve).TrouverNoeud(premier.Id);
                // Depuis le 1/11 (Rémi), enregistrer ne ferme plus l'éditeur : le texte tapé part dans le .tea, le caret reste.
                verifier(_toile.EditionEnCours && nSauve != null && nSauve.TextePlat.EndsWith("fin bis ter"),
                    "enregistrer en pleine édition garde le texte sans fermer l'éditeur (" + (nSauve == null ? "-" : Echapper(nSauve.TextePlat)) + ")");
                _toile.ValiderEditionEnCours();
                await Task.Delay(200);
                verifier(!_toile.EditionEnCours && premier.TextePlat.EndsWith("fin bis ter"), "valider l'édition ferme l'éditeur et garde le texte");

                // Lien vers une fiche, puis tuile, dans la boîte ; aller-retour .tea.
                _toile.AllerAuNoeud(premier);
                await Task.Delay(200);
                HostItem fiche = null, ecrit = null;
                foreach (HostItem item in _host.Items())
                {
                    if (fiche == null && item.Kind == "fiche") fiche = item;
                    if (ecrit == null && item.Kind == "texte") ecrit = item;
                }
                verifier(fiche != null && ecrit != null, "le projet offre une fiche et un écrit à lier");
                int piecesAvant = premier.Fichiers.Count;
                Rect r3 = RectDe(premier);
                if (fiche != null) _toile.AttacherLienHote(fiche.Id, fiche.Title, null);
                await Task.Delay(300);
                Rect r4 = RectDe(premier);
                verifier(premier.Fichiers.Count == piecesAvant + 1 && premier.Fichiers[premier.Fichiers.Count - 1].EstLienHote, "le lien vers la fiche est attaché à la boîte");
                verifier(r4.Height > r3.Height + 10, "la ligne de lien se voit dans la boîte (" + r3.Height + " → " + r4.Height + ")");
                if (ecrit != null) _toile.AttacherLienHote(ecrit.Id, ecrit.Title, "normal");
                await Task.Delay(400);
                Rect r5 = RectDe(premier);
                verifier(premier.Fichiers.Count == piecesAvant + 2 && premier.Fichiers[premier.Fichiers.Count - 1].Affichage == "normal", "la tuile de l'écrit est attachée");
                verifier(r5.Height > r4.Height + 30 && r5.Width >= 200, "la tuile se voit dans la boîte (" + r4.Height + " → " + r5.Height + ", largeur " + r5.Width + ")");
                verifier(_toile.Historique.PeutAnnuler, "le lien et la tuile sont annulables");
                byte[] tea = Fichier.EnregistrerEnMemoire(_toile.Document, null);
                DocumentCarte relu = Fichier.Charger(tea);
                Noeud reluNoeud = relu.TrouverNoeud(premier.Id);
                verifier(reluNoeud != null && reluNoeud.Fichiers.Count == premier.Fichiers.Count
                    && reluNoeud.Fichiers[reluNoeud.Fichiers.Count - 1].EstLienHote
                    && reluNoeud.Fichiers[reluNoeud.Fichiers.Count - 1].Affichage == "normal"
                    && reluNoeud.Fichiers[reluNoeud.Fichiers.Count - 1].CibleHote == ecrit.Id, "le .tea garde les liens vers l'hôte et leur présentation");
                verifier(reluNoeud != null && reluNoeud.TextePlat.IndexOf('\r') < 0, "le .tea ne porte aucun « \\r »");

                // Une photo de la fenêtre (la boîte sélectionnée, l'inspecteur dans le rail) : <sonde>.png.
                _toile.AllerAuNoeud(premier);
                await Task.Delay(400);
                try
                {
                    Window fenetre = _host.MainWindow;
                    PixelSize taille = new PixelSize((int)fenetre.Bounds.Width, (int)fenetre.Bounds.Height);
                    using (Avalonia.Media.Imaging.RenderTargetBitmap image = new Avalonia.Media.Imaging.RenderTargetBitmap(taille, new Vector(96, 96)))
                    {
                        image.Render(fenetre);
                        image.Save(chemin + ".png");
                    }
                    verifier(File.Exists(chemin + ".png"), "la photo de la fenêtre est écrite (" + chemin + ".png)");
                }
                catch (Exception error) { verifier(false, "photo de la fenêtre : " + error.Message); }

                // Le rail (27/10) : note et étiquettes actives pour une boîte, les sections titrées et séparées par des filets.
                verifier(_note.IsEnabled && _etiquettes.IsEnabled && _note.IsEffectivelyVisible, "une boîte sélectionnée : Note et Étiquettes sont actifs sous la section Boîte");
                verifier(CompterVisibles(_inspecteur, delegate(Control c) { return c is Border && Equals(c.Tag, "filet-inspecteur"); }) >= 3, "les sections Boîte et Texte sont séparées par des filets (et un filet clôt l'inspecteur)");
                verifier(!_grouper.IsEffectivelyVisible, "une seule boîte : pas de « Grouper »");
                verifier(CompterVisibles(_inspecteur, delegate(Control c) { TextBlock t = c as TextBlock; return t != null && t.FontWeight == Avalonia.Media.FontWeight.SemiBold && (t.Text == "Boîte" || t.Text == "Texte"); }) == 2, "les titres Boîte et Texte sont en demi-gras");

                // Sélection multiple : le compte et « Grouper » avant la Boîte.
                _toile.SelectionnerTout();
                await Task.Delay(300);
                verifier(_toile.NbSelectionnes == 5, "tout sélectionner : 5 boîtes");
                verifier(_grouper.IsEffectivelyVisible && _grouper.IsEnabled, "sélection multiple : « Grouper » se montre et s'active");
                verifier(CompterVisibles(_inspecteur, delegate(Control c) { TextBlock t = c as TextBlock; return t != null && t.Text == "5 éléments sélectionnés"; }) == 1, "le rail dit « 5 éléments sélectionnés »");

                // Un groupe : son compte, Dégrouper (orangé) et Plier / Déplier avant ses réglages.
                Groupe actes = _toile.Document.Groupes[0];
                _toile.SelectionnerGroupe(actes);
                await Task.Delay(300);
                verifier(_degrouper.IsEffectivelyVisible && _degrouper.IsEnabled && !_grouper.IsEffectivelyVisible, "groupe sélectionné : « Dégrouper » se montre, « Grouper » non");
                verifier(_plier.IsEffectivelyVisible && !_deplier.IsEffectivelyVisible, "groupe déplié : le bouton propose de plier");
                verifier(CompterVisibles(_inspecteur, delegate(Control c) { TextBlock t = c as TextBlock; return t != null && t.Text == "Groupe de 3 éléments"; }) == 1, "le rail dit « Groupe de 3 éléments »");
                _toile.BasculerPliageCible();
                await Task.Delay(300);
                verifier(actes.Plie && _deplier.IsEffectivelyVisible && !_plier.IsEffectivelyVisible, "plié : le bouton propose de déplier");
                _toile.BasculerPliageCible();
                await Task.Delay(200);
                verifier(!actes.Plie, "déplié de nouveau");

                // La recherche dans la barre des menus : Ctrl+F (le geste) y amène le curseur, taper va à la boîte.
                Executer("rechercher", false);
                await Task.Delay(200);
                verifier(ReferenceEquals(FocusActuel(), _champRecherche), "« rechercher » met le curseur dans le champ de la barre des menus");
                _champRecherche.Text = "Keira";
                await Task.Delay(300);
                verifier(_compteurRecherche.Text == "1/1" && _toile.NoeudSelectionne != null && _toile.NoeudSelectionne.TextePlat.StartsWith("Keira"), "taper « Keira » compte 1/1 et sélectionne la boîte");
                _champRecherche.Text = "";
                _toile.Focus();

                // La mise en forme du texte (1/11) : toutes les propriétés, dans les trois situations, par l'API et par le rail.
                await SonderMiseEnForme(verifier);

                // La vignette d'une tuile prend toute la largeur demandée.
                Control vignette = Vignette.Dessiner(tea, 192, 96);
                verifier(vignette != null && vignette.Width == 192 && vignette.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Stretch, "la vignette fait la largeur demandée (192) et s'étire");

                // L'onglet des Préférences : aux formes de Marabook (filets, pas d'onglets dans l'onglet), les raccourcis listés.
                Control onglet = new PreferencesTab(_host).Build();
                onglet.Measure(new Size(700, 600));
                verifier(onglet is ScrollViewer && CompterDescendants(onglet, delegate(Control c) { return c is TabControl; }) == 0, "l'onglet Mental-O n'a plus d'onglets imbriqués");
                verifier(CompterDescendants(onglet, delegate(Control c) { return c is Border && Equals(c.Tag, "caption-divider"); }) == 2, "deux filets d'intertitre (Apparence, Raccourcis)");
                verifier(CompterDescendants(onglet, delegate(Control c) { return c is TextBox; }) >= 15, "les raccourcis de la toile sont listés");

                // Thème de carte : la couleur du texte des boîtes existantes suit.
                string ancienne = _toile.Document.CouleurParDefaut;
                ThemeCarte ardoise = ThemeCarte.Liste()[1];
                _toile.AppliquerThemeCarte(ardoise, true);
                await Task.Delay(300);
                int segments = 0, recolores = 0;
                foreach (Noeud n in _toile.Document.Noeuds)
                    foreach (Segment seg in n.Segments) { segments++; if (string.Equals(seg.Couleur, ardoise.CouleurTexte, StringComparison.OrdinalIgnoreCase)) recolores++; }
                verifier(segments > 0 && recolores == segments, "thème Ardoise : tout le texte passe de " + ancienne + " à " + ardoise.CouleurTexte + " (" + recolores + "/" + segments + ")");

                // Ardoise : éditer une boîte ne doit pas ouvrir un champ blanc (le cadre du gabarit reste transparent).
                _toile.AllerAuNoeud(premier);
                await Task.Delay(200);
                _toile.EditerSelection();
                await Task.Delay(500);
                TextBox editeurArdoise = FocusActuel() as TextBox;
                int opaques = 0, cadres = 0;
                if (editeurArdoise != null)
                    foreach (Visual v in editeurArdoise.GetVisualDescendants())
                    {
                        Border cadre = v as Border;
                        if (cadre == null) continue;
                        cadres++;
                        Avalonia.Media.ISolidColorBrush fondCadre = cadre.Background as Avalonia.Media.ISolidColorBrush;
                        if (fondCadre != null && fondCadre.Color.A > 0) opaques++;
                    }
                verifier(editeurArdoise != null && cadres > 0 && opaques == 0, "thème Ardoise : le champ d'édition reste transparent (" + opaques + " cadre(s) opaque(s) sur " + cadres + ")");
                _toile.ValiderEditionEnCours();
            }
            catch (Exception error) { echecs++; log.AppendLine("KO  exception : " + error); }
            log.AppendLine(echecs == 0 ? "SONDE DLC OK (" + verifications + " vérifications)" : "SONDE DLC KO (" + echecs + " échec(s) sur " + verifications + ")");
            ecrire();
            Environment.Exit(echecs == 0 ? 0 : 1);
        }
    }
}
