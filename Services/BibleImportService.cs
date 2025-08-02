using Path = System.IO.Path;
using Document = BiblicalSearchEngine.Models.Document;
using BiblicalSearchEngine.Models;
using BiblicalSearchEngine.Services;
using BiblicalSearchEngine.Views;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;

namespace BiblicalSearchEngine.Services
{
    public class BibleImportService
    {
        private readonly DatabaseService dbService;
        private readonly SearchService searchService;

        // Mapping des livres bibliques
        private readonly Dictionary<string, string> bookNames = new Dictionary<string, string>
        {
            {"GEN", "Genèse"}, {"EXO", "Exode"}, {"LEV", "Lévitique"},
            {"NUM", "Nombres"}, {"DEU", "Deutéronome"}, {"JOS", "Josué"},
            {"JDG", "Juges"}, {"RUT", "Ruth"}, {"1SA", "1 Samuel"},
            {"2SA", "2 Samuel"}, {"1KI", "1 Rois"}, {"2KI", "2 Rois"},
            {"1CH", "1 Chroniques"}, {"2CH", "2 Chroniques"}, {"EZR", "Esdras"},
            {"NEH", "Néhémie"}, {"EST", "Esther"}, {"JOB", "Job"},
            {"PSA", "Psaumes"}, {"PRO", "Proverbes"}, {"ECC", "Ecclésiaste"},
            {"SNG", "Cantique"}, {"ISA", "Ésaïe"}, {"JER", "Jérémie"},
            {"LAM", "Lamentations"}, {"EZK", "Ézéchiel"}, {"DAN", "Daniel"},
            {"HOS", "Osée"}, {"JOL", "Joël"}, {"AMO", "Amos"},
            {"OBA", "Abdias"}, {"JON", "Jonas"}, {"MIC", "Michée"},
            {"NAM", "Nahum"}, {"HAB", "Habacuc"}, {"ZEP", "Sophonie"},
            {"HAG", "Aggée"}, {"ZEC", "Zacharie"}, {"MAL", "Malachie"},
            {"MAT", "Matthieu"}, {"MRK", "Marc"}, {"LUK", "Luc"},
            {"JHN", "Jean"}, {"ACT", "Actes"}, {"ROM", "Romains"},
            {"1CO", "1 Corinthiens"}, {"2CO", "2 Corinthiens"}, {"GAL", "Galates"},
            {"EPH", "Éphésiens"}, {"PHP", "Philippiens"}, {"COL", "Colossiens"},
            {"1TH", "1 Thessaloniciens"}, {"2TH", "2 Thessaloniciens"}, {"1TI", "1 Timothée"},
            {"2TI", "2 Timothée"}, {"TIT", "Tite"}, {"PHM", "Philémon"},
            {"HEB", "Hébreux"}, {"JAS", "Jacques"}, {"1PE", "1 Pierre"},
            {"2PE", "2 Pierre"}, {"1JN", "1 Jean"}, {"2JN", "2 Jean"},
            {"3JN", "3 Jean"}, {"JUD", "Jude"}, {"REV", "Apocalypse"}
        };

        public BibleImportService(DatabaseService db, SearchService search)
        {
            dbService = db;
            searchService = search;
        }

        public ImportResult ImportBible(string filePath)
        {
            var result = new ImportResult();

            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                var content = File.ReadAllText(filePath);

                if (extension == ".xml")
                {
                    // Détecter le format XML
                    if (content.Contains("osis"))
                        result = ImportOSIS(filePath);
                    else if (content.Contains("XMLBIBLE") || content.Contains("zefania"))
                        result = ImportZefania(filePath);
                    else
                        result.Errors.Add("Format XML non reconnu");
                }
                else if (extension == ".txt")
                {
                    result = ImportTextBible(filePath);
                }

                // Indexer tous les versets importés
                if (result.Success)
                {
                    IndexBibleVerses(result.Verses);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Erreur: {ex.Message}");
            }

            return result;
        }

        private ImportResult ImportOSIS(string filePath)
        {
            var result = new ImportResult();
            var verses = new List<BibleVerse>();

            try
            {
                var doc = XDocument.Load(filePath);
                var ns = doc.Root.GetDefaultNamespace();

                foreach (var verse in doc.Descendants(ns + "verse"))
                {
                    var osisID = verse.Attribute("osisID")?.Value;
                    if (string.IsNullOrEmpty(osisID)) continue;

                    var parts = osisID.Split('.');
                    if (parts.Length < 3) continue;

                    var bookCode = parts[0];
                    if (!bookNames.ContainsKey(bookCode.ToUpper())) continue;

                    verses.Add(new BibleVerse
                    {
                        Book = bookNames[bookCode.ToUpper()],
                        Chapter = int.Parse(parts[1]),
                        Verse = int.Parse(parts[2]),
                        Text = verse.Value.Trim()
                    });
                }

                result.Verses = verses;
                result.Success = verses.Count > 0;
                result.TotalVerses = verses.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Erreur OSIS: {ex.Message}");
            }

            return result;
        }

        private ImportResult ImportZefania(string filePath)
        {
            var result = new ImportResult();
            var verses = new List<BibleVerse>();

            try
            {
                var doc = XDocument.Load(filePath);

                foreach (var book in doc.Descendants("BIBLEBOOK"))
                {
                    var bookName = book.Attribute("bname")?.Value;
                    var bookNumber = book.Attribute("bnumber")?.Value;

                    if (string.IsNullOrEmpty(bookName) && !string.IsNullOrEmpty(bookNumber))
                    {
                        // Convertir le numéro en nom de livre
                        bookName = GetBookNameByNumber(int.Parse(bookNumber));
                    }

                    foreach (var chapter in book.Descendants("CHAPTER"))
                    {
                        var chapterNum = int.Parse(chapter.Attribute("cnumber").Value);

                        foreach (var verse in chapter.Descendants("VERS"))
                        {
                            var verseNum = int.Parse(verse.Attribute("vnumber").Value);

                            verses.Add(new BibleVerse
                            {
                                Book = bookName,
                                Chapter = chapterNum,
                                Verse = verseNum,
                                Text = verse.Value.Trim()
                            });
                        }
                    }
                }

                result.Verses = verses;
                result.Success = verses.Count > 0;
                result.TotalVerses = verses.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Erreur Zefania: {ex.Message}");
            }

            return result;
        }

        private ImportResult ImportTextBible(string filePath)
        {
            var result = new ImportResult();
            var verses = new List<BibleVerse>();

            // Pattern pour détecter les références (ex: "Jean 3:16")
            var refPattern = @"^([\w\s]+)\s+(\d+):(\d+)\s+(.+)$";
            var regex = new Regex(refPattern);

            try
            {
                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    var match = regex.Match(line.Trim());
                    if (match.Success)
                    {
                        verses.Add(new BibleVerse
                        {
                            Book = match.Groups[1].Value.Trim(),
                            Chapter = int.Parse(match.Groups[2].Value),
                            Verse = int.Parse(match.Groups[3].Value),
                            Text = match.Groups[4].Value.Trim()
                        });
                    }
                }

                result.Verses = verses;
                result.Success = verses.Count > 0;
                result.TotalVerses = verses.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Erreur TXT: {ex.Message}");
            }

            return result;
        }

        private void IndexBibleVerses(List<BibleVerse> verses)
        {
            // Créer un document pour chaque livre
            var bookGroups = verses.GroupBy(v => v.Book);

            foreach (var bookGroup in bookGroups)
            {
                var book = bookGroup.Key;
                var bookVerses = bookGroup.OrderBy(v => v.Chapter).ThenBy(v => v.Verse).ToList();

                // Créer un document par chapitre pour une meilleure granularité
                var chapterGroups = bookVerses.GroupBy(v => v.Chapter);

                foreach (var chapterGroup in chapterGroups)
                {
                    var chapter = chapterGroup.Key;
                    var chapterText = string.Join("\n",
                        chapterGroup.Select(v => $"{v.Verse}. {v.Text}"));

                    var doc = new Document
                    {
                        Title = $"{book} {chapter}",
                        Content = chapterText,
                        Type = DocumentType.Bible,
                        Tags = new List<string> { "Bible", book, $"Chapitre {chapter}" }
                    };

                    var id = DatabaseService.SaveDocument(doc);
                    doc.Id = id;
                    searchService.IndexDocument(doc);
                }
            }

            // Sauvegarder aussi les versets individuels
            dbService.SaveBibleVerses(verses);
        }

        private string GetBookNameByNumber(int number)
        {
            // Mapping standard des numéros de livres
            var numberToName = new Dictionary<int, string>
            {
                {1, "Genèse"}, {2, "Exode"}, {3, "Lévitique"},
                // ... ajouter tous les livres
                {66, "Apocalypse"}
            };

            return numberToName.ContainsKey(number) ? numberToName[number] : $"Livre {number}";
        }
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public int TotalVerses { get; set; }
        public List<BibleVerse> Verses { get; set; }
        public List<string> Errors { get; set; }

        public ImportResult()
        {
            Verses = new List<BibleVerse>();
            Errors = new List<string>();
        }
    }
}
