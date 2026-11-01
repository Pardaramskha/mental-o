using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Les interactions au pointeur : sélection, glisser, rectangle de sélection,
    /// pan, zoom à la molette, poignées de liens, grip de redimensionnement.</summary>
    public partial class ToileVue
    {
        private void PointeurEnfonce(object expediteur, PointerPressedEventArgs e)
        {
            PointerPointProperties proprietes = e.GetCurrentPoint(this).Properties;
            if (proprietes.IsMiddleButtonPressed)
            {
                CacherPoignees();
                CommencerPan(e.GetPosition(this), e.Pointer);
                e.Handled = true;
                return;
            }
            if (proprietes.IsRightButtonPressed)
            {
                SourisDroiteEnfoncee(e);
                return;
            }
            if (!proprietes.IsLeftButtonPressed) return;
            if (EstDansEditeur(e.Source)) return;

            if (ReferenceEquals(e.Source, _grip))
            {
                Focus();
                CommencerRedimension(e.Pointer);
                e.Handled = true;
                return;
            }

            if (ReferenceEquals(e.Source, _poigneeCourbure))
            {
                Focus();
                CommencerCourbure(e.Pointer);
                e.Handled = true;
                return;
            }

            int indicePoignee = IndicePoignee(e.Source);
            if (indicePoignee >= 0)
            {
                Focus();
                CommencerCreationLien(indicePoignee, e.Pointer);
                e.Handled = true;
                return;
            }

            Focus(); // retire le focus d'un éventuel éditeur → valide son texte

            if (_espaceEnfonce)
            {
                CommencerPan(e.GetPosition(this), e.Pointer);
                e.Handled = true;
                return;
            }

            NoeudVisuel noeud = TrouverNoeud(e.Source);
            LienVisuel lien = noeud == null ? TrouverLien(e.Source) : null;
            KeyModifiers modificateurs = e.KeyModifiers;

            if (e.ClickCount == 2)
            {
                if (_mode == "lecture")
                {
                    if (noeud != null) Selectionner(noeud);
                    e.Handled = true;
                    return;
                }
                if (noeud != null)
                {
                    Selectionner(noeud);
                    noeud.CommencerEdition();
                    SignalerFormatCible();
                }
                else if (lien != null)
                {
                    SelectionnerLien(lien);
                }
                else
                {
                    // Double-clic sur une pastille de groupe plié : on le déplie.
                    Groupe groupePlie = TrouverGroupe(e.Source);
                    if (groupePlie != null && groupePlie.Plie) BasculerPliageGroupe(groupePlie);
                    else CreerNoeudA(e.GetPosition(_canvas));
                }
                e.Handled = true;
                return;
            }

            if (noeud != null)
            {
                if ((modificateurs & KeyModifiers.Control) != 0)
                {
                    BasculerSelection(noeud);
                    return;
                }
                if (!_selectionnes.Contains(noeud) || _selectionnes.Count <= 1)
                {
                    Selectionner(noeud);
                }
                else
                {
                    _selection = noeud; // la boîte cliquée devient la principale
                    MettreAJourGrip();
                    SignalerFormatCible();
                }
                noeud.ZIndex = ++_zCompteur;
                if (_mode != "liens" && _mode != "lecture")
                    CommencerDeplacement(e.GetPosition(_canvas), ModelesSelectionnes(), e.Pointer);
            }
            else if (lien != null)
            {
                SelectionnerLien(lien);
            }
            else
            {
                Groupe groupe = TrouverGroupe(e.Source);
                if (groupe != null)
                {
                    // Le cadre se sélectionne seul (personnalisation directe) ;
                    // le glisser déplace quand même tout le bloc.
                    SelectionnerGroupe(groupe);
                    if (_mode != "liens" && _mode != "lecture")
                        CommencerDeplacement(e.GetPosition(_canvas), NoeudsDuGroupe(groupe), e.Pointer);
                }
                else
                {
                    // Rectangle de sélection (un simple clic sans glisser désélectionnera).
                    _rectangleActif = true;
                    _rectangleDepart = e.GetPosition(_canvas);
                    Capturer(e.Pointer);
                }
            }
        }

        public void SelectionnerGroupe(Groupe groupe)
        {
            Selectionner(null); // vide boîtes et liens, remet _groupeActif à null
            _groupeActif = groupe;
            MettreAJourSurbrillanceGroupes();
            MettreAJourGrip();
            SignalerFormatCible();
        }

        private void MettreAJourSurbrillanceGroupes()
        {
            foreach (KeyValuePair<Groupe, Rectangle> kv in _visuelsGroupes)
                AppliquerStyleVisuelGroupe(kv.Key);
        }

        private List<Noeud> ModelesSelectionnes()
        {
            List<Noeud> noeuds = new List<Noeud>();
            foreach (NoeudVisuel v in _selectionnes)
                noeuds.Add(v.Modele);
            return noeuds;
        }

        private List<Noeud> NoeudsDuGroupe(Groupe groupe)
        {
            List<Noeud> noeuds = new List<Noeud>();
            foreach (string id in groupe.NoeudIds)
            {
                Noeud noeud = Document.TrouverNoeud(id);
                if (noeud != null) noeuds.Add(noeud);
            }
            return noeuds;
        }

        private void CommencerDeplacement(Point origine, List<Noeud> noeuds, IPointer pointeur)
        {
            _dragActif = true;
            _deplacementEffectif = false;
            _dragOrigine = origine;
            _dragNoeuds.Clear();
            _dragDeparts.Clear();
            foreach (Noeud n in noeuds)
            {
                _dragNoeuds.Add(n);
                _dragDeparts[n.Id] = new Point(n.X, n.Y);
            }
            CacherPoignees();
            Capturer(pointeur);
        }

        private void SourisDroiteEnfoncee(PointerPressedEventArgs e)
        {
            if (EstDansEditeur(e.Source)) return;
            if (_mode == "lecture")
            {
                NoeudVisuel visee = TrouverNoeud(e.Source);
                if (visee != null) Selectionner(visee);
                e.Handled = true;
                return;
            }
            LienVisuel lien = TrouverLien(e.Source);
            if (lien != null)
            {
                SelectionnerLien(lien);
                OuvrirMenuLien(lien.Modele);
                e.Handled = true;
                return;
            }
            NoeudVisuel noeud = TrouverNoeud(e.Source);
            if (noeud != null)
            {
                if (!_selectionnes.Contains(noeud)) Selectionner(noeud);
                else { _selection = noeud; SignalerFormatCible(); }
                Ui.OuvrirMenu(ConstruireMenuFormatage(false), this);
                e.Handled = true;
                return;
            }
            Groupe groupe = TrouverGroupe(e.Source);
            if (groupe != null)
            {
                SelectionnerGroupe(groupe);
                OuvrirMenuGroupe(groupe);
                e.Handled = true;
            }
        }

        private void PointeurDeplace(object expediteur, PointerEventArgs e)
        {
            if (_panEnCours)
            {
                Point p = e.GetPosition(this);
                _translation.X += p.X - _panDernier.X;
                _translation.Y += p.Y - _panDernier.Y;
                _panDernier = p;
                SignalerVue();
                return;
            }

            if (_courbureEnCours)
            {
                GlisserCourbure(e.GetPosition(_canvas));
                return;
            }

            if (_redimensionEnCours)
            {
                Point p = e.GetPosition(_canvas);
                if (_groupeRedimensionne != null)
                {
                    // Le cadre peut excéder son contenu, jamais être plus petit.
                    Size minimum = TailleMinimaleGroupe(_groupeRedimensionne);
                    DefinirTailleGroupe(_groupeRedimensionne,
                        Math.Max(minimum.Width, p.X - _origineCadreX),
                        Math.Max(minimum.Height, p.Y - _origineCadreY));
                    return;
                }
                if (_noeudRedimensionne == null) return;
                if (_noeudRedimensionne.EstImage)
                {
                    double cibleL = p.X - _noeudRedimensionne.X - _chromeL;
                    double cibleH = p.Y - _noeudRedimensionne.Y - _chromeH;
                    // Proportions conservées par défaut ; Maj pour redimensionner librement.
                    if ((e.KeyModifiers & KeyModifiers.Shift) == 0)
                    {
                        double facteur = Math.Max(cibleL / _departImgL, cibleH / _departImgH);
                        cibleL = _departImgL * facteur;
                        cibleH = _departImgH * facteur;
                    }
                    DefinirTailleImage(_noeudRedimensionne, Math.Max(20, cibleL), Math.Max(20, cibleH));
                }
                else
                {
                    // Boîte texte : la poignée règle la largeur, la hauteur suit le texte.
                    double cible = Math.Max(60, Math.Min(1200, p.X - _noeudRedimensionne.X));
                    DefinirLargeurNoeud(_noeudRedimensionne, cible);
                }
                return;
            }

            if (_lienEnCreation)
            {
                Point p = e.GetPosition(_canvas);
                _lienTemporaire.StartPoint = _departLien;
                _lienTemporaire.EndPoint = p;

                NoeudVisuel cible = NoeudSousPoint(p);
                Groupe cibleGroupe = null;
                if (cible != null && cible.Modele.Id == _lienSourceId) cible = null;
                if (cible == null)
                {
                    cibleGroupe = GroupeSousPoint(p);
                    if (cibleGroupe != null && cibleGroupe.Id == _lienSourceId) cibleGroupe = null;
                }
                if (!ReferenceEquals(cible, _cibleSurvolee))
                {
                    if (_cibleSurvolee != null) _cibleSurvolee.DefinirCibleDeLien(false);
                    _cibleSurvolee = cible;
                    if (_cibleSurvolee != null) _cibleSurvolee.DefinirCibleDeLien(true);
                }
                if (!ReferenceEquals(cibleGroupe, _cibleGroupeSurvole))
                {
                    if (_cibleGroupeSurvole != null) AppliquerStyleVisuelGroupe(_cibleGroupeSurvole);
                    _cibleGroupeSurvole = cibleGroupe;
                    if (_cibleGroupeSurvole != null)
                    {
                        Rectangle cadre;
                        if (_visuelsGroupes.TryGetValue(_cibleGroupeSurvole, out cadre))
                            cadre.Stroke = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
                    }
                }
                return;
            }

            bool gaucheEnfonce = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
            if (_dragActif && gaucheEnfonce)
            {
                Point p = e.GetPosition(_canvas);
                Vector delta = p - _dragOrigine;
                if (!_deplacementEffectif)
                {
                    if (Math.Abs(delta.X) < SeuilDeplacement && Math.Abs(delta.Y) < SeuilDeplacement)
                        return;
                    _deplacementEffectif = true;
                }
                double dx = delta.X, dy = delta.Y;
                if (Mentalo.Parametres.ParametresGlobaux.Magnetisme
                    && (e.KeyModifiers & KeyModifiers.Alt) == 0)
                    AppliquerMagnetisme(ref dx, ref dy);
                else
                    CacherGuides();
                foreach (Noeud n in _dragNoeuds)
                {
                    Point depart = _dragDeparts[n.Id];
                    PositionnerNoeud(n, depart.X + dx, depart.Y + dy);
                }
                return;
            }

            if (_rectangleActif && gaucheEnfonce)
            {
                Point p = e.GetPosition(_canvas);
                Rect zone = RectEntre(_rectangleDepart, p);
                Canvas.SetLeft(_rectangleSelection, zone.X);
                Canvas.SetTop(_rectangleSelection, zone.Y);
                _rectangleSelection.Width = zone.Width;
                _rectangleSelection.Height = zone.Height;
                _rectangleSelection.IsVisible = zone.Width > 3 || zone.Height > 3;
                return;
            }

            MettreAJourPoignees(e);
        }

        private void PointeurRelache(object expediteur, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Middle)
            {
                if (_panEnCours) TerminerPan();
                return;
            }
            if (e.InitialPressMouseButton != MouseButton.Left) return;

            if (_panEnCours)
            {
                TerminerPan();
                return;
            }

            if (_courbureEnCours)
            {
                TerminerCourbure();
                return;
            }

            if (_redimensionEnCours)
            {
                Liberer();
                _redimensionEnCours = false;
                if (_groupeRedimensionne != null)
                {
                    if (_groupeRedimensionne.LargeurFixe != _departGrpL || _groupeRedimensionne.HauteurFixe != _departGrpH)
                        Historique.Pousser(new CommandeRedimensionnerGroupe(this, _groupeRedimensionne,
                            _departGrpL, _departGrpH,
                            _groupeRedimensionne.LargeurFixe, _groupeRedimensionne.HauteurFixe));
                    _groupeRedimensionne = null;
                    return;
                }
                if (_noeudRedimensionne == null) return;
                if (_noeudRedimensionne.EstImage)
                {
                    if (_noeudRedimensionne.ImageLargeur != _departImgL || _noeudRedimensionne.ImageHauteur != _departImgH)
                        Historique.Pousser(new CommandeRedimensionnerImage(this, _noeudRedimensionne,
                            _departImgL, _departImgH,
                            _noeudRedimensionne.ImageLargeur, _noeudRedimensionne.ImageHauteur));
                }
                else if (_noeudRedimensionne.LargeurFixe != _departLargeurFixe)
                {
                    Historique.Pousser(new CommandeRedimensionnerNoeud(this, _noeudRedimensionne,
                        _departLargeurFixe, _noeudRedimensionne.LargeurFixe));
                }
                _noeudRedimensionne = null;
                return;
            }

            if (_lienEnCreation)
            {
                TerminerCreationLien();
                return;
            }

            if (_dragActif)
            {
                Liberer();
                _dragActif = false;
                CacherGuides();
                if (_deplacementEffectif)
                {
                    List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
                    foreach (Noeud n in _dragNoeuds)
                    {
                        Point depart = _dragDeparts[n.Id];
                        if (depart.X == n.X && depart.Y == n.Y) continue;
                        CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                        d.Noeud = n;
                        d.AncienX = depart.X;
                        d.AncienY = depart.Y;
                        d.NouveauX = n.X;
                        d.NouveauY = n.Y;
                        deplacements.Add(d);
                    }
                    if (deplacements.Count > 0)
                        Historique.Pousser(new CommandeDeplacerNoeuds(this, deplacements));
                }
                _dragNoeuds.Clear();
                _dragDeparts.Clear();
                return;
            }

            if (_rectangleActif)
            {
                Liberer();
                _rectangleActif = false;
                _rectangleSelection.IsVisible = false;
                Point p = e.GetPosition(_canvas);
                Rect zone = RectEntre(_rectangleDepart, p);
                bool ajout = (e.KeyModifiers & KeyModifiers.Control) != 0;
                bool retrait = (e.KeyModifiers & KeyModifiers.Alt) != 0; // Alt : désélectionner ce qui est touché
                if (zone.Width < 4 && zone.Height < 4)
                {
                    if (!ajout && !retrait) Selectionner(null);
                    return;
                }
                List<NoeudVisuel> touches = new List<NoeudVisuel>();
                if (ajout || retrait) touches.AddRange(_selectionnes);
                foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
                {
                    if (!kv.Value.IsVisible) continue; // boîtes d'un groupe plié
                    if (!zone.Intersects(RectangleNoeud(kv.Key))) continue;
                    if (retrait) touches.Remove(kv.Value);
                    else if (!touches.Contains(kv.Value)) touches.Add(kv.Value);
                }
                SelectionnerEnsemble(touches, touches.Count > 0 ? touches[0] : null);
            }
        }

        private void MoletteTournee(object expediteur, PointerWheelEventArgs e)
        {
            double delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                double vitesse = Mentalo.Parametres.ParametresGlobaux.FacteurZoom();
                double facteur = delta > 0 ? vitesse : 1.0 / vitesse;
                ZoomVers(_echelle.ScaleX * facteur, e.GetPosition(this));
            }
            else if ((e.KeyModifiers & KeyModifiers.Shift) != 0 || e.Delta.X != 0)
            {
                _translation.X += delta * 80;
            }
            else
            {
                _translation.Y += delta * 80;
            }
            SignalerVue();
            e.Handled = true;
        }

        /// <summary>Activé quand la touche Espace est maintenue (géré par la fenêtre).</summary>
        public void ModePanClavier(bool actif)
        {
            _espaceEnfonce = actif;
            if (!_panEnCours)
                Cursor = actif ? Ui.CurseurMain : Ui.CurseurFleche;
        }

        private void CommencerPan(Point positionEcran, IPointer pointeur)
        {
            _panEnCours = true;
            _panDernier = positionEcran;
            Cursor = Ui.CurseurMain;
            Capturer(pointeur);
        }

        private void TerminerPan()
        {
            _panEnCours = false;
            Cursor = _espaceEnfonce ? Ui.CurseurMain : Ui.CurseurFleche;
            Liberer();
        }

        // ------------------------------------------------------------------ poignées et création de liens

        private void MettreAJourPoignees(PointerEventArgs e)
        {
            if (_mode == "deplacement" || _mode == "lecture")
            {
                CacherPoignees();
                return;
            }
            Point souris = e.GetPosition(_canvas);
            NoeudVisuel noeud = TrouverNoeud(e.Source);
            Groupe groupeSurvole = noeud == null ? TrouverGroupe(e.Source) : null;

            if (noeud != null && !noeud.EnEdition)
            {
                _idSousPoignee = noeud.Modele.Id;
            }
            else if (groupeSurvole != null)
            {
                _idSousPoignee = groupeSurvole.Id; // les cadres de groupes se lient aussi
            }
            else if (_idSousPoignee != null)
            {
                // Zone de grâce : les poignées restent tant que la souris est proche,
                // pour qu'on puisse aller les agripper sans les voir disparaître.
                Rect zone;
                if (!RectangleExtremite(_idSousPoignee, out zone))
                {
                    _idSousPoignee = null;
                }
                else
                {
                    zone = zone.Inflate(MargeGracePoignees);
                    if (!zone.Contains(souris))
                        _idSousPoignee = null;
                }
            }

            if (_idSousPoignee == null)
            {
                CacherPoignees();
                return;
            }

            bool porteurSurvole = (noeud != null && noeud.Modele.Id == _idSousPoignee)
                || (groupeSurvole != null && groupeSurvole.Id == _idSousPoignee);
            PositionnerPoignees(porteurSurvole, souris);
        }

        private void PositionnerPoignees(bool boiteSurvolee, Point souris)
        {
            Rect rect;
            if (!RectangleExtremite(_idSousPoignee, out rect))
            {
                CacherPoignees();
                return;
            }
            Point[] centres = CentresPoignees(rect);
            for (int i = 0; i < _poignees.Length; i++)
            {
                // Surbrillance : épaissies quand la boîte est survolée, encore plus sous le curseur.
                double facteurPoignees = Mentalo.Parametres.ParametresGlobaux.FacteurPoignees();
                double distance = ((Vector)(souris - centres[i])).Length;
                double taille;
                if (distance <= TaillePoigneeSaisie * facteurPoignees) taille = TaillePoigneeSaisie;
                else if (boiteSurvolee) taille = TaillePoigneeAccent;
                else taille = TaillePoignee;
                taille *= facteurPoignees;

                Ellipse poignee = _poignees[i];
                poignee.Width = taille;
                poignee.Height = taille;
                poignee.StrokeThickness = taille >= TaillePoigneeAccent * facteurPoignees ? 3 : 2;
                Canvas.SetLeft(poignee, centres[i].X - taille / 2);
                Canvas.SetTop(poignee, centres[i].Y - taille / 2);
                poignee.IsVisible = true;
            }
        }

        /// <summary>Les centres des quatre bordures : haut, droite, bas, gauche.</summary>
        private static Point[] CentresPoignees(Rect rect)
        {
            double cx = rect.X + rect.Width / 2;
            double cy = rect.Y + rect.Height / 2;
            return new Point[]
            {
                new Point(cx, rect.Y),
                new Point(rect.Right, cy),
                new Point(cx, rect.Bottom),
                new Point(rect.X, cy)
            };
        }

        private void CacherPoignees()
        {
            foreach (Ellipse poignee in _poignees)
                poignee.IsVisible = false;
        }

        private int IndicePoignee(object origine)
        {
            for (int i = 0; i < _poignees.Length; i++)
                if (ReferenceEquals(origine, _poignees[i])) return i;
            return -1;
        }

        private void CommencerCreationLien(int indicePoignee, IPointer pointeur)
        {
            if (_idSousPoignee == null || _mode == "lecture") return;
            Rect rect;
            if (!RectangleExtremite(_idSousPoignee, out rect)) return;
            _lienEnCreation = true;
            _lienSourceId = _idSousPoignee;
            _departLien = CentresPoignees(rect)[indicePoignee];
            foreach (Ellipse poignee in _poignees)
                poignee.IsHitTestVisible = false;
            CacherPoignees();
            _lienTemporaire.StartPoint = _departLien;
            _lienTemporaire.EndPoint = _departLien;
            _lienTemporaire.IsVisible = true;
            Cursor = Ui.CurseurCroix;
            Capturer(pointeur);
        }

        private void TerminerCreationLien()
        {
            Liberer();
            Cursor = _espaceEnfonce ? Ui.CurseurMain : Ui.CurseurFleche;
            _lienTemporaire.IsVisible = false;
            foreach (Ellipse poignee in _poignees)
                poignee.IsHitTestVisible = true;
            CacherPoignees();
            _lienEnCreation = false;

            NoeudVisuel cible = _cibleSurvolee;
            _cibleSurvolee = null;
            if (cible != null) cible.DefinirCibleDeLien(false);
            Groupe cibleGroupe = _cibleGroupeSurvole;
            _cibleGroupeSurvole = null;
            if (cibleGroupe != null) AppliquerStyleVisuelGroupe(cibleGroupe);

            string cibleId = cible != null ? cible.Modele.Id
                : (cibleGroupe != null ? cibleGroupe.Id : null);
            if (cibleId != null && _lienSourceId != null && !LienExiste(_lienSourceId, cibleId))
            {
                Lien lien = Document.NouveauLien();
                lien.SourceId = _lienSourceId;
                lien.CibleId = cibleId;
                Historique.ExecuterEtPousser(new CommandeCreerLien(this, lien));
                SelectionnerLien(_visuelsLiens[lien]);
            }
            _lienSourceId = null;
        }

        private Groupe GroupeSousPoint(Point pointCanvas)
        {
            foreach (KeyValuePair<Groupe, Rectangle> kv in _visuelsGroupes)
            {
                if (!kv.Value.IsVisible) continue;
                Rect rect = new Rect(Canvas.GetLeft(kv.Value), Canvas.GetTop(kv.Value),
                    kv.Value.Width, kv.Value.Height);
                if (rect.Contains(pointCanvas)) return kv.Key;
            }
            return null;
        }

        /// <summary>La boîte visible sous ce point du canevas, la plus haute d'abord
        /// (test géométrique : le trait provisoire du lien n'entre pas en jeu).</summary>
        private NoeudVisuel NoeudSousPoint(Point pointCanvas)
        {
            NoeudVisuel trouve = null;
            int zMax = int.MinValue;
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
            {
                if (!kv.Value.IsVisible) continue;
                if (!RectangleNoeud(kv.Key).Contains(pointCanvas)) continue;
                if (kv.Value.ZIndex > zMax)
                {
                    zMax = kv.Value.ZIndex;
                    trouve = kv.Value;
                }
            }
            return trouve;
        }

        // ------------------------------------------------------------------ zoom

        public void ZoomAvant() { ZoomVers(_echelle.ScaleX * 1.2, CentreVue()); }
        public void ZoomArriere() { ZoomVers(_echelle.ScaleX / 1.2, CentreVue()); }
        public void ZoomReinitialiser() { ZoomVers(1.0, CentreVue()); }
        /// <summary>Un zoom imposé (le curseur de zoom de Marabook), autour du centre de la vue.</summary>
        public void DefinirZoom(double echelle) { ZoomVers(echelle, CentreVue()); }

        private Point CentreVue()
        {
            return new Point(LargeurVue / 2, HauteurVue / 2);
        }

        private void ZoomVers(double cible, Point pivotEcran)
        {
            double ancienne = _echelle.ScaleX;
            double nouvelle = Math.Max(ZoomMin, Math.Min(ZoomMax, cible));
            if (nouvelle == ancienne) return;
            double facteur = nouvelle / ancienne;
            _translation.X = pivotEcran.X - facteur * (pivotEcran.X - _translation.X);
            _translation.Y = pivotEcran.Y - facteur * (pivotEcran.Y - _translation.Y);
            _echelle.ScaleX = nouvelle;
            _echelle.ScaleY = nouvelle;
            SignalerZoom();
            SignalerVue();
        }

        public void Recentrer()
        {
            if (Document.Noeuds.Count == 0)
            {
                _translation.X = 0;
                _translation.Y = 0;
                SignalerVue();
                return;
            }
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (Noeud n in Document.Noeuds)
            {
                Rect rect = RectangleNoeud(n);
                minX = Math.Min(minX, rect.X);
                minY = Math.Min(minY, rect.Y);
                maxX = Math.Max(maxX, rect.Right);
                maxY = Math.Max(maxY, rect.Bottom);
            }
            _translation.X = LargeurVue / 2 - (minX + maxX) / 2 * _echelle.ScaleX;
            _translation.Y = HauteurVue / 2 - (minY + maxY) / 2 * _echelle.ScaleY;
            SignalerVue();
        }

        // ------------------------------------------------------------------ création / édition de nœuds

        private void CreerNoeudA(Point positionCanvas)
        {
            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.X = positionCanvas.X;
            noeud.Y = positionCanvas.Y - 14;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            _noeudFraichementCree = noeud;

            NoeudVisuel visuel = _visuels[noeud];
            Selectionner(visuel);
            visuel.CommencerEdition();
            SignalerFormatCible();
        }

        private void NoeudEditionTerminee(NoeudVisuel visuel, ResultatEdition resultat)
        {
            // Le clavier revient à la toile — sauf s'il est déjà parti ailleurs dans la
            // fenêtre (un champ du rail de Marabook, 26/10) : on ne le lui reprend pas.
            if (!FocusAilleurs()) Focus();
            bool fraichementCree = (_noeudFraichementCree == visuel.Modele);
            _noeudFraichementCree = null;

            if (visuel.Modele.EstVide)
            {
                // Une boîte vide n'a pas de raison d'exister (sauf si elle porte une image ou un fichier).
                if (fraichementCree)
                    Historique.AnnulerEtOublier();
                else
                    Historique.ExecuterEtPousser(new CommandeSupprimerNoeud(this, visuel.Modele, LiensAttaches(visuel.Modele)));
                SignalerFormatCible();
                return;
            }

            // Pour une boîte fraîche, la commande de création porte déjà le texte final :
            // une seule étape d'annulation pour « créer + taper ».
            bool change = !Noeud.SegmentsEgaux(resultat.AnciensSegments, resultat.NouveauxSegments)
                || !Noeud.SegmentsEgaux(resultat.AnciensSegmentsApres, resultat.NouveauxSegmentsApres)
                || resultat.AncienAlignement != resultat.NouvelAlignement;
            if (!fraichementCree && change)
                Historique.Pousser(new CommandeModifierTexteRiche(this, visuel.Modele,
                    resultat.AnciensSegments, resultat.AnciensSegmentsApres, resultat.AncienAlignement,
                    resultat.NouveauxSegments, resultat.NouveauxSegmentsApres, resultat.NouvelAlignement));
            else if (fraichementCree || change)
                SignalerModification();
            MettreAJourGrip();
            SignalerFormatCible();
        }

        /// <summary>Vrai si le clavier est sur un contrôle hors de la toile (et pas nulle part).</summary>
        private bool FocusAilleurs()
        {
            TopLevel sommet = TopLevel.GetTopLevel(this);
            IInputElement focus = sommet == null || sommet.FocusManager == null ? null : sommet.FocusManager.GetFocusedElement();
            Visual visuel = focus as Visual;
            if (visuel == null) return false;
            for (Visual v = visuel; v != null; v = v.GetVisualParent())
                if (ReferenceEquals(v, this)) return false;
            return true;
        }

        private void Selectionner(NoeudVisuel visuel)
        {
            List<NoeudVisuel> ensemble = new List<NoeudVisuel>();
            if (visuel != null) ensemble.Add(visuel);
            SelectionnerEnsemble(ensemble, visuel);
        }

        private void SelectionnerEnsemble(List<NoeudVisuel> ensemble, NoeudVisuel principal)
        {
            if (_groupeActif != null)
            {
                _groupeActif = null;
                MettreAJourSurbrillanceGroupes();
            }
            if (_selectionLien != null)
            {
                _selectionLien.DefinirSelection(false);
                _selectionLien = null;
                MettreAJourPoigneeCourbure();
            }
            foreach (NoeudVisuel v in _selectionnes)
                if (!ensemble.Contains(v)) v.DefinirSelection(false);
            _selectionnes.Clear();
            foreach (NoeudVisuel v in ensemble)
            {
                if (_selectionnes.Contains(v)) continue;
                _selectionnes.Add(v);
                v.DefinirSelection(true);
            }
            _selection = principal != null && _selectionnes.Contains(principal) ? principal
                : (_selectionnes.Count > 0 ? _selectionnes[0] : null);
            MettreAJourGrip();
            SignalerFormatCible();
        }

        private void BasculerSelection(NoeudVisuel visuel)
        {
            List<NoeudVisuel> ensemble = new List<NoeudVisuel>(_selectionnes);
            if (ensemble.Contains(visuel)) ensemble.Remove(visuel);
            else ensemble.Add(visuel);
            SelectionnerEnsemble(ensemble, visuel);
        }

        private void SelectionnerLien(LienVisuel visuel)
        {
            if (_groupeActif != null)
            {
                _groupeActif = null;
                MettreAJourSurbrillanceGroupes();
            }
            if (_selectionnes.Count > 0)
            {
                foreach (NoeudVisuel v in _selectionnes) v.DefinirSelection(false);
                _selectionnes.Clear();
                _selection = null;
                MettreAJourGrip();
            }
            if (_selectionLien == visuel) return;
            if (_selectionLien != null) _selectionLien.DefinirSelection(false);
            _selectionLien = visuel;
            if (_selectionLien != null) _selectionLien.DefinirSelection(true);
            MettreAJourPoigneeCourbure();
            SignalerFormatCible();
        }

        /// <summary>Désélectionne tout (Échap).</summary>
        public void Deselectionner()
        {
            Selectionner(null);
        }

        // ------------------------------------------------------------------ copier / coller / dupliquer

        // Presse-papier interne (partagé par toutes les cartes de la session) ; un marqueur
        // est posé dans le presse-papier système pour savoir s'il est toujours d'actualité.
        private const string FormatPressePapier = "Mentalo.Boites";
        private static readonly List<Noeud> _pressePapierNoeuds = new List<Noeud>();
        private static readonly List<Lien> _pressePapierLiens = new List<Lien>();
        private static int _collages;
        private static string _marqueurPressePapier;

        private static Noeud ClonerNoeud(Noeud source)
        {
            Noeud copie = new Noeud();
            copie.X = source.X;
            copie.Y = source.Y;
            copie.Segments = Noeud.CopieDe(source.Segments);
            copie.SegmentsApres = Noeud.CopieDe(source.SegmentsApres);
            copie.Alignement = source.Alignement;
            copie.LargeurFixe = source.LargeurFixe;
            copie.ImageDonnees = source.ImageDonnees; // octets immuables, partagés sans risque
            copie.ImageExtension = source.ImageExtension;
            copie.ImageLargeur = source.ImageLargeur;
            copie.ImageHauteur = source.ImageHauteur;
            // Identifiants neufs : deux liens ne partagent pas la même entrée du .tea.
            foreach (PieceJointe piece in source.Fichiers)
                copie.Fichiers.Add(piece.ClonerNouvelId());
            copie.Note = source.Note;
            copie.Etiquettes = new List<string>(source.Etiquettes);
            StyleBoite.Depuis(source).AppliquerA(copie);
            return copie;
        }

        private static Lien ClonerLien(Lien source)
        {
            Lien copie = new Lien();
            copie.SourceId = source.SourceId;
            copie.CibleId = source.CibleId;
            copie.Couleur = source.Couleur;
            copie.Epaisseur = source.Epaisseur;
            copie.Trait = source.Trait;
            copie.Fleche = source.Fleche;
            copie.Trace = source.Trace;
            copie.Courbure = source.Courbure;
            copie.Etiquette = source.Etiquette;
            return copie;
        }

        public async void CopierSelection()
        {
            if (_selectionnes.Count == 0) return;
            _pressePapierNoeuds.Clear();
            _pressePapierLiens.Clear();
            _collages = 0;
            HashSet<string> ids = new HashSet<string>();
            foreach (NoeudVisuel v in _selectionnes)
                ids.Add(v.Modele.Id);
            foreach (NoeudVisuel v in _selectionnes)
            {
                Noeud copie = ClonerNoeud(v.Modele);
                copie.Id = v.Modele.Id; // conservé pour recoller les liens ; regénéré au collage
                _pressePapierNoeuds.Add(copie);
            }
            foreach (Lien lien in Document.Liens)
                if (ids.Contains(lien.SourceId) && ids.Contains(lien.CibleId))
                    _pressePapierLiens.Add(ClonerLien(lien));
            try
            {
                // Le marqueur est posé comme texte : le presse-papiers d'Avalonia ne porte
                // que des formats simples d'une application à l'autre.
                string textePlat = "";
                foreach (Noeud n in _pressePapierNoeuds)
                    textePlat += (textePlat.Length > 0 ? "\n" : "") + n.TextePlat;
                _marqueurPressePapier = textePlat;
                IClipboard presse = Ui.PressePapier(this);
                if (presse != null) await presse.SetTextAsync(textePlat);
            }
            catch { }
        }

        /// <summary>Vrai si notre presse-papier interne est toujours le contenu « actuel »
        /// (rien n'a été copié ailleurs depuis) — vérification asynchrone.</summary>
        public async System.Threading.Tasks.Task<bool> PeutCollerBoites()
        {
            if (_pressePapierNoeuds.Count == 0) return false;
            try
            {
                IClipboard presse = Ui.PressePapier(this);
                if (presse == null) return true;
                string texte = await presse.GetTextAsync();
                return texte == _marqueurPressePapier;
            }
            catch { return true; }
        }

        public bool PressePapierInterneRempli { get { return _pressePapierNoeuds.Count > 0; } }

        public void CollerBoites()
        {
            if (_mode == "lecture" || _pressePapierNoeuds.Count == 0) return;
            _collages++;
            CollerElements(_pressePapierNoeuds, _pressePapierLiens, 24 * _collages);
        }

        public void DupliquerSelection()
        {
            if (_mode == "lecture" || _selectionnes.Count == 0) return;
            List<Noeud> noeuds = new List<Noeud>();
            HashSet<string> ids = new HashSet<string>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                noeuds.Add(v.Modele);
                ids.Add(v.Modele.Id);
            }
            List<Lien> liens = new List<Lien>();
            foreach (Lien lien in Document.Liens)
                if (ids.Contains(lien.SourceId) && ids.Contains(lien.CibleId))
                    liens.Add(lien);
            CollerElements(noeuds, liens, 24);
        }

        private void CollerElements(List<Noeud> sources, List<Lien> liens, double decalage)
        {
            Dictionary<string, string> correspondance = new Dictionary<string, string>();
            List<Noeud> nouveaux = new List<Noeud>();
            List<ICommande> commandes = new List<ICommande>();
            foreach (Noeud source in sources)
            {
                Noeud copie = ClonerNoeud(source);
                copie.X = source.X + decalage;
                copie.Y = source.Y + decalage;
                correspondance[source.Id] = copie.Id;
                nouveaux.Add(copie);
                commandes.Add(new CommandeCreerNoeud(this, copie));
            }
            foreach (Lien lien in liens)
            {
                Lien copie = ClonerLien(lien);
                copie.SourceId = correspondance[lien.SourceId];
                copie.CibleId = correspondance[lien.CibleId];
                commandes.Add(new CommandeCreerLien(this, copie));
            }
            if (commandes.Count == 0) return;
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));

            List<NoeudVisuel> visuels = new List<NoeudVisuel>();
            foreach (Noeud n in nouveaux)
            {
                NoeudVisuel visuel;
                if (_visuels.TryGetValue(n, out visuel)) visuels.Add(visuel);
            }
            SelectionnerEnsemble(visuels, visuels.Count > 0 ? visuels[0] : null);
        }

        // ------------------------------------------------------------------ aligner / distribuer

        public void AlignerSelection(string type)
        {
            if (_mode == "lecture" || _selectionnes.Count < 2) return;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (NoeudVisuel v in _selectionnes)
            {
                Rect r = RectangleNoeud(v.Modele);
                minX = Math.Min(minX, r.X);
                minY = Math.Min(minY, r.Y);
                maxX = Math.Max(maxX, r.Right);
                maxY = Math.Max(maxY, r.Bottom);
            }
            List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
            foreach (NoeudVisuel v in _selectionnes)
            {
                Rect r = RectangleNoeud(v.Modele);
                double nx = r.X, ny = r.Y;
                if (type == "gauche") nx = minX;
                else if (type == "centre-h") nx = (minX + maxX) / 2 - r.Width / 2;
                else if (type == "droite") nx = maxX - r.Width;
                else if (type == "haut") ny = minY;
                else if (type == "centre-v") ny = (minY + maxY) / 2 - r.Height / 2;
                else if (type == "bas") ny = maxY - r.Height;
                if (nx == r.X && ny == r.Y) continue;
                CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                d.Noeud = v.Modele;
                d.AncienX = r.X; d.AncienY = r.Y;
                d.NouveauX = nx; d.NouveauY = ny;
                deplacements.Add(d);
            }
            if (deplacements.Count == 0) return;
            Historique.ExecuterEtPousser(new CommandeDeplacerNoeuds(this, deplacements));
        }

        public void DistribuerSelection(bool horizontal)
        {
            if (_mode == "lecture" || _selectionnes.Count < 3) return;
            List<NoeudVisuel> tries = new List<NoeudVisuel>(_selectionnes);
            tries.Sort(delegate(NoeudVisuel a, NoeudVisuel b)
            {
                Rect ra = RectangleNoeud(a.Modele), rb = RectangleNoeud(b.Modele);
                return horizontal ? ra.X.CompareTo(rb.X) : ra.Y.CompareTo(rb.Y);
            });
            double debut, fin, total = 0;
            Rect premier = RectangleNoeud(tries[0].Modele);
            Rect dernier = RectangleNoeud(tries[tries.Count - 1].Modele);
            debut = horizontal ? premier.X : premier.Y;
            fin = horizontal ? dernier.Right : dernier.Bottom;
            foreach (NoeudVisuel v in tries)
            {
                Rect r = RectangleNoeud(v.Modele);
                total += horizontal ? r.Width : r.Height;
            }
            double espace = (fin - debut - total) / (tries.Count - 1);
            List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
            double position = debut;
            foreach (NoeudVisuel v in tries)
            {
                Rect r = RectangleNoeud(v.Modele);
                double nx = horizontal ? position : r.X;
                double ny = horizontal ? r.Y : position;
                if (nx != r.X || ny != r.Y)
                {
                    CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                    d.Noeud = v.Modele;
                    d.AncienX = r.X; d.AncienY = r.Y;
                    d.NouveauX = nx; d.NouveauY = ny;
                    deplacements.Add(d);
                }
                position += (horizontal ? r.Width : r.Height) + espace;
            }
            if (deplacements.Count == 0) return;
            Historique.ExecuterEtPousser(new CommandeDeplacerNoeuds(this, deplacements));
        }

        /// <summary>Range la sélection en colonne (l'esprit des « stacks » de Scapple) :
        /// centres alignés, boîtes empilées de haut en bas à espacement régulier.</summary>
        public void EmpilerSelection()
        {
            if (_mode == "lecture" || _selectionnes.Count < 2) return;
            const double espace = 14;
            List<NoeudVisuel> tries = new List<NoeudVisuel>(_selectionnes);
            tries.Sort(delegate(NoeudVisuel premier, NoeudVisuel second)
            {
                Rect ra = RectangleNoeud(premier.Modele), rb = RectangleNoeud(second.Modele);
                return ra.Y != rb.Y ? ra.Y.CompareTo(rb.Y) : ra.X.CompareTo(rb.X);
            });
            double centreX = 0;
            foreach (NoeudVisuel v in tries)
            {
                Rect r = RectangleNoeud(v.Modele);
                centreX += r.X + r.Width / 2;
            }
            centreX /= tries.Count;
            double y = RectangleNoeud(tries[0].Modele).Y;
            List<CommandeDeplacerNoeuds.Deplacement> deplacements = new List<CommandeDeplacerNoeuds.Deplacement>();
            foreach (NoeudVisuel v in tries)
            {
                Rect r = RectangleNoeud(v.Modele);
                double nx = centreX - r.Width / 2;
                if (nx != r.X || y != r.Y)
                {
                    CommandeDeplacerNoeuds.Deplacement d = new CommandeDeplacerNoeuds.Deplacement();
                    d.Noeud = v.Modele;
                    d.AncienX = r.X; d.AncienY = r.Y;
                    d.NouveauX = nx; d.NouveauY = y;
                    deplacements.Add(d);
                }
                y += r.Height + espace;
            }
            if (deplacements.Count == 0) return;
            Historique.ExecuterEtPousser(new CommandeDeplacerNoeuds(this, deplacements));
        }

        // ------------------------------------------------------------------ recherche & navigation

        public List<Noeud> Rechercher(string texte)
        {
            List<Noeud> resultats = new List<Noeud>();
            if (string.IsNullOrWhiteSpace(texte)) return resultats;
            string bas = texte.Trim().ToLower();
            foreach (Noeud n in Document.Noeuds)
            {
                // Le texte, la note cachée, les étiquettes et les libellés de liens comptent.
                if (n.TextePlat.ToLower().Contains(bas) || (n.Note ?? "").ToLower().Contains(bas))
                {
                    resultats.Add(n);
                    continue;
                }
                bool trouve = false;
                foreach (string etiquette in n.Etiquettes)
                    if (etiquette.ToLower().Contains(bas)) { trouve = true; break; }
                if (!trouve)
                    foreach (PieceJointe piece in n.Fichiers)
                        if ((piece.Nom ?? "").ToLower().Contains(bas)) { trouve = true; break; }
                if (trouve) resultats.Add(n);
            }
            return resultats;
        }

        public void AllerAuNoeud(Noeud noeud)
        {
            NoeudVisuel visuel;
            if (!_visuels.TryGetValue(noeud, out visuel)) return;
            DeplierPour(noeud);
            Selectionner(visuel);
            CentrerSur(Centre(RectangleNoeud(noeud)));
        }

        /// <summary>Sélectionne la boîte la plus proche dans la direction donnée (flèches).</summary>
        public void NaviguerSelection(int dx, int dy)
        {
            if (Document.Noeuds.Count == 0) return;
            if (_selection == null)
            {
                // Rien de sélectionné : la boîte la plus proche du centre de la vue.
                Point centreVue = CentreCanvas();
                Noeud plusProche = null;
                double meilleure = double.MaxValue;
                foreach (Noeud n in Document.Noeuds)
                {
                    if (NoeudMasque(n)) continue;
                    double distance = ((Vector)(Centre(RectangleNoeud(n)) - centreVue)).Length;
                    if (distance < meilleure) { meilleure = distance; plusProche = n; }
                }
                if (plusProche != null) SelectionnerEtMontrer(plusProche);
                return;
            }
            Point origine = Centre(RectangleNoeud(_selection.Modele));
            Noeud meilleur = null;
            double meilleurScore = double.MaxValue;
            foreach (Noeud n in Document.Noeuds)
            {
                if (n == _selection.Modele || NoeudMasque(n)) continue;
                Vector v = Centre(RectangleNoeud(n)) - origine;
                double avant = v.X * dx + v.Y * dy;
                if (avant <= 1) continue; // pas dans la direction demandée
                double travers = Math.Abs(v.X * dy) + Math.Abs(v.Y * dx);
                double score = avant + travers * 2;
                if (score < meilleurScore) { meilleurScore = score; meilleur = n; }
            }
            if (meilleur != null) SelectionnerEtMontrer(meilleur);
        }

        private void SelectionnerEtMontrer(Noeud noeud)
        {
            NoeudVisuel visuel;
            if (!_visuels.TryGetValue(noeud, out visuel)) return;
            DeplierPour(noeud);
            Selectionner(visuel);
            // Recentrer seulement si la boîte sort de la vue.
            if (!RectangleVue().Contains(Centre(RectangleNoeud(noeud))))
                CentrerSur(Centre(RectangleNoeud(noeud)));
        }

        public void EditerSelection()
        {
            if (_selection == null || _mode == "lecture") return;
            _selection.CommencerEdition();
            SignalerFormatCible();
        }

        /// <summary>Vrai si une boîte est en cours d'édition (les raccourcis de la
        /// fenêtre laissent alors le clavier à l'éditeur).</summary>
        /// <summary>Clôt (en validant) l'édition ouverte, s'il y en a une : avant
        /// d'enregistrer ou de changer de document, le texte tapé rejoint le modèle
        /// sans attendre la perte de focus (26/10 soir — Marabook quittait la carte
        /// avec le texte encore dans le TextBox).</summary>
        public void ValiderEditionEnCours()
        {
            foreach (NoeudVisuel v in new List<NoeudVisuel>(_selectionnes))
                if (v.EnEdition) v.TerminerEdition(true);
        }

        /// <summary>Le texte en cours de frappe rejoint le modèle SANS fermer
        /// l'éditeur (1/11, Rémi) : pour une sauvegarde de secours ou un
        /// enregistrement automatique pendant qu'on écrit dans une boîte —
        /// valider fermait la boîte et sortait le caret toutes les 30 s.</summary>
        public void SynchroniserEditionEnCours()
        {
            foreach (NoeudVisuel v in _selectionnes)
                if (v.EnEdition) v.SynchroniserEdition();
        }

        public bool EditionEnCours
        {
            get
            {
                foreach (NoeudVisuel v in _selectionnes) if (v.EnEdition) return true;
                return false;
            }
        }

        // ------------------------------------------------------------------ styles favoris

        public void EnregistrerStyleFavori()
        {
            if (_selection == null) return;
            string forme = StyleBoite.Depuis(_selection.Modele).Serialiser();
            if (Mentalo.Parametres.ParametresGlobaux.StylesFavoris.Contains(forme)) return;
            Mentalo.Parametres.ParametresGlobaux.StylesFavoris.Add(forme);
            Hote.EnregistrerParametres();
        }

        /// <summary>Applique un style favori à toutes les boîtes sélectionnées (une seule annulation).</summary>
        public void AppliquerStyleFavori(StyleBoite style)
        {
            if (_mode == "lecture" || _selectionnes.Count == 0) return;
            List<ICommande> commandes = new List<ICommande>();
            foreach (NoeudVisuel v in _selectionnes)
                if (!style.EgalA(v.Modele))
                    commandes.Add(new CommandeModifierStyleNoeud(this, v.Modele, style.Cloner()));
            if (commandes.Count == 0) return;
            if (commandes.Count == 1) Historique.ExecuterEtPousser(commandes[0]);
            else Historique.ExecuterEtPousser(new CommandeComposite(commandes));
        }
    }
}
