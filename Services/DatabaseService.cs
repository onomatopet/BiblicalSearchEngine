using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using BiblicalSearchEngine.Models;

namespace BiblicalSearchEngine.Services
{
    public class DatabaseService
    {
        private readonly string dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "BiblicalSearch.db"
        );

        private readonly string connectionString;

        public static void Initialize()
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

        public static int SaveDocument(Document doc)
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

        public static List<Document> GetAllDocuments()
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
    }
}