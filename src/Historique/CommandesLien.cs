using Mentalo.Modele;
using Mentalo.Vue;

namespace Mentalo.Historique
{
    public class CommandeCreerLien : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Lien _lien;

        public CommandeCreerLien(ToileVue toile, Lien lien)
        {
            _toile = toile;
            _lien = lien;
        }

        public void Executer() { _toile.AjouterLien(_lien); }
        public void Annuler() { _toile.RetirerLien(_lien); }
    }

    public class CommandeSupprimerLien : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Lien _lien;

        public CommandeSupprimerLien(ToileVue toile, Lien lien)
        {
            _toile = toile;
            _lien = lien;
        }

        public void Executer() { _toile.RetirerLien(_lien); }
        public void Annuler() { _toile.AjouterLien(_lien); }
    }

    public class CommandeModifierStyleLien : ICommande
    {
        private readonly ToileVue _toile;
        private readonly Lien _lien;
        private readonly string _ancienneCouleur, _ancienTrait, _ancienneFleche;
        private readonly double _ancienneEpaisseur;
        private readonly string _nouvelleCouleur, _nouveauTrait, _nouvelleFleche;
        private readonly double _nouvelleEpaisseur;

        public CommandeModifierStyleLien(ToileVue toile, Lien lien,
            string nouvelleCouleur, double nouvelleEpaisseur, string nouveauTrait, string nouvelleFleche)
        {
            _toile = toile;
            _lien = lien;
            _ancienneCouleur = lien.Couleur;
            _ancienneEpaisseur = lien.Epaisseur;
            _ancienTrait = lien.Trait;
            _ancienneFleche = lien.Fleche;
            _nouvelleCouleur = nouvelleCouleur;
            _nouvelleEpaisseur = nouvelleEpaisseur;
            _nouveauTrait = nouveauTrait;
            _nouvelleFleche = nouvelleFleche;
        }

        public void Executer()
        {
            _toile.AppliquerStyleLien(_lien, _nouvelleCouleur, _nouvelleEpaisseur, _nouveauTrait, _nouvelleFleche);
        }

        public void Annuler()
        {
            _toile.AppliquerStyleLien(_lien, _ancienneCouleur, _ancienneEpaisseur, _ancienTrait, _ancienneFleche);
        }
    }
}
