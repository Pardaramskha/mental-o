using System.Collections.Generic;
using Mentalo.Modele;
using Mentalo.Vue;

namespace Mentalo.Historique
{
    public class CommandeCreerNoeud : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;

        public CommandeCreerNoeud(ToileVue toile, Noeud noeud)
        {
            _toile = toile;
            _noeud = noeud;
        }

        public void Executer() { _toile.AjouterNoeud(_noeud); }
        public void Annuler() { _toile.RetirerNoeud(_noeud); }
    }

    public class CommandeSupprimerNoeud : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly List<Lien> _liensAttaches;   // supprimés avec la boîte, restaurés avec elle
        private readonly List<Groupe> _groupes;       // appartenance restituée à l'annulation
        private readonly List<string> _idSeul;

        public CommandeSupprimerNoeud(ToileVue toile, Noeud noeud, List<Lien> liensAttaches)
        {
            _toile = toile;
            _noeud = noeud;
            _liensAttaches = liensAttaches ?? new List<Lien>();
            _groupes = new List<Groupe>();
            Groupe groupe = toile.GroupeDuNoeud(noeud);
            if (groupe != null) _groupes.Add(groupe);
            _idSeul = new List<string> { noeud.Id };
        }

        public void Executer()
        {
            foreach (Lien lien in _liensAttaches)
                _toile.RetirerLien(lien);
            foreach (Groupe groupe in _groupes)
                _toile.RetirerIdsDuGroupe(groupe, _idSeul);
            _toile.RetirerNoeud(_noeud);
        }

        public void Annuler()
        {
            _toile.AjouterNoeud(_noeud);
            foreach (Groupe groupe in _groupes)
                _toile.AjouterIdsAuGroupe(groupe, _idSeul);
            foreach (Lien lien in _liensAttaches)
                _toile.AjouterLien(lien);
        }
    }

    /// <summary>Un déplacement d'une ou plusieurs boîtes (groupe, sélection multiple).</summary>
    public class CommandeDeplacerNoeuds : ICommande
    {
        public class Deplacement
        {
            public Noeud Noeud;
            public double AncienX, AncienY, NouveauX, NouveauY;
        }

        private readonly ToileVue _toile;
        private readonly List<Deplacement> _deplacements;

        public CommandeDeplacerNoeuds(ToileVue toile, List<Deplacement> deplacements)
        {
            _toile = toile;
            _deplacements = deplacements;
        }

        public void Executer()
        {
            foreach (Deplacement d in _deplacements)
                _toile.PositionnerNoeud(d.Noeud, d.NouveauX, d.NouveauY);
        }

        public void Annuler()
        {
            foreach (Deplacement d in _deplacements)
                _toile.PositionnerNoeud(d.Noeud, d.AncienX, d.AncienY);
        }
    }

    public class CommandeModifierStyleNoeud : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly StyleBoite _ancien, _nouveau;

        public CommandeModifierStyleNoeud(ToileVue toile, Noeud noeud, StyleBoite nouveau)
        {
            _toile = toile;
            _noeud = noeud;
            _ancien = StyleBoite.Depuis(noeud);
            _nouveau = nouveau;
        }

        public void Executer() { _toile.AppliquerStyleNoeud(_noeud, _nouveau); }
        public void Annuler() { _toile.AppliquerStyleNoeud(_noeud, _ancien); }
    }

    public class CommandeRedimensionnerNoeud : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly double _ancienneLargeur, _nouvelleLargeur; // 0 = automatique

        public CommandeRedimensionnerNoeud(ToileVue toile, Noeud noeud, double ancienneLargeur, double nouvelleLargeur)
        {
            _toile = toile;
            _noeud = noeud;
            _ancienneLargeur = ancienneLargeur;
            _nouvelleLargeur = nouvelleLargeur;
        }

        public void Executer() { _toile.DefinirLargeurNoeud(_noeud, _nouvelleLargeur); }
        public void Annuler() { _toile.DefinirLargeurNoeud(_noeud, _ancienneLargeur); }
    }

    public class CommandeDefinirImage : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly byte[] _anciensOctets, _nouveauxOctets;
        private readonly string _ancienneExtension, _nouvelleExtension;
        private readonly double _ancienneLargeur, _ancienneHauteur, _nouvelleLargeur, _nouvelleHauteur;

        public CommandeDefinirImage(ToileVue toile, Noeud noeud,
            byte[] octets, string extension, double largeur, double hauteur)
        {
            _toile = toile;
            _noeud = noeud;
            _anciensOctets = noeud.ImageDonnees;
            _ancienneExtension = noeud.ImageExtension;
            _ancienneLargeur = noeud.ImageLargeur;
            _ancienneHauteur = noeud.ImageHauteur;
            _nouveauxOctets = octets;
            _nouvelleExtension = extension;
            _nouvelleLargeur = largeur;
            _nouvelleHauteur = hauteur;
        }

        public void Executer()
        {
            _toile.DefinirImageNoeud(_noeud, _nouveauxOctets, _nouvelleExtension, _nouvelleLargeur, _nouvelleHauteur);
        }

        public void Annuler()
        {
            _toile.DefinirImageNoeud(_noeud, _anciensOctets, _ancienneExtension, _ancienneLargeur, _ancienneHauteur);
        }
    }

    /// <summary>Toute modification des fichiers attachés d'une boîte (ajout, retrait,
    /// renommage, remplacement, intégration au .tea) : on remplace la liste entière,
    /// les opérations construisant une liste neuve à partir de copies.</summary>
    public class CommandeModifierFichiers : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly List<PieceJointe> _anciens, _nouveaux;

        public CommandeModifierFichiers(ToileVue toile, Noeud noeud, List<PieceJointe> nouveaux)
        {
            _toile = toile;
            _noeud = noeud;
            _anciens = noeud.Fichiers;
            _nouveaux = nouveaux;
        }

        public void Executer() { _toile.DefinirFichiersNoeud(_noeud, _nouveaux); }
        public void Annuler() { _toile.DefinirFichiersNoeud(_noeud, _anciens); }
    }

    /// <summary>La note cachée d'une boîte (v0.15).</summary>
    public class CommandeModifierNote : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly string _ancienne, _nouvelle;

        public CommandeModifierNote(ToileVue toile, Noeud noeud, string nouvelle)
        {
            _toile = toile;
            _noeud = noeud;
            _ancienne = noeud.Note ?? "";
            _nouvelle = nouvelle ?? "";
        }

        public void Executer() { _toile.DefinirNoteNoeud(_noeud, _nouvelle); }
        public void Annuler() { _toile.DefinirNoteNoeud(_noeud, _ancienne); }
    }

    /// <summary>Les étiquettes portées par une boîte (v0.15) : liste entière remplacée.</summary>
    public class CommandeModifierEtiquettes : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly List<string> _anciennes, _nouvelles;

        public CommandeModifierEtiquettes(ToileVue toile, Noeud noeud, List<string> nouvelles)
        {
            _toile = toile;
            _noeud = noeud;
            _anciennes = new List<string>(noeud.Etiquettes);
            _nouvelles = nouvelles;
        }

        public void Executer() { _toile.DefinirEtiquettesNoeud(_noeud, _nouvelles); }
        public void Annuler() { _toile.DefinirEtiquettesNoeud(_noeud, _anciennes); }
    }

    /// <summary>Les définitions d'étiquettes du document (création, renommage, couleur,
    /// suppression) — avec, en cas de renommage/suppression, la retouche des boîtes.</summary>
    public class CommandeModifierDefinitionsEtiquettes : ICommande
    {
        private readonly ToileVue _toile;
        private readonly List<Etiquette> _anciennes, _nouvelles;
        private readonly Dictionary<Noeud, List<string>> _anciensPorteurs = new Dictionary<Noeud, List<string>>();
        private readonly Dictionary<Noeud, List<string>> _nouveauxPorteurs;

        public CommandeModifierDefinitionsEtiquettes(ToileVue toile, List<Etiquette> nouvelles,
            Dictionary<Noeud, List<string>> nouveauxPorteurs)
        {
            _toile = toile;
            _anciennes = new List<Etiquette>();
            foreach (Etiquette e in toile.Document.Etiquettes) _anciennes.Add(e.Cloner());
            _nouvelles = nouvelles;
            _nouveauxPorteurs = nouveauxPorteurs ?? new Dictionary<Noeud, List<string>>();
            foreach (KeyValuePair<Noeud, List<string>> kv in _nouveauxPorteurs)
                _anciensPorteurs[kv.Key] = new List<string>(kv.Key.Etiquettes);
        }

        public void Executer()
        {
            _toile.DefinirDefinitionsEtiquettes(_nouvelles);
            foreach (KeyValuePair<Noeud, List<string>> kv in _nouveauxPorteurs)
                _toile.DefinirEtiquettesNoeud(kv.Key, kv.Value);
        }

        public void Annuler()
        {
            _toile.DefinirDefinitionsEtiquettes(_anciennes);
            foreach (KeyValuePair<Noeud, List<string>> kv in _anciensPorteurs)
                _toile.DefinirEtiquettesNoeud(kv.Key, kv.Value);
        }
    }

    public class CommandeRedimensionnerImage : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly double _ancienneLargeur, _ancienneHauteur, _nouvelleLargeur, _nouvelleHauteur;

        public CommandeRedimensionnerImage(ToileVue toile, Noeud noeud,
            double ancienneLargeur, double ancienneHauteur,
            double nouvelleLargeur, double nouvelleHauteur)
        {
            _toile = toile;
            _noeud = noeud;
            _ancienneLargeur = ancienneLargeur;
            _ancienneHauteur = ancienneHauteur;
            _nouvelleLargeur = nouvelleLargeur;
            _nouvelleHauteur = nouvelleHauteur;
        }

        public void Executer() { _toile.DefinirTailleImage(_noeud, _nouvelleLargeur, _nouvelleHauteur); }
        public void Annuler() { _toile.DefinirTailleImage(_noeud, _ancienneLargeur, _ancienneHauteur); }
    }

    public class CommandeModifierTexteRiche : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Noeud _noeud;
        private readonly List<Segment> _anciens, _anciensApres, _nouveaux, _nouveauxApres;
        private readonly string _ancienAlignement, _nouvelAlignement;

        public CommandeModifierTexteRiche(ToileVue toile, Noeud noeud,
            List<Segment> anciens, List<Segment> anciensApres, string ancienAlignement,
            List<Segment> nouveaux, List<Segment> nouveauxApres, string nouvelAlignement)
        {
            _toile = toile;
            _noeud = noeud;
            _anciens = anciens;
            _anciensApres = anciensApres;
            _ancienAlignement = ancienAlignement;
            _nouveaux = nouveaux;
            _nouveauxApres = nouveauxApres;
            _nouvelAlignement = nouvelAlignement;
        }

        public void Executer() { _toile.DefinirTexteRiche(_noeud, _nouveaux, _nouveauxApres, _nouvelAlignement); }
        public void Annuler() { _toile.DefinirTexteRiche(_noeud, _anciens, _anciensApres, _ancienAlignement); }
    }
}
