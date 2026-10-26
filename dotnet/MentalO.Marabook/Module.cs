using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Marabook.App;
using Mentalo;
using Mentalo.Modele;
using Mentalo.Parametres;
using Mentalo.Persistance;
using Mentalo.Vue;

namespace MentalO.Marabook
{
    /// <summary>LE MODULE (26/10/2026) : Mental-O en DLC de Marabook, sur Avalonia.
    /// Marabook charge cette DLL (« entry » du manifeste), instancie ce type, lui
    /// tend son hôte ; en retour le module fournit les cartes mentales — un éditeur
    /// incrusté, une carte neuve, une vignette pour les tuiles — et un onglet
    /// « Mental-O (dlc) » pour les Préférences. La toile (MentalO.Canvas) est la
    /// même que celle de Mental-O seul ; seule la façade Hote change.</summary>
    public class Module : IMarabookModule, IMindMapProvider
    {
        private IModuleHost _host;

        public string Id { get { return "mental-o"; } }

        public IMindMapProvider MindMaps { get { return this; } }

        public string PreferencesTitle { get { return "Mental-O (dlc)"; } }

        public void Initialize(IModuleHost host)
        {
            _host = host;
            Hote.Courant = new HoteMarabook(host);
            // Les réglages de Mental-O vivent à part de ceux de Mental-O seul :
            // %APPDATA%\Marabook\mental-o\parametres.json (raccourcis, favoris,
            // magnétisme, poignées…) ; le thème suit Marabook (clair / sombre).
            try
            {
                ParametresGlobaux.DossierReglages = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Marabook", "mental-o");
            }
            catch { }
            ParametresGlobaux.Charger();
            ParametresGlobaux.ThemeSombre = host.DarkTheme;
            Mentalo.Vue.Chrome.Basculer(host.DarkTheme);
            host.ThemeChanged += delegate
            {
                ParametresGlobaux.ThemeSombre = host.DarkTheme;
                Mentalo.Vue.Chrome.Basculer(host.DarkTheme);
                MindMapEditor editeur = MindMapEditor.Courant;
                if (editeur != null) editeur.ThemeChange();
            };
        }

        public IMindMapEditor CreateEditor()
        {
            return new MindMapEditor(_host);
        }

        /// <summary>Une carte vierge : le document par défaut de Mental-O, au
        /// nom de l'élément, sérialisé en .tea.</summary>
        public byte[] NewMap(string title)
        {
            DocumentCarte document = new DocumentCarte();
            document.NomAffichage = title ?? "";
            return Fichier.EnregistrerEnMemoire(document, null);
        }

        /// <summary>Une carte garnie pour le projet d'exemple de Marabook : cinq
        /// boîtes, des liens fléchés et courbes, un groupe, une étiquette.</summary>
        public byte[] SampleMap(string title)
        {
            DocumentCarte document = new DocumentCarte();
            document.NomAffichage = title ?? "";
            document.Etiquettes.Add(new Etiquette("Intrigue", "#E67E22"));
            Noeud racine = Boite(document, "Le marabout et la mer", 300, 40, "#FFF3A0");
            racine.Segments[0].Gras = true;
            racine.Segments[0].Taille = 18;
            Noeud vent = Boite(document, "Le vent des pins\nL'appel du large", 60, 180, null);
            Noeud maison = Boite(document, "La maison aux volets clos", 300, 200, null);
            Noeud maree = Boite(document, "La grande marée", 560, 180, null);
            Noeud keira = Boite(document, "Keira Varenh\n• têtue\n• loyale", 300, 330, "#C5E4F7");
            keira.Etiquettes.Add("Intrigue");
            keira.Note = "Protagoniste : elle ramène le marabout au rivage.";
            Relier(document, racine, vent, "simple", "droit");
            Relier(document, racine, maison, "simple", "droit");
            Relier(document, racine, maree, "simple", "droit");
            Lien courbe = Relier(document, maison, keira, "double", "courbe");
            courbe.Courbure = 40;
            courbe.Etiquette = "habite";
            Groupe actes = document.NouveauGroupe();
            actes.Titre = "Les trois actes";
            actes.NoeudIds.Add(vent.Id); actes.NoeudIds.Add(maison.Id); actes.NoeudIds.Add(maree.Id);
            document.Groupes.Add(actes);
            return Fichier.EnregistrerEnMemoire(document, null);
        }

        private static Noeud Boite(DocumentCarte document, string texte, double x, double y, string fond)
        {
            Noeud noeud = new Noeud();
            if (document.BoiteParDefaut != null) document.BoiteParDefaut.AppliquerA(noeud);
            noeud.DefinirTexteSimple(texte);
            noeud.X = x; noeud.Y = y;
            if (fond != null) noeud.FondCouleur = fond;
            document.Noeuds.Add(noeud);
            return noeud;
        }

        private static Lien Relier(DocumentCarte document, Noeud a, Noeud b, string fleche, string trace)
        {
            Lien lien = document.NouveauLien();
            lien.SourceId = a.Id; lien.CibleId = b.Id; lien.Fleche = fleche; lien.Trace = trace;
            document.Liens.Add(lien);
            return lien;
        }

        public Control Thumbnail(byte[] tea, double width, double height)
        {
            return Vignette.Dessiner(tea, width, height);
        }

        /// <summary>L'onglet des Préférences de Marabook : les mêmes réglages que
        /// Fichier > Paramètres de Mental-O seul (raccourcis, édition, apparence),
        /// sans le thème (celui de Marabook s'impose).</summary>
        public Control PreferencesTab()
        {
            return new PreferencesTab(_host).Build();
        }
    }

    /// <summary>L'hôte que la toile de Mental-O voit quand elle vit dans Marabook :
    /// les messages passent par le dialogue de Marabook (même thème que le reste
    /// de la fenêtre) ; les réglages s'écrivent dans le dossier de Marabook.</summary>
    public class HoteMarabook : IHote
    {
        private readonly IModuleHost _host;

        public HoteMarabook(IModuleHost host)
        {
            _host = host;
        }

        public async Task<ResultatMessage> Message(object proprietaire, string texte, string titre, BoutonsMessage boutons, IconeMessage icone)
        {
            MessageButtons b = boutons == BoutonsMessage.OKAnnuler ? MessageButtons.OKCancel
                : boutons == BoutonsMessage.OuiNon ? MessageButtons.YesNo
                : boutons == BoutonsMessage.OuiNonAnnuler ? MessageButtons.YesNoCancel
                : MessageButtons.OK;
            MessageIcon i = icone == IconeMessage.Information ? MessageIcon.Information
                : icone == IconeMessage.Question ? MessageIcon.Question
                : icone == IconeMessage.Avertissement ? MessageIcon.Warning
                : icone == IconeMessage.Erreur ? MessageIcon.Error
                : MessageIcon.None;
            MessageResult resultat = await _host.Message(texte, titre, b, i);
            switch (resultat)
            {
                case MessageResult.OK: return ResultatMessage.OK;
                case MessageResult.Yes: return ResultatMessage.Oui;
                case MessageResult.No: return ResultatMessage.Non;
                case MessageResult.Cancel: return ResultatMessage.Annuler;
                default: return ResultatMessage.Aucun;
            }
        }

        public void EnregistrerParametres()
        {
            ParametresGlobaux.Enregistrer();
        }
    }
}
