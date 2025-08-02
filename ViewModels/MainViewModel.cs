using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BiblicalSearchEngine.Models;
using BiblicalSearchEngine.Services;

namespace BiblicalSearchEngine.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private SearchService searchService;
        private string searchQuery;
        private string statusMessage;
        private string resultsInfo;
        private ObservableCollection<SearchResult> searchResults;
        private ObservableCollection<Document> documents;

        public MainViewModel()
        {
            searchService = new SearchService();
            SearchResults = new ObservableCollection<SearchResult>();
            Documents = new ObservableCollection<Document>();

            LoadDocuments();
            StatusMessage = "Prêt";
        }

        public string SearchQuery
        {
            get => searchQuery;
            set { searchQuery = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => statusMessage;
            set { statusMessage = value; OnPropertyChanged(); }
        }

        public string ResultsInfo
        {
            get => resultsInfo;
            set { resultsInfo = value; OnPropertyChanged(); }
        }

        public string IndexInfo => $"{Documents.Count} documents indexés";

        public ObservableCollection<SearchResult> SearchResults
        {
            get => searchResults;
            set { searchResults = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Document> Documents
        {
            get => documents;
            set { documents = value; OnPropertyChanged(); }
        }

        public ICommand SearchCommand => new RelayCommand(Search);

        public void Search()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ResultsInfo = "";
                SearchResults.Clear();
                return;
            }

            StatusMessage = "Recherche en cours...";
            SearchResults.Clear();

            try
            {
                var results = searchService.Search(SearchQuery);

                foreach (var result in results)
                {
                    SearchResults.Add(result);
                }

                ResultsInfo = $"{results.Count} résultat(s) trouvé(s) pour \"{SearchQuery}\"";
                StatusMessage = "Recherche terminée";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }

        public void ImportDocuments(string[] filePaths)
        {
            foreach (var path in filePaths)
            {
                try
                {
                    var doc = new Document
                    {
                        Title = Path.GetFileNameWithoutExtension(path),
                        FilePath = path,
                        Content = File.ReadAllText(path),
                        Type = DocumentType.Predication
                    };

                    DatabaseService.SaveDocument(doc);
                    searchService.IndexDocument(doc);
                    Documents.Add(doc);

                    StatusMessage = $"Document '{doc.Title}' importé";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur import: {ex.Message}";
                }
            }
        }

        public void ImportBible(string filePath)
        {
            // Implémenter l'import de Bible (XML, etc.)
            StatusMessage = "Import Bible en cours...";
        }

        public void ReindexAll()
        {
            StatusMessage = "Réindexation en cours...";

            foreach (var doc in Documents)
            {
                searchService.IndexDocument(doc);
            }

            StatusMessage = "Réindexation terminée";
        }

        private void LoadDocuments()
        {
            var docs = DatabaseService.GetAllDocuments();
            foreach (var doc in docs)
            {
                Documents.Add(doc);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Commande simple pour MVVM
    public class RelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => execute();
    }
}