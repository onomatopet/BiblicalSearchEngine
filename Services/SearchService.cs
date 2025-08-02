using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Fr;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using BiblicalSearchEngine.Models;

namespace BiblicalSearchEngine.Services
{
    public class SearchService
    {
        private readonly string indexPath;
        private readonly Analyzer analyzer;
        private IndexWriter writer;
        private SearcherManager searcherManager;

        public SearchService()
        {
            indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Index");

            // Utiliser l'analyseur français pour une meilleure recherche
            analyzer = new FrenchAnalyzer(LuceneVersion.LUCENE_48);

            InitializeIndex();
        }

        private void InitializeIndex()
        {
            var directory = FSDirectory.Open(indexPath);
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };

            writer = new IndexWriter(directory, config);
            searcherManager = new SearcherManager(writer, true, null);
        }

        public void IndexDocument(Document doc)
        {
            var luceneDoc = new Lucene.Net.Documents.Document();

            // Champs indexés et stockés
            luceneDoc.Add(new StringField("id", doc.Id.ToString(), Field.Store.YES));
            luceneDoc.Add(new TextField("title", doc.Title ?? "", Field.Store.YES));
            luceneDoc.Add(new TextField("content", doc.Content ?? "", Field.Store.YES));
            luceneDoc.Add(new StringField("type", doc.Type.ToString(), Field.Store.YES));
            luceneDoc.Add(new StringField("path", doc.FilePath ?? "", Field.Store.YES));

            // Tags
            foreach (var tag in doc.Tags)
            {
                luceneDoc.Add(new TextField("tag", tag, Field.Store.YES));
            }

            writer.UpdateDocument(new Term("id", doc.Id.ToString()), luceneDoc);
            writer.Commit();
            searcherManager.MaybeRefresh();
        }

        public List<SearchResult> Search(string queryText, int maxResults = 50)
        {
            var results = new List<SearchResult>();

            try
            {
                var searcher = searcherManager.Acquire();
                try
                {
                    // Parser pour requêtes complexes
                    var parser = new MultiFieldQueryParser(
                        LuceneVersion.LUCENE_48,
                        new[] { "title", "content", "tag" },
                        analyzer
                    );

                    var query = parser.Parse(queryText);
                    var topDocs = searcher.Search(query, maxResults);

                    foreach (var scoreDoc in topDocs.ScoreDocs)
                    {
                        var doc = searcher.Doc(scoreDoc.Doc);
                        var result = new SearchResult
                        {
                            Title = doc.Get("title"),
                            Reference = doc.Get("type"),
                            Preview = GetPreview(doc.Get("content"), queryText),
                            Score = scoreDoc.Score
                        };

                        results.Add(result);
                    }
                }
                finally
                {
                    searcherManager.Release(searcher);
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Erreur de recherche: {ex.Message}");
            }

            return results;
        }

        private string GetPreview(string content, string query, int previewLength = 200)
        {
            if (string.IsNullOrEmpty(content)) return "";

            // Trouver la première occurrence d'un mot de la requête
            var queryWords = query.Split(' ');
            var lowerContent = content.ToLower();
            int startIndex = -1;

            foreach (var word in queryWords)
            {
                var index = lowerContent.IndexOf(word.ToLower());
                if (index >= 0 && (startIndex < 0 || index < startIndex))
                {
                    startIndex = index;
                }
            }

            if (startIndex < 0) startIndex = 0;

            // Extraire un aperçu centré sur le mot trouvé
            var start = Math.Max(0, startIndex - 50);
            var length = Math.Min(previewLength, content.Length - start);

            var preview = content.Substring(start, length);
            if (start > 0) preview = "..." + preview;
            if (start + length < content.Length) preview += "...";

            return preview;
        }

        public void Dispose()
        {
            writer?.Dispose();
            searcherManager?.Dispose();
        }
    }
}