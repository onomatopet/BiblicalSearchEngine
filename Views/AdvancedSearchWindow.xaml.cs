using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace BiblicalSearchEngine.Views
{
    public partial class AdvancedSearchWindow : Window
    {
        public string GeneratedQuery { get; private set; }

        public AdvancedSearchWindow()
        {
            InitializeComponent();

            // Événements pour mettre à jour l'aperçu
            AllWordsBox.TextChanged += UpdatePreview;
            ExactPhraseBox.TextChanged += UpdatePreview;
            NoneWordsBox.TextChanged += UpdatePreview;
            TypeCombo.SelectionChanged += UpdatePreview;
            BookCombo.SelectionChanged += UpdatePreview;
            TagsBox.TextChanged += UpdatePreview;
            ProximityCheck.Checked += ProximityCheck_Checked;
            ProximityCheck.Unchecked += ProximityCheck_Unchecked;
            ProximityDistance.TextChanged += UpdatePreview;
        }

        private void ProximityCheck_Checked(object sender, RoutedEventArgs e)
        {
            ProximityGrid.Visibility = Visibility.Visible;
            UpdatePreview(sender, e);
        }

        private void ProximityCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            ProximityGrid.Visibility = Visibility.Collapsed;
            UpdatePreview(sender, e);
        }

        private void UpdatePreview(object sender, EventArgs e)
        {
            var query = BuildQuery();
            QueryPreview.Text = query;
        }

        private string BuildQuery()
        {
            var parts = new List<string>();

            // Tous les mots (AND)
            if (!string.IsNullOrWhiteSpace(AllWordsBox.Text))
            {
                var words = AllWordsBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (ProximityCheck.IsChecked == true && words.Length > 1)
                {
                    var distance = ProximityDistance.Text;
                    parts.Add($"({string.Join($" NEAR/{distance} ", words)})");
                }
                else
                {
                    parts.Add($"({string.Join(" AND ", words)})");
                }
            }

            // Expression exacte
            if (!string.IsNullOrWhiteSpace(ExactPhraseBox.Text))
            {
                parts.Add($"\"{ExactPhraseBox.Text}\"");
            }

            // Mots exclus (NOT)
            if (!string.IsNullOrWhiteSpace(NoneWordsBox.Text))
            {
                var words = NoneWordsBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                parts.Add($"NOT ({string.Join(" OR ", words)})");
            }

            // Filtres
            var filters = new List<string>();

            if (TypeCombo.SelectedIndex > 0)
            {
                var type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                filters.Add($"type:{type}");
            }

            if (BookCombo.SelectedIndex > 0)
            {
                var book = (BookCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                filters.Add($"book:\"{book}\"");
            }

            if (!string.IsNullOrWhiteSpace(TagsBox.Text))
            {
                var tags = TagsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tags)
                {
                    filters.Add($"tag:{tag.Trim()}");
                }
            }

            // Combiner tout
            var mainQuery = string.Join(" AND ", parts);
            if (filters.Count > 0)
            {
                mainQuery = $"({mainQuery}) AND {string.Join(" AND ", filters)}";
            }

            return mainQuery;
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            GeneratedQuery = BuildQuery();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
