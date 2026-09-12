using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Mentalo.Persistance
{
    /// <summary>Aide > Vérifier les mises à jour (v0.18), le standard des applications de la
    /// famille Stargazer : on interroge la dernière release GitHub du dépôt, on compare avec
    /// le fichier VERSION posé à côté de l'exe, et on installe sur place — l'archive portable
    /// est déballée dans un dossier temporaire, puis un petit script cmd attend la fermeture de
    /// l'application, recopie les fichiers par-dessus et la relance. Marche pour l'application
    /// posée par l'installeur, pour la version portable et pour la copie qui vit dans apps\ de
    /// Stargazer. Les réglages de l'utilisateur vivent dans %APPDATA%\Mental-o : rien à préserver
    /// dans le dossier de l'application.
    ///
    /// Au lancement, une vérification silencieuse en arrière-plan : s'il existe plus récent, le
    /// menu Aide porte un point et la barre d'état le dit.
    ///
    /// Dépôt privé : l'API répond 404 sans jeton — GITHUB_TOKEN (variable d'environnement) est
    /// envoyé s'il existe, pratique pour tester avant le passage en public.</summary>
    public static class MiseAJour
    {
        public const string Depot = "Pardaramskha/mental-o";
        public const string NomZip = "mental-o-windows-portable.zip";
        public const string Exe = "Mental-o.exe";
        private const string CleDesinstallation = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Mental-o";

        public class Info
        {
            public string Version;   // « 0.18.0 »
            public string UrlZip;    // l'archive portable Windows de la release
            public string UrlPage;   // la page de la release
            public string Notes;     // le texte de la release (Markdown brut)
        }

        /// <summary>Le dossier de l'application (celui de l'exe).</summary>
        public static string DossierApplication
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>La version de l'application : le fichier VERSION à côté de l'exe, sinon celle
        /// de l'assembly.</summary>
        public static string VersionLocale()
        {
            try
            {
                string fichier = Path.Combine(DossierApplication, "VERSION");
                if (File.Exists(fichier)) return File.ReadAllText(fichier).Trim();
            }
            catch { }
            Version v = Assembly.GetEntryAssembly().GetName().Version;
            return v.Major + "." + v.Minor + "." + v.Build;
        }

        /// <summary>La dernière release publiée. Lève une exception parlante sinon.</summary>
        public static Info Verifier()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
            string json;
            using (WebClient client = Client())
            {
                client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                client.Encoding = Encoding.UTF8;
                try
                {
                    json = client.DownloadString("https://api.github.com/repos/" + Depot + "/releases/latest");
                }
                catch (WebException ex)
                {
                    HttpWebResponse reponse = ex.Response as HttpWebResponse;
                    if (reponse != null && reponse.StatusCode == HttpStatusCode.NotFound)
                        throw new Exception("aucune version publiée trouvée (dépôt privé ou sans release).");
                    if (reponse != null)
                        throw new Exception("GitHub répond " + (int)reponse.StatusCode + " " + reponse.StatusDescription + ".");
                    throw new Exception("pas de connexion à GitHub (" + ex.Message + ").");
                }
            }
            Info info = new Info();
            info.Version = Champ(json, "tag_name").TrimStart('v', 'V');
            info.UrlPage = Champ(json, "html_url");
            info.Notes = Champ(json, "body");
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                if (m.Groups[1].Value.EndsWith("/" + NomZip, StringComparison.OrdinalIgnoreCase))
                {
                    info.UrlZip = m.Groups[1].Value;
                    break;
                }
            }
            if (info.Version.Length == 0) throw new Exception("réponse GitHub illisible.");
            return info;
        }

        private static WebClient Client()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "Mental-o";
            string jeton = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(jeton))
                client.Headers[HttpRequestHeader.Authorization] = "Bearer " + jeton;
            return client;
        }

        /// <summary>Un champ texte du JSON (premier trouvé), séquences d'échappement rendues —
        /// assez pour l'API des releases.</summary>
        private static string Champ(string json, string nom)
        {
            Match m = Regex.Match(json, "\"" + nom + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return "";
            return Regex.Replace(m.Groups[1].Value, @"\\(u[0-9a-fA-F]{4}|.)", delegate(Match e)
            {
                string s = e.Groups[1].Value;
                switch (s[0])
                {
                    case 'n': return "\n";
                    case 'r': return "";
                    case 't': return "\t";
                    case 'u': return ((char)int.Parse(s.Substring(1), NumberStyles.HexNumber)).ToString();
                    default: return s;
                }
            });
        }

        /// <summary>« 0.18.0 » > « 0.17 » ?</summary>
        public static bool PlusRecente(string distante, string locale)
        {
            int[] a = Nombres(distante);
            int[] b = Nombres(locale);
            for (int i = 0; i < 3; i++)
                if (a[i] != b[i]) return a[i] > b[i];
            return false;
        }

        private static int[] Nombres(string v)
        {
            int[] r = new int[3];
            string[] parts = (v ?? "").Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++)
            {
                Match m = Regex.Match(parts[i], @"\d+");
                r[i] = m.Success ? int.Parse(m.Value) : 0;
            }
            return r;
        }

        /// <summary>Télécharge l'archive, la déballe à côté, puis laisse un script cmd finir le
        /// travail une fois l'application fermée (l'exe est verrouillé tant qu'elle tourne).
        /// L'appelant ferme l'application juste après.</summary>
        public static void Installer(Info info, string dossierApplication)
        {
            if (string.IsNullOrEmpty(info.UrlZip))
                throw new Exception("la release ne contient pas " + NomZip + ".");
            string temp = Path.Combine(Path.GetTempPath(), "mental-o-maj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            string zip = Path.Combine(temp, NomZip);
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (WebClient client = Client())
                client.DownloadFile(info.UrlZip, zip);
            string contenu = Path.Combine(temp, "contenu");
            Deballer(zip, contenu);
            if (!File.Exists(Path.Combine(contenu, Exe)))
                throw new Exception("l'archive ne contient pas " + Exe + ".");

            string script = Path.Combine(temp, "maj.cmd");
            int pid = Process.GetCurrentProcess().Id;
            string cible = dossierApplication.TrimEnd('\\');
            StringBuilder lignes = new StringBuilder();
            lignes.Append("@echo off\r\n");
            lignes.Append(":attend\r\n");
            lignes.Append("tasklist /FI \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul\r\n");
            lignes.Append("if not errorlevel 1 (ping 127.0.0.1 -n 2 >nul & goto attend)\r\n");
            lignes.Append("xcopy \"" + contenu + "\\*\" \"" + cible + "\\\" /E /Y /I /Q >nul\r\n");
            lignes.Append("start \"\" \"" + Path.Combine(cible, Exe) + "\"\r\n");
            lignes.Append("cd /d \"%TEMP%\"\r\n");
            lignes.Append("rmdir /s /q \"" + temp + "\"\r\n");
            File.WriteAllText(script, lignes.ToString(), Encoding.Default);

            MettreAJourRegistre(dossierApplication, info.Version);

            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.WorkingDirectory = Path.GetTempPath();
            Process.Start(psi);
        }

        /// <summary>Déballe en refusant les chemins qui sortent du dossier.</summary>
        private static void Deballer(string zip, string dossier)
        {
            Directory.CreateDirectory(dossier);
            string racine = Path.GetFullPath(dossier).TrimEnd('\\') + "\\";
            using (FileStream flux = File.OpenRead(zip))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry e in archive.Entries)
                {
                    if (e.FullName.EndsWith("/")) continue;
                    string cible = Path.GetFullPath(Path.Combine(dossier, e.FullName.Replace('/', '\\')));
                    if (!cible.StartsWith(racine, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(cible));
                    using (Stream source = e.Open())
                    using (FileStream destination = File.Create(cible))
                        source.CopyTo(destination);
                }
            }
        }

        /// <summary>Si l'application a été posée par l'installeur, Paramètres > Applications
        /// installées doit afficher la nouvelle version.</summary>
        private static void MettreAJourRegistre(string dossierApplication, string version)
        {
            try
            {
                using (RegistryKey cle = Registry.CurrentUser.OpenSubKey(CleDesinstallation, true))
                {
                    if (cle == null) return;
                    string lieu = cle.GetValue("InstallLocation") as string;
                    if (lieu == null || !string.Equals(Path.GetFullPath(lieu).TrimEnd('\\'),
                        Path.GetFullPath(dossierApplication).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                    cle.SetValue("DisplayVersion", version);
                }
            }
            catch { }
        }
    }
}
