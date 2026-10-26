using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Les groupes : cadres, titres, pastilles (pliés), menu contextuel et API
    /// pour la barre des groupes.</summary>
    public partial class ToileVue
    {
        public Groupe GroupeDuNoeud(Noeud noeud)
        {
            foreach (Groupe groupe in Document.Groupes)
                if (groupe.Contient(noeud.Id)) return groupe;
            return null;
        }

        private Groupe TrouverGroupe(object origine)
        {
            foreach (KeyValuePair<Groupe, Rectangle> kv in _visuelsGroupes)
                if (ReferenceEquals(kv.Value, origine)) return kv.Key;
            // Le titre (ou la pastille d'un groupe plié) vaut le cadre.
            foreach (KeyValuePair<Groupe, TextBlock> kv in _titresGroupes)
                if (ReferenceEquals(kv.Value, origine)) return kv.Key;
            return null;
        }

        private void CreerVisuelGroupe(Groupe groupe)
        {
            Rectangle cadre = new Rectangle();
            cadre.RadiusX = 10;
            cadre.RadiusY = 10;
            cadre.StrokeThickness = 1.5;
            cadre.StrokeDashArray = new AvaloniaList<double>(new double[] { 5, 3 });
            cadre.ZIndex = -1; // sous les liens (0) et les boîtes (≥ 1)
            _canvas.Children.Add(cadre);
            _visuelsGroupes[groupe] = cadre;

            TextBlock titre = new TextBlock();
            titre.FontWeight = FontWeight.SemiBold;
            titre.FontSize = 12.5;
            titre.Padding = new Thickness(6, 0, 6, 0);
            // Le fond du canevas en arrière-plan : le titre « coupe » visuellement le trait.
            titre.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF5, 0xF0));
            titre.IsVisible = false;
            titre.ZIndex = -1;
            _canvas.Children.Add(titre);
            _titresGroupes[groupe] = titre;

            AppliquerStyleVisuelGroupe(groupe);
            MettreAJourCadreGroupe(groupe);
        }

        private void AppliquerStyleVisuelGroupe(Groupe groupe)
        {
            Color couleur = Palette.CouleurDepuis(groupe.Couleur, Color.FromRgb(0x8A, 0x94, 0xA8));
            Rectangle cadre;
            if (_visuelsGroupes.TryGetValue(groupe, out cadre))
            {
                cadre.Stroke = new SolidColorBrush(couleur);
                cadre.Fill = new SolidColorBrush(Color.FromArgb(0x14, couleur.R, couleur.G, couleur.B));
                cadre.StrokeDashArray = groupe.BordureTrait == "plein" ? null
                    : groupe.BordureTrait == "points" ? new AvaloniaList<double>(new double[] { 0.1, 2.5 })
                    : new AvaloniaList<double>(new double[] { 5, 3 });
                cadre.StrokeLineCap = PenLineCap.Round;
                double epaisseur = groupe.BordureEpaisseur > 0 ? groupe.BordureEpaisseur : 1.5;
                cadre.StrokeThickness = groupe == _groupeActif ? epaisseur + 1 : epaisseur;
                if (groupe.Plie)
                {
                    // Pastille pleine et nette : c'est un objet, plus un cadre.
                    cadre.Fill = new SolidColorBrush(Color.FromArgb(0x3A, couleur.R, couleur.G, couleur.B));
                    cadre.StrokeDashArray = null;
                }
            }
            TextBlock titre;
            if (_titresGroupes.TryGetValue(groupe, out titre))
            {
                Color couleurTitre = string.IsNullOrEmpty(groupe.TitreCouleur)
                    ? couleur : Palette.CouleurDepuis(groupe.TitreCouleur, couleur);
                titre.Foreground = new SolidColorBrush(couleurTitre);
                Color fondCanevas = CouleurFondEffective();
                titre.Background = groupe.Plie ? Brushes.Transparent
                    : new SolidColorBrush(string.IsNullOrEmpty(groupe.TitreFond)
                        ? fondCanevas : Palette.CouleurDepuis(groupe.TitreFond, fondCanevas));
                titre.FontSize = groupe.TitreTaille > 0 ? groupe.TitreTaille : 12.5;
                titre.FontWeight = groupe.Plie ? FontWeight.Bold : FontWeight.SemiBold;
                titre.Cursor = groupe.Plie ? Ui.CurseurMain : null;
                Ui.Infobulle(titre, groupe.Plie ? "Groupe plié — double-clic pour le déplier" : null);
            }
        }

        private void MettreAJourCadreGroupe(Groupe groupe)
        {
            Rectangle cadre;
            if (!_visuelsGroupes.TryGetValue(groupe, out cadre)) return;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int membres = 0;
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud == null) continue;
                membres++;
                Rect rect = RectangleNoeud(noeud);
                minX = Math.Min(minX, rect.X);
                minY = Math.Min(minY, rect.Y);
                maxX = Math.Max(maxX, rect.Right);
                maxY = Math.Max(maxY, rect.Bottom);
            }
            if (membres == 0)
            {
                cadre.IsVisible = false;
                return;
            }
            cadre.IsVisible = true;
            TextBlock titre;
            _titresGroupes.TryGetValue(groupe, out titre);

            if (groupe.Plie)
            {
                // Pastille-titre : « ▸ Titre · n », ancrée en haut à gauche de l'emprise des boîtes.
                if (titre != null)
                {
                    titre.Text = TextePastille(groupe);
                    titre.IsVisible = true;
                    titre.Measure(Size.Infinity);
                }
                Rect pastille = RectanglePastille(groupe, titre == null ? 60 : titre.DesiredSize.Width);
                Canvas.SetLeft(cadre, pastille.X);
                Canvas.SetTop(cadre, pastille.Y);
                cadre.Width = pastille.Width;
                cadre.Height = pastille.Height;
                if (titre != null)
                {
                    Canvas.SetLeft(titre, pastille.X + 12);
                    Canvas.SetTop(titre, pastille.Y + (pastille.Height - titre.DesiredSize.Height) / 2);
                }
                if (_groupeActif == groupe) MettreAJourGrip();
                MettreAJourLiensDeId(groupe.Id);
                return;
            }

            Canvas.SetLeft(cadre, minX - MargeGroupe);
            Canvas.SetTop(cadre, minY - MargeGroupe);
            cadre.Width = Math.Max(maxX - minX + MargeGroupe * 2, groupe.LargeurFixe);
            cadre.Height = Math.Max(maxY - minY + MargeGroupe * 2, groupe.HauteurFixe);

            if (titre != null)
            {
                // Le titre porte l'avancement des cases à cocher de ses boîtes (« Titre · 3/5 »).
                string progression = ProgressionGroupe(groupe);
                string texteTitre = (groupe.Titre ?? "").Trim();
                if (progression.Length > 0)
                    texteTitre = texteTitre.Length == 0 ? progression : texteTitre + " · " + progression;
                if (texteTitre.Length == 0)
                {
                    titre.IsVisible = false;
                }
                else
                {
                    titre.Text = texteTitre;
                    titre.IsVisible = true;
                    titre.Measure(Size.Infinity);
                    bool enBas = groupe.TitrePosition != null && groupe.TitrePosition.StartsWith("bas");
                    bool aGauche = groupe.TitrePosition != null && groupe.TitrePosition.EndsWith("gauche");
                    double x = aGauche
                        ? Canvas.GetLeft(cadre) + 10
                        : Canvas.GetLeft(cadre) + cadre.Width - titre.DesiredSize.Width - 10;
                    double y = (enBas ? Canvas.GetTop(cadre) + cadre.Height : Canvas.GetTop(cadre))
                        - titre.DesiredSize.Height / 2;
                    Canvas.SetLeft(titre, x);
                    Canvas.SetTop(titre, y);
                }
            }
            if (_groupeActif == groupe) MettreAJourGrip();
            MettreAJourLiensDeId(groupe.Id); // le cadre peut être une extrémité de lien
        }

        private Size TailleMinimaleGroupe(Groupe groupe)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int membres = 0;
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud == null) continue;
                membres++;
                Rect rect = RectangleNoeud(noeud);
                minX = Math.Min(minX, rect.X);
                minY = Math.Min(minY, rect.Y);
                maxX = Math.Max(maxX, rect.Right);
                maxY = Math.Max(maxY, rect.Bottom);
            }
            if (membres == 0) return new Size(0, 0);
            return new Size(maxX - minX + MargeGroupe * 2, maxY - minY + MargeGroupe * 2);
        }

        public void DefinirTailleGroupe(Groupe groupe, double largeur, double hauteur)
        {
            groupe.LargeurFixe = largeur;
            groupe.HauteurFixe = hauteur;
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
        }

        private void MettreAJourGroupesDe(Noeud noeud)
        {
            foreach (Groupe groupe in Document.Groupes)
                if (groupe.Contient(noeud.Id)) MettreAJourCadreGroupe(groupe);
        }

        public void AjouterGroupe(Groupe groupe)
        {
            Document.Groupes.Add(groupe);
            CreerVisuelGroupe(groupe);
            if (groupe.Plie) AppliquerVisibilitePliage(groupe);
            SignalerModification();
        }

        public void RetirerGroupe(Groupe groupe)
        {
            // Un groupe plié qui disparaît rend ses boîtes visibles.
            if (groupe.Plie)
            {
                groupe.Plie = false;
                AppliquerVisibilitePliage(groupe);
                groupe.Plie = true; // l'annulation (re-création) le retrouvera plié
            }
            Rectangle cadre;
            if (_visuelsGroupes.TryGetValue(groupe, out cadre))
            {
                _canvas.Children.Remove(cadre);
                _visuelsGroupes.Remove(groupe);
            }
            TextBlock titre;
            if (_titresGroupes.TryGetValue(groupe, out titre))
            {
                _canvas.Children.Remove(titre);
                _titresGroupes.Remove(groupe);
            }
            if (_groupeActif == groupe)
            {
                _groupeActif = null;
                MettreAJourGrip();
            }
            Document.Groupes.Remove(groupe);
            SignalerModification();
        }

        public void RetirerIdsDuGroupe(Groupe groupe, List<string> ids)
        {
            foreach (string id in ids)
                groupe.NoeudIds.Remove(id);
            if (groupe.Plie) RecalerVisibiliteMembres(groupe, ids);
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
        }

        public void AjouterIdsAuGroupe(Groupe groupe, List<string> ids)
        {
            foreach (string id in ids)
                if (!groupe.NoeudIds.Contains(id)) groupe.NoeudIds.Add(id);
            if (groupe.Plie) RecalerVisibiliteMembres(groupe, ids);
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
        }

        public void AppliquerStyleGroupe(Groupe groupe, string couleur, string trait, double epaisseur)
        {
            groupe.Couleur = couleur;
            groupe.BordureTrait = trait;
            groupe.BordureEpaisseur = epaisseur;
            AppliquerStyleVisuelGroupe(groupe);
            SignalerModification();
            SignalerFormatCible();
        }

        public void AppliquerStyleTitreGroupe(Groupe groupe, string couleurTitre, string fondTitre, double taille)
        {
            groupe.TitreCouleur = couleurTitre;
            groupe.TitreFond = fondTitre;
            groupe.TitreTaille = taille;
            AppliquerStyleVisuelGroupe(groupe);
            MettreAJourCadreGroupe(groupe); // la taille du titre change sa position
            SignalerModification();
            SignalerFormatCible();
        }

        public void AppliquerTitreGroupe(Groupe groupe, string titre, string position)
        {
            groupe.Titre = titre ?? "";
            groupe.TitrePosition = string.IsNullOrEmpty(position) ? "haut-droite" : position;
            MettreAJourCadreGroupe(groupe);
            SignalerModification();
            SignalerFormatCible();
        }

        // --- API pour la barre des groupes -------------------------------------------------

        public Groupe GroupeSelectionne { get { return _groupeActif; } }

        public void ChangerCouleurGroupeSelection(string hexa)
        {
            if (_groupeActif == null || _mode == "lecture" || _groupeActif.Couleur == hexa) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, _groupeActif,
                hexa, _groupeActif.BordureTrait, _groupeActif.BordureEpaisseur));
        }

        public void ChangerTraitGroupeSelection(string trait)
        {
            if (_groupeActif == null || _mode == "lecture" || _groupeActif.BordureTrait == trait) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, _groupeActif,
                _groupeActif.Couleur, trait, _groupeActif.BordureEpaisseur));
        }

        public void ChangerEpaisseurGroupeSelection(double epaisseur)
        {
            if (_groupeActif == null || _mode == "lecture" || _groupeActif.BordureEpaisseur == epaisseur) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, _groupeActif,
                _groupeActif.Couleur, _groupeActif.BordureTrait, epaisseur));
        }

        /// <summary>Style du titre : null = valeur inchangée ; "" = « hériter » (cadre / fond du tableau).</summary>
        public void ChangerStyleTitreGroupeSelection(string couleurTitre, string fondTitre, double? taille)
        {
            if (_groupeActif == null || _mode == "lecture") return;
            string couleur = couleurTitre ?? _groupeActif.TitreCouleur;
            string fond = fondTitre ?? _groupeActif.TitreFond;
            double nouvelleTaille = taille.HasValue ? taille.Value : _groupeActif.TitreTaille;
            if (couleur == _groupeActif.TitreCouleur && fond == _groupeActif.TitreFond
                && nouvelleTaille == _groupeActif.TitreTaille) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleTitreGroupe(this, _groupeActif,
                couleur, fond, nouvelleTaille));
        }

        public async void ChoisirCouleurTitreGroupePersonnalisee(bool pourLeFond)
        {
            if (_groupeActif == null || _mode == "lecture") return;
            string hexa = await OuvrirSelecteurCouleur(null);
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            if (pourLeFond) ChangerStyleTitreGroupeSelection(null, hexa, null);
            else ChangerStyleTitreGroupeSelection(hexa, null, null);
            SignalerFormatCible();
        }

        public async void ChoisirCouleurGroupePersonnalisee()
        {
            if (_groupeActif == null) return;
            string hexa = await OuvrirSelecteurCouleur(_groupeActif.Couleur);
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            ChangerCouleurGroupeSelection(hexa);
            SignalerFormatCible();
        }

        public void ChangerTitreGroupeSelection(string titre, string position)
        {
            if (_groupeActif == null) return;
            if ((titre ?? "") == (_groupeActif.Titre ?? "") && position == _groupeActif.TitrePosition) return;
            Historique.ExecuterEtPousser(new CommandeModifierTitreGroupe(this, _groupeActif, titre ?? "", position));
        }

        public void ReajusterGroupeSelection()
        {
            if (_groupeActif == null) return;
            if (_groupeActif.LargeurFixe > 0 || _groupeActif.HauteurFixe > 0)
                Historique.ExecuterEtPousser(new CommandeRedimensionnerGroupe(this, _groupeActif,
                    _groupeActif.LargeurFixe, _groupeActif.HauteurFixe, 0, 0));
        }

        // --- API pour l'inspecteur ---------------------------------------------------------

        public Noeud NoeudSelectionne { get { return _selection == null ? null : _selection.Modele; } }
        public int NbSelectionnes { get { return _selectionnes.Count; } }
        public Rect RectangleDuNoeud(Noeud noeud) { return RectangleNoeud(noeud); }

        /// <summary>Le cadre mesuré d'un groupe, ou un rectangle vide s'il n'est pas affiché.</summary>
        public Rect RectangleDuGroupe(Groupe groupe)
        {
            Rectangle cadre;
            if (_visuelsGroupes.TryGetValue(groupe, out cadre) && cadre.IsVisible)
                return new Rect(Canvas.GetLeft(cadre), Canvas.GetTop(cadre), cadre.Width, cadre.Height);
            return default(Rect);
        }

        /// <summary>L'emprise du contenu (boîtes et cadres de groupes), sans marge ;
        /// un rectangle vide quand la carte l'est.</summary>
        public Rect BornesContenu()
        {
            bool premier = true;
            Rect bornes = default(Rect);
            foreach (Noeud n in Document.Noeuds)
            {
                Rect r = RectangleNoeud(n);
                bornes = premier ? r : bornes.Union(r);
                premier = false;
            }
            foreach (Groupe g in Document.Groupes)
            {
                Rect cadre = RectangleDuGroupe(g);
                if (EstVide(cadre)) continue;
                bornes = premier ? cadre : bornes.Union(cadre);
                premier = false;
            }
            return bornes;
        }

        public void GrouperSelection()
        {
            if (_selectionnes.Count < 2) return;
            Groupe groupe = Document.NouveauGroupe();
            Dictionary<Groupe, List<string>> retraits = new Dictionary<Groupe, List<string>>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                groupe.NoeudIds.Add(v.Modele.Id);
                Groupe ancien = GroupeDuNoeud(v.Modele);
                if (ancien != null)
                {
                    if (!retraits.ContainsKey(ancien)) retraits[ancien] = new List<string>();
                    retraits[ancien].Add(v.Modele.Id);
                }
            }
            Historique.ExecuterEtPousser(new CommandeCreerGroupe(this, groupe, retraits));
        }

        public void DegrouperSelection()
        {
            List<Groupe> groupes = new List<Groupe>();
            if (_groupeActif != null) groupes.Add(_groupeActif);
            foreach (NoeudVisuel v in _selectionnes)
            {
                Groupe groupe = GroupeDuNoeud(v.Modele);
                if (groupe != null && !groupes.Contains(groupe)) groupes.Add(groupe);
            }
            if (groupes.Count == 0) return;
            if (groupes.Count == 1)
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerGroupe(this, groupes[0]));
                return;
            }
            List<ICommande> commandes = new List<ICommande>();
            foreach (Groupe groupe in groupes)
                commandes.Add(new CommandeSupprimerGroupe(this, groupe));
            Historique.ExecuterEtPousser(new CommandeComposite(commandes));
        }

        private void OuvrirMenuGroupe(Groupe groupe)
        {
            ContextMenu menu = new ContextMenu();
            MenuItem couleur = Ui.SousMenu("Couleur du cadre");
            for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                AjouterChoixCouleurGroupe(couleur, groupe, Palette.Couleurs[i, 0], Palette.Couleurs[i, 1]);
            if (Document.CouleursPersonnalisees.Count > 0)
            {
                couleur.Items.Add(Ui.Separateur());
                foreach (string hexa in Document.CouleursPersonnalisees)
                    AjouterChoixCouleurGroupe(couleur, groupe, hexa, hexa);
            }
            couleur.Items.Add(Ui.Separateur());
            couleur.Items.Add(Ui.Element("Personnalisée…", async delegate
            {
                string hexa = await OuvrirSelecteurCouleur(groupe.Couleur);
                if (hexa == null) return;
                if (Document.AjouterCouleurPersonnalisee(hexa))
                    SignalerModification();
                Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, groupe,
                    hexa, groupe.BordureTrait, groupe.BordureEpaisseur));
            }));
            menu.Items.Add(couleur);

            MenuItem reajuster = Ui.Element("Réajuster au contenu", delegate
            {
                if (groupe.LargeurFixe > 0 || groupe.HauteurFixe > 0)
                    Historique.ExecuterEtPousser(new CommandeRedimensionnerGroupe(this, groupe,
                        groupe.LargeurFixe, groupe.HauteurFixe, 0, 0));
            });
            reajuster.IsEnabled = groupe.LargeurFixe > 0 || groupe.HauteurFixe > 0;
            menu.Items.Add(reajuster);

            MenuItem plier = Ui.Element(groupe.Plie ? "Déplier le groupe" : "Plier le groupe",
                delegate { BasculerPliageGroupe(groupe); });
            Ui.Infobulle(plier, "Plié, le groupe se réduit à une pastille-titre ; ses boîtes et leurs liens intérieurs se cachent");
            menu.Items.Add(plier);
            menu.Items.Add(ConstruireMenuEtiquettes());
            MenuItem ordre = Ui.Element(groupe.OrdrePresentation > 0
                    ? "Ordre dans la présentation… (" + groupe.OrdrePresentation + ")"
                    : "Ordre dans la présentation…",
                delegate { DemanderOrdrePresentation(groupe); });
            Ui.Infobulle(ordre, "En mode présentation (F5), chaque groupe est une diapositive ; sans rang, l'ordre de lecture s'applique");
            menu.Items.Add(ordre);

            menu.Items.Add(Ui.Separateur());
            menu.Items.Add(Ui.Element("Dégrouper", delegate
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerGroupe(this, groupe));
            }, false, "Ctrl+Shift+G"));
            Ui.OuvrirMenu(menu, this);
        }

        private void AjouterChoixCouleurGroupe(MenuItem parent, Groupe groupe, string nom, string hexa)
        {
            string capture = hexa;
            MenuItem element = Ui.Element(nom, delegate
            {
                Historique.ExecuterEtPousser(new CommandeModifierStyleGroupe(this, groupe,
                    capture, groupe.BordureTrait, groupe.BordureEpaisseur));
            }, string.Equals(groupe.Couleur, hexa, StringComparison.OrdinalIgnoreCase));
            element.Icon = Ui.Pastille(hexa, 14);
            parent.Items.Add(element);
        }
    }
}
