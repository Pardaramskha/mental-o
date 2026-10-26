using System;
using System.Threading.Tasks;
using Mentalo.Parametres;

namespace Mentalo
{
    public enum BoutonsMessage { OK, OKAnnuler, OuiNon, OuiNonAnnuler }
    public enum IconeMessage { Aucune, Information, Question, Avertissement, Erreur }
    public enum ResultatMessage { Aucun, OK, Annuler, Oui, Non }

    /// <summary>Ce que le canevas demande à son hôte : afficher un message,
    /// enregistrer les réglages globaux. Mental-O seul répond avec ses propres
    /// dialogues et son fichier de réglages (HoteStandard) ; Marabook, où
    /// Mental-O vit en DLC, pose le sien avant de créer une toile. Le canevas
    /// ne connaît que cette façade : c'est la couture qui laisse le cœur
    /// partagé entre les deux applications. Les messages sont asynchrones
    /// (Avalonia n'a pas de dialogue modal bloquant).</summary>
    public interface IHote
    {
        /// <summary>Un message à l'utilisateur ; « proprietaire » est la
        /// fenêtre (Avalonia.Controls.Window) ou null.</summary>
        Task<ResultatMessage> Message(object proprietaire, string texte, string titre, BoutonsMessage boutons, IconeMessage icone);
        void EnregistrerParametres();
    }

    /// <summary>L'hôte par défaut : écrit les réglages dans %APPDATA%\Mental-o ;
    /// les messages passent par un délégué posé par l'application (le cœur
    /// n'a pas de fenêtre).</summary>
    public class HoteStandard : IHote
    {
        /// <summary>Posé par l'application : sa boîte de message.</summary>
        public static Func<object, string, string, BoutonsMessage, IconeMessage, Task<ResultatMessage>> AfficherMessage;

        public Task<ResultatMessage> Message(object proprietaire, string texte, string titre, BoutonsMessage boutons, IconeMessage icone)
        {
            if (AfficherMessage != null) return AfficherMessage(proprietaire, texte, titre, boutons, icone);
            Console.Error.WriteLine("[Mental-O] " + titre + " : " + texte);
            return Task.FromResult(boutons == BoutonsMessage.OuiNon || boutons == BoutonsMessage.OuiNonAnnuler ? ResultatMessage.Oui : ResultatMessage.OK);
        }

        public void EnregistrerParametres()
        {
            ParametresGlobaux.Enregistrer();
        }
    }

    public static class Hote
    {
        /// <summary>L'hôte courant — Mental-O seul tant que personne n'en pose un autre.</summary>
        public static IHote Courant = new HoteStandard();

        public static Task<ResultatMessage> Message(object proprietaire, string texte, string titre, BoutonsMessage boutons, IconeMessage icone)
        {
            return Courant.Message(proprietaire, texte, titre, boutons, icone);
        }

        public static Task<ResultatMessage> Message(object proprietaire, string texte, string titre, BoutonsMessage boutons)
        {
            return Courant.Message(proprietaire, texte, titre, boutons, IconeMessage.Aucune);
        }

        public static void EnregistrerParametres()
        {
            Courant.EnregistrerParametres();
        }
    }
}
