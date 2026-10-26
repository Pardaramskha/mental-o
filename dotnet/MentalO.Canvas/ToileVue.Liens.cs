using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Le menu contextuel des liens, l'insertion d'une boîte au milieu d'un lien,
    /// et l'API de la barre des liens.</summary>
    public partial class ToileVue
    {
        private void OuvrirMenuLien(Lien lien)
        {
            ContextMenu menu = new ContextMenu();

            MenuItem couleur = Ui.SousMenu("Couleur");
            AjouterChoixCouleur(couleur, lien, "Gris", "#6A665E");
            AjouterChoixCouleur(couleur, lien, "Noir", "#2B2B2B");
            AjouterChoixCouleur(couleur, lien, "Bleu", "#0078D7");
            AjouterChoixCouleur(couleur, lien, "Vert", "#2E8B57");
            AjouterChoixCouleur(couleur, lien, "Rouge", "#C0392B");
            AjouterChoixCouleur(couleur, lien, "Orange", "#E67E22");
            AjouterChoixCouleur(couleur, lien, "Violet", "#8E44AD");
            AjouterChoixCouleur(couleur, lien, "Rose", "#D63384");
            if (Document.CouleursPersonnalisees.Count > 0)
            {
                couleur.Items.Add(Ui.Separateur());
                foreach (string hexa in Document.CouleursPersonnalisees)
                    AjouterChoixCouleur(couleur, lien, hexa, hexa);
            }
            couleur.Items.Add(Ui.Separateur());
            couleur.Items.Add(Ui.Element("Personnalisée…", delegate { ChoisirCouleurLienPersonnalisee(); }));
            menu.Items.Add(couleur);

            MenuItem epaisseur = Ui.SousMenu("Épaisseur");
            AjouterChoixEpaisseur(epaisseur, lien, "Fin", 1);
            AjouterChoixEpaisseur(epaisseur, lien, "Normal", 2);
            AjouterChoixEpaisseur(epaisseur, lien, "Épais", 3);
            AjouterChoixEpaisseur(epaisseur, lien, "Très épais", 5);
            menu.Items.Add(epaisseur);

            MenuItem trait = Ui.SousMenu("Trait");
            AjouterChoixTrait(trait, lien, "Plein", "plein");
            AjouterChoixTrait(trait, lien, "Tirets", "tirets");
            AjouterChoixTrait(trait, lien, "Pointillé", "points");
            menu.Items.Add(trait);

            MenuItem fleche = Ui.SousMenu("Flèche");
            AjouterChoixFleche(fleche, lien, "Aucune", "aucune");
            AjouterChoixFleche(fleche, lien, "Vers la cible", "simple");
            AjouterChoixFleche(fleche, lien, "Dans les deux sens", "double");
            menu.Items.Add(fleche);

            MenuItem trace = Ui.SousMenu("Tracé");
            AjouterChoixTrace(trace, lien, "Droit", "droit");
            AjouterChoixTrace(trace, lien, "Courbe", "courbe");
            AjouterChoixTrace(trace, lien, "Équerre", "equerre");
            menu.Items.Add(trace);

            menu.Items.Add(Ui.Separateur());
            menu.Items.Add(Ui.Element(
                string.IsNullOrEmpty(lien.Etiquette) ? "Ajouter une étiquette…" : "Modifier l'étiquette…",
                delegate { DemanderEtiquetteLien(lien); }));
            if (!string.IsNullOrEmpty(lien.Etiquette))
                menu.Items.Add(Ui.Element("Retirer l'étiquette", delegate
                {
                    Historique.ExecuterEtPousser(new CommandeModifierEtiquetteLien(this, lien, ""));
                }));

            menu.Items.Add(Ui.Separateur());
            MenuItem inserer = Ui.Element("Insérer une boîte au milieu", delegate { InsererBoiteSurLien(lien); });
            Ui.Infobulle(inserer, "Coupe le lien en deux tronçons reliés par une boîte neuve (Échap annule)");
            menu.Items.Add(inserer);

            menu.Items.Add(Ui.Separateur());
            menu.Items.Add(Ui.Element("Supprimer le lien", delegate
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerLien(this, lien));
            }, false, "Delete"));

            Ui.OuvrirMenu(menu, this);
        }

        private void AjouterChoixCouleur(MenuItem parent, Lien lien, string nom, string hexa)
        {
            MenuItem element = Ui.Element(nom, delegate { ChangerStyleLien(lien, hexa, lien.Epaisseur, lien.Trait, lien.Fleche); },
                string.Equals(lien.Couleur, hexa, StringComparison.OrdinalIgnoreCase));
            element.Icon = Ui.Pastille(hexa, 14);
            parent.Items.Add(element);
        }

        private void AjouterChoixEpaisseur(MenuItem parent, Lien lien, string nom, double valeur)
        {
            parent.Items.Add(Ui.Element(nom, delegate { ChangerStyleLien(lien, lien.Couleur, valeur, lien.Trait, lien.Fleche); },
                lien.Epaisseur == valeur));
        }

        private void AjouterChoixTrait(MenuItem parent, Lien lien, string nom, string valeur)
        {
            parent.Items.Add(Ui.Element(nom, delegate { ChangerStyleLien(lien, lien.Couleur, lien.Epaisseur, valeur, lien.Fleche); },
                lien.Trait == valeur));
        }

        private void AjouterChoixFleche(MenuItem parent, Lien lien, string nom, string valeur)
        {
            parent.Items.Add(Ui.Element(nom, delegate { ChangerStyleLien(lien, lien.Couleur, lien.Epaisseur, lien.Trait, valeur); },
                lien.Fleche == valeur));
        }

        private void ChangerStyleLien(Lien lien, string couleur, double epaisseur, string trait, string fleche)
        {
            if (lien.Couleur == couleur && lien.Epaisseur == epaisseur && lien.Trait == trait && lien.Fleche == fleche)
                return;
            Historique.ExecuterEtPousser(new CommandeModifierStyleLien(this, lien, couleur, epaisseur, trait, fleche));
        }

        private void AjouterChoixTrace(MenuItem parent, Lien lien, string nom, string valeur)
        {
            parent.Items.Add(Ui.Element(nom, delegate { ChangerTraceLien(lien, valeur); }, lien.Trace == valeur));
        }

        private void ChangerTraceLien(Lien lien, string trace)
        {
            if (lien.Trace == trace) return;
            // Une courbe toute neuve part avec un bombé visible ; il se règle à la poignée.
            double courbure = trace == "courbe"
                ? (Math.Abs(lien.Courbure) > 0.5 ? lien.Courbure : 46)
                : lien.Courbure;
            Historique.ExecuterEtPousser(new CommandeModifierTraceLien(this, lien, trace, courbure));
        }

        /// <summary>Insère une boîte neuve au milieu d'un lien : le lien d'origine devient
        /// deux tronçons de même style, et l'édition démarre aussitôt. Échap annule tout.</summary>
        public void InsererBoiteSurLien(Lien lien)
        {
            if (_mode == "lecture") return;
            Point a, b;
            if (!PointsLien(lien, out a, out b)) return;
            Point milieu = GeometrieLien.PointMilieu(a, b, lien);

            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.X = milieu.X - 55;
            noeud.Y = milieu.Y - 16;

            Lien premier = ClonerLien(lien);
            premier.SourceId = lien.SourceId;
            premier.CibleId = noeud.Id;
            premier.Courbure = lien.Courbure / 2;
            Lien second = ClonerLien(lien);
            second.SourceId = noeud.Id;
            second.CibleId = lien.CibleId;
            second.Courbure = lien.Courbure / 2;
            second.Etiquette = ""; // l'étiquette reste sur le premier tronçon
            if (lien.Fleche == "simple") premier.Fleche = "aucune"; // la flèche file vers la cible

            List<ICommande> commandes = new List<ICommande>();
            commandes.Add(new CommandeSupprimerLien(this, lien));
            commandes.Add(new CommandeCreerNoeud(this, noeud));
            commandes.Add(new CommandeCreerLien(this, premier));
            commandes.Add(new CommandeCreerLien(this, second));
            Historique.ExecuterEtPousser(new CommandeComposite(commandes));
            // Marquée « fraîche » : rester vide (Échap ou validation à blanc) annule
            // toute l'insertion et restaure le lien d'origine.
            _noeudFraichementCree = noeud;

            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
            {
                Selectionner(visuel);
                visuel.CommencerEdition();
                SignalerFormatCible();
            }
        }

        public async void DemanderEtiquetteLien(Lien lien)
        {
            string texte = await DialogueSaisie.DemanderTexte(FenetreProprietaire(),
                "Étiquette du lien", "Texte posé au milieu du trait :", lien.Etiquette ?? "");
            if (texte == null || texte == (lien.Etiquette ?? "")) return;
            Historique.ExecuterEtPousser(new CommandeModifierEtiquetteLien(this, lien, texte.Trim()));
        }

        // --- API pour la barre des liens ---------------------------------------------------

        public Lien LienSelectionne
        {
            get { return _selectionLien == null ? null : _selectionLien.Modele; }
        }

        /// <summary>Modifie le style du lien sélectionné ; null = valeur inchangée.</summary>
        public void AppliquerStyleLienSelection(string couleur, double? epaisseur, string trait, string fleche)
        {
            if (_selectionLien == null || _mode == "lecture") return;
            Lien lien = _selectionLien.Modele;
            ChangerStyleLien(lien,
                couleur ?? lien.Couleur,
                epaisseur.HasValue ? epaisseur.Value : lien.Epaisseur,
                trait ?? lien.Trait,
                fleche ?? lien.Fleche);
        }

        public void AppliquerTraceLienSelection(string trace)
        {
            if (_selectionLien == null || _mode == "lecture") return;
            ChangerTraceLien(_selectionLien.Modele, trace);
        }

        public void AppliquerEtiquetteLienSelection(string texte)
        {
            if (_selectionLien == null || _mode == "lecture") return;
            Lien lien = _selectionLien.Modele;
            string propre = (texte ?? "").Trim();
            if (propre == (lien.Etiquette ?? "")) return;
            Historique.ExecuterEtPousser(new CommandeModifierEtiquetteLien(this, lien, propre));
        }

        public async void ChoisirCouleurLienPersonnalisee()
        {
            if (_selectionLien == null) return;
            Lien lien = _selectionLien.Modele;
            string hexa = await OuvrirSelecteurCouleur(lien.Couleur);
            if (hexa == null) return;
            if (Document.AjouterCouleurPersonnalisee(hexa))
                SignalerModification();
            ChangerStyleLien(lien, hexa, lien.Epaisseur, lien.Trait, lien.Fleche);
            SignalerFormatCible();
        }

        /// <summary>Les points d'ancrage d'un lien tel qu'il est tracé à l'écran.</summary>
        public bool PointsLien(Lien lien, out Point a, out Point b)
        {
            a = new Point();
            b = new Point();
            Rect rectSource, rectCible;
            if (!RectangleExtremite(lien.SourceId, out rectSource)
                || !RectangleExtremite(lien.CibleId, out rectCible)) return false;
            a = PointAncrage(rectSource, Centre(rectCible));
            b = PointAncrage(rectCible, Centre(rectSource));
            return true;
        }
    }
}
