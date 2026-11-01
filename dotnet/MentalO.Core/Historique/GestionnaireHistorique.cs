using System;
using System.Collections.Generic;

namespace Mentalo.Historique
{
    /// <summary>Une action réversible sur le document.</summary>
    public interface ICommande
    {
        void Executer();
        void Annuler();
    }

    /// <summary>Piles annuler/rétablir. Le raccourci Ctrl+Z est déjà branché (bonus sur le plan v0.7).</summary>
    public class GestionnaireHistorique
    {
        private readonly Stack<ICommande> _pileAnnuler = new Stack<ICommande>();
        private readonly Stack<ICommande> _pileRefaire = new Stack<ICommande>();

        public event Action Change;

        public bool PeutAnnuler { get { return _pileAnnuler.Count > 0; } }
        public bool PeutRefaire { get { return _pileRefaire.Count > 0; } }
        public int NbAnnulables { get { return _pileAnnuler.Count; } }
        public int NbRefaisables { get { return _pileRefaire.Count; } }

        public void ExecuterEtPousser(ICommande commande)
        {
            commande.Executer();
            Pousser(commande);
        }

        /// <summary>Pour les actions déjà appliquées à l'écran (ex. fin d'un glisser).</summary>
        public void Pousser(ICommande commande)
        {
            _pileAnnuler.Push(commande);
            _pileRefaire.Clear();
            Signaler();
        }

        public void Annuler()
        {
            if (_pileAnnuler.Count == 0) return;
            ICommande c = _pileAnnuler.Pop();
            c.Annuler();
            _pileRefaire.Push(c);
            Signaler();
        }

        public void Refaire()
        {
            if (_pileRefaire.Count == 0) return;
            ICommande c = _pileRefaire.Pop();
            c.Executer();
            _pileAnnuler.Push(c);
            Signaler();
        }

        /// <summary>Annule la dernière commande sans la garder (boîte créée puis laissée vide).</summary>
        public void AnnulerEtOublier()
        {
            if (_pileAnnuler.Count == 0) return;
            _pileAnnuler.Pop().Annuler();
            Signaler();
        }

        public void Vider()
        {
            _pileAnnuler.Clear();
            _pileRefaire.Clear();
            Signaler();
        }

        private void Signaler()
        {
            Action h = Change;
            if (h != null) h();
        }
    }
}
