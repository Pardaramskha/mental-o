using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>LES LIENS VERS L'HÔTE (26/10/2026) : quand Mental-O vit en DLC de
    /// Marabook, une boîte peut porter un lien vers une fiche, un écrit ou la
    /// tuile de n'importe quel élément du projet. Côté toile, c'est une pièce
    /// jointe « hote:<cible> » (PieceJointe.DepuisHote) : annulable, enregistrée
    /// dans le .tea, rendue par l'hôte (NoeudVisuel.RenduHote) et ouverte par
    /// lui (OuvrirHote). Mental-O seul ne pose aucun des deux : le lien se
    /// montre alors comme une ligne portant le nom enregistré.</summary>
    public partial class ToileVue
    {
        /// <summary>Ouvre l'élément visé chez l'hôte ; null sans hôte.</summary>
        public static Action<PieceJointe> OuvrirHote;

        /// <summary>Attache un lien vers l'hôte à la boîte sélectionnée — ou, sans
        /// sélection, dans une boîte neuve au centre de la vue. « affichage » : null
        /// pour une ligne de lien, « normal » ou « condense » pour une tuile.</summary>
        public void AttacherLienHote(string cible, string nom, string affichage)
        {
            if (_mode == "lecture" || string.IsNullOrEmpty(cible)) return;
            PieceJointe piece = PieceJointe.DepuisHote(cible, nom, affichage);
            if (_selection != null)
            {
                List<PieceJointe> nouveaux = CopieFichiers(_selection.Modele);
                nouveaux.Add(piece);
                Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, _selection.Modele, nouveaux));
                _selection.RedonnerFocus();
                return;
            }
            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.Fichiers.Add(piece);
            Point centre = CentreCanvas();
            noeud.X = centre.X - 80;
            noeud.Y = centre.Y - 14;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel)) Selectionner(visuel);
        }

        /// <summary>Les boîtes qui portent un lien vers l'hôte se redessinent (un
        /// titre ou une couleur a changé chez lui).</summary>
        public void RafraichirLiensHote()
        {
            foreach (KeyValuePair<Noeud, NoeudVisuel> kv in _visuels)
            {
                bool porte = false;
                foreach (PieceJointe piece in kv.Key.Fichiers) if (piece.EstLienHote) { porte = true; break; }
                if (porte) kv.Value.MettreAJour();
            }
        }

        private void ChangerAffichageHote(Noeud noeud, PieceJointe piece, string affichage)
        {
            if (piece.Affichage == affichage) return;
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            PieceJointe copie = TrouverCopie(nouveaux, piece);
            if (copie == null) return;
            copie.Affichage = affichage;
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        /// <summary>Clic sur un lien vers l'hôte : gauche = ouvrir l'élément ; droit =
        /// le menu (ouvrir, présentation normale / condensée d'une tuile, renommer
        /// une ligne de lien, supprimer).</summary>
        private void OuvrirMenuHote(NoeudVisuel visuel, PieceJointe piece, Control ancre, bool droit)
        {
            Noeud noeud = visuel.Modele;
            if (!_selectionnes.Contains(visuel)) Selectionner(visuel);
            if (!droit)
            {
                if (OuvrirHote != null) OuvrirHote(piece);
                return;
            }
            ContextMenu menu = new ContextMenu();
            MenuItem ouvrir = Ui.Element("Ouvrir", delegate { if (OuvrirHote != null) OuvrirHote(piece); });
            ouvrir.FontWeight = FontWeight.SemiBold;
            ouvrir.IsEnabled = OuvrirHote != null;
            menu.Items.Add(ouvrir);
            menu.Items.Add(Ui.Separateur());
            if (piece.Affichage != null)
            {
                MenuItem normal = Ui.Element("Normal", delegate { ChangerAffichageHote(noeud, piece, "normal"); }, piece.Affichage != "condense");
                Ui.Infobulle(normal, "La tuile telle qu'elle apparaît dans son tableau");
                menu.Items.Add(normal);
                MenuItem condense = Ui.Element("Condensé", delegate { ChangerAffichageHote(noeud, piece, "condense"); }, piece.Affichage == "condense");
                Ui.Infobulle(condense, "Le titre et la couleur seulement");
                menu.Items.Add(condense);
            }
            else
                menu.Items.Add(Ui.Element("Renommer le lien…", delegate { RenommerLien(noeud, piece); }));
            menu.Items.Add(Ui.Separateur());
            menu.Items.Add(Ui.Element("Supprimer le lien", delegate { SupprimerLien(noeud, piece); }));
            Ui.OuvrirMenuSous(menu, ancre);
        }
    }
}
