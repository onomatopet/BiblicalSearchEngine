using BiblicalSearchEngine.Services;
using System.Windows;

namespace BiblicalSearchEngine
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialiser la base de données au démarrage
            DatabaseService.Initialize();
        }
    }
}
