using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mentalo.Modele;
using Mentalo.Parametres;

namespace Mentalo.Vue
{
    /// <summary>Le contenu des panneaux flottants qui accompagnent la toile
    /// (inspecteur, filtre par étiquettes, Tust des raccourcis), partagé par
    /// Mental-O seul et par le DLC Marabook (26/10/2026).</summary>
    public static class PanneauxToile
    {
        /// <summary>Le texte de l'inspecteur de sélection.</summary>
        public static string TexteInspecteur(ToileVue toile)
        {
            Lien lien = toile.LienSelectionne;
            Groupe groupe = toile.GroupeSelectionne;
            Noeud noeud = toile.NoeudSelectionne;
            if (lien != null)
            {
                return string.Format(
                    "Lien\nCouleur : {0}\nÉpaisseur : {1} · Trait : {2}\nFlèche : {3} · Tracé : {4}{5}",
                    lien.Couleur, lien.Epaisseur, lien.Trait, lien.Fleche, lien.Trace,
                    string.IsNullOrEmpty(lien.Etiquette) ? "" : "\nÉtiquette : « " + lien.Etiquette + " »");
            }
            if (groupe != null)
            {
                return string.Format(
                    "Groupe{0}\n{1} boîte(s) · Couleur : {2}",
                    string.IsNullOrEmpty(groupe.Titre) ? "" : " « " + groupe.Titre + " »",
                    groupe.NoeudIds.Count, groupe.Couleur);
            }
            if (noeud != null)
            {
                Rect rect = toile.RectangleDuNoeud(noeud);
                string type = noeud.EstImage ? "Boîte-image" : "Boîte";
                if (noeud.Fichiers.Count > 0)
                    type += string.Format(" · {0} lien{1}", noeud.Fichiers.Count, noeud.Fichiers.Count > 1 ? "s" : "");
                string extra = toile.NbSelectionnes > 1
                    ? string.Format("\n({0} boîtes sélectionnées)", toile.NbSelectionnes) : "";
                if (noeud.Etiquettes.Count > 0) extra += "\nÉtiquettes : " + string.Join(", ", noeud.Etiquettes.ToArray());
                string cases = ToileVue.ProgressionBoite(noeud);
                if (cases.Length > 0) extra += "\nCases cochées : " + cases;
                if (noeud.ANote)
                {
                    string note = noeud.Note.Replace("\r", "").Replace("\n", " ");
                    extra += "\nNote : " + (note.Length > 60 ? note.Substring(0, 60) + "…" : note);
                }
                return string.Format(
                    "{0}\nX : {1:0} · Y : {2:0}\nLargeur : {3:0} · Hauteur : {4:0}{5}",
                    type, rect.X, rect.Y, rect.Width, rect.Height, extra);
            }
            return "Aucune sélection";
        }

        /// <summary>Le panneau de filtre : une case par étiquette du fichier, plus
        /// « sans étiquette ». Dès qu'une case est cochée, les boîtes qui ne
        /// correspondent pas s'estompent. « encre » et « doux » : les pinceaux de l'hôte.</summary>
        public static void RemplirPanneauFiltre(StackPanel contenu, ToileVue toile, Window proprietaire)
        {
            contenu.Children.Clear();
            contenu.Children.Add(new TextBlock
            {
                Text = "Filtre par étiquettes",
                FontWeight = FontWeight.SemiBold,
                Foreground = Chrome.Encre,
                Margin = new Thickness(0, 0, 0, 6)
            });
            HashSet<string> actives = toile.FiltreEtiquettes;
            List<Etiquette> definitions = toile.Document.Etiquettes;
            if (definitions.Count == 0)
                contenu.Children.Add(Ui.TexteDoux("Aucune étiquette dans ce fichier.\nClic droit sur une boîte > Étiquettes."));
            foreach (Etiquette e in definitions)
            {
                string nom = e.Nom;
                StackPanel ligne = new StackPanel { Orientation = Orientation.Horizontal };
                Control pastille = Ui.PastilleRonde(e.Couleur, 11);
                pastille.Margin = new Thickness(0, 0, 6, 0);
                pastille.VerticalAlignment = VerticalAlignment.Center;
                ligne.Children.Add(pastille);
                ligne.Children.Add(new TextBlock { Text = nom, Foreground = Chrome.Encre, VerticalAlignment = VerticalAlignment.Center });
                ligne.Children.Add(new TextBlock
                {
                    Text = " (" + toile.CompterPorteurs(nom) + ")",
                    Foreground = Chrome.TexteDoux, VerticalAlignment = VerticalAlignment.Center
                });
                CheckBox case_ = new CheckBox
                {
                    Content = ligne,
                    IsChecked = actives != null && actives.Contains(nom),
                    Margin = new Thickness(0, 2, 0, 2),
                    Focusable = false,
                    Tag = nom
                };
                case_.Click += delegate { AppliquerFiltreDepuisPanneau(contenu, toile, proprietaire); };
                contenu.Children.Add(case_);
            }
            if (definitions.Count > 0)
            {
                CheckBox sans = new CheckBox
                {
                    Content = new TextBlock { Text = "Sans étiquette", Foreground = Chrome.TexteDoux },
                    IsChecked = actives != null && toile.FiltreSansEtiquette,
                    Margin = new Thickness(0, 6, 0, 2),
                    Focusable = false,
                    Tag = "\u0001sans"
                };
                sans.Click += delegate { AppliquerFiltreDepuisPanneau(contenu, toile, proprietaire); };
                contenu.Children.Add(sans);
            }
            StackPanel boutons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Spacing = 6 };
            Button tout = new Button { Content = "Tout afficher", Focusable = false, Padding = new Thickness(8, 2), IsEnabled = actives != null };
            tout.Click += delegate { toile.DefinirFiltreEtiquettes(null, false); RemplirPanneauFiltre(contenu, toile, proprietaire); };
            boutons.Children.Add(tout);
            Button gerer = new Button { Content = "Gérer…", Focusable = false, Padding = new Thickness(8, 2) };
            gerer.Click += delegate { DialogueEtiquettes.Ouvrir(proprietaire, toile); };
            boutons.Children.Add(gerer);
            contenu.Children.Add(boutons);
        }

        private static void AppliquerFiltreDepuisPanneau(StackPanel contenu, ToileVue toile, Window proprietaire)
        {
            HashSet<string> actives = new HashSet<string>();
            bool sans = false;
            foreach (Control enfant in contenu.Children)
            {
                CheckBox case_ = enfant as CheckBox;
                if (case_ == null || case_.IsChecked != true) continue;
                string nom = case_.Tag as string;
                if (nom == "\u0001sans") { sans = true; continue; }
                if (nom != null) actives.Add(nom);
            }
            if (actives.Count == 0 && !sans) toile.DefinirFiltreEtiquettes(null, false);
            else toile.DefinirFiltreEtiquettes(actives, sans);
            RemplirPanneauFiltre(contenu, toile, proprietaire);
        }

        /// <summary>Le contenu du Tust des raccourcis : les gestes de base, puis les favoris.</summary>
        public static void RemplirTust(StackPanel contenu)
        {
            contenu.Children.Clear();
            contenu.Children.Add(new TextBlock
            {
                Text = "Tust des raccourcis",
                FontWeight = FontWeight.Bold,
                Foreground = Chrome.Encre,
                Margin = new Thickness(0, 0, 0, 6)
            });
            string[] bases =
            {
                "Double-clic : créer une boîte",
                "Poignées bleues : créer un lien",
                "Ctrl+clic / rectangle : sélection multiple",
                "Ctrl+molette : zoom",
                "Espace+glisser : naviguer"
            };
            foreach (string ligne in bases)
                contenu.Children.Add(new TextBlock { Text = ligne, FontSize = 12, Foreground = Chrome.Encre, TextWrapping = TextWrapping.Wrap });
            if (ParametresGlobaux.Favoris.Count > 0)
            {
                contenu.Children.Add(new Border
                {
                    Height = 1,
                    Background = Chrome.Bordure,
                    Margin = new Thickness(0, 6, 0, 6)
                });
                foreach (DefinitionAction action in ParametresGlobaux.Actions)
                {
                    if (!ParametresGlobaux.Favoris.Contains(action.Id)) continue;
                    contenu.Children.Add(new TextBlock
                    {
                        Text = action.Nom + " : " + ParametresGlobaux.AfficherGeste(ParametresGlobaux.Geste(action.Id)),
                        FontSize = 12,
                        Foreground = Chrome.Encre,
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
        }
    }
}
