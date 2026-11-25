using System.Text;
using Microsoft.Data.Sqlite;
using Scanword.Application;
using Scanword.Domain;
using Scanword.Infrastructure.Persistence;

namespace Scanword.Infrastructure.Tests;

public sealed class LevelLoadingServiceTests : IDisposable
{
    private readonly string _levelsDb;
    private readonly string _progressDb;
    private readonly ScanwordLevel _testLevel;

    public LevelLoadingServiceTests()
    {
        _levelsDb = Path.Combine(Path.GetTempPath(), $"scanword_loading_l_{Guid.NewGuid():N}.db");
        _progressDb = Path.Combine(Path.GetTempPath(), $"scanword_loading_p_{Guid.NewGuid():N}.db");
        _testLevel = TestLevelFactory.CreateMinimalLevel();

        CreateLevelsDatabase();
    }

    public void Dispose()
    {
        if (File.Exists(_levelsDb))
            File.Delete(_levelsDb);
        if (File.Exists(_progressDb))
            File.Delete(_progressDb);
    }

    private void CreateLevelsDatabase()
    {
        using var conn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _levelsDb, Pooling = false }.ToString());
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE level(
                id TEXT PRIMARY KEY, version INTEGER NOT NULL,
                width INTEGER NOT NULL, height INTEGER NOT NULL,
                difficulty INTEGER NOT NULL, title TEXT NOT NULL, locale TEXT NOT NULL,
                word_count INTEGER NOT NULL, estimated_minutes INTEGER NOT NULL,
                cells BLOB NOT NULL, words BLOB NOT NULL, clues BLOB NOT NULL, clue_cells BLOB NOT NULL)";
        cmd.ExecuteNonQuery();

        using var insert = conn.CreateCommand();
        insert.CommandText = @"
            INSERT INTO level(id, version, width, height, difficulty, title, locale,
                             word_count, estimated_minutes, cells, words, clues, clue_cells)
            VALUES(@id, @v, @w, @h, @d, @t, @l, @wc, @em, @c, @ws, @cs, @cc)";
        insert.Parameters.AddWithValue("@id", _testLevel.Id);
        insert.Parameters.AddWithValue("@v", _testLevel.Version);
        insert.Parameters.AddWithValue("@w", _testLevel.Width);
        insert.Parameters.AddWithValue("@h", _testLevel.Height);
        insert.Parameters.AddWithValue("@d", (int)_testLevel.Meta.Difficulty);
        insert.Parameters.AddWithValue("@t", _testLevel.Meta.Title);
        insert.Parameters.AddWithValue("@l", _testLevel.Meta.Locale);
        insert.Parameters.AddWithValue("@wc", _testLevel.Meta.WordCount);
        insert.Parameters.AddWithValue("@em", _testLevel.Meta.EstimatedMinutes);
        insert.Parameters.AddWithValue("@c",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelCells(_testLevel)));
        insert.Parameters.AddWithValue("@ws",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelWords(_testLevel)));
        insert.Parameters.AddWithValue("@cs",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelClues(_testLevel)));
        insert.Parameters.AddWithValue("@cc",
            Encoding.UTF8.GetBytes(TestLevelFactory.SerializeLevelClueCells(_testLevel)));
        insert.ExecuteNonQuery();
    }

    [Fact]
    public async Task LoadLevelAsync_starts_fresh_when_no_progress_exists()
    {
        var levelRepo = new SqliteLevelRepository(_levelsDb);
        var progressStore = new SqliteProgressStore(_progressDb);
        var service = new LevelLoadingService(levelRepo, progressStore);

        ScanwordEngine? engine = await service.LoadLevelAsync(_testLevel.Id);

        Assert.NotNull(engine);
        Assert.Equal(LevelStatus.NotStarted, engine.LevelStatus);
        Assert.Equal(0, engine.CoinsSpent);
    }

    [Fact]
    public async Task LoadLevelAsync_restores_progress_when_version_matches()
    {
        var levelRepo = new SqliteLevelRepository(_levelsDb);
        var progressStore = new SqliteProgressStore(_progressDb);

        var engine = new ScanwordEngine(_testLevel);
        engine.SubmitLetter(3, 'B');
        engine.ValidateWord(1);
        LevelProgress snapshot = engine.CreateProgressSnapshot();
        await progressStore.SaveAsync(snapshot);

        var service = new LevelLoadingService(levelRepo, progressStore);
        ScanwordEngine? restored = await service.LoadLevelAsync(_testLevel.Id);

        Assert.NotNull(restored);
        Assert.Equal(LevelStatus.InProgress, restored.LevelStatus);
        Assert.Equal(0, restored.CoinsSpent);
    }

    [Fact]
    public async Task LoadLevelAsync_resets_progress_when_version_mismatches()
    {
        var levelRepo = new SqliteLevelRepository(_levelsDb);
        var progressStore = new SqliteProgressStore(_progressDb);

        var staleProgress = new LevelProgress(
            _testLevel.Id,
            _testLevel.Version + 1,
            LevelStatus.InProgress,
            new[] { new CellProgress(3, 'B', CellState.Filled) },
            new[] { new WordProgress(1, WordStatus.Solved), new WordProgress(2, WordStatus.Unsolved) },
            CoinsSpent: 5);

        await progressStore.SaveAsync(staleProgress);

        var service = new LevelLoadingService(levelRepo, progressStore);
        ScanwordEngine? engine = await service.LoadLevelAsync(_testLevel.Id);

        Assert.NotNull(engine);
        Assert.Equal(LevelStatus.NotStarted, engine.LevelStatus);
        Assert.Equal(0, engine.CoinsSpent);
    }
}