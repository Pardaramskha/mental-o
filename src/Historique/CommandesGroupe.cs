using System.Collections.Generic;
using Mentalo.Modele;
using Mentalo.Vue;

namespace Mentalo.Historique
{
    /// <summary>Enchaîne plusieurs commandes en une seule étape d'annulation.</summary>
    public class CommandeComposite : ICommande
    {
        private readonly List<ICommande> _commandes;

        public CommandeComposite(List<ICommande> commandes)
        {
            _commandes = commandes;
        }

        public void Executer()
        {
            foreach (ICommande commande in _commandes)
                commande.Executer();
        }

        public void Annuler()
        {
            for (int i = _commandes.Count - 1; i >= 0; i--)
                _commandes[i].Annuler();
        }
    }

    public class CommandeCreerGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        // Un nœud n'appartient qu'à un groupe : le nouveau absorbe, l'annulation restitue.
        private readonly Dictionary<Groupe, List<string>> _retraits;

        public CommandeCreerGroupe(ToileVue toile, Groupe groupe, Dictionary<Groupe, List<string>> retraits)
        {
            _toile = toile;
            _groupe = groupe;
            _retraits = retraits;
        }

        public void Executer()
        {
            foreach (KeyValuePair<Groupe, List<string>> retrait in _retraits)
                _toile.RetirerIdsDuGroupe(retrait.Key, retrait.Value);
            _toile.AjouterGroupe(_groupe);
        }

        public void Annuler()
        {
            _toile.RetirerGroupe(_groupe);
            foreach (KeyValuePair<Groupe, List<string>> retrait in _retraits)
                _toile.AjouterIdsAuGroupe(retrait.Key, retrait.Value);
        }
    }

    public class CommandeSupprimerGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        private readonly List<Lien> _liensAttaches; // liens dont le cadre est une extrémité

        public CommandeSupprimerGroupe(ToileVue toile, Groupe groupe)
        {
            _toile = toile;
            _groupe = groupe;
            _liensAttaches = new List<Lien>();
            foreach (Lien lien in toile.Document.Liens)
                if (lien.Concerne(groupe.Id)) _liensAttaches.Add(lien);
        }

        public void Executer()
        {
            foreach (Lien lien in _liensAttaches)
                _toile.RetirerLien(lien);
            _toile.RetirerGroupe(_groupe);
        }

        public void Annuler()
        {
            _toile.AjouterGroupe(_groupe);
            foreach (Lien lien in _liensAttaches)
                _toile.AjouterLien(lien);
        }
    }

    /// <summary>Plier / déplier un groupe (v0.15).</summary>
    public class CommandePlierGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        private readonly bool _plie;

        public CommandePlierGroupe(ToileVue toile, Groupe groupe, bool plie)
        {
            _toile = toile;
            _groupe = groupe;
            _plie = plie;
        }

        public void Executer() { _toile.DefinirPliageGroupe(_groupe, _plie); }
        public void Annuler() { _toile.DefinirPliageGroupe(_groupe, !_plie); }
    }

    /// <summary>Rang d'un groupe dans le mode présentation (v0.17).</summary>
    public class CommandeOrdrePresentationGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        private readonly int _ancien, _nouveau;

        public CommandeOrdrePresentationGroupe(ToileVue toile, Groupe groupe, int nouveau)
        {
            _toile = toile;
            _groupe = groupe;
            _ancien = groupe.OrdrePresentation;
            _nouveau = nouveau;
        }

        public void Executer() { _toile.DefinirOrdrePresentation(_groupe, _nouveau); }
        public void Annuler() { _toile.DefinirOrdrePresentation(_groupe, _ancien); }
    }

    public class CommandeRedimensionnerGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        private readonly double _ancienneLargeur, _ancienneHauteur, _nouvelleLargeur, _nouvelleHauteur;

        public CommandeRedimensionnerGroupe(ToileVue toile, Groupe groupe,
            double ancienneLargeur, double ancienneHauteur,
            double nouvelleLargeur, double nouvelleHauteur)
        {
            _toile = toile;
            _groupe = groupe;
            _ancienneLargeur = ancienneLargeur;
            _ancienneHauteur = ancienneHauteur;
            _nouvelleLargeur = nouvelleLargeur;
            _nouvelleHauteur = nouvelleHauteur;
        }

        public void Executer() { _toile.DefinirTailleGroupe(_groupe, _nouvelleLargeur, _nouvelleHauteur); }
        public void Annuler() { _toile.DefinirTailleGroupe(_groupe, _ancienneLargeur, _ancienneHauteur); }
    }

    public class CommandeModifierTitreGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        private readonly string _ancienTitre, _anciennePosition, _nouveauTitre, _nouvellePosition;

        public CommandeModifierTitreGroupe(ToileVue toile, Groupe groupe, string titre, string position)
        {
            _toile = toile;
            _groupe = groupe;
            _ancienTitre = groupe.Titre;
            _anciennePosition = groupe.TitrePosition;
            _nouveauTitre = titre;
            _nouvellePosition = position;
        }

        public void Executer() { _toile.AppliquerTitreGroupe(_groupe, _nouveauTitre, _nouvellePosition); }
        public void Annuler() { _toile.AppliquerTitreGroupe(_groupe, _ancienTitre, _anciennePosition); }
    }

    public class CommandeModifierStyleGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        private readonly string _ancienneCouleur, _ancienTrait, _nouvelleCouleur, _nouveauTrait;
        private readonly double _ancienneEpaisseur, _nouvelleEpaisseur;

        public CommandeModifierStyleGroupe(ToileVue toile, Groupe groupe,
            string couleur, string trait, double epaisseur)
        {
            _toile = toile;
            _groupe = groupe;
            _ancienneCouleur = groupe.Couleur;
            _ancienTrait = groupe.BordureTrait;
            _ancienneEpaisseur = groupe.BordureEpaisseur;
            _nouvelleCouleur = couleur;
            _nouveauTrait = trait;
            _nouvelleEpaisseur = epaisseur;
        }

        public void Executer() { _toile.AppliquerStyleGroupe(_groupe, _nouvelleCouleur, _nouveauTrait, _nouvelleEpaisseur); }
        public void Annuler() { _toile.AppliquerStyleGroupe(_groupe, _ancienneCouleur, _ancienTrait, _ancienneEpaisseur); }
    }

    public class CommandeModifierStyleTitreGroupe : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Groupe _groupe;
        private readonly string _ancienneCouleur, _ancienFond, _nouvelleCouleur, _nouveauFond;
        private readonly double _ancienneTaille, _nouvelleTaille;

        public CommandeModifierStyleTitreGroupe(ToileVue toile, Groupe groupe,
            string couleurTitre, string fondTitre, double taille)
        {
            _toile = toile;
            _groupe = groupe;
            _ancienneCouleur = groupe.TitreCouleur;
            _ancienFond = groupe.TitreFond;
            _ancienneTaille = groupe.TitreTaille;
            _nouvelleCouleur = couleurTitre;
            _nouveauFond = fondTitre;
            _nouvelleTaille = taille;
        }

        public void Executer() { _toile.AppliquerStyleTitreGroupe(_groupe, _nouvelleCouleur, _nouveauFond, _nouvelleTaille); }
        public void Annuler() { _toile.AppliquerStyleTitreGroupe(_groupe, _ancienneCouleur, _ancienFond, _ancienneTaille); }
    }
}
