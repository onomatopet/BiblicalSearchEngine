using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BiblicalSearchEngine.Models;

namespace BiblicalSearchEngine.Services
{
    public class BibleReferenceAnalyzer
    {
        private readonly DatabaseService dbService;

        // Patterns pour détecter les références bibliques
        private readonly List<Regex> referencePatterns = new List<Regex>
        {
            // Format: Jean 3:16
            new Regex(@"(\d?\s*\w+)\s+(\d+):(\d+)(?:-(\d+))?", RegexOptions.IgnoreCase),
            // Format: Jean 3.16
            new Regex(@"(\d?\s*\w+)\s+(\d+)\.(\d+)(?:-(\d+))?", RegexOptions.IgnoreCase),
            // Format: Jean chapitre 3 verset 16
            new Regex(@"(\d?\s*\w+)\s+chapitre\s+(\d+)\s+verset\s+(\d+)", RegexOptions.IgnoreCase)
        };

        // Abréviations communes
        private readonly Dictionary<string, string> abbreviations = new Dictionary<string, string>
        {
            {"Gen", "Genèse"}, {"Ex", "Exode"}, {"Lev", "Lévitique"},
            {"Nb", "Nombres"}, {"Dt", "Deutéronome"}, {"Jos", "Josué"},
            {"Jg", "Juges"}, {"Rt", "Ruth"}, {"1S", "1 Samuel"},
            {"2S", "2 Samuel"}, {"1R", "1 Rois"}, {"2R", "2 Rois"},
            {"Mt", "Matthieu"}, {"Mc", "Marc"}, {"Lc", "Luc"},
            {"Jn", "Jean"}, {"Ac", "Actes"}, {"Rm", "Romains"},
            {"1Co", "1 Corinthiens"}, {"2Co", "2 Corinthiens"},
            {"Ap", "Apocalypse"}, {"Apo", "Apocalypse"}
        };

        public BibleReferenceAnalyzer(DatabaseService db)
        {
            dbService = db;
        }

        public AnalysisResult AnalyzeText(string text)
        {
            var result = new AnalysisResult();
            var references = ExtractReferences(text);

            foreach (var reference in references)
            {
                // Chercher le verset dans la base
                var verse = dbService.GetBibleVerse(
                    reference.Book,
                    reference.Chapter,
                    reference.VerseStart
                );

                if (verse != null)
                {
                    reference.VerseText = verse.Text;
                    result.FoundReferences.Add(reference);
                }
                else
                {
                    result.UnresolvedReferences.Add(reference);
                }

                // Chercher les références croisées
                var crossRefs = FindCrossReferences(reference);
                result.CrossReferences[reference] = crossRefs;
            }

            // Analyser les thèmes
            result.Themes = AnalyzeThemes(text);

            return result;
        }

        private List<BibleReference> ExtractReferences(string text)
        {
            var references = new List<BibleReference>();
            var foundPositions = new HashSet<int>();

            foreach (var pattern in referencePatterns)
            {
                var matches = pattern.Matches(text);

                foreach (Match match in matches)
                {
                    // Éviter les doublons
                    if (foundPositions.Contains(match.Index)) continue;
                    foundPositions.Add(match.Index);

                    var book = NormalizeBookName(match.Groups[1].Value);
                    var chapter = int.Parse(match.Groups[2].Value);
                    var verseStart = int.Parse(match.Groups[3].Value);
                    var verseEnd = match.Groups[4].Success ?
                        int.Parse(match.Groups[4].Value) : verseStart;

                    references.Add(new BibleReference
                    {
                        Book = book,
                        Chapter = chapter,
                        VerseStart = verseStart,
                        VerseEnd = verseEnd,
                        OriginalText = match.Value,
                        Position = match.Index
                    });
                }
            }

            return references.OrderBy(r => r.Position).ToList();
        }

        private string NormalizeBookName(string book)
        {
            book = book.Trim();

            // Vérifier les abréviations
            foreach (var abbr in abbreviations)
            {
                if (book.Equals(abbr.Key, StringComparison.OrdinalIgnoreCase))
                    return abbr.Value;
            }

            // Normaliser les variantes communes
            book = book.Replace("1 ", "1").Replace("2 ", "2").Replace("3 ", "3");

            // Recherche approximative dans la liste des livres
            var knownBooks = dbService.GetAllBookNames();
            var bestMatch = knownBooks.FirstOrDefault(b =>
                b.StartsWith(book, StringComparison.OrdinalIgnoreCase) ||
                book.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            );

            return bestMatch ?? book;
        }

        private List<BibleReference> FindCrossReferences(BibleReference reference)
        {
            var crossRefs = new List<BibleReference>();

            // Stratégies pour trouver des références croisées:

            // 1. Références parallèles (évangiles synoptiques)
            if (IsGospel(reference.Book))
            {
                crossRefs.AddRange(FindParallelGospelReferences(reference));
            }

            // 2. Citations de l'AT dans le NT
            if (IsNewTestament(reference.Book))
            {
                crossRefs.AddRange(FindOldTestamentQuotes(reference));
            }

            // 3. Thèmes similaires
            crossRefs.AddRange(FindThematicReferences(reference));

            return crossRefs.Distinct().Take(10).ToList();
        }

        private List<Theme> AnalyzeThemes(string text)
        {
            var themes = new List<Theme>();

            // Dictionnaire de mots-clés thématiques
            var themeKeywords = new Dictionary<string, List<string>>
            {
                {"Foi", new List<string> {"foi", "croire", "confiance", "fidélité"}},
                {"Amour", new List<string> {"amour", "aimer", "charité", "affection"}},
                {"Grâce", new List<string> {"grâce", "miséricorde", "pardon", "clémence"}},
                {"Salut", new List<string> {"salut", "sauver", "rédemption", "délivrance"}},
                {"Prière", new List<string> {"prière", "prier", "intercession", "supplication"}},
                {"Saint-Esprit", new List<string> {"esprit", "saint-esprit", "paraclet", "consolateur"}},
                {"Royaume", new List<string> {"royaume", "règne", "roi", "souverain"}}
            };

            var lowerText = text.ToLower();

            foreach (var theme in themeKeywords)
            {
                var count = 0;
                var positions = new List<int>();

                foreach (var keyword in theme.Value)
                {
                    var regex = new Regex($@"\b{keyword}\b", RegexOptions.IgnoreCase);
                    var matches = regex.Matches(text);
                    count += matches.Count;

                    foreach (Match match in matches)
                    {
                        positions.Add(match.Index);
                    }
                }

                if (count > 0)
                {
                    themes.Add(new Theme
                    {
                        Name = theme.Key,
                        Occurrences = count,
                        Positions = positions,
                        Relevance = CalculateRelevance(count, text.Length)
                    });
                }
            }

            return themes.OrderByDescending(t => t.Relevance).ToList();
        }

        private bool IsGospel(string book)
        {
            return new[] { "Matthieu", "Marc", "Luc", "Jean" }.Contains(book);
        }

        private bool IsNewTestament(string book)
        {
            // Liste simplifiée - à compléter
            return new[] {
                "Matthieu", "Marc", "Luc", "Jean", "Actes",
                "Romains", "1 Corinthiens", "2 Corinthiens"
            }.Contains(book);
        }

        private List<BibleReference> FindParallelGospelReferences(BibleReference reference)
        {
            // Logique pour trouver les passages parallèles dans les évangiles
            // Ceci est une version simplifiée - en réalité, il faudrait une table de correspondances
            var parallels = new List<BibleReference>();

            // Exemple: si c'est dans Matthieu, chercher dans Marc et Luc
            if (reference.Book == "Matthieu")
            {
                // Logique de mapping des passages parallèles
            }

            return parallels;
        }

        private List<BibleReference> FindOldTestamentQuotes(BibleReference reference)
        {
            // Chercher les citations de l'AT dans ce passage du NT
            // Nécessiterait une base de données de citations
            return new List<BibleReference>();
        }

        private List<BibleReference> FindThematicReferences(BibleReference reference)
        {
            // Chercher des versets avec des thèmes similaires
            var verse = dbService.GetBibleVerse(reference.Book, reference.Chapter, reference.VerseStart);
            if (verse == null) return new List<BibleReference>();

            // Extraire les mots-clés du verset
            var keywords = ExtractKeywords(verse.Text);

            // Chercher des versets similaires
            return dbService.SearchSimilarVerses(keywords, 5);
        }

        private List<string> ExtractKeywords(string text)
        {
            // Mots vides à ignorer
            var stopWords = new HashSet<string>
            {
                "le", "la", "les", "de", "du", "des", "un", "une",
                "et", "ou", "mais", "car", "donc", "or", "ni", "que"
            };

            var words = Regex.Split(text.ToLower(), @"\W+")
                .Where(w => w.Length > 3 && !stopWords.Contains(w))
                .Distinct()
                .Take(5)
                .ToList();

            return words;
        }

        private double CalculateRelevance(int occurrences, int textLength)
        {
            // Formule simple de pertinence
            return (double)occurrences / Math.Log(textLength + 1) * 100;
        }
    }

    public class AnalysisResult
    {
        public List<BibleReference> FoundReferences { get; set; }
        public List<BibleReference> UnresolvedReferences { get; set; }
        public Dictionary<BibleReference, List<BibleReference>> CrossReferences { get; set; }
        public List<Theme> Themes { get; set; }

        public AnalysisResult()
        {
            FoundReferences = new List<BibleReference>();
            UnresolvedReferences = new List<BibleReference>();
            CrossReferences = new Dictionary<BibleReference, List<BibleReference>>();
            Themes = new List<Theme>();
        }
    }

    public class BibleReference
    {
        public string Book { get; set; }
        public int Chapter { get; set; }
        public int VerseStart { get; set; }
        public int VerseEnd { get; set; }
        public string OriginalText { get; set; }
        public string VerseText { get; set; }
        public int Position { get; set; }

        public string Display => VerseEnd > VerseStart ?
            $"{Book} {Chapter}:{VerseStart}-{VerseEnd}" :
            $"{Book} {Chapter}:{VerseStart}";
    }

    public class Theme
    {
        public string Name { get; set; }
        public int Occurrences { get; set; }
        public List<int> Positions { get; set; }
        public double Relevance { get; set; }
    }
}
