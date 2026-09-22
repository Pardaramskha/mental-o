using System;
using System.Windows;
using Mentalo.Parametres;
using Mentalo.Vue;

namespace Mentalo
{
    /// <summary>Ce que le canevas demande à son hôte (22/09/2026) : afficher un
    /// message, enregistrer les réglages globaux. Mental-o seul répond avec ses
    /// propres dialogues et son fichier de réglages (HoteStandard) ; un autre
    /// hôte — Marabook, où Mental-o vit en DLC — pose le sien avant de créer
    /// une toile. Le canevas (ToileVue, NoeudVisuel) ne connaît que cette
    /// façade : c'est la couture qui laisse le cœur partagé entre les deux
    /// applications.</summary>
    public interface IHote
    {
        MessageBoxResult Message(Window proprietaire, string texte, string titre, MessageBoxButton boutons, MessageBoxImage icone);
        void EnregistrerParametres();
    }

    /// <summary>L'hôte de Mental-o seul : ses dialogues, ses réglages.</summary>
    public class HoteStandard : IHote
    {
        public MessageBoxResult Message(Window proprietaire, string texte, string titre, MessageBoxButton boutons, MessageBoxImage icone)
        {
            return DialogueMessage.Afficher(proprietaire, texte, titre, boutons, icone);
        }

        public void EnregistrerParametres()
        {
            ParametresGlobaux.Enregistrer();
        }
    }

    public static class Hote
    {
        /// <summary>L'hôte courant — Mental-o seul tant que personne n'en pose un autre.</summary>
        public static IHote Courant = new HoteStandard();

        public static MessageBoxResult Message(Window proprietaire, string texte, string titre, MessageBoxButton boutons, MessageBoxImage icone)
        {
            return Courant.Message(proprietaire, texte, titre, boutons, icone);
        }

        public static MessageBoxResult Message(Window proprietaire, string texte, string titre, MessageBoxButton boutons)
        {
            return Courant.Message(proprietaire, texte, titre, boutons, MessageBoxImage.None);
        }

        public static void EnregistrerParametres()
        {
            Courant.EnregistrerParametres();
        }
    }
}
