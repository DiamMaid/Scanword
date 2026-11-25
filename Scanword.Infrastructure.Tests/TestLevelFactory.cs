using Scanword.Domain;

namespace Scanword.Infrastructure.Tests;

internal static class TestLevelFactory
{
    public static ScanwordLevel CreateMinimalLevel()
    {
        var cells = new Cell[]
        {
            new(CellKind.Clue, 0, 0, '\0'),
            new(CellKind.Clue, 0, 0, '\0'),
            new(CellKind.Letter, 0, 2, 'A'),
            new(CellKind.Letter, 1, 0, 'B'),
            new(CellKind.Empty, 0, 0, '\0'),
            new(CellKind.Letter, 0, 2, 'C'),
            new(CellKind.Empty, 0, 0, '\0'),
            new(CellKind.Empty, 0, 0, '\0'),
            new(CellKind.Empty, 0, 0, '\0')
        };

        var words = new Word[]
        {
            new(1, 1, 0, Direction.Right, 1, "B", 1),
            new(2, 0, 2, Direction.Down, 2, "AC", 2)
        };

        var clues = new ClueReference[]
        {
            new(1, "Single letter", Direction.Down, 1),
            new(2, "Two letters down", Direction.Right, 2)
        };

        var clueCells = new ClueCellBinding[]
        {
            new(0, new[] { 1 }),
            new(1, new[] { 2 })
        };

        var meta = new LevelMetadata(Difficulty.Easy, "Test Level", "en", 2, 1);

        return new ScanwordLevel("test-1", 1, 3, 3, cells, words, clues, clueCells, meta);
    }

    public static string SerializeLevelCells(ScanwordLevel level)
    {
        return System.Text.Json.JsonSerializer.Serialize(level.Cells, JsonOptions);
    }

    public static string SerializeLevelWords(ScanwordLevel level)
    {
        return System.Text.Json.JsonSerializer.Serialize(level.Words, JsonOptions);
    }

    public static string SerializeLevelClues(ScanwordLevel level)
    {
        return System.Text.Json.JsonSerializer.Serialize(level.Clues, JsonOptions);
    }

    public static string SerializeLevelClueCells(ScanwordLevel level)
    {
        return System.Text.Json.JsonSerializer.Serialize(level.ClueCells, JsonOptions);
    }

    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}