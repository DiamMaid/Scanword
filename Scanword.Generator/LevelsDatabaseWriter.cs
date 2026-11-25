using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Scanword.Domain;

namespace Scanword.Generator;

public static class LevelsDatabaseWriter
{
    private static readonly JsonSerializerOptions BlobOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions DumpOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Write(string databasePath, string jsonDumpPath, IReadOnlyList<ScanwordLevel> levels)
    {
        if (File.Exists(databasePath))
            File.Delete(databasePath);

        using (var connection = new SqliteConnection(
                   new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString()))
        {
            connection.Open();

            using (var create = connection.CreateCommand())
            {
                create.CommandText = @"
                    CREATE TABLE level(
                        id TEXT PRIMARY KEY,
                        version INTEGER NOT NULL,
                        width INTEGER NOT NULL,
                        height INTEGER NOT NULL,
                        difficulty INTEGER NOT NULL,
                        title TEXT NOT NULL,
                        locale TEXT NOT NULL,
                        word_count INTEGER NOT NULL,
                        estimated_minutes INTEGER NOT NULL,
                        cells BLOB NOT NULL,
                        words BLOB NOT NULL,
                        clues BLOB NOT NULL,
                        clue_cells BLOB NOT NULL
                    )";
                create.ExecuteNonQuery();
            }

            foreach (ScanwordLevel level in levels)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO level(id, version, width, height, difficulty, title, locale,
                                      word_count, estimated_minutes, cells, words, clues, clue_cells)
                    VALUES(@id, @version, @width, @height, @difficulty, @title, @locale,
                           @word_count, @estimated_minutes, @cells, @words, @clues, @clue_cells)";

                insert.Parameters.AddWithValue("@id", level.Id);
                insert.Parameters.AddWithValue("@version", level.Version);
                insert.Parameters.AddWithValue("@width", level.Width);
                insert.Parameters.AddWithValue("@height", level.Height);
                insert.Parameters.AddWithValue("@difficulty", (int)level.Meta.Difficulty);
                insert.Parameters.AddWithValue("@title", level.Meta.Title);
                insert.Parameters.AddWithValue("@locale", level.Meta.Locale);
                insert.Parameters.AddWithValue("@word_count", level.Meta.WordCount);
                insert.Parameters.AddWithValue("@estimated_minutes", level.Meta.EstimatedMinutes);
                insert.Parameters.AddWithValue("@cells", ToBlob(level.Cells));
                insert.Parameters.AddWithValue("@words", ToBlob(level.Words));
                insert.Parameters.AddWithValue("@clues", ToBlob(level.Clues));
                insert.Parameters.AddWithValue("@clue_cells", ToBlob(level.ClueCells));

                insert.ExecuteNonQuery();
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(jsonDumpPath)!);
        File.WriteAllText(jsonDumpPath, JsonSerializer.Serialize(levels, DumpOptions), Encoding.UTF8);
    }

    private static byte[] ToBlob<T>(T value)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, BlobOptions));
    }
}
