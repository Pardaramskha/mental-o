using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Mentalo.Persistance
{
    /// <summary>Aide > Vérifier les mises à jour (v0.18), le standard des applications de la
    /// famille Stargazer : on interroge la dernière release GitHub du dépôt, on compare avec
    /// le fichier VERSION posé à côté de l'exe, et on installe sur place — l'archive portable
    /// est déballée dans un dossier temporaire, puis un petit script attend la fermeture de
    /// l'application, recopie les fichiers par-dessus et la relance. Les réglages de
    /// l'utilisateur vivent dans %APPDATA%\Mental-o : rien à préserver dans le dossier de
    /// l'application.
    ///
    /// Au lancement, une vérification silencieuse en arrière-plan : s'il existe plus récent, le
    /// menu Aide porte un point et la barre d'état le dit.
    ///
    /// Dépôt privé : l'API répond 404 sans jeton — GITHUB_TOKEN (variable d'environnement) est
    /// envoyé s'il existe, pratique pour tester avant le passage en public.</summary>
    public static class MiseAJour
    {
        public const string Depot = "Pardaramskha/mental-o";
        /// <summary>L'asset du DLC Marabook dans la même release (nom stable).</summary>
        public const string NomDlc = "mental-o.mdlc";
        private const string CleDesinstallation = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Mental-o";

        /// <summary>L'archive portable de l'OS courant, dans la release (noms stables).</summary>
        public static string NomZip
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "mental-o-windows-portable.zip";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "mental-o-macos-portable.zip";
                return "mental-o-linux-portable.zip";
            }
        }

        public static string Exe
        {
            get { return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Mental-O.exe" : "Mental-O"; }
        }

        public class Info
        {
            public string Version;   // « 1.0.0 »
            public string UrlZip;    // l'archive portable de l'OS courant
            public string UrlPage;   // la page de la release
            public string Notes;     // le texte de la release (Markdown brut)
        }

        /// <summary>Le dossier de l'application (celui de l'exe).</summary>
        public static string DossierApplication
        {
            get { return AppContext.BaseDirectory; }
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
            Version v = typeof(MiseAJour).Assembly.GetName().Version;
            return v.Major + "." + v.Minor + "." + v.Build;
        }

        /// <summary>La dernière release publiée. Lève une exception parlante sinon.</summary>
        public static Info Verifier()
        {
            string json;
            using (HttpClient client = Client())
            {
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                HttpResponseMessage reponse;
                try
                {
                    reponse = client.GetAsync("https://api.github.com/repos/" + Depot + "/releases/latest").GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    throw new Exception("pas de connexion à GitHub (" + ex.Message + ").");
                }
                if (reponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new Exception("aucune version publiée trouvée (dépôt privé ou sans release).");
                if (!reponse.IsSuccessStatusCode)
                    throw new Exception("GitHub répond " + (int)reponse.StatusCode + " " + reponse.ReasonPhrase + ".");
                json = reponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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

        private static HttpClient Client()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mental-O");
            string jeton = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(jeton))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jeton);
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

        /// <summary>« 1.0.0 » > « 0.18 » ?</summary>
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

        /// <summary>Télécharge l'archive, la déballe à côté, puis laisse un script finir le
        /// travail une fois l'application fermée (l'exe est verrouillé tant qu'elle tourne).
        /// L'appelant ferme l'application juste après.</summary>
        public static void Installer(Info info, string dossierApplication)
        {
            if (string.IsNullOrEmpty(info.UrlZip))
                throw new Exception("la release ne contient pas " + NomZip + ".");
            string temp = Path.Combine(Path.GetTempPath(), "mental-o-maj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            string zip = Path.Combine(temp, NomZip);
            using (HttpClient client = Client())
            {
                byte[] octets = client.GetByteArrayAsync(info.UrlZip).GetAwaiter().GetResult();
                File.WriteAllBytes(zip, octets);
            }
            string contenu = Path.Combine(temp, "contenu");
            Deballer(zip, contenu);
            if (!File.Exists(Path.Combine(contenu, Exe)))
                throw new Exception("l'archive ne contient pas " + Exe + ".");

            int pid = Process.GetCurrentProcess().Id;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string script = Path.Combine(temp, "maj.cmd");
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
            else
            {
                string script = Path.Combine(temp, "maj.sh");
                string cible = dossierApplication.TrimEnd('/');
                StringBuilder lignes = new StringBuilder();
                lignes.Append("#!/bin/sh\n");
                lignes.Append("while kill -0 " + pid + " 2>/dev/null; do sleep 1; done\n");
                lignes.Append("cp -R \"" + contenu + "/.\" \"" + cible + "/\"\n");
                lignes.Append("chmod +x \"" + Path.Combine(cible, Exe) + "\"\n");
                lignes.Append("\"" + Path.Combine(cible, Exe) + "\" &\n");
                lignes.Append("rm -rf \"" + temp + "\"\n");
                File.WriteAllText(script, lignes.ToString(), new UTF8Encoding(false));
                ProcessStartInfo psi = new ProcessStartInfo("/bin/sh", "\"" + script + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = Path.GetTempPath();
                Process.Start(psi);
            }
        }

        /// <summary>Déballe en refusant les chemins qui sortent du dossier.</summary>
        private static void Deballer(string zip, string dossier)
        {
            Directory.CreateDirectory(dossier);
            string racine = Path.GetFullPath(dossier).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (FileStream flux = File.OpenRead(zip))
            using (ZipArchive archive = new ZipArchive(flux, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry e in archive.Entries)
                {
                    if (e.FullName.EndsWith("/")) continue;
                    string cible = Path.GetFullPath(Path.Combine(dossier, e.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!cible.StartsWith(racine, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(cible));
                    using (Stream source = e.Open())
                    using (FileStream destination = File.Create(cible))
                        source.CopyTo(destination);
                }
            }
        }

        /// <summary>Si l'application a été posée par l'installeur, Paramètres > Applications
        /// installées doit afficher la nouvelle version. Par reg.exe : le cœur ne référence
        /// pas Microsoft.Win32.Registry (absent hors Windows).</summary>
        private static void MettreAJourRegistre(string dossierApplication, string version)
        {
            try
            {
                string lieu = LireRegistre("InstallLocation");
                if (lieu == null || !string.Equals(Path.GetFullPath(lieu).TrimEnd('\\'),
                    Path.GetFullPath(dossierApplication).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                ProcessStartInfo psi = new ProcessStartInfo("reg.exe",
                    "add \"HKCU\\" + CleDesinstallation + "\" /v DisplayVersion /t REG_SZ /d \"" + version + "\" /f");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) p.WaitForExit(5000);
            }
            catch { }
        }

        private static string LireRegistre(string valeur)
        {
            ProcessStartInfo psi = new ProcessStartInfo("reg.exe", "query \"HKCU\\" + CleDesinstallation + "\" /v " + valeur);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            using (Process p = Process.Start(psi))
            {
                string sortie = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                if (p.ExitCode != 0) return null;
                Match m = Regex.Match(sortie, valeur + @"\s+REG_\w+\s+(.+)");
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }
        }
    }
}
