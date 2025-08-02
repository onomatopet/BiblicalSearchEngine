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
<<<<<<< HEAD
            var dbService = new DatabaseService();
            dbService.Initialize();
        }
    }
}
=======
            DatabaseService.Initialize();
        }
    }
}
>>>>>>> fa904caa9f4c9cfaa5f9c55f6a5fd4e729e294be
