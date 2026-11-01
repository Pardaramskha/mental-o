using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Ce que les commandes annulables et les exports demandent à la
    /// toile (26/10/2026) : les mutations du document reflétées à l'écran, et
    /// les mesures (rectangles, points d'ancrage). La toile Avalonia
    /// (MentalO.Canvas) l'implémente ; le cœur n'a pas de fenêtre.</summary>
    public interface IToile
    {
        DocumentCarte Document { get; }

        // --- boîtes
        void AjouterNoeud(Noeud noeud);
        void RetirerNoeud(Noeud noeud);
        void PositionnerNoeud(Noeud noeud, double x, double y);
        void DefinirTexteRiche(Noeud noeud, List<Segment> segments, List<Segment> segmentsApres, string alignement);
        void AppliquerStyleNoeud(Noeud noeud, StyleBoite style);
        void DefinirLargeurNoeud(Noeud noeud, double largeur);
        void DefinirImageNoeud(Noeud noeud, byte[] octets, string extension, double largeur, double hauteur);
        void DefinirTailleImage(Noeud noeud, double largeur, double hauteur);
        void DefinirFichiersNoeud(Noeud noeud, List<PieceJointe> fichiers);
        void DefinirNoteNoeud(Noeud noeud, string note);
        void DefinirEtiquettesNoeud(Noeud noeud, List<string> etiquettes);
        void DefinirDefinitionsEtiquettes(List<Etiquette> definitions);
        Groupe GroupeDuNoeud(Noeud noeud);

        // --- liens
        void AjouterLien(Lien lien);
        void RetirerLien(Lien lien);
        void AppliquerStyleLien(Lien lien, string couleur, double epaisseur, string trait, string fleche);
        void AppliquerTraceLien(Lien lien, string trace, double courbure);
        void AppliquerEtiquetteLien(Lien lien, string etiquette);

        // --- groupes
        void AjouterGroupe(Groupe groupe);
        void RetirerGroupe(Groupe groupe);
        void RetirerIdsDuGroupe(Groupe groupe, List<string> ids);
        void AjouterIdsAuGroupe(Groupe groupe, List<string> ids);
        void DefinirPliageGroupe(Groupe groupe, bool plie);
        void DefinirOrdrePresentation(Groupe groupe, int ordre);
        void DefinirTailleGroupe(Groupe groupe, double largeur, double hauteur);
        void AppliquerTitreGroupe(Groupe groupe, string titre, string position);
        void AppliquerStyleGroupe(Groupe groupe, string couleur, string trait, double epaisseur);
        void AppliquerStyleTitreGroupe(Groupe groupe, string couleurTitre, string fondTitre, double taille);

        // --- mesures (exports)
        Rect RectangleDuNoeud(Noeud noeud);
        Rect RectangleDuGroupe(Groupe groupe);
        bool PointsLien(Lien lien, out Point a, out Point b);
        Rect BornesContenu();
        Color CouleurFondEffective();
    }
}
