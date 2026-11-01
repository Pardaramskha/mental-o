using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Mentalo.Parametres;
using Mentalo.Vue;

namespace Mentalo
{
    /// <summary>L'application Avalonia de Mental-O seul (26/10/2026) : thème, fenêtre
    /// principale, fichier passé en argument.</summary>
    public class Application : Avalonia.Application
    {
        public static string FichierADemarrer;
        /// <summary>« --capture &lt;png&gt; » : la fenêtre rendue en image, puis quitte.</summary>
        public static string CaptureVers;

        public override void Initialize()
        {
            Name = "Mental-O";
            // Les réglages et les pinceaux du chrome ne se créent qu'ici, la
            // plateforme en place : un AvaloniaObject bâti avant le démarrage
            // figerait un Dispatcher sans boucle (PlatformNotSupportedException).
            ParametresGlobaux.Charger();
            Chrome.Basculer(ParametresGlobaux.ThemeSombre);
            DialogueMessage.Brancher();
            Styles.Add(new FluentTheme());
            Theme.Installer(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            IClassicDesktopStyleApplicationLifetime bureau = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (bureau != null)
            {
                FenetrePrincipale fenetre = new FenetrePrincipale();
                bureau.MainWindow = fenetre;
                bureau.ShutdownMode = ShutdownMode.OnMainWindowClose;
                if (!string.IsNullOrEmpty(FichierADemarrer) && File.Exists(FichierADemarrer))
                    fenetre.OuvrirFichierAuDemarrage(FichierADemarrer);
                if (!string.IsNullOrEmpty(CaptureVers))
                    fenetre.CapturerPuisQuitter(CaptureVers);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }

    public static class Programme
    {
        [STAThread]
        public static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--capture" && i + 1 < args.Length) { Application.CaptureVers = args[++i]; continue; }
                if (File.Exists(args[i]) && Application.FichierADemarrer == null) Application.FichierADemarrer = Path.GetFullPath(args[i]);
            }
            try
            {
                return ConstruireApplication().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                try
                {
                    string dossier = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mental-o");
                    Directory.CreateDirectory(dossier);
                    File.WriteAllText(Path.Combine(dossier, "plantage.txt"), DateTime.Now + "\n" + ex);
                }
                catch { }
                throw;
            }
        }

        public static AppBuilder ConstruireApplication()
        {
            return AppBuilder.Configure<Application>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        }
    }
}
