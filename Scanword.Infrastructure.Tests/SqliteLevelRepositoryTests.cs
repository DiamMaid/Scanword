using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Scanword.Domain;
using Scanword.Infrastructure.Persistence;

namespace Scanword.Infrastructure.Tests;

public sealed class SqliteLevelRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ScanwordLevel _testLevel;

    public SqliteLevelRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"scanword_levels_{Guid.NewGuid():N}.db");
        _testLevel = TestLevelFactory.CreateMinimalLevel();

        CreateLevelsDatabase();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private void CreateLevelsDatabase()
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _dbPath, Pooling = false }.ToString());
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
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
        cmd.ExecuteNonQuery();

        using var insert = connection.CreateCommand();
        insert.CommandText = @"
            INSERT INTO level(id, version, width, height, difficulty, title, locale,
                             word_count, estimated_minutes, cells, words, clues, clue_cells)
            VALUES(@id, @version, @width, @height, @difficulty, @title, @locale,
                   @word_count, @estimated_minutes, @cells, @words, @clues, @clue_cells)";

        insert.Parameters.AddWithValue("@id", _testLevel.Id);
        insert.Parameters.AddWithValue("@version", _testLevel.Version);
        insert.Parameters.AddWithValue("@width", _testLevel.Width);
        insert.Parameters.AddWithValue("@height", _testLevel.Height);
        insert.Parameters.AddWithValue("@difficulty", (int)_testLevel.Meta.Difficulty);
        insert.Parameters.AddWithValue("@title", _testLevel.Meta.Title);
        insert.Parameters.AddWithValue("@locale", _testLevel.Meta.Locale);
        insert.Parameters.AddWithValue("@word_count", _testLevel.Meta.WordCount);
        insert.Parameters.AddWithValue("@estimated_minutes", _testLevel.Meta.EstimatedMinutes);
        insert.Parameters.AddWithValue("@cells",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelCells(_testLevel)));
        insert.Parameters.AddWithValue("@words",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelWords(_testLevel)));
        insert.Parameters.AddWithValue("@clues",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelClues(_testLevel)));
        insert.Parameters.AddWithValue("@clue_cells",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelClueCells(_testLevel)));

        insert.ExecuteNonQuery();
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_missing_level()
    {
        var repo = new SqliteLevelRepository(_dbPath);
        ScanwordLevel? result = await repo.GetByIdAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_reconstructs_full_level_including_clue_cells()
    {
        var repo = new SqliteLevelRepository(_dbPath);
        ScanwordLevel? result = await repo.GetByIdAsync(_testLevel.Id);

        Assert.NotNull(result);
        Assert.Equal(_testLevel.Id, result!.Id);
        Assert.Equal(_testLevel.Version, result.Version);
        Assert.Equal(_testLevel.Width, result.Width);
        Assert.Equal(_testLevel.Height, result.Height);
        Assert.Equal(_testLevel.Meta.Difficulty, result.Meta.Difficulty);
        Assert.Equal(_testLevel.Meta.Title, result.Meta.Title);
        Assert.Equal(_testLevel.Cells.Length, result.Cells.Length);

        for (int i = 0; i < _testLevel.Cells.Length; i++)
        {
            Assert.Equal(_testLevel.Cells[i].Kind, result.Cells[i].Kind);
            Assert.Equal(_testLevel.Cells[i].AcrossWordId, result.Cells[i].AcrossWordId);
            Assert.Equal(_testLevel.Cells[i].DownWordId, result.Cells[i].DownWordId);
            Assert.Equal(_testLevel.Cells[i].Solution, result.Cells[i].Solution);
        }

        Assert.Equal(_testLevel.Words.Count, result.Words.Count);

        Assert.NotNull(result.ClueCells);
        Assert.NotEmpty(result.ClueCells);
        Assert.Equal(_testLevel.ClueCells.Count, result.ClueCells.Count);

        for (int i = 0; i < _testLevel.ClueCells.Count; i++)
        {
            Assert.Equal(_testLevel.ClueCells[i].CellIndex, result.ClueCells[i].CellIndex);
            Assert.Equal(_testLevel.ClueCells[i].ClueIds, result.ClueCells[i].ClueIds);
        }
    }

    [Fact]
    public async Task reconstructed_level_passes_validation()
    {
        var repo = new SqliteLevelRepository(_dbPath);
        ScanwordLevel? result = await repo.GetByIdAsync(_testLevel.Id);

        Assert.NotNull(result);
        LevelValidationResult validation = LevelValidator.Validate(result!);
        Assert.True(validation.IsValid,
            string.Join("; ", validation.Violations.Select(v => $"[{v.InvariantNumber}] {v.Message}")));
    }

    [Fact]
    public async Task GetSummariesAsync_returns_all_levels_without_blobs()
    {
        var repo = new SqliteLevelRepository(_dbPath);
        var summaries = await repo.GetSummariesAsync();

        Assert.NotEmpty(summaries);
        Assert.Contains(summaries, s => s.Id == _testLevel.Id);
        Assert.Contains(summaries, s => s.Title == _testLevel.Meta.Title);
        Assert.Contains(summaries, s => s.Difficulty == _testLevel.Meta.Difficulty);
    }
}