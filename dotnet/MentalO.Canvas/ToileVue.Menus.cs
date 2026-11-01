using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le style des boîtes, le formatage du texte et le menu contextuel de
    /// formatage (clic droit sur une boîte, menu de l'éditeur).</summary>
    public partial class ToileVue
    {
        /// <summary>Route une propriété vers le texte ou vers le style de la boîte.</summary>
        private void AppliquerPropriete(string propriete, object valeur)
        {
            if (propriete == "fond" || propriete == "bordure-couleur" || propriete == "bordure-epaisseur"
                || propriete == "bordure-trait" || propriete == "rayon"
                || propriete == "ombre" || propriete == "opacite")
                AppliquerStyleBoite(propriete, valeur);
            else
                AppliquerFormatTexte(propriete, valeur);
        }

        public void AppliquerStyleBoite(string propriete, object valeur)
        {
            if (_selection == null || _mode == "lecture") return;
            Noeud noeud = _selection.Modele;
            StyleBoite style = StyleBoite.Depuis(noeud);
            switch (propriete)
            {
                case "fond": style.Fond = (string)valeur; break;
                case "bordure-couleur": style.BordureCouleur = (string)valeur; break;
                case "bordure-epaisseur": style.BordureEpaisseur = (double)valeur; break;
                case "bordure-trait": style.BordureTrait = (string)valeur; break;
                case "rayon": style.Rayon = (double)valeur; break;
                case "ombre": style.Ombre = (string)valeur; break;
                case "opacite": style.Opacite = (double)valeur; break;
            }
            if (style.EgalA(noeud)) return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleNoeud(this, noeud, style));
        }

        /// <summary>Appelé par les commandes d'annulation : applique un style complet.</summary>
        public void AppliquerStyleNoeud(Noeud noeud, StyleBoite style)
        {
            style.AppliquerA(noeud);
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.MettreAJourStyle();
            SignalerModification();
            SignalerFormatCible();
        }

        // ------------------------------------------------------------------ formatage du texte

        /// <summary>Applique un changement de mise en forme : à la sélection de l'éditeur si une
        /// édition est en cours, sinon à toute la boîte sélectionnée (avec commande d'annulation).
        /// Propriétés : police, taille, couleur, surlignage, gras, italique, souligne, barre,
        /// alignement. Pour les bascules, valeur null = inverser.</summary>
        public void AppliquerFormatTexte(string propriete, object valeur)
        {
            if (_selection == null || _mode == "lecture") return;

            if (_selection.EnEdition)
            {
                _selection.AppliquerFormatSelection(propriete, valeur);
                SignalerFormatCible();
                return;
            }

            Noeud noeud = _selection.Modele;
            if (noeud.Segments.Count == 0 && noeud.SegmentsApres.Count == 0 && propriete != "alignement") return;

            List<Segment> anciens = Noeud.CopieDe(noeud.Segments);
            List<Segment> anciensApres = Noeud.CopieDe(noeud.SegmentsApres);
            List<Segment> nouveaux = Noeud.CopieDe(noeud.Segments);
            List<Segment> nouveauxApres = Noeud.CopieDe(noeud.SegmentsApres);
            List<Segment> tous = new List<Segment>(nouveaux);
            tous.AddRange(nouveauxApres);
            string nouvelAlignement = noeud.Alignement;

            switch (propriete)
            {
                case "alignement":
                    // Hors édition : toute la boîte, lignes comprises (et l'image via le nœud).
                    nouvelAlignement = (string)valeur;
                    foreach (Segment s in tous) s.AlignementLigne = nouvelAlignement;
                    break;
                case "police":
                    foreach (Segment s in tous) s.Police = (string)valeur;
                    break;
                case "taille":
                    foreach (Segment s in tous) s.Taille = (double)valeur;
                    break;
                case "couleur":
                    foreach (Segment s in tous) s.Couleur = (string)valeur;
                    break;
                case "surlignage":
                    foreach (Segment s in tous) s.Surlignage = (string)valeur;
                    break;
                case "gras":
                    {
                        bool cible = valeur is bool ? (bool)valeur : !TousLesSegments(tous, "gras");
                        foreach (Segment s in tous) s.Gras = cible;
                    }
                    break;
                case "italique":
                    {
                        bool cible = valeur is bool ? (bool)valeur : !TousLesSegments(tous, "italique");
                        foreach (Segment s in tous) s.Italique = cible;
                    }
                    break;
                case "souligne":
                    {
                        bool cible = valeur is bool ? (bool)valeur : !TousLesSegments(tous, "souligne");
                        foreach (Segment s in tous) s.Souligne = cible;
                    }
                    break;
                case "barre":
                    {
                        bool cible = valeur is bool ? (bool)valeur : !TousLesSegments(tous, "barre");
                        foreach (Segment s in tous) s.Barre = cible;
                    }
                    break;
            }

            if (Noeud.SegmentsEgaux(anciens, nouveaux) && Noeud.SegmentsEgaux(anciensApres, nouveauxApres)
                && nouvelAlignement == noeud.Alignement)
                return;
            Historique.ExecuterEtPousser(new CommandeModifierTexteRiche(
                this, noeud, anciens, anciensApres, noeud.Alignement,
                nouveaux, nouveauxApres, nouvelAlignement));
        }

        private static bool TousLesSegments(List<Segment> segments, string propriete)
        {
            if (segments.Count == 0) return false;
            foreach (Segment s in segments)
            {
                bool valeur = propriete == "gras" ? s.Gras
                    : propriete == "italique" ? s.Italique
                    : propriete == "souligne" ? s.Souligne
                    : s.Barre;
                if (!valeur) return false;
            }
            return true;
        }

        public FormatCourant LireFormatCourant()
        {
            if (_selection == null)
            {
                FormatCourant inactif = new FormatCourant();
                inactif.Actif = false;
                return inactif;
            }
            if (_selection.EnEdition)
                return _selection.LireFormatSelection();

            Noeud noeud = _selection.Modele;
            FormatCourant etat = new FormatCourant();
            etat.Actif = true;
            etat.Alignement = noeud.Alignement;
            Segment premier = noeud.Segments.Count > 0 ? noeud.Segments[0]
                : (noeud.SegmentsApres.Count > 0 ? noeud.SegmentsApres[0] : new Segment());
            etat.Police = premier.Police;
            etat.Taille = premier.Taille;
            etat.Couleur = premier.Couleur;
            etat.Surlignage = premier.Surlignage;
            etat.Gras = TousLesSegments(noeud.Segments, "gras");
            etat.Italique = TousLesSegments(noeud.Segments, "italique");
            etat.Souligne = TousLesSegments(noeud.Segments, "souligne");
            etat.Barre = TousLesSegments(noeud.Segments, "barre");
            _selection.RemplirStyleBoite(etat);
            return etat;
        }

        /// <summary>Le menu contextuel de formatage — utilisé au clic droit sur une boîte et
        /// comme menu contextuel de l'éditeur pendant l'édition.</summary>
        private ContextMenu ConstruireMenuFormatage(bool pourEditeur)
        {
            FormatCourant etat = LireFormatCourant();
            ContextMenu menu = new ContextMenu();
            // Le menu garde l'édition ouverte le temps qu'il est affiché.
            menu.Opened += delegate { NoeudVisuel.ProtectionFocus = true; };
            menu.Closed += delegate
            {
                NoeudVisuel.ProtectionFocus = false;
                if (_selection != null) _selection.RedonnerFocus();
            };
            // Une boîte-image sans texte : on masque la mise en forme du texte,
            // mais l'alignement reste (il positionne aussi l'image).
            bool imageSeule = !pourEditeur && _selection != null && _selection.Modele.EstImage
                && string.IsNullOrWhiteSpace(_selection.Modele.TextePlat);

            if (pourEditeur && _selection != null)
            {
                NoeudVisuel editeur = _selection;
                menu.Items.Add(Ui.Element("Couper", delegate { editeur.EditeurActif.Cut(); }, false, "Ctrl+X"));
                menu.Items.Add(Ui.Element("Copier", delegate { editeur.EditeurActif.Copy(); }, false, "Ctrl+C"));
                menu.Items.Add(Ui.Element("Coller", delegate { editeur.EditeurActif.Paste(); }, false, "Ctrl+V"));
                menu.Items.Add(Ui.Separateur());
            }

            if (!imageSeule)
            {
                MenuItem police = Ui.SousMenu("Police");
                // Les polices système sont nombreuses : le sous-menu se remplit à l'ouverture.
                police.Items.Add(Ui.Element("…", null));
                bool policesConstruites = false;
                police.SubmenuOpened += delegate
                {
                    if (policesConstruites) return;
                    policesConstruites = true;
                    police.Items.Clear();
                    foreach (string nom in TexteRiche.PolicesSysteme())
                    {
                        string capture = nom;
                        MenuItem element = Ui.Element(nom, delegate { AppliquerFormatTexte("police", capture); }, etat.Police == nom);
                        element.FontFamily = TexteRiche.Police(nom);
                        police.Items.Add(element);
                    }
                };
                menu.Items.Add(police);

                MenuItem taille = Ui.SousMenu("Taille");
                foreach (double valeur in Palette.Tailles)
                {
                    double capture = valeur;
                    taille.Items.Add(Ui.Element(valeur.ToString(), delegate { AppliquerFormatTexte("taille", capture); }, etat.Taille == valeur));
                }
                taille.Items.Add(Ui.Separateur());
                taille.Items.Add(Ui.Element("Autre…", delegate { DemanderTaillePersonnalisee(); }));
                menu.Items.Add(taille);

                menu.Items.Add(Ui.Separateur());
                AjouterBascule(menu, "Gras", "gras", etat.Gras, "Ctrl+B");
                AjouterBascule(menu, "Italique", "italique", etat.Italique, "Ctrl+I");
                AjouterBascule(menu, "Souligné", "souligne", etat.Souligne, "Ctrl+U");
                AjouterBascule(menu, "Barré", "barre", etat.Barre, null);
                menu.Items.Add(Ui.Separateur());

                MenuItem couleur = Ui.SousMenu("Couleur du texte");
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                    AjouterChoixCouleurTexte(couleur, "couleur", Palette.Couleurs[i, 0], Palette.Couleurs[i, 1], etat.Couleur);
                AjouterCouleursPersonnalisees(couleur, "couleur", etat.Couleur);
                menu.Items.Add(couleur);

                MenuItem surlignage = Ui.SousMenu("Surlignage");
                surlignage.Items.Add(Ui.Element("Aucun", delegate { AppliquerFormatTexte("surlignage", null); }, etat.Surlignage == null));
                for (int i = 0; i < Palette.Surlignages.GetLength(0); i++)
                    AjouterChoixCouleurTexte(surlignage, "surlignage", Palette.Surlignages[i, 0], Palette.Surlignages[i, 1], etat.Surlignage);
                AjouterCouleursPersonnalisees(surlignage, "surlignage", etat.Surlignage);
                menu.Items.Add(surlignage);

                MenuItem puces = Ui.SousMenu("Liste");
                puces.Items.Add(Ui.Element("Puces", delegate { BasculerPuces("puce"); }));
                puces.Items.Add(Ui.Element("Tirets", delegate { BasculerPuces("tiret"); }));
                puces.Items.Add(Ui.Element("Numéros", delegate { BasculerPuces("numero"); }));
                puces.Items.Add(Ui.Element("Cases à cocher", delegate { BasculerPuces("case"); }));
                menu.Items.Add(puces);
            }

            MenuItem alignement = Ui.SousMenu("Alignement");
            AjouterChoixAlignement(alignement, "Gauche", "gauche", etat.Alignement);
            AjouterChoixAlignement(alignement, "Centre", "centre", etat.Alignement);
            AjouterChoixAlignement(alignement, "Droite", "droite", etat.Alignement);
            menu.Items.Add(alignement);

            if (!pourEditeur)
            {
                menu.Items.Add(Ui.Separateur());

                MenuItem fond = Ui.SousMenu("Fond de la boîte");
                AjouterChoixCouleurTexte(fond, "fond", "Blanc", "#FFFFFF", etat.FondBoite);
                AjouterChoixCouleurTexte(fond, "fond", "Transparent", "Transparent", etat.FondBoite);
                for (int i = 0; i < Palette.Surlignages.GetLength(0); i++)
                    AjouterChoixCouleurTexte(fond, "fond", Palette.Surlignages[i, 0], Palette.Surlignages[i, 1], etat.FondBoite);
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                    AjouterChoixCouleurTexte(fond, "fond", Palette.Couleurs[i, 0], Palette.Couleurs[i, 1], etat.FondBoite);
                AjouterCouleursPersonnalisees(fond, "fond", etat.FondBoite);
                menu.Items.Add(fond);

                MenuItem bordure = Ui.SousMenu("Bordure");
                MenuItem bordureCouleur = Ui.SousMenu("Couleur");
                AjouterChoixCouleurTexte(bordureCouleur, "bordure-couleur", "Gris sable (défaut)", "#B8B2A7", etat.BordureCouleur);
                for (int i = 0; i < Palette.Couleurs.GetLength(0); i++)
                    AjouterChoixCouleurTexte(bordureCouleur, "bordure-couleur", Palette.Couleurs[i, 0], Palette.Couleurs[i, 1], etat.BordureCouleur);
                AjouterCouleursPersonnalisees(bordureCouleur, "bordure-couleur", etat.BordureCouleur);
                bordure.Items.Add(bordureCouleur);

                MenuItem bordureEpaisseur = Ui.SousMenu("Épaisseur");
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Fine (1)", 1, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Normale (1,5)", 1.5, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Moyenne (2)", 2, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Épaisse (3)", 3, etat.BordureEpaisseur);
                AjouterChoixNombre(bordureEpaisseur, "bordure-epaisseur", "Très épaisse (5)", 5, etat.BordureEpaisseur);
                bordure.Items.Add(bordureEpaisseur);

                MenuItem bordureTrait = Ui.SousMenu("Trait");
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Plein", "plein", etat.BordureTrait);
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Tirets", "tirets", etat.BordureTrait);
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Pointillé", "points", etat.BordureTrait);
                AjouterChoixValeur(bordureTrait, "bordure-trait", "Aucune", "aucune", etat.BordureTrait);
                bordure.Items.Add(bordureTrait);
                menu.Items.Add(bordure);

                MenuItem coins = Ui.SousMenu("Coins");
                AjouterChoixNombre(coins, "rayon", "Carrés", 0, etat.Rayon);
                AjouterChoixNombre(coins, "rayon", "Arrondis", 5, etat.Rayon);
                AjouterChoixNombre(coins, "rayon", "Très arrondis", 12, etat.Rayon);
                menu.Items.Add(coins);

                MenuItem ombre = Ui.SousMenu("Ombre");
                AjouterChoixValeur(ombre, "ombre", "Aucune", "aucune", etat.Ombre);
                AjouterChoixValeur(ombre, "ombre", "Légère", "legere", etat.Ombre);
                AjouterChoixValeur(ombre, "ombre", "Marquée", "marquee", etat.Ombre);
                menu.Items.Add(ombre);

                MenuItem opacite = Ui.SousMenu("Opacité");
                AjouterChoixNombre(opacite, "opacite", "100 %", 1, etat.Opacite);
                AjouterChoixNombre(opacite, "opacite", "85 %", 0.85, etat.Opacite);
                AjouterChoixNombre(opacite, "opacite", "70 %", 0.7, etat.Opacite);
                AjouterChoixNombre(opacite, "opacite", "50 %", 0.5, etat.Opacite);
                AjouterChoixNombre(opacite, "opacite", "30 %", 0.3, etat.Opacite);
                menu.Items.Add(opacite);

                menu.Items.Add(Ui.Separateur());
                menu.Items.Add(Ui.Element("Insérer une image dans la boîte…", delegate { InsererImageDansBoite(); }));
                MenuItem insererFichierBoite = Ui.Element("Insérer un fichier dans la boîte…", delegate { InsererFichierDansBoite(); });
                Ui.Infobulle(insererFichierBoite, "Ajoute un lien vers un fichier (tout sauf une image) sous le texte");
                menu.Items.Add(insererFichierBoite);
                menu.Items.Add(Ui.Element("Insérer un lien web…", delegate { InsererLienWeb(); }));
                if (_selection != null)
                {
                    MenuItem note = Ui.Element(_selection.Modele.ANote ? "Modifier la note…" : "Ajouter une note…",
                        delegate { EditerNoteSelection(); });
                    Ui.Infobulle(note, "Un détail hors de la carte : une icône dans le coin, la note en infobulle");
                    menu.Items.Add(note);
                    menu.Items.Add(ConstruireMenuEtiquettes());
                }
                if (_selection != null && _selection.Modele.EstImage)
                    menu.Items.Add(Ui.Element("Retirer l'image", delegate { RetirerImageDeBoite(); }));
                if (_selection != null)
                {
                    MenuItem reajuster = Ui.Element("Réajuster au contenu", delegate { ReajusterBoite(); });
                    reajuster.IsEnabled = _selection.Modele.LargeurFixe > 0 || _selection.Modele.EstImage;
                    menu.Items.Add(reajuster);
                }

                MenuItem favoris = Ui.SousMenu("Styles favoris");
                List<string> formes = Mentalo.Parametres.ParametresGlobaux.StylesFavoris;
                for (int i = 0; i < formes.Count; i++)
                {
                    StyleBoite style = StyleBoite.Deserialiser(formes[i]);
                    if (style == null) continue;
                    StyleBoite capture = style;
                    MenuItem element = Ui.Element("Favori " + (i + 1), delegate { AppliquerStyleFavori(capture); });
                    element.Icon = new Border
                    {
                        Width = 14, Height = 14,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(Palette.CouleurDepuis(style.Fond, Colors.White)),
                        BorderBrush = new SolidColorBrush(Palette.CouleurDepuis(style.BordureCouleur, Colors.Gray)),
                        BorderThickness = new Thickness(2)
                    };
                    favoris.Items.Add(element);
                }
                if (formes.Count > 0) favoris.Items.Add(Ui.Separateur());
                favoris.Items.Add(Ui.Element("Enregistrer le style de cette boîte", delegate { EnregistrerStyleFavori(); }));
                if (formes.Count > 0)
                {
                    MenuItem retirer = Ui.SousMenu("Retirer un favori");
                    for (int i = 0; i < formes.Count; i++)
                    {
                        int indice = i;
                        retirer.Items.Add(Ui.Element("Favori " + (i + 1), delegate
                        {
                            if (indice < Mentalo.Parametres.ParametresGlobaux.StylesFavoris.Count)
                            {
                                Mentalo.Parametres.ParametresGlobaux.StylesFavoris.RemoveAt(indice);
                                Hote.EnregistrerParametres();
                            }
                        }));
                    }
                    favoris.Items.Add(retirer);
                }
                menu.Items.Add(favoris);

                if (_selectionnes.Count >= 2)
                {
                    MenuItem aligner = Ui.SousMenu("Aligner et distribuer");
                    AjouterChoixAlignerMenu(aligner, "Aligner à gauche", "gauche");
                    AjouterChoixAlignerMenu(aligner, "Centrer horizontalement", "centre-h");
                    AjouterChoixAlignerMenu(aligner, "Aligner à droite", "droite");
                    aligner.Items.Add(Ui.Separateur());
                    AjouterChoixAlignerMenu(aligner, "Aligner en haut", "haut");
                    AjouterChoixAlignerMenu(aligner, "Centrer verticalement", "centre-v");
                    AjouterChoixAlignerMenu(aligner, "Aligner en bas", "bas");
                    if (_selectionnes.Count >= 3)
                    {
                        aligner.Items.Add(Ui.Separateur());
                        aligner.Items.Add(Ui.Element("Distribuer horizontalement", delegate { DistribuerSelection(true); }));
                        aligner.Items.Add(Ui.Element("Distribuer verticalement", delegate { DistribuerSelection(false); }));
                    }
                    menu.Items.Add(aligner);
                    MenuItem empiler = Ui.Element("Empiler la sélection", delegate { EmpilerSelection(); });
                    Ui.Infobulle(empiler, "Range les boîtes en colonne, centres alignés");
                    menu.Items.Add(empiler);
                }

                menu.Items.Add(Ui.Separateur());
                MenuItem grouper = Ui.Element("Grouper les boîtes sélectionnées", delegate { GrouperSelection(); }, false, "Ctrl+G");
                grouper.IsEnabled = _selectionnes.Count >= 2;
                menu.Items.Add(grouper);
                bool dansGroupe = _selection != null && GroupeDuNoeud(_selection.Modele) != null;
                MenuItem degrouper = Ui.Element("Dégrouper", delegate { DegrouperSelection(); }, false, "Ctrl+Shift+G");
                degrouper.IsEnabled = dansGroupe;
                menu.Items.Add(degrouper);

                menu.Items.Add(Ui.Separateur());
                menu.Items.Add(Ui.Element("Copier le style de la boîte", delegate
                {
                    if (_selection != null) _stylePresse = StyleBoite.Depuis(_selection.Modele);
                }));
                MenuItem collerStyle = Ui.Element("Coller le style", delegate
                {
                    if (_selection == null || _stylePresse == null) return;
                    if (_stylePresse.EgalA(_selection.Modele)) return;
                    Historique.ExecuterEtPousser(
                        new CommandeModifierStyleNoeud(this, _selection.Modele, _stylePresse.Cloner()));
                });
                collerStyle.IsEnabled = _stylePresse != null;
                menu.Items.Add(collerStyle);
                MenuItem styleDefaut = Ui.Element("Utiliser ce style pour les nouvelles boîtes", delegate
                {
                    if (_selection == null) return;
                    Document.BoiteParDefaut = StyleBoite.Depuis(_selection.Modele);
                    SignalerModification();
                });
                Ui.Infobulle(styleDefaut, "Devient le style par défaut du fichier (visible dans Fichier > Préférences de fichier)");
                menu.Items.Add(styleDefaut);

                menu.Items.Add(Ui.Separateur());
                menu.Items.Add(Ui.Element("Supprimer la boîte", delegate { SupprimerSelection(); }, false, "Delete"));
            }
            return menu;
        }

        private void AjouterChoixAlignerMenu(MenuItem parent, string entete, string type)
        {
            string capture = type;
            parent.Items.Add(Ui.Element(entete, delegate { AlignerSelection(capture); }));
        }

        private void AjouterChoixValeur(MenuItem parent, string propriete, string entete, string valeur, string actuel)
        {
            parent.Items.Add(Ui.Element(entete, delegate { AppliquerPropriete(propriete, valeur); }, actuel == valeur));
        }

        private void AjouterChoixNombre(MenuItem parent, string propriete, string entete, double valeur, double actuel)
        {
            parent.Items.Add(Ui.Element(entete, delegate { AppliquerPropriete(propriete, valeur); }, Math.Abs(actuel - valeur) < 0.01));
        }

        private void AjouterBascule(ContextMenu menu, string entete, string propriete, bool coche, string geste)
        {
            menu.Items.Add(Ui.Element(entete, delegate { AppliquerFormatTexte(propriete, null); }, coche, geste));
        }

        private void AjouterChoixCouleurTexte(MenuItem parent, string propriete, string nom, string hexa, string actuel)
        {
            MenuItem element = Ui.Element(nom, delegate { AppliquerPropriete(propriete, hexa); },
                string.Equals(actuel, hexa, StringComparison.OrdinalIgnoreCase));
            element.Icon = Ui.Pastille(hexa, 14);
            parent.Items.Add(element);
        }

        private void AjouterChoixAlignement(MenuItem parent, string entete, string valeur, string actuel)
        {
            parent.Items.Add(Ui.Element(entete, delegate { AppliquerFormatTexte("alignement", valeur); }, actuel == valeur));
        }

        private void AjouterCouleursPersonnalisees(MenuItem parent, string propriete, string actuel)
        {
            if (Document.CouleursPersonnalisees.Count > 0)
            {
                parent.Items.Add(Ui.Separateur());
                foreach (string hexa in Document.CouleursPersonnalisees)
                    AjouterChoixCouleurTexte(parent, propriete, hexa, hexa, actuel);
            }
            parent.Items.Add(Ui.Separateur());
            string capture = propriete;
            parent.Items.Add(Ui.Element("Personnalisée…", delegate { ChoisirCouleurPersonnalisee(capture); }));
        }

        /// <summary>Ouvre le sélecteur de couleur maison ; renvoie l'hexa choisi ou null.</summary>
        private async Task<string> OuvrirSelecteurCouleur(string initiale)
        {
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                return await SelecteurCouleur.Choisir(FenetreProprietaire(), initiale);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
        }

        /// <summary>Sélecteur de couleur pour la cible courante ; la couleur choisie rejoint
        /// le répertoire du fichier.</summary>
        public async void ChoisirCouleurPersonnalisee(string propriete)
        {
            if (_selection == null) return;
            NoeudVisuel cible = _selection;
            string hexa = await OuvrirSelecteurCouleur(null);
            cible.RedonnerFocus();
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            AppliquerPropriete(propriete, hexa);
            SignalerFormatCible();
        }

        public async void DemanderTaillePersonnalisee()
        {
            if (_selection == null) return;
            NoeudVisuel cible = _selection;
            double? taille;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                taille = await DialogueSaisie.DemanderTaille(FenetreProprietaire(), LireFormatCourant().Taille);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
            cible.RedonnerFocus();
            if (taille.HasValue) AppliquerFormatTexte("taille", taille.Value);
        }
    }
}
