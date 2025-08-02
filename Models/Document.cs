using System;
using System.Collections.Generic;

namespace BiblicalSearchEngine.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string FilePath { get; set; }
        public DocumentType Type { get; set; }
        public DateTime DateAdded { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, string> Metadata { get; set; }

        public Document()
        {
            Tags = new List<string>();
            Metadata = new Dictionary<string, string>();
        }
    }

    public enum DocumentType
    {
        Bible,
        Predication,
        Etude,
        Commentaire,
        Notes
    }

    public class BibleVerse
    {
        public string Book { get; set; }
        public int Chapter { get; set; }
        public int Verse { get; set; }
        public string Text { get; set; }
        public string Reference => $"{Book} {Chapter}:{Verse}";
    }

    public class SearchResult
    {
        public Document Document { get; set; }
        public string Title { get; set; }
        public string Reference { get; set; }
        public string Preview { get; set; }
        public float Score { get; set; }
        public Dictionary<string, List<int>> Highlights { get; set; }
    }
}