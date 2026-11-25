using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Scanword.Domain;

namespace Scanword.Infrastructure.Persistence;

public sealed class SqliteLevelRepository : ILevelRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _connectionString;

    public SqliteLevelRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
    }

    public async Task<ScanwordLevel?> GetByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, version, width, height, difficulty, title, locale,
                   word_count, estimated_minutes, cells, words, clues, clue_cells
            FROM level WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        string levelId = reader.GetString(0);
        int version = reader.GetInt32(1);
        int width = reader.GetInt32(2);
        int height = reader.GetInt32(3);
        var difficulty = (Difficulty)reader.GetInt32(4);
        string title = reader.GetString(5);
        string locale = reader.GetString(6);
        int wordCount = reader.GetInt32(7);
        int estimatedMinutes = reader.GetInt32(8);

        byte[] cellsBlob = GetBlobBytes(reader, 9);
        byte[] wordsBlob = GetBlobBytes(reader, 10);
        byte[] cluesBlob = GetBlobBytes(reader, 11);
        byte[] clueCellsBlob = GetBlobBytes(reader, 12);

        Cell[] cells = Deserialize<Cell[]>(cellsBlob);
        Word[] words = Deserialize<Word[]>(wordsBlob);
        ClueReference[] clues = Deserialize<ClueReference[]>(cluesBlob);
        ClueCellBinding[] clueCells = Deserialize<ClueCellBinding[]>(clueCellsBlob);

        var meta = new LevelMetadata(difficulty, title, locale, wordCount, estimatedMinutes);

        return new ScanwordLevel(levelId, version, width, height, cells, words, clues, clueCells, meta);
    }

    public async Task<IReadOnlyList<LevelSummary>> GetSummariesAsync()
    {
        var summaries = new List<LevelSummary>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, difficulty, word_count, estimated_minutes FROM level ORDER BY id";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string id = reader.GetString(0);
            string title = reader.GetString(1);
            var difficulty = (Difficulty)reader.GetInt32(2);
            int wordCount = reader.GetInt32(3);
            int estimatedMinutes = reader.GetInt32(4);
            summaries.Add(new LevelSummary(id, title, difficulty, wordCount, estimatedMinutes));
        }

        return summaries;
    }

    private static T Deserialize<T>(byte[] blob)
    {
        string json = Encoding.UTF8.GetString(blob);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }

    private static byte[] GetBlobBytes(SqliteDataReader reader, int ordinal)
    {
        using var stream = reader.GetStream(ordinal);
        byte[] buffer = new byte[stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }
}