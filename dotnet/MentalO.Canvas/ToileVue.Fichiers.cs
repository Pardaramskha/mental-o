using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Mentalo.Historique;
using Mentalo.Modele;

namespace Mentalo.Vue
{
    /// <summary>Les fichiers attachés aux boîtes (v0.14) : insertion (menu, dépôt, clic
    /// droit), menu du lien (ouvrir, renommer, remplacer, intégrer au .tea, supprimer…).
    /// Chaque opération construit une liste neuve et passe par CommandeModifierFichiers,
    /// donc s'annule d'un Ctrl+Z.</summary>
    public partial class ToileVue
    {
        /// <summary>Au-delà, l'intégration au .tea demande confirmation : elle alourdit d'autant la carte.</summary>
        private const long TailleFichierAvertissement = 25L * 1024 * 1024;

        /// <summary>Le point d'entrée des commandes d'annulation : remplace la liste et rafraîchit.</summary>
        public void DefinirFichiersNoeud(Noeud noeud, List<PieceJointe> fichiers)
        {
            noeud.Fichiers = fichiers ?? new List<PieceJointe>();
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel))
                visuel.MettreAJour();
            MettreAJourLiensDe(noeud);
            if (_selection != null && _selection.Modele == noeud) MettreAJourGrip();
            SignalerModification();
        }

        /// <summary>Un fichier texte brut ou Markdown : déposé, il devient des boîtes (v0.17).</summary>
        public static bool EstFichierTexte(string chemin)
        {
            string extension = System.IO.Path.GetExtension(chemin ?? "").ToLowerInvariant();
            return extension == ".txt" || extension == ".md" || extension == ".markdown";
        }

        /// <summary>Un fichier « non image » exploitable : existe et n'est pas un dossier.</summary>
        public static bool EstFichierAttachable(string chemin)
        {
            if (string.IsNullOrEmpty(chemin) || ExtensionImageValide(chemin)) return false;
            try { return File.Exists(chemin); }
            catch { return false; }
        }

        // ------------------------------------------------------------------ insertion

        /// <summary>Insertion > Fichier : s'attache à la boîte sélectionnée ; sans sélection,
        /// une boîte neuve portant le seul lien naît au centre de la vue.</summary>
        public void InsererFichierDepuisChemin(string chemin)
        {
            if (_selection != null)
                AttacherFichier(_selection.Modele, chemin);
            else
            {
                Point centre = CentreCanvas();
                InsererFichierNouvelleBoite(chemin, new Point(centre.X - 80, centre.Y - 14));
            }
        }

        /// <summary>Clic droit > Insérer un fichier dans la boîte… (ou, sans sélection, une boîte neuve).</summary>
        public async void InsererFichierDansBoite()
        {
            NoeudVisuel cible = _selection;
            List<string> chemins = await ChoisirFichiers(true);
            if (cible != null) cible.RedonnerFocus();
            if (chemins == null) return;
            foreach (string chemin in chemins)
            {
                if (cible != null) AttacherFichier(cible.Modele, chemin);
                else InsererFichierDepuisChemin(chemin);
            }
        }

        /// <summary>Le sélecteur de fichiers, sous protection du focus de l'éditeur.</summary>
        private async Task<List<string>> ChoisirFichiers(bool plusieurs)
        {
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                List<string> chemins = await Ui.ChoisirFichiers(this,
                    plusieurs ? "Insérer un ou plusieurs fichiers" : "Choisir un fichier", plusieurs, Ui.TypeTous);
                return chemins.Count == 0 ? null : chemins;
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
        }

        private void InsererFichierNouvelleBoite(string chemin, Point position)
        {
            if (ExtensionImageValide(chemin))
            {
                InsererImage(chemin, position);
                return;
            }
            if (!EstFichierAttachable(chemin))
            {
                SignalerFichierIntrouvable(chemin);
                return;
            }
            Noeud noeud = new Noeud();
            if (Document.BoiteParDefaut != null) Document.BoiteParDefaut.AppliquerA(noeud);
            noeud.Fichiers.Add(PieceJointe.DepuisChemin(chemin));
            noeud.X = position.X;
            noeud.Y = position.Y;
            Historique.ExecuterEtPousser(new CommandeCreerNoeud(this, noeud));
            NoeudVisuel visuel;
            if (_visuels.TryGetValue(noeud, out visuel)) Selectionner(visuel);
        }

        private void AttacherFichier(Noeud noeud, string chemin)
        {
            if (ExtensionImageValide(chemin))
            {
                AttacherImage(noeud, chemin);
                return;
            }
            if (!EstFichierAttachable(chemin))
            {
                SignalerFichierIntrouvable(chemin);
                return;
            }
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            nouveaux.Add(PieceJointe.DepuisChemin(chemin));
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        private void SignalerFichierIntrouvable(string chemin)
        {
            Hote.Message(FenetreProprietaire(),
                string.Format("« {0} » n'existe pas ou n'est pas un fichier.", chemin),
                "Mental-O", BoutonsMessage.OK, IconeMessage.Avertissement);
        }

        private static List<PieceJointe> CopieFichiers(Noeud noeud)
        {
            List<PieceJointe> copie = new List<PieceJointe>();
            foreach (PieceJointe piece in noeud.Fichiers)
                copie.Add(piece.Cloner());
            return copie;
        }

        private static PieceJointe TrouverCopie(List<PieceJointe> fichiers, PieceJointe original)
        {
            foreach (PieceJointe piece in fichiers)
                if (piece.Id == original.Id) return piece;
            return null;
        }

        // ------------------------------------------------------------------ menu du lien

        /// <summary>Clic gauche ou droit sur un lien : toutes les actions au même endroit.</summary>
        private void OuvrirMenuFichier(NoeudVisuel visuel, PieceJointe piece, Control ancre)
        {
            if (piece.EstUrl)
            {
                OuvrirMenuUrl(visuel, piece, ancre);
                return;
            }
            Noeud noeud = visuel.Modele;
            if (!_selectionnes.Contains(visuel)) Selectionner(visuel);

            bool integre = piece.EstIntegre;
            bool origine = piece.OrigineDisponible;
            string taille = piece.TailleLisible();

            ContextMenu menu = new ContextMenu();

            // Un en-tête inerte : l'état du fichier d'un coup d'œil.
            string etat = integre ? "Intégré au .tea" : origine ? "Lien externe" : "Fichier introuvable";
            if (taille.Length > 0) etat += " · " + taille;
            MenuItem entete = Ui.Element(etat, null);
            entete.IsEnabled = false;
            entete.FontStyle = FontStyle.Italic;
            menu.Items.Add(entete);
            menu.Items.Add(Ui.Separateur());

            MenuItem ouvrir = Ui.Element("Ouvrir", delegate { OuvrirFichier(piece); });
            ouvrir.FontWeight = FontWeight.SemiBold;
            ouvrir.IsEnabled = integre || origine;
            Ui.Infobulle(ouvrir, integre
                ? "Ouvre une copie temporaire du fichier intégré (les modifications n'y sont pas reprises)"
                : "Ouvre le fichier avec l'application associée");
            menu.Items.Add(ouvrir);

            MenuItem emplacement = Ui.Element("Ouvrir l'emplacement du fichier", delegate { OuvrirEmplacement(piece); });
            emplacement.IsEnabled = origine;
            menu.Items.Add(emplacement);

            MenuItem copierChemin = Ui.Element("Copier le chemin", async delegate
            {
                try
                {
                    IClipboard presse = Ui.PressePapier(this);
                    if (presse != null) await presse.SetTextAsync(piece.Chemin ?? "");
                }
                catch { }
            });
            copierChemin.IsEnabled = !string.IsNullOrEmpty(piece.Chemin);
            menu.Items.Add(copierChemin);
            menu.Items.Add(Ui.Separateur());

            menu.Items.Add(Ui.Element("Renommer le lien…", delegate { RenommerLien(noeud, piece); }));

            MenuItem remplacer = Ui.Element("Remplacer le fichier…", delegate { RemplacerFichier(noeud, piece); });
            Ui.Infobulle(remplacer, "Fait pointer ce lien vers un autre fichier (le libellé personnalisé est conservé)");
            menu.Items.Add(remplacer);
            menu.Items.Add(Ui.Separateur());

            if (!integre)
            {
                MenuItem integrer = Ui.Element("Intégrer le fichier au .tea", delegate { IntegrerFichier(noeud, piece); });
                integrer.IsEnabled = origine;
                Ui.Infobulle(integrer, "Copie le fichier à l'intérieur de la carte : le seul .tea suffit alors à tout emporter");
                menu.Items.Add(integrer);
            }
            else
            {
                MenuItem copie = Ui.Element("Enregistrer une copie sous…", delegate { EnregistrerCopie(piece); });
                Ui.Infobulle(copie, "Extrait le fichier intégré vers l'emplacement de votre choix");
                menu.Items.Add(copie);

                MenuItem detacher = Ui.Element("Détacher du .tea", delegate { DetacherFichier(noeud, piece); });
                detacher.IsEnabled = origine;
                Ui.Infobulle(detacher, origine
                    ? "Redevient un simple lien vers le fichier d'origine ; la copie intégrée est retirée de la carte"
                    : "Impossible : le fichier d'origine n'est plus accessible");
                menu.Items.Add(detacher);
            }
            menu.Items.Add(Ui.Separateur());

            menu.Items.Add(Ui.Element("Supprimer le lien", delegate { SupprimerLien(noeud, piece); }));
            Ui.OuvrirMenuSous(menu, ancre);
        }

        // ------------------------------------------------------------------ actions

        private void OuvrirFichier(PieceJointe piece)
        {
            try
            {
                string chemin;
                if (piece.EstUrl)
                    chemin = piece.Chemin; // le navigateur par défaut s'en charge
                else if (piece.EstIntegre)
                    chemin = ExtraireTemporaire(piece);
                else
                {
                    if (!piece.OrigineDisponible)
                    {
                        SignalerFichierIntrouvable(piece.Chemin ?? piece.NomFichier ?? "");
                        return;
                    }
                    chemin = piece.Chemin;
                }
                OuvrirAvecLeSysteme(chemin);
            }
            catch (Exception ex)
            {
                Hote.Message(FenetreProprietaire(),
                    string.Format("Impossible d'ouvrir le fichier :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
        }

        /// <summary>Ouvre un fichier ou une adresse avec l'application associée, sur chaque OS.</summary>
        public static void OuvrirAvecLeSysteme(string chemin)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ProcessStartInfo lancement = new ProcessStartInfo(chemin);
                lancement.UseShellExecute = true;
                Process.Start(lancement);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", "\"" + chemin + "\"");
            else
                Process.Start("xdg-open", "\"" + chemin + "\"");
        }

        /// <summary>Écrit le fichier intégré dans %TEMP%\Mental-o\&lt;id&gt;\ sous son nom
        /// d'origine (l'application associée s'y retrouve) et rend ce chemin.</summary>
        private static string ExtraireTemporaire(PieceJointe piece)
        {
            string dossier = Path.Combine(Path.GetTempPath(), "Mental-o", piece.Id);
            Directory.CreateDirectory(dossier);
            string nom = string.IsNullOrEmpty(piece.NomFichier) ? "fichier" + piece.Extension : piece.NomFichier;
            string chemin = Path.Combine(dossier, nom);
            File.WriteAllBytes(chemin, piece.Donnees);
            return chemin;
        }

        private void OuvrirEmplacement(PieceJointe piece)
        {
            if (!piece.OrigineDisponible) return;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start("explorer.exe", "/select,\"" + piece.Chemin + "\"");
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", "-R \"" + piece.Chemin + "\"");
                else
                    Process.Start("xdg-open", "\"" + Path.GetDirectoryName(piece.Chemin) + "\"");
            }
            catch (Exception ex)
            {
                Hote.Message(FenetreProprietaire(),
                    string.Format("Impossible d'ouvrir l'emplacement :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
        }

        private async void RenommerLien(Noeud noeud, PieceJointe piece)
        {
            string nouveau;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                nouveau = await DialogueSaisie.DemanderTexte(FenetreProprietaire(),
                    "Renommer le lien", "Libellé du lien :", piece.Nom);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
            if (nouveau == null) return;
            nouveau = nouveau.Trim();
            if (nouveau.Length == 0) nouveau = piece.NomFichier ?? piece.Nom;
            if (nouveau == piece.Nom) return;
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            PieceJointe cible = TrouverCopie(nouveaux, piece);
            if (cible == null) return;
            cible.Nom = nouveau;
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        private async void RemplacerFichier(Noeud noeud, PieceJointe piece)
        {
            List<string> chemins = await ChoisirFichiers(false);
            if (chemins == null || chemins.Count == 0) return;
            string chemin = chemins[0];
            if (!EstFichierAttachable(chemin))
            {
                if (ExtensionImageValide(chemin))
                    await Hote.Message(FenetreProprietaire(),
                        "Une image ne se lie pas : utilisez « Insérer une image dans la boîte… ».",
                        "Mental-O", BoutonsMessage.OK, IconeMessage.Information);
                else
                    SignalerFichierIntrouvable(chemin);
                return;
            }
            byte[] donnees = null;
            if (piece.EstIntegre)
            {
                // Le lien était intégré : le remplaçant l'est aussi, mêmes garde-fous.
                donnees = await LireOctetsPourIntegration(chemin);
                if (donnees == null) return;
            }
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            PieceJointe cible = TrouverCopie(nouveaux, piece);
            if (cible == null) return;
            bool libelleParDefaut = string.IsNullOrEmpty(cible.Nom) || cible.Nom == cible.NomFichier;
            cible.Chemin = Path.GetFullPath(chemin);
            cible.NomFichier = Path.GetFileName(chemin);
            if (libelleParDefaut) cible.Nom = cible.NomFichier;
            cible.Donnees = donnees;
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        /// <summary>Lit le fichier à intégrer ; au-delà du seuil, demande confirmation.
        /// Rend null si l'utilisateur renonce ou si la lecture échoue.</summary>
        private async Task<byte[]> LireOctetsPourIntegration(string chemin)
        {
            try
            {
                FileInfo informations = new FileInfo(chemin);
                if (informations.Length > TailleFichierAvertissement)
                {
                    ResultatMessage reponse = await Hote.Message(FenetreProprietaire(),
                        string.Format("« {0} » pèse {1:0.0} Mo : l'intégrer alourdira d'autant le fichier .tea.\n\nContinuer ?",
                            informations.Name, informations.Length / 1048576.0),
                        "Mental-O", BoutonsMessage.OuiNon, IconeMessage.Question);
                    if (reponse != ResultatMessage.Oui) return null;
                }
                return File.ReadAllBytes(chemin);
            }
            catch (Exception ex)
            {
                await Hote.Message(FenetreProprietaire(),
                    string.Format("Impossible de lire le fichier :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
                return null;
            }
        }

        private async void IntegrerFichier(Noeud noeud, PieceJointe piece)
        {
            if (!piece.OrigineDisponible)
            {
                SignalerFichierIntrouvable(piece.Chemin ?? "");
                return;
            }
            byte[] donnees = await LireOctetsPourIntegration(piece.Chemin);
            if (donnees == null) return;
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            PieceJointe cible = TrouverCopie(nouveaux, piece);
            if (cible == null) return;
            cible.Donnees = donnees;
            if (string.IsNullOrEmpty(cible.NomFichier)) cible.NomFichier = Path.GetFileName(piece.Chemin);
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        private void DetacherFichier(Noeud noeud, PieceJointe piece)
        {
            if (!piece.EstIntegre || !piece.OrigineDisponible) return;
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            PieceJointe cible = TrouverCopie(nouveaux, piece);
            if (cible == null) return;
            cible.Donnees = null;
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }

        private async void EnregistrerCopie(PieceJointe piece)
        {
            if (!piece.EstIntegre) return;
            NoeudVisuel.ProtectionFocus = true;
            try
            {
                string nom = string.IsNullOrEmpty(piece.NomFichier) ? "fichier" + piece.Extension : piece.NomFichier;
                string extension = piece.Extension;
                string chemin = await Ui.EnregistrerSous(this, "Enregistrer une copie du fichier", nom,
                    extension.Length > 1 ? extension.Substring(1) : null,
                    extension.Length > 1 ? Ui.Type("Fichier " + extension.Substring(1).ToUpperInvariant(), "*" + extension) : Ui.TypeTous,
                    Ui.TypeTous);
                if (chemin == null) return;
                File.WriteAllBytes(chemin, piece.Donnees);
            }
            catch (Exception ex)
            {
                await Hote.Message(FenetreProprietaire(),
                    string.Format("Impossible d'enregistrer la copie :\n{0}", ex.Message),
                    "Mental-O", BoutonsMessage.OK, IconeMessage.Erreur);
            }
            finally
            {
                NoeudVisuel.ProtectionFocus = false;
            }
        }

        private void SupprimerLien(Noeud noeud, PieceJointe piece)
        {
            List<PieceJointe> nouveaux = CopieFichiers(noeud);
            PieceJointe cible = TrouverCopie(nouveaux, piece);
            if (cible == null) return;
            nouveaux.Remove(cible);
            // Une boîte qui ne portait que ce lien redeviendrait vide : on la supprime plutôt.
            if (nouveaux.Count == 0 && string.IsNullOrWhiteSpace(noeud.TextePlat) && !noeud.EstImage)
            {
                Historique.ExecuterEtPousser(new CommandeSupprimerNoeud(this, noeud, LiensAttaches(noeud)));
                return;
            }
            Historique.ExecuterEtPousser(new CommandeModifierFichiers(this, noeud, nouveaux));
        }
    }
}
