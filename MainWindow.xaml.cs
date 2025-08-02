using BiblicalSearchEngine.ViewModels;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Input;

namespace BiblicalSearchEngine
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainViewModel();
            DataContext = viewModel;
        }

        private void ImportDocument_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Documents|*.txt;*.docx;*.pdf|Tous|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                viewModel.ImportDocuments(dialog.FileNames);
            }
        }

        private void ImportBible_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Bible|*.xml;*.txt|Tous|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                viewModel.ImportBible(dialog.FileName);
            }
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Reindex_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ReindexAll();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Ouvrir fenêtre de paramètres
        }

        private void AdvancedSearch_Click(object sender, RoutedEventArgs e)
        {
<<<<<<< HEAD
            var advancedSearch = new Views.AdvancedSearchWindow();
            advancedSearch.Owner = this;

            if (advancedSearch.ShowDialog() == true)
            {
                viewModel.SearchQuery = advancedSearch.GeneratedQuery;
                viewModel.Search();
            }
=======
            // Ouvrir fenêtre de recherche avancée
>>>>>>> fa904caa9f4c9cfaa5f9c55f6a5fd4e729e294be
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                viewModel.Search();
            }
        }

        private void Result_Click(object sender, MouseButtonEventArgs e)
        {
            // Ouvrir le document complet
        }
    }
}