using System;
using System.IO;
using System.Windows;

namespace Mentalo
{
    public static class Programme
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Mentalo.Parametres.ParametresGlobaux.Charger();
            Mentalo.Vue.Chrome.Basculer(Mentalo.Parametres.ParametresGlobaux.ThemeSombre);
            Application application = new Application();
            Mentalo.Vue.Theme.Appliquer(application);
            FenetrePrincipale fenetre = new FenetrePrincipale();
            if (args.Length > 0 && File.Exists(args[0]))
                fenetre.OuvrirFichier(args[0]);
            application.Run(fenetre);
        }
    }
}
