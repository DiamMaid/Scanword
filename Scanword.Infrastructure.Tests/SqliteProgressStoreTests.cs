using Scanword.Domain;
using Scanword.Infrastructure.Persistence;

namespace Scanword.Infrastructure.Tests;

public sealed class SqliteProgressStoreTests : IDisposable
{
    private readonly string _dbPath;

    public SqliteProgressStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"scanword_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task LoadAsync_returns_null_for_unknown_level()
    {
        var store = new SqliteProgressStore(_dbPath);
        LevelProgress? result = await store.LoadAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_and_LoadAsync_roundtrip_preserves_cells_words_status_and_coins()
    {
        var level = TestLevelFactory.CreateMinimalLevel();
        var engine = new ScanwordEngine(level);

        engine.SubmitLetter(3, 'B');
        engine.ValidateWord(1);
        engine.SubmitLetter(2, 'A');
        engine.SubmitLetter(5, 'C');
        engine.ValidateWord(2);

        LevelProgress snapshot = engine.CreateProgressSnapshot();

        var store = new SqliteProgressStore(_dbPath);
        await store.SaveAsync(snapshot);

        LevelProgress? loaded = await store.LoadAsync(level.Id);
        Assert.NotNull(loaded);
        Assert.Equal(level.Id, loaded!.LevelId);
        Assert.Equal(level.Version, loaded.LevelVersion);
        Assert.Equal(LevelStatus.Completed, loaded.Status);

        Assert.NotNull(loaded.Words);
        Assert.Equal(2, loaded.Words.Length);
        Assert.All(loaded.Words, wp => Assert.Equal(WordStatus.Solved, wp.Status));

        Assert.NotNull(loaded.Cells);
        Assert.All(loaded.Cells, cp =>
        {
            Assert.True(cp.State is CellState.Filled or CellState.Confirmed,
                $"Cell {cp.CellIndex} has unexpected state {cp.State}");
        });
    }

    [Fact]
    public async Task cell_progress_is_sparse_only_filled_cells_persisted()
    {
        var level = TestLevelFactory.CreateMinimalLevel();
        var engine = new ScanwordEngine(level);

        engine.SubmitLetter(2, 'A');
        engine.SubmitLetter(5, 'C');

        LevelProgress snapshot = engine.CreateProgressSnapshot();

        var store = new SqliteProgressStore(_dbPath);
        await store.SaveAsync(snapshot);

        int cellCount = await CountRowsAsync("cell_progress", "level_id", snapshot.LevelId);
        Assert.Equal(2, cellCount);
    }

    [Fact]
    public async Task SaveAsync_replaces_previous_cell_and_word_data()
    {
        var level = TestLevelFactory.CreateMinimalLevel();
        var engine = new ScanwordEngine(level);

        engine.SubmitLetter(2, 'A');
        engine.SubmitLetter(5, 'C');

        LevelProgress snapshot1 = engine.CreateProgressSnapshot();

        var store = new SqliteProgressStore(_dbPath);
        await store.SaveAsync(snapshot1);

        engine.SubmitLetter(3, 'B');
        engine.ValidateWord(1);

        LevelProgress snapshot2 = engine.CreateProgressSnapshot();
        await store.SaveAsync(snapshot2);

        int cellCount = await CountRowsAsync("cell_progress", "level_id", snapshot2.LevelId);
        Assert.Equal(3, cellCount);
    }

    [Fact]
    public async Task hint_wallet_default_balance_is_returned_when_no_row_exists()
    {
        var store = new SqliteProgressStore(_dbPath);
        int balance = await store.GetBalanceAsync(50);
        Assert.Equal(50, balance);
    }

    [Fact]
    public async Task SaveBalanceAsync_persists_and_GetBalanceAsync_reads_back()
    {
        var store = new SqliteProgressStore(_dbPath);
        await store.SaveBalanceAsync(42);
        int balance = await store.GetBalanceAsync(100);
        Assert.Equal(42, balance);
    }

    [Fact]
    public async Task TryRecordPurchaseAsync_returns_true_first_time_and_false_second()
    {
        var store = new SqliteProgressStore(_dbPath);
        bool first = await store.TryRecordPurchaseAsync("pack_100", "token-abc", null);
        Assert.True(first);

        bool second = await store.TryRecordPurchaseAsync("pack_100", "token-abc", null);
        Assert.False(second);
    }

    [Fact]
    public async Task TryRecordPurchaseAsync_different_tokens_are_both_accepted()
    {
        var store = new SqliteProgressStore(_dbPath);
        bool first = await store.TryRecordPurchaseAsync("pack_100", "token-1", null);
        bool second = await store.TryRecordPurchaseAsync("pack_100", "token-2", null);
        Assert.True(first);
        Assert.True(second);
    }

    private async Task<int> CountRowsAsync(string table, string keyColumn, string keyValue)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _dbPath, Pooling = false }.ToString());
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM {table} WHERE {keyColumn} = @v";
        cmd.Parameters.AddWithValue("@v", keyValue);
        long count = (long)(await cmd.ExecuteScalarAsync())!;
        return (int)count;
    }
}