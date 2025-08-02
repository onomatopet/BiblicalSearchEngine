using BiblicalSearchEngine.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace BiblicalSearchEngine.Services
{
    public class DatabaseService
    {
        private readonly string dbPath;
        private readonly string connectionString;

        public DatabaseService()
        {
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BiblicalSearch.db");
            connectionString = $"Data Source={dbPath};Version=3;";
        }

        public void Initialize()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                // Table Documents
                string createDocTable = @"
                    CREATE TABLE IF NOT EXISTS Documents (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Content TEXT,
                        FilePath TEXT,
                        Type TEXT,
                        DateAdded DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";

                // Table Tags
                string createTagsTable = @"
                    CREATE TABLE IF NOT EXISTS Tags (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DocumentId INTEGER,
                        Tag TEXT,
                        FOREIGN KEY (DocumentId) REFERENCES Documents(Id)
                    )";

                // Table BibleVerses
                string createVersesTable = @"
                    CREATE TABLE IF NOT EXISTS BibleVerses (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Book TEXT,
                        Chapter INTEGER,
                        Verse INTEGER,
                        Text TEXT
                    )";

                using (var cmd = new SQLiteCommand(createDocTable, conn))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SQLiteCommand(createTagsTable, conn))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SQLiteCommand(createVersesTable, conn))
                    cmd.ExecuteNonQuery();
            }
        }

        public int SaveDocument(Document doc)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                string sql = @"
                    INSERT INTO Documents (Title, Content, FilePath, Type)
                    VALUES (@title, @content, @path, @type);
                    SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", doc.Title);
                    cmd.Parameters.AddWithValue("@content", doc.Content);
                    cmd.Parameters.AddWithValue("@path", doc.FilePath);
                    cmd.Parameters.AddWithValue("@type", doc.Type.ToString());

                    doc.Id = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Sauvegarder les tags
                foreach (var tag in doc.Tags)
                {
                    sql = "INSERT INTO Tags (DocumentId, Tag) VALUES (@docId, @tag)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@docId", doc.Id);
                        cmd.Parameters.AddWithValue("@tag", tag);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            return doc.Id;
        }

        public List<Document> GetAllDocuments()
        {
            var documents = new List<Document>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                string sql = "SELECT * FROM Documents";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var doc = new Document
                        {
                            Id = reader.GetInt32(0),
                            Title = reader.GetString(1),
                            Content = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            FilePath = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Type = Enum.Parse<DocumentType>(reader.GetString(4)),
                            DateAdded = reader.GetDateTime(5)
                        };

                        documents.Add(doc);
                    }
                }

                // Charger les tags
                foreach (var doc in documents)
                {
                    sql = "SELECT Tag FROM Tags WHERE DocumentId = @id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", doc.Id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                doc.Tags.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }

            return documents;
        }

        public void SaveBibleVerses(List<BibleVerse> verses)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                using (var transaction = conn.BeginTransaction())
                {
                    foreach (var verse in verses)
                    {
                        string sql = @"
                            INSERT OR REPLACE INTO BibleVerses (Book, Chapter, Verse, Text)
                            VALUES (@book, @chapter, @verse, @text)";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@book", verse.Book);
                            cmd.Parameters.AddWithValue("@chapter", verse.Chapter);
                            cmd.Parameters.AddWithValue("@verse", verse.Verse);
                            cmd.Parameters.AddWithValue("@text", verse.Text);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public BibleVerse GetBibleVerse(string book, int chapter, int verse)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                string sql = @"
                    SELECT * FROM BibleVerses 
                    WHERE Book = @book AND Chapter = @chapter AND Verse = @verse";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@book", book);
                    cmd.Parameters.AddWithValue("@chapter", chapter);
                    cmd.Parameters.AddWithValue("@verse", verse);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new BibleVerse
                            {
                                Book = reader.GetString(1),
                                Chapter = reader.GetInt32(2),
                                Verse = reader.GetInt32(3),
                                Text = reader.GetString(4)
                            };
                        }
                    }
                }
            }

            return null;
        }

        public List<string> GetAllBookNames()
        {
            var books = new List<string>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                string sql = "SELECT DISTINCT Book FROM BibleVerses ORDER BY Book";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        books.Add(reader.GetString(0));
                    }
                }
            }

            return books;
        }

        public List<BibleReference> SearchSimilarVerses(List<string> keywords, int limit)
        {
            var references = new List<BibleReference>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                var whereClause = string.Join(" OR ", keywords.Select(k => "Text LIKE @" + k));
                string sql = $@"
                    SELECT Book, Chapter, Verse, Text FROM BibleVerses 
                    WHERE {whereClause}
                    LIMIT @limit";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    foreach (var keyword in keywords)
                    {
                        cmd.Parameters.AddWithValue("@" + keyword, $"%{keyword}%");
                    }
                    cmd.Parameters.AddWithValue("@limit", limit);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            references.Add(new BibleReference
                            {
                                Book = reader.GetString(0),
                                Chapter = reader.GetInt32(1),
                                VerseStart = reader.GetInt32(2),
                                VerseEnd = reader.GetInt32(2),
                                VerseText = reader.GetString(3)
                            });
                        }
                    }
                }
            }

            return references;
        }
    }
}