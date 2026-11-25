using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Scanword.Domain;

namespace Scanword.Infrastructure.Persistence;

public sealed class SqliteProgressStore : IProgressStore, Application.IWalletStore, Application.IPurchaseStore
{
    private readonly string _connectionString;

    public SqliteProgressStore(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS level_progress(
                level_id TEXT PRIMARY KEY,
                level_version INTEGER NOT NULL,
                status INTEGER NOT NULL,
                percent REAL NOT NULL,
                started_at TEXT NULL,
                completed_at TEXT NULL,
                coins_spent INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS cell_progress(
                level_id TEXT NOT NULL,
                cell_index INTEGER NOT NULL,
                letter TEXT NOT NULL,
                state INTEGER NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY(level_id, cell_index)
            ) WITHOUT ROWID;
            CREATE TABLE IF NOT EXISTS word_progress(
                level_id TEXT NOT NULL,
                word_id INTEGER NOT NULL,
                status INTEGER NOT NULL,
                solved_at TEXT NULL,
                PRIMARY KEY(level_id, word_id)
            ) WITHOUT ROWID;
            CREATE TABLE IF NOT EXISTS hint_wallet(
                id INTEGER PRIMARY KEY CHECK(id=1),
                coins INTEGER NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS purchase(
                purchase_token TEXT PRIMARY KEY,
                product_id TEXT NOT NULL,
                order_id TEXT NULL,
                purchased_at TEXT NOT NULL,
                consumed INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS app_setting(
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    public async Task<LevelProgress?> LoadAsync(string levelId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT level_version, status, percent, coins_spent FROM level_progress WHERE level_id = @id";
        cmd.Parameters.AddWithValue("@id", levelId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        int savedVersion = reader.GetInt32(0);
        var status = (LevelStatus)reader.GetInt32(1);
        float percent = reader.GetFloat(2);
        int coinsSpent = reader.GetInt32(3);
        reader.Close();

        var cells = await LoadCellProgressAsync(connection, levelId);
        var words = await LoadWordProgressAsync(connection, levelId);

        return new LevelProgress(levelId, savedVersion, status, cells.ToArray(), words.ToArray(), coinsSpent);
    }

    public async Task SaveAsync(LevelProgress progress)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        using var deleteCells = connection.CreateCommand();
        deleteCells.CommandText = "DELETE FROM cell_progress WHERE level_id = @id";
        deleteCells.Parameters.AddWithValue("@id", progress.LevelId);
        deleteCells.Transaction = transaction;
        await deleteCells.ExecuteNonQueryAsync();

        using var deleteWords = connection.CreateCommand();
        deleteWords.CommandText = "DELETE FROM word_progress WHERE level_id = @id";
        deleteWords.Parameters.AddWithValue("@id", progress.LevelId);
        deleteWords.Transaction = transaction;
        await deleteWords.ExecuteNonQueryAsync();

        string utcNow = DateTime.UtcNow.ToString("O");

        using var upsertLevel = connection.CreateCommand();
        upsertLevel.CommandText = @"
            INSERT INTO level_progress(level_id, level_version, status, percent, started_at, coins_spent)
            VALUES(@id, @version, @status, @percent, @started_at, @coins_spent)
            ON CONFLICT(level_id) DO UPDATE SET
                level_version = @version,
                status = @status,
                percent = @percent,
                started_at = COALESCE(level_progress.started_at, @started_at),
                completed_at = CASE WHEN @status = 2 THEN @completed_at ELSE level_progress.completed_at END,
                coins_spent = @coins_spent";
        upsertLevel.Parameters.AddWithValue("@id", progress.LevelId);
        upsertLevel.Parameters.AddWithValue("@version", progress.LevelVersion);
        upsertLevel.Parameters.AddWithValue("@status", (int)progress.Status);
        upsertLevel.Parameters.AddWithValue("@percent", ComputePercent(progress));
        upsertLevel.Parameters.AddWithValue("@started_at",
            progress.Status == LevelStatus.NotStarted ? DBNull.Value : utcNow);
        upsertLevel.Parameters.AddWithValue("@completed_at",
            progress.Status == LevelStatus.Completed ? utcNow : DBNull.Value);
        upsertLevel.Parameters.AddWithValue("@coins_spent", progress.CoinsSpent);
        upsertLevel.Transaction = transaction;
        await upsertLevel.ExecuteNonQueryAsync();

        if (progress.Cells.Length > 0)
        {
            using var insertCells = connection.CreateCommand();
            insertCells.CommandText =
                "INSERT INTO cell_progress(level_id, cell_index, letter, state, updated_at) VALUES(@lid, @ci, @lt, @st, @ua)";
            var lidParam = insertCells.Parameters.Add("@lid", SqliteType.Text);
            var ciParam = insertCells.Parameters.Add("@ci", SqliteType.Integer);
            var ltParam = insertCells.Parameters.Add("@lt", SqliteType.Text);
            var stParam = insertCells.Parameters.Add("@st", SqliteType.Integer);
            var uaParam = insertCells.Parameters.Add("@ua", SqliteType.Text);
            insertCells.Transaction = transaction;

            foreach (CellProgress cp in progress.Cells)
            {
                lidParam.Value = progress.LevelId;
                ciParam.Value = cp.CellIndex;
                ltParam.Value = cp.Letter.ToString();
                stParam.Value = (int)cp.State;
                uaParam.Value = utcNow;
                await insertCells.ExecuteNonQueryAsync();
            }
        }

        if (progress.Words.Length > 0)
        {
            using var insertWords = connection.CreateCommand();
            insertWords.CommandText =
                "INSERT INTO word_progress(level_id, word_id, status, solved_at) VALUES(@lid, @wid, @st, @sa)";
            var lidParam = insertWords.Parameters.Add("@lid", SqliteType.Text);
            var widParam = insertWords.Parameters.Add("@wid", SqliteType.Integer);
            var stParam = insertWords.Parameters.Add("@st", SqliteType.Integer);
            var saParam = insertWords.Parameters.Add("@sa", SqliteType.Text);
            insertWords.Transaction = transaction;

            foreach (WordProgress wp in progress.Words)
            {
                lidParam.Value = progress.LevelId;
                widParam.Value = wp.WordId;
                stParam.Value = (int)wp.Status;
                saParam.Value = wp.Status == WordStatus.Solved ? utcNow : DBNull.Value;
                await insertWords.ExecuteNonQueryAsync();
            }
        }

        transaction.Commit();
    }

    public async Task<int> GetBalanceAsync(int defaultBalance)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT coins FROM hint_wallet WHERE id = 1";

        object? result = await cmd.ExecuteScalarAsync();
        return result is long coins ? (int)coins : defaultBalance;
    }

    public async Task SaveBalanceAsync(int newBalance)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO hint_wallet(id, coins, updated_at) VALUES(1, @coins, @updated_at)
            ON CONFLICT(id) DO UPDATE SET coins = @coins, updated_at = @updated_at";
        cmd.Parameters.AddWithValue("@coins", newBalance);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> TryRecordPurchaseAsync(string productId, string purchaseToken, string? orderId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(1) FROM purchase WHERE purchase_token = @token";
        checkCmd.Parameters.AddWithValue("@token", purchaseToken);

        long count = (long)(await checkCmd.ExecuteScalarAsync())!;
        if (count > 0)
            return false;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO purchase(purchase_token, product_id, order_id, purchased_at, consumed)
            VALUES(@token, @pid, @oid, @pa, 1)";
        insertCmd.Parameters.AddWithValue("@pid", productId);
        insertCmd.Parameters.AddWithValue("@token", purchaseToken);
        insertCmd.Parameters.AddWithValue("@oid", (object?)orderId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@pa", DateTime.UtcNow.ToString("O"));
        await insertCmd.ExecuteNonQueryAsync();

        return true;
    }

    private async Task<List<CellProgress>> LoadCellProgressAsync(SqliteConnection connection, string levelId)
    {
        var list = new List<CellProgress>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT cell_index, letter, state FROM cell_progress WHERE level_id = @id ORDER BY cell_index";
        cmd.Parameters.AddWithValue("@id", levelId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int cellIndex = reader.GetInt32(0);
            string letterStr = reader.GetString(1);
            var state = (CellState)reader.GetInt32(2);
            char letter = letterStr.Length > 0 ? letterStr[0] : '\0';
            list.Add(new CellProgress(cellIndex, letter, state));
        }

        return list;
    }

    private async Task<List<WordProgress>> LoadWordProgressAsync(SqliteConnection connection, string levelId)
    {
        var list = new List<WordProgress>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT word_id, status FROM word_progress WHERE level_id = @id ORDER BY word_id";
        cmd.Parameters.AddWithValue("@id", levelId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int wordId = reader.GetInt32(0);
            var status = (WordStatus)reader.GetInt32(1);
            list.Add(new WordProgress(wordId, status));
        }

        return list;
    }

    private static float ComputePercent(LevelProgress progress)
    {
        if (progress.Status == LevelStatus.Completed)
            return 1.0f;

        int totalWords = progress.Words.Length;
        if (totalWords == 0)
            return 0f;

        int solvedWords = 0;
        foreach (WordProgress wp in progress.Words)
        {
            if (wp.Status == WordStatus.Solved)
                solvedWords++;
        }

        return (float)solvedWords / totalWords;
    }
}